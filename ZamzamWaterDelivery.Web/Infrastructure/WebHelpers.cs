using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using ZamzamWaterDelivery.Application.Common;
using ZamzamWaterDelivery.Application.Interfaces;
using ZamzamWaterDelivery.Domain.Enums;

namespace ZamzamWaterDelivery.Web.Infrastructure;

public static class Alerts
{
    public const string Success = "AlertSuccess";
    public const string Error = "AlertError";
    public const string Info = "AlertInfo";
}

public static class ClaimsPrincipalExtensions
{
    public static string GetUserId(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new ForbiddenAccessException("You must be signed in.");
}

/// <summary>Base for MVC controllers: TempData alert helpers.</summary>
public abstract class AppController : Controller
{
    protected string UserId => User.GetUserId();

    protected void Success(string message) => TempData[Alerts.Success] = message;
    protected void Error(string message) => TempData[Alerts.Error] = message;
    protected void Info(string message) => TempData[Alerts.Info] = message;

    protected IActionResult RedirectToLocal(string? returnUrl, string fallbackAction = "Index", string? controller = null) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction(fallbackAction, controller);
}

/// <summary>Helpers used by Razor views for consistent badges, money and dates.</summary>
public static class Ui
{
    /// <summary>Set once at startup from the Company settings.</summary>
    public static string CurrencySymbol { get; set; } = "$";

    public static string Money(decimal value) =>
        CurrencySymbol + value.ToString("N2", CultureInfo.InvariantCulture);

    public static string Date(DateTime? value) => value?.ToString("dd MMM yyyy") ?? "-";
    public static string When(DateTime? value) => value?.ToString("dd MMM yyyy, hh:mm tt") ?? "-";

    public static string Badge(OrderStatus status) => status switch
    {
        OrderStatus.Pending => "bg-warning-subtle text-warning-emphasis",
        OrderStatus.Confirmed => "bg-info-subtle text-info-emphasis",
        OrderStatus.Preparing => "bg-primary-subtle text-primary-emphasis",
        OrderStatus.OutForDelivery => "bg-primary text-white",
        OrderStatus.Delivered => "bg-success-subtle text-success-emphasis",
        _ => "bg-danger-subtle text-danger-emphasis"
    };

    public static string Badge(DeliveryStatus status) => status switch
    {
        DeliveryStatus.Unassigned => "bg-secondary-subtle text-secondary-emphasis",
        DeliveryStatus.Assigned => "bg-warning-subtle text-warning-emphasis",
        DeliveryStatus.Accepted => "bg-info-subtle text-info-emphasis",
        DeliveryStatus.OutForDelivery => "bg-primary text-white",
        DeliveryStatus.Delivered => "bg-success-subtle text-success-emphasis",
        _ => "bg-danger-subtle text-danger-emphasis"
    };

    public static string Badge(PaymentStatus status) => status switch
    {
        PaymentStatus.Paid => "bg-success-subtle text-success-emphasis",
        PaymentStatus.Pending => "bg-warning-subtle text-warning-emphasis",
        PaymentStatus.Refunded => "bg-secondary-subtle text-secondary-emphasis",
        _ => "bg-danger-subtle text-danger-emphasis"
    };

    public static string Badge(DriverStatus status) => status switch
    {
        DriverStatus.Available => "bg-success-subtle text-success-emphasis",
        DriverStatus.Busy => "bg-warning-subtle text-warning-emphasis",
        _ => "bg-secondary-subtle text-secondary-emphasis"
    };

    public static string Icon(OrderStatus status) => status switch
    {
        OrderStatus.Pending => "bi-hourglass-split",
        OrderStatus.Confirmed => "bi-check2-circle",
        OrderStatus.Preparing => "bi-box-seam",
        OrderStatus.OutForDelivery => "bi-truck",
        OrderStatus.Delivered => "bi-house-check",
        _ => "bi-x-circle"
    };

    public static string Icon(NotificationType type) => type switch
    {
        NotificationType.NewOrder => "bi-bag-plus",
        NotificationType.OrderConfirmed => "bi-check2-circle",
        NotificationType.DriverAssigned => "bi-person-badge",
        NotificationType.OutForDelivery => "bi-truck",
        NotificationType.OrderDelivered => "bi-house-check",
        NotificationType.OrderCancelled => "bi-x-circle",
        _ => "bi-bell"
    };

    public static IEnumerable<SelectListItem> EnumOptions<TEnum>(TEnum? selected = null, string? emptyText = null)
        where TEnum : struct, Enum
    {
        if (emptyText is not null) yield return new SelectListItem(emptyText, string.Empty, selected is null);
        foreach (var value in Enum.GetValues<TEnum>())
            yield return new SelectListItem(value.ToDisplayName(), value.ToString(), selected?.Equals(value) == true);
    }

    /// <summary>tel: link friendly phone number (digits and a leading +).</summary>
    public static string Tel(string phone) => new string(phone.Where(c => char.IsDigit(c) || c == '+').ToArray());

    public static string WhatsApp(string phone) => "https://wa.me/" + new string(phone.Where(char.IsDigit).ToArray());

    public static string MapLink(string address) => "https://www.google.com/maps/search/?api=1&query=" + Uri.EscapeDataString(address);
}

/// <summary>Validates an uploaded product image (type and size).</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class AllowedImageAttribute : ValidationAttribute
{
    private static readonly string[] Extensions = { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
    private const long MaxBytes = 2 * 1024 * 1024;

    protected override ValidationResult? IsValid(object? value, ValidationContext context)
    {
        if (value is not IFormFile file) return ValidationResult.Success;

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!Extensions.Contains(extension) || !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return new ValidationResult("Only JPG, PNG, WEBP or GIF images are allowed.");
        if (file.Length > MaxBytes)
            return new ValidationResult("The image must be 2 MB or smaller.");
        return ValidationResult.Success;
    }
}

/// <summary>Stores uploads under wwwroot/uploads with random file names.</summary>
public class LocalFileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _env;

    public LocalFileStorageService(IWebHostEnvironment env) => _env = env;

    public async Task<string> SaveAsync(Stream content, string fileName, string folder, CancellationToken ct = default)
    {
        var safeFolder = new string(folder.Where(char.IsLetterOrDigit).ToArray());
        var directory = Path.Combine(_env.WebRootPath, "uploads", safeFolder);
        Directory.CreateDirectory(directory);

        var name = $"{Guid.NewGuid():N}{Path.GetExtension(fileName).ToLowerInvariant()}";
        await using var target = File.Create(Path.Combine(directory, name));
        await content.CopyToAsync(target, ct);
        return $"/uploads/{safeFolder}/{name}";
    }

    public void Delete(string? relativeUrl)
    {
        if (string.IsNullOrWhiteSpace(relativeUrl) || !relativeUrl.StartsWith("/uploads/", StringComparison.Ordinal)) return;

        var root = Path.GetFullPath(Path.Combine(_env.WebRootPath, "uploads"));
        var path = Path.GetFullPath(Path.Combine(_env.WebRootPath, relativeUrl.TrimStart('/')));
        if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase) && File.Exists(path)) File.Delete(path);
    }
}
