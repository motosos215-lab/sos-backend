# Auth Codes

Auth Codes implementa recuperacion de password y login con codigo temporal sin exponer codigos, tokens ni secretos.

## Configuracion

- `AuthCodes__Enabled=true`
- `AuthCodes__CodeLength=6`
- `AuthCodes__TtlMinutes=10`
- `AuthCodes__MaxAttempts=5`
- `AuthCodes__RateLimitMinutes=1`
- `AuthCodes__Provider=Simulated`

## Seguridad

- Los codigos no se devuelven por API.
- Los codigos no se guardan planos; se guardan hasheados con el hasher de passwords actual.
- Los logs no incluyen codigos, hashes, passwords, access tokens, refresh tokens ni secretos.
- `forgot-password` y `request-access-code` responden `204 No Content` aunque el email no exista, el usuario este inactivo, se alcance rate limit o `AuthCodes__Enabled=false`.
- `reset-password` y `login-with-code` devuelven `invalid_or_expired_code` para codigo invalido, expirado, usado, bloqueado, proposito incorrecto o usuario inactivo.
- Un codigo `PasswordReset` no sirve para `AccessLogin`, y un codigo `AccessLogin` no sirve para `PasswordReset`.
- Al generar un codigo nuevo para el mismo email y proposito se revocan codigos `Active` anteriores del mismo proposito.

## Forgot Password

```http
POST /api/v1/auth/forgot-password
```

```json
{
  "email": "rider@example.com"
}
```

Response: `204 No Content`.

## Reset Password

```http
POST /api/v1/auth/reset-password
```

```json
{
  "email": "rider@example.com",
  "code": "123456",
  "newPassword": "NewStrongPass1!"
}
```

Response `200 OK`:

```json
{
  "success": true,
  "data": null,
  "error": null
}
```

Reset password valida la politica actual de password, cambia el password con el hasher actual, marca el codigo como usado y revoca refresh tokens activos del usuario.

## Request Access Code

```http
POST /api/v1/auth/request-access-code
```

```json
{
  "email": "rider@example.com"
}
```

Response: `204 No Content`.

## Login With Code

```http
POST /api/v1/auth/login-with-code
```

```json
{
  "email": "rider@example.com",
  "code": "123456"
}
```

Response: mismo contrato que `POST /api/v1/auth/login`, con `accessToken`, `refreshToken`, `accessTokenExpiresAtUtc` y `user`.

## Provider Simulado

`SimulatedAuthCodeDeliveryProvider` queda como provider por defecto. No envia email/SMS real y no loguea el codigo. En tests se puede reemplazar por un fake provider mediante DI para capturar el codigo y validar flujos end-to-end.

Email/SMS real queda para otra integracion.
