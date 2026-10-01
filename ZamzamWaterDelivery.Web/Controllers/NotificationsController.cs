using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZamzamWaterDelivery.Application.Interfaces;
using ZamzamWaterDelivery.Domain.Common;
using ZamzamWaterDelivery.Web.Infrastructure;

namespace ZamzamWaterDelivery.Web.Controllers;

[Authorize]
public class NotificationsController : AppController
{
    private readonly INotificationService _notifications;

    public NotificationsController(INotificationService notifications) => _notifications = notifications;

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Layout"] = LayoutForRole();
        return View(await _notifications.GetForUserAsync(UserId, 100, ct));
    }

    /// <summary>Marks the notification as read and opens the related order for the user's role.</summary>
    public async Task<IActionResult> Open(int id, int? orderId, CancellationToken ct)
    {
        await _notifications.MarkAsReadAsync(UserId, id, ct);
        if (orderId is null) return RedirectToAction(nameof(Index));

        if (User.IsInRole(Roles.Admin)) return RedirectToAction("Details", "Orders", new { area = "Admin", id = orderId });
        if (User.IsInRole(Roles.Driver)) return RedirectToAction("Index", "Dashboard", new { area = "Driver" });
        return RedirectToAction("Details", "Orders", new { id = orderId });
    }

    [HttpPost]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await _notifications.MarkAllAsReadAsync(UserId, ct);
        if (Request.IsAjax()) return Json(new { success = true });
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> UnreadCount(CancellationToken ct) =>
        Json(new { count = await _notifications.GetUnreadCountAsync(UserId, ct) });

    private string LayoutForRole() =>
        User.IsInRole(Roles.Admin) ? "~/Areas/Admin/Views/Shared/_AdminLayout.cshtml"
        : User.IsInRole(Roles.Driver) ? "~/Areas/Driver/Views/Shared/_DriverLayout.cshtml"
        : "_Layout";
}
