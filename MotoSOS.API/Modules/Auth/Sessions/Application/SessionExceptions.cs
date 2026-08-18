using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.Auth.Sessions.Contracts;

namespace MotoSOS.API.Modules.Auth.Sessions.Application;

public sealed class ActiveSessionExistsAppException : AppException
{
    public ActiveSessionExistsAppException(ActiveSessionConflictResponse data)
        : base("An active session already exists for this user.", StatusCodes.Status409Conflict, "active_session_exists") => DataPayload = data;

    public ActiveSessionConflictResponse DataPayload { get; }
}

public sealed class ActiveTripTransferRequiredAppException : AppException
{
    public ActiveTripTransferRequiredAppException(ActiveTripSessionResponse data)
        : base("An active trip must be transferred before replacing this session.", StatusCodes.Status409Conflict, "active_trip_transfer_required") => DataPayload = data;

    public ActiveTripSessionResponse DataPayload { get; }
}

public sealed class SessionRevokedAppException : AppException
{
    public SessionRevokedAppException() : base("Session has been revoked.", StatusCodes.Status401Unauthorized, "session_revoked") { }
}

public sealed class TakeoverTokenInvalidAppException : AppException
{
    public TakeoverTokenInvalidAppException(string code = "takeover_token_invalid") : base("Takeover token is invalid.", StatusCodes.Status401Unauthorized, code) { }
}
