using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZamzamWaterDelivery.Application.Interfaces;
using ZamzamWaterDelivery.Domain.Common;
using ZamzamWaterDelivery.Domain.Enums;
using ZamzamWaterDelivery.Web.Infrastructure;
using ZamzamWaterDelivery.Web.Models;

namespace ZamzamWaterDelivery.Web.Areas.Driver.Controllers;

[Area("Driver")]
[Authorize(Roles = Roles.Driver)]
public class DashboardController : AppController
{
    private readonly IDriverService _drivers;
    private readonly IDeliveryService _deliveries;

    public DashboardController(IDriverService drivers, IDeliveryService deliveries)
    {
        _drivers = drivers;
        _deliveries = deliveries;
    }

    public async Task<IActionResult> Index(CancellationToken ct) => View(new DriverDashboardViewModel
    {
        Driver = await _drivers.GetByUserIdAsync(UserId, ct),
        Stats = await _drivers.GetStatsAsync(UserId, ct),
        Active = await _deliveries.GetDriverDeliveriesAsync(UserId, activeOnly: true, ct),
        History = await _deliveries.GetDriverDeliveriesAsync(UserId, activeOnly: false, ct)
    });

    public async Task<IActionResult> Delivery(int id, CancellationToken ct) =>
        View(await _deliveries.GetDriverDeliveryAsync(UserId, id, ct));

    [HttpPost]
    public async Task<IActionResult> Accept(int id, CancellationToken ct)
    {
        await _deliveries.AcceptAsync(UserId, id, ct);
        Success("Delivery accepted. Load the water and start when ready.");
        return RedirectToAction(nameof(Delivery), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Start(int id, CancellationToken ct)
    {
        await _deliveries.StartAsync(UserId, id, ct);
        Success("Marked as Out for Delivery. The customer has been notified.");
        return RedirectToAction(nameof(Delivery), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Complete(int id, CancellationToken ct)
    {
        await _deliveries.CompleteAsync(UserId, id, ct);
        Success("Great job! The order is marked as delivered.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> SetStatus(DriverStatus status, CancellationToken ct)
    {
        await _drivers.SetMyStatusAsync(UserId, status, ct);
        Success($"You are now {status}.");
        return RedirectToAction(nameof(Index));
    }
}
