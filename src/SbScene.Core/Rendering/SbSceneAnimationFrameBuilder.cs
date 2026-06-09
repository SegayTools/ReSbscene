using SbScene.Core.Resources;
using SbScene.Core.Semantics;
using System.Runtime.CompilerServices;

namespace SbScene.Core.Rendering;

/// <summary>
/// 表示某一帧的完整动画状态，包含节点状态和 image cast 状态。
/// </summary>
public sealed class SbSceneAnimationFrameState
{
    /// <summary>
    /// 获取或设置节点集合，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required IReadOnlyList<SbSceneNodeAnimationState> Nodes { get; init; }

    /// <summary>
    /// 获取或设置图像Casts，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required IReadOnlyList<SbSceneImageCastAnimationState> ImageCasts { get; init; }
}

/// <summary>
/// 表示单个节点在某一帧上的变换、显示和颜色状态。
/// </summary>
public sealed class SbSceneNodeAnimationState
{
    /// <summary>
    /// 获取或设置平移X，用于表示坐标、尺寸或向量分量，参与变换和导出计算。
    /// </summary>
    public double TranslationX { get; set; }

    /// <summary>
    /// 获取或设置平移Y，用于描述位置、旋转、缩放或矩阵状态，参与渲染坐标和导出坐标计算。
    /// </summary>
    public double TranslationY { get; set; }

    /// <summary>
    /// 获取或设置旋转Degrees，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public double RotationDegrees { get; set; }

    /// <summary>
    /// 获取或设置输出缩放比例X，用于确定渲染区域、裁剪范围、采样质量或输出尺寸。
    /// </summary>
    public double ScaleX { get; set; }

    /// <summary>
    /// 获取或设置输出缩放比例Y，用于确定渲染区域、裁剪范围、采样质量或输出尺寸。
    /// </summary>
    public double ScaleY { get; set; }

    /// <summary>
    /// 获取或设置Display，用于描述位置、旋转、缩放或矩阵状态，参与渲染坐标和导出坐标计算。
    /// </summary>
    public bool Display { get; set; }

    /// <summary>
    /// 获取或设置材质红色通道值，用于参与颜色、透明度、照明或混合计算。
    /// </summary>
    public byte MaterialR { get; set; }

    /// <summary>
    /// 获取或设置材质绿色通道值，用于参与颜色、透明度、照明或混合计算。
    /// </summary>
    public byte MaterialG { get; set; }

    /// <summary>
    /// 获取或设置材质蓝色通道值，用于参与颜色、透明度、照明或混合计算。
    /// </summary>
    public byte MaterialB { get; set; }

    /// <summary>
    /// 获取或设置材质Alpha 透明度通道值，用于参与颜色、透明度、照明或混合计算。
    /// </summary>
    public byte MaterialA { get; set; }

    /// <summary>
    /// 获取或设置照明红色通道值，用于参与颜色、透明度、照明或混合计算。
    /// </summary>
    public byte IlluminationR { get; set; }

    /// <summary>
    /// 获取或设置照明绿色通道值，用于参与颜色、透明度、照明或混合计算。
    /// </summary>
    public byte IlluminationG { get; set; }

    /// <summary>
    /// 获取或设置照明蓝色通道值，用于参与颜色、透明度、照明或混合计算。
    /// </summary>
    public byte IlluminationB { get; set; }

    /// <summary>
    /// 获取或设置照明Alpha 透明度通道值，用于参与颜色、透明度、照明或混合计算。
    /// </summary>
    public byte IlluminationA { get; set; }

    /// <summary>
    /// 获取或设置Vertex颜色集合，用于参与颜色、透明度、照明或混合计算。
    /// </summary>
    public required IReadOnlyList<RgbaColor> VertexColors { get; set; }

    /// <summary>
    /// 表示材质颜色，用于参与颜色、透明度、照明或混合计算。
    /// </summary>
    public RgbaColor MaterialColor => new(MaterialR, MaterialG, MaterialB, MaterialA);

    /// <summary>
    /// 表示照明颜色，用于参与颜色、透明度、照明或混合计算。
    /// </summary>
    public RgbaColor IlluminationColor => new(IlluminationR, IlluminationG, IlluminationB, IlluminationA);
}

/// <summary>
/// 表示 image cast 在某一帧上的尺寸、引用和翻转状态。
/// </summary>
public sealed class SbSceneImageCastAnimationState
{
    /// <summary>
    /// 获取或设置宽度，用于确定渲染区域、裁剪范围、采样质量或输出尺寸。
    /// </summary>
    public double Width { get; set; }

    /// <summary>
    /// 获取或设置高度，用于确定渲染区域、裁剪范围、采样质量或输出尺寸。
    /// </summary>
    public double Height { get; set; }

    /// <summary>
    /// 获取或设置Primary引用索引，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public int PrimaryReferenceIndex { get; set; }

    /// <summary>
    /// 获取或设置Secondary引用索引，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public int SecondaryReferenceIndex { get; set; }
}

/// <summary>
/// 提供sbscene 场景动画帧构建器，负责构建渲染、导出或诊断流程需要的中间状态。
/// </summary>
public static class SbSceneAnimationFrameBuilder
{
    private const double Epsilon = 0.000001;
    private static readonly ConditionalWeakTable<SbSceneFile, AnimationFrameCache> Caches = new();

    /// <summary>
    /// 构建场景的初始动画帧状态，用于渲染未套用动画时的节点和图像状态。
    /// </summary>
    /// <param name="scene">已解析的 sbscene 场景模型。</param>
    /// <returns>包含节点和 image cast 动画值的帧状态。</returns>
    public static SbSceneAnimationFrameState BuildInitial(SbSceneFile scene)
    {
        ArgumentNullException.ThrowIfNull(scene);

        return GetCache(scene).BuildInitial();
    }

    /// <summary>
    /// 根据动画选择集合构建指定帧的场景动画状态。
    /// </summary>
    /// <param name="scene">已解析的 sbscene 场景模型。</param>
    /// <param name="selections">参与本次处理的一组结构化条目。</param>
    /// <param name="addWarning">接收诊断日志或非致命警告的回调。</param>
    /// <returns>包含已应用选择集合的节点和 image cast 动画值的帧状态。</returns>
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

    /// <summary>
    /// 根据单个动画和帧号构建场景动画状态。
    /// </summary>
    /// <param name="scene">已解析的 sbscene 场景模型。</param>
    /// <param name="animation">参与本次处理的动画。</param>
    /// <param name="frame">要采样或渲染的动画帧位置。</param>
    /// <param name="addWarning">接收诊断日志或非致命警告的回调。</param>
    /// <returns>包含该动画在指定帧上采样结果的帧状态。</returns>
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

    /// <summary>
    /// 应用动画集合，将动画、变换或修补规则写入目标状态。
    /// </summary>
    /// <param name="scene">已解析的 sbscene 场景模型。</param>
    /// <param name="state">要写入动画结果的可变帧状态。</param>
    /// <param name="selections">参与本次处理的一组结构化条目。</param>
    /// <param name="addWarning">接收诊断日志或非致命警告的回调。</param>
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

    /// <summary>
    /// 尝试Resolve动画Selection，并通过返回值或输出参数报告是否成功。
    /// </summary>
    /// <param name="scene">已解析的 sbscene 场景模型。</param>
    /// <param name="selection">要解析的动画槽位或名称选择。</param>
    /// <param name="slotIndex">参与几何边界、坐标或变换计算的位置值。</param>
    /// <param name="animation">参与本次处理的动画。</param>
    /// <param name="warning">接收诊断日志或非致命警告的回调。</param>
    /// <returns>如果条件成立则为 true；否则为 false。</returns>
    public static bool TryResolveAnimationSelection(
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

    /// <summary>
    /// 应用动画，将动画、变换或修补规则写入目标状态。
    /// </summary>
    /// <param name="scene">已解析的 sbscene 场景模型。</param>
    /// <param name="state">要写入单个动画采样结果的可变帧状态。</param>
    /// <param name="animation">参与本次处理的动画。</param>
    /// <param name="frame">要采样或渲染的动画帧位置。</param>
    /// <param name="addWarning">接收诊断日志或非致命警告的回调。</param>
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

        /// <summary>
        /// 初始化动画帧Cache 实例，并保存调用方提供的核心数据。
        /// </summary>
        /// <param name="scene">已解析的 sbscene 场景模型。</param>
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

        /// <summary>
        /// 获取动画集合By名称，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, AnimationInfo> AnimationsByName { get; }

        /// <summary>
        /// 获取图像CastsBy节点，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public IReadOnlyDictionary<int, SbSceneImageCast[]> ImageCastsByNode { get; }

        /// <summary>
        /// 获取节点索引By名称，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> NodeIndexByName { get; }

        /// <summary>
        /// 克隆缓存中的初始节点和 image cast 状态，供单次采样独立修改。
        /// </summary>
        /// <returns>可安全修改的初始动画帧状态副本。</returns>
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
