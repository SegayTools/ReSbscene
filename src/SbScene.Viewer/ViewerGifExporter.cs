using System.IO;
using SbScene.Core.Images;
using SbScene.Core.Rendering;
using SbScene.Core.Semantics;

namespace SbScene.Viewer;

internal static class ViewerGifExporter
{
    public static ViewerGifExportResult Export(
        SbSceneFile scene,
        string svoPath,
        GifExportDialogResult settings,
        IReadOnlyList<SbSceneAnimationSelection> animations)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentException.ThrowIfNullOrWhiteSpace(svoPath);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(animations);

        var warnings = new List<string>();
        var warningSet = new HashSet<string>(StringComparer.Ordinal);
        void AddWarning(string warning)
        {
            if (warningSet.Add(warning))
            {
                warnings.Add(warning);
            }
        }

        var textureSampling = settings.HighQuality
            ? SbSceneTextureSampling.Bilinear
            : SbSceneTextureSampling.Nearest;
        var renderOptions = new SbSceneRenderOptions
        {
            Padding = settings.Padding,
            Scale = settings.Scale,
            Supersample = settings.HighQuality ? 4 : 1,
            TextureSampling = textureSampling,
            BackgroundColor = RgbaColor.Transparent,
            ShowHiddenNodes = settings.ShowHidden,
            RenderSecondaryImages = false,
            Animations = animations,
        };

        var startFrame = settings.FrameRange?.StartFrame ?? 0;
        var endFrame = settings.FrameRange?.EndFrame ?? SbSceneGifAnimationSampler.ResolveEndFrame(scene, animations, AddWarning);
        var frameCount = SbSceneGifAnimationSampler.GetOutputFrameCount(startFrame, endFrame, settings.Fps);
        var frameStates = new List<SbSceneAnimationFrameState>(frameCount);
        var bounds = new List<SbSceneRenderBounds>(frameCount);
        for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
        {
            var frameSelections = SbSceneGifAnimationSampler.BuildFrameSelections(animations, frameIndex, settings.Fps, startFrame);
            var frameState = SbSceneAnimationFrameBuilder.Build(scene, frameSelections, AddWarning);
            frameStates.Add(frameState);
            bounds.Add(SbScenePngRenderer.ComputeContentBounds(scene, frameState, renderOptions));
        }

        var unionBounds = SbSceneGifAnimationSampler.UnionBounds(bounds);
        var frameOptions = CloneRenderOptions(renderOptions, unionBounds);
        var images = new List<RgbaImage>(frameCount);
        var renderedItems = 0;
        var candidateItems = 0;
        foreach (var frameState in frameStates)
        {
            var render = SbScenePngRenderer.Render(scene, svoPath, frameState, frameOptions);
            renderedItems = Math.Max(renderedItems, render.RenderedItemCount);
            candidateItems = Math.Max(candidateItems, render.CandidateItemCount);
            foreach (var warning in render.Warnings)
            {
                AddWarning(warning);
            }

            images.Add(render.Image);
        }

        if (settings.TargetWidth is not null || settings.TargetHeight is not null)
        {
            images = ResizeGifFrames(images, settings.TargetWidth, settings.TargetHeight);
        }

        var delayCentiseconds = Math.Max(1, (int)Math.Round(100.0 / settings.Fps));
        var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(settings.OutputPath));
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        GifWriter.Write(settings.OutputPath, images, delayCentiseconds, settings.MatteColor, compressFrames: settings.CompressFrames);
        return new ViewerGifExportResult(
            images.Count,
            images[0].Width,
            images[0].Height,
            settings.Fps,
            startFrame,
            endFrame,
            renderedItems,
            candidateItems,
            warnings);
    }

    public static IReadOnlyList<SbSceneAnimationSelection> BuildCharacterDefaultSelections()
    {
        return
        [
            new SbSceneAnimationSelection("Change_Fashion", 0) { HasExplicitFrame = true },
            new SbSceneAnimationSelection("Change_Position", 0) { HasExplicitFrame = true },
            new SbSceneAnimationSelection("Change_Accessory", 0) { HasExplicitFrame = true },
            new SbSceneAnimationSelection("Action_Wait1", 0) { HasExplicitFrame = true },
            new SbSceneAnimationSelection("Mouth_Wait1", 0) { HasExplicitFrame = true },
        ];
    }

    private static SbSceneRenderOptions CloneRenderOptions(SbSceneRenderOptions options, SbSceneRenderBounds contentBounds)
    {
        return new SbSceneRenderOptions
        {
            Padding = options.Padding,
            Scale = options.Scale,
            Supersample = options.Supersample,
            TextureSampling = options.TextureSampling,
            BackgroundColor = options.BackgroundColor,
            ShowHiddenNodes = options.ShowHiddenNodes,
            RenderSecondaryImages = options.RenderSecondaryImages,
            Animations = options.Animations,
            ContentBounds = contentBounds,
        };
    }

    private static List<RgbaImage> ResizeGifFrames(IReadOnlyList<RgbaImage> images, int? targetWidth, int? targetHeight)
    {
        if (images.Count == 0)
        {
            return [];
        }

        var (width, height) = ResolveGifOutputSize(images[0].Width, images[0].Height, targetWidth, targetHeight);
        return images.Select(image => ResizeImageBilinear(image, width, height)).ToList();
    }

    private static (int Width, int Height) ResolveGifOutputSize(int sourceWidth, int sourceHeight, int? targetWidth, int? targetHeight)
    {
        if (targetWidth is int requestedWidth)
        {
            var resolvedHeight = Math.Max(1, (int)Math.Round(sourceHeight * (requestedWidth / (double)sourceWidth)));
            return (requestedWidth, resolvedHeight);
        }

        if (targetHeight is int requestedHeight)
        {
            var resolvedWidth = Math.Max(1, (int)Math.Round(sourceWidth * (requestedHeight / (double)sourceHeight)));
            return (resolvedWidth, requestedHeight);
        }

        return (sourceWidth, sourceHeight);
    }

    private static RgbaImage ResizeImageBilinear(RgbaImage input, int width, int height)
    {
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

internal sealed record ViewerGifExportResult(
    int FrameCount,
    int Width,
    int Height,
    int Fps,
    double StartFrame,
    double EndFrame,
    int RenderedItemCount,
    int CandidateItemCount,
    IReadOnlyList<string> Warnings);
