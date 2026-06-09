using System.Text.Json;
using System.Text.Json.Serialization;

namespace SbScene.Core.Output;

/// <summary>
/// 提供sbscene 场景JSON，负责把模型转换为可展示、保存或比较的文本。
/// </summary>
public static class SbSceneJson
{
    /// <summary>
    /// 创建项目统一的 JSON 序列化选项，控制缩进、空值忽略和命名策略。
    /// </summary>
    /// <param name="indented">指示输出 JSON 是否使用缩进格式。</param>
    /// <returns>用于 sbscene 模型输出的 JSON 序列化选项。</returns>
    public static JsonSerializerOptions CreateOptions(bool indented)
    {
        return new JsonSerializerOptions
        {
            WriteIndented = indented,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        };
    }
}
