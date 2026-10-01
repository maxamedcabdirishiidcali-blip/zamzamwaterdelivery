using System.ComponentModel.DataAnnotations;
using ZamzamWaterDelivery.Application.Common;
using ZamzamWaterDelivery.Application.Validators;
using ZamzamWaterDelivery.Domain.Enums;

namespace ZamzamWaterDelivery.Application.DTOs;

public class PaymentDto
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public PaymentStatus Status { get; set; }
    public string? TransactionReference { get; set; }
    public DateTime? PaymentDate { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PaymentCreateDto
{
    [Range(1, int.MaxValue)]
    public int OrderId { get; set; }

    [DefinedEnum]
    public PaymentMethod Method { get; set; } = PaymentMethod.CashOnDelivery;

    [StringLength(100)]
    public string? TransactionReference { get; set; }
}

public class PaymentFilter : PagedRequest
{
    public PaymentStatus? Status { get; set; }
    public PaymentMethod? Method { get; set; }

    [DataType(DataType.Date)]
    public DateTime? DateFrom { get; set; }

    [DataType(DataType.Date)]
    public DateTime? DateTo { get; set; }
}

/// <summary>Outcome returned by a payment gateway.</summary>
public record PaymentResult(PaymentStatus Status, string? TransactionReference, string? Message = null);
