using SbScene.Core.Resources;

namespace SbScene.Core.Rendering;

/// <summary>
/// 表示 image cast 的几何尺寸和 pivot 信息，用于把 SVO 裁剪区域映射到场景节点。
/// </summary>
/// <param name="Width">目标宽度或参与尺寸计算的宽度。</param>
/// <param name="Height">目标高度或参与尺寸计算的高度。</param>
/// <param name="PivotX">参与几何边界、坐标或变换计算的位置值。</param>
/// <param name="PivotY">参与几何边界、坐标或变换计算的位置值。</param>
public readonly record struct SbSceneImageCastGeometry(double Width, double Height, double PivotX, double PivotY);

/// <summary>
/// 提供sbscene 场景图像Cast约定，统一封装项目内约定的格式转换和命名规则。
/// </summary>
public static class SbSceneImageCastConventions
{
    private const double Epsilon = 0.000001;

    /// <summary>
    /// 表示Draw模式Mask，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public const int DrawModeMask = 0xF;
    /// <summary>
    /// 表示AdditiveBlend模式，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public const int AdditiveBlendMode = 0x1;
    /// <summary>
    /// 表示AdditiveBlendMask，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public const int AdditiveBlendMask = AdditiveBlendMode;
    /// <summary>
    /// 表示HorizontalFlipMask，用于表示状态开关或检测结果，调用方据此选择显示、解析、导出或诊断分支。
    /// </summary>
    public const int HorizontalFlipMask = 0x10;
    /// <summary>
    /// 表示VerticalFlipMask，用于表示状态开关或检测结果，调用方据此选择显示、解析、导出或诊断分支。
    /// </summary>
    public const int VerticalFlipMask = 0x20;
    /// <summary>
    /// 表示Uv模式Mask，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public const int UvModeMask = 0xC0;
    /// <summary>
    /// 表示Surface模式Mask，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public const int SurfaceModeMask = 0x7800;

    /// <summary>
    /// 根据动画后的宽高重新计算 image cast 的几何尺寸和 pivot。
    /// </summary>
    /// <param name="imageCast">参与本次处理的图像或输入对象。</param>
    /// <param name="width">目标宽度或参与尺寸计算的宽度。</param>
    /// <param name="height">目标高度或参与尺寸计算的高度。</param>
    /// <returns>包含动画后宽高和缩放后 pivot 的几何信息。</returns>
    public static SbSceneImageCastGeometry ResolveAnimatedGeometry(
        SbSceneImageCast imageCast,
        double width,
        double height)
    {
        ArgumentNullException.ThrowIfNull(imageCast);

        return new SbSceneImageCastGeometry(
            width,
            height,
            ScalePivot(imageCast.PivotX, imageCast.Width, width),
            ScalePivot(imageCast.PivotY, imageCast.Height, height));
    }

    /// <summary>
    /// 判断 image cast 的绘制模式是否表示加法混合。
    /// </summary>
    /// <param name="imageCast">参与本次处理的图像或输入对象。</param>
    /// <returns>如果条件成立则为 true；否则为 false。</returns>
    public static bool HasAdditiveBlendCandidate(SbSceneImageCast imageCast)
    {
        ArgumentNullException.ThrowIfNull(imageCast);
        return HasAdditiveBlendCandidate(imageCast.ImageCastFlags);
    }

    /// <summary>
    /// 判断打包状态中的绘制模式是否表示加法混合。
    /// </summary>
    /// <param name="packedState">image cast 中保存 draw、UV、翻转和 surface 标志的打包状态。</param>
    /// <returns>如果条件成立则为 true；否则为 false。</returns>
    public static bool HasAdditiveBlendCandidate(int packedState)
    {
        return DecodeDrawMode(packedState) == AdditiveBlendMode;
    }

    /// <summary>
    /// 解码Draw模式，将原始或压缩字节转换为可处理的像素数据。
    /// </summary>
    /// <param name="imageCast">参与本次处理的图像或输入对象。</param>
    /// <returns>解码后的图像或像素数据。</returns>
    public static int DecodeDrawMode(SbSceneImageCast imageCast)
    {
        ArgumentNullException.ThrowIfNull(imageCast);
        return DecodeDrawMode(imageCast.ImageCastFlags);
    }

    /// <summary>
    /// 解码Draw模式，将原始或压缩字节转换为可处理的像素数据。
    /// </summary>
    /// <param name="packedState">image cast 中保存 draw、UV、翻转和 surface 标志的打包状态。</param>
    /// <returns>解码后的图像或像素数据。</returns>
    public static int DecodeDrawMode(int packedState)
    {
        return packedState & DrawModeMask;
    }

    /// <summary>
    /// 判断 image cast 是否设置了水平翻转标志。
    /// </summary>
    /// <param name="imageCast">参与本次处理的图像或输入对象。</param>
    /// <returns>如果条件成立则为 true；否则为 false。</returns>
    public static bool HasHorizontalFlip(SbSceneImageCast imageCast)
    {
        ArgumentNullException.ThrowIfNull(imageCast);
        return HasHorizontalFlip(imageCast.ImageCastFlags);
    }

    /// <summary>
    /// 判断打包状态是否设置了水平翻转标志。
    /// </summary>
    /// <param name="packedState">image cast 中保存 draw、UV、翻转和 surface 标志的打包状态。</param>
    /// <returns>如果条件成立则为 true；否则为 false。</returns>
    public static bool HasHorizontalFlip(int packedState)
    {
        return (packedState & HorizontalFlipMask) != 0;
    }

    /// <summary>
    /// 判断 image cast 是否设置了垂直翻转标志。
    /// </summary>
    /// <param name="imageCast">参与本次处理的图像或输入对象。</param>
    /// <returns>如果条件成立则为 true；否则为 false。</returns>
    public static bool HasVerticalFlip(SbSceneImageCast imageCast)
    {
        ArgumentNullException.ThrowIfNull(imageCast);
        return HasVerticalFlip(imageCast.ImageCastFlags);
    }

    /// <summary>
    /// 判断打包状态是否设置了垂直翻转标志。
    /// </summary>
    /// <param name="packedState">image cast 中保存 draw、UV、翻转和 surface 标志的打包状态。</param>
    /// <returns>如果条件成立则为 true；否则为 false。</returns>
    public static bool HasVerticalFlip(int packedState)
    {
        return (packedState & VerticalFlipMask) != 0;
    }

    /// <summary>
    /// 解码Uv模式，将原始或压缩字节转换为可处理的像素数据。
    /// </summary>
    /// <param name="imageCast">参与本次处理的图像或输入对象。</param>
    /// <returns>解码后的图像或像素数据。</returns>
    public static int DecodeUvMode(SbSceneImageCast imageCast)
    {
        ArgumentNullException.ThrowIfNull(imageCast);
        return DecodeUvMode(imageCast.ImageCastFlags);
    }

    /// <summary>
    /// 解码Uv模式，将原始或压缩字节转换为可处理的像素数据。
    /// </summary>
    /// <param name="packedState">image cast 中保存 draw、UV、翻转和 surface 标志的打包状态。</param>
    /// <returns>解码后的图像或像素数据。</returns>
    public static int DecodeUvMode(int packedState)
    {
        return (packedState & UvModeMask) >> 6;
    }

    /// <summary>
    /// 解码Surface模式，将原始或压缩字节转换为可处理的像素数据。
    /// </summary>
    /// <param name="imageCast">参与本次处理的图像或输入对象。</param>
    /// <returns>解码后的图像或像素数据。</returns>
    public static int DecodeSurfaceMode(SbSceneImageCast imageCast)
    {
        ArgumentNullException.ThrowIfNull(imageCast);
        return DecodeSurfaceMode(imageCast.ImageCastFlags);
    }

    /// <summary>
    /// 解码Surface模式，将原始或压缩字节转换为可处理的像素数据。
    /// </summary>
    /// <param name="packedState">image cast 中保存 draw、UV、翻转和 surface 标志的打包状态。</param>
    /// <returns>解码后的图像或像素数据。</returns>
    public static int DecodeSurfaceMode(int packedState)
    {
        return (packedState & SurfaceModeMask) switch
        {
            0x0800 => 1,
            0x1000 => 2,
            0x1800 => 3,
            0x2000 => 4,
            _ => 0,
        };
    }

    /// <summary>
    /// 解析纹理Coordinate，将调用方输入归一化为后续处理可直接使用的值。
    /// </summary>
    /// <param name="u">未应用 UV 模式前的水平纹理坐标。</param>
    /// <param name="v">未应用 UV 模式前的垂直纹理坐标。</param>
    /// <returns>应用翻转和 UV 模式后的纹理坐标。</returns>
    public static (double U, double V) ResolveTextureCoordinate(double u, double v, int packedState)
    {
        var x = ClampUnit(u);
        var y = ClampUnit(v);
        var left = HasHorizontalFlip(packedState) ? 1.0 : 0.0;
        var right = HasHorizontalFlip(packedState) ? 0.0 : 1.0;
        var top = HasVerticalFlip(packedState) ? 1.0 : 0.0;
        var bottom = HasVerticalFlip(packedState) ? 0.0 : 1.0;

        var (topLeft, bottomLeft, topRight, bottomRight) = DecodeUvMode(packedState) switch
        {
            1 => ((right, top), (left, top), (right, bottom), (left, bottom)),
            2 => ((right, bottom), (right, top), (left, bottom), (left, top)),
            3 => ((left, bottom), (right, bottom), (left, top), (right, top)),
            _ => ((left, top), (left, bottom), (right, top), (right, bottom)),
        };

        var topU = Lerp(topLeft.Item1, topRight.Item1, x);
        var topV = Lerp(topLeft.Item2, topRight.Item2, x);
        var bottomU = Lerp(bottomLeft.Item1, bottomRight.Item1, x);
        var bottomV = Lerp(bottomLeft.Item2, bottomRight.Item2, x);
        return (Lerp(topU, bottomU, y), Lerp(topV, bottomV, y));
    }

    private static double ScalePivot(double pivot, double initialSize, double animatedSize)
    {
        return double.IsFinite(initialSize) && initialSize > Epsilon
            && double.IsFinite(animatedSize) && animatedSize > Epsilon
                ? pivot * animatedSize / initialSize
                : pivot;
    }

    private static double ClampUnit(double value)
    {
        return double.IsFinite(value) ? Math.Clamp(value, 0.0, 1.0) : 0.0;
    }

    private static double Lerp(double left, double right, double amount)
    {
        return left + (right - left) * amount;
    }
}
