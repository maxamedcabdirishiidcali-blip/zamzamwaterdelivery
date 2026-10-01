using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZamzamWaterDelivery.Application.DTOs;
using ZamzamWaterDelivery.Application.Interfaces;
using ZamzamWaterDelivery.Domain.Common;
using ZamzamWaterDelivery.Domain.Entities;
using ZamzamWaterDelivery.Domain.Enums;
using ZamzamWaterDelivery.Domain.Interfaces;

namespace ZamzamWaterDelivery.Application.Services;

/// <summary>
/// Stores in-app notifications and forwards them to any enabled external channels
/// (SMS, WhatsApp...). External failures are logged and never break the business operation.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly IUnitOfWork _uow;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IEnumerable<INotificationChannel> _channels;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IUnitOfWork uow,
        UserManager<ApplicationUser> users,
        IEnumerable<INotificationChannel> channels,
        ILogger<NotificationService> logger)
    {
        _uow = uow;
        _users = users;
        _channels = channels;
        _logger = logger;
    }

    public async Task NotifyUserAsync(string userId, NotificationType type, string title, string message,
        int? orderId = null, CancellationToken ct = default)
    {
        await _uow.Notifications.AddAsync(Create(userId, type, title, message, orderId), ct);
        await _uow.SaveChangesAsync(ct);
        await DispatchExternalAsync(userId, type, title, message, orderId, ct);
    }

    public async Task NotifyAdminsAsync(NotificationType type, string title, string message,
        int? orderId = null, CancellationToken ct = default)
    {
        var admins = await _users.GetUsersInRoleAsync(Roles.Admin);
        foreach (var admin in admins)
            await _uow.Notifications.AddAsync(Create(admin.Id, type, title, message, orderId), ct);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<NotificationDto>> GetForUserAsync(string userId, int take = 50, CancellationToken ct = default) =>
        await _uow.Notifications.QueryNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(take)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                Title = n.Title,
                Message = n.Message,
                Type = n.Type,
                IsRead = n.IsRead,
                OrderId = n.OrderId,
                CreatedAt = n.CreatedAt
            })
            .ToListAsync(ct);

    public Task<int> GetUnreadCountAsync(string userId, CancellationToken ct = default) =>
        _uow.Notifications.QueryNoTracking().CountAsync(n => n.UserId == userId && !n.IsRead, ct);

    public async Task MarkAsReadAsync(string userId, int notificationId, CancellationToken ct = default)
    {
        var notification = await _uow.Notifications.Query()
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, ct);
        if (notification is null || notification.IsRead) return;

        notification.IsRead = true;
        notification.ReadAt = DateTime.Now;
        await _uow.SaveChangesAsync(ct);
    }

    public async Task MarkAllAsReadAsync(string userId, CancellationToken ct = default)
    {
        var unread = await _uow.Notifications.Query().Where(n => n.UserId == userId && !n.IsRead).ToListAsync(ct);
        var now = DateTime.Now;
        foreach (var n in unread)
        {
            n.IsRead = true;
            n.ReadAt = now;
        }
        await _uow.SaveChangesAsync(ct);
    }

    private static Notification Create(string userId, NotificationType type, string title, string message, int? orderId) =>
        new()
        {
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            OrderId = orderId,
            Channel = NotificationChannel.InApp
        };

    private async Task DispatchExternalAsync(string userId, NotificationType type, string title, string message,
        int? orderId, CancellationToken ct)
    {
        var enabled = _channels.Where(c => c.IsEnabled).ToList();
        if (enabled.Count == 0) return;

        var user = await _users.FindByIdAsync(userId);
        if (user is null) return;

        var payload = new NotificationMessage(userId, user.PhoneNumber, user.Email, title, message, type, orderId);
        foreach (var channel in enabled)
        {
            try
            {
                await channel.SendAsync(payload, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send {Channel} notification to user {UserId}", channel.Channel, userId);
            }
        }
    }
}
