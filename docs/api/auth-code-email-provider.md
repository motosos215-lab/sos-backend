# Auth Code Email Provider

El provider `Email` entrega por SMTP los codigos temporales de Auth Codes usados por `forgot-password` y `request-access-code`.

## Variables DigitalOcean

- `AuthCodes__Provider=Email`
- `AuthCodes__Email__Enabled=true`
- `AuthCodes__Email__FromEmail=`
- `AuthCodes__Email__FromName=MotoSOS`
- `AuthCodes__Email__SmtpHost=`
- `AuthCodes__Email__SmtpPort=587`
- `AuthCodes__Email__SmtpUsername=`
- `AuthCodes__Email__SmtpPassword=`
- `AuthCodes__Email__UseSsl=true`

`AuthCodes__Email__SmtpPassword` debe configurarse como secret variable. No debe ir en codigo, tests, documentacion con valor real ni logs.

## Reglas

- `Provider=Email` requiere configuracion completa: enabled, from email, SMTP host, puerto, username y password.
- Si un ambiente no debe enviar email real, debe usar `AuthCodes__Provider=Simulated`.
- El codigo solo aparece en el cuerpo del email enviado al usuario.
- El codigo no se devuelve por API, no se guarda plano en MongoDB y no se registra en logs.
- Los logs no incluyen email completo, proposito, codigo, hash, SMTP host, username, password ni variables de entorno.
- Si el envio falla, `forgot-password` y `request-access-code` siguen respondiendo `204 No Content`; internamente el `AuthCode` queda con `DeliveryStatus = Failed`.

## Contenido

Password reset usa asunto: `MotoSOS - Código para restablecer contraseña`.

Access login usa asunto: `MotoSOS - Código de acceso`.

El cuerpo incluye MotoSOS, el codigo, vigencia en minutos y una indicacion para ignorar el mensaje si no fue solicitado.

## Fuera De Alcance

No implementa SMS, WhatsApp, OTP por telefono, links con tokens, panel admin ni proveedores multi-canal.
