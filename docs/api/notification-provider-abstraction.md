# Notification Provider Abstraction

Notification Provider Abstraction define una capa interna para procesar intentos de notificacion sin acoplar `NotificationOutboxService` a una implementacion concreta.

## Estado Actual

- Proveedor disponible: `SimulatedNotificationProvider`.
- Canales modelados: `Sms`, `Email` y `Push`.
- Resolver: `NotificationProviderResolver`.
- No agrega endpoints publicos.
- No cambia rutas ni responses de Notification Outbox.
- No envia mensajes reales.
- No usa SDKs externos.
- No requiere configuracion sensible.

## Componentes

- `INotificationProvider`
- `INotificationProviderResolver`
- `NotificationProviderRequest`
- `NotificationProviderResult`
- `NotificationProviderType`
- `NotificationProviderChannel`
- `NotificationProviderDeliveryStatus`
- `SimulatedNotificationProvider`
- `NotificationProviderResolver`

## Resolver

El resolver devuelve siempre `SimulatedNotificationProvider` para:

- `Sms`
- `Email`
- `Push`

Canales no soportados se manejan como fallo controlado durante el procesamiento del outbox.

## Simulated Provider

`SimulatedNotificationProvider` no realiza I/O externo.

Reglas:

- `simulateFailures = false` devuelve `Sent`.
- `simulateFailures = true` devuelve `Failed` con error controlado `simulated_failure_requested`.
- En exito genera `providerMessageId` seguro con formato `simulated-{guid}`.
- No guarda payloads completos, credenciales, tokens, datos de contacto completos ni errores internos.

## Integracion Con Outbox

Los endpoints existentes se mantienen:

- `POST /api/v1/admin/notifications/outbox/run`
- `GET /api/v1/admin/notifications/outbox/status`
- `POST /api/v1/admin/notifications/outbox/retry-failed`

El comportamiento se mantiene:

- `Prepared -> SimulatedSent` cuando `simulateFailures = false`.
- `Prepared -> Failed` cuando `simulateFailures = true`.
- `retry-failed` mantiene `Failed -> Prepared`.
- `run` no procesa `Cancelled`, `Failed` ni `SimulatedSent`.

## Auditoria

Acciones granularmente auditadas best-effort:

- `NotificationProviderSimulatedSent`
- `NotificationProviderSimulatedFailed`

Metadata segura:

- `notificationDeliveryAttemptId`
- `alertDispatchId`
- `incidentId`
- `channel`
- `providerType`
- `deliveryStatus`
- `providerMessageId`
- `errorCode`

Si falla auditoria, no se rompe la operacion principal.

## Seguridad

La abstraccion no expone ni persiste datos sensibles, configuracion sensible, payloads completos, credenciales, tokens, datos de contacto completos, datos de cobro, stack traces ni errores internos.

## Fuera De Alcance

No implementa proveedores reales, SDKs externos, servicios en segundo plano, colas reales, realtime, tracking en vivo, mapas en tiempo real, IA real, predicciones, cobros ni pairing API de smartwatch.

## Pendientes Futuros

- Integraciones reales previa aprobacion tecnica y operativa.
- Politicas de reintentos por proveedor.
- Plantillas de contenido.
- Circuit breakers.
- Observabilidad granular por proveedor.
- Configuracion segura para entornos reales.
