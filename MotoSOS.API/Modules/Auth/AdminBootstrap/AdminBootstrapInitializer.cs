using FluentValidation;
using Microsoft.Extensions.Options;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Modules.Auth.Contracts;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;
using MotoSOS.API.Security.Hashing;

namespace MotoSOS.API.Modules.Auth.AdminBootstrap;

public sealed class AdminBootstrapInitializer : IAdminBootstrapInitializer
{
    private readonly AdminBootstrapOptions _options;
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IClock _clock;
    private readonly IValidator<RegisterRequest> _registerValidator;
    private readonly ILogger<AdminBootstrapInitializer> _logger;

    public AdminBootstrapInitializer(IOptions<AdminBootstrapOptions> options, IUserRepository users, IPasswordHasher passwordHasher, IClock clock, IValidator<RegisterRequest> registerValidator, ILogger<AdminBootstrapInitializer> logger)
    {
        _options = options.Value;
        _users = users;
        _passwordHasher = passwordHasher;
        _clock = clock;
        _registerValidator = registerValidator;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Admin bootstrap disabled.");
            return;
        }

        string email = _options.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        string password = _options.Password ?? string.Empty;
        string fullName = _options.FullName?.Trim() ?? string.Empty;
        var validationRequest = new RegisterRequest(email, password, password, fullName, null, "Rider", true);
        var validation = await _registerValidator.ValidateAsync(validationRequest, cancellationToken);
        if (!validation.IsValid)
        {
            _logger.LogWarning("Admin bootstrap failed due to invalid config.");
            return;
        }

        if (_options.RunOnlyWhenNoAdminsExist && await _users.CountByRoleAsync(UserRole.Admin, cancellationToken) > 0)
        {
            _logger.LogInformation("Admin bootstrap skipped because admin exists.");
            return;
        }

        User? existing = await _users.GetByEmailAsync(email, cancellationToken);
        if (existing is not null)
        {
            if (existing.Role == UserRole.Admin)
            {
                _logger.LogInformation("Admin bootstrap skipped because admin exists.");
            }
            else
            {
                _logger.LogWarning("Admin bootstrap skipped because configured user already exists.");
            }

            return;
        }

        DateTimeOffset now = _clock.UtcNow;
        var admin = new User
        {
            Email = email,
            PasswordHash = _passwordHasher.Hash(password),
            FullName = fullName,
            Role = UserRole.Admin,
            IsActive = true,
            CreatedAtUtc = now,
            AcceptedTermsAtUtc = now
        };

        await _users.AddAsync(admin, cancellationToken);
        _logger.LogInformation("Admin bootstrap created admin user.");
    }
}
