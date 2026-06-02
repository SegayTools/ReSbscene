using SbScene.Core.Semantics;

namespace SbScene.Core.Rendering;

public sealed class SbSceneResolvedNodeColorState
{
    public required RgbaColor MaterialColor { get; init; }

    public required RgbaColor IlluminationColor { get; init; }
}

public static class SbSceneRenderTree
{
    private const double MinOpacity = 0.0;
    private const double MaxOpacity = 1.0;

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
