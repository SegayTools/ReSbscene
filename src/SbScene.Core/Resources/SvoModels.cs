using SbScene.Core.Semantics;

namespace SbScene.Core.Resources;

public sealed class SvoTextureResource
{
    public required int Index { get; init; }

    public required int DirectoryIndex { get; init; }

    public string? FileName { get; init; }

    public string? AtlasName { get; init; }

    public required long Offset { get; init; }

    public required int Length { get; init; }

    public required int Width { get; init; }

    public required int Height { get; init; }

    public required string Format { get; init; }

    public required byte[] DdsBytes { get; init; }
}

public sealed class SvoHeaderInfo
{
    public required string Magic { get; init; }

    public required int DirectoryCount { get; init; }

    public required int HeaderSize { get; init; }

    public required int DirectoryTableOffset { get; init; }

    public required int DirectoryEntrySize { get; init; }

    public required int HeaderUnknownNonZeroByteCount { get; init; }

    public required IReadOnlyList<int> HeaderUnknownNonZeroByteOffsets { get; init; }

    public required IReadOnlyList<SvoHeaderUnknownWord> UnknownWords { get; init; }
}

public sealed class SvoHeaderUnknownWord
{
    public required int Offset { get; init; }

    public required long Value { get; init; }
}

public sealed class SvoDirectoryEntry
{
    public required int Index { get; init; }

    public required long EntryOffset { get; init; }

    public required string Name { get; init; }

    public required int Kind { get; init; }

    public required int Sequence { get; init; }

    public required long DataOffset { get; init; }

    public required int DataLength { get; init; }

    public required bool IsInBounds { get; init; }

    public required bool IsDds { get; init; }

    public string? DataMagic { get; init; }

    public required int ReservedNonZeroByteCount { get; init; }

    public required IReadOnlyList<int> ReservedNonZeroByteOffsets { get; init; }
}

public sealed class SvoMetadataInfo
{
    public required int DirectoryIndex { get; init; }

    public required long Offset { get; init; }

    public required int Length { get; init; }

    public required string Magic { get; init; }

    public int? Version { get; init; }

    public int? DeclaredPayloadLength { get; init; }

    public long? HeaderHashCandidate { get; init; }

    public required IReadOnlyList<SvoMetadataString> Strings { get; init; }

    public required IReadOnlyList<string> TypeNames { get; init; }

    public required IReadOnlyList<SvoMetadataTypeInfo> TypeSchemas { get; init; }

    public int? ObjectSectionOffset { get; init; }

    public int? DeclaredObjectCount { get; init; }

    public int? ObjectReferenceBase { get; init; }

    public required IReadOnlyList<SvoMetadataObject> Objects { get; init; }

    public required IReadOnlyList<string> FieldNames { get; init; }

    public required IReadOnlyList<string> ResourceNames { get; init; }

    public required IReadOnlyList<SvoMetadataResource> Resources { get; init; }
}

public sealed class SvoMetadataString
{
    public required int Offset { get; init; }

    public required long AbsoluteOffset { get; init; }

    public required string Text { get; init; }
}

public sealed class SvoMetadataTypeInfo
{
    public int? TypeIndex { get; init; }

    public required string Name { get; init; }

    public required IReadOnlyList<string> Fields { get; init; }

    public required IReadOnlyList<SvoMetadataFieldInfo> FieldDescriptors { get; init; }
}

public sealed class SvoMetadataFieldInfo
{
    public required string Name { get; init; }

    public required int Offset { get; init; }

    public required int DescriptorOffset { get; init; }

    public required long AbsoluteDescriptorOffset { get; init; }

    public required string RawDescriptorHex { get; init; }

    public required byte Flags { get; init; }

    public required byte ValueKind { get; init; }

    public required byte Reserved { get; init; }

    public required string ValueKindName { get; init; }
}

public sealed class SvoMetadataObject
{
    public required int Index { get; init; }

    public int? ReferenceId { get; init; }

    public required int Offset { get; init; }

    public required long AbsoluteOffset { get; init; }

    public required int PayloadOffset { get; init; }

    public required int TypeIndex { get; init; }

    public string? TypeName { get; init; }

    public required int PayloadLength { get; init; }

    public int ParsedFieldByteCount { get; init; }

    public int UnparsedByteCount { get; init; }

    public string? UnparsedBytesPreviewHex { get; init; }

    public required IReadOnlyList<SvoMetadataObjectString> Strings { get; init; }

    public required IReadOnlyList<SvoMetadataObjectField> Fields { get; init; }
}

public sealed class SvoMetadataObjectString
{
    public required int Offset { get; init; }

    public required string Text { get; init; }
}

public sealed class SvoMetadataObjectField
{
    public required string Name { get; init; }

    public required int Offset { get; init; }

    public required int Length { get; init; }

    public required string Kind { get; init; }

    public string? RawHex { get; init; }

    public long? IntValue { get; init; }

    public string? StringValue { get; init; }

    public int? ReferenceId { get; init; }

    public int? ReferenceTargetObjectIndex { get; init; }

    public string? ReferenceTargetTypeName { get; init; }

    public IReadOnlyList<int>? ReferenceIds { get; init; }

    public IReadOnlyList<SvoMetadataReferenceTarget>? ReferenceTargets { get; init; }

    public int? Capacity { get; init; }

    public int? StringLengthWithNull { get; init; }
}

public sealed class SvoMetadataReferenceTarget
{
    public required int ReferenceId { get; init; }

    public int? ObjectIndex { get; init; }

    public string? TypeName { get; init; }
}

public sealed class SvoMetadataResource
{
    public required string AtlasName { get; init; }

    public int? TextureObjectIndex { get; init; }

    public int? ImageObjectIndex { get; init; }

    public int? TextureReferenceId { get; init; }

    public int? ImageReferenceId { get; init; }

    public int? TextureImageReferenceId { get; init; }

    public string? FileName { get; init; }

    public string? ChunkFileName { get; init; }

    public int? MetadataWidth { get; init; }

    public int? MetadataHeight { get; init; }

    public int? MetadataFormatCode { get; init; }

    public int? MetadataDataSize { get; init; }

    public int? DirectoryIndex { get; init; }

    public long? DataOffset { get; init; }

    public int? DataLength { get; init; }

    public int? Width { get; init; }

    public int? Height { get; init; }

    public string? Format { get; init; }
}

public sealed class SbSceneTextureAtlas
{
    public required int Index { get; init; }

    public required long Offset { get; init; }

    public required string Name { get; init; }

    public required int Width { get; init; }

    public required int Height { get; init; }

    public int? Field62 { get; init; }

    public required IReadOnlyList<int> Field62Bits { get; init; }

    public required int DeclaredCropCount { get; init; }

    public required IReadOnlyList<SbSceneCropRect> Crops { get; init; }
}

public sealed class SbSceneImageCast
{
    public required int Index { get; init; }

    public required long Offset { get; init; }

    public required int ImageCastFlags { get; init; }

    public required IReadOnlyList<int> ImageCastFlagBits { get; init; }

    public required int CastIndex { get; init; }

    public string? NodeName { get; init; }

    public required float Width { get; init; }

    public required float Height { get; init; }

    public required float PivotX { get; init; }

    public required float PivotY { get; init; }

    public int? DeclaredCropReferenceCount { get; init; }

    public int? PrimaryCropReferenceCount { get; init; }

    public int? SecondaryCropReferenceCount { get; init; }

    public int? SecondaryCropFlag { get; init; }

    public int? PrimaryCropIndex { get; init; }

    public int? SecondaryCropIndex { get; init; }

    public int? PrimaryCropReferenceIndex { get; init; }

    public int? SecondaryCropReferenceIndex { get; init; }

    public int ActualCropReferenceCount => CropReferences.Count;

    public int ActualPrimaryCropReferenceCount => PrimaryCropReferences.Count;

    public int ActualSecondaryCropReferenceCount => SecondaryCropReferences.Count;

    public bool? CropReferenceCountMatches { get; init; }

    public required IReadOnlyList<int> CropIndexValues { get; init; }

    public required IReadOnlyList<int> CropRefCounts { get; init; }

    public required IReadOnlyList<SbSceneCropReference> PrimaryCropReferences { get; init; }

    public required IReadOnlyList<SbSceneCropReference> SecondaryCropReferences { get; init; }

    public required IReadOnlyList<SbSceneCropReference> CropReferences { get; init; }
}

public sealed class SbSceneCropReference
{
    public required int Index { get; init; }

    public required string RawHex { get; init; }

    public required byte Kind { get; init; }

    public required int TextureListIndex { get; init; }

    public required int TextureIndex { get; init; }

    public required int CropIndex { get; init; }

    public string? AtlasName { get; init; }

    public string? CropPath { get; init; }
}

public sealed class SbSceneResourceMap
{
    public string? TextureListName { get; init; }

    public int? DeclaredTextureCount { get; init; }

    public required IReadOnlyList<SbSceneTextureAtlas> Atlases { get; init; }

    public required IReadOnlyList<SbSceneImageCast> ImageCasts { get; init; }

    public required IReadOnlyList<SbSceneCnumRecord> CnumRecords { get; init; }

    public required IReadOnlyList<SbSceneCrfdRecord> CrfdRecords { get; init; }

    public required IReadOnlyList<SbSceneTextRecord> TextRecords { get; init; }

    public required IReadOnlyList<SbSceneSliceCast> SliceCasts { get; init; }
}

public sealed class SbSceneCnumRecord
{
    public required int Index { get; init; }

    public required long Offset { get; init; }

    public int? Field44Count { get; init; }

    public int? Field48 { get; init; }

    public int? Field51 { get; init; }

    public string? NodeName { get; init; }

    public float? Field40 { get; init; }

    public float? Field42 { get; init; }

    public float? Field43 { get; init; }

    public IReadOnlyList<ColorArgbValue>? Field39Colors { get; init; }

    public IReadOnlyList<string>? Field39RawHexValues { get; init; }

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

    public IReadOnlyList<float>? FieldAEFloatValues { get; init; }

    public string? FieldAFRawHex { get; init; }

    public IReadOnlyList<int>? FieldAFPackedValues { get; init; }

    public bool? CropReferenceCountMatchesField44 { get; init; }

    public required IReadOnlyList<int> ZeroLengthMarkerFieldIds { get; init; }

    public required IReadOnlyList<SbSceneCropReference> CropReferences { get; init; }

    public required IReadOnlyList<FieldValueSummary> Fields { get; init; }
}

public sealed class SbSceneCrfdRecord
{
    public required int Index { get; init; }

    public required long Offset { get; init; }

    public int? Field51 { get; init; }

    public string? NodeName { get; init; }

    public string? Field90 { get; init; }

    public string? Field90RawHex { get; init; }

    public string? Field91 { get; init; }

    public string? Field91RawHex { get; init; }

    public int? Field92 { get; init; }

    public int? Field93 { get; init; }

    public float? Field94 { get; init; }

    public int? Field95 { get; init; }

    public required IReadOnlyList<FieldValueSummary> Fields { get; init; }
}

public sealed class SbSceneTextRecord
{
    public required int Index { get; init; }

    public required long Offset { get; init; }

    public string? Field7A { get; init; }

    public string? Field7AShiftJis { get; init; }

    public string? Field7ARawHex { get; init; }

    public string? Field7BRawHex { get; init; }

    public IReadOnlyList<int>? Field7BPackedValues { get; init; }

    public string? Field33RawHex { get; init; }

    public Vector2Value? Field33Vector { get; init; }

    public int? Field41 { get; init; }

    public int? Field78 { get; init; }

    public int? Field79 { get; init; }

    public int? Field7C { get; init; }

    public required IReadOnlyList<int> ZeroLengthMarkerFieldIds { get; init; }

    public required IReadOnlyList<FieldValueSummary> Fields { get; init; }
}

public sealed class SbSceneSliceCast
{
    public required int Index { get; init; }

    public required long Offset { get; init; }

    public int? Field44Count { get; init; }

    public int? TargetIndex { get; init; }

    public string? NodeName { get; init; }

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

    public bool? SlicRecordCountMatchesField44 { get; init; }

    public bool? CropReferenceCountMatchesField44 { get; init; }

    public required IReadOnlyList<SbSceneSliceRecord> Slices { get; init; }

    public required IReadOnlyList<SbSceneCropReference> CropReferences { get; init; }

    public required IReadOnlyList<FieldValueSummary> Fields { get; init; }
}

public sealed class SbSceneSliceRecord
{
    public required int Index { get; init; }

    public required long Offset { get; init; }

    public int? Field83 { get; init; }

    public int? Field40 { get; init; }

    public int? Field41 { get; init; }

    public int? Field45 { get; init; }

    public ColorArgbValue? Field37Color { get; init; }

    public string? Field37RawHex { get; init; }

    public ColorArgbValue? Field38Color { get; init; }

    public string? Field38RawHex { get; init; }

    public required IReadOnlyList<ColorArgbValue> Field39Colors { get; init; }

    public IReadOnlyList<string>? Field39RawHexValues { get; init; }

    public required IReadOnlyList<FieldValueSummary> Fields { get; init; }
}

public sealed class SbSceneCropRect
{
    public required int Index { get; init; }

    public required string RawHex { get; init; }

    public required byte Kind { get; init; }

    public required int Left { get; init; }

    public required int Top { get; init; }

    public required int Right { get; init; }

    public required int Bottom { get; init; }

    public int Width => Right - Left;

    public int Height => Bottom - Top;
}

public sealed class ImageExtractionResult
{
    public required string OutputDirectory { get; init; }

    public required int AtlasCount { get; init; }

    public required int CropCount { get; init; }

    public required int ImageCastCount { get; init; }

    public required IReadOnlyList<string> Warnings { get; init; }
}
