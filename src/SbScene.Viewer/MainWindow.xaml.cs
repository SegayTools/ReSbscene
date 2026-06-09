using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using SbScene.Core.Output;
using SbScene.Core.Rendering;
using SbScene.Core.Semantics;
using SbScene.Core.Unity;

namespace SbScene.Viewer;

/// <summary>
/// 表示 Viewer 主窗口，协调文件加载、资源状态、渲染视图和用户操作。
/// </summary>
public partial class MainWindow : Window
{
    private const double PlaybackFramesPerSecond = 60.0;

    private SbSceneFile? _scene;

    private SceneRenderBuildCache? _renderBuildCache;

    private SvoRenderResources? _resources;

    private RenderScene? _renderScene;

    private string? _scenePath;

    private string? _svoPath;

    private string? _pendingSvoPath;

    private bool _controlsReady;

    private readonly HashSet<int> _hiddenNodeIndexes = [];

    private readonly HashSet<int> _shownNodeIndexes = [];

    private HashSet<int>? _selectedNodeIndexes;

    private int? _selectedNodeIndex;

    private readonly List<AnimationListItem> _animationItems = [];

    private readonly List<AnimationPlaybackSlot> _animationSlots = [];

    private bool _isUpdatingAnimationControls;

    private int? _selectedAnimationIndex;

    private int? _previewAnimationIndex;

    private readonly ViewerSettings _settings = ViewerSettings.Load();

    private bool _isPlaybackRenderingHooked;

    private TimeSpan? _playbackRenderingStartTime;

    private double _playbackStartFrame;

    private double _currentFrame;

    private double _endFrame;

    private bool _isPlaying;

    private bool _isLooping;

    /// <summary>
    /// 初始化MainWindow 实例，并保存调用方提供的核心数据。
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        _controlsReady = true;
        SetZoom(1);
        UpdateAnimationControls();
        UpdateSelectedNodeInfo();
        SetStatus("就绪。");
        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var args = Environment.GetCommandLineArgs().Skip(1).ToArray();
        var scenePath = args.FirstOrDefault(IsSceneFile);
        var svoPath = args.FirstOrDefault(IsSvoFile);

        if (scenePath is not null)
        {
            await LoadSceneAsync(scenePath, svoPath);
        }
        else if (svoPath is not null)
        {
            _pendingSvoPath = svoPath;
            SvoSummaryTextBlock.Text = $"预选 SVO：{Path.GetFileName(svoPath)}";
            SetStatus("已记录 SVO，打开 sbscene 后会尝试绑定。");
        }
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        DetachPlaybackRendering();
    }

    private async void OpenScene_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "打开 sbscene",
            Filter = "SbScene files (*.sbscene)|*.sbscene|All files (*.*)|*.*",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) == true)
        {
            await LoadSceneAsync(dialog.FileName, null);
        }
    }

    private async void OpenSvo_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "绑定 SVO",
            Filter = "SVO files (*.svo)|*.svo|All files (*.*)|*.*",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) == true)
        {
            await LoadSvoAsync(dialog.FileName);
        }
    }

    private async void Reload_Click(object sender, RoutedEventArgs e)
    {
        if (_scenePath is null)
        {
            SetStatus("还没有加载 sbscene。");
            return;
        }

        await LoadSceneAsync(_scenePath, _svoPath);
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        var files = ((string[])e.Data.GetData(DataFormats.FileDrop))
            .Where(File.Exists)
            .ToArray();
        var scenePath = files.FirstOrDefault(IsSceneFile);
        var svoPath = files.FirstOrDefault(IsSvoFile);

        if (scenePath is not null)
        {
            await LoadSceneAsync(scenePath, svoPath);
        }
        else if (svoPath is not null)
        {
            await LoadSvoAsync(svoPath);
        }
    }

    private async Task LoadSceneAsync(string path, string? explicitSvoPath)
    {
        if (!File.Exists(path))
        {
            SetStatus($"找不到 sbscene：{path}");
            return;
        }

        using var busy = BeginBusy($"正在加载 {Path.GetFileName(path)}...");
        try
        {
            var scene = await Task.Run(() => new SbSceneParser().ParseFile(path));
            _scene = scene;
            _renderBuildCache = new SceneRenderBuildCache(scene);
            _scenePath = path;
            _resources = null;
            _svoPath = null;
            _selectedNodeIndex = null;
            _selectedNodeIndexes = null;
            RenderSurface.ClearViewportBounds();
            SelectionRenderSurface.ClearViewportBounds();

            var svoPath = explicitSvoPath ?? _pendingSvoPath ?? FindSiblingSvo(path);
            _pendingSvoPath = null;
            if (svoPath is not null)
            {
                try
                {
                    _resources = await Task.Run(() => SvoRenderResources.Load(scene, svoPath));
                    _svoPath = svoPath;
                }
                catch (Exception ex)
                {
                    SvoSummaryTextBlock.Text = "SVO 绑定失败。";
                    SetStatus($"sbscene 已加载，但 SVO 解析失败：{ex.Message}");
                }
            }

            _hiddenNodeIndexes.Clear();
            _shownNodeIndexes.Clear();
            RefreshAnimationList();
            RefreshNodeTree();
            UpdateSceneSummary();
            RebuildRender();
            FitSceneToViewport();
        }
        catch (Exception ex)
        {
            ClearScene();
            MessageBox.Show(this, ex.Message, "加载 sbscene 失败", MessageBoxButton.OK, MessageBoxImage.Error);
            SetStatus($"加载失败：{ex.Message}");
        }
    }

    private async Task LoadSvoAsync(string path)
    {
        if (!File.Exists(path))
        {
            SetStatus($"找不到 SVO：{path}");
            return;
        }

        if (_scene is null)
        {
            _pendingSvoPath = path;
            SvoSummaryTextBlock.Text = $"预选 SVO：{Path.GetFileName(path)}";
            SetStatus("已记录 SVO，打开 sbscene 后会尝试绑定。");
            return;
        }

        _svoPath = path;
        using var busy = BeginBusy($"正在解码 {Path.GetFileName(path)}...");
        try
        {
            var scene = _scene;
            _resources = await Task.Run(() => SvoRenderResources.Load(scene, path));
            UpdateSceneSummary();
            RebuildRender();
        }
        catch (Exception ex)
        {
            _resources = null;
            MessageBox.Show(this, ex.Message, "绑定 SVO 失败", MessageBoxButton.OK, MessageBoxImage.Error);
            UpdateSceneSummary();
            RebuildRender();
            SetStatus($"SVO 绑定失败：{ex.Message}");
        }
    }

    private void ClearScene()
    {
        _scene = null;
        _renderBuildCache = null;
        _resources = null;
        _renderScene = null;
        _scenePath = null;
        _selectedNodeIndex = null;
        _selectedNodeIndexes = null;
        _hiddenNodeIndexes.Clear();
        _shownNodeIndexes.Clear();
        RefreshAnimationList();
        NodeTree.ItemsSource = null;
        RenderSurface.Scene = null;
        RenderSurface.PrimaryHighlightedNodeIndex = null;
        RenderSurface.HighlightedNodeIndexes = new HashSet<int>();
        SelectionRenderSurface.Scene = null;
        SelectionRenderSurface.PrimaryHighlightedNodeIndex = null;
        SelectionRenderSurface.HighlightedNodeIndexes = new HashSet<int>();
        SceneTitleTextBlock.Text = "未加载 scene";
        SceneSummaryTextBlock.Text = string.Empty;
        SvoSummaryTextBlock.Text = string.Empty;
        UpdateSelectedNodeInfo();
        SetSelectionPreviewEmpty("未选择节点。", visible: true);
        EmptyOverlay.Visibility = Visibility.Visible;
    }

    private IDisposable BeginBusy(string status)
    {
        SetStatus(status);
        Mouse.OverrideCursor = Cursors.Wait;
        return new BusyScope(() => Mouse.OverrideCursor = null);
    }

    private void SetStatus(string status)
    {
        StatusTextBlock.Text = status;
    }

    private static string? GetExistingDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (Directory.Exists(path))
        {
            return path;
        }

        var directory = Path.GetDirectoryName(path);
        return !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory)
            ? directory
            : null;
    }

    private static string? FindSiblingSvo(string scenePath)
    {
        var exact = Path.ChangeExtension(scenePath, ".svo");
        if (File.Exists(exact))
        {
            return exact;
        }

        var directory = Path.GetDirectoryName(scenePath);
        if (directory is null || !Directory.Exists(directory))
        {
            return null;
        }

        var candidates = Directory.GetFiles(directory, "*.svo");
        return candidates.Length == 1 ? candidates[0] : null;
    }

    private static bool IsSceneFile(string path)
    {
        return string.Equals(Path.GetExtension(path), ".sbscene", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSvoFile(string path)
    {
        return string.Equals(Path.GetExtension(path), ".svo", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class BusyScope : IDisposable
    {
        private readonly Action _dispose;
        private bool _disposed;

        /// <summary>
        /// 初始化BusyScope 实例，并保存调用方提供的核心数据。
        /// </summary>
        /// <param name="dispose">离开繁忙状态时要执行的恢复动作。</param>
        public BusyScope(Action dispose)
        {
            _dispose = dispose;
        }

        /// <summary>
        /// 释放 Viewer 窗口持有的计时器和其他托管资源。
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _dispose();
            _disposed = true;
        }
    }
}
