using Microsoft.AspNetCore.Mvc;
using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Application.Exceptions;
using Dsw2025Tpi.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace Dsw2025Ej15.Api.Controllers;

[ApiController]
[Route("api/orders/")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IOrdersManagementService _service;
    public OrdersController(IOrdersManagementService service)
    {
        _service = service;
    }

    [HttpGet]//Endpoint 7
    public async Task<IActionResult> GetAllOrders()
    {
        var orders = await _service.GetOrders();
        return Ok(orders);
    }

    [HttpPost]//Endpoint 6
    public async Task<IActionResult> CreateOrder([FromBody] OrderModel.OrderRequest request)
    {
        var order = await _service.CreateOrder(request);
        return Created($"/api/orders/{order.Id}", order);
    }

    [HttpGet("{id}")]//8

    public async Task<IActionResult> GetOrderById(Guid id)
    {
        var order = await _service.GetOrderById(id);
        return Ok(order);
    }

    [HttpPut("{id}/status")]//9
    public async Task<IActionResult> UpdateOrderStatus(Guid id, [FromBody] OrderModel.OrderRequest request)
    {
        var order = await _service.UpdateOrderStatus(id, request);
        return Ok(order);
    }
}