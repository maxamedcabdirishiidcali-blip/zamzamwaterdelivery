using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ZamzamWaterDelivery.Application.Common;
using ZamzamWaterDelivery.Application.Interfaces;
using ZamzamWaterDelivery.Web.Infrastructure;
using ZamzamWaterDelivery.Web.Models;

namespace ZamzamWaterDelivery.Web.Controllers;

public class HomeController : AppController
{
    private readonly IProductService _products;
    private readonly DeliverySettings _delivery;

    public HomeController(IProductService products, IOptions<DeliverySettings> delivery)
    {
        _products = products;
        _delivery = delivery.Value;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (User.IsInRole(Domain.Common.Roles.Admin)) return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
        if (User.IsInRole(Domain.Common.Roles.Driver)) return RedirectToAction("Index", "Dashboard", new { area = "Driver" });

        return View(new HomeViewModel
        {
            Products = await _products.GetActiveProductsAsync(ct),
            DeliveryFee = _delivery.BaseFee,
            FreeDeliveryThreshold = _delivery.FreeDeliveryThreshold
        });
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });

    [Route("Home/StatusCode/{code:int}")]
    public new IActionResult StatusCode(int code)
    {
        if (code == StatusCodes.Status404NotFound) return View("NotFound");
        return View("Error", new ErrorViewModel { StatusCode = code, RequestId = HttpContext.TraceIdentifier });
    }
}
