# Memorias Tecnicas

## Trip Route Points API

- Trip Route Points API agrega `POST /api/v1/trips/{tripId}/route-points/batch` y `GET /api/v1/trips/{tripId}/route` para Rider autenticado.
- Los puntos GPS reales capturados por Android se guardan en la coleccion separada `tripRoutePoints`; no se embeben en `Trip`.
- Indices: unico `tripId + clientRoutePointId`, `tripId + sequence`, `userId + tripId` y `tripId + recordedAtUtc`.
- Idempotencia persistente por `tripId + clientRoutePointId`: `Accepted`, `Duplicate` o `Conflict` sin sobrescribir datos existentes.
- El batch rechaza errores estructurales completos, incluidos `clientRoutePointId` duplicados dentro del mismo request.
- Viajes `Active` aceptan puntos desde `StartedAtUtc`; viajes `Finished` aceptan sync offline dentro de `Trips__RoutePoints__OfflineSyncGraceHours`.
- `GET route` devuelve `full` ordenado por `sequence` o `preview` con downsampling simple conservando primer y ultimo punto.
- Android dibuja la Polyline con puntos reales; el backend no calcula rutas con Google, no guarda encoded polyline ni usa llaves de mapas.

## Evidence Binary Storage

- Evidence Attachments agrega upload/download binario real para incidentes mediante `multipart/form-data` y descarga stream por API.
- Storage usa abstraccion `IEvidenceFileStorageProvider`; proveedor implementado: `DigitalOceanSpacesEvidenceStorageProvider` compatible S3 con `AWSSDK.S3`.
- Configuracion separada `EvidenceStorage` con `Enabled`, `Provider`, `Bucket`, `Region`, `ServiceUrl`, `AccessKey`, `SecretKey`, `BasePath`, `UsePathStyle` y `MaxFileSizeBytes`.
- `AccessKey` y `SecretKey` deben ser secret variables; no se exponen bucket, object key, rutas internas ni URLs firmadas en respuestas publicas.
- Upload valida archivo requerido, tamaño, extension, content type, filename seguro y calcula SHA256 server-side.
- `clientEvidenceId` es opcional para multipart: si viene se usa idempotencia por `userId + incidentId + clientEvidenceId`; mismo archivo responde `isDuplicate=true`, archivo diferente devuelve `evidence_upload_conflict`.
- Si storage esta deshabilitado o incompleto, upload falla controladamente y no guarda metadata exitosa.
- Rider accede solo a incidentes propios; Monitor solo si esta vinculado por contacto y notification attempt; Admin puede descargar.
- Metadata-only existente se mantiene compatible; no se implementan URLs firmadas, CDN, antivirus, OCR, thumbnails ni procesamiento de imagen.

## Emergency SMS Notification Provider

- Emergency SMS Notification Provider agrega envio Brevo SMS para `NotificationDeliveryAttempt` con `Channel = Sms` desde Notification Outbox.
- La configuracion es separada bajo `Notifications:Providers:Sms` y usa variables DigitalOcean `Notifications__Providers__Sms__Enabled`, `Provider`, `ApiKey`, `Sender`, `DefaultCountryCode` y `TimeoutSeconds`.
- `Provider` soportado en esta version: `Brevo`; `ApiKey` debe ser secret variable.
- Si SMS real se envia correctamente, el attempt conserva estado legacy `SimulatedSent` por compatibilidad y queda con `Provider = Sms`.
- Si SMS falla o falta configuracion, el attempt queda `Failed`, `Provider = Sms` y `FailureReason` controlado sin exponer API key, telefono completo, body, payload ni configuracion.
- Si `Notifications:Providers:Sms:Enabled=false`, el canal `Sms` sigue usando `SimulatedNotificationProvider`.
- El SMS contiene solo alerta minima y `notificationDeliveryAttemptId`; no incluye ubicacion exacta, tokens, links con tokens ni datos sensibles.
- Notification Preferences siguen controlando si se crean attempts `Sms` para monitores enlazados; Auth Codes no usan este provider y no se implementa OTP por SMS.

## Emergency Email Notification Provider

- Emergency Email Notification Provider agrega envio SMTP/Brevo para `NotificationDeliveryAttempt` con `Channel = Email` desde Notification Outbox.
- La configuracion es separada de Auth Codes bajo `Notifications:Providers:Email` y usa variables DigitalOcean `Notifications__Providers__Email__Enabled`, `FromEmail`, `FromName`, `SmtpHost`, `SmtpPort`, `SmtpUsername`, `SmtpPassword` y `UseSsl`.
- Aunque Brevo pueda compartir credenciales operativas con Auth Codes, la API no reutiliza directamente el provider de Auth Codes ni mezcla OTP con emergency notifications.
- Si Email real se envia correctamente, el attempt conserva estado legacy `SimulatedSent` por compatibilidad y queda con `Provider = Email`.
- Si Email falla o falta configuracion, el attempt queda `Failed`, `Provider = Email` y `FailureReason` controlado sin exponer SMTP host, username, password, body, destinatario completo ni configuracion.
- Si `Notifications:Providers:Email:Enabled=false`, el canal `Email` sigue usando `SimulatedNotificationProvider`.
- `GET /api/v1/admin/notifications/providers/status` reporta estado seguro de Email (`emailProviderEnabled`, `emailProviderConfigured`, `emailConfiguredSource`, `realEmailEnabled`) sin credenciales.
- Notification Preferences siguen controlando si se crean attempts `Email` para monitores enlazados; Auth Codes no dependen de estas preferencias.

## Notification Preferences API

- Notification Preferences API agrega `GET /api/v1/notification-preferences/me` y `PUT /api/v1/notification-preferences/me` para que `Rider`, `Monitor` y `Admin` administren solo sus propias preferencias.
- Las preferencias se guardan en MongoDB en la coleccion `notificationPreferences` con indice unico por `UserId` y default `TimeZone = America/Mexico_City`.
- Defaults: `PushEnabled=true`, `EmailEnabled=false`, `SmsEnabled=false`, `CriticalAlertsEnabled=true`, `TripUpdatesEnabled=true`, `SecurityAlertsEnabled=true`, `MarketingEnabled=false` y Quiet Hours deshabilitado.
- `userId` sale siempre del JWT; propiedades extra como `userId` en body se rechazan con `validation_error`.
- Notifications prepare aplica preferencias solo a contactos enlazados por `LinkedUserId`; contactos no enlazados mantienen el comportamiento por snapshot actual.
- `PushEnabled`, `EmailEnabled` y `SmsEnabled` controlan attempts de canales para el Monitor enlazado; `CriticalAlertsEnabled` y Quiet Hours se guardan pero no suprimen emergency attempts en esta version.
- Auth Codes no usan estas preferencias: password reset y access code siguen independientes aunque `EmailEnabled=false`.
- Las respuestas no exponen FCM token, token hash/value, SMTP config, connection strings, emails, telefonos ni otros datos sensibles.

## Auth Code Email Provider

- Auth Codes agrega provider SMTP real con `AuthCodes__Provider=Email` y configuracion `AuthCodes__Email__Enabled`, `FromEmail`, `FromName`, `SmtpHost`, `SmtpPort`, `SmtpUsername`, `SmtpPassword` y `UseSsl`.
- `Provider=Email` requiere configuracion completa, incluido username/password; ambientes sin email real deben usar `Provider=Simulated`.
- `SmtpPassword` debe ser secret variable en DigitalOcean y no debe registrarse en codigo, docs con valor real, tests ni logs.
- El codigo solo aparece en el cuerpo del email; no se devuelve por API, no se guarda plano y no se loguea junto con email, proposito, hash ni configuracion SMTP.
- Si el envio falla, `forgot-password` y `request-access-code` siguen respondiendo `204 No Content`; internamente el codigo queda con `DeliveryStatus = Failed`.

## Auth Password Reset And Code Login

- Auth Codes agrega la coleccion `authCodes` para codigos temporales `PasswordReset` y `AccessLogin`, configurada por `AuthCodes__Enabled`, `AuthCodes__CodeLength`, `AuthCodes__TtlMinutes`, `AuthCodes__MaxAttempts`, `AuthCodes__RateLimitMinutes` y `AuthCodes__Provider`.
- Los codigos se generan con `RandomNumberGenerator` y se guardan solo hasheados reutilizando el password hasher actual; no se guarda ni loguea el codigo plano.
- `POST /api/v1/auth/forgot-password` y `POST /api/v1/auth/request-access-code` responden `204 No Content` de forma neutra aunque el email no exista, el usuario este inactivo, se alcance rate limit o Auth Codes este deshabilitado.
- `POST /api/v1/auth/reset-password` valida codigo, proposito, expiracion, intentos y usuario activo, cambia el password con el hasher actual, marca el codigo como usado y revoca refresh tokens activos.
- `POST /api/v1/auth/login-with-code` valida codigo `AccessLogin` de un solo uso y devuelve el mismo contrato de login normal.
- El provider por defecto `SimulatedAuthCodeDeliveryProvider` no envia email/SMS real y no loguea codigos; providers reales quedan para una integracion posterior.

## Admin Bootstrap Seed

- Admin Bootstrap queda deshabilitado por defecto y se configura con `AdminBootstrap__Enabled`, `AdminBootstrap__Email`, `AdminBootstrap__Password`, `AdminBootstrap__FullName` y `AdminBootstrap__RunOnlyWhenNoAdminsExist`.
- Al iniciar la API, el bootstrap valida email, password y nombre con la politica actual de registro, crea `Role = Admin` solo si corresponde y guarda la password con el hasher actual.
- El flujo es idempotente: con `RunOnlyWhenNoAdminsExist=true` no crea otro Admin si ya existe uno; si el email configurado ya existe, no cambia rol ni password.
- `POST /api/v1/auth/register` sigue rechazando Admin y no se agrega endpoint publico para crear administradores.
- Los logs del bootstrap son operativos y no incluyen password, hash, tokens, connection strings ni valores de variables de entorno.

## SOS Alert Orchestration API

- SOS Alert Orchestration API agrega `POST /api/v1/mobile/sos-alerts` para que la app movil cree incidente, alert dispatch y notification attempts en una sola llamada autenticada de Rider.
- El endpoint reutiliza `IIncidentService`, `IAlertDispatchService` e `INotificationService`; no reemplaza endpoints individuales ni cambia sus contratos.
- `clientIncidentId` y `clientAlertRequestId` deben ser UUID/GUID validos, manteniendo las reglas actuales de Incidents y AlertDispatch.
- La idempotencia se hereda de los modulos existentes: incidente por `userId + tripId + clientIncidentId`, dispatch por `userId + incidentId + clientAlertRequestId` y attempts por `userId + alertDispatchId + emergencyContactId + channel + attemptNumber`.
- El endpoint no ejecuta outbox ni envia notificaciones directamente; deja attempts `Prepared` con `provider = None` para que los procese el outbox worker o el endpoint admin de outbox.
- No agrega SMS real, email real, WhatsApp real, pagos, PDF, evidencia binaria, realtime, ML, distributed lock, CORS ni cambios de deploy.

## FCM Notification Provider

- FCM Notification Provider permite entrega push real para attempts `Push` solo cuando `Notifications:Providers:Fcm:Enabled = true`; por defecto permanece deshabilitado y `Push` usa el provider simulado.
- Las credenciales se configuran solo por variables de entorno y el orden de prioridad es `ServiceAccountJson`, `ServiceAccountJsonBase64` y `ServiceAccountFilePath`.
- Para DigitalOcean App Platform se recomienda `Notifications__Providers__Fcm__ServiceAccountJsonBase64` para evitar problemas al pegar JSON completo como variable de entorno.
- El endpoint Admin-only `GET /api/v1/admin/notifications/providers/status` devuelve solo el origen seguro (`environment_json`, `environment_json_base64`, `environment_file_path` o `none`) y nunca devuelve JSON, Base64, private keys, file paths sensibles ni credenciales.
- Base64 invalido falla de forma controlada con codigo seguro y sin loggear ni auditar el contenido decodificado.

## Notification Outbox Worker Production Readiness

- Notification Outbox Worker ahora bindea configuracion real desde `Notifications:OutboxWorker`, compatible con variables DigitalOcean `Notifications__OutboxWorker__Enabled`, `Notifications__OutboxWorker__IntervalSeconds`, `Notifications__OutboxWorker__MaxItemsPerRun`, `Notifications__OutboxWorker__SimulateFailures` y `Notifications__OutboxWorker__RunOnStartup`.
- `Enabled = false` sigue siendo el default por codigo; el worker solo procesa si se habilita por configuracion.
- `POST /api/v1/mobile/sos-alerts` sigue separado del outbox: crea incidente, alert dispatch y attempts `Prepared`, pero no ejecuta procesamiento ni llama providers.
- El worker procesa attempts `Prepared`; `Push` usa FCM si FCM esta habilitado/configurado, y `Sms`/`Email` siguen simulados.
- `GET /api/v1/admin/notifications/outbox/worker/status` devuelve configuracion efectiva no sensible y ultima corrida sin tokens, payloads, emails, telefonos, credenciales FCM, service accounts ni connection strings.
- No se implementa distributed lock: con una sola replica DigitalOcean se puede habilitar; con multiples replicas puede haber envio duplicado antes del update atomico final y se requiere distributed lock futuro.

## Offline Processing Worker Recovery

- Offline Processing Worker queda implementado como `BackgroundService` nativo, deshabilitado por defecto y configurado desde `Mobile:OfflineProcessingWorker`.
- Variables DigitalOcean: `Mobile__OfflineProcessingWorker__Enabled`, `Mobile__OfflineProcessingWorker__IntervalSeconds`, `Mobile__OfflineProcessingWorker__MaxItemsPerRun`, `Mobile__OfflineProcessingWorker__RunOnStartup` y `Mobile__OfflineProcessingWorker__RecoveryMinutes`.
- `POST /api/v1/mobile/offline-ingestion/batch` sigue respondiendo ACK durable despues de persistir records `PendingProcessing`; el worker procesa esos records despues sin depender del run manual.
- `POST /api/v1/offline-processing/run` se mantiene Rider-only y scoped al Rider autenticado para soporte/manual testing.
- `GET /api/v1/offline-processing/status` se mantiene Rider-only y scoped; no expone conteos globales ni configuracion del worker.
- `GET /api/v1/admin/offline-processing/worker/status` agrega status global seguro Admin-only con configuracion efectiva, conteos y ultima corrida sin payloads, tokens, credenciales, connection strings ni datos personales.
- Recovery reutiliza estados existentes: `Processing` antiguo vuelve a `PendingProcessing` usando `ProcessingStartedAtUtc <= now - RecoveryMinutes` y fallback `UpdatedAtUtc` cuando falta `ProcessingStartedAtUtc`.
- La recuperacion depende de idempotencia en Incidents, Alert Dispatch, Location Sharing y Minor Events. Con una sola replica DigitalOcean se puede habilitar; con multiples replicas puede haber doble procesamiento antes de cerrar estado y se requiere distributed lock futuro.

## Offline SOS Alert Correlation

- Offline Ingestion agrega el tipo `offline-sos-alert` para que Android sincronice una emergencia SOS creada totalmente offline en un solo item autosuficiente.
- Offline Processing reutiliza `CreateSosAlertService` para crear Incident, AlertDispatch y NotificationAttempts `Prepared`; no envia Push directamente.
- Si el payload no trae `tripId`, se usa `OfflineIngestionRecord.TripId`; si no trae `detectedAtUtc`, se usa `OfflineIngestionRecord.OccurredAtUtc`.
- El record offline se marca `Processed` con `remoteRecordId = incident.id` cuando la orquestacion termina correctamente.
- El flujo online `POST /api/v1/mobile/sos-alerts` no cambia. El flujo offline usa `POST /api/v1/mobile/offline-ingestion/batch` con `type = offline-sos-alert`.
- La idempotencia queda en tres niveles: offline ingestion por `clientEventId + payloadVersion`, Incident por `clientIncidentId`, AlertDispatch por `clientAlertRequestId` y attempts por reglas existentes de Notifications.

## Emergency Contact Invitation Accept

- Emergency Contacts API agrega `POST /api/v1/emergency-contacts/invitations/{code}/accept` para que un `Monitor` acepte una invitacion vigente.
- La aceptacion valida contacto activo, codigo existente, expiracion, `InvitationStatus = Invited` y coincidencia de email o telefono normalizado con el Monitor autenticado.
- El telefono se compara por todos sus digitos normalizados; no se comparan solo los ultimos digitos para evitar vinculos incorrectos.
- Al aceptar se mantiene `UserId` como Rider propietario y se setea `LinkedUserId`, `InvitationStatus = Linked`, `LinkedAtUtc` y `UpdatedAtUtc`.
- Aceptar una invitacion ya vinculada al mismo Monitor es idempotente; si esta vinculada a otro Monitor devuelve `invitation_already_linked`.
- Notifications prepare crea attempts `Push` para contactos `Linked` cuando el Monitor vinculado tiene token FCM activo Android o Web; SMS y Email siguen simulados.

## Push Notification Tokens API

- Push Notification Tokens API implementa registro, listado, estado y revocacion logica de tokens de notificacion en la coleccion `pushNotificationTokens`.
- Agrega endpoints `POST /api/v1/push-notification-tokens`, `GET /api/v1/push-notification-tokens`, `GET /api/v1/push-notification-tokens/status`, `POST /api/v1/push-notification-tokens/{id}/revoke`, `GET /api/v1/admin/push-notification-tokens` y `POST /api/v1/admin/push-notification-tokens/{id}/revoke`.
- `Rider`, `Monitor` y `Admin` pueden registrar tokens propios; Admin puede listar y revocar cualquier token desde rutas admin.
- `userId` sale siempre del JWT; si el body envia `userId`, `tokenHash`, `status`, `tokenValue` o credenciales de proveedor, se devuelve `validation_error`.
- La idempotencia usa `userId + tokenHash + platform + channel + deviceId`; registrar el mismo token no duplica y actualiza `LastSeenAtUtc`.
- Registrar un token nuevo para el mismo scope revoca tokens activos anteriores sin borrado fisico.
- `TokenValue` se guarda internamente para providers futuros, pero nunca se devuelve, audita ni registra; queda pendiente encryption/protection at rest.
- `TokenHash` se usa solo internamente y nunca se devuelve ni audita.
- Si se informa `deviceId`, se valida ownership, estado activo, `LinkStatus = Linked` y `RevokedAtUtc = null`; este modulo no crea ni modifica devices.
- Soporta combinaciones `Android + Fcm`, `Ios + Apns`, `Web + WebPush` y `Web + Fcm`.
- Audita best-effort `PushNotificationTokenRegistered` y `PushNotificationTokenRevoked` con metadata minima permitida.
- No envia notificaciones reales, no llama proveedores externos, no agrega SDKs externos, no agrega secretos, no implementa cobros ni pairing API de smartwatch.

## Audit Log Retention Policy

- Audit Log Retention Policy implementa retencion manual Admin-only sobre la coleccion `auditLogs`.
- Agrega endpoints `GET /api/v1/admin/audit-logs/retention/policy`, `POST /api/v1/admin/audit-logs/retention/run`, `GET /api/v1/admin/audit-logs/retention/runs` y `GET /api/v1/admin/audit-logs/retention/runs/{id}`.
- La politica queda definida por codigo con default `180` dias, minimo `90`, maximo `3650`, dry-run default `true`, confirmacion requerida para borrado real y worker automatico deshabilitado.
- El cutoff se calcula como `now - retentionDays`; el borrado real elimina solo documentos de `auditLogs` con `CreatedAtUtc < cutoffUtc`.
- Las ejecuciones se guardan como metadata minima en `auditLogRetentionRuns`; esa coleccion no se borra por la politica.
- Si `dryRun = false` sin `confirmPermanentDelete = true`, se devuelve `validation_error` y no se borra nada.
- La auditoria de retencion es best-effort con acciones `AuditLogRetentionDryRunCompleted`, `AuditLogRetentionDeleteCompleted` y `AuditLogRetentionFailed`.
- No se implementa borrado programado, almacenamiento externo, export automatico, compresion, objetos externos, URLs firmadas, correo, proveedores reales, SDKs externos ni cobros.

## Decisiones actuales

- MotoSOS.API es un proyecto Web API en .NET 9.
- MongoDB es la base de datos central de la plataforma.
- SQLite se usara solo en la app movil como almacenamiento local.
- La sincronizacion desde SQLite hacia datos centrales debe realizarse mediante endpoints de la API.
- GitHub Actions incluye Build & Test.
- GitHub Actions incluye Semgrep SAST.
- GitHub Actions incluye CodeQL como analisis complementario para C#.
- GitHub Actions incluye escaneo de contenedor con Trivy y generacion de SBOM.
- Semgrep SAST usa reglas administradas y reglas custom locales en `.semgrep/semgrep.yaml`.
- Dependabot revisa paquetes NuGet, GitHub Actions y Docker.
- NuGet usa `packages.lock.json` versionados y restore bloqueado en CI.
- El flujo de ramas es `feature/*` -> `develop` -> `main`.
- Las ramas `main` y `develop` estan protegidas con rulesets.
- Los cambios sensibles tienen CODEOWNERS asignado al owner del repositorio `@motosos215-lab`.
- DevSecOps se aplica desde el inicio del proyecto.
- MotoSOS.API es el unico punto de acceso a datos centrales para Web, apps moviles, smartwatch, notificaciones, analitica y Machine Learning.
- La API incluye baseline de security headers, rate limiting y manejo global de errores.
- La API usa HSTS solo en Production.
- La base de autenticacion usa JWT Bearer, roles `Admin`, `Rider` y `Monitor`, BCrypt para passwords y refresh tokens hasheados.
- Las opciones JWT se validan al arranque y requieren una key de prueba o produccion con longitud minima.
- MongoDB Atlas se configura por variables de entorno; cuando esta configurado, se aseguran indices idempotentes al iniciar, incluyendo usuarios por email y refresh tokens por hash, usuario y expiracion.
- `/health/ready` valida MongoDB Atlas en entornos reales y tolera MongoDB no configurado en Development/Testing.
- La pantalla de registro requiere `accountType`, `confirmPassword` y `acceptTerms`.
- El maquetado usa `Conductor`, pero el backend lo mapea a `Rider`.
- `forgot-password` y `access-code` quedan preparados sin proveedor externo real y sin enumerar usuarios.
- Login soporta `rememberMe`, que solo extiende la expiracion del refresh token.
- El onboarding inicial de conductor sigue un flujo web-first: registro, login y configuracion inicial ocurren principalmente en portal web.
- La app movil se vinculara despues mediante codigo o QR y no sustituye el alta inicial del conductor.
- El smartwatch se vinculara desde la app movil, no desde web.
- Decision final Wear OS: el smartwatch se vincula unicamente local entre Android y Wear OS mediante Wear OS Data Layer. La API no administra pairing, QR, codigos, nodeId, Bluetooth ni estado del reloj. El telefono actua como gateway y envia a la API batches resumidos, eventos, incidentes, alertas y ubicacion usando la sesion del Rider.
- El wizard actual de conductor tiene 7 pasos: cuenta, perfil, motocicleta/motoneta, contactos de emergencia, vinculacion de dispositivos, plan/licencia y confirmacion.
- En esta etapa solo `Rider` puede usar onboarding de conductor y perfil; `Conductor` del maquetado se guarda como `Rider`.
- `Monitor` y `Admin` recibiran `403 forbidden` en el flujo de onboarding/perfil de conductor hasta que existan flujos especificos.
- Los perfiles de conductor se guardan en MongoDB en la coleccion `driverProfiles`, con indice unico por `UserId`.
- `profiles/me` puede actualizar `fullName` y `phoneNumber` de `User` de forma controlada, pero no permite cambiar `email`, `role`, `isActive`, permisos ni claims.
- Vehicles API implementa el paso 3 del wizard web-first: Motocicleta / Motoneta.
- Los vehiculos del conductor se guardan en MongoDB en la coleccion `driverVehicles`.
- `driverVehicles` tiene indices por `UserId`, `UserId + IsActive` y `CompletionStatus`; los indices unicos parciales por placa/VIN quedan como pendiente futuro.
- El plan Basico se asume por default hasta que exista modulo Plans y permite solo 1 vehiculo activo por usuario.
- Vehicles API solo permite `Rider`; `Monitor` y `Admin` reciben `403 forbidden`.
- Vehicles API no permite consultar, actualizar o eliminar vehiculos de otro usuario y DELETE aplica baja logica con `IsActive = false`.
- Onboarding avanza a `3/7`, `43%` y `EmergencyContacts` solo cuando Profile esta `Completed` y existe un vehiculo activo `Completed`.
- EmergencyContacts API implementa el paso 4 del wizard web-first: Contactos de emergencia.
- Los contactos se guardan en MongoDB en la coleccion `emergencyContacts` con indices por `UserId`, `UserId + IsActive`, `InvitationStatus` y `LinkingCode`.
- El plan Basico permite solo 1 contacto activo por usuario hasta que exista modulo Plans real.
- `/invite` genera codigo de vinculacion legible con expiracion de 24 horas y no envia SMS/correo real.
- La app monitor puede aceptar invitaciones y setear `LinkedUserId`; `/invite` sigue sin enviar SMS/correo real.
- Onboarding avanza a `4/7`, `57%` y `Devices` solo cuando Profile y Vehicle estan `Completed` y existe contacto activo `Invited` o `Linked`.
- Devices API implementa el paso 5 del wizard web-first: Vinculacion de dispositivos.
- Los codigos de activacion movil se guardan en MongoDB en la coleccion `deviceActivationCodes` y expiran en 15 minutos.
- Los dispositivos vinculados se guardan en MongoDB en la coleccion `userDevices`.
- El portal web genera o consulta el codigo vigente; la app movil autenticada usa el codigo para vincularse.
- El smartwatch no se vincula desde web ni desde la API; la vinculacion Wear OS queda local en la app movil mediante Wear OS Data Layer.
- El plan Basico permite 1 `MobileApp` activa/vinculada por usuario hasta que exista modulo Plans real.
- `deviceIdentifier` se guarda hasheado y no se devuelve en responses.
- El modelo historico de smartwatches dependientes por `ParentDeviceId` queda como contexto legado; nuevas implementaciones no deben crear pairing API de smartwatch.
- Onboarding avanza a `5/7`, `71%` y `Plan` cuando existe una `MobileApp` activa con `LinkStatus = Linked`; smartwatch queda opcional en esta etapa.
- Pendientes futuros de Devices: planes reales, push notifications, sincronizacion offline real, viajes, SOS, incidentes, compatibilidad legado de smartwatch documentada y dashboard operativo.
- Plans API implementa el paso 6 del wizard web-first: Plan y licencia.
- El catalogo de planes se maneja en memoria con Basic, Plus y FamilyPro; solo Basic es seleccionable desde web en esta etapa.
- Las suscripciones del usuario se guardan en MongoDB en la coleccion `userSubscriptions`.
- `subscriptions/me` devuelve `subscription = null` y `defaultPlan = Basic` cuando el usuario aun no confirma plan.
- `subscriptions/select-basic` crea o actualiza de forma idempotente una suscripcion `Basic` con `Status = Active` y `Source = WebBasic`.
- Onboarding avanza a `6/7`, `86%` y `Confirmation` cuando existe suscripcion activa; Confirmation sigue pendiente e `isOperational` sigue `false`.
- Vehicles y EmergencyContacts mantienen por ahora sus limites Basic hardcoded de 1 vehiculo y 1 contacto activo; Plans sera la fuente central de limites en una etapa futura.
- Pendientes futuros de Plans: Google Play Billing real, pagos reales, renovaciones, facturacion, cupones, upgrades y licenciamiento empresarial.
- Confirmation API implementa el paso 7 del wizard web-first dentro del modulo Onboarding.
- Las confirmaciones se guardan en MongoDB en la coleccion `onboardingConfirmations` con metadata minima: `UserId`, `ConfirmedAtUtc`, `IsOperational`, `CreatedAtUtc` y `UpdatedAtUtc`.
- `onboarding/summary` devuelve el resumen seguro del wizard y `canConfirm` solo cuando Account, Profile, Vehicle, EmergencyContacts, Devices y Plan estan completos.
- `onboarding/confirm` es idempotente, no duplica confirmaciones y conserva `ConfirmedAtUtc` si ya existia confirmacion.
- Onboarding avanza a `7/7`, `100%`, `currentStep = Completed` e `isOperational = true` solo si existe confirmacion y los pasos previos siguen completos.
- Si un paso previo queda incompleto despues de confirmar, `isOperational` vuelve a `false` aunque exista confirmacion previa.
- Pendientes futuros tras cerrar wizard: Trips, SOS, Incidents, Notifications, Live Monitoring, Dashboard operativo y Machine Learning.
- Trips API implementa el primer modulo operativo despues del onboarding web-first completo.
- Los viajes se guardan en MongoDB en la coleccion `trips` con indices por `UserId`, `UserId + Status`, `VehicleId`, `MobileDeviceId`, `StartedAtUtc` y `FinishedAtUtc`.
- Trips API agrega `GET /api/v1/trips/active`, `POST /api/v1/trips/start`, `POST /api/v1/trips/{id}/finish`, `GET /api/v1/trips/{id}` y `GET /api/v1/trips`.
- Emergency Resolution Report API crea reportes finales en la coleccion `emergencyResolutionReports` solo para incidentes `Closed` o `FalsePositiveCancelled`.
- Emergency Resolution Report API usa idempotencia `userId + incidentId`; crear dos veces devuelve el reporte existente y no duplica documentos ni devuelve `409`.
- Emergency Resolution Report API no cierra incidentes ni duplica la logica operativa de cierre; el cierre sigue en Incidents API con `/api/v1/incidents/{id}/close` y `/api/v1/incidents/{id}/cancel-false-positive`.
- Emergency Resolution Report API agrega endpoints Rider para crear, consultar y listar reportes, y endpoint Monitor para consultar reportes de alertas asignadas.
- `ILocationSharingRepository.GetLatestByIncidentIdAsync` queda aprobado para reportes finales y consulta el ultimo snapshot por `IncidentId`, ordenado por `RecordedAtUtc` desc y `ReceivedAtUtc` desc, sin historial, polyline ni monitoreo realtime.
- Emergency Resolution Report API persiste metricas finales: intentos de notificacion, acknowledgements, acknowledged/declined, primera notificacion, primer acknowledgement, tiempo de respuesta, cierre del incidente, ultima ubicacion conocida y stale flag.
- Operational Dashboard API implementa endpoints administrativos bajo `/api/v1/admin/dashboard` para metricas globales de usuarios, onboarding, viajes, incidentes, alertas, notificaciones, acknowledgements, reportes de resolucion y procesamiento offline.
- Operational Dashboard API es Admin-only, solo lectura/agregacion, no acepta `userId` externo, no crea colecciones nuevas y consulta colecciones existentes mediante un repositorio agregado read-only.
- Operational Dashboard API no implementa frontend, graficas, ML, realtime, monitoreo en vivo, proveedores reales, pagos ni pairing API de smartwatch.
- Operational Dashboard API deja como pendiente futuro optimizar response times y outcomes con aggregation pipeline si el volumen crece.
- Notification Outbox API implementa endpoints Admin-only bajo `/api/v1/admin/notifications/outbox` para procesar attempts existentes de forma simulada y controlada.
- Notification Outbox API mueve attempts `Prepared` a `SimulatedSent` o a `Failed` con reason `simulated_failure_requested`, usando updates atomicos por `Id` y estado esperado.
- Notification Outbox API permite `retry-failed` para regresar attempts `Failed` a `Prepared`; no procesa attempts `Cancelled` ni `SimulatedSent`.
- Notification Outbox API no crea colecciones, no modifica incidentes, no modifica alert dispatches, no crea acknowledgements, no crea reportes de resolucion, no envia mensajes reales ni agrega proveedores externos.
- Notification Outbox Worker queda implementado como `BackgroundService` nativo, registrado pero deshabilitado por defecto con `Enabled = false`, `IntervalSeconds = 60`, `MaxItemsPerRun = 20`, `SimulateFailures = false` y `RunOnStartup = false`.
- Notification Outbox Worker reutiliza `NotificationOutboxService` y `SimulatedNotificationProvider`; cuando se habilita procesa attempts `Prepared` automaticamente y conserva `Prepared -> SimulatedSent`, `Prepared -> Failed` y `retry-failed` manual como flujos separados.
- Notification Outbox Worker evita ejecuciones simultaneas en la misma instancia con control en memoria, captura errores como estado seguro y continua en el siguiente intervalo; distributed lock queda pendiente futuro para multiples replicas.
- Notification Outbox agrega `GET /api/v1/admin/notifications/outbox/worker/status` como endpoint Admin-only, solo lectura, sin activar ni disparar procesamiento.
- Trips API requiere onboarding completo: `completedSteps = 7`, `currentStep = Completed` e `isOperational = true`.
- Para iniciar viaje se requiere vehiculo propio activo `Completed` y `MobileApp` propio activo `Linked`; smartwatch es opcional pero debe depender del `MobileApp` si se informa.
- Trips API permite solo un viaje `Active` por usuario; repetir start con el mismo vehiculo y mobile devuelve el viaje activo existente, y datos distintos devuelven `active_trip_exists`.
- Sin indice unico parcial o control atomico fuerte, dos requests simultaneos extremos podrian crear dos viajes activos; queda como mejora futura una garantia fuerte con operacion atomica o indice parcial.
- Pendientes futuros de Trips: Offline ingestion, Minor events, Sensor batches, Incidents, SOS, Alerts, Notifications, Live Monitoring, Dashboard y ML.
- Offline Ingestion API implementa `POST /api/v1/mobile/offline-ingestion/batch` para recibir cola offline movil y devolver ACK durable despues de persistir.
- Los registros offline se guardan en MongoDB en la coleccion `offlineIngestionRecords`.
- La idempotency key oficial es `userId + mobileDeviceId + tripId + item.type + item.clientEventId + item.payloadVersion` y tiene indice unico.
- Duplicados de Offline Ingestion no devuelven `409`; responden `Duplicate` como exito estable con el mismo `AckId` y `remoteRecordId`.
- Offline Ingestion acepta `minor-event`, `local-incident`, `alert-dispatch-request` y `location-update`, y guarda nuevos registros con `ProcessingStatus = PendingProcessing`.
- Offline Ingestion no procesa incidentes reales, SOS, alertas, notificaciones, live monitoring, dashboard ni ML todavia.
- Pendientes futuros de Offline Ingestion: processor real, Incidents API, Alert Dispatch API, Notifications, Live Monitoring, Dashboard, ML y sensor batches completos.
- Offline Processing API implementa procesamiento controlado de registros `offlineIngestionRecords` pendientes, sin worker real ni coleccion nueva.
- Offline Processing agrega `POST /api/v1/offline-processing/run` y `GET /api/v1/offline-processing/status`, ambos Rider-only.
- Offline Processing procesa `local-incident`, `alert-dispatch-request` y `location-update`; `minor-event` queda `Ignored` y se muestra como `Skipped` con reason `minor_event_processing_not_implemented`.
- Para `local-incident`, si falta `payload.clientIncidentId`, se usa `OfflineIngestionRecord.ClientEventId` como fallback estable; no se genera GUID en backend.
- Offline Processing usa claim atomico `Id + UserId + PendingProcessing` antes de procesar y mantiene idempotencia de Incidents, Alert Dispatch y Location Sharing.
- Offline Processing no implementa Hangfire, Quartz, cron externo, WebSockets, SignalR, proveedores reales, notificaciones reales, escalamiento ni ML.
- Incidents API implementa el registro remoto de incidentes asociados a viajes, sin alertas ni notificaciones reales todavia.
- Los incidentes se guardan en MongoDB en la coleccion `incidents` con indice unico por `IdempotencyKey`.
- La idempotency key oficial de Incidents es `userId + tripId + clientIncidentId`; duplicados no devuelven `409` y responden el incidente existente como exito estable.
- Incidents API requiere JWT Bearer, solo permite `Rider`, toma `userId` exclusivamente del token y no acepta `userId` en el body.
- Incidents API requiere onboarding completo: `completedSteps = 7`, `currentStep = Completed` e `isOperational = true`.
- `tripId` debe existir y pertenecer al Rider autenticado; puede estar `Active` o `Finished` para sincronizacion tardia.
- `VehicleId`, `MobileDeviceId` y `SmartwatchDeviceId` del incidente se derivan desde el viaje y no desde el request.
- Nuevos incidentes se crean con `Status = Open`; cancelar falso positivo aplica `Open -> FalsePositiveCancelled` y cerrar aplica `Open` o `FalsePositiveCancelled -> Closed`.
- No se borran incidentes fisicamente y cancelar falso positivo sobre `Closed` devuelve `incident_already_closed`.
- Pendientes futuros de Incidents: Alert Dispatch API real, notificaciones, escalamiento, live monitoring, dashboard operativo, ML, processor real de Offline Ingestion y sensor batches completos.
- Alert Dispatch API implementa la preparacion y persistencia de solicitudes de alerta asociadas a incidentes existentes, sin envio real de notificaciones todavia.
- Los alert dispatches se guardan en MongoDB en la coleccion `alertDispatchRequests` con indice unico por `IdempotencyKey`.
- Alert Dispatch API agrega `POST /api/v1/alert-dispatches`, `GET /api/v1/alert-dispatches`, `GET /api/v1/alert-dispatches/{id}` y `POST /api/v1/alert-dispatches/{id}/cancel`.
- La idempotency key oficial de Alert Dispatch es `userId + incidentId + clientAlertRequestId`; duplicados no devuelven `409` y responden la solicitud existente como exito estable.
- Alert Dispatch API requiere JWT Bearer, solo permite `Rider`, toma `userId` exclusivamente del token y no acepta `userId` en el body.
- Alert Dispatch API requiere onboarding completo y solo permite crear solicitudes para incidentes propios en `Status = Open`.
- Incidentes `Closed` devuelven `incident_not_ready`; incidentes `FalsePositiveCancelled` devuelven `alert_not_allowed`.
- `TripId`, `VehicleId`, `MobileDeviceId` y `SmartwatchDeviceId` se derivan desde el incidente y no desde el request.
- Al crear una solicitud se guarda snapshot de contactos de emergencia elegibles: activos con `InvitationStatus = Invited` o `Linked`.
- Si no existe al menos un contacto elegible, Alert Dispatch devuelve `alert_not_allowed`.
- Nuevas solicitudes se crean con `Status = PendingDispatch`; cancelar aplica `PendingDispatch -> Cancelled`, `Cancelled` es idempotente y `Completed` devuelve `alert_dispatch_already_completed`.
- Pendientes futuros de Alert Dispatch: Notifications API, push, SMS, mensajeria instantanea, correo, escalamiento real, acknowledgement de contacto/monitor, live monitoring, dashboard operativo y ML.
- Notifications API implementa la preparacion y persistencia de intentos de notificacion asociados a `AlertDispatchRequest`, sin envio real todavia.
- Los intentos se guardan en MongoDB en la coleccion `notificationDeliveryAttempts` con indice unico por `IdempotencyKey`.
- Notifications API agrega `POST /api/v1/notifications/delivery-attempts/prepare`, `GET /api/v1/notifications/delivery-attempts`, `GET /api/v1/notifications/delivery-attempts/{id}`, `POST /mark-simulated-sent`, `POST /mark-failed` y `POST /cancel`.
- La idempotency key oficial de Notifications es `userId + alertDispatchId + emergencyContactId + channel + attemptNumber`, con `attemptNumber = 1` en esta etapa.
- Notifications API usa exclusivamente `ContactsSnapshot` de Alert Dispatch y no consulta contactos vivos para generar intentos.
- Se crea un intento por contacto: `Sms` si hay telefono y `Email` como fallback si solo hay correo; contactos sin canal se omiten.
- Los intentos nuevos quedan `Prepared` y `Provider = None`; `SimulatedSent` existe solo para pruebas internas.
- Notification Provider Abstraction agrega `INotificationProvider`, `INotificationProviderResolver`, request/result internos, enums de provider/channel/status, `SimulatedNotificationProvider` y provider FCM para Push habilitable por configuracion.
- `NotificationProviderResolver` devuelve proveedor simulado para `Sms` y `Email`; para `Push` devuelve FCM si esta habilitado/configurado y, si no, simulado. Canales no soportados se manejan como fallo controlado.
- Notification Outbox usa la abstraccion interna y mantiene sus endpoints, rutas y contratos publicos existentes sin cambios.
- `simulateFailures = false` conserva `Prepared -> SimulatedSent`; `simulateFailures = true` conserva `Prepared -> Failed`; `retry-failed` conserva `Failed -> Prepared`.
- El provider simulado genera `ProviderMessageId` seguro `simulated-{guid}` en exito y errores controlados en falla; no realiza I/O externo ni requiere configuracion sensible.
- Notification Provider Abstraction audita best-effort `NotificationProviderSimulatedSent` y `NotificationProviderSimulatedFailed` con metadata segura limitada.
- Notification Outbox Worker audita best-effort `NotificationOutboxWorkerRun`, `NotificationOutboxWorkerFailed` y `NotificationOutboxWorkerSkipped` con metadata segura limitada.
- No se agregan SMS real, correo real, mensajeria real, secretos ni escalamiento real.
- Pendientes futuros de Notifications: SMS real, mensajeria instantanea, correo real, escalamiento, acknowledgement, live monitoring, dashboard operativo y ML.
- Alert Acknowledgements API implementa la respuesta del contacto/monitor ante alertas preparadas, sin live monitoring ni notificaciones reales todavia.
- Los acknowledgements se guardan en MongoDB en la coleccion `alertAcknowledgements` con indice unico por `IdempotencyKey`.
- Alert Acknowledgements API usa `EmergencyContact.LinkedUserId == monitorUserId` como relacion segura para asignar alertas al Monitor.
- El documento unico de acknowledgement usa `monitorUserId + notificationDeliveryAttemptId`; las acciones repetidas son idempotentes por transicion de estado.
- Monitor puede listar, ver, marcar vista, confirmar o declinar solo intentos asociados a sus contactos vinculados; intentos ajenos devuelven `not_found`.
- Rider puede consultar acknowledgements asociados a sus propias alertas pero no responder como Monitor.
- Pendientes futuros de Alert Acknowledgements: live monitoring, mapa en tiempo real, streaming de ubicacion, chat, llamadas, proveedores reales, escalamiento, dashboard operativo y ML.
- Emergency Escalation API implementa escalamiento interno simulado en la coleccion `emergencyEscalations`, sin llamadas reales a servicios de emergencia ni proveedores externos.
- Emergency Escalation API agrega endpoints Rider `POST /api/v1/rider/alert-dispatches/{alertDispatchId}/escalate`, `GET /api/v1/rider/alert-dispatches/{alertDispatchId}/escalation-status`, `POST /api/v1/rider/alert-dispatches/{alertDispatchId}/mark-unresolved` y `POST /api/v1/rider/alert-dispatches/{alertDispatchId}/cancel-escalation`.
- Emergency Escalation API agrega `GET /api/v1/monitor/alerts/{notificationDeliveryAttemptId}/escalation-status` para Monitor asignado y `GET /api/v1/admin/escalations` para consulta Admin-only.
- La idempotency key oficial de Emergency Escalation es `userId + alertDispatchId`; hay indices unicos por `IdempotencyKey` y `AlertDispatchId`, y crear dos veces devuelve el mismo escalamiento sin `409`.
- Emergency Escalation API no modifica `Incident`, `AlertDispatchRequest`, `NotificationDeliveryAttempt`, `AlertAcknowledgement` ni `EmergencyResolutionReport`; solo persiste el documento de escalamiento y auditoria best-effort.
- `NoAcknowledgement` requiere incidente `Open`, alert dispatch propio, al menos un attempt existente y al menos un attempt `SimulatedSent`; sin attempts solo se permite `ManualEscalation`.
- `AllContactsDeclined` requiere acknowledgements existentes, todos `Declined` y ninguno `Acknowledged`; cualquier `Acknowledged` bloquea el escalamiento con `emergency_escalation_not_allowed`.
- `ManualEscalation` se permite para incidente propio `Open` si no existe acknowledgement `Acknowledged`; incidentes `Closed` o `FalsePositiveCancelled` devuelven `incident_not_ready`.
- Emergency Escalation API audita `EmergencyEscalationRequested`, `EmergencyEscalationMarkedUnresolved` y `EmergencyEscalationCancelled` con metadata segura limitada.
- Automatic Escalation Worker queda implementado como `BackgroundService` nativo, registrado pero deshabilitado por defecto con `Enabled = false`, `IntervalSeconds = 60`, `MaxItemsPerRun = 20`, `EscalateAfterSeconds = 300` y `RunOnStartup = false`.
- Automatic Escalation Worker crea solo `EmergencyEscalation` interna `NoAcknowledgement` / `Level1` cuando el incidente esta `Open`, el alert dispatch esta `PendingDispatch`, no existe escalation previa, no hay acknowledgement `Acknowledged`, existe attempt `SimulatedSent` y el `SimulatedSentAtUtc` mas antiguo ya supero el umbral.
- Automatic Escalation Worker no escala solo por antiguedad de `AlertDispatchRequest`, no modifica `Incident`, no modifica `AlertDispatchRequest`, no crea attempts, acknowledgements ni reportes de resolucion, y no llama servicios externos.
- Automatic Escalation Worker agrega endpoints Admin-only `GET /api/v1/admin/escalations/worker/status` y `POST /api/v1/admin/escalations/worker/run`; el run manual no activa el worker permanente ni cambia `Enabled`.
- Automatic Escalation Worker evita ejecuciones simultaneas en la misma instancia con control en memoria; distributed lock queda pendiente futuro para multiples replicas.
- Automatic Escalation Worker audita best-effort `AutomaticEscalationWorkerRun`, `AutomaticEscalationWorkerFailed`, `AutomaticEscalationWorkerSkipped` y `EmergencyEscalationAutomaticallyRequested` con metadata segura limitada.
- Pendientes futuros de Emergency Escalation: integraciones reales con servicios de emergencia si hay aprobacion legal/operativa, proveedores reales, automatizacion controlada, dashboard avanzado y correlacion operacional completa.
- Minor Events API implementa registro de eventos menores de viaje en la coleccion `minorEvents`, sin convertirlos en emergencias confirmadas.
- Minor Events API agrega `POST /api/v1/mobile/minor-events`, `GET /api/v1/rider/minor-events`, `GET /api/v1/rider/minor-events/{id}`, `POST /api/v1/rider/minor-events/{id}/mark-reviewed`, `POST /api/v1/rider/minor-events/{id}/ignore` y `GET /api/v1/admin/minor-events`.
- La idempotency key oficial de Minor Events es `userId + tripId + clientEventId + eventType`; duplicados devuelven el mismo `MinorEvent` sin `409` y sin duplicar documentos.
- Minor Events API permite crear eventos solo para viajes propios `Active` o `Finished`; `userId` siempre viene del JWT y no se acepta en el body.
- `mobileDeviceId` y `smartwatchDeviceId`, si se informan, deben pertenecer al Rider y estar vinculados; si no se informan, se permite registrar desde app movil u offline ingestion.
- Minor Events API sanitiza metadata case-insensitive, elimina claves sensibles y trunca valores a 200 caracteres; no guarda payload completo.
- Offline Processing procesa item type `minor-event` creando `MinorEvent`, marca el offline record como `Processed` y usa `MinorEvent.Id` como `remoteRecordId`; payload invalido queda como fallo permanente controlado sin romper todo el batch.
- Minor Events API no crea `Incident`, `AlertDispatchRequest`, `NotificationDeliveryAttempt`, `AlertAcknowledgement`, `EmergencyEscalation` ni `EmergencyResolutionReport`, y no modifica `Trip`.
- Minor Events API audita best-effort `MinorEventRecorded`, `MinorEventMarkedReviewed`, `MinorEventIgnored` y `MinorEventProcessedFromOfflineIngestion`.
- Telemetry Summary API implementa resumenes agregados de senales por viaje en la coleccion `tripTelemetrySummaries`, con documento unico por `UserId + TripId`.
- Telemetry Summary agrega endpoints Rider `GET /api/v1/rider/trips/{tripId}/telemetry-summary`, `POST /api/v1/rider/trips/{tripId}/telemetry-summary/recompute` y endpoints Admin `GET /api/v1/admin/telemetry-summaries`, `GET /api/v1/admin/telemetry-summaries/{id}`.
- Telemetry Summary calcula desde `MinorEvents`, conserva recompute idempotente, no guarda coordenadas, ruta completa, polyline, metadata, mensajes ni lista completa de eventos.
- Telemetry Summary no modifica `Trip` ni `MinorEvents`, no crea incidentes, alert dispatches, notification attempts, acknowledgements, escalations ni reportes de resolucion.
- Telemetry Summary audita best-effort `TelemetrySummaryComputed` y `TelemetrySummaryRecomputed` con metadata segura limitada.
- Telemetry Summary no implementa monitoreo en vivo, mapa en tiempo real, modelos predictivos reales, score de riesgo real, cobros ni pairing API de smartwatch.
- Evidence Attachments API implementa registro metadata only de evidencia en la coleccion `evidenceAttachments`.
- Evidence Attachments agrega endpoints Rider, Monitor y Admin para registrar o consultar metadata segura segun rol.
- Evidence Attachments exige exactamente un target principal entre incident, alert dispatch o resolution report, con `TargetType` explicito.
- Evidence Attachments guarda `UserId` como Rider duenio, `RegisteredByUserId` como actor autenticado y `RegisteredByRole` como Rider o Monitor.
- Evidence Attachments rechaza campos binarios como base64, fileContent, imageBytes, videoBytes y audioBytes; no guarda archivos reales ni usa storage externo.
- Evidence Attachments implementa idempotencia por owner Rider, `clientEvidenceId`, `targetType` y `targetId`.
- Evidence Attachments implementa soft delete solo para Rider con `MarkedDeleted` y `DeletedAtUtc`.
- Evidence Attachments no modifica Incident, AlertDispatch ni EmergencyResolutionReport, no crea entidades externas, no envia notificaciones y no llama proveedores externos.
- Evidence Attachments audita best-effort `EvidenceAttachmentRegistered` y `EvidenceAttachmentDeleted` con metadata segura limitada.
- Resolution Report Export API implementa export JSON estructurado de reportes finales y metadata minima en `resolutionReportExports`.
- Resolution Report Export soporta solo `Json`; no genera PDF real, no descarga binarios, no guarda bytes/base64 y no usa storage externo.
- Resolution Report Export agrega endpoints Rider, Admin y Monitor para exportar segun permisos, y endpoint Admin para listar metadata de exports.
- Resolution Report Export usa idempotencia por `UserId + EmergencyResolutionReportId + ExportType`; exportar dos veces no duplica metadata y regenera la vista con datos actuales.
- Resolution Report Export consolida Incident, Trip, Alert Dispatch, Notifications, Acknowledgements, ultima ubicacion, Resolution Report, Escalation, Telemetry Summary, Evidence Attachments y AuditSummary basico.
- Resolution Report Export no devuelve historial de ubicaciones, polyline, tracking, lista completa de MinorEvents, metadata completa de evidencias ni metadata completa de auditoria.
- Resolution Report Export no modifica ni crea entidades externas; solo upsertea metadata minima del export.
- Resolution Report Export audita best-effort `ResolutionReportExportGenerated` con metadata segura limitada.
- Pendientes futuros de Minor Events: reglas automaticas, dashboard especifico, analitica, modelos predictivos aprobados, correlacion con incidentes, mapas historicos agregados y telemetria resumida avanzada.
- Emergency Location Sharing API implementa ultima ubicacion conocida por incidente abierto, sin historial de ruta ni live tracking.
- Los snapshots se guardan en MongoDB en la coleccion `emergencyLocationSnapshots` con indice unico compuesto `UserId + IncidentId`.
- Location Sharing agrega `POST /api/v1/mobile/location-sharing/snapshot`, `GET /api/v1/monitor/alerts/{notificationDeliveryAttemptId}/location` y `GET /api/v1/rider/incidents/{incidentId}/location`.
- Publicar ubicacion hace upsert por `UserId + IncidentId`; ubicaciones antiguas o duplicadas por `ClientLocationUpdateId` no reemplazan la ultima ubicacion.
- Monitor consulta ubicacion solo cuando el intento de notificacion pertenece a un `EmergencyContact` vinculado por `LinkedUserId`.
- Rider consulta solo ubicacion de incidentes propios; `isStale` indica si la ultima ubicacion supera 5 minutos respecto al reloj del servidor.
- Pendientes futuros de Location Sharing: live monitoring completo, sockets en tiempo real, mapa en vivo, historial controlado, frecuencia configurable, escalamiento, dashboard operativo y ML.
- Emergency Status API implementa resumen de emergencia por lectura/agregacion de Incidents, Trips, Alert Dispatch, Notifications, Alert Acknowledgements y Location Sharing, sin coleccion nueva.
- Emergency Status agrega `GET /api/v1/rider/emergencies/{incidentId}/status`, `GET /api/v1/monitor/alerts/{notificationDeliveryAttemptId}/status` y `GET /api/v1/rider/emergencies/active`.
- Rider solo consulta emergencias propias; Monitor solo consulta alertas asignadas mediante `EmergencyContact.LinkedUserId == monitorUserId`.
- Los conteos de notifications y acknowledgements se acotan por `incidentId` y, cuando existe, por `alertDispatchId`; no se calculan globalmente por usuario.
- Emergency Status no implementa live tracking, WebSockets, SignalR, streaming, mapa en tiempo real ni proveedores reales de notificacion.
- Audit Logs API implementa auditoria interna en la coleccion `auditLogs`, con endpoints Admin-only `GET /api/v1/admin/audit-logs` y `GET /api/v1/admin/audit-logs/{id}`.
- Audit Logs API registra acciones criticas de Auth, Incidents, Alert Dispatch, Notification Outbox, Alert Acknowledgements, Emergency Resolution y Offline Processing.
- La escritura de audit logs es best-effort: si falla guardar auditoria, la operacion principal no se rompe y solo se registra un mensaje controlado sin exponer stack traces ni errores internos.
- La lectura de audit logs no es best-effort: si falla consultar, se devuelve error controlado siguiendo el manejo global actual.
- Audit Logs API sanitiza metadata de forma case-insensitive, elimina claves sensibles, trunca valores a 200 caracteres y no guarda passwords, tokens, device identifiers, provider tokens, payloads completos, correos/telefonos completos, datos de pago, stack traces, errores internos, connection strings ni secretos.
- Los endpoints de consulta de audit logs no se auditan para evitar ruido, crecimiento innecesario o auditoria recursiva.
- Audit Logs API no implementa SIEM externo, Splunk, Datadog, CloudWatch, exportacion automatica, worker real, WebSockets, SignalR, tracking en vivo, proveedores reales, pagos ni pairing API de smartwatch.
- Pendientes futuros de Audit Logs: retencion, exportacion, SIEM externo, alertas de seguridad, auditoria mas amplia y correlationId completo por request.

## Restricciones persistentes

- No usar Entity Framework Core.
- No usar SQL Server, PostgreSQL ni otras bases relacionales en la API.
- No conectar SQLite desde la API.
- No usar MCP en esta etapa.
- No implementar multi-provider de base de datos.
- No guardar secretos reales en `appsettings.json` ni en archivos versionados.
- No registrar informacion sensible en logs.
- No exponer stack traces en produccion.
- No devolver `PasswordHash` ni refresh tokens almacenados en respuestas de API.
- No publicar imagenes Docker a registry desde CI hasta que exista una decision explicita de release.
