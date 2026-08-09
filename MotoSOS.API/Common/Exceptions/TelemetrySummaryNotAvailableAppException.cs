namespace MotoSOS.API.Common.Exceptions;

public sealed class TelemetrySummaryNotAvailableAppException : AppException
{
    public TelemetrySummaryNotAvailableAppException(string message) : base(message, StatusCodes.Status404NotFound, "telemetry_summary_not_available") { }
}
