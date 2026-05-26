namespace Lusamine.ImageIdentifier.Tests;

/// <summary>
/// A non-seekable, read-only stream wrapper that also hands back small chunks per Read call,
/// exercising the readers' partial-read handling and the no-seek code paths.
/// </summary>
internal sealed class ForwardOnlyStream(byte[] data, int maxChunk = 3) : Stream
{
    private int _pos;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var available = Math.Min(Math.Min(count, maxChunk), data.Length - _pos);
        if (available <= 0)
            return 0;
        Array.Copy(data, _pos, buffer, offset, available);
        _pos += available;
        return available;
    }

    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
