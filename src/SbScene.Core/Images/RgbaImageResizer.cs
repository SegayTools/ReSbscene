namespace SbScene.Core.Images;

/// <summary>
/// 提供 RGBA 图像缩放工具，用于按目标尺寸或保持比例重采样图像。
/// </summary>
public static class RgbaImageResizer
{
    /// <summary>
    /// 处理Proportional 的尺寸或区域，生成新的图像结果。
    /// </summary>
    /// <param name="images">参与本次处理的图像或输入对象。</param>
    /// <param name="targetWidth">目标宽度或参与尺寸计算的宽度。</param>
    /// <param name="targetHeight">目标高度或参与尺寸计算的高度。</param>
    /// <returns>处理尺寸或区域后的图像。</returns>
    public static IReadOnlyList<RgbaImage> ResizeProportional(
        IReadOnlyList<RgbaImage> images,
        int? targetWidth,
        int? targetHeight)
    {
        ArgumentNullException.ThrowIfNull(images);
        if (images.Count == 0)
        {
            return [];
        }

        var (width, height) = ResolveProportionalSize(images[0].Width, images[0].Height, targetWidth, targetHeight);
        return images.Select(image => ResizeBilinear(image, width, height)).ToArray();
    }

    /// <summary>
    /// 根据单边目标尺寸计算等比例缩放后的输出尺寸。
    /// </summary>
    /// <param name="sourceWidth">源图像宽度。</param>
    /// <param name="sourceHeight">源图像高度。</param>
    /// <param name="targetWidth">调用方请求的目标宽度；指定时会按比例推导高度。</param>
    /// <param name="targetHeight">调用方请求的目标高度；指定时会按比例推导宽度。</param>
    /// <returns>保持源图像比例后的输出宽高。</returns>
    public static (int Width, int Height) ResolveProportionalSize(
        int sourceWidth,
        int sourceHeight,
        int? targetWidth,
        int? targetHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceHeight);
        if (targetWidth is not null && targetHeight is not null)
        {
            throw new ArgumentException("Only one target dimension can be specified when preserving aspect ratio.");
        }

        if (targetWidth is int requestedWidth)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requestedWidth);
            var resolvedHeight = Math.Max(1, (int)Math.Round(sourceHeight * (requestedWidth / (double)sourceWidth)));
            return (requestedWidth, resolvedHeight);
        }

        if (targetHeight is int requestedHeight)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requestedHeight);
            var resolvedWidth = Math.Max(1, (int)Math.Round(sourceWidth * (requestedHeight / (double)sourceHeight)));
            return (resolvedWidth, requestedHeight);
        }

        return (sourceWidth, sourceHeight);
    }

    /// <summary>
    /// 处理Bilinear 的尺寸或区域，生成新的图像结果。
    /// </summary>
    /// <param name="input">参与本次处理的图像或输入对象。</param>
    /// <param name="width">目标宽度或参与尺寸计算的宽度。</param>
    /// <param name="height">目标高度或参与尺寸计算的高度。</param>
    /// <returns>处理尺寸或区域后的图像。</returns>
    public static RgbaImage ResizeBilinear(RgbaImage input, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (input.Width == width && input.Height == height)
        {
            return input;
        }

        var output = new byte[checked(width * height * 4)];
        var scaleX = input.Width / (double)width;
        var scaleY = input.Height / (double)height;
        for (var y = 0; y < height; y++)
        {
            var sourceY = (y + 0.5) * scaleY - 0.5;
            var y0 = Math.Clamp((int)Math.Floor(sourceY), 0, input.Height - 1);
            var y1 = Math.Min(y0 + 1, input.Height - 1);
            var ty = Math.Clamp(sourceY - y0, 0, 1);
            for (var x = 0; x < width; x++)
            {
                var sourceX = (x + 0.5) * scaleX - 0.5;
                var x0 = Math.Clamp((int)Math.Floor(sourceX), 0, input.Width - 1);
                var x1 = Math.Min(x0 + 1, input.Width - 1);
                var tx = Math.Clamp(sourceX - x0, 0, 1);

                ReadPremultipliedPixel(input, x0, y0, out var r00, out var g00, out var b00, out var a00);
                ReadPremultipliedPixel(input, x1, y0, out var r10, out var g10, out var b10, out var a10);
                ReadPremultipliedPixel(input, x0, y1, out var r01, out var g01, out var b01, out var a01);
                ReadPremultipliedPixel(input, x1, y1, out var r11, out var g11, out var b11, out var a11);

                var topR = Lerp(r00, r10, tx);
                var topG = Lerp(g00, g10, tx);
                var topB = Lerp(b00, b10, tx);
                var topA = Lerp(a00, a10, tx);
                var bottomR = Lerp(r01, r11, tx);
                var bottomG = Lerp(g01, g11, tx);
                var bottomB = Lerp(b01, b11, tx);
                var bottomA = Lerp(a01, a11, tx);

                var premulR = Lerp(topR, bottomR, ty);
                var premulG = Lerp(topG, bottomG, ty);
                var premulB = Lerp(topB, bottomB, ty);
                var alpha = Lerp(topA, bottomA, ty);
                var destinationOffset = (y * width + x) * 4;
                if (alpha <= 0)
                {
                    continue;
                }

                output[destinationOffset] = ToByte(premulR / alpha);
                output[destinationOffset + 1] = ToByte(premulG / alpha);
                output[destinationOffset + 2] = ToByte(premulB / alpha);
                output[destinationOffset + 3] = ToByte(alpha * 255.0);
            }
        }

        return new RgbaImage(width, height, output);
    }

    private static void ReadPremultipliedPixel(RgbaImage image, int x, int y, out double r, out double g, out double b, out double a)
    {
        var offset = (y * image.Width + x) * 4;
        a = image.Pixels[offset + 3] / 255.0;
        r = image.Pixels[offset] * a;
        g = image.Pixels[offset + 1] * a;
        b = image.Pixels[offset + 2] * a;
    }

    private static double Lerp(double left, double right, double amount)
    {
        return left + (right - left) * amount;
    }

    private static byte ToByte(double value)
    {
        return (byte)Math.Clamp((int)Math.Round(value), byte.MinValue, byte.MaxValue);
    }
}
