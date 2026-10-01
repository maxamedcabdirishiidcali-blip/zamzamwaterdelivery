using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ZamzamWaterDelivery.Application.Common;
using ZamzamWaterDelivery.Application.DTOs;
using ZamzamWaterDelivery.Application.Interfaces;
using ZamzamWaterDelivery.Domain.Enums;
using ZamzamWaterDelivery.Domain.Interfaces;

namespace ZamzamWaterDelivery.Application.Services;

public class ReportService : IReportService
{
    private readonly IUnitOfWork _uow;
    private readonly IEnumerable<IReportExporter> _exporters;
    private readonly CompanySettings _company;

    public ReportService(IUnitOfWork uow, IEnumerable<IReportExporter> exporters, IOptions<CompanySettings> company)
    {
        _uow = uow;
        _exporters = exporters;
        _company = company.Value;
    }

    public async Task<ReportDto> GenerateAsync(ReportRequest request, CancellationToken ct = default)
    {
        var (from, to) = ResolveRange(request);
        var end = to.AddDays(1);

        var report = request.Type switch
        {
            ReportType.DailySales => await DailySalesAsync(from, end, ct),
            ReportType.MonthlySales => await MonthlySalesAsync(from, end, ct),
            ReportType.Orders => await OrdersAsync(from, end, ct),
            ReportType.Customers => await CustomersAsync(from, end, ct),
            ReportType.ProductSales => await ProductSalesAsync(from, end, ct),
            ReportType.DriverDeliveries => await DriverDeliveriesAsync(from, end, ct),
            _ => throw new BusinessRuleException("Unknown report type.")
        };

        report.Subtitle = $"{_company.Name} | {from:dd MMM yyyy} - {to:dd MMM yyyy}";
        return report;
    }

    public async Task<ExportedFile> ExportAsync(ReportRequest request, ExportFormat format, CancellationToken ct = default)
    {
        var exporter = _exporters.FirstOrDefault(e => e.Format == format)
                       ?? throw new BusinessRuleException($"Export format {format} is not supported.");
        var report = await GenerateAsync(request, ct);
        var fileName = $"zamzam-{request.Type.ToString().ToLowerInvariant()}-report-{DateTime.Now:yyyyMMdd-HHmm}.{exporter.FileExtension}";
        return new ExportedFile(exporter.Export(report), exporter.ContentType, fileName);
    }

    private static (DateTime From, DateTime To) ResolveRange(ReportRequest request)
    {
        var to = (request.To ?? DateTime.Today).Date;
        var defaultFrom = request.Type == ReportType.MonthlySales
            ? new DateTime(to.Year, to.Month, 1).AddMonths(-11)
            : to.AddDays(-29);
        var from = (request.From ?? defaultFrom).Date;
        if (from > to) throw new BusinessRuleException("The start date must be before the end date.");
        return (from, to);
    }

    private string Money(decimal value) => _company.CurrencySymbol + value.ToString("N2", CultureInfo.InvariantCulture);

    private async Task<List<(DateTime Date, OrderStatus Status, decimal Total, int Items)>> LoadOrdersAsync(
        DateTime from, DateTime end, CancellationToken ct)
    {
        var rows = await _uow.Orders.QueryNoTracking()
            .Where(o => o.OrderDate >= from && o.OrderDate < end)
            .Select(o => new { o.OrderDate, o.Status, o.Total, Items = o.Items.Sum(i => i.Quantity) })
            .ToListAsync(ct);
        return rows.Select(r => (r.OrderDate, r.Status, r.Total, r.Items)).ToList();
    }

    private async Task<ReportDto> DailySalesAsync(DateTime from, DateTime end, CancellationToken ct)
    {
        var orders = await LoadOrdersAsync(from, end, ct);
        var report = SalesReport("Daily Sales Report", "Date");

        for (var day = from; day < end; day = day.AddDays(1))
            AddSalesRow(report, day.ToString("dd MMM yyyy"), orders.Where(o => o.Date.Date == day).ToList());

        AddSalesSummary(report, orders);
        return report;
    }

    private async Task<ReportDto> MonthlySalesAsync(DateTime from, DateTime end, CancellationToken ct)
    {
        var orders = await LoadOrdersAsync(from, end, ct);
        var report = SalesReport("Monthly Sales Report", "Month");

        for (var month = new DateTime(from.Year, from.Month, 1); month < end; month = month.AddMonths(1))
            AddSalesRow(report, month.ToString("MMMM yyyy"),
                orders.Where(o => o.Date.Year == month.Year && o.Date.Month == month.Month).ToList());

        AddSalesSummary(report, orders);
        return report;
    }

    private static ReportDto SalesReport(string title, string periodColumn) => new()
    {
        Title = title,
        Columns = new() { periodColumn, "Orders", "Items Sold", "Delivered", "Cancelled", "Gross Sales", "Revenue (Delivered)" }
    };

    private void AddSalesRow(ReportDto report, string period, List<(DateTime Date, OrderStatus Status, decimal Total, int Items)> orders)
    {
        var active = orders.Where(o => o.Status != OrderStatus.Cancelled).ToList();
        report.Rows.Add(new()
        {
            period,
            orders.Count.ToString(),
            active.Sum(o => o.Items).ToString(),
            orders.Count(o => o.Status == OrderStatus.Delivered).ToString(),
            orders.Count(o => o.Status == OrderStatus.Cancelled).ToString(),
            Money(active.Sum(o => o.Total)),
            Money(orders.Where(o => o.Status == OrderStatus.Delivered).Sum(o => o.Total))
        });
    }

    private void AddSalesSummary(ReportDto report, List<(DateTime Date, OrderStatus Status, decimal Total, int Items)> orders)
    {
        report.Summary["Total orders"] = orders.Count.ToString();
        report.Summary["Gross sales"] = Money(orders.Where(o => o.Status != OrderStatus.Cancelled).Sum(o => o.Total));
        report.Summary["Revenue (delivered)"] = Money(orders.Where(o => o.Status == OrderStatus.Delivered).Sum(o => o.Total));
        report.Summary["Cancelled orders"] = orders.Count(o => o.Status == OrderStatus.Cancelled).ToString();
    }

    private async Task<ReportDto> OrdersAsync(DateTime from, DateTime end, CancellationToken ct)
    {
        var orders = await _uow.Orders.QueryNoTracking()
            .Where(o => o.OrderDate >= from && o.OrderDate < end)
            .OrderByDescending(o => o.OrderDate)
            .Select(o => new
            {
                o.OrderNumber, o.OrderDate, o.CustomerName, o.PhoneNumber, o.Status, o.PaymentMethod, o.Total,
                PaymentStatus = o.Payment != null ? o.Payment.Status : PaymentStatus.Pending,
                Driver = o.Delivery != null && o.Delivery.Driver != null ? o.Delivery.Driver.FullName : "-"
            })
            .ToListAsync(ct);

        var report = new ReportDto
        {
            Title = "Order Report",
            Columns = new() { "Order #", "Date", "Customer", "Phone", "Status", "Payment", "Payment Status", "Driver", "Total" }
        };
        foreach (var o in orders)
            report.Rows.Add(new()
            {
                o.OrderNumber, o.OrderDate.ToString("dd MMM yyyy HH:mm"), o.CustomerName, o.PhoneNumber,
                o.Status.ToDisplayName(), o.PaymentMethod.ToDisplayName(), o.PaymentStatus.ToDisplayName(), o.Driver, Money(o.Total)
            });

        report.Summary["Orders"] = orders.Count.ToString();
        report.Summary["Order value (excl. cancelled)"] = Money(orders.Where(o => o.Status != OrderStatus.Cancelled).Sum(o => o.Total));
        foreach (var group in orders.GroupBy(o => o.Status).OrderBy(g => g.Key))
            report.Summary[group.Key.ToDisplayName()] = group.Count().ToString();
        return report;
    }

    private async Task<ReportDto> CustomersAsync(DateTime from, DateTime end, CancellationToken ct)
    {
        var customers = await _uow.Customers.QueryNoTracking()
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.FullName)
            .Select(c => new
            {
                c.FullName, c.Email, c.PhoneNumber, c.IsActive, c.CreatedAt,
                City = c.Addresses.OrderByDescending(a => a.IsDefault).Select(a => a.City).FirstOrDefault(),
                Orders = c.Orders.Count(o => o.OrderDate >= from && o.OrderDate < end),
                Spent = c.Orders.Where(o => o.OrderDate >= from && o.OrderDate < end && o.Status == OrderStatus.Delivered)
                    .Sum(o => (decimal?)o.Total) ?? 0,
                LifetimeOrders = c.Orders.Count
            })
            .ToListAsync(ct);

        var report = new ReportDto
        {
            Title = "Customer Report",
            Columns = new() { "Customer", "Email", "Phone", "City", "Orders (period)", "Spent (period)", "Lifetime Orders", "Status", "Joined" }
        };
        foreach (var c in customers)
            report.Rows.Add(new()
            {
                c.FullName, c.Email, c.PhoneNumber, c.City ?? "-", c.Orders.ToString(), Money(c.Spent),
                c.LifetimeOrders.ToString(), c.IsActive ? "Active" : "Inactive", c.CreatedAt.ToString("dd MMM yyyy")
            });

        report.Summary["Customers"] = customers.Count.ToString();
        report.Summary["Active customers"] = customers.Count(c => c.IsActive).ToString();
        report.Summary["New in period"] = customers.Count(c => c.CreatedAt >= from && c.CreatedAt < end).ToString();
        report.Summary["Customers who ordered"] = customers.Count(c => c.Orders > 0).ToString();
        return report;
    }

    private async Task<ReportDto> ProductSalesAsync(DateTime from, DateTime end, CancellationToken ct)
    {
        var products = await _uow.Products.QueryNoTracking()
            .Select(p => new
            {
                p.Name, Category = p.Category.Name, p.Price, p.StockQuantity, p.IsActive,
                Quantity = p.OrderItems.Where(i => i.Order.OrderDate >= from && i.Order.OrderDate < end && i.Order.Status != OrderStatus.Cancelled)
                    .Sum(i => (int?)i.Quantity) ?? 0,
                Orders = p.OrderItems.Count(i => i.Order.OrderDate >= from && i.Order.OrderDate < end && i.Order.Status != OrderStatus.Cancelled),
                Revenue = p.OrderItems.Where(i => i.Order.OrderDate >= from && i.Order.OrderDate < end && i.Order.Status != OrderStatus.Cancelled)
                    .Sum(i => (decimal?)i.LineTotal) ?? 0
            })
            .OrderByDescending(p => p.Quantity)
            .ToListAsync(ct);

        var report = new ReportDto
        {
            Title = "Product Sales Report",
            Columns = new() { "Product", "Category", "Price", "Orders", "Units Sold", "Sales", "Current Stock", "Status" }
        };
        foreach (var p in products)
            report.Rows.Add(new()
            {
                p.Name, p.Category, Money(p.Price), p.Orders.ToString(), p.Quantity.ToString(), Money(p.Revenue),
                p.StockQuantity.ToString(), p.IsActive ? "Active" : "Inactive"
            });

        report.Summary["Units sold"] = products.Sum(p => p.Quantity).ToString();
        report.Summary["Product sales"] = Money(products.Sum(p => p.Revenue));
        report.Summary["Best seller"] = products.FirstOrDefault(p => p.Quantity > 0)?.Name ?? "-";
        return report;
    }

    private async Task<ReportDto> DriverDeliveriesAsync(DateTime from, DateTime end, CancellationToken ct)
    {
        var drivers = await _uow.Drivers.QueryNoTracking()
            .OrderBy(d => d.FullName)
            .Select(d => new
            {
                d.FullName, d.PhoneNumber, d.VehicleType, d.VehicleNumber, d.Status,
                Assigned = d.Deliveries.Count(x => x.Order.OrderDate >= from && x.Order.OrderDate < end),
                Delivered = d.Deliveries.Count(x => x.Order.OrderDate >= from && x.Order.OrderDate < end && x.Status == DeliveryStatus.Delivered),
                Active = d.Deliveries.Count(x => x.Status == DeliveryStatus.Assigned || x.Status == DeliveryStatus.Accepted || x.Status == DeliveryStatus.OutForDelivery),
                Cancelled = d.Deliveries.Count(x => x.Order.OrderDate >= from && x.Order.OrderDate < end && x.Status == DeliveryStatus.Cancelled),
                Value = d.Deliveries.Where(x => x.Order.OrderDate >= from && x.Order.OrderDate < end && x.Status == DeliveryStatus.Delivered)
                    .Sum(x => (decimal?)x.Order.Total) ?? 0
            })
            .ToListAsync(ct);

        var report = new ReportDto
        {
            Title = "Driver Delivery Report",
            Columns = new() { "Driver", "Phone", "Vehicle", "Status", "Assigned", "Delivered", "Cancelled", "In Progress", "Delivered Value", "Success Rate" }
        };
        foreach (var d in drivers)
        {
            var rate = d.Assigned == 0 ? "-" : $"{d.Delivered * 100.0 / d.Assigned:0}%";
            report.Rows.Add(new()
            {
                d.FullName, d.PhoneNumber, $"{d.VehicleType} ({d.VehicleNumber})", d.Status.ToDisplayName(),
                d.Assigned.ToString(), d.Delivered.ToString(), d.Cancelled.ToString(), d.Active.ToString(), Money(d.Value), rate
            });
        }

        report.Summary["Drivers"] = drivers.Count.ToString();
        report.Summary["Deliveries completed"] = drivers.Sum(d => d.Delivered).ToString();
        report.Summary["Delivered value"] = Money(drivers.Sum(d => d.Value));
        return report;
    }
}
