using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Application.Services;
using Dsw2025Tpi.Data;
using Dsw2025Tpi.Domain.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Dsw2025Tpi.Api.Tests;

// Exercises the real HTTP pipeline (routing, auth, model binding, middleware) with an in-memory database.
public class ProductErrorHandlingTests : IDisposable
{
    private readonly ProductsApiFactory factory = new();
    private readonly HttpClient client;

    public ProductErrorHandlingTests()
    {
        client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.AdminToken());
    }

    public void Dispose()
    {
        client.Dispose();
        factory.Dispose();
    }

    private static object Product(string sku = "SKU-001", string internalCode = "INT-001", string name = "Producto",
        string description = "Descripción", decimal price = 10, int stock = 5) => new
    {
        sku,
        internalCode,
        name,
        description,
        currentUnitPrice = price,
        stockQuantity = stock
    };

    private async Task<ProductModel.ProductResponse> Create(object product)
    {
        var response = await client.PostAsJsonAsync("/api/products", product);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProductModel.ProductResponse>())!;
    }

    private static async Task<string> AssertError(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)expected, body.RootElement.GetProperty("statusCode").GetInt32());
        var message = body.RootElement.GetProperty("message").GetString();
        Assert.False(string.IsNullOrWhiteSpace(message));
        return message!;
    }

    private async Task<Product?> Stored(Guid id)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<Dsw2025TpiContext>().Products
            .AsNoTracking().SingleOrDefaultAsync(p => p.Id == id);
    }

    private async Task<int> StoredCount()
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<Dsw2025TpiContext>().Products.CountAsync();
    }

    [Fact]
    public async Task CreatingDuplicateSkuReturnsConflict()
    {
        await Create(Product(sku: "DUP-1"));

        var message = await AssertError(await client.PostAsJsonAsync("/api/products", Product(sku: "DUP-1", name: "Otro")),
            HttpStatusCode.Conflict);

        Assert.Contains("DUP-1", message);
        Assert.Equal(1, await StoredCount());
    }

    [Fact]
    public async Task UpdatingWithAnotherProductsSkuReturnsConflictAndKeepsProduct()
    {
        await Create(Product(sku: "FIRST"));
        var second = await Create(Product(sku: "SECOND"));

        var message = await AssertError(
            await client.PutAsJsonAsync($"/api/products/{second.Id}", Product(sku: "FIRST")), HttpStatusCode.Conflict);

        Assert.Contains("FIRST", message);
        Assert.Equal("SECOND", (await Stored(second.Id))!.Sku);
    }

    public static IEnumerable<object[]> InvalidProducts() => new[]
    {
        new object[] { Product(price: 0), "precio" },
        new object[] { Product(price: -1), "precio" },
        new object[] { Product(stock: -1), "stock" },
        new object[] { Product(name: ""), "nombre" },
        new object[] { Product(name: "   "), "nombre" },
        new object[] { Product(name: new string('N', 51)), "nombre" },
        new object[] { Product(sku: ""), "SKU" },
        new object[] { Product(sku: new string('S', 21)), "SKU" },
        new object[] { Product(internalCode: new string('I', 31)), "código interno" },
        new object[] { Product(description: new string('D', 251)), "descripción" },
    };

    [Theory]
    [MemberData(nameof(InvalidProducts))]
    public async Task CreatingInvalidProductReturnsBadRequestWithoutSaving(object product, string field)
    {
        var message = await AssertError(await client.PostAsJsonAsync("/api/products", product), HttpStatusCode.BadRequest);

        Assert.Contains(field, message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await StoredCount());
    }

    [Theory]
    [MemberData(nameof(InvalidProducts))]
    public async Task UpdatingWithInvalidDataReturnsBadRequestAndKeepsProduct(object product, string field)
    {
        var existing = await Create(Product(sku: "KEEP"));

        var message = await AssertError(
            await client.PutAsJsonAsync($"/api/products/{existing.Id}", product), HttpStatusCode.BadRequest);

        Assert.Contains(field, message, StringComparison.OrdinalIgnoreCase);
        var stored = (await Stored(existing.Id))!;
        Assert.Equal("KEEP", stored.Sku);
        Assert.Equal(10, stored.CurrentUnitPrice);
        Assert.Equal(5, stored.StockQuantity);
    }

    [Fact]
    public async Task SeveralInvalidFieldsAreReportedTogether()
    {
        var response = await client.PostAsJsonAsync("/api/products", Product(name: "", price: 0, stock: -1));

        await AssertError(response, HttpStatusCode.BadRequest);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(3, body.RootElement.GetProperty("errors").GetArrayLength());
    }

    [Fact]
    public async Task ValuesAtTheLimitsAreAccepted()
    {
        var created = await Create(Product(sku: new string('S', 20), internalCode: new string('I', 30),
            name: new string('N', 50), description: new string('D', 250), price: 0.01m, stock: 0));

        Assert.Equal(0, created.StockQuantity);
        Assert.NotNull(await Stored(created.Id));
    }

    [Fact]
    public async Task OptionalFieldsCanBeEmpty()
    {
        var created = await Create(Product(sku: "OPTIONAL", internalCode: "", description: ""));

        Assert.Equal("", created.InternalCode);
        Assert.Equal("", created.Description);

    }

    [Fact]
    public async Task OmittedFieldsAreRejectedByModelBindingWithoutSaving()
    {
        // [ApiController] model validation answers before the service runs (ASP.NET ProblemDetails format).
        var response = await client.PostAsJsonAsync("/api/products",
            new { sku = "OMITTED", name = "Producto", currentUnitPrice = 1, stockQuantity = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await StoredCount());
    }

    [Fact]
    public async Task GettingUnknownProductReturnsNotFound()
    {
        var id = Guid.NewGuid();

        var message = await AssertError(await client.GetAsync($"/api/products/{id}"), HttpStatusCode.NotFound);

        Assert.Contains(id.ToString(), message);
    }

    [Fact]
    public async Task UpdatingOrTogglingUnknownProductReturnsNotFound()
    {
        await AssertError(await client.PutAsJsonAsync($"/api/products/{Guid.NewGuid()}", Product()),
            HttpStatusCode.NotFound);
        await AssertError(await client.PatchAsync($"/api/products/{Guid.NewGuid()}", null), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ValidOperationsKeepWorking()
    {
        var created = await Create(Product(sku: "VALID"));
        Assert.Equal("VALID", created.Sku);
        Assert.True(created.IsActive);

        var fetched = await client.GetFromJsonAsync<ProductModel.ProductResponse>($"/api/products/{created.Id}");
        Assert.Equal(created, fetched);

        // Keeping the same SKU is not a duplicate.
        var sameSku = await client.PutAsJsonAsync($"/api/products/{created.Id}", Product(sku: "VALID", price: 20, stock: 0));
        Assert.Equal(HttpStatusCode.OK, sameSku.StatusCode);
        Assert.Equal(20, (await sameSku.Content.ReadFromJsonAsync<ProductModel.ProductResponse>())!.CurrentUnitPrice);

        var newSku = await client.PutAsJsonAsync($"/api/products/{created.Id}", Product(sku: "VALID-2"));
        Assert.Equal(HttpStatusCode.OK, newSku.StatusCode);
        Assert.Equal("VALID-2", (await Stored(created.Id))!.Sku);

        var toggled = await client.PatchAsync($"/api/products/{created.Id}", null);
        Assert.Equal(HttpStatusCode.OK, toggled.StatusCode);
        Assert.False((await toggled.Content.ReadFromJsonAsync<ProductModel.ProductResponse>())!.IsActive);

        var list = await client.GetFromJsonAsync<ProductModel.ResponsePagination>("/api/products");
        Assert.Equal(1, list!.Total);
    }

    private sealed class ProductsApiFactory : WebApplicationFactory<Program>
    {
        private readonly string databaseName = Guid.NewGuid().ToString();
        private readonly Dictionary<string, string?> settings = new()
        {
            // Never opened: the provider is replaced below. It only has to pass startup validation.
            ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=ProductErrorTests;Integrated Security=True",
            ["Jwt:Key"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            ["Jwt:Issuer"] = "product-tests",
            ["Jwt:Audience"] = "product-tests",
            ["Jwt:ExpireInMinutes"] = "60",
            ["Logging:LogLevel:Default"] = "None"
        };

        public string AdminToken() => new JwtTokenServices(
            new ConfigurationBuilder().AddInMemoryCollection(settings).Build()).GenerateToken("product-admin", Role.Admin);

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            foreach (var (key, value) in settings) builder.UseSetting(key, value);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<Dsw2025TpiContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<Dsw2025TpiContext>>();
                services.AddDbContext<Dsw2025TpiContext>(options => options.UseInMemoryDatabase(databaseName));
            });
        }
    }
}
