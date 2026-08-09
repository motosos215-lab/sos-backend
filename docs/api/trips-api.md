# Trips API

## Descripcion

Trips API implementa el primer modulo operativo despues del onboarding web-first completo de MotoSOS.

El flujo general pasa de configurar y vincular a operar. Trips permite que la app movil inicie, consulte y finalice un viaje remoto. El backend genera el `tripId` remoto que despues sera usado por eventos, sensores, incidentes, SOS, alertas, monitoreo y analitica.

Este modulo no implementa todavia ingesta offline, minor events, sensor batches, incidents, SOS, alert dispatch, push notifications, live monitoring, dashboard operativo, Machine Learning ni pagos.

## Reglas

- Todos los endpoints requieren JWT Bearer.
- Solo usuarios `Rider` pueden usar Trips API. `Conductor` del maquetado se guarda como `Rider`.
- `Monitor` y `Admin` reciben `403 forbidden`.
- Todo opera con el `userId` del JWT.
- No se puede consultar, listar, iniciar ni finalizar viajes de otro usuario.
- Para iniciar viaje, onboarding debe estar completo: `completedSteps = 7`, `currentStep = Completed`, `isOperational = true`.
- El vehiculo debe ser propio, activo y `CompletionStatus = Completed`.
- El `MobileApp` debe ser propio, activo, `Linked` y tipo `MobileApp`.
- Decision vigente Wear OS: el smartwatch se vincula localmente con Android mediante Wear OS Data Layer. El telefono es el gateway de viaje hacia la API.
- La API no administra pairing, QR, codigos, nodeId, Bluetooth ni estado Connected/Disconnected del reloj.
- `Smartwatch` remoto asociado al viaje queda como compatibilidad historica; nuevas implementaciones deben iniciar viajes desde el telefono usando `mobileDeviceId`.
- Solo puede existir un viaje `Active` por usuario.
- `POST /api/v1/trips/start` es idempotente si ya existe un viaje activo con el mismo `vehicleId` y `mobileDeviceId`.
- Si ya existe un viaje activo con datos distintos, devuelve `active_trip_exists`.
- `POST /api/v1/trips/{id}/finish` es idempotente si el viaje ya esta `Finished`.
- Los viajes no se borran fisicamente.

## Riesgo De Concurrencia

En esta etapa no se usa indice unico parcial ni control atomico fuerte para garantizar un unico viaje activo ante dos requests simultaneos extremos. La regla se valida a nivel servicio y pruebas secuenciales.

Mejora futura: usar operacion atomica o indice parcial para garantizar un unico `Active` por usuario tambien bajo carrera concurrente.

## Endpoints

### GET /api/v1/trips/active

Devuelve el viaje activo del usuario autenticado o `trip = null`.

Response sin viaje activo:

```json
{
  "success": true,
  "data": {
    "trip": null
  },
  "error": null
}
```

Response con viaje activo:

```json
{
  "success": true,
  "data": {
    "trip": {
      "id": "string",
      "userId": "string",
      "vehicleId": "vehicle-id",
      "mobileDeviceId": "mobile-device-id",
      "smartwatchDeviceId": null,
      "status": "Active",
      "startedAtUtc": "2026-08-06T06:00:00+00:00",
      "finishedAtUtc": null,
      "clientStartedAtUtc": "2026-08-06T06:00:00+00:00",
      "clientFinishedAtUtc": null,
      "startLocation": {
        "latitude": 19.2826,
        "longitude": -99.6557,
        "accuracyMeters": 12.5,
        "provider": "gps",
        "recordedAtUtc": "2026-08-06T06:00:00+00:00"
      },
      "endLocation": null,
      "startBatteryLevel": 87,
      "endBatteryLevel": null,
      "appVersion": "1.0.0",
      "notes": null,
      "createdAtUtc": "2026-08-06T06:00:00+00:00",
      "updatedAtUtc": "2026-08-06T06:00:00+00:00"
    }
  },
  "error": null
}
```

### POST /api/v1/trips/start

Uso principal: app movil autenticada.

Request:

```json
{
  "vehicleId": "vehicle-id",
  "mobileDeviceId": "mobile-device-id",
  "smartwatchDeviceId": null,
  "clientStartedAtUtc": "2026-08-06T06:00:00Z",
  "startLocation": {
    "latitude": 19.2826,
    "longitude": -99.6557,
    "accuracyMeters": 12.5,
    "provider": "gps",
    "recordedAtUtc": "2026-08-06T06:00:00Z"
  },
  "batteryLevel": 87,
  "appVersion": "1.0.0"
}
```

Response:

```json
{
  "success": true,
  "data": {
    "trip": {
      "id": "string",
      "userId": "string",
      "vehicleId": "vehicle-id",
      "mobileDeviceId": "mobile-device-id",
      "smartwatchDeviceId": null,
      "status": "Active",
      "startedAtUtc": "2026-08-06T06:00:00+00:00",
      "finishedAtUtc": null
    }
  },
  "error": null
}
```

Validaciones:

- `vehicleId` requerido.
- `mobileDeviceId` requerido.
- `startLocation` opcional.
- Si se envia `startLocation`, `latitude` debe estar entre -90 y 90.
- Si se envia `startLocation`, `longitude` debe estar entre -180 y 180.
- `accuracyMeters >= 0`.
- `batteryLevel` opcional entre 0 y 100.
- `appVersion` opcional maximo 50 caracteres.

### POST /api/v1/trips/{id}/finish

Uso principal: app movil autenticada.

Request:

```json
{
  "clientFinishedAtUtc": "2026-08-06T06:30:00Z",
  "endLocation": {
    "latitude": 19.285,
    "longitude": -99.66,
    "accuracyMeters": 10,
    "provider": "gps",
    "recordedAtUtc": "2026-08-06T06:30:00Z"
  },
  "batteryLevel": 75,
  "notes": "Viaje finalizado manualmente"
}
```

Response:

```json
{
  "success": true,
  "data": {
    "trip": {
      "id": "string",
      "status": "Finished",
      "finishedAtUtc": "2026-08-06T06:30:00+00:00",
      "endBatteryLevel": 75,
      "notes": "Viaje finalizado manualmente"
    }
  },
  "error": null
}
```

`finish` no crea incidentes, alertas ni notificaciones.

### GET /api/v1/trips/{id}

Devuelve solo un viaje propio. Si el viaje no existe o pertenece a otro usuario, devuelve `404 not_found`.

### GET /api/v1/trips

Query params:

- `status`: opcional, `Active` o `Finished`.
- `pageNumber`: opcional, default `1`.
- `pageSize`: opcional, default `20`, maximo `100`.

Response:

```json
{
  "success": true,
  "data": {
    "trips": [],
    "pageNumber": 1,
    "pageSize": 20,
    "totalCount": 0
  },
  "error": null
}
```

## Errores Esperados

- `401 unauthorized`: sin token o token invalido.
- `403 forbidden`: usuario no Rider.
- `400 validation_error`: payload invalido.
- `400 onboarding_not_ready`: onboarding no completo.
- `400 trip_not_ready`: vehiculo o dispositivo no listo para viaje.
- `404 not_found`: viaje inexistente o ajeno.
- `409 active_trip_exists`: ya existe viaje activo con datos distintos.

## Seguridad

- No devuelve `passwordHash`.
- No devuelve refresh tokens.
- No devuelve `deviceIdentifier` ni `deviceIdentifierHash`.
- No devuelve datos de pago, Google Play, Stripe ni proveedores externos.
- No permite usar vehiculos o dispositivos de otro usuario.
- No permite consultar ni finalizar viajes de otro usuario.

## Ejemplos curl

```bash
curl -X GET "$BASE_URL/api/v1/trips/active" \
  -H "Authorization: Bearer $ACCESS_TOKEN"
```

```bash
curl -X POST "$BASE_URL/api/v1/trips/start" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "vehicleId": "vehicle-id",
    "mobileDeviceId": "mobile-device-id",
    "clientStartedAtUtc": "2026-08-06T06:00:00Z",
    "startLocation": {
      "latitude": 19.2826,
      "longitude": -99.6557,
      "accuracyMeters": 12.5,
      "provider": "gps",
      "recordedAtUtc": "2026-08-06T06:00:00Z"
    },
    "batteryLevel": 87,
    "appVersion": "1.0.0"
  }'
```

```bash
curl -X POST "$BASE_URL/api/v1/trips/{id}/finish" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "clientFinishedAtUtc": "2026-08-06T06:30:00Z",
    "batteryLevel": 75,
    "notes": "Viaje finalizado manualmente"
  }'
```

## MongoDB

Coleccion: `trips`.

Indices:

- `UserId`.
- `UserId + Status`.
- `VehicleId`.
- `MobileDeviceId`.
- `StartedAtUtc`.
- `FinishedAtUtc`.

## Faltantes Futuros

- Offline ingestion.
- Minor events.
- Sensor batches.
- Incidents.
- SOS.
- Alerts.
- Notifications.
- Live Monitoring.
- Dashboard operativo.
- Machine Learning.
