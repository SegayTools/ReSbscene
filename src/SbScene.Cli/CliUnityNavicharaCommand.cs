internal static partial class CliApp
{
    static int ExportUnityNavichara(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            PrintExportUnityNavicharaUsage();
            return args.Length == 0 ? 1 : 0;
        }

        var positionals = new List<string>();
        string? output = null;
        string? profilePath = null;
        string? profileTemplatePath = null;
        string? rawJsonPath = null;
        var characterId = 0;
        var maps = new List<UnityNavicharaAnimationMap>();
        int? fashionFrame = null;
        int? accessoryFrame = null;
        int? positionFrame = null;
        var allowPlaceholderClips = false;
        var bakeSampledCurves = false;
        var extractSprites = false;
        var writeValidationFrames = false;
        var strict = false;
        var autoMap = false;
        var autoCenter = true;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--out" when i + 1 < args.Length:
                    output = args[++i];
                    break;
                case "--character-id" when i + 1 < args.Length && int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedCharacterId):
                    characterId = parsedCharacterId;
                    i++;
                    break;
                case "--profile" when i + 1 < args.Length:
                    profilePath = args[++i];
                    break;
                case "--map" when i + 1 < args.Length:
                    if (!TryParseUnityNavicharaMap(args[++i], out var map))
                    {
                        Console.Error.WriteLine("Invalid --map value. Use <sourceAnimation=targetClip>.");
                        return 1;
                    }

                    maps.Add(map);
                    break;
                case "--write-profile-template" when i + 1 < args.Length:
                    profileTemplatePath = args[++i];
                    break;
                case "--auto-map":
                    autoMap = true;
                    break;
                case "--fashion" when i + 1 < args.Length && int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedFashionFrame):
                    fashionFrame = parsedFashionFrame;
                    i++;
                    break;
                case "--accessory" when i + 1 < args.Length && int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedAccessoryFrame):
                    accessoryFrame = parsedAccessoryFrame;
                    i++;
                    break;
                case "--position" when i + 1 < args.Length && int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPositionFrame):
                    positionFrame = parsedPositionFrame;
                    i++;
                    break;
                case "--allow-placeholder-clips":
                    allowPlaceholderClips = true;
                    break;
                case "--bake-sampled-curves":
                    bakeSampledCurves = true;
                    break;
                case "--extract-sprites":
                    extractSprites = true;
                    break;
                case "--write-validation-frames":
                    writeValidationFrames = true;
                    break;
                case "--strict":
                    strict = true;
                    break;
                case "--no-auto-center":
                    autoCenter = false;
                    break;
                case "--raw-json" when i + 1 < args.Length:
                    rawJsonPath = args[++i];
                    break;
                default:
                    if (args[i].StartsWith("--", StringComparison.Ordinal))
                    {
                        Console.Error.WriteLine($"Unknown or incomplete option: {args[i]}");
                        return 1;
                    }

                    positionals.Add(args[i]);
                    break;
            }
        }

        if (positionals.Count == 0)
        {
            Console.Error.WriteLine("export-unity-navichara requires <sbscene>.");
            PrintExportUnityNavicharaUsage();
            return 1;
        }

        var sbscene = positionals[0];
        if (!File.Exists(sbscene))
        {
            Console.Error.WriteLine($"sbscene does not exist: {sbscene}");
            return 1;
        }

        if (profileTemplatePath is not null)
        {
            var scene = new SbSceneParser().ParseFile(sbscene);
            var template = UnityNavicharaExporter.BuildProfileTemplate(scene);
            EnsureDirectory(profileTemplatePath);
            File.WriteAllText(profileTemplatePath, JsonSerializer.Serialize(template, CreateUnityNavicharaJsonOptions(indented: true)), new UTF8Encoding(false));
            Console.WriteLine($"Wrote NaviChara profile template: {profileTemplatePath}");
            if (autoMap)
            {
                Console.WriteLine("--auto-map only contributes candidate mappings to the template; it is not used as a formal export mapping.");
            }

            return 0;
        }

        if (positionals.Count != 2)
        {
            Console.Error.WriteLine("export-unity-navichara export mode requires <sbscene> <svo>.");
            PrintExportUnityNavicharaUsage();
            return 1;
        }

        var svo = positionals[1];
        if (!File.Exists(svo))
        {
            Console.Error.WriteLine($"SVO does not exist: {svo}");
            return 1;
        }

        if (output is null)
        {
            Console.Error.WriteLine("export-unity-navichara requires --out <dir>.");
            return 1;
        }

        if (profilePath is null && maps.Count == 0)
        {
            Console.Error.WriteLine("export-unity-navichara requires --profile or at least one --map for formal export.");
            return 1;
        }

        if (autoMap)
        {
            Console.WriteLine("--auto-map was requested, but candidate mappings are informational only. Provide --profile or --map for formal clip mapping.");
        }

        var profile = profilePath is null ? null : UnityNavicharaProfileLoader.Load(profilePath);
        var result = UnityNavicharaExporter.Export(
            sbscene,
            svo,
            output,
            new UnityNavicharaExportOptions
            {
                CharacterId = characterId,
                Profile = profile,
                Maps = maps,
                FashionFrame = fashionFrame,
                AccessoryFrame = accessoryFrame,
                PositionFrame = positionFrame,
                AllowPlaceholderClips = allowPlaceholderClips,
                BakeSampledCurves = bakeSampledCurves,
                ExtractSprites = extractSprites,
                WriteValidationFrames = writeValidationFrames,
                Strict = strict,
                AutoCenter = autoCenter,
            });

        Directory.CreateDirectory(output);
        var exportJsonPath = Path.Combine(output, "navichara-export.json");
        File.WriteAllText(exportJsonPath, JsonSerializer.Serialize(result.Export, CreateUnityNavicharaJsonOptions(indented: true)), new UTF8Encoding(false));

        var diagnosticsPath = Path.Combine(output, "diagnostics.md");
        File.WriteAllText(diagnosticsPath, UnityNavicharaExporter.FormatDiagnosticsMarkdown(result.Diagnostics), new UTF8Encoding(false));

        if (rawJsonPath is not null)
        {
            var raw = new SbSceneParser().ParseFile(sbscene);
            EnsureDirectory(rawJsonPath);
            File.WriteAllText(rawJsonPath, JsonSerializer.Serialize(raw, SbSceneJson.CreateOptions(indented: true)), new UTF8Encoding(false));
        }

        Console.WriteLine($"Wrote NaviChara export JSON: {exportJsonPath}");
        Console.WriteLine($"Wrote diagnostics: {diagnosticsPath}");
        Console.WriteLine($"Nodes={result.Export.Nodes.Count}, Sprites={result.Export.Sprites.Count}, Clips={result.Export.Clips.Count}, Diagnostics={result.Diagnostics.Count}");
        if (result.Failed)
        {
            Console.Error.WriteLine(strict
                ? "export-unity-navichara failed because strict mode rejected diagnostics."
                : "export-unity-navichara failed because error diagnostics were produced.");
            return 1;
        }

        return 0;
    }

    static bool TryParseUnityNavicharaMap(string text, out UnityNavicharaAnimationMap map)
    {
        map = new UnityNavicharaAnimationMap(string.Empty, string.Empty);
        var separator = text.IndexOf('=');
        if (separator <= 0 || separator == text.Length - 1)
        {
            return false;
        }

        var source = text[..separator].Trim();
        var target = text[(separator + 1)..].Trim();
        if (source.Length == 0 || target.Length == 0)
        {
            return false;
        }

        map = new UnityNavicharaAnimationMap(source, target);
        return true;
    }

    static JsonSerializerOptions CreateUnityNavicharaJsonOptions(bool indented)
    {
        var options = SbSceneJson.CreateOptions(indented);
        options.DictionaryKeyPolicy = null;
        return options;
    }

    static void PrintExportUnityNavicharaUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  SbScene.Cli export-unity-navichara <sbscene> <svo> --out <dir> [options]");
        Console.WriteLine("  SbScene.Cli export-unity-navichara <sbscene> --write-profile-template <json> [--auto-map]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --character-id <n>            Character id for UI_Navichara_<n>. Default: 0.");
        Console.WriteLine("  --profile <json>              Profile with target clips and sourceSlots.");
        Console.WriteLine("  --map <sourceAnimation=clip>   Override a target clip's curve source. Can be repeated.");
        Console.WriteLine("  --fashion <frame>             Add/override Change_Fashion fixed slot for all core clips.");
        Console.WriteLine("  --accessory <frame>           Add/override Change_Accessory fixed slot for all core clips.");
        Console.WriteLine("  --position <frame>            Add/override Change_Position fixed slot for all core clips.");
        Console.WriteLine("  --allow-placeholder-clips      Export missing core clips as high-severity bind-pose placeholders.");
        Console.WriteLine("  --bake-sampled-curves          Emit sampled60 dense keys instead of keyed curves.");
        Console.WriteLine("  --extract-sprites              Write cropped PNG sprites under <out>/sprites.");
        Console.WriteLine("  --write-validation-frames      Render validation PNGs under <out>/validation.");
        Console.WriteLine("  --strict                       Fail on warning/high/error diagnostics.");
        Console.WriteLine("  --no-auto-center               Keep raw sbscene coordinates instead of centering the character to the origin.");
        Console.WriteLine("  --raw-json <out>               Also write the raw dump JSON.");
    }
}
