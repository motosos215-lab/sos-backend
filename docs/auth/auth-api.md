# Auth API

La autenticacion de MotoSOS.API usa JWT Bearer, refresh tokens hasheados y MongoDB Atlas como almacenamiento central. Las respuestas publicas mantienen el envelope estandar.

## Envelope

Exito:

```json
{
  "success": true,
  "data": {},
  "error": null
}
```

Error:

```json
{
  "success": false,
  "data": null,
  "error": {
    "code": "validation_error",
    "message": "..."
  }
}
```

## Roles

- `Admin = 1`
- `Rider = 2`
- `Monitor = 3`

Mapeo visual del maquetado:

- `Conductor` se guarda como `Rider`.
- `Rider` se guarda como `Rider`.
- `Monitor` se guarda como `Monitor`.

`Admin`, `Administrator` y `Administrador` no se crean desde registro publico. El login funciona para usuarios Admin existentes en MongoDB si fueron creados por un proceso seguro futuro. No insertar passwords planos en MongoDB.

## POST /api/v1/auth/register

Request:

```json
{
  "email": "rider@example.com",
  "password": "StrongPass1!",
  "confirmPassword": "StrongPass1!",
  "fullName": "Moto Rider",
  "phoneNumber": "+52 5512345678",
  "accountType": "Rider",
  "acceptTerms": true
}
```

Response `201 Created`:

```json
{
  "success": true,
  "data": {
    "user": {
      "id": "...",
      "email": "rider@example.com",
      "fullName": "Moto Rider",
      "phoneNumber": "+52 5512345678",
      "role": "Rider",
      "isActive": true
    }
  },
  "error": null
}
```

Errores relevantes:

- `400 validation_error`: request invalido, password debil, `confirmPassword` no coincide o `accountType` no permitido.
- `400 terms_not_accepted`: `acceptTerms` es `false`.
- `409 user_already_exists`: el registro no pudo completarse porque el usuario ya existe.

MongoDB guarda `PasswordHash`, no guarda `password` ni `confirmPassword`.

## POST /api/v1/auth/login

Request:

```json
{
  "email": "rider@example.com",
  "password": "StrongPass1!",
  "rememberMe": true
}
```

Response `200 OK`:

```json
{
  "success": true,
  "data": {
    "accessToken": "...",
    "refreshToken": "...",
    "accessTokenExpiresAtUtc": "2026-07-23T00:15:00+00:00",
    "user": {
      "id": "...",
      "email": "rider@example.com",
      "fullName": "Moto Rider",
      "phoneNumber": "+52 5512345678",
      "role": "Rider",
      "isActive": true
    }
  },
  "error": null
}
```

`rememberMe` solo cambia la expiracion del refresh token. No cambia la expiracion del access token. El refresh token se entrega una vez en respuesta publica y se guarda como `TokenHash` en MongoDB.

Errores relevantes:

- `400 validation_error`: request invalido.
- `401 invalid_credentials`: credenciales invalidas, usuario inexistente o inactivo.

## POST /api/v1/auth/refresh

Request:

```json
{
  "refreshToken": "..."
}
```

Response `200 OK`:

```json
{
  "success": true,
  "data": {
    "accessToken": "...",
    "refreshToken": "...",
    "accessTokenExpiresAtUtc": "2026-07-23T00:15:00+00:00"
  },
  "error": null
}
```

El refresh token anterior se revoca y se reemplaza por uno nuevo hasheado.

## POST /api/v1/auth/logout

Request:

```json
{
  "refreshToken": "..."
}
```

Response `204 No Content`.

## POST /api/v1/auth/forgot-password

Request:

```json
{
  "email": "rider@example.com"
}
```

Response `204 No Content` si el email tiene formato valido, exista o no exista el usuario.

Esta funcionalidad queda preparada sin envio real de correo, SMS o WhatsApp. No devuelve tokens de recuperacion y no revela existencia de usuarios.

## POST /api/v1/auth/request-access-code

Request:

```json
{
  "email": "rider@example.com"
}
```

Response `204 No Content` si el email tiene formato valido, exista o no exista el usuario.

Esta funcionalidad queda preparada sin proveedor externo real y no revela existencia de usuarios.

## POST /api/v1/auth/login-with-code

Request:

```json
{
  "email": "rider@example.com",
  "code": "123456"
}
```

Response `501 Not Implemented`:

```json
{
  "success": false,
  "data": null,
  "error": {
    "code": "feature_not_implemented",
    "message": "Access code login is prepared but pending an external provider."
  }
}
```

No hay OTP hardcodeado y no se acepta cualquier codigo.

## GET /api/v1/users/me

Header:

```http
Authorization: Bearer <accessToken>
```

Response `200 OK`:

```json
{
  "success": true,
  "data": {
    "user": {
      "id": "...",
      "email": "rider@example.com",
      "fullName": "Moto Rider",
      "phoneNumber": "+52 5512345678",
      "role": "Rider",
      "isActive": true,
      "createdAtUtc": "2026-07-23T00:00:00+00:00",
      "updatedAtUtc": "2026-07-23T00:00:00+00:00",
      "lastLoginAtUtc": "2026-07-23T00:00:00+00:00"
    }
  },
  "error": null
}
```

Sin token valido responde `401 Unauthorized`.

## Flujo recomendado para Postman

1. Ejecutar `POST /api/v1/auth/register` con `accountType` `Rider`, `Conductor` o `Monitor` y `acceptTerms: true`.
2. Ejecutar `POST /api/v1/auth/login` con `rememberMe` segun preferencia.
3. Guardar `accessToken` y `refreshToken` como variables de entorno de Postman.
4. Ejecutar `GET /api/v1/users/me` con `Authorization: Bearer {{accessToken}}`.
5. Ejecutar `POST /api/v1/auth/refresh` con `{{refreshToken}}` para rotar tokens.
6. Ejecutar `POST /api/v1/auth/logout` con el refresh token vigente.
7. Probar `forgot-password` y `request-access-code`; ambos responden `204` con emails validos.
8. Probar `login-with-code`; debe responder `501 feature_not_implemented` hasta integrar proveedor externo real.
