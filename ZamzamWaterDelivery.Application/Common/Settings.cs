namespace ZamzamWaterDelivery.Application.Common;

public class DeliverySettings
{
    public const string SectionName = "Delivery";

    private static readonly string[] DefaultTimeSlots =
    {
        "Morning (8:00 AM - 12:00 PM)",
        "Afternoon (12:00 PM - 4:00 PM)",
        "Evening (4:00 PM - 8:00 PM)"
    };

    public decimal BaseFee { get; set; } = 2.00m;

    /// <summary>Orders with a subtotal at or above this amount get free delivery (0 disables).</summary>
    public decimal FreeDeliveryThreshold { get; set; } = 50m;

    public string[] TimeSlots { get; set; } = Array.Empty<string>();

    public IReadOnlyList<string> AvailableTimeSlots => TimeSlots.Length > 0 ? TimeSlots : DefaultTimeSlots;

    public decimal CalculateFee(decimal subTotal)
    {
        if (subTotal <= 0) return 0;
        if (FreeDeliveryThreshold > 0 && subTotal >= FreeDeliveryThreshold) return 0;
        return BaseFee;
    }
}

public class CompanySettings
{
    public const string SectionName = "Company";

    public string Name { get; set; } = "Zamzam Water Delivery";
    public string Tagline { get; set; } = "Pure drinking water, delivered to your door.";
    public string Phone { get; set; } = string.Empty;
    public string WhatsApp { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string WorkingHours { get; set; } = string.Empty;
    public string CurrencySymbol { get; set; } = "$";
}
