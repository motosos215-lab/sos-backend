# Onboarding y Perfil API

MotoSOS usa un flujo web-first: el portal web realiza registro, inicio de sesion y configuracion inicial del conductor. La app movil se vincula despues mediante codigo o QR y se usa principalmente para operacion: iniciar viaje, monitoreo, SOS, sensores, sincronizacion offline y smartwatch.

La app movil no sustituye el alta inicial. El smartwatch se vincula desde la app movil, no desde la web.

MongoDB es la base central. Ningun cliente se conecta directamente a MongoDB. SQLite queda reservado para almacenamiento local/offline de la app movil en una etapa futura y sincronizara mediante endpoints de la API.

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

## Autorizacion

Todos los endpoints requieren JWT Bearer.

```http
Authorization: Bearer <accessToken>
```

Para esta etapa solo `Rider` puede usar este flujo. El registro publico acepta `Conductor`, pero el backend lo guarda como `Rider`.

`Monitor` y `Admin` reciben `403 forbidden` en `GET /api/v1/onboarding/status` y `PUT /api/v1/profiles/me` porque tendran flujos separados en etapas posteriores.

## GET /api/v1/onboarding/status

Devuelve el avance del wizard web-first del conductor.

Response inicial `200 OK`:

```json
{
  "success": true,
  "data": {
    "totalSteps": 7,
    "completedSteps": 1,
    "progressPercentage": 14,
    "currentStep": "Profile",
    "isOperational": false,
    "steps": [
      {
        "key": "Account",
        "order": 1,
        "label": "Cuenta",
        "status": "Completed"
      },
      {
        "key": "Profile",
        "order": 2,
        "label": "Perfil",
        "status": "Pending"
      },
      {
        "key": "Vehicle",
        "order": 3,
        "label": "Motocicleta / Motoneta",
        "status": "Pending"
      },
      {
        "key": "EmergencyContacts",
        "order": 4,
        "label": "Contactos de emergencia",
        "status": "Pending"
      },
      {
        "key": "Devices",
        "order": 5,
        "label": "Vinculación de dispositivos",
        "status": "Pending"
      },
      {
        "key": "Plan",
        "order": 6,
        "label": "Plan y licencia",
        "status": "Pending"
      },
      {
        "key": "Confirmation",
        "order": 7,
        "label": "Confirmación",
        "status": "Pending"
      }
    ]
  },
  "error": null
}
```

Cuando el perfil esta en borrador, `Profile` cambia a `InProgress`, pero el avance sigue en `1/7` y `14%`.

Cuando el perfil esta completado:

```json
{
  "success": true,
  "data": {
    "totalSteps": 7,
    "completedSteps": 2,
    "progressPercentage": 29,
    "currentStep": "Vehicle",
    "isOperational": false,
    "steps": [
      {
        "key": "Account",
        "order": 1,
        "label": "Cuenta",
        "status": "Completed"
      },
      {
        "key": "Profile",
        "order": 2,
        "label": "Perfil",
        "status": "Completed"
      },
      {
        "key": "Vehicle",
        "order": 3,
        "label": "Motocicleta / Motoneta",
        "status": "Pending"
      },
      {
        "key": "EmergencyContacts",
        "order": 4,
        "label": "Contactos de emergencia",
        "status": "Pending"
      },
      {
        "key": "Devices",
        "order": 5,
        "label": "Vinculación de dispositivos",
        "status": "Pending"
      },
      {
        "key": "Plan",
        "order": 6,
        "label": "Plan y licencia",
        "status": "Pending"
      },
      {
        "key": "Confirmation",
        "order": 7,
        "label": "Confirmación",
        "status": "Pending"
      }
    ]
  },
  "error": null
}
```

`isOperational` permanece `false` hasta que los 7 pasos esten completos.

## GET /api/v1/profiles/me

Devuelve el perfil del conductor autenticado. Si no existe perfil, devuelve un objeto inicial vacio sin error.

Response `200 OK`:

```json
{
  "success": true,
  "data": {
    "profile": {
      "id": null,
      "userId": "64f000000000000000000000",
      "fullName": "Moto Rider",
      "email": "rider@example.com",
      "phoneNumber": "+52 555 555 5555",
      "dateOfBirth": null,
      "curpOrIdentifier": null,
      "addressOrZone": null,
      "primaryCity": null,
      "bloodType": null,
      "allergies": null,
      "medicalConditions": null,
      "provisionalEmergencyContactName": null,
      "provisionalEmergencyContactPhone": null,
      "licenseDocumentStatus": "NotUploaded",
      "completionStatus": "Draft",
      "createdAtUtc": null,
      "updatedAtUtc": null,
      "completedAtUtc": null
    }
  },
  "error": null
}
```

## PUT /api/v1/profiles/me

Funciona como upsert. Si no existe perfil, lo crea. Si existe, lo actualiza.

Request `Draft`:

```json
{
  "fullName": "Moto Rider",
  "phoneNumber": null,
  "dateOfBirth": null,
  "curpOrIdentifier": null,
  "addressOrZone": "Colonia Centro",
  "primaryCity": null,
  "bloodType": null,
  "allergies": null,
  "medicalConditions": null,
  "provisionalEmergencyContactName": null,
  "provisionalEmergencyContactPhone": null,
  "saveMode": "Draft"
}
```

Request `Continue`:

```json
{
  "fullName": "Moto Rider",
  "phoneNumber": "+52 555 555 5555",
  "dateOfBirth": "1995-01-15",
  "curpOrIdentifier": "optional",
  "addressOrZone": "Colonia Centro",
  "primaryCity": "Toluca",
  "bloodType": "O+",
  "allergies": "Ninguna",
  "medicalConditions": "Ninguna",
  "provisionalEmergencyContactName": "Contacto Principal",
  "provisionalEmergencyContactPhone": "+52 555 111 2233",
  "saveMode": "Continue"
}
```

Response `200 OK`:

```json
{
  "success": true,
  "data": {
    "profile": {
      "id": "64f000000000000000000001",
      "userId": "64f000000000000000000000",
      "fullName": "Moto Rider",
      "email": "rider@example.com",
      "phoneNumber": "+52 555 555 5555",
      "dateOfBirth": "1995-01-15",
      "curpOrIdentifier": "optional",
      "addressOrZone": "Colonia Centro",
      "primaryCity": "Toluca",
      "bloodType": "O+",
      "allergies": "Ninguna",
      "medicalConditions": "Ninguna",
      "provisionalEmergencyContactName": "Contacto Principal",
      "provisionalEmergencyContactPhone": "+52 555 111 2233",
      "licenseDocumentStatus": "NotUploaded",
      "completionStatus": "Completed",
      "createdAtUtc": "2026-08-04T12:00:00+00:00",
      "updatedAtUtc": "2026-08-04T12:05:00+00:00",
      "completedAtUtc": "2026-08-04T12:05:00+00:00"
    }
  },
  "error": null
}
```

Campos controlados:

- `fullName` y `phoneNumber` pueden actualizar datos de `User`.
- `email` no se puede cambiar en esta etapa.
- Campos inesperados como `role`, `isActive`, permisos o claims se ignoran.

Validaciones de `Continue`:

- `fullName` requerido.
- `phoneNumber` requerido y con formato valido.
- `dateOfBirth` requerido.
- `addressOrZone` requerido.
- `primaryCity` requerido.
- `provisionalEmergencyContactName` requerido.
- `provisionalEmergencyContactPhone` requerido y con formato valido.

No se requiere CURP, grupo sanguineo, alergias, condiciones medicas ni documento/licencia en esta etapa.
