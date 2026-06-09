namespace SbScene.Core.Images;

/// <summary>
/// 表示内存中的 RGBA 图像，保存尺寸和按像素排列的通道数据。
/// </summary>
public sealed class RgbaImage
{
    /// <summary>
    /// 初始化RGBA 图像 实例，并保存调用方提供的核心数据。
    /// </summary>
    /// <param name="width">目标宽度或参与尺寸计算的宽度。</param>
    /// <param name="height">目标高度或参与尺寸计算的高度。</param>
    /// <param name="pixels">参与几何边界、坐标或变换计算的位置值。</param>
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

    /// <summary>
    /// 获取宽度，用于确定渲染区域、裁剪范围、采样质量或输出尺寸。
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// 获取高度，用于确定渲染区域、裁剪范围、采样质量或输出尺寸。
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// 获取RGBA 像素缓冲区，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public byte[] Pixels { get; }

    /// <summary>
    /// 处理Crop 的尺寸或区域，生成新的图像结果。
    /// </summary>
    /// <param name="left">参与几何边界、坐标或变换计算的位置值。</param>
    /// <param name="top">参与几何边界、坐标或变换计算的位置值。</param>
    /// <param name="width">目标宽度或参与尺寸计算的宽度。</param>
    /// <param name="height">目标高度或参与尺寸计算的高度。</param>
    /// <returns>处理尺寸或区域后的图像。</returns>
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

    /// <summary>
    /// 处理CropWith完全透明颜色值透明边距 的尺寸或区域，生成新的图像结果。
    /// </summary>
    /// <param name="left">参与几何边界、坐标或变换计算的位置值。</param>
    /// <param name="top">参与几何边界、坐标或变换计算的位置值。</param>
    /// <param name="width">目标宽度或参与尺寸计算的宽度。</param>
    /// <param name="height">目标高度或参与尺寸计算的高度。</param>
    /// <returns>处理尺寸或区域后的图像。</returns>
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
