namespace ZamzamWaterDelivery.Web.Models;

/// <summary>The standard View / Edit / Delete buttons shown on every admin table row.</summary>
public record RowActions(string Name, string? EditUrl, string? DeleteUrl, string? ViewUrl = null, string? DeleteWarning = null);

/// <summary>Title bar of an admin list page with the "Add" button.</summary>
public record PageHeader(string Title, string? AddUrl = null, string? AddLabel = null, string? Subtitle = null);
