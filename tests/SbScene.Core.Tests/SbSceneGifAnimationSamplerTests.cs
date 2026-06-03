using SbScene.Core.Rendering;
using SbScene.Core.Resources;
using SbScene.Core.Semantics;
using SbScene.Core.Vtbf;

namespace SbScene.Core.Tests;

public sealed class SbSceneGifAnimationSamplerTests
{
    [Fact]
    public void BuildFrameSelectionsKeepsExplicitFramesAndAdvancesImplicitPlayback()
    {
        var selections = new[]
        {
            new SbSceneAnimationSelection("Change_Fashion", 0) { HasExplicitFrame = true },
            new SbSceneAnimationSelection("Action_Joy3", 0),
        };

        var sampled = SbSceneGifAnimationSampler.BuildFrameSelections(selections, gifFrameIndex: 3, fps: 30);

        Assert.Equal(0, sampled[0].Frame);
        Assert.Equal(6, sampled[1].Frame);
    }

    [Fact]
    public void ResolveEndFrameUsesMaximumEnabledAnimationEndFrame()
    {
        var scene = Scene(
            Animation(0, "Change_Fashion", endFrame: 8),
            Animation(1, "Action_Joy3", endFrame: 24));
        var selections = new[]
        {
            new SbSceneAnimationSelection("Change_Fashion", 0) { HasExplicitFrame = true },
            new SbSceneAnimationSelection("Action_Joy3", 0),
        };

        var endFrame = SbSceneGifAnimationSampler.ResolveEndFrame(scene, selections);

        Assert.Equal(24, endFrame);
    }

    [Fact]
    public void ResolveEndFrameFallsBackToMaximumTrackFrame()
    {
        var scene = Scene(Animation(0, "Action_Joy3", endFrame: null, Track(lastFrame: 42)));

        var endFrame = SbSceneGifAnimationSampler.ResolveEndFrame(scene, [new SbSceneAnimationSelection("Action_Joy3", 0)]);

        Assert.Equal(42, endFrame);
    }

    [Fact]
    public void FrameCountUsesSourceSixtyFpsTimeline()
    {
        Assert.Equal(30, SbSceneGifAnimationSampler.GetOutputFrameCount(startFrame: 0, endFrame: 60, fps: 30));
        Assert.Equal(4, SbSceneGifAnimationSampler.GetSourceFrame(gifFrameIndex: 2, fps: 30));
    }

    private static SbSceneFile Scene(params AnimationInfo[] animations)
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
                Nodes = [],
                Transform2DRecords = [],
                NodeCategoryRecords = [],
                NodeCategoryDetails = [],
                NodeGroups = [],
                Resources = new SbSceneResourceMap
                {
                    Atlases = [],
                    ImageCasts = [],
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
                NodeCount = 0,
                AnimationCount = animations.Length,
                VariantHintCount = 0,
                BlockCounts = new Dictionary<string, int>(),
                Warnings = [],
            },
        };
    }

    private static AnimationInfo Animation(int index, string name, int? endFrame, params TrackInfo[] tracks)
    {
        return new AnimationInfo
        {
            Index = index,
            Offset = 0,
            Path = $"ANIM[{index}]",
            Name = name,
            StringFields = [],
            NumericFields = endFrame is null ? [] : [NumericField("0x56", endFrame.Value)],
            Motions = tracks.Length == 0 ? [] : [Motion(tracks)],
        };
    }

    private static MotionInfo Motion(params TrackInfo[] tracks)
    {
        return new MotionInfo
        {
            Index = 0,
            Offset = 0,
            Path = "MOT[0]",
            Name = null,
            TargetName = null,
            TargetIndex = null,
            CastIndex = null,
            DeclaredTrackCount = tracks.Length,
            StringFields = [],
            NumericFields = [],
            Tracks = tracks,
        };
    }

    private static TrackInfo Track(int lastFrame)
    {
        return new TrackInfo
        {
            Index = 0,
            Offset = 0,
            Path = "TRK[0]",
            Name = null,
            TrackId = null,
            TrackType = 0,
            TrackTypeName = null,
            ValueType = null,
            ValueTypeName = null,
            DeclaredKeyCountFromTrack = 2,
            DeclaredKeyCountFromKeyBlock = 2,
            KeyCountMatchesDeclaration = true,
            Flags = 0x13,
            KeyValueStorage = null,
            TargetIndex = null,
            FirstFrame = 0,
            LastFrame = lastFrame,
            DeclaredKeyCount = 2,
            IsLikelyStateTrack = false,
            StringFields = [],
            NumericFields = [],
            Keyframes = [Key(0), Key(lastFrame)],
        };
    }

    private static KeyframeInfo Key(int frame)
    {
        return new KeyframeInfo
        {
            Index = frame,
            Offset = 0,
            Path = $"KEY[{frame}]",
            Fields = [],
            KeyFrame = frame,
            ScalarValue = 0,
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
            ValueCandidates = [0],
            Preview = null,
        };
    }

    private static FieldValueSummary NumericField(string idHex, int value)
    {
        return new FieldValueSummary
        {
            IdHex = idHex,
            TypeHex = "0x0006",
            TypeName = "UInt32",
            Preview = value.ToString(),
            Int64Values = [value],
            Float64Values = null,
            StringValue = null,
        };
    }
}
