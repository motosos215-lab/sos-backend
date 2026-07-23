namespace MotoSOS.API.Common.Results;

public sealed record ApiResponse<T>(bool Success, T? Data = default, ApiError? Error = null)
{
    public static ApiResponse<T> Ok(T data) => new(true, data);

    public static ApiResponse<T> Fail(ApiError error) => new(false, default, error);
}
