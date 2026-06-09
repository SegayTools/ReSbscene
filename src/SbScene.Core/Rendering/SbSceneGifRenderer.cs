using SbScene.Core.Images;
using SbScene.Core.Semantics;

namespace SbScene.Core.Rendering;

public sealed class SbSceneGifRenderOptions
{
    public int Fps { get; init; } = SbSceneGifAnimationSampler.DefaultFps;

    public SbSceneGifFrameRange? FrameRange { get; init; }

    public RgbaColor MatteColor { get; init; } = new(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);

    public bool CompressFrames { get; init; }

    public int? TargetWidth { get; init; }

    public int? TargetHeight { get; init; }
}

public sealed class SbSceneGifRenderResult
{
    public required IReadOnlyList<RgbaImage> Frames { get; init; }

    public required int Width { get; init; }

    public required int Height { get; init; }

    public required int Fps { get; init; }

    public required double StartFrame { get; init; }

    public required double EndFrame { get; init; }

    public required int RenderedItemCount { get; init; }

    public required int CandidateItemCount { get; init; }

    public required IReadOnlyList<string> Warnings { get; init; }
}

public static class SbSceneGifRenderer
{
    public static SbSceneGifRenderResult Render(
        SbSceneFile scene,
        string svoPath,
        SbSceneRenderOptions renderOptions,
        SbSceneGifRenderOptions gifOptions)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentException.ThrowIfNullOrWhiteSpace(svoPath);
        ArgumentNullException.ThrowIfNull(renderOptions);
        ArgumentNullException.ThrowIfNull(gifOptions);

        if (gifOptions.Fps is < SbSceneGifAnimationSampler.MinFps or > SbSceneGifAnimationSampler.MaxFps)
        {
            throw new ArgumentOutOfRangeException(nameof(gifOptions), $"FPS must be between {SbSceneGifAnimationSampler.MinFps} and {SbSceneGifAnimationSampler.MaxFps}.");
        }

        if (gifOptions.TargetWidth is not null && gifOptions.TargetHeight is not null)
        {
            throw new ArgumentException("Only one GIF target dimension can be specified.", nameof(gifOptions));
        }

        var warningState = new WarningCollector();
        var startFrame = gifOptions.FrameRange?.StartFrame ?? 0;
        var endFrame = gifOptions.FrameRange?.EndFrame ?? SbSceneGifAnimationSampler.ResolveEndFrame(scene, renderOptions.Animations, warningState.Add);
        var frameCount = SbSceneGifAnimationSampler.GetOutputFrameCount(startFrame, endFrame, gifOptions.Fps);
        var frameStates = new List<SbSceneAnimationFrameState>(frameCount);
        var bounds = new List<SbSceneRenderBounds>(frameCount);
        for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
        {
            var frameSelections = SbSceneGifAnimationSampler.BuildFrameSelections(renderOptions.Animations, frameIndex, gifOptions.Fps, startFrame);
            var frameState = SbSceneAnimationFrameBuilder.Build(scene, frameSelections, warningState.Add);
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
                warningState.Add(warning);
            }

            images.Add(render.Image);
        }

        var frames = gifOptions.TargetWidth is not null || gifOptions.TargetHeight is not null
            ? RgbaImageResizer.ResizeProportional(images, gifOptions.TargetWidth, gifOptions.TargetHeight)
            : images;

        return new SbSceneGifRenderResult
        {
            Frames = frames,
            Width = frames[0].Width,
            Height = frames[0].Height,
            Fps = gifOptions.Fps,
            StartFrame = startFrame,
            EndFrame = endFrame,
            RenderedItemCount = renderedItems,
            CandidateItemCount = candidateItems,
            Warnings = warningState.Warnings,
        };
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

}
