using System.Net.Http.Headers;
using Dsw2025Tpi.Api;
using Dsw2025Tpi.Application.Services;
using Dsw2025Tpi.Data;
using Dsw2025Tpi.Domain.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Dsw2025Tpi.IntegrationTests;

// Create one factory per test (including each Theory row), never a static/shared fixture.
// The open connection owns a private database until the factory is disposed.
public class SqliteApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection connection = new("Data Source=:memory:;Foreign Keys=True");
    private readonly bool seedAdmin;
    private readonly bool initializeDatabase;
    private readonly string signingKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

    public SqliteApiFactory(bool seedAdmin = false, bool initializeDatabase = true)
    {
        this.seedAdmin = seedAdmin;
        this.initializeDatabase = initializeDatabase;
        connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // This value only satisfies the API's SQL connection-string validation. It is never opened.
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=unused.invalid;Database=TP4;Integrated Security=True");
        builder.UseSetting("Jwt:Key", signingKey);
        builder.UseSetting("Jwt:Issuer", "tp4-tests");
        builder.UseSetting("Jwt:Audience", "tp4-tests");
        builder.UseSetting("Jwt:ExpireInMinutes", "60");
        builder.UseSetting("Seed:Admin:Enabled", seedAdmin.ToString());
        builder.UseSetting("Seed:Admin:Username", "seed-test-admin");
        builder.UseSetting("Seed:Admin:Email", "seed-test-admin@example.invalid");
        builder.UseSetting("Seed:Admin:Password", TestCredentials.Password);
        builder.UseSetting("Logging:LogLevel:Default", "Warning");

        builder.ConfigureServices(services =>
        {
            // Preserve the actual application's EF seeding callbacks, as the original seed
            // factory did. Resolving options builds no SQL connection and performs no I/O.
            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var productionOptions = scope.ServiceProvider.GetRequiredService<DbContextOptions<Dsw2025TpiContext>>();
            var core = productionOptions.FindExtension<CoreOptionsExtension>()
                ?? throw new InvalidOperationException("The API must register EF Core options.");
            var seeder = core.Seeder ?? throw new InvalidOperationException("The API must register its synchronous seeder.");
            var asyncSeeder = core.AsyncSeeder ?? throw new InvalidOperationException("The API must register its asynchronous seeder.");

            services.RemoveAll<Dsw2025TpiContext>();
            services.RemoveAll<DbContextOptions<Dsw2025TpiContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<Dsw2025TpiContext>>();
            services.AddDbContext<Dsw2025TpiContext>(options => options.UseSqlite(connection)
                .UseSeeding(seeder).UseAsyncSeeding(asyncSeeder));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        if (initializeDatabase)
        {
            using var scope = host.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<Dsw2025TpiContext>().Database.EnsureCreated();
        }
        return host;
    }

    public HttpClient CreateApiClient() => CreateClient(new()
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false
    });

    // Uses the real token issuer and the same configuration as JwtBearer in the API.
    // No fake authentication handler or authorization bypass is installed.
    public string CreateToken(string role = Role.Admin, string username = "tp4-admin")
    {
        using var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<JwtTokenServices>().GenerateToken(username, role);
    }

    public void Authenticate(HttpClient client, string role = Role.Admin, string username = "tp4-admin") =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(role, username));

    // The callback and SaveChanges use a fresh scope; no tracked context leaks into requests.
    // Do not run seed/read operations concurrently with requests on this factory's connection.
    public async Task SeedAsync(Func<Dsw2025TpiContext, Task> seed)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Dsw2025TpiContext>();
        await seed(context);
        await context.SaveChangesAsync();
    }

    public async Task<T> ReadAsync<T>(Func<Dsw2025TpiContext, Task<T>> read)
    {
        using var scope = Services.CreateScope();
        return await read(scope.ServiceProvider.GetRequiredService<Dsw2025TpiContext>());
    }

    protected override void Dispose(bool disposing)
    {
        try { base.Dispose(disposing); }
        finally { if (disposing) connection.Dispose(); }
    }

    public override async ValueTask DisposeAsync()
    {
        try { await base.DisposeAsync(); }
        finally { await connection.DisposeAsync(); }
    }
}
