using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace SbScene.Viewer;

public partial class UnityNavicharaExportDialog : Window
{
    internal UnityNavicharaExportDialog(ViewerSettings settings, string? scenePath)
    {
        InitializeComponent();
        CharacterIdTextBox.Text = Math.Max(0, settings.LastUnityNavicharaCharacterId).ToString(CultureInfo.InvariantCulture);
        ProfilePathTextBox.Text = settings.LastUnityNavicharaProfilePath ?? string.Empty;
        OutputDirectoryTextBox.Text = settings.LastUnityNavicharaOutputDirectory ?? BuildDefaultOutputDirectory(scenePath) ?? string.Empty;
        ExtractSpritesCheckBox.IsChecked = settings.LastUnityNavicharaExtractSprites;
        WriteValidationFramesCheckBox.IsChecked = settings.LastUnityNavicharaWriteValidationFrames;
        StrictCheckBox.IsChecked = settings.LastUnityNavicharaStrict;
        AutoCenterCheckBox.IsChecked = settings.LastUnityNavicharaAutoCenter;
        BakeSampledCurvesCheckBox.IsChecked = settings.LastUnityNavicharaBakeSampledCurves;
        AllowPlaceholderClipsCheckBox.IsChecked = settings.LastUnityNavicharaAllowPlaceholderClips;
    }

    public UnityNavicharaExportDialogResult? Result { get; private set; }

    private static string? BuildDefaultOutputDirectory(string? scenePath)
    {
        if (!string.IsNullOrWhiteSpace(scenePath))
        {
            var directory = Path.GetDirectoryName(scenePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                return Path.Combine(directory, "navichara-export");
            }
        }

        return null;
    }

    private void BrowseProfile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择 NaviChara profile JSON",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists = true,
        };

        if (TryGetExistingDirectory(ProfilePathTextBox.Text, out var initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        if (dialog.ShowDialog(this) == true)
        {
            ProfilePathTextBox.Text = dialog.FileName;
            if (string.IsNullOrWhiteSpace(OutputDirectoryTextBox.Text))
            {
                var directory = Path.GetDirectoryName(dialog.FileName);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    OutputDirectoryTextBox.Text = Path.Combine(directory, "navichara-export");
                }
            }
        }
    }

    private void BrowseOutputDirectory_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择 NaviChara 导出输出目录",
            Multiselect = false,
        };

        if (TryGetExistingDirectory(OutputDirectoryTextBox.Text, out var initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        if (dialog.ShowDialog(this) == true)
        {
            OutputDirectoryTextBox.Text = dialog.FolderName;
        }
    }

    private void CharacterIdTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = e.Text.Any(static ch => !char.IsDigit(ch));
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        ErrorTextBlock.Text = string.Empty;

        var profilePath = ProfilePathTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(profilePath))
        {
            ShowError("请选择 profile JSON。");
            return;
        }

        if (!File.Exists(profilePath))
        {
            ShowError($"profile JSON 不存在：{profilePath}");
            return;
        }

        var outputDirectory = OutputDirectoryTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            ShowError("请选择输出目录。");
            return;
        }

        if (!int.TryParse(CharacterIdTextBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var characterId)
            || characterId < 0)
        {
            ShowError("CharacterId 必须是非负整数。");
            return;
        }

        Result = new UnityNavicharaExportDialogResult(
            profilePath,
            outputDirectory,
            characterId,
            ExtractSpritesCheckBox.IsChecked == true,
            WriteValidationFramesCheckBox.IsChecked == true,
            StrictCheckBox.IsChecked == true,
            AutoCenterCheckBox.IsChecked == true,
            BakeSampledCurvesCheckBox.IsChecked == true,
            AllowPlaceholderClipsCheckBox.IsChecked == true);
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
}

public sealed record UnityNavicharaExportDialogResult(
    string ProfilePath,
    string OutputDirectory,
    int CharacterId,
    bool ExtractSprites,
    bool WriteValidationFrames,
    bool Strict,
    bool AutoCenter,
    bool BakeSampledCurves,
    bool AllowPlaceholderClips);
