using System.Globalization;

namespace SbScene.Core.Rendering;

/// <summary>
/// 提供sbscene 场景动画Selection解析器，负责把原始文件或字节流转换为强类型模型。
/// </summary>
public static class SbSceneAnimationSelectionParser
{
    /// <summary>
    /// 尝试Parse，并通过返回值或输出参数报告是否成功。
    /// </summary>
    /// <param name="text">命令行传入的动画选择文本。</param>
    /// <param name="selection">解析得到的动画选择；解析失败时为默认值。</param>
    /// <returns>如果文本能解析为动画选择则为 true；否则为 false。</returns>
    public static bool TryParse(string text, out SbSceneAnimationSelection selection)
    {
        selection = new SbSceneAnimationSelection(string.Empty, 0);
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        if (TryParseBracketed(trimmed, out selection))
        {
            return true;
        }

        if (trimmed.Contains('[') || trimmed.Contains(']'))
        {
            return false;
        }

        var separator = trimmed.LastIndexOf('@');
        if (separator < 0)
        {
            separator = trimmed.LastIndexOf(':');
        }

        if (separator < 0)
        {
            selection = Create(trimmed, 0, hasExplicitFrame: false);
            return selection.Name.Length > 0;
        }

        var name = trimmed[..separator].Trim();
        var frameText = trimmed[(separator + 1)..].Trim();
        if (name.Length == 0 || frameText.Length == 0)
        {
            return false;
        }

        if (!double.TryParse(frameText, NumberStyles.Float, CultureInfo.InvariantCulture, out var frame))
        {
            return false;
        }

        selection = Create(name, frame, hasExplicitFrame: true);
        return true;
    }

    private static bool TryParseBracketed(string text, out SbSceneAnimationSelection selection)
    {
        selection = new SbSceneAnimationSelection(string.Empty, 0);
        if (!text.EndsWith(']'))
        {
            return false;
        }

        var openBracket = text.LastIndexOf('[');
        if (openBracket < 0)
        {
            return false;
        }

        var name = text[..openBracket].Trim();
        var frameText = text[(openBracket + 1)..^1].Trim();
        if (name.Length == 0 || frameText.Length == 0)
        {
            return false;
        }

        if (!double.TryParse(frameText, NumberStyles.Float, CultureInfo.InvariantCulture, out var frame))
        {
            return false;
        }

        selection = Create(name, frame, hasExplicitFrame: true);
        return true;
    }

    private static SbSceneAnimationSelection Create(string target, double frame, bool hasExplicitFrame)
    {
        var trimmed = target.Trim();
        if (trimmed.Length > 1
            && trimmed[0] == '#'
            && int.TryParse(trimmed[1..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)
            && index >= 0)
        {
            return new SbSceneAnimationSelection(trimmed, frame)
            {
                Index = index,
                HasExplicitFrame = hasExplicitFrame,
            };
        }

        return new SbSceneAnimationSelection(trimmed, frame)
        {
            HasExplicitFrame = hasExplicitFrame,
        };
    }
}
