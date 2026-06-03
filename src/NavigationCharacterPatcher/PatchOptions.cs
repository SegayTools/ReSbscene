using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace NavigationCharacterPatcher;

/// <summary>
/// 重打包时输出 ab 使用的压缩方式。<see cref="Keep"/> 表示沿用输入 ab 的原始压缩。
/// </summary>
public enum BundleCompression
{
    Keep,
    None,
    Lz4,
    Lzma,
}

public sealed class PatchOptions
{
    public required string InputPath { get; init; }

    public required string OutputPath { get; init; }

    public long NewScriptPathId { get; init; } = NavigationCharacterScriptPatcher.DefaultScriptPathId;

    public string ScriptClassName { get; init; } = NavigationCharacterScriptPatcher.DefaultScriptClassName;

    public BundleCompression Compression { get; init; } = BundleCompression.Keep;

    public bool DryRun { get; init; }
}

public sealed class PatchResult
{
    public required long ScriptPathId { get; init; }

    public required int ModifiedCount { get; init; }

    public required bool WroteOutput { get; init; }

    public long OutputSize { get; init; }

    public AssetBundleCompressionType OutputCompression { get; init; }
}

/// <summary>
/// 当 ab 内找不到目标脚本，或没有任何 MonoBehaviour 引用它时抛出。退出码映射为 2。
/// </summary>
public sealed class PatchTargetNotFoundException(string message) : Exception(message);
