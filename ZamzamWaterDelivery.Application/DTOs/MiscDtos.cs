using System.ComponentModel.DataAnnotations;
using ZamzamWaterDelivery.Application.Validators;
using ZamzamWaterDelivery.Domain.Enums;

namespace ZamzamWaterDelivery.Application.DTOs;

public class NotificationDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public bool IsRead { get; set; }
    public int? OrderId { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Message handed to external notification channels (SMS, WhatsApp, e-mail).</summary>
public record NotificationMessage(
    string UserId,
    string? PhoneNumber,
    string? Email,
    string Title,
    string Message,
    NotificationType Type,
    int? OrderId);

public class DashboardStatsDto
{
    public int TotalOrders { get; set; }
    public int PendingOrders { get; set; }
    public int CompletedOrders { get; set; }
    public int CancelledOrders { get; set; }
    public int TotalCustomers { get; set; }
    public decimal TotalRevenue { get; set; }
    public int ActiveDrivers { get; set; }
    public int TodaysOrders { get; set; }
    public decimal TodaysSales { get; set; }
    public List<OrderDto> RecentOrders { get; set; } = new();
    public List<ProductDto> LowStockProducts { get; set; } = new();
}

public class ChartSeriesDto
{
    public List<string> Labels { get; set; } = new();
    public List<decimal> Values { get; set; } = new();
}

public class DashboardChartsDto
{
    public ChartSeriesDto DailySales { get; set; } = new();
    public ChartSeriesDto MonthlySales { get; set; } = new();
    public ChartSeriesDto OrdersByStatus { get; set; } = new();
    public ChartSeriesDto PopularProducts { get; set; } = new();
}

public class ReportRequest
{
    [DefinedEnum]
    [Display(Name = "Report")]
    public ReportType Type { get; set; } = ReportType.DailySales;

    [DataType(DataType.Date)]
    public DateTime? From { get; set; }

    [DataType(DataType.Date)]
    public DateTime? To { get; set; }
}

public class ReportDto
{
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; } = DateTime.Now;
    public List<string> Columns { get; set; } = new();
    public List<List<string>> Rows { get; set; } = new();
    public Dictionary<string, string> Summary { get; set; } = new();
}

public record ExportedFile(byte[] Content, string ContentType, string FileName);
