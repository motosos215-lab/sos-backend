# Notification Outbox Worker

Notification Outbox Worker procesa automaticamente `NotificationDeliveryAttempts` en estado `Prepared` usando la misma logica segura del Notification Outbox manual.

Push Notification Tokens API permite que attempts `Push` se resuelvan hacia el Monitor vinculado. Si FCM esta habilitado y configurado, el worker puede procesar Push con FCM. Si Email provider esta habilitado y configurado, el worker puede procesar Email con SMTP/Brevo. Si SMS provider esta habilitado y configurado, el worker puede procesar SMS con Brevo SMS.

Flujo final de produccion: `POST /api/v1/mobile/sos-alerts` crea incidente, alert dispatch y attempts `Prepared`; el worker procesa despues esos attempts. El endpoint SOS no ejecuta outbox ni envia notificaciones directamente.

## Estado Actual

- Implementado como `BackgroundService` nativo de .NET.
- Registrado como hosted service, pero deshabilitado por defecto.
- Usa `IServiceScopeFactory` para resolver servicios scoped durante cada ejecucion.
- Usa `NotificationOutboxService` y `NotificationProviderResolver`.
- `Push` usa FCM si `Notifications:Providers:Fcm:Enabled = true`; si no, usa provider simulado.
- `Email` usa SMTP/Brevo si `Notifications:Providers:Email:Enabled = true`; si no, usa provider simulado.
- `Sms` usa Brevo SMS si `Notifications:Providers:Sms:Enabled = true`; si no, usa provider simulado.
- No envia mensajeria instantanea real.
- FCM requiere configuracion segura por variables de entorno y no expone credenciales.

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

## Configuracion DigitalOcean

El worker se configura desde la seccion exacta `Notifications:OutboxWorker`. En DigitalOcean App Platform usar variables de entorno con doble guion bajo:

```text
Notifications__OutboxWorker__Enabled=true
Notifications__OutboxWorker__IntervalSeconds=30
Notifications__OutboxWorker__MaxItemsPerRun=20
Notifications__OutboxWorker__SimulateFailures=false
Notifications__OutboxWorker__RunOnStartup=true
```

Para Push real tambien se requiere FCM habilitado y configurado en `Notifications:Providers:Fcm`. Para Email real se requiere SMTP habilitado y configurado en `Notifications:Providers:Email`. Para SMS real se requiere Brevo habilitado y configurado en `Notifications:Providers:Sms`. Si un provider no esta habilitado, usa provider simulado.

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

Distributed lock queda pendiente futuro para despliegues con multiples replicas. Con una sola replica DigitalOcean se puede habilitar el worker; con multiples replicas puede haber procesamiento duplicado antes del update final aunque el cambio de estado sea atomico por estado esperado. Para multiples replicas se requiere un distributed lock futuro.

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

- `enabled`
- `running`
- `intervalSeconds`
- `maxItemsPerRun`
- `simulateFailures`
- `runOnStartup`
- `lastRunStartedAtUtc`
- `lastRunCompletedAtUtc`
- `lastProcessedCount`
- `lastFailedCount`
- `lastError`

No expone tokens, `tokenHash`, `tokenValue`, payloads, emails, telefonos, credenciales FCM, service accounts, connection strings, stack traces ni errores internos.

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

No implementa mensajeria instantanea real, scheduler externo, tiempo real, mapa en vivo, modelos predictivos, cobros ni pairing API de smartwatch.
