namespace Lusamine.ImageIdentifier;

/// <summary>
/// Presents an already-sniffed header prefix followed by the rest of an underlying reader as
/// a single continuous stream starting at position 0. This lets the identifier peek at a
/// file's leading bytes to choose a decoder, then hand that decoder a reader it can walk from
/// the very beginning, without re-reading or buffering anything beyond the prefix.
/// </summary>
public sealed class PrefixedByteReader : IByteReader
{
    private readonly byte[] _prefix;
    private readonly IByteReader _inner;
    private int _prefixPos;

    /// <summary>
    /// Wraps an <paramref name="inner"/> reader that has already consumed <paramref name="prefix"/>,
    /// re-serving those bytes first so reads start at position 0.
    /// </summary>
    public PrefixedByteReader(byte[] prefix, IByteReader inner)
    {
        _prefix = prefix ?? throw new ArgumentNullException(nameof(prefix));
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    // The inner reader has already consumed the prefix, so its Position continues seamlessly
    // once the buffered prefix is exhausted.
    /// <inheritdoc />
    public long Position => _prefixPos < _prefix.Length ? _prefixPos : _inner.Position;

    /// <inheritdoc />
    public int Read(Span<byte> buffer)
    {
        var fromPrefix = Math.Min(buffer.Length, _prefix.Length - _prefixPos);
        if (fromPrefix > 0)
        {
            _prefix.AsSpan(_prefixPos, fromPrefix).CopyTo(buffer);
            _prefixPos += fromPrefix;
        }
        if (fromPrefix == buffer.Length)
            return fromPrefix;
        return fromPrefix + _inner.Read(buffer[fromPrefix..]);
    }

    /// <inheritdoc />
    public bool TrySkip(long count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Backward seeks are not supported.");

        var fromPrefix = (int)Math.Min(count, _prefix.Length - _prefixPos);
        if (fromPrefix > 0)
        {
            _prefixPos += fromPrefix;
            count -= fromPrefix;
        }
        return count == 0 || _inner.TrySkip(count);
    }
}
