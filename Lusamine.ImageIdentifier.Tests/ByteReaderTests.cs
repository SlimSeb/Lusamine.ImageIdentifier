namespace Lusamine.ImageIdentifier.Tests;

public class ByteReaderTests
{
    [Fact]
    public void StreamByteReader_tracks_position_and_reads_exact()
    {
        IByteReader reader = new StreamByteReader(new MemoryStream([1, 2, 3, 4, 5]));
        Span<byte> buf = stackalloc byte[3];
        byte[] expected = [1, 2, 3];

        Assert.True(reader.TryReadExact(buf));
        Assert.True(buf.SequenceEqual(expected));
        Assert.Equal(3, reader.Position);
    }

    [Fact]
    public void StreamByteReader_try_read_exact_fails_past_end()
    {
        IByteReader reader = new StreamByteReader(new MemoryStream([1, 2]));
        Span<byte> buf = stackalloc byte[4];
        Assert.False(reader.TryReadExact(buf));
    }

    [Fact]
    public void StreamByteReader_skip_seekable_then_read()
    {
        IByteReader reader = new StreamByteReader(new MemoryStream([0, 1, 2, 3, 4, 5]));
        Assert.True(reader.TrySkip(4));
        Assert.Equal(4, reader.Position);

        Span<byte> buf = stackalloc byte[1];
        Assert.True(reader.TryReadExact(buf));
        Assert.Equal(4, buf[0]);
    }

    [Fact]
    public void StreamByteReader_skip_non_seekable_then_read()
    {
        IByteReader reader = new StreamByteReader(new ForwardOnlyStream([0, 1, 2, 3, 4, 5]));
        Assert.True(reader.TrySkip(4));
        Assert.Equal(4, reader.Position);

        Span<byte> buf = stackalloc byte[1];
        Assert.True(reader.TryReadExact(buf));
        Assert.Equal(4, buf[0]);
    }

    [Fact]
    public void StreamByteReader_skip_past_end_returns_false()
    {
        IByteReader reader = new StreamByteReader(new MemoryStream([0, 1, 2]));
        Assert.False(reader.TrySkip(10));
    }

    [Fact]
    public void StreamByteReader_rejects_negative_skip()
    {
        IByteReader reader = new StreamByteReader(new MemoryStream([0, 1, 2]));
        Assert.Throws<ArgumentOutOfRangeException>(() => reader.TrySkip(-1));
    }

    [Fact]
    public void PrefixedByteReader_reads_across_prefix_boundary()
    {
        // Per the contract, the inner reader has already consumed the 3 prefix bytes.
        var inner = new StreamByteReader(new MemoryStream([1, 2, 3, 4, 5, 6]));
        inner.TrySkip(3);
        IByteReader prefixed = new PrefixedByteReader([1, 2, 3], inner);

        Span<byte> buf = stackalloc byte[5];
        byte[] expected = [1, 2, 3, 4, 5];
        Assert.True(prefixed.TryReadExact(buf));
        Assert.True(buf.SequenceEqual(expected));
        Assert.Equal(5, prefixed.Position);
    }

    [Fact]
    public void PrefixedByteReader_position_continues_from_inner()
    {
        // Inner reader must report position as if the prefix had been consumed from it.
        var inner = new StreamByteReader(new MemoryStream([0, 1, 2, 3, 4, 5, 6]));
        inner.TrySkip(3); // consume the 3 prefix bytes from the real stream
        IByteReader prefixed = new PrefixedByteReader([0, 1, 2], inner);

        Span<byte> buf = stackalloc byte[4];
        byte[] expected = [0, 1, 2, 3];
        Assert.True(prefixed.TryReadExact(buf));
        Assert.True(buf.SequenceEqual(expected));
        Assert.Equal(4, prefixed.Position);
    }

    [Fact]
    public void PrefixedByteReader_skip_spans_prefix_and_inner()
    {
        var inner = new StreamByteReader(new MemoryStream([0, 1, 2, 3, 4, 5, 6, 7]));
        inner.TrySkip(3);
        IByteReader prefixed = new PrefixedByteReader([0, 1, 2], inner);

        Assert.True(prefixed.TrySkip(5)); // 3 from prefix + 2 from inner
        Assert.Equal(5, prefixed.Position);

        Span<byte> buf = stackalloc byte[1];
        Assert.True(prefixed.TryReadExact(buf));
        Assert.Equal(5, buf[0]);
    }
}
