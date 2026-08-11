# Admin Bootstrap

`AdminBootstrap` crea el primer usuario `Admin` al iniciar la API, solo cuando se habilita por configuracion. No agrega endpoints publicos y `POST /api/v1/auth/register` sigue rechazando cuentas Admin.

## Variables

- `AdminBootstrap__Enabled=false`
- `AdminBootstrap__Email=`
- `AdminBootstrap__Password=`
- `AdminBootstrap__FullName=`
- `AdminBootstrap__RunOnlyWhenNoAdminsExist=true`

## Comportamiento

- Con `Enabled=false`, no hace cambios.
- Valida email, password y nombre con la misma politica del registro publico.
- Crea el usuario con `Role = Admin`, activo y password hasheado con BCrypt.
- Si ya existe un Admin y `RunOnlyWhenNoAdminsExist=true`, no crea otro usuario.
- Si el email configurado ya existe, no cambia rol ni password del usuario existente.
- Los logs solo indican estado operativo: disabled, skipped, created o invalid config. No registran passwords, hashes, tokens, connection strings ni variables sensibles.

## Operacion Recomendada

1. Configurar las variables en el entorno privado de despliegue.
2. Iniciar una sola replica de la API para crear el Admin inicial.
3. Confirmar login con `POST /api/v1/auth/login`.
4. Volver a `AdminBootstrap__Enabled=false` despues de crear el Admin.
