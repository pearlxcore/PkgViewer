namespace PkgViewer.Core.Models;

/// <summary>One node in the package file tree shown by the File Browser.</summary>
public sealed class PackageFileNode
{
    public required string Name { get; init; }
    public required string FullPath { get; init; }
    public bool IsDirectory { get; init; }
    public long Size { get; init; }
    public List<PackageFileNode> Children { get; } = [];
}

/// <summary>
/// Builds a directory tree from a package's flat file listing. Intermediate directories that are
/// not present as explicit records are synthesized, and every level is sorted directories-first,
/// then by name.
/// </summary>
public static class PackageFileTree
{
    public static List<PackageFileNode> Build(IReadOnlyList<PackageFileRecord> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        var roots = new List<PackageFileNode>();
        var index = new Dictionary<string, PackageFileNode>(StringComparer.OrdinalIgnoreCase);

        foreach (PackageFileRecord record in files)
        {
            // Skip rooted/traversal/reserved entries rather than building an unsafe tree.
            if (!PackagePath.TryNormalize(record.Path, out string path, out _)) continue;

            string[] parts = path.Split('/');
            string current = string.Empty;
            PackageFileNode? parent = null;
            for (int i = 0; i < parts.Length; i++)
            {
                bool isLast = i == parts.Length - 1;
                current = current.Length == 0 ? parts[i] : current + "/" + parts[i];

                if (!index.TryGetValue(current, out PackageFileNode? node))
                {
                    node = new PackageFileNode
                    {
                        Name = parts[i],
                        FullPath = current,
                        IsDirectory = isLast ? record.IsDirectory : true,
                        Size = isLast ? record.Size : 0
                    };
                    index[current] = node;
                    if (parent is null) roots.Add(node);
                    else parent.Children.Add(node);
                }

                parent = node;
            }
        }

        Sort(roots);
        return roots;
    }

    private static void Sort(List<PackageFileNode> nodes)
    {
        nodes.Sort((left, right) =>
        {
            if (left.IsDirectory != right.IsDirectory) return left.IsDirectory ? -1 : 1;
            return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
        });
        foreach (PackageFileNode node in nodes)
        {
            if (node.Children.Count > 0) Sort(node.Children);
        }
    }
}
