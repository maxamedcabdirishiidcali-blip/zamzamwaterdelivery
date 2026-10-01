using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using ZamzamWaterDelivery.Application.DTOs;
using ZamzamWaterDelivery.Application.Interfaces;
using ZamzamWaterDelivery.Web.Models;

namespace ZamzamWaterDelivery.Web.Areas.Admin.Controllers;

public class ProductsController : AdminController
{
    private readonly IProductService _products;
    private readonly ICategoryService _categories;
    private readonly IFileStorageService _files;

    public ProductsController(IProductService products, ICategoryService categories, IFileStorageService files)
    {
        _products = products;
        _categories = categories;
        _files = files;
    }

    public async Task<IActionResult> Index([FromQuery] ProductFilter filter, CancellationToken ct)
    {
        filter.SortBy ??= "name";
        return View(new ListViewModel<ProductDto, ProductFilter>
        {
            Filter = filter,
            Result = await _products.GetProductsAsync(filter, ct),
            Lookup = await CategoryOptionsAsync(filter.CategoryId, ct)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct) =>
        View("Form", new ProductFormViewModel { Categories = await CategoryOptionsAsync(null, ct) });

    [HttpPost]
    public async Task<IActionResult> Create(ProductFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return await FormAsync(model, ct);

        model.ImageUrl = await SaveImageAsync(model.ImageFile, ct) ?? model.ImageUrl ?? "/images/products/bottle-20l.svg";
        var product = await _products.CreateAsync(model, ct);
        Success($"'{product.Name}' was added.");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var p = await _products.GetByIdAsync(id, ct);
        return View("Form", new ProductFormViewModel
        {
            Id = p.Id, Name = p.Name, Description = p.Description, Price = p.Price, StockQuantity = p.StockQuantity,
            CategoryId = p.CategoryId, IsActive = p.IsActive, ImageUrl = p.ImageUrl,
            Categories = await CategoryOptionsAsync(p.CategoryId, ct)
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, ProductFormViewModel model, CancellationToken ct)
    {
        model.Id = id;
        if (!ModelState.IsValid) return await FormAsync(model, ct);

        var existing = await _products.GetByIdAsync(id, ct);
        var newImage = await SaveImageAsync(model.ImageFile, ct);
        model.ImageUrl = newImage ?? existing.ImageUrl;

        var product = await _products.UpdateAsync(id, model, ct);
        if (newImage is not null) _files.Delete(existing.ImageUrl);

        Success($"'{product.Name}' was updated.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var product = await _products.GetByIdAsync(id, ct);
        if (await _products.DeleteAsync(id, ct))
        {
            _files.Delete(product.ImageUrl);
            Success($"'{product.Name}' was deleted.");
        }
        else
        {
            Success($"'{product.Name}' was deleted. It is kept in the archive because past orders include it.");
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ToggleActive(int id, bool isActive, CancellationToken ct)
    {
        await _products.SetActiveAsync(id, isActive, ct);
        Success(isActive ? "Product activated." : "Product deactivated - customers can no longer order it.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> UpdateStock(int id, StockUpdateDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) { Error("Stock must be zero or more."); return RedirectToAction(nameof(Index)); }
        await _products.UpdateStockAsync(id, dto.StockQuantity, ct);
        Success("Stock updated.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> UpdatePrice(int id, PriceUpdateDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) { Error("Enter a valid price."); return RedirectToAction(nameof(Index)); }
        await _products.UpdatePriceAsync(id, dto.Price, ct);
        Success("Price updated.");
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> FormAsync(ProductFormViewModel model, CancellationToken ct)
    {
        model.Categories = await CategoryOptionsAsync(model.CategoryId, ct);
        return View("Form", model);
    }

    private async Task<string?> SaveImageAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return null;
        await using var stream = file.OpenReadStream();
        return await _files.SaveAsync(stream, file.FileName, "products", ct);
    }

    private async Task<IEnumerable<SelectListItem>> CategoryOptionsAsync(int? selected, CancellationToken ct) =>
        (await _categories.GetAllAsync(ct)).Select(c => new SelectListItem(c.Name, c.Id.ToString(), c.Id == selected));
}
