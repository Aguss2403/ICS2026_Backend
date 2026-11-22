using System.Net;
using System.Net.Http.Json;
using Dsw2025Tpi.Application.Dtos;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Dsw2025Tpi.IntegrationTests;

// TP4-02: HTTP scenarios for POST /api/products. Each test owns its factory and therefore its private SQLite database.
// The duplicate-SKU scenario lives in ProductErrorHandlingTests.CreatingDuplicateSkuReturnsConflictAndKeepsSingleRow.
public class ProductCreationTests
{
    private static object Product(string sku = "TP4-ALTA-1") => new
    {
        sku,
        internalCode = "INT-ALTA",
        name = "Monitor 24",
        description = "Monitor IPS de 24 pulgadas",
        currentUnitPrice = 189999.99m,
        stockQuantity = 7
    };

    [Fact]
    public async Task CreatingProductWithoutTokenReturnsUnauthorizedAndSavesNothing()
    {
        await using var factory = new SqliteApiFactory();
        using var client = factory.CreateApiClient();

        using var response = await client.PostAsJsonAsync("/api/products", Product());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, await factory.ReadAsync(db => db.Products.CountAsync()));
    }

    [Fact]
    public async Task CreatingValidProductAsAdminReturnsCreatedAndPersistsSentData()
    {
        await using var factory = new SqliteApiFactory();
        using var client = factory.CreateApiClient();
        factory.Authenticate(client);

        using var response = await client.PostAsJsonAsync("/api/products", Product());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<ProductModel.ProductResponse>())!;
        var stored = await factory.ReadAsync(db => db.Products.AsNoTracking().SingleAsync());
        Assert.Equal(created.Id, stored.Id);
        Assert.Equal("TP4-ALTA-1", stored.Sku);
        Assert.Equal("INT-ALTA", stored.InternalCode);
        Assert.Equal("Monitor 24", stored.Name);
        Assert.Equal("Monitor IPS de 24 pulgadas", stored.Description);
        Assert.Equal(189999.99m, stored.CurrentUnitPrice);
        Assert.Equal(7, stored.StockQuantity);
        Assert.True(stored.IsActive);
    }
}
