# Audit Logs API

Audit Logs API permite consultar eventos internos importantes de MotoSOS sin guardar informacion sensible. Es un modulo de auditoria interna para trazabilidad operativa, no una integracion SIEM.

## Estado Actual

- Coleccion MongoDB: `auditLogs`.
- Endpoints Admin-only bajo `/api/v1/admin/audit-logs`.
- Escritura de auditoria best-effort: si falla guardar el audit log, la operacion principal no se rompe.
- Lectura de auditoria no es best-effort: si falla consultar, se devuelve error controlado siguiendo el manejo global actual.
- Los endpoints de consulta de audit logs no se auditan para evitar ruido, crecimiento innecesario o auditoria recursiva.

## Seguridad

No se guardan passwords, hashes, access tokens, refresh tokens, authorization headers, bearer tokens, device identifiers, provider tokens, payloads completos, correos completos, telefonos completos, datos de pago, stack traces, errores internos de MongoDB, connection strings ni secretos.

`metadata` se sanitiza de forma case-insensitive. Se remueven claves que contengan:

```text
password
passwordHash
accessToken
refreshToken
token
authorization
bearer
deviceIdentifier
deviceIdentifierHash
providerToken
payload
email
phone
payment
card
connectionString
secret
stackTrace
exception
mongo
mongodb
```

Los valores de metadata permitida se truncan a 200 caracteres por valor para evitar persistir payloads completos.

## Acciones Auditadas

Actualmente se auditan estas acciones exitosas:

- `AuthLogin`
- `AuthLogout`
- `IncidentCreated`
- `IncidentClosed`
- `IncidentCancelledFalsePositive`
- `AlertDispatchCreated`
- `AlertDispatchCancelled`
- `NotificationOutboxRun`
- `NotificationOutboxRetryFailed`
- `AlertAcknowledgementViewed`
- `AlertAcknowledgementAcknowledged`
- `AlertAcknowledgementDeclined`
- `EmergencyResolutionReportCreated`
- `OfflineProcessingRun`
- `EmergencyEscalationRequested`
- `EmergencyEscalationMarkedUnresolved`
- `EmergencyEscalationCancelled`
- `MinorEventRecorded`
- `MinorEventMarkedReviewed`
- `MinorEventIgnored`
- `MinorEventProcessedFromOfflineIngestion`

## Endpoints

### Listar Audit Logs

```http
GET /api/v1/admin/audit-logs
Authorization: Bearer {adminAccessToken}
```

Query params:

| Param | Tipo | Reglas |
| --- | --- | --- |
| `actorUserId` | string | Opcional. Filtro, no identidad autenticada. |
| `action` | enum | Opcional. Case-sensitive. |
| `module` | enum | Opcional. Case-sensitive. |
| `outcome` | enum | Opcional. Case-sensitive. |
| `entityType` | string | Opcional. |
| `entityId` | string | Opcional. |
| `dateFrom` | ISO UTC | Opcional. |
| `dateTo` | ISO UTC | Opcional. |
| `pageNumber` | int | Default `1`, minimo `1`. |
| `pageSize` | int | Default `20`, minimo `1`, maximo `100`. |

Reglas:

- Sin token devuelve `401`.
- `Rider` devuelve `403`.
- `Monitor` devuelve `403`.
- `Admin` puede consultar.
- `dateFrom > dateTo` devuelve `validation_error`.
- Enum invalido devuelve `validation_error`.
- Orden: `createdAtUtc desc`.

Respuesta:

```json
{
  "success": true,
  "data": {
    "auditLogs": [
      {
        "id": "audit-log-id",
        "actorUserId": "admin-user-id",
        "actorRole": "Admin",
        "action": "NotificationOutboxRun",
        "module": "NotificationOutbox",
        "entityType": "NotificationOutbox",
        "entityId": null,
        "outcome": "Success",
        "reason": "run",
        "correlationId": "trace-id",
        "requestPath": "/api/v1/admin/notifications/outbox/run",
        "httpMethod": "POST",
        "createdAtUtc": "2026-08-08T12:00:00Z",
        "metadata": [
          {
            "key": "processed",
            "value": "2"
          },
          {
            "key": "simulateFailures",
            "value": "False"
          }
        ]
      }
    ],
    "pageNumber": 1,
    "pageSize": 20,
    "totalCount": 1
  },
  "error": null
}
```

### Obtener Audit Log Por Id

```http
GET /api/v1/admin/audit-logs/{id}
Authorization: Bearer {adminAccessToken}
```

Reglas:

- Sin token devuelve `401`.
- `Rider` devuelve `403`.
- `Monitor` devuelve `403`.
- Si no existe, devuelve `404`.
- No devuelve metadata sensible.

## Metadata Permitida

Ejemplos de metadata segura:

- ids de entidades.
- `previousStatus` / `newStatus`.
- counts agregados como `processed`, `failed`, `skipped`, `retried`.
- `reason` controlado.
- `maxItems`.
- `simulateFailures`.
- `outcome` de resolucion.

## Fuera De Alcance

No implementa:

- SIEM externo.
- Splunk.
- Datadog.
- CloudWatch.
- Exportacion automatica.
- Worker real.
- Background service.
- WebSockets.
- SignalR.
- Tracking en vivo.
- Twilio.
- SendGrid.
- WhatsApp.
- FCM.
- Pagos.
- Google Play.
- Stripe.
- Pairing API de smartwatch.

## Pendientes Futuros

- Integracion SIEM externa.
- Exportacion controlada.
- Politica de retencion.
- Alertas de seguridad.
- Auditoria mas amplia.
- CorrelationId completo por request.
