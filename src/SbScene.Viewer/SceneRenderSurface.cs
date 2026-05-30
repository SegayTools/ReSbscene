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
    private static readonly Typeface LabelTypeface = new("Segoe UI");

    private RenderScene? _scene;
    private int? _highlightedNodeIndex;
    private double _zoom = 1;

    public RenderScene? Scene
    {
        get => _scene;
        set
        {
            _scene = value;
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    public int? HighlightedNodeIndex
    {
        get => _highlightedNodeIndex;
        set
        {
            _highlightedNodeIndex = value;
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
            drawingContext.DrawImage(bitmap, item.LocalRect);
        }
        else
        {
            var fill = new SolidColorBrush(Color.FromArgb(72, item.PlaceholderColor.R, item.PlaceholderColor.G, item.PlaceholderColor.B));
            fill.Freeze();
            drawingContext.DrawRectangle(fill, PlaceholderPen, item.LocalRect);
            DrawPlaceholderLabel(drawingContext, item);
        }

        drawingContext.Pop();

        if (HighlightedNodeIndex == item.NodeIndex)
        {
            drawingContext.DrawRectangle(null, HighlightPen, item.LocalRect);
        }

        drawingContext.Pop();
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
}
