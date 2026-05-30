using System.Buffers.Binary;
using System.Text;
using SbScene.Core.Images;

namespace SbScene.Core.Resources;

public static class SvoResourceParser
{
    private const int AvtsHeaderSize = 0x80;
    private const int AvtsDirectoryTableOffset = 0x80;
    private const int AvtsDirectoryEntrySize = 0x400;
    private const int AvtsDirectoryEntryKnownSize = 0x210;

    public static SvoHeaderInfo ParseHeaderFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return ParseHeader(File.ReadAllBytes(path));
    }

    public static IReadOnlyList<SvoTextureResource> ParseFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Parse(File.ReadAllBytes(path));
    }

    public static IReadOnlyList<SvoDirectoryEntry> ParseDirectoryFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return ParseDirectory(File.ReadAllBytes(path));
    }

    public static SvoMetadataInfo? ParseMetadataFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return ParseMetadata(File.ReadAllBytes(path));
    }

    public static SvoHeaderInfo ParseHeader(byte[] data)
    {
        ValidateAvts(data);
        if (data.Length < AvtsHeaderSize)
        {
            throw new InvalidDataException("SVO resource file is too small for an AVTS header.");
        }

        var unknownBytes = data.AsSpan(8, AvtsHeaderSize - 8);
        var words = new List<SvoHeaderUnknownWord>((AvtsHeaderSize - 8) / 4);
        for (var offset = 8; offset <= AvtsHeaderSize - 4; offset += 4)
        {
            words.Add(new SvoHeaderUnknownWord
            {
                Offset = offset,
                Value = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, 4)),
            });
        }

        return new SvoHeaderInfo
        {
            Magic = Encoding.ASCII.GetString(data.AsSpan(0, 4)),
            DirectoryCount = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(4, 4)),
            HeaderSize = AvtsHeaderSize,
            DirectoryTableOffset = AvtsDirectoryTableOffset,
            DirectoryEntrySize = AvtsDirectoryEntrySize,
            HeaderUnknownNonZeroByteCount = CountNonZeroBytes(unknownBytes),
            HeaderUnknownNonZeroByteOffsets = FindNonZeroOffsets(data, 8, AvtsHeaderSize - 8).ToArray(),
            UnknownWords = words,
        };
    }

    public static IReadOnlyList<SvoTextureResource> Parse(byte[] data)
    {
        ValidateAvts(data);
        var directory = ParseDirectory(data);
        var ddsEntries = directory.Where(static entry => entry.IsDds).ToArray();
        if (ddsEntries.Length > 0)
        {
            return ParseDirectoryDdsEntries(data, ddsEntries);
        }

        return ParseDdsMagicFallback(data);
    }

    public static IReadOnlyList<SvoDirectoryEntry> ParseDirectory(byte[] data)
    {
        ValidateAvts(data);
        if (data.Length < AvtsHeaderSize)
        {
            return Array.Empty<SvoDirectoryEntry>();
        }

        var count = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(4, 4));
        if (count <= 0 || count > 1024)
        {
            return Array.Empty<SvoDirectoryEntry>();
        }

        var entries = new List<SvoDirectoryEntry>(count);
        for (var i = 0; i < count; i++)
        {
            var entryOffset = AvtsDirectoryTableOffset + i * AvtsDirectoryEntrySize;
            if (entryOffset + AvtsDirectoryEntryKnownSize > data.Length)
            {
                break;
            }

            var name = ReadNullTerminatedAscii(data, entryOffset, 0x200);
            var kind = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(entryOffset + 0x200, 4));
            var sequence = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(entryOffset + 0x204, 4));
            var dataLength = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(entryOffset + 0x208, 4));
            var dataOffset = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(entryOffset + 0x20C, 4));
            var isInBounds = dataOffset <= int.MaxValue
                && dataLength <= int.MaxValue
                && dataOffset + dataLength <= data.Length;
            var magic = isInBounds && dataLength >= 4
                ? Encoding.ASCII.GetString(data.AsSpan((int)dataOffset, 4))
                : null;
            var reservedLength = Math.Min(AvtsDirectoryEntrySize - AvtsDirectoryEntryKnownSize, data.Length - (entryOffset + AvtsDirectoryEntryKnownSize));
            var reservedNonZeroOffsets = reservedLength > 0
                ? FindNonZeroOffsets(data, entryOffset + AvtsDirectoryEntryKnownSize, reservedLength, entryOffset).ToArray()
                : [];

            entries.Add(new SvoDirectoryEntry
            {
                Index = i,
                EntryOffset = entryOffset,
                Name = name,
                Kind = kind <= int.MaxValue ? (int)kind : -1,
                Sequence = sequence <= int.MaxValue ? (int)sequence : -1,
                DataOffset = dataOffset,
                DataLength = dataLength <= int.MaxValue ? (int)dataLength : -1,
                IsInBounds = isInBounds,
                IsDds = isInBounds && magic == "DDS ",
                DataMagic = magic,
                ReservedNonZeroByteCount = reservedNonZeroOffsets.Length,
                ReservedNonZeroByteOffsets = reservedNonZeroOffsets,
            });
        }

        return entries;
    }

    public static SvoMetadataInfo? ParseMetadata(byte[] data)
    {
        var directory = ParseDirectory(data);
        var entry = directory.FirstOrDefault(static entry => entry.DataMagic == "YABX" && entry.IsInBounds);
        if (entry is null || entry.DataLength < 16 || entry.DataOffset > int.MaxValue)
        {
            return null;
        }

        var offset = checked((int)entry.DataOffset);
        var payload = data.AsSpan(offset, entry.DataLength);
        var strings = ExtractAsciiStrings(data, offset, entry.DataLength).ToArray();
        var typeNames = strings
            .Select(static item => item.Text)
            .Where(static text => text.Contains("::", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var fieldNames = strings
            .Select(static item => item.Text)
            .Where(static text => text.StartsWith('_') && !text.StartsWith("__", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var resourceNames = strings
            .Select(static item => item.Text)
            .Where(static text =>
                text.StartsWith("MM_CH_", StringComparison.Ordinal)
                || text.StartsWith("__HmfToSvo__", StringComparison.Ordinal)
                || text.EndsWith(".dds", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var typeSchemas = BuildTypeSchemas(strings, payload, entry.DataOffset);
        var objectSection = ParseObjectSection(payload, entry.DataOffset, typeSchemas);
        var objectReferenceBase = InferObjectReferenceBase(objectSection?.Objects ?? []);
        var objects = EnrichObjectReferences(objectSection?.Objects ?? [], objectReferenceBase);

        return new SvoMetadataInfo
        {
            DirectoryIndex = entry.Index,
            Offset = entry.DataOffset,
            Length = entry.DataLength,
            Magic = Encoding.ASCII.GetString(payload[..4]),
            Version = BinaryPrimitives.ReadInt32LittleEndian(payload[4..8]),
            DeclaredPayloadLength = BinaryPrimitives.ReadInt32LittleEndian(payload[8..12]),
            HeaderHashCandidate = BinaryPrimitives.ReadUInt32LittleEndian(payload[12..16]),
            Strings = strings,
            TypeNames = typeNames,
            TypeSchemas = typeSchemas,
            ObjectSectionOffset = objectSection?.Offset,
            DeclaredObjectCount = objectSection?.DeclaredCount,
            ObjectReferenceBase = objectReferenceBase,
            Objects = objects,
            FieldNames = fieldNames,
            ResourceNames = resourceNames,
            Resources = BuildMetadataResources(data, directory, resourceNames, objects),
        };
    }

    private static IReadOnlyList<SvoTextureResource> ParseDirectoryDdsEntries(byte[] data, IReadOnlyList<SvoDirectoryEntry> ddsEntries)
    {
        var resources = new List<SvoTextureResource>(ddsEntries.Count);
        for (var i = 0; i < ddsEntries.Count; i++)
        {
            var entry = ddsEntries[i];
            var offset = checked((int)entry.DataOffset);
            var header = data.AsSpan(offset);
            var height = BinaryPrimitives.ReadInt32LittleEndian(header[12..16]);
            var width = BinaryPrimitives.ReadInt32LittleEndian(header[16..20]);
            var format = DdsDecoder.GetFormatName(header);
            var dds = new byte[entry.DataLength];
            Buffer.BlockCopy(data, offset, dds, 0, entry.DataLength);

            resources.Add(new SvoTextureResource
            {
                Index = i,
                DirectoryIndex = entry.Index,
                FileName = entry.Name,
                AtlasName = NormalizeAtlasName(entry.Name),
                Offset = entry.DataOffset,
                Length = entry.DataLength,
                Width = width,
                Height = height,
                Format = format,
                DdsBytes = dds,
            });
        }

        return resources;
    }

    private static IReadOnlyList<SvoTextureResource> ParseDdsMagicFallback(byte[] data)
    {
        var names = ParseNameTable(data);
        var ddsOffsets = FindDdsOffsets(data).ToArray();
        if (ddsOffsets.Length == 0)
        {
            throw new InvalidDataException("No DDS payloads found in SVO resource file.");
        }

        var resources = new List<SvoTextureResource>(ddsOffsets.Length);
        for (var i = 0; i < ddsOffsets.Length; i++)
        {
            var offset = ddsOffsets[i];
            var nextOffset = i + 1 < ddsOffsets.Length ? ddsOffsets[i + 1] : data.Length;
            var header = data.AsSpan(offset);
            var height = BinaryPrimitives.ReadInt32LittleEndian(header[12..16]);
            var width = BinaryPrimitives.ReadInt32LittleEndian(header[16..20]);
            var format = DdsDecoder.GetFormatName(header);
            var length = nextOffset - offset;
            var dds = new byte[length];
            Buffer.BlockCopy(data, offset, dds, 0, length);

            resources.Add(new SvoTextureResource
            {
                Index = i,
                DirectoryIndex = i + 1,
                FileName = i + 1 < names.Count ? names[i + 1] : null,
                AtlasName = i + 1 < names.Count ? NormalizeAtlasName(names[i + 1]) : null,
                Offset = offset,
                Length = length,
                Width = width,
                Height = height,
                Format = format,
                DdsBytes = dds,
            });
        }

        return resources;
    }

    private static void ValidateAvts(byte[] data)
    {
        if (data.Length < 4 || Encoding.ASCII.GetString(data.AsSpan(0, 4)) != "AVTS")
        {
            throw new InvalidDataException("SVO resource file must start with 'AVTS'.");
        }
    }

    private static IReadOnlyList<string> ParseNameTable(byte[] data)
    {
        if (data.Length < AvtsHeaderSize)
        {
            return Array.Empty<string>();
        }

        var count = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(4, 4));
        if (count <= 0 || count > 1024)
        {
            return Array.Empty<string>();
        }

        var names = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            var offset = AvtsDirectoryTableOffset + i * AvtsDirectoryEntrySize;
            if (offset >= data.Length)
            {
                break;
            }

            names.Add(ReadNullTerminatedAscii(data, offset, 0x200));
        }

        return names;
    }

    private static string ReadNullTerminatedAscii(byte[] data, int offset, int maxLength)
    {
        var length = 0;
        while (offset + length < data.Length && data[offset + length] != 0 && length < maxLength)
        {
            length++;
        }

        return length == 0 ? string.Empty : Encoding.ASCII.GetString(data, offset, length);
    }

    private static int CountNonZeroBytes(ReadOnlySpan<byte> data)
    {
        var count = 0;
        foreach (var value in data)
        {
            if (value != 0)
            {
                count++;
            }
        }

        return count;
    }

    private static IEnumerable<int> FindNonZeroOffsets(byte[] data, int offset, int length, int relativeBase = 0)
    {
        var end = Math.Min(data.Length, offset + length);
        for (var cursor = offset; cursor < end; cursor++)
        {
            if (data[cursor] != 0)
            {
                yield return cursor - relativeBase;
            }
        }
    }

    private static IReadOnlyList<SvoMetadataTypeInfo> BuildTypeSchemas(
        IReadOnlyList<SvoMetadataString> strings,
        ReadOnlySpan<byte> payload,
        long absoluteBase)
    {
        var firstResourceOffset = strings
            .Where(static item => item.Text.StartsWith("MM_CH_", StringComparison.Ordinal))
            .Select(static item => (int?)item.Offset)
            .FirstOrDefault() ?? int.MaxValue;
        var schemaStartOffset = strings
            .Where(static item => item.Text == "yabukita::Object")
            .Select(static item => (int?)item.Offset)
            .FirstOrDefault() ?? 0;
        var schemas = new List<SvoMetadataTypeInfo>();
        string? currentType = null;
        int? currentTypeIndex = null;
        var nextTypeIndex = 1;
        var fields = new List<SvoMetadataFieldInfo>();

        foreach (var item in strings.Where(item => item.Offset >= schemaStartOffset && item.Offset < firstResourceOffset))
        {
            if (item.Text.Contains("::", StringComparison.Ordinal))
            {
                Flush();
                currentType = item.Text;
                currentTypeIndex = nextTypeIndex++;
                continue;
            }

            if (currentType is not null
                && item.Text.StartsWith('_')
                && !item.Text.StartsWith("__", StringComparison.Ordinal)
                && !fields.Any(field => field.Name == item.Text))
            {
                fields.Add(ReadFieldInfo(payload, item, absoluteBase));
            }
        }

        Flush();
        return schemas;

        void Flush()
        {
            if (currentType is null)
            {
                return;
            }

            schemas.Add(new SvoMetadataTypeInfo
            {
                TypeIndex = currentTypeIndex,
                Name = currentType,
                Fields = fields.Select(static field => field.Name).ToArray(),
                FieldDescriptors = fields.ToArray(),
            });
            fields.Clear();
        }
    }

    private static SvoMetadataFieldInfo ReadFieldInfo(ReadOnlySpan<byte> payload, SvoMetadataString item, long absoluteBase)
    {
        var descriptorOffset = item.Offset + item.Text.Length + 1;
        byte flags = 0;
        byte valueKind = 0;
        byte reserved = 0;
        var rawDescriptorHex = string.Empty;
        if (descriptorOffset + 3 <= payload.Length)
        {
            flags = payload[descriptorOffset];
            valueKind = payload[descriptorOffset + 1];
            reserved = payload[descriptorOffset + 2];
            rawDescriptorHex = Convert.ToHexString(payload.Slice(descriptorOffset, 3)).ToLowerInvariant();
        }

        return new SvoMetadataFieldInfo
        {
            Name = item.Text,
            Offset = item.Offset,
            DescriptorOffset = descriptorOffset,
            AbsoluteDescriptorOffset = absoluteBase + descriptorOffset,
            RawDescriptorHex = rawDescriptorHex,
            Flags = flags,
            ValueKind = valueKind,
            Reserved = reserved,
            ValueKindName = DescribeYabxFieldKind(flags, valueKind),
        };
    }

    private static string DescribeYabxFieldKind(byte flags, byte valueKind)
    {
        return valueKind switch
        {
            0x00 => "OwnerDependent",
            0x02 => "ObjectReference",
            0x04 => "Int32Like",
            _ => $"Unknown0x{valueKind:X2}",
        };
    }

    private static SvoMetadataObjectSection? ParseObjectSection(
        ReadOnlySpan<byte> payload,
        long absoluteBase,
        IReadOnlyList<SvoMetadataTypeInfo> typeSchemas)
    {
        var maxTypeIndex = typeSchemas.Select(static schema => schema.TypeIndex ?? 0).DefaultIfEmpty().Max();
        SvoMetadataObjectSection? best = null;
        var bestEnd = 0;

        for (var offset = 12; offset <= payload.Length - 8; offset++)
        {
            var declaredCount = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(offset, 2));
            if (declaredCount <= 0 || declaredCount > 512)
            {
                continue;
            }

            var cursor = offset + 2;
            var records = new List<SvoMetadataObject>(declaredCount);
            var valid = true;

            for (var i = 0; i < declaredCount; i++)
            {
                if (cursor + 6 > payload.Length)
                {
                    valid = false;
                    break;
                }

                var typeIndex = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(cursor, 2));
                var payloadLength = BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(cursor + 2, 4));
                if (typeIndex <= 0
                    || typeIndex > maxTypeIndex
                    || payloadLength > int.MaxValue
                    || cursor + 6 + payloadLength > payload.Length)
                {
                    valid = false;
                    break;
                }

                var length = (int)payloadLength;
                var payloadOffset = cursor + 6;
                var objectPayload = payload.Slice(payloadOffset, length);
                var schema = typeSchemas.FirstOrDefault(schema => schema.TypeIndex == typeIndex);
                var objectFields = ParseObjectFields(objectPayload, schema);
                var coverage = CalculateFieldCoverage(objectPayload, objectFields);
                records.Add(new SvoMetadataObject
                {
                    Index = i,
                    Offset = cursor,
                    AbsoluteOffset = absoluteBase + cursor,
                    PayloadOffset = payloadOffset,
                    TypeIndex = typeIndex,
                    TypeName = schema?.Name,
                    PayloadLength = length,
                    ParsedFieldByteCount = coverage.ParsedByteCount,
                    UnparsedByteCount = coverage.UnparsedByteCount,
                    UnparsedBytesPreviewHex = coverage.UnparsedBytesPreviewHex,
                    Strings = ExtractObjectStrings(objectPayload),
                    Fields = objectFields,
                });

                cursor = payloadOffset + length;
            }

            if (!valid || !payload[cursor..].ToArray().All(static value => value == 0))
            {
                continue;
            }

            if (cursor > bestEnd)
            {
                best = new SvoMetadataObjectSection(offset, declaredCount, records);
                bestEnd = cursor;
            }
        }

        return best;
    }

    private static int? InferObjectReferenceBase(IReadOnlyList<SvoMetadataObject> objects)
    {
        if (objects.Count == 0)
        {
            return null;
        }

        var refs = new List<int>();
        foreach (var field in objects.SelectMany(static obj => obj.Fields))
        {
            if (field.ReferenceIds is not null)
            {
                refs.AddRange(field.ReferenceIds.Where(static referenceId => referenceId != 0));
            }
            else if (field.ReferenceId is > 0)
            {
                refs.Add(field.ReferenceId.Value);
            }
        }

        var distinctRefs = refs.Distinct().Order().ToArray();
        if (distinctRefs.Length == 0)
        {
            return null;
        }

        var maxRef = distinctRefs[^1];
        var baseCandidate = maxRef - (objects.Count - 1);
        if (distinctRefs.All(referenceId =>
            {
                var index = referenceId - baseCandidate;
                return index >= 0 && index < objects.Count;
            }))
        {
            return baseCandidate;
        }

        var minRef = distinctRefs[0];
        return distinctRefs.All(referenceId =>
            {
                var index = referenceId - minRef;
                return index >= 0 && index < objects.Count;
            })
            ? minRef
            : null;
    }

    private static IReadOnlyList<SvoMetadataObject> EnrichObjectReferences(
        IReadOnlyList<SvoMetadataObject> objects,
        int? referenceBase)
    {
        if (referenceBase is null)
        {
            return objects;
        }

        return objects.Select(obj => new SvoMetadataObject
        {
            Index = obj.Index,
            ReferenceId = referenceBase + obj.Index,
            Offset = obj.Offset,
            AbsoluteOffset = obj.AbsoluteOffset,
            PayloadOffset = obj.PayloadOffset,
            TypeIndex = obj.TypeIndex,
            TypeName = obj.TypeName,
            PayloadLength = obj.PayloadLength,
            ParsedFieldByteCount = obj.ParsedFieldByteCount,
            UnparsedByteCount = obj.UnparsedByteCount,
            UnparsedBytesPreviewHex = obj.UnparsedBytesPreviewHex,
            Strings = obj.Strings,
            Fields = EnrichObjectFieldReferences(obj.Fields, objects, referenceBase.Value),
        }).ToArray();
    }

    private static IReadOnlyList<SvoMetadataObjectField> EnrichObjectFieldReferences(
        IReadOnlyList<SvoMetadataObjectField> fields,
        IReadOnlyList<SvoMetadataObject> objects,
        int referenceBase)
    {
        return fields.Select(field =>
        {
            var referenceTarget = ResolveReferenceTarget(field.ReferenceId, objects, referenceBase);
            var referenceTargets = field.ReferenceIds is null
                ? null
                : field.ReferenceIds.Select(referenceId => ResolveReferenceTarget(referenceId, objects, referenceBase) ?? new SvoMetadataReferenceTarget
                {
                    ReferenceId = referenceId,
                    ObjectIndex = null,
                    TypeName = null,
                }).ToArray();

            return new SvoMetadataObjectField
            {
                Name = field.Name,
                Offset = field.Offset,
                Length = field.Length,
                Kind = field.Kind,
                IntValue = field.IntValue,
                StringValue = field.StringValue,
                ReferenceId = field.ReferenceId,
                ReferenceTargetObjectIndex = referenceTarget?.ObjectIndex,
                ReferenceTargetTypeName = referenceTarget?.TypeName,
                ReferenceIds = field.ReferenceIds,
                ReferenceTargets = referenceTargets,
                Capacity = field.Capacity,
                StringLengthWithNull = field.StringLengthWithNull,
            };
        }).ToArray();
    }

    private static SvoMetadataReferenceTarget? ResolveReferenceTarget(
        int? referenceId,
        IReadOnlyList<SvoMetadataObject> objects,
        int referenceBase)
    {
        if (referenceId is null or 0)
        {
            return null;
        }

        var index = referenceId.Value - referenceBase;
        var target = index >= 0 && index < objects.Count ? objects[index] : null;
        return new SvoMetadataReferenceTarget
        {
            ReferenceId = referenceId.Value,
            ObjectIndex = target?.Index,
            TypeName = target?.TypeName,
        };
    }

    private static IReadOnlyList<SvoMetadataObjectString> ExtractObjectStrings(ReadOnlySpan<byte> payload)
    {
        var strings = new List<SvoMetadataObjectString>();
        var cursor = 0;
        while (cursor < payload.Length)
        {
            if (payload[cursor] is < 0x20 or > 0x7E)
            {
                cursor++;
                continue;
            }

            var start = cursor;
            while (cursor < payload.Length && payload[cursor] is >= 0x20 and <= 0x7E)
            {
                cursor++;
            }

            var length = cursor - start;
            if (length >= 3)
            {
                strings.Add(new SvoMetadataObjectString
                {
                    Offset = start,
                    Text = Encoding.ASCII.GetString(payload.Slice(start, length)),
                });
            }
        }

        return strings;
    }

    private static IReadOnlyList<SvoMetadataObjectField> ParseObjectFields(
        ReadOnlySpan<byte> payload,
        SvoMetadataTypeInfo? schema)
    {
        return schema?.Name switch
        {
            "stevia::Database" => ParseDatabaseObjectFields(payload),
            "stevia::VertexDeclaration" => ParseVertexDeclarationObjectFields(payload),
            "stevia::Texture" => ParseTextureObjectFields(payload),
            "stevia::Image" => ParseImageObjectFields(payload),
            "stevia::VertexElement" => ParseVertexElementObjectFields(payload),
            _ => [],
        };
    }

    private static YabxObjectFieldCoverage CalculateFieldCoverage(
        ReadOnlySpan<byte> payload,
        IReadOnlyList<SvoMetadataObjectField> fields)
    {
        if (payload.Length == 0)
        {
            return new YabxObjectFieldCoverage(0, 0, null);
        }

        var covered = new bool[payload.Length];
        foreach (var field in fields)
        {
            var start = Math.Clamp(field.Offset, 0, payload.Length);
            var end = Math.Clamp(field.Offset + field.Length, 0, payload.Length);
            for (var i = start; i < end; i++)
            {
                covered[i] = true;
            }
        }

        var parsed = covered.Count(static value => value);
        var unparsed = payload.Length - parsed;
        if (unparsed == 0)
        {
            return new YabxObjectFieldCoverage(parsed, 0, null);
        }

        var preview = new List<byte>(16);
        for (var i = 0; i < payload.Length && preview.Count < 16; i++)
        {
            if (!covered[i])
            {
                preview.Add(payload[i]);
            }
        }

        return new YabxObjectFieldCoverage(parsed, unparsed, Convert.ToHexString(preview.ToArray()).ToLowerInvariant());
    }

    private static IReadOnlyList<SvoMetadataObjectField> ParseDatabaseObjectFields(ReadOnlySpan<byte> payload)
    {
        var fields = new List<SvoMetadataObjectField>();
        var cursor = 0;
        foreach (var name in new[]
        {
            "_state",
            "_mesh",
            "_batch",
            "_vertexBuffer",
            "_indexBuffer",
            "_vertexDeclaration",
            "_texture",
            "_image",
            "_tree",
        })
        {
            if (!TryReadReferenceListField(payload, name, ref cursor, fields))
            {
                return fields;
            }
        }

        ParseResourceBaseFields(payload, ref cursor, fields);
        return fields;
    }

    private static IReadOnlyList<SvoMetadataObjectField> ParseVertexDeclarationObjectFields(ReadOnlySpan<byte> payload)
    {
        var fields = new List<SvoMetadataObjectField>();
        var cursor = 0;
        _ = TryReadReferenceListField(payload, "_vertexElement", ref cursor, fields);
        ParseResourceBaseFields(payload, ref cursor, fields);
        return fields;
    }

    private static IReadOnlyList<SvoMetadataObjectField> ParseTextureObjectFields(ReadOnlySpan<byte> payload)
    {
        var fields = new List<SvoMetadataObjectField>();
        var cursor = 0;
        foreach (var name in new[]
        {
            "_wrapU",
            "_wrapV",
            "_minFilter",
            "_magFilter",
            "_mipFilter",
            "_anisoNumber",
            "_lodBias",
            "_id",
            "_uvSetIndex",
        })
        {
            if (!TryReadInt32Field(payload, name, ref cursor, fields))
            {
                return fields;
            }
        }

        if (!TryReadStringField(payload, "_uvSetName", ref cursor, fields)
            || !TryReadStringField(payload, "_attributeName", ref cursor, fields)
            || !TryReadStringField(payload, "_textureType", ref cursor, fields)
            || !TryReadReferenceField(payload, "_image", ref cursor, fields))
        {
            return fields;
        }

        ParseResourceBaseFields(payload, ref cursor, fields);
        return fields;
    }

    private static IReadOnlyList<SvoMetadataObjectField> ParseImageObjectFields(ReadOnlySpan<byte> payload)
    {
        var fields = new List<SvoMetadataObjectField>();
        var cursor = 0;
        foreach (var name in new[] { "_height", "_width", "_maxMipmapLevel", "_format" })
        {
            if (!TryReadInt32Field(payload, name, ref cursor, fields))
            {
                return fields;
            }
        }

        if (!TryReadStringField(payload, "_compressCustomOption", ref cursor, fields)
            || !TryReadInt32Field(payload, "_alphaMode", ref cursor, fields)
            || !TryReadStringField(payload, "_fileName", ref cursor, fields)
            || !TryReadStringField(payload, "_chunkFileName", ref cursor, fields)
            || !TryReadReferenceField(payload, "_file", ref cursor, fields)
            || !TryReadStringField(payload, "_mipmapFileName", ref cursor, fields)
            || !TryReadInt32Field(payload, "_dataSize", ref cursor, fields))
        {
            return fields;
        }

        ParseResourceBaseFields(payload, ref cursor, fields);
        return fields;
    }

    private static IReadOnlyList<SvoMetadataObjectField> ParseVertexElementObjectFields(ReadOnlySpan<byte> payload)
    {
        var fields = new List<SvoMetadataObjectField>();
        var cursor = 0;
        foreach (var name in new[] { "_semantics", "_elementType", "_index" })
        {
            if (!TryReadInt32Field(payload, name, ref cursor, fields))
            {
                return fields;
            }
        }

        return fields;
    }

    private static void ParseResourceBaseFields(
        ReadOnlySpan<byte> payload,
        ref int cursor,
        ICollection<SvoMetadataObjectField> fields)
    {
        _ = TryReadStringField(payload, "_name", ref cursor, fields);
        _ = TryReadInt32Field(payload, "_flag", ref cursor, fields);
        _ = TryReadStringField(payload, "_fullName", ref cursor, fields);
        _ = TryReadStringField(payload, "_userParameter", ref cursor, fields);
    }

    private static bool TryReadInt32Field(
        ReadOnlySpan<byte> payload,
        string name,
        ref int cursor,
        ICollection<SvoMetadataObjectField> fields)
    {
        if (cursor + 4 > payload.Length)
        {
            return false;
        }

        var offset = cursor;
        var value = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(cursor, 4));
        cursor += 4;
        fields.Add(new SvoMetadataObjectField
        {
            Name = name,
            Offset = offset,
            Length = 4,
            Kind = "Int32",
            IntValue = value,
        });
        return true;
    }

    private static bool TryReadReferenceField(
        ReadOnlySpan<byte> payload,
        string name,
        ref int cursor,
        ICollection<SvoMetadataObjectField> fields)
    {
        if (cursor + 2 > payload.Length)
        {
            return false;
        }

        var offset = cursor;
        var value = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(cursor, 2));
        cursor += 2;
        fields.Add(new SvoMetadataObjectField
        {
            Name = name,
            Offset = offset,
            Length = 2,
            Kind = "ObjectReferenceId",
            ReferenceId = value,
        });
        return true;
    }

    private static bool TryReadReferenceListField(
        ReadOnlySpan<byte> payload,
        string name,
        ref int cursor,
        ICollection<SvoMetadataObjectField> fields)
    {
        if (cursor + 8 > payload.Length)
        {
            return false;
        }

        var offset = cursor;
        var byteLength = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(cursor, 4));
        var count = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(cursor + 4, 4));
        if (byteLength < 4
            || count < 0
            || byteLength != 4 + count * 2
            || cursor + 4 + byteLength > payload.Length)
        {
            return false;
        }

        var refs = new List<int>(count);
        var refCursor = cursor + 8;
        for (var i = 0; i < count; i++)
        {
            refs.Add(BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(refCursor, 2)));
            refCursor += 2;
        }

        cursor += 4 + byteLength;
        fields.Add(new SvoMetadataObjectField
        {
            Name = name,
            Offset = offset,
            Length = 4 + byteLength,
            Kind = "ReferenceList",
            ReferenceIds = refs.ToArray(),
            IntValue = count,
        });
        return true;
    }

    private static bool TryReadStringField(
        ReadOnlySpan<byte> payload,
        string name,
        ref int cursor,
        ICollection<SvoMetadataObjectField> fields)
    {
        if (!TryReadYabxString(payload, cursor, out var value))
        {
            return false;
        }

        cursor += value.RawLength;
        fields.Add(new SvoMetadataObjectField
        {
            Name = name,
            Offset = value.Offset,
            Length = value.RawLength,
            Kind = "String",
            StringValue = value.Text,
            Capacity = value.Capacity,
            StringLengthWithNull = value.StringLengthWithNull,
        });
        return true;
    }

    private static bool TryReadYabxString(ReadOnlySpan<byte> payload, int offset, out YabxStringValue value)
    {
        value = default;
        if (offset + 6 > payload.Length)
        {
            return false;
        }

        var capacity = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(offset, 4));
        var lengthWithNull = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(offset + 4, 2));
        if (capacity < 2
            || capacity > payload.Length - offset - 4
            || lengthWithNull > capacity - 2)
        {
            return false;
        }

        var text = string.Empty;
        if (lengthWithNull > 0)
        {
            var textBytes = payload.Slice(offset + 6, lengthWithNull);
            if (textBytes[^1] != 0)
            {
                return false;
            }

            for (var i = 0; i < textBytes.Length - 1; i++)
            {
                if (textBytes[i] is < 0x20 or > 0x7E)
                {
                    return false;
                }
            }

            text = Encoding.ASCII.GetString(textBytes[..^1]);
        }

        value = new YabxStringValue(offset, capacity, lengthWithNull, 4 + capacity, text);
        return true;
    }

    private static IReadOnlyList<SvoMetadataResource> BuildMetadataResources(
        byte[] data,
        IReadOnlyList<SvoDirectoryEntry> directory,
        IReadOnlyList<string> resourceNames,
        IReadOnlyList<SvoMetadataObject> objects)
    {
        var names = resourceNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var atlasNames = directory
            .Where(static entry => entry.IsDds)
            .Select(static entry => NormalizeAtlasName(entry.Name))
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var records = new List<SvoMetadataResource>(atlasNames.Length);

        foreach (var atlasName in atlasNames)
        {
            var fileName = names.Contains($"{atlasName}.dds") ? $"{atlasName}.dds" : null;
            var chunkFileName = resourceNames.FirstOrDefault(name =>
                name.StartsWith("__HmfToSvo__", StringComparison.Ordinal)
                && string.Equals(NormalizeAtlasName(name), atlasName, StringComparison.OrdinalIgnoreCase));
            var entry = directory.FirstOrDefault(entry =>
                entry.IsDds && string.Equals(NormalizeAtlasName(entry.Name), atlasName, StringComparison.OrdinalIgnoreCase));
            var dds = ReadDdsInfo(data, entry);
            var textureObject = FindObject(objects, "stevia::Texture", atlasName);
            var imageObject = FindObject(objects, "stevia::Image", fileName ?? $"{atlasName}.dds")
                ?? FindObject(objects, "stevia::Image", atlasName);
            var imageInfo = ReadYabxImageInfo(data, imageObject, entry?.DataLength);
            var imageWidth = GetObjectInt(imageObject, "_width") ?? imageInfo?.Width;
            var imageHeight = GetObjectInt(imageObject, "_height") ?? imageInfo?.Height;
            var imageFormatCode = GetObjectInt(imageObject, "_format") ?? imageInfo?.FormatCode;
            var imageDataSize = GetObjectInt(imageObject, "_dataSize") ?? imageInfo?.DataSize;

            records.Add(new SvoMetadataResource
            {
                AtlasName = atlasName,
                TextureObjectIndex = textureObject?.Index,
                ImageObjectIndex = imageObject?.Index,
                TextureReferenceId = textureObject?.ReferenceId,
                ImageReferenceId = imageObject?.ReferenceId,
                TextureImageReferenceId = GetObjectReference(textureObject, "_image"),
                FileName = GetObjectString(imageObject, "_fileName") ?? fileName,
                ChunkFileName = GetObjectString(imageObject, "_chunkFileName") ?? chunkFileName,
                MetadataWidth = imageWidth,
                MetadataHeight = imageHeight,
                MetadataFormatCode = imageFormatCode,
                MetadataDataSize = imageDataSize,
                DirectoryIndex = entry?.Index,
                DataOffset = entry?.DataOffset,
                DataLength = entry?.DataLength,
                Width = dds?.Width,
                Height = dds?.Height,
                Format = dds?.Format,
            });
        }

        return records;
    }

    private static int? GetObjectInt(SvoMetadataObject? obj, string fieldName)
    {
        var value = obj?.Fields.FirstOrDefault(field => field.Name == fieldName)?.IntValue;
        return value is >= int.MinValue and <= int.MaxValue ? (int)value.Value : null;
    }

    private static string? GetObjectString(SvoMetadataObject? obj, string fieldName)
    {
        return obj?.Fields.FirstOrDefault(field => field.Name == fieldName)?.StringValue;
    }

    private static int? GetObjectReference(SvoMetadataObject? obj, string fieldName)
    {
        return obj?.Fields.FirstOrDefault(field => field.Name == fieldName)?.ReferenceId;
    }

    private static SvoMetadataObject? FindObject(IReadOnlyList<SvoMetadataObject> objects, string typeName, string text)
    {
        return objects.FirstOrDefault(obj =>
            string.Equals(obj.TypeName, typeName, StringComparison.Ordinal)
            && obj.Strings.Any(item => string.Equals(item.Text, text, StringComparison.OrdinalIgnoreCase)));
    }

    private static YabxImageInfo? ReadYabxImageInfo(byte[] data, SvoMetadataObject? imageObject, int? expectedDataSize)
    {
        if (imageObject is null
            || imageObject.PayloadLength < 16
            || imageObject.AbsoluteOffset + 6 > int.MaxValue
            || imageObject.AbsoluteOffset + 6 + imageObject.PayloadLength > data.Length)
        {
            return null;
        }

        var payloadOffset = checked((int)imageObject.AbsoluteOffset + 6);
        var payload = data.AsSpan(payloadOffset, imageObject.PayloadLength);
        var height = BinaryPrimitives.ReadInt32LittleEndian(payload[..4]);
        var width = BinaryPrimitives.ReadInt32LittleEndian(payload[4..8]);
        var formatCode = BinaryPrimitives.ReadInt32LittleEndian(payload[12..16]);
        var dataSize = expectedDataSize is > 0 && ContainsInt32(payload, expectedDataSize.Value)
            ? expectedDataSize
            : null;

        return new YabxImageInfo(width, height, formatCode, dataSize);
    }

    private static bool ContainsInt32(ReadOnlySpan<byte> payload, int value)
    {
        Span<byte> needle = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(needle, value);
        for (var i = 0; i <= payload.Length - needle.Length; i++)
        {
            if (payload.Slice(i, needle.Length).SequenceEqual(needle))
            {
                return true;
            }
        }

        return false;
    }

    private static DdsInfo? ReadDdsInfo(byte[] data, SvoDirectoryEntry? entry)
    {
        if (entry is null || !entry.IsDds || entry.DataOffset > int.MaxValue || entry.DataOffset + 128 > data.Length)
        {
            return null;
        }

        var header = data.AsSpan((int)entry.DataOffset);
        return new DdsInfo(
            BinaryPrimitives.ReadInt32LittleEndian(header[16..20]),
            BinaryPrimitives.ReadInt32LittleEndian(header[12..16]),
            DdsDecoder.GetFormatName(header));
    }

    private static IEnumerable<SvoMetadataString> ExtractAsciiStrings(byte[] data, int payloadOffset, int payloadLength)
    {
        var end = Math.Min(data.Length, payloadOffset + payloadLength);
        var cursor = payloadOffset;
        while (cursor < end)
        {
            if (data[cursor] is < 0x20 or > 0x7E)
            {
                cursor++;
                continue;
            }

            var start = cursor;
            while (cursor < end && data[cursor] is >= 0x20 and <= 0x7E)
            {
                cursor++;
            }

            var length = cursor - start;
            if (length >= 3)
            {
                yield return new SvoMetadataString
                {
                    Offset = start - payloadOffset,
                    AbsoluteOffset = start,
                    Text = Encoding.ASCII.GetString(data, start, length),
                };
            }
        }
    }

    private static string NormalizeAtlasName(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        const string prefix = "__HmfToSvo__";
        if (name.StartsWith(prefix, StringComparison.Ordinal))
        {
            name = name[prefix.Length..];
        }

        return name;
    }

    private static IEnumerable<int> FindDdsOffsets(byte[] data)
    {
        for (var i = 0; i <= data.Length - 128; i++)
        {
            if (data[i] != (byte)'D' || data[i + 1] != (byte)'D' || data[i + 2] != (byte)'S' || data[i + 3] != (byte)' ')
            {
                continue;
            }

            var headerSize = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(i + 4, 4));
            var width = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(i + 16, 4));
            var height = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(i + 12, 4));
            var fourCc = Encoding.ASCII.GetString(data.AsSpan(i + 84, 4));
            if (headerSize == 124 && width > 0 && height > 0 && fourCc == "DXT5")
            {
                yield return i;
            }
        }
    }

    private sealed record SvoMetadataObjectSection(int Offset, int DeclaredCount, IReadOnlyList<SvoMetadataObject> Objects);

    private readonly record struct YabxStringValue(
        int Offset,
        int Capacity,
        int StringLengthWithNull,
        int RawLength,
        string Text);

    private readonly record struct YabxObjectFieldCoverage(
        int ParsedByteCount,
        int UnparsedByteCount,
        string? UnparsedBytesPreviewHex);

    private sealed record DdsInfo(int Width, int Height, string Format);

    private sealed record YabxImageInfo(int Width, int Height, int FormatCode, int? DataSize);
}
