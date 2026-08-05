# Memorias Tecnicas

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
- La aceptacion real de invitaciones por app monitor queda pendiente; no se setea `LinkedUserId` en esta etapa.
- Onboarding avanza a `4/7`, `57%` y `Devices` solo cuando Profile y Vehicle estan `Completed` y existe contacto activo `Invited` o `Linked`.

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
