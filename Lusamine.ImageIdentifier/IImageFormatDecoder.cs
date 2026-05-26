namespace Lusamine.ImageIdentifier;

/// <summary>
/// Recognizes a single image format from its header and extracts its dimensions.
/// </summary>
public interface IImageFormatDecoder
{
    /// <summary>
    /// Returns <c>true</c> if <paramref name="header"/> begins with this format's signature.
    /// </summary>
    bool CanDecode(ReadOnlySpan<byte> header);

    /// <summary>
    /// Extracts dimensions. <paramref name="header"/> holds the bytes already read from the
    /// front of the stream; <paramref name="reader"/> is positioned immediately after them
    /// (its <see cref="IByteReader.Position"/> equals <paramref name="header"/>'s length) so
    /// formats whose size lives deeper in the file can keep reading without buffering.
    /// Returns <c>null</c> if the header is malformed or truncated.
    /// </summary>
    ImageInfo? Decode(ReadOnlySpan<byte> header, IByteReader reader);
}
