using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dsw2025Tpi.IntegrationTests;

public class ApiSeedTests
{
    [Fact]
    public async Task EfInitializationAllowsAdminLoginAndAuthorizedProductCreation()
    {
        using var factory = new SqliteApiFactory(seedAdmin: true, initializeDatabase: false);
        using var client = factory.CreateApiClient();
        Guid originalId;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<Dsw2025TpiContext>();
            using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table'";
            Assert.Equal(0L, command.ExecuteScalar()); // API startup did not create or seed tables.
            context.Database.EnsureCreated();
            await context.Database.EnsureCreatedAsync(); // Exercises both registered seeding callbacks.
            originalId = (await context.Users.SingleAsync()).Id;
            Assert.Equal(2, await context.Roles.CountAsync());
            Assert.Empty(context.Customers);
        }

        var emptyCatalog = await client.GetAsync("/api/products");
        Assert.Equal(HttpStatusCode.OK, emptyCatalog.StatusCode);
        var emptyPage = await emptyCatalog.Content.ReadFromJsonAsync<ProductModel.ResponsePagination>();
        Assert.NotNull(emptyPage);
        Assert.Empty(emptyPage.Items);
        Assert.Equal(0, emptyPage.Total);

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
        var noMatches = await client.GetAsync("/api/products?search=does-not-match-seed-smoke");
        Assert.Equal(HttpStatusCode.OK, noMatches.StatusCode);
        var noMatchesPage = await noMatches.Content.ReadFromJsonAsync<ProductModel.ResponsePagination>();
        Assert.NotNull(noMatchesPage);
        Assert.Empty(noMatchesPage.Items);
        Assert.Equal(0, noMatchesPage.Total);
        using var verifyScope = factory.Services.CreateScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<Dsw2025TpiContext>();
        Assert.Equal(result.Id, (await verify.Products.SingleAsync()).Id);
        Assert.Single(verify.Users);
        Assert.Empty(verify.Customers);
    }

}
