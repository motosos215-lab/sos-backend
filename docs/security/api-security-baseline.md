# Baseline de Seguridad API

## Controles actuales

- Manejo global de excepciones para respuestas limpias y consistentes.
- Stack traces ocultos fuera de Development.
- Logs de requests sin query string ni datos sensibles.
- Security headers basicos en respuestas HTTP.
- Rate limiter global moderado.
- Politica futura `AuthRateLimit` para endpoints de autenticacion.
- Health checks excluidos del rate limiter global.
- Semgrep SAST con reglas administradas y reglas custom locales.
- Dependabot para NuGet y GitHub Actions.

## Headers de seguridad

La API agrega:

- `X-Content-Type-Options: nosniff`
- `X-Frame-Options: DENY`
- `Referrer-Policy: no-referrer`
- `X-XSS-Protection: 0`
- `Content-Security-Policy` fuera de Development para evitar romper herramientas de desarrollo.

## Rate limiting

La configuracion actual usa capacidades nativas de ASP.NET Core:

- Limite global moderado por ventana fija.
- Exclusion de `/health` y `/health/ready`.
- Politica `AuthRateLimit` preparada para login, refresh token y recuperacion de cuenta cuando existan.

## Persistencia segura

- MongoDB es la unica base central permitida.
- No se conecta MongoDB todavia desde la API en esta etapa.
- No existen connection strings reales en archivos versionados.
- SQLite queda reservado para apps moviles/offline y sincronizacion por endpoints.

## Restricciones

- No Entity Framework Core.
- No SQL Server.
- No PostgreSQL.
- No SQLite en la API.
- No MCP en esta etapa.
- No multi-provider de base de datos.
- No secretos reales en repositorio.
