using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using SbScene.Core.Semantics;

namespace SbScene.Viewer;

public partial class MainWindow : Window
{
    private SbSceneFile? _scene;
    private SvoRenderResources? _resources;
    private RenderScene? _renderScene;
    private string? _scenePath;
    private string? _svoPath;
    private string? _pendingSvoPath;
    private bool _controlsReady;
    private readonly HashSet<int> _hiddenNodeIndexes = [];
    private readonly HashSet<int> _shownNodeIndexes = [];

    public MainWindow()
    {
        InitializeComponent();
        _controlsReady = true;
        SetZoom(1);
        SetStatus("就绪。");
        Loaded += MainWindow_Loaded;
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

    private void Fit_Click(object sender, RoutedEventArgs e)
    {
        FitSceneToViewport();
    }

    private void RenderOption_Changed(object sender, RoutedEventArgs e)
    {
        if (!_controlsReady)
        {
            return;
        }

        RebuildRender();
    }

    private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_controlsReady || RenderSurface is null)
        {
            return;
        }

        SetZoom(e.NewValue, updateSlider: false);
    }

    private void NodeTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        RenderSurface.HighlightedNodeIndex = NodeTree.SelectedItem is NodeTreeItem row ? row.Index : null;
    }

    private void NodeTreeItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var item = FindAncestor<TreeViewItem>((DependencyObject)e.OriginalSource);
        if (item is null)
        {
            return;
        }

        item.Focus();
        item.IsSelected = true;
        e.Handled = true;
    }

    private void ShowSubtree_Click(object sender, RoutedEventArgs e)
    {
        SetSelectedSubtreeVisibility(true);
    }

    private void HideSubtree_Click(object sender, RoutedEventArgs e)
    {
        SetSelectedSubtreeVisibility(false);
    }

    private void ResetSubtreeVisibility_Click(object sender, RoutedEventArgs e)
    {
        SetSelectedSubtreeVisibility(null);
    }

    private void RenderSurface_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var nodeIndex = RenderSurface.HitTestNode(e.GetPosition(RenderSurface));
        if (nodeIndex is null)
        {
            return;
        }

        SelectNode(nodeIndex.Value);
        e.Handled = true;
    }

    private void SceneScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
        {
            return;
        }

        var oldZoom = RenderSurface.Zoom;
        var mouseOnSurface = e.GetPosition(RenderSurface);
        var mouseOnViewer = e.GetPosition(SceneScrollViewer);
        var zoomFactor = e.Delta > 0 ? 1.12 : 1 / 1.12;
        SetZoom(oldZoom * zoomFactor);
        SceneScrollViewer.UpdateLayout();

        var newZoom = RenderSurface.Zoom;
        SceneScrollViewer.ScrollToHorizontalOffset(mouseOnSurface.X / oldZoom * newZoom - mouseOnViewer.X);
        SceneScrollViewer.ScrollToVerticalOffset(mouseOnSurface.Y / oldZoom * newZoom - mouseOnViewer.Y);
        e.Handled = true;
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
            _scenePath = path;
            _resources = null;
            _svoPath = null;

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

    private void RebuildRender()
    {
        if (!_controlsReady || RenderSurface is null || EmptyOverlay is null)
        {
            return;
        }

        if (_scene is null)
        {
            RenderSurface.Scene = null;
            EmptyOverlay.Visibility = Visibility.Visible;
            return;
        }

        var options = new RenderSceneOptions(
            ShowHiddenCheckBox.IsChecked == true,
            ShowMarkersCheckBox.IsChecked == true,
            _hiddenNodeIndexes,
            _shownNodeIndexes);
        _renderScene = SceneRenderBuilder.Build(_scene, _resources, options);
        RenderSurface.Scene = _renderScene;
        EmptyOverlay.Visibility = Visibility.Collapsed;
        UpdateStatusFromRender();
    }

    private void UpdateSceneSummary()
    {
        if (_scene is null)
        {
            ClearScene();
            return;
        }

        var scene = _scene;
        SceneTitleTextBlock.Text = Path.GetFileName(scene.SourcePath);
        SceneSummaryTextBlock.Text = string.Format(
            CultureInfo.InvariantCulture,
            "{0:N0} nodes, {1:N0} image casts, {2:N0} atlases, {3:N0} animations",
            scene.Surfboard.Nodes.Count,
            scene.Surfboard.Resources.ImageCasts.Count,
            scene.Surfboard.Resources.Atlases.Count,
            scene.Surfboard.Animations.Count);

        SvoSummaryTextBlock.Text = _resources is null
            ? "未绑定 SVO；以占位色绘制 CIMG。"
            : string.Format(
                CultureInfo.InvariantCulture,
                "SVO: {0} ({1:N0}/{2:N0} atlases decoded)",
                Path.GetFileName(_resources.Path),
                _resources.AtlasImages.Count,
                scene.Surfboard.Resources.Atlases.Count);
    }

    private void UpdateStatusFromRender()
    {
        if (_scene is null || _renderScene is null)
        {
            SetStatus("就绪。");
            return;
        }

        var images = _renderScene.Items.Count(static item => item.Kind == "image");
        var bitmaps = _renderScene.Items.Count(static item => item.Bitmap is not null);
        var missing = images - bitmaps;
        var warnings = BuildWarningSummary();
        var status = string.Format(
            CultureInfo.InvariantCulture,
            "绘制 {0:N0} items，bitmap {1:N0}/{2:N0}，占位 {3:N0}。{4}",
            _renderScene.Items.Count,
            bitmaps,
            images,
            missing,
            warnings);
        SetStatus(status.TrimEnd());
    }

    private string BuildWarningSummary()
    {
        var warningCount = (_scene?.Summary.Warnings.Count ?? 0) + (_resources?.Warnings.Count ?? 0);
        if (warningCount == 0)
        {
            return string.Empty;
        }

        var firstWarning = _scene?.Summary.Warnings.FirstOrDefault() ?? _resources?.Warnings.FirstOrDefault();
        return $"警告 {warningCount}: {firstWarning}";
    }

    private void ClearScene()
    {
        _scene = null;
        _resources = null;
        _renderScene = null;
        _scenePath = null;
        _hiddenNodeIndexes.Clear();
        _shownNodeIndexes.Clear();
        NodeTree.ItemsSource = null;
        RenderSurface.Scene = null;
        RenderSurface.HighlightedNodeIndex = null;
        SceneTitleTextBlock.Text = "未加载 scene";
        SceneSummaryTextBlock.Text = string.Empty;
        SvoSummaryTextBlock.Text = string.Empty;
        EmptyOverlay.Visibility = Visibility.Visible;
    }

    private void FitSceneToViewport()
    {
        if (RenderSurface.Scene is null || SceneScrollViewer.ViewportWidth <= 0 || SceneScrollViewer.ViewportHeight <= 0)
        {
            return;
        }

        var size = RenderSurface.Scene.SurfaceSize;
        var zoom = Math.Min(SceneScrollViewer.ViewportWidth / size.Width, SceneScrollViewer.ViewportHeight / size.Height);
        SetZoom(Math.Clamp(zoom, 0.1, 4));
        SceneScrollViewer.ScrollToHorizontalOffset(0);
        SceneScrollViewer.ScrollToVerticalOffset(0);
    }

    private void SelectNode(int nodeIndex)
    {
        RenderSurface.HighlightedNodeIndex = nodeIndex;
        if (!_controlsReady || NodeTree.Items.Count == 0)
        {
            return;
        }

        NodeTree.UpdateLayout();
        if (TrySelectTreeItem(NodeTree, nodeIndex, out var item))
        {
            item.IsSelected = true;
            item.Focus();
            item.BringIntoView();
        }
    }

    private void SetSelectedSubtreeVisibility(bool? visible)
    {
        if (NodeTree.SelectedItem is not NodeTreeItem item)
        {
            SetStatus("请先选择一个节点。");
            return;
        }

        var indexes = item.EnumerateSelfAndDescendants()
            .Select(static node => node.Index)
            .ToArray();
        foreach (var index in indexes)
        {
            if (visible == true)
            {
                _hiddenNodeIndexes.Remove(index);
                _shownNodeIndexes.Add(index);
            }
            else if (visible == false)
            {
                _shownNodeIndexes.Remove(index);
                _hiddenNodeIndexes.Add(index);
            }
            else
            {
                _hiddenNodeIndexes.Remove(index);
                _shownNodeIndexes.Remove(index);
            }
        }

        RefreshNodeTree();
        RebuildRender();
        RenderSurface.HighlightedNodeIndex = item.Index;
        var action = visible switch
        {
            true => "显示",
            false => "隐藏",
            _ => "恢复",
        };
        SetStatus($"{action} {indexes.Length:N0} 个节点。");
    }

    private void RefreshNodeTree()
    {
        if (_scene is null || !_controlsReady || NodeTree is null)
        {
            return;
        }

        NodeTree.ItemsSource = SceneRenderBuilder.BuildNodeTree(_scene, _hiddenNodeIndexes, _shownNodeIndexes);
    }

    private void SetZoom(double value, bool updateSlider = true)
    {
        var minimum = ZoomSlider is null ? 0.1 : ZoomSlider.Minimum;
        var maximum = ZoomSlider is null ? 4 : ZoomSlider.Maximum;
        var zoom = Math.Clamp(value, minimum, maximum);
        if (RenderSurface is not null)
        {
            RenderSurface.Zoom = zoom;
        }

        if (updateSlider && ZoomSlider is not null && Math.Abs(ZoomSlider.Value - zoom) > 0.0001)
        {
            ZoomSlider.Value = zoom;
        }

        if (ZoomTextBlock is not null)
        {
            ZoomTextBlock.Text = zoom.ToString("P0", CultureInfo.InvariantCulture);
        }
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

    private static T? FindAncestor<T>(DependencyObject? source)
        where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T target)
            {
                return target;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    private static bool TrySelectTreeItem(ItemsControl parent, int nodeIndex, out TreeViewItem item)
    {
        parent.ApplyTemplate();
        parent.UpdateLayout();

        foreach (var sourceItem in parent.Items)
        {
            var container = parent.ItemContainerGenerator.ContainerFromItem(sourceItem) as TreeViewItem;
            if (container is null)
            {
                continue;
            }

            if (sourceItem is NodeTreeItem node && node.Index == nodeIndex)
            {
                item = container;
                return true;
            }

            container.IsExpanded = true;
            container.ApplyTemplate();
            container.UpdateLayout();
            if (TrySelectTreeItem(container, nodeIndex, out item))
            {
                return true;
            }
        }

        item = null!;
        return false;
    }

    private sealed class BusyScope : IDisposable
    {
        private readonly Action _dispose;
        private bool _disposed;

        public BusyScope(Action dispose)
        {
            _dispose = dispose;
        }

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
