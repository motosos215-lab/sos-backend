# Offline Ingestion API

## Descripcion

Offline Ingestion API recibe elementos de la cola offline cifrada de la app movil MotoSOS y devuelve ACK durable cuando el backend ya persistio cada item.

Este modulo es receptor de datos operativos offline. No procesa incidentes reales, no crea SOS real, no envia alertas reales, no envia push notifications, SMS, WhatsApp ni correo, y no alimenta live monitoring, dashboard operativo ni Machine Learning en esta etapa.

Para Wear OS, la app Android actua como gateway: recibe datos del smartwatch mediante Wear OS Data Layer, combina senales localmente y envia a la API solo batches resumidos usando la sesion del Rider. La API no administra pairing, QR, codigos, nodeId, Bluetooth ni estado Connected/Disconnected del reloj.

## Endpoint

`POST /api/v1/mobile/offline-ingestion/batch`

Requiere JWT Bearer y solo permite `Rider`.

## Contrato De Identidad E Idempotencia

- El `Rider` se obtiene exclusivamente del Bearer JWT.
- El body no acepta `userId`.
- `mobileDeviceId` debe ser remoto y provenir de Devices API.
- `tripId` debe ser remoto y provenir de Trips API.
- El origen de datos del smartwatch, si existe, debe venir resumido en el payload del item; no se envia ni persiste nodeId.
- `batchId` debe ser UUID.
- `clientEventId` debe ser UUID por item.
- No se confia en event IDs locales como IDs globales.

Idempotency key oficial:

```text
userId + mobileDeviceId + tripId + item.type + item.clientEventId + item.payloadVersion
```

La API guarda un indice unico en `IdempotencyKey`. Si llega el mismo item otra vez, no duplica documento y responde `Duplicate` con el mismo `ackId` y `remoteRecordId`.

## ACK Durable

- `Accepted` solo se devuelve despues de persistir el record.
- `Duplicate` se devuelve como exito estable si el record ya existia.
- No se devuelve `409` para duplicados idempotentes.
- El envio movil se considera at-least-once.

## Request

```json
{
  "batchId": "6c76a9a5-7f2f-4f3a-88f7-9b3123456789",
  "mobileDeviceId": "mobile-device-id-remoto",
  "tripId": "trip-id-remoto",
  "schemaVersion": 1,
  "sentAtUtc": "2026-08-06T07:00:00Z",
  "appVersion": "1.0.0",
  "items": [
    {
      "clientEventId": "ef4a5c5e-2a79-49d6-a9a1-88ff12345678",
      "type": "minor-event",
      "occurredAtUtc": "2026-08-06T06:59:20Z",
      "payloadVersion": 1,
      "payload": {
        "type": "bump",
        "score": 35,
        "confidence": 0.82,
        "policyVersion": "rules-v1"
      }
    }
  ]
}
```

Tipos aceptados:

- `minor-event`
- `local-incident`
- `alert-dispatch-request`

## Response Accepted

```json
{
  "success": true,
  "data": {
    "batchId": "6c76a9a5-7f2f-4f3a-88f7-9b3123456789",
    "receivedAtUtc": "2026-08-06T07:00:02+00:00",
    "results": [
      {
        "clientEventId": "ef4a5c5e-2a79-49d6-a9a1-88ff12345678",
        "type": "minor-event",
        "status": "Accepted",
        "ackId": "ack-id-1",
        "remoteRecordId": "record-id-1",
        "isDuplicate": false
      }
    ]
  },
  "error": null
}
```

## Response Duplicate

```json
{
  "success": true,
  "data": {
    "batchId": "6c76a9a5-7f2f-4f3a-88f7-9b3123456789",
    "receivedAtUtc": "2026-08-06T07:05:00+00:00",
    "results": [
      {
        "clientEventId": "ef4a5c5e-2a79-49d6-a9a1-88ff12345678",
        "type": "minor-event",
        "status": "Duplicate",
        "ackId": "ack-id-1",
        "remoteRecordId": "record-id-1",
        "isDuplicate": true
      }
    ]
  },
  "error": null
}
```

## Validaciones

- `batchId` requerido y UUID.
- `mobileDeviceId` requerido.
- `tripId` requerido.
- `schemaVersion = 1`.
- `appVersion` maximo 50.
- `items` minimo 1 y maximo 10.
- `clientEventId` requerido y UUID.
- `type` debe ser permitido.
- `occurredAtUtc` requerido.
- `payloadVersion >= 1`.
- `payload` requerido y no vacio.
- `payload` maximo 32 KB por item.
- `Content-Length` maximo 256 KB cuando el cliente lo envia.

## Reglas De Negocio

- `mobileDeviceId` debe pertenecer al usuario autenticado.
- `mobileDeviceId` debe ser `MobileApp`, activo y `Linked`.
- `tripId` debe existir y pertenecer al usuario autenticado.
- `tripId` puede estar `Active` o `Finished` para sincronizacion tardia.
- Recursos ajenos devuelven `404 not_found`.
- Recursos propios no aptos devuelven `trip_not_ready`.
- Todo record nuevo queda `ProcessingStatus = PendingProcessing`.
- No se devuelve payload completo en responses.

## Errores Esperados

- `401 unauthorized`: sin token o token invalido.
- `403 forbidden`: usuario no Rider.
- `400 validation_error`: request invalido, tipo no permitido o payload invalido.
- `400 trip_not_ready`: dispositivo o viaje propio no apto para sincronizar.
- `404 not_found`: trip o dispositivo inexistente o ajeno.

## Seguridad

- No devuelve `passwordHash`.
- No devuelve `accessToken` ni `refreshToken`.
- No devuelve `deviceIdentifier` ni `deviceIdentifierHash`.
- No devuelve payload completo.
- No devuelve datos de pago, Google Play, Stripe ni proveedores externos.

## MongoDB

Coleccion: `offlineIngestionRecords`.

Indices:

- `UserId`
- `MobileDeviceId`
- `TripId`
- `BatchId`
- `ClientEventId`
- `Type`
- `IdempotencyKey` unico
- `AckId`
- `ProcessingStatus`
- `ReceivedAtUtc`
- `OccurredAtUtc`

## Ejemplo curl

```bash
curl -X POST "$BASE_URL/api/v1/mobile/offline-ingestion/batch" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "batchId": "6c76a9a5-7f2f-4f3a-88f7-9b3123456789",
    "mobileDeviceId": "mobile-device-id-remoto",
    "tripId": "trip-id-remoto",
    "schemaVersion": 1,
    "sentAtUtc": "2026-08-06T07:00:00Z",
    "appVersion": "1.0.0",
    "items": [
      {
        "clientEventId": "ef4a5c5e-2a79-49d6-a9a1-88ff12345678",
        "type": "minor-event",
        "occurredAtUtc": "2026-08-06T06:59:20Z",
        "payloadVersion": 1,
        "payload": { "type": "bump", "score": 35 }
      }
    ]
  }'
```

## Pendientes Futuros

- Processor real.
- Incidents API.
- Alert Dispatch API.
- Notifications.
- Live Monitoring.
- Dashboard operativo.
- ML.
- Sensor batches completos.
- Motor de reglas backend.
