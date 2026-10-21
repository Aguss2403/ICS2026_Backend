using Dsw2025Tpi.Data;
using Dsw2025Tpi.Domain.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Dsw2025Tpi.Orders.Tests;

public class OrderPersistenceTests
{
    [Fact]
    public void SqlServerModelStillMatchesExistingMigration()
    {
        var options = new DbContextOptionsBuilder<Dsw2025TpiContext>()
            .UseSqlServer("Server=localhost;Database=ModelOnly;Integrated Security=True")
            .Options;
        using var context = new Dsw2025TpiContext(options);

        // Builds and compares models without connecting to SQL Server.
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task EfFixupSupportsExistingInitializerFlowAndPersistsTotal()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<Dsw2025TpiContext>()
            .UseSqlite(connection).Options;
        using var context = new Dsw2025TpiContext(options);
        await context.Database.EnsureCreatedAsync();
        var role = new Role { Name = Role.User };
        var user = new User("initializer-user", "initializer@example.invalid", "synthetic-test-value", role);
        var customer = new Customer(user, "Test", "Customer", "Shipping", "123456");
        context.Customers.Add(customer);
        var product = OrderItemTests.ProductWithStock();
        var item = new OrderItem
        {
            Product = product,
            ProductId = product.Id,
            Quantity = 3,
            UnitPrice = product.CurrentUnitPrice
        };
        item.SubTotal = item.CalculateSubTotal();
        var order = new Order("Shipping", "Billing", null, new() { item }, customer.Id);

        context.Orders.Add(order);

        Assert.Same(customer, order.Customer);
        Assert.Same(order, item.Order);
        Assert.Equal(order.Id, item.OrderId);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var loaded = await context.Orders.Include(o => o.OrderItems).SingleAsync();
        Assert.Equal(37.05m, loaded.TotalAmount);
        Assert.Equal(loaded.TotalAmount, loaded.CalculateTotalAmount());
        Assert.Equal(order.Id, Assert.Single(loaded.OrderItems).OrderId);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("Leave at reception", "Leave at reception")]
    public async Task EfPersistsAndReloadsOrderGraphWithRequiredForeignKeys(string? notes, string expectedNotes)
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<Dsw2025TpiContext>()
            .UseSqlite(connection).Options;
        using var context = new Dsw2025TpiContext(options);
        await context.Database.EnsureCreatedAsync();
        var role = new Role { Name = Role.User };
        var user = new User("order-test-user", "order-test@example.invalid", "synthetic-test-value", role);
        var customer = new Customer(user, "Test", "Customer", "Shipping", "123456");
        var items = new List<OrderItem>();
        var order = new Order("Shipping", "Billing", notes, items, customer.Id) { Customer = customer };
        var product = OrderItemTests.ProductWithStock();
        var item = new OrderItem(3, product, order);
        items.Add(item);
        context.Orders.Add(order);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var loaded = await context.Orders
            .Include(o => o.Customer)
            .Include(o => o.OrderItems).ThenInclude(i => i.Product)
            .SingleAsync();

        Assert.Equal(order.Id, loaded.Id);
        Assert.Equal(customer.Id, loaded.CustomerId);
        Assert.Equal(customer.Id, loaded.Customer.Id);
        Assert.Equal("Shipping", loaded.ShippingAddress);
        Assert.Equal("Billing", loaded.BillingAddress);
        Assert.Equal(expectedNotes, loaded.Notes);
        var loadedItem = Assert.Single(loaded.OrderItems);
        Assert.Same(loaded, loadedItem.Order);
        Assert.Equal(loaded.Id, loadedItem.OrderId);
        Assert.Equal(product.Id, loadedItem.ProductId);
        Assert.Equal(product.Id, loadedItem.Product.Id);
        Assert.Equal(12.35m, loadedItem.UnitPrice);
        Assert.Equal(37.05m, loadedItem.SubTotal);
        Assert.Equal(37.05m, loaded.CalculateTotalAmount());
        // TotalAmount remains the constructor snapshot: adding items does not update it.
        Assert.Equal(order.TotalAmount, loaded.TotalAmount);

        context.ChangeTracker.Clear();
        var unloadedOrder = await context.Orders.SingleAsync();
        Assert.Equal(customer.Id, unloadedOrder.CustomerId);
        Assert.Throws<InvalidOperationException>(() => unloadedOrder.Customer);
        await context.Entry(unloadedOrder).Reference(o => o.Customer).LoadAsync();
        Assert.Equal(customer.Id, unloadedOrder.Customer.Id);

        context.ChangeTracker.Clear();
        var unloadedItem = await context.OrderItems.SingleAsync();
        Assert.Equal(order.Id, unloadedItem.OrderId);
        Assert.Equal(product.Id, unloadedItem.ProductId);
        Assert.Throws<InvalidOperationException>(() => unloadedItem.Order);
        Assert.Throws<InvalidOperationException>(() => unloadedItem.Product);
        await context.Entry(unloadedItem).Reference(i => i.Order).LoadAsync();
        await context.Entry(unloadedItem).Reference(i => i.Product).LoadAsync();
        Assert.Equal(order.Id, unloadedItem.Order.Id);
        Assert.Equal(product.Id, unloadedItem.Product.Id);
    }
}
