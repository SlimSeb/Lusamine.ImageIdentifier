using System.Buffers.Binary;

namespace Lusamine.ImageIdentifier.Decoders;

/// <summary>
/// BMP: "BM" signature. The DIB header size at offset 14 distinguishes the legacy
/// BITMAPCOREHEADER (12, uint16 dimensions at 18/20) from later headers (int32 dimensions
/// at 18/22, where a negative height means a top-down bitmap).
/// </summary>
public sealed class BmpDecoder : IImageFormatDecoder
{
    public ImageFormat Format => ImageFormat.Bmp;

    public bool CanDecode(ReadOnlySpan<byte> header) =>
        header.Length >= 2 && header[0] == (byte)'B' && header[1] == (byte)'M';

    public ImageInfo? Decode(ReadOnlySpan<byte> header, IByteReader reader)
    {
        if (header.Length < 26)
            return null;

        var dibSize = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(14, 4));

        int width, height;
        if (dibSize == 12)
        {
            width = BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(18, 2));
            height = BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(20, 2));
        }
        else
        {
            width = BinaryPrimitives.ReadInt32LittleEndian(header.Slice(18, 4));
            height = BinaryPrimitives.ReadInt32LittleEndian(header.Slice(22, 4));
        }

        height = Math.Abs(height);
        if (width <= 0 || height <= 0)
            return null;

        return new ImageInfo(ImageFormat.Bmp, width, height);
    }
}
