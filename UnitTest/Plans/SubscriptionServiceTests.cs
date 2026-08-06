using FluentAssertions;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.Plans.Application;
using MotoSOS.API.Modules.Plans.Contracts;
using MotoSOS.API.Modules.Plans.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace UnitTest.Plans;

public sealed class SubscriptionServiceTests
{
    [Fact]
    public async Task RiderGetsNullSubscriptionAndDefaultBasicPlanWhenMissing()
    {
        var user = CreateUser(UserRole.Rider);
        var service = CreateService(user, new InMemorySubscriptionRepository());

        GetMySubscriptionResponse response = await service.GetMySubscriptionAsync(user.Id, CancellationToken.None);

        response.Subscription.Should().BeNull();
        response.DefaultPlan!.Tier.Should().Be("Basic");
        response.DefaultPlan.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task RiderCanSelectBasic()
    {
        var user = CreateUser(UserRole.Rider);
        var subscriptions = new InMemorySubscriptionRepository();
        var service = CreateService(user, subscriptions);

        SelectBasicSubscriptionResponse response = await service.SelectBasicAsync(user.Id, CancellationToken.None);

        response.Subscription.PlanTier.Should().Be("Basic");
        response.Subscription.Status.Should().Be("Active");
        response.Subscription.Source.Should().Be("WebBasic");
        subscriptions.Subscriptions.Should().ContainSingle();
    }

    [Fact]
    public async Task SelectBasicTwiceIsIdempotent()
    {
        var user = CreateUser(UserRole.Rider);
        var subscriptions = new InMemorySubscriptionRepository();
        var service = CreateService(user, subscriptions);

        SelectBasicSubscriptionResponse first = await service.SelectBasicAsync(user.Id, CancellationToken.None);
        SelectBasicSubscriptionResponse second = await service.SelectBasicAsync(user.Id, CancellationToken.None);

        second.Subscription.Id.Should().Be(first.Subscription.Id);
        subscriptions.Subscriptions.Should().ContainSingle();
    }

    [Theory]
    [InlineData(UserRole.Monitor)]
    [InlineData(UserRole.Admin)]
    public async Task NonRidersReceiveForbidden(UserRole role)
    {
        var user = CreateUser(role);
        var service = CreateService(user, new InMemorySubscriptionRepository());

        Func<Task> act = () => service.SelectBasicAsync(user.Id, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAppException>();
    }

    [Fact]
    public async Task DoesNotOperateWithOtherUserId()
    {
        var user = CreateUser(UserRole.Rider);
        var service = CreateService(user, new InMemorySubscriptionRepository());

        Func<Task> act = () => service.GetMySubscriptionAsync("other-user", CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAppException>();
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    private static SubscriptionService CreateService(User user, InMemorySubscriptionRepository subscriptions) =>
        new(new InMemoryUserRepository(user), subscriptions, new PlanCatalogService(), new TestClock());

    private static User CreateUser(UserRole role) => new() { Email = $"{Guid.NewGuid()}@example.com", FullName = "Moto Rider", Role = role, IsActive = true };

    private sealed class TestClock : IClock { public DateTimeOffset UtcNow => Now; }
    private sealed class InMemoryUserRepository : IUserRepository
    {
        private readonly User _user;
        public InMemoryUserRepository(User user) { _user = user; }
        public Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult<User?>(_user.Id == id ? _user : null);
        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken) => Task.FromResult<User?>(null);
        public Task AddAsync(User user, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task UpdateAsync(User user, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class InMemorySubscriptionRepository : IUserSubscriptionRepository
    {
        public List<UserSubscription> Subscriptions { get; } = [];
        public Task<UserSubscription?> GetByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(Subscriptions.FirstOrDefault(subscription => subscription.UserId == userId));
        public Task<bool> HasActiveSubscriptionAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(Subscriptions.Any(subscription => subscription.UserId == userId && subscription.Status == SubscriptionStatus.Active));
        public Task AddAsync(UserSubscription subscription, CancellationToken cancellationToken) { Subscriptions.Add(subscription); return Task.CompletedTask; }
        public Task UpdateAsync(UserSubscription subscription, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
