# Infraestructura de pruebas TP4

[Tarjeta TP4-01](https://trello.com/c/qDyTiGJF).

## Ejecutar

Requisito: SDK .NET 8. Desde la raíz del repositorio:

```bash
dotnet restore Dsw2025Tpi.sln
dotnet build Dsw2025Tpi.sln --configuration Release --no-restore --warnaserror
dotnet test Dsw2025Tpi.sln --configuration Release --no-build --no-restore
dotnet test Dsw2025Tpi.sln --configuration Release --no-build --no-restore
```

También se puede usar directamente `dotnet test Dsw2025Tpi.sln --configuration Release`.
Para correr un solo proyecto:

```bash
dotnet test tests/Dsw2025Tpi.UnitTests --configuration Release
dotnet test tests/Dsw2025Tpi.IntegrationTests --configuration Release
```

No se necesita `.env`, Azure SQL, SQL Server, Docker Compose ni credenciales externas.
La contraseña y las claves JWT son sintéticas y se generan en memoria. La variable opcional
`ICS_TEST_ADMIN_PASSWORD` se conserva para las regresiones de seed; no es necesaria.
`ICS_TEST_SQL_CONNECTION` y `ICS_TEST_JWT_KEY` no seleccionan ni configuran la fábrica TP4.

## Proyectos y dependencias

Ambos proyectos están bajo `tests/` e incluidos en `Dsw2025Tpi.sln`.

| Proyecto | Contenido | Paquetes específicos |
| --- | --- | --- |
| `Dsw2025Tpi.UnitTests` | Controladores con mocks, middleware, configuración y entidades sin base de datos | Moq 4.20.72 |
| `Dsw2025Tpi.IntegrationTests` | HTTP real, seed y persistencia | Mvc.Testing 8.0.17, EF Core SQLite 9.0.6 |

Los dos usan xUnit 2.9.3, runner VS 2.8.2, Microsoft.NET.Test.Sdk 17.12.0 y
FluentAssertions 6.12.2. Mvc.Testing corresponde a ASP.NET Core 8 y SQLite coincide
con EF Core 9.0.6 de la aplicación. Las referencias explícitas son API, Application y
Domain y Data (en unitarias, Data se usa solo para comprobar configuración sin abrir conexiones).

Se reutilizaron los casos anteriores de `Dsw2025Tpi.Api.Tests`, `Dsw2025Tpi.Orders.Tests`
y `Dsw2025Tpi.Seeding.Tests`, distribuyéndolos por tipo de prueba. Las regresiones de
autenticación que simulan usuarios sin rol conservan EF InMemory 9.0.6 para permitir
relaciones huérfanas deliberadas. Los HTTP de productos ahora usan SQLite.
Los 18 escenarios de negocio del TP4 se implementan en las otras tarjetas.

## Ejemplo HTTP con datos y JWT

Crear el archivo en `tests/Dsw2025Tpi.IntegrationTests`. Este patrón se ejecuta también en
`InfrastructureSmokeTests.TestingHostReadsSeededDataThroughRealJwtAuthorization`:

```csharp
using System.Net;
using System.Net.Http.Json;
using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Dsw2025Tpi.IntegrationTests;

public class ExampleTests
{
    [Fact]
    public async Task ReadKnownProduct()
    {
        using var factory = new SqliteApiFactory();
        using var client = factory.CreateApiClient();
        var product = TestData.Product(sku: "TP4-001", price: 100m, stock: 5);
        Customer customer = null!;
        await factory.SeedAsync(async db =>
        {
            db.Products.Add(product);
            customer = await TestData.AddCustomerAsync(db);
        });
        factory.Authenticate(client, Role.User, customer.User!.Username);

        using var response = await client.GetAsync($"/api/products/{product.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ProductModel.ProductResponse>();
        result!.Id.Should().Be(product.Id);
    }
}
```

Para operaciones de administrador usar `factory.Authenticate(client)` o pasar `Role.Admin`.
Para enviar el JWT en una petición puntual:

```csharp
using var request = new HttpRequestMessage(HttpMethod.Get, "/api/products/admin");
request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
    "Bearer", factory.CreateToken(Role.Admin));
using var response = await client.SendAsync(request);
```

`CreateToken` invoca `JwtTokenServices` registrado en la API. No reemplaza JwtBearer:
se validan firma, emisor, audiencia, vigencia y roles. El token solo necesita un usuario
persistido cuando el escenario consulte ese usuario/cliente; `TestData.AddCustomerAsync`
prepara ese grafo para órdenes. Si el escenario prueba el login, insertar el usuario y
llamar `/api/auth/login` como hace `ApiSeedTests`.

`SeedAsync` guarda al finalizar y usa un scope nuevo. `ReadAsync` permite consultar
resultados desde otro contexto, por ejemplo:

```csharp
var count = await factory.ReadAsync(db =>
    Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(db.Products));
```

## Aislamiento y ciclo de vida

- Crear una fábrica por prueba o fila de `[Theory]`, y disponer cliente y fábrica con `using`.
- Cada fábrica mantiene abierta su propia conexión `Data Source=:memory:` con claves foráneas activas.
  Cerrar la fábrica elimina esa base. No hay base, fábrica ni claves JWT estáticas compartidas.
- `CreateApiClient()` inicia la API en `Testing`, crea el esquema con `EnsureCreated` y
  ejecuta los callbacks de seed reales (roles admin/user; administrador deshabilitado por defecto).
  Se reemplaza el proveedor SQL Server por SQLite y se conservan los callbacks de seed de la API.
- La conexión SQL ficticia solo satisface la validación inicial de configuración; nunca se abre.
  No se aplican migraciones de SQL Server sobre SQLite.
- No usar `IClassFixture<SqliteApiFactory>`, `ICollectionFixture`, una fábrica estática ni
  ejecutar peticiones/seed simultáneos en la misma fábrica. xUnit puede paralelizar clases
  porque cada caso usa su propia fábrica. No hace falta una colección compartida.
- Los callbacks de `SeedAsync` y `ReadAsync` deben terminar su trabajo dentro del scope;
  no devolver `IQueryable` ni depender de carga diferida después de que finalizan.

SQLite valida persistencia relacional y HTTP, pero no reproduce todos los tipos, consultas,
restricciones ni migraciones de SQL Server. `SqlServerModelStillMatchesExistingMigration`
conserva la comparación de modelo sin abrir una conexión SQL.

## Comprobaciones y CI

La comprobación unitaria usa Moq y FluentAssertions con un controlador real sin HTTP/base.
Las comprobaciones de integración verifican el entorno, proveedor, inserción/lectura,
autorización 401/403/200 y que dos fábricas no compartan datos ni claves de firma.
Los casos anteriores se conservan y se ejecutan junto con estas comprobaciones.

`Backend CI` compila con warnings como errores, ejecuta la suite completa dos veces y
publica los TRX de ambas pasadas como artifact `test-results`. Los resultados observados
de esta entrega se registran en el PR asociado a TP4-01.

Verificación local del 14/11/2026 con SDK 8.0.428, sin variables `ICS_TEST_*` ni servicios externos:

| Comprobación | Resultado |
| --- | --- |
| Restore y build Release con `--warnaserror` | 0 advertencias, 0 errores |
| Primera pasada | 48 unitarias + 85 integraciones aprobadas; 0 omitidas |
| Segunda pasada consecutiva | 48 unitarias + 85 integraciones aprobadas; 0 omitidas |
| Regresiones previas | Se conservaron los 130 casos; se agregaron 3 comprobaciones de infraestructura |
| `git diff --check` | Sin errores |
