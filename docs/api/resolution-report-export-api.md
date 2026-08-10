# Resolution Report Export API

Resolution Report Export API genera una vista JSON estructurada y exportable del reporte final de una emergencia.

## Alcance

- Export real soportado: `Json`.
- Metadata minima persistida en `resolutionReportExports`.
- El response se genera con datos actuales.
- La metadata del export se actualiza de forma idempotente.
- No se guarda el JSON completo del reporte.

## Fuera De Alcance

No genera PDF real, no descarga binarios, no guarda archivos fisicos, no usa storage externo, no genera URLs firmadas, no envia email, no llama proveedores externos, no ejecuta OCR, no implementa ML real, no implementa tracking en vivo, no implementa mapa en tiempo real, no agrega SDKs externos, no implementa pagos ni pairing API de smartwatch.

## Endpoints

- `GET /api/v1/rider/emergencies/{incidentId}/resolution-report/export`
- `GET /api/v1/admin/emergencies/{incidentId}/resolution-report/export`
- `GET /api/v1/monitor/alerts/{notificationDeliveryAttemptId}/resolution-report/export`
- `GET /api/v1/admin/resolution-report-exports`

Query param:

- `exportType`: opcional, default `Json`. Cualquier otro valor devuelve `validation_error`.

## Permisos

- Rider exporta solo reportes propios.
- Admin exporta cualquier reporte y lista metadata de exports.
- Monitor exporta solo si esta asignado mediante contacto vinculado y delivery attempt relacionado.
- Si no se puede comprobar asignacion de Monitor, se devuelve `404`.

## Secciones Del JSON

- Header.
- IncidentSummary.
- TripSummary.
- AlertSummary.
- AcknowledgementSummary.
- LocationSummary.
- ResolutionSummary.
- EscalationSummary.
- TelemetrySummary.
- EvidenceSummary.
- AuditSummary.

## Seguridad

No devuelve bytes, base64, contenido de archivo, rutas completas, polyline, tracking, historial de ubicaciones, lista completa de MinorEvents, metadata completa de evidencias, metadata completa de auditoria, referencias locales de cliente, storage object keys ni URLs firmadas.

EvidenceSummary solo incluye metadata segura: id, target type, evidence type, source, status, file name, content type, size, hash declarado, rol registrador y fechas relevantes.

AuditSummary solo incluye conteo total y ultima accion relevante con fecha. No incluye metadata completa.

## Idempotencia

La metadata usa:

```text
userId + emergencyResolutionReportId + exportType
```

Exportar dos veces no duplica metadata. La vista JSON se genera con datos actuales y la metadata se actualiza con `GeneratedAtUtc` y `UpdatedAtUtc`.

## Criterio De AlertDispatch

Si el reporte contiene `AlertDispatchId`, se usa ese alert dispatch. Si no existe o no aplica, se toma el ultimo alert dispatch relacionado al incidente por `CreatedAtUtc`.

## Pendientes Futuros

- PDF real.
- Descarga binaria.
- Plantillas.
- Firma digital.
- Almacenamiento de export completo.
- URLs firmadas.
- Envio por email.
- Export con evidencias descargables.
