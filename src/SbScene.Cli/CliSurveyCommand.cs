internal static partial class CliApp
{
    static int Survey(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("survey requires a file or directory path.");
            PrintUsage();
            return 1;
        }

        var input = args[0];
        string? output = null;
        string? filter = null;
        var limitScenes = int.MaxValue;
        var limitSvos = int.MaxValue;

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--out" when i + 1 < args.Length:
                    output = args[++i];
                    break;
                case "--filter" when i + 1 < args.Length:
                    filter = args[++i];
                    break;
                case "--limit-scenes" when i + 1 < args.Length && int.TryParse(args[i + 1], out var parsedSceneLimit):
                    limitScenes = parsedSceneLimit;
                    i++;
                    break;
                case "--limit-svos" when i + 1 < args.Length && int.TryParse(args[i + 1], out var parsedSvoLimit):
                    limitSvos = parsedSvoLimit;
                    i++;
                    break;
                default:
                    Console.Error.WriteLine($"Unknown or incomplete option: {args[i]}");
                    return 1;
            }
        }

        var scenePaths = EnumerateSurveyFiles(input, "*.sbscene", filter)
            .Take(Math.Max(0, limitScenes))
            .ToArray();
        var svoPaths = EnumerateSurveyFiles(input, "*.svo", filter)
            .Take(Math.Max(0, limitSvos))
            .ToArray();

        var sceneNameIndex = BuildSurveySceneNameIndex(scenePaths);
        var sceneRows = scenePaths.Select(path => BuildSceneSurveyRow(input, path, sceneNameIndex)).ToArray();
        var svoRows = svoPaths.Select(path => BuildSvoSurveyRow(input, path)).ToArray();
        var result = new SurveyResult
        {
            Input = input,
            Filter = filter,
            Scenes = sceneRows,
            Svos = svoRows,
            SceneAggregate = BuildSceneSurveyAggregate(sceneRows),
            SvoAggregate = BuildSvoSurveyAggregate(svoRows),
        };

        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        if (output is not null)
        {
            EnsureDirectory(output);
            File.WriteAllText(output, json, new UTF8Encoding(false));
            Console.WriteLine($"Surveyed scenes={sceneRows.Length}, svos={svoRows.Length}");
            Console.WriteLine($"Output: {output}");
            return 0;
        }

        Console.WriteLine(json);
        return 0;
    }

    static IEnumerable<string> EnumerateSurveyFiles(string input, string pattern, string? filter)
    {
        IEnumerable<string> paths;
        if (File.Exists(input))
        {
            paths = string.Equals(Path.GetExtension(input), Path.GetExtension(pattern.Replace("*", string.Empty)), StringComparison.OrdinalIgnoreCase)
                ? [Path.GetFullPath(input)]
                : [];
        }
        else if (Directory.Exists(input))
        {
            paths = Directory.EnumerateFiles(input, pattern, SearchOption.AllDirectories);
        }
        else
        {
            throw new FileNotFoundException($"Survey input was not found: {input}", input);
        }

        if (!string.IsNullOrWhiteSpace(filter))
        {
            paths = paths.Where(path => path.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }

        return paths.OrderBy(static path => path, StringComparer.OrdinalIgnoreCase);
    }
}
