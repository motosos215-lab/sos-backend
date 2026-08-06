# Plans / Licenses API

## Descripcion

Plans API implementa el paso 6 del wizard web-first de MotoSOS: Plan y licencia.

En esta etapa el portal web permite consultar el catalogo de planes, consultar la suscripcion actual del conductor y confirmar el plan Basico. No hay pagos reales, Google Play Billing, Stripe, renovaciones, cupones ni licenciamiento empresarial real.

## Flujo Web-First

- El conductor completa cuenta, perfil, vehiculo, contactos y vinculacion movil.
- El portal web muestra Basic, Plus y Familiar / Pro.
- Solo Basic se puede seleccionar desde web en esta etapa.
- Plus queda preparado para compra futura desde app movil con Google Play.
- Familiar / Pro queda preparado para licencia empresarial futura.

## Reglas

- Solo `Rider` puede usar Plans API. `Rider` representa Rider/Conductor en el backend actual.
- `Monitor` y `Admin` reciben `403`.
- Si no existe suscripcion, `GET /api/v1/subscriptions/me` devuelve `subscription: null` y `defaultPlan: Basic`.
- `POST /api/v1/subscriptions/select-basic` crea o actualiza una suscripcion `Basic Active` con source `WebBasic`.
- Seleccionar Basic dos veces es idempotente y no duplica documentos.
- No se devuelven datos de pago ni tokens de proveedores externos.

## Limites

Plans documenta limites por plan, pero en esta etapa no reemplaza las reglas locales ya validadas.

- Vehicles mantiene Basic = 1 vehiculo activo.
- EmergencyContacts mantiene Basic = 1 contacto activo.
- Plans sera la fuente central de limites en una etapa futura.

## Onboarding

- Sin suscripcion activa: `completedSteps = 5`, `progressPercentage = 71`, `currentStep = Plan`.
- Con suscripcion activa: `completedSteps = 6`, `progressPercentage = 86`, `currentStep = Confirmation`.
- Confirmation sigue `Pending`.
- `isOperational` sigue en `false`.

## Endpoints

Todos requieren JWT Bearer.

### GET /api/v1/plans

Devuelve el catalogo visible para el portal web.

### GET /api/v1/subscriptions/me

Devuelve la suscripcion actual del usuario autenticado.

Respuesta sin suscripcion:

```json
{
  "success": true,
  "data": {
    "subscription": null,
    "defaultPlan": {
      "tier": "Basic",
      "name": "Básico",
      "description": "Plan incluido con tu cuenta.",
      "isDefault": true,
      "isSelectableInWeb": true,
      "isPaid": false,
      "benefits": ["1 contacto de emergencia", "1 vehículo"],
      "limits": {
        "maxEmergencyContacts": 1,
        "maxVehicles": 1
      }
    }
  },
  "error": null
}
```

### POST /api/v1/subscriptions/select-basic

Confirma el plan Basic desde web.

Request:

```json
{}
```

Response:

```json
{
  "success": true,
  "data": {
    "subscription": {
      "id": "string",
      "userId": "string",
      "planTier": "Basic",
      "status": "Active",
      "source": "WebBasic",
      "startedAtUtc": "2026-08-05T12:00:00+00:00",
      "expiresAtUtc": null,
      "cancelledAtUtc": null,
      "confirmedAtUtc": "2026-08-05T12:00:00+00:00",
      "createdAtUtc": "2026-08-05T12:00:00+00:00",
      "updatedAtUtc": "2026-08-05T12:00:00+00:00"
    }
  },
  "error": null
}
```

## Errores Esperados

- `401 unauthorized`: sin token o token invalido.
- `403 forbidden`: usuario no Rider.
- `400 validation_error`: payload invalido, si aplica en endpoints futuros.
- `404 not_found`: reservado para futuros flujos que requieran recursos especificos.

## Pendientes

- Google Play Billing real.
- Licencias empresariales reales.
- Stripe u otros proveedores, si se decide usarlos.
- Renovaciones automaticas.
- Facturacion, cupones y upgrades.
- Centralizar limites de Vehicles, EmergencyContacts y Devices desde Plans.
