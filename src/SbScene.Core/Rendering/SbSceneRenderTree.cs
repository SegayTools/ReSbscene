using SbScene.Core.Semantics;

namespace SbScene.Core.Rendering;

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

    private static double ClampOpacity(double opacity)
    {
        return double.IsFinite(opacity) ? Math.Clamp(opacity, MinOpacity, MaxOpacity) : MaxOpacity;
    }
}
