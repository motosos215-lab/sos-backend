# SOS Alert Orchestration API

## Descripcion

SOS Alert Orchestration API simplifica el flujo movil de emergencia en un solo endpoint. No reemplaza los endpoints individuales de Incidents, Alert Dispatch ni Notifications; solo los orquesta para que la app movil no tenga que llamar manualmente tres endpoints separados.

El endpoint no envia notificaciones directamente. Solo deja `NotificationDeliveryAttempts` en estado `Prepared`. El envio posterior sigue separado y lo procesa el outbox worker si esta habilitado o `POST /api/v1/admin/notifications/outbox/run` si se ejecuta manualmente. Cuando el worker procesa attempts, `Push` usa FCM si `Notifications:Providers:Fcm` esta habilitado/configurado, `Email` usa SMTP/Brevo si `Notifications:Providers:Email` esta habilitado/configurado y SMS sigue simulado.

Para SOS offline, Android debe usar `POST /api/v1/mobile/offline-ingestion/batch` con item `type = offline-sos-alert`. Ese flujo reutiliza internamente esta misma orquestacion durante Offline Processing.

## Endpoint

```http
POST /api/v1/mobile/sos-alerts
Authorization: Bearer {riderAccessToken}
Content-Type: application/json
```

Roles:

- Solo `Rider`.
- `Monitor` y `Admin` reciben `403 forbidden`.

## Request

`clientIncidentId` y `clientAlertRequestId` deben ser UUID/GUID validos, igual que en los endpoints individuales actuales.

Valores reales actuales:

- `incidentType`: `CountdownTimeout`, `UserRequestedHelp`, `CriticalEvent`, `ManualSos`, `Unknown`.
- `severity`: `Unknown`, `Low`, `Medium`, `High`.
- `priority`: `Low`, `Medium`, `High`, `Critical`.
- `reason`: `IncidentCreated`, `ManualSos`, `CountdownTimeout`, `CriticalEvent`, `UserRequestedHelp`, `Unknown`.

```json
{
  "tripId": "trip-id",
  "clientIncidentId": "11111111-1111-1111-1111-111111111111",
  "clientAlertRequestId": "22222222-2222-2222-2222-222222222222",
  "incidentType": "CountdownTimeout",
  "severity": "High",
  "detectedAtUtc": "2026-08-10T12:05:00Z",
  "latitude": 19.4326,
  "longitude": -99.1332,
  "priority": "High",
  "reason": "IncidentCreated",
  "notes": "Caida detectada por sensores"
}
```

## Response

Codigo HTTP exito: `200 OK`.

```json
{
  "success": true,
  "data": {
    "incident": {
      "id": "incident-id",
      "tripId": "trip-id",
      "status": "Open",
      "incidentType": "CountdownTimeout",
      "severity": "High"
    },
    "alertDispatch": {
      "id": "alert-dispatch-id",
      "incidentId": "incident-id",
      "status": "PendingDispatch",
      "contactsCount": 1
    },
    "notificationAttempts": [
      {
        "id": "attempt-id",
        "channel": "Push",
        "status": "Prepared",
        "provider": "None",
        "emergencyContactId": "contact-id",
        "contactFullName": "Maria Lopez"
      }
    ],
    "summary": {
      "pushPrepared": 1,
      "smsPrepared": 1,
      "emailPrepared": 0,
      "totalPrepared": 2
    }
  },
  "error": null
}
```

## Flujo Interno

1. Crea incidente usando la misma logica de `POST /api/v1/incidents`.
2. Crea alert dispatch usando la misma logica de `POST /api/v1/alert-dispatches`.
3. Prepara attempts usando la misma logica de `POST /api/v1/notifications/delivery-attempts/prepare`.
4. No ejecuta outbox.
5. No envia SMS, email, WhatsApp ni push directamente.
6. El worker procesa despues los attempts `Prepared` si `Notifications:OutboxWorker:Enabled = true`.

Mapeo interno:

- `incidentType` -> `CreateIncidentRequest.Cause`.
- `severity` -> `CreateIncidentRequest.RiskLevel`.
- `detectedAtUtc` -> `OccurredAtUtc`, `RequestedAtUtc` y location `RecordedAtUtc`.
- `latitude`/`longitude` -> `IncidentLocationRequest`.
- `Source` del incidente queda fijo como `MobileDetection`.

## Idempotencia

La idempotencia se hereda de los modulos existentes:

- Incident: `userId + tripId + clientIncidentId`.
- Alert Dispatch: `userId + incidentId + clientAlertRequestId`.
- Notification Attempts: `userId + alertDispatchId + emergencyContactId + channel + attemptNumber`.

Repetir exactamente la misma request no duplica incidente, alert dispatch ni attempts. Devuelve IDs estables.

## Seleccion De Attempts

La seleccion de canales es la misma de Notifications API:

- `Push` si el contacto esta `Linked` y el Monitor vinculado tiene token FCM activo Android o Web.
- `Sms` si el contacto tiene telefono.
- `Email` si no tiene telefono pero tiene email.
- Los attempts quedan `Prepared` con `provider = None`.

## Errores Esperados

- `401 unauthorized`: sin token o credenciales invalidas.
- `403 forbidden`: usuario no Rider.
- `400 validation_error`: request invalido, UUID invalido o enum invalido.
- `400 onboarding_not_ready`: onboarding incompleto o no operacional.
- `400 trip_not_ready`: viaje no apto para incidente.
- `404 not_found`: trip, incident o alert dispatch inexistente o ajeno.
- `400 incident_not_ready`: incidente cerrado/no apto para dispatch.
- `400 alert_not_allowed`: sin contactos elegibles o incidente no permite dispatch.
- `400 alert_dispatch_not_ready`: dispatch no esta listo para preparar attempts.
- `400 alert_dispatch_already_completed`: dispatch ya completado.
- `400 notification_not_allowed`: no hay canales disponibles o transicion no permitida.

## Seguridad

- No acepta `userId` en body.
- No devuelve tokens FCM reales.
- No devuelve `tokenValue` ni `tokenHash`.
- No devuelve credenciales de providers.
- No ejecuta proveedores externos directamente; el outbox lo hace despues si esta habilitado.

## Operacion Admin

- `GET /api/v1/admin/notifications/outbox/status`: conteos globales por estado.
- `GET /api/v1/admin/notifications/outbox/worker/status`: configuracion efectiva segura y ultima corrida del worker.
- `GET /api/v1/admin/notifications/providers/status`: estado seguro de providers, incluidos FCM y Email sin credenciales.

## Fuera De Alcance

No implementa SMS real, WhatsApp real, pagos, PDF, evidencia binaria, realtime, ML, distributed lock, CORS ni cambios de deploy.
