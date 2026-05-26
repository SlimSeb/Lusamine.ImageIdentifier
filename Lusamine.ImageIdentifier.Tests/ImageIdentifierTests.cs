namespace Lusamine.ImageIdentifier.Tests;

public class ImageIdentifierTests
{
    private readonly ImageIdentifier _identifier = new();

    public static IEnumerable<object[]> Samples()
    {
        yield return [ImageFormat.Png, TestImages.Png(640, 480), 640, 480];
        yield return [ImageFormat.Jpeg, TestImages.Jpeg(1920, 1080), 1920, 1080];
        yield return [ImageFormat.Gif, TestImages.Gif(320, 200), 320, 200];
        yield return [ImageFormat.Bmp, TestImages.Bmp(100, 250), 100, 250];
        yield return [ImageFormat.Ico, TestImages.Ico(48, 48), 48, 48];
        yield return [ImageFormat.Ico, TestImages.Ico(256, 256), 256, 256];
        yield return [ImageFormat.Webp, TestImages.WebpVp8X(16384, 8192), 16384, 8192];
        yield return [ImageFormat.Webp, TestImages.WebpVp8Lossy(800, 600), 800, 600];
        yield return [ImageFormat.Webp, TestImages.WebpVp8Lossless(1024, 768), 1024, 768];
        yield return [ImageFormat.Tiff, TestImages.Tiff(512, 384), 512, 384];
        yield return [ImageFormat.Svg, TestImages.Svg(800, 600), 800, 600];
        yield return [ImageFormat.Svg, TestImages.SvgPxUnits(1920, 1080), 1920, 1080];
        yield return [ImageFormat.Svg, TestImages.SvgViewBoxOnly(640, 480), 640, 480];
        yield return [ImageFormat.Svg, TestImages.SvgWithXmlDecl(300, 150), 300, 150];
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void Identify_seekable_stream_returns_format_and_dimensions(
        ImageFormat format, byte[] data, int width, int height)
    {
        using var stream = new MemoryStream(data);

        var info = _identifier.Identify(stream);

        Assert.NotNull(info);
        Assert.Equal(format, info.Format);
        Assert.Equal(width, info.Width);
        Assert.Equal(height, info.Height);
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void Identify_non_seekable_chunked_stream_returns_same_result(
        ImageFormat format, byte[] data, int width, int height)
    {
        using var stream = new ForwardOnlyStream(data);

        var info = _identifier.Identify(stream);

        Assert.NotNull(info);
        Assert.Equal(format, info.Format);
        Assert.Equal(width, info.Width);
        Assert.Equal(height, info.Height);
    }

    [Fact]
    public void Identify_appends_trailing_data_without_reading_it_all()
    {
        // A valid PNG header followed by megabytes of pixel data: identification must not
        // depend on (or consume) the trailing bulk.
        var header = TestImages.Png(42, 7);
        var full = new byte[header.Length + 5_000_000];
        header.CopyTo(full, 0);
        using var stream = new TrackingStream(full);

        var info = _identifier.Identify(stream);

        Assert.Equal(new ImageInfo(ImageFormat.Png, 42, 7), info);
        Assert.True(stream.MaxBytesRead < 1024,
            $"Expected to read only the header, but read {stream.MaxBytesRead} bytes.");
    }

    [Fact]
    public void Identify_unknown_data_returns_null()
    {
        using var stream = new MemoryStream("not an image at all"u8.ToArray());
        Assert.Null(_identifier.Identify(stream));
    }

    [Fact]
    public void Identify_empty_stream_returns_null()
    {
        using var stream = new MemoryStream([]);
        Assert.Null(_identifier.Identify(stream));
    }

    [Fact]
    public void Identify_truncated_png_returns_null()
    {
        // Valid signature but the IHDR dimensions are cut off.
        var truncated = TestImages.Png(10, 10)[..12];
        using var stream = new MemoryStream(truncated);
        Assert.Null(_identifier.Identify(stream));
    }

    public static IEnumerable<object[]> RealSamples()
    {
        yield return ["die.png",        ImageFormat.Png,  800,  600];
        yield return ["clock.gif",      ImageFormat.Gif,  900,  900];
        yield return ["flower.jpg",     ImageFormat.Jpeg, 500,  477];
        yield return ["wikipedia.webp", ImageFormat.Webp, 1024, 935];
        yield return ["logo.svg",       ImageFormat.Svg,  300,  300];
    }

    [Theory]
    [MemberData(nameof(RealSamples))]
    public void Identify_real_image_seekable(string file, ImageFormat format, int width, int height)
    {
        using var stream = OpenResource(file);

        var info = _identifier.Identify(stream);

        Assert.NotNull(info);
        Assert.Equal(format, info.Format);
        Assert.Equal(width, info.Width);
        Assert.Equal(height, info.Height);
    }

    [Theory]
    [MemberData(nameof(RealSamples))]
    public void Identify_real_image_non_seekable(string file, ImageFormat format, int width, int height)
    {
        var bytes = ReadResource(file);
        using var stream = new ForwardOnlyStream(bytes);

        var info = _identifier.Identify(stream);

        Assert.NotNull(info);
        Assert.Equal(format, info.Format);
        Assert.Equal(width, info.Width);
        Assert.Equal(height, info.Height);
    }

    private static Stream OpenResource(string name)
    {
        var asm = typeof(ImageIdentifierTests).Assembly;
        return asm.GetManifestResourceStream($"Lusamine.ImageIdentifier.Tests.Images.{name}")
            ?? throw new InvalidOperationException($"Embedded resource '{name}' not found.");
    }

    private static byte[] ReadResource(string name)
    {
        using var s = OpenResource(name);
        var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    [Fact]
    public void Identify_null_stream_throws()
    {
        Assert.Throws<ArgumentNullException>(() => _identifier.Identify((Stream)null!));
    }

    [Fact]
    public void Custom_decoder_set_is_honored()
    {
        var identifier = new ImageIdentifier([new GifDecoderOnly()]);
        using var png = new MemoryStream(TestImages.Png(10, 10));

        // Only a GIF decoder is registered, so a PNG is unrecognized.
        Assert.Null(identifier.Identify(png));

        using var gif = new MemoryStream(TestImages.Gif(10, 10));
        Assert.Equal(ImageFormat.Gif, identifier.Identify(gif)!.Format);
    }

    private sealed class GifDecoderOnly : IImageFormatDecoder
    {
        private readonly Decoders.GifDecoder _inner = new();
        public ImageFormat Format => _inner.Format;
        public bool CanDecode(ReadOnlySpan<byte> header) => _inner.CanDecode(header);
        public ImageInfo? Decode(ReadOnlySpan<byte> header, IByteReader reader) =>
            _inner.Decode(header, reader);
    }
}
