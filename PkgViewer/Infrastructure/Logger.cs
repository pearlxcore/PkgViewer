namespace PkgViewer.Infrastructure;

internal static class Logger
{
    public static string LogPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PkgViewer", "PkgViewer.log");

    public static void Info(string message) => Write("INFO", message);

    public static void Exception(string context, Exception exception) =>
        Write("ERROR", context + ": " + exception);

    private static void Write(string level, string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}");
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
