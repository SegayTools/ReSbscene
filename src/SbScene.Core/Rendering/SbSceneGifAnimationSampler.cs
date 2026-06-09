using SbScene.Core.Semantics;

namespace SbScene.Core.Rendering;

/// <summary>
/// 表示 GIF 导出的起止帧范围，用于把动画时间轴裁剪到采样区间。
/// </summary>
/// <param name="StartFrame">要采样或渲染的动画帧位置。</param>
/// <param name="EndFrame">要采样或渲染的动画帧位置。</param>
public readonly record struct SbSceneGifFrameRange(double StartFrame, double EndFrame);

/// <summary>
/// 提供 GIF 导出采样工具，用于在源动画帧和输出 GIF 帧之间换算。
/// </summary>
public static class SbSceneGifAnimationSampler
{
    /// <summary>
    /// 表示来源信息输出帧序列PerSecond，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public const double SourceFramesPerSecond = 60.0;
    /// <summary>
    /// 表示默认输出帧率，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
    /// </summary>
    public const int DefaultFps = 30;
    /// <summary>
    /// 表示最小值输出帧率，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
    /// </summary>
    public const int MinFps = 1;
    /// <summary>
    /// 表示最大值输出帧率，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
    /// </summary>
    public const int MaxFps = 60;

    /// <summary>
    /// 获取来源信息帧，用于展示、比较、索引查找或后续计算。
    /// </summary>
    /// <param name="gifFrameIndex">要采样或渲染的动画帧位置。</param>
    /// <param name="fps">参与本次处理的输出帧率。</param>
    /// <param name="startFrame">要采样或渲染的动画帧位置。</param>
    /// <returns>GIF 第 N 帧对应的源动画帧号。</returns>
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

    /// <summary>
    /// 获取输出帧数量，用于展示、比较、索引查找或后续计算。
    /// </summary>
    /// <param name="startFrame">要采样或渲染的动画帧位置。</param>
    /// <param name="endFrame">要采样或渲染的动画帧位置。</param>
    /// <param name="fps">参与本次处理的输出帧率。</param>
    /// <returns>指定源帧范围和输出帧率下需要导出的 GIF 帧数。</returns>
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

    /// <summary>
    /// 解析结束帧，将调用方输入归一化为后续处理可直接使用的值。
    /// </summary>
    /// <param name="scene">已解析的 sbscene 场景模型。</param>
    /// <param name="selections">参与本次处理的一组结构化条目。</param>
    /// <param name="addWarning">接收诊断日志或非致命警告的回调。</param>
    /// <returns>所有选中动画覆盖到的最大结束帧。</returns>
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

    /// <summary>
    /// 构建帧Selections，为渲染、导出或诊断流程准备中间状态。
    /// </summary>
    /// <param name="selections">参与本次处理的一组结构化条目。</param>
    /// <param name="gifFrameIndex">要采样或渲染的动画帧位置。</param>
    /// <param name="fps">参与本次处理的输出帧率。</param>
    /// <param name="startFrame">要采样或渲染的动画帧位置。</param>
    /// <returns>带有当前源帧号的动画选择集合。</returns>
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

    /// <summary>
    /// 合并两个渲染边界，用于计算 GIF 导出覆盖范围。
    /// </summary>
    /// <param name="bounds">参与本次处理的边界。</param>
    /// <returns>覆盖所有输入边界的合并矩形；输入为空时返回默认视口范围。</returns>
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
