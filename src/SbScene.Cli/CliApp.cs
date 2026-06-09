internal static partial class CliApp
{
    internal static int Run(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            PrintUsage();
            return 0;
        }

        return args[0] switch
        {
            "dump" => Dump(args.Skip(1).ToArray()),
            "export-unity-navichara" => ExportUnityNavichara(args.Skip(1).ToArray()),
            "extract-images" => ExtractImages(args.Skip(1).ToArray()),
            "inspect" => Inspect(args.Skip(1).ToArray()),
            "inspect-svo" => InspectSvo(args.Skip(1).ToArray()),
            "render" => Render(args.Skip(1).ToArray()),
            "survey" => Survey(args.Skip(1).ToArray()),
            _ => UnknownCommand(args[0]),
        };
    }

    static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        PrintUsage();
        return 1;
    }

    static void EnsureDirectory(string path)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  SbScene.Cli inspect <file>");
        Console.WriteLine("  SbScene.Cli inspect-svo <svo>");
        Console.WriteLine("  SbScene.Cli survey <file-or-dir> [--filter <text>] [--limit-scenes <n>] [--limit-svos <n>] [--out <json>]");
        Console.WriteLine("  SbScene.Cli dump <file> --json <out> [--markdown <out>]");
        Console.WriteLine("  SbScene.Cli dump <file> --markdown <out> [--json <out>]");
        Console.WriteLine("  SbScene.Cli extract-images <sbscene> <svo> --out <dir> [--no-atlas]");
        Console.WriteLine("  SbScene.Cli export-unity-navichara <sbscene> <svo> --out <dir> [--profile <json>] [--map <source=target>] [--character-id <n>] [--fashion <frame>] [--accessory <frame>] [--position <frame>] [--extract-sprites] [--write-validation-frames] [--strict]");
        Console.WriteLine("  SbScene.Cli export-unity-navichara <sbscene> --write-profile-template <json> [--auto-map]");
        Console.WriteLine("  SbScene.Cli render <sbscene-or-dir> [svo] --out <png|gif-or-dir> [--filter <text>] [--gif] [--gif-compress] [--gif-width <px>|--gif-height <px>] [--fps <n>] [--frames <start:end>] [--character-defaults] [--anim <name[frame]|#index[frame]>] [--background <color>] [--scale <n>] [--supersample <n>] [--sampling <mode>] [--high-quality] [--padding <px>] [--show-hidden] [--render-secondary]");
    }
}
