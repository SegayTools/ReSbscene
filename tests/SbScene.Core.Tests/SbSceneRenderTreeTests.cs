using SbScene.Core.Rendering;
using SbScene.Core.Semantics;

namespace SbScene.Core.Tests;

public sealed class SbSceneRenderTreeTests
{
    [Fact]
    public void BuildFinalVisibilityInheritsHiddenParents()
    {
        var nodes = new[]
        {
            Node(0, childIndex: 1, display: false),
            Node(1, childIndex: 2),
            Node(2),
        };
        var parentByNode = SbSceneRenderTree.BuildParentMap(nodes);

        var visibility = SbSceneRenderTree.BuildFinalVisibility(
            nodes,
            parentByNode,
            index => nodes[index].Transform2D?.Display != false,
            showHiddenNodes: false);

        Assert.Equal([false, false, false], visibility);
    }

    [Fact]
    public void BuildFinalVisibilityCanForceHiddenNodesVisible()
    {
        var nodes = new[]
        {
            Node(0, childIndex: 1, display: false),
            Node(1),
        };
        var parentByNode = SbSceneRenderTree.BuildParentMap(nodes);

        var visibility = SbSceneRenderTree.BuildFinalVisibility(
            nodes,
            parentByNode,
            index => nodes[index].Transform2D?.Display != false,
            showHiddenNodes: true);

        Assert.Equal([true, true], visibility);
    }

    [Fact]
    public void BuildEffectiveOpacityMultipliesAncestorAlpha()
    {
        var nodes = new[]
        {
            Node(0, childIndex: 1, alpha: 128),
            Node(1, childIndex: 2, alpha: 64),
            Node(2, alpha: 255),
        };
        var parentByNode = SbSceneRenderTree.BuildParentMap(nodes);

        var opacity = SbSceneRenderTree.BuildEffectiveOpacity(
            nodes,
            parentByNode,
            index => (nodes[index].Transform2D?.MaterialColor?.A ?? (byte)255) / 255.0);

        Assert.Equal(128 / 255.0, opacity[0], precision: 6);
        Assert.Equal(128 / 255.0 * (64 / 255.0), opacity[1], precision: 6);
        Assert.Equal(128 / 255.0 * (64 / 255.0), opacity[2], precision: 6);
    }

    [Fact]
    public void BuildEffectiveColorsMultipliesMaterialAndAddsIllumination()
    {
        var nodes = new[]
        {
            Node(0, childIndex: 1, material: new RgbaColor(128, 64, 255, 255), illumination: new RgbaColor(200, 20, 0, 200)),
            Node(1, material: new RgbaColor(128, 255, 64, 255), illumination: new RgbaColor(100, 40, 10, 100)),
        };
        var parentByNode = SbSceneRenderTree.BuildParentMap(nodes);

        var colors = SbSceneRenderTree.BuildEffectiveColors(
            nodes,
            parentByNode,
            index => ToRgba(nodes[index].Transform2D!.MaterialColor!),
            index => ToRgba(nodes[index].Transform2D!.IlluminationColor!));

        Assert.Equal(new RgbaColor(64, 64, 64, 255), colors[1].MaterialColor);
        Assert.Equal(new RgbaColor(255, 60, 10, 255), colors[1].IlluminationColor);
    }

    private static NodeInfo Node(
        int index,
        int? childIndex = null,
        int? siblingIndex = null,
        bool display = true,
        byte alpha = 255,
        RgbaColor? material = null,
        RgbaColor? illumination = null)
    {
        var materialColor = material ?? new RgbaColor(255, 255, 255, alpha);
        var illuminationColor = illumination ?? RgbaColor.Transparent;
        return new NodeInfo
        {
            Index = index,
            Offset = 0,
            Path = $"NODE[{index}]",
            Name = $"node_{index}",
            Flags = null,
            FlagBits = [],
            ChildIndex = childIndex,
            SiblingIndex = siblingIndex,
            Comment = null,
            CategoryId = null,
            Group = string.Empty,
            Transform2D = new Transform2DInfo
            {
                Index = index,
                Offset = 0,
                Path = $"TRS2[{index}]",
                Translation = null,
                RotationZ = null,
                RotationZRaw = null,
                RotationZDegreesCandidate = null,
                Scale = null,
                Display = display,
                MaterialColor = new ColorArgbValue
                {
                    A = materialColor.A,
                    R = materialColor.R,
                    G = materialColor.G,
                    B = materialColor.B,
                },
                IlluminationColor = new ColorArgbValue
                {
                    A = illuminationColor.A,
                    R = illuminationColor.R,
                    G = illuminationColor.G,
                    B = illuminationColor.B,
                },
                VertexColors = [],
                MultiPosFlags = null,
                MultiSizeFlags = null,
                Fields = [],
            },
            HasTransform2 = true,
            HasTransform3 = false,
            HasData = false,
            HasCategory = false,
            StringFields = [],
            NumericFields = [],
            ChildTags = [],
        };
    }

    private static RgbaColor ToRgba(ColorArgbValue color)
    {
        return new RgbaColor(color.R, color.G, color.B, color.A);
    }
}
