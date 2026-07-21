# Estrategia CI/CD

## Build & Test

El workflow `Build & Test` valida la solucion antes de integrar cambios. Debe conservar su nombre porque puede estar asociado a rulesets de ramas protegidas.

Responsabilidades:

- Usar .NET 9.
- Restaurar dependencias con `dotnet restore`.
- Compilar en Release.
- Ejecutar pruebas unitarias.
- Ejecutar pruebas de integracion.
- Ejecutar pruebas de seguridad.
- Publicar resultados de pruebas como artifacts.
- Publicar cobertura como artifact.
- Revisar paquetes vulnerables y obsoletos sin romper el flujo inicial.

## Semgrep SAST

El workflow `Semgrep SAST` ejecuta analisis estatico de seguridad. Debe conservar su nombre para no romper checks requeridos.

Configuraciones activas:

- `p/default`
- `p/security-audit`
- `p/secrets`
- `.semgrep/semgrep.yaml`

Las reglas locales cubren secretos hardcodeados, connection strings, JWT keys, tokens, passwords, TLS inseguro, uso sensible de `Random` y logs inseguros.

## Dependabot

Dependabot revisa dependencias de forma controlada:

- NuGet semanalmente contra `develop`.
- GitHub Actions mensualmente contra `develop`.
- Limite de Pull Requests abiertos para evitar saturacion.

## Validacion antes de merge

- Pull Request obligatorio.
- Build & Test en verde.
- Semgrep SAST en verde.
- Sin secretos reales en el diff.
- Sin paquetes prohibidos: Entity Framework Core, SQL Server, PostgreSQL, SQLite o MCP.
- Sin cambios que conecten la API a SQLite o a una base relacional.
- Pruebas agregadas cuando exista nueva logica ejecutable.
