using ZamzamWaterDelivery.Application.DTOs;
using ZamzamWaterDelivery.Domain.Entities;
using ZamzamWaterDelivery.Domain.Enums;

namespace ZamzamWaterDelivery.Application.Mappings;

/// <summary>In-memory mappers for aggregates loaded with their navigations.</summary>
public static class EntityMappers
{
    private static readonly Func<Delivery, DeliveryDto> DeliveryFunc = Projections.Delivery.Compile();
    private static readonly Func<OrderItem, OrderItemDto> OrderItemFunc = Projections.OrderItem.Compile();
    private static readonly Func<Payment, PaymentDto> PaymentFunc = Projections.Payment.Compile();

    /// <summary>Requires Items (with Product), Delivery (with Driver), Payment and Customer to be loaded.</summary>
    public static OrderDetailsDto ToDetailsDto(this Order o)
    {
        // Back-references let the shared projections resolve order data.
        if (o.Delivery is not null) o.Delivery.Order = o;
        if (o.Payment is not null) o.Payment.Order = o;

        return new OrderDetailsDto
        {
            Id = o.Id,
            OrderNumber = o.OrderNumber,
            OrderDate = o.OrderDate,
            Status = o.Status,
            CustomerId = o.CustomerId,
            CustomerName = o.CustomerName,
            CustomerEmail = o.Customer?.Email ?? string.Empty,
            PhoneNumber = o.PhoneNumber,
            DeliveryAddress = o.DeliveryAddress,
            City = o.City,
            SubTotal = o.SubTotal,
            DeliveryFee = o.DeliveryFee,
            Total = o.Total,
            ItemCount = o.Items.Sum(i => i.Quantity),
            DriverName = o.Delivery?.Driver?.FullName,
            PaymentMethod = o.PaymentMethod,
            PaymentStatus = o.Payment?.Status ?? PaymentStatus.Pending,
            Notes = o.Notes,
            PreferredDeliveryDate = o.PreferredDeliveryDate,
            PreferredTimeSlot = o.PreferredTimeSlot,
            CancellationReason = o.CancellationReason,
            UpdatedAt = o.UpdatedAt,
            Items = o.Items.Select(OrderItemFunc).ToList(),
            Delivery = o.Delivery is null ? null : DeliveryFunc(o.Delivery),
            Payment = o.Payment is null ? null : PaymentFunc(o.Payment)
        };
    }

    /// <summary>Requires Order (with Items.Product and Payment) and Driver to be loaded.</summary>
    public static DeliveryDto ToDtoWithItems(this Delivery d)
    {
        var dto = DeliveryFunc(d);
        dto.Items = d.Order.Items.Select(OrderItemFunc).ToList();
        return dto;
    }
}
