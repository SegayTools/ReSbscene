using SbScene.Core.Resources;

namespace SbScene.Core.Rendering;

public static class SbSceneImageCastConventions
{
    public const int AdditiveBlendMask = 0x1;
    public const int HorizontalFlipMask = 0x10;
    public const int VerticalFlipMask = 0x20;

    public static bool HasAdditiveBlendCandidate(SbSceneImageCast imageCast)
    {
        ArgumentNullException.ThrowIfNull(imageCast);
        return HasAdditiveBlendCandidate(imageCast.ImageCastFlags);
    }

    public static bool HasAdditiveBlendCandidate(int packedState)
    {
        return (packedState & AdditiveBlendMask) != 0;
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
}
