using ZamzamWaterDelivery.Domain.Common;

namespace ZamzamWaterDelivery.Domain.Entities;

public class Address : BaseEntity
{
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    /// <summary>Street, house or building details.</summary>
    public string AddressLine { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    /// <summary>District, neighbourhood or landmark used by drivers.</summary>
    public string Location { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
}
