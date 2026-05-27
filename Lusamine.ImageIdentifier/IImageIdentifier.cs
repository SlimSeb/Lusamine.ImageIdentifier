namespace Lusamine.ImageIdentifier;

public interface IImageIdentifier
{
    /// <summary>
    /// Identifies the image in <paramref name="stream"/>. The stream is read from its current
    /// position. Returns <c>null</c> if no decoder recognizes the data or the header is
    /// truncated/malformed.
    /// </summary>
    ImageInfo? Identify(Stream stream);

    /// <summary>
    /// Identifies the image exposed by <paramref name="reader"/>, starting at its current
    /// position. Returns <c>null</c> if unrecognized or malformed.
    /// </summary>
    ImageInfo? Identify(IByteReader reader);
}