using System.IO;
using System.Text;
using System.Text.Json;
using NavigationCharacterPatcher;

namespace SbScene.Viewer;

internal sealed class ViewerSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// 获取或设置上次使用的Unity NaviChara配置Template路径，用于恢复 Viewer 上次使用的导出设置，减少重复输入。
    /// </summary>
    public string? LastUnityNavicharaProfileTemplatePath { get; set; }

    /// <summary>
    /// 获取或设置上次使用的Unity NaviChara配置路径，用于恢复 Viewer 上次使用的导出设置，减少重复输入。
    /// </summary>
    public string? LastUnityNavicharaProfilePath { get; set; }

    /// <summary>
    /// 获取或设置上次使用的Unity NaviChara输出目录，用于恢复 Viewer 上次使用的导出设置，减少重复输入。
    /// </summary>
    public string? LastUnityNavicharaOutputDirectory { get; set; }

    /// <summary>
    /// 获取或设置上次使用的Unity NaviChara角色信息标识，用于恢复 Viewer 上次使用的导出设置，减少重复输入。
    /// </summary>
    public int LastUnityNavicharaCharacterId { get; set; }

    /// <summary>
    /// 获取或设置上次使用的Unity NaviChara是否提取精灵集合，用于恢复 Viewer 上次使用的导出设置，减少重复输入。
    /// </summary>
    public bool LastUnityNavicharaExtractSprites { get; set; }

    /// <summary>
    /// 获取或设置上次使用的Unity NaviChara是否写出校验信息输出帧序列，用于恢复 Viewer 上次使用的导出设置，减少重复输入。
    /// </summary>
    public bool LastUnityNavicharaWriteValidationFrames { get; set; }

    /// <summary>
    /// 获取或设置上次使用的Unity NaviChara严格校验开关，用于恢复 Viewer 上次使用的导出设置，减少重复输入。
    /// </summary>
    public bool LastUnityNavicharaStrict { get; set; }

    /// <summary>
    /// 表示上次使用的Unity NaviChara自动居中，用于恢复 Viewer 上次使用的导出设置，减少重复输入。
    /// </summary>
    public bool LastUnityNavicharaAutoCenter { get; set; } = true;

    /// <summary>
    /// 获取或设置上次使用的Unity NaviChara烘焙采样后的曲线集合，用于恢复 Viewer 上次使用的导出设置，减少重复输入。
    /// </summary>
    public bool LastUnityNavicharaBakeSampledCurves { get; set; }

    /// <summary>
    /// 获取或设置上次使用的Unity NaviChara是否允许Placeholder动画剪辑集合，用于恢复 Viewer 上次使用的导出设置，减少重复输入。
    /// </summary>
    public bool LastUnityNavicharaAllowPlaceholderClips { get; set; }

    /// <summary>
    /// 获取或设置上次修补 NavigationCharacter prefab AB 的输入路径，用于恢复 Viewer 上次使用的修补设置。
    /// </summary>
    public string? LastNavigationCharacterPatchInputPath { get; set; }

    /// <summary>
    /// 获取或设置上次修补 NavigationCharacter prefab AB 的输出路径，用于恢复 Viewer 上次使用的修补设置。
    /// </summary>
    public string? LastNavigationCharacterPatchOutputPath { get; set; }

    /// <summary>
    /// 获取或设置上次修补 NavigationCharacter prefab AB 的目标脚本 PathID。
    /// </summary>
    public long LastNavigationCharacterPatchPathId { get; set; } = NavigationCharacterScriptPatcher.DefaultScriptPathId;

    /// <summary>
    /// 获取或设置上次修补 NavigationCharacter prefab AB 的脚本类名。
    /// </summary>
    public string LastNavigationCharacterPatchScriptName { get; set; } = NavigationCharacterScriptPatcher.DefaultScriptClassName;

    /// <summary>
    /// 获取或设置上次修补 NavigationCharacter prefab AB 的输出压缩方式。
    /// </summary>
    public string LastNavigationCharacterPatchCompression { get; set; } = "keep";

    /// <summary>
    /// 获取或设置上次修补 NavigationCharacter prefab AB 是否只执行 dry-run。
    /// </summary>
    public bool LastNavigationCharacterPatchDryRun { get; set; }

    /// <summary>
    /// 获取或设置上次使用的GIF 导出清单路径，用于恢复 Viewer 上次使用的导出设置，减少重复输入。
    /// </summary>
    public string? LastGifExportPath { get; set; }

    /// <summary>
    /// 表示上次使用的GIF 输出帧率，用于恢复 Viewer 上次使用的导出设置，减少重复输入。
    /// </summary>
    public int LastGifFps { get; set; } = 30;

    /// <summary>
    /// 获取或设置上次使用的GIF是否使用帧范围，用于恢复 Viewer 上次使用的导出设置，减少重复输入。
    /// </summary>
    public bool LastGifUseFrameRange { get; set; }

    /// <summary>
    /// 获取或设置上次使用的GIF起始帧，用于恢复 Viewer 上次使用的导出设置，减少重复输入。
    /// </summary>
    public double LastGifStartFrame { get; set; }

    /// <summary>
    /// 表示上次使用的GIF结束帧，用于恢复 Viewer 上次使用的导出设置，减少重复输入。
    /// </summary>
    public double LastGifEndFrame { get; set; } = 60;

    /// <summary>
    /// 获取或设置上次使用的GIF压缩输出帧序列，用于恢复 Viewer 上次使用的导出设置，减少重复输入。
    /// </summary>
    public bool LastGifCompressFrames { get; set; }

    /// <summary>
    /// 获取或设置上次使用的GIF是否使用目标宽度，用于恢复 Viewer 上次使用的导出设置，减少重复输入。
    /// </summary>
    public bool LastGifUseTargetWidth { get; set; }

    /// <summary>
    /// 获取或设置上次使用的GIF目标宽度，用于恢复 Viewer 上次使用的导出设置，减少重复输入。
    /// </summary>
    public int LastGifTargetWidth { get; set; }

    /// <summary>
    /// 获取或设置上次使用的GIF是否使用目标高度，用于恢复 Viewer 上次使用的导出设置，减少重复输入。
    /// </summary>
    public bool LastGifUseTargetHeight { get; set; }

    /// <summary>
    /// 获取或设置上次使用的GIF目标高度，用于恢复 Viewer 上次使用的导出设置，减少重复输入。
    /// </summary>
    public int LastGifTargetHeight { get; set; }

    /// <summary>
    /// 表示上次使用的GIF透明边距，用于恢复 Viewer 上次使用的导出设置，减少重复输入。
    /// </summary>
    public int LastGifPadding { get; set; } = 80;

    /// <summary>
    /// 表示上次使用的GIF 输出缩放比例，用于恢复 Viewer 上次使用的导出设置，减少重复输入。
    /// </summary>
    public double LastGifScale { get; set; } = 1.0;

    /// <summary>
    /// 获取或设置上次使用的GIFHigh质量，用于恢复 Viewer 上次使用的导出设置，减少重复输入。
    /// </summary>
    public bool LastGifHighQuality { get; set; }

    /// <summary>
    /// 获取或设置上次使用的GIF是否显示Hidden，用于恢复 Viewer 上次使用的导出设置，减少重复输入。
    /// </summary>
    public bool LastGifShowHidden { get; set; }

    /// <summary>
    /// 获取或设置上次使用的GIF角色信息Defaults，用于恢复 Viewer 上次使用的导出设置，减少重复输入。
    /// </summary>
    public bool LastGifCharacterDefaults { get; set; }

    /// <summary>
    /// 表示上次使用的GIF消隐背景颜色，用于恢复 Viewer 上次使用的导出设置，减少重复输入。
    /// </summary>
    public string LastGifMatteColor { get; set; } = "#FFFFFF";

    /// <summary>
    /// 加载持久化设置或资源；读取失败时由调用方使用默认状态。
    /// </summary>
    /// <returns>加载后的设置或资源对象。</returns>
    public static ViewerSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new ViewerSettings();
            }

            var settings = JsonSerializer.Deserialize<ViewerSettings>(File.ReadAllText(SettingsPath, Encoding.UTF8), JsonOptions)
                ?? new ViewerSettings();
            settings.Normalize();
            return settings;
        }
        catch
        {
            return new ViewerSettings();
        }
    }

    private void Normalize()
    {
        if (LastUnityNavicharaCharacterId < 0)
        {
            LastUnityNavicharaCharacterId = 0;
        }

        LastUnityNavicharaProfileTemplatePath = NormalizePath(LastUnityNavicharaProfileTemplatePath);
        LastUnityNavicharaProfilePath = NormalizePath(LastUnityNavicharaProfilePath);
        LastUnityNavicharaOutputDirectory = NormalizePath(LastUnityNavicharaOutputDirectory);
        LastNavigationCharacterPatchInputPath = NormalizePath(LastNavigationCharacterPatchInputPath);
        LastNavigationCharacterPatchOutputPath = NormalizePath(LastNavigationCharacterPatchOutputPath);
        if (string.IsNullOrWhiteSpace(LastNavigationCharacterPatchScriptName))
        {
            LastNavigationCharacterPatchScriptName = NavigationCharacterScriptPatcher.DefaultScriptClassName;
        }

        if (!IsNavigationCharacterPatchCompression(LastNavigationCharacterPatchCompression))
        {
            LastNavigationCharacterPatchCompression = "keep";
        }

        LastGifExportPath = NormalizePath(LastGifExportPath);
        if (LastGifFps is < 1 or > 60)
        {
            LastGifFps = 30;
        }

        if (!double.IsFinite(LastGifStartFrame) || LastGifStartFrame < 0)
        {
            LastGifStartFrame = 0;
        }

        if (!double.IsFinite(LastGifEndFrame) || LastGifEndFrame < LastGifStartFrame)
        {
            LastGifEndFrame = Math.Max(LastGifStartFrame, 60);
        }

        if (LastGifTargetWidth < 0)
        {
            LastGifTargetWidth = 0;
        }

        if (LastGifTargetHeight < 0)
        {
            LastGifTargetHeight = 0;
        }

        if (LastGifUseTargetWidth && LastGifTargetWidth <= 0)
        {
            LastGifUseTargetWidth = false;
        }

        if (LastGifUseTargetHeight && LastGifTargetHeight <= 0)
        {
            LastGifUseTargetHeight = false;
        }

        if (LastGifUseTargetWidth && LastGifUseTargetHeight)
        {
            LastGifUseTargetHeight = false;
        }

        if (LastGifPadding < 0)
        {
            LastGifPadding = 80;
        }

        if (!double.IsFinite(LastGifScale) || LastGifScale <= 0)
        {
            LastGifScale = 1.0;
        }

        if (string.IsNullOrWhiteSpace(LastGifMatteColor))
        {
            LastGifMatteColor = "#FFFFFF";
        }
    }

    private static string? NormalizePath(string? path)
    {
        return string.IsNullOrWhiteSpace(path) ? null : path;
    }

    private static bool IsNavigationCharacterPatchCompression(string value)
    {
        return value is "keep" or "none" or "lz4" or "lzma";
    }

    /// <summary>
    /// 保存持久化设置或处理结果；失败时不阻断主流程。
    /// </summary>
    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, JsonOptions), new UTF8Encoding(false));
        }
        catch
        {
            // Settings persistence should never block the viewer workflow.
        }
    }

    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SbScene.Viewer",
        "settings.json");
}
