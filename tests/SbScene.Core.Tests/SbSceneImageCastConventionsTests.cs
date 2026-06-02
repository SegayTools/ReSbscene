using SbScene.Core.Rendering;
using SbScene.Core.Resources;

namespace SbScene.Core.Tests;

public sealed class SbSceneImageCastConventionsTests
{
    [Fact]
    public void ResolveAnimatedGeometryScalesPivotWithAnimatedDimensions()
    {
        var imageCast = ImageCast(width: 150, height: 65, pivotX: 75, pivotY: 32.5f);

        var geometry = SbSceneImageCastConventions.ResolveAnimatedGeometry(imageCast, width: 150, height: 26);

        Assert.Equal(150, geometry.Width, precision: 6);
        Assert.Equal(26, geometry.Height, precision: 6);
        Assert.Equal(75, geometry.PivotX, precision: 6);
        Assert.Equal(13, geometry.PivotY, precision: 6);
    }

    [Fact]
    public void ResolveAnimatedGeometryKeepsPivotWhenInitialDimensionIsInvalid()
    {
        var imageCast = ImageCast(width: 0, height: 0, pivotX: 12, pivotY: 8);

        var geometry = SbSceneImageCastConventions.ResolveAnimatedGeometry(imageCast, width: 24, height: 16);

        Assert.Equal(12, geometry.PivotX, precision: 6);
        Assert.Equal(8, geometry.PivotY, precision: 6);
    }

    [Theory]
    [InlineData(0x00408000, false)]
    [InlineData(0x00408001, true)]
    [InlineData(0x00408003, false)]
    public void ReadsAdditiveBlendCandidateBit(int packedState, bool additive)
    {
        Assert.Equal(additive, SbSceneImageCastConventions.HasAdditiveBlendCandidate(packedState));
    }

    [Theory]
    [InlineData(0x00000000, 0)]
    [InlineData(0x00000001, 1)]
    [InlineData(0x00000003, 3)]
    [InlineData(0x0000000F, 15)]
    public void DecodesDrawModeFromLowNibble(int packedState, int drawMode)
    {
        Assert.Equal(drawMode, SbSceneImageCastConventions.DecodeDrawMode(packedState));
    }

    [Theory]
    [InlineData(0x00408000, false, false)]
    [InlineData(0x00408010, true, false)]
    [InlineData(0x00408020, false, true)]
    [InlineData(0x00408030, true, true)]
    public void ReadsImageCastFlipBits(int packedState, bool horizontal, bool vertical)
    {
        Assert.Equal(horizontal, SbSceneImageCastConventions.HasHorizontalFlip(packedState));
        Assert.Equal(vertical, SbSceneImageCastConventions.HasVerticalFlip(packedState));
    }

    [Theory]
    [InlineData(0x00000000, 0)]
    [InlineData(0x00000800, 1)]
    [InlineData(0x00001000, 2)]
    [InlineData(0x00001800, 3)]
    [InlineData(0x00002000, 4)]
    [InlineData(0x00002800, 0)]
    public void DecodesSurfaceModeBits(int packedState, int surfaceMode)
    {
        Assert.Equal(surfaceMode, SbSceneImageCastConventions.DecodeSurfaceMode(packedState));
    }

    [Theory]
    [InlineData(0x00000000, 0)]
    [InlineData(0x00000040, 1)]
    [InlineData(0x00000080, 2)]
    [InlineData(0x000000C0, 3)]
    public void DecodesUvModeBits(int packedState, int uvMode)
    {
        Assert.Equal(uvMode, SbSceneImageCastConventions.DecodeUvMode(packedState));
    }

    [Theory]
    [InlineData(0x00000000, 0.25, 0.75, 0.25, 0.75)]
    [InlineData(0x00000010, 0.25, 0.75, 0.75, 0.75)]
    [InlineData(0x00000020, 0.25, 0.75, 0.25, 0.25)]
    [InlineData(0x00000040, 0.25, 0.75, 0.25, 0.25)]
    [InlineData(0x00000080, 0.25, 0.75, 0.75, 0.25)]
    [InlineData(0x000000C0, 0.25, 0.75, 0.75, 0.75)]
    public void ResolvesTextureCoordinateWithFlipAndUvMode(
        int packedState,
        double u,
        double v,
        double expectedU,
        double expectedV)
    {
        var mapped = SbSceneImageCastConventions.ResolveTextureCoordinate(u, v, packedState);

        Assert.Equal(expectedU, mapped.U, precision: 6);
        Assert.Equal(expectedV, mapped.V, precision: 6);
    }

    private static SbSceneImageCast ImageCast(float width, float height, float pivotX, float pivotY)
    {
        return new SbSceneImageCast
        {
            Index = 0,
            Offset = 0,
            ImageCastFlags = 0,
            ImageCastFlagBits = [],
            CastIndex = 0,
            NodeName = "node",
            Width = width,
            Height = height,
            PivotX = pivotX,
            PivotY = pivotY,
            DeclaredCropReferenceCount = 0,
            PrimaryCropReferenceCount = 0,
            SecondaryCropReferenceCount = null,
            SecondaryCropFlag = null,
            PrimaryCropIndex = null,
            SecondaryCropIndex = null,
            PrimaryCropReferenceIndex = null,
            SecondaryCropReferenceIndex = null,
            CropReferenceCountMatches = true,
            CropIndexValues = [],
            CropRefCounts = [],
            PrimaryCropReferences = [],
            SecondaryCropReferences = [],
            CropReferences = [],
        };
    }
}
