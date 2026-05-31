using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SbScene.Core.Images;
using SbScene.Core.Rendering;
using SbScene.Core.Resources;
using SbScene.Core.Semantics;

namespace SbScene.Viewer;

internal sealed record RenderSceneOptions(
    bool ShowHiddenNodes,
    bool ShowNodeMarkers,
    IReadOnlySet<int> HiddenNodeIndexes,
    IReadOnlySet<int> ShownNodeIndexes,
    AnimationInfo? Animation,
    double CurrentFrame);

internal sealed class RenderScene
{
    public required IReadOnlyList<RenderItem> Items { get; init; }

    public required Rect ContentBounds { get; init; }

    public Size SurfaceSize => new(
        Math.Max(320, ContentBounds.Width + SceneRenderSurface.ScenePadding * 2),
        Math.Max(240, ContentBounds.Height + SceneRenderSurface.ScenePadding * 2));
}

internal sealed class RenderItem
{
    public required int NodeIndex { get; init; }

    public required string NodeName { get; init; }

    public required string Group { get; init; }

    public required string Kind { get; init; }

    public required Matrix WorldTransform { get; init; }

    public required Rect LocalRect { get; init; }

    public required Rect WorldBounds { get; init; }

    public BitmapSource? Bitmap { get; init; }

    public required Color PlaceholderColor { get; init; }

    public required double Opacity { get; init; }

    public required bool FlipX { get; init; }

    public required bool FlipY { get; init; }

    public string? ResourceInfo { get; init; }
}

internal sealed class NodeRow
{
    public required int Index { get; init; }

    public required string Name { get; init; }

    public required string Group { get; init; }

    public required string Flags { get; init; }

    public required string Display { get; init; }

    public required string Image { get; init; }

    public required string Transform { get; init; }
}

internal sealed class NodeTreeItem
{
    public required int Index { get; init; }

    public required string Name { get; init; }

    public required string Group { get; init; }

    public required string Flags { get; init; }

    public required string Display { get; init; }

    public required string Image { get; init; }

    public required string Transform { get; init; }

    public required List<NodeTreeItem> Children { get; init; }

    public IEnumerable<NodeTreeItem> EnumerateSelfAndDescendants()
    {
        yield return this;
        foreach (var child in Children)
        {
            foreach (var descendant in child.EnumerateSelfAndDescendants())
            {
                yield return descendant;
            }
        }
    }
}

internal sealed class SvoRenderResources
{
    private readonly Dictionary<(int AtlasIndex, int CropIndex), BitmapSource> _cropCache = [];

    private SvoRenderResources(string path, IReadOnlyDictionary<int, RgbaImage> atlasImages, IReadOnlyList<string> warnings)
    {
        Path = path;
        AtlasImages = atlasImages;
        Warnings = warnings;
    }

    public string Path { get; }

    public IReadOnlyDictionary<int, RgbaImage> AtlasImages { get; }

    public IReadOnlyList<string> Warnings { get; }

    public static SvoRenderResources Load(SbSceneFile scene, string svoPath)
    {
        var warnings = new List<string>();
        var textures = SvoResourceParser.ParseFile(svoPath);
        var textureByName = textures
            .Where(static texture => !string.IsNullOrWhiteSpace(texture.AtlasName))
            .GroupBy(static texture => texture.AtlasName!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.OrdinalIgnoreCase);
        var atlasImages = new Dictionary<int, RgbaImage>();

        foreach (var atlas in scene.Surfboard.Resources.Atlases)
        {
            var texture = textureByName.TryGetValue(atlas.Name, out var namedTexture)
                ? namedTexture
                : atlas.Index >= 0 && atlas.Index < textures.Count
                    ? textures[atlas.Index]
                    : null;

            if (texture is null)
            {
                warnings.Add($"No SVO texture found for atlas '{atlas.Name}'.");
                continue;
            }

            try
            {
                atlasImages[atlas.Index] = DdsDecoder.Decode(texture.DdsBytes);
            }
            catch (Exception ex)
            {
                warnings.Add($"Failed to decode atlas '{atlas.Name}': {ex.Message}");
            }
        }

        return new SvoRenderResources(svoPath, atlasImages, warnings);
    }

    public BitmapSource? ResolveBitmap(
        SbSceneFile scene,
        SbSceneImageCast imageCast,
        out string? resourceInfo)
    {
        return ResolveBitmap(scene, imageCast, imageCast.PrimaryCropReferenceIndex ?? 0, out resourceInfo);
    }

    public BitmapSource? ResolveBitmap(
        SbSceneFile scene,
        SbSceneImageCast imageCast,
        int primaryReferenceIndex,
        out string? resourceInfo)
    {
        resourceInfo = null;
        var reference = SelectCropReference(imageCast, primaryReferenceIndex);
        if (reference is null)
        {
            resourceInfo = "no crop reference";
            return null;
        }

        var atlas = ResolveAtlas(scene, reference);
        if (atlas is null)
        {
            resourceInfo = $"missing atlas {reference.AtlasName ?? reference.TextureIndex.ToString(CultureInfo.InvariantCulture)}";
            return null;
        }

        if (reference.CropIndex < 0 || reference.CropIndex >= atlas.Crops.Count)
        {
            resourceInfo = $"{atlas.Name}[{reference.CropIndex}] out of range";
            return null;
        }

        if (!AtlasImages.TryGetValue(atlas.Index, out var atlasImage))
        {
            resourceInfo = $"atlas '{atlas.Name}' not decoded";
            return null;
        }

        var cacheKey = (atlas.Index, reference.CropIndex);
        if (_cropCache.TryGetValue(cacheKey, out var cached))
        {
            resourceInfo = $"{atlas.Name}[{reference.CropIndex}]";
            return cached;
        }

        var crop = atlas.Crops[reference.CropIndex];
        if (crop.Width <= 0 || crop.Height <= 0)
        {
            resourceInfo = $"{atlas.Name}[{reference.CropIndex}] invalid crop";
            return null;
        }

        var cropped = atlasImage.CropWithTransparentPadding(crop.Left, crop.Top, crop.Width, crop.Height);
        var bitmap = ToBitmapSource(cropped);
        _cropCache[cacheKey] = bitmap;
        resourceInfo = $"{atlas.Name}[{reference.CropIndex}]";
        return bitmap;
    }

    private static SbSceneCropReference? SelectCropReference(SbSceneImageCast imageCast, int primaryReferenceIndex)
    {
        if (imageCast.PrimaryCropReferences.Count > 0)
        {
            var index = primaryReferenceIndex;
            if (index >= 0 && index < imageCast.PrimaryCropReferences.Count)
            {
                return imageCast.PrimaryCropReferences[index];
            }

            return imageCast.PrimaryCropReferences[0];
        }

        return imageCast.CropReferences.Count > 0 ? imageCast.CropReferences[0] : null;
    }

    private static SbSceneTextureAtlas? ResolveAtlas(SbSceneFile scene, SbSceneCropReference reference)
    {
        if (!string.IsNullOrWhiteSpace(reference.AtlasName))
        {
            var named = scene.Surfboard.Resources.Atlases.FirstOrDefault(
                atlas => string.Equals(atlas.Name, reference.AtlasName, StringComparison.OrdinalIgnoreCase));
            if (named is not null)
            {
                return named;
            }
        }

        return reference.TextureIndex >= 0 && reference.TextureIndex < scene.Surfboard.Resources.Atlases.Count
            ? scene.Surfboard.Resources.Atlases[reference.TextureIndex]
            : null;
    }

    private static BitmapSource ToBitmapSource(RgbaImage image)
    {
        var bgra = new byte[image.Pixels.Length];
        for (var i = 0; i < image.Pixels.Length; i += 4)
        {
            bgra[i] = image.Pixels[i + 2];
            bgra[i + 1] = image.Pixels[i + 1];
            bgra[i + 2] = image.Pixels[i];
            bgra[i + 3] = image.Pixels[i + 3];
        }

        var bitmap = BitmapSource.Create(
            image.Width,
            image.Height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            bgra,
            image.Width * 4);
        bitmap.Freeze();
        return bitmap;
    }
}

internal static class SceneRenderBuilder
{
    public static RenderScene Build(SbSceneFile scene, SvoRenderResources? resources, RenderSceneOptions options)
    {
        var nodes = scene.Surfboard.Nodes;
        var frameState = SbSceneAnimationFrameBuilder.Build(scene, options.Animation, options.CurrentFrame);
        var nodeStates = frameState.Nodes;
        var imageStates = frameState.ImageCasts;
        var parentByNode = SbSceneRenderTree.BuildParentMap(nodes);
        var visibleNodes = SbSceneRenderTree.BuildFinalVisibility(
            nodes,
            parentByNode,
            index => nodeStates[index].Display,
            options.ShowHiddenNodes);
        var effectiveOpacities = SbSceneRenderTree.BuildEffectiveOpacity(
            nodes,
            parentByNode,
            index => nodeStates[index].MaterialA / 255.0);
        var worldTransforms = BuildWorldTransforms(nodes, nodeStates, parentByNode);
        var imageCastByNode = scene.Surfboard.Resources.ImageCasts
            .GroupBy(static imageCast => imageCast.CastIndex)
            .ToDictionary(static group => group.Key, static group => group.ToArray());
        var items = new List<RenderItem>();

        foreach (var imageCast in scene.Surfboard.Resources.ImageCasts)
        {
            if (imageCast.CastIndex < 0 || imageCast.CastIndex >= nodes.Count)
            {
                continue;
            }

            var node = nodes[imageCast.CastIndex];
            if (!ShouldRenderNode(node, options, visibleNodes))
            {
                continue;
            }

            var opacity = effectiveOpacities[node.Index];
            if (opacity <= 0)
            {
                continue;
            }

            var localRect = new Rect(-imageCast.PivotX, -imageCast.PivotY, imageCast.Width, imageCast.Height);
            if (localRect.Width <= 0 || localRect.Height <= 0)
            {
                continue;
            }

            var transform = worldTransforms[node.Index];
            var imageState = imageCast.Index >= 0 && imageCast.Index < imageStates.Count
                ? imageStates[imageCast.Index]
                : null;
            string? resourceInfo = null;
            var bitmap = resources is null
                ? null
                : resources.ResolveBitmap(scene, imageCast, imageState?.PrimaryReferenceIndex ?? 0, out resourceInfo);
            resourceInfo ??= resources is null ? "no SVO bound" : null;
            var worldBounds = TransformRect(localRect, transform);
            items.Add(new RenderItem
            {
                NodeIndex = node.Index,
                NodeName = node.Name ?? $"node_{node.Index}",
                Group = node.Group,
                Kind = "image",
                WorldTransform = transform,
                LocalRect = localRect,
                WorldBounds = worldBounds,
                Bitmap = bitmap,
                PlaceholderColor = ColorFromText(node.Group),
                Opacity = opacity,
                FlipX = SbSceneImageCastConventions.HasHorizontalFlip(imageCast),
                FlipY = SbSceneImageCastConventions.HasVerticalFlip(imageCast),
                ResourceInfo = resourceInfo,
            });
        }

        if (options.ShowNodeMarkers)
        {
            foreach (var node in nodes)
            {
                if (!ShouldRenderNode(node, options, visibleNodes))
                {
                    continue;
                }

                if (imageCastByNode.ContainsKey(node.Index))
                {
                    continue;
                }

                var localRect = new Rect(-4, -4, 8, 8);
                var transform = worldTransforms[node.Index];
                items.Add(new RenderItem
                {
                    NodeIndex = node.Index,
                    NodeName = node.Name ?? $"node_{node.Index}",
                    Group = node.Group,
                    Kind = "node",
                    WorldTransform = transform,
                    LocalRect = localRect,
                    WorldBounds = TransformRect(localRect, transform),
                    PlaceholderColor = ColorFromText(node.Group),
                    Opacity = 0.85,
                    FlipX = false,
                    FlipY = false,
                    ResourceInfo = "node marker",
                });
            }
        }

        var bounds = ComputeBounds(items);
        return new RenderScene
        {
            Items = items,
            ContentBounds = bounds,
        };
    }

    public static IReadOnlyList<NodeRow> BuildNodeRows(SbSceneFile scene)
    {
        var imageCastCounts = scene.Surfboard.Resources.ImageCasts
            .GroupBy(static imageCast => imageCast.CastIndex)
            .ToDictionary(static group => group.Key, static group => group.Count());

        return scene.Surfboard.Nodes.Select(node => new NodeRow
        {
            Index = node.Index,
            Name = node.Name ?? string.Empty,
            Group = node.Group,
            Flags = node.Flags is null ? string.Empty : $"0x{node.Flags.Value:X}",
            Display = node.Transform2D?.Display switch
            {
                true => "show",
                false => "hide",
                _ => "?",
            },
            Image = imageCastCounts.TryGetValue(node.Index, out var count) ? count.ToString(CultureInfo.InvariantCulture) : string.Empty,
            Transform = FormatTransform(node.Transform2D),
        }).ToArray();
    }

    public static IReadOnlyList<NodeTreeItem> BuildNodeTree(
        SbSceneFile scene,
        IReadOnlySet<int> hiddenNodeIndexes,
        IReadOnlySet<int> shownNodeIndexes)
    {
        var imageCastCounts = scene.Surfboard.Resources.ImageCasts
            .GroupBy(static imageCast => imageCast.CastIndex)
            .ToDictionary(static group => group.Key, static group => group.Count());
        var nodes = scene.Surfboard.Nodes;
        var rows = nodes.ToDictionary(
            static node => node.Index,
            node => new NodeTreeItem
            {
                Index = node.Index,
                Name = node.Name ?? string.Empty,
                Group = node.Group,
                Flags = node.Flags is null ? string.Empty : $"0x{node.Flags.Value:X}",
                Display = FormatDisplayState(node, hiddenNodeIndexes, shownNodeIndexes),
                Image = imageCastCounts.TryGetValue(node.Index, out var count)
                    ? count.ToString(CultureInfo.InvariantCulture)
                    : string.Empty,
                Transform = FormatTransform(node.Transform2D),
                Children = [],
            });

        var childIndexes = new HashSet<int>();
        foreach (var node in nodes)
        {
            if (!rows.TryGetValue(node.Index, out var parent))
            {
                continue;
            }

            var seen = new HashSet<int>();
            var childIndex = node.ChildIndex;
            while (childIndex is int index && rows.TryGetValue(index, out var child) && seen.Add(index))
            {
                parent.Children.Add(child);
                childIndexes.Add(index);
                childIndex = index >= 0 && index < nodes.Count ? nodes[index].SiblingIndex : null;
            }
        }

        var roots = nodes
            .Where(node => !childIndexes.Contains(node.Index))
            .Select(node => rows[node.Index])
            .ToArray();
        return roots.Length > 0 ? roots : rows.Values.ToArray();
    }

    private static IReadOnlyDictionary<int, Matrix> BuildWorldTransforms(
        IReadOnlyList<NodeInfo> nodes,
        IReadOnlyList<SbSceneNodeAnimationState> nodeStates,
        IReadOnlyDictionary<int, int> parentByNode)
    {
        var memo = new Dictionary<int, Matrix>();
        var visiting = new HashSet<int>();

        Matrix Resolve(int index)
        {
            if (memo.TryGetValue(index, out var cached))
            {
                return cached;
            }

            if (!visiting.Add(index))
            {
                return BuildLocalTransform(nodeStates[index]);
            }

            var local = BuildLocalTransform(nodeStates[index]);
            var world = local;
            if (parentByNode.TryGetValue(index, out var parentIndex) && parentIndex >= 0 && parentIndex < nodes.Count)
            {
                world.Append(Resolve(parentIndex));
            }

            visiting.Remove(index);
            memo[index] = world;
            return world;
        }

        for (var i = 0; i < nodes.Count; i++)
        {
            Resolve(i);
        }

        return memo;
    }

    private static bool ShouldRenderNode(NodeInfo node, RenderSceneOptions options, IReadOnlyList<bool> visibleNodes)
    {
        if (options.HiddenNodeIndexes.Contains(node.Index))
        {
            return false;
        }

        if (options.ShownNodeIndexes.Contains(node.Index))
        {
            return true;
        }

        return node.Index >= 0 && node.Index < visibleNodes.Count
            ? visibleNodes[node.Index]
            : options.ShowHiddenNodes || node.Transform2D?.Display != false;
    }

    private static Matrix BuildLocalTransform(SbSceneNodeAnimationState state)
    {
        var matrix = Matrix.Identity;
        matrix.Scale(state.ScaleX, state.ScaleY);
        matrix.Rotate(SbSceneTransformConventions.ToScreenRotationDegrees(state.RotationDegrees));
        matrix.Translate(state.TranslationX, state.TranslationY);
        return matrix;
    }

    private static Rect ComputeBounds(IReadOnlyList<RenderItem> items)
    {
        if (items.Count == 0)
        {
            return new Rect(-160, -120, 320, 240);
        }

        var bounds = Rect.Empty;
        foreach (var item in items)
        {
            bounds.Union(item.WorldBounds);
        }

        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return new Rect(bounds.X - 160, bounds.Y - 120, 320, 240);
        }

        return bounds;
    }

    private static Rect TransformRect(Rect rect, Matrix matrix)
    {
        var points = new[]
        {
            matrix.Transform(new Point(rect.Left, rect.Top)),
            matrix.Transform(new Point(rect.Right, rect.Top)),
            matrix.Transform(new Point(rect.Right, rect.Bottom)),
            matrix.Transform(new Point(rect.Left, rect.Bottom)),
        };
        var minX = points.Min(static point => point.X);
        var minY = points.Min(static point => point.Y);
        var maxX = points.Max(static point => point.X);
        var maxY = points.Max(static point => point.Y);
        return new Rect(new Point(minX, minY), new Point(maxX, maxY));
    }

    private static Color ColorFromText(string text)
    {
        var hash = 2166136261u;
        foreach (var ch in text)
        {
            hash = (hash ^ ch) * 16777619u;
        }

        return Color.FromRgb(
            (byte)(80 + hash % 130),
            (byte)(80 + (hash >> 8) % 130),
            (byte)(80 + (hash >> 16) % 130));
    }

    private static string FormatTransform(Transform2DInfo? transform)
    {
        if (transform is null)
        {
            return string.Empty;
        }

        var tx = transform.Translation?.X.ToString("0.##", CultureInfo.InvariantCulture) ?? "?";
        var ty = transform.Translation?.Y.ToString("0.##", CultureInfo.InvariantCulture) ?? "?";
        var sx = transform.Scale?.X.ToString("0.##", CultureInfo.InvariantCulture) ?? "?";
        var sy = transform.Scale?.Y.ToString("0.##", CultureInfo.InvariantCulture) ?? "?";
        var r = (transform.RotationZDegreesCandidate ?? transform.RotationZ)?.ToString("0.##", CultureInfo.InvariantCulture) ?? "?";
        return $"T({tx},{ty}) R({r}) S({sx},{sy})";
    }

    private static string FormatDisplayState(
        NodeInfo node,
        IReadOnlySet<int> hiddenNodeIndexes,
        IReadOnlySet<int> shownNodeIndexes)
    {
        if (hiddenNodeIndexes.Contains(node.Index))
        {
            return "hide*";
        }

        if (shownNodeIndexes.Contains(node.Index))
        {
            return "show*";
        }

        return node.Transform2D?.Display switch
        {
            true => "show",
            false => "hide",
            _ => "?",
        };
    }
}
