using ZamzamWaterDelivery.Domain.Common;

namespace ZamzamWaterDelivery.Domain.Entities;

public class Customer : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    /// <summary>Soft delete: customers with orders are archived so order history stays intact.</summary>
    public bool IsDeleted { get; set; }

    public ICollection<Address> Addresses { get; set; } = new List<Address>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public Cart? Cart { get; set; }
}
