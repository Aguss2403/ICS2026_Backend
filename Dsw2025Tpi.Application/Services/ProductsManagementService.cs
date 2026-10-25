using Azure.Core;
using Dsw2025Tpi.Application.Exceptions;
using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Application.Interfaces;
using Dsw2025Tpi.Domain.Entities;
using Dsw2025Tpi.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Dsw2025Tpi.Application.Dtos.ProductModel;

namespace Dsw2025Tpi.Application.Services;

public class ProductsManagementService : IProductsManagementService
{
    private readonly IRepository _repository;

    public ProductsManagementService(IRepository repository)
    {
        _repository = repository;

    }
    // Same limits as the Products table (Dsw2025TpiContext), so invalid data never reaches the database.
    public const int SkuMaxLength = 20;
    public const int NameMaxLength = 50;
    public const int InternalCodeMaxLength = 30;
    public const int DescriptionMaxLength = 250;

    public void ValidateRequest(ProductRequest? request)
    {
        if (request is null)
            throw new EntityValidationException(new[] { "Los datos del producto son obligatorios." });

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Sku))
            errors.Add("El SKU es obligatorio.");
        else if (request.Sku.Length > SkuMaxLength)
            errors.Add($"El SKU no puede superar los {SkuMaxLength} caracteres.");
        if (string.IsNullOrWhiteSpace(request.Name))
            errors.Add("El nombre es obligatorio.");
        else if (request.Name.Length > NameMaxLength)
            errors.Add($"El nombre no puede superar los {NameMaxLength} caracteres.");
        if (request.InternalCode?.Length > InternalCodeMaxLength)
            errors.Add($"El código interno no puede superar los {InternalCodeMaxLength} caracteres.");
        if (request.Description?.Length > DescriptionMaxLength)
            errors.Add($"La descripción no puede superar los {DescriptionMaxLength} caracteres.");
        if (request.CurrentUnitPrice <= 0)
            errors.Add("El precio unitario debe ser mayor a 0.");
        if (request.StockQuantity < 0)
            errors.Add("La cantidad en stock no puede ser negativa.");
        if (errors.Count > 0)
            throw new EntityValidationException(errors);
    }

    private ProductModel.ProductResponse MapToResponse(Product product)
    {
        return new ProductModel.ProductResponse(
            product.Id,
            product.Sku,
            product.InternalCode,
            product.Name,
            product.Description,
            product.CurrentUnitPrice,
            product.StockQuantity,
            product.IsActive);
    }

    public async Task<ProductModel.ProductResponse?> GetProductById(Guid id)
    {
        var product = await _repository.GetById<Product>(id);
        if (product == null)
            throw new EntityNotFoundException($"No existe el producto con Id: {id}");
        return product != null ? new ProductModel.ProductResponse(
            product.Id,
            product.Sku,
            product.InternalCode,
            product.Name,
            product.Description,
            product.CurrentUnitPrice,
            product.StockQuantity,
            product.IsActive)
            : null;
    }

    public async Task<ProductModel.ResponsePagination?> GetProducts(ProductModel.FilterProduct? request=null)
    {
        var isActive = request?.Status == "enabled" ? (bool?) true :
                       request?.Status == "disabled" ? (bool?) false : null;
        var search = request?.Search;
        var activeProducts = await _repository.GetFiltered<Product>(p => 
            (isActive == null || p.IsActive == isActive)
            &&(string.IsNullOrEmpty(search) || p.Name.Contains(search))
            );

        if (activeProducts is null || !activeProducts.Any())
            return new ProductModel.ResponsePagination(new List<ProductModel.ProductResponse>(), 0);

        var products = activeProducts
            .Select(p => new ProductModel.ProductResponse(
                p.Id,
                p.Sku,
                p.InternalCode,
                p.Name,
                p.Description,
                p.CurrentUnitPrice,
                p.StockQuantity,
                p.IsActive))
            .OrderBy(p => p.Sku)
            .Skip(((request?.PageNumber ?? 1) - 1) * (request?.PageSize ?? activeProducts.Count()))
            .Take(request?.PageSize ?? activeProducts.Count())
            .ToList();

        return new ProductModel.ResponsePagination(products, activeProducts.Count());
    }

    public async Task<ProductModel.ProductResponse> AddProduct(ProductModel.ProductRequest request)
    {
        ValidateRequest(request);

        var exist = await _repository.First<Product>(p => p.Sku == request.Sku);
        if (exist != null) 
            throw new DuplicatedEntityException($"Ya existe un producto con el Sku {request.Sku}");

        var product = new Product(request.Sku,
            request.InternalCode ?? string.Empty,
            request.Name,
            request.Description ?? string.Empty,
            request.CurrentUnitPrice,
            request.StockQuantity);

        await _repository.Add(product);

        return MapToResponse(product);
    }

    public async Task<ProductModel.ProductResponse?> Update(Guid id, ProductModel.ProductRequest request)
    {
        var product = await _repository.GetById<Product>(id);
        if (product == null)
            throw new EntityNotFoundException($"No se encontró un producto con el ID {id}");

        ValidateRequest(request);

        if(request.Sku != product.Sku)
        {
            var exist = await _repository.First<Product>(p => p.Sku == request.Sku);
            if (exist != null) 
                throw new DuplicatedEntityException($"Ya existe un producto con el Sku {request.Sku}");
        }

        product.Sku = request.Sku;
        product.InternalCode = request.InternalCode ?? string.Empty;
        product.Name = request.Name;
        product.Description = request.Description ?? string.Empty;
        product.CurrentUnitPrice = request.CurrentUnitPrice;
        product.StockQuantity = request.StockQuantity;

        var updatedProduct = await _repository.Update(product);

        return MapToResponse(product);
    }

    public async Task<ProductModel.ProductResponse?> ToggleStatus(Guid id)
    {
        var product = await _repository.GetById<Product>(id);
        if (product == null)
            throw new EntityNotFoundException($"No se encontró un producto con el ID {id}");

        product.IsActive = !product.IsActive;

        var updatedProduct = await _repository.Update(product);

        return MapToResponse(product);
    }
}
