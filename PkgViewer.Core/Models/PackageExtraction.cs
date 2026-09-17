namespace PkgViewer.Core.Models;

/// <summary>How an extraction handles a destination file that already exists.</summary>
public enum PackageConflictPolicy
{
    /// <summary>Abort the file with a failure (the caller usually asks the user first).</summary>
    Fail,
    /// <summary>Leave the existing file untouched and report the item as skipped.</summary>
    Skip,
    /// <summary>Replace the existing file atomically.</summary>
    Replace,
    /// <summary>Write beside the existing file with a numbered suffix.</summary>
    KeepBoth
}

public enum PackageExtractionOutcome
{
    Extracted,
    Replaced,
    KeptBoth,
    Skipped,
    Failed
}

/// <summary>A request to extract one package entry beneath <paramref name="DestinationRoot"/>.</summary>
public sealed record PackageExtractionRequest(
    string PackageRelativePath,
    string DestinationRoot,
    PackageConflictPolicy ConflictPolicy = PackageConflictPolicy.Replace);

public sealed record PackageExtractionResult(
    PackageExtractionOutcome Outcome,
    string DestinationPath,
    string? Error = null);

/// <summary>Counts from a full extraction. <see cref="Errors"/> holds one line per failed file.</summary>
public sealed record PackageExtractSummary(
    int Extracted,
    int Skipped,
    int Failed,
    IReadOnlyList<string> Errors)
{
    public bool HasFailures => Failed > 0;
}

/// <summary>
/// Validates package-controlled relative paths and resolves them beneath a destination root. This is
/// the one place that decides whether an entry may be written, so every backend and the UI agree.
/// </summary>
public static class PackagePath
{
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    /// <summary>
    /// Normalizes a package entry path to forward slashes without leading or trailing separators,
    /// rejecting rooted paths, traversal and unsafe Windows names. Returns false with a reason.
    /// </summary>
    public static bool TryNormalize(string? path, out string normalized, out string? error)
    {
        normalized = string.Empty;
        error = null;
        if (string.IsNullOrWhiteSpace(path))
        {
            error = "empty path";
            return false;
        }

        // Leading separators are treated as harmless delimiters (the result is always placed beneath a
        // destination root); drive letters and traversal are the actual hazards and are rejected.
        string candidate = path.Replace('\\', '/').Trim().TrimStart('/');
        if (candidate.Length >= 2 && candidate[1] == ':')
        {
            error = "rooted drive path";
            return false;
        }

        var segments = new List<string>();
        foreach (string raw in candidate.Split('/'))
        {
            string segment = raw.Trim();
            if (segment.Length == 0 || segment == ".") continue;
            if (segment == "..")
            {
                error = "path traversal";
                return false;
            }
            if (segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                segment.EndsWith(' ') || segment.EndsWith('.'))
            {
                error = "invalid name";
                return false;
            }
            if (ReservedNames.Contains(Path.GetFileNameWithoutExtension(segment)))
            {
                error = "reserved device name";
                return false;
            }
            segments.Add(segment);
        }

        if (segments.Count == 0)
        {
            error = "empty path";
            return false;
        }

        normalized = string.Join('/', segments);
        return true;
    }

    /// <summary>
    /// Resolves a validated relative path beneath <paramref name="rootDirectory"/> and confirms the
    /// result stays inside the root.
    /// </summary>
    public static string ResolveInside(string rootDirectory, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        if (!TryNormalize(relativePath, out string normalized, out string? reason))
            throw new IOException($"The package entry path is not safe to extract ({reason}).");

        string root = Path.GetFullPath(rootDirectory);
        string combined = Path.GetFullPath(Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar)));
        string prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                        + Path.DirectorySeparatorChar;
        if (!combined.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new IOException("The package entry path leaves the destination folder.");
        return combined;
    }

    /// <summary>Returns a path that does not collide with an existing file by numbering it.</summary>
    public static string MakeUnique(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path)) return path;
        string? directory = Path.GetDirectoryName(path);
        string name = Path.GetFileNameWithoutExtension(path);
        string extension = Path.GetExtension(path);
        for (int index = 2; index < 10_000; index++)
        {
            string candidate = Path.Combine(directory ?? string.Empty, $"{name} ({index}){extension}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate)) return candidate;
        }
        return path;
    }
}
