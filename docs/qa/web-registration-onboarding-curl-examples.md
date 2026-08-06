# Web Registration / Onboarding curl Examples

## Objetivo

Ejecutar el smoke test web-first de registro y onboarding usando `curl`. Estos ejemplos asumen que la API corre localmente y que se copian manualmente tokens, IDs y codigos entre pasos.

## Variables

PowerShell:

```powershell
$baseUrl = "http://localhost:5000"
$email = "qa.rider.$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())@example.com"
$password = "StrongPass1!"
$accessToken = ""
$refreshToken = ""
$profileId = ""
$vehicleId = ""
$emergencyContactId = ""
$emergencyContactLinkingCode = ""
$activationCode = ""
$mobileDeviceId = ""
$smartwatchDeviceId = ""
$subscriptionId = ""
```

Base URLs:

- Local: `http://localhost:5000`
- Produccion: configurar segun ambiente desplegado, por ejemplo `https://<api-production-host>`

## 1. Register

```powershell
curl.exe -i -X POST "$baseUrl/api/v1/auth/register" `
  -H "Content-Type: application/json" `
  -d "{`"email`":`"$email`",`"password`":`"$password`",`"confirmPassword`":`"$password`",`"fullName`":`"QA Rider`",`"phoneNumber`":`"+52 5512345678`",`"accountType`":`"Conductor`",`"acceptTerms`":true}"
```

Esperado: `201 Created`, `data.user.role = "Rider"`.

## 2. Login

```powershell
curl.exe -i -X POST "$baseUrl/api/v1/auth/login" `
  -H "Content-Type: application/json" `
  -d "{`"email`":`"$email`",`"password`":`"$password`",`"rememberMe`":true}"
```

Esperado: `200 OK`. Copiar `data.accessToken` a `$accessToken` y `data.refreshToken` a `$refreshToken`.

## 3. Users/me

```powershell
curl.exe -i -X GET "$baseUrl/api/v1/users/me" `
  -H "Authorization: Bearer $accessToken"
```

Esperado: `200 OK`, usuario autenticado sin `passwordHash`.

## 4. Onboarding Status Inicial

```powershell
curl.exe -i -X GET "$baseUrl/api/v1/onboarding/status" `
  -H "Authorization: Bearer $accessToken"
```

Esperado: `1/7`, `14%`, `currentStep = "Profile"`, `isOperational = false`.

## 5. Profile Continue

```powershell
curl.exe -i -X PUT "$baseUrl/api/v1/profiles/me" `
  -H "Authorization: Bearer $accessToken" `
  -H "Content-Type: application/json" `
  -d "{`"fullName`":`"QA Rider`",`"phoneNumber`":`"+52 5512345678`",`"dateOfBirth`":`"1995-01-15`",`"curpOrIdentifier`":`"optional`",`"addressOrZone`":`"Colonia Centro`",`"primaryCity`":`"Toluca`",`"bloodType`":`"O+`",`"allergies`":`"Ninguna`",`"medicalConditions`":`"Ninguna`",`"provisionalEmergencyContactName`":`"Maria Lopez`",`"provisionalEmergencyContactPhone`":`"+52 5511112233`",`"saveMode`":`"Continue`"}"
```

Esperado: `200 OK`. Copiar `data.profile.id` a `$profileId`. Status esperado: `2/7`, `29%`, `Vehicle`.

## 6. Vehicle Continue

```powershell
curl.exe -i -X POST "$baseUrl/api/v1/vehicles" `
  -H "Authorization: Bearer $accessToken" `
  -H "Content-Type: application/json" `
  -d "{`"vehicleType`":`"Motorcycle`",`"brand`":`"Yamaha`",`"model`":`"FZ 2.0`",`"year`":2022,`"alias`":`"Mi moto QA`",`"primaryUse`":`"Personal`",`"color`":`"Rojo`",`"plateNumber`":`"QA1234`",`"vin`":`"QA-VIN-123456789`",`"usageFrequency`":`"Daily`",`"saveMode`":`"Continue`"}"
```

Esperado: `201 Created`. Copiar `data.vehicle.id` a `$vehicleId`. Status esperado: `3/7`, `43%`, `EmergencyContacts`.

## 7. EmergencyContact Continue

```powershell
curl.exe -i -X POST "$baseUrl/api/v1/emergency-contacts" `
  -H "Authorization: Bearer $accessToken" `
  -H "Content-Type: application/json" `
  -d "{`"fullName`":`"Maria Lopez`",`"relationship`":`"Esposa`",`"phoneNumber`":`"+52 5512345678`",`"email`":`"maria.lopez@example.com`",`"priority`":1,`"permissions`":{`"canViewRealTimeLocation`":true,`"canReceiveCriticalAlerts`":true,`"canViewIncidentHistory`":false,`"canViewVitalSigns`":false},`"saveMode`":`"Continue`"}"
```

Esperado: `201 Created`. Copiar `data.contact.id` a `$emergencyContactId`.

## 8. Invite Emergency Contact

```powershell
curl.exe -i -X POST "$baseUrl/api/v1/emergency-contacts/$emergencyContactId/invite" `
  -H "Authorization: Bearer $accessToken"
```

Esperado: `200 OK`. Copiar `data.contact.linkingCode` a `$emergencyContactLinkingCode`. Status esperado: `4/7`, `57%`, `Devices`.

Nota: `GET /api/v1/emergency-contacts/invitations/{code}` requiere JWT porque esta dentro de `RequireAuthorization()`.

## 9. Generate Mobile Activation Code

```powershell
curl.exe -i -X POST "$baseUrl/api/v1/devices/mobile/activation-code" `
  -H "Authorization: Bearer $accessToken"
```

Esperado: `200 OK`. Copiar `data.activationCode.code` a `$activationCode`.

## 10. Current Activation Code

```powershell
curl.exe -i -X GET "$baseUrl/api/v1/devices/activation-codes/current" `
  -H "Authorization: Bearer $accessToken"
```

Esperado: `200 OK`, mismo codigo que `$activationCode`.

## 11. Link MobileApp

```powershell
curl.exe -i -X POST "$baseUrl/api/v1/devices/mobile/link" `
  -H "Authorization: Bearer $accessToken" `
  -H "Content-Type: application/json" `
  -d "{`"code`":`"$activationCode`",`"deviceName`":`"QA Android Phone`",`"platform`":`"Android`",`"manufacturer`":`"Motorola`",`"model`":`"Edge 40`",`"operatingSystemVersion`":`"14`",`"appVersion`":`"1.0.0`",`"deviceIdentifier`":`"qa-mobile-device-$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())`"}"
```

Esperado: `200 OK`. Copiar `data.device.id` a `$mobileDeviceId`. Status esperado: `5/7`, `71%`, `Plan`. Validar que no aparece `deviceIdentifier` ni `deviceIdentifierHash`.

## 12. Optional Smartwatch Link

Estado: legado / no aplicable para la decision vigente de Wear OS.

La vinculacion del smartwatch ahora es local entre Android y Wear OS mediante Wear OS Data Layer. MotoSOS.API no debe administrar pairing, QR, codigos, nodeId, Bluetooth ni estado Connected/Disconnected del reloj. Este ejemplo queda solo como contexto historico de la propuesta anterior y no debe usarse para validar nuevas implementaciones.

```powershell
curl.exe -i -X POST "$baseUrl/api/v1/devices/smartwatch/link" `
  -H "Authorization: Bearer $accessToken" `
  -H "Content-Type: application/json" `
  -d "{`"parentDeviceId`":`"$mobileDeviceId`",`"deviceName`":`"QA Galaxy Watch`",`"platform`":`"WearOS`",`"manufacturer`":`"Samsung`",`"model`":`"Galaxy Watch 6`",`"operatingSystemVersion`":`"Wear OS 4`",`"appVersion`":`"1.0.0`",`"deviceIdentifier`":`"qa-watch-device-$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())`",`"batteryLevel`":80}"
```

Esperado: `200 OK`. Copiar `data.device.id` a `$smartwatchDeviceId`. Smartwatch es opcional y no cambia el progreso.

## 13. Get Plans

```powershell
curl.exe -i -X GET "$baseUrl/api/v1/plans" `
  -H "Authorization: Bearer $accessToken"
```

Esperado: `200 OK`, catalogo con Basic.

## 14. Get Subscription

```powershell
curl.exe -i -X GET "$baseUrl/api/v1/subscriptions/me" `
  -H "Authorization: Bearer $accessToken"
```

Esperado antes de seleccionar Basic: `data.subscription = null`, `data.defaultPlan.tier = "Basic"`.

## 15. Select Basic

```powershell
curl.exe -i -X POST "$baseUrl/api/v1/subscriptions/select-basic" `
  -H "Authorization: Bearer $accessToken"
```

Esperado: `200 OK`. Copiar `data.subscription.id` a `$subscriptionId`. Status esperado: `6/7`, `86%`, `Confirmation`.

## 16. Onboarding Summary

```powershell
curl.exe -i -X GET "$baseUrl/api/v1/onboarding/summary" `
  -H "Authorization: Bearer $accessToken"
```

Esperado: `canConfirm = true`, `isConfirmed = false`, `isOperational = false`, `6/7`, `86%`, `Confirmation`.

## 17. Confirm Onboarding

```powershell
curl.exe -i -X POST "$baseUrl/api/v1/onboarding/confirm" `
  -H "Authorization: Bearer $accessToken"
```

Esperado: `200 OK`, `7/7`, `100%`, `currentStep = "Completed"`, `isOperational = true`.

## 18. Final Onboarding Status

```powershell
curl.exe -i -X GET "$baseUrl/api/v1/onboarding/status" `
  -H "Authorization: Bearer $accessToken"
```

Esperado: `7/7`, `100%`, `currentStep = "Completed"`, `isOperational = true`.

## 19. Refresh Token

```powershell
curl.exe -i -X POST "$baseUrl/api/v1/auth/refresh" `
  -H "Content-Type: application/json" `
  -d "{`"refreshToken`":`"$refreshToken`"}"
```

Esperado: `200 OK`. Actualizar `$accessToken` y `$refreshToken` con los nuevos valores.

## 20. Logout

```powershell
curl.exe -i -X POST "$baseUrl/api/v1/auth/logout" `
  -H "Content-Type: application/json" `
  -d "{`"refreshToken`":`"$refreshToken`"}"
```

Esperado: `204 No Content`.

## Seguridad Y Errores Esperados

- Sin JWT en endpoints protegidos: `401 Unauthorized`.
- Usuario `Monitor` en endpoints Rider-only: `403 Forbidden`.
- Registro publico con `Admin`: `400 validation_error`.
- Confirmar onboarding incompleto: `400 onboarding_not_ready`.
- Codigo movil invalido/usado/expirado: `400 activation_code_invalid`.
- Recurso inexistente, ajeno o inactivo: `404 not_found`.
- Email duplicado: `409 user_already_exists`.
- Exceder limites Basic: `409 plan_limit_exceeded`.
- `POST /api/v1/auth/login-with-code`: `501 feature_not_implemented`.

## Faltantes Conocidos

- OTP real no esta implementado todavia.
- `POST /api/v1/auth/login-with-code` existe como stub y devuelve `501 feature_not_implemented`.
- Pagos reales no estan implementados.
- Google Play Billing no esta implementado.
- Stripe no esta implementado.
- Trips, SOS, Incidents, Notifications, Live Monitoring, Dashboard y ML siguen pendientes.
- `http://127.0.0.1:5173` no esta en `appsettings.Development.json`; si el frontend usa esa origin local, debe agregarse o configurarse por variable de entorno.
