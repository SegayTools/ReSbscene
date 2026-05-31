using SbScene.Core.Resources;
using SbScene.Core.Semantics;

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

    public required IReadOnlyList<RgbaColor> VertexColors { get; init; }

    public RgbaColor MaterialColor => new(MaterialR, MaterialG, MaterialB, MaterialA);

    public RgbaColor IlluminationColor => new(IlluminationR, IlluminationG, IlluminationB, IlluminationA);
}

public sealed class SbSceneImageCastAnimationState
{
    public int PrimaryReferenceIndex { get; set; }

    public int SecondaryReferenceIndex { get; set; }
}

public static class SbSceneAnimationFrameBuilder
{
    private const double Epsilon = 0.000001;

    public static SbSceneAnimationFrameState BuildInitial(SbSceneFile scene)
    {
        ArgumentNullException.ThrowIfNull(scene);

        return new SbSceneAnimationFrameState
        {
            Nodes = BuildInitialNodeStates(scene.Surfboard.Nodes),
            ImageCasts = BuildInitialImageStates(scene.Surfboard.Resources.ImageCasts),
        };
    }

    public static SbSceneAnimationFrameState Build(
        SbSceneFile scene,
        IReadOnlyList<SbSceneAnimationSelection> selections,
        Action<string>? addWarning = null)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(selections);

        var state = BuildInitial(scene);
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

        var state = BuildInitial(scene);
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

        var animationsByName = scene.Surfboard.Animations
            .Where(static animation => !string.IsNullOrWhiteSpace(animation.Name))
            .GroupBy(static animation => animation.Name!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var selection in selections)
        {
            if (!animationsByName.TryGetValue(selection.Name, out var animation))
            {
                addWarning?.Invoke($"Animation '{selection.Name}' was not found.");
                continue;
            }

            ApplyAnimation(scene, state, animation, selection.Frame, addWarning);
        }
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

        var imageCastsByNode = scene.Surfboard.Resources.ImageCasts
            .GroupBy(static imageCast => imageCast.CastIndex)
            .ToDictionary(static group => group.Key, static group => group.ToArray());
        var nodeIndexByName = scene.Surfboard.Nodes
            .Where(static node => !string.IsNullOrWhiteSpace(node.Name))
            .GroupBy(static node => node.Name!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.First().Index, StringComparer.OrdinalIgnoreCase);

        foreach (var motion in animation.Motions)
        {
            var nodeIndex = ResolveMotionNodeIndex(scene.Surfboard.Nodes, nodeIndexByName, motion);
            if (nodeIndex is null || nodeIndex < 0 || nodeIndex >= state.Nodes.Count)
            {
                continue;
            }

            var nodeState = state.Nodes[nodeIndex.Value];
            foreach (var track in motion.Tracks)
            {
                ApplyTrack(track, frame, nodeState, nodeIndex.Value, imageCastsByNode, state.ImageCasts);
            }
        }
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

    private static IReadOnlyList<SbSceneImageCastAnimationState> BuildInitialImageStates(IReadOnlyList<SbSceneImageCast> imageCasts)
    {
        return imageCasts.Select(static imageCast => new SbSceneImageCastAnimationState
        {
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
}
