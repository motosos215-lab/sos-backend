namespace MotoSOS.API.Modules.ReportExports.Application;

public interface IResolutionReportExportIdempotencyKeyFactory
{
    string Create(string userId, string emergencyResolutionReportId, string exportType);
}
