using System.Text.RegularExpressions;

namespace ZamzamWaterDelivery.Application.Common;

public static partial class EnumExtensions
{
    /// <summary>Turns "OutForDelivery" into "Out for Delivery".</summary>
    public static string ToDisplayName(this Enum value)
    {
        var spaced = PascalCaseBoundary().Replace(value.ToString(), " $1");
        return spaced.Replace(" For ", " for ").Replace(" On ", " on ");
    }

    [GeneratedRegex("(?<!^)([A-Z])")]
    private static partial Regex PascalCaseBoundary();
}
