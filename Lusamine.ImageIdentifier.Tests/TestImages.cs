using System.Buffers.Binary;
using System.Text;

namespace Lusamine.ImageIdentifier.Tests;

/// <summary>Builds minimal-but-valid image headers for the dimensions under test.</summary>
internal static class TestImages
{
    public static byte[] Png(int width, int height)
    {
        var b = new byte[24];
        ReadOnlySpan<byte> sig = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        sig.CopyTo(b);
        BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(8), 13); // IHDR length
        "IHDR"u8.CopyTo(b.AsSpan(12));
        BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(16), (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(20), (uint)height);
        return b;
    }

    public static byte[] Gif(int width, int height)
    {
        var b = new byte[13];
        "GIF89a"u8.CopyTo(b);
        BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(6), (ushort)width);
        BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(8), (ushort)height);
        return b;
    }

    public static byte[] Bmp(int width, int height)
    {
        var b = new byte[54];
        b[0] = (byte)'B';
        b[1] = (byte)'M';
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(14), 40); // BITMAPINFOHEADER
        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(18), width);
        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(22), height);
        return b;
    }

    public static byte[] Ico(int width, int height)
    {
        var b = new byte[8];
        b[2] = 1; // type: icon
        b[4] = 1; // one entry
        b[6] = (byte)(width == 256 ? 0 : width);
        b[7] = (byte)(height == 256 ? 0 : height);
        return b;
    }

    public static byte[] WebpVp8X(int width, int height)
    {
        var b = new byte[30];
        "RIFF"u8.CopyTo(b);
        "WEBP"u8.CopyTo(b.AsSpan(8));
        "VP8X"u8.CopyTo(b.AsSpan(12));
        var w = width - 1;
        var h = height - 1;
        b[24] = (byte)(w & 0xFF);
        b[25] = (byte)((w >> 8) & 0xFF);
        b[26] = (byte)((w >> 16) & 0xFF);
        b[27] = (byte)(h & 0xFF);
        b[28] = (byte)((h >> 8) & 0xFF);
        b[29] = (byte)((h >> 16) & 0xFF);
        return b;
    }

    public static byte[] WebpVp8Lossy(int width, int height)
    {
        var b = new byte[30];
        "RIFF"u8.CopyTo(b);
        "WEBP"u8.CopyTo(b.AsSpan(8));
        "VP8 "u8.CopyTo(b.AsSpan(12));
        b[23] = 0x9D;
        b[24] = 0x01;
        b[25] = 0x2A;
        BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(26), (ushort)(width & 0x3FFF));
        BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(28), (ushort)(height & 0x3FFF));
        return b;
    }

    public static byte[] WebpVp8Lossless(int width, int height)
    {
        var b = new byte[25];
        "RIFF"u8.CopyTo(b);
        "WEBP"u8.CopyTo(b.AsSpan(8));
        "VP8L"u8.CopyTo(b.AsSpan(12));
        b[20] = 0x2F;
        var w = width - 1;
        var h = height - 1;
        // 14 bits width then 14 bits height, little-endian bit order.
        var bits = (uint)(w & 0x3FFF) | ((uint)(h & 0x3FFF) << 14);
        b[21] = (byte)(bits & 0xFF);
        b[22] = (byte)((bits >> 8) & 0xFF);
        b[23] = (byte)((bits >> 16) & 0xFF);
        b[24] = (byte)((bits >> 24) & 0xFF);
        return b;
    }

    /// <summary>JPEG with an APP0 segment before the SOF0, to exercise segment skipping.</summary>
    public static byte[] Jpeg(int width, int height)
    {
        var b = new List<byte> { 0xFF, 0xD8 };

        // APP0 segment (length 4: itself + 2 payload bytes).
        b.AddRange([0xFF, 0xE0, 0x00, 0x04, 0x01, 0x02]);

        // SOF0 segment.
        b.AddRange([0xFF, 0xC0, 0x00, 0x11, 0x08]);
        b.Add((byte)(height >> 8));
        b.Add((byte)(height & 0xFF));
        b.Add((byte)(width >> 8));
        b.Add((byte)(width & 0xFF));
        b.AddRange([0x03, 0x01, 0x22, 0x00]); // partial component data
        return b.ToArray();
    }

    public static byte[] Svg(int width, int height) =>
        Encoding.UTF8.GetBytes(
            $"""<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}"></svg>""");

    public static byte[] SvgPxUnits(int width, int height) =>
        Encoding.UTF8.GetBytes(
            $"""<svg xmlns="http://www.w3.org/2000/svg" width="{width}px" height="{height}px"></svg>""");

    public static byte[] SvgViewBoxOnly(int width, int height) =>
        Encoding.UTF8.GetBytes(
            $"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {width} {height}"></svg>""");

    public static byte[] SvgWithXmlDecl(int width, int height) =>
        Encoding.UTF8.GetBytes(
            $"""<?xml version="1.0" encoding="UTF-8"?><svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}"></svg>""");

    /// <summary>Little-endian TIFF whose IFD sits past the 32-byte sniff window.</summary>
    public static byte[] Tiff(int width, int height)
    {
        var b = new byte[8 + 2 + 24];
        "II"u8.CopyTo(b);
        b[2] = 0x2A;
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(4), 8); // IFD at offset 8
        BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(8), 2); // 2 entries

        WriteEntry(b.AsSpan(10), 0x0100, (ushort)width);  // ImageWidth
        WriteEntry(b.AsSpan(22), 0x0101, (ushort)height); // ImageLength
        return b;

        static void WriteEntry(Span<byte> e, ushort tag, ushort value)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(e, tag);
            BinaryPrimitives.WriteUInt16LittleEndian(e[2..], 3); // type SHORT
            BinaryPrimitives.WriteUInt32LittleEndian(e[4..], 1); // count
            BinaryPrimitives.WriteUInt16LittleEndian(e[8..], value);
        }
    }
}
