using SbScene.Core.Rendering;
using SbScene.Core.Semantics;

namespace SbScene.Core.Tests;

public sealed class SbSceneAnimationEvaluatorTests
{
    [Fact]
    public void EvaluateTrackUsesHermiteForSplineKeys()
    {
        var track = Track(
            trackType: 1,
            flags: 0x13,
            Key(0, 0, interpolation: 2, tangentOut: 0),
            Key(10, 10, interpolation: 2, tangentIn: 0));

        var value = SbSceneAnimationEvaluator.EvaluateTrack(track, 2.5);

        Assert.NotNull(value);
        Assert.Equal(1.5625, value.Value, precision: 6);
    }

    [Fact]
    public void EvaluateTrackUsesLinearForLinearKeys()
    {
        var track = Track(
            trackType: 1,
            flags: 0x13,
            Key(0, 0, interpolation: 1),
            Key(10, 10, interpolation: 1));

        var value = SbSceneAnimationEvaluator.EvaluateTrack(track, 2.5);

        Assert.NotNull(value);
        Assert.Equal(2.5, value.Value, precision: 6);
    }

    [Fact]
    public void EvaluateTrackHoldsStateTrackValues()
    {
        var track = Track(
            trackType: 18,
            flags: 0x23,
            Key(0, 0, interpolation: 1),
            Key(10, 3, interpolation: 1));

        var value = SbSceneAnimationEvaluator.EvaluateTrack(track, 5);

        Assert.NotNull(value);
        Assert.Equal(0, value.Value, precision: 6);
    }

    [Fact]
    public void EvaluateTrackUsesPackedAngleDegreesForRotation()
    {
        var track = Track(
            trackType: 5,
            flags: 0x43,
            Key(0, rawAngle: 0, degrees: 0, interpolation: 2, tangentOut: 0),
            Key(10, rawAngle: 16384, degrees: 90, interpolation: 2, tangentIn: 0));

        var value = SbSceneAnimationEvaluator.EvaluateTrack(track, 2.5);

        Assert.NotNull(value);
        Assert.Equal(14.0625, value.Value, precision: 6);
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

    private static KeyframeInfo Key(
        int frame,
        double value,
        int interpolation,
        double? tangentIn = null,
        double? tangentOut = null)
    {
        return Key(
            frame,
            scalarValue: value,
            rawAngle: null,
            degrees: null,
            interpolation,
            tangentIn,
            tangentOut);
    }

    private static KeyframeInfo Key(
        int frame,
        int rawAngle,
        double degrees,
        int interpolation,
        double? tangentIn = null,
        double? tangentOut = null)
    {
        return Key(
            frame,
            scalarValue: null,
            rawAngle,
            degrees,
            interpolation,
            tangentIn,
            tangentOut);
    }

    private static KeyframeInfo Key(
        int frame,
        double? scalarValue,
        int? rawAngle,
        double? degrees,
        int interpolation,
        double? tangentIn,
        double? tangentOut)
    {
        return new KeyframeInfo
        {
            Index = frame,
            Offset = 0,
            Path = $"KEY[{frame}]",
            Fields = [],
            KeyFrame = frame,
            ScalarValue = scalarValue,
            BoolValue = null,
            PackedAngleRaw = rawAngle,
            PackedAngleDegreesCandidate = degrees,
            KeyValueTypeHex = null,
            KeyValueTypeName = null,
            KeyValueKind = null,
            Interpolation = interpolation,
            InterpolationName = null,
            TangentIn = tangentIn,
            TangentOut = tangentOut,
            TimeCandidates = [],
            ValueCandidates = scalarValue is null ? [] : [scalarValue.Value],
            Preview = null,
        };
    }
}
