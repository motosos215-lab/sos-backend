namespace MotoSOS.API.Modules.ReportExports.Contracts;

public sealed record ResolutionReportTelemetrySummaryResponse(int TotalMinorEvents, IReadOnlyDictionary<string, int> EventsByType, IReadOnlyDictionary<string, int> EventsBySeverity, IReadOnlyDictionary<string, int> EventsBySource, int HardBrakeCount, int LowBatteryCount, int PossibleFallLowConfidenceCount, double? AverageConfidence, double? MaxScore, double? AverageScore, int? MinBatteryLevel, double? MaxSpeedKmh);
