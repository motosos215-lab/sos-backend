using MotoSOS.API.Common.Errors;

namespace MotoSOS.API.Common.Results;

public sealed record ApiResult(bool IsSuccess, Error? Error = null)
{
    public static ApiResult Success() => new(true);

    public static ApiResult Failure(Error error) => new(false, error);
}
