namespace MotoSOS.API.Common.Exceptions;

public sealed class LocationSharingNotAllowedAppException : AppException
{
    public LocationSharingNotAllowedAppException(string message) : base(message, StatusCodes.Status400BadRequest, "location_sharing_not_allowed") { }
}
