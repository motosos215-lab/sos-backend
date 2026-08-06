# Onboarding Confirmation API

## Descripcion

Onboarding Confirmation API implementa el paso 7 del wizard web-first de MotoSOS: Confirmacion / activacion de cuenta operativa.

Este paso cierra la configuracion inicial del conductor. No activa viajes reales, SOS real, deteccion de accidente real, notificaciones reales, monitoreo en vivo, dashboard operativo ni Machine Learning.

`isOperational = true` significa que la configuracion inicial esta completa y que el usuario puede avanzar hacia los modulos operativos futuros.

## Flujo Web-First

- El usuario crea cuenta e inicia sesion.
- Completa perfil, vehiculo, contacto de emergencia, vinculacion movil y plan Basic.
- El portal web muestra un resumen final.
- El usuario presiona finalizar configuracion o activar cuenta.
- La API guarda metadata de confirmacion en `onboardingConfirmations`.

## Criterios Para Confirmar

`canConfirm = true` solo si estan completos:

- Account
- Profile
- Vehicle
- EmergencyContacts
- Devices
- Plan

Si falta cualquier paso previo, `POST /api/v1/onboarding/confirm` devuelve `onboarding_not_ready`.

## Criterios Para isOperational

- Sin confirmacion: `isOperational = false`.
- Con confirmacion y pasos previos aun completos: `isOperational = true`.
- Si existe confirmacion vieja pero un paso previo queda incompleto: `isOperational = false`.

## Endpoints

Todos requieren JWT Bearer y solo permiten `Rider`.

### GET /api/v1/onboarding/summary

Devuelve el resumen final del wizard.

Response parcial:

```json
{
  "success": true,
  "data": {
    "summary": {
      "canConfirm": true,
      "isConfirmed": false,
      "isOperational": false,
      "completedSteps": 6,
      "progressPercentage": 86,
      "currentStep": "Confirmation"
    }
  },
  "error": null
}
```

El resumen puede incluir usuario, perfil, vehiculo, contacto de emergencia, app movil, smartwatch opcional, suscripcion y pasos.

No devuelve `passwordHash`, refresh tokens, `deviceIdentifier`, `deviceIdentifierHash` ni datos de pago.

### POST /api/v1/onboarding/confirm

Confirma el cierre del wizard. Es idempotente.

Request:

```json
{}
```

Response:

```json
{
  "success": true,
  "data": {
    "onboarding": {
      "totalSteps": 7,
      "completedSteps": 7,
      "progressPercentage": 100,
      "currentStep": "Completed",
      "isOperational": true
    }
  },
  "error": null
}
```

## Errores Esperados

- `401 unauthorized`: sin token o token invalido.
- `403 forbidden`: usuario no Rider.
- `400 onboarding_not_ready`: faltan pasos previos.
- `400 validation_error`: reservado para validaciones generales futuras.

## Persistencia

Coleccion MongoDB: `onboardingConfirmations`.

Campos principales:

- `UserId`
- `ConfirmedAtUtc`
- `IsOperational`
- `CreatedAtUtc`
- `UpdatedAtUtc`

No se guardan snapshots grandes ni informacion sensible.

## Pendientes

- Trips reales.
- SOS real.
- Incidents.
- Notifications y push notifications.
- Live Monitoring.
- Dashboard operativo.
- Machine Learning.
