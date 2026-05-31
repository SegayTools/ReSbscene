using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SbScene.Viewer;

internal sealed class SceneRenderSurface : FrameworkElement
{
    public const double ScenePadding = 80;

    private static readonly Brush SurfaceBrush = new SolidColorBrush(Color.FromRgb(238, 242, 246));
    private static readonly Pen GridPen = new(new SolidColorBrush(Color.FromRgb(216, 224, 232)), 1);
    private static readonly Pen AxisXPen = new(new SolidColorBrush(Color.FromRgb(191, 116, 105)), 1);
    private static readonly Pen AxisYPen = new(new SolidColorBrush(Color.FromRgb(92, 151, 118)), 1);
    private static readonly Pen PlaceholderPen = new(new SolidColorBrush(Color.FromRgb(92, 103, 117)), 1);
    private static readonly Pen HighlightPen = new(new SolidColorBrush(Color.FromRgb(219, 142, 35)), 2);
    private static readonly Pen ChildHighlightPen = new(new SolidColorBrush(Color.FromRgb(31, 137, 152)), 1.5);
    private static readonly Typeface LabelTypeface = new("Segoe UI");

    private RenderScene? _scene;
    private HashSet<int> _highlightedNodeIndexes = [];
    private int? _primaryHighlightedNodeIndex;
    private double _zoom = 1;

    public RenderScene? Scene
    {
        get => _scene;
        set
        {
            var oldSize = _scene?.SurfaceSize;
            _scene = value;
            var newSize = _scene?.SurfaceSize;
            if (!AreClose(oldSize, newSize))
            {
                InvalidateMeasure();
            }

            InvalidateVisual();
        }
    }

    public int? HighlightedNodeIndex
    {
        get => _highlightedNodeIndexes.Count == 1 ? _highlightedNodeIndexes.First() : null;
        set
        {
            _primaryHighlightedNodeIndex = value;
            _highlightedNodeIndexes = value is null ? [] : [value.Value];
            InvalidateVisual();
        }
    }

    public int? PrimaryHighlightedNodeIndex
    {
        get => _primaryHighlightedNodeIndex;
        set
        {
            _primaryHighlightedNodeIndex = value;
            InvalidateVisual();
        }
    }

    public IReadOnlySet<int> HighlightedNodeIndexes
    {
        get => _highlightedNodeIndexes;
        set
        {
            _highlightedNodeIndexes = value.ToHashSet();
            InvalidateVisual();
        }
    }

    public double Zoom
    {
        get => _zoom;
        set
        {
            _zoom = Math.Clamp(value, 0.1, 8);
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    public int? HitTestNode(Point surfacePoint)
    {
        if (Scene is null)
        {
            return null;
        }

        var worldPoint = SurfaceToWorld(surfacePoint, Scene);
        foreach (var item in Scene.Items.Reverse())
        {
            var inverse = item.WorldTransform;
            if (!inverse.HasInverse)
            {
                continue;
            }

            inverse.Invert();
            if (item.LocalRect.Contains(inverse.Transform(worldPoint)))
            {
                return item.NodeIndex;
            }
        }

        return null;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = Scene?.SurfaceSize ?? new Size(960, 640);
        return new Size(size.Width * Zoom, size.Height * Zoom);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        return finalSize;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var baseSize = Scene?.SurfaceSize ?? new Size(Math.Max(ActualWidth / Zoom, 960), Math.Max(ActualHeight / Zoom, 640));
        drawingContext.DrawRectangle(SurfaceBrush, null, new Rect(RenderSize));
        drawingContext.PushTransform(new ScaleTransform(Zoom, Zoom));
        DrawGrid(drawingContext, baseSize);

        if (Scene is not null)
        {
            drawingContext.PushTransform(CreateSceneOffset(Scene));
            DrawAxes(drawingContext);
            foreach (var item in Scene.Items)
            {
                DrawItem(drawingContext, item);
            }

            DrawSelectionBounds(drawingContext, Scene);

            drawingContext.Pop();
        }

        drawingContext.Pop();
    }

    private static void DrawGrid(DrawingContext drawingContext, Size size)
    {
        const double spacing = 64;
        for (double x = 0; x <= size.Width; x += spacing)
        {
            drawingContext.DrawLine(GridPen, new Point(x, 0), new Point(x, size.Height));
        }

        for (double y = 0; y <= size.Height; y += spacing)
        {
            drawingContext.DrawLine(GridPen, new Point(0, y), new Point(size.Width, y));
        }
    }

    private static void DrawAxes(DrawingContext drawingContext)
    {
        drawingContext.DrawLine(AxisXPen, new Point(-10000, 0), new Point(10000, 0));
        drawingContext.DrawLine(AxisYPen, new Point(0, -10000), new Point(0, 10000));
    }

    private void DrawItem(DrawingContext drawingContext, RenderItem item)
    {
        drawingContext.PushTransform(new MatrixTransform(item.WorldTransform));
        drawingContext.PushOpacity(item.Opacity);

        if (item.Kind == "node")
        {
            var markerBrush = new SolidColorBrush(item.PlaceholderColor);
            markerBrush.Freeze();
            drawingContext.DrawEllipse(markerBrush, PlaceholderPen, new Point(0, 0), 4, 4);
        }
        else if (item.Bitmap is BitmapSource bitmap)
        {
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
            if (item.FlipX || item.FlipY)
            {
                drawingContext.PushTransform(new ScaleTransform(
                    item.FlipX ? -1 : 1,
                    item.FlipY ? -1 : 1,
                    item.LocalRect.Left + item.LocalRect.Width / 2,
                    item.LocalRect.Top + item.LocalRect.Height / 2));
            }

            drawingContext.DrawImage(bitmap, item.LocalRect);
            if (item.FlipX || item.FlipY)
            {
                drawingContext.Pop();
            }
        }
        else
        {
            var fill = new SolidColorBrush(Color.FromArgb(72, item.PlaceholderColor.R, item.PlaceholderColor.G, item.PlaceholderColor.B));
            fill.Freeze();
            drawingContext.DrawRectangle(fill, PlaceholderPen, item.LocalRect);
            DrawPlaceholderLabel(drawingContext, item);
        }

        drawingContext.Pop();

        drawingContext.Pop();
    }

    private void DrawSelectionBounds(DrawingContext drawingContext, RenderScene scene)
    {
        if (_highlightedNodeIndexes.Count == 0)
        {
            return;
        }

        var bounds = Rect.Empty;
        var boundsByNode = new Dictionary<int, Rect>();
        foreach (var item in scene.Items)
        {
            if (_highlightedNodeIndexes.Contains(item.NodeIndex))
            {
                bounds.Union(item.WorldBounds);
                if (boundsByNode.TryGetValue(item.NodeIndex, out var nodeBounds))
                {
                    nodeBounds.Union(item.WorldBounds);
                    boundsByNode[item.NodeIndex] = nodeBounds;
                }
                else
                {
                    boundsByNode[item.NodeIndex] = item.WorldBounds;
                }
            }
        }

        foreach (var (nodeIndex, nodeBounds) in boundsByNode)
        {
            if (nodeIndex != _primaryHighlightedNodeIndex && nodeBounds.Width > 0 && nodeBounds.Height > 0)
            {
                drawingContext.DrawRectangle(null, ChildHighlightPen, nodeBounds);
            }
        }

        if (!bounds.IsEmpty && bounds.Width > 0 && bounds.Height > 0)
        {
            drawingContext.DrawRectangle(null, HighlightPen, bounds);
        }
    }

    private static void DrawPlaceholderLabel(DrawingContext drawingContext, RenderItem item)
    {
        var text = string.IsNullOrWhiteSpace(item.ResourceInfo) ? item.NodeName : $"{item.NodeName}\n{item.ResourceInfo}";
        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            LabelTypeface,
            11,
            Brushes.Black,
            1.0)
        {
            MaxTextWidth = Math.Max(24, item.LocalRect.Width - 8),
            MaxTextHeight = Math.Max(16, item.LocalRect.Height - 8),
            Trimming = TextTrimming.CharacterEllipsis,
        };
        drawingContext.DrawText(formatted, new Point(item.LocalRect.Left + 4, item.LocalRect.Top + 4));
    }

    private static TranslateTransform CreateSceneOffset(RenderScene scene)
    {
        return new TranslateTransform(ScenePadding - scene.ContentBounds.Left, ScenePadding - scene.ContentBounds.Top);
    }

    private Point SurfaceToWorld(Point surfacePoint, RenderScene scene)
    {
        var basePoint = new Point(surfacePoint.X / Zoom, surfacePoint.Y / Zoom);
        return new Point(
            basePoint.X - (ScenePadding - scene.ContentBounds.Left),
            basePoint.Y - (ScenePadding - scene.ContentBounds.Top));
    }

    private static bool AreClose(Size? left, Size? right)
    {
        return left is Size leftSize
            && right is Size rightSize
            && Math.Abs(leftSize.Width - rightSize.Width) < 0.01
            && Math.Abs(leftSize.Height - rightSize.Height) < 0.01;
    }
}
