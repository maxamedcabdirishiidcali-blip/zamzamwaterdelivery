using ZamzamWaterDelivery.Domain.Common;
using ZamzamWaterDelivery.Domain.Enums;

namespace ZamzamWaterDelivery.Domain.Entities;

public class Delivery : BaseEntity
{
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public int? DriverId { get; set; }
    public Driver? Driver { get; set; }

    public string DeliveryAddress { get; set; } = string.Empty;
    public decimal DeliveryFee { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public string? DeliveryTime { get; set; }
    public DeliveryStatus Status { get; set; } = DeliveryStatus.Unassigned;
    public string? Notes { get; set; }

    public DateTime? AssignedAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? PickedUpAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
}
