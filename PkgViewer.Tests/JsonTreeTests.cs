using PkgViewer.Core.Models;
using Xunit;

namespace PkgViewer.Tests;

public sealed class JsonTreeTests
{
    [Fact]
    public void BuildsNestedObjectsArraysAndScalars()
    {
        const string json = """
        {
          "contentId": "UP0000-PPSA55555",
          "localizedParameters": { "en-US": { "titleName": "Volume Fixture" } },
          "ageLevel": [1, 2],
          "flag": true
        }
        """;

        IReadOnlyList<JsonTreeNode> roots = JsonTree.Build(json);

        Assert.Equal("UP0000-PPSA55555", roots.Single(node => node.Name == "contentId").Value);

        JsonTreeNode localized = roots.Single(node => node.Name == "localizedParameters");
        Assert.Null(localized.Value);
        JsonTreeNode english = localized.Children.Single();
        Assert.Equal("en-US", english.Name);
        Assert.Equal("titleName", english.Children.Single().Name);
        Assert.Equal("Volume Fixture", english.Children.Single().Value);

        JsonTreeNode age = roots.Single(node => node.Name == "ageLevel");
        Assert.Equal(["[0]", "[1]"], age.Children.Select(child => child.Name));
        Assert.Equal("1", age.Children[0].Value);
        Assert.Equal("true", roots.Single(node => node.Name == "flag").Value);
    }

    [Fact]
    public void EmptyContainers_GetPlaceholders()
    {
        IReadOnlyList<JsonTreeNode> roots = JsonTree.Build("""{ "a": {}, "b": [] }""");

        Assert.Equal("(empty object)", roots.Single(node => node.Name == "a").Value);
        Assert.Equal("(empty array)", roots.Single(node => node.Name == "b").Value);
    }

    [Fact]
    public void InvalidJson_ReturnsSingleDiagnosticNode()
    {
        IReadOnlyList<JsonTreeNode> roots = JsonTree.Build("{ broken");

        Assert.Single(roots);
        Assert.Equal("(invalid JSON)", roots[0].Name);
    }

    [Fact]
    public void EmptyInput_ReturnsNoNodes() => Assert.Empty(JsonTree.Build(""));
}
