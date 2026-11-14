using Dsw2025Ej15.Api.Controllers;
using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Application.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Dsw2025Tpi.UnitTests;

public class InfrastructureSmokeTests
{
    [Fact]
    public async Task ControllerCanUseMockedServiceWithoutHttpOrDatabase()
    {
        var expected = new List<OrderModel.OrderResponse>();
        var service = new Mock<IOrdersManagementService>(MockBehavior.Strict);
        service.Setup(s => s.GetOrders()).ReturnsAsync(expected);

        var result = await new OrdersController(service.Object).GetAllOrders();

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(expected);
        service.Verify(s => s.GetOrders(), Times.Once);
        service.VerifyNoOtherCalls();
    }
}
