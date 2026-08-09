# Devices / Mobile Linking API

## Descripcion

Devices API implementa el paso 5 del wizard web-first de MotoSOS: Vinculacion de dispositivos.

El portal web genera un codigo de activacion que puede mostrarse como texto o QR. La app movil, ya autenticada, usa ese codigo para vincular el telefono del conductor.

Decision vigente para Wear OS: el smartwatch se vincula unicamente local entre Android y Wear OS mediante Wear OS Data Layer. La API no administra pairing, QR, codigos, nodeId, Bluetooth ni estado Connected/Disconnected del reloj. El telefono actua como gateway y envia datos resumidos usando la sesion del Rider.

Devices API no implementa Bluetooth real, Wear OS Data Layer, viajes, SOS, notificaciones, pagos ni sincronizacion offline; esos flujos pertenecen a otros modulos o a la app movil.

## Reglas

- Solo usuarios `Rider` pueden usar este flujo. `Monitor` y `Admin` reciben `403`.
- `Rider` representa Rider/Conductor en el backend actual.
- El codigo de activacion expira en 15 minutos.
- Generar un codigo nuevo revoca codigos activos anteriores del mismo usuario.
- Un codigo usado, expirado, revocado, inexistente o ajeno devuelve `activation_code_invalid`.
- El plan Basico se asume por default y permite 1 `MobileApp` activa/vinculada por usuario.
- `Smartwatch` es opcional para completar Devices.
- La administracion remota de smartwatch por la API queda reemplazada por vinculacion local Android + Wear OS.
- La regla historica de `Smartwatch` dependiente de `MobileApp` por `parentDeviceId` queda como contexto legado, no como direccion para nuevas implementaciones.
- `deviceIdentifier` se guarda como hash y no se devuelve en responses.

## Onboarding

- Sin `MobileApp` activa `Linked`, Devices queda `Pending`, `completedSteps = 4`, `progressPercentage = 57`, `currentStep = Devices`.
- Con `MobileApp` activa `Linked`, Devices queda `Completed`, `completedSteps = 5`, `progressPercentage = 71`, `currentStep = Plan`.
- `isOperational` sigue en `false`.

## Endpoints

Todos requieren JWT Bearer.

### GET /api/v1/devices

Devuelve dispositivos activos del usuario autenticado.

### POST /api/v1/devices/mobile/activation-code

Genera un codigo para vincular app movil y revoca codigos activos anteriores.

Response:

```json
{
  "success": true,
  "data": {
    "activationCode": {
      "code": "MSOS-8X7Q-3M2K",
      "expiresAtUtc": "2026-08-05T12:15:00+00:00"
    }
  },
  "error": null
}
```

### GET /api/v1/devices/activation-codes/current

Devuelve el codigo activo vigente del usuario autenticado. No genera ni revoca codigos.

Si no hay codigo activo:

```json
{
  "success": true,
  "data": {
    "activationCode": null
  },
  "error": null
}
```

### POST /api/v1/devices/mobile/link

Uso principal: app movil autenticada.

Request:

```json
{
  "code": "MSOS-8X7Q-3M2K",
  "deviceName": "Motorola Edge",
  "platform": "Android",
  "manufacturer": "Motorola",
  "model": "Edge 40",
  "operatingSystemVersion": "14",
  "appVersion": "1.0.0",
  "deviceIdentifier": "local-device-id-from-mobile"
}
```

### POST /api/v1/devices/smartwatch/link

Estado: legado / decision reemplazada.

Este endpoint pertenece a la propuesta anterior donde la API recibia el reporte remoto de vinculacion del smartwatch. La decision vigente es no crear ni extender pairing API de smartwatch: Android movil administra localmente el pairing Wear OS mediante Wear OS Data Layer y la API no guarda nodeId, no administra Bluetooth, no administra QR ni codigos de smartwatch y no registra estado Connected/Disconnected del reloj.

Uso principal: app movil despues de emparejar smartwatch.

Request:

```json
{
  "parentDeviceId": "mobile-device-id",
  "deviceName": "Galaxy Watch",
  "platform": "WearOS",
  "manufacturer": "Samsung",
  "model": "Galaxy Watch 6",
  "operatingSystemVersion": "Wear OS 4",
  "appVersion": "1.0.0",
  "deviceIdentifier": "local-watch-id",
  "batteryLevel": 80
}
```

### PATCH /api/v1/devices/{id}/heartbeat

Actualiza estado operativo del dispositivo.

Request:

```json
{
  "batteryLevel": 87,
  "connectionStatus": "Online",
  "appVersion": "1.0.0"
}
```

### POST /api/v1/devices/{id}/revoke

Baja logica del dispositivo. Devuelve `204 No Content`.

## Errores Esperados

- `401 unauthorized`: sin token o token invalido.
- `403 forbidden`: usuario no Rider.
- `400 validation_error`: payload invalido.
- `400 activation_code_invalid`: codigo invalido, ajeno, expirado, usado o revocado.
- `404 not_found`: dispositivo inexistente o ajeno.
- `409 plan_limit_exceeded`: segundo `MobileApp` activo en plan Basico.
