using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZamzamWaterDelivery.Application.Common;
using ZamzamWaterDelivery.Application.DTOs;
using ZamzamWaterDelivery.Application.Interfaces;
using ZamzamWaterDelivery.Application.Mappings;
using ZamzamWaterDelivery.Domain.Common;
using ZamzamWaterDelivery.Domain.Entities;
using ZamzamWaterDelivery.Domain.Enums;
using ZamzamWaterDelivery.Domain.Interfaces;

namespace ZamzamWaterDelivery.Application.Services;

public class OrderService : IOrderService
{
    private readonly IUnitOfWork _uow;
    private readonly IPaymentService _payments;
    private readonly INotificationService _notifications;
    private readonly DeliverySettings _delivery;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IUnitOfWork uow,
        IPaymentService payments,
        INotificationService notifications,
        IOptions<DeliverySettings> delivery,
        ILogger<OrderService> logger)
    {
        _uow = uow;
        _payments = payments;
        _notifications = notifications;
        _delivery = delivery.Value;
        _logger = logger;
    }

    #region Placing orders

    public async Task<CheckoutDto> GetCheckoutDefaultsAsync(string userId, CancellationToken ct = default)
    {
        var customer = await _uow.Customers.QueryNoTracking()
                           .Include(c => c.Addresses)
                           .FirstOrDefaultAsync(c => c.UserId == userId, ct)
                       ?? throw new ForbiddenAccessException("Only customer accounts can place orders.");

        var address = customer.Addresses.OrderByDescending(a => a.IsDefault).FirstOrDefault();
        return new CheckoutDto
        {
            FullName = customer.FullName,
            PhoneNumber = customer.PhoneNumber,
            DeliveryAddress = address?.AddressLine ?? string.Empty,
            City = address?.City ?? string.Empty,
            Location = address?.Location,
            PreferredDeliveryDate = DateTime.Today,
            PreferredTimeSlot = _delivery.AvailableTimeSlots.FirstOrDefault(),
            PaymentMethod = PaymentMethod.CashOnDelivery
        };
    }

    public async Task<OrderDetailsDto> PlaceOrderFromCartAsync(string userId, CheckoutDto dto, CancellationToken ct = default)
    {
        var items = await _uow.CartItems.QueryNoTracking()
            .Where(i => i.Cart.Customer.UserId == userId)
            .Select(i => new OrderItemRequestDto { ProductId = i.ProductId, Quantity = i.Quantity })
            .ToListAsync(ct);

        return await CreateOrderAsync(userId, dto, items, clearCart: true, ct);
    }

    public Task<OrderDetailsDto> PlaceOrderAsync(string userId, CreateOrderDto dto, CancellationToken ct = default) =>
        CreateOrderAsync(userId, dto, dto.Items, clearCart: false, ct);

    private async Task<OrderDetailsDto> CreateOrderAsync(
        string userId, CheckoutDto dto, IReadOnlyCollection<OrderItemRequestDto> items, bool clearCart, CancellationToken ct)
    {
        if (!_payments.AvailableMethods.Contains(dto.PaymentMethod))
            throw new BusinessRuleException(
                $"{dto.PaymentMethod.ToDisplayName()} is not available yet. Please choose Cash on Delivery.");

        var requested = items
            .GroupBy(i => i.ProductId)
            .Select(g => new { ProductId = g.Key, Quantity = g.Sum(x => x.Quantity) })
            .ToList();
        if (requested.Count == 0) throw new BusinessRuleException("Your cart is empty.");
        if (requested.Any(r => r.Quantity <= 0)) throw new BusinessRuleException("Quantities must be at least 1.");

        var customer = await _uow.Customers.Query()
                           .Include(c => c.Addresses)
                           .FirstOrDefaultAsync(c => c.UserId == userId, ct)
                       ?? throw new ForbiddenAccessException("Only customer accounts can place orders.");
        if (!customer.IsActive)
            throw new ForbiddenAccessException("Your account is inactive. Please contact support.");

        var order = new Order();
        await _uow.ExecuteInTransactionAsync(async () =>
        {
            var ids = requested.Select(r => r.ProductId).ToList();
            var products = await _uow.Products.Query().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);

            var now = DateTime.Now;
            order = new Order
            {
                OrderNumber = await GenerateOrderNumberAsync(ct),
                CustomerId = customer.Id,
                OrderDate = now,
                Status = OrderStatus.Pending,
                CustomerName = dto.FullName.Trim(),
                PhoneNumber = dto.PhoneNumber.Trim(),
                DeliveryAddress = ComposeAddress(dto),
                City = dto.City.Trim(),
                Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
                PreferredDeliveryDate = dto.PreferredDeliveryDate?.Date,
                PreferredTimeSlot = dto.PreferredTimeSlot,
                PaymentMethod = dto.PaymentMethod
            };

            foreach (var line in requested)
            {
                if (!products.TryGetValue(line.ProductId, out var product) || !product.IsActive)
                    throw new BusinessRuleException($"Product #{line.ProductId} is no longer available.");
                if (product.StockQuantity < line.Quantity)
                    throw new BusinessRuleException(product.StockQuantity == 0
                        ? $"{product.Name} is out of stock."
                        : $"Only {product.StockQuantity} unit(s) of {product.Name} are available.");

                product.StockQuantity -= line.Quantity;
                order.Items.Add(new OrderItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    UnitPrice = product.Price,
                    Quantity = line.Quantity,
                    LineTotal = product.Price * line.Quantity
                });
            }

            order.SubTotal = order.Items.Sum(i => i.LineTotal);
            order.DeliveryFee = _delivery.CalculateFee(order.SubTotal);
            order.Total = order.SubTotal + order.DeliveryFee;

            order.Delivery = new Delivery
            {
                DeliveryAddress = $"{order.DeliveryAddress}, {order.City}",
                DeliveryFee = order.DeliveryFee,
                DeliveryDate = order.PreferredDeliveryDate,
                DeliveryTime = order.PreferredTimeSlot,
                Status = DeliveryStatus.Unassigned
            };
            order.Payment = await _payments.InitiateAsync(order, ct);

            if (dto.SaveAsDefaultAddress) SaveDefaultAddress(customer, dto);

            await _uow.Orders.AddAsync(order, ct);

            if (clearCart)
            {
                var cartItems = await _uow.CartItems.Query().Where(i => i.Cart.CustomerId == customer.Id).ToListAsync(ct);
                foreach (var cartItem in cartItems) _uow.CartItems.Remove(cartItem);
            }

            await _uow.SaveChangesAsync(ct);
        }, ct);

        _logger.LogInformation("Order {OrderNumber} placed by customer {CustomerId} for {Total}",
            order.OrderNumber, customer.Id, order.Total);

        await _notifications.NotifyUserAsync(customer.UserId, NotificationType.NewOrder, "Order received",
            $"Thank you! Your order {order.OrderNumber} has been received and is awaiting confirmation.", order.Id, ct);
        await _notifications.NotifyAdminsAsync(NotificationType.NewOrder, "New order",
            $"New order {order.OrderNumber} from {order.CustomerName} ({order.Total:0.00}).", order.Id, ct);

        return await GetDetailsAsync(order.Id, ct);
    }

    private static string ComposeAddress(CheckoutDto dto) =>
        string.IsNullOrWhiteSpace(dto.Location)
            ? dto.DeliveryAddress.Trim()
            : $"{dto.DeliveryAddress.Trim()}, {dto.Location.Trim()}";

    private static void SaveDefaultAddress(Customer customer, CheckoutDto dto)
    {
        var address = customer.Addresses.FirstOrDefault(a => a.IsDefault);
        if (address is null)
        {
            address = new Address { IsDefault = true };
            customer.Addresses.Add(address);
        }
        address.AddressLine = dto.DeliveryAddress.Trim();
        address.City = dto.City.Trim();
        address.Location = dto.Location?.Trim() ?? string.Empty;
        address.UpdatedAt = DateTime.Now;
    }

    private async Task<string> GenerateOrderNumberAsync(CancellationToken ct)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var number = $"ZW{DateTime.Now:yyMMdd}{Random.Shared.Next(1000, 10000)}";
            if (!await _uow.Orders.QueryNoTracking().AnyAsync(o => o.OrderNumber == number, ct))
                return number;
        }
        return $"ZW{DateTime.Now:yyMMddHHmmssfff}";
    }

    #endregion

    #region Queries

    public async Task<PagedResult<OrderDto>> SearchAsync(OrderFilter filter, CancellationToken ct = default)
    {
        var query = _uow.Orders.QueryNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(o => o.OrderNumber.Contains(term) || o.CustomerName.Contains(term) ||
                                     o.PhoneNumber.Contains(term) || o.DeliveryAddress.Contains(term));
        }
        if (filter.Status.HasValue) query = query.Where(o => o.Status == filter.Status);
        if (filter.CustomerId.HasValue) query = query.Where(o => o.CustomerId == filter.CustomerId);
        if (filter.DriverId.HasValue) query = query.Where(o => o.Delivery != null && o.Delivery.DriverId == filter.DriverId);
        if (filter.DateFrom.HasValue) query = query.Where(o => o.OrderDate >= filter.DateFrom.Value.Date);
        if (filter.DateTo.HasValue)
        {
            var end = filter.DateTo.Value.Date.AddDays(1);
            query = query.Where(o => o.OrderDate < end);
        }

        return await query.OrderByDescending(o => o.OrderDate)
            .Select(Projections.Order)
            .ToPagedResultAsync(filter.Page, filter.PageSize, ct);
    }

    public async Task<IReadOnlyList<OrderDto>> GetCustomerOrdersAsync(string userId, CancellationToken ct = default) =>
        await _uow.Orders.QueryNoTracking()
            .Where(o => o.Customer.UserId == userId)
            .OrderByDescending(o => o.OrderDate)
            .Select(Projections.Order)
            .ToListAsync(ct);

    public async Task<OrderDetailsDto> GetDetailsAsync(int orderId, CancellationToken ct = default)
    {
        var order = await DetailsQuery().FirstOrDefaultAsync(o => o.Id == orderId, ct)
                    ?? throw new NotFoundException("Order", orderId);
        return order.ToDetailsDto();
    }

    public async Task<OrderDetailsDto> GetCustomerOrderDetailsAsync(string userId, int orderId, CancellationToken ct = default)
    {
        var order = await DetailsQuery().FirstOrDefaultAsync(o => o.Id == orderId && o.Customer.UserId == userId, ct)
                    ?? throw new NotFoundException("Order", orderId);
        return order.ToDetailsDto();
    }

    public async Task<OrderDetailsDto> TrackAsync(string userId, string orderNumber, CancellationToken ct = default)
    {
        var number = orderNumber.Trim().ToUpperInvariant();
        var order = await DetailsQuery().FirstOrDefaultAsync(o => o.OrderNumber == number && o.Customer.UserId == userId, ct)
                    ?? throw new NotFoundException($"No order with number '{orderNumber}' was found on your account.");
        return order.ToDetailsDto();
    }

    private IQueryable<Order> DetailsQuery() =>
        _uow.Orders.QueryNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .Include(o => o.Delivery).ThenInclude(d => d!.Driver)
            .Include(o => o.Payment);

    #endregion

    #region Workflow

    public async Task ChangeStatusAsync(int orderId, OrderStatus newStatus, string? reason = null, CancellationToken ct = default)
    {
        if (newStatus == OrderStatus.Cancelled)
        {
            await CancelAsync(orderId, reason, ct);
            return;
        }

        var order = await LoadForUpdateAsync(orderId, ct);
        if (order.Status == newStatus) return;
        if (!OrderStatusRules.CanTransition(order.Status, newStatus))
            throw new BusinessRuleException(
                $"Order {order.OrderNumber} cannot move from {order.Status.ToDisplayName()} to {newStatus.ToDisplayName()}.");

        var now = DateTime.Now;
        var delivery = EnsureDelivery(order);

        switch (newStatus)
        {
            case OrderStatus.OutForDelivery:
                if (delivery.Driver is null)
                    throw new BusinessRuleException("Assign a driver before sending the order out for delivery.");
                delivery.Status = DeliveryStatus.OutForDelivery;
                delivery.AcceptedAt ??= now;
                delivery.PickedUpAt = now;
                if (delivery.Driver.Status != DriverStatus.Offline) delivery.Driver.Status = DriverStatus.Busy;
                break;

            case OrderStatus.Delivered:
                delivery.Status = DeliveryStatus.Delivered;
                delivery.DeliveredAt = now;
                delivery.DeliveryDate ??= now.Date;
                if (order.Payment is { Method: PaymentMethod.CashOnDelivery, Status: PaymentStatus.Pending } payment)
                {
                    // Cash is collected by the driver at the door.
                    payment.Status = PaymentStatus.Paid;
                    payment.PaymentDate = now;
                    payment.TransactionReference ??= $"COD-{order.OrderNumber}";
                    payment.UpdatedAt = now;
                }
                break;
        }

        order.Status = newStatus;
        order.UpdatedAt = now;
        delivery.UpdatedAt = now;
        await _uow.SaveChangesAsync(ct);

        if (newStatus == OrderStatus.Delivered && delivery.DriverId.HasValue)
            await RefreshDriverAvailabilityAsync(delivery.DriverId.Value, ct);

        _logger.LogInformation("Order {OrderNumber} moved to {Status}", order.OrderNumber, newStatus);
        await NotifyStatusChangeAsync(order, newStatus, ct);
    }

    public async Task AssignDriverAsync(int orderId, int driverId, CancellationToken ct = default)
    {
        var order = await LoadForUpdateAsync(orderId, ct);
        if (!OrderStatusRules.CanAssignDriver(order.Status))
            throw new BusinessRuleException(
                $"A driver cannot be assigned to an order that is {order.Status.ToDisplayName()}.");

        var driver = await _uow.Drivers.Query().Include(d => d.User).FirstOrDefaultAsync(d => d.Id == driverId && !d.IsDeleted, ct)
                     ?? throw new NotFoundException("Driver", driverId);
        if (!driver.User.IsActive) throw new BusinessRuleException($"{driver.FullName}'s account is deactivated.");
        if (driver.Status == DriverStatus.Offline) throw new BusinessRuleException($"{driver.FullName} is offline.");

        var delivery = EnsureDelivery(order);
        var previousDriverUserId = delivery.Driver?.UserId;
        if (delivery.DriverId == driverId) return;

        var now = DateTime.Now;
        delivery.DriverId = driverId;
        delivery.Driver = driver;
        delivery.Status = DeliveryStatus.Assigned;
        delivery.AssignedAt = now;
        delivery.AcceptedAt = null;
        delivery.UpdatedAt = now;

        // Assigning a driver implies the order has been accepted by the company.
        if (order.Status == OrderStatus.Pending) order.Status = OrderStatus.Confirmed;
        order.UpdatedAt = now;

        await _uow.SaveChangesAsync(ct);
        _logger.LogInformation("Driver {DriverId} assigned to order {OrderNumber}", driverId, order.OrderNumber);

        await _notifications.NotifyUserAsync(driver.UserId, NotificationType.DriverAssigned, "New delivery assigned",
            $"Order {order.OrderNumber} for {order.CustomerName} - {order.DeliveryAddress}, {order.City}.", order.Id, ct);
        await _notifications.NotifyUserAsync(order.Customer.UserId, NotificationType.DriverAssigned, "Driver assigned",
            $"{driver.FullName} ({driver.PhoneNumber}) will deliver your order {order.OrderNumber}.", order.Id, ct);

        if (previousDriverUserId is not null)
            await _notifications.NotifyUserAsync(previousDriverUserId, NotificationType.General, "Delivery reassigned",
                $"Order {order.OrderNumber} has been reassigned to another driver.", order.Id, ct);
    }

    public async Task CancelAsync(int orderId, string? reason, CancellationToken ct = default)
    {
        var order = await LoadForUpdateAsync(orderId, ct);
        await CancelInternalAsync(order, reason, byCustomer: false, ct);
    }

    public async Task CancelByCustomerAsync(string userId, int orderId, string? reason, CancellationToken ct = default)
    {
        var order = await LoadForUpdateAsync(orderId, ct);
        if (order.Customer.UserId != userId) throw new NotFoundException("Order", orderId);
        if (order.Status != OrderStatus.Pending)
            throw new BusinessRuleException(
                "Orders can only be cancelled online while they are pending. Please call us to cancel this order.");

        await CancelInternalAsync(order, reason, byCustomer: true, ct);
    }

    private async Task CancelInternalAsync(Order order, string? reason, bool byCustomer, CancellationToken ct)
    {
        if (!OrderStatusRules.CanBeCancelled(order.Status))
            throw new BusinessRuleException(
                $"Order {order.OrderNumber} is {order.Status.ToDisplayName()} and can no longer be cancelled.");

        var now = DateTime.Now;
        await _uow.ExecuteInTransactionAsync(async () =>
        {
            var productIds = order.Items.Select(i => i.ProductId).ToList();
            var products = await _uow.Products.Query().Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
            foreach (var item in order.Items)
            {
                if (products.TryGetValue(item.ProductId, out var product)) product.StockQuantity += item.Quantity;
            }

            order.Status = OrderStatus.Cancelled;
            order.CancellationReason = string.IsNullOrWhiteSpace(reason)
                ? (byCustomer ? "Cancelled by customer" : "Cancelled by administrator")
                : reason.Trim();
            order.UpdatedAt = now;

            var delivery = EnsureDelivery(order);
            delivery.Status = DeliveryStatus.Cancelled;
            delivery.UpdatedAt = now;

            if (order.Payment is not null)
            {
                order.Payment.Status = order.Payment.Status == PaymentStatus.Paid ? PaymentStatus.Refunded : PaymentStatus.Failed;
                order.Payment.UpdatedAt = now;
            }

            await _uow.SaveChangesAsync(ct);
        }, ct);

        _logger.LogInformation("Order {OrderNumber} cancelled ({Reason})", order.OrderNumber, order.CancellationReason);

        var driverId = order.Delivery?.DriverId;
        if (driverId.HasValue) await RefreshDriverAvailabilityAsync(driverId.Value, ct);

        await _notifications.NotifyUserAsync(order.Customer.UserId, NotificationType.OrderCancelled, "Order cancelled",
            $"Order {order.OrderNumber} has been cancelled. Reason: {order.CancellationReason}.", order.Id, ct);
        if (order.Delivery?.Driver is not null)
            await _notifications.NotifyUserAsync(order.Delivery.Driver.UserId, NotificationType.OrderCancelled,
                "Delivery cancelled", $"Order {order.OrderNumber} was cancelled. No delivery needed.", order.Id, ct);
        if (byCustomer)
            await _notifications.NotifyAdminsAsync(NotificationType.OrderCancelled, "Order cancelled by customer",
                $"{order.CustomerName} cancelled order {order.OrderNumber}.", order.Id, ct);
    }

    private async Task<Order> LoadForUpdateAsync(int orderId, CancellationToken ct) =>
        await _uow.Orders.Query()
            .Include(o => o.Customer)
            .Include(o => o.Items)
            .Include(o => o.Delivery).ThenInclude(d => d!.Driver)
            .Include(o => o.Payment)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct)
        ?? throw new NotFoundException("Order", orderId);

    private static Delivery EnsureDelivery(Order order) =>
        order.Delivery ??= new Delivery
        {
            OrderId = order.Id,
            DeliveryAddress = $"{order.DeliveryAddress}, {order.City}",
            DeliveryFee = order.DeliveryFee,
            DeliveryDate = order.PreferredDeliveryDate,
            DeliveryTime = order.PreferredTimeSlot
        };

    /// <summary>A driver is Busy while carrying an order, otherwise Available (Offline is left untouched).</summary>
    private async Task RefreshDriverAvailabilityAsync(int driverId, CancellationToken ct)
    {
        var driver = await _uow.Drivers.GetByIdAsync(driverId, ct);
        if (driver is null || driver.Status == DriverStatus.Offline) return;

        var carrying = await _uow.Deliveries.QueryNoTracking()
            .AnyAsync(d => d.DriverId == driverId && d.Status == DeliveryStatus.OutForDelivery, ct);
        var status = carrying ? DriverStatus.Busy : DriverStatus.Available;
        if (driver.Status == status) return;

        driver.Status = status;
        driver.UpdatedAt = DateTime.Now;
        await _uow.SaveChangesAsync(ct);
    }

    private async Task NotifyStatusChangeAsync(Order order, OrderStatus status, CancellationToken ct)
    {
        var customerUserId = order.Customer.UserId;
        switch (status)
        {
            case OrderStatus.Confirmed:
                await _notifications.NotifyUserAsync(customerUserId, NotificationType.OrderConfirmed, "Order confirmed",
                    $"Your order {order.OrderNumber} has been confirmed.", order.Id, ct);
                break;
            case OrderStatus.Preparing:
                await _notifications.NotifyUserAsync(customerUserId, NotificationType.General, "Order being prepared",
                    $"We are preparing your order {order.OrderNumber}.", order.Id, ct);
                break;
            case OrderStatus.OutForDelivery:
                var driver = order.Delivery?.Driver;
                await _notifications.NotifyUserAsync(customerUserId, NotificationType.OutForDelivery, "Your water is on the way",
                    $"{driver?.FullName ?? "Our driver"} is on the way with order {order.OrderNumber}" +
                    (driver is null ? "." : $". Driver phone: {driver.PhoneNumber}."), order.Id, ct);
                break;
            case OrderStatus.Delivered:
                await _notifications.NotifyUserAsync(customerUserId, NotificationType.OrderDelivered, "Order delivered",
                    $"Order {order.OrderNumber} has been delivered. Thank you for choosing Zamzam Water!", order.Id, ct);
                await _notifications.NotifyAdminsAsync(NotificationType.OrderDelivered, "Order delivered",
                    $"Order {order.OrderNumber} was delivered to {order.CustomerName}.", order.Id, ct);
                break;
        }
    }

    #endregion
}
