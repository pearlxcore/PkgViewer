using OrbisPkgTool;
using OrbisPkgTool.Pkg;
using PkgViewer.Core.Models;

namespace PkgViewer.Core.Trophies;

/// <summary>
/// PS4 trophy loader ported from PS4PKGTool's trophy inspection pipeline: reads
/// Sc0/trophy/trophy00.trp, resolves the NPWR id from npbind.dat, decrypts/parses the trophy
/// metadata and decodes TROPnnn.PNG icons into <see cref="PackageTrophy"/> items.
/// </summary>
internal static class Ps4TrophyReader
{
    public static (IReadOnlyList<PackageTrophy> Trophies, string Message) Read(string packagePath, string? passcode)
    {
        try
        {
            using var reader = new PkgReader(packagePath, passcode ?? PkgReader.DefaultPasscode);
            PkgEntry? trpEntry = reader.Entries.FirstOrDefault(entry => entry.Id == PkgEntryIds.Trophy00Trp);
            if (trpEntry is null)
                return ([], "No trophy data was found in this package.");
            if (trpEntry.IsEncrypted)
                return ([], "The package contains trophy data, but its TRP entry is encrypted and cannot be accessed.");

            byte[] trpBytes = reader.ExtractEntryBytes("Sc0/trophy/trophy00.trp");
            string? npCommunicationId = ResolveNpCommunicationId(reader);
            TrophyMetadataResult result = new TrophyMetadataService().Read(trpBytes, npCommunicationId);
            if (result.Trophies.Count == 0)
                return ([], string.IsNullOrWhiteSpace(result.StatusMessage)
                    ? "Trophy information could not be read."
                    : result.StatusMessage);

            PackageTrophy[] trophies = result.Trophies
                .OrderBy(trophy => trophy.Id)
                .Select(trophy => new PackageTrophy
                {
                    Id = trophy.Id,
                    Name = trophy.Name,
                    Description = trophy.Description,
                    Grade = trophy.Grade.ToString(),
                    Hidden = trophy.IsHidden,
                    Icon = trophy.IconData is { Length: > 0 } icon ? PackageImage.Png(icon) : null
                })
                .ToArray();
            return (trophies, string.Empty);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ([], "Trophy information could not be read. " + ex.Message);
        }
    }

    private static string? ResolveNpCommunicationId(PkgReader reader)
    {
        foreach (string path in new[] { "Sc0/npbind.dat", "Sc0/nptitle.dat" })
        {
            try
            {
                byte[] bytes = reader.ExtractEntryBytes(path);
                string? id = NpCommunicationId.FindInBytes(bytes);
                if (id != null) return id;
            }
            catch (FileNotFoundException) { }
            catch (InvalidDataException) { }
        }
        return null;
    }
}
