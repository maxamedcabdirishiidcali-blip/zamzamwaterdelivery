using Microsoft.EntityFrameworkCore;
using ZamzamWaterDelivery.Application.Common;
using ZamzamWaterDelivery.Application.DTOs;
using ZamzamWaterDelivery.Application.Interfaces;
using ZamzamWaterDelivery.Application.Mappings;
using ZamzamWaterDelivery.Domain.Entities;
using ZamzamWaterDelivery.Domain.Interfaces;

namespace ZamzamWaterDelivery.Application.Services;

public class ProductService : IProductService
{
    private readonly IUnitOfWork _uow;

    public ProductService(IUnitOfWork uow) => _uow = uow;

    public async Task<PagedResult<ProductDto>> GetProductsAsync(ProductFilter filter, CancellationToken ct = default)
    {
        var query = _uow.Products.QueryNoTracking().Where(p => !p.IsDeleted);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(p => p.Name.Contains(term) || p.Description.Contains(term) || p.Category.Name.Contains(term));
        }
        if (filter.CategoryId.HasValue) query = query.Where(p => p.CategoryId == filter.CategoryId);
        if (filter.IsActive.HasValue) query = query.Where(p => p.IsActive == filter.IsActive);
        if (filter.InStockOnly) query = query.Where(p => p.StockQuantity > 0);

        query = filter.SortBy switch
        {
            "name" => query.OrderBy(p => p.Name),
            "price_asc" => query.OrderBy(p => p.Price),
            "price_desc" => query.OrderByDescending(p => p.Price),
            "stock" => query.OrderBy(p => p.StockQuantity),
            "newest" => query.OrderByDescending(p => p.CreatedAt),
            _ => query.OrderBy(p => p.Category.Name).ThenByDescending(p => p.Price)
        };

        return await query.Select(Projections.Product).ToPagedResultAsync(filter.Page, filter.PageSize, ct);
    }

    public async Task<IReadOnlyList<ProductDto>> GetActiveProductsAsync(CancellationToken ct = default) =>
        await _uow.Products.QueryNoTracking()
            .Where(p => p.IsActive && !p.IsDeleted)
            .OrderByDescending(p => p.Price)
            .Select(Projections.Product)
            .ToListAsync(ct);

    public async Task<ProductDto> GetByIdAsync(int id, CancellationToken ct = default) =>
        await _uow.Products.QueryNoTracking().Where(p => p.Id == id && !p.IsDeleted).Select(Projections.Product).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Product", id);

    public async Task<ProductDto> CreateAsync(ProductUpsertDto dto, CancellationToken ct = default)
    {
        await EnsureValidAsync(dto, null, ct);

        var product = new Product();
        Apply(product, dto);
        await _uow.Products.AddAsync(product, ct);
        await _uow.SaveChangesAsync(ct);
        return await GetByIdAsync(product.Id, ct);
    }

    public async Task<ProductDto> UpdateAsync(int id, ProductUpsertDto dto, CancellationToken ct = default)
    {
        var product = await FindAsync(id, ct);
        await EnsureValidAsync(dto, id, ct);

        Apply(product, dto);
        product.UpdatedAt = DateTime.Now;
        await _uow.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var product = await FindAsync(id, ct);

        // Products that appear in orders are archived so order history and reports stay intact.
        if (await _uow.OrderItems.QueryNoTracking().AnyAsync(i => i.ProductId == id, ct))
        {
            product.IsDeleted = true;
            product.IsActive = false;
            product.UpdatedAt = DateTime.Now;
            var cartItems = await _uow.CartItems.Query().Where(i => i.ProductId == id).ToListAsync(ct);
            foreach (var item in cartItems) _uow.CartItems.Remove(item);
            await _uow.SaveChangesAsync(ct);
            return false;
        }

        _uow.Products.Remove(product);
        await _uow.SaveChangesAsync(ct);
        return true;
    }

    public async Task SetActiveAsync(int id, bool isActive, CancellationToken ct = default)
    {
        var product = await FindAsync(id, ct);
        product.IsActive = isActive;
        product.UpdatedAt = DateTime.Now;
        await _uow.SaveChangesAsync(ct);
    }

    public async Task UpdateStockAsync(int id, int stockQuantity, CancellationToken ct = default)
    {
        if (stockQuantity < 0) throw new BusinessRuleException("Stock cannot be negative.");
        var product = await FindAsync(id, ct);
        product.StockQuantity = stockQuantity;
        product.UpdatedAt = DateTime.Now;
        await _uow.SaveChangesAsync(ct);
    }

    public async Task UpdatePriceAsync(int id, decimal price, CancellationToken ct = default)
    {
        if (price <= 0) throw new BusinessRuleException("Price must be greater than zero.");
        var product = await FindAsync(id, ct);
        product.Price = price;
        product.UpdatedAt = DateTime.Now;
        await _uow.SaveChangesAsync(ct);
    }

    private async Task<Product> FindAsync(int id, CancellationToken ct) =>
        await _uow.Products.Query().FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct)
        ?? throw new NotFoundException("Product", id);

    private async Task EnsureValidAsync(ProductUpsertDto dto, int? currentId, CancellationToken ct)
    {
        if (!await _uow.Categories.QueryNoTracking().AnyAsync(c => c.Id == dto.CategoryId && !c.IsDeleted, ct))
            throw new BusinessRuleException("The selected category does not exist.");

        var name = dto.Name.Trim();
        if (await _uow.Products.QueryNoTracking().AnyAsync(p => p.Name == name && p.Id != currentId && !p.IsDeleted, ct))
            throw new BusinessRuleException($"A product named '{name}' already exists.");
    }

    private static void Apply(Product product, ProductUpsertDto dto)
    {
        product.Name = dto.Name.Trim();
        product.Description = dto.Description.Trim();
        product.Price = dto.Price;
        product.StockQuantity = dto.StockQuantity;
        product.CategoryId = dto.CategoryId;
        product.IsActive = dto.IsActive;
        if (!string.IsNullOrWhiteSpace(dto.ImageUrl)) product.ImageUrl = dto.ImageUrl;
    }
}
