using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Dsw2025Tpi.IntegrationTests;

public class InfrastructureSmokeTests
{
    [Fact]
    public async Task TestingHostReadsSeededDataThroughRealJwtAuthorization()
    {
        using var factory = new SqliteApiFactory();
        using var client = factory.CreateApiClient();
        factory.Services.GetRequiredService<IHostEnvironment>().EnvironmentName.Should().Be("Testing");
        (await factory.ReadAsync(db => Task.FromResult(db.Database.ProviderName)))
            .Should().Be("Microsoft.EntityFrameworkCore.Sqlite");
        var product = TestData.Product();
        Customer customer = null!;
        await factory.SeedAsync(async db =>
        {
            db.Products.Add(product);
            customer = await TestData.AddCustomerAsync(db);
        });
        (await factory.ReadAsync(db => db.Customers.CountAsync())).Should().Be(1);

        using var anonymous = await client.GetAsync($"/api/products/{product.Id}");
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        factory.Authenticate(client, Role.User, customer.User!.Username);
        using var authorized = await client.GetAsync($"/api/products/{product.Id}");
        authorized.StatusCode.Should().Be(HttpStatusCode.OK);
        (await authorized.Content.ReadFromJsonAsync<ProductModel.ProductResponse>())!.Id.Should().Be(product.Id);
        using var forbidden = await client.GetAsync("/api/products/admin");
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        factory.Authenticate(client);
        using var admin = await client.GetAsync("/api/products/admin");
        admin.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task FactoriesDoNotShareDataOrSigningKeys()
    {
        using var first = new SqliteApiFactory();
        using var second = new SqliteApiFactory();
        using var firstClient = first.CreateApiClient();
        using var secondClient = second.CreateApiClient();
        await first.SeedAsync(db =>
        {
            db.Products.Add(TestData.Product());
            return Task.CompletedTask;
        });
        (await first.ReadAsync(db => db.Products.CountAsync())).Should().Be(1);
        (await second.ReadAsync(db => db.Products.CountAsync())).Should().Be(0);
        secondClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", first.CreateToken());
        using var wrongSignature = await secondClient.GetAsync("/api/products/admin");
        wrongSignature.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        secondClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token");
        using var malformed = await secondClient.GetAsync("/api/products/admin");
        malformed.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        second.Authenticate(secondClient);
        using var valid = await secondClient.GetAsync("/api/products/admin");
        valid.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
