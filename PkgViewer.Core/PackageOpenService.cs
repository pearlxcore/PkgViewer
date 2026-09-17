using PkgViewer.Core.Backends;
using PkgViewer.Core.Models;
using PkgViewer.Core.Probing;

namespace PkgViewer.Core;

/// <summary>
/// Probes a file and opens it through the first backend that accepts it. For the ambiguous
/// PS4/PS5 PKG magic the probe order is tried first and the other platform is retried when the
/// probe confidence was not high.
/// </summary>
public sealed class PackageOpenService
{
    private readonly BackendRegistry _registry;

    public PackageOpenService(BackendRegistry? registry = null) =>
        _registry = registry ?? BackendRegistry.CreateDefault();

    public PackageProbeResult Probe(string path) => PackageFormatProbe.Probe(path);

    public async Task<IPackageSession> OpenAsync(string path, PackageOpenOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        options ??= new PackageOpenOptions();

        PackageProbeResult probe = PackageFormatProbe.Probe(path);
        List<PackageFormat> candidates = BuildCandidates(probe);
        var failures = new List<string>();

        foreach (PackageFormat format in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IPackageBackend? backend = _registry.Resolve(format);
            if (backend is null) continue;
            try
            {
                return await backend.OpenAsync(path, format, options, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                failures.Add($"{format}: {ex.Message}");
            }
        }

        throw new PackageOpenException(path, probe, failures);
    }

    private static List<PackageFormat> BuildCandidates(PackageProbeResult probe)
    {
        var candidates = new List<PackageFormat>();
        switch (probe.Format)
        {
            case PackageFormat.Ps4Pkg:
            case PackageFormat.Ps5Pkg:
                candidates.Add(probe.Format);
                if (probe.Confidence != ProbeConfidence.High)
                    candidates.Add(probe.Format == PackageFormat.Ps4Pkg ? PackageFormat.Ps5Pkg : PackageFormat.Ps4Pkg);
                break;
            case PackageFormat.Unknown:
                candidates.Add(PackageFormat.Ps4Pkg);
                candidates.Add(PackageFormat.Ps5Pkg);
                break;
            default:
                candidates.Add(probe.Format);
                break;
        }
        return candidates;
    }
}
