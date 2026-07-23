# Memorias Tecnicas

## Decisiones actuales

- MotoSOS.API es un proyecto Web API en .NET 9.
- MongoDB es la base de datos central de la plataforma.
- SQLite se usara solo en la app movil como almacenamiento local.
- La sincronizacion desde SQLite hacia datos centrales debe realizarse mediante endpoints de la API.
- GitHub Actions incluye Build & Test.
- GitHub Actions incluye Semgrep SAST.
- Semgrep SAST usa reglas administradas y reglas custom locales en `.semgrep/semgrep.yaml`.
- Dependabot revisa paquetes NuGet y GitHub Actions.
- El flujo de ramas es `feature/*` -> `develop` -> `main`.
- Las ramas `main` y `develop` estan protegidas con rulesets.
- DevSecOps se aplica desde el inicio del proyecto.
- MotoSOS.API es el unico punto de acceso a datos centrales para Web, apps moviles, smartwatch, notificaciones, analitica y Machine Learning.
- La API incluye baseline de security headers, rate limiting y manejo global de errores.
- La base de autenticacion usa JWT Bearer, roles `Admin`, `Rider` y `Monitor`, BCrypt para passwords y refresh tokens hasheados.
- La pantalla de registro requiere `accountType`, `confirmPassword` y `acceptTerms`.
- El maquetado usa `Conductor`, pero el backend lo mapea a `Rider`.
- `forgot-password` y `access-code` quedan preparados sin proveedor externo real y sin enumerar usuarios.
- Login soporta `rememberMe`, que solo extiende la expiracion del refresh token.

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
