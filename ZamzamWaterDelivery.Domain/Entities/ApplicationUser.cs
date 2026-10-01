using Microsoft.AspNetCore.Identity;

namespace ZamzamWaterDelivery.Domain.Entities;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? LastLoginAt { get; set; }

    public Customer? Customer { get; set; }
    public Driver? Driver { get; set; }
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
