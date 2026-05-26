namespace Lusamine.ImageIdentifier.Decoders;

/// <summary>
/// ICO/CUR: reserved uint16 of 0, then type 1 (icon) or 2 (cursor). The first directory
/// entry stores width/height as single bytes at offsets 6 and 7, where 0 encodes 256.
/// Reports the dimensions of that first entry.
/// </summary>
public sealed class IcoDecoder : IImageFormatDecoder
{
    public bool CanDecode(ReadOnlySpan<byte> header)
    {
        if (header.Length < 6)
            return false;
        if (header[0] != 0 || header[1] != 0)
            return false;
        var type = header[2] | (header[3] << 8);
        if (type is not (1 or 2))
            return false;
        var count = header[4] | (header[5] << 8);
        return count > 0;
    }

    public ImageInfo? Decode(ReadOnlySpan<byte> header, IByteReader reader)
    {
        if (header.Length < 8)
            return null;

        var width = header[6] == 0 ? 256 : header[6];
        var height = header[7] == 0 ? 256 : header[7];
        return new ImageInfo(ImageFormat.Ico, width, height);
    }
}
