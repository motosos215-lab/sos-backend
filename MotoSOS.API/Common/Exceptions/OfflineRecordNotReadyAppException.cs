namespace MotoSOS.API.Common.Exceptions;

public sealed class OfflineRecordNotReadyAppException : AppException
{
    public OfflineRecordNotReadyAppException(string message) : base(message, StatusCodes.Status400BadRequest, "offline_record_not_ready") { }
}
