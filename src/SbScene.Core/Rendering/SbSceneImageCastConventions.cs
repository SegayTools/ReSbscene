using SbScene.Core.Resources;

namespace SbScene.Core.Rendering;

public readonly record struct SbSceneImageCastGeometry(double Width, double Height, double PivotX, double PivotY);

public static class SbSceneImageCastConventions
{
    private const double Epsilon = 0.000001;

    public const int DrawModeMask = 0xF;
    public const int AdditiveBlendMode = 0x1;
    public const int AdditiveBlendMask = AdditiveBlendMode;
    public const int HorizontalFlipMask = 0x10;
    public const int VerticalFlipMask = 0x20;
    public const int UvModeMask = 0xC0;
    public const int SurfaceModeMask = 0x7800;

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

    public static bool HasAdditiveBlendCandidate(SbSceneImageCast imageCast)
    {
        ArgumentNullException.ThrowIfNull(imageCast);
        return HasAdditiveBlendCandidate(imageCast.ImageCastFlags);
    }

    public static bool HasAdditiveBlendCandidate(int packedState)
    {
        return DecodeDrawMode(packedState) == AdditiveBlendMode;
    }

    public static int DecodeDrawMode(SbSceneImageCast imageCast)
    {
        ArgumentNullException.ThrowIfNull(imageCast);
        return DecodeDrawMode(imageCast.ImageCastFlags);
    }

    public static int DecodeDrawMode(int packedState)
    {
        return packedState & DrawModeMask;
    }

    public static bool HasHorizontalFlip(SbSceneImageCast imageCast)
    {
        ArgumentNullException.ThrowIfNull(imageCast);
        return HasHorizontalFlip(imageCast.ImageCastFlags);
    }

    public static bool HasHorizontalFlip(int packedState)
    {
        return (packedState & HorizontalFlipMask) != 0;
    }

    public static bool HasVerticalFlip(SbSceneImageCast imageCast)
    {
        ArgumentNullException.ThrowIfNull(imageCast);
        return HasVerticalFlip(imageCast.ImageCastFlags);
    }

    public static bool HasVerticalFlip(int packedState)
    {
        return (packedState & VerticalFlipMask) != 0;
    }

    public static int DecodeUvMode(SbSceneImageCast imageCast)
    {
        ArgumentNullException.ThrowIfNull(imageCast);
        return DecodeUvMode(imageCast.ImageCastFlags);
    }

    public static int DecodeUvMode(int packedState)
    {
        return (packedState & UvModeMask) >> 6;
    }

    public static int DecodeSurfaceMode(SbSceneImageCast imageCast)
    {
        ArgumentNullException.ThrowIfNull(imageCast);
        return DecodeSurfaceMode(imageCast.ImageCastFlags);
    }

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
