using SbScene.Core.Semantics;

namespace SbScene.Core.Rendering;

public static class SbSceneAnimationEvaluator
{
    private const double Epsilon = 0.000001;

    public static double? EvaluateTrack(TrackInfo track, double frame)
    {
        ArgumentNullException.ThrowIfNull(track);

        var samples = track.Keyframes
            .Select(key => ToKeySample(track, key))
            .Where(static sample => sample is not null)
            .Cast<KeySample>()
            .OrderBy(static sample => sample.Frame)
            .ThenBy(static sample => sample.Index)
            .ToArray();

        if (samples.Length == 0)
        {
            return null;
        }

        if (frame <= samples[0].Frame)
        {
            return samples[0].Value;
        }

        if (frame >= samples[^1].Frame)
        {
            return samples[^1].Value;
        }

        var previous = samples[0];
        for (var i = 1; i < samples.Length; i++)
        {
            var next = samples[i];
            if (frame < next.Frame)
            {
                if (IsStateTrack(track) || previous.Interpolation == 0 || Math.Abs(next.Frame - previous.Frame) < Epsilon)
                {
                    return previous.Value;
                }

                return previous.Interpolation == 2
                    ? EvaluateHermite(previous, next, frame)
                    : EvaluateLinear(previous, next, frame);
            }

            previous = next;
        }

        return previous.Value;
    }

    private static double EvaluateLinear(KeySample previous, KeySample next, double frame)
    {
        var t = (frame - previous.Frame) / (next.Frame - previous.Frame);
        return previous.Value + (next.Value - previous.Value) * t;
    }

    private static double EvaluateHermite(KeySample previous, KeySample next, double frame)
    {
        var frameDelta = next.Frame - previous.Frame;
        var valueDelta = next.Value - previous.Value;
        var t = (frame - previous.Frame) / frameDelta;
        var tangentOut = previous.TangentOut ?? 0.0;
        var tangentIn = next.TangentIn ?? 0.0;
        var cubic = -2.0 * valueDelta + frameDelta * (tangentOut + tangentIn);
        var quadratic = 3.0 * valueDelta - frameDelta * (2.0 * tangentOut + tangentIn);
        var linear = frameDelta * tangentOut;
        return previous.Value + t * (t * (cubic * t + quadratic) + linear);
    }

    private static KeySample? ToKeySample(TrackInfo track, KeyframeInfo key)
    {
        var value = track.TrackType == 5 && key.PackedAngleDegreesCandidate is not null
            ? key.PackedAngleDegreesCandidate
            : key.BoolValue is not null
                ? key.BoolValue.Value ? 1.0 : 0.0
                : key.ScalarValue ?? (key.ValueCandidates.Count > 0 ? key.ValueCandidates[0] : null);

        if (value is null || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
        {
            return null;
        }

        return new KeySample(
            key.Index,
            key.KeyFrame ?? 0,
            value.Value,
            key.Interpolation ?? 0,
            key.TangentIn,
            key.TangentOut);
    }

    private static bool IsStateTrack(TrackInfo track)
    {
        return track.TrackType is 11 or 18 or 19 || (track.Flags & 0xFF) is 0x23 or 0x33;
    }

    private readonly record struct KeySample(
        int Index,
        double Frame,
        double Value,
        int Interpolation,
        double? TangentIn,
        double? TangentOut);
}
