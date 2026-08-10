namespace MotoSOS.API.Common.Exceptions;

public sealed class ReportExportNotAvailableAppException : AppException
{
    public ReportExportNotAvailableAppException(string message) : base(message, StatusCodes.Status404NotFound, "report_export_not_available") { }
}
