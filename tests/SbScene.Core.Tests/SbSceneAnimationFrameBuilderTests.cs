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
                Track(trackType: 18, flags: 0x23, Key(0, 0), Key(10, 2)),
                Track(trackType: 24, flags: 0x13, Key(0, 1), Key(10, 0.5))));
        var scene = Scene(animation);

        var state = SbSceneAnimationFrameBuilder.Build(
            scene,
            [new SbSceneAnimationSelection("Test", 10)]);

        Assert.Equal(20, state.Nodes[0].TranslationX, precision: 6);
        Assert.False(state.Nodes[0].Display);
        Assert.Equal(128, state.Nodes[0].MaterialA);
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

    private static SbSceneFile Scene(params AnimationInfo[] animations)
    {
        var nodes = new[] { Node(0) };
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
                NodeCount = nodes.Length,
                AnimationCount = animations.Length,
                VariantHintCount = 0,
                BlockCounts = new Dictionary<string, int>(),
                Warnings = [],
            },
        };
    }

    private static NodeInfo Node(int index)
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
                IlluminationColor = new ColorArgbValue { A = 0, R = 0, G = 0, B = 0 },
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
