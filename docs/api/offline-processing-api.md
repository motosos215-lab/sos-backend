# Offline Processing / Event Processor API

## Descripcion

Offline Processing API procesa registros ya recibidos por Offline Ingestion API y los convierte en entidades reales cuando aplica.

Este modulo no es un worker real de produccion. No implementa Hangfire, Quartz, cron externo, Azure Functions, WebSockets, SignalR, streaming, push real, SMS real, WhatsApp real, correo real, Twilio, SendGrid, FCM, escalamiento automatico, ML, dashboard operativo completo, pagos ni pairing API de smartwatch.

## Relacion Con Offline Ingestion

Offline Ingestion persiste items offline y devuelve ACK durable. Offline Processing toma registros `PendingProcessing` de `offlineIngestionRecords`, los marca atomicamente como `Processing` y luego los finaliza como `Processed`, `Ignored` o `FailedPermanent`.

No devuelve payload completo en responses.

## Tipos Soportados

- `local-incident`: crea o recupera un Incident usando idempotencia `userId + tripId + clientIncidentId`.
- `alert-dispatch-request`: crea o recupera AlertDispatch usando idempotencia `userId + incidentId + clientAlertRequestId`.
- `location-update`: actualiza el ultimo snapshot de Location Sharing por `UserId + IncidentId`.
- `minor-event`: queda `Ignored` y se muestra como `Skipped` con reason `minor_event_processing_not_implemented`.

## Idempotencia

- Procesar dos veces no duplica Incident.
- Procesar dos veces no duplica AlertDispatch.
- Procesar dos veces no duplica LocationSnapshot.
- Registros ya terminales no se reprocesan porque solo se listan `PendingProcessing`.
- `TryMarkProcessingAsync` usa actualizacion atomica con filtro `Id + UserId + PendingProcessing`.

Para `local-incident`, `clientIncidentId` se resuelve en este orden:

- `payload.clientIncidentId` si viene informado y es valido.
- `OfflineIngestionRecord.ClientEventId` si no viene `payload.clientIncidentId`.
- Si ninguno es valido, el record queda `FailedPermanent` con error controlado.

No se genera un GUID nuevo en backend para este fallback.

## Endpoints

### POST /api/v1/offline-processing/run

Requiere JWT Bearer. Solo `Rider`.

Request:

```json
{
  "maxItems": 20
}
```

Reglas:

- `maxItems` default `20`.
- Minimo `1`.
- Maximo `100`.
- Procesa solo registros propios del Rider autenticado.
- `Monitor` y `Admin` reciben `403`.

Response:

```json
{
  "success": true,
  "data": {
    "processed": 3,
    "skipped": 1,
    "failed": 0,
    "items": [
      {
        "offlineRecordId": "record-id",
        "type": "local-incident",
        "status": "Processed",
        "remoteRecordId": "incident-id",
        "reason": null,
        "errorCode": null
      }
    ]
  },
  "error": null
}
```

### GET /api/v1/offline-processing/status

Requiere JWT Bearer. Solo `Rider`.

Response:

```json
{
  "success": true,
  "data": {
    "pending": 2,
    "processing": 0,
    "processed": 10,
    "failed": 1,
    "skipped": 3
  },
  "error": null
}
```

## Seguridad

- `userId` solo desde JWT.
- No acepta `userId` en body.
- Rider solo procesa registros propios.
- Monitor y Admin reciben `403`.
- No devuelve payload completo.
- No devuelve tokens.
- No devuelve device identifiers.
- No devuelve stack traces.
- No expone errores internos de MongoDB.
- No envia notificaciones reales.

## Concurrencia

Mongo usa claim atomico con filtro `Id + UserId + PendingProcessing`. Esto evita que dos ejecuciones procesen el mismo registro al mismo tiempo.

Riesgo pendiente: si un proceso se interrumpe despues de marcar un registro como `Processing`, puede quedar pendiente definir recuperacion de registros `Processing` antiguos.

## Errores Esperados

- `401 unauthorized`.
- `403 forbidden`.
- `400 validation_error`.
- `400 offline_record_not_ready`.
- `400 offline_processing_failed`.

## Pendientes Futuros

- Worker real.
- Cola real.
- Reintentos programados.
- Recuperacion de registros `Processing` antiguos.
- Minor Events API.
- Sensor batches completos.
- Analytics / ML.
