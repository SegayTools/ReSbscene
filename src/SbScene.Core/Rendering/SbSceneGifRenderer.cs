using SbScene.Core.Images;
using SbScene.Core.Semantics;

namespace SbScene.Core.Rendering;

/// <summary>
/// 表示sbscene 场景GIFRender选项，集中描述调用方可配置的输入、开关和默认策略。
/// </summary>
public sealed class SbSceneGifRenderOptions
{
    /// <summary>
    /// 表示输出帧率，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
    /// </summary>
    public int Fps { get; init; } = SbSceneGifAnimationSampler.DefaultFps;

    /// <summary>
    /// 获取或设置动画采样帧范围，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
    /// </summary>
    public SbSceneGifFrameRange? FrameRange { get; init; }

    /// <summary>
    /// 表示消隐背景颜色，用于参与颜色、透明度、照明或混合计算。
    /// </summary>
    public RgbaColor MatteColor { get; init; } = new(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);

    /// <summary>
    /// 获取或设置压缩输出帧序列，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
    /// </summary>
    public bool CompressFrames { get; init; }

    /// <summary>
    /// 获取或设置目标宽度，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public int? TargetWidth { get; init; }

    /// <summary>
    /// 获取或设置目标高度，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public int? TargetHeight { get; init; }
}

/// <summary>
/// 表示sbscene 场景GIFRender结果，封装处理产物、统计信息和诊断状态。
/// </summary>
public sealed class SbSceneGifRenderResult
{
    /// <summary>
    /// 获取或设置输出帧序列，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
    /// </summary>
    public required IReadOnlyList<RgbaImage> Frames { get; init; }

    /// <summary>
    /// 获取或设置宽度，用于确定渲染区域、裁剪范围、采样质量或输出尺寸。
    /// </summary>
    public required int Width { get; init; }

    /// <summary>
    /// 获取或设置高度，用于确定渲染区域、裁剪范围、采样质量或输出尺寸。
    /// </summary>
    public required int Height { get; init; }

    /// <summary>
    /// 获取或设置输出帧率，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
    /// </summary>
    public required int Fps { get; init; }

    /// <summary>
    /// 获取或设置起始帧，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
    /// </summary>
    public required double StartFrame { get; init; }

    /// <summary>
    /// 获取或设置结束帧，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required double EndFrame { get; init; }

    /// <summary>
    /// 获取或设置实际绘制图层数量，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required int RenderedItemCount { get; init; }

    /// <summary>
    /// 获取或设置候选绘制图层数量，用于报告数量或统计值，便于调用方校验结构规模和处理结果。
    /// </summary>
    public required int CandidateItemCount { get; init; }

    /// <summary>
    /// 获取或设置非致命警告列表，用于把非致命问题返回给调用方，便于诊断解析、渲染或导出过程。
    /// </summary>
    public required IReadOnlyList<string> Warnings { get; init; }
}

/// <summary>
/// 提供sbscene 场景GIF渲染器，负责把场景和资源数据绘制为图像结果。
/// </summary>
public static class SbSceneGifRenderer
{
    /// <summary>
    /// 渲染Render，把场景和资源数据绘制为调用方可写出的图像结果。
    /// </summary>
    /// <param name="scene">已解析的 sbscene 场景模型。</param>
    /// <param name="svoPath">要读取、写入或记录的文件或目录路径。</param>
    /// <param name="renderOptions">控制本次处理行为的选项。</param>
    /// <param name="gifOptions">控制本次处理行为的选项。</param>
    /// <returns>包含输出图像、统计信息和诊断信息的渲染结果。</returns>
    /// <example>
    /// <code>
    /// var scene = new SbSceneParser().ParseFile("scene.sbscene");
    /// var result = SbSceneGifRenderer.Render(
    ///     scene,
    ///     "resource.svo",
    ///     new SbSceneRenderOptions(),
    ///     new SbSceneGifRenderOptions { Fps = 30 });
    /// GifWriter.Write("scene.gif", result.Frames, 3, new RgbaColor(255, 255, 255, 255));
    /// </code>
    /// </example>
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
