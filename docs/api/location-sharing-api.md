# Emergency Location Sharing API

## Descripcion

Emergency Location Sharing API permite que la app movil del Rider comparta la ultima ubicacion conocida asociada a un incidente abierto, y que un Monitor asignado consulte esa ubicacion.

Este modulo no es live tracking. No implementa streaming, sockets en tiempo real, mapa en vivo, historial de ruta, chat, llamadas, escalamiento ni proveedores reales de notificacion.

Para Wear OS, el telefono Android es el unico gateway de ubicacion compartida hacia la API. El smartwatch puede aportar contexto local al telefono, pero la API no administra pairing, QR, codigos, nodeId, Bluetooth, Wear OS Data Layer ni estado Connected/Disconnected del reloj.

## Privacidad

- Se guarda solo la ultima ubicacion conocida por incidente.
- No se guarda historial completo de ruta.
- No se guardan arreglos de puntos.
- No se guarda polyline.
- No existe coleccion de tracking continuo.

## Endpoints

- `POST /api/v1/mobile/location-sharing/snapshot`
- `GET /api/v1/monitor/alerts/{notificationDeliveryAttemptId}/location`
- `GET /api/v1/rider/incidents/{incidentId}/location`

## Publicar Ubicacion

Solo `Rider` puede publicar ubicacion.

Request:

```json
{
  "incidentId": "incident-id-remoto",
  "clientLocationUpdateId": "f92d2b51-37df-46a5-9483-24f8dcad0001",
  "latitude": 19.432608,
  "longitude": -99.133209,
  "accuracyMeters": 15,
  "altitudeMeters": 2240,
  "speedMetersPerSecond": 0,
  "headingDegrees": 90,
  "batteryPercentage": 82,
  "source": "MobileApp",
  "recordedAtUtc": "2026-08-06T14:20:00Z"
}
```

Reglas:

- Requiere onboarding completo.
- Incidente debe existir, pertenecer al Rider y estar `Open`.
- Incidente `Closed` devuelve `incident_not_ready`.
- Incidente `FalsePositiveCancelled` devuelve `location_sharing_not_allowed`.
- `TripId`, `MobileDeviceId` y `SmartwatchDeviceId` se derivan desde el incidente.
- `recordedAtUtc` puede estar maximo 2 minutos en el futuro.
- Upsert por `UserId + IncidentId`.
- Ubicacion mas antigua no reemplaza la actual.
- Mismo `clientLocationUpdateId` devuelve estado actual.

## Consultar Como Monitor

`GET /api/v1/monitor/alerts/{notificationDeliveryAttemptId}/location`

Reglas:

- Solo `Monitor`.
- Usa `EmergencyContact.LinkedUserId == monitorUserId`.
- El intento de notificacion debe pertenecer a un contacto vinculado al Monitor.
- Intento ajeno devuelve `404 not_found`.
- Si no existe snapshot activo devuelve `location_not_available`.
- No inicia streaming ni tracking.

## Consultar Como Rider

`GET /api/v1/rider/incidents/{incidentId}/location`

Reglas:

- Solo `Rider`.
- El incidente debe pertenecer al Rider.
- Incidente ajeno devuelve `404 not_found`.
- Si no existe snapshot activo devuelve `location_not_available`.

## Response

```json
{
  "success": true,
  "data": {
    "location": {
      "incidentId": "incident-id",
      "tripId": "trip-id",
      "latitude": 19.432608,
      "longitude": -99.133209,
      "accuracyMeters": 15,
      "source": "MobileApp",
      "recordedAtUtc": "2026-08-06T14:20:00+00:00",
      "receivedAtUtc": "2026-08-06T14:20:04+00:00",
      "isActive": true,
      "isStale": false
    }
  },
  "error": null
}
```

## Staleness

`isStale = true` cuando `now - recordedAtUtc > 5 minutos`. Esto solo indica antiguedad de la ultima ubicacion conocida; no implica seguimiento en vivo.

## Errores Esperados

- `401 unauthorized`
- `403 forbidden`
- `400 validation_error`
- `400 onboarding_not_ready`
- `400 incident_not_ready`
- `400 location_sharing_not_allowed`
- `404 not_found`
- `404 location_not_available`

## MongoDB

Coleccion: `emergencyLocationSnapshots`.

Indices:

- `UserId`
- `IncidentId`
- `TripId`
- `UserId + IncidentId` unico
- `IncidentId + IsActive`
- `RecordedAtUtc`
- `ReceivedAtUtc`
- `UpdatedAtUtc`
- `IsActive`

## Pendientes Futuros

- Live Monitoring completo.
- Sockets en tiempo real.
- Mapa en vivo.
- Historial controlado.
- Configuracion de frecuencia.
- Proveedores reales de notificacion.
- Escalamiento.
- Dashboard operativo.
- ML.
