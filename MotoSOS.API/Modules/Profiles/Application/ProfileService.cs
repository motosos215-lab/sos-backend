using System.Globalization;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.Profiles.Contracts;
using MotoSOS.API.Modules.Profiles.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace MotoSOS.API.Modules.Profiles.Application;

public sealed class ProfileService : IProfileService
{
    private readonly IUserRepository _users;
    private readonly IDriverProfileRepository _profiles;
    private readonly IClock _clock;

    public ProfileService(IUserRepository users, IDriverProfileRepository profiles, IClock clock)
    {
        _users = users;
        _profiles = profiles;
        _clock = clock;
    }

    public async Task<GetMyProfileResponse> GetMyProfileAsync(string userId, CancellationToken cancellationToken)
    {
        User user = await GetRiderUserAsync(userId, cancellationToken);
        DriverProfile? profile = await _profiles.GetByUserIdAsync(user.Id, cancellationToken);

        return new GetMyProfileResponse(ToResponse(user, profile));
    }

    public async Task<UpsertMyProfileResponse> UpsertMyProfileAsync(string userId, UpsertMyProfileRequest request, CancellationToken cancellationToken)
    {
        User user = await GetRiderUserAsync(userId, cancellationToken);
        DriverProfile? profile = await _profiles.GetByUserIdAsync(user.Id, cancellationToken);
        DateTimeOffset now = _clock.UtcNow;
        bool isNew = profile is null;

        profile ??= new DriverProfile
        {
            UserId = user.Id,
            CreatedAtUtc = now
        };

        ApplyProfileChanges(profile, request);
        profile.UpdatedAtUtc = now;

        if (IsContinue(request.SaveMode))
        {
            profile.CompletionStatus = ProfileCompletionStatus.Completed;
            profile.CompletedAtUtc ??= now;
        }
        else
        {
            profile.CompletionStatus = ProfileCompletionStatus.Draft;
        }

        bool userChanged = ApplyUserChanges(user, request, now);

        if (isNew)
        {
            await _profiles.AddAsync(profile, cancellationToken);
        }
        else
        {
            await _profiles.UpdateAsync(profile, cancellationToken);
        }

        if (userChanged)
        {
            await _users.UpdateAsync(user, cancellationToken);
        }

        return new UpsertMyProfileResponse(ToResponse(user, profile));
    }

    private async Task<User> GetRiderUserAsync(string userId, CancellationToken cancellationToken)
    {
        User? user = await _users.GetByIdAsync(userId, cancellationToken);

        if (user is null || !user.IsActive)
        {
            throw new UnauthorizedAppException("Invalid authentication credentials.");
        }

        if (user.Role != UserRole.Rider)
        {
            throw new ForbiddenAppException("This onboarding flow is available only for riders.");
        }

        return user;
    }

    private static void ApplyProfileChanges(DriverProfile profile, UpsertMyProfileRequest request)
    {
        profile.DateOfBirth = request.DateOfBirth?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        profile.CurpOrIdentifier = NormalizeOptional(request.CurpOrIdentifier);
        profile.AddressOrZone = NormalizeOptional(request.AddressOrZone);
        profile.PrimaryCity = NormalizeOptional(request.PrimaryCity);
        profile.BloodType = NormalizeOptional(request.BloodType);
        profile.Allergies = NormalizeOptional(request.Allergies);
        profile.MedicalConditions = NormalizeOptional(request.MedicalConditions);
        profile.ProvisionalEmergencyContactName = NormalizeOptional(request.ProvisionalEmergencyContactName);
        profile.ProvisionalEmergencyContactPhone = NormalizeOptional(request.ProvisionalEmergencyContactPhone);
    }

    private static bool ApplyUserChanges(User user, UpsertMyProfileRequest request, DateTimeOffset now)
    {
        bool changed = false;

        if (!string.IsNullOrWhiteSpace(request.FullName) && user.FullName != request.FullName.Trim())
        {
            user.FullName = request.FullName.Trim();
            changed = true;
        }

        string? phoneNumber = NormalizeOptional(request.PhoneNumber);
        if (phoneNumber != user.PhoneNumber)
        {
            user.PhoneNumber = phoneNumber;
            changed = true;
        }

        if (changed)
        {
            user.UpdatedAtUtc = now;
        }

        return changed;
    }

    private static DriverProfileResponse ToResponse(User user, DriverProfile? profile)
    {
        return new DriverProfileResponse(
            profile?.Id,
            user.Id,
            user.FullName,
            user.Email,
            user.PhoneNumber,
            ParseDateOfBirth(profile?.DateOfBirth),
            profile?.CurpOrIdentifier,
            profile?.AddressOrZone,
            profile?.PrimaryCity,
            profile?.BloodType,
            profile?.Allergies,
            profile?.MedicalConditions,
            profile?.ProvisionalEmergencyContactName,
            profile?.ProvisionalEmergencyContactPhone,
            (profile?.LicenseDocumentStatus ?? LicenseDocumentStatus.NotUploaded).ToString(),
            (profile?.CompletionStatus ?? ProfileCompletionStatus.Draft).ToString(),
            profile?.CreatedAtUtc,
            profile?.UpdatedAtUtc,
            profile?.CompletedAtUtc);
    }

    private static bool IsContinue(string? saveMode) =>
        string.Equals(saveMode?.Trim(), nameof(SaveMode.Continue), StringComparison.OrdinalIgnoreCase);

    private static DateOnly? ParseDateOfBirth(string? dateOfBirth) =>
        DateOnly.TryParseExact(dateOfBirth, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly value)
            ? value
            : null;

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
