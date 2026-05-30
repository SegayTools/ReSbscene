namespace SbScene.Core.Images;

public sealed class RgbaImage
{
    public RgbaImage(int width, int height, byte[] pixels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (pixels.Length != width * height * 4)
        {
            throw new ArgumentException("RGBA pixel buffer size does not match image dimensions.", nameof(pixels));
        }

        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public int Width { get; }

    public int Height { get; }

    public byte[] Pixels { get; }

    public RgbaImage Crop(int left, int top, int width, int height)
    {
        if (left < 0 || top < 0 || width <= 0 || height <= 0 || left + width > Width || top + height > Height)
        {
            throw new ArgumentOutOfRangeException(nameof(left), $"Invalid crop rectangle {left},{top},{width},{height} for {Width}x{Height}.");
        }

        var output = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            Buffer.BlockCopy(Pixels, ((top + y) * Width + left) * 4, output, y * width * 4, width * 4);
        }

        return new RgbaImage(width, height, output);
    }

    public RgbaImage CropWithTransparentPadding(int left, int top, int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), $"Invalid crop size {width}x{height}.");
        }

        var output = new byte[width * height * 4];
        var sourceLeft = Math.Max(0, left);
        var sourceTop = Math.Max(0, top);
        var sourceRight = Math.Min(Width, left + width);
        var sourceBottom = Math.Min(Height, top + height);
        if (sourceRight <= sourceLeft || sourceBottom <= sourceTop)
        {
            return new RgbaImage(width, height, output);
        }

        var destinationLeft = sourceLeft - left;
        var copyWidth = sourceRight - sourceLeft;
        for (var y = sourceTop; y < sourceBottom; y++)
        {
            var destinationY = y - top;
            Buffer.BlockCopy(Pixels, (y * Width + sourceLeft) * 4, output, (destinationY * width + destinationLeft) * 4, copyWidth * 4);
        }

        return new RgbaImage(width, height, output);
    }
}
