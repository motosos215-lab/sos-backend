namespace MotoSOS.API.Modules.AlertDispatch.Application;

public interface IAlertDispatchIdempotencyKeyFactory
{
    string Create(string userId, string incidentId, string clientAlertRequestId);
}
