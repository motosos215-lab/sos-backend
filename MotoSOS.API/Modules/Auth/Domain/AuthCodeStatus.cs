namespace MotoSOS.API.Modules.Auth.Domain;

public enum AuthCodeStatus
{
    Active = 1,
    Used = 2,
    Expired = 3,
    Revoked = 4,
    Failed = 5
}
