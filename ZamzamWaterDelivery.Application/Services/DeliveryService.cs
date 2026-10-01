using Microsoft.EntityFrameworkCore;
using ZamzamWaterDelivery.Application.Common;
using ZamzamWaterDelivery.Application.DTOs;
using ZamzamWaterDelivery.Application.Interfaces;
using ZamzamWaterDelivery.Application.Mappings;
using ZamzamWaterDelivery.Domain.Entities;
using ZamzamWaterDelivery.Domain.Enums;
using ZamzamWaterDelivery.Domain.Interfaces;

namespace ZamzamWaterDelivery.Application.Services;

public class DeliveryService : IDeliveryService
{
    private static readonly DeliveryStatus[] ActiveStatuses =
        { DeliveryStatus.Assigned, DeliveryStatus.Accepted, DeliveryStatus.OutForDelivery };

    private readonly IUnitOfWork _uow;
    private readonly IOrderService _orders;
    private readonly INotificationService _notifications;

    public DeliveryService(IUnitOfWork uow, IOrderService orders, INotificationService notifications)
    {
        _uow = uow;
        _orders = orders;
        _notifications = notifications;
    }

    public async Task<PagedResult<DeliveryDto>> SearchAsync(DeliveryFilter filter, CancellationToken ct = default)
    {
        var query = _uow.Deliveries.QueryNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(d => d.Order.OrderNumber.Contains(term) || d.Order.CustomerName.Contains(term) ||
                                     d.DeliveryAddress.Contains(term) || (d.Driver != null && d.Driver.FullName.Contains(term)));
        }
        if (filter.Status.HasValue) query = query.Where(d => d.Status == filter.Status);
        if (filter.DriverId.HasValue) query = query.Where(d => d.DriverId == filter.DriverId);
        if (filter.DateFrom.HasValue) query = query.Where(d => d.Order.OrderDate >= filter.DateFrom.Value.Date);
        if (filter.DateTo.HasValue)
        {
            var end = filter.DateTo.Value.Date.AddDays(1);
            query = query.Where(d => d.Order.OrderDate < end);
        }

        return await query.OrderByDescending(d => d.Order.OrderDate)
            .Select(Projections.Delivery)
            .ToPagedResultAsync(filter.Page, filter.PageSize, ct);
    }

    public async Task<DeliveryDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var delivery = await DetailsQuery().FirstOrDefaultAsync(d => d.Id == id, ct)
                       ?? throw new NotFoundException("Delivery", id);
        return delivery.ToDtoWithItems();
    }

    public async Task UpdateAsync(int id, DeliveryUpdateDto dto, CancellationToken ct = default)
    {
        var delivery = await _uow.Deliveries.Query()
                           .Include(d => d.Order).ThenInclude(o => o.Payment)
                           .FirstOrDefaultAsync(d => d.Id == id, ct)
                       ?? throw new NotFoundException("Delivery", id);

        var order = delivery.Order;
        if (order.Status is OrderStatus.Delivered or OrderStatus.Cancelled)
            throw new BusinessRuleException($"Order {order.OrderNumber} is {order.Status.ToDisplayName()}; its delivery can no longer be changed.");

        delivery.DeliveryDate = dto.DeliveryDate?.Date;
        delivery.DeliveryTime = string.IsNullOrWhiteSpace(dto.DeliveryTime) ? null : dto.DeliveryTime.Trim();
        delivery.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();

        if (dto.DeliveryFee.HasValue && dto.DeliveryFee.Value != delivery.DeliveryFee)
        {
            if (order.Payment?.Status == PaymentStatus.Paid)
                throw new BusinessRuleException("The delivery fee cannot be changed after the order has been paid.");

            delivery.DeliveryFee = dto.DeliveryFee.Value;
            order.DeliveryFee = dto.DeliveryFee.Value;
            order.Total = order.SubTotal + order.DeliveryFee;
            if (order.Payment is not null) order.Payment.Amount = order.Total;
            order.UpdatedAt = DateTime.Now;
        }

        delivery.UpdatedAt = DateTime.Now;
        await _uow.SaveChangesAsync(ct);

        if (dto.DriverId.HasValue && dto.DriverId != delivery.DriverId)
            await _orders.AssignDriverAsync(order.Id, dto.DriverId.Value, ct);
    }

    public async Task<IReadOnlyList<DeliveryDto>> GetDriverDeliveriesAsync(string driverUserId, bool activeOnly, CancellationToken ct = default)
    {
        var query = _uow.Deliveries.QueryNoTracking().Where(d => d.Driver != null && d.Driver.UserId == driverUserId);
        query = activeOnly
            ? query.Where(d => ActiveStatuses.Contains(d.Status))
            : query.Where(d => !ActiveStatuses.Contains(d.Status));

        return await query
            .OrderByDescending(d => d.Status == DeliveryStatus.OutForDelivery)
            .ThenByDescending(d => d.AssignedAt ?? d.Order.OrderDate)
            .Take(activeOnly ? 100 : 50)
            .Select(Projections.Delivery)
            .ToListAsync(ct);
    }

    public async Task<DeliveryDto> GetDriverDeliveryAsync(string driverUserId, int deliveryId, CancellationToken ct = default)
    {
        var delivery = await DetailsQuery()
                           .FirstOrDefaultAsync(d => d.Id == deliveryId && d.Driver != null && d.Driver.UserId == driverUserId, ct)
                       ?? throw new NotFoundException("Delivery", deliveryId);
        return delivery.ToDtoWithItems();
    }

    public async Task AcceptAsync(string driverUserId, int deliveryId, CancellationToken ct = default)
    {
        var delivery = await LoadDriverDeliveryAsync(driverUserId, deliveryId, ct);
        if (delivery.Status != DeliveryStatus.Assigned)
            throw new BusinessRuleException($"This delivery is already {delivery.Status.ToDisplayName()}.");

        delivery.Status = DeliveryStatus.Accepted;
        delivery.AcceptedAt = DateTime.Now;
        delivery.UpdatedAt = DateTime.Now;
        await _uow.SaveChangesAsync(ct);

        await _notifications.NotifyAdminsAsync(NotificationType.General, "Delivery accepted",
            $"{delivery.Driver!.FullName} accepted order {delivery.Order.OrderNumber}.", delivery.OrderId, ct);
    }

    public async Task StartAsync(string driverUserId, int deliveryId, CancellationToken ct = default)
    {
        var delivery = await LoadDriverDeliveryAsync(driverUserId, deliveryId, ct);
        if (delivery.Status == DeliveryStatus.Assigned)
            throw new BusinessRuleException("Please accept the delivery before starting it.");
        if (delivery.Status != DeliveryStatus.Accepted)
            throw new BusinessRuleException($"This delivery is already {delivery.Status.ToDisplayName()}.");

        await _orders.ChangeStatusAsync(delivery.OrderId, OrderStatus.OutForDelivery, ct: ct);
    }

    public async Task CompleteAsync(string driverUserId, int deliveryId, CancellationToken ct = default)
    {
        var delivery = await LoadDriverDeliveryAsync(driverUserId, deliveryId, ct);
        if (delivery.Status != DeliveryStatus.OutForDelivery)
            throw new BusinessRuleException("Only deliveries that are out for delivery can be marked as delivered.");

        await _orders.ChangeStatusAsync(delivery.OrderId, OrderStatus.Delivered, ct: ct);
    }

    private async Task<Delivery> LoadDriverDeliveryAsync(string driverUserId, int deliveryId, CancellationToken ct) =>
        await _uow.Deliveries.Query()
            .Include(d => d.Order)
            .Include(d => d.Driver)
            .FirstOrDefaultAsync(d => d.Id == deliveryId && d.Driver != null && d.Driver.UserId == driverUserId, ct)
        ?? throw new NotFoundException("Delivery", deliveryId);

    private IQueryable<Delivery> DetailsQuery() =>
        _uow.Deliveries.QueryNoTracking()
            .Include(d => d.Order).ThenInclude(o => o.Items).ThenInclude(i => i.Product)
            .Include(d => d.Order).ThenInclude(o => o.Payment)
            .Include(d => d.Driver);
}
