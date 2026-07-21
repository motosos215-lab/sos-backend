# OpenCode Repository Guidelines

Este repositorio contiene la API central de MotoSOS. OpenCode debe trabajar de forma incremental, segura y compatible con las decisiones tecnicas actuales.

## Reglas de trabajo

- Revisar el contexto del codigo antes de hacer cambios.
- Evitar cambios masivos si no son necesarios para el objetivo solicitado.
- No romper workflows existentes de GitHub Actions.
- No introducir secretos reales en codigo, configuracion, pruebas o documentacion.
- No usar Entity Framework Core.
- No agregar bases relacionales como SQL Server, PostgreSQL o SQLite dentro de la API.
- No usar MCP en esta etapa del proyecto.
- No implementar multi-provider de base de datos.
- Mantener compatibilidad con MongoDB como base central.
- Crear pruebas unitarias, de integracion o seguridad cuando se agregue logica ejecutable.
- Ejecutar `dotnet build .\MotoSOS.API.slnx -c Release` antes de sugerir commit.
- Ejecutar `dotnet test .\MotoSOS.API.slnx -c Release` antes de sugerir commit.
- No hacer commit automaticamente salvo solicitud explicita.
- Mantener Pull Request obligatorio para cambios hacia `develop` y `main`.
- Mantener Build & Test y Semgrep SAST como checks obligatorios.
- Mantener logs sin secretos, tokens, passwords, refresh tokens ni datos sensibles.
- Mantener rate limiting, security headers y manejo global de errores como baseline de seguridad.
- Revisar alertas de Dependabot y reglas custom de Semgrep antes de mergear.
- Implementar autenticacion con JWT, refresh tokens seguros y hashing de passwords sin secretos hardcodeados.
- No exponer `PasswordHash`, refresh tokens almacenados ni detalles sensibles en respuestas o logs.

## Convenciones

- El codigo fuente usa nombres en ingles.
- La documentacion puede estar en espanol.
- La API es el unico punto de acceso a datos centrales.
- SQLite queda reservado para almacenamiento local de apps moviles y sincronizacion via endpoints.
- El flujo de ramas es `feature/*` -> `develop` -> `main`.
