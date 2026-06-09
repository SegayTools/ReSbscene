using System.IO;
using SbScene.Core.Images;
using SbScene.Core.Rendering;
using SbScene.Core.Semantics;

namespace SbScene.Viewer;

internal static class ViewerGifExporter
{
    /// <summary>
    /// 导出导出清单，将 sbscene 语义模型转换为目标格式的结构化输出。
    /// </summary>
    /// <param name="scene">已解析的 sbscene 场景模型。</param>
    /// <param name="svoPath">要读取、写入或记录的文件或目录路径。</param>
    /// <param name="settings">参与本次处理的导出设置。</param>
    /// <param name="animations">参与本次处理的一组结构化条目。</param>
    /// <returns>包含导出清单、诊断信息和失败状态的导出结果。</returns>
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
        var gifOptions = new SbSceneGifRenderOptions
        {
            Fps = settings.Fps,
            FrameRange = settings.FrameRange,
            MatteColor = settings.MatteColor,
            CompressFrames = settings.CompressFrames,
            TargetWidth = settings.TargetWidth,
            TargetHeight = settings.TargetHeight,
        };
        var result = SbSceneGifRenderer.Render(scene, svoPath, renderOptions, gifOptions);

        var delayCentiseconds = Math.Max(1, (int)Math.Round(100.0 / result.Fps));
        var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(settings.OutputPath));
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        GifWriter.Write(settings.OutputPath, result.Frames, delayCentiseconds, gifOptions.MatteColor, compressFrames: gifOptions.CompressFrames);
        return new ViewerGifExportResult(
            result.Frames.Count,
            result.Width,
            result.Height,
            result.Fps,
            result.StartFrame,
            result.EndFrame,
            result.RenderedItemCount,
            result.CandidateItemCount,
            result.Warnings);
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
