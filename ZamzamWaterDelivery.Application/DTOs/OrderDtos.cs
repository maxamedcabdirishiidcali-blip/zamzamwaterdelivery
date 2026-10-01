using System.ComponentModel.DataAnnotations;
using ZamzamWaterDelivery.Application.Common;
using ZamzamWaterDelivery.Application.Validators;
using ZamzamWaterDelivery.Domain.Enums;

namespace ZamzamWaterDelivery.Application.DTOs;

public class CheckoutDto
{
    [Required, StringLength(100, MinimumLength = 3)]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Required, RegularExpression(ValidationPatterns.Phone, ErrorMessage = ValidationPatterns.PhoneMessage)]
    [Display(Name = "Phone Number")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required, StringLength(250, MinimumLength = 5)]
    [Display(Name = "Delivery Address")]
    public string DeliveryAddress { get; set; } = string.Empty;

    [Required, StringLength(60)]
    public string City { get; set; } = string.Empty;

    [StringLength(100)]
    [Display(Name = "District / Landmark")]
    public string? Location { get; set; }

    [StringLength(500)]
    [Display(Name = "Order Notes")]
    public string? Notes { get; set; }

    [DataType(DataType.Date), NotInPast]
    [Display(Name = "Preferred Delivery Date")]
    public DateTime? PreferredDeliveryDate { get; set; }

    [StringLength(60)]
    [Display(Name = "Preferred Delivery Time")]
    public string? PreferredTimeSlot { get; set; }

    [DefinedEnum]
    [Display(Name = "Payment Method")]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.CashOnDelivery;

    [Display(Name = "Save as my default address")]
    public bool SaveAsDefaultAddress { get; set; }
}

public class OrderItemRequestDto
{
    [Range(1, int.MaxValue)]
    public int ProductId { get; set; }

    [Range(1, 500)]
    public int Quantity { get; set; }
}

/// <summary>API order request: checkout details plus the items to order.</summary>
public class CreateOrderDto : CheckoutDto
{
    [Required, MinLength(1, ErrorMessage = "At least one item is required.")]
    public List<OrderItemRequestDto> Items { get; set; } = new();
}

public class OrderDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public OrderStatus Status { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string DeliveryAddress { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public int ItemCount { get; set; }
    public string? DriverName { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
}

public class OrderItemDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
}

public class OrderDetailsDto : OrderDto
{
    public string CustomerEmail { get; set; } = string.Empty;
    public decimal SubTotal { get; set; }
    public decimal DeliveryFee { get; set; }
    public string? Notes { get; set; }
    public DateTime? PreferredDeliveryDate { get; set; }
    public string? PreferredTimeSlot { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<OrderItemDto> Items { get; set; } = new();
    public DeliveryDto? Delivery { get; set; }
    public PaymentDto? Payment { get; set; }
}

public class OrderFilter : PagedRequest
{
    public OrderStatus? Status { get; set; }
    public int? DriverId { get; set; }
    public int? CustomerId { get; set; }

    [DataType(DataType.Date)]
    public DateTime? DateFrom { get; set; }

    [DataType(DataType.Date)]
    public DateTime? DateTo { get; set; }
}

public class UpdateOrderStatusDto
{
    [DefinedEnum]
    public OrderStatus Status { get; set; }

    [StringLength(300)]
    public string? Reason { get; set; }
}

public class AssignDriverDto
{
    [Range(1, int.MaxValue, ErrorMessage = "Please select a driver.")]
    [Display(Name = "Driver")]
    public int DriverId { get; set; }
}

public class CancelOrderDto
{
    [StringLength(300)]
    public string? Reason { get; set; }
}
