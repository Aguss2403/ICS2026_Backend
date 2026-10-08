# Comprobaciones HTTP de la API dockerizada

El procedimiento utiliza PowerShell 7 y un entorno local exclusivo de prueba preparado según [Docker Compose](docker-compose.md). Cargar mediante variables del proceso las credenciales sintéticas `ICS_SMOKE_ADMIN_USERNAME`, `ICS_SMOKE_ADMIN_PASSWORD` y `ICS_SMOKE_CLIENT_PASSWORD`. La primera cuenta debe existir con rol admin; el cliente se registra con un identificador nuevo en cada ejecución. Los valores privados no se incorporan al repositorio ni se imprimen.

```powershell
pwsh -File ./scripts/Test-DockerApi.ps1 -BaseUrl http://127.0.0.1:5142
```

El script verifica healthcheck, Swagger UI, OpenAPI, listado sin coincidencias (200 con items vacío y total cero), login del administrador, registro/login de un cliente, creación sin token (401), creación con cliente (403), ausencia de datos guardados en ambos rechazos, creación con admin (201) y consulta pública del producto persistido. Los SKU y usuarios incorporan un identificador distinto en cada ejecución.

La prueba automatizada `ApiSeedTests` comprueba además el listado sobre una base completamente vacía y un filtro sin resultados después de crear un producto. Se ejecuta por defecto con SQLite y admite la variante SQL Server descrita en [admin-seed.md](admin-seed.md).

## Inicialización repetida y reinicio

Para incluir la comprobación de persistencia, utilizar un proyecto de Compose explícito, con API y SQL ejecutándose, y `dotnet-ef` 9.0.6 instalado en el host. Compilar previamente la API en Release. Si se utiliza un SDK portable, configurar `DOTNET_ROOT` y agregar su directorio a `PATH` antes de invocar el script.

```powershell
dotnet build Dsw2025Tpi.Api --configuration Release
pwsh -File ./scripts/Test-DockerApi.ps1 -BaseUrl http://127.0.0.1:5142 -VerifyLifecycle -ComposeProjectName nombre-del-proyecto
```

`-EfToolPath` permite indicar otra instalación de la herramienta EF; por defecto utiliza `./.local-tools/dotnet-ef`. La opción de ciclo de vida verifica que el puerto de BaseUrl coincida con el proyecto elegido, carga temporalmente la conexión del host y la configuración del seed, repite `database update`, detiene únicamente los servicios api/sqlserver de ese proyecto y los vuelve a iniciar sin eliminar el volumen. Comprueba que admin, cliente y producto conservan sus identificadores y roles. Finalmente restaura las variables de entorno modificadas.

La ejecución deja sus usuarios y productos sintéticos en la base de prueba para permitir inspección. No borra bases, volúmenes ni registros existentes. No utilizar la opción de reinicio sobre un entorno compartido que deba mantenerse disponible.

La salida aprobada contiene solamente nombres de comprobaciones, códigos HTTP y el indicador `lifecycleVerified`. Ante un fallo identifica el paso, omite respuestas y credenciales y termina con código 1. La ejecución aprobada termina con código 0. Para afirmar que se verificó la persistencia, `lifecycleVerified` debe ser true.

La corrección del catálogo vacío conserva rutas, DTO y permisos; no normaliza otros errores ni modifica PUT/PATCH. Actions y despliegue quedan fuera de esta etapa.
