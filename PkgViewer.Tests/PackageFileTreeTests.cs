using PkgViewer.Core.Models;
using Xunit;

namespace PkgViewer.Tests;

public sealed class PackageFileTreeTests
{
    [Fact]
    public void BuildsHierarchyWithFilesAndSynthesizedDirectories()
    {
        var files = new List<PackageFileRecord>
        {
            new("Image0/sce_sys/param.sfo", false, 100),
            new("Image0/sce_sys/icon0.png", false, 200),
            new("Image0/eboot.bin", false, 300),
            new("Sc0/npbind.dat", false, 10)
        };

        List<PackageFileNode> roots = PackageFileTree.Build(files);

        Assert.Equal(2, roots.Count);
        PackageFileNode image0 = roots.Single(node => node.Name == "Image0");
        Assert.True(image0.IsDirectory);
        Assert.Equal("Image0/eboot.bin", image0.Children.Single(node => node.Name == "eboot.bin").FullPath);
        PackageFileNode sceSys = image0.Children.Single(node => node.Name == "sce_sys");
        Assert.True(sceSys.IsDirectory);
        Assert.Equal(["icon0.png", "param.sfo"], sceSys.Children.Select(node => node.Name));
    }

    [Fact]
    public void KeepsExplicitDirectoryRecordsOnce()
    {
        var files = new List<PackageFileRecord>
        {
            new("Image0/sce_sys", true, 0),
            new("Image0/sce_sys/param.sfo", false, 100)
        };

        List<PackageFileNode> roots = PackageFileTree.Build(files);

        PackageFileNode sceSys = roots.Single().Children.Single();
        Assert.True(sceSys.IsDirectory);
        Assert.Single(sceSys.Children);
    }

    [Fact]
    public void NormalizesSeparatorsAndLeadingSlashes()
    {
        var files = new List<PackageFileRecord>
        {
            new("\\Image0\\data\\file.bin", false, 5)
        };

        List<PackageFileNode> roots = PackageFileTree.Build(files);

        PackageFileNode image0 = roots.Single();
        Assert.Equal("Image0", image0.FullPath.Replace('\\', '/'));
        Assert.Equal("Image0/data/file.bin", image0.Children.Single().Children.Single().FullPath);
    }

    [Fact]
    public void SortsDirectoriesBeforeFilesAtEveryLevel()
    {
        var files = new List<PackageFileRecord>
        {
            new("root/zeta.txt", false, 1),
            new("root/alpha", true, 0),
            new("root/beta.txt", false, 1)
        };

        List<PackageFileNode> children = PackageFileTree.Build(files).Single().Children;

        Assert.Equal(["alpha", "beta.txt", "zeta.txt"], children.Select(node => node.Name));
    }
}
