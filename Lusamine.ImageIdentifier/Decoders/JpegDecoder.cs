using System.Buffers.Binary;

namespace Lusamine.ImageIdentifier.Decoders;

/// <summary>
/// JPEG: starts with SOI (0xFFD8), then a chain of marker segments. Each non-standalone
/// marker is followed by a big-endian length; we skip segments until a Start-Of-Frame (SOFn)
/// marker, whose payload carries height then width as big-endian uint16. Only the bytes up to
/// that marker are read.
/// </summary>
public sealed class JpegDecoder : IImageFormatDecoder
{
    public bool CanDecode(ReadOnlySpan<byte> header) =>
        header.Length >= 2 && header[0] == 0xFF && header[1] == 0xD8;

    public ImageInfo? Decode(ReadOnlySpan<byte> header, IByteReader reader)
    {
        Span<byte> buf = stackalloc byte[2];

        // Consume the SOI marker.
        if (!reader.TryReadExact(buf) || buf[0] != 0xFF || buf[1] != 0xD8)
            return null;

        // Refuse to scan beyond this limit to bound work on crafted files with no SOF. 2 MB
        // comfortably covers a max-size EXIF block plus stacked ICC-profile APP2 segments,
        // which can push the SOF well past the first 64 KB on real camera/editor output.
        const long maxScanBytes = 2 * 1024 * 1024;

        while (reader.Position <= maxScanBytes)
        {
            // Find the next marker: 0xFF followed by a marker code (skip fill bytes).
            if (!reader.TryReadExact(buf[..1]))
                return null;
            if (buf[0] != 0xFF)
                continue;

            byte marker;
            do
            {
                if (!reader.TryReadExact(buf[..1]))
                    return null;
                marker = buf[0];
            } while (marker == 0xFF);

            // Standalone markers (no length): SOI, EOI, TEM, RSTn : nothing to size.
            if (marker is 0x01 or 0xD8 or 0xD9 || (marker >= 0xD0 && marker <= 0xD7))
                continue;

            if (!reader.TryReadExact(buf))
                return null;
            var segmentLength = BinaryPrimitives.ReadUInt16BigEndian(buf);
            if (segmentLength < 2)
                return null;

            if (IsStartOfFrame(marker))
            {
                // SOFn payload: precision(1), height(2), width(2).
                Span<byte> sof = stackalloc byte[5];
                if (!reader.TryReadExact(sof))
                    return null;
                var height = BinaryPrimitives.ReadUInt16BigEndian(sof.Slice(1, 2));
                var width = BinaryPrimitives.ReadUInt16BigEndian(sof.Slice(3, 2));
                if (width == 0 || height == 0)
                    return null;
                return new ImageInfo(ImageFormat.Jpeg, width, height);
            }

            if (!reader.TrySkip(segmentLength - 2))
                return null;
        }

        return null;
    }

    private static bool IsStartOfFrame(byte marker)
    {
        // SOF0..SOF15 (0xC0..0xCF) except DHT(0xC4), JPG(0xC8) and DAC(0xCC).
        if (marker < 0xC0 || marker > 0xCF)
            return false;
        return marker is not (0xC4 or 0xC8 or 0xCC);
    }
}
