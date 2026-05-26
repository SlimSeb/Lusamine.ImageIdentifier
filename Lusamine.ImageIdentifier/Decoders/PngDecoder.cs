using System.Buffers.Binary;

namespace Lusamine.ImageIdentifier.Decoders;

/// <summary>
/// PNG: 8-byte signature, then an IHDR chunk whose width/height are big-endian uint32 at
/// offsets 16 and 20.
/// </summary>
public sealed class PngDecoder : IImageFormatDecoder
{
    private static ReadOnlySpan<byte> Signature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public bool CanDecode(ReadOnlySpan<byte> header) =>
        header.Length >= Signature.Length && header[..Signature.Length].SequenceEqual(Signature);

    public ImageInfo? Decode(ReadOnlySpan<byte> header, IByteReader reader)
    {
        if (header.Length < 24)
            return null;

        var width = BinaryPrimitives.ReadUInt32BigEndian(header.Slice(16, 4));
        var height = BinaryPrimitives.ReadUInt32BigEndian(header.Slice(20, 4));
        if (width == 0 || height == 0 || width > int.MaxValue || height > int.MaxValue)
            return null;

        return new ImageInfo(ImageFormat.Png, (int)width, (int)height);
    }
}
