# Audit Log Retention Policy

La politica de retencion de audit logs permite ejecutar limpieza manual y controlada sobre la coleccion `auditLogs`.

## Estado Actual

- Endpoints Admin-only bajo `/api/v1/admin/audit-logs/retention`.
- Politica definida por codigo, sin configuracion en `appsettings`.
- `dryRun` es el modo seguro por defecto.
- La ejecucion real requiere confirmacion explicita.
- Las ejecuciones se guardan en `auditLogRetentionRuns`.
- No existe worker automatico ni borrado programado.

## Politica

| Campo | Valor |
| --- | --- |
| `retentionDaysDefault` | `180` |
| `minimumRetentionDays` | `90` |
| `maximumRetentionDays` | `3650` |
| `dryRunDefault` | `true` |
| `deleteRequiresConfirmation` | `true` |
| `automaticWorkerEnabled` | `false` |

El cutoff se calcula como:

```text
cutoffUtc = now - retentionDays
```

El borrado real elimina solo audit logs con:

```text
CreatedAtUtc < cutoffUtc
```

Los logs recientes y los logs exactamente en el cutoff se conservan.

## Endpoints

### Consultar Politica

```http
GET /api/v1/admin/audit-logs/retention/policy
Authorization: Bearer {adminToken}
```

### Ejecutar Retencion

```http
POST /api/v1/admin/audit-logs/retention/run
Authorization: Bearer {adminToken}
Content-Type: application/json
```

Body dry-run default:

```json
{}
```

Body delete confirmado:

```json
{
  "retentionDays": 180,
  "dryRun": false,
  "confirmPermanentDelete": true
}
```

Si `dryRun = false` y `confirmPermanentDelete` no es `true`, la API devuelve `validation_error` y no borra documentos.

### Listar Runs

```http
GET /api/v1/admin/audit-logs/retention/runs?pageNumber=1&pageSize=50
Authorization: Bearer {adminToken}
```

### Obtener Run Por Id

```http
GET /api/v1/admin/audit-logs/retention/runs/{id}
Authorization: Bearer {adminToken}
```

## Seguridad

- Sin token devuelve `401`.
- `Rider` devuelve `403`.
- `Monitor` devuelve `403`.
- `Admin` puede consultar politica, ejecutar y leer runs.
- No se expone metadata completa de audit logs.
- No se guardan payloads ni datos sensibles en `auditLogRetentionRuns`.
- La auditoria de la ejecucion es best-effort.
- Si falla escribir auditoria, la operacion principal no se rompe.

## Auditoria

Acciones agregadas:

- `AuditLogRetentionDryRunCompleted`
- `AuditLogRetentionDeleteCompleted`
- `AuditLogRetentionFailed`

Metadata permitida:

- `retentionRunId`
- `mode`
- `retentionDays`
- `cutoffUtc`
- `candidateCount`
- `deletedCount`
- `status`

## Fuera De Alcance

No implementa tareas automaticas, borrado programado, almacenamiento externo, export automatico, compresion, objetos externos, URLs firmadas, correo, proveedores reales, SDKs externos, secretos ni cobros.
