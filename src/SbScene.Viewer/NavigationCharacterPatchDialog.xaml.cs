using System.Globalization;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using NavigationCharacterPatcher;

namespace SbScene.Viewer;

/// <summary>
/// 表示 NavigationCharacter prefab AssetBundle 修补对话框，负责收集输入输出和 patcher 选项。
/// </summary>
public partial class NavigationCharacterPatchDialog : Window
{
    private static readonly IReadOnlyList<CompressionOption> CompressionOptions =
    [
        new("keep", BundleCompression.Keep),
        new("none", BundleCompression.None),
        new("lz4", BundleCompression.Lz4),
        new("lzma", BundleCompression.Lzma),
    ];

    internal NavigationCharacterPatchDialog(ViewerSettings settings)
    {
        InitializeComponent();

        InputPathTextBox.Text = settings.LastNavigationCharacterPatchInputPath ?? string.Empty;
        OutputPathTextBox.Text = settings.LastNavigationCharacterPatchOutputPath
            ?? BuildDefaultOutputPath(settings.LastNavigationCharacterPatchInputPath)
            ?? string.Empty;
        PathIdTextBox.Text = settings.LastNavigationCharacterPatchPathId.ToString(CultureInfo.InvariantCulture);
        ScriptNameTextBox.Text = string.IsNullOrWhiteSpace(settings.LastNavigationCharacterPatchScriptName)
            ? NavigationCharacterScriptPatcher.DefaultScriptClassName
            : settings.LastNavigationCharacterPatchScriptName;
        CompressionComboBox.ItemsSource = CompressionOptions;
        CompressionComboBox.SelectedValue = SelectCompressionValue(settings.LastNavigationCharacterPatchCompression);
        DryRunCheckBox.IsChecked = settings.LastNavigationCharacterPatchDryRun;
    }

    /// <summary>
    /// 获取或设置结果，用于返回修补输入、输出和选项。
    /// </summary>
    public NavigationCharacterPatchDialogResult? Result { get; private set; }

    private static string SelectCompressionValue(string? value)
    {
        return CompressionOptions.Any(option => option.Value == value) ? value! : "keep";
    }

    private static string? BuildDefaultOutputPath(string? inputPath)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return null;
        }

        var directory = Path.GetDirectoryName(inputPath) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(inputPath);
        var extension = Path.GetExtension(inputPath);
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return Path.Combine(directory, name + ".patched" + extension);
    }

    private void BrowseInput_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择 prefab AssetBundle",
            Filter = "AssetBundle files (*.ab)|*.ab|All files (*.*)|*.*",
            CheckFileExists = true,
        };

        if (TryGetExistingDirectory(InputPathTextBox.Text, out var initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        if (dialog.ShowDialog(this) == true)
        {
            InputPathTextBox.Text = dialog.FileName;
            OutputPathTextBox.Text = BuildDefaultOutputPath(dialog.FileName) ?? string.Empty;
        }
    }

    private void BrowseOutput_Click(object sender, RoutedEventArgs e)
    {
        var currentOutput = OutputPathTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(currentOutput))
        {
            currentOutput = BuildDefaultOutputPath(InputPathTextBox.Text.Trim()) ?? "NavigationCharacter.patched.ab";
        }

        var defaultExt = Path.GetExtension(currentOutput);
        var dialog = new SaveFileDialog
        {
            Title = "保存修补后的 AssetBundle",
            Filter = "AssetBundle files (*.ab)|*.ab|All files (*.*)|*.*",
            FileName = Path.GetFileName(currentOutput),
            DefaultExt = string.IsNullOrWhiteSpace(defaultExt) ? ".ab" : defaultExt,
            AddExtension = true,
            OverwritePrompt = true,
        };

        if (TryGetExistingDirectory(currentOutput, out var initialDirectory)
            || TryGetExistingDirectory(InputPathTextBox.Text, out initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        if (dialog.ShowDialog(this) == true)
        {
            OutputPathTextBox.Text = dialog.FileName;
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        ErrorTextBlock.Text = string.Empty;

        var inputPath = InputPathTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            ShowError("请选择输入 AB。");
            return;
        }

        if (!File.Exists(inputPath))
        {
            ShowError($"输入 AB 不存在：{inputPath}");
            return;
        }

        var outputPath = OutputPathTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            ShowError("请选择输出 AB 路径。");
            return;
        }

        if (!long.TryParse(PathIdTextBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var pathId))
        {
            ShowError("目标 PathID 必须是有效的 64 位整数。");
            return;
        }

        var scriptName = ScriptNameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(scriptName))
        {
            ShowError("脚本类名不能为空。");
            return;
        }

        var compression = CompressionOptions.FirstOrDefault(option => option.Value == (string?)CompressionComboBox.SelectedValue)
            ?? CompressionOptions[0];

        Result = new NavigationCharacterPatchDialogResult(
            inputPath,
            outputPath,
            pathId,
            scriptName,
            compression.Value,
            compression.Compression,
            DryRunCheckBox.IsChecked == true);
        DialogResult = true;
    }

    private void ShowError(string message)
    {
        ErrorTextBlock.Text = message;
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

    private sealed record CompressionOption(string Value, BundleCompression Compression)
    {
        public string Label => Value;
    }
}

/// <summary>
/// 表示 NavigationCharacter prefab AssetBundle 修补 Dialog 结果。
/// </summary>
/// <param name="InputPath">要读取的输入 AssetBundle 路径。</param>
/// <param name="OutputPath">要写出的 AssetBundle 路径。</param>
/// <param name="PathId">要写入 MonoBehaviour m_Script.m_PathID 的目标 PathID。</param>
/// <param name="ScriptName">要定位的 MonoScript 类名。</param>
/// <param name="CompressionValue">用于持久化设置的压缩方式名称。</param>
/// <param name="Compression">传给 patcher 的压缩方式。</param>
/// <param name="DryRun">是否只报告将修改的对象数量。</param>
public sealed record NavigationCharacterPatchDialogResult(
    string InputPath,
    string OutputPath,
    long PathId,
    string ScriptName,
    string CompressionValue,
    BundleCompression Compression,
    bool DryRun);
