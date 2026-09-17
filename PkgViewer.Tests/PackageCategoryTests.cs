using PkgViewer.Core.Models;
using Xunit;

namespace PkgViewer.Tests;

public sealed class PackageCategoryTests
{
    [Theory]
    [InlineData("16777216 (0x01000000)", "Base Game")]
    [InlineData("0 (0x00000000)", "Base Game")]
    [InlineData("0", "Base Game")]
    [InlineData("1 (0x00000001)", "Add-on")]
    [InlineData("2", "Patch")]
    [InlineData("3", "App")]
    public void DescribePs5_MapsNumericApplicationCategory(string raw, string expected) =>
        Assert.Equal(expected, PackageCategory.DescribePs5(raw, isPatch: false));

    [Fact]
    public void DescribePs5_PatchKindWins() =>
        Assert.Equal("Patch", PackageCategory.DescribePs5("0 (0x00000000)", isPatch: true));

    [Theory]
    [InlineData("weird-token")]
    [InlineData("")]
    [InlineData(null)]
    public void DescribePs5_ShowsNonNumericVerbatim(string? raw) =>
        Assert.Equal((raw ?? string.Empty).Trim(), PackageCategory.DescribePs5(raw, isPatch: false));

    [Fact]
    public void DescribePs5_ComposesWithDescribe() =>
        Assert.Equal("Base Game", PackageCategory.Describe(PackageCategory.DescribePs5("16777216 (0x01000000)", false)));
}
