internal static partial class CliApp
{
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
}
