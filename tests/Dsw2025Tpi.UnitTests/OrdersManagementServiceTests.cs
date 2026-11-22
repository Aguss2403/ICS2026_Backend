using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Application.Exceptions;
using Dsw2025Tpi.Application.Services;
using Dsw2025Tpi.Domain.Entities;
using Dsw2025Tpi.Domain.Interfaces;
using Moq;
using Xunit;

namespace Dsw2025Tpi.UnitTests;

public class OrdersManagementServiceTests
{
    private readonly Mock<IRepository> repository = new(MockBehavior.Strict);
    private readonly Customer customer = new() { FirstName = "Ana", LastName = "Pérez", Address = "Calle 123" };
    private readonly Product product = new("SKU-1", "CODE-1", "Producto", "Descripción", 12.35m, 10);

    [Fact]
    public async Task ValidOrderCalculatesTotalDecreasesStockAndSavesOrder()
    {
        repository.Setup(r => r.GetById<Customer>(customer.Id)).ReturnsAsync(customer);
        repository.Setup(r => r.GetById<Product>(product.Id)).ReturnsAsync(product);
        Order? savedOrder = null;
        repository.Setup(r => r.Add(It.IsAny<Order>()))
            .Callback<Order>(order => savedOrder = order)
            .ReturnsAsync((Order order) => order);

        var response = await new OrdersManagementService(repository.Object).CreateOrder(
            new(customer.Id, "Entrega", new() { new(product.Id, 3) }));

        var order = Assert.IsType<Order>(savedOrder);
        Assert.Equal(order.Id, response.Id);
        Assert.Equal(customer.Id, order.CustomerId);
        Assert.Equal(37.05m, order.TotalAmount);
        var item = Assert.Single(order.OrderItems);
        Assert.Equal(product.Id, item.ProductId);
        Assert.Equal(3, item.Quantity);
        Assert.Equal(37.05m, item.SubTotal);
        Assert.Equal(7, product.StockQuantity);
        repository.Verify(r => r.Add(order), Times.Once);
    }

    [Fact]
    public async Task InsufficientStockRejectsOrderWithoutDecreasingStockOrSaving()
    {
        repository.Setup(r => r.GetById<Customer>(customer.Id)).ReturnsAsync(customer);
        repository.Setup(r => r.GetById<Product>(product.Id)).ReturnsAsync(product);

        var exception = await Assert.ThrowsAsync<EntityValidationException>(() =>
            new OrdersManagementService(repository.Object).CreateOrder(
                new(customer.Id, "Entrega", new() { new(product.Id, 6), new(product.Id, 5) })));

        Assert.Equal($"Stock insuficiente para el producto {product.Name}.", exception.Message);
        Assert.Equal(10, product.StockQuantity);
        repository.Verify(r => r.Update(It.IsAny<Product>()), Times.Never);
        repository.Verify(r => r.Add(It.IsAny<Order>()), Times.Never);
    }

    [Fact]
    public async Task MissingCustomerRejectsOrderWithoutTouchingProductsOrSaving()
    {
        repository.Setup(r => r.GetById<Customer>(customer.Id)).ReturnsAsync((Customer?)null);

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            new OrdersManagementService(repository.Object).CreateOrder(
                new(customer.Id, "Entrega", new() { new(product.Id, 3) })));

        Assert.Equal(10, product.StockQuantity);
        repository.Verify(r => r.GetById<Customer>(customer.Id), Times.Once);
        repository.VerifyNoOtherCalls();
    }
}
