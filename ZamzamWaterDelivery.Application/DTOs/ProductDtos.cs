using System.ComponentModel.DataAnnotations;
using ZamzamWaterDelivery.Application.Common;

namespace ZamzamWaterDelivery.Application.DTOs;

public class ProductDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
    public int StockQuantity { get; set; }
    public bool IsActive { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public bool InStock => StockQuantity > 0;
}

public class ProductUpsertDto
{
    [Required, StringLength(100, MinimumLength = 2)]
    [Display(Name = "Product Name")]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Range(0.01, 100000, ErrorMessage = "Price must be between 0.01 and 100,000.")]
    [DataType(DataType.Currency)]
    public decimal Price { get; set; }

    [Range(0, 1000000, ErrorMessage = "Stock must be zero or more.")]
    [Display(Name = "Available Quantity")]
    public int StockQuantity { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Please select a category.")]
    [Display(Name = "Category")]
    public int CategoryId { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    [StringLength(500)]
    [Display(Name = "Image URL")]
    public string? ImageUrl { get; set; }
}

public class ProductFilter : PagedRequest
{
    public int? CategoryId { get; set; }
    public bool? IsActive { get; set; }
    public bool InStockOnly { get; set; }
    /// <summary>name | price_asc | price_desc | stock | newest</summary>
    public string? SortBy { get; set; }
}

public class StockUpdateDto
{
    [Range(0, 1000000, ErrorMessage = "Stock must be zero or more.")]
    public int StockQuantity { get; set; }
}

public class PriceUpdateDto
{
    [Range(0.01, 100000, ErrorMessage = "Price must be between 0.01 and 100,000.")]
    public decimal Price { get; set; }
}

public class CategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int ProductCount { get; set; }
}

public class CategoryUpsertDto
{
    [Required, StringLength(80, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }
}
