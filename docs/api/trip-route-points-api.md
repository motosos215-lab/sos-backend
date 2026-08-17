# Trip Route Points API

## Descripcion

Trip Route Points API persiste el recorrido real de un viaje con puntos GPS capturados por Android durante el trayecto.

Android envia puntos reales al backend. Google Maps solo debe dibujar una Polyline usando esos puntos reales. El backend no calcula rutas con Google, no guarda encoded polyline y no llama servicios externos de mapas.

## Endpoints

- `POST /api/v1/trips/{tripId}/route-points/batch`
- `GET /api/v1/trips/{tripId}/route`

Ambos endpoints requieren JWT Bearer y son solo para `Rider`.

`Monitor` y `Admin` reciben `403 forbidden`. Si el viaje no existe o pertenece a otro Rider, se devuelve `404 not_found` para no exponer existencia de viajes ajenos.

## Configuracion

- `Trips__RoutePoints__OfflineSyncGraceHours=24`
- `Trips__RoutePoints__MaxBatchSize=500`
- `Trips__RoutePoints__PreviewMaxPoints=50`

## POST Batch

Request:

```json
{
  "points": [
    {
      "clientRoutePointId": "11111111-1111-1111-1111-111111111111",
      "sequence": 1,
      "recordedAtUtc": "2026-08-16T18:00:00Z",
      "latitude": 19.4326,
      "longitude": -99.1332,
      "accuracyMeters": 12.5,
      "speedMetersPerSecond": 8.4,
      "bearingDegrees": 180.0
    }
  ]
}
```

Validaciones:

- `points` requerido y no vacio.
- Maximo configurable, default `500` puntos.
- `clientRoutePointId` requerido, UUID valido y unico dentro del batch.
- `sequence >= 1`.
- `latitude` entre `-90` y `90`.
- `longitude` entre `-180` y `180`.
- `accuracyMeters` entre `0` y `5000`.
- `speedMetersPerSecond` opcional entre `0` y `120`.
- `bearingDegrees` opcional entre `0` y `360`.
- `recordedAtUtc` requerido y no demasiado en el futuro.

Si hay errores estructurales, se rechaza todo el batch con `validation_error`.

## Idempotencia

La idempotencia persistente es por `tripId + clientRoutePointId`.

- Si no existe, se crea y devuelve `Accepted`.
- Si ya existe con los mismos datos relevantes, devuelve `Duplicate` y no crea otro registro.
- Si ya existe con datos diferentes, devuelve `Conflict` para ese punto y no sobrescribe.

## Offline Sync

Para viajes `Active`, se aceptan puntos desde `StartedAtUtc` hasta `now + 5 minutos`.

Para viajes `Finished`, Android puede sincronizar offline hasta `FinishedAtUtc + OfflineSyncGraceHours`. No se aceptan puntos posteriores a `FinishedAtUtc + 5 minutos`.

Los puntos se conservan despues de finalizar el viaje.

## GET Route

Ejemplos:

- `GET /api/v1/trips/{tripId}/route`
- `GET /api/v1/trips/{tripId}/route?mode=preview`
- `GET /api/v1/trips/{tripId}/route?mode=preview&maxPoints=50`

`mode=full` devuelve puntos ordenados por `sequence` ascendente. `mode=preview` reduce puntos con downsampling uniforme simple y conserva el primer y ultimo punto.

## Persistencia

Los puntos se guardan en la coleccion separada `tripRoutePoints`, no dentro del documento `Trip`.

Indices:

- `ux_tripRoutePoints_tripId_clientRoutePointId`, unico.
- `ix_tripRoutePoints_tripId_sequence`.
- `ix_tripRoutePoints_userId_tripId`.
- `ix_tripRoutePoints_tripId_recordedAtUtc`.

## Android

Android debe capturar GPS durante el viaje, enviar lotes con `clientRoutePointId` estable para reintentos y dibujar la Polyline con los puntos devueltos por `GET route`.
