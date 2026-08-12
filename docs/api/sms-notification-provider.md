# SMS Notification Provider

Provider Brevo SMS para enviar emergency notifications reales por SMS desde `NotificationOutboxWorker` o desde el run manual Admin del outbox.

## Configuracion

Seccion separada:

```text
Notifications:Providers:Sms
```

Variables DigitalOcean esperadas:

```text
Notifications__Providers__Sms__Enabled=true
Notifications__Providers__Sms__Provider=Brevo
Notifications__Providers__Sms__ApiKey=
Notifications__Providers__Sms__Sender=MotoSOS
Notifications__Providers__Sms__DefaultCountryCode=+52
Notifications__Providers__Sms__TimeoutSeconds=15
```

`ApiKey` debe configurarse como secret variable.

## Alcance

- Aplica solo a `NotificationDeliveryAttempt` con `channel = Sms`.
- Si esta habilitado y configurado, envia SMS real por Brevo SMS.
- Si esta deshabilitado, `Sms` usa `SimulatedNotificationProvider`.
- Si falta configuracion o Brevo falla, el attempt queda `Failed` con `Provider = Sms` y error controlado.
- Si envia correctamente, el attempt conserva estado legacy `SimulatedSent` y queda con `Provider = Sms`.
- No envia OTP ni codigos de autenticacion.
- Auth Codes no usan este provider.

## Contenido

El SMS usa texto corto y seguro:

```text
MotoSOS: alerta de emergencia detectada. Abre la app MotoSOS para revisar detalles. ID: {notificationDeliveryAttemptId}
```

No incluye ubicacion exacta, links con tokens, telefonos/emails de otros contactos, credenciales, tokens, OTP ni payloads completos.

## Telefono

Normalizacion minima:

- Si el numero ya viene con `+`, se conserva despues de limpiar espacios, guiones y parentesis.
- Si no viene con `+`, se antepone `DefaultCountryCode`.
- Valores vacios o invalidos fallan de forma controlada.

No se loguea ni expone el telefono completo.

## Provider Status

`GET /api/v1/admin/notifications/providers/status` expone solo metadata segura:

- `smsProviderEnabled`
- `smsProviderConfigured`
- `smsProviderName`
- `realSmsEnabled`

No expone API key, telefonos, payloads, tokens, connection strings ni configuracion completa.
