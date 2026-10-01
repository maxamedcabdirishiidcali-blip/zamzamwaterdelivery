using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ZamzamWaterDelivery.Application.Common;
using ZamzamWaterDelivery.Application.DTOs;
using ZamzamWaterDelivery.Application.Interfaces;
using ZamzamWaterDelivery.Domain.Common;
using ZamzamWaterDelivery.Web.Infrastructure;
using ZamzamWaterDelivery.Web.Models;

namespace ZamzamWaterDelivery.Web.Controllers;

[Authorize(Roles = Roles.Customer)]
public class CheckoutController : AppController
{
    private readonly ICartService _cart;
    private readonly IOrderService _orders;
    private readonly IPaymentService _payments;
    private readonly DeliverySettings _delivery;

    public CheckoutController(ICartService cart, IOrderService orders, IPaymentService payments, IOptions<DeliverySettings> delivery)
    {
        _cart = cart;
        _orders = orders;
        _payments = payments;
        _delivery = delivery.Value;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var model = await BuildModelAsync(await _orders.GetCheckoutDefaultsAsync(UserId, ct), ct);
        if (model.Cart.IsEmpty)
        {
            Info("Your cart is empty. Add some water first!");
            return RedirectToAction("Index", "Products");
        }
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Index([Bind(Prefix = nameof(CheckoutViewModel.Checkout))] CheckoutDto checkout, CancellationToken ct)
    {
        if (!_payments.AvailableMethods.Contains(checkout.PaymentMethod))
            ModelState.AddModelError("Checkout.PaymentMethod", "This payment method is not available yet.");
        if (!ModelState.IsValid) return View(await BuildModelAsync(checkout, ct));

        var order = await _orders.PlaceOrderFromCartAsync(UserId, checkout, ct);
        return RedirectToAction(nameof(Success), new { id = order.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Success(int id, CancellationToken ct) =>
        View(await _orders.GetCustomerOrderDetailsAsync(UserId, id, ct));

    private async Task<CheckoutViewModel> BuildModelAsync(CheckoutDto checkout, CancellationToken ct) => new()
    {
        Checkout = checkout,
        Cart = await _cart.GetCartAsync(UserId, ct),
        TimeSlots = _delivery.AvailableTimeSlots,
        AvailableMethods = _payments.AvailableMethods
    };
}
