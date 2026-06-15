using System.Globalization;
using System.Text;
using NavigationCharacterPatcher;

Console.OutputEncoding = Encoding.UTF8;

try
{
    return Run(args);
}
catch (PatchTargetNotFoundException ex)
{
    Console.Error.WriteLine("未找到目标: " + ex.Message);
    return 2;
}
catch (Exception ex)
{
    Console.Error.WriteLine("错误: " + ex.Message);
    return 1;
}

static int Run(string[] args)
{
    if (args.Length == 0 || args[0] is "-h" or "--help")
    {
        PrintUsage();
        return args.Length == 0 ? 1 : 0;
    }

    string? input = null;
    string? output = null;
    long pathId = NavigationCharacterScriptPatcher.DefaultScriptPathId;
    string scriptName = NavigationCharacterScriptPatcher.DefaultScriptClassName;
    var compression = BundleCompression.Keep;
    var dryRun = false;

    for (var i = 0; i < args.Length; i++)
    {
        var arg = args[i];
        switch (arg)
        {
            case "-o" or "--output" when i + 1 < args.Length:
                output = args[++i];
                break;
            case "--path-id" when i + 1 < args.Length:
                if (!long.TryParse(args[++i], NumberStyles.Integer, CultureInfo.InvariantCulture, out pathId))
                {
                    Console.Error.WriteLine("--path-id 需要一个有效的 64 位整数: " + args[i]);
                    return 1;
                }

                break;
            case "--script-name" when i + 1 < args.Length:
                scriptName = args[++i];
                break;
            case "--compression" when i + 1 < args.Length:
                if (!TryParseCompression(args[++i], out compression))
                {
                    Console.Error.WriteLine("--compression 取值须为 keep|none|lz4|lzma: " + args[i]);
                    return 1;
                }

                break;
            case "--dry-run":
                dryRun = true;
                break;
            default:
                if (arg.StartsWith('-'))
                {
                    Console.Error.WriteLine("未知或不完整的选项: " + arg);
                    return 1;
                }

                if (input is not null)
                {
                    Console.Error.WriteLine("只能指定一个输入 ab 文件，多余参数: " + arg);
                    return 1;
                }

                input = arg;
                break;
        }
    }

    if (input is null)
    {
        Console.Error.WriteLine("缺少输入 ab 文件。");
        PrintUsage();
        return 1;
    }

    output ??= BuildDefaultOutputPath(input);

    var patcher = new NavigationCharacterScriptPatcher(Console.WriteLine);
    var result = patcher.Patch(new PatchOptions
    {
        InputPath = input,
        OutputPath = output,
        NewScriptPathId = pathId,
        ScriptClassName = scriptName,
        Compression = compression,
        DryRun = dryRun,
    });

    if (result.WroteOutput)
    {
        Console.WriteLine(
            $"完成: 修改 {result.ModifiedScriptCount} 个 MonoScript PathID, " +
            $"{result.ModifiedBehaviourCount} 个 MonoBehaviour 引用；" +
            $"有效引用 {result.VerifiedBehaviourCount} 个。");
    }
    else if (dryRun)
    {
        Console.WriteLine(
            $"dry-run: 将修改 {result.ModifiedScriptCount} 个 MonoScript PathID, " +
            $"{result.ModifiedBehaviourCount} 个 MonoBehaviour 引用；" +
            $"有效引用 {result.VerifiedBehaviourCount} 个。");
    }
    else
    {
        Console.WriteLine($"完成: 无需修改；有效引用 {result.VerifiedBehaviourCount} 个。");
    }

    return 0;
}

static string BuildDefaultOutputPath(string input)
{
    var directory = Path.GetDirectoryName(input) ?? string.Empty;
    var name = Path.GetFileNameWithoutExtension(input);
    var extension = Path.GetExtension(input);
    return Path.Combine(directory, name + ".patched" + extension);
}

static bool TryParseCompression(string value, out BundleCompression compression)
{
    switch (value.ToLowerInvariant())
    {
        case "keep":
            compression = BundleCompression.Keep;
            return true;
        case "none":
            compression = BundleCompression.None;
            return true;
        case "lz4":
            compression = BundleCompression.Lz4;
            return true;
        case "lzma":
            compression = BundleCompression.Lzma;
            return true;
        default:
            compression = BundleCompression.Keep;
            return false;
    }
}

static void PrintUsage()
{
    Console.WriteLine("NavigationCharacterPatcher — 校正 prefab ab 内 NavigationCharacter MonoScript PathID 和 m_Script 引用");
    Console.WriteLine();
    Console.WriteLine("用法:");
    Console.WriteLine("  NavigationCharacterPatcher <input.ab> [选项]");
    Console.WriteLine();
    Console.WriteLine("选项:");
    Console.WriteLine("  -o, --output <path>      输出 ab 路径 (默认: <input>.patched.ab)");
    Console.WriteLine($"      --path-id <long>     目标 MonoScript PathID / m_Script.m_PathID (默认: {NavigationCharacterScriptPatcher.DefaultScriptPathId})");
    Console.WriteLine($"      --script-name <name> 要定位的脚本类名 (默认: {NavigationCharacterScriptPatcher.DefaultScriptClassName})");
    Console.WriteLine("      --compression <type> keep|none|lz4|lzma (默认: keep，沿用输入压缩)");
    Console.WriteLine("      --dry-run            只报告将修改的数量，不写出文件");
    Console.WriteLine("  -h, --help               显示帮助");
}
