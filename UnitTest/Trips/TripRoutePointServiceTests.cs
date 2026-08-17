using FluentAssertions;
using Microsoft.Extensions.Options;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.Trips.Application;
using MotoSOS.API.Modules.Trips.Contracts;
using MotoSOS.API.Modules.Trips.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace UnitTest.Trips;

public sealed class TripRoutePointServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 16, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RiderCanSavePointsForOwnActiveTrip()
    {
        Ctx ctx = Ctx.Create();

        CreateTripRoutePointsBatchResponse response = await ctx.Service.CreateBatchAsync(ctx.Rider.Id, ctx.Trip.Id, Batch(Point(sequence: 1)), CancellationToken.None);

        response.Accepted.Should().Be(1);
        ctx.RoutePoints.Items.Should().ContainSingle(point => point.TripId == ctx.Trip.Id && point.UserId == ctx.Rider.Id);
    }

    [Fact]
    public async Task RiderCanSavePointsForOwnFinishedTripWithinGracePeriod()
    {
        Ctx ctx = Ctx.Create(status: TripStatus.Finished, finishedAtUtc: Now.AddHours(-1));

        CreateTripRoutePointsBatchResponse response = await ctx.Service.CreateBatchAsync(ctx.Rider.Id, ctx.Trip.Id, Batch(Point(recordedAtUtc: Now.AddHours(-2))), CancellationToken.None);

        response.Accepted.Should().Be(1);
    }

    [Fact]
    public async Task FinishedTripOutsideGraceRejectsSync()
    {
        Ctx ctx = Ctx.Create(status: TripStatus.Finished, finishedAtUtc: Now.AddHours(-25));

        Func<Task> act = () => ctx.Service.CreateBatchAsync(ctx.Rider.Id, ctx.Trip.Id, Batch(Point(recordedAtUtc: Now.AddHours(-26))), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationAppException>();
    }

    [Fact]
    public async Task RiderCannotSaveOrReadOtherRiderTrip()
    {
        Ctx ctx = Ctx.Create();
        User other = new() { Role = UserRole.Rider, IsActive = true };
        ctx.Users.Items.Add(other);

        await ctx.Service.Invoking(service => service.CreateBatchAsync(other.Id, ctx.Trip.Id, Batch(Point()), CancellationToken.None)).Should().ThrowAsync<NotFoundAppException>();
        await ctx.Service.Invoking(service => service.GetRouteAsync(other.Id, ctx.Trip.Id, null, null, null, null, CancellationToken.None)).Should().ThrowAsync<NotFoundAppException>();
    }

    [Theory]
    [InlineData(UserRole.Monitor)]
    [InlineData(UserRole.Admin)]
    public async Task MonitorAndAdminCannotUseRouteEndpoints(UserRole role)
    {
        Ctx ctx = Ctx.Create(role: role);

        await ctx.Service.Invoking(service => service.CreateBatchAsync(ctx.Rider.Id, ctx.Trip.Id, Batch(Point()), CancellationToken.None)).Should().ThrowAsync<ForbiddenAppException>();
        await ctx.Service.Invoking(service => service.GetRouteAsync(ctx.Rider.Id, ctx.Trip.Id, null, null, null, null, CancellationToken.None)).Should().ThrowAsync<ForbiddenAppException>();
    }

    [Fact]
    public async Task DuplicateWithSameDataDoesNotCreateAnotherPoint()
    {
        Ctx ctx = Ctx.Create();
        CreateTripRoutePointRequest point = Point(clientId: Guid.NewGuid().ToString());

        await ctx.Service.CreateBatchAsync(ctx.Rider.Id, ctx.Trip.Id, Batch(point), CancellationToken.None);
        CreateTripRoutePointsBatchResponse retry = await ctx.Service.CreateBatchAsync(ctx.Rider.Id, ctx.Trip.Id, Batch(point), CancellationToken.None);

        retry.Duplicates.Should().Be(1);
        ctx.RoutePoints.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task DuplicateWithDifferentDataReturnsConflict()
    {
        Ctx ctx = Ctx.Create();
        string clientId = Guid.NewGuid().ToString();
        await ctx.Service.CreateBatchAsync(ctx.Rider.Id, ctx.Trip.Id, Batch(Point(clientId: clientId, sequence: 1)), CancellationToken.None);

        CreateTripRoutePointsBatchResponse retry = await ctx.Service.CreateBatchAsync(ctx.Rider.Id, ctx.Trip.Id, Batch(Point(clientId: clientId, sequence: 2)), CancellationToken.None);

        retry.Conflicts.Should().Be(1);
        ctx.RoutePoints.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task GetRouteReturnsOrderedPointsAndPreviewKeepsFirstAndLast()
    {
        Ctx ctx = Ctx.Create();
        await ctx.Service.CreateBatchAsync(ctx.Rider.Id, ctx.Trip.Id, Batch(Enumerable.Range(1, 10).Select(i => Point(sequence: i)).Reverse().ToArray()), CancellationToken.None);

        GetTripRouteResponse full = await ctx.Service.GetRouteAsync(ctx.Rider.Id, ctx.Trip.Id, "full", null, null, null, CancellationToken.None);
        GetTripRouteResponse preview = await ctx.Service.GetRouteAsync(ctx.Rider.Id, ctx.Trip.Id, "preview", null, null, 4, CancellationToken.None);

        full.Points.Select(point => point.Sequence).Should().BeInAscendingOrder();
        preview.Points.Should().HaveCount(4);
        preview.Points[0].Sequence.Should().Be(1);
        preview.Points[^1].Sequence.Should().Be(10);
    }

    [Fact]
    public async Task PointsRemainAfterTripIsFinished()
    {
        Ctx ctx = Ctx.Create();
        await ctx.Service.CreateBatchAsync(ctx.Rider.Id, ctx.Trip.Id, Batch(Point()), CancellationToken.None);
        ctx.Trip.Status = TripStatus.Finished;
        ctx.Trip.FinishedAtUtc = Now;

        GetTripRouteResponse route = await ctx.Service.GetRouteAsync(ctx.Rider.Id, ctx.Trip.Id, null, null, null, null, CancellationToken.None);

        route.TotalPoints.Should().Be(1);
    }

    private static CreateTripRoutePointsBatchRequest Batch(params CreateTripRoutePointRequest[] points) => new(points);
    private static CreateTripRoutePointRequest Point(string? clientId = null, int sequence = 1, DateTimeOffset? recordedAtUtc = null) => new(clientId ?? Guid.NewGuid().ToString(), sequence, recordedAtUtc ?? Now, 19.4326 + sequence / 10000d, -99.1332, 12.5, 8.4, 180);

    private sealed class Ctx
    {
        public User Rider { get; private set; } = null!;
        public Trip Trip { get; private set; } = null!;
        public Users Users { get; private set; } = null!;
        public RoutePoints RoutePoints { get; private set; } = null!;
        public TripRoutePointService Service { get; private set; } = null!;

        public static Ctx Create(UserRole role = UserRole.Rider, TripStatus status = TripStatus.Active, DateTimeOffset? finishedAtUtc = null)
        {
            var c = new Ctx();
            c.Rider = new User { Role = role, IsActive = true };
            c.Trip = new Trip { UserId = c.Rider.Id, VehicleId = "vehicle", MobileDeviceId = "mobile", Status = status, StartedAtUtc = Now.AddHours(-3), FinishedAtUtc = finishedAtUtc, CreatedAtUtc = Now.AddHours(-3) };
            c.Users = new Users(c.Rider);
            var trips = new Trips(c.Trip);
            c.RoutePoints = new RoutePoints();
            c.Service = new TripRoutePointService(c.Users, trips, c.RoutePoints, Options.Create(new TripRoutePointOptions()), new TripRouteQueryValidator(), new Clock());
            return c;
        }
    }

    private sealed class Clock : IClock { public DateTimeOffset UtcNow => Now; }
    private sealed class Users(params User[] users) : IUserRepository { public List<User> Items { get; } = users.ToList(); public Task<User?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(u => u.Id == id)); public Task<User?> GetByEmailAsync(string email, CancellationToken ct) => Task.FromResult<User?>(null); public Task AddAsync(User user, CancellationToken ct) { Items.Add(user); return Task.CompletedTask; } public Task UpdateAsync(User user, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Trips(params Trip[] trips) : ITripRepository { private readonly List<Trip> _trips = trips.ToList(); public Task<Trip?> GetActiveByUserIdAsync(string userId, CancellationToken ct) => Task.FromResult(_trips.FirstOrDefault(t => t.UserId == userId && t.Status == TripStatus.Active)); public Task<Trip?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(_trips.FirstOrDefault(t => t.Id == id)); public Task<IReadOnlyList<Trip>> ListByUserIdAsync(string userId, TripStatus? status, int pageNumber, int pageSize, CancellationToken ct) => Task.FromResult<IReadOnlyList<Trip>>([]); public Task<long> CountByUserIdAsync(string userId, TripStatus? status, CancellationToken ct) => Task.FromResult(0L); public Task AddAsync(Trip trip, CancellationToken ct) { _trips.Add(trip); return Task.CompletedTask; } public Task UpdateAsync(Trip trip, CancellationToken ct) => Task.CompletedTask; }
    private sealed class RoutePoints : ITripRoutePointRepository { public List<TripRoutePoint> Items { get; } = []; public Task<IReadOnlyList<TripRoutePoint>> GetByTripIdAndClientIdsAsync(string tripId, IReadOnlyCollection<string> ids, CancellationToken ct) => Task.FromResult<IReadOnlyList<TripRoutePoint>>(Items.Where(p => p.TripId == tripId && ids.Contains(p.ClientRoutePointId)).ToArray()); public Task<(TripRoutePoint RoutePoint, bool IsDuplicate)> AddOrGetDuplicateAsync(TripRoutePoint point, CancellationToken ct) { TripRoutePoint? existing = Items.FirstOrDefault(p => p.TripId == point.TripId && p.ClientRoutePointId == point.ClientRoutePointId); if (existing is not null) return Task.FromResult((existing, true)); Items.Add(point); return Task.FromResult((point, false)); } public Task<IReadOnlyList<TripRoutePoint>> ListByUserIdAndTripIdAsync(string userId, string tripId, CancellationToken ct) => Task.FromResult<IReadOnlyList<TripRoutePoint>>(Items.Where(p => p.UserId == userId && p.TripId == tripId).OrderBy(p => p.Sequence).ToArray()); }
}
