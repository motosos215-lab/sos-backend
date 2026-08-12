# Email Notification Provider

Provider SMTP para enviar emergency notifications reales por email desde `NotificationOutboxWorker` o desde el run manual Admin del outbox.

## Configuracion

Seccion separada de Auth Codes:

```text
Notifications:Providers:Email
```

Variables DigitalOcean esperadas:

```text
Notifications__Providers__Email__Enabled=true
Notifications__Providers__Email__FromEmail=
Notifications__Providers__Email__FromName=MotoSOS
Notifications__Providers__Email__SmtpHost=smtp-relay.brevo.com
Notifications__Providers__Email__SmtpPort=587
Notifications__Providers__Email__SmtpUsername=
Notifications__Providers__Email__SmtpPassword=
Notifications__Providers__Email__UseSsl=true
```

`SmtpPassword` debe configurarse como secret variable. `SmtpUsername` tambien puede configurarse como secret variable.

## Alcance

- Aplica solo a `NotificationDeliveryAttempt` con `channel = Email`.
- Si esta habilitado y configurado, envia email real por SMTP/Brevo.
- Si esta deshabilitado, `Email` usa `SimulatedNotificationProvider`.
- Si falta configuracion o SMTP falla, el attempt queda `Failed` con `Provider = Email` y error controlado.
- Si envia correctamente, el attempt conserva estado legacy `SimulatedSent` y queda con `Provider = Email`.
- No envia OTP ni codigos de autenticacion.
- Auth Codes mantienen configuracion y provider independientes bajo `AuthCodes:Email`.

## Contenido

El email usa asunto `MotoSOS - Alerta de emergencia` y body texto plano con informacion minima:

- Alerta de emergencia detectada.
- Instruccion de abrir la app MotoSOS.
- `incidentId`.
- `alertDispatchId`.
- `notificationDeliveryAttemptId`.
- Canal `Email`.

No incluye ubicacion exacta, links con tokens, telefonos/emails de otros contactos, credenciales, tokens, refresh tokens, OTP ni payloads completos.

## Provider Status

`GET /api/v1/admin/notifications/providers/status` expone solo metadata segura:

- `emailProviderEnabled`
- `emailProviderConfigured`
- `emailConfiguredSource`
- `realEmailEnabled`

No expone SMTP password, username, host, credenciales FCM, connection strings ni configuracion completa.
