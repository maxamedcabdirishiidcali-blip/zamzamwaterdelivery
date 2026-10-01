using ZamzamWaterDelivery.Domain.Common;
using ZamzamWaterDelivery.Domain.Enums;

namespace ZamzamWaterDelivery.Domain.Entities;

public class Notification : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public int? OrderId { get; set; }
    public Order? Order { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public NotificationChannel Channel { get; set; } = NotificationChannel.InApp;
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
}
