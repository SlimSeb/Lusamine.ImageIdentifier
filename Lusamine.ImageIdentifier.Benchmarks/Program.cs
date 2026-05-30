using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Lusamine.ImageIdentifier;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

BenchmarkSwitcher
    .FromTypes([typeof(IdentifyBenchmarks), typeof(EdgeCaseBenchmarks)])
    .Run(args);

/// <summary>
/// Compares this library against ImageSharp's <c>Image.Identify</c> at the same task:
/// reading an image's format and dimensions from a stream without decoding pixels.
/// Real images are encoded once with ImageSharp so both identifiers see valid files.
/// </summary>
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class IdentifyBenchmarks
{
    private static readonly ImageIdentifier Identifier = new();

    /// <summary>The encoded image under test; rewound and re-read each invocation.</summary>
    private byte[] _data = [];

    /// <summary>
    /// Image dimensions. Large enough that fully decoding (which this library never does)
    /// would dwarf header parsing, highlighting the streaming advantage.
    /// </summary>
    private const int Width = 4000;
    private const int Height = 3000;

    [Params("png", "jpeg", "gif", "bmp", "webp", "tiff")]
    public string Format = "png";

    [GlobalSetup]
    public void Setup()
    {
        using var image = new Image<Rgba32>(Width, Height);
        using var ms = new MemoryStream();
        switch (Format)
        {
            case "png": image.SaveAsPng(ms); break;
            case "jpeg": image.SaveAsJpeg(ms); break;
            case "gif": image.SaveAsGif(ms); break;
            case "bmp": image.SaveAsBmp(ms); break;
            case "webp": image.SaveAsWebp(ms); break;
            case "tiff": image.SaveAsTiff(ms); break;
            default: throw new ArgumentOutOfRangeException(nameof(Format), Format, null);
        }

        _data = ms.ToArray();
    }

    [Benchmark(Baseline = true)]
    public (int, int) Lusamine()
    {
        using var stream = new MemoryStream(_data, writable: false);
        var info = Identifier.Identify(stream);
        return (info!.Width, info.Height);
    }

    [Benchmark]
    public (int, int) ImageSharp()
    {
        using var stream = new MemoryStream(_data, writable: false);
        var info = Image.Identify(stream);
        return (info.Width, info.Height);
    }
}

/// <summary>
/// Exercises the failure paths: data that is unrecognized, truncated, or corrupt. Lusamine
/// returns <c>null</c> for these, while ImageSharp throws, so the ImageSharp arm measures the
/// realistic cost of a try/catch around <c>Image.Identify</c>. This is where header-only
/// sniffing pays off most: rejecting junk is nearly free and never allocates an exception.
/// </summary>
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class EdgeCaseBenchmarks
{
    private static readonly ImageIdentifier Identifier = new();

    private byte[] _data = [];

    /// <summary>
    /// <list type="bullet">
    /// <item><c>empty</c> : zero bytes; nothing to sniff.</item>
    /// <item><c>random</c> : 4 KB of noise matching no signature (unsupported format).</item>
    /// <item><c>text</c> : UTF-8 text, the classic "wrong file" case.</item>
    /// <item><c>truncated-png</c> : a valid PNG signature with the rest of the header cut off.</item>
    /// <item><c>corrupt-jpeg</c> : a valid JPEG magic followed by garbage instead of segments.</item>
    /// </list>
    /// </summary>
    [Params("empty", "random", "text", "truncated-png", "corrupt-jpeg")]
    public string Scenario = "random";

    [GlobalSetup]
    public void Setup()
    {
        switch (Scenario)
        {
            case "empty":
                _data = [];
                break;
            case "random":
                _data = new byte[4096];
                new Random(20260529).NextBytes(_data);
                // Guarantee the first bytes never collide with a real magic number.
                _data[0] = 0x00;
                _data[1] = 0x01;
                _data[2] = 0x02;
                _data[3] = 0x03;
                break;
            case "text":
                _data = "This is not an image, just some plain ASCII text content."u8.ToArray();
                break;
            case "truncated-png":
                // PNG signature only; no IHDR follows, so dimensions can't be read.
                _data = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
                break;
            case "corrupt-jpeg":
                // SOI marker (FF D8) then 30 bytes of junk instead of valid segments.
                _data = new byte[32];
                new Random(1).NextBytes(_data);
                _data[0] = 0xFF;
                _data[1] = 0xD8;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(Scenario), Scenario, null);
        }
    }

    [Benchmark(Baseline = true)]
    public bool Lusamine()
    {
        using var stream = new MemoryStream(_data, writable: false);
        return Identifier.Identify(stream) is not null;
    }

    [Benchmark]
    public bool ImageSharp()
    {
        using var stream = new MemoryStream(_data, writable: false);
        try
        {
            return Image.Identify(stream) is not null;
        }
        catch (Exception)
        {
            // ImageSharp signals "can't identify" by throwing; Lusamine returns null.
            return false;
        }
    }
}
