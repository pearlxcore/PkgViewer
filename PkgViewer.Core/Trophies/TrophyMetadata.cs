using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace PkgViewer.Core.Trophies;

internal enum TrophyGrade
{
    Unknown,
    Bronze,
    Silver,
    Gold,
    Platinum
}

internal enum TrophyMetadataFailureKind
{
    None,
    MissingMetadata,
    UnsupportedMetadata,
    MissingNpCommunicationId,
    DecryptionFailed,
    ParseFailed
}

internal sealed class TrophyInfo
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public TrophyGrade Grade { get; init; }
    public bool IsHidden { get; init; }
    public int? GroupId { get; init; }
    public string GroupName { get; init; } = "Base Game";
    public int? PlatinumId { get; init; }
    public byte[]? IconData { get; init; }
}

internal sealed class TrophyMetadataResult
{
    public IReadOnlyList<TrophyInfo> Trophies { get; init; } = [];
    public string? NpCommunicationId { get; init; }
    public string MetadataEntryName { get; init; } = string.Empty;
    public TrophyMetadataFailureKind FailureKind { get; init; }
    public string StatusMessage { get; init; } = string.Empty;
}

/// <summary>Finds/validates the NPWRxxxxx_00 trophy identifier, ported from PS4PKGTool.</summary>
internal static class NpCommunicationId
{
    private static readonly Regex IdRegex = new(@"\bNPWR\d{5}_\d{2}\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool IsValid(string? value) =>
        value != null && IdRegex.Match(value) is { Success: true } match && match.Value.Length == value.Length;

    public static string? FindInBytes(byte[] bytes)
    {
        if (bytes.Length == 0) return null;
        string? ascii = FindId(Encoding.ASCII.GetString(bytes));
        string? unicode = bytes.Length >= 2 ? FindId(Encoding.Unicode.GetString(bytes)) : null;
        if (ascii != null && unicode != null && !string.Equals(ascii, unicode, StringComparison.Ordinal))
            throw new InvalidDataException("Conflicting NP Communication IDs were found inside the data.");
        return ascii ?? unicode;
    }

    private static string? FindId(string text)
    {
        MatchCollection matches = IdRegex.Matches(text);
        if (matches.Count == 0) return null;
        string first = matches[0].Value;
        for (int index = 1; index < matches.Count; index++)
            if (!string.Equals(first, matches[index].Value, StringComparison.Ordinal))
                throw new InvalidDataException("More than one NP Communication ID was found in a candidate file.");
        return first;
    }
}

/// <summary>AES-128-CBC ESFM decryptor from PS4PKGTool's EsfmDecryptor.</summary>
internal sealed class EsfmDecryptor
{
    private static readonly byte[] TrophyMasterKey =
    [
        0x21, 0xF4, 0x1A, 0x6B, 0xAD, 0x8A, 0x1D, 0x3E,
        0xCA, 0x7A, 0xD5, 0x86, 0xC1, 0x01, 0xB7, 0xA9
    ];

    public byte[] Decrypt(ReadOnlySpan<byte> encryptedData, string npCommunicationId)
    {
        if (!NpCommunicationId.IsValid(npCommunicationId))
            throw new ArgumentException("NP Communication ID must match NPWRxxxxx_00.", nameof(npCommunicationId));
        if (encryptedData.Length < 32 || encryptedData.Length % 16 != 0)
            throw new InvalidDataException("ESFM data must contain at least two complete AES blocks.");

        byte[] titleKey = DeriveTitleKey(npCommunicationId);
        byte[] decrypted;
        try
        {
            using Aes aes = CreateAes(titleKey, PaddingMode.PKCS7);
            using ICryptoTransform transform = aes.CreateDecryptor();
            byte[] encrypted = encryptedData.ToArray();
            decrypted = transform.TransformFinalBlock(encrypted, 0, encrypted.Length);
        }
        catch (CryptographicException ex)
        {
            throw new CryptographicException(
                "ESFM decryption failed. The NP Communication ID may be incorrect or the payload is damaged.", ex);
        }

        if (decrypted.Length < 17 || !decrypted.AsSpan(0, 16).SequenceEqual(new byte[16]))
            throw new CryptographicException(
                "ESFM decryption did not produce the required 16-byte zero prefix. The NP Communication ID is incorrect or this ESFM variant is unsupported.");

        byte[] xml = decrypted.AsSpan(16).ToArray();
        _ = new UTF8Encoding(false, true).GetString(xml);
        return xml;
    }

    private static byte[] DeriveTitleKey(string npCommunicationId)
    {
        byte[] input = new byte[16];
        int written = Encoding.ASCII.GetBytes(npCommunicationId, input);
        if (written != 12)
            throw new ArgumentException("NP Communication ID must be exactly 12 ASCII bytes.", nameof(npCommunicationId));

        using Aes aes = CreateAes(TrophyMasterKey, PaddingMode.None);
        using ICryptoTransform transform = aes.CreateEncryptor();
        return transform.TransformFinalBlock(input, 0, input.Length);
    }

    private static Aes CreateAes(byte[] key, PaddingMode padding)
    {
        Aes aes = Aes.Create();
        aes.KeySize = 128;
        aes.BlockSize = 128;
        aes.Mode = CipherMode.CBC;
        aes.Padding = padding;
        aes.Key = key;
        aes.IV = new byte[16];
        return aes;
    }
}

/// <summary>Trophy XML parser from PS4PKGTool's TrophyMetadataParser.</summary>
internal static class TrophyMetadataParser
{
    public static IReadOnlyList<TrophyInfo> Parse(ReadOnlySpan<byte> xmlBytes, string? expectedNpCommunicationId = null)
    {
        string xml = new UTF8Encoding(false, true).GetString(xmlBytes);

        XDocument document;
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = 16 * 1024 * 1024
            };
            using var stringReader = new StringReader(xml);
            using XmlReader reader = XmlReader.Create(stringReader, settings);
            document = XDocument.Load(reader, LoadOptions.None);
        }
        catch (XmlException ex)
        {
            throw new InvalidDataException("Trophy metadata XML is invalid.", ex);
        }

        XElement root = document.Root ?? throw new InvalidDataException("Trophy metadata XML has no root element.");
        if (!string.Equals(root.Name.LocalName, "trophyconf", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Unsupported trophy metadata root element '{root.Name.LocalName}'.");

        string? documentId = Child(root, "npcommid")?.Value.Trim();
        if (expectedNpCommunicationId != null && !string.Equals(documentId, expectedNpCommunicationId, StringComparison.Ordinal))
            throw new InvalidDataException(
                $"Decrypted metadata NP Communication ID '{documentId}' does not match '{expectedNpCommunicationId}'.");

        var groups = root.Elements().Where(e => e.Name.LocalName == "group")
            .Select(e => new { Id = ParseRequiredInt(e, "id"), Name = Child(e, "name")?.Value.Trim() })
            .ToDictionary(g => g.Id, g => string.IsNullOrWhiteSpace(g.Name) ? $"Group {g.Id:000}" : g.Name!);

        var trophies = new List<TrophyInfo>();
        foreach (XElement element in root.Elements().Where(e => e.Name.LocalName == "trophy"))
        {
            int id = ParseRequiredInt(element, "id");
            int? groupId = ParseOptionalInt(element.Attribute("gid")?.Value);
            if (groupId == 0) groupId = null;
            int? platinumId = ParseOptionalInt(element.Attribute("pid")?.Value);
            string hidden = element.Attribute("hidden")?.Value ?? "no";
            if (!hidden.Equals("yes", StringComparison.OrdinalIgnoreCase) &&
                !hidden.Equals("no", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Trophy {id:000} has invalid hidden value '{hidden}'.");

            trophies.Add(new TrophyInfo
            {
                Id = id,
                Name = Child(element, "name")?.Value.Trim() ?? string.Empty,
                Description = Child(element, "detail")?.Value.Trim() ?? string.Empty,
                Grade = ParseGrade(element.Attribute("ttype")?.Value),
                IsHidden = hidden.Equals("yes", StringComparison.OrdinalIgnoreCase),
                GroupId = groupId,
                GroupName = groupId.HasValue ? groups.GetValueOrDefault(groupId.Value, $"Group {groupId:000}") : "Base Game",
                PlatinumId = platinumId
            });
        }

        if (trophies.Count == 0)
            throw new InvalidDataException("Trophy metadata contains no trophy definitions.");
        if (trophies.Select(t => t.Id).Distinct().Count() != trophies.Count)
            throw new InvalidDataException("Trophy metadata contains duplicate trophy IDs.");
        return trophies;
    }

    private static XElement? Child(XElement parent, string localName) =>
        parent.Elements().FirstOrDefault(e => e.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase));

    private static int ParseRequiredInt(XElement element, string attributeName)
    {
        string? value = element.Attribute(attributeName)?.Value;
        if (!int.TryParse(value, System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out int result) || result < 0)
            throw new InvalidDataException($"Element {element.Name.LocalName} has invalid {attributeName} '{value}'.");
        return result;
    }

    private static int? ParseOptionalInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!int.TryParse(value, System.Globalization.NumberStyles.AllowLeadingSign,
                System.Globalization.CultureInfo.InvariantCulture, out int result))
            throw new InvalidDataException($"Invalid trophy relationship ID '{value}'.");
        if (result == -1) return null;
        if (result < 0) throw new InvalidDataException($"Invalid trophy relationship ID '{value}'.");
        return result;
    }

    private static TrophyGrade ParseGrade(string? value) => value?.ToUpperInvariant() switch
    {
        "B" => TrophyGrade.Bronze,
        "S" => TrophyGrade.Silver,
        "G" => TrophyGrade.Gold,
        "P" => TrophyGrade.Platinum,
        _ => TrophyGrade.Unknown
    };
}

/// <summary>Trophy metadata orchestration from PS4PKGTool's TrophyMetadataService (in-memory).</summary>
internal sealed class TrophyMetadataService
{
    private static readonly Regex TrophyIconName =
        new(@"^TROP(?<id>\d{3})\.PNG$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public TrophyMetadataResult Read(byte[] trpData, string? explicitNpCommunicationId)
    {
        TrpArchive archive = TrpArchiveReader.Read(trpData);
        TrpEntry? metadataEntry = SelectMetadataEntry(archive);
        if (metadataEntry == null)
            return Failure(null, TrophyMetadataFailureKind.MissingMetadata,
                "Metadata unavailable: no trophy XML or ESFM entry was found.");

        if (metadataEntry.Size > 32 * 1024 * 1024)
            return Failure(metadataEntry, TrophyMetadataFailureKind.UnsupportedMetadata,
                $"Metadata unavailable: {metadataEntry.Name} exceeds the 32 MiB safety limit.");

        byte[] payload = TrpArchiveReader.ReadEntry(trpData, metadataEntry);
        TrophyResourceKind kind = TrophyResourceDetector.Detect(payload, metadataEntry);
        if (kind == TrophyResourceKind.Xml)
            return ParseAndAttach(trpData, archive, metadataEntry, payload, explicitNpCommunicationId, false);
        if (kind != TrophyResourceKind.EncryptedEsfm)
            return Failure(metadataEntry, TrophyMetadataFailureKind.UnsupportedMetadata,
                $"Metadata unavailable: {metadataEntry.Name} is {kind}, not a supported ESFM payload.");

        if (string.IsNullOrWhiteSpace(explicitNpCommunicationId) || !NpCommunicationId.IsValid(explicitNpCommunicationId))
            return Failure(metadataEntry, TrophyMetadataFailureKind.MissingNpCommunicationId,
                "Metadata unavailable: NP Communication ID is required (NPWRxxxxx_00).");

        try
        {
            byte[] xml = new EsfmDecryptor().Decrypt(payload, explicitNpCommunicationId);
            return ParseAndAttach(trpData, archive, metadataEntry, xml, explicitNpCommunicationId, true);
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            return Failure(metadataEntry, TrophyMetadataFailureKind.DecryptionFailed,
                $"Metadata unavailable: {ex.Message}", explicitNpCommunicationId);
        }
    }

    private static TrophyMetadataResult ParseAndAttach(
        byte[] trpData, TrpArchive archive, TrpEntry entry, byte[] xml, string? expectedId, bool decryptionAttempted)
    {
        try
        {
            IReadOnlyList<TrophyInfo> trophies = TrophyMetadataParser.Parse(xml, expectedId);
            trophies = AttachIcons(trpData, archive, trophies);
            return new TrophyMetadataResult
            {
                Trophies = trophies,
                NpCommunicationId = expectedId,
                MetadataEntryName = entry.Name,
                FailureKind = TrophyMetadataFailureKind.None,
                StatusMessage = $"Loaded {trophies.Count} trophies from {entry.Name}."
            };
        }
        catch (Exception ex)
        {
            return new TrophyMetadataResult
            {
                NpCommunicationId = expectedId,
                MetadataEntryName = entry.Name,
                FailureKind = TrophyMetadataFailureKind.ParseFailed,
                StatusMessage = $"Metadata unavailable: {ex.Message}"
            };
        }
    }

    private static IReadOnlyList<TrophyInfo> AttachIcons(
        byte[] trpData, TrpArchive archive, IReadOnlyList<TrophyInfo> trophies)
    {
        var icons = new Dictionary<int, byte[]>();
        foreach (TrpEntry entry in archive.Entries)
        {
            Match match = TrophyIconName.Match(entry.Name);
            if (!match.Success) continue;
            byte[] bytes = TrpArchiveReader.ReadEntry(trpData, entry);
            if (TrophyResourceDetector.Detect(bytes, entry) != TrophyResourceKind.Png) continue;
            icons.TryAdd(int.Parse(match.Groups["id"].Value), bytes);
        }

        return trophies.Select(trophy => new TrophyInfo
        {
            Id = trophy.Id,
            Name = trophy.Name,
            Description = trophy.Description,
            Grade = trophy.Grade,
            IsHidden = trophy.IsHidden,
            GroupId = trophy.GroupId,
            GroupName = trophy.GroupName,
            PlatinumId = trophy.PlatinumId,
            IconData = icons.TryGetValue(trophy.Id, out byte[]? icon) ? icon : null
        }).ToArray();
    }

    private static TrpEntry? SelectMetadataEntry(TrpArchive archive)
    {
        string[] preferred = ["TROP.ESFM", "TROP.SFM", "TROPCONF.ESFM", "TROPCONF.SFM"];
        foreach (string name in preferred)
        {
            TrpEntry? match = archive.Entries.FirstOrDefault(e =>
                e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;
        }
        return archive.Entries.FirstOrDefault(e =>
            e.Name.EndsWith(".ESFM", StringComparison.OrdinalIgnoreCase) ||
            e.Name.EndsWith(".SFM", StringComparison.OrdinalIgnoreCase));
    }

    private static TrophyMetadataResult Failure(
        TrpEntry? entry, TrophyMetadataFailureKind kind, string message, string? id = null) => new()
    {
        NpCommunicationId = id,
        MetadataEntryName = entry?.Name ?? string.Empty,
        FailureKind = kind,
        StatusMessage = message
    };
}
