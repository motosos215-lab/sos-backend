namespace MotoSOS.API.Modules.MinorEvents.Domain;

public enum MinorEventType
{
    Unknown = 0,
    HardBrake = 1,
    HarshAcceleration = 2,
    SharpTurn = 3,
    GpsSignalLost = 4,
    GpsSignalRecovered = 5,
    LowBattery = 6,
    SmartwatchDisconnected = 7,
    SmartwatchReconnected = 8,
    SensorAnomaly = 9,
    PossibleFallLowConfidence = 10,
    TripSignalWeak = 11,
    Informational = 12
}
