namespace MotoSOS.API.Modules.TelemetrySummary.Contracts;

public sealed record GetTelemetrySummariesResponse(IReadOnlyList<TelemetrySummaryResponse> Summaries, int PageNumber, int PageSize, long TotalCount);
