namespace Lusamine.ImageIdentifier.Decoders;

/// <summary>
/// SVG: XML-based vector format whose root element is &lt;svg&gt;. Dimensions are extracted
/// from the <c>width</c>/<c>height</c> attributes (integer or <c>px</c> values only; relative
/// units such as <c>%</c>, <c>em</c>, <c>cm</c> are not resolved) or the <c>viewBox</c>
/// attribute as a fallback. Files beginning with an XML declaration (<c>&lt;?xml</c>) are also
/// probed up to 4 KB ahead to locate the svg root.
/// </summary>
public sealed class SvgDecoder : IImageFormatDecoder
{
    private const int MaxScanBytes = 4096;

    public ImageFormat Format => ImageFormat.Svg;

    public bool CanDecode(ReadOnlySpan<byte> header)
    {
        var h = StripBom(header);
        if (FindSvgTag(h) >= 0)
            return true;
        // Also accept files that open with an XML declaration; Decode will confirm the root is <svg>.
        return h.Length >= 5 &&
               h[0] == '<' && h[1] == '?' &&
               (h[2] | 0x20) == 'x' && (h[3] | 0x20) == 'm' && (h[4] | 0x20) == 'l';
    }

    public ImageInfo? Decode(ReadOnlySpan<byte> header, IByteReader reader)
    {
        // Read enough to cover the opening <svg ...> element including its attributes.
        var buf = new byte[MaxScanBytes];
        var total = 0;
        while (total < buf.Length)
        {
            var n = reader.Read(buf.AsSpan(total));
            if (n == 0) break;
            total += n;
        }

        var data = StripBom(buf.AsSpan(0, total));

        var tagStart = FindSvgTag(data);
        if (tagStart < 0)
            return null;

        var afterOpen = data[tagStart..];
        var closeIdx = afterOpen.IndexOf((byte)'>');
        if (closeIdx < 0)
            return null;

        var tag = afterOpen[..(closeIdx + 1)];

        var width  = GetPixelAttr(tag, "width"u8);
        var height = GetPixelAttr(tag, "height"u8);
        if (width > 0 && height > 0)
            return new ImageInfo(ImageFormat.Svg, width, height);

        if (TryParseViewBox(tag, out var vbW, out var vbH))
            return new ImageInfo(ImageFormat.Svg, vbW, vbH);

        return null;
    }

    private static ReadOnlySpan<byte> StripBom(ReadOnlySpan<byte> data) =>
        data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF
            ? data[3..] : data;

    // Finds the byte offset of the first <svg...> open tag (case-insensitive element name).
    private static int FindSvgTag(ReadOnlySpan<byte> data)
    {
        for (var i = 0; i + 4 <= data.Length; i++)
        {
            if (data[i] != '<') continue;
            if ((data[i + 1] | 0x20) != 's') continue;
            if ((data[i + 2] | 0x20) != 'v') continue;
            if ((data[i + 3] | 0x20) != 'g') continue;
            var next = i + 4 < data.Length ? data[i + 4] : (byte)' ';
            if (IsWs(next) || next is (byte)'>' or (byte)'/') return i;
        }
        return -1;
    }

    // Returns the integer pixel value of a dimensional attribute, or 0 if absent/relative.
    private static int GetPixelAttr(ReadOnlySpan<byte> tag, ReadOnlySpan<byte> name)
    {
        var idx = FindAttr(tag, name);
        if (idx < 0) return 0;

        var rest = tag[(idx + name.Length)..];
        var pos = SkipWs(rest, 0);
        if (pos >= rest.Length || rest[pos] != '=') return 0;
        pos = SkipWs(rest, pos + 1);
        if (pos >= rest.Length) return 0;

        var quote = rest[pos++];
        if (quote is not ((byte)'"' or (byte)'\'')) return 0;

        var numStart = pos;
        while (pos < rest.Length && rest[pos] >= '0' && rest[pos] <= '9') pos++;
        if (pos == numStart) return 0;
        var value = ParseInt(rest.Slice(numStart, pos - numStart));

        // Skip optional fractional part (truncate to integer).
        if (pos < rest.Length && rest[pos] == '.')
        {
            pos++;
            while (pos < rest.Length && rest[pos] >= '0' && rest[pos] <= '9') pos++;
        }

        if (pos >= rest.Length) return 0;
        var after = rest[pos];

        // Bare number ("100") or px suffix ("100px") — both treated as pixel values.
        if (after == quote || IsWs(after)) return value;
        if ((after | 0x20) == 'p' &&
            pos + 1 < rest.Length && (rest[pos + 1] | 0x20) == 'x' &&
            (pos + 2 >= rest.Length || rest[pos + 2] == quote || IsWs(rest[pos + 2])))
            return value;

        return 0; // relative unit (%, em, cm, …)
    }

    // Parses viewBox="min-x min-y width height" and returns the width/height integers.
    private static bool TryParseViewBox(ReadOnlySpan<byte> tag, out int width, out int height)
    {
        width = 0;
        height = 0;

        var idx = FindAttr(tag, "viewBox"u8);
        if (idx < 0) return false;

        var rest = tag[(idx + 7)..]; // 7 == "viewBox".Length
        var pos = SkipWs(rest, 0);
        if (pos >= rest.Length || rest[pos] != '=') return false;
        pos = SkipWs(rest, pos + 1);
        if (pos >= rest.Length) return false;

        var quote = rest[pos++];
        if (quote is not ((byte)'"' or (byte)'\'')) return false;

        var nums = new int[4];
        for (var n = 0; n < 4; n++)
        {
            while (pos < rest.Length && (IsWs(rest[pos]) || rest[pos] == ',')) pos++;
            if (pos >= rest.Length || rest[pos] == quote) return false;

            // min-x/min-y may be negative; width/height must be positive.
            var negative = rest[pos] == '-';
            if (negative)
            {
                if (n >= 2) return false;
                pos++;
            }

            var start = pos;
            while (pos < rest.Length && rest[pos] >= '0' && rest[pos] <= '9') pos++;
            if (pos == start) return false;
            if (n >= 2) nums[n] = ParseInt(rest.Slice(start, pos - start));

            // Skip fractional part.
            if (pos < rest.Length && rest[pos] == '.') { pos++; while (pos < rest.Length && rest[pos] >= '0' && rest[pos] <= '9') pos++; }
        }

        width  = nums[2];
        height = nums[3];
        return width > 0 && height > 0;
    }

    // Finds the start of an attribute name inside a tag, verifying word boundaries.
    private static int FindAttr(ReadOnlySpan<byte> tag, ReadOnlySpan<byte> name)
    {
        var offset = 0;
        while (offset + name.Length <= tag.Length)
        {
            var rel = tag[offset..].IndexOf(name);
            if (rel < 0) return -1;
            var abs = offset + rel;
            // Must be preceded by whitespace (not in the middle of another identifier).
            if (abs > 0 && !IsWs(tag[abs - 1])) { offset = abs + 1; continue; }
            // Must be followed by '=', whitespace, or end of tag (word boundary).
            var after = abs + name.Length;
            if (after < tag.Length && !IsWs(tag[after]) && tag[after] != '=') { offset = abs + 1; continue; }
            return abs;
        }
        return -1;
    }

    private static int SkipWs(ReadOnlySpan<byte> data, int pos)
    {
        while (pos < data.Length && IsWs(data[pos])) pos++;
        return pos;
    }

    private static bool IsWs(byte b) => b is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n';

    private static int ParseInt(ReadOnlySpan<byte> digits)
    {
        var result = 0;
        foreach (var b in digits)
        {
            if (b < '0' || b > '9') break;
            if (result > (int.MaxValue - 9) / 10) return 0;
            result = result * 10 + (b - '0');
        }
        return result;
    }
}
