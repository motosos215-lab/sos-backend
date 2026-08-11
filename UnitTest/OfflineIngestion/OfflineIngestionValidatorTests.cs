using System.Text.Json;
using FluentAssertions;
using MotoSOS.API.Modules.OfflineIngestion.Application;
using MotoSOS.API.Modules.OfflineIngestion.Contracts;

namespace UnitTest.OfflineIngestion;

public sealed class OfflineIngestionValidatorTests
{
    [Fact]
    public void BatchFieldsAreRequiredAndValidated()
    {
        var validator = new OfflineIngestionBatchRequestValidator();

        validator.Validate(new OfflineIngestionBatchRequest(null, null, null, 2, null, new string('a', 51), null)).IsValid.Should().BeFalse();
        validator.Validate(ValidBatch()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ItemsAreRequiredLimitedAndValidated()
    {
        var validator = new OfflineIngestionBatchRequestValidator();
        OfflineIngestionItemRequest invalid = new(null, "bad", null, 0, JsonDocument.Parse("{}").RootElement);
        OfflineIngestionItemRequest[] tooMany = Enumerable.Range(0, 11).Select(_ => ValidItem()).ToArray();

        validator.Validate(ValidBatch([])).IsValid.Should().BeFalse();
        validator.Validate(ValidBatch([invalid])).IsValid.Should().BeFalse();
        validator.Validate(ValidBatch(tooMany)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void OfflineSosAlertTypeIsAccepted()
    {
        var validator = new OfflineIngestionBatchRequestValidator();

        validator.Validate(ValidBatch([ValidItem(type: "offline-sos-alert", payload: "{\"clientIncidentId\":\"22222222-2222-2222-2222-222222222222\"}")])).IsValid.Should().BeTrue();
    }

    [Fact]
    public void PayloadMustNotBeEmptyOrTooLarge()
    {
        var validator = new OfflineIngestionBatchRequestValidator();
        OfflineIngestionItemRequest empty = ValidItem(payload: "{}");
        OfflineIngestionItemRequest large = ValidItem(payload: $"{{\"data\":\"{new string('a', 33 * 1024)}\"}}");

        validator.Validate(ValidBatch([empty])).IsValid.Should().BeFalse();
        validator.Validate(ValidBatch([large])).IsValid.Should().BeFalse();
    }

    private static OfflineIngestionBatchRequest ValidBatch(IReadOnlyList<OfflineIngestionItemRequest>? items = null) => new(
        Guid.NewGuid().ToString(),
        "mobile",
        "trip",
        1,
        DateTimeOffset.UtcNow,
        "1.0.0",
        items ?? [ValidItem()]);

    private static OfflineIngestionItemRequest ValidItem(string payload = "{\"score\":35}", string type = "minor-event") => new(
        Guid.NewGuid().ToString(),
        type,
        DateTimeOffset.UtcNow,
        1,
        JsonDocument.Parse(payload).RootElement.Clone());
}
