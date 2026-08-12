using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace MotoSOS.API.Modules.Notifications.Providers;

public sealed class BrevoSmsNotificationSender : ISmsNotificationSender
{
    private const string Endpoint = "https://api.brevo.com/v3/transactionalSMS/sms";
    private readonly IHttpClientFactory _httpClientFactory;

    public BrevoSmsNotificationSender(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<string?> SendAsync(SmsNotificationMessage message, SmsNotificationProviderOptions options, CancellationToken cancellationToken)
    {
        using HttpClient client = _httpClientFactory.CreateClient(nameof(BrevoSmsNotificationSender));
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = JsonContent.Create(new BrevoSmsRequest(message.Sender, message.ToPhoneNumber, message.Body))
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("api-key", options.ApiKey);

        using HttpResponseMessage response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException("Brevo SMS request failed.");

        BrevoSmsResponse? result = await response.Content.ReadFromJsonAsync<BrevoSmsResponse>(cancellationToken: cancellationToken);
        return result?.MessageId;
    }

    private sealed record BrevoSmsRequest([property: JsonPropertyName("sender")] string Sender, [property: JsonPropertyName("recipient")] string Recipient, [property: JsonPropertyName("content")] string Content);
    private sealed record BrevoSmsResponse([property: JsonPropertyName("messageId")] string? MessageId);
}
