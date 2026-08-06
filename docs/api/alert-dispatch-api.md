# Alert Dispatch API

## Descripcion

Alert Dispatch API registra solicitudes de alerta asociadas a incidentes existentes de MotoSOS. Su objetivo actual es preparar y persistir solicitudes para una futura capa de notificaciones.

Este modulo no envia push notifications reales, SMS, WhatsApp, correo, llamadas, escalamiento, confirmaciones de contactos/monitores, live monitoring, dashboard operativo ni Machine Learning en esta etapa.

## Relacion Con La App Movil E Incidents

- La app movil puede generar un `alert-dispatch-request` local despues de crear un incidente.
- El backend expone el contrato HTTP final para persistir esa solicitud como registro remoto.
- Cada solicitud debe apuntar a un `incidentId` existente, propio y en estado `Open`.
- Los datos operativos `tripId`, `vehicleId`, `mobileDeviceId` y `smartwatchDeviceId` se derivan del incidente, no del request.
- Si el disparo original involucra Wear OS, Android movil debe resolverlo localmente y enviar la solicitud usando la sesion del Rider. La API no administra pairing, QR, codigos, nodeId, Bluetooth ni estado Connected/Disconnected del smartwatch.

## Endpoints

- `POST /api/v1/alert-dispatches`
- `GET /api/v1/alert-dispatches?status=&incidentId=&pageNumber=&pageSize=`
- `GET /api/v1/alert-dispatches/{id}`
- `POST /api/v1/alert-dispatches/{id}/cancel`

Todos requieren JWT Bearer y solo permiten `Rider`.

## Identidad E Idempotencia

- El `userId` se obtiene exclusivamente del Bearer JWT.
- El body no acepta `userId`.
- `clientAlertRequestId` es obligatorio y debe ser UUID.
- No se confia en IDs locales como IDs globales.

Idempotency key oficial:

```text
userId + incidentId + clientAlertRequestId
```

La API guarda un indice unico en `IdempotencyKey`. Si llega el mismo request otra vez, no duplica documento y responde la solicitud existente como exito estable. No devuelve `409` para duplicados idempotentes.

## Snapshot De Contactos

Al crear la solicitud, la API guarda un snapshot de contactos de emergencia elegibles del usuario.

Contactos elegibles:

- `IsActive = true`.
- `InvitationStatus = Invited` o `Linked`.

Si no existe al menos un contacto elegible, la API devuelve `alert_not_allowed`. Aunque no se envian notificaciones reales todavia, la solicitud debe quedar preparada con destinatarios potenciales.

Campos del snapshot:

- `emergencyContactId`
- `fullName`
- `phoneNumber`
- `email`
- `relationship`
- `priority`
- `invitationStatus`

El response publico solo devuelve `contactsCount`, no el snapshot completo.

## Crear Solicitud

`POST /api/v1/alert-dispatches`

Request:

```json
{
  "incidentId": "incident-id-remoto",
  "clientAlertRequestId": "1ab6266e-1dd2-4b7a-981c-778812345678",
  "priority": "High",
  "reason": "IncidentCreated",
  "requestedAtUtc": "2026-08-06T07:12:00Z",
  "notes": "Solicitud generada por la app movil despues de incidente"
}
```

Response:

```json
{
  "success": true,
  "data": {
    "alertDispatch": {
      "id": "alert-dispatch-id",
      "incidentId": "incident-id-remoto",
      "tripId": "trip-id-remoto",
      "vehicleId": "vehicle-id",
      "mobileDeviceId": "mobile-device-id",
      "smartwatchDeviceId": "smartwatch-device-id",
      "priority": "High",
      "reason": "IncidentCreated",
      "status": "PendingDispatch",
      "requestedAtUtc": "2026-08-06T07:12:00+00:00",
      "createdAtUtc": "2026-08-06T07:12:05+00:00",
      "updatedAtUtc": "2026-08-06T07:12:05+00:00",
      "cancelledAtUtc": null,
      "completedAtUtc": null,
      "notes": "Solicitud generada por la app movil despues de incidente",
      "contactsCount": 1
    }
  },
  "error": null
}
```

## Listar Solicitudes

`GET /api/v1/alert-dispatches?status=PendingDispatch&incidentId=incident-id&pageNumber=1&pageSize=20`

Reglas:

- Lista solo solicitudes del usuario autenticado.
- `status` es opcional.
- `incidentId` es opcional.
- `pageNumber` default `1`.
- `pageSize` default `20`, maximo `100`.

## Obtener Solicitud

`GET /api/v1/alert-dispatches/{id}`

Reglas:

- Devuelve solo solicitud propia.
- Solicitud ajena devuelve `404 not_found`.

## Cancelar Solicitud

`POST /api/v1/alert-dispatches/{id}/cancel`

Request:

```json
{
  "reason": "El conductor cancelo la alerta",
  "clientCancelledAtUtc": "2026-08-06T07:13:00Z"
}
```

Reglas:

- `PendingDispatch -> Cancelled`.
- `Cancelled` devuelve estado actual de forma idempotente.
- `Completed` devuelve `400 alert_dispatch_already_completed`.
- No se borra fisicamente el documento.
- `Completed` no tiene transicion publica en esta etapa.

## Validaciones

- `incidentId` requerido.
- `clientAlertRequestId` requerido y UUID.
- `priority` requerido y permitido.
- `reason` requerido y permitido.
- `requestedAtUtc` requerido.
- `notes` opcional, maximo 500 caracteres.
- Cancel `reason` opcional, maximo 500 caracteres.

Valores aceptados:

- `priority`: `Low`, `Medium`, `High`, `Critical`.
- `reason`: `IncidentCreated`, `ManualSos`, `CountdownTimeout`, `CriticalEvent`, `UserRequestedHelp`, `Unknown`.
- `status`: `PendingDispatch`, `Cancelled`, `Completed`.

## Reglas De Negocio

- Requiere onboarding completo: `completedSteps = 7`, `currentStep = Completed` e `isOperational = true`.
- `incidentId` debe existir y pertenecer al Rider autenticado.
- Solo incidentes `Open` pueden preparar solicitudes.
- Incidentes `Closed` devuelven `incident_not_ready`.
- Incidentes `FalsePositiveCancelled` devuelven `alert_not_allowed`.
- Se requiere al menos un contacto activo `Invited` o `Linked`.
- Nuevas solicitudes se crean con `Status = PendingDispatch`.
- No se crean intentos reales de entrega.
- No se envian notificaciones reales.

## Errores Esperados

- `401 unauthorized`: sin token o token invalido.
- `403 forbidden`: usuario no Rider.
- `400 validation_error`: request invalido.
- `400 onboarding_not_ready`: onboarding incompleto o no operacional.
- `400 incident_not_ready`: incidente cerrado o no apto.
- `400 alert_not_allowed`: incidente cancelado como falso positivo o sin contactos elegibles.
- `400 alert_dispatch_already_completed`: intento de cancelar solicitud completada.
- `404 not_found`: incidente o solicitud inexistente o ajena.

## Seguridad

- No devuelve `passwordHash`.
- No devuelve `accessToken` ni `refreshToken`.
- No devuelve `deviceIdentifier` ni `deviceIdentifierHash`.
- No devuelve datos de pago, Google Play, Stripe ni proveedores externos.
- No devuelve tokens de proveedores de notificacion.
- No acepta `userId` desde el body.
- No permite crear, consultar ni cancelar solicitudes de otro usuario.

## MongoDB

Coleccion: `alertDispatchRequests`.

Indices:

- `UserId`
- `IncidentId`
- `UserId + Status`
- `ClientAlertRequestId`
- `IdempotencyKey` unico
- `RequestedAtUtc`
- `CreatedAtUtc`
- `CancelledAtUtc`
- `CompletedAtUtc`

## Ejemplo curl

```bash
curl -X POST "$BASE_URL/api/v1/alert-dispatches" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "incidentId": "incident-id-remoto",
    "clientAlertRequestId": "1ab6266e-1dd2-4b7a-981c-778812345678",
    "priority": "High",
    "reason": "IncidentCreated",
    "requestedAtUtc": "2026-08-06T07:12:00Z",
    "notes": "Solicitud generada por la app movil despues de incidente"
  }'
```

## Pendientes Futuros

- Notifications API.
- Push notifications.
- SMS.
- WhatsApp.
- Correo.
- Escalamiento real.
- Contact/Monitor acknowledgement.
- Live Monitoring.
- Dashboard operativo.
- Machine Learning.
- Procesador real de Offline Ingestion.
- Sensor batches completos.
