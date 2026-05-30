using System.Buffers.Binary;

namespace Lusamine.ImageIdentifier.Decoders;

/// <summary>
/// TIFF: an 8-byte header giving byte order ("II"/"MM") and a version magic, then the offset
/// to the first IFD. The IFD is a count of fixed-size entries; ImageWidth (tag 0x0100) and
/// ImageLength (tag 0x0101) hold the dimensions. Both classic TIFF (magic 42, 32-bit offsets,
/// 12-byte entries) and BigTIFF (magic 43, 64-bit offsets, 20-byte entries) are supported.
/// Only the header and that one IFD are read.
/// </summary>
public sealed class TiffDecoder : IImageFormatDecoder
{
    private static ReadOnlySpan<byte> ClassicLittle => [0x49, 0x49, 0x2A, 0x00];
    private static ReadOnlySpan<byte> ClassicBig => [0x4D, 0x4D, 0x00, 0x2A];
    private static ReadOnlySpan<byte> BigTiffLittle => [0x49, 0x49, 0x2B, 0x00];
    private static ReadOnlySpan<byte> BigTiffBig => [0x4D, 0x4D, 0x00, 0x2B];

    // Backward offsets and suspiciously large offsets are rejected to prevent resource
    // exhaustion (especially on non-seekable streams that skip by reading and discarding).
    private const uint MaxIfdOffset = 128 * 1024 * 1024;

    // Guards against a crafted IFD claiming an absurd entry count. Real IFDs have a handful.
    private const long MaxIfdEntries = 65536;

    public bool CanDecode(ReadOnlySpan<byte> header) =>
        header.Length >= 4 &&
        (header[..4].SequenceEqual(ClassicLittle) || header[..4].SequenceEqual(ClassicBig) ||
         header[..4].SequenceEqual(BigTiffLittle) || header[..4].SequenceEqual(BigTiffBig));

    public ImageInfo? Decode(ReadOnlySpan<byte> header, IByteReader reader)
    {
        Span<byte> head = stackalloc byte[8];
        if (!reader.TryReadExact(head))
            return null;

        var little = head[0] == 0x49;
        var version = ReadU16(head.Slice(2, 2), little);

        if (version == 42)
        {
            // Classic: the first-IFD offset is the last 4 bytes of the 8-byte header.
            var ifdOffset = ReadU32(head.Slice(4, 4), little);
            return SeekAndScan(reader, little, bigTiff: false, ifdOffset);
        }

        if (version == 43)
        {
            // BigTIFF: bytes 4..5 are the offset byte-size (always 8) and 6..7 a reserved zero,
            // both already consumed in head. The 8-byte first-IFD offset follows.
            Span<byte> ifdOffsetBytes = stackalloc byte[8];
            if (!reader.TryReadExact(ifdOffsetBytes))
                return null;
            return SeekAndScan(reader, little, bigTiff: true, ReadU64(ifdOffsetBytes, little));
        }

        return null;
    }

    private static ImageInfo? SeekAndScan(IByteReader reader, bool little, bool bigTiff, ulong ifdOffset)
    {
        if (ifdOffset < (ulong)reader.Position || ifdOffset > MaxIfdOffset ||
            !reader.TrySkip((long)(ifdOffset - (ulong)reader.Position)))
            return null;

        long entryCount;
        if (bigTiff)
        {
            Span<byte> countBuf = stackalloc byte[8];
            if (!reader.TryReadExact(countBuf))
                return null;
            entryCount = (long)ReadU64(countBuf, little);
        }
        else
        {
            Span<byte> countBuf = stackalloc byte[2];
            if (!reader.TryReadExact(countBuf))
                return null;
            entryCount = ReadU16(countBuf, little);
        }

        if (entryCount > MaxIfdEntries)
            return null;

        // Entry layout is tag(2) + type(2) + count + value/offset. Classic uses a 4-byte count
        // and 4-byte value field (12-byte entry, value at offset 8); BigTIFF uses an 8-byte
        // count and 8-byte value field (20-byte entry, value at offset 12).
        var entrySize = bigTiff ? 20 : 12;
        var valueFieldOffset = bigTiff ? 12 : 8;
        var valueFieldSize = bigTiff ? 8 : 4;

        long? width = null, height = null;
        Span<byte> entry = stackalloc byte[20];
        for (var i = 0; i < entryCount; i++)
        {
            var e = entry[..entrySize];
            if (!reader.TryReadExact(e))
                return null;

            var tag = ReadU16(e[..2], little);
            if (tag is not (0x0100 or 0x0101))
                continue;

            var type = ReadU16(e.Slice(2, 2), little);
            var value = ReadTagValue(e.Slice(valueFieldOffset, valueFieldSize), type, little);
            if (value == 0)
                continue;

            if (tag == 0x0100)
                width = value;
            else
                height = value;

            if (width is not null && height is not null)
                break;
        }

        if (width is null or 0 || height is null or 0 || width > int.MaxValue || height > int.MaxValue)
            return null;
        return new ImageInfo(ImageFormat.Tiff, (int)width.Value, (int)height.Value);
    }

    private static long ReadTagValue(ReadOnlySpan<byte> valueField, uint type, bool little) =>
        type switch
        {
            3 => ReadU16(valueField[..2], little),                                  // SHORT
            4 => ReadU32(valueField[..4], little),                                  // LONG
            16 when valueField.Length >= 8 => (long)ReadU64(valueField[..8], little), // LONG8 (BigTIFF)
            _ => 0
        };

    private static uint ReadU16(ReadOnlySpan<byte> b, bool little) =>
        little ? BinaryPrimitives.ReadUInt16LittleEndian(b) : BinaryPrimitives.ReadUInt16BigEndian(b);

    private static uint ReadU32(ReadOnlySpan<byte> b, bool little) =>
        little ? BinaryPrimitives.ReadUInt32LittleEndian(b) : BinaryPrimitives.ReadUInt32BigEndian(b);

    private static ulong ReadU64(ReadOnlySpan<byte> b, bool little) =>
        little ? BinaryPrimitives.ReadUInt64LittleEndian(b) : BinaryPrimitives.ReadUInt64BigEndian(b);
}
