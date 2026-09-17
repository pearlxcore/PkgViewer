using System.Text.Json;

namespace PkgViewer.Infrastructure;

/// <summary>Small per-user settings store at %LOCALAPPDATA%\PkgViewer\settings.json.</summary>
internal sealed class AppSettings
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PkgViewer", "settings.json");

    /// <summary>Set once PkgViewer integration has been added from the first-run prompt.</summary>
    public bool IntegrationPromptShown { get; set; }

    /// <summary>Set when the user chose "Not now", so the first-run prompt is not repeated.</summary>
    public bool IntegrationPromptDeclined { get; set; }

    // Remembered view state.
    public int WindowWidth { get; set; }
    public int WindowHeight { get; set; }
    public bool WindowMaximized { get; set; }
    /// <summary>File browser splitter sizes as a comma-separated list.</summary>
    public string FilesSplitterSizes { get; set; } = string.Empty;
    /// <summary>File list column widths as a comma-separated list.</summary>
    public string FileColumnWidths { get; set; } = string.Empty;
    public bool PreviewPaneVisible { get; set; } = true;
    public string LastExtractionDirectory { get; set; } = string.Empty;

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new AppSettings();
            AppSettings settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), SerializerOptions)
                ?? new AppSettings();
            settings.FilesSplitterSizes ??= string.Empty;
            settings.FileColumnWidths ??= string.Empty;
            settings.LastExtractionDirectory ??= string.Empty;
            return settings;
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
