using SbScene.Core.Rendering;

namespace SbScene.Core.Tests;

public sealed class SbSceneTransformConventionsTests
{
    [Theory]
    [InlineData(30.0, -30.0)]
    [InlineData(-5.0, 5.0)]
    [InlineData(0.0, -0.0)]
    public void ConvertsSceneRotationToScreenRotation(double sceneDegrees, double screenDegrees)
    {
        Assert.Equal(screenDegrees, SbSceneTransformConventions.ToScreenRotationDegrees(sceneDegrees));
    }
}
