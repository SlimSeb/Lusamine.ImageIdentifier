using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Lusamine.ImageIdentifier;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

BenchmarkRunner.Run<IdentifyBenchmarks>();

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
