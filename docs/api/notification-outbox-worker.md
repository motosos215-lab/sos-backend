# Notification Outbox Worker

Notification Outbox Worker procesa automaticamente `NotificationDeliveryAttempts` en estado `Prepared` usando la misma logica segura del Notification Outbox manual.

## Estado Actual

- Implementado como `BackgroundService` nativo de .NET.
- Registrado como hosted service, pero deshabilitado por defecto.
- Usa `IServiceScopeFactory` para resolver servicios scoped durante cada ejecucion.
- Usa `NotificationOutboxService` y `SimulatedNotificationProvider`.
- No envia mensajes reales.
- No usa proveedores reales ni SDKs externos.
- No realiza I/O externo de mensajeria.
- No requiere configuracion sensible.

## Defaults

Defaults por codigo:

| Opcion | Default |
| --- | --- |
| `Enabled` | `false` |
| `IntervalSeconds` | `60` |
| `MaxItemsPerRun` | `20` |
| `SimulateFailures` | `false` |
| `RunOnStartup` | `false` |

Con `Enabled = false`, el worker no procesa nada aunque este registrado como hosted service.

## Procesamiento

Cuando esta habilitado:

- Busca attempts `Prepared`.
- Respeta `MaxItemsPerRun`.
- Si `SimulateFailures = false`, aplica `Prepared -> SimulatedSent`.
- Si `SimulateFailures = true`, aplica `Prepared -> Failed` con razon controlada.
- No reprocesa `Cancelled`.
- No reprocesa `Failed`.
- No reprocesa `SimulatedSent`.
- No crea acknowledgements.
- No crea reportes de resolucion.
- No modifica incidentes.
- No modifica alert dispatches.
- No crea escalaciones.
- Puede ser prerequisito para Automatic Escalation Worker porque este requiere attempts `SimulatedSent` antiguos.
- No crea eventos menores.

Los endpoints manuales siguen existiendo para QA y operacion:

- `POST /api/v1/admin/notifications/outbox/run`
- `GET /api/v1/admin/notifications/outbox/status`
- `POST /api/v1/admin/notifications/outbox/retry-failed`

## Concurrencia

El worker evita ejecuciones simultaneas dentro de la misma instancia usando control en memoria.

Si una ejecucion sigue activa y llega otra, la segunda se marca como skipped interno y no procesa attempts.

Distributed lock queda pendiente futuro para despliegues con multiples replicas.

## Estado Seguro

Endpoint Admin-only:

```http
GET /api/v1/admin/notifications/outbox/worker/status
```

Reglas:

- Sin token devuelve `401`.
- `Rider` y `Monitor` devuelven `403`.
- `Admin` recibe `200`.
- Solo lectura.
- No cambia configuracion.
- No dispara procesamiento.

Respuesta segura:

- `isEnabled`
- `isRunning`
- `lastRunStartedAtUtc`
- `lastRunCompletedAtUtc`
- `lastRunSucceeded`
- `lastRunProcessed`
- `lastRunSimulatedSent`
- `lastRunFailed`
- `lastRunSkipped`
- `lastErrorCode`
- `lastErrorMessage`

No expone payloads, datos de contacto completos, credenciales, tokens, stack traces ni errores internos.

## Errores

Los errores durante una corrida se capturan como fallo controlado:

- El estado interno se actualiza con codigo y mensaje seguro.
- La API no se detiene permanentemente.
- El worker continua en el siguiente intervalo.
- Si auditoria falla, no se rompe la ejecucion principal.

## Auditoria

Acciones best-effort:

- `NotificationOutboxWorkerRun`
- `NotificationOutboxWorkerFailed`
- `NotificationOutboxWorkerSkipped`

Metadata permitida:

- `processed`
- `simulatedSent`
- `failed`
- `skipped`
- `maxItems`
- `simulateFailures`
- `intervalSeconds`
- `runSource = Worker`

## Fuera De Alcance

No implementa proveedores reales, llamadas externas, mensajeria real, scheduler externo, tiempo real, mapa en vivo, modelos predictivos, cobros ni pairing API de smartwatch.
