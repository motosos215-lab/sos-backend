# Notifications API

## Descripcion

Notifications API prepara y registra intentos de notificacion asociados a un `AlertDispatchRequest`. En esta etapa solo persiste trazabilidad de intentos y estados internos; no envia mensajes reales.

## Relacion Con Alert Dispatch

- Cada preparacion requiere un `alertDispatchId` existente y propio.
- El `AlertDispatch` debe estar en `PendingDispatch`.
- Los intentos se generan exclusivamente desde `ContactsSnapshot` guardado en Alert Dispatch.
- No se consultan contactos vivos para crear intentos, preservando trazabilidad del momento en que se preparo la alerta.

## Endpoints

- `POST /api/v1/notifications/delivery-attempts/prepare`
- `GET /api/v1/notifications/delivery-attempts?alertDispatchId=&incidentId=&status=&pageNumber=&pageSize=`
- `GET /api/v1/notifications/delivery-attempts/{id}`
- `POST /api/v1/notifications/delivery-attempts/{id}/mark-simulated-sent`
- `POST /api/v1/notifications/delivery-attempts/{id}/mark-failed`
- `POST /api/v1/notifications/delivery-attempts/{id}/cancel`

Todos requieren JWT Bearer y solo permiten `Rider`.

## Preparar Intentos

Request:

```json
{
  "alertDispatchId": "alert-dispatch-id",
  "notes": "Preparacion inicial de intentos de notificacion"
}
```

Response:

```json
{
  "success": true,
  "data": {
    "attempts": [
      {
        "id": "attempt-id",
        "alertDispatchId": "alert-dispatch-id",
        "incidentId": "incident-id",
        "tripId": "trip-id",
        "emergencyContactId": "contact-id",
        "contactFullName": "Contacto Uno",
        "contactRelationship": "Hermano",
        "contactPriority": 1,
        "channel": "Sms",
        "status": "Prepared",
        "provider": "None",
        "attemptNumber": 1,
        "preparedAtUtc": "2026-08-06T10:00:00+00:00",
        "simulatedSentAtUtc": null,
        "failedAtUtc": null,
        "cancelledAtUtc": null,
        "lastStatusChangedAtUtc": "2026-08-06T10:00:00+00:00",
        "failureReason": null,
        "notes": "Preparacion inicial de intentos de notificacion",
        "createdAtUtc": "2026-08-06T10:00:00+00:00",
        "updatedAtUtc": "2026-08-06T10:00:00+00:00"
      }
    ]
  },
  "error": null
}
```

## Seleccion De Canal

Se crea un solo intento por contacto del snapshot.

- `Sms` si el contacto tiene `phoneNumber`.
- `Email` si no tiene `phoneNumber` pero tiene `email`.
- Contactos sin `phoneNumber` ni `email` se omiten.
- Si no queda ningun intento para crear, se devuelve `notification_not_allowed`.
- Canales de app de contacto y mensajeria instantanea quedan pendientes.

## Idempotencia

Idempotency key oficial:

```text
userId + alertDispatchId + emergencyContactId + channel + attemptNumber
```

En esta etapa `attemptNumber = 1`. Existe indice unico en `IdempotencyKey`. Si se prepara dos veces el mismo alert dispatch, se devuelven los intentos existentes sin duplicar documentos y sin `409`.

## Estados

- `Prepared`: intento persistido, sin envio real.
- `SimulatedSent`: marcado manualmente como envio simulado para pruebas internas.
- `Failed`: marcado manualmente como falla simulada.
- `Cancelled`: cancelado logicamente.

## Transiciones

`mark-simulated-sent`:

- `Prepared -> SimulatedSent`.
- `provider = Simulated`.
- `SimulatedSent` devuelve estado actual.
- `Cancelled` o `Failed` devuelven `notification_not_allowed`.

`mark-failed`:

- `Prepared -> Failed`.
- `Failed` devuelve estado actual.
- `SimulatedSent` o `Cancelled` devuelven `notification_not_allowed`.

`cancel`:

- `Prepared -> Cancelled`.
- `Cancelled` devuelve estado actual.
- `SimulatedSent` o `Failed` devuelven `notification_not_allowed`.
- No hay borrado fisico.

## Errores Esperados

- `401 unauthorized`: sin token o token invalido.
- `403 forbidden`: usuario no Rider.
- `400 validation_error`: request invalido.
- `400 onboarding_not_ready`: onboarding incompleto o no operacional.
- `400 alert_dispatch_not_ready`: alert dispatch cancelado o no apto.
- `400 alert_dispatch_already_completed`: alert dispatch completado.
- `400 notification_not_allowed`: operacion o transicion no permitida.
- `404 not_found`: alert dispatch o intento inexistente o ajeno.

## Seguridad

- No acepta `userId` desde el body.
- No devuelve password hashes ni tokens.
- No devuelve identificadores de dispositivo hasheados.
- No devuelve tokens de proveedores.
- No integra proveedores reales.
- No envia push, SMS, correo ni mensajeria real.
- No devuelve datos de pagos ni proveedores externos.

## MongoDB

Coleccion: `notificationDeliveryAttempts`.

Indices:

- `UserId`
- `AlertDispatchId`
- `IncidentId`
- `EmergencyContactId`
- `UserId + Status`
- `Channel`
- `IdempotencyKey` unico
- `PreparedAtUtc`
- `SimulatedSentAtUtc`
- `FailedAtUtc`
- `CancelledAtUtc`
- `CreatedAtUtc`

## Ejemplo curl

```bash
curl -X POST "$BASE_URL/api/v1/notifications/delivery-attempts/prepare" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "alertDispatchId": "alert-dispatch-id",
    "notes": "Preparacion inicial de intentos de notificacion"
  }'
```

## Pendientes Futuros

- Provider real.
- Push.
- SMS real.
- Mensajeria instantanea.
- Email real.
- Escalamiento.
- Acknowledgement de contacto/monitor.
- Live Monitoring.
- Dashboard operativo.
- Machine Learning.
