using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZamzamWaterDelivery.Application.DTOs;
using ZamzamWaterDelivery.Application.Interfaces;
using ZamzamWaterDelivery.Domain.Enums;

namespace ZamzamWaterDelivery.Infrastructure.Notifications;

public class NotificationChannelSettings
{
    public const string SectionName = "Notifications";

    public ChannelOptions Sms { get; set; } = new();
    public ChannelOptions WhatsApp { get; set; } = new();

    public class ChannelOptions
    {
        public bool Enabled { get; set; }
        public string? Provider { get; set; }
        public string? SenderId { get; set; }
    }
}

/// <summary>
/// Placeholder channels: they log the outgoing message. To go live, replace SendAsync with a call
/// to an SMS / WhatsApp provider (e.g. Hormuud, Somtel, Twilio, WhatsApp Cloud API).
/// </summary>
public abstract class LoggingNotificationChannel : INotificationChannel
{
    private readonly ILogger _logger;

    protected LoggingNotificationChannel(ILogger logger) => _logger = logger;

    public abstract NotificationChannel Channel { get; }
    public abstract bool IsEnabled { get; }

    public virtual Task SendAsync(NotificationMessage message, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(message.PhoneNumber))
        {
            _logger.LogDebug("{Channel} skipped for user {UserId}: no phone number", Channel, message.UserId);
            return Task.CompletedTask;
        }

        _logger.LogInformation("[{Channel}] To {Phone}: {Title} - {Message}", Channel, message.PhoneNumber, message.Title, message.Message);
        return Task.CompletedTask;
    }
}

public class SmsNotificationChannel : LoggingNotificationChannel
{
    private readonly NotificationChannelSettings _settings;

    public SmsNotificationChannel(IOptions<NotificationChannelSettings> settings, ILogger<SmsNotificationChannel> logger)
        : base(logger) => _settings = settings.Value;

    public override NotificationChannel Channel => NotificationChannel.Sms;
    public override bool IsEnabled => _settings.Sms.Enabled;
}

public class WhatsAppNotificationChannel : LoggingNotificationChannel
{
    private readonly NotificationChannelSettings _settings;

    public WhatsAppNotificationChannel(IOptions<NotificationChannelSettings> settings, ILogger<WhatsAppNotificationChannel> logger)
        : base(logger) => _settings = settings.Value;

    public override NotificationChannel Channel => NotificationChannel.WhatsApp;
    public override bool IsEnabled => _settings.WhatsApp.Enabled;
}
