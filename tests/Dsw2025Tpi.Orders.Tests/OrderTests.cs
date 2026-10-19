using Dsw2025Tpi.Domain.Entities;
using Xunit;

namespace Dsw2025Tpi.Orders.Tests;

public class OrderTests
{
    [Fact]
    public void ParameterlessConstructorInitializesStringsAndCollection()
    {
        var order = new Order();

        Assert.Equal(string.Empty, order.ShippingAddress);
        Assert.Equal(string.Empty, order.BillingAddress);
        Assert.Equal(string.Empty, order.Notes);
        Assert.Empty(order.OrderItems);
        Assert.Equal(0m, order.CalculateTotalAmount());
        Assert.Throws<InvalidOperationException>(() => order.Customer);

        var customer = new Customer();
        order.Customer = customer;
        Assert.Same(customer, order.Customer);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("Leave at reception", "Leave at reception")]
    [InlineData("   ", "   ")]
    public void ConstructorNormalizesOnlyMissingNotes(string? notes, string expected)
    {
        var order = new Order("Shipping", "Billing", notes, new(), Guid.NewGuid());

        Assert.Equal(expected, order.Notes);
    }

    [Fact]
    public void ConstructorPreservesOrderValuesAndSumsStoredSubtotals()
    {
        var customerId = Guid.NewGuid();
        var items = new List<OrderItem>
        {
            new() { Quantity = 3, UnitPrice = 12.35m, SubTotal = 37.05m },
            new() { Quantity = 2, UnitPrice = 7.10m, SubTotal = 14.20m }
        };
        var before = DateTime.UtcNow;

        var order = new Order("Shipping", "Billing", "Notes", items, customerId);

        Assert.Equal("Shipping", order.ShippingAddress);
        Assert.Equal("Billing", order.BillingAddress);
        Assert.Equal(customerId, order.CustomerId);
        Assert.Same(items, order.OrderItems);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.InRange(order.OrderDate, before, DateTime.UtcNow);
        Assert.Equal(DateTimeKind.Utc, order.OrderDate.Kind);
        Assert.Equal(51.25m, order.TotalAmount);
        Assert.Equal(51.25m, order.CalculateTotalAmount());
    }

    [Fact]
    public void TotalUsesStoredSubtotalsAndDoesNotImplicitlyRecalculateThem()
    {
        var item = new OrderItem { Quantity = 3, UnitPrice = 12.35m, SubTotal = 37.05m };
        var order = new Order("Shipping", "Billing", null, new() { item }, Guid.NewGuid());
        item.UnitPrice = 20m;

        Assert.Equal(37.05m, order.CalculateTotalAmount());
        item.SubTotal = item.CalculateSubTotal();
        Assert.Equal(60m, order.CalculateTotalAmount());
        Assert.Equal(37.05m, order.TotalAmount);
    }

    [Fact]
    public void EmptyItemsProduceZeroTotal()
    {
        var order = new Order("Shipping", "Billing", null, new(), Guid.NewGuid());

        Assert.Empty(order.OrderItems);
        Assert.Equal(0m, order.TotalAmount);
        Assert.Equal(0m, order.CalculateTotalAmount());
    }
}
