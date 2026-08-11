using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Modules.Auth.AdminBootstrap;
using MotoSOS.API.Modules.Auth.Application;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;
using MotoSOS.API.Security.Hashing;

namespace UnitTest.Auth;

public sealed class AdminBootstrapInitializerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RunAsyncDoesNothingWhenDisabled()
    {
        var users = new Users();
        var initializer = Create(users, new AdminBootstrapOptions { Enabled = false });

        await initializer.RunAsync(CancellationToken.None);

        users.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task RunAsyncCreatesAdminWithHashedPassword()
    {
        var users = new Users();
        var initializer = Create(users, ValidOptions());

        await initializer.RunAsync(CancellationToken.None);

        users.Items.Should().ContainSingle();
        User admin = users.Items.Single();
        admin.Email.Should().Be("admin@example.com");
        admin.FullName.Should().Be("System Admin");
        admin.Role.Should().Be(UserRole.Admin);
        admin.IsActive.Should().BeTrue();
        admin.PasswordHash.Should().Be("hashed:StrongPass1!");
        admin.CreatedAtUtc.Should().Be(Now);
        admin.AcceptedTermsAtUtc.Should().Be(Now);
    }

    [Fact]
    public async Task RunAsyncSkipsWhenAdminAlreadyExists()
    {
        var users = new Users(new User { Email = "other-admin@example.com", Role = UserRole.Admin });
        var initializer = Create(users, ValidOptions());

        await initializer.RunAsync(CancellationToken.None);

        users.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task RunAsyncSkipsExistingEmailWithoutChangingRoleOrPassword()
    {
        var existing = new User { Email = "admin@example.com", PasswordHash = "existing-hash", Role = UserRole.Rider };
        var users = new Users(existing);
        AdminBootstrapOptions options = ValidOptions();
        options.RunOnlyWhenNoAdminsExist = false;
        var initializer = Create(users, options);

        await initializer.RunAsync(CancellationToken.None);

        users.Items.Should().ContainSingle().Which.Should().BeSameAs(existing);
        existing.Role.Should().Be(UserRole.Rider);
        existing.PasswordHash.Should().Be("existing-hash");
    }

    [Fact]
    public async Task RunAsyncDoesNotCreateAdminForInvalidConfig()
    {
        var users = new Users();
        AdminBootstrapOptions options = ValidOptions();
        options.Password = "weak";
        var initializer = Create(users, options);

        await initializer.RunAsync(CancellationToken.None);

        users.Items.Should().BeEmpty();
    }

    private static AdminBootstrapOptions ValidOptions() => new()
    {
        Enabled = true,
        Email = " ADMIN@example.com ",
        Password = "StrongPass1!",
        FullName = " System Admin ",
        RunOnlyWhenNoAdminsExist = true
    };

    private static AdminBootstrapInitializer Create(Users users, AdminBootstrapOptions options) => new(
        Options.Create(options),
        users,
        new Hasher(),
        new Clock(),
        new RegisterRequestValidator(),
        NullLogger<AdminBootstrapInitializer>.Instance);

    private sealed class Clock : IClock { public DateTimeOffset UtcNow => Now; }

    private sealed class Hasher : IPasswordHasher
    {
        public string Hash(string password) => $"hashed:{password}";

        public bool Verify(string password, string passwordHash) => passwordHash == Hash(password);
    }

    private sealed class Users(params User[] users) : IUserRepository
    {
        public List<User> Items { get; } = users.ToList();

        public Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult(Items.FirstOrDefault(user => user.Id == id));

        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken) => Task.FromResult(Items.FirstOrDefault(user => string.Equals(user.Email, email.Trim(), StringComparison.OrdinalIgnoreCase)));

        public Task<long> CountByRoleAsync(UserRole role, CancellationToken cancellationToken) => Task.FromResult((long)Items.Count(user => user.Role == role));

        public Task AddAsync(User user, CancellationToken cancellationToken)
        {
            Items.Add(user);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(User user, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
