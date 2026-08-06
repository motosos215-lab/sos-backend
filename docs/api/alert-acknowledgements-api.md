# Alert Acknowledgements API

## Descripcion

Alert Acknowledgements API registra la respuesta de un contacto/monitor ante una alerta preparada. Permite consultar alertas asignadas, marcar vista, confirmar apoyo o declinar.

No implementa live monitoring, mapa en tiempo real, streaming de ubicacion, chat, llamadas, escalamiento, proveedores reales ni envio real de notificaciones.

## Relacion Con Notifications Y Alert Dispatch

- Los endpoints de monitor usan `notificationDeliveryAttemptId` como `id` de ruta.
- El intento de notificacion determina `AlertDispatchId`, `IncidentId`, `TripId`, `EmergencyContactId` y Rider propietario.
- La asignacion al Monitor se valida con `EmergencyContact.LinkedUserId == monitorUserId`.
- Se crea un solo `AlertAcknowledgement` por `monitorUserId + notificationDeliveryAttemptId`.

## Endpoints Monitor

- `GET /api/v1/monitor/alerts`
- `GET /api/v1/monitor/alerts/{notificationDeliveryAttemptId}`
- `POST /api/v1/monitor/alerts/{notificationDeliveryAttemptId}/view`
- `POST /api/v1/monitor/alerts/{notificationDeliveryAttemptId}/acknowledge`
- `POST /api/v1/monitor/alerts/{notificationDeliveryAttemptId}/decline`

Solo permiten `Monitor`.

## Endpoint Rider

- `GET /api/v1/rider/alerts/acknowledgements?alertDispatchId=&incidentId=&status=&pageNumber=&pageSize=`

Solo permite `Rider` y lista acknowledgements donde `UserId` es el Rider autenticado.

## Idempotencia

Documento unico:

```text
monitorUserId + notificationDeliveryAttemptId
```

El indice unico `IdempotencyKey` evita duplicados. Las acciones repetidas son estables por transicion de estado.

## Estados

- `Pending`
- `Viewed`
- `Acknowledged`
- `Declined`

## Transiciones

View:

- `Pending -> Viewed`
- `Viewed`, `Acknowledged` o `Declined` devuelven estado actual.

Acknowledge:

- `Pending` o `Viewed -> Acknowledged`
- `Acknowledged` devuelve estado actual.
- `Declined` devuelve `acknowledgement_already_declined`.

Decline:

- `Pending` o `Viewed -> Declined`
- `Declined` devuelve estado actual.
- `Acknowledged` devuelve `acknowledgement_already_confirmed`.

## Requests

`POST /api/v1/monitor/alerts/{id}/acknowledge`

```json
{
  "responseType": "CanAssist",
  "message": "Estoy al pendiente y puedo apoyar."
}
```

`POST /api/v1/monitor/alerts/{id}/decline`

```json
{
  "responseType": "CannotAssist",
  "message": "No puedo atender en este momento."
}
```

## Errores Esperados

- `401 unauthorized`: sin token o token invalido.
- `403 forbidden`: rol no permitido.
- `400 validation_error`: request invalido.
- `400 acknowledgement_already_declined`: intento de confirmar despues de declinar.
- `400 acknowledgement_already_confirmed`: intento de declinar despues de confirmar.
- `400 acknowledgement_not_allowed`: operacion no permitida.
- `404 not_found`: alerta inexistente, no asignada o sin contacto vinculado.

## Seguridad

- No acepta `userId` ni `monitorUserId` en body.
- No expone password hashes, tokens ni identificadores de dispositivo.
- No expone datos de pago ni proveedores externos.
- Monitor solo ve intentos asociados a contactos con `LinkedUserId` igual a su usuario.
- Rider solo consulta acknowledgements de sus propias alertas.

## MongoDB

Coleccion: `alertAcknowledgements`.

Indices:

- `UserId`
- `MonitorUserId`
- `EmergencyContactId`
- `AlertDispatchId`
- `NotificationDeliveryAttemptId`
- `IncidentId`
- `TripId`
- `Status`
- `MonitorUserId + Status`
- `UserId + Status`
- `IdempotencyKey` unico
- `CreatedAtUtc`
- `ViewedAtUtc`
- `AcknowledgedAtUtc`
- `DeclinedAtUtc`

## Ejemplo curl

```bash
curl -X POST "$BASE_URL/api/v1/monitor/alerts/$NOTIFICATION_DELIVERY_ATTEMPT_ID/acknowledge" \
  -H "Authorization: Bearer $MONITOR_ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "responseType": "CanAssist",
    "message": "Estoy al pendiente y puedo apoyar."
  }'
```

## Pendientes Futuros

- Live Monitoring.
- Mapa en tiempo real.
- Streaming de ubicacion.
- Chat.
- Llamadas.
- Proveedores reales de notificacion.
- Escalamiento.
- Dashboard operativo.
- Machine Learning.
