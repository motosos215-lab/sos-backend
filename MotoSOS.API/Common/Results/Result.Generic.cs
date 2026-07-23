namespace MotoSOS.API.Common.Results;

public sealed record Result<T>(bool IsSuccess, T? Value = default, ApiError? Error = null)
    : Result(IsSuccess, Error)
{
    public static Result<T> Success(T value) => new(true, value);

    public new static Result<T> Failure(ApiError error) => new(false, default, error);
}
