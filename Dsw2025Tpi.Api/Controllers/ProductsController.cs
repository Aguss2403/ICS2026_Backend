using Dsw2025Tpi.Application.Exceptions;
using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Client;
using System.Threading.Tasks;

namespace Dsw2025Tpi.Api.Controllers;

[ApiController]
[Route("api/products/")]
[Authorize]
public class ProductsController : ControllerBase
{
    private readonly IProductsManagementService _service;
    public ProductsController(IProductsManagementService service)
    {
        _service = service;
    }

    [HttpGet()]
    [AllowAnonymous]
    public async Task<IActionResult> GetAllProducts([FromQuery] ProductModel.FilterProduct filter)
    {
        var products = await _service.GetProducts(filter);
        return Ok(products);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetProduct(Guid id)
    {
        var product = await _service.GetProductById(id);
        return Ok(product);

    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> AddProduct([FromBody]ProductModel.ProductRequest request)
    {
        var product = await _service.AddProduct(request);
        return Created($"/api/products/{product.Id}", product);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateProduct(Guid id, [FromBody] ProductModel.ProductRequest request)
    {
        var updatedProduct = await _service.Update(id, request);
        return Ok(updatedProduct);
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> ToggleProductStatus(Guid id)
    {
        var updatedProduct = await _service.ToggleStatus(id);
        return Ok(updatedProduct);
    }

    [HttpGet("admin")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetAllProductsAdmin([FromQuery] ProductModel.FilterProduct request)
    {
        var products = await _service.GetProducts(request);
        if (products == null)
        {
            Response.Headers.Append("X-Message", "There are no active products");
            return NoContent();
        }
        return Ok(products);
    }

}
