using PkgViewer.Core.Models;

namespace PkgViewer.Core.Backends;

public sealed class BackendRegistry
{
    private readonly List<IPackageBackend> _backends = [];

    public void Register(IPackageBackend backend)
    {
        ArgumentNullException.ThrowIfNull(backend);
        _backends.Add(backend);
    }

    public IPackageBackend? Resolve(PackageFormat format) =>
        _backends.FirstOrDefault(backend => backend.CanOpen(format));

    public static BackendRegistry CreateDefault()
    {
        var registry = new BackendRegistry();
        registry.Register(new Ps4PackageBackend());
        registry.Register(new Ps5PackageBackend());
        return registry;
    }
}
