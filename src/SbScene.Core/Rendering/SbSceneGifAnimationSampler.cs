using SbScene.Core.Semantics;

namespace SbScene.Core.Rendering;

public readonly record struct SbSceneGifFrameRange(double StartFrame, double EndFrame);

public static class SbSceneGifAnimationSampler
{
    public const double SourceFramesPerSecond = 60.0;
    public const int DefaultFps = 30;
    public const int MinFps = 1;
    public const int MaxFps = 60;

    public static double GetSourceFrame(int gifFrameIndex, int fps, double startFrame = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(gifFrameIndex);
        ValidateFps(fps);
        if (!double.IsFinite(startFrame))
        {
            throw new ArgumentOutOfRangeException(nameof(startFrame), "Start frame must be finite.");
        }

        return startFrame + gifFrameIndex * SourceFramesPerSecond / fps;
    }

    public static int GetOutputFrameCount(double startFrame, double endFrame, int fps)
    {
        ValidateFps(fps);
        if (!double.IsFinite(startFrame) || !double.IsFinite(endFrame) || endFrame < startFrame)
        {
            throw new ArgumentOutOfRangeException(nameof(endFrame), "Frame range must be finite and ordered.");
        }

        var step = SourceFramesPerSecond / fps;
        return Math.Max(1, (int)Math.Ceiling((endFrame - startFrame) / step));
    }

    public static int ResolveEndFrame(
        SbSceneFile scene,
        IReadOnlyList<SbSceneAnimationSelection> selections,
        Action<string>? addWarning = null)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(selections);

        var endFrame = 0;
        foreach (var selection in selections)
        {
            if (!SbSceneAnimationFrameBuilder.TryResolveAnimationSelection(scene, selection, out _, out var animation, out var warning))
            {
                addWarning?.Invoke(warning);
                continue;
            }

            endFrame = Math.Max(endFrame, SbSceneAnimationTimeline.GetEndFrame(animation));
        }

        return endFrame;
    }

    public static IReadOnlyList<SbSceneAnimationSelection> BuildFrameSelections(
        IReadOnlyList<SbSceneAnimationSelection> selections,
        int gifFrameIndex,
        int fps,
        double startFrame = 0)
    {
        ArgumentNullException.ThrowIfNull(selections);

        var sourceFrame = GetSourceFrame(gifFrameIndex, fps, startFrame);
        return selections
            .Select(selection => selection.HasExplicitFrame ? selection : selection with { Frame = sourceFrame })
            .ToArray();
    }

    public static SbSceneRenderBounds UnionBounds(IEnumerable<SbSceneRenderBounds> bounds)
    {
        ArgumentNullException.ThrowIfNull(bounds);

        var any = false;
        var left = double.PositiveInfinity;
        var top = double.PositiveInfinity;
        var right = double.NegativeInfinity;
        var bottom = double.NegativeInfinity;
        foreach (var bound in bounds)
        {
            any = true;
            left = Math.Min(left, bound.Left);
            top = Math.Min(top, bound.Top);
            right = Math.Max(right, bound.Right);
            bottom = Math.Max(bottom, bound.Bottom);
        }

        return any
            ? new SbSceneRenderBounds(left, top, right, bottom)
            : new SbSceneRenderBounds(-160, -120, 160, 120);
    }

    private static void ValidateFps(int fps)
    {
        if (fps is < MinFps or > MaxFps)
        {
            throw new ArgumentOutOfRangeException(nameof(fps), $"FPS must be between {MinFps} and {MaxFps}.");
        }
    }
}
