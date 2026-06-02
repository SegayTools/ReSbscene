using SbScene.Core.Rendering;
using SbScene.Core.Resources;
using SbScene.Core.Semantics;
using SbScene.Core.Vtbf;

namespace SbScene.Core.Tests;

public sealed class SbSceneAnimationFrameBuilderTests
{
    [Fact]
    public void BuildAppliesAnimationTracksToFrameState()
    {
        var animation = Animation(
            "Test",
            Motion(
                castIndex: 0,
                Track(trackType: 0, flags: 0x13, Key(0, 0), Key(10, 20)),
                Track(trackType: 11, flags: 0x33, Key(0, 1), Key(10, 0)),
                Track(trackType: 12, flags: 0x13, Key(0, 16), Key(10, 32)),
                Track(trackType: 13, flags: 0x13, Key(0, 16), Key(10, 8)),
                Track(trackType: 18, flags: 0x23, Key(0, 0), Key(10, 2)),
                Track(trackType: 24, flags: 0x13, Key(0, 1), Key(10, 0.5))));
        var scene = Scene(animation);

        var state = SbSceneAnimationFrameBuilder.Build(
            scene,
            [new SbSceneAnimationSelection("Test", 10)]);

        Assert.Equal(20, state.Nodes[0].TranslationX, precision: 6);
        Assert.False(state.Nodes[0].Display);
        Assert.Equal(128, state.Nodes[0].MaterialA);
        Assert.Equal(32, state.ImageCasts[0].Width, precision: 6);
        Assert.Equal(8, state.ImageCasts[0].Height, precision: 6);
        Assert.Equal(2, state.ImageCasts[0].PrimaryReferenceIndex);
    }

    [Fact]
    public void BuildCanApplySpecificAnimationInstance()
    {
        var first = Animation("Duplicate", Motion(castIndex: 0, Track(trackType: 0, flags: 0x13, Key(0, 0), Key(10, 10))));
        var second = Animation("Duplicate", Motion(castIndex: 0, Track(trackType: 0, flags: 0x13, Key(0, 0), Key(10, 30))));
        var scene = Scene(first, second);

        var state = SbSceneAnimationFrameBuilder.Build(scene, second, 10);

        Assert.Equal(30, state.Nodes[0].TranslationX, precision: 6);
    }

    [Fact]
    public void BuildAppliesMultipleAnimationsInSlotIndexOrder()
    {
        var fashion = Animation(
            "Change_Fashion",
            Motion(
                castIndex: 0,
                Track(trackType: 0, flags: 0x13, Key(0, 0), Key(2, 8)),
                Track(trackType: 1, flags: 0x13, Key(0, 0), Key(2, 6))));
        var action = Animation(
            "Action_Joy3",
            Motion(castIndex: 0, Track(trackType: 0, flags: 0x13, Key(0, 0), Key(10, 20))));
        var scene = Scene(fashion, action);

        var state = SbSceneAnimationFrameBuilder.Build(
            scene,
            [
                new SbSceneAnimationSelection("Action_Joy3", 10),
                new SbSceneAnimationSelection("Change_Fashion", 2),
            ]);

        Assert.Equal(20, state.Nodes[0].TranslationX, precision: 6);
        Assert.Equal(6, state.Nodes[0].TranslationY, precision: 6);
    }

    [Fact]
    public void BuildUsesLastSelectionForSameAnimationSlot()
    {
        var animation = Animation(
            "Action",
            Motion(castIndex: 0, Track(trackType: 0, flags: 0x13, Key(0, 0), Key(10, 20))));
        var scene = Scene(animation);

        var state = SbSceneAnimationFrameBuilder.Build(
            scene,
            [
                new SbSceneAnimationSelection("Action", 0),
                new SbSceneAnimationSelection("Action", 10),
            ]);

        Assert.Equal(20, state.Nodes[0].TranslationX, precision: 6);
    }

    [Fact]
    public void BuildCanApplyAnimationSelectionBySlotIndex()
    {
        var first = Animation("Duplicate", Motion(castIndex: 0, Track(trackType: 0, flags: 0x13, Key(0, 0), Key(10, 10))));
        var second = Animation("Duplicate", Motion(castIndex: 0, Track(trackType: 0, flags: 0x13, Key(0, 0), Key(10, 30))));
        var scene = Scene(first, second);

        var state = SbSceneAnimationFrameBuilder.Build(
            scene,
            [new SbSceneAnimationSelection("#1", 10) { Index = 1 }]);

        Assert.Equal(30, state.Nodes[0].TranslationX, precision: 6);
    }

    [Fact]
    public void BuildAppliesVertexColorTracks()
    {
        var animation = Animation(
            "Vertex",
            Motion(
                castIndex: 0,
                Track(trackType: 29, flags: 0x13, Key(0, 1), Key(10, 0.25)),
                Track(trackType: 30, flags: 0x13, Key(0, 1), Key(10, 0.5)),
                Track(trackType: 31, flags: 0x13, Key(0, 1), Key(10, 0.75)),
                Track(trackType: 32, flags: 0x13, Key(0, 1), Key(10, 0.125))));
        var scene = Scene(animation);

        var state = SbSceneAnimationFrameBuilder.Build(scene, animation, 10);

        Assert.Equal(new RgbaColor(64, 128, 191, 32), state.Nodes[0].VertexColors[0]);
        Assert.Equal(SbSceneColorConventions.OpaqueWhite, state.Nodes[0].VertexColors[1]);
    }

    [Fact]
    public void BuildInitialReturnsCleanCloneAfterAnimationStateWasMutated()
    {
        var animation = Animation("Action", Motion(castIndex: 0, Track(trackType: 0, flags: 0x13, Key(0, 0), Key(10, 20))));
        var scene = Scene(animation);
        var animated = SbSceneAnimationFrameBuilder.Build(scene, animation, 10);

        var initial = SbSceneAnimationFrameBuilder.BuildInitial(scene);

        Assert.Equal(20, animated.Nodes[0].TranslationX, precision: 6);
        Assert.Equal(0, initial.Nodes[0].TranslationX, precision: 6);
    }

    [Fact]
    public void BuildInitialUsesOpaqueBlackIlluminationWhenFieldIsMissing()
    {
        var scene = SceneWithNodes([Node(0, includeIllumination: false)]);

        var state = SbSceneAnimationFrameBuilder.BuildInitial(scene);

        Assert.Equal(SbSceneColorConventions.OpaqueBlack, state.Nodes[0].IlluminationColor);
    }

    private static SbSceneFile Scene(params AnimationInfo[] animations)
    {
        var nodes = new[] { Node(0) };
        return SceneWithNodes(nodes, animations);
    }

    private static SbSceneFile SceneWithNodes(IReadOnlyList<NodeInfo> nodes, params AnimationInfo[] animations)
    {
        return new SbSceneFile
        {
            SourcePath = "test.sbscene",
            SourceSize = 0,
            Vtbf = new VtbfDocument
            {
                Magic = "VTBF",
                Length = 0,
                Blocks = [],
                BlockCounts = new Dictionary<string, int>(),
                Warnings = [],
            },
            Surfboard = new SurfboardModel
            {
                Objects = [],
                Nodes = nodes,
                Transform2DRecords = [nodes[0].Transform2D!],
                NodeCategoryRecords = [],
                NodeCategoryDetails = [],
                NodeGroups = [],
                Resources = new SbSceneResourceMap
                {
                    Atlases = [],
                    ImageCasts = [ImageCast(0, 0)],
                    CnumRecords = [],
                    CrfdRecords = [],
                    TextRecords = [],
                    SliceCasts = [],
                },
                Camera = null,
                Animations = animations,
                AnimationBindings = [],
                VariantHints = [],
                UnknownFields = [],
            },
            Summary = new ParseSummary
            {
                RootBlockCount = 0,
                TotalBlockCount = 0,
                NodeCount = nodes.Count,
                AnimationCount = animations.Length,
                VariantHintCount = 0,
                BlockCounts = new Dictionary<string, int>(),
                Warnings = [],
            },
        };
    }

    private static NodeInfo Node(int index, bool includeIllumination = true)
    {
        return new NodeInfo
        {
            Index = index,
            Offset = 0,
            Path = $"NODE[{index}]",
            Name = $"node_{index}",
            Flags = null,
            FlagBits = [],
            ChildIndex = null,
            SiblingIndex = null,
            Comment = null,
            CategoryId = null,
            Group = "test",
            Transform2D = new Transform2DInfo
            {
                Index = index,
                Offset = 0,
                Path = $"TRS2[{index}]",
                Translation = new Vector2Value { X = 0, Y = 0 },
                RotationZ = null,
                RotationZRaw = null,
                RotationZDegreesCandidate = 0,
                Scale = new Vector2Value { X = 1, Y = 1 },
                Display = true,
                MaterialColor = new ColorArgbValue { A = 255, R = 255, G = 255, B = 255 },
                IlluminationColor = includeIllumination ? new ColorArgbValue { A = 0, R = 0, G = 0, B = 0 } : null,
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

    private static SbSceneImageCast ImageCast(int index, int castIndex)
    {
        return new SbSceneImageCast
        {
            Index = index,
            Offset = 0,
            ImageCastFlags = 0,
            ImageCastFlagBits = [],
            CastIndex = castIndex,
            NodeName = $"node_{castIndex}",
            Width = 16,
            Height = 16,
            PivotX = 0,
            PivotY = 0,
            DeclaredCropReferenceCount = 3,
            PrimaryCropReferenceCount = 3,
            SecondaryCropReferenceCount = null,
            SecondaryCropFlag = null,
            PrimaryCropIndex = null,
            SecondaryCropIndex = null,
            PrimaryCropReferenceIndex = 0,
            SecondaryCropReferenceIndex = null,
            CropReferenceCountMatches = true,
            CropIndexValues = [],
            CropRefCounts = [],
            PrimaryCropReferences = [CropReference(0), CropReference(1), CropReference(2)],
            SecondaryCropReferences = [],
            CropReferences = [CropReference(0), CropReference(1), CropReference(2)],
        };
    }

    private static SbSceneCropReference CropReference(int index)
    {
        return new SbSceneCropReference
        {
            Index = index,
            RawHex = string.Empty,
            Kind = 0,
            TextureListIndex = 0,
            TextureIndex = 0,
            CropIndex = index,
            AtlasName = null,
            CropPath = null,
        };
    }

    private static AnimationInfo Animation(string name, params MotionInfo[] motions)
    {
        return new AnimationInfo
        {
            Index = 0,
            Offset = 0,
            Path = "ANIM[0]",
            Name = name,
            StringFields = [],
            NumericFields = [],
            Motions = motions,
        };
    }

    private static MotionInfo Motion(int castIndex, params TrackInfo[] tracks)
    {
        return new MotionInfo
        {
            Index = 0,
            Offset = 0,
            Path = "MOT[0]",
            Name = null,
            TargetName = null,
            TargetIndex = null,
            CastIndex = castIndex,
            DeclaredTrackCount = tracks.Length,
            StringFields = [],
            NumericFields = [],
            Tracks = tracks,
        };
    }

    private static TrackInfo Track(int trackType, int flags, params KeyframeInfo[] keyframes)
    {
        return new TrackInfo
        {
            Index = 0,
            Offset = 0,
            Path = "TRK[0]",
            Name = null,
            TrackId = null,
            TrackType = trackType,
            TrackTypeName = null,
            ValueType = null,
            ValueTypeName = null,
            DeclaredKeyCountFromTrack = keyframes.Length,
            DeclaredKeyCountFromKeyBlock = keyframes.Length,
            KeyCountMatchesDeclaration = true,
            Flags = flags,
            KeyValueStorage = null,
            TargetIndex = null,
            FirstFrame = keyframes.Length > 0 ? keyframes[0].KeyFrame : null,
            LastFrame = keyframes.Length > 0 ? keyframes[^1].KeyFrame : null,
            DeclaredKeyCount = keyframes.Length,
            IsLikelyStateTrack = false,
            StringFields = [],
            NumericFields = [],
            Keyframes = keyframes,
        };
    }

    private static KeyframeInfo Key(int frame, double value)
    {
        return new KeyframeInfo
        {
            Index = frame,
            Offset = 0,
            Path = $"KEY[{frame}]",
            Fields = [],
            KeyFrame = frame,
            ScalarValue = value,
            BoolValue = null,
            PackedAngleRaw = null,
            PackedAngleDegreesCandidate = null,
            KeyValueTypeHex = null,
            KeyValueTypeName = null,
            KeyValueKind = null,
            Interpolation = 1,
            InterpolationName = null,
            TangentIn = null,
            TangentOut = null,
            TimeCandidates = [],
            ValueCandidates = [value],
            Preview = null,
        };
    }
}
