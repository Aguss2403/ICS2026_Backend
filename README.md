# Trabajo Práctico Integrador
## Desarrollo de Software
### Backend

## Introducción
Se desea desarrollar una plataforma de comercio electrónico (E-commerce). 
En esta primera etapa el objetivo es construir el módulo de Órdenes, permitiendo la gestión completa de éstas.

## Visión General del Producto
Del relevamiento preliminar se identificaron los siguientes requisitos:
- Los visitantes pueden consultar los productos sin necesidad de estar registrados o iniciar sesión.
- Para realizar un pedido se requiere el inicio de sesión.
- Una orden, para ser aceptada, debe incluir la información básica del cliente, envío y facturación.
- Antes de registrar la orden se debe verificar la disponibilidad de stock (o existencias) de los productos.
- Si la orden es exitosa hay que actualizar el stock de cada producto.
- Se deben poder consultar órdenes individuales o listar varias con posibilidad de filtrado.
- Será necesario el cambio de estado de una orden a medida que avanza en su ciclo de vida.
- Los administradores solo pueden gestionar los productos (alta, modificación y baja) y actualizar el estado de la orden.
- Los clientes pueden crear y consultar órdenes.

[Documento completo](https://frtutneduar.sharepoint.com/:b:/s/DSW2025/ETueAd4rTe1Gilj_Yfi64RYB5oz9s2dOamxKSfMFPREbiA?e=azZcwg) 

## Alcance para el Primer Parcial
> [!IMPORTANT]
> Del apartado `IMPLEMENTACIÓN` (Pag. 7), completo hasta el punto `6` (inclusive)


### Características de la Solución

- Lenguaje: C# 12.0
- Plataforma: .NET 8

## Inicialización local de roles y administrador

La migración e inicialización con EF Core 9.0.6, las variables externas del administrador y las pruebas acotadas están documentadas en [docs/admin-seed.md](docs/admin-seed.md). El administrador está deshabilitado por defecto; la inicialización se ejecuta con la migración, antes de iniciar la API.

[Resultados verificables de este cambio](docs/admin-seed-verification.md).

## Estrategia de ramas

- `main` contiene la versión estable de la API.
- `development` reúne los cambios aprobados para integración.
- `feature/nombre-funcionalidad` se crea desde `development` para desarrollar una funcionalidad o una tarea del sprint.
- `hotfix/descripcion` se crea desde `main` para corregir un problema urgente de la versión estable.

### Integración de cambios

1. Crear una rama `feature/` desde la última versión de `development` y realizar allí los commits.
2. Abrir un Pull Request de la rama `feature/` hacia `development`.
3. Solicitar la revisión de otro integrante. El Pull Request debe recibir al menos una aprobación antes de fusionarse.
4. Para publicar una versión estable, abrir un Pull Request de `development` hacia `main` y obtener una aprobación antes de fusionarlo.

Los cambios en `main` y `development` se integran únicamente mediante Pull Requests; no se realizan commits directos en esas ramas.

### Correcciones urgentes

1. Crear una rama `hotfix/` desde `main` y realizar la corrección allí.
2. Abrir un Pull Request hacia `main` y obtener la aprobación de otro integrante antes de fusionarlo.
3. Incorporar la misma corrección en `development` mediante otro Pull Request para que las ramas no diverjan.

## Configuración local y contenedor de la API

Requisitos: SDK .NET 8 para ejecutar desde el host, Docker Desktop con contenedores Linux para construir la imagen y una base SQL Server preparada con la migración del repositorio.

La conexión utilizada por toda la API es `ConnectionStrings:DefaultConnection`. El contexto se registra una sola vez. `Dsw2025TpiEntities` ya no se utiliza.

| Variable | Requisito / valor predeterminado |
| --- | --- |
| `ConnectionStrings__DefaultConnection` | Obligatoria en Production; debe indicar servidor y base SQL Server. Development conserva un ejemplo LocalDB sin contraseña. |
| `Jwt__Key` | Obligatoria en todos los entornos; clave privada de al menos 32 bytes UTF-8 para HS256. No existe una clave incorporada al código. |
| `Jwt__Issuer` | `Dsw2025Tpi.Api`; puede reemplazarse por entorno. |
| `Jwt__Audience` | `Dsw2025Tpi.Api.Users`; puede reemplazarse por entorno. |
| `Jwt__ExpireInMinutes` | `60`; debe ser un entero positivo. |
| `ASPNETCORE_ENVIRONMENT` | La imagen utiliza `Production`; seleccionar explícitamente el entorno al ejecutar desde el host. |
| `ASPNETCORE_URLS` | Opcional; por ejemplo `http://+:8080` en Docker o `http://127.0.0.1:5142` en el host. La imagen escucha HTTP 8080 por defecto. |

El arranque valida SQL y los parámetros JWT antes de construir el servidor. Los errores indican la configuración ausente o inválida y no incluyen sus valores secretos. La validación del formato no garantiza que SQL esté disponible: se debe comprobar una operación real de datos.

### Desde PowerShell

Proporcionar los valores privados mediante el entorno de la terminal, sin incorporarlos a Git. Las siguientes variables de sesión deben existir antes de ejecutar:

```powershell
$env:ConnectionStrings__DefaultConnection = $conexionSqlLocal
$env:Jwt__Key = $claveJwtLocal
$env:ASPNETCORE_ENVIRONMENT = 'Production'
$env:ASPNETCORE_URLS = 'http://127.0.0.1:5142'
dotnet run --project Dsw2025Tpi.Api --configuration Release --no-launch-profile
```

`$conexionSqlLocal` y `$claveJwtLocal` representan valores privados suministrados por el integrante. Si se usa un perfil de lanzamiento, este puede reemplazar el entorno y el puerto; por eso el ejemplo utiliza `--no-launch-profile`.

### Docker

```powershell
Copy-Item .env.example .env
# Completar .env con configuración y credenciales sintéticas de la base local.
docker build -t ics2026-backend:local .
docker run -d --name ics2026-api --env-file .env -p 127.0.0.1:5142:8080 ics2026-backend:local
Invoke-RestMethod http://127.0.0.1:5142/healthcheck
Invoke-RestMethod http://127.0.0.1:5142/api/products
docker stop ics2026-api
docker rm ics2026-api
```

El puerto 5142 del host debe estar libre. Se puede publicar otro puerto sin cambiar el 8080 interno. `.env` está excluido de Git y del contexto Docker; el ejemplo no contiene credenciales reales. `docker --env-file` no expande referencias a otras variables: escribir la conexión completa y no envolver los valores en comillas de shell.

En Docker Desktop, para acceder a SQL publicado en el host se puede utilizar `Server=host.docker.internal,14333;Database=ICS2026_LocalTest;...`. `localhost` dentro del contenedor identifica al propio contenedor. Cuando SQL se ejecute en una red de Compose, utilizar el nombre de su servicio y su puerto interno. `TrustServerCertificate=True` puede utilizarse exclusivamente para la base local de prueba; el ejemplo de conexión debe adaptarse a las credenciales y políticas del entorno.

La imagen final contiene el runtime ASP.NET Core y la API publicada, utiliza el usuario sin privilegios `app` y no incluye el SDK, el repositorio Git ni los archivos locales de configuración.

Esta parte no modifica el comportamiento actual de Swagger: todavía está habilitado solamente en Development. Para probarlo con la imagen mientras se integra el trabajo correspondiente, se puede ejecutar con `-e ASPNETCORE_ENVIRONMENT=Development` y una conexión SQL externa. La prueba de Production verifica `/healthcheck` y una operación contra SQL. La habilitación de Swagger en Production, la inicialización del administrador y Compose se integran por separado.

### Pruebas de configuración

```powershell
dotnet test Dsw2025Tpi.sln --configuration Release
```

El proyecto `Dsw2025Tpi.Api.Tests` verifica configuración completa, valores obligatorios ausentes, clave corta, expiración inválida, conexión SQL mal formada, ausencia de secretos en los errores y uso exclusivo de `DefaultConnection`. Estas pruebas no requieren SQL real y no reemplazan la suite de negocio del TP1.

Referencias técnicas: [configuración por entorno](https://learn.microsoft.com/aspnet/core/fundamentals/configuration/?view=aspnetcore-8.0) y [contenedores ASP.NET Core](https://learn.microsoft.com/aspnet/core/host-and-deploy/docker/building-net-docker-images?view=aspnetcore-8.0).
