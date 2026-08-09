namespace MotoSOS.API.Common.Exceptions;

public sealed class OfflineProcessingFailedAppException : AppException
{
    public OfflineProcessingFailedAppException(string message) : base(message, StatusCodes.Status400BadRequest, "offline_processing_failed") { }
}
