using SbScene.Core.Rendering;

namespace SbScene.Core.Tests;

public sealed class SbSceneImageCastConventionsTests
{
    [Theory]
    [InlineData(0x00408000, false)]
    [InlineData(0x00408001, true)]
    public void ReadsAdditiveBlendCandidateBit(int packedState, bool additive)
    {
        Assert.Equal(additive, SbSceneImageCastConventions.HasAdditiveBlendCandidate(packedState));
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
}
