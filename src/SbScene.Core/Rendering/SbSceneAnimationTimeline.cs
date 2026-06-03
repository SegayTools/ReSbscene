using SbScene.Core.Semantics;

namespace SbScene.Core.Rendering;

public static class SbSceneAnimationTimeline
{
    public static int GetEndFrame(AnimationInfo animation)
    {
        ArgumentNullException.ThrowIfNull(animation);

        return GetNumericFieldInt(animation.NumericFields, "0x56")
            ?? animation.Motions
                .SelectMany(static motion => motion.Tracks)
                .Select(static track => track.LastFrame ?? 0)
                .DefaultIfEmpty(0)
                .Max();
    }

    public static int GetMaxKeyFrame(AnimationInfo animation)
    {
        ArgumentNullException.ThrowIfNull(animation);

        return animation.Motions
            .SelectMany(static motion => motion.Tracks)
            .SelectMany(static track => track.Keyframes)
            .Select(static key => key.KeyFrame ?? 0)
            .DefaultIfEmpty(0)
            .Max();
    }

    public static int? GetNumericFieldInt(IReadOnlyList<FieldValueSummary> fields, string idHex)
    {
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentException.ThrowIfNullOrWhiteSpace(idHex);

        var field = fields.FirstOrDefault(field => string.Equals(field.IdHex, idHex, StringComparison.OrdinalIgnoreCase));
        var value = field?.Int64Values?.FirstOrDefault();
        return value is >= int.MinValue and <= int.MaxValue ? (int)value.Value : null;
    }
}
