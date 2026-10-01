using ZamzamWaterDelivery.Domain.Common;
using ZamzamWaterDelivery.Domain.Enums;

namespace ZamzamWaterDelivery.Domain.Entities;

public class Driver : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string VehicleType { get; set; } = string.Empty;
    public string VehicleNumber { get; set; } = string.Empty;
    public string LicenseNumber { get; set; } = string.Empty;
    public DriverStatus Status { get; set; } = DriverStatus.Available;

    /// <summary>Soft delete: drivers with delivery history are archived instead of removed.</summary>
    public bool IsDeleted { get; set; }

    public ICollection<Delivery> Deliveries { get; set; } = new List<Delivery>();
}
