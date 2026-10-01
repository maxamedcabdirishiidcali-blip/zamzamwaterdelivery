using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZamzamWaterDelivery.Application.Common;
using ZamzamWaterDelivery.Application.DTOs;
using ZamzamWaterDelivery.Application.Interfaces;
using ZamzamWaterDelivery.Application.Mappings;
using ZamzamWaterDelivery.Domain.Entities;
using ZamzamWaterDelivery.Domain.Enums;
using ZamzamWaterDelivery.Domain.Interfaces;

namespace ZamzamWaterDelivery.Application.Services.Payments;

public class PaymentService : IPaymentService
{
    private readonly IUnitOfWork _uow;
    private readonly IReadOnlyDictionary<PaymentMethod, IPaymentGateway> _gateways;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(IUnitOfWork uow, IEnumerable<IPaymentGateway> gateways, ILogger<PaymentService> logger)
    {
        _uow = uow;
        _gateways = gateways.ToDictionary(g => g.Method);
        _logger = logger;
    }

    public IReadOnlyList<PaymentMethod> AvailableMethods => _gateways.Keys.OrderBy(m => m).ToList();

    public async Task<Payment> InitiateAsync(Order order, CancellationToken ct = default)
    {
        if (!_gateways.TryGetValue(order.PaymentMethod, out var gateway))
            throw new BusinessRuleException($"{order.PaymentMethod.ToDisplayName()} is not available yet.");

        var result = await gateway.InitiateAsync(order, ct);
        _logger.LogInformation("Payment initiated for {OrderNumber} via {Method}: {Status}",
            order.OrderNumber, order.PaymentMethod, result.Status);

        return new Payment
        {
            Amount = order.Total,
            Method = order.PaymentMethod,
            Status = result.Status,
            TransactionReference = result.TransactionReference,
            PaymentDate = result.Status == PaymentStatus.Paid ? DateTime.Now : null
        };
    }

    public async Task<PagedResult<PaymentDto>> SearchAsync(PaymentFilter filter, CancellationToken ct = default)
    {
        var query = _uow.Payments.QueryNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(p => p.Order.OrderNumber.Contains(term) || p.Order.CustomerName.Contains(term) ||
                                     (p.TransactionReference != null && p.TransactionReference.Contains(term)));
        }
        if (filter.Status.HasValue) query = query.Where(p => p.Status == filter.Status);
        if (filter.Method.HasValue) query = query.Where(p => p.Method == filter.Method);
        if (filter.DateFrom.HasValue) query = query.Where(p => p.CreatedAt >= filter.DateFrom.Value.Date);
        if (filter.DateTo.HasValue)
        {
            var end = filter.DateTo.Value.Date.AddDays(1);
            query = query.Where(p => p.CreatedAt < end);
        }

        return await query.OrderByDescending(p => p.CreatedAt)
            .Select(Projections.Payment)
            .ToPagedResultAsync(filter.Page, filter.PageSize, ct);
    }

    public async Task<PaymentDto> GetByIdAsync(int id, CancellationToken ct = default) =>
        await _uow.Payments.QueryNoTracking().Where(p => p.Id == id).Select(Projections.Payment).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Payment", id);

    public async Task<PaymentDto> RecordPaymentAsync(PaymentCreateDto dto, CancellationToken ct = default)
    {
        var order = await _uow.Orders.Query().Include(o => o.Payment).FirstOrDefaultAsync(o => o.Id == dto.OrderId, ct)
                    ?? throw new NotFoundException("Order", dto.OrderId);
        if (order.Status == OrderStatus.Cancelled)
            throw new BusinessRuleException($"Order {order.OrderNumber} is cancelled and cannot be paid.");

        var payment = order.Payment ??= new Payment { OrderId = order.Id };
        if (payment.Status == PaymentStatus.Paid)
            throw new BusinessRuleException($"Order {order.OrderNumber} has already been paid.");

        MarkPaid(payment, order, dto.Method, dto.TransactionReference);
        await _uow.SaveChangesAsync(ct);
        return await GetByIdAsync(payment.Id, ct);
    }

    public async Task MarkAsPaidAsync(int id, string? transactionReference, CancellationToken ct = default)
    {
        var payment = await LoadAsync(id, ct);
        if (payment.Status == PaymentStatus.Paid) return;
        if (payment.Status == PaymentStatus.Refunded || payment.Order.Status == OrderStatus.Cancelled)
            throw new BusinessRuleException("Refunded or cancelled payments cannot be marked as paid.");

        MarkPaid(payment, payment.Order, payment.Method, transactionReference);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task RefundAsync(int id, CancellationToken ct = default)
    {
        var payment = await LoadAsync(id, ct);
        if (payment.Status != PaymentStatus.Paid)
            throw new BusinessRuleException("Only paid payments can be refunded.");

        payment.Status = PaymentStatus.Refunded;
        payment.UpdatedAt = DateTime.Now;
        await _uow.SaveChangesAsync(ct);
        _logger.LogInformation("Payment {PaymentId} for {OrderNumber} refunded", id, payment.Order.OrderNumber);
    }

    private async Task<Payment> LoadAsync(int id, CancellationToken ct) =>
        await _uow.Payments.Query().Include(p => p.Order).FirstOrDefaultAsync(p => p.Id == id, ct)
        ?? throw new NotFoundException("Payment", id);

    private static void MarkPaid(Payment payment, Order order, PaymentMethod method, string? reference)
    {
        var now = DateTime.Now;
        payment.Method = method;
        payment.Amount = order.Total;
        payment.Status = PaymentStatus.Paid;
        payment.PaymentDate = now;
        payment.TransactionReference = string.IsNullOrWhiteSpace(reference)
            ? payment.TransactionReference ?? $"PAY-{order.OrderNumber}"
            : reference.Trim();
        payment.UpdatedAt = now;
    }
}
