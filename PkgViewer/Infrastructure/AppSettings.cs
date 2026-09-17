using System.Text.Json;

namespace PkgViewer.Infrastructure;

/// <summary>Small per-user settings store at %LOCALAPPDATA%\PkgViewer\settings.json.</summary>
internal sealed class AppSettings
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PkgViewer", "settings.json");

    /// <summary>Set once PkgViewer integration has been added from the first-run prompt.</summary>
    public bool IntegrationPromptShown { get; set; }

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new AppSettings();
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, SerializerOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Persisting settings is best-effort; never block the app.
        }
    }
}
