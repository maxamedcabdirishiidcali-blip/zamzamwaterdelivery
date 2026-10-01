using Microsoft.AspNetCore.Mvc.Rendering;
using ZamzamWaterDelivery.Application.Common;
using ZamzamWaterDelivery.Application.DTOs;
using ZamzamWaterDelivery.Domain.Enums;
using ZamzamWaterDelivery.Web.Infrastructure;

namespace ZamzamWaterDelivery.Web.Models;

public class ErrorViewModel
{
    public string? RequestId { get; set; }
    public int StatusCode { get; set; } = 500;
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}

public class HomeViewModel
{
    public IReadOnlyList<ProductDto> Products { get; set; } = Array.Empty<ProductDto>();
    public decimal DeliveryFee { get; set; }
    public decimal FreeDeliveryThreshold { get; set; }
}

/// <summary>A paged list with its filter and optional lookup lists for filter dropdowns.</summary>
public class ListViewModel<TItem, TFilter> where TFilter : PagedRequest
{
    public PagedResult<TItem> Result { get; set; } = new();
    public TFilter Filter { get; set; } = default!;
    public IEnumerable<SelectListItem> Lookup { get; set; } = Array.Empty<SelectListItem>();
}

public class CheckoutViewModel
{
    public CheckoutDto Checkout { get; set; } = new();
    public CartDto Cart { get; set; } = new();
    public IReadOnlyList<string> TimeSlots { get; set; } = Array.Empty<string>();
    public IReadOnlyList<PaymentMethod> AvailableMethods { get; set; } = Array.Empty<PaymentMethod>();
}

public class ProductFormViewModel : ProductUpsertDto
{
    public int? Id { get; set; }

    [AllowedImage]
    public IFormFile? ImageFile { get; set; }

    public IEnumerable<SelectListItem> Categories { get; set; } = Array.Empty<SelectListItem>();
}

public class AdminOrderDetailsViewModel
{
    public OrderDetailsDto Order { get; set; } = new();
    public IEnumerable<SelectListItem> Drivers { get; set; } = Array.Empty<SelectListItem>();
    public IReadOnlyList<OrderStatus> NextStatuses { get; set; } = Array.Empty<OrderStatus>();
}

public class DriverDetailsViewModel
{
    public DriverDto Driver { get; set; } = new();
    public IReadOnlyList<DeliveryDto> Deliveries { get; set; } = Array.Empty<DeliveryDto>();
}

public class DeliveryEditViewModel
{
    public DeliveryDto Delivery { get; set; } = new();
    public DeliveryUpdateDto Form { get; set; } = new();
    public IEnumerable<SelectListItem> Drivers { get; set; } = Array.Empty<SelectListItem>();
    public IReadOnlyList<string> TimeSlots { get; set; } = Array.Empty<string>();
}

public class ReportViewModel
{
    public ReportRequest Request { get; set; } = new();
    public ReportDto? Report { get; set; }
}

public class DriverDashboardViewModel
{
    public DriverDto Driver { get; set; } = new();
    public DriverStatsDto Stats { get; set; } = new();
    public IReadOnlyList<DeliveryDto> Active { get; set; } = Array.Empty<DeliveryDto>();
    public IReadOnlyList<DeliveryDto> History { get; set; } = Array.Empty<DeliveryDto>();
}
