using System.Buffers.Binary;

namespace Lusamine.ImageIdentifier.Decoders;

/// <summary>
/// GIF: "GIF87a"/"GIF89a" signature followed by the logical screen width/height as
/// little-endian uint16 at offsets 6 and 8.
/// </summary>
public sealed class GifDecoder : IImageFormatDecoder
{
    private static ReadOnlySpan<byte> Gif87a => "GIF87a"u8;
    private static ReadOnlySpan<byte> Gif89a => "GIF89a"u8;

    public bool CanDecode(ReadOnlySpan<byte> header)
    {
        if (header.Length < 6)
            return false;
        var sig = header[..6];
        return sig.SequenceEqual(Gif87a) || sig.SequenceEqual(Gif89a);
    }

    public ImageInfo? Decode(ReadOnlySpan<byte> header, IByteReader reader)
    {
        if (header.Length < 10)
            return null;

        var width = BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(6, 2));
        var height = BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(8, 2));
        if (width == 0 || height == 0)
            return null;

        return new ImageInfo(ImageFormat.Gif, width, height);
    }
}
