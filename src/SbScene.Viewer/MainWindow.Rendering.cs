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
/// 表示 Viewer 主窗口的渲染交互部分，负责画布重建、缩放和预览刷新。
/// </summary>
public partial class MainWindow : Window
{
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

    private void SelectionPreviewBounds_Changed(object sender, RoutedEventArgs e)
    {
        if (!_controlsReady || SelectionRenderSurface is null || SelectionPreviewBoundsCheckBox is null)
        {
            return;
        }

        SelectionRenderSurface.ShowSelectionBounds = SelectionPreviewBoundsCheckBox.IsChecked == true;
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

    private void SceneScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateRenderSurfaceMinimumWidth();
    }

    private void RebuildRender(bool fitSelectionPreview = true, bool updateDetails = true)
    {
        if (!_controlsReady
            || RenderSurface is null
            || SelectionRenderSurface is null
            || EmptyOverlay is null
            || SelectionEmptyOverlay is null)
        {
            return;
        }

        if (_scene is null)
        {
            RenderSurface.Scene = null;
            SelectionRenderSurface.Scene = null;
            SetSelectionPreviewEmpty("未选择节点。", visible: true);
            EmptyOverlay.Visibility = Visibility.Visible;
            return;
        }

        var options = new RenderSceneOptions(
            ShowHiddenCheckBox.IsChecked == true,
            ShowMarkersCheckBox.IsChecked == true,
            _hiddenNodeIndexes,
            _shownNodeIndexes,
            BuildActiveAnimationStates());
        _renderBuildCache ??= new SceneRenderBuildCache(_scene);
        _renderScene = SceneRenderBuilder.Build(_renderBuildCache, _resources, options);
        UpdateRenderSurfaceScene(fitSelectionPreview: fitSelectionPreview && _selectedNodeIndexes is { Count: > 0 });
        if (updateDetails)
        {
            UpdateSelectedNodeInfo();
        }

        EmptyOverlay.Visibility = Visibility.Collapsed;
        if (updateDetails)
        {
            UpdateStatusFromRender();
        }
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

    private void UpdateSelectedNodeInfo()
    {
        if (!_controlsReady || SelectedNodeInfoTextBlock is null)
        {
            return;
        }

        if (_scene is null)
        {
            SelectedNodeInfoTextBlock.Text = "未加载 scene。";
            return;
        }

        if (_selectedNodeIndex is not int nodeIndex
            || nodeIndex < 0
            || nodeIndex >= _scene.Surfboard.Nodes.Count)
        {
            SelectedNodeInfoTextBlock.Text = "未选择节点。";
            return;
        }

        var node = _scene.Surfboard.Nodes[nodeIndex];
        var selectedIndexes = _selectedNodeIndexes ?? [nodeIndex];
        var imageCastCount = _scene.Surfboard.Resources.ImageCasts.Count(imageCast => selectedIndexes.Contains(imageCast.CastIndex));
        var itemCount = _renderScene?.Items.Count(item => selectedIndexes.Contains(item.NodeIndex)) ?? 0;
        var builder = new StringBuilder();
        builder.AppendLine($"Index:      {node.Index}");
        builder.AppendLine($"Name:       {node.Name ?? "(unnamed)"}");
        builder.AppendLine($"Group:      {node.Group}");
        builder.AppendLine($"Display:    {FormatDisplayState(node)}");
        builder.AppendLine($"Flags:      {(node.Flags is null ? "-" : $"0x{node.Flags.Value:X}")}");
        builder.AppendLine($"Subtree:    {selectedIndexes.Count:N0} nodes");
        builder.AppendLine($"Images:     {imageCastCount:N0} image casts, {itemCount:N0} render items");
        builder.AppendLine($"Transform:  {FormatTransform(node.Transform2D)}");
        if (!string.IsNullOrWhiteSpace(node.Comment))
        {
            builder.AppendLine($"Comment:    {node.Comment}");
        }

        builder.AppendLine($"Path:       {node.Path}");
        SelectedNodeInfoTextBlock.Text = builder.ToString();
    }

    private string FormatDisplayState(NodeInfo node)
    {
        if (_hiddenNodeIndexes.Contains(node.Index))
        {
            return "hide* (viewer override)";
        }

        if (_shownNodeIndexes.Contains(node.Index))
        {
            return "show* (viewer override)";
        }

        return node.Transform2D?.Display switch
        {
            true => "show",
            false => "hide",
            _ => "?",
        };
    }

    private static string FormatTransform(Transform2DInfo? transform)
    {
        if (transform is null)
        {
            return "-";
        }

        var tx = transform.Translation?.X.ToString("0.##", CultureInfo.InvariantCulture) ?? "?";
        var ty = transform.Translation?.Y.ToString("0.##", CultureInfo.InvariantCulture) ?? "?";
        var sx = transform.Scale?.X.ToString("0.##", CultureInfo.InvariantCulture) ?? "?";
        var sy = transform.Scale?.Y.ToString("0.##", CultureInfo.InvariantCulture) ?? "?";
        var rotation = (transform.RotationZDegreesCandidate ?? transform.RotationZ)?.ToString("0.##", CultureInfo.InvariantCulture) ?? "?";
        return $"T({tx},{ty}) R({rotation}) S({sx},{sy})";
    }

    private string BuildWarningSummary()
    {
        var warningCount = (_scene?.Summary.Warnings.Count ?? 0) + (_resources?.Warnings.Count ?? 0) + (_renderScene?.Warnings.Count ?? 0);
        if (warningCount == 0)
        {
            return string.Empty;
        }

        var firstWarning = _scene?.Summary.Warnings.FirstOrDefault()
            ?? _resources?.Warnings.FirstOrDefault()
            ?? _renderScene?.Warnings.FirstOrDefault();
        return $"警告 {warningCount}: {firstWarning}";
    }

    private void FitSceneToViewport()
    {
        if (RenderSurface.Scene is null || SceneScrollViewer.ViewportWidth <= 0 || SceneScrollViewer.ViewportHeight <= 0)
        {
            return;
        }

        RenderSurface.CaptureCurrentContentBounds();
        var size = RenderSurface.SurfaceSize;
        var zoom = Math.Min(SceneScrollViewer.ViewportWidth / size.Width, SceneScrollViewer.ViewportHeight / size.Height);
        SetZoom(Math.Clamp(zoom, 0.1, 4));
        SceneScrollViewer.ScrollToHorizontalOffset(0);
        SceneScrollViewer.ScrollToVerticalOffset(0);
    }

    private void UpdateRenderSurfaceScene(bool fitSelectionPreview)
    {
        if (!_controlsReady || RenderSurface is null || SelectionRenderSurface is null)
        {
            return;
        }

        UpdateRenderSurfaceMinimumWidth();

        if (_renderScene is null)
        {
            RenderSurface.Scene = null;
            RenderSurface.PrimaryHighlightedNodeIndex = null;
            RenderSurface.HighlightedNodeIndexes = new HashSet<int>();
            SelectionRenderSurface.Scene = null;
            SelectionRenderSurface.PrimaryHighlightedNodeIndex = null;
            SelectionRenderSurface.HighlightedNodeIndexes = new HashSet<int>();
            SetSelectionPreviewEmpty("未选择节点。", visible: true);
            return;
        }

        RenderSurface.Scene = _renderScene;
        RenderSurface.PrimaryHighlightedNodeIndex = _selectedNodeIndex;
        RenderSurface.HighlightedNodeIndexes = _selectedNodeIndexes ?? new HashSet<int>();

        if (_selectedNodeIndexes is not { Count: > 0 } indexes)
        {
            SelectionRenderSurface.Scene = null;
            SelectionRenderSurface.PrimaryHighlightedNodeIndex = null;
            SelectionRenderSurface.HighlightedNodeIndexes = new HashSet<int>();
            SetSelectionPreviewEmpty("未选择节点。", visible: true);
            return;
        }

        var selectionScene = FilterRenderScene(_renderScene, indexes);
        SelectionRenderSurface.Scene = selectionScene;
        SelectionRenderSurface.PrimaryHighlightedNodeIndex = _selectedNodeIndex;
        SelectionRenderSurface.HighlightedNodeIndexes = indexes;
        SetSelectionPreviewEmpty(
            selectionScene.Items.Count == 0 ? "选中子树没有可绘制项。" : null,
            visible: selectionScene.Items.Count == 0);

        if (fitSelectionPreview)
        {
            FitSelectionPreviewToViewport();
        }
    }

    private void UpdateRenderSurfaceMinimumWidth()
    {
        if (!_controlsReady || RenderSurface is null || SceneScrollViewer is null)
        {
            return;
        }

        var width = SceneScrollViewer.ViewportWidth;
        if (!double.IsFinite(width) || width <= 0)
        {
            width = SceneScrollViewer.ActualWidth;
        }

        RenderSurface.MinWidth = Math.Max(0, width);
    }

    private void FitSelectionPreviewToViewport()
    {
        if (SelectionRenderSurface.Scene is null
            || SelectionScrollViewer.ViewportWidth <= 0
            || SelectionScrollViewer.ViewportHeight <= 0)
        {
            return;
        }

        SelectionScrollViewer.UpdateLayout();
        SelectionRenderSurface.CaptureCurrentContentBounds();
        var size = SelectionRenderSurface.SurfaceSize;
        var zoom = Math.Min(
            SelectionScrollViewer.ViewportWidth / size.Width,
            SelectionScrollViewer.ViewportHeight / size.Height);
        SelectionRenderSurface.Zoom = Math.Clamp(zoom, 0.1, 4);
        SelectionScrollViewer.ScrollToHorizontalOffset(0);
        SelectionScrollViewer.ScrollToVerticalOffset(0);
    }

    private void SelectionScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        FitSelectionPreviewToViewport();
    }

    private void SetSelectionPreviewEmpty(string? text, bool visible)
    {
        if (SelectionEmptyTextBlock is not null && text is not null)
        {
            SelectionEmptyTextBlock.Text = text;
        }

        if (SelectionEmptyOverlay is not null)
        {
            SelectionEmptyOverlay.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private static RenderScene FilterRenderScene(RenderScene source, IReadOnlySet<int> nodeIndexes)
    {
        var items = source.Items
            .Where(item => nodeIndexes.Contains(item.NodeIndex))
            .ToArray();
        var bounds = Rect.Empty;
        foreach (var item in items)
        {
            bounds.Union(item.WorldBounds);
        }

        if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
        {
            bounds = new Rect(-160, -120, 320, 240);
        }

        return new RenderScene
        {
            Items = items,
            ContentBounds = bounds,
            Warnings = source.Warnings,
        };
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
}
