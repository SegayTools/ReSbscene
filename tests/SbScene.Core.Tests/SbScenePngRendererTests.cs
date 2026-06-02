using System.Reflection;
using SbScene.Core.Rendering;

namespace SbScene.Core.Tests;

public sealed class SbScenePngRendererTests
{
    [Fact]
    public void AdditiveBlendAccumulatesColorAndKeepsMaximumAlpha()
    {
        var pixels = new byte[] { 10, 20, 30, 64 };

        InvokeBlendPixel(pixels, sourceR: 100, sourceG: 80, sourceB: 60, sourceAlphaByte: 128, additive: true);

        Assert.Equal([60, 60, 60, 128], pixels);
    }

    [Fact]
    public void AdditiveBlendClampsAccumulatedColor()
    {
        var pixels = new byte[] { 200, 210, 220, 255 };

        InvokeBlendPixel(pixels, sourceR: 255, sourceG: 255, sourceB: 255, sourceAlphaByte: 255, additive: true);

        Assert.Equal([255, 255, 255, 255], pixels);
    }

    private static void InvokeBlendPixel(
        byte[] pixels,
        double sourceR,
        double sourceG,
        double sourceB,
        double sourceAlphaByte,
        bool additive)
    {
        var method = typeof(SbScenePngRenderer).GetMethod(
            "BlendPixel",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        method.Invoke(null, [pixels, 0, sourceR, sourceG, sourceB, sourceAlphaByte, additive]);
    }
}
