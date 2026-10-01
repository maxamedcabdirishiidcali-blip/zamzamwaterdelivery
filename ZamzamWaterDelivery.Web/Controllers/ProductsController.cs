using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using ZamzamWaterDelivery.Application.DTOs;
using ZamzamWaterDelivery.Application.Interfaces;
using ZamzamWaterDelivery.Web.Infrastructure;
using ZamzamWaterDelivery.Web.Models;

namespace ZamzamWaterDelivery.Web.Controllers;

public class ProductsController : AppController
{
    private readonly IProductService _products;
    private readonly ICategoryService _categories;

    public ProductsController(IProductService products, ICategoryService categories)
    {
        _products = products;
        _categories = categories;
    }

    public async Task<IActionResult> Index([FromQuery] ProductFilter filter, CancellationToken ct)
    {
        filter.IsActive = true;
        filter.PageSize = 12;

        var categories = await _categories.GetAllAsync(ct);
        return View(new ListViewModel<ProductDto, ProductFilter>
        {
            Filter = filter,
            Result = await _products.GetProductsAsync(filter, ct),
            Lookup = categories.Select(c => new SelectListItem(c.Name, c.Id.ToString(), c.Id == filter.CategoryId))
        });
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var product = await _products.GetByIdAsync(id, ct);
        if (!product.IsActive) return View("NotFound");
        return View(product);
    }
}
