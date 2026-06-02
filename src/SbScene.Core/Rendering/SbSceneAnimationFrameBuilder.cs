using SbScene.Core.Resources;
using SbScene.Core.Semantics;
using System.Runtime.CompilerServices;

namespace SbScene.Core.Rendering;

public sealed class SbSceneAnimationFrameState
{
    public required IReadOnlyList<SbSceneNodeAnimationState> Nodes { get; init; }

    public required IReadOnlyList<SbSceneImageCastAnimationState> ImageCasts { get; init; }
}

public sealed class SbSceneNodeAnimationState
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

    public required IReadOnlyList<RgbaColor> VertexColors { get; set; }

    public RgbaColor MaterialColor => new(MaterialR, MaterialG, MaterialB, MaterialA);

    public RgbaColor IlluminationColor => new(IlluminationR, IlluminationG, IlluminationB, IlluminationA);
}

public sealed class SbSceneImageCastAnimationState
{
    public double Width { get; set; }

    public double Height { get; set; }

    public int PrimaryReferenceIndex { get; set; }

    public int SecondaryReferenceIndex { get; set; }
}

public static class SbSceneAnimationFrameBuilder
{
    private const double Epsilon = 0.000001;
    private static readonly ConditionalWeakTable<SbSceneFile, AnimationFrameCache> Caches = new();

    public static SbSceneAnimationFrameState BuildInitial(SbSceneFile scene)
    {
        ArgumentNullException.ThrowIfNull(scene);

        return GetCache(scene).BuildInitial();
    }

    public static SbSceneAnimationFrameState Build(
        SbSceneFile scene,
        IReadOnlyList<SbSceneAnimationSelection> selections,
        Action<string>? addWarning = null)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(selections);

        var state = GetCache(scene).BuildInitial();
        ApplyAnimations(scene, state, selections, addWarning);
        return state;
    }

    public static SbSceneAnimationFrameState Build(
        SbSceneFile scene,
        AnimationInfo? animation,
        double frame,
        Action<string>? addWarning = null)
    {
        ArgumentNullException.ThrowIfNull(scene);

        var state = GetCache(scene).BuildInitial();
        if (animation is not null)
        {
            ApplyAnimation(scene, state, animation, frame, addWarning);
        }

        return state;
    }

    public static void ApplyAnimations(
        SbSceneFile scene,
        SbSceneAnimationFrameState state,
        IReadOnlyList<SbSceneAnimationSelection> selections,
        Action<string>? addWarning = null)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(selections);

        if (selections.Count == 0)
        {
            return;
        }

        var slots = new SortedDictionary<int, (AnimationInfo Animation, double Frame)>();
        foreach (var selection in selections)
        {
            if (!TryResolveAnimationSelection(scene, selection, out var slotIndex, out var animation, out var warning))
            {
                addWarning?.Invoke(warning);
                continue;
            }

            slots[slotIndex] = (animation, selection.Frame);
        }

        foreach (var (_, (animation, frame)) in slots)
        {
            ApplyAnimation(scene, state, animation, frame, addWarning);
        }
    }

    private static bool TryResolveAnimationSelection(
        SbSceneFile scene,
        SbSceneAnimationSelection selection,
        out int slotIndex,
        out AnimationInfo animation,
        out string warning)
    {
        if (selection.Index is int index)
        {
            if (index >= 0 && index < scene.Surfboard.Animations.Count)
            {
                slotIndex = index;
                animation = scene.Surfboard.Animations[index];
                warning = string.Empty;
                return true;
            }

            slotIndex = -1;
            animation = null!;
            warning = $"Animation slot #{index} was not found.";
            return false;
        }

        var cache = GetCache(scene);
        if (cache.AnimationsByName.TryGetValue(selection.Name, out animation!))
        {
            slotIndex = FindAnimationSlotIndex(scene.Surfboard.Animations, animation);
            warning = string.Empty;
            return true;
        }

        slotIndex = -1;
        warning = $"Animation '{selection.Name}' was not found.";
        return false;
    }

    private static int FindAnimationSlotIndex(IReadOnlyList<AnimationInfo> animations, AnimationInfo animation)
    {
        for (var i = 0; i < animations.Count; i++)
        {
            if (ReferenceEquals(animations[i], animation))
            {
                return i;
            }
        }

        return animation.Index;
    }

    public static void ApplyAnimation(
        SbSceneFile scene,
        SbSceneAnimationFrameState state,
        AnimationInfo animation,
        double frame,
        Action<string>? addWarning = null)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(animation);

        if (!double.IsFinite(frame))
        {
            addWarning?.Invoke($"Animation '{animation.Name ?? animation.Index.ToString()}' used a non-finite frame value.");
            return;
        }

        var cache = GetCache(scene);

        foreach (var motion in animation.Motions)
        {
            var nodeIndex = ResolveMotionNodeIndex(scene.Surfboard.Nodes, cache.NodeIndexByName, motion);
            if (nodeIndex is null || nodeIndex < 0 || nodeIndex >= state.Nodes.Count)
            {
                continue;
            }

            var nodeState = state.Nodes[nodeIndex.Value];
            foreach (var track in motion.Tracks)
            {
                ApplyTrack(track, frame, nodeState, nodeIndex.Value, cache.ImageCastsByNode, state.ImageCasts);
            }
        }
    }

    private static AnimationFrameCache GetCache(SbSceneFile scene)
    {
        return Caches.GetValue(scene, static cachedScene => new AnimationFrameCache(cachedScene));
    }

    private static IReadOnlyList<SbSceneNodeAnimationState> BuildInitialNodeStates(IReadOnlyList<NodeInfo> nodes)
    {
        return nodes.Select(static node =>
        {
            var transform = node.Transform2D;
            var material = transform?.MaterialColor;
            var illumination = transform?.IlluminationColor;
            return new SbSceneNodeAnimationState
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
                IlluminationR = illumination?.R ?? SbSceneColorConventions.OpaqueBlack.R,
                IlluminationG = illumination?.G ?? SbSceneColorConventions.OpaqueBlack.G,
                IlluminationB = illumination?.B ?? SbSceneColorConventions.OpaqueBlack.B,
                IlluminationA = illumination?.A ?? SbSceneColorConventions.OpaqueBlack.A,
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

    private static IReadOnlyList<SbSceneImageCastAnimationState> BuildInitialImageStates(IReadOnlyList<SbSceneImageCast> imageCasts)
    {
        return imageCasts.Select(static imageCast => new SbSceneImageCastAnimationState
        {
            Width = imageCast.Width,
            Height = imageCast.Height,
            PrimaryReferenceIndex = ClampReferenceIndex(imageCast.PrimaryCropReferenceIndex, imageCast.PrimaryCropReferences.Count),
            SecondaryReferenceIndex = ClampReferenceIndex(imageCast.SecondaryCropReferenceIndex, imageCast.SecondaryCropReferences.Count),
        }).ToArray();
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
        SbSceneNodeAnimationState state,
        int nodeIndex,
        IReadOnlyDictionary<int, SbSceneImageCast[]> imageCastsByNode,
        IReadOnlyList<SbSceneImageCastAnimationState> imageStates)
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
            case 12 when SbSceneAnimationEvaluator.EvaluateTrack(track, frame) is double width:
                ApplyImageDimension(nodeIndex, imageCastsByNode, imageStates, width, setWidth: true);
                break;
            case 13 when SbSceneAnimationEvaluator.EvaluateTrack(track, frame) is double height:
                ApplyImageDimension(nodeIndex, imageCastsByNode, imageStates, height, setWidth: false);
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
            case >= 29 and <= 44 when SbSceneAnimationEvaluator.EvaluateTrack(track, frame) is double vertexChannel:
                ApplyVertexColorChannel(state, track.TrackType.Value, vertexChannel);
                break;
        }
    }

    private static void ApplyVertexColorChannel(SbSceneNodeAnimationState state, int trackType, double value)
    {
        var relative = trackType - 29;
        var vertexIndex = relative / 4;
        if (vertexIndex < 0 || vertexIndex >= 4)
        {
            return;
        }

        var colors = state.VertexColors.Count >= 4
            ? state.VertexColors.Take(4).ToArray()
            : state.VertexColors.Concat(Enumerable.Repeat(SbSceneColorConventions.OpaqueWhite, 4)).Take(4).ToArray();
        var channel = relative & 3;
        var color = colors[vertexIndex];
        colors[vertexIndex] = channel switch
        {
            0 => color with { R = ToByteChannel(value) },
            1 => color with { G = ToByteChannel(value) },
            2 => color with { B = ToByteChannel(value) },
            _ => color with { A = ToByteChannel(value) },
        };
        state.VertexColors = colors;
    }

    private static void ApplyImageDimension(
        int nodeIndex,
        IReadOnlyDictionary<int, SbSceneImageCast[]> imageCastsByNode,
        IReadOnlyList<SbSceneImageCastAnimationState> imageStates,
        double value,
        bool setWidth)
    {
        if (!double.IsFinite(value) || value <= 0 || !imageCastsByNode.TryGetValue(nodeIndex, out var imageCasts))
        {
            return;
        }

        foreach (var imageCast in imageCasts)
        {
            if (imageCast.Index < 0 || imageCast.Index >= imageStates.Count)
            {
                continue;
            }

            if (setWidth)
            {
                imageStates[imageCast.Index].Width = value;
            }
            else
            {
                imageStates[imageCast.Index].Height = value;
            }
        }
    }

    private static void ApplyImageReferenceIndex(
        int nodeIndex,
        IReadOnlyDictionary<int, SbSceneImageCast[]> imageCastsByNode,
        IReadOnlyList<SbSceneImageCastAnimationState> imageStates,
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

    private static int ClampReferenceIndex(int? value, int count)
    {
        return value is >= 0 && value < count ? value.Value : 0;
    }

    private static byte ToByteChannel(double value)
    {
        var scaled = value is >= 0 and <= 1.0 + Epsilon ? value * 255.0 : value;
        return (byte)Math.Clamp((int)Math.Round(scaled), byte.MinValue, byte.MaxValue);
    }

    private sealed class AnimationFrameCache
    {
        private readonly IReadOnlyList<SbSceneNodeAnimationState> _initialNodes;
        private readonly IReadOnlyList<SbSceneImageCastAnimationState> _initialImageCasts;

        public AnimationFrameCache(SbSceneFile scene)
        {
            AnimationsByName = scene.Surfboard.Animations
                .Where(static animation => !string.IsNullOrWhiteSpace(animation.Name))
                .GroupBy(static animation => animation.Name!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.OrdinalIgnoreCase);
            ImageCastsByNode = scene.Surfboard.Resources.ImageCasts
                .GroupBy(static imageCast => imageCast.CastIndex)
                .ToDictionary(static group => group.Key, static group => group.ToArray());
            NodeIndexByName = scene.Surfboard.Nodes
                .Where(static node => !string.IsNullOrWhiteSpace(node.Name))
                .GroupBy(static node => node.Name!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(static group => group.Key, static group => group.First().Index, StringComparer.OrdinalIgnoreCase);
            _initialNodes = BuildInitialNodeStates(scene.Surfboard.Nodes);
            _initialImageCasts = BuildInitialImageStates(scene.Surfboard.Resources.ImageCasts);
        }

        public IReadOnlyDictionary<string, AnimationInfo> AnimationsByName { get; }

        public IReadOnlyDictionary<int, SbSceneImageCast[]> ImageCastsByNode { get; }

        public IReadOnlyDictionary<string, int> NodeIndexByName { get; }

        public SbSceneAnimationFrameState BuildInitial()
        {
            return new SbSceneAnimationFrameState
            {
                Nodes = _initialNodes.Select(CloneNodeState).ToArray(),
                ImageCasts = _initialImageCasts.Select(CloneImageCastState).ToArray(),
            };
        }

        private static SbSceneNodeAnimationState CloneNodeState(SbSceneNodeAnimationState source)
        {
            return new SbSceneNodeAnimationState
            {
                TranslationX = source.TranslationX,
                TranslationY = source.TranslationY,
                RotationDegrees = source.RotationDegrees,
                ScaleX = source.ScaleX,
                ScaleY = source.ScaleY,
                Display = source.Display,
                MaterialR = source.MaterialR,
                MaterialG = source.MaterialG,
                MaterialB = source.MaterialB,
                MaterialA = source.MaterialA,
                IlluminationR = source.IlluminationR,
                IlluminationG = source.IlluminationG,
                IlluminationB = source.IlluminationB,
                IlluminationA = source.IlluminationA,
                VertexColors = source.VertexColors.ToArray(),
            };
        }

        private static SbSceneImageCastAnimationState CloneImageCastState(SbSceneImageCastAnimationState source)
        {
            return new SbSceneImageCastAnimationState
            {
                Width = source.Width,
                Height = source.Height,
                PrimaryReferenceIndex = source.PrimaryReferenceIndex,
                SecondaryReferenceIndex = source.SecondaryReferenceIndex,
            };
        }
    }
}
