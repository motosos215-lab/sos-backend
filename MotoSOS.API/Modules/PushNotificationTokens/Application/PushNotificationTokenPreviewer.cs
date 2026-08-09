namespace MotoSOS.API.Modules.PushNotificationTokens.Application;

public sealed class PushNotificationTokenPreviewer : IPushNotificationTokenPreviewer
{
    public string CreatePreview(string token)
    {
        string value = token.Trim();
        if (value.Length < 12) return "****";
        return string.Concat(value.AsSpan(0, Math.Min(6, value.Length)), "****", value.AsSpan(value.Length - Math.Min(4, value.Length)));
    }
}
