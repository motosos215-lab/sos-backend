namespace MotoSOS.API.Common.Exceptions;

public class AppException : Exception
{
    public AppException(string message, int statusCode, string code)
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
    }

    public int StatusCode { get; }

    public string Code { get; }
}
