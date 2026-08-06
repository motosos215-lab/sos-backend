using FluentAssertions;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.Devices.Application;
using MotoSOS.API.Modules.Devices.Contracts;
using MotoSOS.API.Modules.Devices.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace UnitTest.Devices;

public sealed class DeviceServiceTests
{
    [Fact]
    public async Task RiderCanGenerateCodeAndPreviousActiveCodesAreRevoked()
    {
        var user = CreateUser(UserRole.Rider);
        var codes = new InMemoryActivationCodeRepository(new DeviceActivationCode { UserId = user.Id, Code = "MSOS-AAAA-BBBB", ExpiresAtUtc = Now.AddMinutes(10) });
        var service = CreateService(user, codes, new InMemoryUserDeviceRepository());

        CreateMobileActivationCodeResponse response = await service.CreateMobileActivationCodeAsync(user.Id, CancellationToken.None);

        response.ActivationCode!.Code.Should().Be("MSOS-8X7Q-3M2K");
        codes.Codes.First().IsRevoked.Should().BeTrue();
    }

    [Theory]
    [InlineData(UserRole.Monitor)]
    [InlineData(UserRole.Admin)]
    public async Task NonRidersReceiveForbidden(UserRole role)
    {
        var user = CreateUser(role);
        var service = CreateService(user, new InMemoryActivationCodeRepository(), new InMemoryUserDeviceRepository());

        Func<Task> act = () => service.CreateMobileActivationCodeAsync(user.Id, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAppException>();
    }

    [Fact]
    public async Task CurrentCodeReturnsNullWhenNoActiveCodeExists()
    {
        var user = CreateUser(UserRole.Rider);
        var codes = new InMemoryActivationCodeRepository(
            new DeviceActivationCode { UserId = user.Id, Code = "MSOS-USED-1111", ExpiresAtUtc = Now.AddMinutes(10), IsUsed = true },
            new DeviceActivationCode { UserId = user.Id, Code = "MSOS-OLD1-1111", ExpiresAtUtc = Now.AddMinutes(-1) },
            new DeviceActivationCode { UserId = user.Id, Code = "MSOS-REVO-1111", ExpiresAtUtc = Now.AddMinutes(10), IsRevoked = true });
        var service = CreateService(user, codes, new InMemoryUserDeviceRepository());

        GetCurrentMobileActivationCodeResponse response = await service.GetCurrentMobileActivationCodeAsync(user.Id, CancellationToken.None);

        response.ActivationCode.Should().BeNull();
    }

    [Fact]
    public async Task RiderCanLinkMobileWithValidCode()
    {
        var user = CreateUser(UserRole.Rider);
        var code = new DeviceActivationCode { UserId = user.Id, Code = "MSOS-8X7Q-3M2K", ExpiresAtUtc = Now.AddMinutes(10) };
        var codes = new InMemoryActivationCodeRepository(code);
        var devices = new InMemoryUserDeviceRepository();
        var service = CreateService(user, codes, devices);

        LinkMobileDeviceResponse response = await service.LinkMobileDeviceAsync(user.Id, MobileRequest(), CancellationToken.None);

        response.Device.DeviceType.Should().Be("MobileApp");
        response.Device.IsPrimary.Should().BeTrue();
        code.IsUsed.Should().BeTrue();
        devices.Devices.Should().ContainSingle(device => device.DeviceIdentifierHash != "local-device-id-from-mobile");
    }

    [Fact]
    public async Task InvalidActivationCodesFail()
    {
        var user = CreateUser(UserRole.Rider);
        var other = CreateUser(UserRole.Rider);
        var service = CreateService(user, new InMemoryActivationCodeRepository(new DeviceActivationCode { UserId = other.Id, Code = "MSOS-8X7Q-3M2K", ExpiresAtUtc = Now.AddMinutes(10) }), new InMemoryUserDeviceRepository());

        Func<Task> act = () => service.LinkMobileDeviceAsync(user.Id, MobileRequest(), CancellationToken.None);

        await act.Should().ThrowAsync<ActivationCodeInvalidAppException>();
    }

    [Theory]
    [InlineData(true, false, 10)]
    [InlineData(false, true, 10)]
    [InlineData(false, false, -1)]
    public async Task UsedRevokedOrExpiredCodesFail(bool isUsed, bool isRevoked, int expiresInMinutes)
    {
        var user = CreateUser(UserRole.Rider);
        var code = new DeviceActivationCode { UserId = user.Id, Code = "MSOS-8X7Q-3M2K", ExpiresAtUtc = Now.AddMinutes(expiresInMinutes), IsUsed = isUsed, IsRevoked = isRevoked };
        var service = CreateService(user, new InMemoryActivationCodeRepository(code), new InMemoryUserDeviceRepository());

        Func<Task> act = () => service.LinkMobileDeviceAsync(user.Id, MobileRequest(), CancellationToken.None);

        await act.Should().ThrowAsync<ActivationCodeInvalidAppException>();
    }

    [Fact]
    public async Task BasicPlanBlocksSecondActiveMobileApp()
    {
        var user = CreateUser(UserRole.Rider);
        var code = new DeviceActivationCode { UserId = user.Id, Code = "MSOS-8X7Q-3M2K", ExpiresAtUtc = Now.AddMinutes(10) };
        var devices = new InMemoryUserDeviceRepository(new UserDevice { UserId = user.Id, DeviceType = DeviceType.MobileApp, IsActive = true, LinkStatus = DeviceLinkStatus.Linked });
        var service = CreateService(user, new InMemoryActivationCodeRepository(code), devices);

        Func<Task> act = () => service.LinkMobileDeviceAsync(user.Id, MobileRequest(), CancellationToken.None);

        await act.Should().ThrowAsync<PlanLimitExceededAppException>();
    }

    [Fact]
    public async Task RiderCanListOnlyOwnDevicesAndCannotModifyOtherUsersDevice()
    {
        var user = CreateUser(UserRole.Rider);
        var other = CreateUser(UserRole.Rider);
        var own = new UserDevice { UserId = user.Id, DeviceType = DeviceType.MobileApp, DeviceName = "Own", IsActive = true, LinkStatus = DeviceLinkStatus.Linked };
        var otherDevice = new UserDevice { UserId = other.Id, DeviceType = DeviceType.MobileApp, DeviceName = "Other", IsActive = true, LinkStatus = DeviceLinkStatus.Linked };
        var service = CreateService(user, new InMemoryActivationCodeRepository(), new InMemoryUserDeviceRepository(own, otherDevice));

        GetDevicesResponse list = await service.GetMyDevicesAsync(user.Id, CancellationToken.None);
        Func<Task> heartbeatOther = () => service.HeartbeatAsync(user.Id, otherDevice.Id, new HeartbeatDeviceRequest(80, "Online", null), CancellationToken.None);

        list.Devices.Should().ContainSingle(device => device.Id == own.Id);
        await heartbeatOther.Should().ThrowAsync<NotFoundAppException>();
    }

    [Fact]
    public async Task SmartwatchRequiresOwnedActiveLinkedMobileParent()
    {
        var user = CreateUser(UserRole.Rider);
        var mobile = new UserDevice { UserId = user.Id, DeviceType = DeviceType.MobileApp, DeviceName = "Mobile", IsActive = true, LinkStatus = DeviceLinkStatus.Linked };
        var devices = new InMemoryUserDeviceRepository(mobile);
        var service = CreateService(user, new InMemoryActivationCodeRepository(), devices);

        LinkSmartwatchResponse response = await service.LinkSmartwatchAsync(user.Id, SmartwatchRequest(mobile.Id), CancellationToken.None);
        Func<Task> missingParent = () => service.LinkSmartwatchAsync(user.Id, SmartwatchRequest("missing"), CancellationToken.None);

        response.Device.DeviceType.Should().Be("Smartwatch");
        response.Device.ParentDeviceId.Should().Be(mobile.Id);
        await missingParent.Should().ThrowAsync<NotFoundAppException>();
    }

    [Fact]
    public async Task HeartbeatUpdatesStateAndRevokeMobileRevokesDependents()
    {
        var user = CreateUser(UserRole.Rider);
        var mobile = new UserDevice { UserId = user.Id, DeviceType = DeviceType.MobileApp, DeviceName = "Mobile", IsActive = true, LinkStatus = DeviceLinkStatus.Linked };
        var watch = new UserDevice { UserId = user.Id, DeviceType = DeviceType.Smartwatch, DeviceName = "Watch", ParentDeviceId = mobile.Id, IsActive = true, LinkStatus = DeviceLinkStatus.Linked };
        var service = CreateService(user, new InMemoryActivationCodeRepository(), new InMemoryUserDeviceRepository(mobile, watch));

        HeartbeatDeviceResponse heartbeat = await service.HeartbeatAsync(user.Id, mobile.Id, new HeartbeatDeviceRequest(87, "Online", "1.0.0"), CancellationToken.None);
        await service.RevokeAsync(user.Id, mobile.Id, CancellationToken.None);

        heartbeat.Device.BatteryLevel.Should().Be(87);
        mobile.IsActive.Should().BeFalse();
        watch.IsActive.Should().BeFalse();
        watch.LinkStatus.Should().Be(DeviceLinkStatus.Revoked);
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    private static DeviceService CreateService(User user, InMemoryActivationCodeRepository codes, InMemoryUserDeviceRepository devices) =>
        new(new InMemoryUserRepository(user), codes, devices, new StaticCodeGenerator(), new DeviceIdentifierHasher(), new TestClock());

    private static User CreateUser(UserRole role) => new() { Email = $"{Guid.NewGuid()}@example.com", FullName = "Moto Rider", Role = role, IsActive = true };
    private static LinkMobileDeviceRequest MobileRequest() => new("MSOS-8X7Q-3M2K", "Motorola Edge", "Android", "Motorola", "Edge 40", "14", "1.0.0", "local-device-id-from-mobile");
    private static LinkSmartwatchRequest SmartwatchRequest(string parentId) => new(parentId, "Galaxy Watch", "WearOS", "Samsung", "Galaxy Watch 6", "Wear OS 4", "1.0.0", "local-watch-id", 80);

    private sealed class TestClock : IClock { public DateTimeOffset UtcNow => Now; }
    private sealed class StaticCodeGenerator : IActivationCodeGenerator { public string CreateCode() => "MSOS-8X7Q-3M2K"; }
    private sealed class InMemoryUserRepository : IUserRepository
    {
        private readonly User _user;
        public InMemoryUserRepository(User user) { _user = user; }
        public Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult<User?>(_user.Id == id ? _user : null);
        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken) => Task.FromResult<User?>(null);
        public Task AddAsync(User user, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task UpdateAsync(User user, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class InMemoryActivationCodeRepository : IDeviceActivationCodeRepository
    {
        public List<DeviceActivationCode> Codes { get; }
        public InMemoryActivationCodeRepository(params DeviceActivationCode[] codes) { Codes = codes.ToList(); }
        public Task<IReadOnlyList<DeviceActivationCode>> GetActiveByUserIdAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<DeviceActivationCode>>(Codes.Where(code => code.UserId == userId && !code.IsUsed && !code.IsRevoked && code.ExpiresAtUtc > now).ToArray());
        public Task<DeviceActivationCode?> GetActiveCurrentByUserIdAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken) => Task.FromResult(Codes.LastOrDefault(code => code.UserId == userId && !code.IsUsed && !code.IsRevoked && code.ExpiresAtUtc > now));
        public Task<DeviceActivationCode?> GetByCodeAsync(string code, CancellationToken cancellationToken) => Task.FromResult(Codes.FirstOrDefault(activationCode => activationCode.Code == code));
        public Task AddAsync(DeviceActivationCode code, CancellationToken cancellationToken) { Codes.Add(code); return Task.CompletedTask; }
        public Task UpdateAsync(DeviceActivationCode code, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class InMemoryUserDeviceRepository : IUserDeviceRepository
    {
        public List<UserDevice> Devices { get; }
        public InMemoryUserDeviceRepository(params UserDevice[] devices) { Devices = devices.ToList(); }
        public Task<IReadOnlyList<UserDevice>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<UserDevice>>(Devices.Where(device => device.UserId == userId && device.IsActive).ToArray());
        public Task<IReadOnlyList<UserDevice>> GetActiveByParentDeviceIdAsync(string parentDeviceId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<UserDevice>>(Devices.Where(device => device.ParentDeviceId == parentDeviceId && device.IsActive).ToArray());
        public Task<UserDevice?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult(Devices.FirstOrDefault(device => device.Id == id));
        public Task<UserDevice?> GetByDeviceIdentifierHashAsync(string userId, string hash, DeviceType deviceType, CancellationToken cancellationToken) => Task.FromResult(Devices.FirstOrDefault(device => device.UserId == userId && device.DeviceIdentifierHash == hash && device.DeviceType == deviceType));
        public Task<int> CountActiveLinkedByUserIdAndTypeAsync(string userId, DeviceType deviceType, CancellationToken cancellationToken) => Task.FromResult(Devices.Count(device => device.UserId == userId && device.DeviceType == deviceType && device.IsActive && device.LinkStatus == DeviceLinkStatus.Linked));
        public Task<bool> HasActiveLinkedMobileAppAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(Devices.Any(device => device.UserId == userId && device.DeviceType == DeviceType.MobileApp && device.IsActive && device.LinkStatus == DeviceLinkStatus.Linked));
        public Task AddAsync(UserDevice device, CancellationToken cancellationToken) { Devices.Add(device); return Task.CompletedTask; }
        public Task UpdateAsync(UserDevice device, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
