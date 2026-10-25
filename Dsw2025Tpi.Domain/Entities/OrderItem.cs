namespace Dsw2025Tpi.Domain.Entities;

public class OrderItem : EntityBase
{
    private Product? _product;
    private Order? _order;

    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal SubTotal { get; set; }
    public Guid ProductId { get; set; }
    // Required relationships may not be loaded when EF materializes the entity.
    public Product Product
    {
        get => _product ?? throw new InvalidOperationException("La relación Product no está cargada.");
        set => _product = value;
    }
    public Guid OrderId { get; set; }
    public Order Order
    {
        get => _order ?? throw new InvalidOperationException("La relación Order no está cargada.");
        set => _order = value;
    }

    public OrderItem() { }

    public OrderItem(int quantity, Product product, Order order)
    {
        if (!product.HasSufficientStock(quantity))
            throw new InvalidOperationException($"Stock insuficiente para el producto {product.Name}");

        Product = product;
        ProductId = product.Id;
        Quantity = quantity;
        UnitPrice = product.CurrentUnitPrice;
        SubTotal = CalculateSubTotal();
        Order = order;
        OrderId = Order.Id;
    }

    public decimal CalculateSubTotal() => UnitPrice * Quantity;
}
