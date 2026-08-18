# Auth Session Takeover API

La API soporta sesion unica activa por `SessionType`, no una sesion global por usuario.

Todo endpoint que emite tokens crea, reutiliza o valida una `UserSession` y emite access token JWT con claim `sid`.

Aplica a `POST /api/v1/auth/login`, `POST /api/v1/auth/login-with-code`, `POST /api/v1/auth/refresh` y `POST /api/v1/auth/sessions/takeover`.

No aplica a `request-access-code`, `forgot-password`, `reset-password`, `health` ni `health/ready`. `Admin` conserva compatibilidad y puede seguir sin `clientDevice` movil, pero los tokens nuevos incluyen `sid` de tipo `AdminWeb`.

## Role, SessionType E Identificadores

- `Role`: `Admin`, `Rider` o `Monitor`.
- `SessionType`: `MobileApp`, `WebApp`, `AdminWeb` o `Unknown`.
- `clientDeviceId`: instalacion Android/iOS estable.
- `mobileDeviceId`: dispositivo movil vinculado en Devices y usado por Trips.
- `sid`: id de `UserSession` dentro del JWT.

Un mismo usuario puede tener simultaneamente una sesion `MobileApp` y una sesion `WebApp` o `AdminWeb`. El indice unico activo es por `UserId + SessionType`.

## clientDeviceId

`clientDeviceId` identifica la instalacion Android. Android lo genera una vez por instalacion como UUID estable.

No es `mobileDeviceId`, `smartwatchDeviceId`, FCM token, push token id, user id, access token ni refresh token.

## Login Y Login-With-Code

Request de login:

```json
{
  "email": "usuario@motosos.com",
  "password": "password",
  "rememberMe": false,
  "clientDevice": {
    "clientDeviceId": "a05a7ccd-e072-4663-86db-2596528a3693",
    "deviceName": "Samsung SM-A346M",
    "platform": "Android",
    "osVersion": "Android 16",
    "appVersion": "1.0.0"
  }
}
```

Request de login-with-code:

```json
{
  "email": "usuario@motosos.com",
  "code": "123456",
  "clientDevice": {
    "clientDeviceId": "a05a7ccd-e072-4663-86db-2596528a3693",
    "deviceName": "Samsung SM-A346M",
    "platform": "Android",
    "osVersion": "Android 16",
    "appVersion": "1.0.0"
  }
}
```

Credenciales/codigo se validan antes de revisar sesion activa. Si son invalidos, no se revela informacion de sesion. `login-with-code` no consume el codigo si termina en `active_session_exists`.

Clasificacion:

- `clientDevice.platform = Android` o `iOS`: `SessionType = MobileApp` y `clientDevice` es obligatorio completo.
- Sin `clientDevice`: `SessionType = WebApp` para `Rider`/`Monitor`.
- Sin `clientDevice` y usuario `Admin`: `SessionType = AdminWeb`.
- `clientType` opcional puede indicar `WebApp` o `AdminWeb` sin romper compatibilidad.

Response `200`:

```json
{
  "success": true,
  "data": {
    "accessToken": "...",
    "refreshToken": "...",
    "accessTokenExpiresAtUtc": "2026-08-18T15:30:00Z",
    "session": {
      "id": "session-id",
      "deviceName": "Samsung SM-A346M",
      "platform": "Android",
      "createdAtUtc": "2026-08-18T15:00:00Z",
      "lastSeenAtUtc": "2026-08-18T15:00:00Z"
    }
  },
  "error": null
}
```

El access token incluye `sid=session.id`. El refresh token queda ligado internamente a `session.id`.

Si existe sesion activa del mismo `SessionType` con el mismo `clientDeviceId`, se reutiliza la `UserSession`, se rotan tokens y se mantiene una sola sesion activa.

Si existe sesion `MobileApp` activa con otro `clientDeviceId`, responde `409 active_session_exists` con `activeSession`, `takeoverToken`, `takeoverExpiresAtUtc`, `hasActiveTrip` y `activeTrip` cuando aplica. No se devuelve access token anterior, refresh token anterior, token hash ni FCM token.

El login `MobileApp` no cierra `WebApp/AdminWeb`. El login web no cierra `MobileApp`. Para web sin identificador estable, la API reutiliza la sesion `WebApp/AdminWeb` activa y rota tokens; no ejecuta takeover web en esta entrega.

## Takeover

Endpoint publico sin access token anterior: `POST /api/v1/auth/sessions/takeover`.

```json
{
  "takeoverToken": "TOKEN_TEMPORAL",
  "clientDevice": {
    "clientDeviceId": "new-installation-uuid",
    "deviceName": "Samsung SM-A536E",
    "platform": "Android",
    "osVersion": "Android 16",
    "appVersion": "1.0.0"
  },
  "transferActiveTrip": false,
  "mobileDeviceId": null
}
```

El token expira por defecto en 3 minutos, se guarda hasheado, es de un solo uso, queda ligado al usuario y al nuevo `clientDeviceId`, y no sirve como access token ni refresh token.

Takeover exitoso revoca la sesion anterior del mismo `SessionType`, revoca sus refresh tokens, revoca push tokens asociados a esa sesion y emite tokens nuevos con nuevo `sid`.

## Rider Con Viaje Activo

Si el takeover tiene viaje activo, Android debe confirmar transferencia con `transferActiveTrip=true` y `mobileDeviceId` del nuevo movil backend.

El backend valida que el viaje esta `Active`, pertenece al Rider y que el nuevo `mobileDeviceId` pertenece al mismo Rider y esta activo/vinculado.

La transferencia mantiene exactamente el mismo `trip.id`, `status=Active`, `startedAtUtc`, route points, incidentes, alert dispatches, notification attempts y SOS existentes. Solo cambia `Trip.mobileDeviceId`.

Si `transferActiveTrip=false`, responde `409 active_trip_transfer_required`, no revoca la sesion anterior, no modifica el viaje y no consume el takeover token.

## Logout Y Refresh

`POST /api/v1/auth/logout` requiere JWT, lee `sid`, revoca la `UserSession`, revoca refresh tokens de esa sesion y revoca push tokens asociados. Responde `{ "loggedOut": true }` en el wrapper estandar.

`refresh` valida que el refresh token pertenece a una sesion activa. Una sesion revocada devuelve `401 session_revoked`; un refresh token viejo no puede revivir una sesion vieja.

## Codigos De Error

- `active_session_exists`
- `active_trip_transfer_required`
- `takeover_token_invalid`
- `takeover_token_expired`
- `takeover_token_already_used`
- `session_revoked`
- `device_not_available`
- `active_trip_not_available`

## Indices Mongo

- `ux_userSessions_userId_sessionType_active`
- `ix_userSessions_userId`
- `ix_userSessions_userId_sessionType`
- `ix_userSessions_userId_clientDeviceId`
- `ix_userSessions_revokedAtUtc`
- `ix_userSessions_lastSeenAtUtc`
- `ux_sessionTakeoverTokens_tokenHash`
- `ix_sessionTakeoverTokens_userId`
- `ix_sessionTakeoverTokens_expiresAtUtc`
- `ix_sessionTakeoverTokens_usedAtUtc`
- `ix_pushNotificationTokens_sessionId`
