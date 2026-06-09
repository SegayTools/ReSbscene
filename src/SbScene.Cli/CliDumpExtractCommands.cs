internal static partial class CliApp
{
    static int Dump(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("dump requires a file path.");
            PrintUsage();
            return 1;
        }

        var input = args[0];
        string? jsonOut = null;
        string? markdownOut = null;

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--json" when i + 1 < args.Length:
                    jsonOut = args[++i];
                    break;
                case "--markdown" when i + 1 < args.Length:
                    markdownOut = args[++i];
                    break;
                default:
                    Console.Error.WriteLine($"Unknown or incomplete option: {args[i]}");
                    return 1;
            }
        }

        if (jsonOut is null && markdownOut is null)
        {
            Console.Error.WriteLine("dump requires at least one output: --json <out> or --markdown <out>.");
            return 1;
        }

        var file = new SbSceneParser().ParseFile(input);
        if (jsonOut is not null)
        {
            EnsureDirectory(jsonOut);
            var json = JsonSerializer.Serialize(file, SbSceneJson.CreateOptions(indented: true));
            File.WriteAllText(jsonOut, json, new UTF8Encoding(false));
        }

        if (markdownOut is not null)
        {
            EnsureDirectory(markdownOut);
            File.WriteAllText(markdownOut, MarkdownExporter.ToMarkdown(file), new UTF8Encoding(false));
        }

        Console.WriteLine($"Parsed {input}");
        Console.WriteLine($"Blocks={file.Summary.TotalBlockCount}, Nodes={file.Summary.NodeCount}, Animations={file.Summary.AnimationCount}, Hints={file.Summary.VariantHintCount}");
        return 0;
    }

    static int ExtractImages(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("extract-images requires <sbscene> <svo> --out <dir>.");
            PrintUsage();
            return 1;
        }

        var sbscene = args[0];
        var svo = args[1];
        string? output = null;
        var writeAtlases = true;

        for (var i = 2; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--out" when i + 1 < args.Length:
                    output = args[++i];
                    break;
                case "--no-atlas":
                    writeAtlases = false;
                    break;
                default:
                    Console.Error.WriteLine($"Unknown or incomplete option: {args[i]}");
                    return 1;
            }
        }

        if (output is null)
        {
            Console.Error.WriteLine("extract-images requires --out <dir>.");
            return 1;
        }

        var result = SvoImageExtractor.Extract(sbscene, svo, output, writeAtlases);
        Console.WriteLine($"Extracted {result.CropCount} crop PNG(s) from {result.AtlasCount} atlas image(s).");
        Console.WriteLine($"Mapped {result.ImageCastCount} image cast record(s).");
        Console.WriteLine($"Output: {result.OutputDirectory}");
        foreach (var warning in result.Warnings)
        {
            Console.WriteLine($"Warning: {warning}");
        }

        return 0;
    }
}
