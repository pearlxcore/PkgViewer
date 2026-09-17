using System.Text.Json;

namespace PkgViewer.Core.Models;

/// <summary>A single node of a JSON document shown in the PS5 param.json tree.</summary>
public sealed class JsonTreeNode
{
    public required string Name { get; init; }

    /// <summary>Scalar text, or null for an object/array container.</summary>
    public string? Value { get; init; }

    public List<JsonTreeNode> Children { get; } = [];

    public static JsonTreeNode Container(string name) => new() { Name = name };
    public static JsonTreeNode Scalar(string name, string value) => new() { Name = name, Value = value };
}

/// <summary>Builds a node hierarchy from a JSON document (objects, arrays and scalars).</summary>
public static class JsonTree
{
    public static IReadOnlyList<JsonTreeNode> Build(string? json)
    {
        var roots = new List<JsonTreeNode>();
        if (string.IsNullOrWhiteSpace(json)) return roots;

        try
        {
            using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });

            switch (document.RootElement.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (JsonProperty property in document.RootElement.EnumerateObject())
                        roots.Add(BuildNode(property.Name, property.Value));
                    if (roots.Count == 0) roots.Add(JsonTreeNode.Scalar("(root)", "(empty object)"));
                    break;
                case JsonValueKind.Array:
                    int index = 0;
                    foreach (JsonElement item in document.RootElement.EnumerateArray())
                        roots.Add(BuildNode($"[{index++}]", item));
                    if (roots.Count == 0) roots.Add(JsonTreeNode.Scalar("(root)", "(empty array)"));
                    break;
                default:
                    roots.Add(JsonTreeNode.Scalar("(root)", ScalarText(document.RootElement)));
                    break;
            }
        }
        catch (JsonException)
        {
            roots.Add(JsonTreeNode.Scalar("(invalid JSON)", "The parameter file could not be parsed."));
        }

        return roots;
    }

    private static JsonTreeNode BuildNode(string name, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
            {
                JsonTreeNode node = JsonTreeNode.Container(name);
                foreach (JsonProperty property in element.EnumerateObject())
                    node.Children.Add(BuildNode(property.Name, property.Value));
                return node.Children.Count == 0
                    ? JsonTreeNode.Scalar(name, "(empty object)")
                    : node;
            }
            case JsonValueKind.Array:
            {
                JsonTreeNode node = JsonTreeNode.Container(name);
                int index = 0;
                foreach (JsonElement item in element.EnumerateArray())
                    node.Children.Add(BuildNode($"[{index++}]", item));
                return node.Children.Count == 0
                    ? JsonTreeNode.Scalar(name, "(empty array)")
                    : node;
            }
            default:
                return JsonTreeNode.Scalar(name, ScalarText(element));
        }
    }

    private static string ScalarText(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString() ?? string.Empty,
        JsonValueKind.Number => element.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null => "null",
        _ => element.GetRawText()
    };
}
