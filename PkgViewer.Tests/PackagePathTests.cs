using PkgViewer.Core.Models;
using Xunit;

namespace PkgViewer.Tests;

public sealed class PackagePathTests
{
    [Theory]
    [InlineData("../evil.bin")]
    [InlineData("sce_sys/../../evil.bin")]
    [InlineData("C:/Windows/system32/evil.bin")]
    [InlineData("C:\\Windows\\system32\\evil.bin")]
    [InlineData("con.txt")]
    [InlineData("")]
    [InlineData("   ")]
    public void TryNormalize_RejectsUnsafePaths(string path)
    {
        Assert.False(PackagePath.TryNormalize(path, out _, out string? reason));
        Assert.False(string.IsNullOrWhiteSpace(reason));
    }

    [Theory]
    [InlineData("/sce_sys/param.json", "sce_sys/param.json")]
    [InlineData("\\Image0\\data\\file.bin", "Image0/data/file.bin")]
    [InlineData("a//b/./c.bin", "a/b/c.bin")]
    [InlineData("sce_sys/", "sce_sys")]
    public void TryNormalize_NormalizesSeparatorsAndDots(string path, string expected)
    {
        Assert.True(PackagePath.TryNormalize(path, out string normalized, out _));
        Assert.Equal(expected, normalized);
    }

    [Fact]
    public void ResolveInside_StaysBeneathRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "PkgViewerPathTests-" + Guid.NewGuid().ToString("N"));
        string resolved = PackagePath.ResolveInside(root, "sce_sys/param.json");
        Assert.StartsWith(Path.GetFullPath(root), resolved, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(Path.Combine("sce_sys", "param.json"), resolved, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveInside_ThrowsForTraversal()
    {
        string root = Path.Combine(Path.GetTempPath(), "PkgViewerPathTests-" + Guid.NewGuid().ToString("N"));
        Assert.Throws<IOException>(() => PackagePath.ResolveInside(root, "../../outside.bin"));
    }

    [Fact]
    public void Build_SkipsTraversalEntries()
    {
        var files = new List<PackageFileRecord>
        {
            new("..\\evil.bin", false, 1),
            new("sce_sys/param.json", false, 1)
        };

        List<PackageFileNode> roots = PackageFileTree.Build(files);

        Assert.Single(roots);
        Assert.Equal("sce_sys", roots[0].Name);
    }

    [Fact]
    public void MakeUnique_AppendsSuffix()
    {
        string directory = Directory.CreateTempSubdirectory("PkgViewerUnique").FullName;
        try
        {
            string existing = Path.Combine(directory, "icon.png");
            File.WriteAllText(existing, "x");
            string unique = PackagePath.MakeUnique(existing);
            Assert.NotEqual(existing, unique);
            Assert.EndsWith("icon (2).png", unique, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
