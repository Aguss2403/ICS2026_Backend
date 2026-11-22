using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Domain.Entities;
using Xunit;

namespace Dsw2025Tpi.IntegrationTests;

public class ProductListingTests
{
    [Fact]
    public async Task PublicListingReturnsPaginatedJsonWithOrWithoutResults()
    {
        await using var factory = new SqliteApiFactory();
        using var client = factory.CreateApiClient();
        var product = TestData.Product(sku: "PUBLICO-01");
        await SeedProductsAsync(factory, product);

        using var response = await client.GetAsync("/api/products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, page.GetProperty("total").GetInt32());
        var item = Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Equal(product.Id, item.GetProperty("id").GetGuid());
        Assert.Equal(product.Sku, item.GetProperty("sku").GetString());

        using var emptyResponse = await client.GetAsync("/api/products?search=sin-coincidencias");

        Assert.Equal(HttpStatusCode.OK, emptyResponse.StatusCode);
        var emptyPage = await ReadPageAsync(emptyResponse);
        Assert.Empty(emptyPage.Items);
        Assert.Equal(0, emptyPage.Total);
    }

    [Fact]
    public async Task PublicListingCombinesSearchAndPaginationOnSqliteData()
    {
        await using var factory = new SqliteApiFactory();
        using var client = factory.CreateApiClient();
        await SeedProductsAsync(factory,
            new Product("ZZ-02", "CÓDIGO-2", "Monitor", "", 10m, 5),
            new Product("AA-01", "PERIF-1", "Mouse óptico", "", 10m, 5),
            new Product("AA-03", "PERIF-3", "Teclado", "", 10m, 5),
            new Product("ÓPTICO-04", "PERIF-4", "Cable", "", 10m, 5),
            new Product("BB-05", "ÓPTICO-5", "Lector", "", 10m, 5));

        using var response = await client.GetAsync($"/api/products?search={Uri.EscapeDataString("ÓPTIC")}&pageNumber=2&pageSize=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await ReadPageAsync(response);
        Assert.Equal("BB-05", Assert.Single(page.Items).Sku);
        Assert.Equal(3, page.Total);
    }

    [Fact]
    public async Task AdminListingIncludesInactiveProductsAndPublicListingExcludesThem()
    {
        await using var factory = new SqliteApiFactory();
        using var anonymous = factory.CreateApiClient();
        using var admin = factory.CreateApiClient();
        factory.Authenticate(admin);
        var active = TestData.Product(sku: "ACTIVO");
        var inactive = TestData.Product(sku: "INACTIVO");
        inactive.IsActive = false;
        await SeedProductsAsync(factory, active, inactive);

        using var publicResponse = await anonymous.GetAsync("/api/products");
        using var publicDisabled = await anonymous.GetAsync("/api/products?status=disabled");
        using var adminResponse = await admin.GetAsync("/api/products/admin");
        using var adminDisabled = await admin.GetAsync("/api/products/admin?status=disabled");

        Assert.Equal(HttpStatusCode.OK, publicResponse.StatusCode);
        var publicPage = await ReadPageAsync(publicResponse);
        Assert.Equal(active.Id, Assert.Single(publicPage.Items).Id);
        Assert.Equal(1, publicPage.Total);
        Assert.Equal(HttpStatusCode.OK, publicDisabled.StatusCode);
        Assert.Empty((await ReadPageAsync(publicDisabled)).Items);

        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
        var adminPage = await ReadPageAsync(adminResponse);
        Assert.Equal(2, adminPage.Total);
        Assert.Contains(adminPage.Items, product => product.Id == inactive.Id && !product.IsActive);
        Assert.Equal(HttpStatusCode.OK, adminDisabled.StatusCode);
        Assert.Equal(inactive.Id, Assert.Single((await ReadPageAsync(adminDisabled)).Items).Id);
    }

    private static Task SeedProductsAsync(SqliteApiFactory factory, params Product[] products) =>
        factory.SeedAsync(db =>
        {
            db.Products.AddRange(products);
            return Task.CompletedTask;
        });

    private static async Task<ProductModel.ResponsePagination> ReadPageAsync(HttpResponseMessage response) =>
        Assert.IsType<ProductModel.ResponsePagination>(
            await response.Content.ReadFromJsonAsync<ProductModel.ResponsePagination>());
}
