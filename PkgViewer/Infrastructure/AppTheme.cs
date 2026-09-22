using System.Text.Json;
using DarkUI.Config;

namespace PkgViewer.Infrastructure;

/// <summary>
/// Resolves and applies PkgViewer's DarkUI theme. PkgViewer keeps its own choice, but until the
/// user picks one it follows the theme already chosen in PS5 PKG Tool (read-only, best effort).
/// </summary>
internal static class AppTheme
{
    private static readonly string Ps5PkgToolSettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PS5PKGTool", "settings.json");

    /// <summary>
    /// PkgViewer's own theme if it has one, otherwise PS5 PKG Tool's theme, otherwise the DarkUI
    /// default.
    /// </summary>
    public static Theme Resolve(AppSettings settings) =>
        Find(settings.Theme) ?? Find(ReadPs5PkgToolTheme()) ?? ThemeManager.BuiltIn.Default;

    public static void Apply(AppSettings settings)
    {
        Theme theme = Resolve(settings);
        ThemeManager.Apply(theme);
        Logger.Info($"Theme: {theme.Name}");
    }

    /// <summary>Case-insensitive lookup of a built-in theme by its display name.</summary>
    public static Theme? Find(string? name) =>
        string.IsNullOrWhiteSpace(name)
            ? null
            : ThemeManager.Presets.FirstOrDefault(theme =>
                string.Equals(theme.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Reads the theme name from PS5 PKG Tool's settings, or null when unavailable.</summary>
    private static string? ReadPs5PkgToolTheme()
    {
        try
        {
            if (!File.Exists(Ps5PkgToolSettingsPath)) return null;
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Ps5PkgToolSettingsPath));
            if (document.RootElement.ValueKind != JsonValueKind.Object) return null;
            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                if (!string.Equals(property.Name, "Theme", StringComparison.OrdinalIgnoreCase)) continue;
                return property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Another app's settings file is a convenience only; never let it block startup.
        }
        return null;
    }
}
