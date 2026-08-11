# Offline Processing / Event Processor API

## Descripcion

Offline Processing API procesa registros ya recibidos por Offline Ingestion API y los convierte en entidades reales cuando aplica.

El modulo incluye un worker automatico nativo de .NET, deshabilitado por defecto. No implementa Hangfire, Quartz, cron externo, Azure Functions, WebSockets, SignalR, streaming, push real, SMS real, WhatsApp real, correo real, Twilio, SendGrid, FCM, escalamiento automatico, ML, dashboard operativo completo, pagos ni pairing API de smartwatch.

## Relacion Con Offline Ingestion

Offline Ingestion persiste items offline y devuelve ACK durable. Offline Processing toma registros `PendingProcessing` de `offlineIngestionRecords`, los marca atomicamente como `Processing` y luego los finaliza como `Processed`, `Ignored` o `FailedPermanent`.

Flujo Android offline:

1. Android guarda eventos localmente mientras no hay conexion.
2. Al recuperar conexion, envia `POST /api/v1/mobile/offline-ingestion/batch`.
3. Backend persiste cada item y responde ACK durable.
4. Offline Processing Worker procesa automaticamente registros `PendingProcessing` si esta habilitado.
5. Rider puede consultar su status scoped con `GET /api/v1/offline-processing/status`.
6. Admin puede consultar status global seguro del worker con `GET /api/v1/admin/offline-processing/worker/status`.

No devuelve payload completo en responses.

## Tipos Soportados

- `local-incident`: crea o recupera un Incident usando idempotencia `userId + tripId + clientIncidentId`.
- `alert-dispatch-request`: crea o recupera AlertDispatch usando idempotencia `userId + incidentId + clientAlertRequestId`.
- `location-update`: actualiza el ultimo snapshot de Location Sharing por `UserId + IncidentId`.
- `minor-event`: crea o recupera MinorEvent usando idempotencia del modulo de Minor Events.

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

Este endpoint se mantiene scoped al Rider autenticado. No devuelve configuracion global del worker ni conteos globales.

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

### GET /api/v1/admin/offline-processing/worker/status

Requiere JWT Bearer. Solo `Admin`.

Devuelve configuracion efectiva segura y conteos globales sin payloads ni datos sensibles:

```json
{
  "success": true,
  "data": {
    "workerEnabled": true,
    "workerRunning": false,
    "intervalSeconds": 30,
    "maxItemsPerRun": 20,
    "runOnStartup": true,
    "recoveryMinutes": 10,
    "pendingCount": 0,
    "processingCount": 0,
    "processedCount": 0,
    "failedCount": 0,
    "lastRunStartedAtUtc": null,
    "lastRunCompletedAtUtc": null,
    "lastProcessedCount": 0,
    "lastFailedCount": 0,
    "lastRecoveredCount": 0,
    "lastError": null
  },
  "error": null
}
```

## Worker Automatico

Se configura desde la seccion exacta `Mobile:OfflineProcessingWorker`. En DigitalOcean App Platform usar:

```text
Mobile__OfflineProcessingWorker__Enabled=true
Mobile__OfflineProcessingWorker__IntervalSeconds=30
Mobile__OfflineProcessingWorker__MaxItemsPerRun=20
Mobile__OfflineProcessingWorker__RunOnStartup=true
Mobile__OfflineProcessingWorker__RecoveryMinutes=10
```

Defaults por codigo:

| Opcion | Default |
| --- | --- |
| `Enabled` | `false` |
| `IntervalSeconds` | `60` |
| `MaxItemsPerRun` | `20` |
| `RunOnStartup` | `false` |
| `RecoveryMinutes` | `10` |

El worker procesa globalmente registros de distintos Riders, pero no expone payloads ni datos personales en status.

## Recovery

Antes de procesar, el worker recupera registros `Processing` antiguos cuyo `ProcessingStartedAtUtc` sea menor o igual a `now - RecoveryMinutes`. Si `ProcessingStartedAtUtc` no existe, usa `UpdatedAtUtc` como fallback. Los registros recuperados vuelven a `PendingProcessing` y pueden ser reclamados otra vez por el claim atomico normal.

La recuperacion depende de la idempotencia de Incidents, Alert Dispatch, Location Sharing y Minor Events. Si un proceso muy lento sigue vivo despues de `RecoveryMinutes`, puede haber doble intento antes del cierre terminal; por eso `RecoveryMinutes` debe ser conservador.

## Seguridad

- `userId` solo desde JWT.
- No acepta `userId` en body.
- Rider solo procesa registros propios.
- Monitor y Admin reciben `403`.
- No devuelve payload completo.
- No devuelve tokens.
- No devuelve credenciales ni connection strings.
- No devuelve device identifiers.
- No devuelve stack traces.
- No expone errores internos de MongoDB.
- No envia notificaciones reales.

## Concurrencia

Mongo usa claim atomico con filtro `Id + UserId + PendingProcessing`. Esto evita que dos ejecuciones reclamen el mismo registro `PendingProcessing` al mismo tiempo.

Con una sola replica DigitalOcean se puede habilitar el worker. Con multiples replicas puede haber doble procesamiento antes de cerrar estado, especialmente durante recovery. Para produccion multi-replica se requiere distributed lock futuro.

## Errores Esperados

- `401 unauthorized`.
- `403 forbidden`.
- `400 validation_error`.
- `400 offline_record_not_ready`.
- `400 offline_processing_failed`.

## Pendientes Futuros

- Cola real.
- Reintentos programados.
- Distributed lock para multiples replicas.
- Sensor batches completos.
- Analytics / ML.
