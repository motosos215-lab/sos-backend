# Telemetry Summary API

Telemetry Summary API genera y consulta resumenes agregados de senales de viaje usando datos existentes de `MinorEvents`.

## Estado Actual

- Coleccion MongoDB: `tripTelemetrySummaries`.
- Un documento maximo por `UserId + TripId`.
- Recompute actualiza el mismo documento.
- Rider puede consultar y recomputar resumenes de viajes propios.
- Admin puede consultar resumenes persistidos de forma global.
- Monitor no tiene acceso en esta etapa.
- Usa datos agregados de `MinorEvents`.
- No guarda ruta completa.
- No guarda coordenadas.
- No guarda lista de eventos.
- No guarda metadata ni mensajes de eventos.
- No hace monitoreo en vivo.
- No implementa modelos predictivos reales.

## Endpoints Rider

### Obtener Summary

```http
GET /api/v1/rider/trips/{tripId}/telemetry-summary
Authorization: Bearer {riderAccessToken}
```

Reglas:

- Solo `Rider`.
- El viaje debe existir y pertenecer al Rider autenticado.
- Si no existe summary persistido, se calcula on-demand y se persiste.
- Viaje ajeno devuelve `404`.
- Sin token devuelve `401`.
- `Monitor` y `Admin` reciben `403` en endpoint Rider.

### Recompute

```http
POST /api/v1/rider/trips/{tripId}/telemetry-summary/recompute
Authorization: Bearer {riderAccessToken}
```

Reglas:

- Recalcula desde `MinorEvents` actuales.
- Mantiene un solo documento por `UserId + TripId`.
- Conserva `createdAtUtc` si ya existia.
- Actualiza `updatedAtUtc` y `lastComputedAtUtc`.
- No modifica `Trip`.
- No modifica `MinorEvents`.
- No crea incidentes, alertas, notificaciones, acknowledgements, escalations ni reportes.

## Endpoints Admin

### Listar Summaries

```http
GET /api/v1/admin/telemetry-summaries
Authorization: Bearer {adminAccessToken}
```

Query params:

| Param | Tipo | Reglas |
| --- | --- | --- |
| `userId` | string | Opcional. Filtro, no identidad autenticada. |
| `tripId` | string | Opcional. |
| `tripStatus` | enum | Opcional: `Active`, `Finished`. |
| `summaryStatus` | enum | Opcional: `Computed`, `NoData`, `Stale`. |
| `dateFrom` | ISO UTC | Opcional. Aplica a `lastComputedAtUtc`. |
| `dateTo` | ISO UTC | Opcional. Aplica a `lastComputedAtUtc`. |
| `pageNumber` | int | Default `1`, minimo `1`. |
| `pageSize` | int | Default `20`, minimo `1`, maximo `100`. |

Admin list es solo lectura y no recomputa resumenes.

### Obtener Por Id

```http
GET /api/v1/admin/telemetry-summaries/{id}
Authorization: Bearer {adminAccessToken}
```

Reglas:

- Solo `Admin`.
- Si no existe, devuelve `telemetry_summary_not_available` con `404`.
- No modifica registros.
- No recomputa.

## Response

La response contiene solo datos agregados:

- Conteos por tipo, severidad, fuente y estado.
- Conteos especificos por `MinorEventType`.
- Primera y ultima fecha de evento.
- Promedios y maximos calculados solo con valores disponibles.
- `gpsQualitySamples` como conteo agregado por calidad GPS.

No contiene coordenadas, ruta, polyline, lista completa de eventos, metadata, mensaje, payload completo ni datos sensibles.

## NoData

Si no hay `MinorEvents`:

- `summaryStatus = NoData`.
- `totalMinorEvents = 0`.
- Diccionarios vacios.
- Conteos especificos en cero.
- Fechas y metricas agregadas nullable en `null`.

## Computed

Si hay `MinorEvents`:

- `summaryStatus = Computed`.
- `totalMinorEvents` usa el conteo real.
- Los diccionarios se agrupan por enum string.
- Las metricas nullable se calculan solo con valores no null.

## Auditoria

Acciones best-effort:

- `TelemetrySummaryComputed`
- `TelemetrySummaryRecomputed`

Metadata permitida:

- `telemetrySummaryId`
- `tripId`
- `totalMinorEvents`
- `summaryStatus`
- `lastComputedAtUtc`

Si falla auditoria, no se rompe la operacion principal.

## Fuera De Alcance

No implementa monitoreo en vivo, mapa en tiempo real, ruta completa, polyline, streaming, modelos predictivos reales, score de riesgo real, cobros, proveedores reales ni pairing API de smartwatch.

## Pendientes Futuros

- Dashboard avanzado.
- Mapas historicos agregados sin ruta completa.
- Analitica avanzada.
- Modelos predictivos reales con aprobacion futura.
- Score de riesgo real.
- Correlacion con incidentes.
- Optimizacion con aggregation pipeline Mongo si el volumen crece.
