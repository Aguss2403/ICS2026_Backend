# Verificación de autenticación con rol

Fecha: 19 de octubre de 2026. Rama: `feature/validar-rol-autenticacion`, creada desde `origin/development` actualizado en `14255e2d15ca2d23b5b3977c0e26589f75b986dc` (incluye el seed de la PR #3).

Entorno: Linux x64, SDK .NET 8.0.426, runtime 8.0.32, EF Core 9.0.6, configuración Release. Las variables de la suite de seed se generaron como valores sintéticos en memoria, sin usar credenciales personales ni conexión a SQL Server.

| Comprobación | Resultado |
| --- | --- |
| Reconstrucción completa de la solución en Release | Correcta; 0 errores, 17 advertencias preexistentes en otros archivos. Ninguna advertencia en AuthenticationService. |
| Pruebas nuevas de autenticación | 10 aprobadas, 0 fallidas, 0 omitidas. |
| Dsw2025Tpi.Api.Tests completo | 35 aprobadas: 25 de configuración y 10 de autenticación. |
| Dsw2025Tpi.Seeding.Tests existente | 39 aprobadas, incluida la prueba HTTP con SQLite. Sin cambios en el seed ni en sus pruebas. |
| Total de la solución | 74 aprobadas, 0 fallidas, 0 omitidas. |
| git diff --check | Correcto. |

Las pruebas nuevas validan login y JWT firmado de `admin` y `user`, rechazo del rol nulo antes de generar el token, rechazo del huérfano con el mapeo de producción, usuario inexistente, contraseña incorrecta y credenciales vacías o con espacios. Ver los detalles y límites del contexto de pruebas en [authentication-role.md](authentication-role.md).

## Reproducción

Desde la raíz, con SDK .NET 8:

```powershell
dotnet build Dsw2025Tpi.sln --configuration Release --no-incremental
dotnet test Dsw2025Tpi.Api.Tests --configuration Release --no-build --filter FullyQualifiedName~AuthenticationServiceTests
```

Para la suite completa, cargar externamente `ICS_TEST_ADMIN_PASSWORD` y `ICS_TEST_JWT_KEY` según [admin-seed.md](admin-seed.md), y dejar sin configurar `ICS_TEST_SQL_CONNECTION` para usar SQLite en memoria:

```powershell
dotnet test Dsw2025Tpi.sln --configuration Release --no-build
```

En el entorno aislado de esta revisión se invocaron `MSBuild.dll` (`-t:Rebuild -p:Configuration=Release -m:1`) y `vstest.console.dll` del mismo SDK para la reconstrucción y ejecución completa. Fue necesario un ajuste temporal externo al repositorio para que el runner resolviera su propio proceso en `/proc`. Las pruebas se ejecutaron normalmente con xUnit/VSTest; no se sustituyeron por una inspección estática. Este ajuste no forma parte de la rama ni es un requisito del proyecto en una instalación habitual de .NET.

Esta evidencia no afirma una ejecución nueva de Docker, migraciones ni persistencia en SQL Server. La revisión de esos puntos corresponde a la PR #5 cuando su base sea `development`.

## Preparación de la PR

- Base: `development`.
- Compare: `feature/validar-rol-autenticacion`.
- Título sugerido: `Validar el rol antes de emitir el JWT en el login`.
- Describir el rechazo `401` sin token, la conservación del login válido y enlazar este archivo como evidencia.
- Solicitar revisión de otro integrante y esperar su aprobación antes de fusionar.

La preparación de esta rama no crea una PR ni integra cambios en `development` o `main`.
