using System.Linq.Expressions;
using ZamzamWaterDelivery.Application.DTOs;
using ZamzamWaterDelivery.Domain.Entities;
using ZamzamWaterDelivery.Domain.Enums;

namespace ZamzamWaterDelivery.Application.Mappings;

/// <summary>EF-translatable projections from entities to DTOs, shared by all services.</summary>
public static class Projections
{
    public static readonly Expression<Func<Product, ProductDto>> Product = p => new ProductDto
    {
        Id = p.Id,
        Name = p.Name,
        Description = p.Description,
        Price = p.Price,
        ImageUrl = p.ImageUrl,
        StockQuantity = p.StockQuantity,
        IsActive = p.IsActive,
        CategoryId = p.CategoryId,
        CategoryName = p.Category.Name
    };

    public static readonly Expression<Func<Order, OrderDto>> Order = o => new OrderDto
    {
        Id = o.Id,
        OrderNumber = o.OrderNumber,
        OrderDate = o.OrderDate,
        Status = o.Status,
        CustomerId = o.CustomerId,
        CustomerName = o.CustomerName,
        PhoneNumber = o.PhoneNumber,
        DeliveryAddress = o.DeliveryAddress,
        City = o.City,
        Total = o.Total,
        ItemCount = o.Items.Sum(i => i.Quantity),
        DriverName = o.Delivery != null && o.Delivery.Driver != null ? o.Delivery.Driver.FullName : null,
        PaymentMethod = o.PaymentMethod,
        PaymentStatus = o.Payment != null ? o.Payment.Status : PaymentStatus.Pending
    };

    public static readonly Expression<Func<Delivery, DeliveryDto>> Delivery = d => new DeliveryDto
    {
        Id = d.Id,
        OrderId = d.OrderId,
        OrderNumber = d.Order.OrderNumber,
        OrderStatus = d.Order.Status,
        OrderTotal = d.Order.Total,
        OrderDate = d.Order.OrderDate,
        OrderNotes = d.Order.Notes,
        PaymentMethod = d.Order.PaymentMethod,
        PaymentStatus = d.Order.Payment != null ? d.Order.Payment.Status : PaymentStatus.Pending,
        CustomerName = d.Order.CustomerName,
        CustomerPhone = d.Order.PhoneNumber,
        DriverId = d.DriverId,
        DriverName = d.Driver != null ? d.Driver.FullName : null,
        DriverPhone = d.Driver != null ? d.Driver.PhoneNumber : null,
        DeliveryAddress = d.DeliveryAddress,
        City = d.Order.City,
        DeliveryFee = d.DeliveryFee,
        DeliveryDate = d.DeliveryDate,
        DeliveryTime = d.DeliveryTime,
        Status = d.Status,
        Notes = d.Notes,
        AssignedAt = d.AssignedAt,
        AcceptedAt = d.AcceptedAt,
        PickedUpAt = d.PickedUpAt,
        DeliveredAt = d.DeliveredAt
    };

    public static readonly Expression<Func<Driver, DriverDto>> Driver = d => new DriverDto
    {
        Id = d.Id,
        UserId = d.UserId,
        FullName = d.FullName,
        PhoneNumber = d.PhoneNumber,
        Email = d.Email,
        VehicleType = d.VehicleType,
        VehicleNumber = d.VehicleNumber,
        LicenseNumber = d.LicenseNumber,
        Status = d.Status,
        IsActive = d.User.IsActive,
        CreatedAt = d.CreatedAt,
        ActiveDeliveries = d.Deliveries.Count(x =>
            x.Status == DeliveryStatus.Assigned || x.Status == DeliveryStatus.Accepted || x.Status == DeliveryStatus.OutForDelivery),
        CompletedDeliveries = d.Deliveries.Count(x => x.Status == DeliveryStatus.Delivered)
    };

    public static readonly Expression<Func<Customer, CustomerDto>> Customer = c => new CustomerDto
    {
        Id = c.Id,
        UserId = c.UserId,
        FullName = c.FullName,
        Email = c.Email,
        PhoneNumber = c.PhoneNumber,
        IsActive = c.IsActive,
        CreatedAt = c.CreatedAt,
        AddressLine = c.Addresses.OrderByDescending(a => a.IsDefault).Select(a => a.AddressLine).FirstOrDefault(),
        City = c.Addresses.OrderByDescending(a => a.IsDefault).Select(a => a.City).FirstOrDefault(),
        Location = c.Addresses.OrderByDescending(a => a.IsDefault).Select(a => a.Location).FirstOrDefault(),
        OrderCount = c.Orders.Count,
        TotalSpent = c.Orders.Where(o => o.Status == OrderStatus.Delivered).Sum(o => (decimal?)o.Total) ?? 0
    };

    public static readonly Expression<Func<Payment, PaymentDto>> Payment = p => new PaymentDto
    {
        Id = p.Id,
        OrderId = p.OrderId,
        OrderNumber = p.Order.OrderNumber,
        CustomerName = p.Order.CustomerName,
        Amount = p.Amount,
        Method = p.Method,
        Status = p.Status,
        TransactionReference = p.TransactionReference,
        PaymentDate = p.PaymentDate,
        CreatedAt = p.CreatedAt
    };

    public static readonly Expression<Func<OrderItem, OrderItemDto>> OrderItem = i => new OrderItemDto
    {
        ProductId = i.ProductId,
        ProductName = i.ProductName,
        ImageUrl = i.Product.ImageUrl,
        UnitPrice = i.UnitPrice,
        Quantity = i.Quantity,
        LineTotal = i.LineTotal
    };
}
