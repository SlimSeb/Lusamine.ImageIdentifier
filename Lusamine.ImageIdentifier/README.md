# Lusamine.ImageIdentifier

Identify an image's **format** and **pixel dimensions** by reading only its header. The
full image is never loaded into memory. Works over any readable stream, including
non-seekable ones (network, compression).

Supported formats: **PNG, JPEG, GIF, BMP, WebP (VP8/VP8L/VP8X), TIFF, ICO**.

## Usage

```csharp
using Lusamine.ImageIdentifier;

var identifier = new ImageIdentifier();

using var stream = File.OpenRead("photo.jpg");
ImageInfo? info = identifier.Identify(stream);

if (info is not null)
    Console.WriteLine($"{info.Format} {info.Width}x{info.Height}");
```

`Identify` returns `null` when the data is unrecognized or the header is truncated.

## Why

Only the leading header bytes are read (typically well under 1 KB), so identifying a
multi-megabyte image is allocation-light and near-instant. For non-seekable streams the
reader skips forward by reading and discarding, so it still never buffers the whole file.

## Performance

Benchmarks run against [ImageSharp](https://github.com/SixLabors/ImageSharp)'s Image.Identify on .NET 10, Windows 11, Intel Core i7-11800H:

| Library    | Format |             Mean |      Ratio | Allocated |
|------------|--------|-----------------:|-----------:|----------:|
| Lusamine   | bmp    |         58.71 ns |       1.00 |     256 B |
| ImageSharp | bmp    |        618.13 ns |      10.57 |   1,144 B |
| Lusamine   | gif    |         39.53 ns |       1.00 |     224 B |
| ImageSharp | gif    | 25,894,502.47 ns | 658,221.37 |   6,224 B |
| Lusamine   | jpeg   |        116.84 ns |       1.00 |     256 B |
| ImageSharp | jpeg   |      1,883.00 ns |      16.13 |  10,768 B |
| Lusamine   | png    |         42.09 ns |       1.00 |     256 B |
| ImageSharp | png    |      5,390.30 ns |     128.68 |   1,520 B |
| Lusamine   | webp   |         64.48 ns |       1.00 |     256 B |
| ImageSharp | webp   |      1,584.73 ns |      24.66 |   1,424 B |
| Lusamine   | tiff   |        131.42 ns |       1.00 |     256 B |
| ImageSharp | tiff   |     62,062.19 ns |     472.65 | 220,437 B |

And on .NET 10, macOS Tahoe 26.4, Apple M3:

| Library    | Format |             Mean |        Ratio | Allocated |
|------------|--------|-----------------:|-------------:|----------:|
| Lusamine   | bmp    |         35.88 ns |         1.00 |     256 B |
| ImageSharp | bmp    |        459.39 ns |        12.81 |   1,144 B |
| Lusamine   | gif    |         26.55 ns |         1.00 |     224 B |
| ImageSharp | gif    | 41,702,047.39 ns | 1,571,280.33 |   6,224 B |
| Lusamine   | jpeg   |         64.35 ns |         1.00 |     224 B |
| ImageSharp | jpeg   |      1,026.48 ns |        15.96 |  10,768 B |
| Lusamine   | png    |         26.64 ns |         1.00 |     256 B |
| ImageSharp | png    |      7,844.73 ns |       294.64 |   1,520 B |
| Lusamine   | webp   |         39.98 ns |         1.00 |     256 B |
| ImageSharp | webp   |      1,030.49 ns |        25.78 |   1,424 B |
| Lusamine   | tiff   |         78.10 ns |         1.00 |     256 B |
| ImageSharp | tiff   |     36,514.15 ns |       467.55 | 220,440 B |

+Lusamine reads only the image header (typically under 1 KB), while ImageSharp decodes significantly more of the file. The result is consistently **one to several orders of magnitude faster** across formats — from ~11–13x for BMP up to hundreds of thousands of times faster for GIF — with a fraction of the allocations.

## Extensibility

The decoder set is injectable for custom formats or testing:

```csharp
var identifier = new ImageIdentifier(new IImageFormatDecoder[] { new MyFormatDecoder() });
```
