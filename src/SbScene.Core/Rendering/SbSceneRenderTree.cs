using SbScene.Core.Semantics;

namespace SbScene.Core.Rendering;

/// <summary>
/// 表示节点继承后的材质色和照明色，用于渲染最终颜色。
/// </summary>
public sealed class SbSceneResolvedNodeColorState
{
    /// <summary>
    /// 获取或设置材质颜色，用于参与颜色、透明度、照明或混合计算。
    /// </summary>
    public required RgbaColor MaterialColor { get; init; }

    /// <summary>
    /// 获取或设置照明颜色，用于参与颜色、透明度、照明或混合计算。
    /// </summary>
    public required RgbaColor IlluminationColor { get; init; }
}

/// <summary>
/// 提供场景渲染树辅助方法，用于父子关系、可见性、不透明度和颜色继承计算。
/// </summary>
public static class SbSceneRenderTree
{
    private const double MinOpacity = 0.0;
    private const double MaxOpacity = 1.0;

    /// <summary>
    /// 构建父级Map，为渲染、导出或诊断流程准备中间状态。
    /// </summary>
    /// <param name="nodes">参与本次处理的一组结构化条目。</param>
    /// <returns>子节点索引到父节点索引的映射。</returns>
    public static IReadOnlyDictionary<int, int> BuildParentMap(IReadOnlyList<NodeInfo> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        var parentByNode = new Dictionary<int, int>();
        for (var parentIndex = 0; parentIndex < nodes.Count; parentIndex++)
        {
            var seen = new HashSet<int>();
            var childIndex = nodes[parentIndex].ChildIndex;
            while (childIndex is int index && index >= 0 && index < nodes.Count && seen.Add(index))
            {
                parentByNode.TryAdd(index, parentIndex);
                childIndex = nodes[index].SiblingIndex;
            }
        }

        return parentByNode;
    }

    /// <summary>
    /// 结合节点自身显示状态和父子层级，计算最终可见性列表。
    /// </summary>
    /// <param name="nodes">要计算可见性的场景节点列表。</param>
    /// <param name="parentByNode">节点索引到父节点索引的映射。</param>
    /// <param name="isLocallyVisible">判断单个节点自身是否可见的回调。</param>
    /// <param name="showHiddenNodes">指示是否忽略隐藏标记并强制显示全部节点。</param>
    /// <returns>与节点列表顺序一致的最终可见性布尔列表。</returns>
    public static IReadOnlyList<bool> BuildFinalVisibility(
        IReadOnlyList<NodeInfo> nodes,
        IReadOnlyDictionary<int, int> parentByNode,
        Func<int, bool> isLocallyVisible,
        bool showHiddenNodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(parentByNode);
        ArgumentNullException.ThrowIfNull(isLocallyVisible);

        if (showHiddenNodes)
        {
            return Enumerable.Repeat(true, nodes.Count).ToArray();
        }

        var memo = new Dictionary<int, bool>();
        var visiting = new HashSet<int>();

        bool Resolve(int index)
        {
            if (memo.TryGetValue(index, out var cached))
            {
                return cached;
            }

            if (!visiting.Add(index))
            {
                return isLocallyVisible(index);
            }

            var visible = isLocallyVisible(index);
            if (visible && parentByNode.TryGetValue(index, out var parentIndex) && parentIndex >= 0 && parentIndex < nodes.Count)
            {
                visible = Resolve(parentIndex);
            }

            visiting.Remove(index);
            memo[index] = visible;
            return visible;
        }

        return Enumerable.Range(0, nodes.Count).Select(Resolve).ToArray();
    }

    /// <summary>
    /// 沿父子层级累乘节点不透明度，计算每个节点的最终不透明度。
    /// </summary>
    /// <param name="nodes">参与本次处理的一组结构化条目。</param>
    /// <param name="parentByNode">参与几何边界、坐标或变换计算的位置值。</param>
    /// <param name="getLocalOpacity">参与几何边界、坐标或变换计算的位置值。</param>
    /// <returns>与节点列表顺序一致的最终不透明度列表。</returns>
    public static IReadOnlyList<double> BuildEffectiveOpacity(
        IReadOnlyList<NodeInfo> nodes,
        IReadOnlyDictionary<int, int> parentByNode,
        Func<int, double> getLocalOpacity)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(parentByNode);
        ArgumentNullException.ThrowIfNull(getLocalOpacity);

        var memo = new Dictionary<int, double>();
        var visiting = new HashSet<int>();

        double Resolve(int index)
        {
            if (memo.TryGetValue(index, out var cached))
            {
                return cached;
            }

            var localOpacity = ClampOpacity(getLocalOpacity(index));
            if (!visiting.Add(index))
            {
                return localOpacity;
            }

            var opacity = localOpacity;
            if (parentByNode.TryGetValue(index, out var parentIndex) && parentIndex >= 0 && parentIndex < nodes.Count)
            {
                opacity *= Resolve(parentIndex);
            }

            visiting.Remove(index);
            memo[index] = opacity;
            return opacity;
        }

        return Enumerable.Range(0, nodes.Count).Select(Resolve).ToArray();
    }

    /// <summary>
    /// 沿父子层级合成材质色和照明色，计算每个节点的最终颜色状态。
    /// </summary>
    /// <param name="nodes">参与本次处理的一组结构化条目。</param>
    /// <param name="parentByNode">参与几何边界、坐标或变换计算的位置值。</param>
    /// <param name="getLocalMaterialColor">参与颜色、透明度或混合计算的通道值。</param>
    /// <param name="getLocalIlluminationColor">参与颜色、透明度或混合计算的通道值。</param>
    /// <returns>与节点列表顺序一致的最终材质色和照明色列表。</returns>
    public static IReadOnlyList<SbSceneResolvedNodeColorState> BuildEffectiveColors(
        IReadOnlyList<NodeInfo> nodes,
        IReadOnlyDictionary<int, int> parentByNode,
        Func<int, RgbaColor> getLocalMaterialColor,
        Func<int, RgbaColor> getLocalIlluminationColor)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(parentByNode);
        ArgumentNullException.ThrowIfNull(getLocalMaterialColor);
        ArgumentNullException.ThrowIfNull(getLocalIlluminationColor);

        var memo = new Dictionary<int, SbSceneResolvedNodeColorState>();
        var visiting = new HashSet<int>();

        SbSceneResolvedNodeColorState Resolve(int index)
        {
            if (memo.TryGetValue(index, out var cached))
            {
                return cached;
            }

            var local = new SbSceneResolvedNodeColorState
            {
                MaterialColor = getLocalMaterialColor(index),
                IlluminationColor = getLocalIlluminationColor(index),
            };
            if (!visiting.Add(index))
            {
                return local;
            }

            var result = local;
            if (parentByNode.TryGetValue(index, out var parentIndex) && parentIndex >= 0 && parentIndex < nodes.Count)
            {
                var parent = Resolve(parentIndex);
                result = new SbSceneResolvedNodeColorState
                {
                    MaterialColor = Multiply(parent.MaterialColor, local.MaterialColor),
                    IlluminationColor = SaturatingAdd(parent.IlluminationColor, local.IlluminationColor),
                };
            }

            visiting.Remove(index);
            memo[index] = result;
            return result;
        }

        return Enumerable.Range(0, nodes.Count).Select(Resolve).ToArray();
    }

    private static RgbaColor Multiply(RgbaColor left, RgbaColor right)
    {
        return new RgbaColor(
            MultiplyChannel(left.R, right.R),
            MultiplyChannel(left.G, right.G),
            MultiplyChannel(left.B, right.B),
            MultiplyChannel(left.A, right.A));
    }

    private static RgbaColor SaturatingAdd(RgbaColor left, RgbaColor right)
    {
        return new RgbaColor(
            SaturatingAddChannel(left.R, right.R),
            SaturatingAddChannel(left.G, right.G),
            SaturatingAddChannel(left.B, right.B),
            SaturatingAddChannel(left.A, right.A));
    }

    private static byte MultiplyChannel(byte left, byte right)
    {
        return (byte)Math.Clamp((int)Math.Round(left * right / 255.0), byte.MinValue, byte.MaxValue);
    }

    private static byte SaturatingAddChannel(byte left, byte right)
    {
        return (byte)Math.Min(byte.MaxValue, left + right);
    }

    private static double ClampOpacity(double opacity)
    {
        return double.IsFinite(opacity) ? Math.Clamp(opacity, MinOpacity, MaxOpacity) : MaxOpacity;
    }
}
