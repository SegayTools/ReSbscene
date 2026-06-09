internal static partial class CliApp
{
    static int Render(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            PrintRenderUsage();
            return args.Length == 0 ? 1 : 0;
        }

        var positionals = new List<string>();
        string? output = null;
        string? filter = null;
        var padding = 80;
        var scale = 1.0;
        var supersample = 1;
        var textureSampling = SbSceneTextureSampling.Nearest;
        var background = RgbaColor.Transparent;
        var backgroundSpecified = false;
        var showHidden = false;
        var renderSecondary = false;
        var characterDefaults = false;
        var gif = false;
        var fps = SbSceneGifAnimationSampler.DefaultFps;
        SbSceneGifFrameRange? frameRange = null;
        var gifCompress = false;
        int? gifWidth = null;
        int? gifHeight = null;
        var animations = new List<SbSceneAnimationSelection>();

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--out" when i + 1 < args.Length:
                    output = args[++i];
                    break;
                case "--filter" when i + 1 < args.Length:
                    filter = args[++i];
                    break;
                case "--padding" when i + 1 < args.Length && int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPadding):
                    padding = parsedPadding;
                    i++;
                    break;
                case "--scale" when i + 1 < args.Length && double.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedScale):
                    scale = parsedScale;
                    i++;
                    break;
                case "--supersample" when i + 1 < args.Length && int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedSupersample):
                    supersample = parsedSupersample;
                    i++;
                    break;
                case "--sampling" when i + 1 < args.Length:
                    if (!TryParseTextureSampling(args[++i], out textureSampling))
                    {
                        Console.Error.WriteLine("Invalid --sampling value. Use nearest or bilinear.");
                        return 1;
                    }

                    break;
                case "--high-quality":
                    textureSampling = SbSceneTextureSampling.Bilinear;
                    supersample = Math.Max(supersample, 4);
                    break;
                case "--background" when i + 1 < args.Length:
                    if (!TryParseColor(args[++i], out background))
                    {
                        Console.Error.WriteLine("Invalid --background value. Use transparent, #RRGGBB, or #AARRGGBB.");
                        return 1;
                    }

                    backgroundSpecified = true;
                    break;
                case "--transparent":
                    background = RgbaColor.Transparent;
                    backgroundSpecified = true;
                    break;
                case "--gif":
                    gif = true;
                    break;
                case "--gif-compress":
                    gif = true;
                    gifCompress = true;
                    break;
                case "--gif-width" when i + 1 < args.Length:
                    if (!int.TryParse(args[++i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedGifWidth) || parsedGifWidth <= 0)
                    {
                        Console.Error.WriteLine("Invalid --gif-width value. Use a positive integer pixel width.");
                        return 1;
                    }

                    gif = true;
                    gifWidth = parsedGifWidth;
                    break;
                case "--gif-height" when i + 1 < args.Length:
                    if (!int.TryParse(args[++i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedGifHeight) || parsedGifHeight <= 0)
                    {
                        Console.Error.WriteLine("Invalid --gif-height value. Use a positive integer pixel height.");
                        return 1;
                    }

                    gif = true;
                    gifHeight = parsedGifHeight;
                    break;
                case "--fps" when i + 1 < args.Length && int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedFps):
                    if (parsedFps is < SbSceneGifAnimationSampler.MinFps or > SbSceneGifAnimationSampler.MaxFps)
                    {
                        Console.Error.WriteLine($"Invalid --fps value. Use {SbSceneGifAnimationSampler.MinFps}..{SbSceneGifAnimationSampler.MaxFps}.");
                        return 1;
                    }

                    fps = parsedFps;
                    i++;
                    break;
                case "--frames" when i + 1 < args.Length:
                    if (!TryParseGifFrameRange(args[++i], out var parsedFrameRange))
                    {
                        Console.Error.WriteLine("Invalid --frames value. Use <start:end> with finite numeric frames and start <= end.");
                        return 1;
                    }

                    frameRange = parsedFrameRange;
                    break;
                case "--show-hidden":
                    showHidden = true;
                    break;
                case "--render-secondary":
                    renderSecondary = true;
                    break;
                case "--character-defaults":
                    characterDefaults = true;
                    break;
                case "--animation" or "--anim" when i + 1 < args.Length:
                    var animationOption = args[i];
                    if (!TryParseAnimationSelection(args[++i], out var selection))
                    {
                        Console.Error.WriteLine($"Invalid {animationOption} value. Use Name, #Index, Name[Frame], #Index[Frame], Name@Frame, or Name:Frame.");
                        return 1;
                    }

                    animations.Add(selection);
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

        if (positionals.Count is < 1 or > 2)
        {
            Console.Error.WriteLine("render requires <sbscene-or-dir> and optionally <svo>.");
            return 1;
        }

        if (output is null)
        {
            Console.Error.WriteLine("render requires --out <png-or-dir>.");
            return 1;
        }

        if (characterDefaults)
        {
            animations.InsertRange(0, SbSceneCharacterAnimationDefaults.BuildSelections());
        }

        if (gifWidth is not null && gifHeight is not null)
        {
            Console.Error.WriteLine("Use only one of --gif-width or --gif-height; GIF scaling preserves aspect ratio.");
            return 1;
        }

        if (string.Equals(Path.GetExtension(output), ".gif", StringComparison.OrdinalIgnoreCase))
        {
            gif = true;
        }

        var gifMatteColor = backgroundSpecified && background.A > 0
            ? new RgbaColor(background.R, background.G, background.B, byte.MaxValue)
            : new RgbaColor(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
        var options = new SbSceneRenderOptions
        {
            Padding = padding,
            Scale = scale,
            Supersample = supersample,
            TextureSampling = textureSampling,
            BackgroundColor = gif ? RgbaColor.Transparent : background,
            ShowHiddenNodes = showHidden,
            RenderSecondaryImages = renderSecondary,
            Animations = animations,
        };
        var gifOptions = new RenderGifOptions(
            gif,
            new SbSceneGifRenderOptions
            {
                Fps = fps,
                FrameRange = frameRange,
                MatteColor = gifMatteColor,
                CompressFrames = gifCompress,
                TargetWidth = gifWidth,
                TargetHeight = gifHeight,
            });

        var input = positionals[0];
        if (Directory.Exists(input))
        {
            if (positionals.Count > 1)
            {
                Console.Error.WriteLine("render directory mode does not accept an explicit <svo>; SVO files are matched per sbscene directory.");
                return 1;
            }

            return RenderDirectory(input, output, filter, options, gifOptions);
        }

        if (!File.Exists(input))
        {
            Console.Error.WriteLine($"Input does not exist: {input}");
            return 1;
        }

        var svoCandidateCount = -1;
        var svo = positionals.Count > 1 ? positionals[1] : FindSingleSvoForScene(input, out svoCandidateCount);
        if (svo is null)
        {
            Console.Error.WriteLine(svoCandidateCount == 0
                ? "render requires <svo>; no .svo files were found in the sbscene directory."
                : $"render requires an explicit <svo>; found {svoCandidateCount} .svo files in the sbscene directory.");
            return 1;
        }

        if (!File.Exists(svo))
        {
            Console.Error.WriteLine($"SVO does not exist: {svo}");
            return 1;
        }

        return RenderOne(input, svo, output, options, gifOptions);
    }

    static int RenderDirectory(string inputDirectory, string outputDirectory, string? filter, SbSceneRenderOptions options, RenderGifOptions gifOptions)
    {
        var scenePaths = Directory.EnumerateFiles(inputDirectory, "*.sbscene", SearchOption.AllDirectories)
            .Where(path => filter is null || path.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (scenePaths.Length == 0)
        {
            Console.Error.WriteLine("No sbscene files matched.");
            return 1;
        }

        Directory.CreateDirectory(outputDirectory);
        var rendered = 0;
        var skipped = 0;
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sbscene in scenePaths)
        {
            var svo = FindSingleSvoForScene(sbscene, out var svoCandidateCount);
            if (svo is null)
            {
                skipped++;
                Console.WriteLine(svoCandidateCount == 0
                    ? $"Skipped {sbscene}: no .svo files were found in the same directory."
                    : $"Skipped {sbscene}: found {svoCandidateCount} .svo files in the same directory.");
                continue;
            }

            var output = BuildBatchRenderOutputPath(inputDirectory, outputDirectory, sbscene, usedNames, gifOptions.Enabled ? ".gif" : ".png");
            var code = RenderOne(sbscene, svo, output, options, gifOptions);
            if (code != 0)
            {
                return code;
            }

            rendered++;
        }

        Console.WriteLine($"Rendered {rendered} scene(s), skipped {skipped}.");
        return rendered > 0 ? 0 : 1;
    }

    static int RenderOne(string sbscene, string svo, string output, SbSceneRenderOptions options, RenderGifOptions gifOptions)
    {
        var scene = new SbSceneParser().ParseFile(sbscene);
        if (gifOptions.Enabled)
        {
            return RenderOneGif(scene, svo, output, options, gifOptions);
        }

        var result = SbScenePngRenderer.Render(scene, svo, options);

        EnsureDirectory(output);
        PngWriter.Write(output, result.Image);
        Console.WriteLine($"Rendered {result.RenderedItemCount}/{result.CandidateItemCount} item(s) to {output} ({result.Image.Width}x{result.Image.Height}).");
        foreach (var warning in result.Warnings)
        {
            Console.WriteLine($"Warning: {warning}");
        }

        return 0;
    }

    static int RenderOneGif(SbSceneFile scene, string svo, string output, SbSceneRenderOptions options, RenderGifOptions gifOptions)
    {
        var result = SbSceneGifRenderer.Render(scene, svo, options, gifOptions.Options);
        var delayCentiseconds = Math.Max(1, (int)Math.Round(100.0 / result.Fps));
        EnsureDirectory(output);
        GifWriter.Write(output, result.Frames, delayCentiseconds, gifOptions.Options.MatteColor, compressFrames: gifOptions.Options.CompressFrames);
        var compressionText = gifOptions.Options.CompressFrames ? ", compressed" : string.Empty;
        Console.WriteLine($"Rendered {result.Frames.Count} GIF frame(s) to {output} ({result.Width}x{result.Height}, {result.Fps} fps{compressionText}, source frames {result.StartFrame:R}..{result.EndFrame:R}).");
        Console.WriteLine($"Rendered up to {result.RenderedItemCount}/{result.CandidateItemCount} item(s) per frame.");
        foreach (var warning in result.Warnings)
        {
            Console.WriteLine($"Warning: {warning}");
        }

        return 0;
    }

    static string? FindSingleSvoForScene(string sbscene, out int candidateCount)
    {
        candidateCount = 0;
        var directory = Path.GetDirectoryName(Path.GetFullPath(sbscene));
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return null;
        }

        var candidates = Directory.EnumerateFiles(directory, "*.svo", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        candidateCount = candidates.Length;
        return candidates.Length == 1 ? candidates[0] : null;
    }

    static string BuildBatchRenderOutputPath(string inputDirectory, string outputDirectory, string sbscene, ISet<string> usedNames, string extension)
    {
        var fullInput = Path.GetFullPath(inputDirectory);
        var fullScene = Path.GetFullPath(sbscene);
        var relative = Path.GetRelativePath(fullInput, fullScene);
        var directoryName = Path.GetFileName(Path.GetDirectoryName(fullScene));
        var stem = Path.GetFileNameWithoutExtension(fullScene);
        var baseName = string.IsNullOrWhiteSpace(directoryName)
            ? stem
            : string.Equals(directoryName, stem, StringComparison.OrdinalIgnoreCase)
                ? stem
                : stem.StartsWith($"{directoryName}__", StringComparison.OrdinalIgnoreCase)
                    ? stem
                    : $"{directoryName}__{stem}";

        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = Path.GetFileNameWithoutExtension(relative);
        }

        var safeName = MakeSafeFileName(baseName);
        var uniqueName = safeName;
        for (var suffix = 2; !usedNames.Add(uniqueName); suffix++)
        {
            uniqueName = $"{safeName}_{suffix}";
        }

        return Path.Combine(outputDirectory, $"{uniqueName}{extension}");
    }

    static string MakeSafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        return new string(chars);
    }

    static bool TryParseGifFrameRange(string text, out SbSceneGifFrameRange range)
    {
        range = default;
        var separator = text.IndexOf(':');
        if (separator <= 0 || separator == text.Length - 1)
        {
            return false;
        }

        if (!double.TryParse(text[..separator].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var startFrame) ||
            !double.TryParse(text[(separator + 1)..].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var endFrame))
        {
            return false;
        }

        if (!double.IsFinite(startFrame) || !double.IsFinite(endFrame) || endFrame < startFrame)
        {
            return false;
        }

        range = new SbSceneGifFrameRange(startFrame, endFrame);
        return true;
    }

    static bool TryParseAnimationSelection(string text, out SbSceneAnimationSelection selection)
    {
        return SbSceneAnimationSelectionParser.TryParse(text, out selection);
    }

    static bool TryParseTextureSampling(string text, out SbSceneTextureSampling textureSampling)
    {
        if (string.Equals(text, "nearest", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(text, "point", StringComparison.OrdinalIgnoreCase))
        {
            textureSampling = SbSceneTextureSampling.Nearest;
            return true;
        }

        if (string.Equals(text, "bilinear", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(text, "linear", StringComparison.OrdinalIgnoreCase))
        {
            textureSampling = SbSceneTextureSampling.Bilinear;
            return true;
        }

        textureSampling = SbSceneTextureSampling.Nearest;
        return false;
    }

    static bool TryParseColor(string text, out RgbaColor color)
    {
        color = RgbaColor.Transparent;
        if (string.Equals(text, "transparent", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var value = text.Trim();
        if (value.StartsWith('#'))
        {
            value = value[1..];
        }

        if (value.Length == 6)
        {
            if (!int.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
            {
                return false;
            }

            color = new RgbaColor((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), 0xFF);
            return true;
        }

        if (value.Length == 8)
        {
            if (!uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var argb))
            {
                return false;
            }

            color = new RgbaColor((byte)((argb >> 16) & 0xFF), (byte)((argb >> 8) & 0xFF), (byte)(argb & 0xFF), (byte)((argb >> 24) & 0xFF));
            return true;
        }

        return false;
    }

    static void PrintRenderUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  SbScene.Cli render <sbscene> [svo] --out <png|gif> [options]");
        Console.WriteLine("  SbScene.Cli render <dir> --out <dir> [--filter <text>] [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --character-defaults       Apply Change_Fashion, Change_Position, Change_Accessory, Action_Wait1, and Mouth_Wait1 at frame 0.");
        Console.WriteLine("  --gif                     Write animated GIF output. Also enabled automatically when --out ends with .gif.");
        Console.WriteLine("  --gif-compress            Write changed frame rectangles instead of full GIF frames.");
        Console.WriteLine("  --gif-width <px>          Resize GIF output proportionally to this width.");
        Console.WriteLine("  --gif-height <px>         Resize GIF output proportionally to this height.");
        Console.WriteLine("  --fps <n>                 GIF frame rate, 1..60. Default: 30.");
        Console.WriteLine("  --frames <start:end>      GIF source frame range override. Default: 0 to max enabled animation end frame.");
        Console.WriteLine("  --anim <name[frame]>      Enable an animation slot at a frame. Alias: --animation. #index[frame] is also accepted.");
        Console.WriteLine("                             In GIF mode, --anim <name> plays on the timeline; explicit frames stay fixed.");
        Console.WriteLine("                             Enabled slots are applied by animation index; duplicate slots use the last specified frame.");
        Console.WriteLine("  --background <color>      transparent, #RRGGBB, or #AARRGGBB. GIF output composites to an opaque matte; default is white.");
        Console.WriteLine("  --scale <n>               Output scale. Default: 1.");
        Console.WriteLine("  --sampling <mode>         Texture sampling: nearest or bilinear. Default: nearest.");
        Console.WriteLine("  --supersample <n>         Render internally at n times the output scale, then box downsample. 1..8, default: 1.");
        Console.WriteLine("  --high-quality            Shortcut for --sampling bilinear --supersample 4.");
        Console.WriteLine("  --padding <px>            Transparent padding around content. Default: 80.");
        Console.WriteLine("  --show-hidden             Render nodes even if display state is false.");
        Console.WriteLine("  --render-secondary        Deprecated; secondary CIMG references are rendered only when the CIMG surface mode uses them.");
    }

    internal sealed record RenderGifOptions(
        bool Enabled,
        SbSceneGifRenderOptions Options);
}
