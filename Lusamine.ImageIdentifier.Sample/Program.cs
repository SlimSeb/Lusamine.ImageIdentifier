using Lusamine.ImageIdentifier;

var identifier = new ImageIdentifier();

if (args.Length == 0)
{
    Console.WriteLine("Usage: dotnet run -- <image-path> [<image-path> ...]");
    Console.WriteLine();
    Console.WriteLine("Identifies each image's format and dimensions by reading only its");
    Console.WriteLine("header. The file is never fully loaded into memory.");
    return;
}

foreach (var path in args)
{
    if (!File.Exists(path))
    {
        Console.WriteLine($"{path}: file not found");
        continue;
    }

    // FileStream is read lazily; only the header bytes are ever pulled in.
    using var stream = File.OpenRead(path);
    var info = identifier.Identify(stream);

    Console.WriteLine(info is null
        ? $"{path}: unrecognized image format"
        : $"{path}: {info.Format} {info.Width}x{info.Height}");
}
