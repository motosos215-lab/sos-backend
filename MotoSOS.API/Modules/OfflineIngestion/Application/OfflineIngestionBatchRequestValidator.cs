using System.Text;
using System.Text.Json;
using FluentValidation;
using MotoSOS.API.Modules.OfflineIngestion.Contracts;

namespace MotoSOS.API.Modules.OfflineIngestion.Application;

public sealed class OfflineIngestionBatchRequestValidator : AbstractValidator<OfflineIngestionBatchRequest>
{
    private const int MaxPayloadBytes = 32 * 1024;
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "minor-event",
        "local-incident",
        "alert-dispatch-request"
    };

    public OfflineIngestionBatchRequestValidator()
    {
        RuleFor(request => request.BatchId).NotEmpty().Must(BeUuid).WithMessage("BatchId must be a valid UUID.");
        RuleFor(request => request.MobileDeviceId).NotEmpty();
        RuleFor(request => request.TripId).NotEmpty();
        RuleFor(request => request.SchemaVersion).Equal(1);
        RuleFor(request => request.AppVersion).MaximumLength(50);
        RuleFor(request => request.Items).NotNull().Must(items => items is { Count: >= 1 and <= 10 }).WithMessage("Items must contain between 1 and 10 entries.");
        RuleForEach(request => request.Items).ChildRules(item =>
        {
            item.RuleFor(value => value.ClientEventId).NotEmpty().Must(BeUuid).WithMessage("ClientEventId must be a valid UUID.");
            item.RuleFor(value => value.Type).NotEmpty().Must(type => !string.IsNullOrWhiteSpace(type) && AllowedTypes.Contains(type)).WithMessage("Unsupported offline ingestion item type.");
            item.RuleFor(value => value.OccurredAtUtc).NotNull();
            item.RuleFor(value => value.PayloadVersion).NotNull().GreaterThanOrEqualTo(1);
            item.RuleFor(value => value.Payload).Must(HavePayload).WithMessage("Payload is required.");
            item.RuleFor(value => value.Payload).Must(FitPayloadLimit).WithMessage("Payload is too large.");
        });
    }

    private static bool BeUuid(string? value) => Guid.TryParse(value, out _);

    private static bool HavePayload(JsonElement payload) => payload.ValueKind switch
    {
        JsonValueKind.Object => payload.EnumerateObject().Any(),
        JsonValueKind.Array => payload.GetArrayLength() > 0,
        JsonValueKind.Undefined or JsonValueKind.Null => false,
        _ => true
    };

    private static bool FitPayloadLimit(JsonElement payload)
    {
        if (payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) return false;
        return Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(payload)) <= MaxPayloadBytes;
    }
}
