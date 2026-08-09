# Minor Events API

Minor Events API registra eventos menores del viaje que no representan una emergencia confirmada. El objetivo es conservar senales utiles para historial, analisis futuro y reglas operativas posteriores sin crear incidentes ni alertas.

## Estado Actual

- Coleccion MongoDB: `minorEvents`.
- Idempotencia: `userId + tripId + clientEventId + eventType`.
- Indice unico por `IdempotencyKey`.
- Permite viajes `Active` o `Finished`.
- No crea emergencias confirmadas, alertas, intentos de aviso, acknowledgements, escalations ni reportes finales.
- Offline Processing procesa `minor-event` y lo convierte en `MinorEvent` cuando el payload es valido.

## Tipos

`eventType` acepta:

- `HardBrake`
- `HarshAcceleration`
- `SharpTurn`
- `GpsSignalLost`
- `GpsSignalRecovered`
- `LowBattery`
- `SmartwatchDisconnected`
- `SmartwatchReconnected`
- `SensorAnomaly`
- `PossibleFallLowConfidence`
- `TripSignalWeak`
- `Informational`

`severity` acepta `Info`, `Low` o `Medium`.

`source` acepta `MobileApp`, `Smartwatch`, `OfflineIngestion` o `System`.

`status` puede ser `Recorded`, `Reviewed` o `Ignored`.

## Endpoints

- `POST /api/v1/mobile/minor-events`
- `GET /api/v1/rider/minor-events`
- `GET /api/v1/rider/minor-events/{id}`
- `POST /api/v1/rider/minor-events/{id}/mark-reviewed`
- `POST /api/v1/rider/minor-events/{id}/ignore`
- `GET /api/v1/admin/minor-events`

## Crear Minor Event

```http
POST /api/v1/mobile/minor-events
Authorization: Bearer {riderAccessToken}
```

```json
{
  "tripId": "trip-id",
  "clientEventId": "mobile-event-001",
  "eventType": "HardBrake",
  "severity": "Low",
  "source": "MobileApp",
  "mobileDeviceId": "mobile-device-id",
  "smartwatchDeviceId": null,
  "score": 42.5,
  "confidence": 0.72,
  "gpsQuality": "Good",
  "latitude": 19.2826,
  "longitude": -99.6557,
  "speedKmh": 45.2,
  "batteryLevel": 82,
  "message": "Hard brake detected by mobile sensors.",
  "occurredAtUtc": "2026-08-08T18:30:00Z",
  "metadata": {
    "sensor": "accelerometer",
    "sampleWindow": "3s"
  }
}
```

Reglas:

- Solo `Rider` puede crear.
- `Monitor` y `Admin` reciben `403 forbidden`.
- Sin token devuelve `401 unauthorized`.
- `tripId` debe existir, pertenecer al Rider y estar `Active` o `Finished`.
- `clientEventId` es requerido.
- Crear dos veces el mismo evento devuelve el mismo registro.
- `occurredAtUtc` no puede estar mas de 2 minutos en el futuro.
- `confidence` debe estar entre `0` y `1`.
- `score`, si viene informado, debe estar entre `0` y `100`.
- `message` admite maximo `1000` caracteres.

Respuesta:

```json
{
  "success": true,
  "data": {
    "minorEvent": {
      "id": "minor-event-id",
      "tripId": "trip-id",
      "eventType": "HardBrake",
      "severity": "Low",
      "source": "MobileApp",
      "status": "Recorded",
      "score": 42.5,
      "confidence": 0.72,
      "gpsQuality": "Good",
      "latitude": 19.2826,
      "longitude": -99.6557,
      "speedKmh": 45.2,
      "batteryLevel": 82,
      "message": "Hard brake detected by mobile sensors.",
      "occurredAtUtc": "2026-08-08T18:30:00Z",
      "receivedAtUtc": "2026-08-08T18:31:00Z",
      "createdAtUtc": "2026-08-08T18:31:00Z",
      "updatedAtUtc": "2026-08-08T18:31:00Z",
      "processedFromOfflineIngestionRecordId": null,
      "metadata": {
        "sensor": "accelerometer",
        "sampleWindow": "3s"
      }
    }
  },
  "error": null
}
```

## Consultas Rider

```http
GET /api/v1/rider/minor-events?tripId=trip-id&eventType=HardBrake&pageNumber=1&pageSize=20
Authorization: Bearer {riderAccessToken}
```

Filtros:

| Param | Tipo | Reglas |
| --- | --- | --- |
| `tripId` | string | Opcional. |
| `eventType` | enum | Opcional. |
| `severity` | enum | Opcional. |
| `status` | enum | Opcional. |
| `dateFrom` | ISO UTC | Opcional. |
| `dateTo` | ISO UTC | Opcional. |
| `pageNumber` | int | Default `1`, minimo `1`. |
| `pageSize` | int | Default `20`, maximo `100`. |

El orden es `occurredAtUtc desc`. El Rider solo ve eventos propios.

## Cambios De Estado

```http
POST /api/v1/rider/minor-events/{id}/mark-reviewed
POST /api/v1/rider/minor-events/{id}/ignore
```

Ambas operaciones son idempotentes. No modifican el viaje ni crean incidentes o alertas.

## Admin List

```http
GET /api/v1/admin/minor-events?userId=user-id&status=Recorded&pageNumber=1&pageSize=20
Authorization: Bearer {adminAccessToken}
```

Admin list es solo lectura. `Rider` y `Monitor` reciben `403 forbidden`.

## Offline Processing

Offline Processing acepta payload minimo de `minor-event`:

```json
{
  "tripId": "trip-id",
  "clientEventId": "mobile-event-001",
  "eventType": "HardBrake",
  "severity": "Low",
  "source": "OfflineIngestion",
  "score": 42.5,
  "confidence": 0.72,
  "occurredAtUtc": "2026-08-08T18:30:00Z"
}
```

Si faltan `tripId`, `clientEventId`, `mobileDeviceId` u `occurredAtUtc`, se usan fallbacks seguros desde `OfflineIngestionRecord` cuando existen. Si el payload no es valido, el registro offline queda como fallo permanente controlado y el batch continua procesando otros items.

## Metadata

`metadata` se guarda como pares string/string seguros. Se eliminan claves sensibles de credenciales, tokens, identificadores de dispositivo, datos de contacto, datos de cobro, payloads completos, trazas, errores internos y detalles de persistencia. Los valores se truncan a `200` caracteres.

## Auditoria

Acciones auditadas best-effort:

- `MinorEventRecorded`
- `MinorEventMarkedReviewed`
- `MinorEventIgnored`
- `MinorEventProcessedFromOfflineIngestion`

Si falla la auditoria, la operacion principal no se rompe.

## Fuera De Alcance

No agrega proveedores reales, mensajeria real, worker real, servicios en segundo plano, realtime, tracking en vivo, mapa en tiempo real, IA real, predicciones, cobros ni pairing API de smartwatch.

## Pendientes Futuros

- Reglas automaticas controladas.
- Dashboard de eventos menores.
- Analitica agregada.
- Modelos predictivos cuando exista aprobacion.
- Correlacion con incidentes reales.
- Mapas historicos agregados.
- Telemetria resumida avanzada.
