# Emergency Resolution Report API

Emergency Resolution Report API crea y consulta el reporte final formal de una emergencia ya cerrada o cancelada como falso positivo.

## Endpoints

- `POST /api/v1/rider/emergencies/{incidentId}/resolution-report`
- `GET /api/v1/rider/emergencies/{incidentId}/resolution-report`
- `GET /api/v1/monitor/alerts/{notificationDeliveryAttemptId}/resolution-report`
- `GET /api/v1/rider/emergencies/resolution-reports`

## Reglas

- Solo `Rider` puede crear reportes.
- `Rider` solo consulta y lista reportes propios.
- `Monitor` solo consulta reportes de alertas asignadas por `EmergencyContact.LinkedUserId`.
- `Admin` recibe `403 forbidden`.
- Sin token se devuelve `401 unauthorized`.
- `userId` se obtiene siempre desde JWT y no se acepta en el body.
- Incidentes ajenos devuelven `404 not_found`.
- Solo se crea reporte para incidentes `Closed` o `FalsePositiveCancelled`.
- Incidentes `Open` devuelven `400 incident_not_ready`.
- Crear dos veces para el mismo `userId + incidentId` devuelve el reporte existente sin `409`.
- El cierre operativo no se duplica: sigue en `POST /api/v1/incidents/{id}/close` y `POST /api/v1/incidents/{id}/cancel-false-positive`.

## Request

```json
{
  "outcome": "RealEmergency",
  "summary": "Rider is safe and the incident was resolved.",
  "notes": "Optional internal notes."
}
```

`outcome` acepta `RealEmergency`, `FalsePositive`, `UserSafe`, `Assisted` o `Cancelled`.

## Metricas Persistidas

- `notificationAttemptsTotal`
- `acknowledgementsTotal`
- `acknowledgedCount`
- `declinedCount`
- `firstNotificationPreparedAtUtc`
- `firstAcknowledgedAtUtc`
- `responseTimeSeconds`
- `incidentClosedAtUtc`
- `finalLatitude`
- `finalLongitude`
- `finalLocationRecordedAtUtc`
- `lastKnownLocationWasStale`

La ubicacion final usa el ultimo `EmergencyLocationSnapshot` asociado al incidente, ordenado por `RecordedAtUtc` desc y luego `ReceivedAtUtc` desc. No crea historial, polyline, tracking continuo ni monitoreo en vivo.

## Seguridad

Las respuestas no exponen hashes de credenciales, tokens de sesion, identificadores de dispositivo, tokens de proveedores, telefonos/correos completos ni datos de cobro.

Esta API no implementa proveedores reales, push real, SMS real, mensajeria real, correo real, protocolos realtime, streaming, dashboard operativo, mapa en tiempo real, cobros en tiendas ni pairing API de smartwatch.

## Relacion Con Evidence Attachments

Evidence Attachments API puede asociar metadata segura a un `EmergencyResolutionReport`. No sube ni descarga archivos reales y no modifica el reporte de resolucion.
