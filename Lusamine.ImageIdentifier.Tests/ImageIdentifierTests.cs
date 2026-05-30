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

    /// <summary>
    /// Real files crafted to stress the awkward corners of each format, all of which the
    /// header-only approach resolves: SVGs whose pixel size must fall back to <c>viewBox</c>,
    /// JPEGs whose SOF marker sits behind large APPn metadata, and TIFFs (classic and BigTIFF)
    /// whose first IFD is far from the header.
    /// </summary>
    public static IEnumerable<object[]> EdgeCaseIdentified()
    {
        // width/height ("400") take precedence over the conflicting viewBox.
        yield return ["svg_conflicting.svg",        ImageFormat.Svg,  400, 400];
        // em units can't be resolved to pixels, so the viewBox supplies the size.
        yield return ["svg_em_dimensions.svg",      ImageFormat.Svg,  320, 160];
        // Percentages can't be resolved either; viewBox fallback again.
        yield return ["svg_percent_dimensions.svg", ImageFormat.Svg,  400, 200];
        // No width/height at all; dimensions come straight from the viewBox.
        yield return ["svg_viewbox_only.svg",       ImageFormat.Svg,  200, 100];
        // SOF preceded by a JFIF APP0 then a max-size Exif APP1 (SOF at byte 65,626).
        yield return ["jpeg_app0_then_app1.jpg",    ImageFormat.Jpeg,   8,   8];
        // SOF behind a single monolithic max-size Exif APP1 (SOF at byte 65,608).
        yield return ["jpeg_fat_app1.jpg",          ImageFormat.Jpeg,   8,   8];
        // SOF behind two stacked max-size ICC APP2 segments (SOF at byte 131,145).
        yield return ["jpeg_many_app2.jpg",         ImageFormat.Jpeg,   8,   8];
        // First IFD lives ~200 KB in; reachable by skipping forward.
        yield return ["tiff_far_ifd.tif",           ImageFormat.Tiff,   4,   4];
        // Pixel strips precede the IFD, so the IFD offset is far from the header.
        yield return ["tiff_backward_ifd.tif",      ImageFormat.Tiff,   4,   4];
        // Multiple chained IFDs; only the first is read.
        yield return ["tiff_chained_far.tif",       ImageFormat.Tiff,   4,   4];
        // BigTIFF: magic 43, 64-bit offsets, 20-byte IFD entries.
        yield return ["tiff_bigtiff.tif",           ImageFormat.Tiff,   4,   4];
    }

    [Theory]
    [MemberData(nameof(EdgeCaseIdentified))]
    public void Identify_edge_case_seekable(string file, ImageFormat format, int width, int height)
    {
        using var stream = OpenResource(file);

        var info = _identifier.Identify(stream);

        Assert.NotNull(info);
        Assert.Equal(format, info.Format);
        Assert.Equal(width, info.Width);
        Assert.Equal(height, info.Height);
    }

    [Theory]
    [MemberData(nameof(EdgeCaseIdentified))]
    public void Identify_edge_case_non_seekable(string file, ImageFormat format, int width, int height)
    {
        // Forward-only proves the far-IFD TIFFs are reached by reading-and-discarding,
        // never by seeking.
        using var stream = new ForwardOnlyStream(ReadResource(file));

        var info = _identifier.Identify(stream);

        Assert.NotNull(info);
        Assert.Equal(format, info.Format);
        Assert.Equal(width, info.Width);
        Assert.Equal(height, info.Height);
    }

    /// <summary>
    /// Real files that legitimately cannot yield raster dimensions from the header alone, so
    /// identification returns <c>null</c> by design.
    /// </summary>
    public static IEnumerable<object[]> EdgeCaseUnsupported()
    {
        // No width/height/viewBox: an SVG with no intrinsic raster size.
        yield return ["svg_no_dimensions.svg"];
    }

    [Theory]
    [MemberData(nameof(EdgeCaseUnsupported))]
    public void Identify_edge_case_beyond_limits_returns_null_seekable(string file)
    {
        using var stream = OpenResource(file);
        Assert.Null(_identifier.Identify(stream));
    }

    [Theory]
    [MemberData(nameof(EdgeCaseUnsupported))]
    public void Identify_edge_case_beyond_limits_returns_null_non_seekable(string file)
    {
        using var stream = new ForwardOnlyStream(ReadResource(file));
        Assert.Null(_identifier.Identify(stream));
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

    [Fact]
    public async Task Identification_OverHttp()
    {
        using var httpClient = new HttpClient();
        const string url = "https://i.imgur.com/1HPHmrQ.png";
        await using var stream = await httpClient.GetStreamAsync(url);

        var imageInfo = _identifier.Identify(stream);

        Assert.NotNull(imageInfo);
        Assert.Equal(ImageFormat.Png, imageInfo.Format);
        Assert.Equal(1254, imageInfo.Width);
        Assert.Equal(1254, imageInfo.Height);
    }

    private sealed class GifDecoderOnly : IImageFormatDecoder
    {
        private readonly Decoders.GifDecoder _inner = new();
        public bool CanDecode(ReadOnlySpan<byte> header) => _inner.CanDecode(header);
        public ImageInfo? Decode(ReadOnlySpan<byte> header, IByteReader reader) =>
            _inner.Decode(header, reader);
    }
}
