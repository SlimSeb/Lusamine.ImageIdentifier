namespace Lusamine.ImageIdentifier;

/// <summary>
/// Reads bytes from a <see cref="Stream"/> on demand. For seekable streams forward skips
/// use <see cref="Stream.Seek"/>; otherwise bytes are read and discarded so non-seekable
/// streams (e.g. network/compression streams) are still supported.
/// </summary>
public sealed class StreamByteReader : IByteReader
{
    private readonly Stream _stream;
    private long _position;

    /// <summary>Wraps a readable stream. The stream is read from its current position.</summary>
    public StreamByteReader(Stream stream)
    {
        if (stream is null)
            throw new ArgumentNullException(nameof(stream));
        if (!stream.CanRead)
            throw new ArgumentException("Stream must be readable.", nameof(stream));
        _stream = stream;
    }

    /// <inheritdoc />
    public long Position => _position;

    /// <inheritdoc />
    public int Read(Span<byte> buffer)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = _stream.Read(buffer[total..]);
            if (read == 0)
                break;
            total += read;
        }
        _position += total;
        return total;
    }

    /// <inheritdoc />
    public bool TrySkip(long count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Backward seeks are not supported.");
        if (count == 0)
            return true;

        if (_stream.CanSeek)
        {
            var remaining = _stream.Length - _stream.Position;
            if (count > remaining)
            {
                _stream.Seek(0, SeekOrigin.End);
                _position += remaining;
                return false;
            }
            _stream.Seek(count, SeekOrigin.Current);
            _position += count;
            return true;
        }

        Span<byte> scratch = stackalloc byte[4096];
        var left = count;
        while (left > 0)
        {
            var chunk = (int)Math.Min(left, scratch.Length);
            var read = _stream.Read(scratch[..chunk]);
            if (read == 0)
            {
                _position += count - left;
                return false;
            }
            left -= read;
        }
        _position += count;
        return true;
    }
}
