using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dsw2025Tpi.Domain.Entities;

public class Product : EntityBase
{
    public string Sku { get; set; } = string.Empty;
    public string InternalCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal CurrentUnitPrice { get; set; }
    public int StockQuantity { get; set; }
    public bool IsActive { get; set; }

    public Product()
    {
        
    }
    public Product(string sku, string internalCode, string name, string description, decimal currentUnitPrice, int stockQuantity)
    {
        Sku = sku;
        InternalCode = internalCode;
        Name = name;
        Description = description;
        CurrentUnitPrice = currentUnitPrice;
        StockQuantity = stockQuantity;
        IsActive = true;
    }

    public bool HasSufficientStock(int quantity)
    {
        return StockQuantity >= quantity;
    }

    public void DecreaseStock(int quantity)
    {
        StockQuantity -= quantity;
    }
}
