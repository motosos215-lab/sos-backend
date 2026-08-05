# EmergencyContacts API

EmergencyContacts API implementa el paso 4 del wizard web-first: Contactos de emergencia.

El alta ocurre desde el portal web. La app movil no administra contactos en esta etapa. No hay envio real de SMS, correo ni notificaciones; `/invite` solo genera un codigo de vinculacion para mostrarlo en web o generar QR desde frontend.

## Reglas

- Todos los endpoints requieren JWT Bearer.
- Solo `Rider` puede administrar contactos del conductor.
- `Conductor` del maquetado se guarda como `Rider`.
- `Monitor` y `Admin` reciben `403 forbidden` en endpoints de administracion.
- Todo opera con el `userId` del JWT.
- Contactos ajenos devuelven `404 not_found`.
- Plan Basico default permite solo 1 contacto activo por usuario.
- DELETE es baja logica/revocacion.
- El codigo de vinculacion expira en 24 horas.
- No se implementa aceptacion real desde app monitor todavia.

## Onboarding

- Profile + Vehicle Completed + sin contacto: `3/7`, `43%`, `EmergencyContacts = Pending`, `currentStep = EmergencyContacts`.
- Contacto Draft/Pending: `EmergencyContacts = InProgress`, `3/7`, `43%`.
- Contacto Invited/Linked: `EmergencyContacts = Completed`, `4/7`, `57%`, `currentStep = Devices`.
- Si Vehicle no esta Completed, EmergencyContacts permanece Pending aunque exista contacto Invited.
- `isOperational` sigue `false`.

## POST /api/v1/emergency-contacts

Request `Continue`:

```json
{
  "fullName": "Maria Lopez",
  "relationship": "Esposa",
  "phoneNumber": "+52 5512345678",
  "email": "maria.lopez@gmail.com",
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

Response `201 Created`:

```json
{
  "success": true,
  "data": {
    "contact": {
      "id": "64f000000000000000000001",
      "userId": "64f000000000000000000000",
      "fullName": "Maria Lopez",
      "relationship": "Esposa",
      "phoneNumber": "+52 5512345678",
      "email": "maria.lopez@gmail.com",
      "priority": 1,
      "invitationStatus": "Pending",
      "linkingCode": null,
      "linkingCodeExpiresAtUtc": null,
      "linkedUserId": null,
      "permissions": {
        "canViewRealTimeLocation": true,
        "canReceiveCriticalAlerts": true,
        "canViewIncidentHistory": false,
        "canViewVitalSigns": false
      },
      "isPrimary": true,
      "isActive": true,
      "createdAtUtc": "2026-08-04T12:00:00+00:00",
      "updatedAtUtc": "2026-08-04T12:00:00+00:00",
      "invitedAtUtc": null,
      "linkedAtUtc": null,
      "revokedAtUtc": null
    }
  },
  "error": null
}
```

## GET /api/v1/emergency-contacts

Devuelve contactos activos del usuario autenticado.

## GET /api/v1/emergency-contacts/{id}

Devuelve un contacto propio activo. Contactos inexistentes, inactivos o ajenos devuelven `404 not_found`.

## PUT /api/v1/emergency-contacts/{id}

Actualiza un contacto propio activo. Usa el mismo shape que `POST`.

- `saveMode = Draft`: `InvitationStatus = Draft` si no esta Linked.
- `saveMode = Continue`: `InvitationStatus = Pending` si no esta Linked.

## DELETE /api/v1/emergency-contacts/{id}

Baja logica/revocacion:

- `isActive = false`.
- `invitationStatus = Revoked`.
- `revokedAtUtc = now`.
- `updatedAtUtc = now`.

Response `204 No Content`.

## POST /api/v1/emergency-contacts/{id}/invite

Genera o regenera codigo de vinculacion. No envia SMS/correo real.

Response `200 OK`:

```json
{
  "success": true,
  "data": {
    "contact": {
      "id": "64f000000000000000000001",
      "invitationStatus": "Invited",
      "linkingCode": "8X7Q-3M2K-9L6R",
      "linkingCodeExpiresAtUtc": "2026-08-05T12:00:00+00:00"
    }
  },
  "error": null
}
```

## GET /api/v1/emergency-contacts/invitations/{code}

Endpoint protegido con JWT. Devuelve informacion minima de una invitacion valida.

Response `200 OK`:

```json
{
  "success": true,
  "data": {
    "invitation": {
      "driverFullName": "Moto Rider",
      "contactFullName": "Maria Lopez",
      "permissions": {
        "canViewRealTimeLocation": true,
        "canReceiveCriticalAlerts": true,
        "canViewIncidentHistory": false,
        "canViewVitalSigns": false
      },
      "expiresAtUtc": "2026-08-05T12:00:00+00:00",
      "status": "Invited"
    }
  },
  "error": null
}
```

Si el codigo no existe, esta expirado, inactivo o revocado, devuelve `404 not_found`.

## Validaciones

Para `Continue` se requiere:

- `fullName` maximo 150.
- `relationship` maximo 80.
- `phoneNumber` con patron `^[+0-9 ()-]{7,20}$`.
- `email` con formato valido.
- `priority >= 1`.
- `saveMode`: `Draft` o `Continue`.

## Pendientes

- Envio real de SMS/correo.
- Notificaciones reales.
- `POST /api/v1/emergency-contacts/invitations/{code}/accept` para app monitor.
- Asociacion real de `linkedUserId`.
- Escalamiento real.
