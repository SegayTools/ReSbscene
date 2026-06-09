using SbScene.Core.Images;

namespace SbScene.Core.Tests;

public sealed class RgbaImageResizerTests
{
    [Fact]
    public void ResolveProportionalSizePreservesAspectRatio()
    {
        Assert.Equal((50, 25), RgbaImageResizer.ResolveProportionalSize(100, 50, targetWidth: 50, targetHeight: null));
        Assert.Equal((50, 25), RgbaImageResizer.ResolveProportionalSize(100, 50, targetWidth: null, targetHeight: 25));
        Assert.Equal((100, 50), RgbaImageResizer.ResolveProportionalSize(100, 50, targetWidth: null, targetHeight: null));
    }

    [Fact]
    public void ResizeBilinearInterpolatesPremultipliedAlpha()
    {
        var image = new RgbaImage(
            2,
            1,
            [
                255, 0, 0, 128,
                0, 0, 255, 0,
            ]);

        var resized = RgbaImageResizer.ResizeBilinear(image, 1, 1);

        Assert.Equal(1, resized.Width);
        Assert.Equal(1, resized.Height);
        Assert.Equal([255, 0, 0, 64], resized.Pixels);
    }
}
