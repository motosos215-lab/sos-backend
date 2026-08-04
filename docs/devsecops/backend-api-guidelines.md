# Guia DevSecOps para Backend API

## Desarrollo seguro

- Validar todas las entradas provenientes de clientes Web, moviles, smartwatch o integraciones externas.
- Rechazar payloads incompletos, mal formados o fuera de rango antes de ejecutar casos de uso.
- No exponer secretos en respuestas, logs, excepciones, documentacion o archivos versionados.
- No guardar contrasenas en texto plano.
- Usar hashing resistente para contrasenas cuando se implemente autenticacion.
- Usar BCrypt o Argon2id para passwords; no usar SHA256 simple para contrasenas.
- Manejar JWT con expiracion corta, issuer y audience validados.
- Manejar refresh tokens como secretos de alta sensibilidad.
- Almacenar refresh tokens con proteccion adecuada y capacidad de revocacion.
- Guardar refresh tokens hasheados, nunca en claro.
- No registrar tokens, contrasenas, codigos temporales, datos personales sensibles ni cadenas de conexion.
- Usar rate limiting en endpoints publicos, especialmente autenticacion, recuperacion de cuenta y sincronizacion.
- Mantener security headers basicos en todas las respuestas HTTP.
- Usar HSTS solo en Production; DigitalOcean puede terminar TLS delante de la aplicacion, pero la API conserva el baseline defensivo.
- Usar manejo global de errores para evitar stack traces en produccion.
- No usar `Console.WriteLine` para datos sensibles.
- Validar opciones sensibles al arranque con `ValidateDataAnnotations()` y `ValidateOnStart()` cuando aplica.

## Persistencia

- MongoDB es la base central de la API.
- No usar Entity Framework Core.
- No usar SQL Server ni PostgreSQL.
- No conectar SQLite desde la API.
- SQLite queda limitado a almacenamiento local en apps moviles y sincronizacion via endpoints.
- No usar MCP en esta etapa.
- No implementar multi-provider de base de datos.
- La API debe usar MongoDB Atlas mediante variables de entorno, sin connection strings versionadas.
- Los indices de MongoDB deben asegurarse de forma idempotente al iniciar cuando MongoDB este configurado.
- `/health` es liveness basico; `/health/ready` debe validar MongoDB Atlas en entornos reales y seguir siendo compatible con DigitalOcean App Platform.

## Pruebas

- Agregar pruebas unitarias cuando se agregue logica de dominio o aplicacion.
- Agregar pruebas de integracion para endpoints, persistencia e integraciones internas.
- Agregar pruebas de seguridad para autenticacion, autorizacion, validaciones, rate limiting y manejo de errores.
- Verificar que endpoints protegidos rechacen requests anonimas.
- Verificar que `PasswordHash` no aparezca en respuestas.
- Evitar pruebas que dependan de secretos reales.

## Pull Requests

- Todo cambio debe pasar por Pull Request.
- El flujo esperado es `feature/*` -> `develop` -> `main`.
- Las ramas `develop` y `main` estan protegidas por rulesets.
- Build & Test debe pasar antes del merge.
- Semgrep SAST debe pasar antes del merge.
- CodeQL complementa Semgrep para analisis estatico de C#.
- Semgrep debe ejecutar `p/default`, `p/security-audit`, `p/secrets` y reglas custom locales.
- Dependabot debe revisar paquetes NuGet semanalmente, GitHub Actions mensualmente y Docker semanalmente.
- Revisar dependencias nuevas antes de aprobar un PR.
- No aprobar cambios que introduzcan secretos, paquetes innecesarios o bases de datos no permitidas.
- Los cambios sensibles deben pasar por CODEOWNERS configurado con el usuario o equipo real del repositorio.

## CI/CD

- Build & Test debe ejecutar restore en modo bloqueado, build Release, pruebas unitarias, integracion y seguridad.
- Los `packages.lock.json` son parte del baseline de supply chain y deben mantenerse versionados.
- `dotnet format --verify-no-changes` debe ser bloqueante.
- Las vulnerabilidades NuGet altas o criticas deben bloquear el pipeline; paquetes obsoletos pueden reportarse como informacion inicial.
- Los resultados de pruebas y cobertura deben subirse como artifacts.
- La imagen Docker debe construirse en CI, escanearse con Trivy y producir un SBOM como artifact antes de publicar o desplegar imagenes.
- Las imagenes base de Docker usan tags oficiales revisados por Dependabot; no se fijan digests manualmente sin validacion confiable del digest.
- El deploy a DigitalOcean debe usar el Environment `production` de GitHub con reviewers/aprobaciones configurados en Settings.
- El deploy debe leer la URL publica desde `vars.DO_APP_URL`; `DO_API_TOKEN` y `DO_APP_ID` permanecen como secrets.
- Los nombres de workflows y jobs requeridos por rulesets no deben cambiarse sin actualizar las protecciones.
- Los Pull Requests deben revisarse antes de mergear hacia `develop` o `main`.
