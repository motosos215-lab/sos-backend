namespace MotoSOS.API.Common.Exceptions;

public sealed class ReportExportNotAllowedAppException : AppException
{
    public ReportExportNotAllowedAppException(string message) : base(message, StatusCodes.Status400BadRequest, "report_export_not_allowed") { }
}
