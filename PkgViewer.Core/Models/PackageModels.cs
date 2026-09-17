namespace PkgViewer.Core.Models;

/// <summary>Console generation a package belongs to.</summary>
public enum PkgPlatform
{
    Unknown,
    Ps4,
    Ps5
}

/// <summary>Top-level container format recognised by the viewer.</summary>
public enum PackageFormat
{
    Unknown,
    Ps4Pkg,
    Ps5Pkg,
    Ffpfsc,
    Ffpkg,
    ExfatImage
}

/// <summary>One label/value pair shown in the Package Summary extras or PKG Internals grids.</summary>
public sealed record PackageInfoRow(string Label, string Value);

/// <summary>A single param.sfo / param.json field shown in the Overview PARAM.SFO grid.</summary>
public sealed record PackageSfoEntry(string Name, string Value);

/// <summary>One raw container entry row shown in the PKG Internals Entries grid.</summary>
public sealed record PackageEntryRecord(string Name, string Offset, string Size, string Flags1, string Flags2, string Encrypted);

/// <summary>A single trophy shown in the Trophy grid.</summary>
public sealed class PackageTrophy
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Grade { get; init; } = string.Empty;
    public bool Hidden { get; init; }
    public PackageImage? Icon { get; init; }
}

/// <summary>
/// An extra, platform-specific tab (for example PS5 activities, executable or param.json).
/// Rendered as a grid when <see cref="Text"/> is null, otherwise as read-only text.
/// </summary>
public sealed record PackageDetailTab(string Title, IReadOnlyList<PackageInfoRow> Rows, string? Text = null);

/// <summary>A single file or directory entry inside a package.</summary>
public sealed record PackageFileRecord(string Path, bool IsDirectory, long Size);

/// <summary>Encoded (PNG) or decoded (RGBA) artwork bytes ready for the UI to turn into a bitmap.</summary>
public sealed class PackageImage
{
    public byte[] Bytes { get; init; } = [];
    public bool IsPng { get; init; }
    public bool IsRgba { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public bool IsEmpty => Bytes.Length == 0;

    public static PackageImage Png(byte[] bytes) => new() { Bytes = bytes, IsPng = true };
    public static PackageImage Rgba(byte[] bytes, int width, int height) =>
        new() { Bytes = bytes, IsRgba = true, Width = width, Height = height };
}

public sealed class PackageArtwork
{
    public PackageImage? Icon { get; init; }
    public PackageImage? Pic0 { get; init; }
    public PackageImage? Pic1 { get; init; }
    public PackageImage? Pic2 { get; init; }
}

/// <summary>Platform-neutral package summary produced by every backend.</summary>
public sealed class PackageInfo
{
    public required PkgPlatform Platform { get; init; }
    public required PackageFormat Format { get; init; }
    public string SourcePath { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public long FileSize { get; init; }
    public string Title { get; init; } = string.Empty;
    public string TitleId { get; init; } = string.Empty;
    public string ContentId { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string PackageVersion { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string BuildState { get; init; } = string.Empty;
    public string RequiredFirmware { get; init; } = string.Empty;
    public string SdkVersion { get; init; } = string.Empty;
    public int FileCount { get; init; }
    public long ContentSize { get; init; }
    public IReadOnlyList<PackageInfoRow> ExtraRows { get; init; } = [];

    public string PlatformDisplay => Platform switch
    {
        PkgPlatform.Ps4 => "PlayStation 4",
        PkgPlatform.Ps5 => "PlayStation 5",
        _ => "Unknown"
    };

    public string FormatDisplay => Format switch
    {
        PackageFormat.Ps4Pkg => "PS4 PKG",
        PackageFormat.Ps5Pkg => "PS5 PKG",
        PackageFormat.Ffpfsc => "FFPFSC",
        PackageFormat.Ffpkg => "FFPKG",
        PackageFormat.ExfatImage => "exFAT image",
        _ => "Unknown"
    };

    /// <summary>Short format token used beside the platform label (PKG / FFPFSC / FFPKG / exFAT).</summary>
    public string FormatShort => Format switch
    {
        PackageFormat.Ps4Pkg or PackageFormat.Ps5Pkg => "PKG",
        PackageFormat.Ffpfsc => "FFPFSC",
        PackageFormat.Ffpkg => "FFPKG",
        PackageFormat.ExfatImage => "exFAT",
        _ => string.Empty
    };
}
