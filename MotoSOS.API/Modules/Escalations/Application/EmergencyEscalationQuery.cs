using MotoSOS.API.Modules.Escalations.Domain;

namespace MotoSOS.API.Modules.Escalations.Application;

public sealed record EmergencyEscalationQuery(EmergencyEscalationStatus? Status, EmergencyEscalationReason? Reason, EmergencyEscalationLevel? Level, DateTimeOffset? DateFrom, DateTimeOffset? DateTo, int PageNumber, int PageSize);
