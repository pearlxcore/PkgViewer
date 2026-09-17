using System.Buffers.Binary;
using System.Text;

namespace PkgViewer.Core.Trophies;

internal enum TrophyResourceKind
{
    Unknown,
    Png,
    Xml,
    EncryptedEsfm,
    EsfmMarker,
    EmptyMarker
}

internal sealed class TrpEntry
{
    public int Index { get; init; }
    public string Name { get; init; } = string.Empty;
    public long Offset { get; init; }
    public long Size { get; init; }
    public uint Flags { get; init; }
}

internal sealed class TrpArchive
{
    public uint Version { get; init; }
    public uint EntrySize { get; init; }
    public IReadOnlyList<TrpEntry> Entries { get; init; } = [];
}

/// <summary>In-memory reader for a PS4 TRP archive (magic DCA24D00), ported from PS4PKGTool.</summary>
internal static class TrpArchiveReader
{
    private static readonly byte[] Magic = [0xDC, 0xA2, 0x4D, 0x00];
    private const int CommonHeaderSize = 28;
    private const int VersionOneTwoHeaderSize = 64;
    private const int VersionThreeHeaderSize = 96;
    private const int MinimumEntrySize = 64;
    private const int EntryNameSize = 36;

    public static TrpArchive Read(byte[] data)
    {
        if (data.Length < VersionOneTwoHeaderSize)
            throw new InvalidDataException("The TRP is truncated before its header is complete.");
        if (!data.AsSpan(0, 4).SequenceEqual(Magic))
            throw new InvalidDataException("The file does not have the PS4 TRP magic DCA24D00.");

        uint version = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(4, 4));
        if (version is < 1 or > 3)
            throw new NotSupportedException($"TRP version {version} is not supported.");

        long declaredSize = checked((long)BinaryPrimitives.ReadUInt64BigEndian(data.AsSpan(8, 8)));
        uint entryCount = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(16, 4));
        uint entrySize = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(20, 4));
        int headerSize = version == 3 ? VersionThreeHeaderSize : VersionOneTwoHeaderSize;

        if (declaredSize != data.Length)
            throw new InvalidDataException($"TRP header size {declaredSize} does not match the actual length {data.Length}.");
        if (entrySize < MinimumEntrySize || entrySize > 4096)
            throw new InvalidDataException($"TRP entry size {entrySize} is outside the supported range.");
        long tableSize = checked((long)entryCount * entrySize);
        if (headerSize > data.Length || tableSize > data.Length - headerSize)
            throw new InvalidDataException("The TRP entry table extends beyond the file.");

        var entries = new List<TrpEntry>(checked((int)entryCount));
        int position = headerSize;
        for (int index = 0; index < entryCount; index++)
        {
            ReadOnlySpan<byte> raw = data.AsSpan(position, checked((int)entrySize));
            position += checked((int)entrySize);

            int zero = raw[..EntryNameSize].IndexOf((byte)0);
            int nameLength = zero < 0 ? EntryNameSize : zero;
            string name = new UTF8Encoding(false, true).GetString(raw[..nameLength]);
            long offset = BinaryPrimitives.ReadUInt32BigEndian(raw.Slice(36, 4));
            ulong unsignedSize = BinaryPrimitives.ReadUInt64BigEndian(raw.Slice(40, 8));
            if (unsignedSize > long.MaxValue)
                throw new InvalidDataException($"TRP entry {index} has an unsupported size.");
            long size = (long)unsignedSize;
            uint flags = BinaryPrimitives.ReadUInt32BigEndian(raw.Slice(48, 4));
            ValidateBounds(index, name, offset, size, data.Length);

            entries.Add(new TrpEntry { Index = index, Name = name, Offset = offset, Size = size, Flags = flags });
        }

        return new TrpArchive { Version = version, EntrySize = entrySize, Entries = entries };
    }

    public static byte[] ReadEntry(byte[] data, TrpEntry entry)
    {
        if (entry.Size > int.MaxValue)
            throw new InvalidDataException($"Entry {entry.Name} is too large to process in memory.");
        ValidateBounds(entry.Index, entry.Name, entry.Offset, entry.Size, data.Length);
        return data.AsSpan((int)entry.Offset, (int)entry.Size).ToArray();
    }

    private static void ValidateBounds(int index, string name, long offset, long size, long fileLength)
    {
        if (offset < 0 || size < 0 || offset > fileLength || size > fileLength - offset)
            throw new InvalidDataException(
                $"TRP entry {index} ({name}) points outside the file: offset={offset}, size={size}, file={fileLength}.");
    }
}

/// <summary>Detects the payload kind of a TRP metadata/icon entry, ported from PS4PKGTool.</summary>
internal static class TrophyResourceDetector
{
    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static ReadOnlySpan<byte> EsfmMarker => [0x45, 0x53, 0x46, 0x4D, 0, 0, 0, 0];

    public static TrophyResourceKind Detect(byte[] data, TrpEntry? entry = null)
    {
        ReadOnlySpan<byte> span = data;
        if (span.StartsWith(PngSignature)) return TrophyResourceKind.Png;
        if (span.StartsWith(EsfmMarker) && span.Length == EsfmMarker.Length) return TrophyResourceKind.EsfmMarker;
        if (span.Length == 8 && span.IndexOfAnyExcept((byte)0) < 0) return TrophyResourceKind.EmptyMarker;

        ReadOnlySpan<byte> trimmed = TrimLeadingWhitespace(span);
        if (trimmed.StartsWith("<?xml"u8) || trimmed.StartsWith("<trophy"u8) || trimmed.StartsWith("<!--"u8))
            return TrophyResourceKind.Xml;

        if (entry is not null && entry.Name.EndsWith(".ESFM", StringComparison.OrdinalIgnoreCase) &&
            entry.Flags == 3 && span.Length >= 32 && span.Length % 16 == 0)
            return TrophyResourceKind.EncryptedEsfm;

        return TrophyResourceKind.Unknown;
    }

    private static ReadOnlySpan<byte> TrimLeadingWhitespace(ReadOnlySpan<byte> data)
    {
        int index = 0;
        while (index < data.Length && data[index] is 0x09 or 0x0A or 0x0D or 0x20) index++;
        return data[index..];
    }
}
