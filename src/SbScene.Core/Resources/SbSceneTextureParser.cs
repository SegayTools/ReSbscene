using System.Buffers.Binary;
using System.Text;
using SbScene.Core.Semantics;
using SbScene.Core.Vtbf;

namespace SbScene.Core.Resources;

public static class SbSceneTextureParser
{
    private static readonly Encoding ShiftJisStrict = CreateShiftJisStrictEncoding();

    public static SbSceneResourceMap ParseResourceMap(string sbscenePath)
    {
        var file = new SbSceneParser().ParseFile(sbscenePath);
        return ParseResourceMap(file.Vtbf, file.Surfboard.Nodes);
    }

    public static SbSceneResourceMap ParseResourceMap(VtbfDocument document, IReadOnlyList<NodeInfo> nodes)
    {
        var textureList = ParseTextureList(document);
        var atlases = ParseTextureAtlases(document);
        return new SbSceneResourceMap
        {
            TextureListName = textureList.Name,
            DeclaredTextureCount = textureList.DeclaredTextureCount,
            Atlases = atlases,
            ImageCasts = ParseImageCasts(document, nodes, atlases),
            CnumRecords = ParseCnumRecords(document, nodes, atlases),
            CrfdRecords = ParseCrfdRecords(document, nodes),
            TextRecords = ParseTextRecords(document),
            SliceCasts = ParseSliceCasts(document, nodes, atlases),
        };
    }

    private static TextureListInfo ParseTextureList(VtbfDocument document)
    {
        var block = document.Blocks.SelectMany(Flatten).OrderBy(static block => block.Offset).FirstOrDefault(static block => block.Tag == "TEXL");
        return new TextureListInfo
        {
            Name = block is null ? null : GetString(block, 0x03),
            DeclaredTextureCount = block is null ? null : GetInt(block, 0x60),
        };
    }

    public static IReadOnlyList<SbSceneTextureAtlas> ParseTextureAtlases(string sbscenePath)
    {
        var document = VtbfParser.ParseFile(sbscenePath);
        return ParseTextureAtlases(document);
    }

    public static IReadOnlyList<SbSceneTextureAtlas> ParseTextureAtlases(VtbfDocument document)
    {
        var blocks = document.Blocks.SelectMany(Flatten).OrderBy(static block => block.Offset).ToArray();
        var atlases = new List<SbSceneTextureAtlas>();
        PendingAtlas? pending = null;

        foreach (var block in blocks)
        {
            if (block.Tag == "TEX ")
            {
                if (pending is not null)
                {
                    atlases.Add(pending.ToAtlas());
                }

                pending = new PendingAtlas
                {
                    Index = atlases.Count,
                    Offset = block.Offset,
                    Name = GetString(block, 0x61) ?? $"texture_{atlases.Count:D3}",
                    Width = GetInt(block, 0x40) ?? 0,
                    Height = GetInt(block, 0x41) ?? 0,
                    Field62 = GetInt(block, 0x62),
                    DeclaredCropCount = GetInt(block, 0x63) ?? 0,
                };
            }
            else if (block.Tag == "CROP" && pending is not null)
            {
                foreach (var field in block.Fields.Where(static field => field.Id == 0x65 && field.Raw.Length == 9))
                {
                    var raw = field.Raw;
                    pending.Crops.Add(new SbSceneCropRect
                    {
                        Index = pending.Crops.Count,
                        RawHex = Convert.ToHexString(raw),
                        Kind = raw[0],
                        Left = BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan(1, 2)),
                        Top = BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan(3, 2)),
                        Right = BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan(5, 2)),
                        Bottom = BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan(7, 2)),
                    });
                }

                atlases.Add(pending.ToAtlas());
                pending = null;
            }
        }

        if (pending is not null)
        {
            atlases.Add(pending.ToAtlas());
        }

        return atlases;
    }

    public static IReadOnlyList<SbSceneImageCast> ParseImageCasts(
        VtbfDocument document,
        IReadOnlyList<NodeInfo> nodes,
        IReadOnlyList<SbSceneTextureAtlas> atlases)
    {
        var blocks = document.Blocks.SelectMany(Flatten).OrderBy(static block => block.Offset).ToArray();
        var imageCasts = new List<SbSceneImageCast>();
        PendingImageCast? pending = null;

        foreach (var block in blocks)
        {
            if (block.Tag == "CIMG")
            {
                if (pending is not null)
                {
                    imageCasts.Add(pending.ToImageCast(nodes, atlases));
                }

                pending = new PendingImageCast
                {
                    Index = imageCasts.Count,
                    Offset = block.Offset,
                    ImageCastFlags = GetInt(block, 0x48) ?? 0,
                    CastIndex = GetInt(block, 0x51) ?? -1,
                    Width = GetFloat(block, 0x40) ?? 0,
                    Height = GetFloat(block, 0x41) ?? 0,
                    PivotX = GetFloat(block, 0x42) ?? 0,
                    PivotY = GetFloat(block, 0x43) ?? 0,
                    CropIndexValues = GetAllInts(block, 0x45).ToArray(),
                    CropRefCounts = GetAllInts(block, 0x44).ToArray(),
                };
            }
            else if (block.Tag == "CREF" && pending is not null)
            {
                var target = pending.NextCropReferenceGroup();
                foreach (var reference in ParseCropReferences(block, pending.TotalCropReferenceCount, atlases))
                {
                    target.Add(reference);
                }

                pending.CropReferenceGroupsRead++;
                if (pending.HasReadExpectedCropReferenceGroups)
                {
                    imageCasts.Add(pending.ToImageCast(nodes, atlases));
                    pending = null;
                }
            }
            else if (pending is not null && block.Tag != "CREF")
            {
                imageCasts.Add(pending.ToImageCast(nodes, atlases));
                pending = null;
            }
        }

        if (pending is not null)
        {
            imageCasts.Add(pending.ToImageCast(nodes, atlases));
        }

        return imageCasts;
    }

    private static IReadOnlyList<SbSceneCnumRecord> ParseCnumRecords(
        VtbfDocument document,
        IReadOnlyList<NodeInfo> nodes,
        IReadOnlyList<SbSceneTextureAtlas> atlases)
    {
        var blocks = document.Blocks.SelectMany(Flatten).OrderBy(static block => block.Offset).ToArray();
        var records = new List<SbSceneCnumRecord>();
        PendingCnumRecord? pending = null;

        foreach (var block in blocks)
        {
            if (block.Tag == "CNUM")
            {
                if (pending is not null)
                {
                    records.Add(pending.ToCnumRecord(nodes));
                }

                pending = new PendingCnumRecord
                {
                    Index = records.Count,
                    Offset = block.Offset,
                    Field44Count = GetInt(block, 0x44),
                    Field48 = GetInt(block, 0x48),
                    Field51 = GetInt(block, 0x51),
                    Field40 = GetFloat(block, 0x40),
                    Field42 = GetFloat(block, 0x42),
                    Field43 = GetFloat(block, 0x43),
                    Field39Colors = GetAllColors(block, 0x39).ToArray(),
                    Field39RawHexValues = GetAllRawHex(block, 0x39).ToArray(),
                    FieldA0 = GetInt(block, 0xA0),
                    FieldA1 = GetString(block, 0xA1),
                    FieldA1RawHex = GetRawHex(block, 0xA1),
                    FieldA2 = GetInt(block, 0xA2),
                    FieldA3 = GetInt(block, 0xA3),
                    FieldA4 = GetInt(block, 0xA4),
                    FieldA5 = GetInt(block, 0xA5),
                    FieldA6 = GetInt(block, 0xA6),
                    FieldA7 = GetInt(block, 0xA7),
                    FieldA8 = GetInt(block, 0xA8),
                    FieldA9 = GetInt(block, 0xA9),
                    FieldAA = GetInt(block, 0xAA),
                    FieldAB = GetInt(block, 0xAB),
                    FieldAC = GetInt(block, 0xAC),
                    FieldAD = GetInt(block, 0xAD),
                    FieldAERawHex = GetRawHex(block, 0xAE),
                    FieldAEFloatValues = GetAllFloats(block, 0xAE).ToArray(),
                    FieldAFRawHex = GetRawHex(block, 0xAF),
                    FieldAFPackedValues = GetAllInts(block, 0xAF).ToArray(),
                    ZeroLengthMarkerFieldIds = GetZeroLengthMarkerIds(block).ToArray(),
                    Fields = block.Fields.Select(ToFieldValue).ToArray(),
                };
            }
            else if (block.Tag == "CREF" && pending is not null)
            {
                pending.CropReferences.AddRange(ParseCropReferences(block, pending.CropReferences.Count, atlases));
                records.Add(pending.ToCnumRecord(nodes));
                pending = null;
            }
            else if (pending is not null && block.Tag != "CREF")
            {
                records.Add(pending.ToCnumRecord(nodes));
                pending = null;
            }
        }

        if (pending is not null)
        {
            records.Add(pending.ToCnumRecord(nodes));
        }

        return records;
    }

    private static IReadOnlyList<SbSceneCrfdRecord> ParseCrfdRecords(VtbfDocument document, IReadOnlyList<NodeInfo> nodes)
    {
        var blocks = document.Blocks.SelectMany(Flatten).OrderBy(static block => block.Offset).Where(static block => block.Tag == "CRFD");
        var records = new List<SbSceneCrfdRecord>();

        foreach (var block in blocks)
        {
            var field51 = GetInt(block, 0x51);
            var nodeName = field51 is >= 0 && field51 < nodes.Count ? nodes[field51.Value].Name : null;
            records.Add(new SbSceneCrfdRecord
            {
                Index = records.Count,
                Offset = block.Offset,
                Field51 = field51,
                NodeName = nodeName,
                Field90 = GetString(block, 0x90),
                Field90RawHex = GetRawHex(block, 0x90),
                Field91 = GetString(block, 0x91),
                Field91RawHex = GetRawHex(block, 0x91),
                Field92 = GetInt(block, 0x92),
                Field93 = GetInt(block, 0x93),
                Field94 = GetFloat(block, 0x94),
                Field95 = GetInt(block, 0x95),
                Fields = block.Fields.Select(ToFieldValue).ToArray(),
            });
        }

        return records;
    }

    private static IReadOnlyList<SbSceneTextRecord> ParseTextRecords(VtbfDocument document)
    {
        var blocks = document.Blocks.SelectMany(Flatten).OrderBy(static block => block.Offset).Where(static block => block.Tag == "TEXT");
        var records = new List<SbSceneTextRecord>();

        foreach (var block in blocks)
        {
            var field7BPackedValues = GetAllInts(block, 0x7B).ToArray();
            records.Add(new SbSceneTextRecord
            {
                Index = records.Count,
                Offset = block.Offset,
                Field7A = GetString(block, 0x7A),
                Field7AShiftJis = GetShiftJisString(block, 0x7A),
                Field7ARawHex = GetRawHex(block, 0x7A),
                Field7BRawHex = GetRawHex(block, 0x7B),
                Field7BPackedValues = field7BPackedValues.Length == 0 ? null : field7BPackedValues,
                Field33RawHex = GetRawHex(block, 0x33),
                Field33Vector = GetVector2(block, 0x33),
                Field41 = GetInt(block, 0x41),
                Field78 = GetInt(block, 0x78),
                Field79 = GetInt(block, 0x79),
                Field7C = GetInt(block, 0x7C),
                ZeroLengthMarkerFieldIds = GetZeroLengthMarkerIds(block).ToArray(),
                Fields = block.Fields.Select(ToFieldValue).ToArray(),
            });
        }

        return records;
    }

    private static IReadOnlyList<SbSceneSliceCast> ParseSliceCasts(
        VtbfDocument document,
        IReadOnlyList<NodeInfo> nodes,
        IReadOnlyList<SbSceneTextureAtlas> atlases)
    {
        var blocks = document.Blocks.SelectMany(Flatten).OrderBy(static block => block.Offset).ToArray();
        var sliceCasts = new List<SbSceneSliceCast>();
        PendingSliceCast? pending = null;

        foreach (var block in blocks)
        {
            if (block.Tag == "CSLI")
            {
                if (pending is not null)
                {
                    sliceCasts.Add(pending.ToSliceCast(nodes));
                }

                pending = new PendingSliceCast
                {
                    Index = sliceCasts.Count,
                    Offset = block.Offset,
                    Field44Count = GetInt(block, 0x44),
                    TargetIndex = GetInt(block, 0x51),
                    Field40 = GetFloat(block, 0x40),
                    Field41 = GetFloat(block, 0x41),
                    Field42 = GetFloat(block, 0x42),
                    Field43 = GetFloat(block, 0x43),
                    Field80 = GetInt(block, 0x80),
                    Field81 = GetInt(block, 0x81),
                    Field82 = GetInt(block, 0x82),
                    Field84 = GetInt(block, 0x84),
                    Field85 = GetInt(block, 0x85),
                    Field86 = GetFloat(block, 0x86),
                    Field87 = GetFloat(block, 0x87),
                    Fields = block.Fields.Select(ToFieldValue).ToArray(),
                };
            }
            else if (block.Tag == "CREF" && pending is not null)
            {
                pending.CropReferences.AddRange(ParseCropReferences(block, pending.CropReferences.Count, atlases));
            }
            else if (block.Tag == "SLIC" && pending is not null)
            {
                pending.Slices.AddRange(ParseSliceRecords(block, pending.Slices.Count));
                sliceCasts.Add(pending.ToSliceCast(nodes));
                pending = null;
            }
            else if (pending is not null && block.Tag is not "CREF" and not "SLIC")
            {
                sliceCasts.Add(pending.ToSliceCast(nodes));
                pending = null;
            }
        }

        if (pending is not null)
        {
            sliceCasts.Add(pending.ToSliceCast(nodes));
        }

        return sliceCasts;
    }

    private static IEnumerable<SbSceneSliceRecord> ParseSliceRecords(VtbfBlock block, int indexBase)
    {
        var records = block.Fields.Any(IsRecordMarker)
            ? SplitRecords(block.Fields)
            : block.Fields.Select(static field => (IReadOnlyList<VtbfField>)[field]).ToArray();

        for (var i = 0; i < records.Count; i++)
        {
            var fields = records[i];
            yield return new SbSceneSliceRecord
            {
                Index = indexBase + i,
                Offset = fields.Count > 0 ? fields[0].Offset : block.Offset,
                Field83 = GetInt(fields, 0x83),
                Field40 = GetInt(fields, 0x40),
                Field41 = GetInt(fields, 0x41),
                Field45 = GetInt(fields, 0x45),
                Field37Color = ParseColor(fields.FirstOrDefault(static field => field.Id == 0x37)),
                Field37RawHex = GetRawHex(fields, 0x37),
                Field38Color = ParseColor(fields.FirstOrDefault(static field => field.Id == 0x38)),
                Field38RawHex = GetRawHex(fields, 0x38),
                Field39Colors = fields
                    .Where(static field => field.Id == 0x39)
                    .Select(ParseColor)
                    .Where(static color => color is not null)
                    .Cast<ColorArgbValue>()
                    .ToArray(),
                Field39RawHexValues = GetAllRawHex(fields, 0x39).ToArray(),
                Fields = fields.Select(ToFieldValue).ToArray(),
            };
        }
    }

    private static string? GetString(VtbfBlock block, int id)
    {
        return block.Fields.FirstOrDefault(field => field.Id == id)?.StringValue;
    }

    private static string? GetRawHex(VtbfBlock block, int id)
    {
        var field = block.Fields.FirstOrDefault(field => field.Id == id);
        return field is null ? null : Convert.ToHexString(field.Raw);
    }

    private static string? GetShiftJisString(VtbfBlock block, int id)
    {
        var raw = block.Fields.FirstOrDefault(field => field.Id == id)?.Raw;
        return raw is null ? null : DecodeShiftJisString(raw);
    }

    private static IEnumerable<string> GetAllRawHex(VtbfBlock block, int id)
    {
        return block.Fields
            .Where(field => field.Id == id)
            .Select(static field => Convert.ToHexString(field.Raw));
    }

    private static string? GetRawHex(IReadOnlyList<VtbfField> fields, int id)
    {
        var field = fields.FirstOrDefault(field => field.Id == id);
        return field is null ? null : Convert.ToHexString(field.Raw);
    }

    private static IEnumerable<string> GetAllRawHex(IReadOnlyList<VtbfField> fields, int id)
    {
        return fields
            .Where(field => field.Id == id)
            .Select(static field => Convert.ToHexString(field.Raw));
    }

    private static int? GetInt(VtbfBlock block, int id)
    {
        var value = block.Fields.FirstOrDefault(field => field.Id == id)?.Int64Values?.FirstOrDefault();
        return value is >= int.MinValue and <= int.MaxValue ? (int)value.Value : null;
    }

    private static float? GetFloat(VtbfBlock block, int id)
    {
        var value = block.Fields.FirstOrDefault(field => field.Id == id)?.Float64Values?.FirstOrDefault();
        return value is not null && value >= float.MinValue && value <= float.MaxValue ? (float)value.Value : null;
    }

    private static Vector2Value? GetVector2(VtbfBlock block, int id)
    {
        var values = block.Fields.FirstOrDefault(field => field.Id == id)?.Float64Values;
        if (values is not { Length: >= 2 })
        {
            return null;
        }

        return new Vector2Value
        {
            X = (float)values[0],
            Y = (float)values[1],
        };
    }

    private static IEnumerable<float> GetAllFloats(VtbfBlock block, int id)
    {
        return block.Fields
            .Where(field => field.Id == id)
            .SelectMany(static field => field.Float64Values ?? [])
            .Where(static value => value >= float.MinValue && value <= float.MaxValue)
            .Select(static value => (float)value);
    }

    private static IEnumerable<int> GetAllInts(VtbfBlock block, int id)
    {
        return block.Fields
            .Where(field => field.Id == id)
            .SelectMany(static field => field.Int64Values ?? [])
            .Where(static value => value is >= int.MinValue and <= int.MaxValue)
            .Select(static value => (int)value);
    }

    private static IEnumerable<int> GetZeroLengthMarkerIds(VtbfBlock block)
    {
        return block.Fields
            .Where(static field => field.TypeCode == 0x00 && field.Raw.Length == 0)
            .Select(static field => field.Id);
    }

    private static string? DecodeShiftJisString(byte[] raw)
    {
        var terminator = Array.IndexOf(raw, (byte)0);
        var length = terminator >= 0 ? terminator : raw.Length;
        try
        {
            return ShiftJisStrict.GetString(raw, 0, length);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    private static Encoding CreateShiftJisStrictEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(932, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
    }

    private static int? GetInt(IReadOnlyList<VtbfField> fields, int id)
    {
        var value = fields.FirstOrDefault(field => field.Id == id)?.Int64Values?.FirstOrDefault();
        return value is >= int.MinValue and <= int.MaxValue ? (int)value.Value : null;
    }

    private static FieldValueSummary ToFieldValue(VtbfField field)
    {
        return new FieldValueSummary
        {
            IdHex = field.IdHex,
            TypeHex = field.TypeHex,
            TypeName = field.TypeName,
            Preview = field.Preview,
            Int64Values = field.Int64Values,
            Float64Values = field.Float64Values,
            StringValue = field.StringValue,
        };
    }

    private static ColorArgbValue? ParseColor(VtbfField? field)
    {
        if (field?.Raw is not { Length: >= 4 } raw)
        {
            return null;
        }

        return new ColorArgbValue
        {
            A = raw[0],
            R = raw[1],
            G = raw[2],
            B = raw[3],
        };
    }

    private static IEnumerable<ColorArgbValue> GetAllColors(VtbfBlock block, int id)
    {
        return block.Fields
            .Where(field => field.Id == id)
            .Select(ParseColor)
            .Where(static color => color is not null)
            .Cast<ColorArgbValue>();
    }

    private static IReadOnlyList<IReadOnlyList<VtbfField>> SplitRecords(IReadOnlyList<VtbfField> fields)
    {
        var records = new List<IReadOnlyList<VtbfField>>();
        List<VtbfField>? current = null;

        foreach (var field in fields)
        {
            if (IsRecordMarker(field))
            {
                if (field.TypeCode == 0xFD)
                {
                    if (current is { Count: > 0 })
                    {
                        records.Add(current);
                        current = null;
                    }

                    continue;
                }

                if (current is { Count: > 0 })
                {
                    records.Add(current);
                }

                current = [];
                continue;
            }

            current ??= [];
            current.Add(field);
        }

        if (current is { Count: > 0 })
        {
            records.Add(current);
        }

        return records;
    }

    private static bool IsRecordMarker(VtbfField field)
    {
        return field.TypeCode is 0xFC or 0xFD or 0xFE;
    }

    private static IEnumerable<SbSceneCropReference> ParseCropReferences(
        VtbfBlock block,
        int startIndex,
        IReadOnlyList<SbSceneTextureAtlas> atlases)
    {
        var index = startIndex;
        foreach (var field in block.Fields.Where(static field => field.Id == 0x49 && field.Raw.Length == 7))
        {
            var raw = field.Raw;
            var textureListIndex = BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(1, 2));
            var textureIndex = BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(3, 2));
            var cropIndex = BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(5, 2));
            var atlas = textureIndex < atlases.Count ? atlases[textureIndex] : null;

            yield return new SbSceneCropReference
            {
                Index = index++,
                RawHex = Convert.ToHexString(raw),
                Kind = raw[0],
                TextureListIndex = textureListIndex,
                TextureIndex = textureIndex,
                CropIndex = cropIndex,
                AtlasName = atlas?.Name,
                CropPath = atlas is null ? null : $"crops/{textureIndex:D3}_{atlas.Name}/{cropIndex:D3}.png",
            };
        }
    }

    private static IEnumerable<VtbfBlock> Flatten(VtbfBlock block)
    {
        yield return block;
        foreach (var child in block.Children)
        {
            foreach (var nested in Flatten(child))
            {
                yield return nested;
            }
        }
    }

    private sealed class PendingAtlas
    {
        public required int Index { get; init; }

        public required long Offset { get; init; }

        public required string Name { get; init; }

        public required int Width { get; init; }

        public required int Height { get; init; }

        public int? Field62 { get; init; }

        public required int DeclaredCropCount { get; init; }

        public List<SbSceneCropRect> Crops { get; } = [];

        public SbSceneTextureAtlas ToAtlas()
        {
            return new SbSceneTextureAtlas
            {
                Index = Index,
                Offset = Offset,
                Name = Name,
                Width = Width,
                Height = Height,
                Field62 = Field62,
                Field62Bits = Field62 is null
                    ? []
                    : Enumerable.Range(0, 32)
                        .Where(bit => ((uint)Field62.Value & (1u << bit)) != 0)
                        .ToArray(),
                DeclaredCropCount = DeclaredCropCount,
                Crops = Crops.ToArray(),
            };
        }
    }

    private sealed class PendingImageCast
    {
        public required int Index { get; init; }

        public required long Offset { get; init; }

        public required int ImageCastFlags { get; init; }

        public required int CastIndex { get; init; }

        public required float Width { get; init; }

        public required float Height { get; init; }

        public required float PivotX { get; init; }

        public required float PivotY { get; init; }

        public required IReadOnlyList<int> CropIndexValues { get; init; }

        public required IReadOnlyList<int> CropRefCounts { get; init; }

        public int CropReferenceGroupsRead { get; set; }

        public List<SbSceneCropReference> PrimaryCropReferences { get; } = [];

        public List<SbSceneCropReference> SecondaryCropReferences { get; } = [];

        public int TotalCropReferenceCount => PrimaryCropReferences.Count + SecondaryCropReferences.Count;

        public int PrimaryCropReferenceCount => CropRefCounts.Count > 0 ? CropRefCounts[0] : 0;

        public int SecondaryCropReferenceCount => CropRefCounts.Count > 1 ? CropRefCounts[1] : 0;

        public bool HasReadExpectedCropReferenceGroups => CropReferenceGroupsRead >= ExpectedCropReferenceGroupCount;

        public int ExpectedCropReferenceGroupCount
        {
            get
            {
                var count = 0;
                if (PrimaryCropReferenceCount > 0)
                {
                    count++;
                }

                if (SecondaryCropReferenceCount > 0)
                {
                    count++;
                }

                return count;
            }
        }

        public List<SbSceneCropReference> NextCropReferenceGroup()
        {
            if (PrimaryCropReferenceCount > 0 && CropReferenceGroupsRead == 0)
            {
                return PrimaryCropReferences;
            }

            return SecondaryCropReferences;
        }

        public SbSceneImageCast ToImageCast(IReadOnlyList<NodeInfo> nodes, IReadOnlyList<SbSceneTextureAtlas> atlases)
        {
            _ = atlases;
            var nodeName = CastIndex >= 0 && CastIndex < nodes.Count ? nodes[CastIndex].Name : null;
            var cropReferences = PrimaryCropReferences.Concat(SecondaryCropReferences).ToArray();
            bool? primaryCountMatches = CropRefCounts.Count > 0 ? CropRefCounts[0] == PrimaryCropReferences.Count : null;
            bool? secondaryCountMatches = CropRefCounts.Count > 1 ? CropRefCounts[1] == SecondaryCropReferences.Count : null;
            bool? countMatches = primaryCountMatches is null && secondaryCountMatches is null
                ? null
                : primaryCountMatches is not false && secondaryCountMatches is not false;
            return new SbSceneImageCast
            {
                Index = Index,
                Offset = Offset,
                ImageCastFlags = ImageCastFlags,
                ImageCastFlagBits = Enumerable.Range(0, 32)
                    .Where(bit => ((uint)ImageCastFlags & (1u << bit)) != 0)
                    .ToArray(),
                CastIndex = CastIndex,
                NodeName = nodeName,
                Width = Width,
                Height = Height,
                PivotX = PivotX,
                PivotY = PivotY,
                DeclaredCropReferenceCount = CropRefCounts.Count > 0 ? CropRefCounts[0] : null,
                PrimaryCropReferenceCount = CropRefCounts.Count > 0 ? CropRefCounts[0] : null,
                SecondaryCropReferenceCount = CropRefCounts.Count > 1 ? CropRefCounts[1] : null,
                SecondaryCropFlag = CropRefCounts.Count > 1 ? CropRefCounts[1] : null,
                PrimaryCropIndex = CropIndexValues.Count > 0 ? CropIndexValues[0] : null,
                SecondaryCropIndex = CropIndexValues.Count > 1 ? CropIndexValues[1] : null,
                PrimaryCropReferenceIndex = CropIndexValues.Count > 0 ? CropIndexValues[0] : null,
                SecondaryCropReferenceIndex = CropIndexValues.Count > 1 ? CropIndexValues[1] : null,
                CropReferenceCountMatches = countMatches,
                CropIndexValues = CropIndexValues,
                CropRefCounts = CropRefCounts,
                PrimaryCropReferences = PrimaryCropReferences.ToArray(),
                SecondaryCropReferences = SecondaryCropReferences.ToArray(),
                CropReferences = cropReferences,
            };
        }
    }

    private sealed class PendingCnumRecord
    {
        public required int Index { get; init; }

        public required long Offset { get; init; }

        public int? Field44Count { get; init; }

        public int? Field48 { get; init; }

        public int? Field51 { get; init; }

        public float? Field40 { get; init; }

        public float? Field42 { get; init; }

        public float? Field43 { get; init; }

        public required IReadOnlyList<ColorArgbValue> Field39Colors { get; init; }

        public required IReadOnlyList<string> Field39RawHexValues { get; init; }

        public int? FieldA0 { get; init; }

        public string? FieldA1 { get; init; }

        public string? FieldA1RawHex { get; init; }

        public int? FieldA2 { get; init; }

        public int? FieldA3 { get; init; }

        public int? FieldA4 { get; init; }

        public int? FieldA5 { get; init; }

        public int? FieldA6 { get; init; }

        public int? FieldA7 { get; init; }

        public int? FieldA8 { get; init; }

        public int? FieldA9 { get; init; }

        public int? FieldAA { get; init; }

        public int? FieldAB { get; init; }

        public int? FieldAC { get; init; }

        public int? FieldAD { get; init; }

        public string? FieldAERawHex { get; init; }

        public required IReadOnlyList<float> FieldAEFloatValues { get; init; }

        public string? FieldAFRawHex { get; init; }

        public required IReadOnlyList<int> FieldAFPackedValues { get; init; }

        public required IReadOnlyList<int> ZeroLengthMarkerFieldIds { get; init; }

        public required IReadOnlyList<FieldValueSummary> Fields { get; init; }

        public List<SbSceneCropReference> CropReferences { get; } = [];

        public SbSceneCnumRecord ToCnumRecord(IReadOnlyList<NodeInfo> nodes)
        {
            var nodeName = Field51 is >= 0 && Field51 < nodes.Count ? nodes[Field51.Value].Name : null;
            return new SbSceneCnumRecord
            {
                Index = Index,
                Offset = Offset,
                Field44Count = Field44Count,
                Field48 = Field48,
                Field51 = Field51,
                NodeName = nodeName,
                Field40 = Field40,
                Field42 = Field42,
                Field43 = Field43,
                Field39Colors = Field39Colors.Count == 0 ? null : Field39Colors,
                Field39RawHexValues = Field39RawHexValues.Count == 0 ? null : Field39RawHexValues,
                FieldA0 = FieldA0,
                FieldA1 = FieldA1,
                FieldA1RawHex = FieldA1RawHex,
                FieldA2 = FieldA2,
                FieldA3 = FieldA3,
                FieldA4 = FieldA4,
                FieldA5 = FieldA5,
                FieldA6 = FieldA6,
                FieldA7 = FieldA7,
                FieldA8 = FieldA8,
                FieldA9 = FieldA9,
                FieldAA = FieldAA,
                FieldAB = FieldAB,
                FieldAC = FieldAC,
                FieldAD = FieldAD,
                FieldAERawHex = FieldAERawHex,
                FieldAEFloatValues = FieldAEFloatValues.Count == 0 ? null : FieldAEFloatValues,
                FieldAFRawHex = FieldAFRawHex,
                FieldAFPackedValues = FieldAFPackedValues.Count == 0 ? null : FieldAFPackedValues,
                CropReferenceCountMatchesField44 = Field44Count is null ? null : Field44Count == CropReferences.Count,
                ZeroLengthMarkerFieldIds = ZeroLengthMarkerFieldIds,
                CropReferences = CropReferences.ToArray(),
                Fields = Fields,
            };
        }
    }

    private sealed class PendingSliceCast
    {
        public required int Index { get; init; }

        public required long Offset { get; init; }

        public int? Field44Count { get; init; }

        public int? TargetIndex { get; init; }

        public float? Field40 { get; init; }

        public float? Field41 { get; init; }

        public float? Field42 { get; init; }

        public float? Field43 { get; init; }

        public int? Field80 { get; init; }

        public int? Field81 { get; init; }

        public int? Field82 { get; init; }

        public int? Field84 { get; init; }

        public int? Field85 { get; init; }

        public float? Field86 { get; init; }

        public float? Field87 { get; init; }

        public required IReadOnlyList<FieldValueSummary> Fields { get; init; }

        public List<SbSceneSliceRecord> Slices { get; } = [];

        public List<SbSceneCropReference> CropReferences { get; } = [];

        public SbSceneSliceCast ToSliceCast(IReadOnlyList<NodeInfo> nodes)
        {
            var nodeName = TargetIndex is >= 0 && TargetIndex < nodes.Count ? nodes[TargetIndex.Value].Name : null;
            return new SbSceneSliceCast
            {
                Index = Index,
                Offset = Offset,
                Field44Count = Field44Count,
                TargetIndex = TargetIndex,
                NodeName = nodeName,
                Field40 = Field40,
                Field41 = Field41,
                Field42 = Field42,
                Field43 = Field43,
                Field80 = Field80,
                Field81 = Field81,
                Field82 = Field82,
                Field84 = Field84,
                Field85 = Field85,
                Field86 = Field86,
                Field87 = Field87,
                SlicRecordCountMatchesField44 = Field44Count is null ? null : Field44Count == Slices.Count,
                CropReferenceCountMatchesField44 = Field44Count is null ? null : Field44Count == CropReferences.Count,
                Slices = Slices.ToArray(),
                CropReferences = CropReferences.ToArray(),
                Fields = Fields,
            };
        }
    }

    private sealed class TextureListInfo
    {
        public string? Name { get; init; }

        public int? DeclaredTextureCount { get; init; }
    }
}
