using System.IO;
using System.Text;
using System.Text.Json;

namespace SbScene.Viewer;

internal sealed class ViewerSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public string? LastUnityNavicharaProfileTemplatePath { get; set; }

    public string? LastUnityNavicharaProfilePath { get; set; }

    public string? LastUnityNavicharaOutputDirectory { get; set; }

    public int LastUnityNavicharaCharacterId { get; set; }

    public bool LastUnityNavicharaExtractSprites { get; set; }

    public bool LastUnityNavicharaWriteValidationFrames { get; set; }

    public bool LastUnityNavicharaStrict { get; set; }

    public bool LastUnityNavicharaAutoCenter { get; set; } = true;

    public bool LastUnityNavicharaBakeSampledCurves { get; set; }

    public bool LastUnityNavicharaAllowPlaceholderClips { get; set; }

    public string? LastGifExportPath { get; set; }

    public int LastGifFps { get; set; } = 30;

    public bool LastGifUseFrameRange { get; set; }

    public double LastGifStartFrame { get; set; }

    public double LastGifEndFrame { get; set; } = 60;

    public bool LastGifCompressFrames { get; set; }

    public bool LastGifUseTargetWidth { get; set; }

    public int LastGifTargetWidth { get; set; }

    public bool LastGifUseTargetHeight { get; set; }

    public int LastGifTargetHeight { get; set; }

    public int LastGifPadding { get; set; } = 80;

    public double LastGifScale { get; set; } = 1.0;

    public bool LastGifHighQuality { get; set; }

    public bool LastGifShowHidden { get; set; }

    public bool LastGifCharacterDefaults { get; set; }

    public string LastGifMatteColor { get; set; } = "#FFFFFF";

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
