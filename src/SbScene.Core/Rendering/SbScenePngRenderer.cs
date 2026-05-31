using SbScene.Core.Images;
using SbScene.Core.Resources;
using SbScene.Core.Semantics;

namespace SbScene.Core.Rendering;

public sealed class SbSceneRenderOptions
{
    public int Padding { get; init; } = 80;

    public double Scale { get; init; } = 1.0;

    public int Supersample { get; init; } = 1;

    public SbSceneTextureSampling TextureSampling { get; init; } = SbSceneTextureSampling.Nearest;

    public RgbaColor BackgroundColor { get; init; } = RgbaColor.Transparent;

    public bool ShowHiddenNodes { get; init; }

    public bool RenderSecondaryImages { get; init; }

    public IReadOnlyList<SbSceneAnimationSelection> Animations { get; init; } = Array.Empty<SbSceneAnimationSelection>();
}

public enum SbSceneTextureSampling
{
    Nearest,
    Bilinear,
}

public sealed record SbSceneAnimationSelection(string Name, double Frame);

public readonly record struct RgbaColor(byte R, byte G, byte B, byte A)
{
    public static RgbaColor Transparent { get; } = new(0, 0, 0, 0);
}

public sealed class SbSceneRenderResult
{
    public required RgbaImage Image { get; init; }

    public required int RenderedItemCount { get; init; }

    public required int CandidateItemCount { get; init; }

    public required IReadOnlyList<string> Warnings { get; init; }
}

public static class SbScenePngRenderer
{
    private const double Epsilon = 0.000001;

    public static SbSceneRenderResult Render(SbSceneFile scene, string svoPath, SbSceneRenderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentException.ThrowIfNullOrWhiteSpace(svoPath);

        options ??= new SbSceneRenderOptions();
        if (options.Scale <= 0 || double.IsNaN(options.Scale) || double.IsInfinity(options.Scale))
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Render scale must be a positive finite number.");
        }

        if (options.Padding < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Render padding must be non-negative.");
        }

        if (options.Supersample is < 1 or > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Supersample factor must be between 1 and 8.");
        }

        var warnings = new List<string>();
        var warningSet = new HashSet<string>(StringComparer.Ordinal);
        void AddWarning(string warning)
        {
            if (warningSet.Add(warning))
            {
                warnings.Add(warning);
            }
        }

        var nodeStates = BuildInitialNodeStates(scene.Surfboard.Nodes);
        var imageStates = BuildInitialImageStates(scene.Surfboard.Resources.ImageCasts);
        ApplyAnimations(scene, nodeStates, imageStates, options.Animations, AddWarning);

        var parentByNode = SbSceneRenderTree.BuildParentMap(scene.Surfboard.Nodes);
        var visibleNodes = SbSceneRenderTree.BuildFinalVisibility(
            scene.Surfboard.Nodes,
            parentByNode,
            index => nodeStates[index].Display,
            options.ShowHiddenNodes);
        var effectiveOpacities = SbSceneRenderTree.BuildEffectiveOpacity(
            scene.Surfboard.Nodes,
            parentByNode,
            index => nodeStates[index].MaterialA / 255.0);
        var worldTransforms = BuildWorldTransforms(scene.Surfboard.Nodes, nodeStates, parentByNode);
        var layers = BuildRenderLayers(scene, nodeStates, imageStates, visibleNodes, effectiveOpacities, worldTransforms, options.RenderSecondaryImages);
        var contentBounds = ComputeBounds(layers);
        var width = Math.Max(1, (int)Math.Ceiling((contentBounds.Width + options.Padding * 2) * options.Scale));
        var height = Math.Max(1, (int)Math.Ceiling((contentBounds.Height + options.Padding * 2) * options.Scale));
        var renderScale = options.Scale * options.Supersample;
        var renderWidth = width * options.Supersample;
        var renderHeight = height * options.Supersample;
        var output = CreateImage(renderWidth, renderHeight, options.BackgroundColor);
        var resources = RenderResourceResolver.Load(scene, svoPath, AddWarning);
        var offsetX = options.Padding - contentBounds.Left;
        var offsetY = options.Padding - contentBounds.Top;
        var rendered = 0;

        foreach (var layer in layers)
        {
            var crop = resources.ResolveCrop(layer.Reference, AddWarning);
            if (crop is null)
            {
                continue;
            }

            if (DrawLayer(output, crop, layer, offsetX, offsetY, renderScale, options.TextureSampling))
            {
                rendered++;
            }
        }

        var finalImage = options.Supersample == 1 ? output : DownsampleBox(output, width, height, options.Supersample);
        return new SbSceneRenderResult
        {
            Image = finalImage,
            RenderedItemCount = rendered,
            CandidateItemCount = layers.Count,
            Warnings = warnings,
        };
    }

    private static RgbaImage CreateImage(int width, int height, RgbaColor color)
    {
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = color.R;
            pixels[i + 1] = color.G;
            pixels[i + 2] = color.B;
            pixels[i + 3] = color.A;
        }

        return new RgbaImage(width, height, pixels);
    }

    private static IReadOnlyList<NodeRenderState> BuildInitialNodeStates(IReadOnlyList<NodeInfo> nodes)
    {
        return nodes.Select(static node =>
        {
            var transform = node.Transform2D;
            var material = transform?.MaterialColor;
            var illumination = transform?.IlluminationColor;
            return new NodeRenderState
            {
                TranslationX = transform?.Translation?.X ?? 0,
                TranslationY = transform?.Translation?.Y ?? 0,
                RotationDegrees = transform?.RotationZDegreesCandidate ?? transform?.RotationZ ?? 0,
                ScaleX = transform?.Scale?.X ?? 1,
                ScaleY = transform?.Scale?.Y ?? 1,
                Display = transform?.Display ?? true,
                MaterialR = material?.R ?? byte.MaxValue,
                MaterialG = material?.G ?? byte.MaxValue,
                MaterialB = material?.B ?? byte.MaxValue,
                MaterialA = material?.A ?? byte.MaxValue,
                IlluminationR = illumination?.R ?? byte.MinValue,
                IlluminationG = illumination?.G ?? byte.MinValue,
                IlluminationB = illumination?.B ?? byte.MinValue,
                IlluminationA = illumination?.A ?? byte.MinValue,
                VertexColors = BuildVertexColors(transform),
            };
        }).ToArray();
    }

    private static IReadOnlyList<RgbaColor> BuildVertexColors(Transform2DInfo? transform)
    {
        if (transform is null || transform.VertexColors.Count == 0)
        {
            return [SbSceneColorConventions.OpaqueWhite, SbSceneColorConventions.OpaqueWhite, SbSceneColorConventions.OpaqueWhite, SbSceneColorConventions.OpaqueWhite];
        }

        var colors = transform.VertexColors
            .Take(4)
            .Select(static color => new RgbaColor(color.R, color.G, color.B, color.A))
            .ToList();
        while (colors.Count < 4)
        {
            colors.Add(SbSceneColorConventions.OpaqueWhite);
        }

        return colors;
    }

    private static IReadOnlyList<ImageCastRenderState> BuildInitialImageStates(IReadOnlyList<SbSceneImageCast> imageCasts)
    {
        return imageCasts.Select(static imageCast => new ImageCastRenderState
        {
            PrimaryReferenceIndex = ClampReferenceIndex(imageCast.PrimaryCropReferenceIndex, imageCast.PrimaryCropReferences.Count),
            SecondaryReferenceIndex = ClampReferenceIndex(imageCast.SecondaryCropReferenceIndex, imageCast.SecondaryCropReferences.Count),
        }).ToArray();
    }

    private static int ClampReferenceIndex(int? value, int count)
    {
        return value is >= 0 && value < count ? value.Value : 0;
    }

    private static void ApplyAnimations(
        SbSceneFile scene,
        IReadOnlyList<NodeRenderState> nodeStates,
        IReadOnlyList<ImageCastRenderState> imageStates,
        IReadOnlyList<SbSceneAnimationSelection> selections,
        Action<string> addWarning)
    {
        if (selections.Count == 0)
        {
            return;
        }

        var animationsByName = scene.Surfboard.Animations
            .Where(static animation => !string.IsNullOrWhiteSpace(animation.Name))
            .GroupBy(static animation => animation.Name!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.OrdinalIgnoreCase);
        var imageCastsByNode = scene.Surfboard.Resources.ImageCasts
            .GroupBy(static imageCast => imageCast.CastIndex)
            .ToDictionary(static group => group.Key, static group => group.ToArray());
        var nodeIndexByName = scene.Surfboard.Nodes
            .Where(static node => !string.IsNullOrWhiteSpace(node.Name))
            .GroupBy(static node => node.Name!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.First().Index, StringComparer.OrdinalIgnoreCase);

        foreach (var selection in selections)
        {
            if (!animationsByName.TryGetValue(selection.Name, out var animation))
            {
                addWarning($"Animation '{selection.Name}' was not found.");
                continue;
            }

            foreach (var motion in animation.Motions)
            {
                var nodeIndex = ResolveMotionNodeIndex(scene.Surfboard.Nodes, nodeIndexByName, motion);
                if (nodeIndex is null || nodeIndex < 0 || nodeIndex >= nodeStates.Count)
                {
                    continue;
                }

                var state = nodeStates[nodeIndex.Value];
                foreach (var track in motion.Tracks)
                {
                    ApplyTrack(track, selection.Frame, state, nodeIndex.Value, imageCastsByNode, imageStates);
                }
            }
        }
    }

    private static int? ResolveMotionNodeIndex(
        IReadOnlyList<NodeInfo> nodes,
        IReadOnlyDictionary<string, int> nodeIndexByName,
        MotionInfo motion)
    {
        if (motion.CastIndex is int castIndex && castIndex >= 0 && castIndex < nodes.Count)
        {
            return castIndex;
        }

        if (motion.TargetIndex is int targetIndex && targetIndex >= 0 && targetIndex < nodes.Count)
        {
            return targetIndex;
        }

        return !string.IsNullOrWhiteSpace(motion.TargetName) && nodeIndexByName.TryGetValue(motion.TargetName, out var namedIndex)
            ? namedIndex
            : null;
    }

    private static void ApplyTrack(
        TrackInfo track,
        double frame,
        NodeRenderState state,
        int nodeIndex,
        IReadOnlyDictionary<int, SbSceneImageCast[]> imageCastsByNode,
        IReadOnlyList<ImageCastRenderState> imageStates)
    {
        switch (track.TrackType)
        {
            case 0 when SbSceneAnimationEvaluator.EvaluateTrack(track, frame) is double translateX:
                state.TranslationX = translateX;
                break;
            case 1 when SbSceneAnimationEvaluator.EvaluateTrack(track, frame) is double translateY:
                state.TranslationY = translateY;
                break;
            case 5 when SbSceneAnimationEvaluator.EvaluateTrack(track, frame) is double rotation:
                state.RotationDegrees = rotation;
                break;
            case 6 when SbSceneAnimationEvaluator.EvaluateTrack(track, frame) is double scaleX:
                state.ScaleX = scaleX;
                break;
            case 7 when SbSceneAnimationEvaluator.EvaluateTrack(track, frame) is double scaleY:
                state.ScaleY = scaleY;
                break;
            case 11 when SbSceneAnimationEvaluator.EvaluateTrack(track, frame) is double display:
                state.Display = display >= 0.5;
                break;
            case 18 when SbSceneAnimationEvaluator.EvaluateTrack(track, frame) is double primaryIndex:
                ApplyImageReferenceIndex(nodeIndex, imageCastsByNode, imageStates, primary: true, (int)Math.Round(primaryIndex));
                break;
            case 19 when SbSceneAnimationEvaluator.EvaluateTrack(track, frame) is double secondaryIndex:
                ApplyImageReferenceIndex(nodeIndex, imageCastsByNode, imageStates, primary: false, (int)Math.Round(secondaryIndex));
                break;
            case 21 when SbSceneAnimationEvaluator.EvaluateTrack(track, frame) is double materialRed:
                state.MaterialR = ToByteChannel(materialRed);
                break;
            case 22 when SbSceneAnimationEvaluator.EvaluateTrack(track, frame) is double materialGreen:
                state.MaterialG = ToByteChannel(materialGreen);
                break;
            case 23 when SbSceneAnimationEvaluator.EvaluateTrack(track, frame) is double materialBlue:
                state.MaterialB = ToByteChannel(materialBlue);
                break;
            case 24 when SbSceneAnimationEvaluator.EvaluateTrack(track, frame) is double materialAlpha:
                state.MaterialA = ToByteChannel(materialAlpha);
                break;
            case 25 when SbSceneAnimationEvaluator.EvaluateTrack(track, frame) is double illuminationRed:
                state.IlluminationR = ToByteChannel(illuminationRed);
                break;
            case 26 when SbSceneAnimationEvaluator.EvaluateTrack(track, frame) is double illuminationGreen:
                state.IlluminationG = ToByteChannel(illuminationGreen);
                break;
            case 27 when SbSceneAnimationEvaluator.EvaluateTrack(track, frame) is double illuminationBlue:
                state.IlluminationB = ToByteChannel(illuminationBlue);
                break;
            case 28 when SbSceneAnimationEvaluator.EvaluateTrack(track, frame) is double illuminationAlpha:
                state.IlluminationA = ToByteChannel(illuminationAlpha);
                break;
        }
    }

    private static void ApplyImageReferenceIndex(
        int nodeIndex,
        IReadOnlyDictionary<int, SbSceneImageCast[]> imageCastsByNode,
        IReadOnlyList<ImageCastRenderState> imageStates,
        bool primary,
        int referenceIndex)
    {
        if (!imageCastsByNode.TryGetValue(nodeIndex, out var imageCasts))
        {
            return;
        }

        foreach (var imageCast in imageCasts)
        {
            if (imageCast.Index < 0 || imageCast.Index >= imageStates.Count)
            {
                continue;
            }

            var state = imageStates[imageCast.Index];
            if (primary)
            {
                state.PrimaryReferenceIndex = ClampReferenceIndex(referenceIndex, imageCast.PrimaryCropReferences.Count);
            }
            else
            {
                state.SecondaryReferenceIndex = ClampReferenceIndex(referenceIndex, imageCast.SecondaryCropReferences.Count);
            }
        }
    }

    private static byte ToByteChannel(double value)
    {
        var scaled = value is >= 0 and <= 1.0 + Epsilon ? value * 255.0 : value;
        return (byte)Math.Clamp((int)Math.Round(scaled), byte.MinValue, byte.MaxValue);
    }

    private static IReadOnlyList<Matrix2D> BuildWorldTransforms(
        IReadOnlyList<NodeInfo> nodes,
        IReadOnlyList<NodeRenderState> nodeStates,
        IReadOnlyDictionary<int, int> parentByNode)
    {
        var memo = new Dictionary<int, Matrix2D>();
        var visiting = new HashSet<int>();

        Matrix2D Resolve(int index)
        {
            if (memo.TryGetValue(index, out var cached))
            {
                return cached;
            }

            var local = Matrix2D.FromTransform(nodeStates[index]);
            if (!visiting.Add(index))
            {
                return local;
            }

            var world = local;
            if (parentByNode.TryGetValue(index, out var parentIndex) && parentIndex >= 0 && parentIndex < nodes.Count)
            {
                world = Matrix2D.Multiply(Resolve(parentIndex), local);
            }

            visiting.Remove(index);
            memo[index] = world;
            return world;
        }

        return Enumerable.Range(0, nodes.Count).Select(Resolve).ToArray();
    }

    private static IReadOnlyList<RenderLayer> BuildRenderLayers(
        SbSceneFile scene,
        IReadOnlyList<NodeRenderState> nodeStates,
        IReadOnlyList<ImageCastRenderState> imageStates,
        IReadOnlyList<bool> visibleNodes,
        IReadOnlyList<double> effectiveOpacities,
        IReadOnlyList<Matrix2D> worldTransforms,
        bool renderSecondaryImages)
    {
        var layers = new List<RenderLayer>();
        foreach (var imageCast in scene.Surfboard.Resources.ImageCasts)
        {
            if (imageCast.CastIndex < 0 || imageCast.CastIndex >= scene.Surfboard.Nodes.Count)
            {
                continue;
            }

            if (!visibleNodes[imageCast.CastIndex])
            {
                continue;
            }

            var opacity = effectiveOpacities[imageCast.CastIndex];
            if (opacity <= 0)
            {
                continue;
            }

            if (imageCast.Width <= 0 || imageCast.Height <= 0)
            {
                continue;
            }

            if (imageCast.Index < 0 || imageCast.Index >= imageStates.Count)
            {
                continue;
            }

            var localRect = RenderRect.FromLeftTopWidthHeight(-imageCast.PivotX, -imageCast.PivotY, imageCast.Width, imageCast.Height);
            var world = worldTransforms[imageCast.CastIndex];
            var worldBounds = TransformRect(localRect, world);
            AddLayer(imageCast.PrimaryCropReferences, imageStates[imageCast.Index].PrimaryReferenceIndex);
            if (renderSecondaryImages)
            {
                AddLayer(imageCast.SecondaryCropReferences, imageStates[imageCast.Index].SecondaryReferenceIndex);
            }

            void AddLayer(IReadOnlyList<SbSceneCropReference> references, int referenceIndex)
            {
                if (references.Count == 0)
                {
                    return;
                }

                var index = referenceIndex >= 0 && referenceIndex < references.Count ? referenceIndex : 0;
                layers.Add(new RenderLayer
                {
                    NodeState = nodeStates[imageCast.CastIndex],
                    LocalRect = localRect,
                    WorldTransform = world,
                    WorldBounds = worldBounds,
                    EffectiveOpacity = opacity,
                    AdditiveBlend = SbSceneImageCastConventions.HasAdditiveBlendCandidate(imageCast),
                    FlipX = SbSceneImageCastConventions.HasHorizontalFlip(imageCast),
                    FlipY = SbSceneImageCastConventions.HasVerticalFlip(imageCast),
                    Reference = references[index],
                });
            }
        }

        return layers;
    }

    private static RenderRect ComputeBounds(IReadOnlyList<RenderLayer> layers)
    {
        if (layers.Count == 0)
        {
            return RenderRect.FromLeftTopWidthHeight(-160, -120, 320, 240);
        }

        var left = double.PositiveInfinity;
        var top = double.PositiveInfinity;
        var right = double.NegativeInfinity;
        var bottom = double.NegativeInfinity;

        foreach (var layer in layers)
        {
            left = Math.Min(left, layer.WorldBounds.Left);
            top = Math.Min(top, layer.WorldBounds.Top);
            right = Math.Max(right, layer.WorldBounds.Right);
            bottom = Math.Max(bottom, layer.WorldBounds.Bottom);
        }

        return right <= left || bottom <= top
            ? RenderRect.FromLeftTopWidthHeight(left - 160, top - 120, 320, 240)
            : RenderRect.FromEdges(left, top, right, bottom);
    }

    private static RenderRect TransformRect(RenderRect rect, Matrix2D matrix)
    {
        var points = new[]
        {
            matrix.Transform(rect.Left, rect.Top),
            matrix.Transform(rect.Right, rect.Top),
            matrix.Transform(rect.Right, rect.Bottom),
            matrix.Transform(rect.Left, rect.Bottom),
        };

        return RenderRect.FromEdges(
            points.Min(static point => point.X),
            points.Min(static point => point.Y),
            points.Max(static point => point.X),
            points.Max(static point => point.Y));
    }

    private static bool DrawLayer(
        RgbaImage output,
        RgbaImage crop,
        RenderLayer layer,
        double offsetX,
        double offsetY,
        double scale,
        SbSceneTextureSampling textureSampling)
    {
        if (!layer.WorldTransform.TryInvert(out var inverse))
        {
            return false;
        }

        var minX = Math.Clamp((int)Math.Floor((layer.WorldBounds.Left + offsetX) * scale), 0, output.Width);
        var minY = Math.Clamp((int)Math.Floor((layer.WorldBounds.Top + offsetY) * scale), 0, output.Height);
        var maxX = Math.Clamp((int)Math.Ceiling((layer.WorldBounds.Right + offsetX) * scale), 0, output.Width);
        var maxY = Math.Clamp((int)Math.Ceiling((layer.WorldBounds.Bottom + offsetY) * scale), 0, output.Height);
        var drewAny = false;

        for (var y = minY; y < maxY; y++)
        {
            var worldY = (y + 0.5) / scale - offsetY;
            for (var x = minX; x < maxX; x++)
            {
                var worldX = (x + 0.5) / scale - offsetX;
                var local = inverse.Transform(worldX, worldY);
                if (!layer.LocalRect.Contains(local.X, local.Y))
                {
                    continue;
                }

                var u = (local.X - layer.LocalRect.Left) / layer.LocalRect.Width;
                var v = (local.Y - layer.LocalRect.Top) / layer.LocalRect.Height;
                SampleCrop(crop, u, v, layer.FlipX, layer.FlipY, textureSampling, out var sampleR, out var sampleG, out var sampleB, out var sampleA);
                var sourceAlpha = sampleA * layer.EffectiveOpacity;
                if (sourceAlpha <= 0)
                {
                    continue;
                }

                var vertexColor = SbSceneColorConventions.InterpolateVertexColor(layer.NodeState.VertexColors, u, v);
                var litColor = SbSceneColorConventions.ApplyLighting(
                    sampleR,
                    sampleG,
                    sampleB,
                    sourceAlpha,
                    layer.NodeState.MaterialColor,
                    layer.NodeState.IlluminationColor,
                    vertexColor);
                BlendPixel(output.Pixels, (y * output.Width + x) * 4, litColor.R, litColor.G, litColor.B, litColor.A, layer.AdditiveBlend);
                drewAny = true;
            }
        }

        return drewAny;
    }

    private static void SampleCrop(
        RgbaImage crop,
        double u,
        double v,
        bool flipX,
        bool flipY,
        SbSceneTextureSampling textureSampling,
        out double r,
        out double g,
        out double b,
        out double a)
    {
        if (textureSampling == SbSceneTextureSampling.Bilinear)
        {
            SampleCropBilinear(crop, u, v, flipX, flipY, out r, out g, out b, out a);
            return;
        }

        var sourceX = ToSourceCoordinate(u, crop.Width, flipX);
        var sourceY = ToSourceCoordinate(v, crop.Height, flipY);
        var sourceOffset = (sourceY * crop.Width + sourceX) * 4;
        r = crop.Pixels[sourceOffset];
        g = crop.Pixels[sourceOffset + 1];
        b = crop.Pixels[sourceOffset + 2];
        a = crop.Pixels[sourceOffset + 3];
    }

    private static int ToSourceCoordinate(double unitCoordinate, int size, bool flipped)
    {
        var source = Math.Clamp((int)Math.Floor(unitCoordinate * size), 0, size - 1);
        return flipped ? size - 1 - source : source;
    }

    private static void SampleCropBilinear(
        RgbaImage crop,
        double u,
        double v,
        bool flipX,
        bool flipY,
        out double r,
        out double g,
        out double b,
        out double a)
    {
        var sourceX = ToSourceCoordinateForInterpolation(u, crop.Width, flipX);
        var sourceY = ToSourceCoordinateForInterpolation(v, crop.Height, flipY);
        var x0 = Math.Clamp((int)Math.Floor(sourceX), 0, crop.Width - 1);
        var y0 = Math.Clamp((int)Math.Floor(sourceY), 0, crop.Height - 1);
        var x1 = Math.Min(x0 + 1, crop.Width - 1);
        var y1 = Math.Min(y0 + 1, crop.Height - 1);
        var tx = Math.Clamp(sourceX - x0, 0, 1);
        var ty = Math.Clamp(sourceY - y0, 0, 1);

        ReadPixelPremultiplied(crop, x0, y0, out var r00, out var g00, out var b00, out var a00);
        ReadPixelPremultiplied(crop, x1, y0, out var r10, out var g10, out var b10, out var a10);
        ReadPixelPremultiplied(crop, x0, y1, out var r01, out var g01, out var b01, out var a01);
        ReadPixelPremultiplied(crop, x1, y1, out var r11, out var g11, out var b11, out var a11);

        var topR = Lerp(r00, r10, tx);
        var topG = Lerp(g00, g10, tx);
        var topB = Lerp(b00, b10, tx);
        var topA = Lerp(a00, a10, tx);
        var bottomR = Lerp(r01, r11, tx);
        var bottomG = Lerp(g01, g11, tx);
        var bottomB = Lerp(b01, b11, tx);
        var bottomA = Lerp(a01, a11, tx);

        var premulR = Lerp(topR, bottomR, ty);
        var premulG = Lerp(topG, bottomG, ty);
        var premulB = Lerp(topB, bottomB, ty);
        a = Lerp(topA, bottomA, ty);
        if (a <= 0)
        {
            r = 0;
            g = 0;
            b = 0;
            return;
        }

        r = premulR / a;
        g = premulG / a;
        b = premulB / a;
        a *= 255.0;
    }

    private static double ToSourceCoordinateForInterpolation(double unitCoordinate, int size, bool flipped)
    {
        var source = Math.Clamp(unitCoordinate * size - 0.5, 0, size - 1);
        return flipped ? size - 1 - source : source;
    }

    private static void ReadPixelPremultiplied(RgbaImage image, int x, int y, out double r, out double g, out double b, out double a)
    {
        var offset = (y * image.Width + x) * 4;
        a = image.Pixels[offset + 3] / 255.0;
        r = image.Pixels[offset] * a;
        g = image.Pixels[offset + 1] * a;
        b = image.Pixels[offset + 2] * a;
    }

    private static double Lerp(double left, double right, double amount)
    {
        return left + (right - left) * amount;
    }

    private static RgbaImage DownsampleBox(RgbaImage input, int width, int height, int factor)
    {
        var output = new byte[width * height * 4];
        var sampleCount = factor * factor;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var alphaSum = 0.0;
                var premulR = 0.0;
                var premulG = 0.0;
                var premulB = 0.0;
                for (var sampleY = 0; sampleY < factor; sampleY++)
                {
                    var sourceY = y * factor + sampleY;
                    for (var sampleX = 0; sampleX < factor; sampleX++)
                    {
                        var sourceX = x * factor + sampleX;
                        var sourceOffset = (sourceY * input.Width + sourceX) * 4;
                        var alpha = input.Pixels[sourceOffset + 3] / 255.0;
                        alphaSum += alpha;
                        premulR += input.Pixels[sourceOffset] * alpha;
                        premulG += input.Pixels[sourceOffset + 1] * alpha;
                        premulB += input.Pixels[sourceOffset + 2] * alpha;
                    }
                }

                var averageAlpha = alphaSum / sampleCount;
                var destinationOffset = (y * width + x) * 4;
                if (averageAlpha <= 0)
                {
                    output[destinationOffset] = 0;
                    output[destinationOffset + 1] = 0;
                    output[destinationOffset + 2] = 0;
                    output[destinationOffset + 3] = 0;
                    continue;
                }

                output[destinationOffset] = ToByte(premulR / alphaSum);
                output[destinationOffset + 1] = ToByte(premulG / alphaSum);
                output[destinationOffset + 2] = ToByte(premulB / alphaSum);
                output[destinationOffset + 3] = ToByte(averageAlpha * 255.0);
            }
        }

        return new RgbaImage(width, height, output);
    }

    private static void BlendPixel(byte[] pixels, int offset, double sourceR, double sourceG, double sourceB, double sourceAlphaByte, bool additive)
    {
        var sourceA = sourceAlphaByte / 255.0;
        var destinationA = pixels[offset + 3] / 255.0;
        var outA = sourceA + destinationA * (1.0 - sourceA);
        if (outA <= 0)
        {
            pixels[offset] = 0;
            pixels[offset + 1] = 0;
            pixels[offset + 2] = 0;
            pixels[offset + 3] = 0;
            return;
        }

        var destinationScale = additive ? 1.0 : 1.0 - sourceA;
        pixels[offset] = ToByte((sourceR * sourceA + pixels[offset] * destinationA * destinationScale) / outA);
        pixels[offset + 1] = ToByte((sourceG * sourceA + pixels[offset + 1] * destinationA * destinationScale) / outA);
        pixels[offset + 2] = ToByte((sourceB * sourceA + pixels[offset + 2] * destinationA * destinationScale) / outA);
        pixels[offset + 3] = ToByte(outA * 255.0);
    }

    private static byte ToByte(double value)
    {
        return (byte)Math.Clamp((int)Math.Round(value), byte.MinValue, byte.MaxValue);
    }

    private sealed class RenderResourceResolver
    {
        private readonly SbSceneFile _scene;
        private readonly IReadOnlyDictionary<int, RgbaImage> _atlasImages;
        private readonly Dictionary<(int AtlasIndex, int CropIndex), RgbaImage> _cropCache = [];

        private RenderResourceResolver(SbSceneFile scene, IReadOnlyDictionary<int, RgbaImage> atlasImages)
        {
            _scene = scene;
            _atlasImages = atlasImages;
        }

        public static RenderResourceResolver Load(SbSceneFile scene, string svoPath, Action<string> addWarning)
        {
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
                    addWarning($"No SVO texture found for atlas '{atlas.Name}'.");
                    continue;
                }

                if (texture.Width != atlas.Width || texture.Height != atlas.Height)
                {
                    addWarning($"Atlas '{atlas.Name}' dimension mismatch: SVO {texture.Width}x{texture.Height}, sbscene {atlas.Width}x{atlas.Height}.");
                }

                try
                {
                    atlasImages[atlas.Index] = DdsDecoder.Decode(texture.DdsBytes);
                }
                catch (Exception ex)
                {
                    addWarning($"Failed to decode atlas '{atlas.Name}': {ex.Message}");
                }
            }

            return new RenderResourceResolver(scene, atlasImages);
        }

        public RgbaImage? ResolveCrop(SbSceneCropReference reference, Action<string> addWarning)
        {
            var atlas = ResolveAtlas(reference);
            if (atlas is null)
            {
                addWarning($"Missing atlas for crop reference {reference.RawHex}.");
                return null;
            }

            if (reference.CropIndex < 0 || reference.CropIndex >= atlas.Crops.Count)
            {
                addWarning($"Crop index {reference.CropIndex} is out of range for atlas '{atlas.Name}'.");
                return null;
            }

            if (!_atlasImages.TryGetValue(atlas.Index, out var atlasImage))
            {
                addWarning($"Atlas '{atlas.Name}' was not decoded.");
                return null;
            }

            var cacheKey = (atlas.Index, reference.CropIndex);
            if (_cropCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            var crop = atlas.Crops[reference.CropIndex];
            if (crop.Width <= 0 || crop.Height <= 0)
            {
                addWarning($"Skipped invalid crop {atlas.Name}[{crop.Index}] = {crop.Left},{crop.Top},{crop.Right},{crop.Bottom}.");
                return null;
            }

            if (crop.Left < 0 || crop.Top < 0 || crop.Right > atlasImage.Width || crop.Bottom > atlasImage.Height)
            {
                addWarning($"Crop {atlas.Name}[{crop.Index}] extends outside atlas bounds and was padded with transparency.");
            }

            var image = atlasImage.CropWithTransparentPadding(crop.Left, crop.Top, crop.Width, crop.Height);
            _cropCache[cacheKey] = image;
            return image;
        }

        private SbSceneTextureAtlas? ResolveAtlas(SbSceneCropReference reference)
        {
            if (!string.IsNullOrWhiteSpace(reference.AtlasName))
            {
                var named = _scene.Surfboard.Resources.Atlases.FirstOrDefault(
                    atlas => string.Equals(atlas.Name, reference.AtlasName, StringComparison.OrdinalIgnoreCase));
                if (named is not null)
                {
                    return named;
                }
            }

            return reference.TextureIndex >= 0 && reference.TextureIndex < _scene.Surfboard.Resources.Atlases.Count
                ? _scene.Surfboard.Resources.Atlases[reference.TextureIndex]
                : null;
        }
    }

    private sealed class NodeRenderState
    {
        public double TranslationX { get; set; }

        public double TranslationY { get; set; }

        public double RotationDegrees { get; set; }

        public double ScaleX { get; set; }

        public double ScaleY { get; set; }

        public bool Display { get; set; }

        public byte MaterialR { get; set; }

        public byte MaterialG { get; set; }

        public byte MaterialB { get; set; }

        public byte MaterialA { get; set; }

        public byte IlluminationR { get; set; }

        public byte IlluminationG { get; set; }

        public byte IlluminationB { get; set; }

        public byte IlluminationA { get; set; }

        public required IReadOnlyList<RgbaColor> VertexColors { get; init; }

        public RgbaColor MaterialColor => new(MaterialR, MaterialG, MaterialB, MaterialA);

        public RgbaColor IlluminationColor => new(IlluminationR, IlluminationG, IlluminationB, IlluminationA);
    }

    private sealed class ImageCastRenderState
    {
        public int PrimaryReferenceIndex { get; set; }

        public int SecondaryReferenceIndex { get; set; }
    }

    private sealed class RenderLayer
    {
        public required NodeRenderState NodeState { get; init; }

        public required RenderRect LocalRect { get; init; }

        public required Matrix2D WorldTransform { get; init; }

        public required RenderRect WorldBounds { get; init; }

        public required double EffectiveOpacity { get; init; }

        public required bool AdditiveBlend { get; init; }

        public required bool FlipX { get; init; }

        public required bool FlipY { get; init; }

        public required SbSceneCropReference Reference { get; init; }
    }

    private readonly record struct Point2D(double X, double Y);

    private readonly record struct RenderRect(double Left, double Top, double Right, double Bottom)
    {
        public double Width => Right - Left;

        public double Height => Bottom - Top;

        public static RenderRect FromEdges(double left, double top, double right, double bottom)
        {
            return new RenderRect(left, top, right, bottom);
        }

        public static RenderRect FromLeftTopWidthHeight(double left, double top, double width, double height)
        {
            return new RenderRect(left, top, left + width, top + height);
        }

        public bool Contains(double x, double y)
        {
            return x >= Left && x < Right && y >= Top && y < Bottom;
        }
    }

    private readonly record struct Matrix2D(double M11, double M12, double M21, double M22, double OffsetX, double OffsetY)
    {
        public static Matrix2D FromTransform(NodeRenderState state)
        {
            var radians = SbSceneTransformConventions.ToScreenRotationDegrees(state.RotationDegrees) * Math.PI / 180.0;
            var cos = Math.Cos(radians);
            var sin = Math.Sin(radians);
            return new Matrix2D(
                cos * state.ScaleX,
                sin * state.ScaleX,
                -sin * state.ScaleY,
                cos * state.ScaleY,
                state.TranslationX,
                state.TranslationY);
        }

        public static Matrix2D Multiply(Matrix2D left, Matrix2D right)
        {
            return new Matrix2D(
                left.M11 * right.M11 + left.M21 * right.M12,
                left.M12 * right.M11 + left.M22 * right.M12,
                left.M11 * right.M21 + left.M21 * right.M22,
                left.M12 * right.M21 + left.M22 * right.M22,
                left.M11 * right.OffsetX + left.M21 * right.OffsetY + left.OffsetX,
                left.M12 * right.OffsetX + left.M22 * right.OffsetY + left.OffsetY);
        }

        public Point2D Transform(double x, double y)
        {
            return new Point2D(
                M11 * x + M21 * y + OffsetX,
                M12 * x + M22 * y + OffsetY);
        }

        public bool TryInvert(out Matrix2D inverse)
        {
            var determinant = M11 * M22 - M12 * M21;
            if (Math.Abs(determinant) < Epsilon)
            {
                inverse = default;
                return false;
            }

            inverse = new Matrix2D(
                M22 / determinant,
                -M12 / determinant,
                -M21 / determinant,
                M11 / determinant,
                (M21 * OffsetY - M22 * OffsetX) / determinant,
                (M12 * OffsetX - M11 * OffsetY) / determinant);
            return true;
        }
    }
}
