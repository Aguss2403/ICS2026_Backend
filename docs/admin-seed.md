# Inicialización de roles y administrador local

El esquema sigue siendo el de `20251125215438_InitialCreate`. No hay una migración nueva: se insertan datos compatibles con las tablas `Roles` y `Users` existentes. El contexto se registra una sola vez en `AddDomainServices`.

La inicialización se ejecuta en `UseSeeding` y `UseAsyncSeeding` de EF Core 9.0.6. `dotnet ef database update` utiliza la ruta síncrona; `Database.MigrateAsync` utiliza la asíncrona. Ambas usan la misma validación y política de conflictos. EF mantiene el bloqueo de migración durante el callback. Al repetir el update, EF vuelve a ejecutar la inicialización aunque no haya migraciones pendientes. No se llama al seed desde el arranque de la API y no hay endpoint de seed. El `RoleSeeder` antiguo de Identity no está registrado: las entidades reales son `Role` y `User` del dominio, no Identity.

Referencia: [inicialización de datos en EF Core](https://learn.microsoft.com/en-us/ef/core/modeling/data-seeding).

## Configuración externa

| Variable de entorno | Comportamiento |
| --- | --- |
| `Seed__Admin__Enabled` | `false` por defecto; usar `true` para habilitar la creación. Un valor que no sea booleano falla. |
| `Seed__Admin__Username` | Obligatorio si está habilitado, máximo 50 caracteres, sin espacios en los extremos ni caracteres de control. |
| `Seed__Admin__Email` | Correo válido, sin nombre de presentación, máximo 100 caracteres. |
| `Seed__Admin__Password` | Obligatoria si está habilitado, no vacía ni solo espacios, máximo 100 caracteres. Se conserva exactamente como se suministra. |
| `ConnectionStrings__DefaultConnection` | Conexión externa a SQL Server. Es la única conexión utilizada por la API en todos los entornos. |
| `Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience` | Configuración externa de la autenticación existente; usar una clave sintética de al menos 32 bytes para la comprobación local. |

No hay username, email ni contraseña predeterminados para el administrador. La validación menciona las claves, sin incluir sus valores. No activar `EnableSensitiveDataLogging` ni guardar archivos de credenciales en Git. La clave JWT y la conexión SQL se suministran externamente; utilizar credenciales sintéticas para esta ejecución.

Los roles siempre se comprueban individualmente. Si falta `admin` o `user`, se inserta únicamente el faltante. Los identificadores existentes se conservan. Con el administrador deshabilitado, se ignoran sus campos y solo se completan los roles.

Si no hay ninguna cuenta con ese username o correo, se crea un `User` con rol `admin`, sin `Customer`. Si una única cuenta coincide en ambos campos y su rol es `admin`, se conserva completa, incluidos Id, contraseña y RoleId. Una contraseña distinta en la configuración no la rota. Si coincide solo un campo, coinciden cuentas diferentes, el correo está duplicado o el rol es distinto, la inicialización falla sin modificar ni elevar cuentas y sin insertar los roles pendientes. La búsqueda respeta la comparación del proveedor de base de datos; la confirmación de la identidad admite diferencias de mayúsculas y minúsculas, pero rechaza otras diferencias.

`AuthenticationService` almacena y compara actualmente la contraseña en texto plano. El seed usa ese formato para que el login existente funcione. Esta etapa no modifica registro/login ni implementa hash o migración de contraseñas. Después de integrar la tarea de hash del TP1, habrá que adaptar el seed al mecanismo nuevo.

## Migración e inicialización con SQL Server en Docker

Requisitos: Docker con un SQL Server compatible, SDK .NET 8 y herramienta `dotnet-ef` 9.0.6. Los comandos Bash siguientes se ejecutan desde la raíz del repositorio; no requieren Compose ni un Dockerfile del backend. El backend y la herramienta EF corren en el host y SQL Server corre en Docker. No se afirma una ejecución del backend dentro de un contenedor.

Para un contenedor SQL **existente**, conservar su volumen y usar su host/puerto y sus credenciales externas. No ejecutar `docker rm`, `docker volume rm`, `EnsureDeleted`, `EnsureCreated` ni bajar migraciones para inicializar esa base.

Solo si se necesita una instancia nueva de prueba, elegir nombres nuevos para contenedor y volumen y cargar `MSSQL_SA_PASSWORD` desde un entorno externo con una contraseña sintética que cumpla los requisitos de SQL Server. Las variables de nombres tampoco deben apuntar a recursos existentes:

```bash
: "${MSSQL_SA_PASSWORD:?Cargar una contraseña sintética externa}"
: "${ICS_SQL_CONTAINER:?Elegir un nombre nuevo de contenedor}"
: "${ICS_SQL_VOLUME:?Elegir un nombre nuevo de volumen}"
if docker container inspect "$ICS_SQL_CONTAINER" >/dev/null 2>&1; then
  echo "El contenedor ya existe; elegir un nombre nuevo."; exit 1
fi
if docker volume inspect "$ICS_SQL_VOLUME" >/dev/null 2>&1; then
  echo "El volumen ya existe; elegir un nombre nuevo."; exit 1
fi
docker volume create "$ICS_SQL_VOLUME"
docker run --name "$ICS_SQL_CONTAINER" \
  --env ACCEPT_EULA=Y --env MSSQL_PID=Developer --env MSSQL_SA_PASSWORD \
  --publish 127.0.0.1:14333:1433 \
  --mount "type=volume,source=$ICS_SQL_VOLUME,target=/var/opt/mssql" \
  --detach mcr.microsoft.com/mssql/server:2022-latest
```

Esperar a que SQL Server esté listo. Cargar la conexión externa con servidor `127.0.0.1,14333`, una base de prueba nueva elegida para esta ejecución y `TrustServerCertificate=True` para el certificado local. Si se usa un contenedor existente, adaptar el puerto. No imprimir la conexión.

Cargar también las variables de seed y JWT de la tabla anterior mediante el entorno o un gestor de secretos. Para crear el administrador, `Seed__Admin__Enabled` debe valer `true` y deben estar completos los otros tres campos. No usar credenciales personales. Ejecución:

```bash
: "${ConnectionStrings__DefaultConnection:?Cargar la conexión externa}"
: "${Jwt__Key:?Cargar una clave JWT sintética externa}"
: "${Jwt__Issuer:?Cargar el issuer externo}"
: "${Jwt__Audience:?Cargar el audience externo}"
dotnet tool install dotnet-ef --version 9.0.6 --tool-path ./.local-tools
./.local-tools/dotnet-ef --version
dotnet restore Dsw2025Tpi.sln
./.local-tools/dotnet-ef database update \
  --project Dsw2025Tpi.Data --startup-project Dsw2025Tpi.Api
```

Si la herramienta ya está instalada en esa ruta, verificar que sea 9.0.6 en lugar de reinstalarla. También puede utilizarse una instalación existente de esa versión. Una configuración inválida o un conflicto produce error y código de salida no cero. Si EF ya aplicó el esquema antes de fallar el seed, corregir la configuración/conflicto y repetir `database update`; no borrar la base. Un script SQL de migración por sí solo no ejecuta los callbacks de seed.

Repetir el mismo `database update` para comprobar idempotencia. Luego deshabilitar el seed y quitar su contraseña del entorno de ejecución habitual de la API:

```bash
export Seed__Admin__Enabled=false
unset Seed__Admin__Password
dotnet run --project Dsw2025Tpi.Api --no-launch-profile \
  --urls http://localhost:5080
```

Esto no deshabilita ni cambia la cuenta ya creada. En otro proceso puede hacerse login en `POST /api/auth/login` con las credenciales sintéticas conservadas fuera del repositorio y crear un producto por `POST /api/products` usando el Bearer token devuelto. No escribir tokens ni contraseñas en logs. La prueba automatizada siguiente comprueba esas solicitudes sin imprimir sus contenidos.

## Pruebas acotadas

La suite genera credenciales sintéticas en memoria y utiliza SQLite. No requiere variables de entorno, SQL Server ni Docker Compose:

```bash
dotnet test tests/Dsw2025Tpi.IntegrationTests --configuration Release --filter FullyQualifiedName~ApiSeedTests
```

`ApiSeedTests` conserva la comprobación de ambos callbacks EF de inicialización, login y autorización HTTP. Los casos de `DataSeederTests` y `SeedRegistrationTests` también están en ese proyecto. La variante HTTP opcional que migraba SQL Server se retiró al unificar la infraestructura del TP4; las migraciones SQL no se aplican sobre SQLite.

Ver [infraestructura de pruebas TP4](tp4-testing.md).
