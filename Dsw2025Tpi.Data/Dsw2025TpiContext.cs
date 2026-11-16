using Dsw2025Tpi.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dsw2025Tpi.Data;

public class Dsw2025TpiContext : DbContext
{
    public Dsw2025TpiContext(DbContextOptions<Dsw2025TpiContext> options)
        : base(options)
    {
        
    }

    public DbSet<Role> Roles { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Customer> Customers { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Role>(eb =>
        {
            eb.ToTable("Roles");
            eb.HasKey(r => r.Id);
            eb.Property(r => r.Name).HasMaxLength(50).IsRequired();
            eb.HasIndex(r => r.Name).IsUnique();
        });
        modelBuilder.Entity<User>(eb =>
        {
            eb.ToTable("Users");
            eb.HasKey(u => u.Id);
            eb.Property(u => u.Username).HasMaxLength(50).IsRequired();
            eb.Property(u => u.Email).HasMaxLength(100).IsRequired();
            eb.Property(u => u.Password).HasMaxLength(100).IsRequired();
            eb.HasIndex(u => u.Username).IsUnique();
            eb.HasOne(u => u.Role)
              .WithMany(r => r.Users)
              .HasForeignKey(u => u.RoleId)
              .OnDelete(DeleteBehavior.Restrict);
            eb.HasOne(u => u.Customer)
              .WithOne(c => c.User)
              .HasForeignKey<Customer>(c => c.UserId)
              .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<Customer>(eb =>
        {
            eb.ToTable("Customers");
            eb.HasKey(c => c.Id);
            eb.Property(c => c.FirstName).HasMaxLength(50).IsRequired();
            eb.Property(c => c.LastName).HasMaxLength(50).IsRequired();
            eb.Property(c => c.PhoneNumber).HasMaxLength(15).IsRequired();
            eb.Property(c => c.Address).HasMaxLength(150).IsRequired();
        });
        modelBuilder.Entity<Product>(eb =>
        {
            eb.ToTable("Products");
            eb.HasKey(p => p.Id);
            eb.Property(p => p.Sku).HasMaxLength(20).IsRequired();
            eb.Property(p => p.Name).HasMaxLength(50).IsRequired();
            eb.Property(p => p.InternalCode).HasMaxLength(30).IsRequired();
            eb.Property(p => p.Description).HasMaxLength(250);
            eb.Property(p => p.CurrentUnitPrice).HasColumnType("decimal(18,2)").IsRequired();
            eb.Property(p => p.StockQuantity).IsRequired().IsConcurrencyToken();
            eb.Property(p => p.IsActive).IsRequired();
        });
        modelBuilder.Entity<Order>(eb =>
        {
            eb.ToTable("Orders");
            eb.HasKey(o => o.Id);
            eb.Property(o => o.OrderDate).IsRequired();
            eb.Property(o => o.ShippingAddress).HasMaxLength(150).IsRequired();
            eb.Property(o => o.BillingAddress).HasMaxLength(150).IsRequired();
            eb.Property(o => o.Notes).HasMaxLength(500);
            eb.Property(o => o.TotalAmount).HasColumnType("decimal(18,2)").IsRequired();
        });
        modelBuilder.Entity<OrderItem>(eb =>
        {
            eb.ToTable("OrderItems");
            eb.HasKey(oi => oi.Id);
            eb.Property(oi => oi.Quantity).IsRequired();
            eb.Property(oi => oi.UnitPrice).HasColumnType("decimal(18,2)").IsRequired();
            eb.Property(oi => oi.SubTotal).HasColumnType("decimal(18,2)").IsRequired();
        });
    }
}
