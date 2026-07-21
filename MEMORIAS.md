# Memorias Tecnicas

## Decisiones actuales

- MotoSOS.API es un proyecto Web API en .NET 9.
- MongoDB es la base de datos central de la plataforma.
- SQLite se usara solo en la app movil como almacenamiento local.
- La sincronizacion desde SQLite hacia datos centrales debe realizarse mediante endpoints de la API.
- GitHub Actions incluye Build & Test.
- GitHub Actions incluye Semgrep SAST.
- El flujo de ramas es `feature/*` -> `develop` -> `main`.
- Las ramas `main` y `develop` estan protegidas con rulesets.
- DevSecOps se aplica desde el inicio del proyecto.
- MotoSOS.API es el unico punto de acceso a datos centrales para Web, apps moviles, smartwatch, notificaciones, analitica y Machine Learning.

## Restricciones persistentes

- No usar Entity Framework Core.
- No usar SQL Server, PostgreSQL ni otras bases relacionales en la API.
- No conectar SQLite desde la API.
- No guardar secretos reales en `appsettings.json` ni en archivos versionados.
