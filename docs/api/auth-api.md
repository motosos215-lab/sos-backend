# Auth API

Auth API expone registro, login, login con codigo, refresh, logout y takeover de sesion.

`POST /api/v1/auth/login` y `POST /api/v1/auth/login-with-code` emiten JWT con claim `sid`. `clientDevice` es obligatorio solo para `MobileApp` (`Android`/`iOS`); web usa `SessionType = WebApp` o `AdminWeb` sin romper compatibilidad. La sesion unica por `SessionType`, takeover seguro y transferencia de viaje activo estan documentados en `docs/api/auth-session-takeover-api.md`.

`Admin` conserva compatibilidad y puede seguir autenticando sin `clientDevice` movil.

Endpoints publicos que no emiten tokens (`request-access-code`, `forgot-password`, `reset-password`) no crean ni validan `UserSession`.
