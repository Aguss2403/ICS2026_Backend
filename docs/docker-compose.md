# Entorno local con Docker Compose

Requisitos: Docker Desktop con contenedores Linux, Docker Compose y SDK .NET 8 en el host. La herramienta dotnet-ef debe ser 9.0.6.

## Fase 1: SQL y preparación desde el host

```powershell
Copy-Item .env.example .env
# Completar DB_PASSWORD y JWT_KEY con valores privados de prueba.
docker compose config --quiet
docker compose up -d --wait sqlserver
```

Los puertos del host deben estar libres. Ajustar SQL_PORT y API_PORT en .env si es necesario. Ambos servicios se publican en 127.0.0.1. El volumen sqlserver_data conserva los datos y sus recursos quedan separados por el nombre del proyecto de Compose.

La siguiente preparación lee la configuración de Compose sin imprimir sus credenciales, y adapta únicamente el servidor de la conexión para las herramientas del host:

```powershell
$composeConfig = docker compose config --format json | ConvertFrom-Json
$sqlHostPort = $composeConfig.services.sqlserver.ports[0].published
$env:ConnectionStrings__DefaultConnection = $composeConfig.services.api.environment.ConnectionStrings__DefaultConnection.Replace('Server=sqlserver,1433;', "Server=127.0.0.1,$sqlHostPort;")
$env:Jwt__Key = $composeConfig.services.api.environment.Jwt__Key
$env:Jwt__Issuer = $composeConfig.services.api.environment.Jwt__Issuer
$env:Jwt__Audience = $composeConfig.services.api.environment.Jwt__Audience
$env:Jwt__ExpireInMinutes = $composeConfig.services.api.environment.Jwt__ExpireInMinutes
$env:ASPNETCORE_ENVIRONMENT = 'Production'
if (-not (Test-Path ./.local-tools/dotnet-ef.exe)) {
    dotnet tool install dotnet-ef --tool-path ./.local-tools --version 9.0.6
}
dotnet build Dsw2025Tpi.Api --configuration Release
& ./.local-tools/dotnet-ef database update --project Dsw2025Tpi.Data --startup-project Dsw2025Tpi.Api --configuration Release --no-build
```

Continuar con la fase 2 solo si la migración finaliza con código cero. SQL saludable no implica que el esquema esté preparado. Se utiliza la migración existente, sin borrar o recrear bases.

El seed siempre completa los roles faltantes. Para crear además el administrador opcional, cargar antes del update las variables Seed__Admin__Enabled=true, Seed__Admin__Username, Seed__Admin__Email y Seed__Admin__Password en el entorno del host. No hay credenciales de administrador predeterminadas. Al repetir el update se conservan la cuenta, el rol, el Id y la contraseña existentes. Ver [política y configuración del seed](admin-seed.md).

Después de preparar la cuenta, deshabilitar el seed y retirar su contraseña del entorno habitual. Estas credenciales no se suministran al servicio api de Compose.

## Fase 2: API

```powershell
docker compose up -d --build api
Invoke-RestMethod http://127.0.0.1:5142/healthcheck
```

Swagger está disponible en http://127.0.0.1:<API_PORT>/swagger/index.html con Production. La API espera SQL saludable y utiliza DefaultConnection con servidor sqlserver,1433 y base Dsw2025Tpi. Dentro de Docker no se utilizan LocalDB ni el puerto publicado del host. La imagen de la API conserva únicamente el runtime ASP.NET Core y ejecuta con el usuario sin privilegios app.

## Variables de Compose

| Variable | Uso |
| --- | --- |
| DB_PASSWORD | Contraseña SQL local obligatoria, compatible con los requisitos de SQL Server. Sin valor predeterminado. |
| JWT_KEY | Clave JWT privada obligatoria de al menos 32 bytes UTF-8. Sin valor predeterminado. |
| SQL_PORT, API_PORT | Puertos publicados en el host; predeterminados 14333 y 5142. |
| JWT_ISSUER, JWT_AUDIENCE, JWT_EXPIRE_IN_MINUTES | Configuración JWT indicada en .env.example. |

Las variables ConnectionStrings__DefaultConnection y Jwt__* del mismo ejemplo sirven para ejecutar la imagen individualmente con docker run --env-file. Compose utiliza DB_PASSWORD/JWT_KEY y construye la conexión a su servicio SQL. .env no se importa automáticamente al entorno de las herramientas del host: por eso la fase 1 adapta la configuración explícitamente.

.env está excluido de Git y del contexto Docker. No imprimir docker compose config sin --quiet: la salida interpolada contiene credenciales. Para DB_PASSWORD utilizar valores locales sin separadores de cadena SQL (;), y aplicar las reglas de comillas e interpolación de Compose cuando los valores contengan $ o #.

## Reinicio y persistencia

```powershell
docker compose down
docker compose up -d api
```

Estos comandos conservan el volumen y los datos. Para repetir la migración y la inicialización sobre esa base, ejecutar de nuevo la fase 1 sin borrar el volumen. La disponibilidad de SQL y la preparación del esquema son pasos distintos.

El catálogo queda vacío en una base nueva y responde HTTP 200 con items vacío y total cero. Los productos de prueba se crean mediante la API con un JWT de administrador. Ver [comprobaciones HTTP y persistencia](docker-api-validation.md).
