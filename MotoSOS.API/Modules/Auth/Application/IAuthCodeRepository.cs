using MotoSOS.API.Modules.Auth.Domain;

namespace MotoSOS.API.Modules.Auth.Application;

public interface IAuthCodeRepository
{
    Task<AuthCode?> GetLatestByEmailAndPurposeAsync(string emailNormalized, AuthCodePurpose purpose, CancellationToken cancellationToken);

    Task AddAsync(AuthCode authCode, CancellationToken cancellationToken);

    Task UpdateAsync(AuthCode authCode, CancellationToken cancellationToken);

    Task RevokeActiveAsync(string emailNormalized, AuthCodePurpose purpose, DateTimeOffset revokedAtUtc, CancellationToken cancellationToken);
}
