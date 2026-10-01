using Microsoft.EntityFrameworkCore;
using ZamzamWaterDelivery.Application.Common;
using ZamzamWaterDelivery.Application.DTOs;
using ZamzamWaterDelivery.Application.Interfaces;
using ZamzamWaterDelivery.Domain.Entities;
using ZamzamWaterDelivery.Domain.Interfaces;

namespace ZamzamWaterDelivery.Application.Services;

public class CategoryService : ICategoryService
{
    private readonly IUnitOfWork _uow;

    public CategoryService(IUnitOfWork uow) => _uow = uow;

    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken ct = default) =>
        await _uow.Categories.QueryNoTracking()
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                ProductCount = c.Products.Count(p => !p.IsDeleted)
            })
            .ToListAsync(ct);

    public async Task<CategoryDto> GetByIdAsync(int id, CancellationToken ct = default) =>
        (await GetAllAsync(ct)).FirstOrDefault(c => c.Id == id) ?? throw new NotFoundException("Category", id);

    public async Task<CategoryDto> CreateAsync(CategoryUpsertDto dto, CancellationToken ct = default)
    {
        await EnsureUniqueAsync(dto.Name, null, ct);
        var category = new Category { Name = dto.Name.Trim(), Description = dto.Description?.Trim() };
        await _uow.Categories.AddAsync(category, ct);
        await _uow.SaveChangesAsync(ct);
        return new CategoryDto { Id = category.Id, Name = category.Name, Description = category.Description };
    }

    public async Task<CategoryDto> UpdateAsync(int id, CategoryUpsertDto dto, CancellationToken ct = default)
    {
        var category = await FindAsync(id, ct);
        await EnsureUniqueAsync(dto.Name, id, ct);
        category.Name = dto.Name.Trim();
        category.Description = dto.Description?.Trim();
        category.UpdatedAt = DateTime.Now;
        await _uow.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var category = await FindAsync(id, ct);

        var activeProducts = await _uow.Products.QueryNoTracking().CountAsync(p => p.CategoryId == id && !p.IsDeleted, ct);
        if (activeProducts > 0)
            throw new BusinessRuleException(
                $"Category '{category.Name}' still contains {activeProducts} product(s). Delete them or move them to another category first.");

        // Archived products still reference the category, so keep it (hidden) for their history.
        if (await _uow.Products.QueryNoTracking().AnyAsync(p => p.CategoryId == id, ct))
        {
            category.IsDeleted = true;
            category.UpdatedAt = DateTime.Now;
            await _uow.SaveChangesAsync(ct);
            return false;
        }

        _uow.Categories.Remove(category);
        await _uow.SaveChangesAsync(ct);
        return true;
    }

    private async Task<Category> FindAsync(int id, CancellationToken ct) =>
        await _uow.Categories.Query().FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct)
        ?? throw new NotFoundException("Category", id);

    private async Task EnsureUniqueAsync(string name, int? currentId, CancellationToken ct)
    {
        var trimmed = name.Trim();
        if (await _uow.Categories.QueryNoTracking().AnyAsync(c => c.Name == trimmed && c.Id != currentId && !c.IsDeleted, ct))
            throw new BusinessRuleException($"A category named '{trimmed}' already exists.");
    }
}
