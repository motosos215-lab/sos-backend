# Push Notification Tokens API

Push Notification Tokens API registra y administra tokens de notificacion de app movil y web para uso futuro de proveedores reales.

## Estado Actual

- Coleccion MongoDB: `pushNotificationTokens`.
- Registra tokens para `Rider`, `Monitor` y `Admin` autenticados.
- Cada usuario registra solo tokens propios; `userId` viene siempre del JWT.
- Para `Monitor`, el token queda asociado internamente al `sid` del JWT. Android no envia `sessionId`.
- Si la sesion del Monitor se reemplaza por takeover o se cierra con logout, los push tokens asociados a esa sesion se revocan para que el telefono anterior no reciba nuevas alertas SOS.
- No envia notificaciones reales.
- No llama proveedores externos.
- No agrega SDKs externos.
- No modifica Device Linking.

## Plataformas Y Canales

Combinaciones validas:

| Platform | Channel |
| --- | --- |
| `Android` | `Fcm` |
| `Ios` | `Apns` |
| `Web` | `WebPush` |
| `Web` | `Fcm` |

Cualquier otra combinacion devuelve `validation_error`.

## Seguridad Del Token

- `TokenValue` se guarda internamente para uso futuro de providers reales.
- `TokenValue` nunca se devuelve en responses.
- El token completo nunca se devuelve.
- `TokenHash` se usa solo para deduplicacion interna y nunca se devuelve.
- `TokenPreview` se devuelve solo enmascarado.
- `TokenValue`, `TokenHash` y `TokenPreview` no se incluyen en metadata de auditoria.
- `SessionId` es interno y no permite recuperar el token FCM completo.
- Pendiente futuro: encryption/protection at rest para `TokenValue`.
- Este modulo no agrega secretos de cifrado.

## Idempotencia

La idempotency key usa:

```text
userId + tokenHash + platform + channel + deviceId
```

Reglas:

- Registrar el mismo token dos veces devuelve el mismo registro.
- No devuelve `409` por duplicado.
- `LastSeenAtUtc` y `UpdatedAtUtc` se actualizan.
- `CreatedAtUtc` se conserva.
- Un token nuevo para el mismo `userId + platform + channel + deviceId` revoca tokens activos anteriores de ese scope.

## Device Ownership

Si `deviceId` se informa:

- Debe pertenecer al usuario autenticado.
- Debe estar activo.
- Debe tener `LinkStatus = Linked`.
- No debe estar revocado.
- Si no puede validarse, se devuelve `push_notification_token_not_allowed`.

Si `deviceId` no se informa, el token queda asociado solo al usuario.

## Endpoints

### Registrar Token

```http
POST /api/v1/push-notification-tokens
Authorization: Bearer {accessToken}
Content-Type: application/json
```

Request:

```json
{
  "platform": "Android",
  "channel": "Fcm",
  "deviceId": "mobile-device-id",
  "token": "push-token-from-client",
  "metadata": {
    "appVersion": "1.0.0",
    "osVersion": "Android 15"
  }
}
```

No se acepta `userId`, `tokenHash`, `status`, `tokenValue` ni credenciales de proveedor en el body.

Response:

```json
{
  "success": true,
  "data": {
    "pushNotificationToken": {
      "id": "token-id",
      "platform": "Android",
      "channel": "Fcm",
      "deviceId": "mobile-device-id",
      "tokenPreview": "abc123****wxyz",
      "status": "Active",
      "registeredAtUtc": "2026-08-09T18:30:00Z",
      "lastSeenAtUtc": "2026-08-09T18:30:00Z",
      "revokedAtUtc": null
    }
  },
  "error": null
}
```

### Listar Mis Tokens

```http
GET /api/v1/push-notification-tokens?platform=Android&channel=Fcm&status=Active&pageNumber=1&pageSize=20
Authorization: Bearer {accessToken}
```

Cada usuario ve solo sus propios tokens. La respuesta nunca incluye `TokenValue`, token completo ni `TokenHash`.

### Estado

```http
GET /api/v1/push-notification-tokens/status
Authorization: Bearer {accessToken}
```

Devuelve conteos activos/revocados, flags por combinacion activa y `lastRegisteredAtUtc`.

### Revocar Token Propio

```http
POST /api/v1/push-notification-tokens/{id}/revoke
Authorization: Bearer {accessToken}
```

Revocacion logica idempotente. No borra fisicamente ni llama proveedores externos.

### Admin List

```http
GET /api/v1/admin/push-notification-tokens
Authorization: Bearer {adminToken}
```

Filtros: `userId`, `platform`, `channel`, `status`, `dateFrom`, `dateTo`, `pageNumber`, `pageSize`.

### Admin Revoke

```http
POST /api/v1/admin/push-notification-tokens/{id}/revoke
Authorization: Bearer {adminToken}
```

Admin puede revocar cualquier token de forma idempotente.

## Auditoria

Acciones:

- `PushNotificationTokenRegistered`
- `PushNotificationTokenRevoked`

Metadata permitida:

- `pushNotificationTokenId`
- `platform`
- `channel`
- `deviceId`
- `status`

## Errores Esperados

- `401 unauthorized`
- `403 forbidden`
- `400 validation_error`
- `400 push_notification_token_not_allowed`
- `404 push_notification_token_not_available`

## Fuera De Alcance

No implementa envio real de push, validacion real con proveedores, SDKs externos, SMS real, correo real, mensajeria externa real, modelos predictivos, cobros ni pairing API de smartwatch.

## Pendientes Futuros

- Provider real para `Fcm`.
- Provider real para `Apns`.
- Provider real para `WebPush`.
- Encryption/protection at rest de `TokenValue`.
- Preferencias de notificacion.
- Health de providers.
- Reintentos reales por provider.
