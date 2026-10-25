using Dsw2025Tpi.Domain.Entities;

public class Order : EntityBase
{
    private Customer? _customer;

    public DateTime OrderDate { get; set; }
    public string ShippingAddress { get; set; } = string.Empty;
    public string BillingAddress { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public decimal TotalAmount { get; private set; }
    public Guid CustomerId { get; set; }
    // EF can materialize the entity before loading this required relationship.
    public Customer Customer
    {
        get => _customer ?? throw new InvalidOperationException("La relación Customer no está cargada.");
        set => _customer = value;
    }
    public List<OrderItem> OrderItems { get; set; } = new();
    public OrderStatus Status { get; set; }

    public Order() { }

    public Order(string shippingAddress, string billingAddress, string? notes, List<OrderItem> orderItems, Guid customerId)
    {
        ShippingAddress = shippingAddress;
        BillingAddress = billingAddress;
        Notes = notes ?? string.Empty;
        OrderDate = DateTime.UtcNow;
        OrderItems = orderItems ?? new List<OrderItem>();
        CustomerId = customerId;
        TotalAmount = CalculateTotalAmount();
        Status = OrderStatus.Pending;
    }

    public decimal CalculateTotalAmount() => OrderItems.Sum(item => item.SubTotal);
}
