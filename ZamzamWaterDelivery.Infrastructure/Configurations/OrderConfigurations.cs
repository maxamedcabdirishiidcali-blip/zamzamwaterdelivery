using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZamzamWaterDelivery.Domain.Entities;

namespace ZamzamWaterDelivery.Infrastructure.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> b)
    {
        b.Property(o => o.OrderNumber).HasMaxLength(20).IsRequired();
        b.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(o => o.PaymentMethod).HasConversion<string>().HasMaxLength(20);
        b.Property(o => o.SubTotal).HasPrecision(18, 2);
        b.Property(o => o.DeliveryFee).HasPrecision(18, 2);
        b.Property(o => o.Total).HasPrecision(18, 2);
        b.Property(o => o.CustomerName).HasMaxLength(100).IsRequired();
        b.Property(o => o.PhoneNumber).HasMaxLength(30).IsRequired();
        b.Property(o => o.DeliveryAddress).HasMaxLength(300).IsRequired();
        b.Property(o => o.City).HasMaxLength(60).IsRequired();
        b.Property(o => o.Notes).HasMaxLength(500);
        b.Property(o => o.PreferredTimeSlot).HasMaxLength(60);
        b.Property(o => o.CancellationReason).HasMaxLength(300);

        b.HasIndex(o => o.OrderNumber).IsUnique();
        b.HasIndex(o => o.OrderDate);
        b.HasIndex(o => o.Status);

        // Customers are deactivated, never deleted, so their order history is preserved.
        b.HasOne(o => o.Customer)
            .WithMany(c => c.Orders)
            .HasForeignKey(o => o.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> b)
    {
        b.Property(i => i.ProductName).HasMaxLength(100).IsRequired();
        b.Property(i => i.UnitPrice).HasPrecision(18, 2);
        b.Property(i => i.LineTotal).HasPrecision(18, 2);

        b.HasOne(i => i.Order)
            .WithMany(o => o.Items)
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(i => i.Product)
            .WithMany(p => p.OrderItems)
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class DeliveryConfiguration : IEntityTypeConfiguration<Delivery>
{
    public void Configure(EntityTypeBuilder<Delivery> b)
    {
        b.Property(d => d.DeliveryAddress).HasMaxLength(400).IsRequired();
        b.Property(d => d.DeliveryFee).HasPrecision(18, 2);
        b.Property(d => d.DeliveryTime).HasMaxLength(60);
        b.Property(d => d.Notes).HasMaxLength(500);
        b.Property(d => d.Status).HasConversion<string>().HasMaxLength(20);

        b.HasIndex(d => d.OrderId).IsUnique();
        b.HasIndex(d => d.Status);

        b.HasOne(d => d.Order)
            .WithOne(o => o.Delivery)
            .HasForeignKey<Delivery>(d => d.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict: drivers with delivery history are deactivated rather than deleted.
        b.HasOne(d => d.Driver)
            .WithMany(dr => dr.Deliveries)
            .HasForeignKey(d => d.DriverId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.Property(p => p.Amount).HasPrecision(18, 2);
        b.Property(p => p.Method).HasConversion<string>().HasMaxLength(20);
        b.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(p => p.TransactionReference).HasMaxLength(100);

        b.HasIndex(p => p.OrderId).IsUnique();
        b.HasIndex(p => p.Status);
        b.HasIndex(p => p.TransactionReference);

        b.HasOne(p => p.Order)
            .WithOne(o => o.Payment)
            .HasForeignKey<Payment>(p => p.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
