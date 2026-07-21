namespace MotoSOS.API.Common.Results;

public record Result(bool IsSuccess, ApiError? Error = null)
{
    public static Result Success() => new(true);

    public static Result Failure(ApiError error) => new(false, error);
}
