namespace Lusamine.ImageIdentifier;

/// <summary>Image container formats recognized by the identifier.</summary>
public enum ImageFormat
{
    /// <summary>Format could not be determined.</summary>
    Unknown = 0,

    /// <summary>Portable Network Graphics.</summary>
    Png,

    /// <summary>JPEG / JFIF.</summary>
    Jpeg,

    /// <summary>Graphics Interchange Format.</summary>
    Gif,

    /// <summary>Windows Bitmap.</summary>
    Bmp,

    /// <summary>Google WebP.</summary>
    Webp,

    /// <summary>Tagged Image File Format.</summary>
    Tiff,

    /// <summary>Windows icon / cursor.</summary>
    Ico,

    /// <summary>Scalable Vector Graphics.</summary>
    Svg
}
