using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ZamzamWaterDelivery.Application.Common;
using ZamzamWaterDelivery.Application.DTOs;
using ZamzamWaterDelivery.Application.Interfaces;
using ZamzamWaterDelivery.Domain.Entities;
using ZamzamWaterDelivery.Domain.Interfaces;

namespace ZamzamWaterDelivery.Application.Services;

public class CartService : ICartService
{
    private const int MaxQuantityPerItem = 500;

    private readonly IUnitOfWork _uow;
    private readonly DeliverySettings _delivery;

    public CartService(IUnitOfWork uow, IOptions<DeliverySettings> delivery)
    {
        _uow = uow;
        _delivery = delivery.Value;
    }

    public async Task<CartDto> GetCartAsync(string userId, CancellationToken ct = default)
    {
        var items = await _uow.CartItems.QueryNoTracking()
            .Where(i => i.Cart.Customer.UserId == userId)
            .OrderBy(i => i.CreatedAt)
            .Select(i => new CartItemDto
            {
                Id = i.Id,
                ProductId = i.ProductId,
                ProductName = i.Product.Name,
                ImageUrl = i.Product.ImageUrl,
                UnitPrice = i.Product.Price,
                Quantity = i.Quantity,
                AvailableStock = i.Product.StockQuantity,
                IsAvailable = i.Product.IsActive && i.Product.StockQuantity >= i.Quantity
            })
            .ToListAsync(ct);

        var subTotal = items.Sum(i => i.LineTotal);
        var fee = _delivery.CalculateFee(subTotal);
        return new CartDto
        {
            Items = items,
            SubTotal = subTotal,
            DeliveryFee = fee,
            Total = subTotal + fee,
            FreeDeliveryThreshold = _delivery.FreeDeliveryThreshold
        };
    }

    public async Task<CartSummaryDto> AddItemAsync(string userId, int productId, int quantity, CancellationToken ct = default)
    {
        if (quantity < 1) throw new BusinessRuleException("Quantity must be at least 1.");

        var product = await _uow.Products.QueryNoTracking().FirstOrDefaultAsync(p => p.Id == productId, ct)
                      ?? throw new NotFoundException("Product", productId);
        if (!product.IsActive) throw new BusinessRuleException($"{product.Name} is currently unavailable.");

        var cart = await GetOrCreateCartAsync(userId, ct);
        var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
        var newQuantity = (item?.Quantity ?? 0) + quantity;
        EnsureStock(product, newQuantity);

        if (item is null)
            cart.Items.Add(new CartItem { ProductId = productId, Quantity = newQuantity });
        else
        {
            item.Quantity = newQuantity;
            item.UpdatedAt = DateTime.Now;
        }

        await _uow.SaveChangesAsync(ct);
        return await GetSummaryAsync(userId, null, ct);
    }

    public async Task<CartSummaryDto> UpdateQuantityAsync(string userId, int cartItemId, int quantity, CancellationToken ct = default)
    {
        if (quantity <= 0) return await RemoveItemAsync(userId, cartItemId, ct);

        var item = await FindItemAsync(userId, cartItemId, ct);
        EnsureStock(item.Product, quantity);
        item.Quantity = quantity;
        item.UpdatedAt = DateTime.Now;
        await _uow.SaveChangesAsync(ct);
        return await GetSummaryAsync(userId, cartItemId, ct);
    }

    public async Task<CartSummaryDto> RemoveItemAsync(string userId, int cartItemId, CancellationToken ct = default)
    {
        var item = await FindItemAsync(userId, cartItemId, ct);
        _uow.CartItems.Remove(item);
        await _uow.SaveChangesAsync(ct);
        return await GetSummaryAsync(userId, null, ct);
    }

    public async Task ClearAsync(string userId, CancellationToken ct = default)
    {
        var items = await _uow.CartItems.Query().Where(i => i.Cart.Customer.UserId == userId).ToListAsync(ct);
        foreach (var item in items) _uow.CartItems.Remove(item);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<int> GetItemCountAsync(string userId, CancellationToken ct = default) =>
        await _uow.CartItems.QueryNoTracking()
            .Where(i => i.Cart.Customer.UserId == userId)
            .SumAsync(i => (int?)i.Quantity, ct) ?? 0;

    private async Task<CartSummaryDto> GetSummaryAsync(string userId, int? cartItemId, CancellationToken ct)
    {
        var cart = await GetCartAsync(userId, ct);
        return new CartSummaryDto
        {
            ItemCount = cart.ItemCount,
            SubTotal = cart.SubTotal,
            DeliveryFee = cart.DeliveryFee,
            Total = cart.Total,
            LineTotal = cartItemId is null ? null : cart.Items.FirstOrDefault(i => i.Id == cartItemId)?.LineTotal
        };
    }

    private async Task<Cart> GetOrCreateCartAsync(string userId, CancellationToken ct)
    {
        var customer = await _uow.Customers.Query()
                           .Include(c => c.Cart!).ThenInclude(c => c.Items)
                           .FirstOrDefaultAsync(c => c.UserId == userId, ct)
                       ?? throw new ForbiddenAccessException("Only customer accounts can use the shopping cart.");

        if (customer.Cart is null)
        {
            customer.Cart = new Cart { CustomerId = customer.Id };
        }
        return customer.Cart;
    }

    private async Task<CartItem> FindItemAsync(string userId, int cartItemId, CancellationToken ct) =>
        await _uow.CartItems.Query()
            .Include(i => i.Product)
            .FirstOrDefaultAsync(i => i.Id == cartItemId && i.Cart.Customer.UserId == userId, ct)
        ?? throw new NotFoundException("Cart item", cartItemId);

    private static void EnsureStock(Product product, int quantity)
    {
        if (quantity > MaxQuantityPerItem)
            throw new BusinessRuleException($"You can order at most {MaxQuantityPerItem} units of a product at once.");
        if (quantity > product.StockQuantity)
            throw new BusinessRuleException(product.StockQuantity == 0
                ? $"{product.Name} is out of stock."
                : $"Only {product.StockQuantity} unit(s) of {product.Name} are available.");
    }
}
