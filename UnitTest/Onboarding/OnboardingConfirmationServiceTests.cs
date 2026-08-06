using FluentAssertions;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.Onboarding.Application;
using MotoSOS.API.Modules.Onboarding.Contracts;
using MotoSOS.API.Modules.Onboarding.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace UnitTest.Onboarding;

public sealed class OnboardingConfirmationServiceTests
{
    [Fact]
    public async Task RiderCanConfirmWhenPreviousStepsAreCompleted()
    {
        var user = CreateUser(UserRole.Rider);
        var confirmations = new InMemoryConfirmationRepository();
        var onboarding = new FakeOnboardingService(ReadyStatus());
        var service = CreateService(user, onboarding, confirmations);

        ConfirmOnboardingResponse response = await service.ConfirmAsync(user.Id, CancellationToken.None);

        confirmations.Confirmations.Should().ContainSingle(confirmation => confirmation.UserId == user.Id && confirmation.IsOperational);
        response.Onboarding.CompletedSteps.Should().Be(7);
        response.Onboarding.IsOperational.Should().BeTrue();
    }

    [Theory]
    [InlineData("Profile")]
    [InlineData("Vehicle")]
    [InlineData("EmergencyContacts")]
    [InlineData("Devices")]
    [InlineData("Plan")]
    public async Task MissingPreviousStepReturnsOnboardingNotReady(string missingStep)
    {
        var user = CreateUser(UserRole.Rider);
        var service = CreateService(user, new FakeOnboardingService(NotReadyStatus(missingStep)), new InMemoryConfirmationRepository());

        Func<Task> act = () => service.ConfirmAsync(user.Id, CancellationToken.None);

        await act.Should().ThrowAsync<OnboardingNotReadyAppException>();
    }

    [Fact]
    public async Task ConfirmTwiceIsIdempotentAndKeepsConfirmedAt()
    {
        var user = CreateUser(UserRole.Rider);
        var existing = new OnboardingConfirmation { UserId = user.Id, IsOperational = true, ConfirmedAtUtc = Now.AddDays(-1), CreatedAtUtc = Now.AddDays(-1) };
        var confirmations = new InMemoryConfirmationRepository(existing);
        var service = CreateService(user, new FakeOnboardingService(ReadyStatus()), confirmations);

        await service.ConfirmAsync(user.Id, CancellationToken.None);

        confirmations.Confirmations.Should().ContainSingle();
        existing.ConfirmedAtUtc.Should().Be(Now.AddDays(-1));
        existing.UpdatedAtUtc.Should().Be(Now);
    }

    [Theory]
    [InlineData(UserRole.Monitor)]
    [InlineData(UserRole.Admin)]
    public async Task NonRidersReceiveForbidden(UserRole role)
    {
        var user = CreateUser(role);
        var service = CreateService(user, new FakeOnboardingService(ReadyStatus()), new InMemoryConfirmationRepository());

        Func<Task> act = () => service.ConfirmAsync(user.Id, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAppException>();
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    private static OnboardingConfirmationService CreateService(User user, IOnboardingService onboarding, InMemoryConfirmationRepository confirmations) =>
        new(new InMemoryUserRepository(user), onboarding, confirmations, new TestClock());

    private static User CreateUser(UserRole role) => new() { Email = $"{Guid.NewGuid()}@example.com", FullName = "Moto Rider", Role = role, IsActive = true };

    private static OnboardingStatusResponse ReadyStatus() => new(7, 7, 100, "Completed", true,
    [
        Step("Account", "Completed"), Step("Profile", "Completed"), Step("Vehicle", "Completed"), Step("EmergencyContacts", "Completed"), Step("Devices", "Completed"), Step("Plan", "Completed"), Step("Confirmation", "Completed")
    ]);

    private static OnboardingStatusResponse NotReadyStatus(string missingStep) => new(7, 5, 71, missingStep, false,
    [
        Step("Account", "Completed"), Step("Profile", missingStep == "Profile" ? "Pending" : "Completed"), Step("Vehicle", missingStep == "Vehicle" ? "Pending" : "Completed"), Step("EmergencyContacts", missingStep == "EmergencyContacts" ? "Pending" : "Completed"), Step("Devices", missingStep == "Devices" ? "Pending" : "Completed"), Step("Plan", missingStep == "Plan" ? "Pending" : "Completed"), Step("Confirmation", "Pending")
    ]);

    private static OnboardingStepResponse Step(string key, string status) => new(key, key == "Account" ? 1 : 2, key, status);
    private sealed class TestClock : IClock { public DateTimeOffset UtcNow => Now; }
    private sealed class FakeOnboardingService : IOnboardingService { private readonly OnboardingStatusResponse _status; public FakeOnboardingService(OnboardingStatusResponse status) { _status = status; } public Task<OnboardingStatusResponse> GetStatusAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(_status); }
    private sealed class InMemoryUserRepository : IUserRepository { private readonly User _user; public InMemoryUserRepository(User user) { _user = user; } public Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult<User?>(_user.Id == id ? _user : null); public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken) => Task.FromResult<User?>(null); public Task AddAsync(User user, CancellationToken cancellationToken) => Task.CompletedTask; public Task UpdateAsync(User user, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class InMemoryConfirmationRepository : IOnboardingConfirmationRepository { public List<OnboardingConfirmation> Confirmations { get; } = []; public InMemoryConfirmationRepository(params OnboardingConfirmation[] confirmations) { Confirmations = confirmations.ToList(); } public Task<OnboardingConfirmation?> GetByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(Confirmations.FirstOrDefault(confirmation => confirmation.UserId == userId)); public Task AddAsync(OnboardingConfirmation confirmation, CancellationToken cancellationToken) { Confirmations.Add(confirmation); return Task.CompletedTask; } public Task UpdateAsync(OnboardingConfirmation confirmation, CancellationToken cancellationToken) => Task.CompletedTask; }
}
