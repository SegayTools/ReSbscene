using System.Text.RegularExpressions;

namespace SbScene.Core.Semantics;

internal static partial class VariantHintAnalyzer
{
    /// <summary>
    /// 根据节点分组、动画名称和轨道信息推断可导出的变体提示。
    /// </summary>
    /// <param name="nodes">场景节点集合，用于推断服装、附件等节点变体。</param>
    /// <param name="animations">动画集合，用于推断可绑定的动作和轨道变体。</param>
    /// <returns>去重并按置信度排序后的变体提示列表。</returns>
    public static IReadOnlyList<VariantHint> Build(IReadOnlyList<NodeInfo> nodes, IReadOnlyList<AnimationInfo> animations)
    {
        var hints = new List<VariantHint>();
        AddNodeGroupHints(hints, nodes);
        AddAnimationHints(hints, animations);
        AddTrackHints(hints, animations);

        return hints
            .GroupBy(static hint => $"{hint.Category}|{hint.SourceKind}|{hint.Name}|{hint.TrackPath}", StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.OrderByDescending(static hint => hint.Confidence).First())
            .OrderByDescending(static hint => hint.Confidence)
            .ThenBy(static hint => hint.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static hint => hint.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void AddNodeGroupHints(List<VariantHint> hints, IReadOnlyList<NodeInfo> nodes)
    {
        foreach (var group in nodes.GroupBy(static node => node.Group, StringComparer.OrdinalIgnoreCase))
        {
            var category = group.Key switch
            {
                "plain" or "uniform" or "gorgeous" or "present" => "Fashion",
                "acc" or "accessory" or "acs" => "Accessory",
                "mouth" or "lip" => "Mouth",
                "face" or "eye" or "brow" => "Expression",
                "pos" or "position" => "Position",
                _ => null,
            };

            if (category is null)
            {
                continue;
            }

            hints.Add(new VariantHint
            {
                Category = category,
                SourceKind = "NodeGroup",
                Name = group.Key,
                Confidence = 0.68,
                Reason = $"NODE name prefix '{group.Key}_' appears {group.Count()} time(s).",
                NodeGroup = group.Key,
            });
        }
    }

    private static void AddAnimationHints(List<VariantHint> hints, IReadOnlyList<AnimationInfo> animations)
    {
        foreach (var animation in animations)
        {
            if (string.IsNullOrWhiteSpace(animation.Name))
            {
                continue;
            }

            var match = VariantAnimationRegex().Match(animation.Name);
            if (!match.Success)
            {
                continue;
            }

            var rawCategory = match.Groups["category"].Value;
            var category = rawCategory.ToLowerInvariant() switch
            {
                "change_fashion" => "Fashion",
                "change_position" => "Position",
                "change_accessory" => "Accessory",
                "dresschange" => "DressChange",
                "action" => "Action",
                "mouth" => "Mouth",
                _ => rawCategory,
            };

            hints.Add(new VariantHint
            {
                Category = category,
                SourceKind = "AnimationName",
                Name = animation.Name,
                Confidence = 0.86,
                Reason = "Animation name matches known sbscene variant/action naming pattern.",
                AnimationName = animation.Name,
            });
        }
    }

    private static void AddTrackHints(List<VariantHint> hints, IReadOnlyList<AnimationInfo> animations)
    {
        foreach (var animation in animations)
        {
            foreach (var motion in animation.Motions)
            {
                foreach (var track in motion.Tracks.Where(static track => track.IsLikelyStateTrack))
                {
                    hints.Add(new VariantHint
                    {
                        Category = GuessCategoryFromAnimation(animation.Name),
                        SourceKind = "TrackState",
                        Name = track.Name ?? $"TRK@0x{track.Offset:X}",
                        Confidence = 0.58,
                        Reason = "Track name or key values look like a boolean visibility/state channel.",
                        AnimationName = animation.Name,
                        TrackPath = track.Path,
                    });
                }
            }
        }
    }

    private static string GuessCategoryFromAnimation(string? animationName)
    {
        if (string.IsNullOrWhiteSpace(animationName))
        {
            return "State";
        }

        var lower = animationName.ToLowerInvariant();
        if (lower.Contains("fashion", StringComparison.Ordinal))
        {
            return "Fashion";
        }

        if (lower.Contains("position", StringComparison.Ordinal))
        {
            return "Position";
        }

        if (lower.Contains("accessory", StringComparison.Ordinal))
        {
            return "Accessory";
        }

        if (lower.Contains("dress", StringComparison.Ordinal))
        {
            return "DressChange";
        }

        if (lower.Contains("mouth", StringComparison.Ordinal))
        {
            return "Mouth";
        }

        if (lower.Contains("action", StringComparison.Ordinal))
        {
            return "Action";
        }

        return "State";
    }

    [GeneratedRegex("^(?<category>Change_Fashion|Change_Position|Change_Accessory|DressChange|Action|Mouth)(?<suffix>[_A-Za-z0-9-]*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VariantAnimationRegex();
}
