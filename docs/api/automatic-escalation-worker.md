# Automatic Escalation Worker

Automatic Escalation Worker crea `EmergencyEscalation` automaticas cuando una alerta simulada no recibe acknowledgement suficiente despues de un umbral configurado.

## Estado Actual

- Implementado como `BackgroundService` nativo de .NET.
- Registrado como hosted service, pero deshabilitado por defecto.
- Usa `IServiceScopeFactory` para resolver servicios scoped en cada corrida.
- Crea solo documentos `EmergencyEscalation` internos.
- No envia mensajes reales.
- No llama servicios externos.
- No usa proveedores reales ni SDKs externos.
- No requiere configuracion sensible.

## Defaults

Defaults por codigo:

| Opcion | Default |
| --- | --- |
| `Enabled` | `false` |
| `IntervalSeconds` | `60` |
| `MaxItemsPerRun` | `20` |
| `EscalateAfterSeconds` | `300` |
| `RunOnStartup` | `false` |

Con `Enabled = false`, el worker no procesa nada aunque este registrado como hosted service.

## Criterios De Escalamiento

Solo crea escalation automatica cuando:

- Existe el `AlertDispatchRequest`.
- El alert dispatch esta en `PendingDispatch`.
- Existe el incidente asociado.
- El incidente pertenece al Rider del alert dispatch.
- El incidente esta `Open`.
- No existe `EmergencyEscalation` para ese alert dispatch.
- No existe `AlertAcknowledgement` con `Acknowledged`.
- Existe al menos un `NotificationDeliveryAttempt`.
- Existe al menos un attempt `SimulatedSent`.
- El `SimulatedSentAtUtc` mas antiguo ya supero `EscalateAfterSeconds`.
- No hay otra corrida activa en la misma instancia.

El worker puede usar `AlertDispatch.RequestedAtUtc` como filtro inicial, pero la decision final se toma con el `SimulatedSentAtUtc` mas antiguo. No escala solo porque el alert dispatch sea viejo.

## Resultado Creado

La escalation automatica usa:

- `Reason = NoAcknowledgement`
- `Level = Level1`
- `Status = Requested`
- `RequestedByUserId = automatic-escalation-worker`
- `RequestedByRole = System`

La idempotencia sigue siendo `userId + alertDispatchId`. Si ya existe escalation, no se duplica y se cuenta como `alreadyEscalated`.

## Endpoints Admin

Status seguro:

```http
GET /api/v1/admin/escalations/worker/status
```

Run manual para QA/admin:

```http
POST /api/v1/admin/escalations/worker/run
```

Request:

```json
{
  "maxItems": 20,
  "escalateAfterSeconds": 300
}
```

Reglas:

- Sin token devuelve `401`.
- `Rider` y `Monitor` devuelven `403`.
- `Admin` recibe `200`.
- `run` manual no activa el worker permanente.
- `run` manual no cambia `Enabled`.
- `maxItems` default `20`, minimo `1`, maximo `100`.
- `escalateAfterSeconds` default `300`, minimo `60`.

## No Modifica

El worker no modifica:

- `Incident`
- `AlertDispatchRequest`
- `NotificationDeliveryAttempt`
- `AlertAcknowledgement`
- `EmergencyResolutionReport`

Tampoco crea notification attempts, acknowledgements ni reportes de resolucion.

## Concurrencia

Evita ejecuciones simultaneas dentro de la misma instancia con control en memoria. Si llega otra corrida mientras una sigue activa, se marca skipped interno.

Distributed lock queda pendiente futuro para multiples replicas.

## Estado Seguro

El status expone solo:

- `isEnabled`
- `isRunning`
- `lastRunStartedAtUtc`
- `lastRunCompletedAtUtc`
- `lastRunSucceeded`
- `lastRunProcessed`
- `lastRunEscalated`
- `lastRunSkipped`
- `lastRunAlreadyEscalated`
- `lastRunAlreadyAcknowledged`
- `lastRunNotReady`
- `lastRunFailed`
- `lastErrorCode`
- `lastErrorMessage`

No expone payloads, datos de contacto completos, credenciales, tokens, stack traces ni errores internos.

## Auditoria

Acciones best-effort:

- `AutomaticEscalationWorkerRun`
- `AutomaticEscalationWorkerFailed`
- `AutomaticEscalationWorkerSkipped`
- `EmergencyEscalationAutomaticallyRequested`

Metadata permitida:

- `processed`
- `escalated`
- `skipped`
- `alreadyEscalated`
- `alreadyAcknowledged`
- `notReady`
- `failed`
- `maxItems`
- `escalateAfterSeconds`
- `runSource = Worker`
- `escalationId`
- `incidentId`
- `alertDispatchId`
- `reason`
- `level`

## Fuera De Alcance

No implementa llamadas reales a servicios de emergencia, mensajeria real, proveedores externos, scheduler externo, tiempo real, mapa en vivo, modelos predictivos, cobros ni pairing API de smartwatch.
