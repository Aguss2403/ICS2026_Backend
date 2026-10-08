# Entorno local con Docker Compose

Requisito: Docker Desktop con contenedores Linux y Docker Compose. No se requiere instalar el SDK ni la herramienta EF en el host para levantar este entorno.

```powershell
Copy-Item .env.example .env
# Completar DB_PASSWORD y JWT_KEY con valores privados de prueba.
docker compose config --quiet
docker compose up -d --build
Invoke-RestMethod http://127.0.0.1:5142/healthcheck
```

Los puertos del host deben estar libres. Ajustar `SQL_PORT` y `API_PORT` en `.env` si es necesario. SQL se publica exclusivamente en `127.0.0.1`; la API se publica igualmente en loopback para la prueba local. Swagger está disponible en `http://127.0.0.1:<API_PORT>/swagger/index.html`, incluso con el entorno Production utilizado por Compose.

El orden de arranque es SQL Server saludable, `db-init` completado correctamente y API. `db-init` utiliza una etapa SDK independiente con `dotnet-ef` 9.0.6: aplica la migración existente y ejecuta los callbacks de inicialización. Si la migración o el seed fallan, la API no se inicia. La imagen de la API sigue utilizando únicamente el runtime ASP.NET Core, con el usuario sin privilegios `app`.

Ambos servicios utilizan `DefaultConnection` con servidor `sqlserver,1433` y base `Dsw2025Tpi`. No utilizan LocalDB ni los puertos publicados del host para comunicarse dentro de la red de Compose. El volumen `sqlserver_data` conserva los datos entre ejecuciones; sus recursos quedan separados por el nombre del proyecto de Compose.

## Variables

| Variable | Uso |
| --- | --- |
| `DB_PASSWORD` | Contraseña SQL local obligatoria, compatible con los requisitos de SQL Server. No tiene valor predeterminado. |
| `JWT_KEY` | Clave JWT privada obligatoria de al menos 32 bytes UTF-8. No tiene valor predeterminado. |
| `SQL_PORT`, `API_PORT` | Puertos publicados en el host; predeterminados 14333 y 5142. |
| `JWT_ISSUER`, `JWT_AUDIENCE`, `JWT_EXPIRE_IN_MINUTES` | Valores JWT comunes a inicialización y API; valores predeterminados indicados en `.env.example`. |
| `SEED_ADMIN_ENABLED` | `false` por defecto. Activar explícitamente para crear el administrador durante la migración. |
| `SEED_ADMIN_USERNAME`, `SEED_ADMIN_EMAIL`, `SEED_ADMIN_PASSWORD` | Obligatorios si el administrador está habilitado. Se suministran únicamente a `db-init`, no al proceso habitual de la API. |

Las variables `ConnectionStrings__DefaultConnection` y `Jwt__*` del mismo ejemplo sirven para la ejecución individual con `docker run --env-file`. Compose construye la conexión a su servicio SQL y utiliza las variables `DB_PASSWORD`/`JWT_KEY` de esta tabla. No basta con completar una sección para utilizar la otra.

`.env` permanece local y está excluido de Git y del contexto Docker. No imprimir `docker compose config` sin `--quiet`, ya que la salida interpolada contiene credenciales. Utilizar valores locales sin separadores de cadena SQL (`;`) para `DB_PASSWORD` y aplicar las reglas de comillas e interpolación de Compose cuando los valores contengan `$` o `#`.

## Administrador e idempotencia

Completar las cuatro variables de administrador antes del primer arranque si se necesita esa cuenta. Al repetir la inicialización, se completan solo los roles faltantes y se conserva la cuenta existente, incluidos Id, rol y contraseña. Cambiar `SEED_ADMIN_PASSWORD` no rota una contraseña existente. Los conflictos con otra cuenta fallan sin modificarla. Ver [política del seed](admin-seed.md).

Para aplicar de nuevo migraciones y seed sobre el volumen conservado:

```powershell
docker compose stop api
docker compose run --rm db-init
# Continuar solo si la inicialización terminó correctamente.
docker compose up -d api
```

El catálogo queda vacío en una base nueva. Los productos de prueba se crean mediante la API utilizando un JWT de administrador; no se incluyen credenciales ni productos de ejemplo en la imagen.

La consulta del catálogo vacío tiene una incidencia preexistente: responde HTTP 500. Su corrección se mantiene en una tarea separada y no forma parte de esta integración de Compose.

## Detener el entorno

```powershell
docker compose down
```

Este comando conserva el volumen. La eliminación de datos debe ser una acción explícita sobre un entorno de prueba identificado; no forma parte del arranque ni de la inicialización.

Referencia: [orden de arranque y condiciones de dependencias en Compose](https://docs.docker.com/compose/how-tos/startup-order/).
