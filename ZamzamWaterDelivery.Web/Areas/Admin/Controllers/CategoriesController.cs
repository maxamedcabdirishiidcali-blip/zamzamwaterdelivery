using Microsoft.AspNetCore.Mvc;
using ZamzamWaterDelivery.Application.DTOs;
using ZamzamWaterDelivery.Application.Interfaces;

namespace ZamzamWaterDelivery.Web.Areas.Admin.Controllers;

public class CategoriesController : AdminController
{
    private readonly ICategoryService _categories;

    public CategoriesController(ICategoryService categories) => _categories = categories;

    public async Task<IActionResult> Index(CancellationToken ct) => View(await _categories.GetAllAsync(ct));

    [HttpGet]
    public IActionResult Create() => View("Form", new CategoryUpsertDto());

    [HttpPost]
    public async Task<IActionResult> Create(CategoryUpsertDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View("Form", dto);
        var category = await _categories.CreateAsync(dto, ct);
        Success($"Category '{category.Name}' was added.");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var c = await _categories.GetByIdAsync(id, ct);
        ViewData["CategoryId"] = id;
        return View("Form", new CategoryUpsertDto { Name = c.Name, Description = c.Description });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, CategoryUpsertDto dto, CancellationToken ct)
    {
        ViewData["CategoryId"] = id;
        if (!ModelState.IsValid) return View("Form", dto);
        await _categories.UpdateAsync(id, dto, ct);
        Success("Category updated.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _categories.DeleteAsync(id, ct);
        Success("Category deleted.");
        return RedirectToAction(nameof(Index));
    }
}
