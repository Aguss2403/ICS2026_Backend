using System.Linq.Expressions;
using Dsw2025Tpi.Application.Exceptions;
using Dsw2025Tpi.Application.Services;
using Dsw2025Tpi.Domain.Entities;
using Dsw2025Tpi.Domain.Interfaces;
using Moq;
using Xunit;
using static Dsw2025Tpi.Application.Dtos.ProductModel;

namespace Dsw2025Tpi.UnitTests;

// TP4-02: unit scenarios for creating products. The repository is mocked, so only the service rules are exercised.
public class ProductsManagementServiceAddProductTests
{
    private readonly Mock<IRepository> repository = new(MockBehavior.Strict);
    private readonly ProductsManagementService service;

    public ProductsManagementServiceAddProductTests()
    {
        service = new ProductsManagementService(repository.Object);
    }

    private static ProductRequest Request(string sku = "SKU-001", decimal price = 150.50m, int stock = 8) =>
        new(sku, "INT-001", "Teclado", "Teclado mecánico", price, stock);

    private void SetupSkuLookup(Product? existing) =>
        repository
            .Setup(r => r.First(It.IsAny<Expression<Func<Product, bool>>>(), It.IsAny<string[]>()))
            .ReturnsAsync(existing);

    [Fact]
    public async Task AddProduct_WithValidDataAndUniqueSku_ReturnsCreatedValuesAndAddsOnce()
    {
        var request = Request();
        Expression<Func<Product, bool>>? skuPredicate = null;
        repository
            .Setup(r => r.First(It.IsAny<Expression<Func<Product, bool>>>(), It.IsAny<string[]>()))
            .Callback<Expression<Func<Product, bool>>, string[]>((predicate, _) => skuPredicate = predicate)
            .ReturnsAsync((Product?)null);
        Product? added = null;
        repository
            .Setup(r => r.Add(It.IsAny<Product>()))
            .Callback<Product>(p => added = p)
            .ReturnsAsync((Product p) => p);

        var response = await service.AddProduct(request);

        Assert.NotEqual(Guid.Empty, response.Id);
        Assert.Equal(request.Sku, response.Sku);
        Assert.Equal(request.InternalCode, response.InternalCode);
        Assert.Equal(request.Name, response.Name);
        Assert.Equal(request.Description, response.Description);
        Assert.Equal(request.CurrentUnitPrice, response.CurrentUnitPrice);
        Assert.Equal(request.StockQuantity, response.StockQuantity);
        Assert.True(response.IsActive);

        // The uniqueness check must look up the requested SKU, and the persisted entity must be the one returned.
        var matchesSku = skuPredicate!.Compile();
        Assert.True(matchesSku(new Product { Sku = request.Sku }));
        Assert.False(matchesSku(new Product { Sku = "OTHER" }));
        repository.Verify(r => r.Add(It.IsAny<Product>()), Times.Once);
        Assert.Equal(response.Id, added!.Id);
    }

    [Fact]
    public async Task AddProduct_WithExistingSku_ThrowsDuplicatedEntityAndDoesNotAdd()
    {
        SetupSkuLookup(new Product("SKU-001", "OLD", "Existente", "", 10m, 3));

        var exception = await Assert.ThrowsAsync<DuplicatedEntityException>(() => service.AddProduct(Request(sku: "SKU-001")));

        Assert.Contains("SKU-001", exception.Message);
        repository.Verify(r => r.Add(It.IsAny<Product>()), Times.Never);
    }

    [Theory]
    [InlineData(150.50, 0, "stock")]
    [InlineData(150.50, -1, "stock")]
    [InlineData(-1, 8, "precio")]
    public async Task AddProduct_WithInvalidStockOrPrice_ThrowsValidationBeforeTouchingRepository(
        decimal price, int stock, string field)
    {
        var exception = await Assert.ThrowsAsync<EntityValidationException>(
            () => service.AddProduct(Request(price: price, stock: stock)));

        var error = Assert.Single(exception.Errors);
        Assert.Contains(field, error, StringComparison.OrdinalIgnoreCase);
        // Strict mock + no setups: any First/Add call would have failed the test; this makes the intent explicit.
        repository.VerifyNoOtherCalls();
    }
}
