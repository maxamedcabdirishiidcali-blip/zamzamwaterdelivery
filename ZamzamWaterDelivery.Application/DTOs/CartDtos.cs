using System.ComponentModel.DataAnnotations;

namespace ZamzamWaterDelivery.Application.DTOs;

public class CartDto
{
    public List<CartItemDto> Items { get; set; } = new();
    public decimal SubTotal { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal Total { get; set; }
    public decimal FreeDeliveryThreshold { get; set; }
    public int ItemCount => Items.Sum(i => i.Quantity);
    public bool IsEmpty => Items.Count == 0;
}

public class CartItemDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public int AvailableStock { get; set; }
    public bool IsAvailable { get; set; }
    public decimal LineTotal => UnitPrice * Quantity;
}

public class AddToCartDto
{
    [Range(1, int.MaxValue)]
    public int ProductId { get; set; }

    [Range(1, 500, ErrorMessage = "Quantity must be between 1 and 500.")]
    public int Quantity { get; set; } = 1;
}

public class UpdateCartItemDto
{
    [Range(1, int.MaxValue)]
    public int CartItemId { get; set; }

    [Range(0, 500, ErrorMessage = "Quantity must be between 0 and 500.")]
    public int Quantity { get; set; }
}

public class CartSummaryDto
{
    public int ItemCount { get; set; }
    public decimal SubTotal { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal Total { get; set; }
    public decimal? LineTotal { get; set; }
}
