using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using NavigationCharacterPatcher;
using SbScene.Core.Output;
using SbScene.Core.Rendering;
using SbScene.Core.Semantics;
using SbScene.Core.Unity;

namespace SbScene.Viewer;

/// <summary>
/// 表示 Viewer 主窗口的导出部分，负责 GIF 和 Unity NaviChara 导出流程。
/// </summary>
public partial class MainWindow : Window
{
    private async void ExportGif_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetLoadedScene(out var scene, out _))
        {
            MessageBox.Show(this, "请先打开 .sbscene 文件。", "导出 GIF", MessageBoxButton.OK, MessageBoxImage.Information);
            SetStatus("请先打开 .sbscene 文件。");
            return;
        }

        var svoPath = _svoPath;
        if (svoPath is null || _resources is null)
        {
            MessageBox.Show(this, "请先绑定 .svo 文件。GIF 导出需要可解析的 SVO。", "导出 GIF", MessageBoxButton.OK, MessageBoxImage.Information);
            SetStatus("请先绑定 .svo 文件。");
            return;
        }

        var dialog = new GifExportDialog(_settings, _scenePath)
        {
            Owner = this,
        };

        if (dialog.ShowDialog() != true || dialog.Result is not { } gifSettings)
        {
            return;
        }

        SaveGifExportSettings(gifSettings);
        var animations = BuildGifAnimationSelections(gifSettings.CharacterDefaults);
        using var busy = BeginBusy("正在导出 GIF...");
        try
        {
            var result = await Task.Run(() => ViewerGifExporter.Export(scene, svoPath, gifSettings, animations));
            var warningText = result.Warnings.Count == 0 ? string.Empty : $"，警告 {result.Warnings.Count:N0} 条";
            SetStatus(string.Format(
                CultureInfo.InvariantCulture,
                "GIF 导出完成：{0:N0} 帧，{1}x{2}，{3} fps{4}。{5}",
                result.FrameCount,
                result.Width,
                result.Height,
                result.Fps,
                warningText,
                gifSettings.OutputPath));

            var message = string.Format(
                CultureInfo.InvariantCulture,
                "已生成：\n{0}\n\n{1:N0} 帧，{2}x{3}，{4} fps，源帧 {5:0.###}..{6:0.###}。\n每帧最多渲染 {7:N0}/{8:N0} 项。",
                gifSettings.OutputPath,
                result.FrameCount,
                result.Width,
                result.Height,
                result.Fps,
                result.StartFrame,
                result.EndFrame,
                result.RenderedItemCount,
                result.CandidateItemCount);
            if (result.Warnings.Count > 0)
            {
                message += $"\n\n警告 {result.Warnings.Count:N0} 条，首条：{result.Warnings[0]}";
            }

            MessageBox.Show(this, message, "GIF 导出完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "GIF 导出失败", MessageBoxButton.OK, MessageBoxImage.Error);
            SetStatus($"GIF 导出失败：{ex.Message}");
        }
    }

    private async void ExportUnityNavicharaProfileTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetLoadedScene(out var scene, out var scenePath))
        {
            ShowExportNotice("请先打开 .sbscene 文件。");
            return;
        }

        var defaultTemplatePath = $"{Path.GetFileNameWithoutExtension(scenePath)}.profiletemplate.json";
        var dialog = new SaveFileDialog
        {
            Title = "保存 NaviChara profiletemplate JSON",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            FileName = Path.GetFileName(_settings.LastUnityNavicharaProfileTemplatePath ?? defaultTemplatePath),
            DefaultExt = ".json",
            AddExtension = true,
            OverwritePrompt = true,
        };
        var initialTemplateDirectory = GetExistingDirectory(_settings.LastUnityNavicharaProfileTemplatePath)
            ?? GetExistingDirectory(scenePath);
        if (initialTemplateDirectory is not null)
        {
            dialog.InitialDirectory = initialTemplateDirectory;
        }

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        using var busy = BeginBusy("正在生成 NaviChara profiletemplate JSON...");
        try
        {
            var json = await Task.Run(() =>
            {
                var template = UnityNavicharaExporter.BuildProfileTemplate(scene);
                return JsonSerializer.Serialize(template, CreateUnityNavicharaJsonOptions(indented: true));
            });
            await File.WriteAllTextAsync(dialog.FileName, json, new UTF8Encoding(false));
            _settings.LastUnityNavicharaProfileTemplatePath = dialog.FileName;
            _settings.Save();
            SetStatus($"已写出 NaviChara profiletemplate：{dialog.FileName}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "生成 profiletemplate 失败", MessageBoxButton.OK, MessageBoxImage.Error);
            SetStatus($"生成 profiletemplate 失败：{ex.Message}");
        }
    }

    private async void ExportUnityNavichara_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetLoadedScene(out var scene, out var scenePath))
        {
            ShowExportNotice("请先打开 .sbscene 文件。");
            return;
        }

        if (_svoPath is null || _resources is null)
        {
            ShowExportNotice("请先绑定 .svo 文件。Unity NaviChara 正式导出需要可解析的 SVO。");
            return;
        }

        var dialog = new UnityNavicharaExportDialog(_settings, scenePath)
        {
            Owner = this,
        };

        if (dialog.ShowDialog() != true || dialog.Result is not { } exportSettings)
        {
            return;
        }

        SaveUnityNavicharaExportSettings(exportSettings);

        using var busy = BeginBusy("正在导出 Unity NaviChara...");
        try
        {
            var profile = UnityNavicharaProfileLoader.Load(exportSettings.ProfilePath);
            var options = new UnityNavicharaExportOptions
            {
                CharacterId = exportSettings.CharacterId,
                Profile = profile,
                ExtractSprites = exportSettings.ExtractSprites,
                WriteValidationFrames = exportSettings.WriteValidationFrames,
                Strict = exportSettings.Strict,
                AutoCenter = exportSettings.AutoCenter,
                BakeSampledCurves = exportSettings.BakeSampledCurves,
                AllowPlaceholderClips = exportSettings.AllowPlaceholderClips,
            };

            var result = await Task.Run(() =>
            {
                Directory.CreateDirectory(exportSettings.OutputDirectory);
                return UnityNavicharaExporter.Export(scene, scenePath, _svoPath, exportSettings.OutputDirectory, options);
            });

            Directory.CreateDirectory(exportSettings.OutputDirectory);
            var jsonOptions = CreateUnityNavicharaJsonOptions(indented: true);
            var exportJsonPath = Path.Combine(exportSettings.OutputDirectory, "navichara-export.json");
            await File.WriteAllTextAsync(exportJsonPath, JsonSerializer.Serialize(result.Export, jsonOptions), new UTF8Encoding(false));

            var diagnosticsPath = Path.Combine(exportSettings.OutputDirectory, "diagnostics.md");
            await File.WriteAllTextAsync(diagnosticsPath, UnityNavicharaExporter.FormatDiagnosticsMarkdown(result.Diagnostics), new UTF8Encoding(false));

            var summary = string.Format(
                CultureInfo.InvariantCulture,
                "Unity NaviChara 导出完成：Nodes={0:N0}, Sprites={1:N0}, Clips={2:N0}, Diagnostics={3:N0}",
                result.Export.Nodes.Count,
                result.Export.Sprites.Count,
                result.Export.Clips.Count,
                result.Diagnostics.Count);

            if (result.Failed)
            {
                SetStatus($"{summary}。请检查 diagnostics.md。");
                MessageBox.Show(
                    this,
                    $"已生成：\n{exportJsonPath}\n{diagnosticsPath}\n\n导出诊断包含错误或 strict 模式拒绝的条目，请检查 diagnostics.md。",
                    "Unity NaviChara 导出诊断失败",
                    MessageBoxButton.OK,
                MessageBoxImage.Warning);
                return;
            }

            SetStatus($"{summary}。输出目录：{exportSettings.OutputDirectory}");
            MessageBox.Show(
                this,
                $"已生成：\n{exportJsonPath}\n{diagnosticsPath}",
                "Unity NaviChara 导出完成",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Unity NaviChara 导出失败", MessageBoxButton.OK, MessageBoxImage.Error);
            SetStatus($"Unity NaviChara 导出失败：{ex.Message}");
        }
    }

    private async void PatchNavigationCharacterPrefab_Click(object sender, RoutedEventArgs e)
    {
        const string title = "修补 NavigationCharacter prefab AB";
        var dialog = new NavigationCharacterPatchDialog(_settings)
        {
            Owner = this,
        };

        if (dialog.ShowDialog() != true || dialog.Result is not { } patchSettings)
        {
            return;
        }

        SaveNavigationCharacterPatchSettings(patchSettings);

        using var busy = BeginBusy(patchSettings.DryRun
            ? "正在 dry-run 检查 NavigationCharacter prefab AB..."
            : "正在修补 NavigationCharacter prefab AB...");
        try
        {
            var result = await Task.Run(() =>
            {
                var patcher = new NavigationCharacterScriptPatcher();
                return patcher.Patch(new PatchOptions
                {
                    InputPath = patchSettings.InputPath,
                    OutputPath = patchSettings.OutputPath,
                    NewScriptPathId = patchSettings.PathId,
                    ScriptClassName = patchSettings.ScriptName,
                    Compression = patchSettings.Compression,
                    DryRun = patchSettings.DryRun,
                });
            });

            if (patchSettings.DryRun)
            {
                var dryRunMessage = string.Format(
                    CultureInfo.InvariantCulture,
                    "dry-run 完成：将修改 {0:N0} 个 MonoScript PathID、{1:N0} 个 MonoBehaviour 引用。\n\n输入：{2}\n脚本类名：{3}\n目标 PathID：{4}\n有效引用：{5:N0}",
                    result.ModifiedScriptCount,
                    result.ModifiedBehaviourCount,
                    patchSettings.InputPath,
                    patchSettings.ScriptName,
                    patchSettings.PathId,
                    result.VerifiedBehaviourCount);
                SetStatus(
                    $"dry-run 完成：将修改 {result.ModifiedScriptCount:N0} 个 MonoScript PathID、" +
                    $"{result.ModifiedBehaviourCount:N0} 个 MonoBehaviour 引用。");
                MessageBox.Show(this, dryRunMessage, title, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!result.WroteOutput)
            {
                var unchangedMessage = string.Format(
                    CultureInfo.InvariantCulture,
                    "无需修改。\n\n输入：{0}\n脚本类名：{1}\n目标 PathID：{2}\n有效引用：{3:N0}",
                    patchSettings.InputPath,
                    patchSettings.ScriptName,
                    patchSettings.PathId,
                    result.VerifiedBehaviourCount);
                SetStatus($"NavigationCharacter prefab AB 无需修改：有效引用 {result.VerifiedBehaviourCount:N0} 个。");
                MessageBox.Show(this, unchangedMessage, title, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var outputSize = result.OutputSize > 0 && result.WroteOutput
                ? result.OutputSize
                : new FileInfo(patchSettings.OutputPath).Length;
            var message = string.Format(
                CultureInfo.InvariantCulture,
                "修补完成：修改 {0:N0} 个 MonoScript PathID、{1:N0} 个 MonoBehaviour 引用。\n\n输出：{2}\n压缩方式：{3}\n输出大小：{4}\n有效引用：{5:N0}",
                result.ModifiedScriptCount,
                result.ModifiedBehaviourCount,
                patchSettings.OutputPath,
                result.OutputCompressionName,
                FormatByteSize(outputSize),
                result.VerifiedBehaviourCount);
            SetStatus(
                $"NavigationCharacter prefab AB 修补完成：修改 {result.ModifiedScriptCount:N0} 个 MonoScript PathID、" +
                $"{result.ModifiedBehaviourCount:N0} 个 MonoBehaviour 引用，输出 {patchSettings.OutputPath}");
            MessageBox.Show(this, message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (PatchTargetNotFoundException ex)
        {
            MessageBox.Show(this, ex.Message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
            SetStatus($"NavigationCharacter prefab AB 未找到目标：{ex.Message}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, title, MessageBoxButton.OK, MessageBoxImage.Error);
            SetStatus($"NavigationCharacter prefab AB 修补失败：{ex.Message}");
        }
    }

    private IReadOnlyList<SbSceneAnimationSelection> BuildGifAnimationSelections(bool includeCharacterDefaults)
    {
        if (_scene is null)
        {
            return includeCharacterDefaults ? SbSceneCharacterAnimationDefaults.BuildSelections() : [];
        }

        var selections = new List<SbSceneAnimationSelection>();
        if (includeCharacterDefaults)
        {
            selections.AddRange(SbSceneCharacterAnimationDefaults.BuildSelections());
        }

        var usedSlots = new HashSet<int>();
        for (var index = 0; index < _animationSlots.Count && index < _scene.Surfboard.Animations.Count; index++)
        {
            if (_previewAnimationIndex == index)
            {
                selections.Add(new SbSceneAnimationSelection($"#{index}", 0)
                {
                    Index = index,
                    HasExplicitFrame = false,
                });
                usedSlots.Add(index);
            }
        }

        for (var index = 0; index < _animationSlots.Count && index < _scene.Surfboard.Animations.Count; index++)
        {
            var slot = _animationSlots[index];
            if (!slot.IsLocked || usedSlots.Contains(index))
            {
                continue;
            }

            selections.Add(new SbSceneAnimationSelection($"#{index}", slot.Frame)
            {
                Index = index,
                HasExplicitFrame = true,
            });
        }

        if (selections.Count == 0 && _selectedAnimationIndex is int selectedIndex && selectedIndex >= 0 && selectedIndex < _scene.Surfboard.Animations.Count)
        {
            selections.Add(new SbSceneAnimationSelection($"#{selectedIndex}", 0)
            {
                Index = selectedIndex,
                HasExplicitFrame = false,
            });
        }

        return selections;
    }

    private void SaveUnityNavicharaExportSettings(UnityNavicharaExportDialogResult exportSettings)
    {
        _settings.LastUnityNavicharaProfilePath = exportSettings.ProfilePath;
        _settings.LastUnityNavicharaOutputDirectory = exportSettings.OutputDirectory;
        _settings.LastUnityNavicharaCharacterId = exportSettings.CharacterId;
        _settings.LastUnityNavicharaExtractSprites = exportSettings.ExtractSprites;
        _settings.LastUnityNavicharaWriteValidationFrames = exportSettings.WriteValidationFrames;
        _settings.LastUnityNavicharaStrict = exportSettings.Strict;
        _settings.LastUnityNavicharaAutoCenter = exportSettings.AutoCenter;
        _settings.LastUnityNavicharaBakeSampledCurves = exportSettings.BakeSampledCurves;
        _settings.LastUnityNavicharaAllowPlaceholderClips = exportSettings.AllowPlaceholderClips;
        _settings.Save();
    }

    private void SaveNavigationCharacterPatchSettings(NavigationCharacterPatchDialogResult patchSettings)
    {
        _settings.LastNavigationCharacterPatchInputPath = patchSettings.InputPath;
        _settings.LastNavigationCharacterPatchOutputPath = patchSettings.OutputPath;
        _settings.LastNavigationCharacterPatchPathId = patchSettings.PathId;
        _settings.LastNavigationCharacterPatchScriptName = patchSettings.ScriptName;
        _settings.LastNavigationCharacterPatchCompression = patchSettings.CompressionValue;
        _settings.LastNavigationCharacterPatchDryRun = patchSettings.DryRun;
        _settings.Save();
    }

    private void SaveGifExportSettings(GifExportDialogResult gifSettings)
    {
        _settings.LastGifExportPath = gifSettings.OutputPath;
        _settings.LastGifFps = gifSettings.Fps;
        _settings.LastGifUseFrameRange = gifSettings.UseFrameRange;
        _settings.LastGifStartFrame = gifSettings.StartFrame;
        _settings.LastGifEndFrame = gifSettings.EndFrame;
        _settings.LastGifCompressFrames = gifSettings.CompressFrames;
        _settings.LastGifUseTargetWidth = gifSettings.UseTargetWidth;
        _settings.LastGifTargetWidth = gifSettings.TargetWidthValue;
        _settings.LastGifUseTargetHeight = gifSettings.UseTargetHeight;
        _settings.LastGifTargetHeight = gifSettings.TargetHeightValue;
        _settings.LastGifPadding = gifSettings.Padding;
        _settings.LastGifScale = gifSettings.Scale;
        _settings.LastGifHighQuality = gifSettings.HighQuality;
        _settings.LastGifShowHidden = gifSettings.ShowHidden;
        _settings.LastGifCharacterDefaults = gifSettings.CharacterDefaults;
        _settings.LastGifMatteColor = gifSettings.MatteColorText;
        _settings.Save();
    }

    private bool TryGetLoadedScene(out SbSceneFile scene, out string scenePath)
    {
        if (_scene is not null && _scenePath is not null)
        {
            scene = _scene;
            scenePath = _scenePath;
            return true;
        }

        scene = null!;
        scenePath = string.Empty;
        return false;
    }

    private void ShowExportNotice(string message)
    {
        MessageBox.Show(this, message, "Unity NaviChara 导出", MessageBoxButton.OK, MessageBoxImage.Information);
        SetStatus(message);
    }

    private static string FormatByteSize(long bytes)
    {
        string[] units = ["B", "KiB", "MiB", "GiB"];
        var value = (double)Math.Max(0, bytes);
        var unitIndex = 0;
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return unitIndex == 0
            ? string.Format(CultureInfo.InvariantCulture, "{0:N0} {1}", value, units[unitIndex])
            : string.Format(CultureInfo.InvariantCulture, "{0:N2} {1} ({2:N0} B)", value, units[unitIndex], bytes);
    }

    private static JsonSerializerOptions CreateUnityNavicharaJsonOptions(bool indented)
    {
        var options = SbSceneJson.CreateOptions(indented);
        options.DictionaryKeyPolicy = null;
        return options;
    }
}
