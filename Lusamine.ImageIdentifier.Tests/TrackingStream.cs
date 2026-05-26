namespace Lusamine.ImageIdentifier.Tests;

/// <summary>A seekable MemoryStream that records how many bytes were actually read.</summary>
internal sealed class TrackingStream(byte[] data) : MemoryStream(data)
{
    public long MaxBytesRead { get; private set; }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var n = base.Read(buffer, offset, count);
        MaxBytesRead += n;
        return n;
    }

    public override int Read(Span<byte> buffer)
    {
        var n = base.Read(buffer);
        MaxBytesRead += n;
        return n;
    }
}
