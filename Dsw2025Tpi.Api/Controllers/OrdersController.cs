using Microsoft.AspNetCore.Mvc;
using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Application.Interfaces;
using Dsw2025Tpi.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

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
        try
        {
            var order = await _service.CreateOrder(request);
            return Created($"/api/orders/{order.Id}", order);
        }
        catch (DbUpdateConcurrencyException exception) when (exception.Entries.Any(entry => entry.Entity is Product))
        {
            return Conflict(new
            {
                statusCode = StatusCodes.Status409Conflict,
                message = "El stock cambió mientras se creaba la orden. Volvé a intentar."
            });
        }
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
