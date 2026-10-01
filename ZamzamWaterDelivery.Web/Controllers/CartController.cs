using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZamzamWaterDelivery.Application.DTOs;
using ZamzamWaterDelivery.Application.Interfaces;
using ZamzamWaterDelivery.Domain.Common;
using ZamzamWaterDelivery.Web.Infrastructure;

namespace ZamzamWaterDelivery.Web.Controllers;

[Authorize(Roles = Roles.Customer)]
public class CartController : AppController
{
    private readonly ICartService _cart;

    public CartController(ICartService cart) => _cart = cart;

    public async Task<IActionResult> Index(CancellationToken ct) => View(await _cart.GetCartAsync(UserId, ct));

    [HttpPost]
    public async Task<IActionResult> Add(AddToCartDto dto, string? returnUrl, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequestResult("Please choose a valid quantity.");

        var summary = await _cart.AddItemAsync(UserId, dto.ProductId, dto.Quantity, ct);
        if (Request.IsAjax()) return Json(new { success = true, message = "Added to your cart.", summary });

        Success("Added to your cart.");
        return RedirectToLocal(returnUrl, "Index");
    }

    [HttpPost]
    public async Task<IActionResult> Update(UpdateCartItemDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequestResult("Please choose a valid quantity.");

        var summary = await _cart.UpdateQuantityAsync(UserId, dto.CartItemId, dto.Quantity, ct);
        if (Request.IsAjax()) return Json(new { success = true, summary });
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Remove(int id, CancellationToken ct)
    {
        var summary = await _cart.RemoveItemAsync(UserId, id, ct);
        if (Request.IsAjax()) return Json(new { success = true, message = "Item removed.", summary });

        Success("Item removed from your cart.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Clear(CancellationToken ct)
    {
        await _cart.ClearAsync(UserId, ct);
        Info("Your cart has been emptied.");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Count(CancellationToken ct) =>
        Json(new { count = await _cart.GetItemCountAsync(UserId, ct) });

    private IActionResult BadRequestResult(string message)
    {
        if (Request.IsAjax()) return BadRequest(new { success = false, message });
        Error(message);
        return RedirectToAction(nameof(Index));
    }
}
