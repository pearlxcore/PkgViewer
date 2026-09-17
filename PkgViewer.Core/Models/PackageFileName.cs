using System.Text;

namespace PkgViewer.Core.Models;

/// <summary>Path/folder name helpers shared by the UI.</summary>
public static class PackageFileName
{
    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

    public static string Sanitize(string name)
    {
        var builder = new StringBuilder(name.Length);
        foreach (char character in name)
            builder.Append(Array.IndexOf(InvalidFileNameChars, character) >= 0 ? '_' : character);
        return builder.ToString().Trim().TrimEnd('.');
    }
}
