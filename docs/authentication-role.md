# Validación del rol en el login

[Evidencia de compilación y pruebas](authentication-role-verification.md).

`AuthenticationService.Login` carga al usuario con `Role` y `Customer`, comprueba las credenciales y valida que la referencia `Role` exista antes de llamar a `GenerateToken`. Conserva el rol validado en una variable local y lo utiliza tanto en el JWT como en `ResponseLoginModel`.

| Situación | Resultado |
| --- | --- |
| Usuario y contraseña correctos, con rol | `200`, datos de sesión y JWT firmado con el rol existente. |
| Credenciales correctas, pero sin rol disponible | `InvalidCredentialException`; el controlador existente responde `401` con `Usuario o contraseña inválidos.`. No se genera ni se devuelve un JWT. |
| Usuario inexistente o contraseña incorrecta | Conserva la misma excepción, respuesta `401` y mensaje. |
| Usuario o contraseña vacíos o solo espacios | Conserva `400` con `El usuario y la contraseña son obligatorios.`. |

Se reutiliza `System.Security.Authentication.InvalidCredentialException`, que el controlador ya maneja, en lugar de introducir otra excepción o cambiar el contrato de respuesta. El mensaje genérico evita revelar si existe una cuenta sin rol. No se asigna un rol por defecto ni se permite continuar con un token incompleto.

No se modifican permisos, DTO, entidades, esquema, almacenamiento de contraseñas, registro de usuarios ni el seed integrado mediante la PR #3. No hace falta una migración ni ejecutar nuevamente la inicialización para aplicar esta corrección.

## Pruebas

`Dsw2025Tpi.Api.Tests/AuthenticationServiceTests.cs` ejecuta el servicio y el controlador reales con una base EF Core InMemory aislada por caso:

- Login válido para `admin` y `user`: verifica la respuesta, la firma, emisor, destinatario, vigencia, usuario y rol del JWT.
- Usuario sin rol: verifica explícitamente la navegación nula, la excepción controlada y la respuesta `401` sin un DTO de sesión ni token.
- Usuario inexistente y contraseña incorrecta: conserva rechazo y mensaje.
- Credenciales vacías o solo espacios: conserva `400`.

En los casos de rechazo se utiliza un generador real sin configuración JWT: si el flujo intentara generar el token, fallaría con otra excepción y las pruebas no pasarían. No se cambian las clases de producción para poder simular el generador.

InMemory permite preparar una cuenta inconsistente sin alterar una base SQL Server. Con el mapeo de producción, el `Include` usa una relación obligatoria y excluye al usuario huérfano; una prueba comprueba que también se rechaza ese caso. Para ejercitar directamente la nueva validación, otra prueba utiliza un contexto derivado exclusivo de pruebas con una relación opcional mediante una clave foránea sombra nullable: verifica que la consulta realmente devuelva al usuario con `Role == null`. El modelo y el esquema de producción permanecen intactos.

Estas son pruebas del servicio y del resultado MVC del controlador, no una ejecución HTTP ni una comprobación de migraciones o persistencia en SQL Server. La validación adicional protege el acceso a la navegación si se obtiene un usuario sin rol.

Desde la raíz del repositorio, con SDK .NET 8:

```powershell
dotnet build Dsw2025Tpi.sln --configuration Release
dotnet test Dsw2025Tpi.Api.Tests --configuration Release --no-build --filter FullyQualifiedName~AuthenticationServiceTests
```

Para ejecutar toda la solución, cargar antes las variables sintéticas `ICS_TEST_ADMIN_PASSWORD` y `ICS_TEST_JWT_KEY` que ya requiere la suite de seed; ver [admin-seed.md](admin-seed.md). No configurar `ICS_TEST_SQL_CONNECTION` para la ejecución predeterminada con SQLite en memoria.

```powershell
dotnet test Dsw2025Tpi.sln --configuration Release --no-build
```
