using System.Diagnostics;
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
    IReadOnlyList<RenderSceneAnimationState> Animations);

internal sealed record RenderSceneAnimationState(AnimationInfo Animation, double Frame);

internal sealed class RenderScene
{
    public required IReadOnlyList<RenderItem> Items { get; init; }

    public required Rect ContentBounds { get; init; }

    public required IReadOnlyList<string> Warnings { get; init; }

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

internal sealed class SceneRenderBuildCache
{
    public SceneRenderBuildCache(SbSceneFile scene)
    {
        Scene = scene;
        Nodes = scene.Surfboard.Nodes;
        ParentByNode = SbSceneRenderTree.BuildParentMap(Nodes);
        ParentIndexes = BuildParentIndexes(Nodes, ParentByNode);
        ImageCastNodeIndexes = scene.Surfboard.Resources.ImageCasts
            .Select(static imageCast => imageCast.CastIndex)
            .Where(static index => index >= 0)
            .ToHashSet();
        var warnings = new List<string>();
        ImageEntries = BuildImageEntries(scene, warnings).ToArray();
        NodeMarkerEntries = BuildNodeMarkerEntries(scene, ImageCastNodeIndexes).ToArray();
        Warnings = warnings;
    }

    public SbSceneFile Scene { get; }

    public IReadOnlyList<NodeInfo> Nodes { get; }

    public IReadOnlyDictionary<int, int> ParentByNode { get; }

    public IReadOnlyList<int> ParentIndexes { get; }

    public IReadOnlySet<int> ImageCastNodeIndexes { get; }

    public IReadOnlyList<ImageRenderEntry> ImageEntries { get; }

    public IReadOnlyList<NodeMarkerRenderEntry> NodeMarkerEntries { get; }

    public IReadOnlyList<string> Warnings { get; }

    private static IReadOnlyList<int> BuildParentIndexes(
        IReadOnlyList<NodeInfo> nodes,
        IReadOnlyDictionary<int, int> parentByNode)
    {
        var indexes = new int[nodes.Count];
        Array.Fill(indexes, -1);
        for (var i = 0; i < nodes.Count; i++)
        {
            indexes[i] = parentByNode.TryGetValue(i, out var parentIndex) ? parentIndex : -1;
        }

        return indexes;
    }

    private static IEnumerable<ImageRenderEntry> BuildImageEntries(SbSceneFile scene, List<string> warnings)
    {
        var nodes = scene.Surfboard.Nodes;
        foreach (var imageCast in scene.Surfboard.Resources.ImageCasts)
        {
            if (imageCast.CastIndex < 0 || imageCast.CastIndex >= nodes.Count)
            {
                continue;
            }

            var geometry = SbSceneImageCastConventions.ResolveAnimatedGeometry(imageCast, imageCast.Width, imageCast.Height);
            var localRect = new Rect(-geometry.PivotX, -geometry.PivotY, geometry.Width, geometry.Height);
            if (localRect.Width <= 0 || localRect.Height <= 0)
            {
                continue;
            }

            var node = nodes[imageCast.CastIndex];
            var nodeName = node.Name ?? $"node_{node.Index}";
            if (!HasCropReference(imageCast))
            {
                AddWarning(
                    warnings,
                    $"Skipped CIMG node '{nodeName}' (node {node.Index}, cast {imageCast.Index}) because it has no crop references.");
                continue;
            }

            yield return new ImageRenderEntry
            {
                ImageCast = imageCast,
                Node = node,
                NodeName = nodeName,
                Group = node.Group,
                LocalRect = localRect,
                PlaceholderColor = ColorFromText(node.Group),
                FlipX = SbSceneImageCastConventions.HasHorizontalFlip(imageCast),
                FlipY = SbSceneImageCastConventions.HasVerticalFlip(imageCast),
            };
        }
    }

    private static bool HasCropReference(SbSceneImageCast imageCast)
    {
        return imageCast.CropReferences.Count > 0
            || imageCast.PrimaryCropReferences.Count > 0
            || imageCast.SecondaryCropReferences.Count > 0;
    }

    private static void AddWarning(List<string> warnings, string warning)
    {
        warnings.Add(warning);
        Debug.WriteLine($"SbScene.Viewer: {warning}");
    }

    private static IEnumerable<NodeMarkerRenderEntry> BuildNodeMarkerEntries(
        SbSceneFile scene,
        IReadOnlySet<int> imageCastNodeIndexes)
    {
        var localRect = new Rect(-4, -4, 8, 8);
        foreach (var node in scene.Surfboard.Nodes)
        {
            if (imageCastNodeIndexes.Contains(node.Index))
            {
                continue;
            }

            yield return new NodeMarkerRenderEntry
            {
                Node = node,
                NodeName = node.Name ?? $"node_{node.Index}",
                Group = node.Group,
                LocalRect = localRect,
                PlaceholderColor = ColorFromText(node.Group),
            };
        }
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
}

internal sealed class ImageRenderEntry
{
    public required SbSceneImageCast ImageCast { get; init; }

    public required NodeInfo Node { get; init; }

    public required string NodeName { get; init; }

    public required string Group { get; init; }

    public required Rect LocalRect { get; init; }

    public required Color PlaceholderColor { get; init; }

    public required bool FlipX { get; init; }

    public required bool FlipY { get; init; }
}

internal sealed class NodeMarkerRenderEntry
{
    public required NodeInfo Node { get; init; }

    public required string NodeName { get; init; }

    public required string Group { get; init; }

    public required Rect LocalRect { get; init; }

    public required Color PlaceholderColor { get; init; }
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
    private readonly Dictionary<(int AtlasIndex, int CropIndex), RgbaImage> _cropCache = [];
    private readonly Dictionary<LitBitmapCacheKey, BitmapSource> _litBitmapCache = [];

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
        return ResolveBitmap(
            scene,
            imageCast,
            imageCast.PrimaryCropReferenceIndex ?? 0,
            DefaultNodeState,
            DefaultColorState,
            flipX: false,
            flipY: false,
            out resourceInfo);
    }

    public BitmapSource? ResolveBitmap(
        SbSceneFile scene,
        SbSceneImageCast imageCast,
        int primaryReferenceIndex,
        SbSceneNodeAnimationState nodeState,
        SbSceneResolvedNodeColorState colorState,
        bool flipX,
        bool flipY,
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
            return ResolveLitBitmap(cached, cacheKey, nodeState, colorState, flipX, flipY);
        }

        var crop = atlas.Crops[reference.CropIndex];
        if (crop.Width <= 0 || crop.Height <= 0)
        {
            resourceInfo = $"{atlas.Name}[{reference.CropIndex}] invalid crop";
            return null;
        }

        var cropped = atlasImage.CropWithTransparentPadding(crop.Left, crop.Top, crop.Width, crop.Height);
        _cropCache[cacheKey] = cropped;
        resourceInfo = $"{atlas.Name}[{reference.CropIndex}]";
        return ResolveLitBitmap(cropped, cacheKey, nodeState, colorState, flipX, flipY);
    }

    private static SbSceneNodeAnimationState DefaultNodeState { get; } = new()
    {
        TranslationX = 0,
        TranslationY = 0,
        RotationDegrees = 0,
        ScaleX = 1,
        ScaleY = 1,
        Display = true,
        MaterialR = byte.MaxValue,
        MaterialG = byte.MaxValue,
        MaterialB = byte.MaxValue,
        MaterialA = byte.MaxValue,
        IlluminationR = SbSceneColorConventions.OpaqueBlack.R,
        IlluminationG = SbSceneColorConventions.OpaqueBlack.G,
        IlluminationB = SbSceneColorConventions.OpaqueBlack.B,
        IlluminationA = SbSceneColorConventions.OpaqueBlack.A,
        VertexColors =
        [
            SbSceneColorConventions.OpaqueWhite,
            SbSceneColorConventions.OpaqueWhite,
            SbSceneColorConventions.OpaqueWhite,
            SbSceneColorConventions.OpaqueWhite,
        ],
    };

    private static SbSceneResolvedNodeColorState DefaultColorState { get; } = new()
    {
        MaterialColor = SbSceneColorConventions.OpaqueWhite,
        IlluminationColor = SbSceneColorConventions.OpaqueBlack,
    };

    private BitmapSource ResolveLitBitmap(
        RgbaImage crop,
        (int AtlasIndex, int CropIndex) cropKey,
        SbSceneNodeAnimationState nodeState,
        SbSceneResolvedNodeColorState colorState,
        bool flipX,
        bool flipY)
    {
        var key = LitBitmapCacheKey.Create(cropKey, nodeState, colorState, flipX, flipY);
        if (_litBitmapCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var bitmap = ToLitBitmapSource(crop, nodeState, colorState, flipX, flipY);
        _litBitmapCache[key] = bitmap;
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

    private static BitmapSource ToLitBitmapSource(
        RgbaImage image,
        SbSceneNodeAnimationState nodeState,
        SbSceneResolvedNodeColorState colorState,
        bool flipX,
        bool flipY)
    {
        var bgra = new byte[image.Pixels.Length];
        var material = new RgbaColor(colorState.MaterialColor.R, colorState.MaterialColor.G, colorState.MaterialColor.B, byte.MaxValue);
        var illumination = colorState.IlluminationColor;
        for (var y = 0; y < image.Height; y++)
        {
            var sourceV = (y + 0.5) / image.Height;
            var vertexV = flipY ? 1.0 - sourceV : sourceV;
            for (var x = 0; x < image.Width; x++)
            {
                var sourceOffset = (y * image.Width + x) * 4;
                var sourceU = (x + 0.5) / image.Width;
                // Texture flipping changes which source pixel lands on the quad; vertex color still belongs to the unflipped quad position.
                var vertexU = flipX ? 1.0 - sourceU : sourceU;
                var vertex = SbSceneColorConventions.InterpolateVertexColor(nodeState.VertexColors, vertexU, vertexV);
                var lit = SbSceneColorConventions.ApplyLighting(
                    image.Pixels[sourceOffset],
                    image.Pixels[sourceOffset + 1],
                    image.Pixels[sourceOffset + 2],
                    image.Pixels[sourceOffset + 3],
                    material,
                    illumination,
                    vertex);

                bgra[sourceOffset] = ToByte(lit.B);
                bgra[sourceOffset + 1] = ToByte(lit.G);
                bgra[sourceOffset + 2] = ToByte(lit.R);
                bgra[sourceOffset + 3] = ToByte(lit.A);
            }
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

    private static byte ToByte(double value)
    {
        return (byte)Math.Clamp((int)Math.Round(value), byte.MinValue, byte.MaxValue);
    }

    private readonly record struct LitBitmapCacheKey(
        int AtlasIndex,
        int CropIndex,
        RgbaColor MaterialRgb,
        RgbaColor Illumination,
        RgbaColor Vertex0,
        RgbaColor Vertex1,
        RgbaColor Vertex2,
        RgbaColor Vertex3,
        bool FlipX,
        bool FlipY)
    {
        public static LitBitmapCacheKey Create(
            (int AtlasIndex, int CropIndex) cropKey,
            SbSceneNodeAnimationState nodeState,
            SbSceneResolvedNodeColorState colorState,
            bool flipX,
            bool flipY)
        {
            return new LitBitmapCacheKey(
                cropKey.AtlasIndex,
                cropKey.CropIndex,
                new RgbaColor(colorState.MaterialColor.R, colorState.MaterialColor.G, colorState.MaterialColor.B, byte.MaxValue),
                colorState.IlluminationColor,
                GetVertexColor(nodeState, 0),
                GetVertexColor(nodeState, 1),
                GetVertexColor(nodeState, 2),
                GetVertexColor(nodeState, 3),
                flipX,
                flipY);
        }

        private static RgbaColor GetVertexColor(SbSceneNodeAnimationState nodeState, int index)
        {
            return index >= 0 && index < nodeState.VertexColors.Count
                ? nodeState.VertexColors[index]
                : SbSceneColorConventions.OpaqueWhite;
        }
    }
}

internal static class SceneRenderBuilder
{
    public static RenderScene Build(SbSceneFile scene, SvoRenderResources? resources, RenderSceneOptions options)
    {
        return Build(new SceneRenderBuildCache(scene), resources, options);
    }

    public static RenderScene Build(SceneRenderBuildCache cache, SvoRenderResources? resources, RenderSceneOptions options)
    {
        var scene = cache.Scene;
        var nodes = cache.Nodes;
        var frameState = SbSceneAnimationFrameBuilder.BuildInitial(scene);
        foreach (var animation in options.Animations)
        {
            SbSceneAnimationFrameBuilder.ApplyAnimation(scene, frameState, animation.Animation, animation.Frame);
        }

        var nodeStates = frameState.Nodes;
        var imageStates = frameState.ImageCasts;
        var visibleNodes = SbSceneRenderTree.BuildFinalVisibility(
            nodes,
            cache.ParentByNode,
            index => nodeStates[index].Display,
            options.ShowHiddenNodes);
        var effectiveOpacities = SbSceneRenderTree.BuildEffectiveOpacity(
            nodes,
            cache.ParentByNode,
            index => nodeStates[index].MaterialA / 255.0);
        var effectiveColors = SbSceneRenderTree.BuildEffectiveColors(
            nodes,
            cache.ParentByNode,
            index => nodeStates[index].MaterialColor,
            index => nodeStates[index].IlluminationColor);
        var worldTransforms = BuildWorldTransforms(nodes, nodeStates, cache.ParentIndexes);
        var items = new List<RenderItem>();

        foreach (var entry in cache.ImageEntries)
        {
            var node = entry.Node;
            if (!ShouldRenderNode(node, options, visibleNodes))
            {
                continue;
            }

            var opacity = effectiveOpacities[node.Index];
            if (opacity <= 0)
            {
                continue;
            }

            var transform = worldTransforms[node.Index];
            var imageCast = entry.ImageCast;
            var imageState = imageCast.Index >= 0 && imageCast.Index < imageStates.Count
                ? imageStates[imageCast.Index]
                : null;
            var localRect = imageState is null
                ? entry.LocalRect
                : CreateImageLocalRect(imageCast, imageState);
            if (localRect.Width <= 0 || localRect.Height <= 0)
            {
                continue;
            }

            string? resourceInfo = null;
            var bitmap = resources is null
                ? null
                : resources.ResolveBitmap(
                    scene,
                    imageCast,
                    imageState?.PrimaryReferenceIndex ?? 0,
                    nodeStates[node.Index],
                    effectiveColors[node.Index],
                    entry.FlipX,
                    entry.FlipY,
                    out resourceInfo);
            resourceInfo ??= resources is null ? "no SVO bound" : null;
            var worldBounds = TransformRect(localRect, transform);
            items.Add(new RenderItem
            {
                NodeIndex = node.Index,
                NodeName = entry.NodeName,
                Group = entry.Group,
                Kind = "image",
                WorldTransform = transform,
                LocalRect = localRect,
                WorldBounds = worldBounds,
                Bitmap = bitmap,
                PlaceholderColor = entry.PlaceholderColor,
                Opacity = opacity,
                FlipX = entry.FlipX,
                FlipY = entry.FlipY,
                ResourceInfo = resourceInfo,
            });
        }

        if (options.ShowNodeMarkers)
        {
            foreach (var entry in cache.NodeMarkerEntries)
            {
                var node = entry.Node;
                if (!ShouldRenderNode(node, options, visibleNodes))
                {
                    continue;
                }

                var transform = worldTransforms[node.Index];
                items.Add(new RenderItem
                {
                    NodeIndex = node.Index,
                    NodeName = entry.NodeName,
                    Group = entry.Group,
                    Kind = "node",
                    WorldTransform = transform,
                    LocalRect = entry.LocalRect,
                    WorldBounds = TransformRect(entry.LocalRect, transform),
                    PlaceholderColor = entry.PlaceholderColor,
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
            Warnings = cache.Warnings,
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

    private static IReadOnlyList<Matrix> BuildWorldTransforms(
        IReadOnlyList<NodeInfo> nodes,
        IReadOnlyList<SbSceneNodeAnimationState> nodeStates,
        IReadOnlyList<int> parentIndexes)
    {
        var memo = new Matrix[nodes.Count];
        var hasMemo = new bool[nodes.Count];
        var visiting = new bool[nodes.Count];

        Matrix Resolve(int index)
        {
            if (hasMemo[index])
            {
                return memo[index];
            }

            if (visiting[index])
            {
                return BuildLocalTransform(nodeStates[index]);
            }

            visiting[index] = true;
            var local = BuildLocalTransform(nodeStates[index]);
            var world = local;
            var parentIndex = index >= 0 && index < parentIndexes.Count ? parentIndexes[index] : -1;
            if (parentIndex >= 0 && parentIndex < nodes.Count)
            {
                world.Append(Resolve(parentIndex));
            }

            visiting[index] = false;
            memo[index] = world;
            hasMemo[index] = true;
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

    private static Rect CreateImageLocalRect(SbSceneImageCast imageCast, SbSceneImageCastAnimationState imageState)
    {
        var geometry = SbSceneImageCastConventions.ResolveAnimatedGeometry(imageCast, imageState.Width, imageState.Height);
        return new Rect(-geometry.PivotX, -geometry.PivotY, geometry.Width, geometry.Height);
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
