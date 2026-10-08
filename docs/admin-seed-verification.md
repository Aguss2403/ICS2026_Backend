# Verificación de la inicialización

Verificación de integración realizada el 8 de octubre de 2026 con SDK .NET 8.0.425, EF Core 9.0.6 y SQL Server local en Docker. Las credenciales sintéticas se suministraron mediante variables de entorno y no se incluyen en este informe.

| Comprobación | Resultado |
| --- | --- |
| Solución integrada con configuración por entorno | 64 pruebas aprobadas: 39 de inicialización y 25 de configuración. |
| Roles vacíos, parciales y completos; rutas síncrona y asíncrona | Se completan los faltantes, sin duplicar ni cambiar los Id existentes. |
| Administrador opcional | Creación sin Customer; al repetir se conservan Id, rol y contraseña. |
| Configuración incompleta o inválida y conflictos de identidad | Se rechazan sin exponer credenciales, modificar cuentas ni elevar roles. |
| Registro del contexto | Una sola conexión, DefaultConnection, con ambos callbacks EF. |
| HTTP con SQLite | Login 200, producto sin token 401 y producto con JWT admin 201. |
| HTTP con SQL Server y esquema real | Migración existente aplicada en una base nueva ICSSeedTests_<GUID>, repetición con MigrateAsync, login 200, producto sin token 401 y producto con JWT admin 201. Persistencia y ausencia de Customer verificadas. |
| Compilación | Correcta; conserva los 18 warnings de nulabilidad preexistentes de Domain/Application. |
| git diff --check | Correcto. |

Con las variables de prueba documentadas en [admin-seed.md](admin-seed.md) cargadas externamente:

```powershell
dotnet test Dsw2025Tpi.sln --configuration Release
# Cargar ICS_TEST_SQL_CONNECTION y ejecutar la variante SQL Server:
dotnet test tests/Dsw2025Tpi.Seeding.Tests --configuration Release --no-build --filter FullyQualifiedName~ApiSeedTests
```

La comprobación inicial utilizaba únicamente SQLite. La verificación adicional contra SQL Server resuelve esa limitación. Las pruebas SQL crean una base nueva para cada ejecución y no eliminan bases ni volúmenes existentes.
