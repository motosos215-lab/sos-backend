# Emergency Escalation API

Emergency Escalation API registra y consulta escalamiento interno simulado de emergencias cuando una alerta no recibe respuesta suficiente. No llama servicios de emergencia reales ni envia notificaciones reales.

## Estado Actual

- Coleccion MongoDB: `emergencyEscalations`.
- Idempotencia: `userId + alertDispatchId`.
- Indices unicos: `IdempotencyKey` y `AlertDispatchId`.
- Crear dos veces para el mismo alert dispatch devuelve el mismo `EmergencyEscalation`; no devuelve `409`.
- No modifica `Incident`, `AlertDispatchRequest`, `NotificationDeliveryAttempt`, `AlertAcknowledgement` ni `EmergencyResolutionReport`.
- Registra auditoria best-effort para requests, marcado unresolved y cancelacion.
- Automatic Escalation Worker puede crear escalations automaticas `NoAcknowledgement` / `Level1` cuando existe un attempt `SimulatedSent` antiguo sin acknowledgement.

## Endpoints

- `POST /api/v1/rider/alert-dispatches/{alertDispatchId}/escalate`
- `GET /api/v1/rider/alert-dispatches/{alertDispatchId}/escalation-status`
- `GET /api/v1/monitor/alerts/{notificationDeliveryAttemptId}/escalation-status`
- `POST /api/v1/rider/alert-dispatches/{alertDispatchId}/mark-unresolved`
- `POST /api/v1/rider/alert-dispatches/{alertDispatchId}/cancel-escalation`
- `GET /api/v1/admin/escalations`
- `GET /api/v1/admin/escalations/worker/status`
- `POST /api/v1/admin/escalations/worker/run`

## Request

```json
{
  "reason": "NoAcknowledgement",
  "level": "Level1",
  "notes": "Optional internal escalation note."
}
```

`reason` acepta `NoAcknowledgement`, `AllContactsDeclined`, `ManualEscalation` o `SimulatedEmergencyFollowUp`.

`level` acepta `Level1`, `Level2` o `ManualReview`.

## Reglas

- Solo `Rider` puede crear, marcar unresolved o cancelar escalamiento.
- `Rider` solo opera alert dispatches propios.
- `Monitor` solo consulta escalation status de alertas asignadas por `EmergencyContact.LinkedUserId`.
- `Admin` solo puede listar escalations desde `/api/v1/admin/escalations`.
- Sin token devuelve `401 unauthorized`.
- Roles no permitidos devuelven `403 forbidden`.
- Recursos ajenos devuelven `404 not_found`.
- Incidentes `Closed` o `FalsePositiveCancelled` devuelven `incident_not_ready`.
- Si existe cualquier acknowledgement `Acknowledged`, el escalamiento queda bloqueado con `emergency_escalation_not_allowed`.
- `NoAcknowledgement` requiere incidente `Open`, alert dispatch propio, al menos un `NotificationDeliveryAttempt` y al menos un attempt `SimulatedSent`.
- `AllContactsDeclined` requiere al menos un acknowledgement, todos en `Declined` y ninguno en `Acknowledged`.
- Sin attempts solo se permite `ManualEscalation`.
- `ManualEscalation` se permite para incidente propio `Open` si no existe acknowledgement `Acknowledged`.
- El worker automatico no reemplaza el escalamiento manual del Rider y no escala solo por antiguedad del alert dispatch; valida el `SimulatedSentAtUtc` mas antiguo.

## Admin List

```http
GET /api/v1/admin/escalations?status=Requested&reason=NoAcknowledgement&level=Level1&pageNumber=1&pageSize=20
Authorization: Bearer {adminAccessToken}
```

Filtros:

| Param | Tipo | Reglas |
| --- | --- | --- |
| `status` | enum | Opcional. |
| `reason` | enum | Opcional. `Unknown` no es valido. |
| `level` | enum | Opcional. |
| `dateFrom` | ISO UTC | Opcional. |
| `dateTo` | ISO UTC | Opcional. |
| `pageNumber` | int | Default `1`, minimo `1`. |
| `pageSize` | int | Default `20`, minimo `1`, maximo `100`. |

Admin list no acepta `userId` como identidad externa. La identidad se toma siempre del JWT.

## Respuesta

```json
{
  "success": true,
  "data": {
    "escalation": {
      "id": "emergency-escalation-id",
      "incidentId": "incident-id",
      "tripId": "trip-id",
      "alertDispatchId": "alert-dispatch-id",
      "status": "Requested",
      "reason": "NoAcknowledgement",
      "level": "Level1",
      "notes": "Optional internal escalation note.",
      "notificationsTotal": 1,
      "notificationsPrepared": 0,
      "notificationsSimulatedSent": 1,
      "notificationsFailed": 0,
      "notificationsCancelled": 0,
      "acknowledgementsTotal": 0,
      "acknowledgedCount": 0,
      "declinedCount": 0,
      "viewedCount": 0,
      "pendingCount": 0,
      "createdAtUtc": "2026-08-08T12:00:00Z",
      "updatedAtUtc": null,
      "resolvedAtUtc": null,
      "markedUnresolvedAtUtc": null,
      "cancelledAtUtc": null
    }
  },
  "error": null
}
```

## Auditoria

Acciones nuevas:

- `EmergencyEscalationRequested`
- `EmergencyEscalationMarkedUnresolved`
- `EmergencyEscalationCancelled`
- `AutomaticEscalationWorkerRun`
- `AutomaticEscalationWorkerFailed`
- `AutomaticEscalationWorkerSkipped`
- `EmergencyEscalationAutomaticallyRequested`

Metadata permitida:

- `escalationId`
- `incidentId`
- `alertDispatchId`
- `reason`
- `level`
- `status`
- `notificationsTotal`
- `acknowledgedCount`
- `declinedCount`

## Fuera De Alcance

No implementa llamadas reales a servicios de emergencia, mensajeria real, proveedores externos, protocolos realtime, tracking en vivo, cobros, ML ni pairing API de smartwatch.
