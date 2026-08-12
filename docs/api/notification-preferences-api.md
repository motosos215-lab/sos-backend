# Notification Preferences API

Endpoints autenticados para que `Rider`, `Monitor` o `Admin` administren sus propias preferencias de notificacion. No existen endpoints para administrar preferencias de otro usuario.

## Endpoints

| Metodo | Ruta | Rol | Proposito |
| --- | --- | --- | --- |
| GET | `/api/v1/notification-preferences/me` | Auth | Obtener preferencias propias; crea defaults si no existen |
| PUT | `/api/v1/notification-preferences/me` | Auth | Actualizar preferencias propias |

## Defaults

```json
{
  "pushEnabled": true,
  "emailEnabled": false,
  "smsEnabled": false,
  "criticalAlertsEnabled": true,
  "tripUpdatesEnabled": true,
  "securityAlertsEnabled": true,
  "marketingEnabled": false,
  "quietHoursEnabled": false,
  "quietHoursStartLocal": null,
  "quietHoursEndLocal": null,
  "timeZone": "America/Mexico_City"
}
```

## PUT Body

```json
{
  "pushEnabled": true,
  "emailEnabled": false,
  "smsEnabled": false,
  "criticalAlertsEnabled": true,
  "tripUpdatesEnabled": true,
  "securityAlertsEnabled": true,
  "marketingEnabled": false,
  "quietHoursEnabled": true,
  "quietHoursStartLocal": "22:00",
  "quietHoursEndLocal": "06:00",
  "timeZone": "America/Mexico_City"
}
```

`quietHoursStartLocal` y `quietHoursEndLocal` usan formato `HH:mm`, son requeridos cuando `quietHoursEnabled=true` y deben ser `null` cuando `quietHoursEnabled=false`.

## Alcance Actual

- Preferences se aplican a emergency notification attempts solo para contactos enlazados por `LinkedUserId`.
- Si el contacto no esta enlazado, se mantiene el comportamiento por snapshot actual y no se consultan preferencias.
- `PushEnabled`, `EmailEnabled` y `SmsEnabled` gobiernan si se preparan attempts de esos canales para el monitor enlazado.
- `EmailEnabled=true` permite preparar attempts `Email`; el envio real depende de `Notifications:Providers:Email` y del outbox.
- `CriticalAlertsEnabled` y Quiet Hours se guardan y exponen, pero no suprimen alertas criticas ni emergency attempts en esta version.
- Auth Codes (`forgot-password`, `request-access-code`, `reset-password`, `login-with-code`) no usan estas preferencias.
- Las respuestas no exponen `userId`, tokens FCM, hashes, telefonos, emails, SMTP config, credenciales ni connection strings.
