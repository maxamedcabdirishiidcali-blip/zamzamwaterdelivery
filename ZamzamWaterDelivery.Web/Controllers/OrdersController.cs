using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZamzamWaterDelivery.Application.Interfaces;
using ZamzamWaterDelivery.Domain.Common;
using ZamzamWaterDelivery.Web.Infrastructure;

namespace ZamzamWaterDelivery.Web.Controllers;

[Authorize(Roles = Roles.Customer)]
public class OrdersController : AppController
{
    private readonly IOrderService _orders;

    public OrdersController(IOrderService orders) => _orders = orders;

    public async Task<IActionResult> Index(CancellationToken ct) => View(await _orders.GetCustomerOrdersAsync(UserId, ct));

    /// <summary>Order tracking page with the visual progress indicator.</summary>
    public async Task<IActionResult> Details(int id, CancellationToken ct) =>
        View(await _orders.GetCustomerOrderDetailsAsync(UserId, id, ct));

    [HttpGet]
    public async Task<IActionResult> Track(string? orderNumber, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
        {
            Error("Please enter an order number.");
            return RedirectToAction(nameof(Index));
        }
        var order = await _orders.TrackAsync(UserId, orderNumber, ct);
        return RedirectToAction(nameof(Details), new { id = order.Id });
    }

    [HttpPost]
    public async Task<IActionResult> Cancel(int id, string? reason, CancellationToken ct)
    {
        await _orders.CancelByCustomerAsync(UserId, id, reason, ct);
        Success("Your order has been cancelled.");
        return RedirectToAction(nameof(Details), new { id });
    }
}
