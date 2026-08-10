# Notification Outbox API

Notification Outbox API procesa de forma controlada los `NotificationDeliveryAttempts` existentes. Es un outbox simulado para ambientes de desarrollo y validacion operativa.

## Alcance

- Solo trabaja sobre attempts existentes.
- No crea colecciones nuevas.
- No envia mensajes reales.
- No llama proveedores externos.
- El worker automatico existe, pero queda deshabilitado por defecto y no reemplaza los endpoints manuales.
- No modifica incidentes, dispatches, acknowledgements, reportes de resolucion ni ubicaciones.
- Usa `NotificationProviderResolver` y `SimulatedNotificationProvider` como abstraccion interna.

## Permisos

- Requiere JWT Bearer.
- Solo `Admin` puede ejecutar o consultar.
- `Rider` y `Monitor` reciben `403 forbidden`.
- Sin token devuelve `401 unauthorized`.
- No acepta `userId` externo.

## Endpoints

### `POST /api/v1/admin/notifications/outbox/run`

Request:

```json
{
  "maxItems": 20,
  "simulateFailures": false
}
```

Reglas:

- `maxItems` default `20`, minimo `1`, maximo `100`.
- `simulateFailures` default `false`.
- Si `simulateFailures = false`, los attempts `Prepared` pasan a `SimulatedSent`.
- Si `simulateFailures = true`, todos los attempts seleccionados pasan a `Failed` con reason `simulated_failure_requested`.
- Los cambios usan actualizacion atomica por `Id` y estado esperado.
- Attempts en `Cancelled`, `Failed` o `SimulatedSent` no se procesan en `run`.
- El procesamiento pasa por el proveedor simulado interno; no cambia la response publica.

### `GET /api/v1/admin/notifications/outbox/status`

Devuelve conteos globales por estado:

- `prepared`
- `simulatedSent`
- `failed`
- `cancelled`

### `GET /api/v1/admin/notifications/outbox/worker/status`

Devuelve estado seguro del worker automatico.

Reglas:

- Solo `Admin`.
- Solo lectura.
- No dispara procesamiento.
- No modifica configuracion.
- El worker queda deshabilitado por defecto.

### `POST /api/v1/admin/notifications/outbox/retry-failed`

Request:

```json
{
  "maxItems": 20
}
```

Reglas:

- Toma attempts `Failed`.
- Los regresa a `Prepared` mediante actualizacion atomica por `Id` y estado esperado.
- Limpia reason y timestamp de falla.
- No procesa `Cancelled` ni `SimulatedSent`.
- No envia nada; el siguiente `run` procesa los attempts preparados.

## Provider Abstraction

Notification Outbox usa una abstraccion interna de proveedor para desacoplar el procesamiento simulado. En esta etapa el resolver siempre devuelve `SimulatedNotificationProvider` para `Sms`, `Email` y `Push`.

No se agregan proveedores reales, SDKs externos, secretos ni configuracion sensible.

## Seguridad

Las respuestas no exponen identificadores de usuario, correos, telefonos, hashes de credenciales, tokens de sesion, identificadores de dispositivo, tokens de proveedores, payloads completos, stack traces, errores internos de MongoDB ni datos de cobro.

## Pendiente Futuro

- Distributed lock para despliegues con multiples replicas.
- Cola real.
- Reintentos programados.
- Integraciones reales de mensajeria.
- Escalamiento automatico.
- Completar `AlertDispatch` cuando todos los attempts esten en estado terminal.
- Indice global por `Status` si el volumen de attempts crece.
