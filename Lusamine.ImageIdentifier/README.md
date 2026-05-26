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

## Extensibility

The decoder set is injectable for custom formats or testing:

```csharp
var identifier = new ImageIdentifier(new IImageFormatDecoder[] { new MyFormatDecoder() });
```
