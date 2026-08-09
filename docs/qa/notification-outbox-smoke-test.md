# Notification Outbox Smoke Test

## Objetivo

Validar que Notification Outbox procesa delivery attempts simulados y que sus cambios se reflejan en Emergency Status y Operational Dashboard sin crear efectos secundarios operativos.

## Precondiciones

- API MotoSOS levantada en entorno de QA o local.
- MongoDB configurado para el entorno de prueba.
- Usuario `Admin` disponible.
- Usuario `Rider` con onboarding operativo.
- Emergencia abierta con `AlertDispatch` y `NotificationDeliveryAttempts` existentes.
- Attempts preparados para cubrir al menos estos estados: `Prepared`, `Cancelled`, `SimulatedSent` y `Failed`.

## Flujo Recomendado

1. Autenticarse como `Admin` y guardar el bearer token.
2. Autenticarse como `Rider` y guardar el bearer token.
3. Consultar estado inicial de outbox.
4. Ejecutar outbox con `simulateFailures = false`.
5. Verificar que attempts `Prepared` pasan a `SimulatedSent`.
6. Crear o dejar disponible otro attempt `Prepared`.
7. Ejecutar outbox con `simulateFailures = true`.
8. Verificar que ese attempt pasa a `Failed` con reason `simulated_failure_requested`.
9. Ejecutar `retry-failed`.
10. Verificar que attempts `Failed` regresan a `Prepared`.
11. Consultar Emergency Status del Rider.
12. Consultar Operational Dashboard summary como Admin.

## Endpoints En Orden

```bash
curl -X GET "$BASE_URL/api/v1/admin/notifications/outbox/status" \
  -H "Authorization: Bearer $ADMIN_TOKEN"
```

```bash
curl -X POST "$BASE_URL/api/v1/admin/notifications/outbox/run" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{ "maxItems": 20, "simulateFailures": false }'
```

```bash
curl -X POST "$BASE_URL/api/v1/admin/notifications/outbox/run" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{ "maxItems": 20, "simulateFailures": true }'
```

```bash
curl -X POST "$BASE_URL/api/v1/admin/notifications/outbox/retry-failed" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{ "maxItems": 20 }'
```

```bash
curl -X GET "$BASE_URL/api/v1/rider/emergencies/$INCIDENT_ID/status" \
  -H "Authorization: Bearer $RIDER_TOKEN"
```

```bash
curl -X GET "$BASE_URL/api/v1/admin/dashboard/summary" \
  -H "Authorization: Bearer $ADMIN_TOKEN"
```

## Resultado Esperado

- `run` con `simulateFailures = false` marca attempts `Prepared` como `SimulatedSent`.
- `run` con `simulateFailures = true` marca attempts `Prepared` como `Failed`.
- La razon de falla simulada es `simulated_failure_requested`.
- `retry-failed` regresa attempts `Failed` a `Prepared`.
- `Cancelled` no se reprocesa.
- `SimulatedSent` no se reprocesa.
- `Prepared` no se altera por `retry-failed`.
- Emergency Status refleja correctamente `notifications.total`, `prepared`, `simulatedSent`, `failed` y `cancelled`.
- Operational Dashboard refleja los conteos globales de notifications.
- No se crean acknowledgements automaticamente.
- No se crean reportes de resolucion automaticamente.
- No se modifica el incidente.
- No se modifica el alert dispatch.

## Casos Negativos

- Sin token: `401 unauthorized`.
- `Rider` en endpoints admin: `403 forbidden`.
- `Monitor` en endpoints admin: `403 forbidden`.
- `maxItems = 0`: `400 validation_error`.
- `maxItems > 100`: `400 validation_error`.

## Confirmaciones De Seguridad

- Las respuestas no deben exponer hashes de credenciales.
- Las respuestas no deben exponer tokens de sesion.
- Las respuestas no deben exponer identificadores de dispositivo.
- Las respuestas no deben exponer tokens de proveedores.
- Las respuestas no deben exponer payloads completos.
- Las respuestas no deben exponer telefonos o correos completos.
- Las respuestas no deben exponer stack traces ni errores internos de MongoDB.

## Fuera De Alcance

- Proveedores externos reales.
- Worker real o scheduler.
- Realtime o streaming.
- Tracking en vivo o mapas en tiempo real.
- ML.
- Pagos.
- Pairing API de smartwatch.
