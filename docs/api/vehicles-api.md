# Vehicles API

Vehicles API implementa el paso 3 del wizard web-first de MotoSOS: Motocicleta / Motoneta.

El portal web realiza el alta inicial del conductor y su vehiculo. La app movil se vincula despues mediante codigo o QR y consumira estos datos cuando se habilite la operacion. En esta etapa la app movil no da de alta vehiculos.

No hay carga real de fotos, tarjeta de circulacion ni documentos del vehiculo en esta fase.

## Reglas

- Todos los endpoints requieren JWT Bearer.
- Solo usuarios `Rider` pueden usar Vehicles API.
- `Conductor` del maquetado se guarda como `Rider`.
- `Monitor` y `Admin` reciben `403 forbidden`.
- Los endpoints operan siempre con el `userId` del JWT.
- No se puede consultar, actualizar ni eliminar vehiculos de otro usuario.
- DELETE es baja logica: `isActive = false`.
- Por plan Basico default se permite solo 1 vehiculo activo por usuario.
- No se permite cambiar `email`, `role`, `isActive`, permisos ni claims de usuario desde Vehicles.

## Onboarding

Vehicles integra el paso 3 del wizard:

- Perfil completado y sin vehiculo activo: `Vehicle = Pending`, `completedSteps = 2`, `progressPercentage = 29`, `currentStep = Vehicle`.
- Perfil completado y vehiculo Draft activo: `Vehicle = InProgress`, `completedSteps = 2`, `progressPercentage = 29`, `currentStep = Vehicle`.
- Perfil completado y vehiculo Completed activo: `Vehicle = Completed`, `completedSteps = 3`, `progressPercentage = 43`, `currentStep = EmergencyContacts`.
- Si Profile no esta Completed, Vehicle permanece Pending aunque exista un vehiculo Completed.
- `isOperational` sigue en `false`.

## GET /api/v1/vehicles

Devuelve vehiculos activos del usuario autenticado.

Response `200 OK`:

```json
{
  "success": true,
  "data": {
    "vehicles": [
      {
        "id": "64f000000000000000000001",
        "userId": "64f000000000000000000000",
        "vehicleType": "Motorcycle",
        "brand": "Yamaha",
        "model": "FZ 2.0",
        "year": 2022,
        "alias": "Mi moto",
        "primaryUse": "Personal",
        "color": "Rojo",
        "plateNumber": "ABC1234",
        "vin": "VIN123456789",
        "usageFrequency": "Daily",
        "completionStatus": "Completed",
        "isPrimary": true,
        "isActive": true,
        "createdAtUtc": "2026-08-04T12:00:00+00:00",
        "updatedAtUtc": "2026-08-04T12:05:00+00:00",
        "completedAtUtc": "2026-08-04T12:05:00+00:00"
      }
    ]
  },
  "error": null
}
```

## GET /api/v1/vehicles/{id}

Devuelve un vehiculo propio activo. Si no existe, esta inactivo o pertenece a otro usuario, devuelve `404 not_found`.

## POST /api/v1/vehicles

Crea un vehiculo como Draft o Completed segun `saveMode`.

Request `Continue`:

```json
{
  "vehicleType": "Motorcycle",
  "brand": "Yamaha",
  "model": "FZ 2.0",
  "year": 2022,
  "alias": "Mi moto",
  "primaryUse": "Personal",
  "color": "Rojo",
  "plateNumber": "ABC1234",
  "vin": "VIN123456789",
  "usageFrequency": "Daily",
  "saveMode": "Continue"
}
```

Response `201 Created`:

```json
{
  "success": true,
  "data": {
    "vehicle": {
      "id": "64f000000000000000000001",
      "userId": "64f000000000000000000000",
      "vehicleType": "Motorcycle",
      "brand": "Yamaha",
      "model": "FZ 2.0",
      "year": 2022,
      "alias": "Mi moto",
      "primaryUse": "Personal",
      "color": "Rojo",
      "plateNumber": "ABC1234",
      "vin": "VIN123456789",
      "usageFrequency": "Daily",
      "completionStatus": "Completed",
      "isPrimary": true,
      "isActive": true,
      "createdAtUtc": "2026-08-04T12:00:00+00:00",
      "updatedAtUtc": "2026-08-04T12:00:00+00:00",
      "completedAtUtc": "2026-08-04T12:00:00+00:00"
    }
  },
  "error": null
}
```

Request `Draft` puede enviar datos parciales:

```json
{
  "vehicleType": "Motorcycle",
  "brand": "Yamaha",
  "alias": "Mi moto",
  "saveMode": "Draft"
}
```

## PUT /api/v1/vehicles/{id}

Actualiza un vehiculo propio activo. Usa el mismo shape de request que `POST`.

- `saveMode = Draft`: marca `completionStatus = Draft` y limpia `completedAtUtc`.
- `saveMode = Continue`: marca `completionStatus = Completed` y establece `completedAtUtc` si estaba null.

## DELETE /api/v1/vehicles/{id}

Baja logica del vehiculo propio activo.

Response `204 No Content`.

## Validaciones

Para `Continue` se requiere:

- `vehicleType`
- `brand`
- `model`
- `year`
- `alias`
- `primaryUse`
- `color`
- `plateNumber`
- `vin`
- `usageFrequency`

Valores permitidos:

- `vehicleType`: `Motorcycle`, `Scooter`.
- `primaryUse`: `Personal`, `Work`, `Delivery`, `Mixed`, `Other`.
- `usageFrequency`: `Daily`, `Weekly`, `Occasional`.
- `saveMode`: `Draft`, `Continue`.

Limites:

- `year`: 1950 a anio actual + 1.
- `brand`, `model`, `alias`: maximo 80 caracteres.
- `color`: maximo 50 caracteres.
- `plateNumber`: maximo 20 caracteres.
- `vin`: maximo 40 caracteres.

## Errores

- `400 validation_error`: request invalido.
- `401 unauthorized`: no hay token o token invalido.
- `403 forbidden`: usuario Monitor/Admin o rol no permitido.
- `404 not_found`: vehiculo inexistente, inactivo o ajeno.
- `409 plan_limit_exceeded`: plan Basico permite solo 1 vehiculo activo.

## MongoDB

Coleccion: `driverVehicles`.

Indices actuales:

- `UserId`.
- `UserId + IsActive`.
- `CompletionStatus`.

Pendientes futuros:

- Evaluar indice unico parcial por `UserId + PlateNumber` cuando `IsActive = true` y `PlateNumber` exista.
- Evaluar indice unico parcial por `UserId + Vin` cuando `IsActive = true` y `Vin` exista.
- Mejorar control de duplicados cuando exista el modulo Plans y reglas comerciales completas.
