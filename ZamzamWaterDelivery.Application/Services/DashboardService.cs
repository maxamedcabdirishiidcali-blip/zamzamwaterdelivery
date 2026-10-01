using Microsoft.EntityFrameworkCore;
using ZamzamWaterDelivery.Application.Common;
using ZamzamWaterDelivery.Application.DTOs;
using ZamzamWaterDelivery.Application.Interfaces;
using ZamzamWaterDelivery.Application.Mappings;
using ZamzamWaterDelivery.Domain.Enums;
using ZamzamWaterDelivery.Domain.Interfaces;

namespace ZamzamWaterDelivery.Application.Services;

public class DashboardService : IDashboardService
{
    private const int LowStockThreshold = 20;
    private const int DailyChartDays = 14;
    private const int MonthlyChartMonths = 12;

    private readonly IUnitOfWork _uow;

    public DashboardService(IUnitOfWork uow) => _uow = uow;

    public async Task<DashboardStatsDto> GetStatsAsync(CancellationToken ct = default)
    {
        var orders = _uow.Orders.QueryNoTracking();
        var today = DateTime.Today;

        return new DashboardStatsDto
        {
            TotalOrders = await orders.CountAsync(ct),
            PendingOrders = await orders.CountAsync(o => o.Status == OrderStatus.Pending, ct),
            CompletedOrders = await orders.CountAsync(o => o.Status == OrderStatus.Delivered, ct),
            CancelledOrders = await orders.CountAsync(o => o.Status == OrderStatus.Cancelled, ct),
            TotalCustomers = await _uow.Customers.QueryNoTracking().CountAsync(c => !c.IsDeleted, ct),
            TotalRevenue = await orders.Where(o => o.Status == OrderStatus.Delivered).SumAsync(o => (decimal?)o.Total, ct) ?? 0,
            ActiveDrivers = await _uow.Drivers.QueryNoTracking().CountAsync(d => d.User.IsActive && d.Status != DriverStatus.Offline, ct),
            TodaysOrders = await orders.CountAsync(o => o.OrderDate >= today, ct),
            TodaysSales = await orders.Where(o => o.OrderDate >= today && o.Status != OrderStatus.Cancelled)
                .SumAsync(o => (decimal?)o.Total, ct) ?? 0,
            RecentOrders = await orders.OrderByDescending(o => o.OrderDate).Take(8).Select(Projections.Order).ToListAsync(ct),
            LowStockProducts = await _uow.Products.QueryNoTracking()
                .Where(p => p.IsActive && p.StockQuantity <= LowStockThreshold)
                .OrderBy(p => p.StockQuantity).Take(5)
                .Select(Projections.Product).ToListAsync(ct)
        };
    }

    public async Task<DashboardChartsDto> GetChartsAsync(CancellationToken ct = default)
    {
        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1).AddMonths(-(MonthlyChartMonths - 1));
        var dayStart = today.AddDays(-(DailyChartDays - 1));
        var rangeStart = dayStart < monthStart ? dayStart : monthStart;

        var sales = await _uow.Orders.QueryNoTracking()
            .Where(o => o.OrderDate >= rangeStart && o.Status != OrderStatus.Cancelled)
            .Select(o => new { o.OrderDate, o.Total })
            .ToListAsync(ct);

        var daily = new ChartSeriesDto();
        for (var day = dayStart; day <= today; day = day.AddDays(1))
        {
            daily.Labels.Add(day.ToString("dd MMM"));
            daily.Values.Add(sales.Where(s => s.OrderDate.Date == day).Sum(s => s.Total));
        }

        var monthly = new ChartSeriesDto();
        for (var month = monthStart; month <= today; month = month.AddMonths(1))
        {
            monthly.Labels.Add(month.ToString("MMM yyyy"));
            monthly.Values.Add(sales.Where(s => s.OrderDate.Year == month.Year && s.OrderDate.Month == month.Month).Sum(s => s.Total));
        }

        var statusCounts = await _uow.Orders.QueryNoTracking()
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var byStatus = new ChartSeriesDto();
        foreach (var status in Enum.GetValues<OrderStatus>())
        {
            byStatus.Labels.Add(status.ToDisplayName());
            byStatus.Values.Add(statusCounts.FirstOrDefault(s => s.Status == status)?.Count ?? 0);
        }

        var popular = await _uow.OrderItems.QueryNoTracking()
            .Where(i => i.Order.Status != OrderStatus.Cancelled)
            .GroupBy(i => i.ProductName)
            .Select(g => new { Name = g.Key, Quantity = g.Sum(x => x.Quantity) })
            .OrderByDescending(x => x.Quantity)
            .Take(5)
            .ToListAsync(ct);

        return new DashboardChartsDto
        {
            DailySales = daily,
            MonthlySales = monthly,
            OrdersByStatus = byStatus,
            PopularProducts = new ChartSeriesDto
            {
                Labels = popular.Select(p => p.Name).ToList(),
                Values = popular.Select(p => (decimal)p.Quantity).ToList()
            }
        };
    }
}
