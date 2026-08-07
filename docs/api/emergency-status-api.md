# Emergency Status / Monitoring Summary API

## Descripcion

Emergency Status API entrega un resumen de una emergencia usando datos existentes de incidentes, viajes, alert dispatch, notification attempts, acknowledgements y ultima ubicacion conocida.

Este modulo evita que clientes Rider o Monitor llamen multiples endpoints para conocer el estado operativo basico de una emergencia.

No es live monitoring completo. No implementa WebSockets, SignalR, streaming de ubicacion, mapa en tiempo real, tracking continuo, historial completo de ruta, chat, llamadas, proveedores reales de notificacion, escalamiento automatico, dashboard operativo completo ni Machine Learning.

## Endpoints

- `GET /api/v1/rider/emergencies/{incidentId}/status`
- `GET /api/v1/monitor/alerts/{notificationDeliveryAttemptId}/status`
- `GET /api/v1/rider/emergencies/active?pageNumber=1&pageSize=20`

Todos requieren JWT Bearer.

## Rider Status

`GET /api/v1/rider/emergencies/{incidentId}/status`

Reglas:

- Solo `Rider`.
- `userId` se obtiene exclusivamente del token.
- El incidente debe existir y pertenecer al Rider autenticado.
- Incidente ajeno devuelve `404 not_found`.
- Los conteos de notifications y acknowledgements se acotan a la emergencia por `incidentId` y, cuando existe, por `alertDispatchId`.

## Monitor Status

`GET /api/v1/monitor/alerts/{notificationDeliveryAttemptId}/status`

Reglas:

- Solo `Monitor`.
- Usa la relacion segura `EmergencyContact.LinkedUserId == monitorUserId`.
- El `NotificationDeliveryAttempt` debe estar asociado a un contacto vinculado al Monitor.
- Intentos ajenos devuelven `404 not_found`.
- El resumen se resuelve desde `NotificationDeliveryAttempt.IncidentId`.
- No devuelve telefono/correo completo ni datos sensibles del Rider.

## Emergencias Activas

`GET /api/v1/rider/emergencies/active?pageNumber=1&pageSize=20`

Reglas:

- Solo `Rider`.
- Lista incidentes propios con `IncidentStatus = Open`.
- `pageNumber` default `1`.
- `pageSize` default `20`, maximo `100`.
- Cada resumen se calcula por `incidentId` especifico.

## Response

```json
{
  "success": true,
  "data": {
    "incident": {
      "id": "incident-id",
      "status": "Open",
      "source": "MobileDetection",
      "cause": "CountdownTimeout",
      "riskLevel": "High",
      "occurredAtUtc": "2026-08-06T14:20:00Z",
      "createdAtUtc": "2026-08-06T14:20:05Z"
    },
    "trip": {
      "id": "trip-id",
      "status": "Active",
      "startedAtUtc": "2026-08-06T14:00:00Z",
      "finishedAtUtc": null
    },
    "alertDispatch": {
      "id": "alert-dispatch-id",
      "status": "PendingDispatch",
      "priority": "High",
      "reason": "IncidentCreated",
      "createdAtUtc": "2026-08-06T14:20:10Z"
    },
    "notifications": {
      "total": 1,
      "prepared": 1,
      "simulatedSent": 0,
      "failed": 0,
      "cancelled": 0
    },
    "acknowledgements": {
      "total": 1,
      "pending": 0,
      "viewed": 0,
      "acknowledged": 1,
      "declined": 0
    },
    "location": {
      "available": true,
      "incidentId": "incident-id",
      "tripId": "trip-id",
      "latitude": 19.432608,
      "longitude": -99.133209,
      "accuracyMeters": 15,
      "source": "MobileApp",
      "recordedAtUtc": "2026-08-06T14:20:00Z",
      "receivedAtUtc": "2026-08-06T14:20:04Z",
      "isActive": true,
      "isStale": false
    },
    "overallStatus": "Acknowledged",
    "requiresAttention": false,
    "lastUpdatedAtUtc": "2026-08-06T14:25:00Z"
  },
  "error": null
}
```

Si no hay alert dispatch, `alertDispatch = null` y los conteos quedan en cero si no hay registros asociados.

Si no hay ubicacion activa, `location.available = false` y los campos de coordenadas quedan `null`.

## overallStatus

- `Closed`: `IncidentStatus = Closed`.
- `Cancelled`: `IncidentStatus = FalsePositiveCancelled`.
- `Acknowledged`: existe acknowledgement `Acknowledged`.
- `Declined`: existen acknowledgements y todos estan `Declined`.
- `AwaitingAcknowledgement`: hay notification attempts pero ningun acknowledgement confirmado.
- `Active`: `IncidentStatus = Open`.
- `Unknown`: fallback si no se puede determinar.

## requiresAttention

- `true` si el incidente esta `Open` y no hay acknowledgement `Acknowledged`.
- `false` si existe acknowledgement `Acknowledged`.
- `false` si el incidente esta `Closed` o `FalsePositiveCancelled`.

## lastUpdatedAtUtc

Se calcula con el timestamp mas reciente entre registros de la emergencia especifica:

- Incident.
- Trip.
- AlertDispatch.
- NotificationDeliveryAttempt.
- AlertAcknowledgement.
- EmergencyLocationSnapshot.

## Seguridad

- No devuelve `passwordHash`.
- No devuelve refresh tokens ni access tokens.
- No devuelve `deviceIdentifier` ni `DeviceIdentifierHash`.
- No devuelve provider tokens.
- No devuelve telefono/correo completo de contactos.
- No devuelve datos de pago.
- No inicia envio real de notificaciones.
- No inicia tracking ni streaming.

## Errores Esperados

- `401 unauthorized`.
- `403 forbidden`.
- `400 validation_error`.
- `404 not_found`.
- `404 emergency_status_not_available`.

## Ejemplo curl

```bash
curl -X GET "$BASE_URL/api/v1/rider/emergencies/$INCIDENT_ID/status" \
  -H "Authorization: Bearer $ACCESS_TOKEN"
```

```bash
curl -X GET "$BASE_URL/api/v1/monitor/alerts/$NOTIFICATION_DELIVERY_ATTEMPT_ID/status" \
  -H "Authorization: Bearer $ACCESS_TOKEN"
```

## Pendientes Futuros

- Live Monitoring completo.
- WebSockets/SignalR.
- Mapa en tiempo real.
- Streaming.
- Dashboard operativo.
- Escalamiento.
- Proveedores reales de notificacion.
- ML.
