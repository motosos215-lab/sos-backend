# Incidents API

## Descripcion

Incidents API registra incidentes operativos asociados a viajes de MotoSOS y permite consultarlos, cancelar falsos positivos y cerrarlos.

Este modulo no envia alertas reales, no envia push notifications, SMS, WhatsApp ni correo, no ejecuta escalamiento, no alimenta live monitoring, dashboard operativo ni Machine Learning en esta etapa.

Para Wear OS, el telefono Android es el gateway: puede combinar senales del smartwatch y del movil localmente, pero Incidents API recibe un incidente resumido usando la sesion del Rider. La API no administra pairing de smartwatch, QR, codigos, nodeId, Bluetooth, Wear OS Data Layer ni estado Connected/Disconnected del reloj.

## Endpoints

- `POST /api/v1/incidents`
- `GET /api/v1/incidents?status=&tripId=&pageNumber=&pageSize=`
- `GET /api/v1/incidents/{id}`
- `POST /api/v1/incidents/{id}/cancel-false-positive`
- `POST /api/v1/incidents/{id}/close`

Todos requieren JWT Bearer y solo permiten `Rider`.

## Contrato De Identidad E Idempotencia

- El `Rider` se obtiene exclusivamente del Bearer JWT.
- El body no acepta `userId`.
- `tripId` debe ser remoto y provenir de Trips API.
- `clientIncidentId` debe ser UUID y estable en reintentos del cliente.
- La evidencia puede indicar senales resumidas de telefono y smartwatch, pero no debe incluir nodeId ni estado remoto de pairing.
- No se confia en IDs locales como IDs globales.

Idempotency key oficial:

```text
userId + tripId + clientIncidentId
```

La API guarda un indice unico en `IdempotencyKey`. Si llega el mismo incidente otra vez, no duplica documento y responde el incidente existente como exito estable. No devuelve `409` para duplicados idempotentes.

## Crear Incidente

Request:

```json
{
  "tripId": "trip-id-remoto",
  "clientIncidentId": "ef4a5c5e-2a79-49d6-a9a1-88ff12345678",
  "source": "MobileDetection",
  "cause": "CountdownTimeout",
  "riskLevel": "High",
  "score": 87,
  "confidence": 0.91,
  "gpsQuality": "Good",
  "ruleSetVersion": "rules-v1",
  "validationPolicyVersion": "validation-v1",
  "occurredAtUtc": "2026-08-06T07:00:00Z",
  "location": {
    "latitude": 19.4326,
    "longitude": -99.1332,
    "accuracyMeters": 12.5,
    "speedKph": 42.3,
    "headingDegrees": 180
  },
  "evidenceSummary": {
    "accelerometerPeakG": 2.4,
    "gyroscopePeak": 1.7,
    "offlineRecordIds": ["offline-record-id"],
    "sensorWindowSeconds": 30,
    "phoneBatteryPercent": 82,
    "watchBatteryPercent": 70,
    "appVersion": "1.0.0",
    "deviceModel": "Android"
  }
}
```

Response:

```json
{
  "success": true,
  "data": {
    "incident": {
      "id": "incident-id-remoto",
      "tripId": "trip-id-remoto",
      "vehicleId": "vehicle-id-remoto",
      "mobileDeviceId": "mobile-device-id-remoto",
      "smartwatchDeviceId": "smartwatch-device-id-remoto",
      "source": "MobileDetection",
      "cause": "CountdownTimeout",
      "riskLevel": "High",
      "status": "Open",
      "score": 87,
      "confidence": 0.91,
      "occurredAtUtc": "2026-08-06T07:00:00+00:00",
      "createdAtUtc": "2026-08-06T07:00:02+00:00",
      "updatedAtUtc": null,
      "cancelledAtUtc": null,
      "closedAtUtc": null,
      "closureReason": null,
      "closureNotes": null
    }
  },
  "error": null
}
```

## Cancelar Falso Positivo

`POST /api/v1/incidents/{id}/cancel-false-positive`

```json
{
  "reason": "Estoy bien",
  "cancelledAtUtc": "2026-08-06T07:02:00Z"
}
```

Reglas:

- `Open -> FalsePositiveCancelled`.
- `FalsePositiveCancelled` devuelve el incidente actual.
- `Closed` devuelve `400 incident_already_closed`.

## Cerrar Incidente

`POST /api/v1/incidents/{id}/close`

```json
{
  "closureReason": "Resolved",
  "closureNotes": "Validado por el conductor",
  "closedAtUtc": "2026-08-06T07:05:00Z"
}
```

Reglas:

- `Open -> Closed`.
- `FalsePositiveCancelled -> Closed`.
- `Closed` devuelve el incidente actual.
- No hay borrado fisico de incidentes.

## Validaciones

- `tripId` requerido.
- `clientIncidentId` requerido y UUID.
- `source` requerido y permitido.
- `cause` requerido y permitido.
- `riskLevel` requerido y permitido.
- `score` entre 0 y 100 cuando se envia.
- `confidence` entre 0 y 1 cuando se envia.
- `gpsQuality`, `ruleSetVersion`, `validationPolicyVersion`, `appVersion` y `deviceModel` tienen longitud maxima.
- `occurredAtUtc` requerido.
- `offlineRecordIds` maximo 20.

Valores aceptados iniciales:

- `source`: `MobileDetection`, `ManualSos`, `OfflineIngestion`.
- `cause`: `CountdownTimeout`, `UserRequestedHelp`, `CriticalEvent`, `ManualSos`, `Unknown`.
- `riskLevel`: `Unknown`, `Low`, `Medium`, `High`.
- `status`: `Open`, `FalsePositiveCancelled`, `Closed`.

## Reglas De Negocio

- Requiere onboarding completo: `completedSteps = 7`, `currentStep = Completed` e `isOperational = true`.
- `tripId` debe existir y pertenecer al usuario autenticado.
- `tripId` puede estar `Active` o `Finished` para sincronizacion tardia.
- `VehicleId`, `MobileDeviceId` y `SmartwatchDeviceId` se derivan desde el viaje.
- Recursos ajenos devuelven `404 not_found`.
- Nuevos incidentes se crean con `Status = Open`.
- Solo el owner puede consultar, cancelar o cerrar sus incidentes.

## Errores Esperados

- `401 unauthorized`: sin token o token invalido.
- `403 forbidden`: usuario no Rider.
- `400 validation_error`: request invalido.
- `400 onboarding_not_ready`: onboarding incompleto o no operacional.
- `400 incident_already_closed`: intento de cancelar falso positivo sobre incidente cerrado.
- `404 not_found`: trip o incidente inexistente o ajeno.

## Seguridad

- No devuelve `passwordHash`.
- No devuelve `accessToken` ni `refreshToken`.
- No devuelve `deviceIdentifier` ni `deviceIdentifierHash`.
- No devuelve datos de pago, Google Play, Stripe ni proveedores externos.
- No acepta `userId` desde el body.

## MongoDB

Coleccion: `incidents`.

Indices:

- `UserId`
- `TripId`
- `UserId + Status`
- `ClientIncidentId`
- `IdempotencyKey` unico
- `OccurredAtUtc`
- `CreatedAtUtc`
- `ClosedAtUtc`

## Ejemplo curl

```bash
curl -X POST "$BASE_URL/api/v1/incidents" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "tripId": "trip-id-remoto",
    "clientIncidentId": "ef4a5c5e-2a79-49d6-a9a1-88ff12345678",
    "source": "MobileDetection",
    "cause": "CountdownTimeout",
    "riskLevel": "High",
    "score": 87,
    "confidence": 0.91,
    "gpsQuality": "Good",
    "ruleSetVersion": "rules-v1",
    "validationPolicyVersion": "validation-v1",
    "occurredAtUtc": "2026-08-06T07:00:00Z"
  }'
```

## Pendientes Futuros

- Alert Dispatch API real.
- Notifications push, SMS, WhatsApp y correo.
- Escalamiento operativo.
- Live Monitoring.
- Dashboard operativo.
- Machine Learning.
- Processor real de Offline Ingestion.
- Sensor batches completos.
