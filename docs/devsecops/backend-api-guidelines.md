# Guia DevSecOps para Backend API

## Desarrollo seguro

- Validar todas las entradas provenientes de clientes Web, moviles, smartwatch o integraciones externas.
- Rechazar payloads incompletos, mal formados o fuera de rango antes de ejecutar casos de uso.
- No exponer secretos en respuestas, logs, excepciones, documentacion o archivos versionados.
- No guardar contrasenas en texto plano.
- Usar hashing resistente para contrasenas cuando se implemente autenticacion.
- Manejar JWT con expiracion corta, issuer y audience validados.
- Manejar refresh tokens como secretos de alta sensibilidad.
- Almacenar refresh tokens con proteccion adecuada y capacidad de revocacion.
- No registrar tokens, contrasenas, codigos temporales, datos personales sensibles ni cadenas de conexion.
- Usar rate limiting en endpoints publicos, especialmente autenticacion, recuperacion de cuenta y sincronizacion.

## Persistencia

- MongoDB es la base central de la API.
- No usar Entity Framework Core.
- No usar SQL Server ni PostgreSQL.
- No conectar SQLite desde la API.
- SQLite queda limitado a almacenamiento local en apps moviles y sincronizacion via endpoints.

## Pruebas

- Agregar pruebas unitarias cuando se agregue logica de dominio o aplicacion.
- Agregar pruebas de integracion para endpoints, persistencia e integraciones internas.
- Agregar pruebas de seguridad para autenticacion, autorizacion, validaciones, rate limiting y manejo de errores.
- Evitar pruebas que dependan de secretos reales.

## Pull Requests

- Todo cambio debe pasar por Pull Request.
- El flujo esperado es `feature/*` -> `develop` -> `main`.
- Las ramas `develop` y `main` estan protegidas por rulesets.
- Build & Test debe pasar antes del merge.
- Semgrep SAST debe pasar antes del merge.
- Revisar dependencias nuevas antes de aprobar un PR.
- No aprobar cambios que introduzcan secretos, paquetes innecesarios o bases de datos no permitidas.
