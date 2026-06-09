namespace SbScene.Core.Rendering;

internal sealed class WarningCollector
{
    private readonly List<string> _warnings = [];
    private readonly HashSet<string> _warningSet = new(StringComparer.Ordinal);

    /// <summary>
    /// 表示非致命警告列表，用于把非致命问题返回给调用方，便于诊断解析、渲染或导出过程。
    /// </summary>
    public IReadOnlyList<string> Warnings => _warnings;

    /// <summary>
    /// 添加Add，并更新调用方可读取的收集状态。
    /// </summary>
    /// <param name="warning">接收诊断日志或非致命警告的回调。</param>
    public void Add(string warning)
    {
        if (_warningSet.Add(warning))
        {
            _warnings.Add(warning);
        }
    }
}
