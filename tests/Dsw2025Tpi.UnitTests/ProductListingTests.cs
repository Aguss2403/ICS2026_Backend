using System.Linq.Expressions;
using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Application.Services;
using Dsw2025Tpi.Domain.Entities;
using Dsw2025Tpi.Domain.Interfaces;
using Moq;
using Xunit;

namespace Dsw2025Tpi.UnitTests;

public class ProductListingTests
{
    [Fact]
    public async Task GetProducts_ExcludesInactiveProductsByDefault()
    {
        var service = CreateService(
            new Product { Sku = "ACTIVO", IsActive = true },
            new Product { Sku = "INACTIVO", IsActive = false });

        var page = Assert.IsType<ProductModel.ResponsePagination>(await service.GetProducts());

        Assert.Equal("ACTIVO", Assert.Single(page.Items).Sku);
        Assert.Equal(1, page.Total);
    }

    [Fact]
    public async Task GetProducts_PageTwoReturnsFiveProductsInSkuOrderAndFullTotal()
    {
        var products = Enumerable.Range(1, 15).Reverse()
            .Select(i => new Product { Sku = $"SKU-{i:D2}", IsActive = true }).ToArray();
        var service = CreateService(products);
        var filter = new ProductModel.FilterProduct(null, null, 2, 5);

        var page = Assert.IsType<ProductModel.ResponsePagination>(await service.GetProducts(filter));

        Assert.Equal(new[] { "SKU-06", "SKU-07", "SKU-08", "SKU-09", "SKU-10" },
            page.Items.Select(product => product.Sku));
        Assert.Equal(15, page.Total);
    }

    [Theory]
    [InlineData("ÓPTIC", "P01")]
    [InlineData("aBc", "ABC-02")]
    [InlineData("xYz", "P03")]
    public async Task GetProducts_SearchMatchesNameSkuOrInternalCodeIgnoringCase(string search, string expectedSku)
    {
        var service = CreateService(
            new Product { Sku = "P01", Name = "Mouse óptico", IsActive = true },
            new Product { Sku = "ABC-02", Name = "Teclado", IsActive = true },
            new Product { Sku = "P03", Name = "Monitor", InternalCode = "INT-XYZ-03", IsActive = true });
        var filter = new ProductModel.FilterProduct(null, search, null, null);

        var page = Assert.IsType<ProductModel.ResponsePagination>(await service.GetProducts(filter));

        Assert.Equal(expectedSku, Assert.Single(page.Items).Sku);
        Assert.Equal(1, page.Total);
    }

    private static ProductsManagementService CreateService(params Product[] products)
    {
        var repository = new Mock<IRepository>(MockBehavior.Strict);
        repository.Setup(repo => repo.GetFiltered<Product>(
                It.IsAny<Expression<Func<Product, bool>>>(), It.IsAny<string[]>()))
            .ReturnsAsync((Expression<Func<Product, bool>> filter, string[] _) =>
                products.Where(filter.Compile()).ToList());

        return new ProductsManagementService(repository.Object);
    }
}
