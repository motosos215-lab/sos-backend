namespace MotoSOS.API.Modules.Notifications.Providers;

public sealed record FcmPushRequest(string RecipientToken, string Title, string Body, IReadOnlyDictionary<string, string> Data, int TtlSeconds);
