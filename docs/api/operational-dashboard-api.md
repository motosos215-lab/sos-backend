# Operational Dashboard API

Operational Dashboard API expone metricas administrativas agregadas para MotoSOS. Es backend-only, solo lectura y no crea datos nuevos.

## Permisos

- Requiere JWT Bearer.
- Solo usuarios `Admin` pueden consultar.
- `Rider` y `Monitor` reciben `403 forbidden`.
- Sin token devuelve `401 unauthorized`.
- No acepta `userId` externo.

## Fuentes

- Users
- Onboarding confirmations
- Trips
- Incidents
- Alert Dispatch
- Notification Delivery Attempts
- Alert Acknowledgements
- Emergency Resolution Reports
- Offline Ingestion

## Endpoints

### `GET /api/v1/admin/dashboard/summary`

Devuelve conteos globales por usuarios, onboarding, viajes, incidentes, alertas, notificaciones, acknowledgements, reportes de resolucion y procesamiento offline.

### `GET /api/v1/admin/dashboard/incidents`

Query params:

- `dateFrom`: opcional, ISO UTC.
- `dateTo`: opcional, ISO UTC.
- `status`: opcional, enum `IncidentStatus` exacto.
- `pageNumber`: default `1`.
- `pageSize`: default `20`, maximo `100`.

Los filtros de fecha aplican sobre `CreatedAtUtc`. La lista se ordena por `CreatedAtUtc` descendente.

### `GET /api/v1/admin/dashboard/response-times`

Query params:

- `dateFrom`: opcional, ISO UTC.
- `dateTo`: opcional, ISO UTC.

Usa Emergency Resolution Reports. `totalReports` cuenta todos los reportes filtrados. `reportsWithResponseTime` cuenta solo reportes con tiempo de respuesta. Average/min/max ignoran valores null y devuelven null cuando no hay tiempos disponibles.

### `GET /api/v1/admin/dashboard/resolution-outcomes`

Query params:

- `dateFrom`: opcional, ISO UTC.
- `dateTo`: opcional, ISO UTC.

Agrupa Emergency Resolution Reports por `Outcome`. Si no hay datos devuelve `items: []`.

### `GET /api/v1/admin/dashboard/offline-processing`

Devuelve conteos globales por estado de procesamiento offline. No expone payloads.

## Errores Esperados

- `401 unauthorized`: token ausente o invalido.
- `403 forbidden`: rol distinto de `Admin`.
- `400 validation_error`: fechas invalidas, `dateFrom > dateTo`, `status` invalido, paginacion fuera de rango.

## Seguridad

Las respuestas no exponen identificadores de usuario, correos, telefonos, hashes de credenciales, tokens de sesion, identificadores de dispositivo, tokens de proveedores, payloads offline completos, stack traces, errores internos de MongoDB ni datos de cobro.

## No Implementado En Esta Etapa

- Frontend de dashboard.
- Graficas frontend.
- Exportacion PDF.
- Analitica avanzada.
- ML.
- Metricas por zona o por ventanas complejas de tiempo.
- Realtime, streaming o monitoreo en vivo.
- Proveedores reales.
- Pagos.
- Pairing API de smartwatch.

## Pendiente Futuro

Si el volumen crece, las metricas de response times y outcomes pueden optimizarse con aggregation pipeline de MongoDB.
