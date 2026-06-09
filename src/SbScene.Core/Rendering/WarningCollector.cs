namespace SbScene.Core.Rendering;

internal sealed class WarningCollector
{
    private readonly List<string> _warnings = [];
    private readonly HashSet<string> _warningSet = new(StringComparer.Ordinal);

    public IReadOnlyList<string> Warnings => _warnings;

    public void Add(string warning)
    {
        if (_warningSet.Add(warning))
        {
            _warnings.Add(warning);
        }
    }
}
