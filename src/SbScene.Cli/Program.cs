using System.Globalization;
using System.Text;
using System.Text.Json;
using SbScene.Core.Output;
using SbScene.Core.Resources;
using SbScene.Core.Semantics;
using SbScene.Core.Vtbf;

Console.OutputEncoding = Encoding.UTF8;
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

try
{
    return Run(args);
}
catch (VtbfParseException ex)
{
    Console.Error.WriteLine($"Parse error: {ex.Message}");
    return 2;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}

static int Run(string[] args)
{
    if (args.Length == 0 || args[0] is "-h" or "--help")
    {
        PrintUsage();
        return 0;
    }

    return args[0] switch
    {
        "dump" => Dump(args.Skip(1).ToArray()),
        "extract-images" => ExtractImages(args.Skip(1).ToArray()),
        "inspect" => Inspect(args.Skip(1).ToArray()),
        "inspect-svo" => InspectSvo(args.Skip(1).ToArray()),
        "survey" => Survey(args.Skip(1).ToArray()),
        _ => UnknownCommand(args[0]),
    };
}

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

static int Inspect(string[] args)
{
    if (args.Length != 1)
    {
        Console.Error.WriteLine("inspect requires exactly one file path.");
        PrintUsage();
        return 1;
    }

    var file = new SbSceneParser().ParseFile(args[0]);
    Console.Write(InspectFormatter.Format(file));
    return 0;
}

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

static int InspectSvo(string[] args)
{
    if (args.Length != 1)
    {
        Console.Error.WriteLine("inspect-svo requires exactly one file path.");
        PrintUsage();
        return 1;
    }

    var header = SvoResourceParser.ParseHeaderFile(args[0]);
    var entries = SvoResourceParser.ParseDirectoryFile(args[0]);
    var metadata = SvoResourceParser.ParseMetadataFile(args[0]);
    var textures = SvoResourceParser.ParseFile(args[0]);
    var fileSize = new FileInfo(args[0]).Length;
    Console.WriteLine($"File: {args[0]}");
    Console.WriteLine($"Header: magic={header.Magic}, directoryCount={header.DirectoryCount}, headerSize=0x{header.HeaderSize:X}, tableOffset=0x{header.DirectoryTableOffset:X}, entrySize=0x{header.DirectoryEntrySize:X}");
    Console.WriteLine($"Header unknown bytes: nonZero={header.HeaderUnknownNonZeroByteCount}");
    Console.WriteLine($"Header unknown word classes: {FormatAvtsHeaderWordClassDistribution(header, entries, fileSize)}");
    Console.WriteLine("Header unknown non-zero words:");
    foreach (var word in header.UnknownWords.Where(static word => word.Value != 0))
    {
        Console.WriteLine($"  0x{word.Offset:X2}=0x{word.Value:X8} [{DescribeAvtsHeaderWord(word.Value, entries, fileSize)}]");
    }

    Console.WriteLine($"Directory entries: {entries.Count}");
    Console.WriteLine($"DDS textures: {textures.Count}");
    Console.WriteLine();
    Console.WriteLine("Directory:");
    foreach (var entry in entries)
    {
        Console.WriteLine(
            $"  [{entry.Index}] {entry.Name}: kind={entry.Kind}, seq={entry.Sequence}, offset=0x{entry.DataOffset:X}, length=0x{entry.DataLength:X}, magic={entry.DataMagic ?? "?"}, dds={entry.IsDds}, reservedNonZero={entry.ReservedNonZeroByteCount}");
    }

    Console.WriteLine($"Directory reserved summary: entriesWithNonZero={entries.Count(static entry => entry.ReservedNonZeroByteCount > 0)}/{entries.Count}, totalNonZero={entries.Sum(static entry => entry.ReservedNonZeroByteCount)}, checkedRange=+0x210..+0x3FF");

    if (metadata is not null)
    {
        Console.WriteLine();
        Console.WriteLine($"YABX metadata: dir={metadata.DirectoryIndex}, offset=0x{metadata.Offset:X}, length=0x{metadata.Length:X}, version={metadata.Version}, declaredPayload=0x{metadata.DeclaredPayloadLength:X}, headerHashCandidate=0x{metadata.HeaderHashCandidate:X}");
        Console.WriteLine($"  strings={metadata.Strings.Count}, types={metadata.TypeNames.Count}, fields={metadata.FieldNames.Count}, resources={metadata.ResourceNames.Count}");
        Console.WriteLine($"  types=[{string.Join(", ", metadata.TypeNames.Take(8))}]");
        Console.WriteLine($"  fields=[{string.Join(", ", metadata.FieldNames.Take(16))}]");
        Console.WriteLine($"  resources=[{string.Join(", ", metadata.ResourceNames.Take(16))}]");
        if (metadata.ObjectSectionOffset is not null)
        {
            Console.WriteLine($"  objectSection=0x{metadata.ObjectSectionOffset:X}, declaredObjects={metadata.DeclaredObjectCount}, parsedObjects={metadata.Objects.Count}, referenceBase=0x{metadata.ObjectReferenceBase:X}");
            Console.WriteLine($"  objectTypes=[{string.Join(", ", metadata.Objects.GroupBy(static obj => obj.TypeName ?? $"type{obj.TypeIndex}").Select(static group => $"{group.Key}:{group.Count()}"))}]");
            Console.WriteLine($"  objectFieldCoverage=full:{metadata.Objects.Count(static obj => obj.UnparsedByteCount == 0)}/{metadata.Objects.Count}, parsedBytes:{metadata.Objects.Sum(static obj => obj.ParsedFieldByteCount)}/{metadata.Objects.Sum(static obj => obj.PayloadLength)}, unparsedBytes:{metadata.Objects.Sum(static obj => obj.UnparsedByteCount)}");
        }

        Console.WriteLine("  schemas:");
        foreach (var schema in metadata.TypeSchemas)
        {
            var prefix = schema.TypeIndex is null ? schema.Name : $"{schema.TypeIndex}:{schema.Name}";
            Console.WriteLine($"    {prefix}: [{string.Join(", ", schema.FieldDescriptors.Select(static field => $"{field.Name}/{field.ValueKindName}/{field.RawDescriptorHex}@0x{field.DescriptorOffset:X}"))}]");
        }

        Console.WriteLine("  descriptor distribution:");
        foreach (var group in metadata.TypeSchemas
            .SelectMany(static schema => schema.FieldDescriptors)
            .GroupBy(static field => field.RawDescriptorHex)
            .OrderBy(static group => group.Key))
        {
            Console.WriteLine($"    {group.Key}: {group.Count()} [{string.Join(", ", group.Select(static field => field.Name).Take(8))}]");
        }

        var descriptors = metadata.TypeSchemas.SelectMany(static schema => schema.FieldDescriptors).ToArray();
        Console.WriteLine($"  descriptor byte distribution: flags=[{FormatByteDistribution(descriptors.Select(static field => field.Flags))}], valueKind=[{FormatByteDistribution(descriptors.Select(static field => field.ValueKind))}], reserved=[{FormatByteDistribution(descriptors.Select(static field => field.Reserved))}]");

        Console.WriteLine("  descriptor usage evidence:");
        foreach (var row in BuildDescriptorUsageRows(metadata))
        {
            Console.WriteLine($"    {row.OwnerType}.{row.FieldName}: raw={row.RawDescriptorHex}, descriptorKind={row.DescriptorKind}, objectKinds=[{row.ObjectKinds}], lengths=[{row.Lengths}], samples=[{row.Samples}]");
        }

        Console.WriteLine("  object field kind distribution:");
        foreach (var group in metadata.Objects
            .SelectMany(static obj => obj.Fields)
            .GroupBy(static field => field.Name, StringComparer.Ordinal)
            .OrderBy(static group => group.Key, StringComparer.Ordinal))
        {
            var kinds = string.Join(", ", group
                .GroupBy(static field => field.Kind, StringComparer.Ordinal)
                .OrderByDescending(static kindGroup => kindGroup.Count())
                .ThenBy(static kindGroup => kindGroup.Key, StringComparer.Ordinal)
                .Select(static kindGroup => $"{kindGroup.Key}:{kindGroup.Count()}"));
            var lengths = string.Join(", ", group
                .GroupBy(static field => field.Length)
                .OrderBy(static lengthGroup => lengthGroup.Key)
                .Select(static lengthGroup => $"{lengthGroup.Key}:{lengthGroup.Count()}"));
            Console.WriteLine($"    {group.Key}: {group.Count()} [{kinds}], lengths=[{lengths}]");
        }

        if (metadata.ObjectReferenceBase is not null)
        {
            Console.WriteLine("  reference map:");
            foreach (var obj in metadata.Objects)
            {
                Console.WriteLine($"    0x{obj.ReferenceId:X} -> object[{obj.Index}] {obj.TypeName ?? "?"}");
            }
        }

        Console.WriteLine("  object records:");
        foreach (var obj in metadata.Objects)
        {
            var text = string.Join(", ", obj.Strings.Select(static item => item.Text).Take(5));
            var fields = string.Join(", ", obj.Fields.Select(FormatObjectField).Take(24));
            var unparsed = obj.UnparsedByteCount == 0
                ? "0"
                : $"{obj.UnparsedByteCount}, preview={obj.UnparsedBytesPreviewHex}";
            Console.WriteLine($"    [{obj.Index}] ref=0x{obj.ReferenceId:X}, type={obj.TypeIndex}:{obj.TypeName ?? "?"}, offset=0x{obj.Offset:X}, length=0x{obj.PayloadLength:X}, parsedBytes={obj.ParsedFieldByteCount}, unparsed={unparsed}, strings=[{text}], fields=[{fields}]");
        }

        Console.WriteLine("  resource records:");
        foreach (var resource in metadata.Resources)
        {
            Console.WriteLine(
                $"    {resource.AtlasName}: objects={resource.TextureObjectIndex}/{resource.ImageObjectIndex}, refs=0x{resource.TextureReferenceId:X}/0x{resource.ImageReferenceId:X}, texImageRef=0x{resource.TextureImageReferenceId:X}, dir={resource.DirectoryIndex}, size={resource.Width}x{resource.Height}, yabxSize={resource.MetadataWidth}x{resource.MetadataHeight}, format={resource.Format}/code{resource.MetadataFormatCode}, data=0x{resource.DataOffset:X}/0x{resource.DataLength:X}, yabxData=0x{resource.MetadataDataSize:X}, file={resource.FileName}, chunk={resource.ChunkFileName}");
        }
    }

    return 0;
}

static string DescribeAvtsHeaderWord(long value, IReadOnlyList<SvoDirectoryEntry> entries, long fileSize)
{
    if (entries.Any(entry => entry.EntryOffset == value))
    {
        return "directory-entry-offset";
    }

    if (entries.Any(entry => entry.DataOffset == value))
    {
        return "payload-offset";
    }

    if (entries.Any(entry => entry.DataLength == value))
    {
        return "payload-length";
    }

    if (entries.Any(entry => entry.DataOffset + entry.DataLength == value))
    {
        return "payload-end-offset";
    }

    if (value == fileSize)
    {
        return "file-size";
    }

    if (value > 0 && value < 0x1000)
    {
        return "small-scalar-candidate";
    }

    var bytes = BitConverter.GetBytes((uint)value);
    if (bytes[1] == 0
        && bytes[3] == 0
        && bytes[0] is >= 0x20 and <= 0x7E
        && bytes[2] is >= 0x20 and <= 0x7E)
    {
        return $"utf16-chars-candidate:{(char)bytes[0]}{(char)bytes[2]}";
    }

    if (value > 0 && value < fileSize)
    {
        return "in-file-offset-candidate";
    }

    if ((value & 0xFF000000) is 0x77000000 or 0x67000000 || (value & 0xFFFF0000) == 0x00A50000)
    {
        return "pointer-or-residue-candidate";
    }

    return "unknown";
}

static string DescribeAvtsHeaderWordRelation(long value, IReadOnlyList<SvoDirectoryEntry> entries, long fileSize)
{
    if (value == 0)
    {
        return "zero";
    }

    const int directoryEntrySize = 0x400;

    if (entries.Any(entry => entry.EntryOffset == value))
    {
        return "directory-entry-offset";
    }

    if (entries.Any(entry => entry.EntryOffset + directoryEntrySize == value))
    {
        return "directory-entry-end-offset";
    }

    if (entries.Any(entry => entry.DataOffset == value))
    {
        return "payload-offset";
    }

    if (entries.Any(entry => entry.DataLength == value))
    {
        return "payload-length";
    }

    if (entries.Any(entry => entry.DataOffset + entry.DataLength == value))
    {
        return "payload-end-offset";
    }

    if (value == fileSize)
    {
        return "file-size";
    }

    if (entries.Any(entry => entry.Sequence == value))
    {
        return "entry-sequence";
    }

    if (entries.Any(entry => entry.Kind == value))
    {
        return "entry-kind";
    }

    var containingPayload = entries.FirstOrDefault(entry => entry.DataOffset <= value && value < entry.DataOffset + entry.DataLength);
    if (containingPayload is not null)
    {
        return $"inside-payload:{NormalizeAvtsPayloadMagic(containingPayload.DataMagic)}";
    }

    if (entries.Count > 0)
    {
        var directoryStart = entries.Min(static entry => entry.EntryOffset);
        var directoryEnd = entries.Max(static entry => entry.EntryOffset) + directoryEntrySize;
        if (directoryStart <= value && value < directoryEnd)
        {
            return "inside-directory-table";
        }
    }

    if (value > 0 && value < fileSize)
    {
        return "other-in-file-offset";
    }

    if (value > 0 && value < 0x1000)
    {
        return "small-scalar";
    }

    if ((value & 0xFF000000) is 0x77000000 or 0x67000000 || (value & 0xFFFF0000) == 0x00A50000)
    {
        return "pointer-or-residue";
    }

    return "out-of-file-or-unknown";
}

static string? DescribeAvtsHeaderWordPayloadLocation(long value, IReadOnlyList<SvoDirectoryEntry> entries)
{
    var containingPayload = entries.FirstOrDefault(entry => entry.DataOffset <= value && value < entry.DataOffset + entry.DataLength);
    if (containingPayload is null)
    {
        return null;
    }

    var relative = value - containingPayload.DataOffset;
    var endMinus = containingPayload.DataOffset + containingPayload.DataLength - value;
    return $"inside-payload:{NormalizeAvtsPayloadMagic(containingPayload.DataMagic)}|entry-index:{containingPayload.Index}|relative:0x{relative:X}|end-minus:0x{endMinus:X}";
}

static string NormalizeAvtsPayloadMagic(string? magic)
{
    return string.IsNullOrWhiteSpace(magic)
        ? "?"
        : magic.Trim();
}

static string NormalizeAvtsHeaderWordClass(string kind)
{
    return kind.StartsWith("utf16-chars-candidate:", StringComparison.Ordinal)
        ? "utf16-chars-candidate"
        : kind;
}

static string FormatAvtsHeaderWordClassDistribution(SvoHeaderInfo header, IReadOnlyList<SvoDirectoryEntry> entries, long fileSize)
{
    return string.Join(", ", header.UnknownWords
        .Where(static word => word.Value != 0)
        .Select(word => DescribeAvtsHeaderWord(word.Value, entries, fileSize))
        .Select(NormalizeAvtsHeaderWordClass)
        .GroupBy(static kind => kind, StringComparer.Ordinal)
        .OrderByDescending(static group => group.Count())
        .ThenBy(static group => group.Key, StringComparer.Ordinal)
        .Select(static group => $"{group.Key}:{group.Count()}"));
}

static string FormatByteDistribution(IEnumerable<byte> values)
{
    return string.Join(", ", values
        .GroupBy(static value => value)
        .OrderBy(static group => group.Key)
        .Select(static group => $"0x{group.Key:X2}:{group.Count()}"));
}

static IReadOnlyList<(string OwnerType, string FieldName, string RawDescriptorHex, string DescriptorKind, string ObjectKinds, string Lengths, string Samples)> BuildDescriptorUsageRows(SvoMetadataInfo metadata)
{
    var rows = new List<(string OwnerType, string FieldName, string RawDescriptorHex, string DescriptorKind, string ObjectKinds, string Lengths, string Samples)>();
    var resourceSchema = metadata.TypeSchemas.FirstOrDefault(static schema => schema.Name == "stevia::Resource");
    foreach (var schema in metadata.TypeSchemas)
    {
        foreach (var descriptor in schema.FieldDescriptors)
        {
            var usages = metadata.Objects
                .Where(obj => string.Equals(obj.TypeName, schema.Name, StringComparison.Ordinal))
                .SelectMany(obj => obj.Fields.Where(field => field.Name == descriptor.Name))
                .ToArray();

            if (schema.Name == "stevia::Resource")
            {
                usages = metadata.Objects
                    .SelectMany(obj => obj.Fields.Where(field => field.Name == descriptor.Name))
                    .ToArray();
            }

            rows.Add(BuildDescriptorUsageRow(schema.Name, descriptor, usages));
        }
    }

    return rows
        .OrderBy(static row => row.OwnerType, StringComparer.Ordinal)
        .ThenBy(static row => row.FieldName, StringComparer.Ordinal)
        .ToArray();
}

static (string OwnerType, string FieldName, string RawDescriptorHex, string DescriptorKind, string ObjectKinds, string Lengths, string Samples) BuildDescriptorUsageRow(
    string ownerType,
    SvoMetadataFieldInfo descriptor,
    IReadOnlyList<SvoMetadataObjectField> usages)
{
    var objectKinds = usages.Count == 0
        ? string.Empty
        : string.Join(", ", usages
            .GroupBy(static field => field.Kind, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group => $"{group.Key}:{group.Count()}"));
    var lengths = usages.Count == 0
        ? string.Empty
        : string.Join(", ", usages
            .GroupBy(static field => field.Length)
            .OrderBy(static group => group.Key)
            .Select(static group => $"{group.Key}:{group.Count()}"));
    var samples = usages.Count == 0
        ? string.Empty
        : string.Join(", ", usages.Select(FormatObjectField).Take(4));
    return (
        ownerType,
        descriptor.Name,
        descriptor.RawDescriptorHex,
        descriptor.ValueKindName,
        objectKinds,
        lengths,
        samples);
}

static string FormatObjectField(SvoMetadataObjectField field)
{
    if (field.StringValue is not null)
    {
        return $"{field.Name}=\"{field.StringValue}\"";
    }

    if (field.ReferenceId is not null)
    {
        var target = field.ReferenceTargetObjectIndex is null ? string.Empty : $"->[{field.ReferenceTargetObjectIndex}]{field.ReferenceTargetTypeName}";
        return $"{field.Name}=0x{field.ReferenceId:X}{target}";
    }

    if (field.ReferenceIds is { Count: > 0 } refs)
    {
        var values = field.ReferenceTargets is null
            ? refs.Select(static value => $"0x{value:X}")
            : field.ReferenceTargets.Select(static target => target.ObjectIndex is null
                ? $"0x{target.ReferenceId:X}"
                : $"0x{target.ReferenceId:X}->[{target.ObjectIndex}]{target.TypeName}");
        return $"{field.Name}=[{string.Join(", ", values)}]";
    }

    if (field.ReferenceIds is not null)
    {
        return $"{field.Name}=[]";
    }

    return field.IntValue is null ? field.Name : $"{field.Name}={field.IntValue}";
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

static SurveySceneNameIndex BuildSurveySceneNameIndex(IEnumerable<string> scenePaths)
{
    var directoryNames = new HashSet<string>(StringComparer.Ordinal);
    var fileStems = new HashSet<string>(StringComparer.Ordinal);
    var scenePrefixes = new HashSet<string>(StringComparer.Ordinal);
    var sceneSuffixes = new HashSet<string>(StringComparer.Ordinal);
    var fileStemsByDirectory = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
    var sceneSuffixesByDirectory = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

    foreach (var path in scenePaths)
    {
        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        var directoryName = Path.GetFileName(directory);
        var fileStem = Path.GetFileNameWithoutExtension(path) ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(directoryName))
        {
            directoryNames.Add(directoryName);
        }

        if (string.IsNullOrWhiteSpace(fileStem))
        {
            continue;
        }

        fileStems.Add(fileStem);
        scenePrefixes.Add(GetSceneStemPrefix(fileStem));
        if (TryGetSceneStemSuffix(fileStem) is { } suffix)
        {
            sceneSuffixes.Add(suffix);
        }

        if (!fileStemsByDirectory.TryGetValue(directory, out var directoryFileStems))
        {
            directoryFileStems = new HashSet<string>(StringComparer.Ordinal);
            fileStemsByDirectory.Add(directory, directoryFileStems);
        }

        directoryFileStems.Add(fileStem);

        if (!sceneSuffixesByDirectory.TryGetValue(directory, out var directorySceneSuffixes))
        {
            directorySceneSuffixes = new HashSet<string>(StringComparer.Ordinal);
            sceneSuffixesByDirectory.Add(directory, directorySceneSuffixes);
        }

        if (TryGetSceneStemSuffix(fileStem) is { } directorySuffix)
        {
            directorySceneSuffixes.Add(directorySuffix);
        }
    }

    return new SurveySceneNameIndex(
        directoryNames,
        fileStems,
        scenePrefixes,
        sceneSuffixes,
        fileStemsByDirectory.ToDictionary(
            static pair => pair.Key,
            static pair => (IReadOnlySet<string>)pair.Value,
            StringComparer.OrdinalIgnoreCase),
        sceneSuffixesByDirectory.ToDictionary(
            static pair => pair.Key,
            static pair => (IReadOnlySet<string>)pair.Value,
            StringComparer.OrdinalIgnoreCase));
}

static SceneSurveyRow BuildSceneSurveyRow(string root, string path, SurveySceneNameIndex sceneNameIndex)
{
    try
    {
        var file = new SbSceneParser().ParseFile(path);
        var blocks = FlattenSurveyBlocks(file.Vtbf.Blocks).ToArray();
        var rootBlock = file.Vtbf.Blocks.FirstOrDefault();
        var vtbfStructureSurvey = BuildVtbfStructureSurvey(blocks);
        var catrSurvey = BuildCatrSurvey(blocks);
        var scnSurvey = BuildScnSurvey(blocks);
        var layerSurvey = BuildLayerSurvey(blocks, file.Surfboard.Nodes.Count);
        var cameraSurvey = BuildCameraSurvey(blocks);
        var dataBlocks = blocks.Where(static block => block.Tag == "DATA").ToArray();
        var dataFollowingImageCastCounts = CountFollowingTags(blocks, "CIMG");
        var dataFollowingCimgCrfdCounts = CountFollowingTags(blocks, "CIMG", "CRFD");
        var dataFollowingCimgCnumCrfdCounts = CountFollowingTags(blocks, "CIMG", "CNUM", "CRFD");
        var dataFollowingCimgCnumCrfdCsliCounts = CountFollowingTags(blocks, "CIMG", "CNUM", "CRFD", "CSLI");
        var dataFollowingTagCounts = CountFollowingTagRuns(blocks);
        var tracks = file.Surfboard.Animations
            .SelectMany(static animation => animation.Motions)
            .SelectMany(static motion => motion.Tracks)
            .ToArray();
        var keys = tracks.SelectMany(static track => track.Keyframes).ToArray();
        var projectSurvey = BuildProjectSurvey(blocks, tracks);
        var packedAngleTracks = tracks
            .Where(static track => track.Keyframes.Any(static key => key.PackedAngleRaw is not null))
            .ToArray();
        var packedAngleKeys = tracks
            .SelectMany(static track => track.Keyframes
                .Where(static key => key.PackedAngleRaw is not null)
                .Select(key => new PackedAngleSurveyKey(track, key)))
            .ToArray();
        var resources = file.Surfboard.Resources;
        var ncatSurvey = BuildNcatSurvey(file);
        var cimgIndex = BuildCimgIndexSurvey(resources.ImageCasts);
        var nodeFlagBitSurvey = BuildNodeFlagBitSurvey(file);
        var cimgFlagBitSurvey = BuildCimgFlagBitSurvey(file);
        var trackFlagExtraSurvey = BuildTrackFlagExtraSurvey(file);
        var keyInterpolationTangentSurvey = BuildKeyInterpolationTangentSurvey(file);
        var trackKeyStructureSurvey = BuildTrackKeyStructureSurvey(file);
        var animationMotionStructureSurvey = BuildAnimationMotionStructureSurvey(file, blocks);
        var imageVariantSurvey = BuildImageVariantSurvey(file);
        var colorAlphaSurvey = BuildColorAlphaSurvey(file);
        var transformTrackSurvey = BuildTransformTrackSurvey(file);
        var compactTailSurvey = BuildCompactTailSurvey(file);
        var cropPackedSurvey = BuildCropPackedSurvey(resources);
        var textureAtlasSurvey = BuildTextureAtlasSurvey(resources);
        var sharedPackedStateSurvey = BuildSharedPackedStateSurvey(blocks);
        var crfdReferenceSurvey = BuildCrfdReferenceSurvey(file, path, sceneNameIndex);

        return new SceneSurveyRow
        {
            Path = path,
            RelativePath = GetRelativeSurveyPath(root, path),
            Size = file.SourceSize,
            Error = null,
            RootParamRaw = rootBlock?.ParamRawHex,
            RootParamLow = rootBlock?.ParamLow,
            RootParamHigh = rootBlock?.ParamHigh,
            TotalBlocks = file.Summary.TotalBlockCount,
            VtbfTagCounts = vtbfStructureSurvey.TagCounts,
            VtbfTagParamRawCounts = vtbfStructureSurvey.TagParamRawCounts,
            VtbfTagParamLowHighCounts = vtbfStructureSurvey.TagParamLowHighCounts,
            VtbfTagPropertyCountCounts = vtbfStructureSurvey.TagPropertyCountCounts,
            VtbfTagParamHighPropertyCountCounts = vtbfStructureSurvey.TagParamHighPropertyCountCounts,
            VtbfTagTrailingByteCounts = vtbfStructureSurvey.TagTrailingByteCounts,
            VtbfKeyParamHighModulo5Counts = vtbfStructureSurvey.KeyParamHighModulo5Counts,
            VtbfFieldDirectoryCounts = vtbfStructureSurvey.FieldDirectoryCounts,
            VtbfFieldDirectoryBlockCounts = vtbfStructureSurvey.FieldDirectoryBlockCounts,
            VtbfFieldCountValueCounts = vtbfStructureSurvey.FieldCountValueCounts,
            VtbfFieldStrideValueCounts = vtbfStructureSurvey.FieldStrideValueCounts,
            SharedPackedStateOwnerCounts = sharedPackedStateSurvey.OwnerCounts,
            SharedPackedStateOwnerRawCounts = sharedPackedStateSurvey.OwnerRawCounts,
            SharedPackedStateOwnerBitCounts = sharedPackedStateSurvey.OwnerBitCounts,
            SharedPackedStateOwnerLowNibbleCounts = sharedPackedStateSurvey.OwnerLowNibbleCounts,
            SharedPackedStateOwnerMaskF0Counts = sharedPackedStateSurvey.OwnerMaskF0Counts,
            SharedPackedStateOwnerMaskF00Counts = sharedPackedStateSurvey.OwnerMaskF00Counts,
            SharedPackedStateOwnerUpperMaskCounts = sharedPackedStateSurvey.OwnerUpperMaskCounts,
            CatrField03Counts = catrSurvey.Field03Counts,
            CatrField0DCounts = catrSurvey.Field0DCounts,
            CatrField0ECounts = catrSurvey.Field0ECounts,
            CatrField0FTypeCounts = catrSurvey.Field0FTypeCounts,
            CatrField0FPreviewCounts = catrSurvey.Field0FPreviewCounts,
            CatrFieldSequenceCounts = catrSurvey.FieldSequenceCounts,
            CatrFieldSetCounts = catrSurvey.FieldSetCounts,
            ProjectField00Counts = projectSurvey.Field00Counts,
            ProjectField01Counts = projectSurvey.Field01Counts,
            ProjectField05Counts = projectSurvey.Field05Counts,
            ProjectField55Counts = projectSurvey.Field55Counts,
            ProjectField56Counts = projectSurvey.Field56Counts,
            ProjectField56TrackLastRelationCounts = projectSurvey.Field56TrackLastRelationCounts,
            ProjectField56KeyMaxRelationCounts = projectSurvey.Field56KeyMaxRelationCounts,
            ProjectField56DeltaToTrackLastCounts = projectSurvey.Field56DeltaToTrackLastCounts,
            ProjectField56DeltaToKeyMaxCounts = projectSurvey.Field56DeltaToKeyMaxCounts,
            ProjectFieldSequenceCounts = projectSurvey.FieldSequenceCounts,
            ProjectFieldSetCounts = projectSurvey.FieldSetCounts,
            ScnNameCounts = scnSurvey.NameCounts,
            ScnField04RawHexCounts = scnSurvey.Field04RawHexCounts,
            ScnField10Counts = scnSurvey.Field10Counts,
            ScnField11Counts = scnSurvey.Field11Counts,
            ScnField40Counts = scnSurvey.Field40Counts,
            ScnField41Counts = scnSurvey.Field41Counts,
            ScnField10Field11Counts = scnSurvey.Field10Field11Counts,
            ScnField40Field41Counts = scnSurvey.Field40Field41Counts,
            ScnParamLowLayerCountDeltaCounts = scnSurvey.ParamLowLayerCountDeltaCounts,
            ScnParamLowField10DeltaCounts = scnSurvey.ParamLowField10DeltaCounts,
            ScnField10LayerCountDeltaCounts = scnSurvey.Field10LayerCountDeltaCounts,
            ScnFieldSequenceCounts = scnSurvey.FieldSequenceCounts,
            ScnFieldSetCounts = scnSurvey.FieldSetCounts,
            LayerNameCounts = layerSurvey.NameCounts,
            LayerField20Counts = layerSurvey.Field20Counts,
            LayerField20BitCounts = layerSurvey.Field20BitCounts,
            LayerField21Counts = layerSurvey.Field21Counts,
            LayerField21BitCounts = layerSurvey.Field21BitCounts,
            LayerField22Counts = layerSurvey.Field22Counts,
            LayerField22BitCounts = layerSurvey.Field22BitCounts,
            LayerField21SceneNodeCountDeltaCounts = layerSurvey.Field21SceneNodeCountDeltaCounts,
            LayerParamLowField22DeltaCounts = layerSurvey.ParamLowField22DeltaCounts,
            LayerFieldSequenceCounts = layerSurvey.FieldSequenceCounts,
            LayerFieldSetCounts = layerSurvey.FieldSetCounts,
            CameraNameCounts = cameraSurvey.NameCounts,
            CameraField12VectorCounts = cameraSurvey.Field12VectorCounts,
            CameraField13VectorCounts = cameraSurvey.Field13VectorCounts,
            CameraField14Counts = cameraSurvey.Field14Counts,
            CameraField14BitCounts = cameraSurvey.Field14BitCounts,
            CameraField15Counts = cameraSurvey.Field15Counts,
            CameraField16Counts = cameraSurvey.Field16Counts,
            CameraFieldSequenceCounts = cameraSurvey.FieldSequenceCounts,
            CameraFieldSetCounts = cameraSurvey.FieldSetCounts,
            AnimationFieldSequenceCounts = animationMotionStructureSurvey.AnimationFieldSequenceCounts,
            AnimationFieldSetCounts = animationMotionStructureSurvey.AnimationFieldSetCounts,
            AnimationParamLowMotionDeltaCounts = animationMotionStructureSurvey.AnimationParamLowMotionDeltaCounts,
            AnimationField50MotionDeltaCounts = animationMotionStructureSurvey.AnimationField50MotionDeltaCounts,
            AnimationField50MaxMotionTrackDeltaCounts = animationMotionStructureSurvey.AnimationField50MaxMotionTrackDeltaCounts,
            AnimationField50MotionOrMaxTrackRelationCounts = animationMotionStructureSurvey.AnimationField50MotionOrMaxTrackRelationCounts,
            AnimationParamLowField50DeltaCounts = animationMotionStructureSurvey.AnimationParamLowField50DeltaCounts,
            AnimationField5FCounts = animationMotionStructureSurvey.AnimationField5FCounts,
            AnimationField5FMotionPresenceCounts = animationMotionStructureSurvey.AnimationField5FMotionPresenceCounts,
            AnimationField5FAnimationNameCounts = animationMotionStructureSurvey.AnimationField5FAnimationNameCounts,
            AnimationField5FParamLowMotionDeltaCounts = animationMotionStructureSurvey.AnimationField5FParamLowMotionDeltaCounts,
            AnimationField5FField50MotionDeltaCounts = animationMotionStructureSurvey.AnimationField5FField50MotionDeltaCounts,
            AnimationField5FField50RelationCounts = animationMotionStructureSurvey.AnimationField5FField50RelationCounts,
            AnimationField5FEndFrameRelationCounts = animationMotionStructureSurvey.AnimationField5FEndFrameRelationCounts,
            AnimationEndFrameRelationCounts = animationMotionStructureSurvey.AnimationEndFrameRelationCounts,
            AnimationEndFrameDeltaToTrackLastCounts = animationMotionStructureSurvey.AnimationEndFrameDeltaToTrackLastCounts,
            AnimationEndFrameDeltaToKeyMaxCounts = animationMotionStructureSurvey.AnimationEndFrameDeltaToKeyMaxCounts,
            MotionFieldSequenceCounts = animationMotionStructureSurvey.MotionFieldSequenceCounts,
            MotionFieldSetCounts = animationMotionStructureSurvey.MotionFieldSetCounts,
            MotionParamLowTrackDeltaCounts = animationMotionStructureSurvey.MotionParamLowTrackDeltaCounts,
            MotionField52TrackDeltaCounts = animationMotionStructureSurvey.MotionField52TrackDeltaCounts,
            MotionParamLowField52DeltaCounts = animationMotionStructureSurvey.MotionParamLowField52DeltaCounts,
            MotionTargetIndexRangeCounts = animationMotionStructureSurvey.MotionTargetIndexRangeCounts,
            UnknownTypeCodeCounts = CountBy(blocks.SelectMany(static block => block.Fields).Where(static field => !field.IsKnownType).Select(static field => field.TypeHex)),
            Warnings = file.Summary.Warnings,
            NodeCount = file.Surfboard.Nodes.Count,
            Transform2DCount = file.Surfboard.Transform2DRecords.Count,
            ImageCastCount = resources.ImageCasts.Count,
            CnumCount = resources.CnumRecords.Count,
            CnumCropReferenceCount = resources.CnumRecords.Sum(static record => record.CropReferences.Count),
            CnumField44Matches = resources.CnumRecords.Count(static record => record.CropReferenceCountMatchesField44 == true),
            CnumField44Mismatches = resources.CnumRecords.Count(static record => record.CropReferenceCountMatchesField44 == false),
            CnumField44Missing = resources.CnumRecords.Count(static record => record.CropReferenceCountMatchesField44 is null),
            CnumField51InRange = resources.CnumRecords.Count(record => record.Field51 is int index && index >= 0 && index < file.Surfboard.Nodes.Count),
            CnumField51OutOfRange = resources.CnumRecords.Count(record => record.Field51 is int index && (index < 0 || index >= file.Surfboard.Nodes.Count)),
            CnumField51Missing = resources.CnumRecords.Count(static record => record.Field51 is null),
            CnumField44Counts = CountBy(resources.CnumRecords.Select(static record => FormatNullableInt(record.Field44Count))),
            CnumZeroMarkerFieldCounts = CountByHex(resources.CnumRecords.SelectMany(static record => record.ZeroLengthMarkerFieldIds).Select(static value => (int?)value)),
            CnumFieldA1Counts = CountBy(resources.CnumRecords.Select(static record => record.FieldA1)),
            CnumField48Counts = compactTailSurvey.CnumField48Counts,
            CnumFieldA0Counts = compactTailSurvey.CnumFieldA0Counts,
            CnumFieldA1RawLengthCounts = compactTailSurvey.CnumFieldA1RawLengthCounts,
            CnumFieldA1ContentLengthCounts = compactTailSurvey.CnumFieldA1ContentLengthCounts,
            CnumFieldA1Utf8StatusCounts = compactTailSurvey.CnumFieldA1Utf8StatusCounts,
            CnumFieldA1ShiftJisByteShapeCounts = compactTailSurvey.CnumFieldA1ShiftJisByteShapeCounts,
            CnumFieldA1RawPreviewCounts = compactTailSurvey.CnumFieldA1RawPreviewCounts,
            CnumFieldA1Field44Counts = compactTailSurvey.CnumFieldA1Field44Counts,
            CnumFieldA1CropReferenceCountCounts = compactTailSurvey.CnumFieldA1CropReferenceCountCounts,
            CnumFieldA1ZeroMarkerFieldCounts = compactTailSurvey.CnumFieldA1ZeroMarkerFieldCounts,
            CnumFieldA1NodeFlagCounts = compactTailSurvey.CnumFieldA1NodeFlagCounts,
            CnumFieldA1NodeGroupCounts = compactTailSurvey.CnumFieldA1NodeGroupCounts,
            CnumFieldA1DisplayCounts = compactTailSurvey.CnumFieldA1DisplayCounts,
            CnumFieldA1CimgTargetCounts = compactTailSurvey.CnumFieldA1CimgTargetCounts,
            CnumFieldA1AnimatedTargetCounts = compactTailSurvey.CnumFieldA1AnimatedTargetCounts,
            CnumFieldSequenceCounts = compactTailSurvey.CnumFieldSequenceCounts,
            CnumFieldSetCounts = compactTailSurvey.CnumFieldSetCounts,
            CrfdCount = resources.CrfdRecords.Count,
            CrfdField51InRange = resources.CrfdRecords.Count(record => record.Field51 is int index && index >= 0 && index < file.Surfboard.Nodes.Count),
            CrfdField51OutOfRange = resources.CrfdRecords.Count(record => record.Field51 is int index && (index < 0 || index >= file.Surfboard.Nodes.Count)),
            CrfdField51Missing = resources.CrfdRecords.Count(static record => record.Field51 is null),
            CrfdField90Counts = CountBy(resources.CrfdRecords.Select(static record => record.Field90)),
            CrfdField91Counts = CountBy(resources.CrfdRecords.Select(static record => record.Field91)),
            CrfdField90Field91Counts = CountBy(resources.CrfdRecords.Select(static record => $"{FormatSurveyStringKey(record.Field90)}|{FormatSurveyStringKey(record.Field91)}")),
            CrfdField90Field91Field92Counts = CountBy(resources.CrfdRecords.Select(static record => $"{FormatSurveyStringKey(record.Field90)}|{FormatSurveyStringKey(record.Field91)}|{FormatNullableInt(record.Field92)}")),
            CrfdStringFieldRelationCounts = crfdReferenceSurvey.StringFieldRelationCounts,
            CrfdStringFieldTargetTypeCounts = crfdReferenceSurvey.StringFieldTargetTypeCounts,
            CrfdField90Field91RelationCounts = crfdReferenceSurvey.Field90Field91RelationCounts,
            CrfdField90Field91EqualityCounts = crfdReferenceSurvey.Field90Field91EqualityCounts,
            CrfdField90Field91Field92RelationCounts = crfdReferenceSurvey.Field90Field91Field92RelationCounts,
            CrfdField92Counts = CountBy(resources.CrfdRecords.Select(static record => FormatNullableInt(record.Field92))),
            CrfdField93Counts = CountBy(resources.CrfdRecords.Select(static record => FormatNullableInt(record.Field93))),
            CrfdField94Counts = CountBy(resources.CrfdRecords.Select(static record => FormatNullableFloat(record.Field94))),
            CrfdField94NonZero = resources.CrfdRecords.Count(static record => Math.Abs(record.Field94 ?? 0) > 0.000001f),
            CrfdField95Counts = CountBy(resources.CrfdRecords.Select(static record => FormatNullableInt(record.Field95))),
            TextCount = resources.TextRecords.Count,
            TextField7APresent = resources.TextRecords.Count(static record => !string.IsNullOrEmpty(record.Field7A)),
            TextZeroMarkerFieldCounts = CountByHex(resources.TextRecords.SelectMany(static record => record.ZeroLengthMarkerFieldIds).Select(static value => (int?)value)),
            TextField41Counts = CountBy(resources.TextRecords.Select(static record => FormatNullableInt(record.Field41))),
            TextField78Counts = CountBy(resources.TextRecords.Select(static record => FormatNullableInt(record.Field78))),
            TextField79Counts = CountBy(resources.TextRecords.Select(static record => FormatNullableInt(record.Field79))),
            TextField7CCounts = CountBy(resources.TextRecords.Select(static record => FormatNullableInt(record.Field7C))),
            TextField7AStringCounts = compactTailSurvey.TextField7AStringCounts,
            TextField7ARawLengthCounts = compactTailSurvey.TextField7ARawLengthCounts,
            TextField7AContentLengthCounts = compactTailSurvey.TextField7AContentLengthCounts,
            TextField7AUtf8StatusCounts = compactTailSurvey.TextField7AUtf8StatusCounts,
            TextField7AShiftJisByteShapeCounts = compactTailSurvey.TextField7AShiftJisByteShapeCounts,
            TextField7AShiftJisDecodeStatusCounts = compactTailSurvey.TextField7AShiftJisDecodeStatusCounts,
            TextField7AShiftJisStringCounts = compactTailSurvey.TextField7AShiftJisStringCounts,
            TextField7ARawPreviewCounts = compactTailSurvey.TextField7ARawPreviewCounts,
            TextField7AField41Counts = compactTailSurvey.TextField7AField41Counts,
            TextField7AField78Counts = compactTailSurvey.TextField7AField78Counts,
            TextField7AField79Counts = compactTailSurvey.TextField7AField79Counts,
            TextField7AField7CCounts = compactTailSurvey.TextField7AField7CCounts,
            TextField33VectorCounts = compactTailSurvey.TextField33VectorCounts,
            TextField33RawHexCounts = compactTailSurvey.TextField33RawHexCounts,
            TextField7BPackedValuesCounts = compactTailSurvey.TextField7BPackedValuesCounts,
            TextField7BRawHexCounts = compactTailSurvey.TextField7BRawHexCounts,
            TextField78Field79Counts = compactTailSurvey.TextField78Field79Counts,
            TextZeroMarkerField7ACounts = compactTailSurvey.TextZeroMarkerField7ACounts,
            TextFieldSequenceCounts = compactTailSurvey.TextFieldSequenceCounts,
            TextFieldSetCounts = compactTailSurvey.TextFieldSetCounts,
            SliceCastCount = resources.SliceCasts.Count,
            SliceRecordCount = resources.SliceCasts.Sum(static sliceCast => sliceCast.Slices.Count),
            SliceCropReferenceCount = resources.SliceCasts.Sum(static sliceCast => sliceCast.CropReferences.Count),
            SliceField44SlicRecordMatches = resources.SliceCasts.Count(static sliceCast => sliceCast.SlicRecordCountMatchesField44 == true),
            SliceField44SlicRecordMismatches = resources.SliceCasts.Count(static sliceCast => sliceCast.SlicRecordCountMatchesField44 == false),
            SliceField44CropReferenceMatches = resources.SliceCasts.Count(static sliceCast => sliceCast.CropReferenceCountMatchesField44 == true),
            SliceField44CropReferenceMismatches = resources.SliceCasts.Count(static sliceCast => sliceCast.CropReferenceCountMatchesField44 == false),
            SliceTargetIndexInRange = resources.SliceCasts.Count(sliceCast => sliceCast.TargetIndex is int index && index >= 0 && index < file.Surfboard.Nodes.Count),
            SliceTargetIndexOutOfRange = resources.SliceCasts.Count(sliceCast => sliceCast.TargetIndex is int index && (index < 0 || index >= file.Surfboard.Nodes.Count)),
            SliceField83Counts = CountByHex(resources.SliceCasts
                .SelectMany(static sliceCast => sliceCast.Slices)
                .Select(static slice => slice.Field83)),
            SliceCastField40Counts = compactTailSurvey.SliceCastField40Counts,
            SliceCastField41Counts = compactTailSurvey.SliceCastField41Counts,
            SliceCastField42Counts = compactTailSurvey.SliceCastField42Counts,
            SliceCastField43Counts = compactTailSurvey.SliceCastField43Counts,
            SliceCastField80Counts = compactTailSurvey.SliceCastField80Counts,
            SliceCastField81Counts = compactTailSurvey.SliceCastField81Counts,
            SliceCastField82Counts = compactTailSurvey.SliceCastField82Counts,
            SliceCastField84Counts = compactTailSurvey.SliceCastField84Counts,
            SliceCastField85Counts = compactTailSurvey.SliceCastField85Counts,
            SliceCastField86Counts = compactTailSurvey.SliceCastField86Counts,
            SliceCastField87Counts = compactTailSurvey.SliceCastField87Counts,
            SliceCastTargetNodeFlagCounts = compactTailSurvey.SliceCastTargetNodeFlagCounts,
            SliceCastTargetNodeGroupCounts = compactTailSurvey.SliceCastTargetNodeGroupCounts,
            SliceCastTargetDisplayCounts = compactTailSurvey.SliceCastTargetDisplayCounts,
            SliceCastTargetCimgTargetCounts = compactTailSurvey.SliceCastTargetCimgTargetCounts,
            SliceCastFieldSequenceCounts = compactTailSurvey.SliceCastFieldSequenceCounts,
            SliceCastFieldSetCounts = compactTailSurvey.SliceCastFieldSetCounts,
            SliceRecordField40Counts = compactTailSurvey.SliceRecordField40Counts,
            SliceRecordField41Counts = compactTailSurvey.SliceRecordField41Counts,
            SliceRecordField45Counts = compactTailSurvey.SliceRecordField45Counts,
            SliceRecordField37ColorCounts = compactTailSurvey.SliceRecordField37ColorCounts,
            SliceRecordField38ColorCounts = compactTailSurvey.SliceRecordField38ColorCounts,
            SliceRecordField39ColorCounts = compactTailSurvey.SliceRecordField39ColorCounts,
            SliceRecordField39ColorCountCounts = compactTailSurvey.SliceRecordField39ColorCountCounts,
            SliceRecordField83Field40Counts = compactTailSurvey.SliceRecordField83Field40Counts,
            SliceRecordField83Field41Counts = compactTailSurvey.SliceRecordField83Field41Counts,
            SliceRecordField83Field45Counts = compactTailSurvey.SliceRecordField83Field45Counts,
            SliceRecordFieldSequenceCounts = compactTailSurvey.SliceRecordFieldSequenceCounts,
            SliceRecordFieldSetCounts = compactTailSurvey.SliceRecordFieldSetCounts,
            SliceRecordShapeCounts = compactTailSurvey.SliceRecordShapeCounts,
            DataBlockCount = dataBlocks.Length,
            DataParamLowValues = dataBlocks.Select(static block => block.ParamLow).Where(static value => value is not null).Select(static value => value!.Value).ToArray(),
            DataFollowingImageCastCounts = dataFollowingImageCastCounts,
            DataFollowingCimgCrfdCounts = dataFollowingCimgCrfdCounts,
            DataFollowingCimgCnumCrfdCounts = dataFollowingCimgCnumCrfdCounts,
            DataFollowingCimgCnumCrfdCsliCounts = dataFollowingCimgCnumCrfdCsliCounts,
            DataFollowingTagCounts = dataFollowingTagCounts,
            DataFields = dataBlocks.Sum(static block => block.Fields.Count),
            DataTrailingBytes = dataBlocks.Sum(static block => block.TrailingBytes?.Length ?? 0),
            DataParamLowMatchesImageCasts = dataBlocks.Length == 1 && dataBlocks[0].ParamLow == resources.ImageCasts.Count,
            DataParamLowMatchesFollowingImageCasts = dataBlocks.Length == 0
                ? null
                : dataBlocks.Length == dataFollowingImageCastCounts.Count
                  && dataBlocks.Zip(dataFollowingImageCastCounts).All(static pair => pair.First.ParamLow == pair.Second),
            DataParamLowMatchesFollowingCimgCrfd = dataBlocks.Length == 0
                ? null
                : dataBlocks.Length == dataFollowingCimgCrfdCounts.Count
                  && dataBlocks.Zip(dataFollowingCimgCrfdCounts).All(static pair => pair.First.ParamLow == pair.Second),
            DataParamLowMatchesFollowingCimgCnumCrfd = dataBlocks.Length == 0
                ? null
                : dataBlocks.Length == dataFollowingCimgCnumCrfdCounts.Count
                  && dataBlocks.Zip(dataFollowingCimgCnumCrfdCounts).All(static pair => pair.First.ParamLow == pair.Second),
            DataParamLowMatchesFollowingCimgCnumCrfdCsli = dataBlocks.Length == 0
                ? null
                : dataBlocks.Length == dataFollowingCimgCnumCrfdCsliCounts.Count
                  && dataBlocks.Zip(dataFollowingCimgCnumCrfdCsliCounts).All(static pair => pair.First.ParamLow == pair.Second),
            NcatRecordCount = file.Surfboard.NodeCategoryRecords.Count,
            NcatDetailRecordCount = file.Surfboard.NodeCategoryDetails.Count,
            NcatNonZeroCount = file.Surfboard.NodeCategoryRecords.Count(static value => value != 0),
            NcatMatchesNodes = file.Surfboard.NodeCategoryRecords.Count == file.Surfboard.Nodes.Count,
            NcatRecordsWithCategory = file.Surfboard.NodeCategoryDetails.Count(static record => record.CategoryId is not null),
            NcatRecordsWithoutCategory = file.Surfboard.NodeCategoryDetails.Count(static record => record.CategoryId is null),
            NcatKindCounts = CountBy(file.Surfboard.NodeCategoryDetails.Select(static record => NormalizeNcatKind(record.KindName))),
            NcatTypeByteCounts = CountByHex(file.Surfboard.NodeCategoryDetails.Select(static record => record.TypeByte)),
            NcatCategoryCounts = CountBy(file.Surfboard.NodeCategoryDetails.Select(static record => FormatNullableInt(record.CategoryId))),
            NcatKindTypeByteCounts = ncatSurvey.KindTypeByteCounts,
            NcatKindCategoryCounts = ncatSurvey.KindCategoryCounts,
            NcatTypeByteCategoryCounts = ncatSurvey.TypeByteCategoryCounts,
            NcatKindParameterPresenceCounts = ncatSurvey.KindParameterPresenceCounts,
            NcatParameterStringCounts = ncatSurvey.ParameterStringCounts,
            NcatParameterFieldTypeCounts = ncatSurvey.ParameterFieldTypeCounts,
            NcatKindParameterFieldTypeCounts = ncatSurvey.KindParameterFieldTypeCounts,
            NcatCategoryParameterFieldTypeCounts = ncatSurvey.CategoryParameterFieldTypeCounts,
            NcatParameterFieldTypePreviewCounts = ncatSurvey.ParameterFieldTypePreviewCounts,
            NcatKindNodeFlagCounts = ncatSurvey.KindNodeFlagCounts,
            NcatKindNodeFlagBitCounts = ncatSurvey.KindNodeFlagBitCounts,
            NcatKindNodeGroupCounts = ncatSurvey.KindNodeGroupCounts,
            NcatKindDisplayCounts = ncatSurvey.KindDisplayCounts,
            NcatKindCimgTargetCounts = ncatSurvey.KindCimgTargetCounts,
            NcatKindAnimatedNodeCounts = ncatSurvey.KindAnimatedNodeCounts,
            NodeFlagCounts = CountByHex(file.Surfboard.Nodes.Select(static node => node.Flags)),
            NodeFlagBitCounts = CountBy(file.Surfboard.Nodes.SelectMany(static node => node.FlagBits).Select(static bit => bit.ToString())),
            NodeFlagBitDisplayFalseNodeCounts = nodeFlagBitSurvey.DisplayFalseNodeCounts,
            NodeFlagBitCimgTargetNodeCounts = nodeFlagBitSurvey.CimgTargetNodeCounts,
            NodeFlagBitAnimatedNodeCounts = nodeFlagBitSurvey.AnimatedNodeCounts,
            NodeFlagBitDataNodeCounts = nodeFlagBitSurvey.DataNodeCounts,
            NodeFlagBitCategoryRecordNodeCounts = nodeFlagBitSurvey.CategoryRecordNodeCounts,
            NodeFlagBitCategoryNonZeroNodeCounts = nodeFlagBitSurvey.CategoryNonZeroNodeCounts,
            NodeFlagBitExactFlagCounts = nodeFlagBitSurvey.ExactFlagCounts,
            NodeFlagBitGroupCounts = nodeFlagBitSurvey.GroupCounts,
            NodeFlagBitImageCastFlagBitCounts = nodeFlagBitSurvey.ImageCastFlagBitCounts,
            NodeFlagBitTrackTypeCounts = nodeFlagBitSurvey.TrackTypeCounts,
            NodeFlagBitPairCounts = nodeFlagBitSurvey.PairCounts,
            Cimg44Matches = resources.ImageCasts.Count(static image => image.CropReferenceCountMatches == true),
            Cimg44Mismatches = resources.ImageCasts.Count(static image => image.CropReferenceCountMatches == false),
            Cimg44Unknown = resources.ImageCasts.Count(static image => image.CropReferenceCountMatches is null),
            Cimg44CountTupleCounts = cimgIndex.CountTupleCounts,
            Cimg44PrimaryCountCounts = cimgIndex.PrimaryCountCounts,
            Cimg44SecondaryCountCounts = cimgIndex.SecondaryCountCounts,
            Cimg44SecondaryNonZeroSamples = cimgIndex.SecondaryNonZeroSamples,
            Cimg45ActiveGroups = cimgIndex.ActiveGroups,
            Cimg45InRangeGroups = cimgIndex.InRangeGroups,
            Cimg45OutOfRangeGroups = cimgIndex.OutOfRangeGroups,
            Cimg45EmptyGroupNonZero = cimgIndex.EmptyGroupNonZeroIndices,
            Cimg45NonZeroIndices = cimgIndex.NonZeroIndices,
            Cimg45NonZeroImageCasts = cimgIndex.NonZeroImageCasts,
            Cimg45GroupIndexCounts = cimgIndex.GroupIndexCounts,
            Cimg45GroupCountIndexCounts = cimgIndex.GroupCountIndexCounts,
            Cimg45NonZeroGroupCounts = cimgIndex.NonZeroGroupCounts,
            Cimg45NonZeroSamples = cimgIndex.NonZeroSamples,
            CimgFlagCounts = CountByHex(resources.ImageCasts.Select(static image => (int?)image.ImageCastFlags)),
            CimgFlagBitCounts = CountBy(resources.ImageCasts.SelectMany(static image => image.ImageCastFlagBits).Select(static bit => bit.ToString())),
            CimgFlagBitDisplayFalseCounts = cimgFlagBitSurvey.DisplayFalseCounts,
            CimgFlagBitMultiReferenceCounts = cimgFlagBitSurvey.MultiReferenceCounts,
            CimgFlagBitSecondaryReferenceCounts = cimgFlagBitSurvey.SecondaryReferenceCounts,
            CimgFlagBitNonZeroReferenceIndexCounts = cimgFlagBitSurvey.NonZeroReferenceIndexCounts,
            CimgFlagBitMissingNodeCounts = cimgFlagBitSurvey.MissingNodeCounts,
            CimgFlagBitNodeFlagCounts = cimgFlagBitSurvey.NodeFlagCounts,
            CimgFlagBitGroupCounts = cimgFlagBitSurvey.GroupCounts,
            CimgFlagBitPairCounts = cimgFlagBitSurvey.PairCounts,
            TextureAtlasCount = textureAtlasSurvey.AtlasCount,
            TextureAtlasField62Counts = textureAtlasSurvey.Field62Counts,
            TextureAtlasField62BitCounts = textureAtlasSurvey.Field62BitCounts,
            TextureAtlasField62CropCountCounts = textureAtlasSurvey.Field62CropCountCounts,
            TextureAtlasField62SizeCounts = textureAtlasSurvey.Field62SizeCounts,
            CropKindCounts = CountByHex(resources.Atlases.SelectMany(static atlas => atlas.Crops).Select(static crop => (int?)crop.Kind)),
            CrefKindCounts = cropPackedSurvey.ReferenceKindCounts,
            CropRectCount = cropPackedSurvey.CropRectCount,
            CropAtlasDeclaredCountMatches = cropPackedSurvey.AtlasDeclaredCountMatches,
            CropAtlasDeclaredCountMismatches = cropPackedSurvey.AtlasDeclaredCountMismatches,
            CropRectInAtlasBounds = cropPackedSurvey.CropRectInAtlasBounds,
            CropRectOutOfAtlasBounds = cropPackedSurvey.CropRectOutOfAtlasBounds,
            CropRectNonPositiveSize = cropPackedSurvey.CropRectNonPositiveSize,
            CropReferenceCount = cropPackedSurvey.CropReferenceCount,
            CropReferenceKindCounts = cropPackedSurvey.ReferenceKindCounts,
            CropReferenceOwnerCounts = cropPackedSurvey.ReferenceOwnerCounts,
            CropReferenceOwnerKindCounts = cropPackedSurvey.ReferenceOwnerKindCounts,
            CropReferenceTextureListIndexCounts = cropPackedSurvey.ReferenceTextureListIndexCounts,
            CropReferenceTextureIndexRangeCounts = cropPackedSurvey.ReferenceTextureIndexRangeCounts,
            CropReferenceCropIndexRangeCounts = cropPackedSurvey.ReferenceCropIndexRangeCounts,
            CropRectOutOfAtlasBoundsReasonCounts = cropPackedSurvey.CropRectOutOfAtlasBoundsReasonCounts,
            CropReferenceOutOfRangeOwnerCounts = cropPackedSurvey.ReferenceOutOfRangeOwnerCounts,
            CropRectOutOfAtlasBoundsSamples = cropPackedSurvey.CropRectOutOfAtlasBoundsSamples,
            CropReferenceOutOfRangeSamples = cropPackedSurvey.ReferenceOutOfRangeSamples,
            TrackCount = tracks.Length,
            TrackKeyCountMismatches = tracks.Count(static track => track.KeyCountMatchesDeclaration == false),
            TrackFlagCounts = CountByHex(tracks.Select(static track => track.Flags)),
            TrackFlagBaseCounts = CountByHex(tracks.Select(static track => track.Flags is null ? (int?)null : track.Flags.Value & 0xFF)),
            TrackFlagExtraCounts = CountByHex(tracks.Select(static track => track.Flags is null ? (int?)null : track.Flags.Value & ~0xFF)),
            TrackFlagExtraSceneCounts = CountBy(tracks
                .Select(static track => FormatNullableHex(track.Flags is null ? null : track.Flags.Value & ~0xFF))
                .Distinct(StringComparer.Ordinal)),
            TrackFlagExtraBaseCounts = trackFlagExtraSurvey.BaseCounts,
            TrackFlagExtraAnimationCounts = trackFlagExtraSurvey.AnimationCounts,
            TrackFlagExtraTrackTypeCounts = trackFlagExtraSurvey.TrackTypeCounts,
            TrackFlagExtraKeyValueTypeCounts = trackFlagExtraSurvey.KeyValueTypeCounts,
            TrackFlagExtraNodeFlagCounts = trackFlagExtraSurvey.NodeFlagCounts,
            TrackFlagExtraNodeFlagBitCounts = trackFlagExtraSurvey.NodeFlagBitCounts,
            TrackFlagExtraGroupCounts = trackFlagExtraSurvey.GroupCounts,
            TrackFlagExtraCimgTargetCounts = trackFlagExtraSurvey.CimgTargetCounts,
            TrackFlagExtraInitialDisplayCounts = trackFlagExtraSurvey.InitialDisplayCounts,
            TrackFlagExtraCimgFlagCounts = trackFlagExtraSurvey.CimgFlagCounts,
            TrackFlagExtraCimgFlagBitCounts = trackFlagExtraSurvey.CimgFlagBitCounts,
            TrackFlagExtraCimgReferenceCountCounts = trackFlagExtraSurvey.CimgReferenceCountCounts,
            TrackTypeCounts = CountBy(tracks.Select(static track => FormatNullableInt(track.TrackType))),
            KeyValueTypeCounts = CountBy(keys.Select(static key => $"{key.KeyValueTypeHex ?? "?"} {key.KeyValueKind ?? key.KeyValueTypeName ?? "?"}")),
            KeyTangentPresent = keys.Count(static key => key.TangentIn is not null || key.TangentOut is not null),
            KeyTangentNonZero = keys.Count(static key => IsNonZero(key.TangentIn) || IsNonZero(key.TangentOut)),
            KeyTangentMismatch = keys.Count(static key => key.TangentIn is not null && key.TangentOut is not null && Math.Abs(key.TangentIn.Value - key.TangentOut.Value) > 0.000001),
            KeyInterpolationCounts = keyInterpolationTangentSurvey.InterpolationCounts,
            KeyInterpolationTrackTypeCounts = keyInterpolationTangentSurvey.InterpolationTrackTypeCounts,
            KeyInterpolationKeyValueTypeCounts = keyInterpolationTangentSurvey.InterpolationKeyValueTypeCounts,
            KeyTangentPresentInterpolationCounts = keyInterpolationTangentSurvey.TangentPresentInterpolationCounts,
            KeyTangentPresentTrackTypeCounts = keyInterpolationTangentSurvey.TangentPresentTrackTypeCounts,
            KeyTangentNonZeroInterpolationCounts = keyInterpolationTangentSurvey.TangentNonZeroInterpolationCounts,
            KeyTangentNonZeroTrackTypeCounts = keyInterpolationTangentSurvey.TangentNonZeroTrackTypeCounts,
            KeyTangentMismatchInterpolationCounts = keyInterpolationTangentSurvey.TangentMismatchInterpolationCounts,
            KeyTangentMismatchTrackTypeCounts = keyInterpolationTangentSurvey.TangentMismatchTrackTypeCounts,
            KeyTangentMismatchAnimationCounts = keyInterpolationTangentSurvey.TangentMismatchAnimationCounts,
            KeyTangentMismatchNodeFlagCounts = keyInterpolationTangentSurvey.TangentMismatchNodeFlagCounts,
            KeyTangentMismatchGroupCounts = keyInterpolationTangentSurvey.TangentMismatchGroupCounts,
            KeyTangentMismatchTrackExtraCounts = keyInterpolationTangentSurvey.TangentMismatchTrackExtraCounts,
            KeyTangentMismatchTangentPairCounts = keyInterpolationTangentSurvey.TangentMismatchTangentPairCounts,
            KeyTangentNonZeroFramePositionCounts = keyInterpolationTangentSurvey.TangentNonZeroFramePositionCounts,
            KeyTangentMismatchFramePositionCounts = keyInterpolationTangentSurvey.TangentMismatchFramePositionCounts,
            KeyTangentDeltaSignCounts = keyInterpolationTangentSurvey.TangentDeltaSignCounts,
            TrackKeyStorageMatrixCounts = trackKeyStructureSurvey.StorageMatrixCounts,
            TrackFieldSequenceCounts = trackKeyStructureSurvey.TrackFieldSequenceCounts,
            KeyFieldSequenceCounts = trackKeyStructureSurvey.KeyFieldSequenceCounts,
            TrackFrameRangeRelationCounts = trackKeyStructureSurvey.FrameRangeRelationCounts,
            TrackKeyFrameOrderCounts = trackKeyStructureSurvey.KeyFrameOrderCounts,
            TrackKeyFrameDuplicateCounts = trackKeyStructureSurvey.KeyFrameDuplicateCounts,
            TrackFirstFrameDeltaCounts = trackKeyStructureSurvey.FirstFrameDeltaCounts,
            TrackLastFrameDeltaCounts = trackKeyStructureSurvey.LastFrameDeltaCounts,
            TransformTrackCount = transformTrackSurvey.TrackCount,
            TransformTrackKeyCount = transformTrackSurvey.KeyCount,
            TransformTracksWithInitialChannel = transformTrackSurvey.TracksWithInitialChannel,
            TransformTracksMissingInitialChannel = transformTrackSurvey.TracksMissingInitialChannel,
            TransformTrackInitialValueMatches = transformTrackSurvey.InitialValueMatches,
            TransformTrackInitialValueMismatches = transformTrackSurvey.InitialValueMismatches,
            TransformTrackKeysMissingValue = transformTrackSurvey.KeysMissingValue,
            TransformTrackTypeCounts = transformTrackSurvey.TrackTypeCounts,
            TransformTrackKeyTypeCounts = transformTrackSurvey.KeyTypeCounts,
            TransformTrackStorageCounts = transformTrackSurvey.StorageCounts,
            TransformTrackKeyValueKindCounts = transformTrackSurvey.KeyValueKindCounts,
            TransformTrackInitialMatchTypeCounts = transformTrackSurvey.InitialMatchTypeCounts,
            TransformTrackValueRangeCounts = transformTrackSurvey.ValueRangeCounts,
            TransformCandidateDefaultKeyCounts = transformTrackSurvey.CandidateDefaultKeyCounts,
            PackedAngleTrackCount = packedAngleTracks.Length,
            PackedAngleKeyCount = packedAngleKeys.Length,
            PackedAngleTrackTypeCounts = CountBy(packedAngleTracks.Select(FormatPackedAngleTrackTypeKey)),
            PackedAngleKeyTrackTypeCounts = CountBy(packedAngleKeys.Select(static item => FormatPackedAngleTrackTypeKey(item.Track))),
            PackedAngleRawCounts = CountBy(packedAngleKeys.Select(static item => item.Key.PackedAngleRaw?.ToString(CultureInfo.InvariantCulture))),
            PackedAngleDegreeCandidateCounts = CountBy(packedAngleKeys.Select(static item => FormatPackedAngleDegreesCandidate(item.Key.PackedAngleDegreesCandidate))),
            ImageVariantTrackCount = imageVariantSurvey.TrackCount,
            ImageVariantKeyCount = imageVariantSurvey.KeyCount,
            ImageVariantTracksWithCimg = imageVariantSurvey.TracksWithCimg,
            ImageVariantTracksMissingCimg = imageVariantSurvey.TracksMissingCimg,
            ImageVariantTrackRangeMatches = imageVariantSurvey.TrackRangeMatches,
            ImageVariantTrackRangeMismatches = imageVariantSurvey.TrackRangeMismatches,
            ImageVariantKeysInRange = imageVariantSurvey.KeysInRange,
            ImageVariantKeysOutOfRange = imageVariantSurvey.KeysOutOfRange,
            ImageVariantKeysMissingCimg = imageVariantSurvey.KeysMissingCimg,
            ImageVariantKeysNonInteger = imageVariantSurvey.KeysNonInteger,
            ImageVariantKeysMissingValue = imageVariantSurvey.KeysMissingValue,
            ImageVariantReferenceCountCounts = imageVariantSurvey.ReferenceCountCounts,
            ImageVariantValueCounts = imageVariantSurvey.ValueCounts,
            ImageVariantGroupTrackCounts = imageVariantSurvey.GroupTrackCounts,
            ImageVariantGroupKeyCounts = imageVariantSurvey.GroupKeyCounts,
            ImageVariantGroupTracksWithCimgCounts = imageVariantSurvey.GroupTracksWithCimgCounts,
            ImageVariantGroupTracksMissingCimgCounts = imageVariantSurvey.GroupTracksMissingCimgCounts,
            ImageVariantGroupTrackRangeMatchCounts = imageVariantSurvey.GroupTrackRangeMatchCounts,
            ImageVariantGroupTrackRangeMismatchCounts = imageVariantSurvey.GroupTrackRangeMismatchCounts,
            ImageVariantGroupKeysInRangeCounts = imageVariantSurvey.GroupKeysInRangeCounts,
            ImageVariantGroupKeysOutOfRangeCounts = imageVariantSurvey.GroupKeysOutOfRangeCounts,
            ImageVariantGroupKeysMissingCimgCounts = imageVariantSurvey.GroupKeysMissingCimgCounts,
            ImageVariantGroupKeysNonIntegerCounts = imageVariantSurvey.GroupKeysNonIntegerCounts,
            ImageVariantGroupKeysMissingValueCounts = imageVariantSurvey.GroupKeysMissingValueCounts,
            ImageVariantGroupReferenceCountCounts = imageVariantSurvey.GroupReferenceCountCounts,
            ImageVariantGroupValueCounts = imageVariantSurvey.GroupValueCounts,
            ImageVariantGroupCimg45FirstKeyRelationCounts = imageVariantSurvey.GroupCimg45FirstKeyRelationCounts,
            ImageVariantGroupCimg45FirstKeyDeltaCounts = imageVariantSurvey.GroupCimg45FirstKeyDeltaCounts,
            ImageVariantGroupCimg45FirstKeyPairCounts = imageVariantSurvey.GroupCimg45FirstKeyPairCounts,
            ColorTrackCount = colorAlphaSurvey.ColorTrackCount,
            ColorTrackKeyCount = colorAlphaSurvey.ColorTrackKeyCount,
            ColorTracksWithInitialChannel = colorAlphaSurvey.ColorTracksWithInitialChannel,
            ColorTracksMissingInitialChannel = colorAlphaSurvey.ColorTracksMissingInitialChannel,
            ColorTrackInitialValueMatches = colorAlphaSurvey.ColorTrackInitialValueMatches,
            ColorTrackInitialValueMismatches = colorAlphaSurvey.ColorTrackInitialValueMismatches,
            ColorTrackKeysInUnitRange = colorAlphaSurvey.ColorTrackKeysInUnitRange,
            ColorTrackKeysOutOfUnitRange = colorAlphaSurvey.ColorTrackKeysOutOfUnitRange,
            ColorTrackKeysMissingValue = colorAlphaSurvey.ColorTrackKeysMissingValue,
            ColorTrackTypeCounts = colorAlphaSurvey.ColorTrackTypeCounts,
            ColorTrackKeyTypeCounts = colorAlphaSurvey.ColorTrackKeyTypeCounts,
            ColorTrackInitialMatchTypeCounts = colorAlphaSurvey.ColorTrackInitialMatchTypeCounts,
            AlphaOpacityTrackCount = colorAlphaSurvey.AlphaOpacityTrackCount,
            AlphaOpacityKeyCount = colorAlphaSurvey.AlphaOpacityKeyCount,
            AlphaOpacityTracksWithMaterialAlpha = colorAlphaSurvey.AlphaOpacityTracksWithMaterialAlpha,
            AlphaOpacityTracksMissingMaterialAlpha = colorAlphaSurvey.AlphaOpacityTracksMissingMaterialAlpha,
            AlphaOpacityInitialAlphaMatches = colorAlphaSurvey.AlphaOpacityInitialAlphaMatches,
            AlphaOpacityInitialAlphaMismatches = colorAlphaSurvey.AlphaOpacityInitialAlphaMismatches,
            AlphaOpacityCimgTargets = colorAlphaSurvey.AlphaOpacityCimgTargets,
            AlphaOpacityDisplayFalseTargets = colorAlphaSurvey.AlphaOpacityDisplayFalseTargets,
            AlphaOpacityKeysInUnitRange = colorAlphaSurvey.AlphaOpacityKeysInUnitRange,
            AlphaOpacityKeysOutOfUnitRange = colorAlphaSurvey.AlphaOpacityKeysOutOfUnitRange,
            AlphaOpacityKeysMissingValue = colorAlphaSurvey.AlphaOpacityKeysMissingValue,
        };
    }
    catch (Exception ex)
    {
        return new SceneSurveyRow
        {
            Path = path,
            RelativePath = GetRelativeSurveyPath(root, path),
            Size = new FileInfo(path).Length,
            Error = ex.Message,
        };
    }
}

static SvoSurveyRow BuildSvoSurveyRow(string root, string path)
{
    try
    {
        var header = SvoResourceParser.ParseHeaderFile(path);
        var entries = SvoResourceParser.ParseDirectoryFile(path);
        var metadata = SvoResourceParser.ParseMetadataFile(path);
        var textures = SvoResourceParser.ParseFile(path);
        var fileSize = new FileInfo(path).Length;
        var descriptors = metadata?.TypeSchemas.SelectMany(static schema => schema.FieldDescriptors).ToArray() ?? [];
        var descriptorUsageRows = metadata is null ? [] : BuildDescriptorUsageRows(metadata);
        var expectedObjectCountFromDds = metadata is null ? (int?)null : 6 + textures.Count * 2;

        return new SvoSurveyRow
        {
            Path = path,
            RelativePath = GetRelativeSurveyPath(root, path),
            Size = fileSize,
            Error = null,
            DirectoryCount = header.DirectoryCount,
            HeaderUnknownNonZeroBytes = header.HeaderUnknownNonZeroByteCount,
            HeaderUnknownWordClassCounts = CountBy(header.UnknownWords
                .Where(static word => word.Value != 0)
                .Select(word => DescribeAvtsHeaderWord(word.Value, entries, fileSize))
                .Select(NormalizeAvtsHeaderWordClass)),
            HeaderUnknownNonZeroOffsetCounts = CountBy(header.HeaderUnknownNonZeroByteOffsets
                .Select(static offset => $"0x{offset:X2}")),
            HeaderUnknownWordValueCounts = CountBy(header.UnknownWords
                .Where(static word => word.Value != 0)
                .Select(static word => $"0x{word.Value:X8}")),
            HeaderUnknownWordOffsetValueCounts = CountBy(header.UnknownWords
                .Select(static word => $"0x{word.Offset:X2}|0x{word.Value:X8}")),
            HeaderUnknownWordOffsetClassCounts = CountBy(header.UnknownWords
                .Where(static word => word.Value != 0)
                .Select(word => $"0x{word.Offset:X2}|{NormalizeAvtsHeaderWordClass(DescribeAvtsHeaderWord(word.Value, entries, fileSize))}")),
            HeaderUnknownWordRelationCounts = CountBy(header.UnknownWords
                .Where(static word => word.Value != 0)
                .Select(word => DescribeAvtsHeaderWordRelation(word.Value, entries, fileSize))),
            HeaderUnknownWordOffsetRelationCounts = CountBy(header.UnknownWords
                .Where(static word => word.Value != 0)
                .Select(word => $"0x{word.Offset:X2}|{DescribeAvtsHeaderWordRelation(word.Value, entries, fileSize)}")),
            HeaderUnknownWordPayloadLocationCounts = CountBy(header.UnknownWords
                .Where(static word => word.Value != 0)
                .Select(word => DescribeAvtsHeaderWordPayloadLocation(word.Value, entries))
                .OfType<string>()),
            HeaderUnknownWordOffsetPayloadLocationCounts = CountBy(header.UnknownWords
                .Where(static word => word.Value != 0)
                .Select(word => DescribeAvtsHeaderWordPayloadLocation(word.Value, entries) is { } location
                    ? $"0x{word.Offset:X2}|{location}"
                    : null)
                .OfType<string>()),
            DdsCount = textures.Count,
            DirectoryReservedEntriesWithNonZero = entries.Count(static entry => entry.ReservedNonZeroByteCount > 0),
            DirectoryReservedNonZeroBytes = entries.Sum(static entry => entry.ReservedNonZeroByteCount),
            YabxPresent = metadata is not null,
            YabxHeaderHashCandidate = metadata?.HeaderHashCandidate is null ? null : $"0x{metadata.HeaderHashCandidate.Value:X}",
            YabxDeclaredPayloadLengthMatchesEntryLength = metadata is null ? null : metadata.DeclaredPayloadLength == metadata.Length - 16,
            YabxReferenceBase = metadata?.ObjectReferenceBase is null ? null : $"0x{metadata.ObjectReferenceBase.Value:X}",
            YabxObjectCount = metadata?.Objects.Count,
            YabxExpectedObjectCountFromDds = expectedObjectCountFromDds,
            YabxObjectCountMatchesDdsSkeleton = metadata is null ? null : metadata.Objects.Count == expectedObjectCountFromDds,
            YabxObjectTypeOrderMatchesDdsSkeleton = metadata is null ? null : MatchesDdsObjectSkeleton(metadata.Objects, textures.Count),
            YabxUnparsedBytes = metadata?.Objects.Sum(static obj => obj.UnparsedByteCount),
            YabxObjectTypeCounts = metadata is null
                ? new SortedDictionary<string, int>(StringComparer.Ordinal)
                : CountBy(metadata.Objects.Select(static obj => obj.TypeName ?? $"type{obj.TypeIndex}")),
            YabxDescriptorRawCounts = CountBy(descriptors.Select(static descriptor => descriptor.RawDescriptorHex)),
            YabxDescriptorFlagsCounts = CountByHex(descriptors.Select(static descriptor => (int?)descriptor.Flags)),
            YabxDescriptorValueKindCounts = CountByHex(descriptors.Select(static descriptor => (int?)descriptor.ValueKind)),
            YabxDescriptorReservedCounts = CountByHex(descriptors.Select(static descriptor => (int?)descriptor.Reserved)),
            YabxDescriptorUsageCounts = CountBy(descriptorUsageRows.Select(FormatDescriptorUsageSurveyKey)),
            YabxDescriptorRawUsageCounts = CountBy(descriptorUsageRows.Select(FormatDescriptorRawUsageSurveyKey)),
            YabxDescriptorRawObjectKindCounts = CountBy(descriptorUsageRows.Select(FormatDescriptorRawObjectKindSurveyKey)),
            YabxResourceRecordCount = metadata?.Resources.Count ?? 0,
            YabxResourceRecordCountMatchesDds = metadata is null ? null : metadata.Resources.Count == textures.Count,
            YabxResourceTextureImageReferenceMatches = metadata?.Resources.Count(ResourceTextureImageReferenceMatches) ?? 0,
            YabxResourceTextureImageReferenceMismatches = metadata?.Resources.Count(ResourceTextureImageReferenceMismatches) ?? 0,
            YabxResourceTextureImageReferenceMissing = metadata?.Resources.Count(ResourceTextureImageReferenceMissing) ?? 0,
            YabxResourceDataSizeMatchesDirectory = metadata?.Resources.Count(ResourceDataSizeMatchesDirectory) ?? 0,
            YabxResourceDataSizeMismatchesDirectory = metadata?.Resources.Count(ResourceDataSizeMismatchesDirectory) ?? 0,
            YabxResourceDataSizeMissing = metadata?.Resources.Count(ResourceDataSizeMissing) ?? 0,
            YabxResourceDimensionsMatchDds = metadata?.Resources.Count(ResourceDimensionsMatchDds) ?? 0,
            YabxResourceDimensionsMismatchDds = metadata?.Resources.Count(ResourceDimensionsMismatchDds) ?? 0,
            YabxResourceDimensionsMissing = metadata?.Resources.Count(ResourceDimensionsMissing) ?? 0,
            TextureFormatCounts = CountBy(textures.Select(static texture => texture.Format)),
        };
    }
    catch (Exception ex)
    {
        return new SvoSurveyRow
        {
            Path = path,
            RelativePath = GetRelativeSurveyPath(root, path),
            Size = new FileInfo(path).Length,
            Error = ex.Message,
        };
    }
}

static SceneSurveyAggregate BuildSceneSurveyAggregate(IReadOnlyList<SceneSurveyRow> rows)
{
    var parsed = rows.Where(static row => row.Error is null).ToArray();
    return new SceneSurveyAggregate
    {
        Total = rows.Count,
        Parsed = parsed.Length,
        Failed = rows.Count - parsed.Length,
        RootParamRawCounts = CountBy(parsed.Select(static row => row.RootParamRaw ?? "?")),
        VtbfTagCounts = MergeCounts(parsed.Select(static row => row.VtbfTagCounts)),
        VtbfTagParamRawCounts = MergeCounts(parsed.Select(static row => row.VtbfTagParamRawCounts)),
        VtbfTagParamLowHighCounts = MergeCounts(parsed.Select(static row => row.VtbfTagParamLowHighCounts)),
        VtbfTagPropertyCountCounts = MergeCounts(parsed.Select(static row => row.VtbfTagPropertyCountCounts)),
        VtbfTagParamHighPropertyCountCounts = MergeCounts(parsed.Select(static row => row.VtbfTagParamHighPropertyCountCounts)),
        VtbfTagTrailingByteCounts = MergeCounts(parsed.Select(static row => row.VtbfTagTrailingByteCounts)),
        VtbfKeyParamHighModulo5Counts = MergeCounts(parsed.Select(static row => row.VtbfKeyParamHighModulo5Counts)),
        VtbfFieldDirectoryCounts = MergeCounts(parsed.Select(static row => row.VtbfFieldDirectoryCounts)),
        VtbfFieldDirectoryBlockCounts = MergeCounts(parsed.Select(static row => row.VtbfFieldDirectoryBlockCounts)),
        VtbfFieldCountValueCounts = MergeCounts(parsed.Select(static row => row.VtbfFieldCountValueCounts)),
        VtbfFieldStrideValueCounts = MergeCounts(parsed.Select(static row => row.VtbfFieldStrideValueCounts)),
        SharedPackedStateOwnerCounts = MergeCounts(parsed.Select(static row => row.SharedPackedStateOwnerCounts)),
        SharedPackedStateOwnerRawCounts = MergeCounts(parsed.Select(static row => row.SharedPackedStateOwnerRawCounts)),
        SharedPackedStateOwnerBitCounts = MergeCounts(parsed.Select(static row => row.SharedPackedStateOwnerBitCounts)),
        SharedPackedStateOwnerLowNibbleCounts = MergeCounts(parsed.Select(static row => row.SharedPackedStateOwnerLowNibbleCounts)),
        SharedPackedStateOwnerMaskF0Counts = MergeCounts(parsed.Select(static row => row.SharedPackedStateOwnerMaskF0Counts)),
        SharedPackedStateOwnerMaskF00Counts = MergeCounts(parsed.Select(static row => row.SharedPackedStateOwnerMaskF00Counts)),
        SharedPackedStateOwnerUpperMaskCounts = MergeCounts(parsed.Select(static row => row.SharedPackedStateOwnerUpperMaskCounts)),
        CatrField03Counts = MergeCounts(parsed.Select(static row => row.CatrField03Counts)),
        CatrField0DCounts = MergeCounts(parsed.Select(static row => row.CatrField0DCounts)),
        CatrField0ECounts = MergeCounts(parsed.Select(static row => row.CatrField0ECounts)),
        CatrField0FTypeCounts = MergeCounts(parsed.Select(static row => row.CatrField0FTypeCounts)),
        CatrField0FPreviewCounts = MergeCounts(parsed.Select(static row => row.CatrField0FPreviewCounts)),
        CatrFieldSequenceCounts = MergeCounts(parsed.Select(static row => row.CatrFieldSequenceCounts)),
        CatrFieldSetCounts = MergeCounts(parsed.Select(static row => row.CatrFieldSetCounts)),
        ProjectField00Counts = MergeCounts(parsed.Select(static row => row.ProjectField00Counts)),
        ProjectField01Counts = MergeCounts(parsed.Select(static row => row.ProjectField01Counts)),
        ProjectField05Counts = MergeCounts(parsed.Select(static row => row.ProjectField05Counts)),
        ProjectField55Counts = MergeCounts(parsed.Select(static row => row.ProjectField55Counts)),
        ProjectField56Counts = MergeCounts(parsed.Select(static row => row.ProjectField56Counts)),
        ProjectField56TrackLastRelationCounts = MergeCounts(parsed.Select(static row => row.ProjectField56TrackLastRelationCounts)),
        ProjectField56KeyMaxRelationCounts = MergeCounts(parsed.Select(static row => row.ProjectField56KeyMaxRelationCounts)),
        ProjectField56DeltaToTrackLastCounts = MergeCounts(parsed.Select(static row => row.ProjectField56DeltaToTrackLastCounts)),
        ProjectField56DeltaToKeyMaxCounts = MergeCounts(parsed.Select(static row => row.ProjectField56DeltaToKeyMaxCounts)),
        ProjectFieldSequenceCounts = MergeCounts(parsed.Select(static row => row.ProjectFieldSequenceCounts)),
        ProjectFieldSetCounts = MergeCounts(parsed.Select(static row => row.ProjectFieldSetCounts)),
        ScnNameCounts = MergeCounts(parsed.Select(static row => row.ScnNameCounts)),
        ScnField04RawHexCounts = MergeCounts(parsed.Select(static row => row.ScnField04RawHexCounts)),
        ScnField10Counts = MergeCounts(parsed.Select(static row => row.ScnField10Counts)),
        ScnField11Counts = MergeCounts(parsed.Select(static row => row.ScnField11Counts)),
        ScnField40Counts = MergeCounts(parsed.Select(static row => row.ScnField40Counts)),
        ScnField41Counts = MergeCounts(parsed.Select(static row => row.ScnField41Counts)),
        ScnField10Field11Counts = MergeCounts(parsed.Select(static row => row.ScnField10Field11Counts)),
        ScnField40Field41Counts = MergeCounts(parsed.Select(static row => row.ScnField40Field41Counts)),
        ScnParamLowLayerCountDeltaCounts = MergeCounts(parsed.Select(static row => row.ScnParamLowLayerCountDeltaCounts)),
        ScnParamLowField10DeltaCounts = MergeCounts(parsed.Select(static row => row.ScnParamLowField10DeltaCounts)),
        ScnField10LayerCountDeltaCounts = MergeCounts(parsed.Select(static row => row.ScnField10LayerCountDeltaCounts)),
        ScnFieldSequenceCounts = MergeCounts(parsed.Select(static row => row.ScnFieldSequenceCounts)),
        ScnFieldSetCounts = MergeCounts(parsed.Select(static row => row.ScnFieldSetCounts)),
        LayerNameCounts = MergeCounts(parsed.Select(static row => row.LayerNameCounts)),
        LayerField20Counts = MergeCounts(parsed.Select(static row => row.LayerField20Counts)),
        LayerField20BitCounts = MergeCounts(parsed.Select(static row => row.LayerField20BitCounts)),
        LayerField21Counts = MergeCounts(parsed.Select(static row => row.LayerField21Counts)),
        LayerField21BitCounts = MergeCounts(parsed.Select(static row => row.LayerField21BitCounts)),
        LayerField22Counts = MergeCounts(parsed.Select(static row => row.LayerField22Counts)),
        LayerField22BitCounts = MergeCounts(parsed.Select(static row => row.LayerField22BitCounts)),
        LayerField21SceneNodeCountDeltaCounts = MergeCounts(parsed.Select(static row => row.LayerField21SceneNodeCountDeltaCounts)),
        LayerParamLowField22DeltaCounts = MergeCounts(parsed.Select(static row => row.LayerParamLowField22DeltaCounts)),
        LayerFieldSequenceCounts = MergeCounts(parsed.Select(static row => row.LayerFieldSequenceCounts)),
        LayerFieldSetCounts = MergeCounts(parsed.Select(static row => row.LayerFieldSetCounts)),
        CameraNameCounts = MergeCounts(parsed.Select(static row => row.CameraNameCounts)),
        CameraField12VectorCounts = MergeCounts(parsed.Select(static row => row.CameraField12VectorCounts)),
        CameraField13VectorCounts = MergeCounts(parsed.Select(static row => row.CameraField13VectorCounts)),
        CameraField14Counts = MergeCounts(parsed.Select(static row => row.CameraField14Counts)),
        CameraField14BitCounts = MergeCounts(parsed.Select(static row => row.CameraField14BitCounts)),
        CameraField15Counts = MergeCounts(parsed.Select(static row => row.CameraField15Counts)),
        CameraField16Counts = MergeCounts(parsed.Select(static row => row.CameraField16Counts)),
        CameraFieldSequenceCounts = MergeCounts(parsed.Select(static row => row.CameraFieldSequenceCounts)),
        CameraFieldSetCounts = MergeCounts(parsed.Select(static row => row.CameraFieldSetCounts)),
        AnimationFieldSequenceCounts = MergeCounts(parsed.Select(static row => row.AnimationFieldSequenceCounts)),
        AnimationFieldSetCounts = MergeCounts(parsed.Select(static row => row.AnimationFieldSetCounts)),
        AnimationParamLowMotionDeltaCounts = MergeCounts(parsed.Select(static row => row.AnimationParamLowMotionDeltaCounts)),
        AnimationField50MotionDeltaCounts = MergeCounts(parsed.Select(static row => row.AnimationField50MotionDeltaCounts)),
        AnimationField50MaxMotionTrackDeltaCounts = MergeCounts(parsed.Select(static row => row.AnimationField50MaxMotionTrackDeltaCounts)),
        AnimationField50MotionOrMaxTrackRelationCounts = MergeCounts(parsed.Select(static row => row.AnimationField50MotionOrMaxTrackRelationCounts)),
        AnimationParamLowField50DeltaCounts = MergeCounts(parsed.Select(static row => row.AnimationParamLowField50DeltaCounts)),
        AnimationField5FCounts = MergeCounts(parsed.Select(static row => row.AnimationField5FCounts)),
        AnimationField5FMotionPresenceCounts = MergeCounts(parsed.Select(static row => row.AnimationField5FMotionPresenceCounts)),
        AnimationField5FAnimationNameCounts = MergeCounts(parsed.Select(static row => row.AnimationField5FAnimationNameCounts)),
        AnimationField5FParamLowMotionDeltaCounts = MergeCounts(parsed.Select(static row => row.AnimationField5FParamLowMotionDeltaCounts)),
        AnimationField5FField50MotionDeltaCounts = MergeCounts(parsed.Select(static row => row.AnimationField5FField50MotionDeltaCounts)),
        AnimationField5FField50RelationCounts = MergeCounts(parsed.Select(static row => row.AnimationField5FField50RelationCounts)),
        AnimationField5FEndFrameRelationCounts = MergeCounts(parsed.Select(static row => row.AnimationField5FEndFrameRelationCounts)),
        AnimationEndFrameRelationCounts = MergeCounts(parsed.Select(static row => row.AnimationEndFrameRelationCounts)),
        AnimationEndFrameDeltaToTrackLastCounts = MergeCounts(parsed.Select(static row => row.AnimationEndFrameDeltaToTrackLastCounts)),
        AnimationEndFrameDeltaToKeyMaxCounts = MergeCounts(parsed.Select(static row => row.AnimationEndFrameDeltaToKeyMaxCounts)),
        MotionFieldSequenceCounts = MergeCounts(parsed.Select(static row => row.MotionFieldSequenceCounts)),
        MotionFieldSetCounts = MergeCounts(parsed.Select(static row => row.MotionFieldSetCounts)),
        MotionParamLowTrackDeltaCounts = MergeCounts(parsed.Select(static row => row.MotionParamLowTrackDeltaCounts)),
        MotionField52TrackDeltaCounts = MergeCounts(parsed.Select(static row => row.MotionField52TrackDeltaCounts)),
        MotionParamLowField52DeltaCounts = MergeCounts(parsed.Select(static row => row.MotionParamLowField52DeltaCounts)),
        MotionTargetIndexRangeCounts = MergeCounts(parsed.Select(static row => row.MotionTargetIndexRangeCounts)),
        DataParamLowMatchesImageCasts = parsed.Count(static row => row.DataParamLowMatchesImageCasts == true),
        DataParamLowMatchesFollowingImageCasts = parsed.Count(static row => row.DataParamLowMatchesFollowingImageCasts == true),
        DataParamLowMatchesFollowingCimgCrfd = parsed.Count(static row => row.DataParamLowMatchesFollowingCimgCrfd == true),
        DataParamLowMatchesFollowingCimgCnumCrfd = parsed.Count(static row => row.DataParamLowMatchesFollowingCimgCnumCrfd == true),
        DataParamLowMatchesFollowingCimgCnumCrfdCsli = parsed.Count(static row => row.DataParamLowMatchesFollowingCimgCnumCrfdCsli == true),
        DataBlocksWithFields = parsed.Count(static row => row.DataFields > 0),
        DataBlocksWithTrailingBytes = parsed.Count(static row => row.DataTrailingBytes > 0),
        NcatMatchesNodes = parsed.Count(static row => row.NcatMatchesNodes == true),
        NcatNonZeroRecords = parsed.Sum(static row => row.NcatNonZeroCount),
        NcatDetailRecords = parsed.Sum(static row => row.NcatDetailRecordCount),
        NcatRecordsWithCategory = parsed.Sum(static row => row.NcatRecordsWithCategory),
        NcatRecordsWithoutCategory = parsed.Sum(static row => row.NcatRecordsWithoutCategory),
        NcatKindCounts = MergeCounts(parsed.Select(static row => row.NcatKindCounts)),
        NcatTypeByteCounts = MergeCounts(parsed.Select(static row => row.NcatTypeByteCounts)),
        NcatCategoryCounts = MergeCounts(parsed.Select(static row => row.NcatCategoryCounts)),
        NcatKindTypeByteCounts = MergeCounts(parsed.Select(static row => row.NcatKindTypeByteCounts)),
        NcatKindCategoryCounts = MergeCounts(parsed.Select(static row => row.NcatKindCategoryCounts)),
        NcatTypeByteCategoryCounts = MergeCounts(parsed.Select(static row => row.NcatTypeByteCategoryCounts)),
        NcatKindParameterPresenceCounts = MergeCounts(parsed.Select(static row => row.NcatKindParameterPresenceCounts)),
        NcatParameterStringCounts = MergeCounts(parsed.Select(static row => row.NcatParameterStringCounts)),
        NcatParameterFieldTypeCounts = MergeCounts(parsed.Select(static row => row.NcatParameterFieldTypeCounts)),
        NcatKindParameterFieldTypeCounts = MergeCounts(parsed.Select(static row => row.NcatKindParameterFieldTypeCounts)),
        NcatCategoryParameterFieldTypeCounts = MergeCounts(parsed.Select(static row => row.NcatCategoryParameterFieldTypeCounts)),
        NcatParameterFieldTypePreviewCounts = MergeCounts(parsed.Select(static row => row.NcatParameterFieldTypePreviewCounts)),
        NcatKindNodeFlagCounts = MergeCounts(parsed.Select(static row => row.NcatKindNodeFlagCounts)),
        NcatKindNodeFlagBitCounts = MergeCounts(parsed.Select(static row => row.NcatKindNodeFlagBitCounts)),
        NcatKindNodeGroupCounts = MergeCounts(parsed.Select(static row => row.NcatKindNodeGroupCounts)),
        NcatKindDisplayCounts = MergeCounts(parsed.Select(static row => row.NcatKindDisplayCounts)),
        NcatKindCimgTargetCounts = MergeCounts(parsed.Select(static row => row.NcatKindCimgTargetCounts)),
        NcatKindAnimatedNodeCounts = MergeCounts(parsed.Select(static row => row.NcatKindAnimatedNodeCounts)),
        ScenesWithWarnings = parsed.Count(static row => row.Warnings.Count > 0),
        WarningKindCounts = CountBy(parsed.SelectMany(static row => row.Warnings).Select(ClassifySurveyWarning)),
        Cimg44Matches = parsed.Sum(static row => row.Cimg44Matches),
        Cimg44Mismatches = parsed.Sum(static row => row.Cimg44Mismatches),
        Cimg44CountTupleCounts = MergeCounts(parsed.Select(static row => row.Cimg44CountTupleCounts)),
        Cimg44PrimaryCountCounts = MergeCounts(parsed.Select(static row => row.Cimg44PrimaryCountCounts)),
        Cimg44SecondaryCountCounts = MergeCounts(parsed.Select(static row => row.Cimg44SecondaryCountCounts)),
        Cimg45ActiveGroups = parsed.Sum(static row => row.Cimg45ActiveGroups),
        Cimg45InRangeGroups = parsed.Sum(static row => row.Cimg45InRangeGroups),
        Cimg45OutOfRangeGroups = parsed.Sum(static row => row.Cimg45OutOfRangeGroups),
        Cimg45EmptyGroupNonZero = parsed.Sum(static row => row.Cimg45EmptyGroupNonZero),
        Cimg45NonZeroIndices = parsed.Sum(static row => row.Cimg45NonZeroIndices),
        Cimg45NonZeroImageCasts = parsed.Sum(static row => row.Cimg45NonZeroImageCasts),
        Cimg45GroupIndexCounts = MergeCounts(parsed.Select(static row => row.Cimg45GroupIndexCounts)),
        Cimg45GroupCountIndexCounts = MergeCounts(parsed.Select(static row => row.Cimg45GroupCountIndexCounts)),
        Cimg45NonZeroGroupCounts = MergeCounts(parsed.Select(static row => row.Cimg45NonZeroGroupCounts)),
        CnumCount = parsed.Sum(static row => row.CnumCount),
        CnumCropReferenceCount = parsed.Sum(static row => row.CnumCropReferenceCount),
        CnumField44Matches = parsed.Sum(static row => row.CnumField44Matches),
        CnumField44Mismatches = parsed.Sum(static row => row.CnumField44Mismatches),
        CnumField44Missing = parsed.Sum(static row => row.CnumField44Missing),
        CnumField51InRange = parsed.Sum(static row => row.CnumField51InRange),
        CnumField51OutOfRange = parsed.Sum(static row => row.CnumField51OutOfRange),
        CnumField51Missing = parsed.Sum(static row => row.CnumField51Missing),
        CnumField44Counts = MergeCounts(parsed.Select(static row => row.CnumField44Counts)),
        CnumZeroMarkerFieldCounts = MergeCounts(parsed.Select(static row => row.CnumZeroMarkerFieldCounts)),
        CnumFieldA1Counts = MergeCounts(parsed.Select(static row => row.CnumFieldA1Counts)),
        CnumField48Counts = MergeCounts(parsed.Select(static row => row.CnumField48Counts)),
        CnumFieldA0Counts = MergeCounts(parsed.Select(static row => row.CnumFieldA0Counts)),
        CnumFieldA1RawLengthCounts = MergeCounts(parsed.Select(static row => row.CnumFieldA1RawLengthCounts)),
        CnumFieldA1ContentLengthCounts = MergeCounts(parsed.Select(static row => row.CnumFieldA1ContentLengthCounts)),
        CnumFieldA1Utf8StatusCounts = MergeCounts(parsed.Select(static row => row.CnumFieldA1Utf8StatusCounts)),
        CnumFieldA1ShiftJisByteShapeCounts = MergeCounts(parsed.Select(static row => row.CnumFieldA1ShiftJisByteShapeCounts)),
        CnumFieldA1RawPreviewCounts = MergeCounts(parsed.Select(static row => row.CnumFieldA1RawPreviewCounts)),
        CnumFieldA1Field44Counts = MergeCounts(parsed.Select(static row => row.CnumFieldA1Field44Counts)),
        CnumFieldA1CropReferenceCountCounts = MergeCounts(parsed.Select(static row => row.CnumFieldA1CropReferenceCountCounts)),
        CnumFieldA1ZeroMarkerFieldCounts = MergeCounts(parsed.Select(static row => row.CnumFieldA1ZeroMarkerFieldCounts)),
        CnumFieldA1NodeFlagCounts = MergeCounts(parsed.Select(static row => row.CnumFieldA1NodeFlagCounts)),
        CnumFieldA1NodeGroupCounts = MergeCounts(parsed.Select(static row => row.CnumFieldA1NodeGroupCounts)),
        CnumFieldA1DisplayCounts = MergeCounts(parsed.Select(static row => row.CnumFieldA1DisplayCounts)),
        CnumFieldA1CimgTargetCounts = MergeCounts(parsed.Select(static row => row.CnumFieldA1CimgTargetCounts)),
        CnumFieldA1AnimatedTargetCounts = MergeCounts(parsed.Select(static row => row.CnumFieldA1AnimatedTargetCounts)),
        CnumFieldSequenceCounts = MergeCounts(parsed.Select(static row => row.CnumFieldSequenceCounts)),
        CnumFieldSetCounts = MergeCounts(parsed.Select(static row => row.CnumFieldSetCounts)),
        CrfdCount = parsed.Sum(static row => row.CrfdCount),
        CrfdField51InRange = parsed.Sum(static row => row.CrfdField51InRange),
        CrfdField51OutOfRange = parsed.Sum(static row => row.CrfdField51OutOfRange),
        CrfdField51Missing = parsed.Sum(static row => row.CrfdField51Missing),
        CrfdField90Counts = MergeCounts(parsed.Select(static row => row.CrfdField90Counts)),
        CrfdField91Counts = MergeCounts(parsed.Select(static row => row.CrfdField91Counts)),
        CrfdField90Field91Counts = MergeCounts(parsed.Select(static row => row.CrfdField90Field91Counts)),
        CrfdField90Field91Field92Counts = MergeCounts(parsed.Select(static row => row.CrfdField90Field91Field92Counts)),
        CrfdStringFieldRelationCounts = MergeCounts(parsed.Select(static row => row.CrfdStringFieldRelationCounts)),
        CrfdStringFieldTargetTypeCounts = MergeCounts(parsed.Select(static row => row.CrfdStringFieldTargetTypeCounts)),
        CrfdField90Field91RelationCounts = MergeCounts(parsed.Select(static row => row.CrfdField90Field91RelationCounts)),
        CrfdField90Field91EqualityCounts = MergeCounts(parsed.Select(static row => row.CrfdField90Field91EqualityCounts)),
        CrfdField90Field91Field92RelationCounts = MergeCounts(parsed.Select(static row => row.CrfdField90Field91Field92RelationCounts)),
        CrfdField92Counts = MergeCounts(parsed.Select(static row => row.CrfdField92Counts)),
        CrfdField93Counts = MergeCounts(parsed.Select(static row => row.CrfdField93Counts)),
        CrfdField94Counts = MergeCounts(parsed.Select(static row => row.CrfdField94Counts)),
        CrfdField94NonZero = parsed.Sum(static row => row.CrfdField94NonZero),
        CrfdField95Counts = MergeCounts(parsed.Select(static row => row.CrfdField95Counts)),
        TextCount = parsed.Sum(static row => row.TextCount),
        TextField7APresent = parsed.Sum(static row => row.TextField7APresent),
        TextZeroMarkerFieldCounts = MergeCounts(parsed.Select(static row => row.TextZeroMarkerFieldCounts)),
        TextField41Counts = MergeCounts(parsed.Select(static row => row.TextField41Counts)),
        TextField78Counts = MergeCounts(parsed.Select(static row => row.TextField78Counts)),
        TextField79Counts = MergeCounts(parsed.Select(static row => row.TextField79Counts)),
        TextField7CCounts = MergeCounts(parsed.Select(static row => row.TextField7CCounts)),
        TextField7AStringCounts = MergeCounts(parsed.Select(static row => row.TextField7AStringCounts)),
        TextField7ARawLengthCounts = MergeCounts(parsed.Select(static row => row.TextField7ARawLengthCounts)),
        TextField7AContentLengthCounts = MergeCounts(parsed.Select(static row => row.TextField7AContentLengthCounts)),
        TextField7AUtf8StatusCounts = MergeCounts(parsed.Select(static row => row.TextField7AUtf8StatusCounts)),
        TextField7AShiftJisByteShapeCounts = MergeCounts(parsed.Select(static row => row.TextField7AShiftJisByteShapeCounts)),
        TextField7AShiftJisDecodeStatusCounts = MergeCounts(parsed.Select(static row => row.TextField7AShiftJisDecodeStatusCounts)),
        TextField7AShiftJisStringCounts = MergeCounts(parsed.Select(static row => row.TextField7AShiftJisStringCounts)),
        TextField7ARawPreviewCounts = MergeCounts(parsed.Select(static row => row.TextField7ARawPreviewCounts)),
        TextField7AField41Counts = MergeCounts(parsed.Select(static row => row.TextField7AField41Counts)),
        TextField7AField78Counts = MergeCounts(parsed.Select(static row => row.TextField7AField78Counts)),
        TextField7AField79Counts = MergeCounts(parsed.Select(static row => row.TextField7AField79Counts)),
        TextField7AField7CCounts = MergeCounts(parsed.Select(static row => row.TextField7AField7CCounts)),
        TextField33VectorCounts = MergeCounts(parsed.Select(static row => row.TextField33VectorCounts)),
        TextField33RawHexCounts = MergeCounts(parsed.Select(static row => row.TextField33RawHexCounts)),
        TextField7BPackedValuesCounts = MergeCounts(parsed.Select(static row => row.TextField7BPackedValuesCounts)),
        TextField7BRawHexCounts = MergeCounts(parsed.Select(static row => row.TextField7BRawHexCounts)),
        TextField78Field79Counts = MergeCounts(parsed.Select(static row => row.TextField78Field79Counts)),
        TextZeroMarkerField7ACounts = MergeCounts(parsed.Select(static row => row.TextZeroMarkerField7ACounts)),
        TextFieldSequenceCounts = MergeCounts(parsed.Select(static row => row.TextFieldSequenceCounts)),
        TextFieldSetCounts = MergeCounts(parsed.Select(static row => row.TextFieldSetCounts)),
        SliceCasts = parsed.Sum(static row => row.SliceCastCount),
        SliceRecords = parsed.Sum(static row => row.SliceRecordCount),
        SliceCropReferences = parsed.Sum(static row => row.SliceCropReferenceCount),
        SliceField44SlicRecordMatches = parsed.Sum(static row => row.SliceField44SlicRecordMatches),
        SliceField44SlicRecordMismatches = parsed.Sum(static row => row.SliceField44SlicRecordMismatches),
        SliceField44CropReferenceMatches = parsed.Sum(static row => row.SliceField44CropReferenceMatches),
        SliceField44CropReferenceMismatches = parsed.Sum(static row => row.SliceField44CropReferenceMismatches),
        SliceTargetIndexInRange = parsed.Sum(static row => row.SliceTargetIndexInRange),
        SliceTargetIndexOutOfRange = parsed.Sum(static row => row.SliceTargetIndexOutOfRange),
        SliceField83Counts = MergeCounts(parsed.Select(static row => row.SliceField83Counts)),
        SliceCastField40Counts = MergeCounts(parsed.Select(static row => row.SliceCastField40Counts)),
        SliceCastField41Counts = MergeCounts(parsed.Select(static row => row.SliceCastField41Counts)),
        SliceCastField42Counts = MergeCounts(parsed.Select(static row => row.SliceCastField42Counts)),
        SliceCastField43Counts = MergeCounts(parsed.Select(static row => row.SliceCastField43Counts)),
        SliceCastField80Counts = MergeCounts(parsed.Select(static row => row.SliceCastField80Counts)),
        SliceCastField81Counts = MergeCounts(parsed.Select(static row => row.SliceCastField81Counts)),
        SliceCastField82Counts = MergeCounts(parsed.Select(static row => row.SliceCastField82Counts)),
        SliceCastField84Counts = MergeCounts(parsed.Select(static row => row.SliceCastField84Counts)),
        SliceCastField85Counts = MergeCounts(parsed.Select(static row => row.SliceCastField85Counts)),
        SliceCastField86Counts = MergeCounts(parsed.Select(static row => row.SliceCastField86Counts)),
        SliceCastField87Counts = MergeCounts(parsed.Select(static row => row.SliceCastField87Counts)),
        SliceCastTargetNodeFlagCounts = MergeCounts(parsed.Select(static row => row.SliceCastTargetNodeFlagCounts)),
        SliceCastTargetNodeGroupCounts = MergeCounts(parsed.Select(static row => row.SliceCastTargetNodeGroupCounts)),
        SliceCastTargetDisplayCounts = MergeCounts(parsed.Select(static row => row.SliceCastTargetDisplayCounts)),
        SliceCastTargetCimgTargetCounts = MergeCounts(parsed.Select(static row => row.SliceCastTargetCimgTargetCounts)),
        SliceCastFieldSequenceCounts = MergeCounts(parsed.Select(static row => row.SliceCastFieldSequenceCounts)),
        SliceCastFieldSetCounts = MergeCounts(parsed.Select(static row => row.SliceCastFieldSetCounts)),
        SliceRecordField40Counts = MergeCounts(parsed.Select(static row => row.SliceRecordField40Counts)),
        SliceRecordField41Counts = MergeCounts(parsed.Select(static row => row.SliceRecordField41Counts)),
        SliceRecordField45Counts = MergeCounts(parsed.Select(static row => row.SliceRecordField45Counts)),
        SliceRecordField37ColorCounts = MergeCounts(parsed.Select(static row => row.SliceRecordField37ColorCounts)),
        SliceRecordField38ColorCounts = MergeCounts(parsed.Select(static row => row.SliceRecordField38ColorCounts)),
        SliceRecordField39ColorCounts = MergeCounts(parsed.Select(static row => row.SliceRecordField39ColorCounts)),
        SliceRecordField39ColorCountCounts = MergeCounts(parsed.Select(static row => row.SliceRecordField39ColorCountCounts)),
        SliceRecordField83Field40Counts = MergeCounts(parsed.Select(static row => row.SliceRecordField83Field40Counts)),
        SliceRecordField83Field41Counts = MergeCounts(parsed.Select(static row => row.SliceRecordField83Field41Counts)),
        SliceRecordField83Field45Counts = MergeCounts(parsed.Select(static row => row.SliceRecordField83Field45Counts)),
        SliceRecordFieldSequenceCounts = MergeCounts(parsed.Select(static row => row.SliceRecordFieldSequenceCounts)),
        SliceRecordFieldSetCounts = MergeCounts(parsed.Select(static row => row.SliceRecordFieldSetCounts)),
        SliceRecordShapeCounts = MergeCounts(parsed.Select(static row => row.SliceRecordShapeCounts)),
        TrackKeyCountMismatches = parsed.Sum(static row => row.TrackKeyCountMismatches),
        KeyTangentPresent = parsed.Sum(static row => row.KeyTangentPresent),
        KeyTangentNonZero = parsed.Sum(static row => row.KeyTangentNonZero),
        KeyTangentMismatch = parsed.Sum(static row => row.KeyTangentMismatch),
        KeyTangentNonZeroScenes = parsed.Count(static row => row.KeyTangentNonZero > 0),
        KeyTangentMismatchScenes = parsed.Count(static row => row.KeyTangentMismatch > 0),
        UnknownTypeCodeCounts = MergeCounts(parsed.Select(static row => row.UnknownTypeCodeCounts)),
        NodeFlagCounts = MergeCounts(parsed.Select(static row => row.NodeFlagCounts)),
        NodeFlagBitCounts = MergeCounts(parsed.Select(static row => row.NodeFlagBitCounts)),
        NodeFlagBitDisplayFalseNodeCounts = MergeCounts(parsed.Select(static row => row.NodeFlagBitDisplayFalseNodeCounts)),
        NodeFlagBitCimgTargetNodeCounts = MergeCounts(parsed.Select(static row => row.NodeFlagBitCimgTargetNodeCounts)),
        NodeFlagBitAnimatedNodeCounts = MergeCounts(parsed.Select(static row => row.NodeFlagBitAnimatedNodeCounts)),
        NodeFlagBitDataNodeCounts = MergeCounts(parsed.Select(static row => row.NodeFlagBitDataNodeCounts)),
        NodeFlagBitCategoryRecordNodeCounts = MergeCounts(parsed.Select(static row => row.NodeFlagBitCategoryRecordNodeCounts)),
        NodeFlagBitCategoryNonZeroNodeCounts = MergeCounts(parsed.Select(static row => row.NodeFlagBitCategoryNonZeroNodeCounts)),
        NodeFlagBitExactFlagCounts = MergeCounts(parsed.Select(static row => row.NodeFlagBitExactFlagCounts)),
        NodeFlagBitGroupCounts = MergeCounts(parsed.Select(static row => row.NodeFlagBitGroupCounts)),
        NodeFlagBitImageCastFlagBitCounts = MergeCounts(parsed.Select(static row => row.NodeFlagBitImageCastFlagBitCounts)),
        NodeFlagBitTrackTypeCounts = MergeCounts(parsed.Select(static row => row.NodeFlagBitTrackTypeCounts)),
        NodeFlagBitPairCounts = MergeCounts(parsed.Select(static row => row.NodeFlagBitPairCounts)),
        CimgFlagCounts = MergeCounts(parsed.Select(static row => row.CimgFlagCounts)),
        CimgFlagBitCounts = MergeCounts(parsed.Select(static row => row.CimgFlagBitCounts)),
        CimgFlagBitDisplayFalseCounts = MergeCounts(parsed.Select(static row => row.CimgFlagBitDisplayFalseCounts)),
        CimgFlagBitMultiReferenceCounts = MergeCounts(parsed.Select(static row => row.CimgFlagBitMultiReferenceCounts)),
        CimgFlagBitSecondaryReferenceCounts = MergeCounts(parsed.Select(static row => row.CimgFlagBitSecondaryReferenceCounts)),
        CimgFlagBitNonZeroReferenceIndexCounts = MergeCounts(parsed.Select(static row => row.CimgFlagBitNonZeroReferenceIndexCounts)),
        CimgFlagBitMissingNodeCounts = MergeCounts(parsed.Select(static row => row.CimgFlagBitMissingNodeCounts)),
        CimgFlagBitNodeFlagCounts = MergeCounts(parsed.Select(static row => row.CimgFlagBitNodeFlagCounts)),
        CimgFlagBitGroupCounts = MergeCounts(parsed.Select(static row => row.CimgFlagBitGroupCounts)),
        CimgFlagBitPairCounts = MergeCounts(parsed.Select(static row => row.CimgFlagBitPairCounts)),
        TextureAtlasCount = parsed.Sum(static row => row.TextureAtlasCount),
        TextureAtlasField62Counts = MergeCounts(parsed.Select(static row => row.TextureAtlasField62Counts)),
        TextureAtlasField62BitCounts = MergeCounts(parsed.Select(static row => row.TextureAtlasField62BitCounts)),
        TextureAtlasField62CropCountCounts = MergeCounts(parsed.Select(static row => row.TextureAtlasField62CropCountCounts)),
        TextureAtlasField62SizeCounts = MergeCounts(parsed.Select(static row => row.TextureAtlasField62SizeCounts)),
        CropKindCounts = MergeCounts(parsed.Select(static row => row.CropKindCounts)),
        CrefKindCounts = MergeCounts(parsed.Select(static row => row.CrefKindCounts)),
        CropRectCount = parsed.Sum(static row => row.CropRectCount),
        CropAtlasDeclaredCountMatches = parsed.Sum(static row => row.CropAtlasDeclaredCountMatches),
        CropAtlasDeclaredCountMismatches = parsed.Sum(static row => row.CropAtlasDeclaredCountMismatches),
        CropRectInAtlasBounds = parsed.Sum(static row => row.CropRectInAtlasBounds),
        CropRectOutOfAtlasBounds = parsed.Sum(static row => row.CropRectOutOfAtlasBounds),
        CropRectNonPositiveSize = parsed.Sum(static row => row.CropRectNonPositiveSize),
        CropReferenceCount = parsed.Sum(static row => row.CropReferenceCount),
        CropReferenceKindCounts = MergeCounts(parsed.Select(static row => row.CropReferenceKindCounts)),
        CropReferenceOwnerCounts = MergeCounts(parsed.Select(static row => row.CropReferenceOwnerCounts)),
        CropReferenceOwnerKindCounts = MergeCounts(parsed.Select(static row => row.CropReferenceOwnerKindCounts)),
        CropReferenceTextureListIndexCounts = MergeCounts(parsed.Select(static row => row.CropReferenceTextureListIndexCounts)),
        CropReferenceTextureIndexRangeCounts = MergeCounts(parsed.Select(static row => row.CropReferenceTextureIndexRangeCounts)),
        CropReferenceCropIndexRangeCounts = MergeCounts(parsed.Select(static row => row.CropReferenceCropIndexRangeCounts)),
        CropRectOutOfAtlasBoundsReasonCounts = MergeCounts(parsed.Select(static row => row.CropRectOutOfAtlasBoundsReasonCounts)),
        CropReferenceOutOfRangeOwnerCounts = MergeCounts(parsed.Select(static row => row.CropReferenceOutOfRangeOwnerCounts)),
        TrackFlagCounts = MergeCounts(parsed.Select(static row => row.TrackFlagCounts)),
        TrackFlagBaseCounts = MergeCounts(parsed.Select(static row => row.TrackFlagBaseCounts)),
        TrackFlagExtraCounts = MergeCounts(parsed.Select(static row => row.TrackFlagExtraCounts)),
        TrackFlagExtraSceneCounts = MergeCounts(parsed.Select(static row => row.TrackFlagExtraSceneCounts)),
        TrackFlagExtraBaseCounts = MergeCounts(parsed.Select(static row => row.TrackFlagExtraBaseCounts)),
        TrackFlagExtraAnimationCounts = MergeCounts(parsed.Select(static row => row.TrackFlagExtraAnimationCounts)),
        TrackFlagExtraTrackTypeCounts = MergeCounts(parsed.Select(static row => row.TrackFlagExtraTrackTypeCounts)),
        TrackFlagExtraKeyValueTypeCounts = MergeCounts(parsed.Select(static row => row.TrackFlagExtraKeyValueTypeCounts)),
        TrackFlagExtraNodeFlagCounts = MergeCounts(parsed.Select(static row => row.TrackFlagExtraNodeFlagCounts)),
        TrackFlagExtraNodeFlagBitCounts = MergeCounts(parsed.Select(static row => row.TrackFlagExtraNodeFlagBitCounts)),
        TrackFlagExtraGroupCounts = MergeCounts(parsed.Select(static row => row.TrackFlagExtraGroupCounts)),
        TrackFlagExtraCimgTargetCounts = MergeCounts(parsed.Select(static row => row.TrackFlagExtraCimgTargetCounts)),
        TrackFlagExtraInitialDisplayCounts = MergeCounts(parsed.Select(static row => row.TrackFlagExtraInitialDisplayCounts)),
        TrackFlagExtraCimgFlagCounts = MergeCounts(parsed.Select(static row => row.TrackFlagExtraCimgFlagCounts)),
        TrackFlagExtraCimgFlagBitCounts = MergeCounts(parsed.Select(static row => row.TrackFlagExtraCimgFlagBitCounts)),
        TrackFlagExtraCimgReferenceCountCounts = MergeCounts(parsed.Select(static row => row.TrackFlagExtraCimgReferenceCountCounts)),
        TrackTypeCounts = MergeCounts(parsed.Select(static row => row.TrackTypeCounts)),
        KeyValueTypeCounts = MergeCounts(parsed.Select(static row => row.KeyValueTypeCounts)),
        KeyInterpolationCounts = MergeCounts(parsed.Select(static row => row.KeyInterpolationCounts)),
        KeyInterpolationTrackTypeCounts = MergeCounts(parsed.Select(static row => row.KeyInterpolationTrackTypeCounts)),
        KeyInterpolationKeyValueTypeCounts = MergeCounts(parsed.Select(static row => row.KeyInterpolationKeyValueTypeCounts)),
        KeyTangentPresentInterpolationCounts = MergeCounts(parsed.Select(static row => row.KeyTangentPresentInterpolationCounts)),
        KeyTangentPresentTrackTypeCounts = MergeCounts(parsed.Select(static row => row.KeyTangentPresentTrackTypeCounts)),
        KeyTangentNonZeroInterpolationCounts = MergeCounts(parsed.Select(static row => row.KeyTangentNonZeroInterpolationCounts)),
        KeyTangentNonZeroTrackTypeCounts = MergeCounts(parsed.Select(static row => row.KeyTangentNonZeroTrackTypeCounts)),
        KeyTangentMismatchInterpolationCounts = MergeCounts(parsed.Select(static row => row.KeyTangentMismatchInterpolationCounts)),
        KeyTangentMismatchTrackTypeCounts = MergeCounts(parsed.Select(static row => row.KeyTangentMismatchTrackTypeCounts)),
        KeyTangentMismatchAnimationCounts = MergeCounts(parsed.Select(static row => row.KeyTangentMismatchAnimationCounts)),
        KeyTangentMismatchNodeFlagCounts = MergeCounts(parsed.Select(static row => row.KeyTangentMismatchNodeFlagCounts)),
        KeyTangentMismatchGroupCounts = MergeCounts(parsed.Select(static row => row.KeyTangentMismatchGroupCounts)),
        KeyTangentMismatchTrackExtraCounts = MergeCounts(parsed.Select(static row => row.KeyTangentMismatchTrackExtraCounts)),
        KeyTangentMismatchTangentPairCounts = MergeCounts(parsed.Select(static row => row.KeyTangentMismatchTangentPairCounts)),
        KeyTangentNonZeroFramePositionCounts = MergeCounts(parsed.Select(static row => row.KeyTangentNonZeroFramePositionCounts)),
        KeyTangentMismatchFramePositionCounts = MergeCounts(parsed.Select(static row => row.KeyTangentMismatchFramePositionCounts)),
        KeyTangentDeltaSignCounts = MergeCounts(parsed.Select(static row => row.KeyTangentDeltaSignCounts)),
        TrackKeyStorageMatrixCounts = MergeCounts(parsed.Select(static row => row.TrackKeyStorageMatrixCounts)),
        TrackFieldSequenceCounts = MergeCounts(parsed.Select(static row => row.TrackFieldSequenceCounts)),
        KeyFieldSequenceCounts = MergeCounts(parsed.Select(static row => row.KeyFieldSequenceCounts)),
        TrackFrameRangeRelationCounts = MergeCounts(parsed.Select(static row => row.TrackFrameRangeRelationCounts)),
        TrackKeyFrameOrderCounts = MergeCounts(parsed.Select(static row => row.TrackKeyFrameOrderCounts)),
        TrackKeyFrameDuplicateCounts = MergeCounts(parsed.Select(static row => row.TrackKeyFrameDuplicateCounts)),
        TrackFirstFrameDeltaCounts = MergeCounts(parsed.Select(static row => row.TrackFirstFrameDeltaCounts)),
        TrackLastFrameDeltaCounts = MergeCounts(parsed.Select(static row => row.TrackLastFrameDeltaCounts)),
        TransformTrackCount = parsed.Sum(static row => row.TransformTrackCount),
        TransformTrackKeyCount = parsed.Sum(static row => row.TransformTrackKeyCount),
        TransformTracksWithInitialChannel = parsed.Sum(static row => row.TransformTracksWithInitialChannel),
        TransformTracksMissingInitialChannel = parsed.Sum(static row => row.TransformTracksMissingInitialChannel),
        TransformTrackInitialValueMatches = parsed.Sum(static row => row.TransformTrackInitialValueMatches),
        TransformTrackInitialValueMismatches = parsed.Sum(static row => row.TransformTrackInitialValueMismatches),
        TransformTrackKeysMissingValue = parsed.Sum(static row => row.TransformTrackKeysMissingValue),
        TransformTrackTypeCounts = MergeCounts(parsed.Select(static row => row.TransformTrackTypeCounts)),
        TransformTrackKeyTypeCounts = MergeCounts(parsed.Select(static row => row.TransformTrackKeyTypeCounts)),
        TransformTrackStorageCounts = MergeCounts(parsed.Select(static row => row.TransformTrackStorageCounts)),
        TransformTrackKeyValueKindCounts = MergeCounts(parsed.Select(static row => row.TransformTrackKeyValueKindCounts)),
        TransformTrackInitialMatchTypeCounts = MergeCounts(parsed.Select(static row => row.TransformTrackInitialMatchTypeCounts)),
        TransformTrackValueRangeCounts = MergeCounts(parsed.Select(static row => row.TransformTrackValueRangeCounts)),
        TransformCandidateDefaultKeyCounts = MergeCounts(parsed.Select(static row => row.TransformCandidateDefaultKeyCounts)),
        PackedAngleTrackCount = parsed.Sum(static row => row.PackedAngleTrackCount),
        PackedAngleKeyCount = parsed.Sum(static row => row.PackedAngleKeyCount),
        PackedAngleTrackTypeCounts = MergeCounts(parsed.Select(static row => row.PackedAngleTrackTypeCounts)),
        PackedAngleKeyTrackTypeCounts = MergeCounts(parsed.Select(static row => row.PackedAngleKeyTrackTypeCounts)),
        PackedAngleRawCounts = MergeCounts(parsed.Select(static row => row.PackedAngleRawCounts)),
        PackedAngleDegreeCandidateCounts = MergeCounts(parsed.Select(static row => row.PackedAngleDegreeCandidateCounts)),
        ImageVariantTrackCount = parsed.Sum(static row => row.ImageVariantTrackCount),
        ImageVariantKeyCount = parsed.Sum(static row => row.ImageVariantKeyCount),
        ImageVariantTracksWithCimg = parsed.Sum(static row => row.ImageVariantTracksWithCimg),
        ImageVariantTracksMissingCimg = parsed.Sum(static row => row.ImageVariantTracksMissingCimg),
        ImageVariantTrackRangeMatches = parsed.Sum(static row => row.ImageVariantTrackRangeMatches),
        ImageVariantTrackRangeMismatches = parsed.Sum(static row => row.ImageVariantTrackRangeMismatches),
        ImageVariantKeysInRange = parsed.Sum(static row => row.ImageVariantKeysInRange),
        ImageVariantKeysOutOfRange = parsed.Sum(static row => row.ImageVariantKeysOutOfRange),
        ImageVariantKeysMissingCimg = parsed.Sum(static row => row.ImageVariantKeysMissingCimg),
        ImageVariantKeysNonInteger = parsed.Sum(static row => row.ImageVariantKeysNonInteger),
        ImageVariantKeysMissingValue = parsed.Sum(static row => row.ImageVariantKeysMissingValue),
        ImageVariantReferenceCountCounts = MergeCounts(parsed.Select(static row => row.ImageVariantReferenceCountCounts)),
        ImageVariantValueCounts = MergeCounts(parsed.Select(static row => row.ImageVariantValueCounts)),
        ImageVariantGroupTrackCounts = MergeCounts(parsed.Select(static row => row.ImageVariantGroupTrackCounts)),
        ImageVariantGroupKeyCounts = MergeCounts(parsed.Select(static row => row.ImageVariantGroupKeyCounts)),
        ImageVariantGroupTracksWithCimgCounts = MergeCounts(parsed.Select(static row => row.ImageVariantGroupTracksWithCimgCounts)),
        ImageVariantGroupTracksMissingCimgCounts = MergeCounts(parsed.Select(static row => row.ImageVariantGroupTracksMissingCimgCounts)),
        ImageVariantGroupTrackRangeMatchCounts = MergeCounts(parsed.Select(static row => row.ImageVariantGroupTrackRangeMatchCounts)),
        ImageVariantGroupTrackRangeMismatchCounts = MergeCounts(parsed.Select(static row => row.ImageVariantGroupTrackRangeMismatchCounts)),
        ImageVariantGroupKeysInRangeCounts = MergeCounts(parsed.Select(static row => row.ImageVariantGroupKeysInRangeCounts)),
        ImageVariantGroupKeysOutOfRangeCounts = MergeCounts(parsed.Select(static row => row.ImageVariantGroupKeysOutOfRangeCounts)),
        ImageVariantGroupKeysMissingCimgCounts = MergeCounts(parsed.Select(static row => row.ImageVariantGroupKeysMissingCimgCounts)),
        ImageVariantGroupKeysNonIntegerCounts = MergeCounts(parsed.Select(static row => row.ImageVariantGroupKeysNonIntegerCounts)),
        ImageVariantGroupKeysMissingValueCounts = MergeCounts(parsed.Select(static row => row.ImageVariantGroupKeysMissingValueCounts)),
        ImageVariantGroupReferenceCountCounts = MergeCounts(parsed.Select(static row => row.ImageVariantGroupReferenceCountCounts)),
        ImageVariantGroupValueCounts = MergeCounts(parsed.Select(static row => row.ImageVariantGroupValueCounts)),
        ImageVariantGroupCimg45FirstKeyRelationCounts = MergeCounts(parsed.Select(static row => row.ImageVariantGroupCimg45FirstKeyRelationCounts)),
        ImageVariantGroupCimg45FirstKeyDeltaCounts = MergeCounts(parsed.Select(static row => row.ImageVariantGroupCimg45FirstKeyDeltaCounts)),
        ImageVariantGroupCimg45FirstKeyPairCounts = MergeCounts(parsed.Select(static row => row.ImageVariantGroupCimg45FirstKeyPairCounts)),
        ColorTrackCount = parsed.Sum(static row => row.ColorTrackCount),
        ColorTrackKeyCount = parsed.Sum(static row => row.ColorTrackKeyCount),
        ColorTracksWithInitialChannel = parsed.Sum(static row => row.ColorTracksWithInitialChannel),
        ColorTracksMissingInitialChannel = parsed.Sum(static row => row.ColorTracksMissingInitialChannel),
        ColorTrackInitialValueMatches = parsed.Sum(static row => row.ColorTrackInitialValueMatches),
        ColorTrackInitialValueMismatches = parsed.Sum(static row => row.ColorTrackInitialValueMismatches),
        ColorTrackKeysInUnitRange = parsed.Sum(static row => row.ColorTrackKeysInUnitRange),
        ColorTrackKeysOutOfUnitRange = parsed.Sum(static row => row.ColorTrackKeysOutOfUnitRange),
        ColorTrackKeysMissingValue = parsed.Sum(static row => row.ColorTrackKeysMissingValue),
        ColorTrackTypeCounts = MergeCounts(parsed.Select(static row => row.ColorTrackTypeCounts)),
        ColorTrackKeyTypeCounts = MergeCounts(parsed.Select(static row => row.ColorTrackKeyTypeCounts)),
        ColorTrackInitialMatchTypeCounts = MergeCounts(parsed.Select(static row => row.ColorTrackInitialMatchTypeCounts)),
        AlphaOpacityTrackCount = parsed.Sum(static row => row.AlphaOpacityTrackCount),
        AlphaOpacityKeyCount = parsed.Sum(static row => row.AlphaOpacityKeyCount),
        AlphaOpacityTracksWithMaterialAlpha = parsed.Sum(static row => row.AlphaOpacityTracksWithMaterialAlpha),
        AlphaOpacityTracksMissingMaterialAlpha = parsed.Sum(static row => row.AlphaOpacityTracksMissingMaterialAlpha),
        AlphaOpacityInitialAlphaMatches = parsed.Sum(static row => row.AlphaOpacityInitialAlphaMatches),
        AlphaOpacityInitialAlphaMismatches = parsed.Sum(static row => row.AlphaOpacityInitialAlphaMismatches),
        AlphaOpacityCimgTargets = parsed.Sum(static row => row.AlphaOpacityCimgTargets),
        AlphaOpacityDisplayFalseTargets = parsed.Sum(static row => row.AlphaOpacityDisplayFalseTargets),
        AlphaOpacityKeysInUnitRange = parsed.Sum(static row => row.AlphaOpacityKeysInUnitRange),
        AlphaOpacityKeysOutOfUnitRange = parsed.Sum(static row => row.AlphaOpacityKeysOutOfUnitRange),
        AlphaOpacityKeysMissingValue = parsed.Sum(static row => row.AlphaOpacityKeysMissingValue),
    };
}

static CatrSurvey BuildCatrSurvey(IReadOnlyList<VtbfBlock> blocks)
{
    var field03Counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var field0DCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var field0ECounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var field0FTypeCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var field0FPreviewCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var fieldSequenceCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var fieldSetCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);

    foreach (var block in blocks.Where(static block => block.Tag == "CATR"))
    {
        AddCount(fieldSequenceCounts, FormatVtbfFieldSequence(block.Fields));
        AddCount(fieldSetCounts, FormatVtbfFieldSet(block.Fields));

        foreach (var field in block.Fields)
        {
            switch (field.Id)
            {
                case 0x03:
                    AddCount(field03Counts, FormatSurveyStringKey(field.StringValue ?? field.Preview));
                    break;
                case 0x0D:
                    AddCount(field0DCounts, FormatSurveyStringKey(field.Preview));
                    break;
                case 0x0E:
                    AddCount(field0ECounts, FormatSurveyStringKey(field.Preview));
                    break;
                case 0x0F:
                    var typeKey = $"{field.TypeHex} {field.TypeName}";
                    AddCount(field0FTypeCounts, typeKey);
                    AddCount(field0FPreviewCounts, $"{typeKey}|{FormatSurveyStringKey(field.Preview)}");
                    break;
            }
        }
    }

    return new CatrSurvey(
        field03Counts,
        field0DCounts,
        field0ECounts,
        field0FTypeCounts,
        field0FPreviewCounts,
        fieldSequenceCounts,
        fieldSetCounts);
}

static SharedPackedStateSurvey BuildSharedPackedStateSurvey(IReadOnlyList<VtbfBlock> blocks)
{
    var ownerCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var ownerRawCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var ownerBitCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var ownerLowNibbleCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var ownerMaskF0Counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var ownerMaskF00Counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var ownerUpperMaskCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);

    foreach (var block in blocks)
    {
        foreach (var field in block.Fields)
        {
            if (!TryGetSharedPackedStateOwner(block.Tag, field.Id, out var owner))
            {
                continue;
            }

            var value = GetFirstFieldInt32(field);
            AddCount(ownerCounts, owner);
            AddCount(ownerRawCounts, $"{owner}|{FormatNullableHex(value)}");

            foreach (var bit in EnumerateSetBits(value))
            {
                AddCount(ownerBitCounts, $"{owner}|{bit}");
            }

            if (value is null)
            {
                AddCount(ownerLowNibbleCounts, $"{owner}|?");
                AddCount(ownerMaskF0Counts, $"{owner}|?");
                AddCount(ownerMaskF00Counts, $"{owner}|?");
                AddCount(ownerUpperMaskCounts, $"{owner}|?");
                continue;
            }

            var raw = unchecked((uint)value.Value);
            AddCount(ownerLowNibbleCounts, $"{owner}|{FormatSurveyHex(raw & 0xFu)}");
            AddCount(ownerMaskF0Counts, $"{owner}|{FormatSurveyHex(raw & 0xF0u)}");
            AddCount(ownerMaskF00Counts, $"{owner}|{FormatSurveyHex(raw & 0xF00u)}");
            AddCount(ownerUpperMaskCounts, $"{owner}|{FormatSurveyHex(raw & ~0xFFFu)}");
        }
    }

    return new SharedPackedStateSurvey(
        ownerCounts,
        ownerRawCounts,
        ownerBitCounts,
        ownerLowNibbleCounts,
        ownerMaskF0Counts,
        ownerMaskF00Counts,
        ownerUpperMaskCounts);
}

static bool TryGetSharedPackedStateOwner(string tag, int fieldId, out string owner)
{
    owner = (tag, fieldId) switch
    {
        ("CIMG", 0x48) => "CIMG.0x48",
        ("CNUM", 0x48) => "CNUM.0x48",
        ("CSLI", 0x80) => "CSLI.0x80",
        ("LAYR", 0x20) => "LAYR.0x20",
        ("SLIC", 0x83) => "SLIC.0x83",
        ("TEX ", 0x62) => "TEX.0x62",
        _ => string.Empty,
    };

    return owner.Length > 0;
}

static CrfdReferenceSurvey BuildCrfdReferenceSurvey(
    SbSceneFile file,
    string path,
    SurveySceneNameIndex sceneNameIndex)
{
    var context = BuildCrfdReferenceContext(file, path, sceneNameIndex);
    var stringFieldRelationCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var stringFieldTargetTypeCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var field90Field91RelationCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var field90Field91EqualityCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var field90Field91Field92RelationCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);

    foreach (var record in file.Surfboard.Resources.CrfdRecords)
    {
        AddCrfdStringFieldSurvey(
            "0x90",
            record.Field90,
            context,
            stringFieldRelationCounts,
            stringFieldTargetTypeCounts);
        AddCrfdStringFieldSurvey(
            "0x91",
            record.Field91,
            context,
            stringFieldRelationCounts,
            stringFieldTargetTypeCounts);
        AddCrfdField90Field91Relations(record.Field90, record.Field91, context, field90Field91RelationCounts);
        AddCount(field90Field91EqualityCounts, FormatCrfdField90Field91Equality(record.Field90, record.Field91));
        AddCount(field90Field91Field92RelationCounts, FormatCrfdField90Field91Field92Relation(record));
    }

    return new CrfdReferenceSurvey(
        stringFieldRelationCounts,
        stringFieldTargetTypeCounts,
        field90Field91RelationCounts,
        field90Field91EqualityCounts,
        field90Field91Field92RelationCounts);
}

static CrfdReferenceContext BuildCrfdReferenceContext(
    SbSceneFile file,
    string path,
    SurveySceneNameIndex sceneNameIndex)
{
    var resources = file.Surfboard.Resources;
    var directory = Path.GetDirectoryName(path) ?? string.Empty;
    var ownerFileStem = Path.GetFileNameWithoutExtension(path) ?? string.Empty;
    var ownerScenePrefix = GetSceneStemPrefix(ownerFileStem);
    var cropReferences = EnumerateCropReferences(resources).Select(static item => item.Reference).ToArray();

    return new CrfdReferenceContext(
        Path.GetFileName(directory),
        ownerFileStem,
        ownerScenePrefix,
        TryGetSceneStemSuffix(ownerFileStem),
        file.Surfboard.Objects.FirstOrDefault(static item => item.Tag is "SCN " or "SCN")?.Name,
        sceneNameIndex,
        sceneNameIndex.FileStemsByDirectory.TryGetValue(directory, out var siblingFileStems)
            ? siblingFileStems
            : new HashSet<string>(StringComparer.Ordinal),
        sceneNameIndex.SceneSuffixesByDirectory.TryGetValue(directory, out var siblingSceneSuffixes)
            ? siblingSceneSuffixes
            : new HashSet<string>(StringComparer.Ordinal),
        CreateSurveyStringSet(new[] { resources.TextureListName }),
        CreateSurveyStringSet(resources.Atlases.Select(static atlas => atlas.Name)),
        CreateSurveyStringSet(resources.ImageCasts.Select(static imageCast => imageCast.NodeName)),
        CreateSurveyStringSet(resources.CnumRecords
            .Select(static record => record.FieldA1)
            .Concat(resources.CnumRecords.Select(static record => record.NodeName))),
        CreateSurveyStringSet(resources.SliceCasts.Select(static sliceCast => sliceCast.NodeName)),
        CreateSurveyStringSet(file.Surfboard.Nodes.Select(static node => node.Name)),
        CreateSurveyStringSet(cropReferences.Select(static reference => reference.AtlasName)),
        CreateSurveyStringSet(cropReferences.Select(static reference => reference.CropPath)));
}

static void AddCrfdStringFieldSurvey(
    string fieldName,
    string? value,
    CrfdReferenceContext context,
    IDictionary<string, int> relationCounts,
    IDictionary<string, int> targetTypeCounts)
{
    var fieldPrefix = fieldName + "|";
    if (string.IsNullOrWhiteSpace(value))
    {
        AddCount(relationCounts, fieldPrefix + "empty");
        AddCount(targetTypeCounts, fieldPrefix + "empty");
        return;
    }

    var matchedTarget = false;
    AddCount(relationCounts, fieldPrefix + "present");
    AddCount(relationCounts, fieldPrefix + (IsAsciiPrintable(value) ? "ascii-printable" : "non-ascii-or-control"));
    if (value.Contains('/', StringComparison.Ordinal) || value.Contains('\\', StringComparison.Ordinal))
    {
        AddCount(relationCounts, fieldPrefix + "contains-slash");
    }

    if (value.Contains('.', StringComparison.Ordinal))
    {
        AddCount(relationCounts, fieldPrefix + "contains-dot");
    }

    AddCrfdStringMatch(value == context.OwnerSceneName, "eq-owner-scene-name", "owner-scene-name");
    AddCrfdStringMatch(value == context.OwnerFileStem, "eq-owner-scene-stem", "owner-scene-stem");
    AddCrfdStringMatch(value == context.OwnerScenePrefix, "eq-owner-scene-prefix", "owner-scene-prefix");
    AddCrfdStringMatch(value == context.OwnerDirectoryName, "eq-owner-directory", "owner-directory");
    AddCrfdStringMatch(
        string.Equals(value, context.OwnerDirectoryName, StringComparison.OrdinalIgnoreCase),
        "eq-owner-directory-ignore-case",
        "owner-directory-ignore-case");
    AddCrfdStringMatch(context.SceneNameIndex.FileStems.Contains(value), "eq-any-scene-stem", "scene-stem");
    AddCrfdStringMatch(
        ContainsOrdinalIgnoreCase(context.SceneNameIndex.FileStems, value),
        "eq-any-scene-stem-ignore-case",
        "scene-stem-ignore-case");
    AddCrfdStringMatch(context.SceneNameIndex.ScenePrefixes.Contains(value), "eq-any-scene-prefix", "scene-prefix");
    AddCrfdStringMatch(
        ContainsOrdinalIgnoreCase(context.SceneNameIndex.ScenePrefixes, value),
        "eq-any-scene-prefix-ignore-case",
        "scene-prefix-ignore-case");
    AddCrfdStringMatch(context.SceneNameIndex.SceneSuffixes.Contains(value), "eq-any-scene-suffix", "scene-suffix");
    AddCrfdStringMatch(
        ContainsOrdinalIgnoreCase(context.SceneNameIndex.SceneSuffixes, value),
        "eq-any-scene-suffix-ignore-case",
        "scene-suffix-ignore-case");
    AddCrfdStringMatch(context.SiblingFileStems.Contains(value), "eq-sibling-scene-stem", "sibling-scene-stem");
    AddCrfdStringMatch(
        ContainsOrdinalIgnoreCase(context.SiblingFileStems, value),
        "eq-sibling-scene-stem-ignore-case",
        "sibling-scene-stem-ignore-case");
    AddCrfdStringMatch(context.SiblingSceneSuffixes.Contains(value), "eq-sibling-scene-suffix", "sibling-scene-suffix");
    AddCrfdStringMatch(
        ContainsOrdinalIgnoreCase(context.SiblingSceneSuffixes, value),
        "eq-sibling-scene-suffix-ignore-case",
        "sibling-scene-suffix-ignore-case");
    AddCrfdStringMatch(context.LocalTextureListNames.Contains(value), "eq-local-texture-list-name", "local-texture-list");
    AddCrfdStringMatch(context.LocalTextureNames.Contains(value), "eq-local-resource-name:TEX", "local-TEX");
    AddCrfdStringMatch(context.LocalImageCastNames.Contains(value), "eq-local-resource-name:CIMG", "local-CIMG");
    AddCrfdStringMatch(context.LocalCnumNames.Contains(value), "eq-local-resource-name:CNUM", "local-CNUM");
    AddCrfdStringMatch(context.LocalSliceCastNames.Contains(value), "eq-local-resource-name:CSLI", "local-CSLI");
    AddCrfdStringMatch(context.LocalNodeNames.Contains(value), "eq-local-node-name", "local-node");
    AddCrfdStringMatch(context.LocalCrefAtlasNames.Contains(value), "eq-cref-target-atlas", "cref-atlas");
    AddCrfdStringMatch(context.LocalCrefCropPaths.Contains(value), "eq-cref-target-crop-path", "cref-crop-path");

    if (!matchedTarget)
    {
        AddCount(relationCounts, fieldPrefix + "no-mechanical-match");
        AddCount(targetTypeCounts, fieldPrefix + "no-mechanical-match");
    }

    void AddCrfdStringMatch(bool matches, string relation, string targetType)
    {
        if (!matches)
        {
            return;
        }

        matchedTarget = true;
        AddCount(relationCounts, fieldPrefix + relation);
        AddCount(targetTypeCounts, fieldPrefix + targetType);
    }
}

static void AddCrfdField90Field91Relations(
    string? field90,
    string? field91,
    CrfdReferenceContext context,
    IDictionary<string, int> counts)
{
    if (string.IsNullOrWhiteSpace(field90) || string.IsNullOrWhiteSpace(field91))
    {
        AddCount(counts, "missing-0x90-or-0x91");
        return;
    }

    var matchedPair = false;
    var compositeSceneStem = $"{field90}__{field91}";
    AddPairRelation(field90 == context.OwnerDirectoryName, "0x90:eq-owner-directory");
    AddPairRelation(field90 == context.OwnerScenePrefix, "0x90:eq-owner-scene-prefix");
    AddPairRelation(context.SceneNameIndex.ScenePrefixes.Contains(field90), "0x90:eq-any-scene-prefix");
    AddPairRelation(context.SceneNameIndex.SceneSuffixes.Contains(field91), "0x91:eq-any-scene-suffix");
    AddPairRelation(ContainsOrdinalIgnoreCase(context.SceneNameIndex.SceneSuffixes, field91), "0x91:eq-any-scene-suffix-ignore-case");
    AddPairRelation(context.SiblingSceneSuffixes.Contains(field91), "0x91:eq-sibling-scene-suffix");
    AddPairRelation(ContainsOrdinalIgnoreCase(context.SiblingSceneSuffixes, field91), "0x91:eq-sibling-scene-suffix-ignore-case");
    AddPairRelation(context.SceneNameIndex.FileStems.Contains(compositeSceneStem), "0x90+__+0x91:eq-any-scene-stem");
    AddPairRelation(ContainsOrdinalIgnoreCase(context.SceneNameIndex.FileStems, compositeSceneStem), "0x90+__+0x91:eq-any-scene-stem-ignore-case");
    AddPairRelation(context.SiblingFileStems.Contains(compositeSceneStem), "0x90+__+0x91:eq-sibling-scene-stem");
    AddPairRelation(ContainsOrdinalIgnoreCase(context.SiblingFileStems, compositeSceneStem), "0x90+__+0x91:eq-sibling-scene-stem-ignore-case");
    AddPairRelation(compositeSceneStem == context.OwnerFileStem, "0x90+__+0x91:eq-owner-scene-stem");
    AddPairRelation(
        field90 == context.OwnerDirectoryName && context.SiblingSceneSuffixes.Contains(field91),
        "0x90-owner-directory+0x91-sibling-scene-suffix");
    AddPairRelation(
        string.Equals(field90, context.OwnerDirectoryName, StringComparison.OrdinalIgnoreCase)
            && ContainsOrdinalIgnoreCase(context.SiblingSceneSuffixes, field91),
        "0x90-owner-directory+0x91-sibling-scene-suffix-ignore-case");
    AddPairRelation(
        field90 == context.OwnerScenePrefix && context.SceneNameIndex.SceneSuffixes.Contains(field91),
        "0x90-owner-prefix+0x91-any-scene-suffix");
    AddPairRelation(
        field90 == context.OwnerScenePrefix && ContainsOrdinalIgnoreCase(context.SceneNameIndex.SceneSuffixes, field91),
        "0x90-owner-prefix+0x91-any-scene-suffix-ignore-case");

    if (!matchedPair)
    {
        AddCount(counts, "no-pair-scene-match");
    }

    void AddPairRelation(bool matches, string relation)
    {
        if (!matches)
        {
            return;
        }

        matchedPair = true;
        AddCount(counts, relation);
    }
}

static string FormatCrfdField90Field91Equality(string? field90, string? field91)
{
    var has90 = !string.IsNullOrWhiteSpace(field90);
    var has91 = !string.IsNullOrWhiteSpace(field91);
    return (has90, has91) switch
    {
        (false, false) => "0x90=empty|0x91=empty",
        (true, false) => "0x90=present|0x91=empty",
        (false, true) => "0x90=empty|0x91=present",
        _ => string.Equals(field90, field91, StringComparison.Ordinal)
            ? "0x90==0x91"
            : "0x90!=0x91",
    };
}

static string FormatCrfdField90Field91Field92Relation(SbSceneCrfdRecord record)
{
    var has90 = !string.IsNullOrWhiteSpace(record.Field90);
    var has91 = !string.IsNullOrWhiteSpace(record.Field91);
    var has92 = record.Field92 is not null;
    if (!has90 || !has91 || !has92)
    {
        return $"0x90={(has90 ? "present" : "empty")}|0x91={(has91 ? "present" : "empty")}|0x92={(has92 ? "present" : "empty")}";
    }

    var field92 = record.Field92!.Value.ToString(CultureInfo.InvariantCulture);
    var equal90And91 = string.Equals(record.Field90, record.Field91, StringComparison.Ordinal);
    var equal90And92 = string.Equals(record.Field90, field92, StringComparison.Ordinal);
    var equal91And92 = string.Equals(record.Field91, field92, StringComparison.Ordinal);

    return (equal90And91, equal90And92, equal91And92) switch
    {
        (true, true, true) => "0x90==0x91==0x92",
        (true, false, false) => "0x90==0x91!=0x92",
        (false, true, false) => "0x90==0x92!=0x91",
        (false, false, true) => "0x91==0x92!=0x90",
        _ => "all-present-all-distinct",
    };
}

static string GetSceneStemPrefix(string stem)
{
    var separator = stem.IndexOf("__", StringComparison.Ordinal);
    return separator < 0 ? stem : stem[..separator];
}

static string? TryGetSceneStemSuffix(string stem)
{
    var separator = stem.IndexOf("__", StringComparison.Ordinal);
    return separator < 0 || separator + 2 >= stem.Length ? null : stem[(separator + 2)..];
}

static IReadOnlySet<string> CreateSurveyStringSet(IEnumerable<string?> values)
{
    var result = new HashSet<string>(StringComparer.Ordinal);
    foreach (var value in values)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            result.Add(value);
        }
    }

    return result;
}

static bool IsAsciiPrintable(string value)
{
    return value.All(static ch => ch is >= ' ' and <= '~');
}

static bool ContainsOrdinalIgnoreCase(IEnumerable<string> values, string value)
{
    return values.Any(candidate => string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase));
}

static ProjectSurvey BuildProjectSurvey(IReadOnlyList<VtbfBlock> blocks, IReadOnlyList<TrackInfo> tracks)
{
    var field00Counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var field01Counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var field05Counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var field55Counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var field56Counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var field56TrackLastRelationCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var field56KeyMaxRelationCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var field56DeltaToTrackLastCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var field56DeltaToKeyMaxCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var fieldSequenceCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var fieldSetCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var trackLastFrames = tracks
        .Select(static track => track.LastFrame)
        .Where(static frame => frame is not null)
        .Select(static frame => frame!.Value)
        .ToArray();
    var keyFrames = tracks
        .SelectMany(static track => track.Keyframes)
        .Select(static key => key.KeyFrame)
        .Where(static frame => frame is not null)
        .Select(static frame => frame!.Value)
        .ToArray();
    var maxTrackLast = trackLastFrames.Length == 0 ? (int?)null : trackLastFrames.Max();
    var maxKeyFrame = keyFrames.Length == 0 ? (int?)null : keyFrames.Max();

    foreach (var block in blocks.Where(static block => block.Tag == "PROJ"))
    {
        AddCount(fieldSequenceCounts, FormatVtbfFieldSequence(block.Fields));
        AddCount(fieldSetCounts, FormatVtbfFieldSet(block.Fields));

        var field56Value = GetFirstInt32(block.Fields, 0x56);
        AddCount(field56TrackLastRelationCounts, FormatProjectFrameRelation(field56Value, maxTrackLast, "TrackLast"));
        AddCount(field56KeyMaxRelationCounts, FormatProjectFrameRelation(field56Value, maxKeyFrame, "KeyMax"));
        AddCount(field56DeltaToTrackLastCounts, FormatProjectFrameDelta(field56Value, maxTrackLast, "TrackLast"));
        AddCount(field56DeltaToKeyMaxCounts, FormatProjectFrameDelta(field56Value, maxKeyFrame, "KeyMax"));

        foreach (var field in block.Fields)
        {
            switch (field.Id)
            {
                case 0x00:
                    AddCount(field00Counts, FormatNullableInt(GetFirstFieldInt32(field)));
                    break;
                case 0x01:
                    AddCount(field01Counts, FormatNullableInt(GetFirstFieldInt32(field)));
                    break;
                case 0x05:
                    AddCount(field05Counts, FormatNullableInt(GetFirstFieldInt32(field)));
                    break;
                case 0x55:
                    AddCount(field55Counts, FormatNullableInt(GetFirstFieldInt32(field)));
                    break;
                case 0x56:
                    AddCount(field56Counts, FormatNullableInt(GetFirstFieldInt32(field)));
                    break;
            }
        }
    }

    return new ProjectSurvey(
        field00Counts,
        field01Counts,
        field05Counts,
        field55Counts,
        field56Counts,
        field56TrackLastRelationCounts,
        field56KeyMaxRelationCounts,
        field56DeltaToTrackLastCounts,
        field56DeltaToKeyMaxCounts,
        fieldSequenceCounts,
        fieldSetCounts);
}

static string FormatProjectFrameRelation(int? projectFrame, int? maxFrame, string frameKind)
{
    if (projectFrame is null)
    {
        return "missingProjectField56";
    }

    if (maxFrame is null)
    {
        return $"missing{frameKind}";
    }

    return (projectFrame.Value - maxFrame.Value) switch
    {
        0 => "equals",
        > 0 => "greater",
        _ => "less",
    };
}

static string FormatProjectFrameDelta(int? projectFrame, int? maxFrame, string frameKind)
{
    if (projectFrame is null)
    {
        return "missingProjectField56";
    }

    if (maxFrame is null)
    {
        return $"missing{frameKind}";
    }

    return FormatNullableInt(projectFrame.Value - maxFrame.Value);
}

static ScnSurvey BuildScnSurvey(IReadOnlyList<VtbfBlock> blocks)
{
    var nameCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var field04RawHexCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var field10Counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var field11Counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var field40Counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var field41Counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var field10Field11Counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var field40Field41Counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var paramLowLayerCountDeltaCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var paramLowField10DeltaCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var field10LayerCountDeltaCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var fieldSequenceCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var fieldSetCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var layerCount = blocks.Count(static block => block.Tag == "LAYR");

    foreach (var block in blocks.Where(static block => block.Tag == "SCN "))
    {
        AddCount(fieldSequenceCounts, FormatVtbfFieldSequence(block.Fields));
        AddCount(fieldSetCounts, FormatVtbfFieldSet(block.Fields));
        AddCount(paramLowLayerCountDeltaCounts, FormatDelta(block.ParamLow, layerCount));

        var field10Value = GetFirstInt32(block.Fields, 0x10);
        var field11Value = GetFirstInt32(block.Fields, 0x11);
        var field40Value = GetFirstInt32(block.Fields, 0x40);
        var field41Value = GetFirstInt32(block.Fields, 0x41);
        AddCount(field10Field11Counts, $"{FormatScnIntValue(field10Value)}|{FormatScnIntValue(field11Value)}");
        AddCount(field40Field41Counts, $"{FormatScnIntValue(field40Value)}|{FormatScnIntValue(field41Value)}");
        AddCount(paramLowField10DeltaCounts, FormatDelta(block.ParamLow, field10Value));
        AddCount(field10LayerCountDeltaCounts, FormatDelta(field10Value, layerCount));

        foreach (var field in block.Fields)
        {
            switch (field.Id)
            {
                case 0x03:
                    AddCount(nameCounts, FormatSurveyStringKey(field.StringValue ?? field.Preview));
                    break;
                case 0x04:
                    AddCount(field04RawHexCounts, Convert.ToHexString(field.Raw));
                    break;
                case 0x10:
                    AddCount(field10Counts, FormatScnIntValue(GetFirstFieldInt32(field)));
                    break;
                case 0x11:
                    AddCount(field11Counts, FormatScnIntValue(GetFirstFieldInt32(field)));
                    break;
                case 0x40:
                    AddCount(field40Counts, FormatScnIntValue(GetFirstFieldInt32(field)));
                    break;
                case 0x41:
                    AddCount(field41Counts, FormatScnIntValue(GetFirstFieldInt32(field)));
                    break;
            }
        }
    }

    return new ScnSurvey(
        nameCounts,
        field04RawHexCounts,
        field10Counts,
        field11Counts,
        field40Counts,
        field41Counts,
        field10Field11Counts,
        field40Field41Counts,
        paramLowLayerCountDeltaCounts,
        paramLowField10DeltaCounts,
        field10LayerCountDeltaCounts,
        fieldSequenceCounts,
        fieldSetCounts);
}

static string FormatScnIntValue(int? value)
{
    return $"{FormatNullableInt(value)}|{FormatNullableHex(value)}";
}

static LayerSurvey BuildLayerSurvey(IReadOnlyList<VtbfBlock> blocks, int sceneNodeCount)
{
    var nameCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var field20Counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var field20BitCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var field21Counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var field21BitCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var field22Counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var field22BitCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var field21SceneNodeCountDeltaCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var paramLowField22DeltaCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var fieldSequenceCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var fieldSetCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var field21Sum = 0;
    var hasField21 = false;

    foreach (var block in blocks.Where(static block => block.Tag == "LAYR"))
    {
        AddCount(fieldSequenceCounts, FormatVtbfFieldSequence(block.Fields));
        AddCount(fieldSetCounts, FormatVtbfFieldSet(block.Fields));
        var field21Value = GetFirstInt32(block.Fields, 0x21);
        var field22Value = GetFirstInt32(block.Fields, 0x22);
        AddCount(paramLowField22DeltaCounts, FormatDelta(block.ParamLow, field22Value));
        if (field21Value is not null)
        {
            field21Sum += field21Value.Value;
            hasField21 = true;
        }

        foreach (var field in block.Fields)
        {
            switch (field.Id)
            {
                case 0x03:
                    AddCount(nameCounts, FormatSurveyStringKey(field.StringValue ?? field.Preview));
                    break;
                case 0x20:
                    AddLayerIntField(field20Counts, field20BitCounts, field);
                    break;
                case 0x21:
                    AddLayerIntField(field21Counts, field21BitCounts, field);
                    break;
                case 0x22:
                    AddLayerIntField(field22Counts, field22BitCounts, field);
                    break;
            }
        }
    }

    AddCount(field21SceneNodeCountDeltaCounts, FormatDelta(hasField21 ? field21Sum : null, sceneNodeCount));

    return new LayerSurvey(
        nameCounts,
        field20Counts,
        field20BitCounts,
        field21Counts,
        field21BitCounts,
        field22Counts,
        field22BitCounts,
        field21SceneNodeCountDeltaCounts,
        paramLowField22DeltaCounts,
        fieldSequenceCounts,
        fieldSetCounts);
}

static void AddLayerIntField(
    IDictionary<string, int> valueCounts,
    IDictionary<string, int> bitCounts,
    VtbfField field)
{
    var value = GetFirstFieldInt32(field);
    AddCount(valueCounts, $"{FormatNullableInt(value)}|{FormatNullableHex(value)}");
    foreach (var bit in EnumerateSetBits(value))
    {
        AddCount(bitCounts, bit.ToString(CultureInfo.InvariantCulture));
    }
}

static CameraSurvey BuildCameraSurvey(IReadOnlyList<VtbfBlock> blocks)
{
    var nameCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var field12VectorCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var field13VectorCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var field14Counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var field14BitCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var field15Counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var field16Counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var fieldSequenceCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var fieldSetCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);

    foreach (var block in blocks.Where(static block => block.Tag == "CAM "))
    {
        AddCount(fieldSequenceCounts, FormatVtbfFieldSequence(block.Fields));
        AddCount(fieldSetCounts, FormatVtbfFieldSet(block.Fields));

        foreach (var field in block.Fields)
        {
            switch (field.Id)
            {
                case 0x03:
                    AddCount(nameCounts, FormatSurveyStringKey(field.StringValue ?? field.Preview));
                    break;
                case 0x12:
                    AddCount(field12VectorCounts, FormatSurveyStringKey(field.Preview));
                    break;
                case 0x13:
                    AddCount(field13VectorCounts, FormatSurveyStringKey(field.Preview));
                    break;
                case 0x14:
                    var field14Value = GetFirstFieldInt32(field);
                    AddCount(field14Counts, $"{FormatNullableInt(field14Value)}|{FormatNullableHex(field14Value)}");
                    foreach (var bit in EnumerateSetBits(field14Value))
                    {
                        AddCount(field14BitCounts, bit.ToString(CultureInfo.InvariantCulture));
                    }

                    break;
                case 0x15:
                    AddCount(field15Counts, FormatNullableDouble(GetFirstFieldDouble(field)));
                    break;
                case 0x16:
                    AddCount(field16Counts, FormatNullableDouble(GetFirstFieldDouble(field)));
                    break;
            }
        }
    }

    return new CameraSurvey(
        nameCounts,
        field12VectorCounts,
        field13VectorCounts,
        field14Counts,
        field14BitCounts,
        field15Counts,
        field16Counts,
        fieldSequenceCounts,
        fieldSetCounts);
}

static NcatSurvey BuildNcatSurvey(SbSceneFile file)
{
    var kindTypeByteCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var kindCategoryCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var typeByteCategoryCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var kindParameterPresenceCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var parameterStringCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var parameterFieldTypeCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var kindParameterFieldTypeCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var categoryParameterFieldTypeCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var parameterFieldTypePreviewCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var kindNodeFlagCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var kindNodeFlagBitCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var kindNodeGroupCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var kindDisplayCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var kindCimgTargetCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var kindAnimatedNodeCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);

    var imageCastNodeIndexes = file.Surfboard.Resources.ImageCasts
        .Where(static imageCast => imageCast.CastIndex >= 0)
        .Select(static imageCast => imageCast.CastIndex)
        .ToHashSet();
    var animatedNodeIndexes = file.Surfboard.Animations
        .SelectMany(static animation => animation.Motions)
        .Select(static motion => motion.TargetIndex)
        .Where(static nodeIndex => nodeIndex is not null)
        .Select(static nodeIndex => nodeIndex!.Value)
        .ToHashSet();

    foreach (var record in file.Surfboard.NodeCategoryDetails)
    {
        var kindKey = NormalizeNcatKind(record.KindName);
        var typeKey = FormatNullableHex(record.TypeByte);
        var categoryKey = FormatNullableInt(record.CategoryId);
        var parameterKey = NormalizeSurveyParameter(record.ParameterString);
        var parameterPresenceKey = string.IsNullOrWhiteSpace(record.ParameterString) ? "empty" : "present";
        var parameterFields = record.Fields
            .Where(static field => field.IdHex == "0x000F")
            .ToArray();

        AddCount(kindTypeByteCounts, $"{kindKey}|{typeKey}");
        AddCount(kindCategoryCounts, $"{kindKey}|{categoryKey}");
        AddCount(typeByteCategoryCounts, $"{typeKey}|{categoryKey}");
        AddCount(kindParameterPresenceCounts, $"{kindKey}|{parameterPresenceKey}");
        AddCount(parameterStringCounts, parameterKey);
        if (parameterFields.Length == 0)
        {
            AddNcatParameterFieldCounts(null, kindKey, categoryKey, parameterFieldTypeCounts, kindParameterFieldTypeCounts, categoryParameterFieldTypeCounts, parameterFieldTypePreviewCounts);
        }
        else
        {
            foreach (var parameterField in parameterFields)
            {
                AddNcatParameterFieldCounts(parameterField, kindKey, categoryKey, parameterFieldTypeCounts, kindParameterFieldTypeCounts, categoryParameterFieldTypeCounts, parameterFieldTypePreviewCounts);
            }
        }

        var node = record.Index >= 0 && record.Index < file.Surfboard.Nodes.Count
            ? file.Surfboard.Nodes[record.Index]
            : null;
        if (node is null)
        {
            AddCount(kindNodeFlagCounts, $"{kindKey}|missingNode");
            AddCount(kindNodeGroupCounts, $"{kindKey}|missingNode");
            AddCount(kindDisplayCounts, $"{kindKey}|missingNode");
            AddCount(kindCimgTargetCounts, $"{kindKey}|missingNode");
            AddCount(kindAnimatedNodeCounts, $"{kindKey}|missingNode");
            continue;
        }

        AddCount(kindNodeFlagCounts, $"{kindKey}|{FormatNullableHex(node.Flags)}");
        AddCount(kindNodeGroupCounts, $"{kindKey}|{NormalizeSurveyGroup(node.Group)}");
        AddCount(kindDisplayCounts, $"{kindKey}|{FormatInitialDisplayState(node)}");
        AddCount(kindCimgTargetCounts, $"{kindKey}|{(imageCastNodeIndexes.Contains(node.Index) ? "true" : "false")}");
        AddCount(kindAnimatedNodeCounts, $"{kindKey}|{(animatedNodeIndexes.Contains(node.Index) ? "true" : "false")}");

        foreach (var bit in node.FlagBits.Distinct().Order())
        {
            AddCount(kindNodeFlagBitCounts, $"{kindKey}|{bit}");
        }
    }

    return new NcatSurvey(
        kindTypeByteCounts,
        kindCategoryCounts,
        typeByteCategoryCounts,
        kindParameterPresenceCounts,
        parameterStringCounts,
        parameterFieldTypeCounts,
        kindParameterFieldTypeCounts,
        categoryParameterFieldTypeCounts,
        parameterFieldTypePreviewCounts,
        kindNodeFlagCounts,
        kindNodeFlagBitCounts,
        kindNodeGroupCounts,
        kindDisplayCounts,
        kindCimgTargetCounts,
        kindAnimatedNodeCounts);
}

static VtbfStructureSurvey BuildVtbfStructureSurvey(IReadOnlyList<VtbfBlock> blocks)
{
    var tagCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var tagParamRawCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var tagParamLowHighCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var tagPropertyCountCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var tagParamHighPropertyCountCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var tagTrailingByteCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var keyParamHighModulo5Counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var fieldDirectoryCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var fieldDirectoryBlockCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var fieldCountValueCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var fieldStrideValueCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);

    foreach (var block in blocks)
    {
        AddCount(tagCounts, block.Tag);
        AddCount(tagParamRawCounts, $"{block.Tag}|0x{block.ParamRawHex ?? "?"}");
        AddCount(tagParamLowHighCounts, $"{block.Tag}|{FormatNullableInt(block.ParamLow)}|{FormatNullableInt(block.ParamHigh)}");
        AddCount(tagPropertyCountCounts, $"{block.Tag}|{block.PropertyCount}");
        AddCount(tagParamHighPropertyCountCounts, $"{block.Tag}|{FormatNullableInt(block.ParamHigh)}|{block.PropertyCount}");
        AddCount(tagTrailingByteCounts, $"{block.Tag}|{block.TrailingBytes?.Length ?? 0}");
        if (block.Tag == "KEY " && block.ParamHigh is int paramHigh)
        {
            AddCount(keyParamHighModulo5Counts, (paramHigh % 5).ToString(CultureInfo.InvariantCulture));
        }

        var blockFieldKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in block.Fields)
        {
            var key = FormatVtbfFieldDirectoryKey(block.Tag, field);
            AddCount(fieldDirectoryCounts, key);
            AddCount(fieldCountValueCounts, $"{key}|{field.Count}");
            AddCount(fieldStrideValueCounts, $"{key}|{field.Stride}");
            blockFieldKeys.Add(key);
        }

        foreach (var key in blockFieldKeys)
        {
            AddCount(fieldDirectoryBlockCounts, key);
        }
    }

    return new VtbfStructureSurvey(
        tagCounts,
        tagParamRawCounts,
        tagParamLowHighCounts,
        tagPropertyCountCounts,
        tagParamHighPropertyCountCounts,
        tagTrailingByteCounts,
        keyParamHighModulo5Counts,
        fieldDirectoryCounts,
        fieldDirectoryBlockCounts,
        fieldCountValueCounts,
        fieldStrideValueCounts);
}

static CompactTailSurvey BuildCompactTailSurvey(SbSceneFile file)
{
    var cnumField48Counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var cnumFieldA0Counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var cnumFieldA1Field44Counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var cnumFieldA1CropReferenceCountCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var cnumFieldA1ZeroMarkerFieldCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var cnumFieldA1NodeFlagCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var cnumFieldA1NodeGroupCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var cnumFieldA1DisplayCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var cnumFieldA1CimgTargetCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var cnumFieldA1AnimatedTargetCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var cnumFieldSequenceCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var cnumFieldSetCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var cnumFieldA1RawLengthCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var cnumFieldA1ContentLengthCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var cnumFieldA1Utf8StatusCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var cnumFieldA1ShiftJisByteShapeCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var cnumFieldA1RawPreviewCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var textField7AStringCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var textField7ARawLengthCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var textField7AContentLengthCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var textField7AUtf8StatusCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var textField7AShiftJisByteShapeCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var textField7AShiftJisDecodeStatusCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var textField7AShiftJisStringCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var textField7ARawPreviewCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var textField7AField41Counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var textField7AField78Counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var textField7AField79Counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var textField7AField7CCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var textField33VectorCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var textField33RawHexCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var textField7BPackedValuesCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var textField7BRawHexCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var textField78Field79Counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var textZeroMarkerField7ACounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var textFieldSequenceCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var textFieldSetCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var sliceCastField40Counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var sliceCastField41Counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var sliceCastField42Counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var sliceCastField43Counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var sliceCastField80Counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var sliceCastField81Counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var sliceCastField82Counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var sliceCastField84Counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var sliceCastField85Counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var sliceCastField86Counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var sliceCastField87Counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var sliceCastTargetNodeFlagCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var sliceCastTargetNodeGroupCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var sliceCastTargetDisplayCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var sliceCastTargetCimgTargetCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var sliceCastFieldSequenceCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var sliceCastFieldSetCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var sliceRecordField40Counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var sliceRecordField41Counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var sliceRecordField45Counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var sliceRecordField37ColorCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var sliceRecordField38ColorCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var sliceRecordField39ColorCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var sliceRecordField39ColorCountCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var sliceRecordField83Field40Counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var sliceRecordField83Field41Counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var sliceRecordField83Field45Counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var sliceRecordFieldSequenceCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var sliceRecordFieldSetCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var sliceRecordShapeCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);

    var imageCastNodeIndexes = file.Surfboard.Resources.ImageCasts
        .Where(static imageCast => imageCast.CastIndex >= 0)
        .Select(static imageCast => imageCast.CastIndex)
        .ToHashSet();
    var animatedNodeIndexes = file.Surfboard.Animations
        .SelectMany(static animation => animation.Motions)
        .Select(static motion => motion.TargetIndex)
        .Where(static nodeIndex => nodeIndex is not null)
        .Select(static nodeIndex => nodeIndex!.Value)
        .ToHashSet();

    foreach (var record in file.Surfboard.Resources.CnumRecords)
    {
        var fieldA1Key = FormatSurveyStringKey(record.FieldA1);
        var node = TryGetNode(file.Surfboard.Nodes, record.Field51);
        AddCount(cnumField48Counts, FormatNullableInt(record.Field48));
        AddCount(cnumFieldA0Counts, FormatNullableInt(record.FieldA0));
        AddCount(cnumFieldA1Field44Counts, $"{fieldA1Key}|{FormatNullableInt(record.Field44Count)}");
        AddCount(cnumFieldA1CropReferenceCountCounts, $"{fieldA1Key}|{record.CropReferences.Count}");

        if (record.ZeroLengthMarkerFieldIds.Count == 0)
        {
            AddCount(cnumFieldA1ZeroMarkerFieldCounts, $"{fieldA1Key}|none");
        }
        else
        {
            foreach (var markerId in record.ZeroLengthMarkerFieldIds)
            {
                AddCount(cnumFieldA1ZeroMarkerFieldCounts, $"{fieldA1Key}|0x{markerId:X}");
            }
        }

        AddCount(cnumFieldA1NodeFlagCounts, $"{fieldA1Key}|{FormatTargetNodeFlag(record.Field51, node)}");
        AddCount(cnumFieldA1NodeGroupCounts, $"{fieldA1Key}|{FormatTargetNodeGroup(record.Field51, node)}");
        AddCount(cnumFieldA1DisplayCounts, $"{fieldA1Key}|{FormatTargetDisplay(record.Field51, node)}");
        AddCount(cnumFieldA1CimgTargetCounts, $"{fieldA1Key}|{FormatTargetSetMembership(record.Field51, node, imageCastNodeIndexes)}");
        AddCount(cnumFieldA1AnimatedTargetCounts, $"{fieldA1Key}|{FormatTargetSetMembership(record.Field51, node, animatedNodeIndexes)}");
        AddRecordShapeCounts(record.Fields, cnumFieldSequenceCounts, cnumFieldSetCounts);
    }

    foreach (var field in EnumerateSurveyFields(file, "CNUM", 0xA1))
    {
        AddStringRawFieldCounts(
            field.Raw,
            cnumFieldA1RawLengthCounts,
            cnumFieldA1ContentLengthCounts,
            cnumFieldA1Utf8StatusCounts,
            cnumFieldA1ShiftJisByteShapeCounts,
            null,
            null,
            cnumFieldA1RawPreviewCounts);
    }

    foreach (var record in file.Surfboard.Resources.TextRecords)
    {
        var textKey = FormatSurveyStringKey(record.Field7A);
        AddCount(textField7AStringCounts, textKey);
        AddCount(textField7AField41Counts, $"{textKey}|{FormatNullableInt(record.Field41)}");
        AddCount(textField7AField78Counts, $"{textKey}|{FormatNullableInt(record.Field78)}");
        AddCount(textField7AField79Counts, $"{textKey}|{FormatNullableInt(record.Field79)}");
        AddCount(textField7AField7CCounts, $"{textKey}|{FormatNullableInt(record.Field7C)}");
        AddCount(textField33VectorCounts, FormatSurveyVectorKey(record.Field33Vector));
        AddCount(textField33RawHexCounts, FormatSurveyRawKey(record.Field33RawHex));
        AddCount(textField7BPackedValuesCounts, FormatSurveyIntListKey(record.Field7BPackedValues));
        AddCount(textField7BRawHexCounts, FormatSurveyRawKey(record.Field7BRawHex));
        AddCount(textField78Field79Counts, $"{FormatNullableInt(record.Field78)}|{FormatNullableInt(record.Field79)}");

        if (record.ZeroLengthMarkerFieldIds.Count == 0)
        {
            AddCount(textZeroMarkerField7ACounts, "none|" + textKey);
        }
        else
        {
            foreach (var markerId in record.ZeroLengthMarkerFieldIds)
            {
                AddCount(textZeroMarkerField7ACounts, $"0x{markerId:X}|{textKey}");
            }
        }

        AddRecordShapeCounts(record.Fields, textFieldSequenceCounts, textFieldSetCounts);
    }

    foreach (var field in EnumerateSurveyFields(file, "TEXT", 0x7A))
    {
        AddStringRawFieldCounts(
            field.Raw,
            textField7ARawLengthCounts,
            textField7AContentLengthCounts,
            textField7AUtf8StatusCounts,
            textField7AShiftJisByteShapeCounts,
            textField7AShiftJisDecodeStatusCounts,
            textField7AShiftJisStringCounts,
            textField7ARawPreviewCounts);
    }

    foreach (var sliceCast in file.Surfboard.Resources.SliceCasts)
    {
        var node = TryGetNode(file.Surfboard.Nodes, sliceCast.TargetIndex);
        AddCount(sliceCastField40Counts, FormatNullableFloat(sliceCast.Field40));
        AddCount(sliceCastField41Counts, FormatNullableFloat(sliceCast.Field41));
        AddCount(sliceCastField42Counts, FormatNullableFloat(sliceCast.Field42));
        AddCount(sliceCastField43Counts, FormatNullableFloat(sliceCast.Field43));
        AddCount(sliceCastField80Counts, FormatNullableInt(sliceCast.Field80));
        AddCount(sliceCastField81Counts, FormatNullableInt(sliceCast.Field81));
        AddCount(sliceCastField82Counts, FormatNullableInt(sliceCast.Field82));
        AddCount(sliceCastField84Counts, FormatNullableInt(sliceCast.Field84));
        AddCount(sliceCastField85Counts, FormatNullableInt(sliceCast.Field85));
        AddCount(sliceCastField86Counts, FormatNullableFloat(sliceCast.Field86));
        AddCount(sliceCastField87Counts, FormatNullableFloat(sliceCast.Field87));
        AddCount(sliceCastTargetNodeFlagCounts, FormatTargetNodeFlag(sliceCast.TargetIndex, node));
        AddCount(sliceCastTargetNodeGroupCounts, FormatTargetNodeGroup(sliceCast.TargetIndex, node));
        AddCount(sliceCastTargetDisplayCounts, FormatTargetDisplay(sliceCast.TargetIndex, node));
        AddCount(sliceCastTargetCimgTargetCounts, FormatTargetSetMembership(sliceCast.TargetIndex, node, imageCastNodeIndexes));
        AddRecordShapeCounts(sliceCast.Fields, sliceCastFieldSequenceCounts, sliceCastFieldSetCounts);

        foreach (var slice in sliceCast.Slices)
        {
            var field83Key = FormatNullableHex(slice.Field83);
            AddRecordShapeCounts(slice.Fields, sliceRecordFieldSequenceCounts, sliceRecordFieldSetCounts);
            AddCount(sliceRecordShapeCounts, FormatSliceRecordShape(slice));
            AddCount(sliceRecordField40Counts, FormatNullableInt(slice.Field40));
            AddCount(sliceRecordField41Counts, FormatNullableInt(slice.Field41));
            AddCount(sliceRecordField45Counts, FormatNullableInt(slice.Field45));
            AddCount(sliceRecordField37ColorCounts, FormatColor(slice.Field37Color));
            AddCount(sliceRecordField38ColorCounts, FormatColor(slice.Field38Color));
            AddCount(sliceRecordField39ColorCountCounts, slice.Field39Colors.Count.ToString(CultureInfo.InvariantCulture));
            AddCount(sliceRecordField83Field40Counts, $"{field83Key}|{FormatNullableInt(slice.Field40)}");
            AddCount(sliceRecordField83Field41Counts, $"{field83Key}|{FormatNullableInt(slice.Field41)}");
            AddCount(sliceRecordField83Field45Counts, $"{field83Key}|{FormatNullableInt(slice.Field45)}");

            if (slice.Field39Colors.Count == 0)
            {
                AddCount(sliceRecordField39ColorCounts, "?");
            }
            else
            {
                foreach (var color in slice.Field39Colors)
                {
                    AddCount(sliceRecordField39ColorCounts, FormatColor(color));
                }
            }
        }
    }

    return new CompactTailSurvey(
        cnumField48Counts,
        cnumFieldA0Counts,
        cnumFieldA1Field44Counts,
        cnumFieldA1CropReferenceCountCounts,
        cnumFieldA1ZeroMarkerFieldCounts,
        cnumFieldA1NodeFlagCounts,
        cnumFieldA1NodeGroupCounts,
        cnumFieldA1DisplayCounts,
        cnumFieldA1CimgTargetCounts,
        cnumFieldA1AnimatedTargetCounts,
        cnumFieldSequenceCounts,
        cnumFieldSetCounts,
        cnumFieldA1RawLengthCounts,
        cnumFieldA1ContentLengthCounts,
        cnumFieldA1Utf8StatusCounts,
        cnumFieldA1ShiftJisByteShapeCounts,
        cnumFieldA1RawPreviewCounts,
        textField7AStringCounts,
        textField7ARawLengthCounts,
        textField7AContentLengthCounts,
        textField7AUtf8StatusCounts,
        textField7AShiftJisByteShapeCounts,
        textField7AShiftJisDecodeStatusCounts,
        textField7AShiftJisStringCounts,
        textField7ARawPreviewCounts,
        textField7AField41Counts,
        textField7AField78Counts,
        textField7AField79Counts,
        textField7AField7CCounts,
        textField33VectorCounts,
        textField33RawHexCounts,
        textField7BPackedValuesCounts,
        textField7BRawHexCounts,
        textField78Field79Counts,
        textZeroMarkerField7ACounts,
        textFieldSequenceCounts,
        textFieldSetCounts,
        sliceCastField40Counts,
        sliceCastField41Counts,
        sliceCastField42Counts,
        sliceCastField43Counts,
        sliceCastField80Counts,
        sliceCastField81Counts,
        sliceCastField82Counts,
        sliceCastField84Counts,
        sliceCastField85Counts,
        sliceCastField86Counts,
        sliceCastField87Counts,
        sliceCastTargetNodeFlagCounts,
        sliceCastTargetNodeGroupCounts,
        sliceCastTargetDisplayCounts,
        sliceCastTargetCimgTargetCounts,
        sliceCastFieldSequenceCounts,
        sliceCastFieldSetCounts,
        sliceRecordField40Counts,
        sliceRecordField41Counts,
        sliceRecordField45Counts,
        sliceRecordField37ColorCounts,
        sliceRecordField38ColorCounts,
        sliceRecordField39ColorCounts,
        sliceRecordField39ColorCountCounts,
        sliceRecordField83Field40Counts,
        sliceRecordField83Field41Counts,
        sliceRecordField83Field45Counts,
        sliceRecordFieldSequenceCounts,
        sliceRecordFieldSetCounts,
        sliceRecordShapeCounts);
}

static ColorAlphaSurvey BuildColorAlphaSurvey(SbSceneFile file)
{
    var imageCastNodeIndexes = file.Surfboard.Resources.ImageCasts
        .Where(static imageCast => imageCast.CastIndex >= 0)
        .Select(static imageCast => imageCast.CastIndex)
        .ToHashSet();
    var colorTrackTypeCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var colorTrackKeyTypeCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var colorTrackInitialMatchTypeCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var colorTrackCount = 0;
    var colorTrackKeyCount = 0;
    var colorTracksWithInitialChannel = 0;
    var colorTracksMissingInitialChannel = 0;
    var colorTrackInitialValueMatches = 0;
    var colorTrackInitialValueMismatches = 0;
    var colorTrackKeysInUnitRange = 0;
    var colorTrackKeysOutOfUnitRange = 0;
    var colorTrackKeysMissingValue = 0;
    var alphaOpacityTrackCount = 0;
    var alphaOpacityKeyCount = 0;
    var alphaOpacityTracksWithMaterialAlpha = 0;
    var alphaOpacityTracksMissingMaterialAlpha = 0;
    var alphaOpacityInitialAlphaMatches = 0;
    var alphaOpacityInitialAlphaMismatches = 0;
    var alphaOpacityCimgTargets = 0;
    var alphaOpacityDisplayFalseTargets = 0;
    var alphaOpacityKeysInUnitRange = 0;
    var alphaOpacityKeysOutOfUnitRange = 0;
    var alphaOpacityKeysMissingValue = 0;

    foreach (var animation in file.Surfboard.Animations)
    {
        _ = animation;
        foreach (var motion in animation.Motions)
        {
            var node = motion.TargetIndex is int nodeIndex && nodeIndex >= 0 && nodeIndex < file.Surfboard.Nodes.Count
                ? file.Surfboard.Nodes[nodeIndex]
                : null;
            foreach (var track in motion.Tracks)
            {
                if (IsColorTrackType(track.TrackType))
                {
                    colorTrackCount++;
                    var typeKey = FormatTrackTypeKey(track);
                    AddCount(colorTrackTypeCounts, typeKey);
                    var scalarValues = GetScalarValues(track).ToArray();
                    colorTrackKeyCount += track.Keyframes.Count;
                    AddCountByAmount(colorTrackKeyTypeCounts, typeKey, track.Keyframes.Count);
                    CountUnitRangeKeys(track, ref colorTrackKeysInUnitRange, ref colorTrackKeysOutOfUnitRange, ref colorTrackKeysMissingValue);

                    if (TryGetInitialColorChannel(node?.Transform2D, track.TrackType, out var initialValue))
                    {
                        colorTracksWithInitialChannel++;
                        if (scalarValues.Any(value => Math.Abs(value - initialValue) <= 0.01))
                        {
                            colorTrackInitialValueMatches++;
                            AddCount(colorTrackInitialMatchTypeCounts, typeKey);
                        }
                        else
                        {
                            colorTrackInitialValueMismatches++;
                        }
                    }
                    else
                    {
                        colorTracksMissingInitialChannel++;
                    }
                }

                if (track.TrackType == 24)
                {
                    alphaOpacityTrackCount++;
                    alphaOpacityKeyCount += track.Keyframes.Count;
                    CountUnitRangeKeys(track, ref alphaOpacityKeysInUnitRange, ref alphaOpacityKeysOutOfUnitRange, ref alphaOpacityKeysMissingValue);
                    if (motion.TargetIndex is int targetNodeIndex && imageCastNodeIndexes.Contains(targetNodeIndex))
                    {
                        alphaOpacityCimgTargets++;
                    }

                    if (node?.Transform2D?.Display == false)
                    {
                        alphaOpacityDisplayFalseTargets++;
                    }

                    var initialMaterialAlpha = node?.Transform2D?.MaterialColor is { } materialColor
                        ? materialColor.A / 255.0
                        : (double?)null;
                    if (initialMaterialAlpha is null)
                    {
                        alphaOpacityTracksMissingMaterialAlpha++;
                        continue;
                    }

                    alphaOpacityTracksWithMaterialAlpha++;
                    var scalarValues = GetScalarValues(track).ToArray();
                    if (scalarValues.Any(value => Math.Abs(value - initialMaterialAlpha.Value) <= 0.01))
                    {
                        alphaOpacityInitialAlphaMatches++;
                    }
                    else
                    {
                        alphaOpacityInitialAlphaMismatches++;
                    }
                }
            }
        }
    }

    return new ColorAlphaSurvey(
        colorTrackCount,
        colorTrackKeyCount,
        colorTracksWithInitialChannel,
        colorTracksMissingInitialChannel,
        colorTrackInitialValueMatches,
        colorTrackInitialValueMismatches,
        colorTrackKeysInUnitRange,
        colorTrackKeysOutOfUnitRange,
        colorTrackKeysMissingValue,
        colorTrackTypeCounts,
        colorTrackKeyTypeCounts,
        colorTrackInitialMatchTypeCounts,
        alphaOpacityTrackCount,
        alphaOpacityKeyCount,
        alphaOpacityTracksWithMaterialAlpha,
        alphaOpacityTracksMissingMaterialAlpha,
        alphaOpacityInitialAlphaMatches,
        alphaOpacityInitialAlphaMismatches,
        alphaOpacityCimgTargets,
        alphaOpacityDisplayFalseTargets,
        alphaOpacityKeysInUnitRange,
        alphaOpacityKeysOutOfUnitRange,
        alphaOpacityKeysMissingValue);
}

static TransformTrackSurvey BuildTransformTrackSurvey(SbSceneFile file)
{
    var trackTypeCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var keyTypeCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var storageCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var keyValueKindCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var initialMatchTypeCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var valueRangeCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var candidateDefaultKeyCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var trackCount = 0;
    var keyCount = 0;
    var tracksWithInitialChannel = 0;
    var tracksMissingInitialChannel = 0;
    var initialValueMatches = 0;
    var initialValueMismatches = 0;
    var keysMissingValue = 0;

    foreach (var animation in file.Surfboard.Animations)
    {
        _ = animation;
        foreach (var motion in animation.Motions)
        {
            var node = TryGetNode(file.Surfboard.Nodes, motion.TargetIndex);
            foreach (var track in motion.Tracks.Where(static track => IsTransformTrackType(track.TrackType)))
            {
                trackCount++;
                keyCount += track.Keyframes.Count;
                var typeKey = FormatTrackTypeKey(track);
                AddCount(trackTypeCounts, typeKey);
                AddCountByAmount(keyTypeCounts, typeKey, track.Keyframes.Count);
                AddCount(storageCounts, $"{typeKey}|{track.KeyValueStorage ?? "?"}");

                var values = new List<double>(track.Keyframes.Count);
                foreach (var keyframe in track.Keyframes)
                {
                    AddCount(keyValueKindCounts, $"{typeKey}|{FormatKeyValueTypeKey(keyframe)}");
                    if (!TryGetTransformKeyValue(keyframe, track.TrackType, out var value))
                    {
                        keysMissingValue++;
                        AddTransformCandidateDefaultKeyCount(candidateDefaultKeyCounts, track, null);
                        continue;
                    }

                    values.Add(value);
                    AddTransformCandidateDefaultKeyCount(candidateDefaultKeyCounts, track, value);
                }

                AddCount(valueRangeCounts, $"{typeKey}|{FormatValueRange(values)}");

                if (TryGetInitialTransformChannel(node?.Transform2D, track.TrackType, out var initialValue))
                {
                    tracksWithInitialChannel++;
                    if (values.Any(value => Math.Abs(value - initialValue) <= 0.01))
                    {
                        initialValueMatches++;
                        AddCount(initialMatchTypeCounts, typeKey);
                    }
                    else
                    {
                        initialValueMismatches++;
                    }
                }
                else
                {
                    tracksMissingInitialChannel++;
                }
            }
        }
    }

    return new TransformTrackSurvey(
        trackCount,
        keyCount,
        tracksWithInitialChannel,
        tracksMissingInitialChannel,
        initialValueMatches,
        initialValueMismatches,
        keysMissingValue,
        trackTypeCounts,
        keyTypeCounts,
        storageCounts,
        keyValueKindCounts,
        initialMatchTypeCounts,
        valueRangeCounts,
        candidateDefaultKeyCounts);
}

static bool IsTransformTrackType(int? trackType)
{
    return trackType is >= 0 and <= 8;
}

static bool TryGetTransformKeyValue(KeyframeInfo keyframe, int? trackType, out double value)
{
    if (trackType is 3 or 4 or 5 && keyframe.PackedAngleDegreesCandidate is double degrees)
    {
        value = degrees;
        return true;
    }

    if (keyframe.ScalarValue is double scalarValue)
    {
        value = scalarValue;
        return true;
    }

    value = 0;
    return false;
}

static bool TryGetInitialTransformChannel(Transform2DInfo? transform, int? trackType, out double value)
{
    value = 0;
    if (transform is null)
    {
        return false;
    }

    switch (trackType)
    {
        case 0 when transform.Translation is not null:
            value = transform.Translation.X;
            return true;
        case 1 when transform.Translation is not null:
            value = transform.Translation.Y;
            return true;
        case 5 when transform.RotationZDegreesCandidate is not null:
            value = transform.RotationZDegreesCandidate.Value;
            return true;
        case 5 when transform.RotationZ is not null:
            value = transform.RotationZ.Value;
            return true;
        case 6 when transform.Scale is not null:
            value = transform.Scale.X;
            return true;
        case 7 when transform.Scale is not null:
            value = transform.Scale.Y;
            return true;
        default:
            return false;
    }
}

static void AddTransformCandidateDefaultKeyCount(
    IDictionary<string, int> counts,
    TrackInfo track,
    double? value)
{
    if (track.TrackType is not (2 or 3 or 4 or 8))
    {
        return;
    }

    var state = value is null
        ? "missing"
        : Math.Abs(value.Value - GetTransformCandidateDefaultValue(track.TrackType.Value)) <= 0.000001
            ? "default"
            : "nonDefault";
    AddCount(counts, $"{FormatTrackTypeKey(track)}|{state}");
}

static double GetTransformCandidateDefaultValue(int trackType)
{
    return trackType == 8 ? 1.0 : 0.0;
}

static void CountUnitRangeKeys(TrackInfo track, ref int inRange, ref int outOfRange, ref int missing)
{
    foreach (var key in track.Keyframes)
    {
        if (key.ScalarValue is not double value)
        {
            missing++;
            continue;
        }

        if (value >= 0 && value <= 1)
        {
            inRange++;
        }
        else
        {
            outOfRange++;
        }
    }
}

static IEnumerable<double> GetScalarValues(TrackInfo track)
{
    return track.Keyframes
        .Select(static key => key.ScalarValue)
        .Where(static value => value is not null)
        .Select(static value => value!.Value);
}

static bool IsColorTrackType(int? trackType)
{
    return trackType is 21 or 22 or 23 or 25 or 26 or 27 or 28;
}

static bool TryGetInitialColorChannel(Transform2DInfo? transform, int? trackType, out double value)
{
    value = 0;
    if (transform is null)
    {
        return false;
    }

    value = trackType switch
    {
        21 when transform.MaterialColor is not null => transform.MaterialColor.R / 255.0,
        22 when transform.MaterialColor is not null => transform.MaterialColor.G / 255.0,
        23 when transform.MaterialColor is not null => transform.MaterialColor.B / 255.0,
        25 when transform.IlluminationColor is not null => transform.IlluminationColor.R / 255.0,
        26 when transform.IlluminationColor is not null => transform.IlluminationColor.G / 255.0,
        27 when transform.IlluminationColor is not null => transform.IlluminationColor.B / 255.0,
        28 when transform.IlluminationColor is not null => transform.IlluminationColor.A / 255.0,
        _ => double.NaN,
    };

    return !double.IsNaN(value);
}

static NodeFlagBitSurvey BuildNodeFlagBitSurvey(SbSceneFile file)
{
    var displayFalseNodeCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var cimgTargetNodeCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var animatedNodeCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var dataNodeCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var categoryRecordNodeCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var categoryNonZeroNodeCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var exactFlagCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var groupCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var imageCastFlagBitCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var trackTypeCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var pairCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);

    var imageCastsByNode = file.Surfboard.Resources.ImageCasts
        .Where(imageCast => imageCast.CastIndex >= 0 && imageCast.CastIndex < file.Surfboard.Nodes.Count)
        .GroupBy(static imageCast => imageCast.CastIndex)
        .ToDictionary(static group => group.Key, static group => group.ToArray());
    var tracksByNode = new Dictionary<int, List<TrackInfo>>();
    foreach (var animation in file.Surfboard.Animations)
    {
        foreach (var motion in animation.Motions)
        {
            if (motion.TargetIndex is not int nodeIndex || nodeIndex < 0 || nodeIndex >= file.Surfboard.Nodes.Count)
            {
                continue;
            }

            if (!tracksByNode.TryGetValue(nodeIndex, out var tracks))
            {
                tracks = [];
                tracksByNode.Add(nodeIndex, tracks);
            }

            tracks.AddRange(motion.Tracks);
        }
    }

    var animatedNodeIndexes = tracksByNode.Keys.ToHashSet();

    foreach (var node in file.Surfboard.Nodes)
    {
        var bits = node.FlagBits.Distinct().Order().ToArray();
        var hasCimg = imageCastsByNode.TryGetValue(node.Index, out var imageCasts);
        var hasTracks = tracksByNode.TryGetValue(node.Index, out var tracks);

        foreach (var bit in bits)
        {
            var bitKey = bit.ToString(CultureInfo.InvariantCulture);
            AddCount(exactFlagCounts, $"{bitKey}|{FormatNullableHex(node.Flags)}");
            AddCount(groupCounts, $"{bitKey}|{NormalizeSurveyGroup(node.Group)}");

            if (node.Transform2D?.Display == false)
            {
                AddCount(displayFalseNodeCounts, bitKey);
            }

            if (hasCimg)
            {
                AddCount(cimgTargetNodeCounts, bitKey);
                foreach (var imageCast in imageCasts!)
                {
                    foreach (var imageCastBit in imageCast.ImageCastFlagBits.Distinct().Order())
                    {
                        AddCount(imageCastFlagBitCounts, $"{bitKey}|{imageCastBit}");
                    }
                }
            }

            if (animatedNodeIndexes.Contains(node.Index))
            {
                AddCount(animatedNodeCounts, bitKey);
            }

            if (node.HasData)
            {
                AddCount(dataNodeCounts, bitKey);
            }

            if (node.CategoryId is not null)
            {
                AddCount(categoryRecordNodeCounts, bitKey);
                if (node.CategoryId.Value != 0)
                {
                    AddCount(categoryNonZeroNodeCounts, bitKey);
                }
            }

            if (hasTracks)
            {
                foreach (var track in tracks!)
                {
                    AddCount(trackTypeCounts, $"{bitKey}|{FormatTrackTypeKey(track)}");
                }
            }
        }

        for (var leftIndex = 0; leftIndex < bits.Length; leftIndex++)
        {
            for (var rightIndex = leftIndex + 1; rightIndex < bits.Length; rightIndex++)
            {
                AddCount(pairCounts, $"{bits[leftIndex]}+{bits[rightIndex]}");
            }
        }
    }

    return new NodeFlagBitSurvey(
        displayFalseNodeCounts,
        cimgTargetNodeCounts,
        animatedNodeCounts,
        dataNodeCounts,
        categoryRecordNodeCounts,
        categoryNonZeroNodeCounts,
        exactFlagCounts,
        groupCounts,
        imageCastFlagBitCounts,
        trackTypeCounts,
        pairCounts);
}

static CimgFlagBitSurvey BuildCimgFlagBitSurvey(SbSceneFile file)
{
    var displayFalseCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var multiReferenceCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var secondaryReferenceCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var nonZeroReferenceIndexCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var missingNodeCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var nodeFlagCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var groupCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var pairCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);

    foreach (var imageCast in file.Surfboard.Resources.ImageCasts)
    {
        var node = imageCast.CastIndex >= 0 && imageCast.CastIndex < file.Surfboard.Nodes.Count
            ? file.Surfboard.Nodes[imageCast.CastIndex]
            : null;
        var bits = imageCast.ImageCastFlagBits.Distinct().Order().ToArray();
        foreach (var bit in bits)
        {
            var bitKey = bit.ToString(CultureInfo.InvariantCulture);
            if (node is null)
            {
                AddCount(missingNodeCounts, bitKey);
            }
            else
            {
                AddCount(nodeFlagCounts, $"{bitKey}|{FormatNullableHex(node.Flags)}");
                AddCount(groupCounts, $"{bitKey}|{NormalizeSurveyGroup(node.Group)}");
                if (node.Transform2D?.Display == false)
                {
                    AddCount(displayFalseCounts, bitKey);
                }
            }

            if (imageCast.CropReferences.Count > 1)
            {
                AddCount(multiReferenceCounts, bitKey);
            }

            if (imageCast.SecondaryCropReferences.Count > 0)
            {
                AddCount(secondaryReferenceCounts, bitKey);
            }

            if (imageCast.PrimaryCropReferenceIndex is > 0 || imageCast.SecondaryCropReferenceIndex is > 0)
            {
                AddCount(nonZeroReferenceIndexCounts, bitKey);
            }
        }

        for (var leftIndex = 0; leftIndex < bits.Length; leftIndex++)
        {
            for (var rightIndex = leftIndex + 1; rightIndex < bits.Length; rightIndex++)
            {
                AddCount(pairCounts, $"{bits[leftIndex]}+{bits[rightIndex]}");
            }
        }
    }

    return new CimgFlagBitSurvey(
        displayFalseCounts,
        multiReferenceCounts,
        secondaryReferenceCounts,
        nonZeroReferenceIndexCounts,
        missingNodeCounts,
        nodeFlagCounts,
        groupCounts,
        pairCounts);
}

static TrackFlagExtraSurvey BuildTrackFlagExtraSurvey(SbSceneFile file)
{
    var baseCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var trackTypeCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var keyValueTypeCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var nodeFlagCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var nodeFlagBitCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var groupCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var cimgTargetCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var initialDisplayCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var cimgFlagCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var cimgFlagBitCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var cimgReferenceCountCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var animationCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);

    var imageCastsByNode = file.Surfboard.Resources.ImageCasts
        .Where(imageCast => imageCast.CastIndex >= 0 && imageCast.CastIndex < file.Surfboard.Nodes.Count)
        .GroupBy(static imageCast => imageCast.CastIndex)
        .ToDictionary(static group => group.Key, static group => group.ToArray());

    foreach (var animation in file.Surfboard.Animations)
    {
        foreach (var motion in animation.Motions)
        {
            var node = motion.TargetIndex is int nodeIndex && nodeIndex >= 0 && nodeIndex < file.Surfboard.Nodes.Count
                ? file.Surfboard.Nodes[nodeIndex]
                : null;
            var imageCasts = node is null || !imageCastsByNode.TryGetValue(node.Index, out var nodeImageCasts)
                ? Array.Empty<SbSceneImageCast>()
                : nodeImageCasts;

            foreach (var track in motion.Tracks)
            {
                var extraKey = FormatNullableHex(track.Flags is null ? null : track.Flags.Value & ~0xFF);
                AddCount(baseCounts, $"{extraKey}|{FormatNullableHex(track.Flags is null ? null : track.Flags.Value & 0xFF)}");
                AddCount(animationCounts, $"{extraKey}|{NormalizeSurveyName(animation.Name)}");
                AddCount(trackTypeCounts, $"{extraKey}|{FormatTrackTypeKey(track)}");

                foreach (var keyframe in track.Keyframes)
                {
                    AddCount(keyValueTypeCounts, $"{extraKey}|{FormatKeyValueTypeKey(keyframe)}");
                }

                if (node is null)
                {
                    AddCount(nodeFlagCounts, $"{extraKey}|missingTarget");
                    AddCount(groupCounts, $"{extraKey}|missingTarget");
                    AddCount(cimgTargetCounts, $"{extraKey}|missingTarget");
                    AddCount(initialDisplayCounts, $"{extraKey}|missingTarget");
                    continue;
                }

                AddCount(nodeFlagCounts, $"{extraKey}|{FormatNullableHex(node.Flags)}");
                AddCount(groupCounts, $"{extraKey}|{NormalizeSurveyGroup(node.Group)}");
                AddCount(cimgTargetCounts, $"{extraKey}|{(imageCasts.Length > 0 ? "true" : "false")}");
                AddCount(initialDisplayCounts, $"{extraKey}|{FormatInitialDisplayState(node)}");

                foreach (var nodeFlagBit in node.FlagBits.Distinct().Order())
                {
                    AddCount(nodeFlagBitCounts, $"{extraKey}|{nodeFlagBit}");
                }

                foreach (var imageCast in imageCasts)
                {
                    AddCount(cimgFlagCounts, $"{extraKey}|{FormatNullableHex(imageCast.ImageCastFlags)}");
                    AddCount(cimgReferenceCountCounts, $"{extraKey}|{imageCast.CropReferences.Count}");
                    foreach (var imageCastFlagBit in imageCast.ImageCastFlagBits.Distinct().Order())
                    {
                        AddCount(cimgFlagBitCounts, $"{extraKey}|{imageCastFlagBit}");
                    }
                }
            }
        }
    }

    return new TrackFlagExtraSurvey(
        baseCounts,
        trackTypeCounts,
        keyValueTypeCounts,
        nodeFlagCounts,
        nodeFlagBitCounts,
        groupCounts,
        cimgTargetCounts,
        initialDisplayCounts,
        cimgFlagCounts,
        cimgFlagBitCounts,
        cimgReferenceCountCounts,
        animationCounts);
}

static KeyInterpolationTangentSurvey BuildKeyInterpolationTangentSurvey(SbSceneFile file)
{
    var interpolationCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var interpolationTrackTypeCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var interpolationKeyValueTypeCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var tangentPresentInterpolationCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var tangentPresentTrackTypeCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var tangentNonZeroInterpolationCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var tangentNonZeroTrackTypeCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var tangentMismatchInterpolationCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var tangentMismatchTrackTypeCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var tangentMismatchAnimationCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var tangentMismatchNodeFlagCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var tangentMismatchGroupCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var tangentMismatchTrackExtraCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var tangentMismatchTangentPairCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var tangentNonZeroFramePositionCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var tangentMismatchFramePositionCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var tangentDeltaSignCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);

    foreach (var animation in file.Surfboard.Animations)
    {
        foreach (var motion in animation.Motions)
        {
            var node = motion.TargetIndex is int nodeIndex && nodeIndex >= 0 && nodeIndex < file.Surfboard.Nodes.Count
                ? file.Surfboard.Nodes[nodeIndex]
                : null;

            foreach (var track in motion.Tracks)
            {
                var trackTypeKey = FormatTrackTypeKey(track);
                var extraKey = FormatNullableHex(track.Flags is null ? null : track.Flags.Value & ~0xFF);
                for (var keyIndex = 0; keyIndex < track.Keyframes.Count; keyIndex++)
                {
                    var keyframe = track.Keyframes[keyIndex];
                    var interpolationKey = FormatInterpolationKey(keyframe);
                    AddCount(interpolationCounts, interpolationKey);
                    AddCount(interpolationTrackTypeCounts, $"{interpolationKey}|{trackTypeKey}");
                    AddCount(interpolationKeyValueTypeCounts, $"{interpolationKey}|{FormatKeyValueTypeKey(keyframe)}");

                    if (keyframe.TangentIn is not null || keyframe.TangentOut is not null)
                    {
                        AddCount(tangentPresentInterpolationCounts, interpolationKey);
                        AddCount(tangentPresentTrackTypeCounts, trackTypeKey);
                        AddTangentDeltaSignCount(tangentDeltaSignCounts, "all", track, track.Keyframes, keyIndex);
                    }

                    if (IsNonZero(keyframe.TangentIn) || IsNonZero(keyframe.TangentOut))
                    {
                        AddCount(tangentNonZeroInterpolationCounts, interpolationKey);
                        AddCount(tangentNonZeroTrackTypeCounts, trackTypeKey);
                        AddCount(tangentNonZeroFramePositionCounts, FormatKeyframeSequencePosition(track.Keyframes, keyIndex));
                        AddTangentDeltaSignCount(tangentDeltaSignCounts, "nonzero", track, track.Keyframes, keyIndex);
                    }

                    if (keyframe.TangentIn is null
                        || keyframe.TangentOut is null
                        || Math.Abs(keyframe.TangentIn.Value - keyframe.TangentOut.Value) <= 0.000001)
                    {
                        continue;
                    }

                    AddCount(tangentMismatchInterpolationCounts, interpolationKey);
                    AddCount(tangentMismatchTrackTypeCounts, trackTypeKey);
                    AddCount(tangentMismatchAnimationCounts, NormalizeSurveyName(animation.Name));
                    AddCount(tangentMismatchNodeFlagCounts, node is null ? "missingTarget" : FormatNullableHex(node.Flags));
                    AddCount(tangentMismatchGroupCounts, node is null ? "missingTarget" : NormalizeSurveyGroup(node.Group));
                    AddCount(tangentMismatchTrackExtraCounts, extraKey);
                    AddCount(
                        tangentMismatchTangentPairCounts,
                        $"{FormatNullableDouble(keyframe.TangentIn)}|{FormatNullableDouble(keyframe.TangentOut)}");
                    AddCount(tangentMismatchFramePositionCounts, FormatKeyframeSequencePosition(track.Keyframes, keyIndex));
                    AddTangentDeltaSignCount(tangentDeltaSignCounts, "mismatch", track, track.Keyframes, keyIndex);
                }
            }
        }
    }

    return new KeyInterpolationTangentSurvey(
        interpolationCounts,
        interpolationTrackTypeCounts,
        interpolationKeyValueTypeCounts,
        tangentPresentInterpolationCounts,
        tangentPresentTrackTypeCounts,
        tangentNonZeroInterpolationCounts,
        tangentNonZeroTrackTypeCounts,
        tangentMismatchInterpolationCounts,
        tangentMismatchTrackTypeCounts,
        tangentMismatchAnimationCounts,
        tangentMismatchNodeFlagCounts,
        tangentMismatchGroupCounts,
        tangentMismatchTrackExtraCounts,
        tangentMismatchTangentPairCounts,
        tangentNonZeroFramePositionCounts,
        tangentMismatchFramePositionCounts,
        tangentDeltaSignCounts);
}

static void AddTangentDeltaSignCount(
    IDictionary<string, int> counts,
    string scope,
    TrackInfo track,
    IReadOnlyList<KeyframeInfo> keyframes,
    int keyIndex)
{
    var keyframe = keyframes[keyIndex];
    AddCount(
        counts,
        string.Join(
            "|",
            scope,
            FormatInterpolationKey(keyframe),
            FormatTrackTypeKey(track),
            $"in={FormatNullableSign(keyframe.TangentIn)}",
            $"prevDelta={FormatAdjacentKeyValueDeltaSign(track, keyframes, keyIndex, -1)}",
            $"out={FormatNullableSign(keyframe.TangentOut)}",
            $"nextDelta={FormatAdjacentKeyValueDeltaSign(track, keyframes, keyIndex, 1)}"));
}

static string FormatKeyframeSequencePosition(IReadOnlyList<KeyframeInfo> keyframes, int keyIndex)
{
    if (keyframes.Count <= 0 || keyIndex < 0 || keyIndex >= keyframes.Count)
    {
        return "missing";
    }

    if (keyframes.Count == 1)
    {
        return "single";
    }

    if (keyIndex == 0)
    {
        return "first";
    }

    return keyIndex == keyframes.Count - 1 ? "last" : "middle";
}

static string FormatAdjacentKeyValueDeltaSign(
    TrackInfo track,
    IReadOnlyList<KeyframeInfo> keyframes,
    int keyIndex,
    int direction)
{
    var otherIndex = keyIndex + direction;
    if (otherIndex < 0 || otherIndex >= keyframes.Count)
    {
        return "edge";
    }

    if (!TryGetComparableKeyValue(track, keyframes[keyIndex], out var current)
        || !TryGetComparableKeyValue(track, keyframes[otherIndex], out var other))
    {
        return "missing";
    }

    var delta = direction < 0 ? current - other : other - current;
    return FormatDoubleSign(delta);
}

static bool TryGetComparableKeyValue(TrackInfo track, KeyframeInfo keyframe, out double value)
{
    if (track.TrackType is 3 or 4 or 5 && keyframe.PackedAngleDegreesCandidate is double degrees)
    {
        value = degrees;
        return true;
    }

    if (keyframe.ScalarValue is double scalar)
    {
        value = scalar;
        return true;
    }

    if (keyframe.BoolValue is bool boolValue)
    {
        value = boolValue ? 1 : 0;
        return true;
    }

    value = 0;
    return false;
}

static string FormatNullableSign(double? value)
{
    return value is null ? "missing" : FormatDoubleSign(value.Value);
}

static string FormatDoubleSign(double value)
{
    if (Math.Abs(value) <= 0.000001)
    {
        return "zero";
    }

    return value > 0 ? "positive" : "negative";
}

static string FormatNullableHex(int? value)
{
    return value is null ? "?" : $"0x{value.Value:X}";
}

static string FormatSurveyHex(uint value)
{
    return $"0x{value:X}";
}

static string FormatInterpolationKey(KeyframeInfo keyframe)
{
    return $"{FormatNullableInt(keyframe.Interpolation)} {keyframe.InterpolationName ?? "?"}";
}

static AnimationMotionStructureSurvey BuildAnimationMotionStructureSurvey(SbSceneFile file, IReadOnlyList<VtbfBlock> blocks)
{
    var animationFieldSequenceCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var animationFieldSetCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var animationParamLowMotionDeltaCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var animationField50MotionDeltaCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var animationField50MaxMotionTrackDeltaCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var animationField50MotionOrMaxTrackRelationCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var animationParamLowField50DeltaCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var animationField5FCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var animationField5FMotionPresenceCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var animationField5FAnimationNameCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var animationField5FParamLowMotionDeltaCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var animationField5FField50MotionDeltaCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var animationField5FField50RelationCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var animationField5FEndFrameRelationCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var animationEndFrameRelationCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var animationEndFrameDeltaToTrackLastCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var animationEndFrameDeltaToKeyMaxCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var motionFieldSequenceCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var motionFieldSetCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var motionParamLowTrackDeltaCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var motionField52TrackDeltaCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var motionParamLowField52DeltaCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var motionTargetIndexRangeCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var animationBlocksByOffset = blocks
        .Where(static block => block.Tag == "ANIM")
        .ToDictionary(static block => block.Offset);
    var motionBlocksByOffset = blocks
        .Where(static block => block.Tag is "MOT " or "MOT")
        .ToDictionary(static block => block.Offset);

    foreach (var animation in file.Surfboard.Animations)
    {
        if (animationBlocksByOffset.TryGetValue(animation.Offset, out var block))
        {
            AddCount(animationFieldSequenceCounts, FormatVtbfFieldSequence(block.Fields));
            AddCount(animationFieldSetCounts, FormatVtbfFieldSet(block.Fields));
            AddCount(animationParamLowMotionDeltaCounts, FormatDelta(block.ParamLow, animation.Motions.Count));
            var declaredMotionCount = GetFirstInt32(block.Fields, 0x50);
            var maxMotionTrackCount = animation.Motions.Count == 0
                ? (int?)null
                : animation.Motions.Max(static motion => motion.Tracks.Count);
            AddCount(animationField50MotionDeltaCounts, FormatDelta(declaredMotionCount, animation.Motions.Count));
            AddCount(
                animationField50MaxMotionTrackDeltaCounts,
                FormatAnimationField50MaxMotionTrackDelta(declaredMotionCount, maxMotionTrackCount));
            AddCount(
                animationField50MotionOrMaxTrackRelationCounts,
                FormatAnimationField50Relation(declaredMotionCount, animation.Motions.Count, maxMotionTrackCount));
            AddCount(animationParamLowField50DeltaCounts, FormatDelta(block.ParamLow, declaredMotionCount));
            var field5F = GetFirstInt32(block.Fields, 0x5F);
            var field5FKey = FormatNullableInt(field5F);
            AddCount(animationField5FCounts, field5FKey);
            AddCount(animationField5FMotionPresenceCounts, $"{field5FKey}|{(animation.Motions.Count == 0 ? "noMotions" : "hasMotions")}");
            AddCount(animationField5FAnimationNameCounts, $"{field5FKey}|{NormalizeSurveyName(animation.Name)}");
            AddCount(animationField5FParamLowMotionDeltaCounts, $"{field5FKey}|{FormatDelta(block.ParamLow, animation.Motions.Count)}");
            AddCount(animationField5FField50MotionDeltaCounts, $"{field5FKey}|{FormatDelta(declaredMotionCount, animation.Motions.Count)}");
            AddCount(animationField5FField50RelationCounts, $"{field5FKey}|{FormatAnimationField50Relation(declaredMotionCount, animation.Motions.Count, maxMotionTrackCount)}");
            var endFrame = GetFirstInt32(block.Fields, 0x56);
            AddCount(animationField5FEndFrameRelationCounts, $"{field5FKey}|{FormatAnimationEndFrameRelation(animation, endFrame)}");
            AddAnimationEndFrameRelationCounts(
                animation,
                endFrame,
                animationEndFrameRelationCounts,
                animationEndFrameDeltaToTrackLastCounts,
                animationEndFrameDeltaToKeyMaxCounts);
        }

        foreach (var motion in animation.Motions)
        {
            if (!motionBlocksByOffset.TryGetValue(motion.Offset, out var motionBlock))
            {
                continue;
            }

            AddCount(motionFieldSequenceCounts, FormatVtbfFieldSequence(motionBlock.Fields));
            AddCount(motionFieldSetCounts, FormatVtbfFieldSet(motionBlock.Fields));
            AddCount(motionParamLowTrackDeltaCounts, FormatDelta(motionBlock.ParamLow, motion.Tracks.Count));
            var declaredTrackCount = GetFirstInt32(motionBlock.Fields, 0x52);
            AddCount(motionField52TrackDeltaCounts, FormatDelta(declaredTrackCount, motion.Tracks.Count));
            AddCount(motionParamLowField52DeltaCounts, FormatDelta(motionBlock.ParamLow, declaredTrackCount));
            AddCount(motionTargetIndexRangeCounts, FormatTargetIndexRange(motion.TargetIndex, file.Surfboard.Nodes.Count));
        }
    }

    return new AnimationMotionStructureSurvey(
        animationFieldSequenceCounts,
        animationFieldSetCounts,
        animationParamLowMotionDeltaCounts,
        animationField50MotionDeltaCounts,
        animationField50MaxMotionTrackDeltaCounts,
        animationField50MotionOrMaxTrackRelationCounts,
        animationParamLowField50DeltaCounts,
        animationField5FCounts,
        animationField5FMotionPresenceCounts,
        animationField5FAnimationNameCounts,
        animationField5FParamLowMotionDeltaCounts,
        animationField5FField50MotionDeltaCounts,
        animationField5FField50RelationCounts,
        animationField5FEndFrameRelationCounts,
        animationEndFrameRelationCounts,
        animationEndFrameDeltaToTrackLastCounts,
        animationEndFrameDeltaToKeyMaxCounts,
        motionFieldSequenceCounts,
        motionFieldSetCounts,
        motionParamLowTrackDeltaCounts,
        motionField52TrackDeltaCounts,
        motionParamLowField52DeltaCounts,
        motionTargetIndexRangeCounts);
}

static void AddAnimationEndFrameRelationCounts(
    AnimationInfo animation,
    int? endFrame,
    IDictionary<string, int> relationCounts,
    IDictionary<string, int> deltaToTrackLastCounts,
    IDictionary<string, int> deltaToKeyMaxCounts)
{
    var trackLastFrames = animation.Motions
        .SelectMany(static motion => motion.Tracks)
        .Select(static track => track.LastFrame)
        .Where(static frame => frame is not null)
        .Select(static frame => frame!.Value)
        .ToArray();
    var keyFrames = animation.Motions
        .SelectMany(static motion => motion.Tracks)
        .SelectMany(static track => track.Keyframes)
        .Select(static key => key.KeyFrame)
        .Where(static frame => frame is not null)
        .Select(static frame => frame!.Value)
        .ToArray();

    if (endFrame is null)
    {
        AddCount(relationCounts, "missingAnimationEndFrame");
        AddCount(deltaToTrackLastCounts, "missingAnimationEndFrame");
        AddCount(deltaToKeyMaxCounts, "missingAnimationEndFrame");
        return;
    }

    if (trackLastFrames.Length == 0)
    {
        AddCount(relationCounts, "noTrackLastFrames");
        AddCount(deltaToTrackLastCounts, "noTrackLastFrames");
    }
    else
    {
        var maxTrackLast = trackLastFrames.Max();
        var deltaToTrackLast = endFrame.Value - maxTrackLast;
        AddCount(deltaToTrackLastCounts, FormatNullableInt(deltaToTrackLast));
        AddCount(relationCounts, deltaToTrackLast switch
        {
            0 => "endEqualsMaxTrackLast",
            > 0 => "endContainsMaxTrackLast",
            _ => "endBeforeMaxTrackLast",
        });
    }

    if (keyFrames.Length == 0)
    {
        AddCount(deltaToKeyMaxCounts, "noKeyFrames");
        return;
    }

    AddCount(deltaToKeyMaxCounts, FormatNullableInt(endFrame.Value - keyFrames.Max()));
}

static string FormatAnimationEndFrameRelation(AnimationInfo animation, int? endFrame)
{
    if (endFrame is null)
    {
        return "missingAnimationEndFrame";
    }

    var trackLastFrames = animation.Motions
        .SelectMany(static motion => motion.Tracks)
        .Select(static track => track.LastFrame)
        .Where(static frame => frame is not null)
        .Select(static frame => frame!.Value)
        .ToArray();

    if (trackLastFrames.Length == 0)
    {
        return "noTrackLastFrames";
    }

    return (endFrame.Value - trackLastFrames.Max()) switch
    {
        0 => "endEqualsMaxTrackLast",
        > 0 => "endContainsMaxTrackLast",
        _ => "endBeforeMaxTrackLast",
    };
}

static TrackKeyStructureSurvey BuildTrackKeyStructureSurvey(SbSceneFile file)
{
    var storageMatrixCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var trackFieldSequenceCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var keyFieldSequenceCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var frameRangeRelationCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var keyFrameOrderCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var keyFrameDuplicateCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var firstFrameDeltaCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var lastFrameDeltaCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);

    foreach (var animation in file.Surfboard.Animations)
    {
        foreach (var motion in animation.Motions)
        {
            foreach (var track in motion.Tracks)
            {
                AddCount(trackFieldSequenceCounts, FormatFieldSequence(track.NumericFields));
                AddCount(keyFrameOrderCounts, FormatKeyFrameOrder(track));
                AddCount(keyFrameDuplicateCounts, FormatKeyFrameDuplicateState(track));
                AddTrackFrameRangeCounts(track, frameRangeRelationCounts, firstFrameDeltaCounts, lastFrameDeltaCounts);

                foreach (var keyframe in track.Keyframes)
                {
                    AddCount(keyFieldSequenceCounts, FormatFieldSequence(keyframe.Fields));
                    AddCount(storageMatrixCounts, FormatTrackKeyStorageMatrixKey(track, keyframe));
                }
            }
        }
    }

    return new TrackKeyStructureSurvey(
        storageMatrixCounts,
        trackFieldSequenceCounts,
        keyFieldSequenceCounts,
        frameRangeRelationCounts,
        keyFrameOrderCounts,
        keyFrameDuplicateCounts,
        firstFrameDeltaCounts,
        lastFrameDeltaCounts);
}

static void AddTrackFrameRangeCounts(
    TrackInfo track,
    IDictionary<string, int> relationCounts,
    IDictionary<string, int> firstDeltaCounts,
    IDictionary<string, int> lastDeltaCounts)
{
    var frames = track.Keyframes
        .Select(static key => key.KeyFrame)
        .Where(static frame => frame is not null)
        .Select(static frame => frame!.Value)
        .ToArray();

    if (frames.Length == 0)
    {
        AddCount(relationCounts, "missingKeyFrames");
        AddCount(firstDeltaCounts, "missingKeyFrames");
        AddCount(lastDeltaCounts, "missingKeyFrames");
        return;
    }

    var min = frames.Min();
    var max = frames.Max();
    var firstDelta = track.FirstFrame is int first ? first - min : (int?)null;
    var lastDelta = track.LastFrame is int last ? last - max : (int?)null;
    AddCount(firstDeltaCounts, FormatNullableInt(firstDelta));
    AddCount(lastDeltaCounts, FormatNullableInt(lastDelta));

    var relation = (firstDelta, lastDelta) switch
    {
        (0, 0) => "trackRangeEqualsKeyMinMax",
        (not null, not null) when firstDelta <= 0 && lastDelta >= 0 => "trackRangeContainsKeyFrames",
        (not null, not null) => "trackRangeDoesNotContainKeyFrames",
        _ => "missingTrackRange",
    };
    AddCount(relationCounts, relation);
}

static string FormatKeyFrameOrder(TrackInfo track)
{
    var frames = track.Keyframes.Select(static key => key.KeyFrame).ToArray();
    if (frames.Length == 0)
    {
        return "noKeys";
    }

    if (frames.Any(static frame => frame is null))
    {
        return "missingKeyFrame";
    }

    var ints = frames.Select(static frame => frame!.Value).ToArray();
    return ints.SequenceEqual(ints.OrderBy(static value => value))
        ? "nonDecreasing"
        : "outOfOrder";
}

static string FormatKeyFrameDuplicateState(TrackInfo track)
{
    var frames = track.Keyframes
        .Select(static key => key.KeyFrame)
        .Where(static frame => frame is not null)
        .Select(static frame => frame!.Value)
        .ToArray();
    if (frames.Length == 0)
    {
        return "noComparableFrames";
    }

    return frames.Distinct().Count() == frames.Length
        ? "unique"
        : "duplicate";
}

static string FormatTargetIndexRange(int? targetIndex, int nodeCount)
{
    return targetIndex switch
    {
        null => "missing",
        < 0 => "negative",
        _ when targetIndex.Value < nodeCount => "inRange",
        _ => "outOfRange",
    };
}

static string FormatTrackKeyStorageMatrixKey(TrackInfo track, KeyframeInfo keyframe)
{
    var flags = track.Flags;
    var baseCode = flags is null ? (int?)null : flags.Value & 0xFF;
    var extra = flags is null ? (int?)null : flags.Value & ~0xFF;
    return string.Join("|", new[]
    {
        FormatTrackTypeKey(track),
        FormatNullableHex(baseCode),
        FormatNullableHex(extra),
        FormatKeyValueTypeKey(keyframe),
        FormatInterpolationKey(keyframe),
    });
}

static string FormatDelta(int? left, int? right)
{
    if (left is null || right is null)
    {
        return "?";
    }

    return FormatNullableInt(left.Value - right.Value);
}

static string FormatAnimationField50Relation(int? field50, int motionCount, int? maxMotionTrackCount)
{
    if (field50 is null)
    {
        return "missingField50";
    }

    if (maxMotionTrackCount is null)
    {
        return "noMotions";
    }

    var matchesMotionCount = field50.Value == motionCount;
    var matchesMaxMotionTrackCount = field50.Value == maxMotionTrackCount.Value;
    return (matchesMotionCount, matchesMaxMotionTrackCount) switch
    {
        (true, true) => "equalsMotionCountAndMaxMotionTrackCount",
        (true, false) => "equalsMotionCountOnly",
        (false, true) => "equalsMaxMotionTrackCountOnly",
        _ => "equalsNeither",
    };
}

static string FormatAnimationField50MaxMotionTrackDelta(int? field50, int? maxMotionTrackCount)
{
    if (field50 is null)
    {
        return "missingField50";
    }

    if (maxMotionTrackCount is null)
    {
        return "noMotions";
    }

    return FormatNullableInt(field50.Value - maxMotionTrackCount.Value);
}

static int? GetFirstInt32(IEnumerable<VtbfField> fields, int id)
{
    return fields.FirstOrDefault(field => field.Id == id)?.Int64Values is { Length: > 0 } values
        ? ToNullableInt(values[0])
        : null;
}

static int? GetFirstFieldInt32(VtbfField field)
{
    return field.Int64Values is { Length: > 0 } values ? ToNullableInt(values[0]) : null;
}

static double? GetFirstFieldDouble(VtbfField field)
{
    return field.Float64Values is { Length: > 0 } values ? values[0] : null;
}

static int? ToNullableInt(long value)
{
    return value is >= int.MinValue and <= int.MaxValue ? (int)value : null;
}

static IEnumerable<int> EnumerateSetBits(int? value)
{
    if (value is null)
    {
        yield break;
    }

    var bits = unchecked((uint)value.Value);
    for (var bit = 0; bit < 32; bit++)
    {
        if ((bits & (1u << bit)) != 0)
        {
            yield return bit;
        }
    }
}

static string FormatInitialDisplayState(NodeInfo node)
{
    if (node.Transform2D is null)
    {
        return "missingTransform";
    }

    return node.Transform2D.Display switch
    {
        true => "true",
        false => "false",
        _ => "unknown",
    };
}

static NodeInfo? TryGetNode(IReadOnlyList<NodeInfo> nodes, int? index)
{
    return index is int nodeIndex && nodeIndex >= 0 && nodeIndex < nodes.Count
        ? nodes[nodeIndex]
        : null;
}

static string FormatTargetNodeFlag(int? index, NodeInfo? node)
{
    if (index is null)
    {
        return "missingTarget";
    }

    return node is null ? "outOfRange" : FormatNullableHex(node.Flags);
}

static string FormatTargetNodeGroup(int? index, NodeInfo? node)
{
    if (index is null)
    {
        return "missingTarget";
    }

    return node is null ? "outOfRange" : NormalizeSurveyGroup(node.Group);
}

static string FormatTargetDisplay(int? index, NodeInfo? node)
{
    if (index is null)
    {
        return "missingTarget";
    }

    return node is null ? "outOfRange" : FormatInitialDisplayState(node);
}

static string FormatTargetSetMembership(int? index, NodeInfo? node, ISet<int> targetIndexes)
{
    if (index is null)
    {
        return "missingTarget";
    }

    return node is null ? "outOfRange" : (targetIndexes.Contains(node.Index) ? "true" : "false");
}

static string NormalizeSurveyGroup(string? value)
{
    return string.IsNullOrWhiteSpace(value) ? "(none)" : value;
}

static string NormalizeSurveyName(string? value)
{
    return string.IsNullOrWhiteSpace(value) ? "(none)" : value;
}

static string NormalizeSurveyParameter(string? value)
{
    return string.IsNullOrWhiteSpace(value) ? "(empty)" : value;
}

static string FormatNullableDouble(double? value)
{
    return value is null ? "?" : value.Value.ToString("R", CultureInfo.InvariantCulture);
}

static string FormatSurveyStringKey(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return "(empty)";
    }

    return value
        .Replace("\r", "\\r", StringComparison.Ordinal)
        .Replace("\n", "\\n", StringComparison.Ordinal)
        .Replace("\t", "\\t", StringComparison.Ordinal);
}

static string FormatSurveyRawKey(string? value)
{
    return string.IsNullOrWhiteSpace(value) ? "?" : value;
}

static string FormatSurveyVectorKey(Vector2Value? value)
{
    return value is null
        ? "?"
        : $"({value.X.ToString("0.###", CultureInfo.InvariantCulture)},{value.Y.ToString("0.###", CultureInfo.InvariantCulture)})";
}

static string FormatSurveyIntListKey(IReadOnlyList<int>? values)
{
    return values is { Count: > 0 }
        ? $"[{string.Join(", ", values)}]"
        : "?";
}

static string FormatColor(ColorArgbValue? color)
{
    return color?.Hex ?? "?";
}

static IEnumerable<VtbfField> EnumerateSurveyFields(SbSceneFile file, string tag, int fieldId)
{
    return FlattenSurveyBlocks(file.Vtbf.Blocks)
        .Where(block => block.Tag == tag)
        .SelectMany(static block => block.Fields)
        .Where(field => field.Id == fieldId);
}

static void AddStringRawFieldCounts(
    byte[] raw,
    IDictionary<string, int> rawLengthCounts,
    IDictionary<string, int> contentLengthCounts,
    IDictionary<string, int> utf8StatusCounts,
    IDictionary<string, int> shiftJisByteShapeCounts,
    IDictionary<string, int>? shiftJisDecodeStatusCounts,
    IDictionary<string, int>? shiftJisStringCounts,
    IDictionary<string, int> rawPreviewCounts)
{
    var content = TrimAtNul(raw);
    AddCount(rawLengthCounts, raw.Length.ToString(CultureInfo.InvariantCulture));
    AddCount(contentLengthCounts, content.Length.ToString(CultureInfo.InvariantCulture));
    AddCount(utf8StatusCounts, IsStrictUtf8(content) ? "validUtf8" : "invalidUtf8");
    AddCount(shiftJisByteShapeCounts, ClassifyShiftJisByteShape(content));
    if (shiftJisDecodeStatusCounts is not null || shiftJisStringCounts is not null)
    {
        var shiftJisText = DecodeStrictShiftJis(content);
        if (shiftJisDecodeStatusCounts is not null)
        {
            AddCount(shiftJisDecodeStatusCounts, shiftJisText is null ? "invalidShiftJis" : "validShiftJis");
        }

        if (shiftJisStringCounts is not null)
        {
            AddCount(shiftJisStringCounts, shiftJisText is null ? "(invalidShiftJis)" : FormatSurveyStringKey(shiftJisText));
        }
    }

    AddCount(rawPreviewCounts, FormatRawHexPreview(raw));
}

static byte[] TrimAtNul(byte[] raw)
{
    var terminator = Array.IndexOf(raw, (byte)0);
    return terminator >= 0 ? raw[..terminator] : raw;
}

static bool IsStrictUtf8(byte[] raw)
{
    try
    {
        _ = new UTF8Encoding(false, true).GetString(raw);
        return true;
    }
    catch (DecoderFallbackException)
    {
        return false;
    }
}

static string? DecodeStrictShiftJis(byte[] raw)
{
    try
    {
        return Encoding.GetEncoding(932, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback).GetString(raw);
    }
    catch (DecoderFallbackException)
    {
        return null;
    }
}

static string ClassifyShiftJisByteShape(byte[] raw)
{
    if (raw.Length == 0)
    {
        return "empty";
    }

    var hasNonAscii = false;
    for (var i = 0; i < raw.Length; i++)
    {
        var value = raw[i];
        if (IsAsciiTextByte(value))
        {
            continue;
        }

        hasNonAscii = true;
        if (value is >= 0xA1 and <= 0xDF)
        {
            continue;
        }

        if (IsShiftJisLeadByte(value)
            && i + 1 < raw.Length
            && IsShiftJisTrailByte(raw[i + 1]))
        {
            i++;
            continue;
        }

        return "invalidShiftJisByteShape";
    }

    return hasNonAscii ? "validShiftJisByteShapeWithNonAscii" : "asciiOnly";
}

static bool IsAsciiTextByte(byte value)
{
    return value is 0x09 or 0x0A or 0x0D || value is >= 0x20 and <= 0x7E;
}

static bool IsShiftJisLeadByte(byte value)
{
    return value is >= 0x81 and <= 0x9F || value is >= 0xE0 and <= 0xFC;
}

static bool IsShiftJisTrailByte(byte value)
{
    return value is >= 0x40 and <= 0x7E || value is >= 0x80 and <= 0xFC;
}

static string FormatRawHexPreview(byte[] raw)
{
    const int maxPreviewBytes = 32;
    if (raw.Length <= maxPreviewBytes)
    {
        return Convert.ToHexString(raw);
    }

    return $"{Convert.ToHexString(raw.AsSpan(0, maxPreviewBytes))}...(len={raw.Length})";
}

static ImageVariantSurvey BuildImageVariantSurvey(SbSceneFile file)
{
    var imageCastsByNode = file.Surfboard.Resources.ImageCasts
        .Where(static imageCast => imageCast.CastIndex >= 0)
        .GroupBy(static imageCast => imageCast.CastIndex)
        .ToDictionary(static group => group.Key, static group => group.ToArray());
    var referenceCountCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var valueCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var groupTrackCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var groupKeyCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var groupTracksWithCimgCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var groupTracksMissingCimgCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var groupTrackRangeMatchCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var groupTrackRangeMismatchCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var groupKeysInRangeCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var groupKeysOutOfRangeCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var groupKeysMissingCimgCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var groupKeysNonIntegerCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var groupKeysMissingValueCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var groupReferenceCountCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var groupValueCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var groupCimg45FirstKeyRelationCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var groupCimg45FirstKeyDeltaCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var groupCimg45FirstKeyPairCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var trackCount = 0;
    var keyCount = 0;
    var tracksWithCimg = 0;
    var tracksMissingCimg = 0;
    var trackRangeMatches = 0;
    var trackRangeMismatches = 0;
    var keysInRange = 0;
    var keysOutOfRange = 0;
    var keysMissingCimg = 0;
    var keysNonInteger = 0;
    var keysMissingValue = 0;

    foreach (var animation in file.Surfboard.Animations)
    {
        foreach (var motion in animation.Motions)
        {
            foreach (var track in motion.Tracks)
            {
                if (track.TrackType == 18)
                {
                    trackCount++;
                    var hasCimg = imageCastsByNode.TryGetValue(motion.TargetIndex ?? -1, out var imageCasts);
                    var referenceCount = hasCimg
                        ? imageCasts!.Sum(static imageCast => imageCast.CropReferences.Count)
                        : (int?)null;
                    if (referenceCount is int count)
                    {
                        tracksWithCimg++;
                        AddCount(referenceCountCounts, count.ToString(CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        tracksMissingCimg++;
                    }

                    var trackHasMismatch = false;
                    var trackHasCheckableKey = false;
                    foreach (var key in track.Keyframes)
                    {
                        keyCount++;
                        if (key.ScalarValue is not double scalar)
                        {
                            keysMissingValue++;
                            trackHasMismatch = true;
                            continue;
                        }

                        if (Math.Abs(scalar - Math.Round(scalar)) > 0.000001)
                        {
                            keysNonInteger++;
                            trackHasMismatch = true;
                            continue;
                        }

                        var intValue = (int)Math.Round(scalar);
                        AddCount(valueCounts, intValue.ToString(CultureInfo.InvariantCulture));
                        if (referenceCount is not int refCount)
                        {
                            keysMissingCimg++;
                            trackHasMismatch = true;
                            continue;
                        }

                        trackHasCheckableKey = true;
                        if (intValue >= 0 && intValue < refCount)
                        {
                            keysInRange++;
                        }
                        else
                        {
                            keysOutOfRange++;
                            trackHasMismatch = true;
                        }
                    }

                    if (referenceCount is not null && trackHasCheckableKey && !trackHasMismatch)
                    {
                        trackRangeMatches++;
                    }
                    else
                    {
                        trackRangeMismatches++;
                    }

                    AddImageVariantGroup(
                        "18 primary",
                        track,
                        imageCasts,
                        hasCimg,
                        imageCast => imageCast.PrimaryCropReferences.Count,
                        imageCast => imageCast.PrimaryCropReferenceIndex);
                }

                if (track.TrackType == 19)
                {
                    var hasCimg = imageCastsByNode.TryGetValue(motion.TargetIndex ?? -1, out var imageCasts);
                    AddImageVariantGroup(
                        "19 secondary",
                        track,
                        imageCasts,
                        hasCimg,
                        imageCast => imageCast.SecondaryCropReferences.Count,
                        imageCast => imageCast.SecondaryCropReferenceIndex);
                }
            }
        }
    }

    return new ImageVariantSurvey(
        trackCount,
        keyCount,
        tracksWithCimg,
        tracksMissingCimg,
        trackRangeMatches,
        trackRangeMismatches,
        keysInRange,
        keysOutOfRange,
        keysMissingCimg,
        keysNonInteger,
        keysMissingValue,
        referenceCountCounts,
        valueCounts,
        groupTrackCounts,
        groupKeyCounts,
        groupTracksWithCimgCounts,
        groupTracksMissingCimgCounts,
        groupTrackRangeMatchCounts,
        groupTrackRangeMismatchCounts,
        groupKeysInRangeCounts,
        groupKeysOutOfRangeCounts,
        groupKeysMissingCimgCounts,
        groupKeysNonIntegerCounts,
        groupKeysMissingValueCounts,
        groupReferenceCountCounts,
        groupValueCounts,
        groupCimg45FirstKeyRelationCounts,
        groupCimg45FirstKeyDeltaCounts,
        groupCimg45FirstKeyPairCounts);

    void AddImageVariantGroup(
        string groupKey,
        TrackInfo track,
        SbSceneImageCast[]? imageCasts,
        bool hasCimg,
        Func<SbSceneImageCast, int> referenceCountSelector,
        Func<SbSceneImageCast, int?> referenceIndexSelector)
    {
        AddCount(groupTrackCounts, groupKey);
        var referenceCount = hasCimg
            ? imageCasts!.Sum(referenceCountSelector)
            : (int?)null;
        if (referenceCount is int count)
        {
            AddCount(groupTracksWithCimgCounts, groupKey);
            AddCount(groupReferenceCountCounts, $"{groupKey}|{count.ToString(CultureInfo.InvariantCulture)}");
        }
        else
        {
            AddCount(groupTracksMissingCimgCounts, groupKey);
        }

        var trackHasMismatch = false;
        var trackHasCheckableKey = false;
        foreach (var key in track.Keyframes)
        {
            AddCount(groupKeyCounts, groupKey);
            if (key.ScalarValue is not double scalar)
            {
                AddCount(groupKeysMissingValueCounts, groupKey);
                trackHasMismatch = true;
                continue;
            }

            if (Math.Abs(scalar - Math.Round(scalar)) > 0.000001)
            {
                AddCount(groupKeysNonIntegerCounts, groupKey);
                trackHasMismatch = true;
                continue;
            }

            var intValue = (int)Math.Round(scalar);
            AddCount(groupValueCounts, $"{groupKey}|{intValue.ToString(CultureInfo.InvariantCulture)}");
            if (referenceCount is not int refCount)
            {
                AddCount(groupKeysMissingCimgCounts, groupKey);
                trackHasMismatch = true;
                continue;
            }

            trackHasCheckableKey = true;
            if (intValue >= 0 && intValue < refCount)
            {
                AddCount(groupKeysInRangeCounts, groupKey);
            }
            else
            {
                AddCount(groupKeysOutOfRangeCounts, groupKey);
                trackHasMismatch = true;
            }
        }

        if (referenceCount is not null && trackHasCheckableKey && !trackHasMismatch)
        {
            AddCount(groupTrackRangeMatchCounts, groupKey);
        }
        else
        {
            AddCount(groupTrackRangeMismatchCounts, groupKey);
        }

        AddCimg45FirstKeyRelation(groupKey, track, imageCasts, hasCimg, referenceIndexSelector);
    }

    void AddCimg45FirstKeyRelation(
        string groupKey,
        TrackInfo track,
        SbSceneImageCast[]? imageCasts,
        bool hasCimg,
        Func<SbSceneImageCast, int?> referenceIndexSelector)
    {
        if (!hasCimg || imageCasts is null || imageCasts.Length == 0)
        {
            AddCount(groupCimg45FirstKeyRelationCounts, $"{groupKey}|missingCimg");
            return;
        }

        if (imageCasts.Length != 1)
        {
            AddCount(groupCimg45FirstKeyRelationCounts, $"{groupKey}|multiCimg");
            return;
        }

        var cimg45Index = referenceIndexSelector(imageCasts[0]);
        if (cimg45Index is not int storedIndex)
        {
            AddCount(groupCimg45FirstKeyRelationCounts, $"{groupKey}|missingCimg45");
            return;
        }

        var firstKey = track.Keyframes
            .OrderBy(static key => key.KeyFrame ?? int.MaxValue)
            .ThenBy(static key => key.Index)
            .FirstOrDefault();
        if (firstKey is null)
        {
            AddCount(groupCimg45FirstKeyRelationCounts, $"{groupKey}|missingKey");
            return;
        }

        if (firstKey.ScalarValue is not double scalar)
        {
            AddCount(groupCimg45FirstKeyRelationCounts, $"{groupKey}|missingKeyValue");
            return;
        }

        if (Math.Abs(scalar - Math.Round(scalar)) > 0.000001)
        {
            AddCount(groupCimg45FirstKeyRelationCounts, $"{groupKey}|nonIntegerKeyValue");
            return;
        }

        var firstKeyIndex = (int)Math.Round(scalar);
        var relation = firstKeyIndex == storedIndex ? "match" : "mismatch";
        AddCount(groupCimg45FirstKeyRelationCounts, $"{groupKey}|{relation}");
        AddCount(groupCimg45FirstKeyDeltaCounts, $"{groupKey}|{(firstKeyIndex - storedIndex).ToString(CultureInfo.InvariantCulture)}");
        AddCount(
            groupCimg45FirstKeyPairCounts,
            $"{groupKey}|cimg45={storedIndex.ToString(CultureInfo.InvariantCulture)}|firstKey={firstKeyIndex.ToString(CultureInfo.InvariantCulture)}");
    }
}

static void AddCount(IDictionary<string, int> counts, string key)
{
    AddCountByAmount(counts, key, 1);
}

static void AddCountByAmount(IDictionary<string, int> counts, string key, int amount)
{
    counts[key] = counts.TryGetValue(key, out var count) ? count + amount : amount;
}

static string FormatPackedAngleTrackTypeKey(TrackInfo track)
{
    return FormatTrackTypeKey(track);
}

static string FormatTrackTypeKey(TrackInfo track)
{
    var id = track.TrackType?.ToString(CultureInfo.InvariantCulture) ?? "?";
    var name = string.IsNullOrWhiteSpace(track.TrackTypeName) ? "?" : track.TrackTypeName;
    return $"{id} {name}";
}

static string FormatKeyValueTypeKey(KeyframeInfo keyframe)
{
    return $"{keyframe.KeyValueTypeHex ?? "?"} {keyframe.KeyValueKind ?? keyframe.KeyValueTypeName ?? "?"}";
}

static string FormatVtbfFieldDirectoryKey(string tag, VtbfField field)
{
    return $"{tag}|{field.IdHex}|{field.TypeHex}|{field.TypeName}";
}

static void AddRecordShapeCounts(
    IReadOnlyList<FieldValueSummary> fields,
    IDictionary<string, int> sequenceCounts,
    IDictionary<string, int> setCounts)
{
    AddCount(sequenceCounts, FormatFieldSequence(fields));
    AddCount(setCounts, FormatFieldSet(fields));
}

static string FormatFieldSequence(IEnumerable<FieldValueSummary> fields)
{
    var parts = fields.Select(static field => $"{field.IdHex}:{field.TypeHex}");
    return string.Join(">", parts);
}

static string FormatFieldSet(IEnumerable<FieldValueSummary> fields)
{
    var parts = fields
        .Select(static field => $"{field.IdHex}:{field.TypeHex}")
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal);
    return string.Join("+", parts);
}

static string FormatVtbfFieldSequence(IEnumerable<VtbfField> fields)
{
    var parts = fields.Select(static field => $"{field.IdHex}:{field.TypeHex}");
    return string.Join(">", parts);
}

static string FormatVtbfFieldSet(IEnumerable<VtbfField> fields)
{
    var parts = fields
        .Select(static field => $"{field.IdHex}:{field.TypeHex}")
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal);
    return string.Join("+", parts);
}

static string FormatSliceRecordShape(SbSceneSliceRecord record)
{
    return string.Join("|", new[]
    {
        FormatNullableHex(record.Field83),
        FormatNullableInt(record.Field40),
        FormatNullableInt(record.Field41),
        FormatNullableInt(record.Field45),
        FormatNullableInt(record.Field39Colors.Count),
        record.Field37Color?.Hex ?? "?",
        record.Field38Color?.Hex ?? "?",
    });
}

static string FormatPackedAngleDegreesCandidate(double? degrees)
{
    return degrees is null ? "?" : degrees.Value.ToString("0.######", CultureInfo.InvariantCulture);
}

static string FormatValueRange(IReadOnlyList<double> values)
{
    if (values.Count == 0)
    {
        return "noValue";
    }

    return $"{FormatSurveyDouble(values.Min())}..{FormatSurveyDouble(values.Max())}";
}

static string FormatSurveyDouble(double value)
{
    return value.ToString("0.######", CultureInfo.InvariantCulture);
}

static SvoSurveyAggregate BuildSvoSurveyAggregate(IReadOnlyList<SvoSurveyRow> rows)
{
    var parsed = rows.Where(static row => row.Error is null).ToArray();
    return new SvoSurveyAggregate
    {
        Total = rows.Count,
        Parsed = parsed.Length,
        Failed = rows.Count - parsed.Length,
        DirectoryReservedEntriesWithNonZero = parsed.Sum(static row => row.DirectoryReservedEntriesWithNonZero),
        DirectoryReservedNonZeroBytes = parsed.Sum(static row => row.DirectoryReservedNonZeroBytes),
        YabxWithUnparsedBytes = parsed.Count(static row => row.YabxUnparsedBytes > 0),
        YabxUnparsedBytes = parsed.Sum(static row => row.YabxUnparsedBytes ?? 0),
        YabxExpectedObjectCountFromDds = parsed.Sum(static row => row.YabxExpectedObjectCountFromDds ?? 0),
        YabxObjectCountDdsSkeletonMatches = parsed.Count(static row => row.YabxObjectCountMatchesDdsSkeleton == true),
        YabxObjectCountDdsSkeletonMismatches = parsed.Count(static row => row.YabxObjectCountMatchesDdsSkeleton == false),
        YabxObjectTypeOrderDdsSkeletonMatches = parsed.Count(static row => row.YabxObjectTypeOrderMatchesDdsSkeleton == true),
        YabxObjectTypeOrderDdsSkeletonMismatches = parsed.Count(static row => row.YabxObjectTypeOrderMatchesDdsSkeleton == false),
        HeaderUnknownWordClassCounts = MergeCounts(parsed.Select(static row => row.HeaderUnknownWordClassCounts)),
        HeaderUnknownNonZeroOffsetCounts = MergeCounts(parsed.Select(static row => row.HeaderUnknownNonZeroOffsetCounts)),
        HeaderUnknownWordValueCounts = MergeCounts(parsed.Select(static row => row.HeaderUnknownWordValueCounts)),
        HeaderUnknownWordOffsetValueCounts = MergeCounts(parsed.Select(static row => row.HeaderUnknownWordOffsetValueCounts)),
        HeaderUnknownWordOffsetClassCounts = MergeCounts(parsed.Select(static row => row.HeaderUnknownWordOffsetClassCounts)),
        HeaderUnknownWordRelationCounts = MergeCounts(parsed.Select(static row => row.HeaderUnknownWordRelationCounts)),
        HeaderUnknownWordOffsetRelationCounts = MergeCounts(parsed.Select(static row => row.HeaderUnknownWordOffsetRelationCounts)),
        HeaderUnknownWordPayloadLocationCounts = MergeCounts(parsed.Select(static row => row.HeaderUnknownWordPayloadLocationCounts)),
        HeaderUnknownWordOffsetPayloadLocationCounts = MergeCounts(parsed.Select(static row => row.HeaderUnknownWordOffsetPayloadLocationCounts)),
        YabxHeaderHashCandidateCounts = CountBy(parsed.Select(static row => row.YabxHeaderHashCandidate ?? "?")),
        YabxDeclaredPayloadLengthMatchesEntryLength = parsed.Count(static row => row.YabxDeclaredPayloadLengthMatchesEntryLength == true),
        YabxDeclaredPayloadLengthMismatchesEntryLength = parsed.Count(static row => row.YabxDeclaredPayloadLengthMatchesEntryLength == false),
        YabxReferenceBaseCounts = CountBy(parsed.Select(static row => row.YabxReferenceBase ?? "?")),
        YabxDescriptorRawCounts = MergeCounts(parsed.Select(static row => row.YabxDescriptorRawCounts)),
        YabxDescriptorFlagsCounts = MergeCounts(parsed.Select(static row => row.YabxDescriptorFlagsCounts)),
        YabxDescriptorValueKindCounts = MergeCounts(parsed.Select(static row => row.YabxDescriptorValueKindCounts)),
        YabxDescriptorReservedCounts = MergeCounts(parsed.Select(static row => row.YabxDescriptorReservedCounts)),
        YabxDescriptorUsageCounts = MergeCounts(parsed.Select(static row => row.YabxDescriptorUsageCounts)),
        YabxDescriptorRawUsageCounts = MergeCounts(parsed.Select(static row => row.YabxDescriptorRawUsageCounts)),
        YabxDescriptorRawObjectKindCounts = MergeCounts(parsed.Select(static row => row.YabxDescriptorRawObjectKindCounts)),
        TextureFormatCounts = MergeCounts(parsed.Select(static row => row.TextureFormatCounts)),
        YabxObjectTypeCounts = MergeCounts(parsed.Select(static row => row.YabxObjectTypeCounts)),
        YabxResourceRecordCount = parsed.Sum(static row => row.YabxResourceRecordCount),
        YabxResourceRecordCountDdsMatches = parsed.Count(static row => row.YabxResourceRecordCountMatchesDds == true),
        YabxResourceRecordCountDdsMismatches = parsed.Count(static row => row.YabxResourceRecordCountMatchesDds == false),
        YabxResourceTextureImageReferenceMatches = parsed.Sum(static row => row.YabxResourceTextureImageReferenceMatches),
        YabxResourceTextureImageReferenceMismatches = parsed.Sum(static row => row.YabxResourceTextureImageReferenceMismatches),
        YabxResourceTextureImageReferenceMissing = parsed.Sum(static row => row.YabxResourceTextureImageReferenceMissing),
        YabxResourceDataSizeMatchesDirectory = parsed.Sum(static row => row.YabxResourceDataSizeMatchesDirectory),
        YabxResourceDataSizeMismatchesDirectory = parsed.Sum(static row => row.YabxResourceDataSizeMismatchesDirectory),
        YabxResourceDataSizeMissing = parsed.Sum(static row => row.YabxResourceDataSizeMissing),
        YabxResourceDimensionsMatchDds = parsed.Sum(static row => row.YabxResourceDimensionsMatchDds),
        YabxResourceDimensionsMismatchDds = parsed.Sum(static row => row.YabxResourceDimensionsMismatchDds),
        YabxResourceDimensionsMissing = parsed.Sum(static row => row.YabxResourceDimensionsMissing),
    };
}

static bool MatchesDdsObjectSkeleton(IReadOnlyList<SvoMetadataObject> objects, int ddsCount)
{
    if (objects.Count != 6 + ddsCount * 2)
    {
        return false;
    }

    var expectedPrefix = new[]
    {
        "stevia::Database",
        "stevia::VertexDeclaration",
        "stevia::VertexElement",
        "stevia::VertexDeclaration",
        "stevia::VertexElement",
        "stevia::VertexElement",
    };

    for (var i = 0; i < expectedPrefix.Length; i++)
    {
        if (!string.Equals(objects[i].TypeName, expectedPrefix[i], StringComparison.Ordinal))
        {
            return false;
        }
    }

    for (var i = 0; i < ddsCount; i++)
    {
        var textureIndex = 6 + i * 2;
        var imageIndex = textureIndex + 1;
        if (!string.Equals(objects[textureIndex].TypeName, "stevia::Texture", StringComparison.Ordinal)
            || !string.Equals(objects[imageIndex].TypeName, "stevia::Image", StringComparison.Ordinal))
        {
            return false;
        }
    }

    return true;
}

static bool ResourceTextureImageReferenceMatches(SvoMetadataResource resource)
{
    return resource.TextureImageReferenceId is not null
        && resource.ImageReferenceId is not null
        && resource.TextureImageReferenceId == resource.ImageReferenceId;
}

static bool ResourceTextureImageReferenceMismatches(SvoMetadataResource resource)
{
    return resource.TextureImageReferenceId is not null
        && resource.ImageReferenceId is not null
        && resource.TextureImageReferenceId != resource.ImageReferenceId;
}

static bool ResourceTextureImageReferenceMissing(SvoMetadataResource resource)
{
    return resource.TextureImageReferenceId is null || resource.ImageReferenceId is null;
}

static bool ResourceDataSizeMatchesDirectory(SvoMetadataResource resource)
{
    return resource.MetadataDataSize is not null
        && resource.DataLength is not null
        && resource.MetadataDataSize == resource.DataLength;
}

static bool ResourceDataSizeMismatchesDirectory(SvoMetadataResource resource)
{
    return resource.MetadataDataSize is not null
        && resource.DataLength is not null
        && resource.MetadataDataSize != resource.DataLength;
}

static bool ResourceDataSizeMissing(SvoMetadataResource resource)
{
    return resource.MetadataDataSize is null || resource.DataLength is null;
}

static bool ResourceDimensionsMatchDds(SvoMetadataResource resource)
{
    return resource.MetadataWidth is not null
        && resource.MetadataHeight is not null
        && resource.Width is not null
        && resource.Height is not null
        && resource.MetadataWidth == resource.Width
        && resource.MetadataHeight == resource.Height;
}

static bool ResourceDimensionsMismatchDds(SvoMetadataResource resource)
{
    return resource.MetadataWidth is not null
        && resource.MetadataHeight is not null
        && resource.Width is not null
        && resource.Height is not null
        && (resource.MetadataWidth != resource.Width || resource.MetadataHeight != resource.Height);
}

static bool ResourceDimensionsMissing(SvoMetadataResource resource)
{
    return resource.MetadataWidth is null
        || resource.MetadataHeight is null
        || resource.Width is null
        || resource.Height is null;
}

static string FormatDescriptorUsageSurveyKey(
    (string OwnerType, string FieldName, string RawDescriptorHex, string DescriptorKind, string ObjectKinds, string Lengths, string Samples) row)
{
    return $"{row.OwnerType}.{row.FieldName}|raw={row.RawDescriptorHex}|label={row.DescriptorKind}|objectKinds={FormatDistributionForSurvey(row.ObjectKinds)}|lengths={FormatDistributionForSurvey(row.Lengths)}";
}

static string FormatDescriptorRawUsageSurveyKey(
    (string OwnerType, string FieldName, string RawDescriptorHex, string DescriptorKind, string ObjectKinds, string Lengths, string Samples) row)
{
    var objectKinds = FormatDistributionKeysForSurvey(row.ObjectKinds);
    var lengths = FormatDistributionKeysForSurvey(row.Lengths);
    return $"raw={row.RawDescriptorHex}|objectKinds={objectKinds}|lengths={lengths}";
}

static string FormatDescriptorRawObjectKindSurveyKey(
    (string OwnerType, string FieldName, string RawDescriptorHex, string DescriptorKind, string ObjectKinds, string Lengths, string Samples) row)
{
    var objectKinds = FormatDistributionKeysForSurvey(row.ObjectKinds);
    return $"raw={row.RawDescriptorHex}|objectKinds={objectKinds}";
}

static string FormatDistributionForSurvey(string distribution)
{
    return string.IsNullOrWhiteSpace(distribution) ? "(none)" : distribution;
}

static string FormatDistributionKeysForSurvey(string distribution)
{
    if (string.IsNullOrWhiteSpace(distribution))
    {
        return "(none)";
    }

    return string.Join(", ", distribution
        .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .Select(static part =>
        {
            var separator = part.LastIndexOf(':');
            return separator < 0 ? part : part[..separator];
        })
        .OrderBy(static part => part, StringComparer.Ordinal));
}

static string ClassifySurveyWarning(string warning)
{
    var tagEnd = warning.IndexOf(' ');
    var tag = tagEnd > 0 ? warning[..tagEnd] : "?";
    var unknownCompact = warning.LastIndexOf("unknown compact field", StringComparison.Ordinal);
    if (unknownCompact >= 0)
    {
        return $"{tag}: {warning[unknownCompact..]}";
    }

    if (warning.Contains("compact trailing byte", StringComparison.Ordinal))
    {
        return $"{tag}: compact trailing bytes";
    }

    return warning;
}

static string NormalizeNcatKind(string? value)
{
    return string.IsNullOrWhiteSpace(value) ? "(none)" : value.Trim().ToLowerInvariant();
}

static string FormatNcatParameterFieldType(FieldValueSummary? field)
{
    return field is null ? "(missing)" : $"{field.TypeHex} {field.TypeName}";
}

static void AddNcatParameterFieldCounts(
    FieldValueSummary? field,
    string kindKey,
    string categoryKey,
    IDictionary<string, int> parameterFieldTypeCounts,
    IDictionary<string, int> kindParameterFieldTypeCounts,
    IDictionary<string, int> categoryParameterFieldTypeCounts,
    IDictionary<string, int> parameterFieldTypePreviewCounts)
{
    var typeKey = FormatNcatParameterFieldType(field);
    AddCount(parameterFieldTypeCounts, typeKey);
    AddCount(kindParameterFieldTypeCounts, $"{kindKey}|{typeKey}");
    AddCount(categoryParameterFieldTypeCounts, $"{categoryKey}|{typeKey}");
    AddCount(parameterFieldTypePreviewCounts, $"{typeKey}|{FormatSurveyStringKey(field?.Preview)}");
}

static CimgIndexSurvey BuildCimgIndexSurvey(IReadOnlyList<SbSceneImageCast> imageCasts)
{
    const int sampleLimit = 16;

    var activeGroups = 0;
    var inRangeGroups = 0;
    var outOfRangeGroups = 0;
    var emptyGroupNonZeroIndices = 0;
    var nonZeroIndices = 0;
    var nonZeroImageCasts = 0;
    var countTupleCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var primaryCountCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var secondaryCountCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var groupIndexCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var groupCountIndexCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var nonZeroGroupCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var secondaryNonZeroSamples = new List<Cimg44SecondaryNonZeroSample>();
    var nonZeroSamples = new List<Cimg45NonZeroSample>();

    foreach (var image in imageCasts)
    {
        var primaryCount = image.PrimaryCropReferenceCount ?? 0;
        var secondaryCount = image.SecondaryCropReferenceCount ?? 0;
        AddCount(countTupleCounts, $"{primaryCount},{secondaryCount}");
        AddCount(primaryCountCounts, primaryCount.ToString(CultureInfo.InvariantCulture));
        AddCount(secondaryCountCounts, secondaryCount.ToString(CultureInfo.InvariantCulture));
        if (secondaryCount > 0 && secondaryNonZeroSamples.Count < sampleLimit)
        {
            secondaryNonZeroSamples.Add(BuildCimg44SecondaryNonZeroSample(image));
        }

        var imageHasNonZero = false;
        foreach (var (groupName, count, index, references) in new (string GroupName, int? Count, int? Index, IReadOnlyList<SbSceneCropReference> References)[]
        {
            ("primary", image.PrimaryCropReferenceCount, image.PrimaryCropReferenceIndex, image.PrimaryCropReferences),
            ("secondary", image.SecondaryCropReferenceCount, image.SecondaryCropReferenceIndex, image.SecondaryCropReferences),
        })
        {
            AddCount(groupIndexCounts, $"{groupName}|{FormatNullableInt(index)}");
            AddCount(groupCountIndexCounts, $"{groupName}|{FormatNullableInt(count)}|{FormatNullableInt(index)}");

            if (index is > 0)
            {
                nonZeroIndices++;
                imageHasNonZero = true;
                AddCount(nonZeroGroupCounts, groupName);
                if (nonZeroSamples.Count < sampleLimit)
                {
                    nonZeroSamples.Add(BuildCimg45NonZeroSample(image, groupName, count, index.Value, references));
                }
            }

            if (count is > 0)
            {
                activeGroups++;
                if (index is >= 0 && index < count)
                {
                    inRangeGroups++;
                }
                else
                {
                    outOfRangeGroups++;
                }
            }
            else if (index is > 0)
            {
                emptyGroupNonZeroIndices++;
            }
        }

        if (imageHasNonZero)
        {
            nonZeroImageCasts++;
        }
    }

    return new CimgIndexSurvey(
        activeGroups,
        inRangeGroups,
        outOfRangeGroups,
        emptyGroupNonZeroIndices,
        nonZeroIndices,
        nonZeroImageCasts,
        countTupleCounts,
        primaryCountCounts,
        secondaryCountCounts,
        secondaryNonZeroSamples,
        groupIndexCounts,
        groupCountIndexCounts,
        nonZeroGroupCounts,
        nonZeroSamples);
}

static Cimg44SecondaryNonZeroSample BuildCimg44SecondaryNonZeroSample(SbSceneImageCast image)
{
    return new Cimg44SecondaryNonZeroSample(
        image.Index,
        $"0x{image.Offset:X}",
        image.NodeName,
        image.PrimaryCropReferenceCount,
        image.SecondaryCropReferenceCount,
        image.PrimaryCropReferences.Count,
        image.SecondaryCropReferences.Count,
        image.PrimaryCropReferenceIndex,
        image.SecondaryCropReferenceIndex,
        image.SecondaryCropReferences.Select(static reference => reference.RawHex).ToArray());
}

static Cimg45NonZeroSample BuildCimg45NonZeroSample(
    SbSceneImageCast image,
    string groupName,
    int? declaredCount,
    int index,
    IReadOnlyList<SbSceneCropReference> references)
{
    var indexedReference = index >= 0 && index < references.Count ? references[index] : null;
    return new Cimg45NonZeroSample(
        image.Index,
        $"0x{image.Offset:X}",
        image.NodeName,
        groupName,
        declaredCount,
        references.Count,
        index,
        indexedReference?.RawHex,
        indexedReference?.TextureListIndex,
        indexedReference?.TextureIndex,
        indexedReference?.CropIndex,
        indexedReference?.AtlasName,
        indexedReference?.CropPath);
}

static TextureAtlasSurvey BuildTextureAtlasSurvey(SbSceneResourceMap resources)
{
    return new TextureAtlasSurvey(
        resources.Atlases.Count,
        CountByHex(resources.Atlases.Select(static atlas => atlas.Field62)),
        CountBy(resources.Atlases
            .SelectMany(static atlas => atlas.Field62Bits)
            .Select(static bit => bit.ToString(CultureInfo.InvariantCulture))),
        CountBy(resources.Atlases.Select(static atlas => $"{FormatNullableHex(atlas.Field62)}|declared={atlas.DeclaredCropCount}|parsed={atlas.Crops.Count}")),
        CountBy(resources.Atlases.Select(static atlas => $"{FormatNullableHex(atlas.Field62)}|{atlas.Width}x{atlas.Height}")));
}

static CropPackedSurvey BuildCropPackedSurvey(SbSceneResourceMap resources)
{
    const int sampleLimit = 16;

    var cropRects = resources.Atlases
        .SelectMany(static atlas => atlas.Crops.Select(crop => (Atlas: atlas, Crop: crop)))
        .ToArray();
    var cropReferences = EnumerateCropReferences(resources).ToArray();
    var cropRefsOutOfRange = cropReferences
        .Where(item => FormatTextureIndexRange(item.Reference, resources) != "in-range"
            || FormatCropIndexRange(item.Reference, resources) != "in-range")
        .ToArray();

    var cropRectInAtlasBounds = 0;
    var cropRectOutOfAtlasBounds = 0;
    var cropRectNonPositiveSize = 0;
    foreach (var (atlas, crop) in cropRects)
    {
        if (crop.Width <= 0 || crop.Height <= 0)
        {
            cropRectNonPositiveSize++;
        }

        if (crop.Left < 0 || crop.Top < 0 || crop.Right > atlas.Width || crop.Bottom > atlas.Height)
        {
            cropRectOutOfAtlasBounds++;
        }
        else
        {
            cropRectInAtlasBounds++;
        }
    }

    var cropRectOutOfAtlasBoundsSamples = cropRects
        .Where(static item => IsCropRectOutOfAtlasBounds(item.Atlas, item.Crop))
        .Take(sampleLimit)
        .Select(static item => BuildCropRectBoundsSample(item.Atlas, item.Crop))
        .ToArray();
    var referenceOutOfRangeSamples = cropRefsOutOfRange
        .Take(sampleLimit)
        .Select(item => BuildCropReferenceRangeSample(item, resources))
        .ToArray();

    return new CropPackedSurvey(
        cropRects.Length,
        resources.Atlases.Count(static atlas => atlas.DeclaredCropCount == atlas.Crops.Count),
        resources.Atlases.Count(static atlas => atlas.DeclaredCropCount != atlas.Crops.Count),
        cropRectInAtlasBounds,
        cropRectOutOfAtlasBounds,
        cropRectNonPositiveSize,
        cropReferences.Length,
        CountByHex(cropReferences.Select(static item => (int?)item.Reference.Kind)),
        CountBy(cropReferences.Select(static item => item.OwnerKind)),
        CountBy(cropReferences.Select(static item => $"{item.OwnerKind}|{FormatNullableHex(item.Reference.Kind)}")),
        CountBy(cropReferences.Select(static item => item.Reference.TextureListIndex.ToString(CultureInfo.InvariantCulture))),
        CountBy(cropReferences.Select(item => FormatTextureIndexRange(item.Reference, resources))),
        CountBy(cropReferences.Select(item => FormatCropIndexRange(item.Reference, resources))),
        CountBy(cropRects
            .Where(static item => IsCropRectOutOfAtlasBounds(item.Atlas, item.Crop))
            .Select(static item => FormatCropRectBoundsReason(item.Atlas, item.Crop))),
        CountBy(cropRefsOutOfRange.Select(static item => item.OwnerKind)),
        cropRectOutOfAtlasBoundsSamples,
        referenceOutOfRangeSamples);
}

static IEnumerable<CropReferenceOwner> EnumerateCropReferences(SbSceneResourceMap resources)
{
    foreach (var imageCast in resources.ImageCasts)
    {
        foreach (var reference in imageCast.CropReferences)
        {
            yield return new CropReferenceOwner("CIMG", imageCast.Index, imageCast.Offset, imageCast.NodeName, reference);
        }
    }

    foreach (var cnumRecord in resources.CnumRecords)
    {
        foreach (var reference in cnumRecord.CropReferences)
        {
            yield return new CropReferenceOwner("CNUM", cnumRecord.Index, cnumRecord.Offset, FormatCnumOwnerName(cnumRecord), reference);
        }
    }

    foreach (var sliceCast in resources.SliceCasts)
    {
        foreach (var reference in sliceCast.CropReferences)
        {
            yield return new CropReferenceOwner("CSLI", sliceCast.Index, sliceCast.Offset, sliceCast.NodeName, reference);
        }
    }
}

static bool IsCropRectOutOfAtlasBounds(SbSceneTextureAtlas atlas, SbSceneCropRect crop)
{
    return crop.Left < 0 || crop.Top < 0 || crop.Right > atlas.Width || crop.Bottom > atlas.Height;
}

static string FormatCropRectBoundsReason(SbSceneTextureAtlas atlas, SbSceneCropRect crop)
{
    var parts = new List<string>(4);
    if (crop.Left < 0)
    {
        parts.Add("left<0");
    }

    if (crop.Top < 0)
    {
        parts.Add("top<0");
    }

    if (crop.Right > atlas.Width)
    {
        parts.Add("right>width");
    }

    if (crop.Bottom > atlas.Height)
    {
        parts.Add("bottom>height");
    }

    return parts.Count == 0 ? "in-bounds" : string.Join("+", parts);
}

static CropRectBoundsSample BuildCropRectBoundsSample(SbSceneTextureAtlas atlas, SbSceneCropRect crop)
{
    return new CropRectBoundsSample(
        atlas.Index,
        atlas.Name,
        atlas.Width,
        atlas.Height,
        crop.Index,
        crop.RawHex,
        crop.Left,
        crop.Top,
        crop.Width,
        crop.Height,
        crop.Right,
        crop.Bottom);
}

static CropReferenceRangeSample BuildCropReferenceRangeSample(CropReferenceOwner item, SbSceneResourceMap resources)
{
    var reference = item.Reference;
    var atlas = reference.TextureIndex >= 0 && reference.TextureIndex < resources.Atlases.Count
        ? resources.Atlases[reference.TextureIndex]
        : null;
    return new CropReferenceRangeSample(
        item.OwnerKind,
        item.OwnerIndex,
        $"0x{item.OwnerOffset:X}",
        item.OwnerName,
        reference.Index,
        reference.RawHex,
        reference.Kind,
        reference.TextureListIndex,
        reference.TextureIndex,
        reference.CropIndex,
        FormatTextureIndexRange(reference, resources),
        FormatCropIndexRange(reference, resources),
        atlas?.Index,
        atlas?.Name,
        atlas?.Crops.Count);
}

static string? FormatCnumOwnerName(SbSceneCnumRecord record)
{
    if (!string.IsNullOrEmpty(record.NodeName) && !string.IsNullOrEmpty(record.FieldA1))
    {
        return $"{record.NodeName}|{record.FieldA1}";
    }

    return record.NodeName ?? record.FieldA1;
}

static string FormatTextureIndexRange(SbSceneCropReference reference, SbSceneResourceMap resources)
{
    return reference.TextureIndex >= 0 && reference.TextureIndex < resources.Atlases.Count
        ? "in-range"
        : "out-of-range";
}

static string FormatCropIndexRange(SbSceneCropReference reference, SbSceneResourceMap resources)
{
    if (reference.TextureIndex < 0 || reference.TextureIndex >= resources.Atlases.Count)
    {
        return "missing-texture";
    }

    var atlas = resources.Atlases[reference.TextureIndex];
    return reference.CropIndex >= 0 && reference.CropIndex < atlas.Crops.Count
        ? "in-range"
        : "out-of-range";
}

static IEnumerable<VtbfBlock> FlattenSurveyBlocks(IEnumerable<VtbfBlock> blocks)
{
    foreach (var block in blocks)
    {
        yield return block;
        foreach (var child in FlattenSurveyBlocks(block.Children))
        {
            yield return child;
        }
    }
}

static IReadOnlyList<int> CountFollowingTags(IReadOnlyList<VtbfBlock> blocks, params string[] countedTags)
{
    var counted = new HashSet<string>(countedTags, StringComparer.Ordinal);
    var counts = new List<int>();
    for (var i = 0; i < blocks.Count; i++)
    {
        if (blocks[i].Tag != "DATA")
        {
            continue;
        }

        var count = 0;
        for (var j = i + 1; j < blocks.Count; j++)
        {
            var tag = blocks[j].Tag;
            if (counted.Contains(tag))
            {
                count++;
                continue;
            }

            if (StopsDataImageCastRun(tag))
            {
                break;
            }
        }

        counts.Add(count);
    }

    return counts;
}

static IReadOnlyList<IReadOnlyDictionary<string, int>> CountFollowingTagRuns(IReadOnlyList<VtbfBlock> blocks)
{
    var runs = new List<IReadOnlyDictionary<string, int>>();
    for (var i = 0; i < blocks.Count; i++)
    {
        if (blocks[i].Tag != "DATA")
        {
            continue;
        }

        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        for (var j = i + 1; j < blocks.Count; j++)
        {
            var tag = blocks[j].Tag;
            if (StopsDataImageCastRun(tag))
            {
                break;
            }

            counts[tag] = counts.TryGetValue(tag, out var count) ? count + 1 : 1;
        }

        runs.Add(counts);
    }

    return runs;
}

static bool StopsDataImageCastRun(string tag)
{
    return tag is "NCAT" or "NODE" or "TRS2" or "TRS3" or "DATA" or "LAYR" or "CAST" or "ANIM" or "MOT " or "TRK " or "KEY " or "CAM " or "SRCK" or "PROJ" or "SCN " or "SCN" or "SRFF";
}

static string GetRelativeSurveyPath(string root, string path)
{
    return Directory.Exists(root)
        ? Path.GetRelativePath(Path.GetFullPath(root), path)
        : Path.GetFileName(path);
}

static SortedDictionary<string, int> CountBy(IEnumerable<string?> values)
{
    var result = new SortedDictionary<string, int>(StringComparer.Ordinal);
    foreach (var value in values)
    {
        var key = string.IsNullOrWhiteSpace(value) ? "?" : value;
        result[key] = result.TryGetValue(key, out var count) ? count + 1 : 1;
    }

    return result;
}

static SortedDictionary<string, int> CountByHex(IEnumerable<int?> values)
{
    return CountBy(values.Select(static value => value is null ? "?" : $"0x{value.Value:X}"));
}

static SortedDictionary<string, int> MergeCounts(IEnumerable<IReadOnlyDictionary<string, int>> dictionaries)
{
    var result = new SortedDictionary<string, int>(StringComparer.Ordinal);
    foreach (var dictionary in dictionaries)
    {
        foreach (var (key, value) in dictionary)
        {
            result[key] = result.TryGetValue(key, out var existing) ? existing + value : value;
        }
    }

    return result;
}

static string FormatNullableInt(int? value)
{
    return value?.ToString() ?? "?";
}

static string FormatNullableFloat(float? value)
{
    return value?.ToString("R", CultureInfo.InvariantCulture) ?? "?";
}

static bool IsNonZero(double? value)
{
    return value is not null && Math.Abs(value.Value) > 0.000001;
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
}

internal sealed class SurveyResult
{
    public required string Input { get; init; }

    public string? Filter { get; init; }

    public required IReadOnlyList<SceneSurveyRow> Scenes { get; init; }

    public required IReadOnlyList<SvoSurveyRow> Svos { get; init; }

    public required SceneSurveyAggregate SceneAggregate { get; init; }

    public required SvoSurveyAggregate SvoAggregate { get; init; }
}

internal sealed class SceneSurveyRow
{
    public required string Path { get; init; }

    public required string RelativePath { get; init; }

    public required long Size { get; init; }

    public string? Error { get; init; }

    public string? RootParamRaw { get; init; }

    public int? RootParamLow { get; init; }

    public int? RootParamHigh { get; init; }

    public int TotalBlocks { get; init; }

    public IReadOnlyDictionary<string, int> VtbfTagCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> VtbfTagParamRawCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> VtbfTagParamLowHighCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> VtbfTagPropertyCountCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> VtbfTagParamHighPropertyCountCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> VtbfTagTrailingByteCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> VtbfKeyParamHighModulo5Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> VtbfFieldDirectoryCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> VtbfFieldDirectoryBlockCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> VtbfFieldCountValueCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> VtbfFieldStrideValueCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> SharedPackedStateOwnerCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> SharedPackedStateOwnerRawCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> SharedPackedStateOwnerBitCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> SharedPackedStateOwnerLowNibbleCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> SharedPackedStateOwnerMaskF0Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> SharedPackedStateOwnerMaskF00Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> SharedPackedStateOwnerUpperMaskCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CatrField03Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CatrField0DCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CatrField0ECounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CatrField0FTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CatrField0FPreviewCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CatrFieldSequenceCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CatrFieldSetCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> ProjectField00Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> ProjectField01Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> ProjectField05Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> ProjectField55Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> ProjectField56Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> ProjectField56TrackLastRelationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> ProjectField56KeyMaxRelationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> ProjectField56DeltaToTrackLastCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> ProjectField56DeltaToKeyMaxCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> ProjectFieldSequenceCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> ProjectFieldSetCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> ScnNameCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> ScnField04RawHexCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> ScnField10Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> ScnField11Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> ScnField40Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> ScnField41Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> ScnField10Field11Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> ScnField40Field41Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> ScnParamLowLayerCountDeltaCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> ScnParamLowField10DeltaCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> ScnField10LayerCountDeltaCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> ScnFieldSequenceCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> ScnFieldSetCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> LayerNameCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> LayerField20Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> LayerField20BitCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> LayerField21Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> LayerField21BitCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> LayerField22Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> LayerField22BitCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> LayerField21SceneNodeCountDeltaCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> LayerParamLowField22DeltaCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> LayerFieldSequenceCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> LayerFieldSetCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CameraNameCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CameraField12VectorCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CameraField13VectorCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CameraField14Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CameraField14BitCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CameraField15Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CameraField16Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CameraFieldSequenceCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CameraFieldSetCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> AnimationFieldSequenceCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> AnimationFieldSetCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> AnimationParamLowMotionDeltaCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> AnimationField50MotionDeltaCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> AnimationField50MaxMotionTrackDeltaCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> AnimationField50MotionOrMaxTrackRelationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> AnimationParamLowField50DeltaCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> AnimationField5FCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> AnimationField5FMotionPresenceCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> AnimationField5FAnimationNameCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> AnimationField5FParamLowMotionDeltaCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> AnimationField5FField50MotionDeltaCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> AnimationField5FField50RelationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> AnimationField5FEndFrameRelationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> AnimationEndFrameRelationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> AnimationEndFrameDeltaToTrackLastCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> AnimationEndFrameDeltaToKeyMaxCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> MotionFieldSequenceCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> MotionFieldSetCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> MotionParamLowTrackDeltaCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> MotionField52TrackDeltaCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> MotionParamLowField52DeltaCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> MotionTargetIndexRangeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> UnknownTypeCodeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public int NodeCount { get; init; }

    public int Transform2DCount { get; init; }

    public int ImageCastCount { get; init; }

    public int CnumCount { get; init; }

    public int CnumCropReferenceCount { get; init; }

    public int CnumField44Matches { get; init; }

    public int CnumField44Mismatches { get; init; }

    public int CnumField44Missing { get; init; }

    public int CnumField51InRange { get; init; }

    public int CnumField51OutOfRange { get; init; }

    public int CnumField51Missing { get; init; }

    public IReadOnlyDictionary<string, int> CnumField44Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CnumZeroMarkerFieldCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CnumFieldA1Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CnumField48Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CnumFieldA0Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CnumFieldA1RawLengthCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CnumFieldA1ContentLengthCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CnumFieldA1Utf8StatusCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CnumFieldA1ShiftJisByteShapeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CnumFieldA1RawPreviewCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CnumFieldA1Field44Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CnumFieldA1CropReferenceCountCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CnumFieldA1ZeroMarkerFieldCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CnumFieldA1NodeFlagCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CnumFieldA1NodeGroupCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CnumFieldA1DisplayCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CnumFieldA1CimgTargetCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CnumFieldA1AnimatedTargetCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CnumFieldSequenceCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CnumFieldSetCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public int CrfdCount { get; init; }

    public int CrfdField51InRange { get; init; }

    public int CrfdField51OutOfRange { get; init; }

    public int CrfdField51Missing { get; init; }

    public IReadOnlyDictionary<string, int> CrfdField90Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CrfdField91Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CrfdField90Field91Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CrfdField90Field91Field92Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CrfdStringFieldRelationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CrfdStringFieldTargetTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CrfdField90Field91RelationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CrfdField90Field91EqualityCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CrfdField90Field91Field92RelationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CrfdField92Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CrfdField93Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CrfdField94Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public int CrfdField94NonZero { get; init; }

    public IReadOnlyDictionary<string, int> CrfdField95Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public int TextCount { get; init; }

    public int TextField7APresent { get; init; }

    public IReadOnlyDictionary<string, int> TextZeroMarkerFieldCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TextField41Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TextField78Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TextField79Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TextField7CCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TextField7AStringCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TextField7ARawLengthCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TextField7AContentLengthCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TextField7AUtf8StatusCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TextField7AShiftJisByteShapeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TextField7AShiftJisDecodeStatusCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TextField7AShiftJisStringCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TextField7ARawPreviewCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TextField7AField41Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TextField7AField78Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TextField7AField79Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TextField7AField7CCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TextField33VectorCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TextField33RawHexCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TextField7BPackedValuesCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TextField7BRawHexCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TextField78Field79Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TextZeroMarkerField7ACounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TextFieldSequenceCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TextFieldSetCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public int SliceCastCount { get; init; }

    public int SliceRecordCount { get; init; }

    public int SliceCropReferenceCount { get; init; }

    public int SliceField44SlicRecordMatches { get; init; }

    public int SliceField44SlicRecordMismatches { get; init; }

    public int SliceField44CropReferenceMatches { get; init; }

    public int SliceField44CropReferenceMismatches { get; init; }

    public int SliceTargetIndexInRange { get; init; }

    public int SliceTargetIndexOutOfRange { get; init; }

    public IReadOnlyDictionary<string, int> SliceField83Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> SliceCastField40Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> SliceCastField41Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> SliceCastField42Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> SliceCastField43Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> SliceCastField80Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> SliceCastField81Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> SliceCastField82Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> SliceCastField84Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> SliceCastField85Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> SliceCastField86Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> SliceCastField87Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> SliceCastTargetNodeFlagCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> SliceCastTargetNodeGroupCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> SliceCastTargetDisplayCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> SliceCastTargetCimgTargetCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> SliceCastFieldSequenceCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> SliceCastFieldSetCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> SliceRecordField40Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> SliceRecordField41Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> SliceRecordField45Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> SliceRecordField37ColorCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> SliceRecordField38ColorCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> SliceRecordField39ColorCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> SliceRecordField39ColorCountCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> SliceRecordField83Field40Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> SliceRecordField83Field41Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> SliceRecordField83Field45Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> SliceRecordFieldSequenceCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> SliceRecordFieldSetCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> SliceRecordShapeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public int DataBlockCount { get; init; }

    public IReadOnlyList<int> DataParamLowValues { get; init; } = [];

    public IReadOnlyList<int> DataFollowingImageCastCounts { get; init; } = [];

    public IReadOnlyList<int> DataFollowingCimgCrfdCounts { get; init; } = [];

    public IReadOnlyList<int> DataFollowingCimgCnumCrfdCounts { get; init; } = [];

    public IReadOnlyList<int> DataFollowingCimgCnumCrfdCsliCounts { get; init; } = [];

    public IReadOnlyList<IReadOnlyDictionary<string, int>> DataFollowingTagCounts { get; init; } = [];

    public int DataFields { get; init; }

    public int DataTrailingBytes { get; init; }

    public bool? DataParamLowMatchesImageCasts { get; init; }

    public bool? DataParamLowMatchesFollowingImageCasts { get; init; }

    public bool? DataParamLowMatchesFollowingCimgCrfd { get; init; }

    public bool? DataParamLowMatchesFollowingCimgCnumCrfd { get; init; }

    public bool? DataParamLowMatchesFollowingCimgCnumCrfdCsli { get; init; }

    public int NcatRecordCount { get; init; }

    public int NcatDetailRecordCount { get; init; }

    public int NcatNonZeroCount { get; init; }

    public bool? NcatMatchesNodes { get; init; }

    public int NcatRecordsWithCategory { get; init; }

    public int NcatRecordsWithoutCategory { get; init; }

    public IReadOnlyDictionary<string, int> NcatKindCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> NcatTypeByteCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> NcatCategoryCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> NcatKindTypeByteCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> NcatKindCategoryCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> NcatTypeByteCategoryCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> NcatKindParameterPresenceCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> NcatParameterStringCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> NcatParameterFieldTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> NcatKindParameterFieldTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> NcatCategoryParameterFieldTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> NcatParameterFieldTypePreviewCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> NcatKindNodeFlagCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> NcatKindNodeFlagBitCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> NcatKindNodeGroupCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> NcatKindDisplayCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> NcatKindCimgTargetCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> NcatKindAnimatedNodeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> NodeFlagCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> NodeFlagBitCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> NodeFlagBitDisplayFalseNodeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> NodeFlagBitCimgTargetNodeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> NodeFlagBitAnimatedNodeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> NodeFlagBitDataNodeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> NodeFlagBitCategoryRecordNodeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> NodeFlagBitCategoryNonZeroNodeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> NodeFlagBitExactFlagCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> NodeFlagBitGroupCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> NodeFlagBitImageCastFlagBitCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> NodeFlagBitTrackTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> NodeFlagBitPairCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public int Cimg44Matches { get; init; }

    public int Cimg44Mismatches { get; init; }

    public int Cimg44Unknown { get; init; }

    public IReadOnlyDictionary<string, int> Cimg44CountTupleCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> Cimg44PrimaryCountCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> Cimg44SecondaryCountCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyList<Cimg44SecondaryNonZeroSample> Cimg44SecondaryNonZeroSamples { get; init; } = [];

    public int Cimg45ActiveGroups { get; init; }

    public int Cimg45InRangeGroups { get; init; }

    public int Cimg45OutOfRangeGroups { get; init; }

    public int Cimg45EmptyGroupNonZero { get; init; }

    public int Cimg45NonZeroIndices { get; init; }

    public int Cimg45NonZeroImageCasts { get; init; }

    public IReadOnlyDictionary<string, int> Cimg45GroupIndexCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> Cimg45GroupCountIndexCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> Cimg45NonZeroGroupCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyList<Cimg45NonZeroSample> Cimg45NonZeroSamples { get; init; } = [];

    public IReadOnlyDictionary<string, int> CimgFlagCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CimgFlagBitCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CimgFlagBitDisplayFalseCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CimgFlagBitMultiReferenceCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CimgFlagBitSecondaryReferenceCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CimgFlagBitNonZeroReferenceIndexCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CimgFlagBitMissingNodeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CimgFlagBitNodeFlagCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CimgFlagBitGroupCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CimgFlagBitPairCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public int TextureAtlasCount { get; init; }

    public IReadOnlyDictionary<string, int> TextureAtlasField62Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TextureAtlasField62BitCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TextureAtlasField62CropCountCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TextureAtlasField62SizeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CropKindCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CrefKindCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public int CropRectCount { get; init; }

    public int CropAtlasDeclaredCountMatches { get; init; }

    public int CropAtlasDeclaredCountMismatches { get; init; }

    public int CropRectInAtlasBounds { get; init; }

    public int CropRectOutOfAtlasBounds { get; init; }

    public int CropRectNonPositiveSize { get; init; }

    public int CropReferenceCount { get; init; }

    public IReadOnlyDictionary<string, int> CropReferenceKindCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CropReferenceOwnerCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CropReferenceOwnerKindCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CropReferenceTextureListIndexCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CropReferenceTextureIndexRangeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CropReferenceCropIndexRangeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CropRectOutOfAtlasBoundsReasonCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> CropReferenceOutOfRangeOwnerCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyList<CropRectBoundsSample> CropRectOutOfAtlasBoundsSamples { get; init; } = [];

    public IReadOnlyList<CropReferenceRangeSample> CropReferenceOutOfRangeSamples { get; init; } = [];

    public int TrackCount { get; init; }

    public int TrackKeyCountMismatches { get; init; }

    public IReadOnlyDictionary<string, int> TrackFlagCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TrackFlagBaseCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TrackFlagExtraCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TrackFlagExtraSceneCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TrackFlagExtraBaseCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TrackFlagExtraAnimationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TrackFlagExtraTrackTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TrackFlagExtraKeyValueTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TrackFlagExtraNodeFlagCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TrackFlagExtraNodeFlagBitCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TrackFlagExtraGroupCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TrackFlagExtraCimgTargetCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TrackFlagExtraInitialDisplayCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TrackFlagExtraCimgFlagCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TrackFlagExtraCimgFlagBitCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TrackFlagExtraCimgReferenceCountCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TrackTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> KeyValueTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public int KeyTangentPresent { get; init; }

    public int KeyTangentNonZero { get; init; }

    public int KeyTangentMismatch { get; init; }

    public IReadOnlyDictionary<string, int> KeyInterpolationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> KeyInterpolationTrackTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> KeyInterpolationKeyValueTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> KeyTangentPresentInterpolationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> KeyTangentPresentTrackTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> KeyTangentNonZeroInterpolationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> KeyTangentNonZeroTrackTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> KeyTangentMismatchInterpolationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> KeyTangentMismatchTrackTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> KeyTangentMismatchAnimationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> KeyTangentMismatchNodeFlagCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> KeyTangentMismatchGroupCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> KeyTangentMismatchTrackExtraCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> KeyTangentMismatchTangentPairCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> KeyTangentNonZeroFramePositionCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> KeyTangentMismatchFramePositionCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> KeyTangentDeltaSignCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TrackKeyStorageMatrixCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TrackFieldSequenceCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> KeyFieldSequenceCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TrackFrameRangeRelationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TrackKeyFrameOrderCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TrackKeyFrameDuplicateCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TrackFirstFrameDeltaCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TrackLastFrameDeltaCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public int TransformTrackCount { get; init; }

    public int TransformTrackKeyCount { get; init; }

    public int TransformTracksWithInitialChannel { get; init; }

    public int TransformTracksMissingInitialChannel { get; init; }

    public int TransformTrackInitialValueMatches { get; init; }

    public int TransformTrackInitialValueMismatches { get; init; }

    public int TransformTrackKeysMissingValue { get; init; }

    public IReadOnlyDictionary<string, int> TransformTrackTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TransformTrackKeyTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TransformTrackStorageCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TransformTrackKeyValueKindCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TransformTrackInitialMatchTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TransformTrackValueRangeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> TransformCandidateDefaultKeyCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public int PackedAngleTrackCount { get; init; }

    public int PackedAngleKeyCount { get; init; }

    public IReadOnlyDictionary<string, int> PackedAngleTrackTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> PackedAngleKeyTrackTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> PackedAngleRawCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> PackedAngleDegreeCandidateCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public int ImageVariantTrackCount { get; init; }

    public int ImageVariantKeyCount { get; init; }

    public int ImageVariantTracksWithCimg { get; init; }

    public int ImageVariantTracksMissingCimg { get; init; }

    public int ImageVariantTrackRangeMatches { get; init; }

    public int ImageVariantTrackRangeMismatches { get; init; }

    public int ImageVariantKeysInRange { get; init; }

    public int ImageVariantKeysOutOfRange { get; init; }

    public int ImageVariantKeysMissingCimg { get; init; }

    public int ImageVariantKeysNonInteger { get; init; }

    public int ImageVariantKeysMissingValue { get; init; }

    public IReadOnlyDictionary<string, int> ImageVariantReferenceCountCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> ImageVariantValueCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> ImageVariantGroupTrackCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> ImageVariantGroupKeyCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> ImageVariantGroupTracksWithCimgCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> ImageVariantGroupTracksMissingCimgCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> ImageVariantGroupTrackRangeMatchCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> ImageVariantGroupTrackRangeMismatchCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> ImageVariantGroupKeysInRangeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> ImageVariantGroupKeysOutOfRangeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> ImageVariantGroupKeysMissingCimgCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> ImageVariantGroupKeysNonIntegerCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> ImageVariantGroupKeysMissingValueCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> ImageVariantGroupReferenceCountCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> ImageVariantGroupValueCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> ImageVariantGroupCimg45FirstKeyRelationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> ImageVariantGroupCimg45FirstKeyDeltaCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> ImageVariantGroupCimg45FirstKeyPairCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public int ColorTrackCount { get; init; }

    public int ColorTrackKeyCount { get; init; }

    public int ColorTracksWithInitialChannel { get; init; }

    public int ColorTracksMissingInitialChannel { get; init; }

    public int ColorTrackInitialValueMatches { get; init; }

    public int ColorTrackInitialValueMismatches { get; init; }

    public int ColorTrackKeysInUnitRange { get; init; }

    public int ColorTrackKeysOutOfUnitRange { get; init; }

    public int ColorTrackKeysMissingValue { get; init; }

    public IReadOnlyDictionary<string, int> ColorTrackTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> ColorTrackKeyTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> ColorTrackInitialMatchTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public int AlphaOpacityTrackCount { get; init; }

    public int AlphaOpacityKeyCount { get; init; }

    public int AlphaOpacityTracksWithMaterialAlpha { get; init; }

    public int AlphaOpacityTracksMissingMaterialAlpha { get; init; }

    public int AlphaOpacityInitialAlphaMatches { get; init; }

    public int AlphaOpacityInitialAlphaMismatches { get; init; }

    public int AlphaOpacityCimgTargets { get; init; }

    public int AlphaOpacityDisplayFalseTargets { get; init; }

    public int AlphaOpacityKeysInUnitRange { get; init; }

    public int AlphaOpacityKeysOutOfUnitRange { get; init; }

    public int AlphaOpacityKeysMissingValue { get; init; }
}

internal sealed class SceneSurveyAggregate
{
    public required int Total { get; init; }

    public required int Parsed { get; init; }

    public required int Failed { get; init; }

    public required IReadOnlyDictionary<string, int> RootParamRawCounts { get; init; }

    public required IReadOnlyDictionary<string, int> VtbfTagCounts { get; init; }

    public required IReadOnlyDictionary<string, int> VtbfTagParamRawCounts { get; init; }

    public required IReadOnlyDictionary<string, int> VtbfTagParamLowHighCounts { get; init; }

    public required IReadOnlyDictionary<string, int> VtbfTagPropertyCountCounts { get; init; }

    public required IReadOnlyDictionary<string, int> VtbfTagParamHighPropertyCountCounts { get; init; }

    public required IReadOnlyDictionary<string, int> VtbfTagTrailingByteCounts { get; init; }

    public required IReadOnlyDictionary<string, int> VtbfKeyParamHighModulo5Counts { get; init; }

    public required IReadOnlyDictionary<string, int> VtbfFieldDirectoryCounts { get; init; }

    public required IReadOnlyDictionary<string, int> VtbfFieldDirectoryBlockCounts { get; init; }

    public required IReadOnlyDictionary<string, int> VtbfFieldCountValueCounts { get; init; }

    public required IReadOnlyDictionary<string, int> VtbfFieldStrideValueCounts { get; init; }

    public required IReadOnlyDictionary<string, int> SharedPackedStateOwnerCounts { get; init; }

    public required IReadOnlyDictionary<string, int> SharedPackedStateOwnerRawCounts { get; init; }

    public required IReadOnlyDictionary<string, int> SharedPackedStateOwnerBitCounts { get; init; }

    public required IReadOnlyDictionary<string, int> SharedPackedStateOwnerLowNibbleCounts { get; init; }

    public required IReadOnlyDictionary<string, int> SharedPackedStateOwnerMaskF0Counts { get; init; }

    public required IReadOnlyDictionary<string, int> SharedPackedStateOwnerMaskF00Counts { get; init; }

    public required IReadOnlyDictionary<string, int> SharedPackedStateOwnerUpperMaskCounts { get; init; }

    public required IReadOnlyDictionary<string, int> CatrField03Counts { get; init; }

    public required IReadOnlyDictionary<string, int> CatrField0DCounts { get; init; }

    public required IReadOnlyDictionary<string, int> CatrField0ECounts { get; init; }

    public required IReadOnlyDictionary<string, int> CatrField0FTypeCounts { get; init; }

    public required IReadOnlyDictionary<string, int> CatrField0FPreviewCounts { get; init; }

    public required IReadOnlyDictionary<string, int> CatrFieldSequenceCounts { get; init; }

    public required IReadOnlyDictionary<string, int> CatrFieldSetCounts { get; init; }

    public required IReadOnlyDictionary<string, int> ProjectField00Counts { get; init; }

    public required IReadOnlyDictionary<string, int> ProjectField01Counts { get; init; }

    public required IReadOnlyDictionary<string, int> ProjectField05Counts { get; init; }

    public required IReadOnlyDictionary<string, int> ProjectField55Counts { get; init; }

    public required IReadOnlyDictionary<string, int> ProjectField56Counts { get; init; }

    public required IReadOnlyDictionary<string, int> ProjectField56TrackLastRelationCounts { get; init; }

    public required IReadOnlyDictionary<string, int> ProjectField56KeyMaxRelationCounts { get; init; }

    public required IReadOnlyDictionary<string, int> ProjectField56DeltaToTrackLastCounts { get; init; }

    public required IReadOnlyDictionary<string, int> ProjectField56DeltaToKeyMaxCounts { get; init; }

    public required IReadOnlyDictionary<string, int> ProjectFieldSequenceCounts { get; init; }

    public required IReadOnlyDictionary<string, int> ProjectFieldSetCounts { get; init; }

    public required IReadOnlyDictionary<string, int> ScnNameCounts { get; init; }

    public required IReadOnlyDictionary<string, int> ScnField04RawHexCounts { get; init; }

    public required IReadOnlyDictionary<string, int> ScnField10Counts { get; init; }

    public required IReadOnlyDictionary<string, int> ScnField11Counts { get; init; }

    public required IReadOnlyDictionary<string, int> ScnField40Counts { get; init; }

    public required IReadOnlyDictionary<string, int> ScnField41Counts { get; init; }

    public required IReadOnlyDictionary<string, int> ScnField10Field11Counts { get; init; }

    public required IReadOnlyDictionary<string, int> ScnField40Field41Counts { get; init; }

    public required IReadOnlyDictionary<string, int> ScnParamLowLayerCountDeltaCounts { get; init; }

    public required IReadOnlyDictionary<string, int> ScnParamLowField10DeltaCounts { get; init; }

    public required IReadOnlyDictionary<string, int> ScnField10LayerCountDeltaCounts { get; init; }

    public required IReadOnlyDictionary<string, int> ScnFieldSequenceCounts { get; init; }

    public required IReadOnlyDictionary<string, int> ScnFieldSetCounts { get; init; }

    public required IReadOnlyDictionary<string, int> LayerNameCounts { get; init; }

    public required IReadOnlyDictionary<string, int> LayerField20Counts { get; init; }

    public required IReadOnlyDictionary<string, int> LayerField20BitCounts { get; init; }

    public required IReadOnlyDictionary<string, int> LayerField21Counts { get; init; }

    public required IReadOnlyDictionary<string, int> LayerField21BitCounts { get; init; }

    public required IReadOnlyDictionary<string, int> LayerField22Counts { get; init; }

    public required IReadOnlyDictionary<string, int> LayerField22BitCounts { get; init; }

    public required IReadOnlyDictionary<string, int> LayerField21SceneNodeCountDeltaCounts { get; init; }

    public required IReadOnlyDictionary<string, int> LayerParamLowField22DeltaCounts { get; init; }

    public required IReadOnlyDictionary<string, int> LayerFieldSequenceCounts { get; init; }

    public required IReadOnlyDictionary<string, int> LayerFieldSetCounts { get; init; }

    public required IReadOnlyDictionary<string, int> CameraNameCounts { get; init; }

    public required IReadOnlyDictionary<string, int> CameraField12VectorCounts { get; init; }

    public required IReadOnlyDictionary<string, int> CameraField13VectorCounts { get; init; }

    public required IReadOnlyDictionary<string, int> CameraField14Counts { get; init; }

    public required IReadOnlyDictionary<string, int> CameraField14BitCounts { get; init; }

    public required IReadOnlyDictionary<string, int> CameraField15Counts { get; init; }

    public required IReadOnlyDictionary<string, int> CameraField16Counts { get; init; }

    public required IReadOnlyDictionary<string, int> CameraFieldSequenceCounts { get; init; }

    public required IReadOnlyDictionary<string, int> CameraFieldSetCounts { get; init; }

    public required IReadOnlyDictionary<string, int> AnimationFieldSequenceCounts { get; init; }

    public required IReadOnlyDictionary<string, int> AnimationFieldSetCounts { get; init; }

    public required IReadOnlyDictionary<string, int> AnimationParamLowMotionDeltaCounts { get; init; }

    public required IReadOnlyDictionary<string, int> AnimationField50MotionDeltaCounts { get; init; }

    public required IReadOnlyDictionary<string, int> AnimationField50MaxMotionTrackDeltaCounts { get; init; }

    public required IReadOnlyDictionary<string, int> AnimationField50MotionOrMaxTrackRelationCounts { get; init; }

    public required IReadOnlyDictionary<string, int> AnimationParamLowField50DeltaCounts { get; init; }

    public required IReadOnlyDictionary<string, int> AnimationField5FCounts { get; init; }

    public required IReadOnlyDictionary<string, int> AnimationField5FMotionPresenceCounts { get; init; }

    public required IReadOnlyDictionary<string, int> AnimationField5FAnimationNameCounts { get; init; }

    public required IReadOnlyDictionary<string, int> AnimationField5FParamLowMotionDeltaCounts { get; init; }

    public required IReadOnlyDictionary<string, int> AnimationField5FField50MotionDeltaCounts { get; init; }

    public required IReadOnlyDictionary<string, int> AnimationField5FField50RelationCounts { get; init; }

    public required IReadOnlyDictionary<string, int> AnimationField5FEndFrameRelationCounts { get; init; }

    public required IReadOnlyDictionary<string, int> AnimationEndFrameRelationCounts { get; init; }

    public required IReadOnlyDictionary<string, int> AnimationEndFrameDeltaToTrackLastCounts { get; init; }

    public required IReadOnlyDictionary<string, int> AnimationEndFrameDeltaToKeyMaxCounts { get; init; }

    public required IReadOnlyDictionary<string, int> MotionFieldSequenceCounts { get; init; }

    public required IReadOnlyDictionary<string, int> MotionFieldSetCounts { get; init; }

    public required IReadOnlyDictionary<string, int> MotionParamLowTrackDeltaCounts { get; init; }

    public required IReadOnlyDictionary<string, int> MotionField52TrackDeltaCounts { get; init; }

    public required IReadOnlyDictionary<string, int> MotionParamLowField52DeltaCounts { get; init; }

    public required IReadOnlyDictionary<string, int> MotionTargetIndexRangeCounts { get; init; }

    public required int DataParamLowMatchesImageCasts { get; init; }

    public required int DataParamLowMatchesFollowingImageCasts { get; init; }

    public required int DataParamLowMatchesFollowingCimgCrfd { get; init; }

    public required int DataParamLowMatchesFollowingCimgCnumCrfd { get; init; }

    public required int DataParamLowMatchesFollowingCimgCnumCrfdCsli { get; init; }

    public required int DataBlocksWithFields { get; init; }

    public required int DataBlocksWithTrailingBytes { get; init; }

    public required int NcatMatchesNodes { get; init; }

    public required int NcatNonZeroRecords { get; init; }

    public required int NcatDetailRecords { get; init; }

    public required int NcatRecordsWithCategory { get; init; }

    public required int NcatRecordsWithoutCategory { get; init; }

    public required IReadOnlyDictionary<string, int> NcatKindCounts { get; init; }

    public required IReadOnlyDictionary<string, int> NcatTypeByteCounts { get; init; }

    public required IReadOnlyDictionary<string, int> NcatCategoryCounts { get; init; }

    public required IReadOnlyDictionary<string, int> NcatKindTypeByteCounts { get; init; }

    public required IReadOnlyDictionary<string, int> NcatKindCategoryCounts { get; init; }

    public required IReadOnlyDictionary<string, int> NcatTypeByteCategoryCounts { get; init; }

    public required IReadOnlyDictionary<string, int> NcatKindParameterPresenceCounts { get; init; }

    public required IReadOnlyDictionary<string, int> NcatParameterStringCounts { get; init; }

    public required IReadOnlyDictionary<string, int> NcatParameterFieldTypeCounts { get; init; }

    public required IReadOnlyDictionary<string, int> NcatKindParameterFieldTypeCounts { get; init; }

    public required IReadOnlyDictionary<string, int> NcatCategoryParameterFieldTypeCounts { get; init; }

    public required IReadOnlyDictionary<string, int> NcatParameterFieldTypePreviewCounts { get; init; }

    public required IReadOnlyDictionary<string, int> NcatKindNodeFlagCounts { get; init; }

    public required IReadOnlyDictionary<string, int> NcatKindNodeFlagBitCounts { get; init; }

    public required IReadOnlyDictionary<string, int> NcatKindNodeGroupCounts { get; init; }

    public required IReadOnlyDictionary<string, int> NcatKindDisplayCounts { get; init; }

    public required IReadOnlyDictionary<string, int> NcatKindCimgTargetCounts { get; init; }

    public required IReadOnlyDictionary<string, int> NcatKindAnimatedNodeCounts { get; init; }

    public required int ScenesWithWarnings { get; init; }

    public required IReadOnlyDictionary<string, int> WarningKindCounts { get; init; }

    public required int Cimg44Matches { get; init; }

    public required int Cimg44Mismatches { get; init; }

    public required IReadOnlyDictionary<string, int> Cimg44CountTupleCounts { get; init; }

    public required IReadOnlyDictionary<string, int> Cimg44PrimaryCountCounts { get; init; }

    public required IReadOnlyDictionary<string, int> Cimg44SecondaryCountCounts { get; init; }

    public required int Cimg45ActiveGroups { get; init; }

    public required int Cimg45InRangeGroups { get; init; }

    public required int Cimg45OutOfRangeGroups { get; init; }

    public required int Cimg45EmptyGroupNonZero { get; init; }

    public required int Cimg45NonZeroIndices { get; init; }

    public required int Cimg45NonZeroImageCasts { get; init; }

    public required IReadOnlyDictionary<string, int> Cimg45GroupIndexCounts { get; init; }

    public required IReadOnlyDictionary<string, int> Cimg45GroupCountIndexCounts { get; init; }

    public required IReadOnlyDictionary<string, int> Cimg45NonZeroGroupCounts { get; init; }

    public required int CnumCount { get; init; }

    public required int CnumCropReferenceCount { get; init; }

    public required int CnumField44Matches { get; init; }

    public required int CnumField44Mismatches { get; init; }

    public required int CnumField44Missing { get; init; }

    public required int CnumField51InRange { get; init; }

    public required int CnumField51OutOfRange { get; init; }

    public required int CnumField51Missing { get; init; }

    public required IReadOnlyDictionary<string, int> CnumField44Counts { get; init; }

    public required IReadOnlyDictionary<string, int> CnumZeroMarkerFieldCounts { get; init; }

    public required IReadOnlyDictionary<string, int> CnumFieldA1Counts { get; init; }

    public required IReadOnlyDictionary<string, int> CnumField48Counts { get; init; }

    public required IReadOnlyDictionary<string, int> CnumFieldA0Counts { get; init; }

    public required IReadOnlyDictionary<string, int> CnumFieldA1RawLengthCounts { get; init; }

    public required IReadOnlyDictionary<string, int> CnumFieldA1ContentLengthCounts { get; init; }

    public required IReadOnlyDictionary<string, int> CnumFieldA1Utf8StatusCounts { get; init; }

    public required IReadOnlyDictionary<string, int> CnumFieldA1ShiftJisByteShapeCounts { get; init; }

    public required IReadOnlyDictionary<string, int> CnumFieldA1RawPreviewCounts { get; init; }

    public required IReadOnlyDictionary<string, int> CnumFieldA1Field44Counts { get; init; }

    public required IReadOnlyDictionary<string, int> CnumFieldA1CropReferenceCountCounts { get; init; }

    public required IReadOnlyDictionary<string, int> CnumFieldA1ZeroMarkerFieldCounts { get; init; }

    public required IReadOnlyDictionary<string, int> CnumFieldA1NodeFlagCounts { get; init; }

    public required IReadOnlyDictionary<string, int> CnumFieldA1NodeGroupCounts { get; init; }

    public required IReadOnlyDictionary<string, int> CnumFieldA1DisplayCounts { get; init; }

    public required IReadOnlyDictionary<string, int> CnumFieldA1CimgTargetCounts { get; init; }

    public required IReadOnlyDictionary<string, int> CnumFieldA1AnimatedTargetCounts { get; init; }

    public required IReadOnlyDictionary<string, int> CnumFieldSequenceCounts { get; init; }

    public required IReadOnlyDictionary<string, int> CnumFieldSetCounts { get; init; }

    public required int CrfdCount { get; init; }

    public required int CrfdField51InRange { get; init; }

    public required int CrfdField51OutOfRange { get; init; }

    public required int CrfdField51Missing { get; init; }

    public required IReadOnlyDictionary<string, int> CrfdField90Counts { get; init; }

    public required IReadOnlyDictionary<string, int> CrfdField91Counts { get; init; }

    public required IReadOnlyDictionary<string, int> CrfdField90Field91Counts { get; init; }

    public required IReadOnlyDictionary<string, int> CrfdField90Field91Field92Counts { get; init; }

    public required IReadOnlyDictionary<string, int> CrfdStringFieldRelationCounts { get; init; }

    public required IReadOnlyDictionary<string, int> CrfdStringFieldTargetTypeCounts { get; init; }

    public required IReadOnlyDictionary<string, int> CrfdField90Field91RelationCounts { get; init; }

    public required IReadOnlyDictionary<string, int> CrfdField90Field91EqualityCounts { get; init; }

    public required IReadOnlyDictionary<string, int> CrfdField90Field91Field92RelationCounts { get; init; }

    public required IReadOnlyDictionary<string, int> CrfdField92Counts { get; init; }

    public required IReadOnlyDictionary<string, int> CrfdField93Counts { get; init; }

    public required IReadOnlyDictionary<string, int> CrfdField94Counts { get; init; }

    public required int CrfdField94NonZero { get; init; }

    public required IReadOnlyDictionary<string, int> CrfdField95Counts { get; init; }

    public required int TextCount { get; init; }

    public required int TextField7APresent { get; init; }

    public required IReadOnlyDictionary<string, int> TextZeroMarkerFieldCounts { get; init; }

    public required IReadOnlyDictionary<string, int> TextField41Counts { get; init; }

    public required IReadOnlyDictionary<string, int> TextField78Counts { get; init; }

    public required IReadOnlyDictionary<string, int> TextField79Counts { get; init; }

    public required IReadOnlyDictionary<string, int> TextField7CCounts { get; init; }

    public required IReadOnlyDictionary<string, int> TextField7AStringCounts { get; init; }

    public required IReadOnlyDictionary<string, int> TextField7ARawLengthCounts { get; init; }

    public required IReadOnlyDictionary<string, int> TextField7AContentLengthCounts { get; init; }

    public required IReadOnlyDictionary<string, int> TextField7AUtf8StatusCounts { get; init; }

    public required IReadOnlyDictionary<string, int> TextField7AShiftJisByteShapeCounts { get; init; }

    public required IReadOnlyDictionary<string, int> TextField7AShiftJisDecodeStatusCounts { get; init; }

    public required IReadOnlyDictionary<string, int> TextField7AShiftJisStringCounts { get; init; }

    public required IReadOnlyDictionary<string, int> TextField7ARawPreviewCounts { get; init; }

    public required IReadOnlyDictionary<string, int> TextField7AField41Counts { get; init; }

    public required IReadOnlyDictionary<string, int> TextField7AField78Counts { get; init; }

    public required IReadOnlyDictionary<string, int> TextField7AField79Counts { get; init; }

    public required IReadOnlyDictionary<string, int> TextField7AField7CCounts { get; init; }

    public required IReadOnlyDictionary<string, int> TextField33VectorCounts { get; init; }

    public required IReadOnlyDictionary<string, int> TextField33RawHexCounts { get; init; }

    public required IReadOnlyDictionary<string, int> TextField7BPackedValuesCounts { get; init; }

    public required IReadOnlyDictionary<string, int> TextField7BRawHexCounts { get; init; }

    public required IReadOnlyDictionary<string, int> TextField78Field79Counts { get; init; }

    public required IReadOnlyDictionary<string, int> TextZeroMarkerField7ACounts { get; init; }

    public required IReadOnlyDictionary<string, int> TextFieldSequenceCounts { get; init; }

    public required IReadOnlyDictionary<string, int> TextFieldSetCounts { get; init; }

    public required int SliceCasts { get; init; }

    public required int SliceRecords { get; init; }

    public required int SliceCropReferences { get; init; }

    public required int SliceField44SlicRecordMatches { get; init; }

    public required int SliceField44SlicRecordMismatches { get; init; }

    public required int SliceField44CropReferenceMatches { get; init; }

    public required int SliceField44CropReferenceMismatches { get; init; }

    public required int SliceTargetIndexInRange { get; init; }

    public required int SliceTargetIndexOutOfRange { get; init; }

    public required IReadOnlyDictionary<string, int> SliceField83Counts { get; init; }

    public required IReadOnlyDictionary<string, int> SliceCastField40Counts { get; init; }

    public required IReadOnlyDictionary<string, int> SliceCastField41Counts { get; init; }

    public required IReadOnlyDictionary<string, int> SliceCastField42Counts { get; init; }

    public required IReadOnlyDictionary<string, int> SliceCastField43Counts { get; init; }

    public required IReadOnlyDictionary<string, int> SliceCastField80Counts { get; init; }

    public required IReadOnlyDictionary<string, int> SliceCastField81Counts { get; init; }

    public required IReadOnlyDictionary<string, int> SliceCastField82Counts { get; init; }

    public required IReadOnlyDictionary<string, int> SliceCastField84Counts { get; init; }

    public required IReadOnlyDictionary<string, int> SliceCastField85Counts { get; init; }

    public required IReadOnlyDictionary<string, int> SliceCastField86Counts { get; init; }

    public required IReadOnlyDictionary<string, int> SliceCastField87Counts { get; init; }

    public required IReadOnlyDictionary<string, int> SliceCastTargetNodeFlagCounts { get; init; }

    public required IReadOnlyDictionary<string, int> SliceCastTargetNodeGroupCounts { get; init; }

    public required IReadOnlyDictionary<string, int> SliceCastTargetDisplayCounts { get; init; }

    public required IReadOnlyDictionary<string, int> SliceCastTargetCimgTargetCounts { get; init; }

    public required IReadOnlyDictionary<string, int> SliceCastFieldSequenceCounts { get; init; }

    public required IReadOnlyDictionary<string, int> SliceCastFieldSetCounts { get; init; }

    public required IReadOnlyDictionary<string, int> SliceRecordField40Counts { get; init; }

    public required IReadOnlyDictionary<string, int> SliceRecordField41Counts { get; init; }

    public required IReadOnlyDictionary<string, int> SliceRecordField45Counts { get; init; }

    public required IReadOnlyDictionary<string, int> SliceRecordField37ColorCounts { get; init; }

    public required IReadOnlyDictionary<string, int> SliceRecordField38ColorCounts { get; init; }

    public required IReadOnlyDictionary<string, int> SliceRecordField39ColorCounts { get; init; }

    public required IReadOnlyDictionary<string, int> SliceRecordField39ColorCountCounts { get; init; }

    public required IReadOnlyDictionary<string, int> SliceRecordField83Field40Counts { get; init; }

    public required IReadOnlyDictionary<string, int> SliceRecordField83Field41Counts { get; init; }

    public required IReadOnlyDictionary<string, int> SliceRecordField83Field45Counts { get; init; }

    public required IReadOnlyDictionary<string, int> SliceRecordFieldSequenceCounts { get; init; }

    public required IReadOnlyDictionary<string, int> SliceRecordFieldSetCounts { get; init; }

    public required IReadOnlyDictionary<string, int> SliceRecordShapeCounts { get; init; }

    public required int TrackKeyCountMismatches { get; init; }

    public required int KeyTangentPresent { get; init; }

    public required int KeyTangentNonZero { get; init; }

    public required int KeyTangentMismatch { get; init; }

    public required int KeyTangentNonZeroScenes { get; init; }

    public required int KeyTangentMismatchScenes { get; init; }

    public required IReadOnlyDictionary<string, int> UnknownTypeCodeCounts { get; init; }

    public required IReadOnlyDictionary<string, int> NodeFlagCounts { get; init; }

    public required IReadOnlyDictionary<string, int> NodeFlagBitCounts { get; init; }

    public required IReadOnlyDictionary<string, int> NodeFlagBitDisplayFalseNodeCounts { get; init; }

    public required IReadOnlyDictionary<string, int> NodeFlagBitCimgTargetNodeCounts { get; init; }

    public required IReadOnlyDictionary<string, int> NodeFlagBitAnimatedNodeCounts { get; init; }

    public required IReadOnlyDictionary<string, int> NodeFlagBitDataNodeCounts { get; init; }

    public required IReadOnlyDictionary<string, int> NodeFlagBitCategoryRecordNodeCounts { get; init; }

    public required IReadOnlyDictionary<string, int> NodeFlagBitCategoryNonZeroNodeCounts { get; init; }

    public required IReadOnlyDictionary<string, int> NodeFlagBitExactFlagCounts { get; init; }

    public required IReadOnlyDictionary<string, int> NodeFlagBitGroupCounts { get; init; }

    public required IReadOnlyDictionary<string, int> NodeFlagBitImageCastFlagBitCounts { get; init; }

    public required IReadOnlyDictionary<string, int> NodeFlagBitTrackTypeCounts { get; init; }

    public required IReadOnlyDictionary<string, int> NodeFlagBitPairCounts { get; init; }

    public required IReadOnlyDictionary<string, int> CimgFlagCounts { get; init; }

    public required IReadOnlyDictionary<string, int> CimgFlagBitCounts { get; init; }

    public required IReadOnlyDictionary<string, int> CimgFlagBitDisplayFalseCounts { get; init; }

    public required IReadOnlyDictionary<string, int> CimgFlagBitMultiReferenceCounts { get; init; }

    public required IReadOnlyDictionary<string, int> CimgFlagBitSecondaryReferenceCounts { get; init; }

    public required IReadOnlyDictionary<string, int> CimgFlagBitNonZeroReferenceIndexCounts { get; init; }

    public required IReadOnlyDictionary<string, int> CimgFlagBitMissingNodeCounts { get; init; }

    public required IReadOnlyDictionary<string, int> CimgFlagBitNodeFlagCounts { get; init; }

    public required IReadOnlyDictionary<string, int> CimgFlagBitGroupCounts { get; init; }

    public required IReadOnlyDictionary<string, int> CimgFlagBitPairCounts { get; init; }

    public required int TextureAtlasCount { get; init; }

    public required IReadOnlyDictionary<string, int> TextureAtlasField62Counts { get; init; }

    public required IReadOnlyDictionary<string, int> TextureAtlasField62BitCounts { get; init; }

    public required IReadOnlyDictionary<string, int> TextureAtlasField62CropCountCounts { get; init; }

    public required IReadOnlyDictionary<string, int> TextureAtlasField62SizeCounts { get; init; }

    public required IReadOnlyDictionary<string, int> CropKindCounts { get; init; }

    public required IReadOnlyDictionary<string, int> CrefKindCounts { get; init; }

    public required int CropRectCount { get; init; }

    public required int CropAtlasDeclaredCountMatches { get; init; }

    public required int CropAtlasDeclaredCountMismatches { get; init; }

    public required int CropRectInAtlasBounds { get; init; }

    public required int CropRectOutOfAtlasBounds { get; init; }

    public required int CropRectNonPositiveSize { get; init; }

    public required int CropReferenceCount { get; init; }

    public required IReadOnlyDictionary<string, int> CropReferenceKindCounts { get; init; }

    public required IReadOnlyDictionary<string, int> CropReferenceOwnerCounts { get; init; }

    public required IReadOnlyDictionary<string, int> CropReferenceOwnerKindCounts { get; init; }

    public required IReadOnlyDictionary<string, int> CropReferenceTextureListIndexCounts { get; init; }

    public required IReadOnlyDictionary<string, int> CropReferenceTextureIndexRangeCounts { get; init; }

    public required IReadOnlyDictionary<string, int> CropReferenceCropIndexRangeCounts { get; init; }

    public required IReadOnlyDictionary<string, int> CropRectOutOfAtlasBoundsReasonCounts { get; init; }

    public required IReadOnlyDictionary<string, int> CropReferenceOutOfRangeOwnerCounts { get; init; }

    public required IReadOnlyDictionary<string, int> TrackFlagCounts { get; init; }

    public required IReadOnlyDictionary<string, int> TrackFlagBaseCounts { get; init; }

    public required IReadOnlyDictionary<string, int> TrackFlagExtraCounts { get; init; }

    public required IReadOnlyDictionary<string, int> TrackFlagExtraSceneCounts { get; init; }

    public required IReadOnlyDictionary<string, int> TrackFlagExtraBaseCounts { get; init; }

    public required IReadOnlyDictionary<string, int> TrackFlagExtraAnimationCounts { get; init; }

    public required IReadOnlyDictionary<string, int> TrackFlagExtraTrackTypeCounts { get; init; }

    public required IReadOnlyDictionary<string, int> TrackFlagExtraKeyValueTypeCounts { get; init; }

    public required IReadOnlyDictionary<string, int> TrackFlagExtraNodeFlagCounts { get; init; }

    public required IReadOnlyDictionary<string, int> TrackFlagExtraNodeFlagBitCounts { get; init; }

    public required IReadOnlyDictionary<string, int> TrackFlagExtraGroupCounts { get; init; }

    public required IReadOnlyDictionary<string, int> TrackFlagExtraCimgTargetCounts { get; init; }

    public required IReadOnlyDictionary<string, int> TrackFlagExtraInitialDisplayCounts { get; init; }

    public required IReadOnlyDictionary<string, int> TrackFlagExtraCimgFlagCounts { get; init; }

    public required IReadOnlyDictionary<string, int> TrackFlagExtraCimgFlagBitCounts { get; init; }

    public required IReadOnlyDictionary<string, int> TrackFlagExtraCimgReferenceCountCounts { get; init; }

    public required IReadOnlyDictionary<string, int> TrackTypeCounts { get; init; }

    public required IReadOnlyDictionary<string, int> KeyValueTypeCounts { get; init; }

    public required IReadOnlyDictionary<string, int> KeyInterpolationCounts { get; init; }

    public required IReadOnlyDictionary<string, int> KeyInterpolationTrackTypeCounts { get; init; }

    public required IReadOnlyDictionary<string, int> KeyInterpolationKeyValueTypeCounts { get; init; }

    public required IReadOnlyDictionary<string, int> KeyTangentPresentInterpolationCounts { get; init; }

    public required IReadOnlyDictionary<string, int> KeyTangentPresentTrackTypeCounts { get; init; }

    public required IReadOnlyDictionary<string, int> KeyTangentNonZeroInterpolationCounts { get; init; }

    public required IReadOnlyDictionary<string, int> KeyTangentNonZeroTrackTypeCounts { get; init; }

    public required IReadOnlyDictionary<string, int> KeyTangentMismatchInterpolationCounts { get; init; }

    public required IReadOnlyDictionary<string, int> KeyTangentMismatchTrackTypeCounts { get; init; }

    public required IReadOnlyDictionary<string, int> KeyTangentMismatchAnimationCounts { get; init; }

    public required IReadOnlyDictionary<string, int> KeyTangentMismatchNodeFlagCounts { get; init; }

    public required IReadOnlyDictionary<string, int> KeyTangentMismatchGroupCounts { get; init; }

    public required IReadOnlyDictionary<string, int> KeyTangentMismatchTrackExtraCounts { get; init; }

    public required IReadOnlyDictionary<string, int> KeyTangentMismatchTangentPairCounts { get; init; }

    public required IReadOnlyDictionary<string, int> KeyTangentNonZeroFramePositionCounts { get; init; }

    public required IReadOnlyDictionary<string, int> KeyTangentMismatchFramePositionCounts { get; init; }

    public required IReadOnlyDictionary<string, int> KeyTangentDeltaSignCounts { get; init; }

    public required IReadOnlyDictionary<string, int> TrackKeyStorageMatrixCounts { get; init; }

    public required IReadOnlyDictionary<string, int> TrackFieldSequenceCounts { get; init; }

    public required IReadOnlyDictionary<string, int> KeyFieldSequenceCounts { get; init; }

    public required IReadOnlyDictionary<string, int> TrackFrameRangeRelationCounts { get; init; }

    public required IReadOnlyDictionary<string, int> TrackKeyFrameOrderCounts { get; init; }

    public required IReadOnlyDictionary<string, int> TrackKeyFrameDuplicateCounts { get; init; }

    public required IReadOnlyDictionary<string, int> TrackFirstFrameDeltaCounts { get; init; }

    public required IReadOnlyDictionary<string, int> TrackLastFrameDeltaCounts { get; init; }

    public required int TransformTrackCount { get; init; }

    public required int TransformTrackKeyCount { get; init; }

    public required int TransformTracksWithInitialChannel { get; init; }

    public required int TransformTracksMissingInitialChannel { get; init; }

    public required int TransformTrackInitialValueMatches { get; init; }

    public required int TransformTrackInitialValueMismatches { get; init; }

    public required int TransformTrackKeysMissingValue { get; init; }

    public required IReadOnlyDictionary<string, int> TransformTrackTypeCounts { get; init; }

    public required IReadOnlyDictionary<string, int> TransformTrackKeyTypeCounts { get; init; }

    public required IReadOnlyDictionary<string, int> TransformTrackStorageCounts { get; init; }

    public required IReadOnlyDictionary<string, int> TransformTrackKeyValueKindCounts { get; init; }

    public required IReadOnlyDictionary<string, int> TransformTrackInitialMatchTypeCounts { get; init; }

    public required IReadOnlyDictionary<string, int> TransformTrackValueRangeCounts { get; init; }

    public required IReadOnlyDictionary<string, int> TransformCandidateDefaultKeyCounts { get; init; }

    public required int PackedAngleTrackCount { get; init; }

    public required int PackedAngleKeyCount { get; init; }

    public required IReadOnlyDictionary<string, int> PackedAngleTrackTypeCounts { get; init; }

    public required IReadOnlyDictionary<string, int> PackedAngleKeyTrackTypeCounts { get; init; }

    public required IReadOnlyDictionary<string, int> PackedAngleRawCounts { get; init; }

    public required IReadOnlyDictionary<string, int> PackedAngleDegreeCandidateCounts { get; init; }

    public required int ImageVariantTrackCount { get; init; }

    public required int ImageVariantKeyCount { get; init; }

    public required int ImageVariantTracksWithCimg { get; init; }

    public required int ImageVariantTracksMissingCimg { get; init; }

    public required int ImageVariantTrackRangeMatches { get; init; }

    public required int ImageVariantTrackRangeMismatches { get; init; }

    public required int ImageVariantKeysInRange { get; init; }

    public required int ImageVariantKeysOutOfRange { get; init; }

    public required int ImageVariantKeysMissingCimg { get; init; }

    public required int ImageVariantKeysNonInteger { get; init; }

    public required int ImageVariantKeysMissingValue { get; init; }

    public required IReadOnlyDictionary<string, int> ImageVariantReferenceCountCounts { get; init; }

    public required IReadOnlyDictionary<string, int> ImageVariantValueCounts { get; init; }

    public required IReadOnlyDictionary<string, int> ImageVariantGroupTrackCounts { get; init; }

    public required IReadOnlyDictionary<string, int> ImageVariantGroupKeyCounts { get; init; }

    public required IReadOnlyDictionary<string, int> ImageVariantGroupTracksWithCimgCounts { get; init; }

    public required IReadOnlyDictionary<string, int> ImageVariantGroupTracksMissingCimgCounts { get; init; }

    public required IReadOnlyDictionary<string, int> ImageVariantGroupTrackRangeMatchCounts { get; init; }

    public required IReadOnlyDictionary<string, int> ImageVariantGroupTrackRangeMismatchCounts { get; init; }

    public required IReadOnlyDictionary<string, int> ImageVariantGroupKeysInRangeCounts { get; init; }

    public required IReadOnlyDictionary<string, int> ImageVariantGroupKeysOutOfRangeCounts { get; init; }

    public required IReadOnlyDictionary<string, int> ImageVariantGroupKeysMissingCimgCounts { get; init; }

    public required IReadOnlyDictionary<string, int> ImageVariantGroupKeysNonIntegerCounts { get; init; }

    public required IReadOnlyDictionary<string, int> ImageVariantGroupKeysMissingValueCounts { get; init; }

    public required IReadOnlyDictionary<string, int> ImageVariantGroupReferenceCountCounts { get; init; }

    public required IReadOnlyDictionary<string, int> ImageVariantGroupValueCounts { get; init; }

    public required IReadOnlyDictionary<string, int> ImageVariantGroupCimg45FirstKeyRelationCounts { get; init; }

    public required IReadOnlyDictionary<string, int> ImageVariantGroupCimg45FirstKeyDeltaCounts { get; init; }

    public required IReadOnlyDictionary<string, int> ImageVariantGroupCimg45FirstKeyPairCounts { get; init; }

    public required int ColorTrackCount { get; init; }

    public required int ColorTrackKeyCount { get; init; }

    public required int ColorTracksWithInitialChannel { get; init; }

    public required int ColorTracksMissingInitialChannel { get; init; }

    public required int ColorTrackInitialValueMatches { get; init; }

    public required int ColorTrackInitialValueMismatches { get; init; }

    public required int ColorTrackKeysInUnitRange { get; init; }

    public required int ColorTrackKeysOutOfUnitRange { get; init; }

    public required int ColorTrackKeysMissingValue { get; init; }

    public required IReadOnlyDictionary<string, int> ColorTrackTypeCounts { get; init; }

    public required IReadOnlyDictionary<string, int> ColorTrackKeyTypeCounts { get; init; }

    public required IReadOnlyDictionary<string, int> ColorTrackInitialMatchTypeCounts { get; init; }

    public required int AlphaOpacityTrackCount { get; init; }

    public required int AlphaOpacityKeyCount { get; init; }

    public required int AlphaOpacityTracksWithMaterialAlpha { get; init; }

    public required int AlphaOpacityTracksMissingMaterialAlpha { get; init; }

    public required int AlphaOpacityInitialAlphaMatches { get; init; }

    public required int AlphaOpacityInitialAlphaMismatches { get; init; }

    public required int AlphaOpacityCimgTargets { get; init; }

    public required int AlphaOpacityDisplayFalseTargets { get; init; }

    public required int AlphaOpacityKeysInUnitRange { get; init; }

    public required int AlphaOpacityKeysOutOfUnitRange { get; init; }

    public required int AlphaOpacityKeysMissingValue { get; init; }
}

internal sealed class SvoSurveyRow
{
    public required string Path { get; init; }

    public required string RelativePath { get; init; }

    public required long Size { get; init; }

    public string? Error { get; init; }

    public int DirectoryCount { get; init; }

    public int HeaderUnknownNonZeroBytes { get; init; }

    public IReadOnlyDictionary<string, int> HeaderUnknownWordClassCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> HeaderUnknownNonZeroOffsetCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> HeaderUnknownWordValueCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> HeaderUnknownWordOffsetValueCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> HeaderUnknownWordOffsetClassCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> HeaderUnknownWordRelationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> HeaderUnknownWordOffsetRelationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> HeaderUnknownWordPayloadLocationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> HeaderUnknownWordOffsetPayloadLocationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public int DdsCount { get; init; }

    public int DirectoryReservedEntriesWithNonZero { get; init; }

    public int DirectoryReservedNonZeroBytes { get; init; }

    public bool YabxPresent { get; init; }

    public string? YabxHeaderHashCandidate { get; init; }

    public bool? YabxDeclaredPayloadLengthMatchesEntryLength { get; init; }

    public string? YabxReferenceBase { get; init; }

    public int? YabxObjectCount { get; init; }

    public int? YabxExpectedObjectCountFromDds { get; init; }

    public bool? YabxObjectCountMatchesDdsSkeleton { get; init; }

    public bool? YabxObjectTypeOrderMatchesDdsSkeleton { get; init; }

    public int? YabxUnparsedBytes { get; init; }

    public IReadOnlyDictionary<string, int> YabxObjectTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> YabxDescriptorRawCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> YabxDescriptorFlagsCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> YabxDescriptorValueKindCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> YabxDescriptorReservedCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> YabxDescriptorUsageCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> YabxDescriptorRawUsageCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> YabxDescriptorRawObjectKindCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    public int YabxResourceRecordCount { get; init; }

    public bool? YabxResourceRecordCountMatchesDds { get; init; }

    public int YabxResourceTextureImageReferenceMatches { get; init; }

    public int YabxResourceTextureImageReferenceMismatches { get; init; }

    public int YabxResourceTextureImageReferenceMissing { get; init; }

    public int YabxResourceDataSizeMatchesDirectory { get; init; }

    public int YabxResourceDataSizeMismatchesDirectory { get; init; }

    public int YabxResourceDataSizeMissing { get; init; }

    public int YabxResourceDimensionsMatchDds { get; init; }

    public int YabxResourceDimensionsMismatchDds { get; init; }

    public int YabxResourceDimensionsMissing { get; init; }

    public IReadOnlyDictionary<string, int> TextureFormatCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);
}

internal sealed class SvoSurveyAggregate
{
    public required int Total { get; init; }

    public required int Parsed { get; init; }

    public required int Failed { get; init; }

    public required int DirectoryReservedEntriesWithNonZero { get; init; }

    public required int DirectoryReservedNonZeroBytes { get; init; }

    public required int YabxWithUnparsedBytes { get; init; }

    public required int YabxUnparsedBytes { get; init; }

    public required int YabxExpectedObjectCountFromDds { get; init; }

    public required int YabxObjectCountDdsSkeletonMatches { get; init; }

    public required int YabxObjectCountDdsSkeletonMismatches { get; init; }

    public required int YabxObjectTypeOrderDdsSkeletonMatches { get; init; }

    public required int YabxObjectTypeOrderDdsSkeletonMismatches { get; init; }

    public required IReadOnlyDictionary<string, int> HeaderUnknownWordClassCounts { get; init; }

    public required IReadOnlyDictionary<string, int> HeaderUnknownNonZeroOffsetCounts { get; init; }

    public required IReadOnlyDictionary<string, int> HeaderUnknownWordValueCounts { get; init; }

    public required IReadOnlyDictionary<string, int> HeaderUnknownWordOffsetValueCounts { get; init; }

    public required IReadOnlyDictionary<string, int> HeaderUnknownWordOffsetClassCounts { get; init; }

    public required IReadOnlyDictionary<string, int> HeaderUnknownWordRelationCounts { get; init; }

    public required IReadOnlyDictionary<string, int> HeaderUnknownWordOffsetRelationCounts { get; init; }

    public required IReadOnlyDictionary<string, int> HeaderUnknownWordPayloadLocationCounts { get; init; }

    public required IReadOnlyDictionary<string, int> HeaderUnknownWordOffsetPayloadLocationCounts { get; init; }

    public required IReadOnlyDictionary<string, int> YabxHeaderHashCandidateCounts { get; init; }

    public required int YabxDeclaredPayloadLengthMatchesEntryLength { get; init; }

    public required int YabxDeclaredPayloadLengthMismatchesEntryLength { get; init; }

    public required IReadOnlyDictionary<string, int> YabxReferenceBaseCounts { get; init; }

    public required IReadOnlyDictionary<string, int> YabxDescriptorRawCounts { get; init; }

    public required IReadOnlyDictionary<string, int> YabxDescriptorFlagsCounts { get; init; }

    public required IReadOnlyDictionary<string, int> YabxDescriptorValueKindCounts { get; init; }

    public required IReadOnlyDictionary<string, int> YabxDescriptorReservedCounts { get; init; }

    public required IReadOnlyDictionary<string, int> YabxDescriptorUsageCounts { get; init; }

    public required IReadOnlyDictionary<string, int> YabxDescriptorRawUsageCounts { get; init; }

    public required IReadOnlyDictionary<string, int> YabxDescriptorRawObjectKindCounts { get; init; }

    public required IReadOnlyDictionary<string, int> TextureFormatCounts { get; init; }

    public required IReadOnlyDictionary<string, int> YabxObjectTypeCounts { get; init; }

    public required int YabxResourceRecordCount { get; init; }

    public required int YabxResourceRecordCountDdsMatches { get; init; }

    public required int YabxResourceRecordCountDdsMismatches { get; init; }

    public required int YabxResourceTextureImageReferenceMatches { get; init; }

    public required int YabxResourceTextureImageReferenceMismatches { get; init; }

    public required int YabxResourceTextureImageReferenceMissing { get; init; }

    public required int YabxResourceDataSizeMatchesDirectory { get; init; }

    public required int YabxResourceDataSizeMismatchesDirectory { get; init; }

    public required int YabxResourceDataSizeMissing { get; init; }

    public required int YabxResourceDimensionsMatchDds { get; init; }

    public required int YabxResourceDimensionsMismatchDds { get; init; }

    public required int YabxResourceDimensionsMissing { get; init; }
}

internal sealed record CimgIndexSurvey(
    int ActiveGroups,
    int InRangeGroups,
    int OutOfRangeGroups,
    int EmptyGroupNonZeroIndices,
    int NonZeroIndices,
    int NonZeroImageCasts,
    IReadOnlyDictionary<string, int> CountTupleCounts,
    IReadOnlyDictionary<string, int> PrimaryCountCounts,
    IReadOnlyDictionary<string, int> SecondaryCountCounts,
    IReadOnlyList<Cimg44SecondaryNonZeroSample> SecondaryNonZeroSamples,
    IReadOnlyDictionary<string, int> GroupIndexCounts,
    IReadOnlyDictionary<string, int> GroupCountIndexCounts,
    IReadOnlyDictionary<string, int> NonZeroGroupCounts,
    IReadOnlyList<Cimg45NonZeroSample> NonZeroSamples);

internal sealed record Cimg44SecondaryNonZeroSample(
    int ImageCastIndex,
    string ImageCastOffset,
    string? NodeName,
    int? PrimaryDeclaredCount,
    int? SecondaryDeclaredCount,
    int PrimaryActualReferenceCount,
    int SecondaryActualReferenceCount,
    int? PrimaryIndex,
    int? SecondaryIndex,
    IReadOnlyList<string> SecondaryRawHex);

internal sealed record Cimg45NonZeroSample(
    int ImageCastIndex,
    string ImageCastOffset,
    string? NodeName,
    string GroupName,
    int? DeclaredCount,
    int ActualReferenceCount,
    int GroupIndex,
    string? IndexedRawHex,
    int? TextureListIndex,
    int? TextureIndex,
    int? CropIndex,
    string? AtlasName,
    string? CropPath);

internal sealed record CropReferenceOwner(
    string OwnerKind,
    int OwnerIndex,
    long OwnerOffset,
    string? OwnerName,
    SbSceneCropReference Reference);

internal sealed record CropRectBoundsSample(
    int AtlasIndex,
    string AtlasName,
    int AtlasWidth,
    int AtlasHeight,
    int CropIndex,
    string RawHex,
    int Left,
    int Top,
    int Width,
    int Height,
    int Right,
    int Bottom);

internal sealed record CropReferenceRangeSample(
    string OwnerKind,
    int OwnerIndex,
    string OwnerOffset,
    string? OwnerName,
    int ReferenceIndex,
    string RawHex,
    int Kind,
    int TextureListIndex,
    int TextureIndex,
    int CropIndex,
    string TextureIndexRange,
    string CropIndexRange,
    int? AtlasIndex,
    string? AtlasName,
    int? AtlasCropCount);

internal sealed record TextureAtlasSurvey(
    int AtlasCount,
    IReadOnlyDictionary<string, int> Field62Counts,
    IReadOnlyDictionary<string, int> Field62BitCounts,
    IReadOnlyDictionary<string, int> Field62CropCountCounts,
    IReadOnlyDictionary<string, int> Field62SizeCounts);

internal sealed record CropPackedSurvey(
    int CropRectCount,
    int AtlasDeclaredCountMatches,
    int AtlasDeclaredCountMismatches,
    int CropRectInAtlasBounds,
    int CropRectOutOfAtlasBounds,
    int CropRectNonPositiveSize,
    int CropReferenceCount,
    IReadOnlyDictionary<string, int> ReferenceKindCounts,
    IReadOnlyDictionary<string, int> ReferenceOwnerCounts,
    IReadOnlyDictionary<string, int> ReferenceOwnerKindCounts,
    IReadOnlyDictionary<string, int> ReferenceTextureListIndexCounts,
    IReadOnlyDictionary<string, int> ReferenceTextureIndexRangeCounts,
    IReadOnlyDictionary<string, int> ReferenceCropIndexRangeCounts,
    IReadOnlyDictionary<string, int> CropRectOutOfAtlasBoundsReasonCounts,
    IReadOnlyDictionary<string, int> ReferenceOutOfRangeOwnerCounts,
    IReadOnlyList<CropRectBoundsSample> CropRectOutOfAtlasBoundsSamples,
    IReadOnlyList<CropReferenceRangeSample> ReferenceOutOfRangeSamples);

internal sealed record PackedAngleSurveyKey(TrackInfo Track, KeyframeInfo Key);

internal sealed record ImageVariantSurvey(
    int TrackCount,
    int KeyCount,
    int TracksWithCimg,
    int TracksMissingCimg,
    int TrackRangeMatches,
    int TrackRangeMismatches,
    int KeysInRange,
    int KeysOutOfRange,
    int KeysMissingCimg,
    int KeysNonInteger,
    int KeysMissingValue,
    IReadOnlyDictionary<string, int> ReferenceCountCounts,
    IReadOnlyDictionary<string, int> ValueCounts,
    IReadOnlyDictionary<string, int> GroupTrackCounts,
    IReadOnlyDictionary<string, int> GroupKeyCounts,
    IReadOnlyDictionary<string, int> GroupTracksWithCimgCounts,
    IReadOnlyDictionary<string, int> GroupTracksMissingCimgCounts,
    IReadOnlyDictionary<string, int> GroupTrackRangeMatchCounts,
    IReadOnlyDictionary<string, int> GroupTrackRangeMismatchCounts,
    IReadOnlyDictionary<string, int> GroupKeysInRangeCounts,
    IReadOnlyDictionary<string, int> GroupKeysOutOfRangeCounts,
    IReadOnlyDictionary<string, int> GroupKeysMissingCimgCounts,
    IReadOnlyDictionary<string, int> GroupKeysNonIntegerCounts,
    IReadOnlyDictionary<string, int> GroupKeysMissingValueCounts,
    IReadOnlyDictionary<string, int> GroupReferenceCountCounts,
    IReadOnlyDictionary<string, int> GroupValueCounts,
    IReadOnlyDictionary<string, int> GroupCimg45FirstKeyRelationCounts,
    IReadOnlyDictionary<string, int> GroupCimg45FirstKeyDeltaCounts,
    IReadOnlyDictionary<string, int> GroupCimg45FirstKeyPairCounts);

internal sealed record ColorAlphaSurvey(
    int ColorTrackCount,
    int ColorTrackKeyCount,
    int ColorTracksWithInitialChannel,
    int ColorTracksMissingInitialChannel,
    int ColorTrackInitialValueMatches,
    int ColorTrackInitialValueMismatches,
    int ColorTrackKeysInUnitRange,
    int ColorTrackKeysOutOfUnitRange,
    int ColorTrackKeysMissingValue,
    IReadOnlyDictionary<string, int> ColorTrackTypeCounts,
    IReadOnlyDictionary<string, int> ColorTrackKeyTypeCounts,
    IReadOnlyDictionary<string, int> ColorTrackInitialMatchTypeCounts,
    int AlphaOpacityTrackCount,
    int AlphaOpacityKeyCount,
    int AlphaOpacityTracksWithMaterialAlpha,
    int AlphaOpacityTracksMissingMaterialAlpha,
    int AlphaOpacityInitialAlphaMatches,
    int AlphaOpacityInitialAlphaMismatches,
    int AlphaOpacityCimgTargets,
    int AlphaOpacityDisplayFalseTargets,
    int AlphaOpacityKeysInUnitRange,
    int AlphaOpacityKeysOutOfUnitRange,
    int AlphaOpacityKeysMissingValue);

internal sealed record TransformTrackSurvey(
    int TrackCount,
    int KeyCount,
    int TracksWithInitialChannel,
    int TracksMissingInitialChannel,
    int InitialValueMatches,
    int InitialValueMismatches,
    int KeysMissingValue,
    IReadOnlyDictionary<string, int> TrackTypeCounts,
    IReadOnlyDictionary<string, int> KeyTypeCounts,
    IReadOnlyDictionary<string, int> StorageCounts,
    IReadOnlyDictionary<string, int> KeyValueKindCounts,
    IReadOnlyDictionary<string, int> InitialMatchTypeCounts,
    IReadOnlyDictionary<string, int> ValueRangeCounts,
    IReadOnlyDictionary<string, int> CandidateDefaultKeyCounts);

internal sealed record SharedPackedStateSurvey(
    IReadOnlyDictionary<string, int> OwnerCounts,
    IReadOnlyDictionary<string, int> OwnerRawCounts,
    IReadOnlyDictionary<string, int> OwnerBitCounts,
    IReadOnlyDictionary<string, int> OwnerLowNibbleCounts,
    IReadOnlyDictionary<string, int> OwnerMaskF0Counts,
    IReadOnlyDictionary<string, int> OwnerMaskF00Counts,
    IReadOnlyDictionary<string, int> OwnerUpperMaskCounts);

internal sealed record SurveySceneNameIndex(
    IReadOnlySet<string> DirectoryNames,
    IReadOnlySet<string> FileStems,
    IReadOnlySet<string> ScenePrefixes,
    IReadOnlySet<string> SceneSuffixes,
    IReadOnlyDictionary<string, IReadOnlySet<string>> FileStemsByDirectory,
    IReadOnlyDictionary<string, IReadOnlySet<string>> SceneSuffixesByDirectory);

internal sealed record CrfdReferenceSurvey(
    IReadOnlyDictionary<string, int> StringFieldRelationCounts,
    IReadOnlyDictionary<string, int> StringFieldTargetTypeCounts,
    IReadOnlyDictionary<string, int> Field90Field91RelationCounts,
    IReadOnlyDictionary<string, int> Field90Field91EqualityCounts,
    IReadOnlyDictionary<string, int> Field90Field91Field92RelationCounts);

internal sealed record CrfdReferenceContext(
    string OwnerDirectoryName,
    string OwnerFileStem,
    string OwnerScenePrefix,
    string? OwnerSceneSuffix,
    string? OwnerSceneName,
    SurveySceneNameIndex SceneNameIndex,
    IReadOnlySet<string> SiblingFileStems,
    IReadOnlySet<string> SiblingSceneSuffixes,
    IReadOnlySet<string> LocalTextureListNames,
    IReadOnlySet<string> LocalTextureNames,
    IReadOnlySet<string> LocalImageCastNames,
    IReadOnlySet<string> LocalCnumNames,
    IReadOnlySet<string> LocalSliceCastNames,
    IReadOnlySet<string> LocalNodeNames,
    IReadOnlySet<string> LocalCrefAtlasNames,
    IReadOnlySet<string> LocalCrefCropPaths);

internal sealed record CatrSurvey(
    IReadOnlyDictionary<string, int> Field03Counts,
    IReadOnlyDictionary<string, int> Field0DCounts,
    IReadOnlyDictionary<string, int> Field0ECounts,
    IReadOnlyDictionary<string, int> Field0FTypeCounts,
    IReadOnlyDictionary<string, int> Field0FPreviewCounts,
    IReadOnlyDictionary<string, int> FieldSequenceCounts,
    IReadOnlyDictionary<string, int> FieldSetCounts);

internal sealed record ProjectSurvey(
    IReadOnlyDictionary<string, int> Field00Counts,
    IReadOnlyDictionary<string, int> Field01Counts,
    IReadOnlyDictionary<string, int> Field05Counts,
    IReadOnlyDictionary<string, int> Field55Counts,
    IReadOnlyDictionary<string, int> Field56Counts,
    IReadOnlyDictionary<string, int> Field56TrackLastRelationCounts,
    IReadOnlyDictionary<string, int> Field56KeyMaxRelationCounts,
    IReadOnlyDictionary<string, int> Field56DeltaToTrackLastCounts,
    IReadOnlyDictionary<string, int> Field56DeltaToKeyMaxCounts,
    IReadOnlyDictionary<string, int> FieldSequenceCounts,
    IReadOnlyDictionary<string, int> FieldSetCounts);

internal sealed record ScnSurvey(
    IReadOnlyDictionary<string, int> NameCounts,
    IReadOnlyDictionary<string, int> Field04RawHexCounts,
    IReadOnlyDictionary<string, int> Field10Counts,
    IReadOnlyDictionary<string, int> Field11Counts,
    IReadOnlyDictionary<string, int> Field40Counts,
    IReadOnlyDictionary<string, int> Field41Counts,
    IReadOnlyDictionary<string, int> Field10Field11Counts,
    IReadOnlyDictionary<string, int> Field40Field41Counts,
    IReadOnlyDictionary<string, int> ParamLowLayerCountDeltaCounts,
    IReadOnlyDictionary<string, int> ParamLowField10DeltaCounts,
    IReadOnlyDictionary<string, int> Field10LayerCountDeltaCounts,
    IReadOnlyDictionary<string, int> FieldSequenceCounts,
    IReadOnlyDictionary<string, int> FieldSetCounts);

internal sealed record LayerSurvey(
    IReadOnlyDictionary<string, int> NameCounts,
    IReadOnlyDictionary<string, int> Field20Counts,
    IReadOnlyDictionary<string, int> Field20BitCounts,
    IReadOnlyDictionary<string, int> Field21Counts,
    IReadOnlyDictionary<string, int> Field21BitCounts,
    IReadOnlyDictionary<string, int> Field22Counts,
    IReadOnlyDictionary<string, int> Field22BitCounts,
    IReadOnlyDictionary<string, int> Field21SceneNodeCountDeltaCounts,
    IReadOnlyDictionary<string, int> ParamLowField22DeltaCounts,
    IReadOnlyDictionary<string, int> FieldSequenceCounts,
    IReadOnlyDictionary<string, int> FieldSetCounts);

internal sealed record CameraSurvey(
    IReadOnlyDictionary<string, int> NameCounts,
    IReadOnlyDictionary<string, int> Field12VectorCounts,
    IReadOnlyDictionary<string, int> Field13VectorCounts,
    IReadOnlyDictionary<string, int> Field14Counts,
    IReadOnlyDictionary<string, int> Field14BitCounts,
    IReadOnlyDictionary<string, int> Field15Counts,
    IReadOnlyDictionary<string, int> Field16Counts,
    IReadOnlyDictionary<string, int> FieldSequenceCounts,
    IReadOnlyDictionary<string, int> FieldSetCounts);

internal sealed record NcatSurvey(
    IReadOnlyDictionary<string, int> KindTypeByteCounts,
    IReadOnlyDictionary<string, int> KindCategoryCounts,
    IReadOnlyDictionary<string, int> TypeByteCategoryCounts,
    IReadOnlyDictionary<string, int> KindParameterPresenceCounts,
    IReadOnlyDictionary<string, int> ParameterStringCounts,
    IReadOnlyDictionary<string, int> ParameterFieldTypeCounts,
    IReadOnlyDictionary<string, int> KindParameterFieldTypeCounts,
    IReadOnlyDictionary<string, int> CategoryParameterFieldTypeCounts,
    IReadOnlyDictionary<string, int> ParameterFieldTypePreviewCounts,
    IReadOnlyDictionary<string, int> KindNodeFlagCounts,
    IReadOnlyDictionary<string, int> KindNodeFlagBitCounts,
    IReadOnlyDictionary<string, int> KindNodeGroupCounts,
    IReadOnlyDictionary<string, int> KindDisplayCounts,
    IReadOnlyDictionary<string, int> KindCimgTargetCounts,
    IReadOnlyDictionary<string, int> KindAnimatedNodeCounts);

internal sealed record VtbfStructureSurvey(
    IReadOnlyDictionary<string, int> TagCounts,
    IReadOnlyDictionary<string, int> TagParamRawCounts,
    IReadOnlyDictionary<string, int> TagParamLowHighCounts,
    IReadOnlyDictionary<string, int> TagPropertyCountCounts,
    IReadOnlyDictionary<string, int> TagParamHighPropertyCountCounts,
    IReadOnlyDictionary<string, int> TagTrailingByteCounts,
    IReadOnlyDictionary<string, int> KeyParamHighModulo5Counts,
    IReadOnlyDictionary<string, int> FieldDirectoryCounts,
    IReadOnlyDictionary<string, int> FieldDirectoryBlockCounts,
    IReadOnlyDictionary<string, int> FieldCountValueCounts,
    IReadOnlyDictionary<string, int> FieldStrideValueCounts);

internal sealed record AnimationMotionStructureSurvey(
    IReadOnlyDictionary<string, int> AnimationFieldSequenceCounts,
    IReadOnlyDictionary<string, int> AnimationFieldSetCounts,
    IReadOnlyDictionary<string, int> AnimationParamLowMotionDeltaCounts,
    IReadOnlyDictionary<string, int> AnimationField50MotionDeltaCounts,
    IReadOnlyDictionary<string, int> AnimationField50MaxMotionTrackDeltaCounts,
    IReadOnlyDictionary<string, int> AnimationField50MotionOrMaxTrackRelationCounts,
    IReadOnlyDictionary<string, int> AnimationParamLowField50DeltaCounts,
    IReadOnlyDictionary<string, int> AnimationField5FCounts,
    IReadOnlyDictionary<string, int> AnimationField5FMotionPresenceCounts,
    IReadOnlyDictionary<string, int> AnimationField5FAnimationNameCounts,
    IReadOnlyDictionary<string, int> AnimationField5FParamLowMotionDeltaCounts,
    IReadOnlyDictionary<string, int> AnimationField5FField50MotionDeltaCounts,
    IReadOnlyDictionary<string, int> AnimationField5FField50RelationCounts,
    IReadOnlyDictionary<string, int> AnimationField5FEndFrameRelationCounts,
    IReadOnlyDictionary<string, int> AnimationEndFrameRelationCounts,
    IReadOnlyDictionary<string, int> AnimationEndFrameDeltaToTrackLastCounts,
    IReadOnlyDictionary<string, int> AnimationEndFrameDeltaToKeyMaxCounts,
    IReadOnlyDictionary<string, int> MotionFieldSequenceCounts,
    IReadOnlyDictionary<string, int> MotionFieldSetCounts,
    IReadOnlyDictionary<string, int> MotionParamLowTrackDeltaCounts,
    IReadOnlyDictionary<string, int> MotionField52TrackDeltaCounts,
    IReadOnlyDictionary<string, int> MotionParamLowField52DeltaCounts,
    IReadOnlyDictionary<string, int> MotionTargetIndexRangeCounts);

internal sealed record CompactTailSurvey(
    IReadOnlyDictionary<string, int> CnumField48Counts,
    IReadOnlyDictionary<string, int> CnumFieldA0Counts,
    IReadOnlyDictionary<string, int> CnumFieldA1Field44Counts,
    IReadOnlyDictionary<string, int> CnumFieldA1CropReferenceCountCounts,
    IReadOnlyDictionary<string, int> CnumFieldA1ZeroMarkerFieldCounts,
    IReadOnlyDictionary<string, int> CnumFieldA1NodeFlagCounts,
    IReadOnlyDictionary<string, int> CnumFieldA1NodeGroupCounts,
    IReadOnlyDictionary<string, int> CnumFieldA1DisplayCounts,
    IReadOnlyDictionary<string, int> CnumFieldA1CimgTargetCounts,
    IReadOnlyDictionary<string, int> CnumFieldA1AnimatedTargetCounts,
    IReadOnlyDictionary<string, int> CnumFieldSequenceCounts,
    IReadOnlyDictionary<string, int> CnumFieldSetCounts,
    IReadOnlyDictionary<string, int> CnumFieldA1RawLengthCounts,
    IReadOnlyDictionary<string, int> CnumFieldA1ContentLengthCounts,
    IReadOnlyDictionary<string, int> CnumFieldA1Utf8StatusCounts,
    IReadOnlyDictionary<string, int> CnumFieldA1ShiftJisByteShapeCounts,
    IReadOnlyDictionary<string, int> CnumFieldA1RawPreviewCounts,
    IReadOnlyDictionary<string, int> TextField7AStringCounts,
    IReadOnlyDictionary<string, int> TextField7ARawLengthCounts,
    IReadOnlyDictionary<string, int> TextField7AContentLengthCounts,
    IReadOnlyDictionary<string, int> TextField7AUtf8StatusCounts,
    IReadOnlyDictionary<string, int> TextField7AShiftJisByteShapeCounts,
    IReadOnlyDictionary<string, int> TextField7AShiftJisDecodeStatusCounts,
    IReadOnlyDictionary<string, int> TextField7AShiftJisStringCounts,
    IReadOnlyDictionary<string, int> TextField7ARawPreviewCounts,
    IReadOnlyDictionary<string, int> TextField7AField41Counts,
    IReadOnlyDictionary<string, int> TextField7AField78Counts,
    IReadOnlyDictionary<string, int> TextField7AField79Counts,
    IReadOnlyDictionary<string, int> TextField7AField7CCounts,
    IReadOnlyDictionary<string, int> TextField33VectorCounts,
    IReadOnlyDictionary<string, int> TextField33RawHexCounts,
    IReadOnlyDictionary<string, int> TextField7BPackedValuesCounts,
    IReadOnlyDictionary<string, int> TextField7BRawHexCounts,
    IReadOnlyDictionary<string, int> TextField78Field79Counts,
    IReadOnlyDictionary<string, int> TextZeroMarkerField7ACounts,
    IReadOnlyDictionary<string, int> TextFieldSequenceCounts,
    IReadOnlyDictionary<string, int> TextFieldSetCounts,
    IReadOnlyDictionary<string, int> SliceCastField40Counts,
    IReadOnlyDictionary<string, int> SliceCastField41Counts,
    IReadOnlyDictionary<string, int> SliceCastField42Counts,
    IReadOnlyDictionary<string, int> SliceCastField43Counts,
    IReadOnlyDictionary<string, int> SliceCastField80Counts,
    IReadOnlyDictionary<string, int> SliceCastField81Counts,
    IReadOnlyDictionary<string, int> SliceCastField82Counts,
    IReadOnlyDictionary<string, int> SliceCastField84Counts,
    IReadOnlyDictionary<string, int> SliceCastField85Counts,
    IReadOnlyDictionary<string, int> SliceCastField86Counts,
    IReadOnlyDictionary<string, int> SliceCastField87Counts,
    IReadOnlyDictionary<string, int> SliceCastTargetNodeFlagCounts,
    IReadOnlyDictionary<string, int> SliceCastTargetNodeGroupCounts,
    IReadOnlyDictionary<string, int> SliceCastTargetDisplayCounts,
    IReadOnlyDictionary<string, int> SliceCastTargetCimgTargetCounts,
    IReadOnlyDictionary<string, int> SliceCastFieldSequenceCounts,
    IReadOnlyDictionary<string, int> SliceCastFieldSetCounts,
    IReadOnlyDictionary<string, int> SliceRecordField40Counts,
    IReadOnlyDictionary<string, int> SliceRecordField41Counts,
    IReadOnlyDictionary<string, int> SliceRecordField45Counts,
    IReadOnlyDictionary<string, int> SliceRecordField37ColorCounts,
    IReadOnlyDictionary<string, int> SliceRecordField38ColorCounts,
    IReadOnlyDictionary<string, int> SliceRecordField39ColorCounts,
    IReadOnlyDictionary<string, int> SliceRecordField39ColorCountCounts,
    IReadOnlyDictionary<string, int> SliceRecordField83Field40Counts,
    IReadOnlyDictionary<string, int> SliceRecordField83Field41Counts,
    IReadOnlyDictionary<string, int> SliceRecordField83Field45Counts,
    IReadOnlyDictionary<string, int> SliceRecordFieldSequenceCounts,
    IReadOnlyDictionary<string, int> SliceRecordFieldSetCounts,
    IReadOnlyDictionary<string, int> SliceRecordShapeCounts);

internal sealed record NodeFlagBitSurvey(
    IReadOnlyDictionary<string, int> DisplayFalseNodeCounts,
    IReadOnlyDictionary<string, int> CimgTargetNodeCounts,
    IReadOnlyDictionary<string, int> AnimatedNodeCounts,
    IReadOnlyDictionary<string, int> DataNodeCounts,
    IReadOnlyDictionary<string, int> CategoryRecordNodeCounts,
    IReadOnlyDictionary<string, int> CategoryNonZeroNodeCounts,
    IReadOnlyDictionary<string, int> ExactFlagCounts,
    IReadOnlyDictionary<string, int> GroupCounts,
    IReadOnlyDictionary<string, int> ImageCastFlagBitCounts,
    IReadOnlyDictionary<string, int> TrackTypeCounts,
    IReadOnlyDictionary<string, int> PairCounts);

internal sealed record TrackFlagExtraSurvey(
    IReadOnlyDictionary<string, int> BaseCounts,
    IReadOnlyDictionary<string, int> TrackTypeCounts,
    IReadOnlyDictionary<string, int> KeyValueTypeCounts,
    IReadOnlyDictionary<string, int> NodeFlagCounts,
    IReadOnlyDictionary<string, int> NodeFlagBitCounts,
    IReadOnlyDictionary<string, int> GroupCounts,
    IReadOnlyDictionary<string, int> CimgTargetCounts,
    IReadOnlyDictionary<string, int> InitialDisplayCounts,
    IReadOnlyDictionary<string, int> CimgFlagCounts,
    IReadOnlyDictionary<string, int> CimgFlagBitCounts,
    IReadOnlyDictionary<string, int> CimgReferenceCountCounts,
    IReadOnlyDictionary<string, int> AnimationCounts);

internal sealed record KeyInterpolationTangentSurvey(
    IReadOnlyDictionary<string, int> InterpolationCounts,
    IReadOnlyDictionary<string, int> InterpolationTrackTypeCounts,
    IReadOnlyDictionary<string, int> InterpolationKeyValueTypeCounts,
    IReadOnlyDictionary<string, int> TangentPresentInterpolationCounts,
    IReadOnlyDictionary<string, int> TangentPresentTrackTypeCounts,
    IReadOnlyDictionary<string, int> TangentNonZeroInterpolationCounts,
    IReadOnlyDictionary<string, int> TangentNonZeroTrackTypeCounts,
    IReadOnlyDictionary<string, int> TangentMismatchInterpolationCounts,
    IReadOnlyDictionary<string, int> TangentMismatchTrackTypeCounts,
    IReadOnlyDictionary<string, int> TangentMismatchAnimationCounts,
    IReadOnlyDictionary<string, int> TangentMismatchNodeFlagCounts,
    IReadOnlyDictionary<string, int> TangentMismatchGroupCounts,
    IReadOnlyDictionary<string, int> TangentMismatchTrackExtraCounts,
    IReadOnlyDictionary<string, int> TangentMismatchTangentPairCounts,
    IReadOnlyDictionary<string, int> TangentNonZeroFramePositionCounts,
    IReadOnlyDictionary<string, int> TangentMismatchFramePositionCounts,
    IReadOnlyDictionary<string, int> TangentDeltaSignCounts);

internal sealed record TrackKeyStructureSurvey(
    IReadOnlyDictionary<string, int> StorageMatrixCounts,
    IReadOnlyDictionary<string, int> TrackFieldSequenceCounts,
    IReadOnlyDictionary<string, int> KeyFieldSequenceCounts,
    IReadOnlyDictionary<string, int> FrameRangeRelationCounts,
    IReadOnlyDictionary<string, int> KeyFrameOrderCounts,
    IReadOnlyDictionary<string, int> KeyFrameDuplicateCounts,
    IReadOnlyDictionary<string, int> FirstFrameDeltaCounts,
    IReadOnlyDictionary<string, int> LastFrameDeltaCounts);

internal sealed record CimgFlagBitSurvey(
    IReadOnlyDictionary<string, int> DisplayFalseCounts,
    IReadOnlyDictionary<string, int> MultiReferenceCounts,
    IReadOnlyDictionary<string, int> SecondaryReferenceCounts,
    IReadOnlyDictionary<string, int> NonZeroReferenceIndexCounts,
    IReadOnlyDictionary<string, int> MissingNodeCounts,
    IReadOnlyDictionary<string, int> NodeFlagCounts,
    IReadOnlyDictionary<string, int> GroupCounts,
    IReadOnlyDictionary<string, int> PairCounts);
