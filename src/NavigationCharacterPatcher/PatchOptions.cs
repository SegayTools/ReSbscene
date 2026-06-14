using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace NavigationCharacterPatcher;

/// <summary>
/// 定义Bundle输出压缩方式 的可选值，供调用方选择解析、渲染或导出策略。
/// </summary>
public enum BundleCompression
{
    Keep,
    None,
    Lz4,
    Lzma,
}

/// <summary>
/// 表示Patch选项，集中描述调用方可配置的输入、开关和默认策略。
/// </summary>
public sealed class PatchOptions
{
    /// <summary>
    /// 获取或设置输入文件路径，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public required string InputPath { get; init; }

    /// <summary>
    /// 获取或设置输出文件路径，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public required string OutputPath { get; init; }

    /// <summary>
    /// 表示NewScript路径标识，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public long NewScriptPathId { get; init; } = NavigationCharacterScriptPatcher.DefaultScriptPathId;

    /// <summary>
    /// 表示ScriptClass名称，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public string ScriptClassName { get; init; } = NavigationCharacterScriptPatcher.DefaultScriptClassName;

    /// <summary>
    /// 表示输出压缩方式，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public BundleCompression Compression { get; init; } = BundleCompression.Keep;

    /// <summary>
    /// 获取或设置模拟运行开关，用于表示状态开关或检测结果，调用方据此选择显示、解析、导出或诊断分支。
    /// </summary>
    public bool DryRun { get; init; }
}

/// <summary>
/// 表示Patch结果，封装处理产物、统计信息和诊断状态。
/// </summary>
public sealed class PatchResult
{
    /// <summary>
    /// 获取或设置目标 MonoScript PathID，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public required long ScriptPathId { get; init; }

    /// <summary>
    /// 获取或设置Modified数量，用于报告数量或统计值，便于调用方校验结构规模和处理结果。
    /// </summary>
    public required int ModifiedCount { get; init; }

    /// <summary>
    /// 获取或设置输出文件写出状态，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public required bool WroteOutput { get; init; }

    /// <summary>
    /// 获取或设置输出大小，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public long OutputSize { get; init; }

    /// <summary>
    /// 获取或设置输出输出压缩方式，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public AssetBundleCompressionType OutputCompression { get; init; }

    /// <summary>
    /// 获取输出压缩方式名称，供不直接引用 AssetsTools.NET 类型的调用方显示结果。
    /// </summary>
    public string OutputCompressionName => OutputCompression.ToString();
}

/// <summary>
/// 表示修补流程没有找到目标 MonoBehaviour 时抛出的异常。
/// </summary>
public sealed class PatchTargetNotFoundException(string message) : Exception(message);
