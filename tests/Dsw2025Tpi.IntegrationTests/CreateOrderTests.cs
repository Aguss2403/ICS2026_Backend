using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Data;
using Dsw2025Tpi.Domain.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dsw2025Tpi.IntegrationTests;

public class CreateOrderTests
{
    [Fact]
    public async Task WithoutTokenReturnsUnauthorizedAndLeavesDatabaseUnchanged()
    {
        await using var factory = new SqliteApiFactory();
        using var client = factory.CreateApiClient();
        var product = TestData.Product();
        var request = await SeedOrderAsync(factory, (product, 3));

        using var response = await client.PostAsJsonAsync("/api/orders", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertRejectedOrderAsync(factory, (product, 10));
    }

    [Fact]
    public async Task AuthenticatedCustomerCreatesOrderAndPersistsTotalAndRemainingStock()
    {
        await using var factory = new SqliteApiFactory();
        using var client = factory.CreateApiClient();
        var product = TestData.Product();
        var request = await SeedOrderAsync(factory, (product, 3));
        factory.Authenticate(client, Role.User, "tp4-user");

        using var response = await client.PostAsJsonAsync("/api/orders", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = Assert.IsType<OrderModel.OrderResponse>(
            await response.Content.ReadFromJsonAsync<OrderModel.OrderResponse>());
        Assert.Equal($"/api/orders/{created.Id}", response.Headers.Location?.OriginalString);
        var order = await factory.ReadAsync(db => db.Orders.Include(o => o.OrderItems).SingleAsync());
        Assert.Equal(created.Id, order.Id);
        Assert.Equal(request.CustomerId, order.CustomerId);
        Assert.Equal(37.05m, order.TotalAmount);
        var item = Assert.Single(order.OrderItems);
        Assert.Equal(product.Id, item.ProductId);
        Assert.Equal(3, item.Quantity);
        Assert.Equal(12.35m, item.UnitPrice);
        Assert.Equal(37.05m, item.SubTotal);
        Assert.Equal(7, await factory.ReadAsync(db => db.Products
            .Where(p => p.Id == product.Id).Select(p => p.StockQuantity).SingleAsync()));
    }

    [Fact]
    public async Task InsufficientStockInLaterItemReturnsBadRequestAndLeavesDatabaseUnchanged()
    {
        await using var factory = new SqliteApiFactory();
        using var client = factory.CreateApiClient();
        var available = TestData.Product();
        var insufficient = new Product("LOW-SKU", "LOW-CODE", "Producto escaso", "Test", 5m, 2);
        var request = await SeedOrderAsync(factory, (available, 3), (insufficient, 3));
        factory.Authenticate(client, Role.User, "tp4-user");

        using var response = await client.PostAsJsonAsync("/api/orders", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertRejectedOrderAsync(factory, (available, 10), (insufficient, 2));
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(400, error.GetProperty("statusCode").GetInt32());
        Assert.Equal("Stock insuficiente para el producto Producto escaso.", error.GetProperty("message").GetString());
    }

    [Fact]
    public async Task ConcurrentStockChangeReturnsConflictAndRollsBackOrder()
    {
        var product = TestData.Product();
        var other = new Product("OTHER-SKU", "OTHER-CODE", "Otro producto", "Test", 5m, 10);
        await using var factory = new ConcurrentStockFactory(product.Id);
        using var client = factory.CreateApiClient();
        var request = await SeedOrderAsync(factory, (other, 3), (product, 3));
        factory.Authenticate(client, Role.User, "tp4-user");

        using var response = await client.PostAsJsonAsync("/api/orders", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(409, error.GetProperty("statusCode").GetInt32());
        Assert.Equal("El stock cambió mientras se creaba la orden. Volvé a intentar.", error.GetProperty("message").GetString());
        await AssertRejectedOrderAsync(factory, (product, 8), (other, 10));
    }

    private static async Task<OrderModel.OrderRequest> SeedOrderAsync(
        SqliteApiFactory factory, params (Product Product, int Quantity)[] items)
    {
        Customer customer = null!;
        await factory.SeedAsync(async db =>
        {
            customer = await TestData.AddCustomerAsync(db);
            db.Products.AddRange(items.Select(item => item.Product));
        });

        return new OrderModel.OrderRequest(customer.Id, "Entrega", items.Select(item =>
            new OrderModel.OrderItemRequest(item.Product.Id, item.Quantity)).ToList());
    }

    private static async Task AssertRejectedOrderAsync(
        SqliteApiFactory factory, params (Product Product, int Stock)[] expected)
    {
        var state = await factory.ReadAsync(async db => (
            Orders: await db.Orders.CountAsync(),
            Items: await db.OrderItems.CountAsync(),
            Stock: await db.Products.ToDictionaryAsync(p => p.Id, p => p.StockQuantity)));

        Assert.Equal(0, state.Orders);
        Assert.Equal(0, state.Items);
        Assert.Equal(expected.Length, state.Stock.Count);
        foreach (var (product, stock) in expected)
            Assert.Equal(stock, state.Stock[product.Id]);
    }

    private sealed class ConcurrentStockFactory(Guid productId) : SqliteApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services => services.ConfigureDbContext<Dsw2025TpiContext>(
                options => options.AddInterceptors(new ChangeStockBeforeSavingOrder(() =>
                    SeedAsync(async db =>
                    {
                        var product = await db.Products.SingleAsync(p => p.Id == productId);
                        product.DecreaseStock(2);
                    })))));
        }
    }

    private sealed class ChangeStockBeforeSavingOrder(Func<Task> changeStock) : SaveChangesInterceptor
    {
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            // Commit in a separate context after the request has read the stock and before its save.
            if (eventData.Context!.ChangeTracker.Entries<Order>().Any(entry => entry.State == EntityState.Added))
                await changeStock();

            return result;
        }
    }
}
