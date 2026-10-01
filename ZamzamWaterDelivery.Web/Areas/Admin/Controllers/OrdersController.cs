using Microsoft.AspNetCore.Mvc;
using ZamzamWaterDelivery.Application.DTOs;
using ZamzamWaterDelivery.Application.Interfaces;
using ZamzamWaterDelivery.Domain.Common;
using ZamzamWaterDelivery.Domain.Enums;
using ZamzamWaterDelivery.Web.Models;

namespace ZamzamWaterDelivery.Web.Areas.Admin.Controllers;

public class OrdersController : AdminController
{
    private readonly IOrderService _orders;
    private readonly IDriverService _drivers;

    public OrdersController(IOrderService orders, IDriverService drivers)
    {
        _orders = orders;
        _drivers = drivers;
    }

    public async Task<IActionResult> Index([FromQuery] OrderFilter filter, CancellationToken ct)
    {
        var drivers = await _drivers.SearchAsync(new DriverFilter { PageSize = 100 }, ct);
        return View(new ListViewModel<OrderDto, OrderFilter>
        {
            Filter = filter,
            Result = await _orders.SearchAsync(filter, ct),
            Lookup = drivers.Items.Select(d => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(
                d.FullName, d.Id.ToString(), d.Id == filter.DriverId))
        });
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var order = await _orders.GetDetailsAsync(id, ct);
        return View(new AdminOrderDetailsViewModel
        {
            Order = order,
            Drivers = await DriverOptionsAsync(_drivers, order.Delivery?.DriverId, ct),
            NextStatuses = OrderStatusRules.NextStatuses(order.Status).Where(s => s != OrderStatus.Cancelled).ToList()
        });
    }

    [HttpPost]
    public async Task<IActionResult> Confirm(int id, CancellationToken ct)
    {
        await _orders.ChangeStatusAsync(id, OrderStatus.Confirmed, ct: ct);
        Success("Order confirmed and the customer was notified.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> AssignDriver(int id, AssignDriverDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) { Error("Please select a driver."); return RedirectToAction(nameof(Details), new { id }); }
        await _orders.AssignDriverAsync(id, dto.DriverId, ct);
        Success("Driver assigned. The driver and customer were notified.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> ChangeStatus(int id, UpdateOrderStatusDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) { Error("Invalid status."); return RedirectToAction(nameof(Details), new { id }); }
        await _orders.ChangeStatusAsync(id, dto.Status, dto.Reason, ct);
        Success("Order status updated.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Cancel(int id, CancelOrderDto dto, CancellationToken ct)
    {
        await _orders.CancelAsync(id, dto.Reason, ct);
        Success("Order cancelled and stock restored.");
        return RedirectToAction(nameof(Details), new { id });
    }
}
