namespace Lusamine.ImageIdentifier.Decoders;

/// <summary>
/// WebP: a RIFF container ("RIFF"...."WEBP") wrapping one of three bitstream chunks —
/// "VP8 " (lossy), "VP8L" (lossless) or "VP8X" (extended). Each encodes the canvas size
/// differently; all variants fit within the first 32 header bytes.
/// </summary>
public sealed class WebpDecoder : IImageFormatDecoder
{
    public ImageFormat Format => ImageFormat.Webp;

    public bool CanDecode(ReadOnlySpan<byte> header) =>
        header.Length >= 12 &&
        header[..4].SequenceEqual("RIFF"u8) &&
        header.Slice(8, 4).SequenceEqual("WEBP"u8);

    public ImageInfo? Decode(ReadOnlySpan<byte> header, IByteReader reader)
    {
        if (header.Length < 16)
            return null;

        var chunk = header.Slice(12, 4);

        if (chunk.SequenceEqual("VP8 "u8))
        {
            // Lossy: keyframe start code at 23-25, then 14-bit width/height.
            if (header.Length < 30 || header[23] != 0x9D || header[24] != 0x01 || header[25] != 0x2A)
                return null;
            var width = (header[26] | (header[27] << 8)) & 0x3FFF;
            var height = (header[28] | (header[29] << 8)) & 0x3FFF;
            return Make(width, height);
        }

        if (chunk.SequenceEqual("VP8L"u8))
        {
            // Lossless: 0x2F signature at 20, then 14-bit (width-1) and (height-1).
            if (header.Length < 25 || header[20] != 0x2F)
                return null;
            int b0 = header[21], b1 = header[22], b2 = header[23], b3 = header[24];
            var width = 1 + (((b1 & 0x3F) << 8) | b0);
            var height = 1 + (((b3 & 0x0F) << 10) | (b2 << 2) | ((b1 & 0xC0) >> 6));
            return Make(width, height);
        }

        if (chunk.SequenceEqual("VP8X"u8))
        {
            // Extended: 24-bit (canvas width-1) at 24, (height-1) at 27.
            if (header.Length < 30)
                return null;
            var width = 1 + (header[24] | (header[25] << 8) | (header[26] << 16));
            var height = 1 + (header[27] | (header[28] << 8) | (header[29] << 16));
            return Make(width, height);
        }

        return null;
    }

    private static ImageInfo? Make(int width, int height) =>
        width > 0 && height > 0 ? new ImageInfo(ImageFormat.Webp, width, height) : null;
}
