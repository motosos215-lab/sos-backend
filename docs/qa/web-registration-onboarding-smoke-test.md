# Web Registration / Onboarding Smoke Test

## Objetivo

Validar manualmente que el flujo web-first de registro y onboarding de MotoSOS funciona de punta a punta para un conductor, desde creacion de cuenta hasta confirmacion operativa.

Este smoke test esta orientado a QA manual con Postman. No valida pagos reales, OTP real, viajes, SOS, incidentes, notificaciones reales, monitoreo en vivo, dashboard operativo ni ML.

## Base URLs

- Local: `http://localhost:5000`
- Produccion: configurar segun el ambiente desplegado, por ejemplo `https://<api-production-host>`

## CORS Local

En `appsettings.Development.json` estan permitidos:

- `http://localhost:5173`
- `http://localhost:3000`

`http://127.0.0.1:5173` no esta en `appsettings.Development.json`. Si el frontend local usa `127.0.0.1`, se debe agregar a `Cors:AllowedOrigins` o configurar por variable de entorno en el ambiente correspondiente.

## Variables Postman

Crear un environment con estas variables:

| Variable | Valor inicial sugerido |
| --- | --- |
| `baseUrl` | `http://localhost:5000` |
| `email` | `qa.rider.{{$timestamp}}@example.com` |
| `password` | `StrongPass1!` |
| `accessToken` | vacio |
| `refreshToken` | vacio |
| `profileId` | vacio |
| `vehicleId` | vacio |
| `emergencyContactId` | vacio |
| `emergencyContactLinkingCode` | vacio |
| `activationCode` | vacio |
| `mobileDeviceId` | vacio |
| `smartwatchDeviceId` | vacio |
| `subscriptionId` | vacio |

Header para endpoints protegidos:

```http
Authorization: Bearer {{accessToken}}
Content-Type: application/json
```

Envelope esperado de exito:

```json
{
  "success": true,
  "data": {},
  "error": null
}
```

Envelope esperado de error:

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

## Flujo Completo

### 1. Register

`POST {{baseUrl}}/api/v1/auth/register`

Request:

```json
{
  "email": "{{email}}",
  "password": "{{password}}",
  "confirmPassword": "{{password}}",
  "fullName": "QA Rider",
  "phoneNumber": "+52 5512345678",
  "accountType": "Conductor",
  "acceptTerms": true
}
```

Response esperada: `201 Created`.

Validar:

- `success = true`
- `data.user.email = {{email}}`
- `data.user.role = "Rider"`
- No aparece `password`, `confirmPassword`, `passwordHash` ni refresh token almacenado.

Nota: `accountType = "Conductor"` se acepta desde web y se guarda como `Rider`.

### 2. Login

`POST {{baseUrl}}/api/v1/auth/login`

Request:

```json
{
  "email": "{{email}}",
  "password": "{{password}}",
  "rememberMe": true
}
```

Response esperada: `200 OK`.

Validar y guardar variables:

- `accessToken = data.accessToken`
- `refreshToken = data.refreshToken`
- `data.user.role = "Rider"`
- No aparece `passwordHash`.

### 3. Users/me

`GET {{baseUrl}}/api/v1/users/me`

Response esperada: `200 OK`.

Validar:

- `data.user.email = {{email}}`
- `data.user.role = "Rider"`
- No aparece `passwordHash` ni tokens.

### 4. Onboarding Status Inicial

`GET {{baseUrl}}/api/v1/onboarding/status`

Response esperada: `200 OK`.

Validar onboarding:

- `totalSteps = 7`
- `completedSteps = 1`
- `progressPercentage = 14`
- `currentStep = "Profile"`
- `isOperational = false`
- `Account = Completed`
- `Profile = Pending`

### 5. Profile Continue

`PUT {{baseUrl}}/api/v1/profiles/me`

Request:

```json
{
  "fullName": "QA Rider",
  "phoneNumber": "+52 5512345678",
  "dateOfBirth": "1995-01-15",
  "curpOrIdentifier": "optional",
  "addressOrZone": "Colonia Centro",
  "primaryCity": "Toluca",
  "bloodType": "O+",
  "allergies": "Ninguna",
  "medicalConditions": "Ninguna",
  "provisionalEmergencyContactName": "Maria Lopez",
  "provisionalEmergencyContactPhone": "+52 5511112233",
  "saveMode": "Continue"
}
```

Response esperada: `200 OK`.

Guardar:

- `profileId = data.profile.id`

Validar onboarding con `GET /api/v1/onboarding/status`:

- `completedSteps = 2`
- `progressPercentage = 29`
- `currentStep = "Vehicle"`
- `Profile = Completed`
- `isOperational = false`

### 6. Vehicle Continue

`POST {{baseUrl}}/api/v1/vehicles`

Request:

```json
{
  "vehicleType": "Motorcycle",
  "brand": "Yamaha",
  "model": "FZ 2.0",
  "year": 2022,
  "alias": "Mi moto QA",
  "primaryUse": "Personal",
  "color": "Rojo",
  "plateNumber": "QA1234",
  "vin": "QA-VIN-123456789",
  "usageFrequency": "Daily",
  "saveMode": "Continue"
}
```

Response esperada: `201 Created`.

Guardar:

- `vehicleId = data.vehicle.id`

Validar onboarding:

- `completedSteps = 3`
- `progressPercentage = 43`
- `currentStep = "EmergencyContacts"`
- `Vehicle = Completed`
- `isOperational = false`

### 7. EmergencyContact Continue

`POST {{baseUrl}}/api/v1/emergency-contacts`

Request:

```json
{
  "fullName": "Maria Lopez",
  "relationship": "Esposa",
  "phoneNumber": "+52 5512345678",
  "email": "maria.lopez@example.com",
  "priority": 1,
  "permissions": {
    "canViewRealTimeLocation": true,
    "canReceiveCriticalAlerts": true,
    "canViewIncidentHistory": false,
    "canViewVitalSigns": false
  },
  "saveMode": "Continue"
}
```

Response esperada: `201 Created`.

Guardar:

- `emergencyContactId = data.contact.id`

Validar:

- `data.contact.invitationStatus = "Pending"`
- `data.contact.linkingCode = null`

### 8. Invite Emergency Contact

`POST {{baseUrl}}/api/v1/emergency-contacts/{{emergencyContactId}}/invite`

Request: sin body.

Response esperada: `200 OK`.

Guardar:

- `emergencyContactLinkingCode = data.contact.linkingCode`

Validar onboarding:

- `completedSteps = 4`
- `progressPercentage = 57`
- `currentStep = "Devices"`
- `EmergencyContacts = Completed`
- `isOperational = false`

Hallazgo importante: `GET /api/v1/emergency-contacts/invitations/{code}` requiere JWT porque esta dentro del grupo con `RequireAuthorization()`.

### 9. Generate Mobile Activation Code

`POST {{baseUrl}}/api/v1/devices/mobile/activation-code`

Request: sin body.

Response esperada: `200 OK`.

Guardar:

- `activationCode = data.activationCode.code`

Validar:

- `data.activationCode.code` no esta vacio.
- `data.activationCode.expiresAtUtc` no esta vacio.

### 10. Current Activation Code

`GET {{baseUrl}}/api/v1/devices/activation-codes/current`

Response esperada: `200 OK`.

Validar:

- `data.activationCode.code = {{activationCode}}`

### 11. Link MobileApp

`POST {{baseUrl}}/api/v1/devices/mobile/link`

Request:

```json
{
  "code": "{{activationCode}}",
  "deviceName": "QA Android Phone",
  "platform": "Android",
  "manufacturer": "Motorola",
  "model": "Edge 40",
  "operatingSystemVersion": "14",
  "appVersion": "1.0.0",
  "deviceIdentifier": "qa-mobile-device-{{$timestamp}}"
}
```

Response esperada: `200 OK`.

Guardar:

- `mobileDeviceId = data.device.id`

Validar:

- `data.device.deviceType = "MobileApp"`
- `data.device.status = "Linked"`
- No aparece `deviceIdentifier` ni `deviceIdentifierHash`.

Validar onboarding:

- `completedSteps = 5`
- `progressPercentage = 71`
- `currentStep = "Plan"`
- `Devices = Completed`
- `isOperational = false`

### 12. Optional Smartwatch Link

Estado: legado / no aplicable para la decision vigente de Wear OS.

La vinculacion del smartwatch ahora es local entre Android y Wear OS mediante Wear OS Data Layer. MotoSOS.API no debe administrar pairing, QR, codigos, nodeId, Bluetooth ni estado Connected/Disconnected del reloj. Este paso queda documentado solo como contexto historico de la propuesta anterior y no debe usarse para validar nuevas implementaciones.

`POST {{baseUrl}}/api/v1/devices/smartwatch/link`

Request:

```json
{
  "parentDeviceId": "{{mobileDeviceId}}",
  "deviceName": "QA Galaxy Watch",
  "platform": "WearOS",
  "manufacturer": "Samsung",
  "model": "Galaxy Watch 6",
  "operatingSystemVersion": "Wear OS 4",
  "appVersion": "1.0.0",
  "deviceIdentifier": "qa-watch-device-{{$timestamp}}",
  "batteryLevel": 80
}
```

Response esperada: `200 OK`.

Guardar si se ejecuta:

- `smartwatchDeviceId = data.device.id`

Validar:

- `data.device.deviceType = "Smartwatch"`
- `data.device.parentDeviceId = {{mobileDeviceId}}`
- No aparece `deviceIdentifier` ni `deviceIdentifierHash`.

Nota: el smartwatch es opcional y no cambia el avance del onboarding.

### 13. Get Plans

`GET {{baseUrl}}/api/v1/plans`

Response esperada: `200 OK`.

Validar:

- El catalogo contiene plan `Basic`.
- No aparecen datos de pago ni tokens de proveedores externos.

### 14. Get Subscription

`GET {{baseUrl}}/api/v1/subscriptions/me`

Response esperada: `200 OK`.

Antes de seleccionar Basic, validar:

- `data.subscription = null`
- `data.defaultPlan.tier = "Basic"`

### 15. Select Basic

`POST {{baseUrl}}/api/v1/subscriptions/select-basic`

Request: sin body.

Response esperada: `200 OK`.

Guardar:

- `subscriptionId = data.subscription.id`

Validar:

- `data.subscription.planTier = "Basic"`
- `data.subscription.status = "Active"`
- `data.subscription.source = "WebBasic"`

Validar onboarding:

- `completedSteps = 6`
- `progressPercentage = 86`
- `currentStep = "Confirmation"`
- `Plan = Completed`
- `isOperational = false`

### 16. Onboarding Summary

`GET {{baseUrl}}/api/v1/onboarding/summary`

Response esperada: `200 OK`.

Validar:

- `data.summary.canConfirm = true`
- `data.summary.isConfirmed = false`
- `data.summary.isOperational = false`
- `data.summary.completedSteps = 6`
- `data.summary.progressPercentage = 86`
- `data.summary.currentStep = "Confirmation"`
- No aparece `passwordHash`, refresh token almacenado, `deviceIdentifier`, `deviceIdentifierHash` ni datos de pago.

### 17. Confirm Onboarding

`POST {{baseUrl}}/api/v1/onboarding/confirm`

Request: sin body.

Response esperada: `200 OK`.

Validar:

- `data.onboarding.totalSteps = 7`
- `data.onboarding.completedSteps = 7`
- `data.onboarding.progressPercentage = 100`
- `data.onboarding.currentStep = "Completed"`
- `data.onboarding.isOperational = true`

### 18. Final Onboarding Status

`GET {{baseUrl}}/api/v1/onboarding/status`

Response esperada: `200 OK`.

Validar final:

- `totalSteps = 7`
- `completedSteps = 7`
- `progressPercentage = 100`
- `currentStep = "Completed"`
- `isOperational = true`
- Todos los pasos estan `Completed`.

### 19. Refresh Token

`POST {{baseUrl}}/api/v1/auth/refresh`

Request:

```json
{
  "refreshToken": "{{refreshToken}}"
}
```

Response esperada: `200 OK`.

Actualizar variables:

- `accessToken = data.accessToken`
- `refreshToken = data.refreshToken`

Validar:

- El response entrega un nuevo refresh token.
- No expone hash del refresh token almacenado.

### 20. Logout

`POST {{baseUrl}}/api/v1/auth/logout`

Request:

```json
{
  "refreshToken": "{{refreshToken}}"
}
```

Response esperada: `204 No Content`.

## Validaciones De Seguridad

- Ejecutar un endpoint protegido sin `Authorization` y validar `401 Unauthorized`.
- Registrar un usuario `Monitor`, iniciar sesion y validar que endpoints Rider-only devuelven `403 Forbidden`.
- Intentar `accountType = "Admin"` en registro publico y validar `400 validation_error`.
- Validar que responses no exponen `passwordHash`, `PasswordHash`, `refreshToken` almacenado, `TokenHash`, `deviceIdentifier`, `deviceIdentifierHash`, connection strings ni secretos.
- Validar que `/api/v1/emergency-contacts/invitations/{code}` requiere JWT.
- Validar que `POST /api/v1/auth/login-with-code` devuelve `501 feature_not_implemented`.
- Validar que `forgot-password` y `request-access-code` no revelan existencia de usuarios y responden `204` para emails validos.

## Errores Esperados

- `400 validation_error`: payload invalido, password debil, campos requeridos faltantes o `accountType` no permitido.
- `400 terms_not_accepted`: `acceptTerms = false` en registro.
- `400 activation_code_invalid`: codigo movil usado, expirado, revocado, inexistente o ajeno.
- `400 onboarding_not_ready`: intentar confirmar onboarding antes de completar pasos previos.
- `401 Unauthorized`: token ausente, invalido o expirado.
- `403 Forbidden`: usuario `Monitor` o rol no permitido en endpoints Rider-only.
- `404 not_found`: recurso inexistente, inactivo o ajeno.
- `409 user_already_exists`: email ya registrado.
- `409 plan_limit_exceeded`: exceder limites actuales de Basic, como segundo vehiculo activo, segundo contacto activo o segundo MobileApp activo.
- `501 feature_not_implemented`: `POST /api/v1/auth/login-with-code`.

## Checklist Final

- Registro crea usuario Rider usando `accountType = Conductor`.
- Login devuelve `accessToken` y `refreshToken`.
- `GET /api/v1/users/me` responde con usuario autenticado.
- Onboarding inicial esta en `1/7`, `14%`, `Profile`.
- Profile Continue avanza a `2/7`, `29%`, `Vehicle`.
- Vehicle Continue avanza a `3/7`, `43%`, `EmergencyContacts`.
- Emergency contact invitado avanza a `4/7`, `57%`, `Devices`.
- MobileApp linked avanza a `5/7`, `71%`, `Plan`.
- Select Basic avanza a `6/7`, `86%`, `Confirmation`.
- Summary indica `canConfirm = true`.
- Confirm deja onboarding en `7/7`, `100%`, `Completed`, `isOperational = true`.
- Refresh token rota tokens correctamente.
- Logout devuelve `204 No Content`.
- No se exponen secretos ni datos sensibles en responses.
- CORS local funciona desde `localhost:5173` y `localhost:3000`.

## Faltantes Conocidos

- OTP real no esta implementado todavia.
- `POST /api/v1/auth/login-with-code` existe como stub y devuelve `501 feature_not_implemented`.
- Pagos reales no estan implementados.
- Google Play Billing no esta implementado.
- Stripe no esta implementado.
- Trips sigue pendiente.
- SOS sigue pendiente.
- Incidents sigue pendiente.
- Notifications sigue pendiente.
- Live Monitoring sigue pendiente.
- Dashboard sigue pendiente.
- ML sigue pendiente.
- Envio real de SMS/correo/notificaciones para invitaciones sigue pendiente.
- Aceptacion real de invitaciones por app monitor sigue pendiente.
