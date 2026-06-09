using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using SbScene.Core.Rendering;

namespace SbScene.Viewer;

/// <summary>
/// 表示 GIF 导出设置对话框，负责收集帧率、范围、尺寸和背景色等导出选项。
/// </summary>
public partial class GifExportDialog : Window
{
    internal GifExportDialog(ViewerSettings settings, string? scenePath)
    {
        InitializeComponent();
        OutputPathTextBox.Text = settings.LastGifExportPath ?? BuildDefaultOutputPath(scenePath) ?? string.Empty;
        FpsTextBox.Text = settings.LastGifFps.ToString(CultureInfo.InvariantCulture);
        UseFrameRangeCheckBox.IsChecked = settings.LastGifUseFrameRange;
        StartFrameTextBox.Text = settings.LastGifStartFrame.ToString("0.###", CultureInfo.InvariantCulture);
        EndFrameTextBox.Text = settings.LastGifEndFrame.ToString("0.###", CultureInfo.InvariantCulture);
        CompressFramesCheckBox.IsChecked = settings.LastGifCompressFrames;
        UseTargetWidthCheckBox.IsChecked = settings.LastGifUseTargetWidth;
        TargetWidthTextBox.Text = settings.LastGifTargetWidth > 0
            ? settings.LastGifTargetWidth.ToString(CultureInfo.InvariantCulture)
            : string.Empty;
        UseTargetHeightCheckBox.IsChecked = settings.LastGifUseTargetHeight;
        TargetHeightTextBox.Text = settings.LastGifTargetHeight > 0
            ? settings.LastGifTargetHeight.ToString(CultureInfo.InvariantCulture)
            : string.Empty;
        PaddingTextBox.Text = settings.LastGifPadding.ToString(CultureInfo.InvariantCulture);
        ScaleTextBox.Text = settings.LastGifScale.ToString("0.###", CultureInfo.InvariantCulture);
        HighQualityCheckBox.IsChecked = settings.LastGifHighQuality;
        ShowHiddenCheckBox.IsChecked = settings.LastGifShowHidden;
        CharacterDefaultsCheckBox.IsChecked = settings.LastGifCharacterDefaults;
        MatteColorTextBox.Text = settings.LastGifMatteColor;
        UpdateFrameRangeState();
        UpdateTargetSizeState();
    }

    /// <summary>
    /// 获取或设置结果，用于返回导出或处理产物及其统计、校验和诊断信息。
    /// </summary>
    public GifExportDialogResult? Result { get; private set; }

    private static string? BuildDefaultOutputPath(string? scenePath)
    {
        if (string.IsNullOrWhiteSpace(scenePath))
        {
            return null;
        }

        var directory = Path.GetDirectoryName(scenePath);
        var name = Path.GetFileNameWithoutExtension(scenePath);
        return string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(name)
            ? null
            : Path.Combine(directory, $"{name}.gif");
    }

    private void BrowseOutput_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "保存 GIF",
            Filter = "GIF files (*.gif)|*.gif|All files (*.*)|*.*",
            FileName = string.IsNullOrWhiteSpace(OutputPathTextBox.Text)
                ? "animation.gif"
                : Path.GetFileName(OutputPathTextBox.Text),
            DefaultExt = ".gif",
            AddExtension = true,
            OverwritePrompt = true,
        };

        if (TryGetExistingDirectory(OutputPathTextBox.Text, out var initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        if (dialog.ShowDialog(this) == true)
        {
            OutputPathTextBox.Text = dialog.FileName;
        }
    }

    private void IntegerTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = e.Text.Any(static ch => !char.IsDigit(ch));
    }

    private void UseFrameRange_Changed(object sender, RoutedEventArgs e)
    {
        UpdateFrameRangeState();
    }

    private void TargetSize_Changed(object sender, RoutedEventArgs e)
    {
        if (sender == UseTargetWidthCheckBox && UseTargetWidthCheckBox.IsChecked == true)
        {
            UseTargetHeightCheckBox.IsChecked = false;
        }
        else if (sender == UseTargetHeightCheckBox && UseTargetHeightCheckBox.IsChecked == true)
        {
            UseTargetWidthCheckBox.IsChecked = false;
        }

        UpdateTargetSizeState();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        ErrorTextBlock.Text = string.Empty;

        var outputPath = OutputPathTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            ShowError("请选择输出 GIF 路径。");
            return;
        }

        if (!int.TryParse(FpsTextBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var fps)
            || fps is < SbSceneGifAnimationSampler.MinFps or > SbSceneGifAnimationSampler.MaxFps)
        {
            ShowError($"FPS 必须是 {SbSceneGifAnimationSampler.MinFps}..{SbSceneGifAnimationSampler.MaxFps}。");
            return;
        }

        if (!int.TryParse(PaddingTextBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var padding)
            || padding < 0)
        {
            ShowError("Padding 必须是非负整数。");
            return;
        }

        if (!double.TryParse(ScaleTextBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var scale)
            || !double.IsFinite(scale)
            || scale <= 0)
        {
            ShowError("Scale 必须是正数。");
            return;
        }

        var useFrameRange = UseFrameRangeCheckBox.IsChecked == true;
        var hasStartFrame = double.TryParse(StartFrameTextBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var startFrame);
        var hasEndFrame = double.TryParse(EndFrameTextBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var endFrame);
        if (useFrameRange)
        {
            if (!hasStartFrame
                || !hasEndFrame
                || !double.IsFinite(startFrame)
                || !double.IsFinite(endFrame)
                || startFrame < 0
                || endFrame < startFrame)
            {
                ShowError("源帧范围必须是有限数字，并且 End frame >= Start frame。");
                return;
            }
        }
        else
        {
            if (!hasStartFrame || !double.IsFinite(startFrame) || startFrame < 0)
            {
                startFrame = 0;
            }

            if (!hasEndFrame || !double.IsFinite(endFrame) || endFrame < startFrame)
            {
                endFrame = Math.Max(startFrame, 60);
            }
        }

        var frameRange = useFrameRange ? new SbSceneGifFrameRange(startFrame, endFrame) : (SbSceneGifFrameRange?)null;

        var useTargetWidth = UseTargetWidthCheckBox.IsChecked == true;
        var targetWidthValue = 0;
        int? targetWidth = null;
        var targetWidthText = TargetWidthTextBox.Text.Trim();
        if (targetWidthText.Length > 0)
        {
            _ = int.TryParse(targetWidthText, NumberStyles.Integer, CultureInfo.InvariantCulture, out targetWidthValue);
        }

        if (useTargetWidth)
        {
            if (targetWidthValue <= 0)
            {
                ShowError("指定宽度时，宽度必须是正整数。");
                return;
            }

            targetWidth = targetWidthValue;
        }

        var useTargetHeight = UseTargetHeightCheckBox.IsChecked == true;
        var targetHeightValue = 0;
        int? targetHeight = null;
        var targetHeightText = TargetHeightTextBox.Text.Trim();
        if (targetHeightText.Length > 0)
        {
            _ = int.TryParse(targetHeightText, NumberStyles.Integer, CultureInfo.InvariantCulture, out targetHeightValue);
        }

        if (useTargetHeight)
        {
            if (targetHeightValue <= 0)
            {
                ShowError("指定高度时，高度必须是正整数。");
                return;
            }

            targetHeight = targetHeightValue;
        }

        if (targetWidth is not null && targetHeight is not null)
        {
            ShowError("宽度和高度只能指定一个，GIF 会按比例缩放。");
            return;
        }

        var matteColorText = MatteColorTextBox.Text.Trim();
        if (!TryParseOpaqueColor(matteColorText, out var matteColor))
        {
            ShowError("Matte 颜色必须是 #RRGGBB 或 #AARRGGBB。");
            return;
        }

        Result = new GifExportDialogResult(
            outputPath,
            fps,
            useFrameRange,
            startFrame,
            endFrame,
            frameRange,
            CompressFramesCheckBox.IsChecked == true,
            useTargetWidth,
            targetWidthValue,
            targetWidth,
            useTargetHeight,
            targetHeightValue,
            targetHeight,
            padding,
            scale,
            HighQualityCheckBox.IsChecked == true,
            ShowHiddenCheckBox.IsChecked == true,
            CharacterDefaultsCheckBox.IsChecked == true,
            matteColor,
            matteColorText);
        DialogResult = true;
    }

    private void UpdateFrameRangeState()
    {
        var enabled = UseFrameRangeCheckBox?.IsChecked == true;
        if (StartFrameTextBox is not null)
        {
            StartFrameTextBox.IsEnabled = enabled;
        }

        if (EndFrameTextBox is not null)
        {
            EndFrameTextBox.IsEnabled = enabled;
        }
    }

    private void UpdateTargetSizeState()
    {
        if (TargetWidthTextBox is not null)
        {
            TargetWidthTextBox.IsEnabled = UseTargetWidthCheckBox?.IsChecked == true;
        }

        if (TargetHeightTextBox is not null)
        {
            TargetHeightTextBox.IsEnabled = UseTargetHeightCheckBox?.IsChecked == true;
        }
    }

    private void ShowError(string message)
    {
        ErrorTextBlock.Text = message;
    }

    private static bool TryParseOpaqueColor(string text, out RgbaColor color)
    {
        color = new RgbaColor(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
        if (!text.StartsWith('#') || text.Length is not (7 or 9))
        {
            return false;
        }

        if (!uint.TryParse(text[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        if (text.Length == 7)
        {
            color = new RgbaColor(
                (byte)((value >> 16) & 0xFF),
                (byte)((value >> 8) & 0xFF),
                (byte)(value & 0xFF),
                byte.MaxValue);
            return true;
        }

        color = new RgbaColor(
            (byte)((value >> 16) & 0xFF),
            (byte)((value >> 8) & 0xFF),
            (byte)(value & 0xFF),
            byte.MaxValue);
        return true;
    }

    private static bool TryGetExistingDirectory(string path, out string directory)
    {
        directory = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (Directory.Exists(path))
        {
            directory = path;
            return true;
        }

        var parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(parent) && Directory.Exists(parent))
        {
            directory = parent;
            return true;
        }

        return false;
    }
}

/// <summary>
/// 表示GIF 导出清单Dialog结果，封装处理产物、统计信息和诊断状态。
/// </summary>
/// <param name="OutputPath">要读取、写入或记录的文件或目录路径。</param>
/// <param name="Fps">参与本次处理的输出帧率。</param>
/// <param name="UseFrameRange">要采样或渲染的动画帧位置。</param>
/// <param name="StartFrame">要采样或渲染的动画帧位置。</param>
/// <param name="EndFrame">要采样或渲染的动画帧位置。</param>
/// <param name="FrameRange">要采样或渲染的动画帧位置。</param>
/// <param name="CompressFrames">要采样或渲染的动画帧位置。</param>
/// <param name="UseTargetWidth">目标宽度或参与尺寸计算的宽度。</param>
/// <param name="TargetWidthValue">目标宽度或参与尺寸计算的宽度。</param>
/// <param name="TargetWidth">目标宽度或参与尺寸计算的宽度。</param>
/// <param name="UseTargetHeight">目标高度或参与尺寸计算的高度。</param>
/// <param name="TargetHeightValue">目标高度或参与尺寸计算的高度。</param>
/// <param name="TargetHeight">目标高度或参与尺寸计算的高度。</param>
/// <param name="Padding">参与颜色、透明度或混合计算的通道值。</param>
/// <param name="Scale">参与本次处理的输出缩放比例。</param>
/// <param name="HighQuality">参与几何边界、坐标或变换计算的位置值。</param>
/// <param name="ShowHidden">用于关联节点、资源或导出条目的索引或标识。</param>
/// <param name="CharacterDefaults">参与本次处理的角色信息Defaults。</param>
/// <param name="MatteColor">参与颜色、透明度或混合计算的通道值。</param>
/// <param name="MatteColorText">参与几何边界、坐标或变换计算的位置值。</param>
public sealed record GifExportDialogResult(
    string OutputPath,
    int Fps,
    bool UseFrameRange,
    double StartFrame,
    double EndFrame,
    SbSceneGifFrameRange? FrameRange,
    bool CompressFrames,
    bool UseTargetWidth,
    int TargetWidthValue,
    int? TargetWidth,
    bool UseTargetHeight,
    int TargetHeightValue,
    int? TargetHeight,
    int Padding,
    double Scale,
    bool HighQuality,
    bool ShowHidden,
    bool CharacterDefaults,
    RgbaColor MatteColor,
    string MatteColorText);
