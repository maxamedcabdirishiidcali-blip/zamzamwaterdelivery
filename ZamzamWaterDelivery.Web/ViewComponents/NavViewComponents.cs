using Microsoft.AspNetCore.Mvc;
using ZamzamWaterDelivery.Application.Interfaces;
using ZamzamWaterDelivery.Domain.Common;
using ZamzamWaterDelivery.Web.Infrastructure;

namespace ZamzamWaterDelivery.Web.ViewComponents;

public class CartBadgeViewComponent : ViewComponent
{
    private readonly ICartService _cart;

    public CartBadgeViewComponent(ICartService cart) => _cart = cart;

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var count = UserClaimsPrincipal.IsInRole(Roles.Customer)
            ? await _cart.GetItemCountAsync(UserClaimsPrincipal.GetUserId())
            : 0;
        return View(count);
    }
}

public class NotificationBellViewComponent : ViewComponent
{
    private readonly INotificationService _notifications;

    public NotificationBellViewComponent(INotificationService notifications) => _notifications = notifications;

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var userId = UserClaimsPrincipal.GetUserId();
        ViewData["Unread"] = await _notifications.GetUnreadCountAsync(userId);
        return View(await _notifications.GetForUserAsync(userId, 6));
    }
}
