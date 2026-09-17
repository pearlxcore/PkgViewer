namespace PkgViewer.Core.Models;

/// <summary>Maps raw package category codes (PS4 CATEGORY, PS5 application category) to display names.</summary>
public static class PackageCategory
{
    /// <summary>PS4 param.sfo CATEGORY codes ("gd", "gp", ...).</summary>
    public static string Describe(string? category)
    {
        string value = (category ?? string.Empty).Trim();
        return value.ToLowerInvariant() switch
        {
            "gd" => "Game",
            "gde" or "gdk" => "App",
            "ac" => "Addon",
            "gp" => "Patch",
            "th" => "Theme",
            "av" => "Avatar",
            "wa" => "Wallpaper",
            _ => value
        };
    }

    /// <summary>
    /// PS5 param.json <c>applicationCategoryType</c> (often "0 (0x00000000)") and package kind.
    /// 1 = Add-on, 2 = Patch, 3 = App. Every other numeric encoding is a base application, including
    /// 0 and publisher-specific values such as 0x01000000; non-numeric values are shown verbatim.
    /// </summary>
    public static string DescribePs5(string? applicationCategory, bool isPatch)
    {
        if (isPatch) return "Patch";
        string raw = (applicationCategory ?? string.Empty).Trim();
        if (ParseLeadingInt(raw) is not int value) return raw;
        return value switch
        {
            1 => "Add-on",
            2 => "Patch",
            3 => "App",
            _ => "Base Game"
        };
    }

    /// <summary>Strips the " (0x...)" suffix the PS5 param reader appends to numeric fields.</summary>
    public static string NormalizeNumeric(string? value)
    {
        string raw = (value ?? string.Empty).Trim();
        if (raw.Length == 0) return raw;
        int? numeric = ParseLeadingInt(raw);
        return numeric is null ? raw : numeric.Value.ToString();
    }

    private static int? ParseLeadingInt(string value)
    {
        int index = 0;
        while (index < value.Length && char.IsDigit(value[index])) index++;
        if (index == 0) return null;
        return int.TryParse(value[..index], out int result) ? result : null;
    }
}
