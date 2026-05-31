using SbScene.Core.Rendering;

namespace SbScene.Core.Tests;

public sealed class SbSceneColorConventionsTests
{
    [Fact]
    public void ApplyLightingAddsIlluminationAfterMaterialTint()
    {
        var lit = SbSceneColorConventions.ApplyLighting(
            textureR: 255,
            textureG: 255,
            textureB: 255,
            textureA: 128,
            material: new RgbaColor(0, 0, 0, 255),
            illumination: new RgbaColor(223, 0, 255, 255),
            vertex: SbSceneColorConventions.OpaqueWhite);

        Assert.Equal(223, lit.R);
        Assert.Equal(0, lit.G);
        Assert.Equal(255, lit.B);
        Assert.Equal(128, lit.A);
    }

    [Fact]
    public void ApplyLightingMultipliesVertexColorAndAlpha()
    {
        var lit = SbSceneColorConventions.ApplyLighting(
            textureR: 200,
            textureG: 200,
            textureB: 200,
            textureA: 100,
            material: new RgbaColor(255, 255, 255, 255),
            illumination: new RgbaColor(0, 0, 0, 255),
            vertex: new RgbaColor(128, 64, 255, 128));

        Assert.Equal(200 * (128 / 255.0), lit.R, precision: 6);
        Assert.Equal(200 * (64 / 255.0), lit.G, precision: 6);
        Assert.Equal(200, lit.B, precision: 6);
        Assert.Equal(100 * (128 / 255.0), lit.A, precision: 6);
    }

    [Fact]
    public void InterpolateVertexColorUsesQuadCornerOrder()
    {
        var colors = new[]
        {
            new RgbaColor(255, 0, 0, 255),
            new RgbaColor(0, 255, 0, 255),
            new RgbaColor(0, 0, 255, 255),
            new RgbaColor(255, 255, 255, 255),
        };

        Assert.Equal(colors[0], SbSceneColorConventions.InterpolateVertexColor(colors, 0, 0));
        Assert.Equal(colors[1], SbSceneColorConventions.InterpolateVertexColor(colors, 1, 0));
        Assert.Equal(colors[2], SbSceneColorConventions.InterpolateVertexColor(colors, 1, 1));
        Assert.Equal(colors[3], SbSceneColorConventions.InterpolateVertexColor(colors, 0, 1));
    }
}
