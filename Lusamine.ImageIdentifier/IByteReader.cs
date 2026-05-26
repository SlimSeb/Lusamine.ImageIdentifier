namespace Lusamine.ImageIdentifier;

/// <summary>
/// A forward-oriented byte source over a stream. Implementations read only the bytes
/// requested, never buffering the whole image into memory.
/// </summary>
public interface IByteReader
{
    /// <summary>Absolute offset (in bytes) of the next byte to be read.</summary>
    long Position { get; }

    /// <summary>
    /// Reads up to <paramref name="buffer"/>'s length, filling as much as the source allows.
    /// Returns the number of bytes read; <c>0</c> means end of stream.
    /// </summary>
    int Read(Span<byte> buffer);

    /// <summary>
    /// Advances the read position forward by <paramref name="count"/> bytes. Returns
    /// <c>false</c> if the stream ends first. Negative seeks are not supported.
    /// </summary>
    bool TrySkip(long count);

    /// <summary>
    /// Fills <paramref name="buffer"/> completely. Returns <c>false</c> if the stream ends
    /// before the buffer is full (its contents are then undefined).
    /// </summary>
    bool TryReadExact(Span<byte> buffer)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = Read(buffer[total..]);
            if (read == 0)
                return false;
            total += read;
        }
        return true;
    }
}
