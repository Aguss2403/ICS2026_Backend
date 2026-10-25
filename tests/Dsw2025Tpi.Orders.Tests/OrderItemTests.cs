using Dsw2025Tpi.Domain.Entities;
using Xunit;

namespace Dsw2025Tpi.Orders.Tests;

public class OrderItemTests
{
    [Fact]
    public void ConstructorAssignsSuppliedOrderBeforeReadingItsId()
    {
        var product = ProductWithStock();
        var order = new Order();

        // Regression: the original constructor throws NullReferenceException here.
        var item = new OrderItem(3, product, order);

        Assert.Same(order, item.Order);
        Assert.Equal(order.Id, item.OrderId);
        Assert.NotEqual(Guid.Empty, item.OrderId);
        Assert.Same(product, item.Product);
        Assert.Equal(product.Id, item.ProductId);
        Assert.NotEqual(Guid.Empty, item.ProductId);
        Assert.Equal(3, item.Quantity);
        Assert.Equal(12.35m, item.UnitPrice);
        Assert.Equal(37.05m, item.SubTotal);
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(1, "12.35")]
    [InlineData(3, "37.05")]
    public void SubtotalUsesQuantityAndDecimalUnitPrice(int quantity, string expected)
    {
        var item = new OrderItem(quantity, ProductWithStock(), new Order());

        var amount = decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(amount, item.SubTotal);
        Assert.Equal(amount, item.CalculateSubTotal());
    }

    [Fact]
    public void ConstructorKeepsPriceSnapshotAndDoesNotDecreaseStock()
    {
        var product = ProductWithStock();
        var item = new OrderItem(2, product, new Order());

        product.CurrentUnitPrice = 99m;

        Assert.Equal(12.35m, item.UnitPrice);
        Assert.Equal(24.70m, item.CalculateSubTotal());
        Assert.Equal(10, product.StockQuantity);
    }

    [Fact]
    public void InsufficientStockStillRejectsTheItem()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            new OrderItem(11, ProductWithStock(), new Order()));

        Assert.Equal("Stock insuficiente para el producto Test product", error.Message);
    }

    [Fact]
    public void ParameterlessConstructorAllowsNavigationAssignment()
    {
        var item = new OrderItem();
        var product = ProductWithStock();
        var order = new Order();

        Assert.Throws<InvalidOperationException>(() => item.Product);
        Assert.Throws<InvalidOperationException>(() => item.Order);

        item.Product = product;
        item.Order = order;

        Assert.Same(product, item.Product);
        Assert.Same(order, item.Order);
    }

    internal static Product ProductWithStock() =>
        new("TEST-SKU", "TEST-CODE", "Test product", "Test description", 12.35m, 10);
}
