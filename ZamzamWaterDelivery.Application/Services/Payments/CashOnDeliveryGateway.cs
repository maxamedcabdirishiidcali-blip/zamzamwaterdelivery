using ZamzamWaterDelivery.Application.DTOs;
using ZamzamWaterDelivery.Application.Interfaces;
using ZamzamWaterDelivery.Domain.Entities;
using ZamzamWaterDelivery.Domain.Enums;

namespace ZamzamWaterDelivery.Application.Services.Payments;

/// <summary>Cash is collected by the driver; the payment stays Pending until the order is delivered.</summary>
public class CashOnDeliveryGateway : IPaymentGateway
{
    public PaymentMethod Method => PaymentMethod.CashOnDelivery;

    public Task<PaymentResult> InitiateAsync(Order order, CancellationToken ct = default) =>
        Task.FromResult(new PaymentResult(PaymentStatus.Pending, null, "Pay the driver in cash on delivery."));
}
