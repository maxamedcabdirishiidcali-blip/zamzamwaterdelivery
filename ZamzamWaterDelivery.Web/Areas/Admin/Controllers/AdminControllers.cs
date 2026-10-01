using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using ZamzamWaterDelivery.Application.Interfaces;
using ZamzamWaterDelivery.Domain.Common;
using ZamzamWaterDelivery.Web.Infrastructure;

namespace ZamzamWaterDelivery.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = Roles.Admin)]
public abstract class AdminController : AppController
{
    /// <summary>Drivers that can take new deliveries, formatted for a dropdown.</summary>
    protected static async Task<IEnumerable<SelectListItem>> DriverOptionsAsync(IDriverService drivers, int? selected, CancellationToken ct) =>
        (await drivers.GetAssignableDriversAsync(ct)).Select(d =>
            new SelectListItem($"{d.FullName} - {d.Status} ({d.ActiveDeliveries} active)", d.Id.ToString(), d.Id == selected));
}

public class DashboardController : AdminController
{
    private readonly IDashboardService _dashboard;

    public DashboardController(IDashboardService dashboard) => _dashboard = dashboard;

    public async Task<IActionResult> Index(CancellationToken ct) => View(await _dashboard.GetStatsAsync(ct));

    [HttpGet]
    public async Task<IActionResult> Charts(CancellationToken ct) => Json(await _dashboard.GetChartsAsync(ct));
}
