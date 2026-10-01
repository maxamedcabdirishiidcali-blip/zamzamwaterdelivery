using System.ComponentModel.DataAnnotations;
using ZamzamWaterDelivery.Application.Common;
using ZamzamWaterDelivery.Application.Validators;
using ZamzamWaterDelivery.Domain.Enums;

namespace ZamzamWaterDelivery.Application.DTOs;

public class CustomerDto
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string? AddressLine { get; set; }
    public string? City { get; set; }
    public string? Location { get; set; }
    public int OrderCount { get; set; }
    public decimal TotalSpent { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AddressDto
{
    public int Id { get; set; }
    public string AddressLine { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
}

public class CustomerDetailsDto : CustomerDto
{
    public DateTime? LastLoginAt { get; set; }
    public List<AddressDto> Addresses { get; set; } = new();
    public List<OrderDto> Orders { get; set; } = new();
}

public class CustomerUpdateDto
{
    [Required, StringLength(100, MinimumLength = 3)]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Required, RegularExpression(ValidationPatterns.Phone, ErrorMessage = ValidationPatterns.PhoneMessage)]
    [Display(Name = "Phone Number")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 3)]
    [Display(Name = "Address")]
    public string AddressLine { get; set; } = string.Empty;

    [Required, StringLength(60)]
    public string City { get; set; } = string.Empty;

    [Required, StringLength(100)]
    [Display(Name = "Location / Delivery Area")]
    public string Location { get; set; } = string.Empty;

    [Display(Name = "Active (can log in)")]
    public bool IsActive { get; set; } = true;
}

public class CustomerFilter : PagedRequest
{
    public bool? IsActive { get; set; }
    public string? City { get; set; }
}

public class DriverDto
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string VehicleType { get; set; } = string.Empty;
    public string VehicleNumber { get; set; } = string.Empty;
    public string LicenseNumber { get; set; } = string.Empty;
    public DriverStatus Status { get; set; }
    public bool IsActive { get; set; }
    public int ActiveDeliveries { get; set; }
    public int CompletedDeliveries { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class DriverUpdateDto
{
    [Required, StringLength(100, MinimumLength = 3)]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Required, RegularExpression(ValidationPatterns.Phone, ErrorMessage = ValidationPatterns.PhoneMessage)]
    [Display(Name = "Phone Number")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(50)]
    [Display(Name = "Vehicle Type")]
    public string VehicleType { get; set; } = string.Empty;

    [Required, StringLength(30)]
    [Display(Name = "Vehicle Number")]
    public string VehicleNumber { get; set; } = string.Empty;

    [Required, StringLength(50)]
    [Display(Name = "License Number")]
    public string LicenseNumber { get; set; } = string.Empty;

    [DefinedEnum]
    public DriverStatus Status { get; set; } = DriverStatus.Available;
}

public class DriverCreateDto : DriverUpdateDto
{
    [Required, DataType(DataType.Password)]
    [RegularExpression(ValidationPatterns.Password, ErrorMessage = ValidationPatterns.PasswordMessage)]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    [Display(Name = "Confirm Password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class DriverFilter : PagedRequest
{
    public DriverStatus? Status { get; set; }
}

public class DriverStatsDto
{
    public int AssignedCount { get; set; }
    public int InProgressCount { get; set; }
    public int DeliveredToday { get; set; }
    public int TotalDelivered { get; set; }
    public decimal CashToCollect { get; set; }
}

public class DriverStatusUpdateDto
{
    [DefinedEnum]
    public DriverStatus Status { get; set; }
}
