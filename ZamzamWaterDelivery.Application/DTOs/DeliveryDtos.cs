using System.ComponentModel.DataAnnotations;
using ZamzamWaterDelivery.Application.Common;
using ZamzamWaterDelivery.Domain.Enums;

namespace ZamzamWaterDelivery.Application.DTOs;

public class DeliveryDto
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public OrderStatus OrderStatus { get; set; }
    public decimal OrderTotal { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public int? DriverId { get; set; }
    public string? DriverName { get; set; }
    public string? DriverPhone { get; set; }
    public string DeliveryAddress { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public decimal DeliveryFee { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public string? DeliveryTime { get; set; }
    public DeliveryStatus Status { get; set; }
    public string? Notes { get; set; }
    public string? OrderNotes { get; set; }
    public DateTime OrderDate { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? PickedUpAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public List<OrderItemDto> Items { get; set; } = new();
}

public class DeliveryUpdateDto
{
    [Display(Name = "Driver")]
    public int? DriverId { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Delivery Date")]
    public DateTime? DeliveryDate { get; set; }

    [StringLength(60)]
    [Display(Name = "Delivery Time")]
    public string? DeliveryTime { get; set; }

    [Range(0, 1000)]
    [Display(Name = "Delivery Fee")]
    public decimal? DeliveryFee { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}

public class DeliveryFilter : PagedRequest
{
    public DeliveryStatus? Status { get; set; }
    public int? DriverId { get; set; }

    [DataType(DataType.Date)]
    public DateTime? DateFrom { get; set; }

    [DataType(DataType.Date)]
    public DateTime? DateTo { get; set; }
}
