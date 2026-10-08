using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dsw2025Tpi.Api;
using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Dsw2025Tpi.Seeding.Tests;

public class ApiSeedTests
{
    [Fact]
    public async Task EfInitializationAllowsAdminLoginAndAuthorizedProductCreation()
    {
        using var factory = new SeedApiFactory();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        Guid originalId;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<Dsw2025TpiContext>();
            if (factory.IsSqlServer)
            {
                // Migrations use the actual SQL Server schema and registered EF callbacks.
                context.Database.Migrate();
                await context.Database.MigrateAsync(); // Repetition; exercises async callback too.
                Assert.Single(await context.Database.GetAppliedMigrationsAsync());
            }
            else
            {
                using var command = context.Database.GetDbConnection().CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table'";
                Assert.Equal(0L, command.ExecuteScalar()); // API startup did not create or seed tables.
                context.Database.EnsureCreated(); // This factory owns a new in-memory database.
                await context.Database.EnsureCreatedAsync();
            }
            originalId = (await context.Users.SingleAsync()).Id;
            Assert.Equal(2, await context.Roles.CountAsync());
            Assert.Empty(context.Customers);
        }

        var login = await client.PostAsJsonAsync("/api/auth/login",
            new RequestLoginModel("seed-test-admin", DataSeederTests.Password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = await login.Content.ReadFromJsonAsync<ResponseLoginModel>();
        Assert.NotNull(session);
        Assert.Equal(originalId, session.Id);
        Assert.Equal("admin", session.Role);
        Assert.Equal(Guid.Empty, session.CustomerId);

        var product = new ProductModel.ProductRequest("SEED-SMOKE", "SEED-SMOKE",
            "Seed smoke product", "Synthetic integration test", 10, 1);
        var anonymous = await client.PostAsJsonAsync("/api/products", product);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);
        var created = await client.PostAsJsonAsync("/api/products", product);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var result = await created.Content.ReadFromJsonAsync<ProductModel.ProductResponse>();
        Assert.NotNull(result);
        using var verifyScope = factory.Services.CreateScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<Dsw2025TpiContext>();
        Assert.Equal(result.Id, (await verify.Products.SingleAsync()).Id);
        Assert.Single(verify.Users);
        Assert.Empty(verify.Customers);
    }

    private sealed class SeedApiFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:");
        public bool IsSqlServer => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ICS_TEST_SQL_CONNECTION"));

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            var jwtKey = Environment.GetEnvironmentVariable("ICS_TEST_JWT_KEY")
                ?? throw new InvalidOperationException("Set ICS_TEST_JWT_KEY to a synthetic signing key of at least 32 bytes.");
            builder.UseEnvironment("Development");
            builder.UseSetting("Jwt:Key", jwtKey);
            builder.UseSetting("Jwt:Issuer", "seed-tests");
            builder.UseSetting("Jwt:Audience", "seed-tests");
            builder.UseSetting("Seed:Admin:Enabled", "true");
            builder.UseSetting("Seed:Admin:Username", "seed-test-admin");
            builder.UseSetting("Seed:Admin:Email", "seed-test-admin@example.invalid");
            builder.UseSetting("Seed:Admin:Password", DataSeederTests.Password);
            // Suppress routine HTTP/database logs; credentials and JWTs are not test output.
            builder.UseSetting("Logging:LogLevel:Default", "Warning");
            if (IsSqlServer)
            {
                var sql = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ICS_TEST_SQL_CONNECTION"))
                {
                    // Always use a new database, never the database named by the caller.
                    InitialCatalog = "ICSSeedTests_" + Guid.NewGuid().ToString("N")
                };
                builder.UseSetting("ConnectionStrings:DefaultConnection", sql.ConnectionString);
            }
            else
            {
                connection.Open();
                builder.ConfigureServices(services =>
                {
                    // Keep the production callbacks and replace only the SQL Server provider.
                    using var provider = services.BuildServiceProvider();
                    using var scope = provider.CreateScope();
                    var productionOptions = scope.ServiceProvider.GetRequiredService<DbContextOptions<Dsw2025TpiContext>>();
                    var core = productionOptions.FindExtension<CoreOptionsExtension>()!;
                    Assert.NotNull(core.Seeder);
                    Assert.NotNull(core.AsyncSeeder);
                    services.RemoveAll<DbContextOptions<Dsw2025TpiContext>>();
                    services.RemoveAll<IDbContextOptionsConfiguration<Dsw2025TpiContext>>();
                    services.AddDbContext<Dsw2025TpiContext>(options => options.UseSqlite(connection)
                        .UseSeeding(core.Seeder).UseAsyncSeeding(core.AsyncSeeder));
                });
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) connection.Dispose();
        }
    }
}
