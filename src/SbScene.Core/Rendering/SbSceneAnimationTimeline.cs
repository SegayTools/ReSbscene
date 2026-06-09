using SbScene.Core.Semantics;

namespace SbScene.Core.Rendering;

/// <summary>
/// 提供动画时间轴辅助方法，用于计算结束帧、最大关键帧和字段中的帧值。
/// </summary>
public static class SbSceneAnimationTimeline
{
    /// <summary>
    /// 获取结束帧，用于展示、比较、索引查找或后续计算。
    /// </summary>
    /// <param name="animation">参与本次处理的动画。</param>
    /// <returns>动画声明的结束帧；缺失时使用所有轨道的最大结束帧。</returns>
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

    /// <summary>
    /// 获取最大值Key帧，用于展示、比较、索引查找或后续计算。
    /// </summary>
    /// <param name="animation">参与本次处理的动画。</param>
    /// <returns>动画所有轨道关键帧中的最大帧号。</returns>
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

    /// <summary>
    /// 获取Numeric字段整数，用于展示、比较、索引查找或后续计算。
    /// </summary>
    /// <param name="fields">要搜索的数值字段摘要集合。</param>
    /// <param name="idHex">目标字段 ID 的十六进制文本。</param>
    /// <returns>字段的第一个整数值；字段缺失或超出 Int32 范围时返回 null。</returns>
    public static int? GetNumericFieldInt(IReadOnlyList<FieldValueSummary> fields, string idHex)
    {
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentException.ThrowIfNullOrWhiteSpace(idHex);

        var field = fields.FirstOrDefault(field => string.Equals(field.IdHex, idHex, StringComparison.OrdinalIgnoreCase));
        var value = field?.Int64Values?.FirstOrDefault();
        return value is >= int.MinValue and <= int.MaxValue ? (int)value.Value : null;
    }
}
