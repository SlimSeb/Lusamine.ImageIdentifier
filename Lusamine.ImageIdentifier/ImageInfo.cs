namespace Lusamine.ImageIdentifier;

/// <summary>The format and pixel dimensions extracted from an image's header.</summary>
public sealed record ImageInfo(ImageFormat Format, int Width, int Height);
