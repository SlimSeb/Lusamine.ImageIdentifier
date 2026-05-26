using System.Buffers.Binary;

namespace Lusamine.ImageIdentifier.Decoders;

/// <summary>
/// TIFF: an 8-byte header giving byte order ("II"/"MM"), the magic number 42 and the offset
/// to the first IFD. The IFD is a count of 12-byte entries; ImageWidth (tag 0x0100) and
/// ImageLength (tag 0x0101) hold the dimensions. Only the header and that one IFD are read.
/// </summary>
public sealed class TiffDecoder : IImageFormatDecoder
{
    private static ReadOnlySpan<byte> LittleEndian => [0x49, 0x49, 0x2A, 0x00];
    private static ReadOnlySpan<byte> BigEndian => [0x4D, 0x4D, 0x00, 0x2A];

    public ImageFormat Format => ImageFormat.Tiff;

    public bool CanDecode(ReadOnlySpan<byte> header) =>
        header.Length >= 4 &&
        (header[..4].SequenceEqual(LittleEndian) || header[..4].SequenceEqual(BigEndian));

    public ImageInfo? Decode(ReadOnlySpan<byte> header, IByteReader reader)
    {
        Span<byte> head = stackalloc byte[8];
        if (!reader.TryReadExact(head))
            return null;

        var little = head[0] == 0x49;
        var ifdOffset = ReadU32(head.Slice(4, 4), little);

        // Seek forward to the IFD. Backward offsets and suspiciously large offsets are rejected
        // to prevent resource exhaustion on non-seekable streams.
        const uint maxIfdOffset = 128 * 1024 * 1024;
        if (ifdOffset < reader.Position || ifdOffset > maxIfdOffset || !reader.TrySkip(ifdOffset - reader.Position))
            return null;

        Span<byte> countBuf = stackalloc byte[2];
        if (!reader.TryReadExact(countBuf))
            return null;
        var entryCount = ReadU16(countBuf, little);

        long? width = null, height = null;
        Span<byte> entry = stackalloc byte[12];
        for (var i = 0; i < entryCount; i++)
        {
            if (!reader.TryReadExact(entry))
                return null;

            var tag = ReadU16(entry[..2], little);
            if (tag is not (0x0100 or 0x0101))
                continue;

            var type = ReadU16(entry.Slice(2, 2), little);
            var value = type switch
            {
                3 => ReadU16(entry.Slice(8, 2), little), // SHORT
                4 => ReadU32(entry.Slice(8, 4), little), // LONG
                _ => 0u
            };
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

    private static uint ReadU16(ReadOnlySpan<byte> b, bool little) =>
        little ? BinaryPrimitives.ReadUInt16LittleEndian(b) : BinaryPrimitives.ReadUInt16BigEndian(b);

    private static uint ReadU32(ReadOnlySpan<byte> b, bool little) =>
        little ? BinaryPrimitives.ReadUInt32LittleEndian(b) : BinaryPrimitives.ReadUInt32BigEndian(b);
}
