using Lusamine.ImageIdentifier.Decoders;

namespace Lusamine.ImageIdentifier;

/// <summary>
/// Identifies an image's format and pixel dimensions by reading only its header. Never
/// loading the full image into memory. Works over any readable <see cref="Stream"/> and, for
/// non-seekable streams, by reading bytes forward only.
/// </summary>
public sealed class ImageIdentifier : IImageIdentifier
{
    /// <summary>
    /// Bytes sniffed up front for format detection. Large enough that every header-resident
    /// format (PNG, GIF, BMP, WebP, ICO) can read its dimensions directly from this prefix.
    /// </summary>
    public const int HeaderSize = 32;

    private readonly IReadOnlyList<IImageFormatDecoder> _decoders;

    /// <summary>Creates an identifier with all built-in format decoders.</summary>
    public ImageIdentifier() : this(DefaultDecoders())
    {
    }

    /// <summary>
    /// Creates an identifier with a custom decoder set. Decoders are tried in order; the first
    /// whose <see cref="IImageFormatDecoder.CanDecode"/> matches wins.
    /// </summary>
    public ImageIdentifier(IEnumerable<IImageFormatDecoder> decoders)
    {
        ArgumentNullException.ThrowIfNull(decoders);
        _decoders = decoders.ToArray();
    }

    /// <summary>The built-in decoders, in detection order.</summary>
    public static IReadOnlyList<IImageFormatDecoder> DefaultDecoders() =>
    [
        new PngDecoder(),
        new JpegDecoder(),
        new GifDecoder(),
        new BmpDecoder(),
        new WebpDecoder(),
        new TiffDecoder(),
        new IcoDecoder(),
        new SvgDecoder()
    ];

    /// <summary>
    /// Identifies the image in <paramref name="stream"/>. The stream is read from its current
    /// position. Returns <c>null</c> if no decoder recognizes the data or the header is
    /// truncated/malformed.
    /// </summary>
    public ImageInfo? Identify(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return Identify(new StreamByteReader(stream));
    }

    /// <summary>
    /// Identifies the image exposed by <paramref name="reader"/>, starting at its current
    /// position. Returns <c>null</c> if unrecognized or malformed.
    /// </summary>
    public ImageInfo? Identify(IByteReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var buffer = new byte[HeaderSize];
        var read = 0;
        while (read < buffer.Length)
        {
            var n = reader.Read(buffer.AsSpan(read));
            if (n == 0)
                break;
            read += n;
        }

        var prefix = read == buffer.Length ? buffer : buffer[..read];
        ReadOnlySpan<byte> header = prefix;

        foreach (var decoder in _decoders)
        {
            if (!decoder.CanDecode(header))
                continue;
            var combined = new PrefixedByteReader(prefix, reader);
            return decoder.Decode(header, combined);
        }

        return null;
    }
}
