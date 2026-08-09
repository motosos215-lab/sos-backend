using MongoDB.Bson;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.AuditLogRetention.Contracts;
using MotoSOS.API.Modules.AuditLogRetention.Domain;
using MotoSOS.API.Modules.AuditLogs.Application;
using MotoSOS.API.Modules.AuditLogs.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace MotoSOS.API.Modules.AuditLogRetention.Application;

public sealed class AuditLogRetentionService : IAuditLogRetentionService
{
    private const string EntityType = "AuditLogRetentionRun";
    private readonly IUserRepository _users;
    private readonly IAuditLogRepository _auditLogs;
    private readonly IAuditLogRetentionRunRepository _runs;
    private readonly IAuditLogService? _auditLogService;
    private readonly IClock _clock;

    public AuditLogRetentionService(IUserRepository users, IAuditLogRepository auditLogs, IAuditLogRetentionRunRepository runs, IClock clock, IAuditLogService? auditLogService = null)
    {
        _users = users;
        _auditLogs = auditLogs;
        _runs = runs;
        _clock = clock;
        _auditLogService = auditLogService;
    }

    public async Task<AuditLogRetentionPolicyResponse> GetPolicyAsync(string adminUserId, CancellationToken cancellationToken)
    {
        await EnsureAdminAsync(adminUserId, cancellationToken);
        return AuditLogRetentionPolicy.ToResponse();
    }

    public async Task<AuditLogRetentionRunResponse> RunAsync(string adminUserId, ValidatedAuditLogRetentionRunRequest request, CancellationToken cancellationToken)
    {
        User admin = await EnsureAdminAsync(adminUserId, cancellationToken);
        DateTimeOffset startedAtUtc = _clock.UtcNow;
        DateTimeOffset cutoffUtc = startedAtUtc.AddDays(-request.RetentionDays);
        AuditLogRetentionMode mode = request.DryRun ? AuditLogRetentionMode.DryRun : AuditLogRetentionMode.Delete;

        try
        {
            long candidateCount = await _auditLogs.CountOlderThanAsync(cutoffUtc, cancellationToken);
            long deletedCount = request.DryRun ? 0 : await _auditLogs.DeleteOlderThanAsync(cutoffUtc, cancellationToken);
            DateTimeOffset completedAtUtc = _clock.UtcNow;
            var run = new AuditLogRetentionRun
            {
                Id = ObjectId.GenerateNewId().ToString(),
                RequestedByUserId = admin.Id,
                RequestedByRole = admin.Role,
                Mode = mode,
                Status = AuditLogRetentionRunStatus.Completed,
                RetentionDays = request.RetentionDays,
                CutoffUtc = cutoffUtc,
                CandidateCount = candidateCount,
                DeletedCount = deletedCount,
                StartedAtUtc = startedAtUtc,
                CompletedAtUtc = completedAtUtc,
                CreatedAtUtc = completedAtUtc,
                UpdatedAtUtc = completedAtUtc
            };

            await _runs.AddAsync(run, cancellationToken);
            await RecordAsync(admin, run, mode == AuditLogRetentionMode.DryRun ? AuditAction.AuditLogRetentionDryRunCompleted : AuditAction.AuditLogRetentionDeleteCompleted, AuditOutcome.Success, cancellationToken);
            return ToResponse(run);
        }
        catch (AppException)
        {
            throw;
        }
        catch
        {
            DateTimeOffset completedAtUtc = _clock.UtcNow;
            var run = new AuditLogRetentionRun
            {
                Id = ObjectId.GenerateNewId().ToString(),
                RequestedByUserId = admin.Id,
                RequestedByRole = admin.Role,
                Mode = mode,
                Status = AuditLogRetentionRunStatus.Failed,
                RetentionDays = request.RetentionDays,
                CutoffUtc = cutoffUtc,
                CandidateCount = 0,
                DeletedCount = 0,
                StartedAtUtc = startedAtUtc,
                CompletedAtUtc = completedAtUtc,
                CreatedAtUtc = completedAtUtc,
                UpdatedAtUtc = completedAtUtc,
                ErrorCode = "retention_run_failed",
                ErrorMessage = "Audit log retention run failed."
            };

            try
            {
                await _runs.AddAsync(run, cancellationToken);
                await RecordAsync(admin, run, AuditAction.AuditLogRetentionFailed, AuditOutcome.Failed, cancellationToken);
            }
            catch
            {
            }

            throw new ValidationAppException("Audit log retention run failed.");
        }
    }

    public async Task<GetAuditLogRetentionRunsResponse> ListRunsAsync(string adminUserId, AuditLogRetentionRunQuery query, CancellationToken cancellationToken)
    {
        await EnsureAdminAsync(adminUserId, cancellationToken);
        IReadOnlyList<AuditLogRetentionRun> items = await _runs.ListAsync(query, cancellationToken);
        long total = await _runs.CountAsync(cancellationToken);
        return new GetAuditLogRetentionRunsResponse(items.Select(ToResponse).ToArray(), query.PageNumber, query.PageSize, total);
    }

    public async Task<AuditLogRetentionRunResponse> GetRunAsync(string adminUserId, string id, CancellationToken cancellationToken)
    {
        await EnsureAdminAsync(adminUserId, cancellationToken);
        AuditLogRetentionRun run = await _runs.GetByIdAsync(id.Trim(), cancellationToken) ?? throw new NotFoundAppException("Audit log retention run was not found.");
        return ToResponse(run);
    }

    private async Task<User> EnsureAdminAsync(string userId, CancellationToken cancellationToken)
    {
        User? user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive) throw new UnauthorizedAppException("Invalid authentication credentials.");
        if (user.Role != UserRole.Admin) throw new ForbiddenAppException("Audit Log Retention API is available only for admins.");
        return user;
    }

    private async Task RecordAsync(User admin, AuditLogRetentionRun run, AuditAction action, AuditOutcome outcome, CancellationToken cancellationToken)
    {
        try
        {
            if (_auditLogService is null) return;
            await _auditLogService.RecordAsync(
                admin.Id,
                admin.Role.ToString(),
                action,
                AuditModule.OperationalDashboard,
                EntityType,
                run.Id,
                outcome,
                run.ErrorCode,
                null,
                null,
                new Dictionary<string, string>
                {
                    ["retentionRunId"] = run.Id,
                    ["mode"] = run.Mode.ToString(),
                    ["retentionDays"] = run.RetentionDays.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["cutoffUtc"] = run.CutoffUtc.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                    ["candidateCount"] = run.CandidateCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["deletedCount"] = run.DeletedCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["status"] = run.Status.ToString()
                },
                cancellationToken);
        }
        catch
        {
        }
    }

    private static AuditLogRetentionRunResponse ToResponse(AuditLogRetentionRun run) => new(
        run.Id,
        run.RequestedByUserId,
        run.RequestedByRole.ToString(),
        run.Mode.ToString(),
        run.Status.ToString(),
        run.RetentionDays,
        run.CutoffUtc,
        run.CandidateCount,
        run.DeletedCount,
        run.StartedAtUtc,
        run.CompletedAtUtc,
        run.CreatedAtUtc,
        run.UpdatedAtUtc,
        run.ErrorCode,
        run.ErrorMessage);
}
