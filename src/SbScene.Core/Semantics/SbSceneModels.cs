using SbScene.Core.Vtbf;
using SbScene.Core.Resources;

namespace SbScene.Core.Semantics;

public sealed class SbSceneFile
{
    public required string SourcePath { get; init; }

    public required long SourceSize { get; init; }

    public required VtbfDocument Vtbf { get; init; }

    public required SurfboardModel Surfboard { get; init; }

    public required ParseSummary Summary { get; init; }
}

public sealed class ParseSummary
{
    public required int RootBlockCount { get; init; }

    public required int TotalBlockCount { get; init; }

    public required int NodeCount { get; init; }

    public required int AnimationCount { get; init; }

    public required int VariantHintCount { get; init; }

    public required IReadOnlyDictionary<string, int> BlockCounts { get; init; }

    public required IReadOnlyList<string> Warnings { get; init; }
}

public sealed class SurfboardModel
{
    public required IReadOnlyList<SceneObjectInfo> Objects { get; init; }

    public required IReadOnlyList<NodeInfo> Nodes { get; init; }

    public required IReadOnlyList<Transform2DInfo> Transform2DRecords { get; init; }

    public required IReadOnlyList<int> NodeCategoryRecords { get; init; }

    public required IReadOnlyList<NodeCategoryInfo> NodeCategoryDetails { get; init; }

    public required IReadOnlyList<NodeGroupInfo> NodeGroups { get; init; }

    public required SbSceneResourceMap Resources { get; init; }

    public required CameraInfo? Camera { get; init; }

    public required IReadOnlyList<AnimationInfo> Animations { get; init; }

    public required IReadOnlyList<AnimationBindingInfo> AnimationBindings { get; init; }

    public required IReadOnlyList<VariantHint> VariantHints { get; init; }

    public required IReadOnlyList<UnknownFieldInfo> UnknownFields { get; init; }
}

public sealed class SceneObjectInfo
{
    public required string Tag { get; init; }

    public required long Offset { get; init; }

    public required string Path { get; init; }

    public string? Name { get; init; }

    public required IReadOnlyList<FieldValueSummary> StringFields { get; init; }

    public required IReadOnlyList<FieldValueSummary> NumericFields { get; init; }

    public required IReadOnlyList<string> ChildTags { get; init; }
}

public sealed class NodeInfo
{
    public required int Index { get; init; }

    public required long Offset { get; init; }

    public required string Path { get; init; }

    public string? Name { get; init; }

    public int? Flags { get; init; }

    public required IReadOnlyList<int> FlagBits { get; init; }

    public int? ChildIndex { get; init; }

    public int? SiblingIndex { get; init; }

    public string? Comment { get; init; }

    public int? CategoryId { get; init; }

    public required string Group { get; init; }

    public Transform2DInfo? Transform2D { get; init; }

    public required bool HasTransform2 { get; init; }

    public required bool HasTransform3 { get; init; }

    public required bool HasData { get; init; }

    public required bool HasCategory { get; init; }

    public required IReadOnlyList<FieldValueSummary> StringFields { get; init; }

    public required IReadOnlyList<FieldValueSummary> NumericFields { get; init; }

    public required IReadOnlyList<string> ChildTags { get; init; }
}

public sealed class NodeCategoryInfo
{
    public required int Index { get; init; }

    public required long Offset { get; init; }

    public string? KindName { get; init; }

    public int? TypeByte { get; init; }

    public int? CategoryId { get; init; }

    public string? ParameterPreview { get; init; }

    public string? ParameterString { get; init; }

    public required IReadOnlyList<FieldValueSummary> Fields { get; init; }
}

public sealed class Transform2DInfo
{
    public required int Index { get; init; }

    public required long Offset { get; init; }

    public required string Path { get; init; }

    public Vector2Value? Translation { get; init; }

    public float? RotationZ { get; init; }

    public int? RotationZRaw { get; init; }

    public double? RotationZDegreesCandidate { get; init; }

    public Vector2Value? Scale { get; init; }

    public bool? Display { get; init; }

    public ColorArgbValue? MaterialColor { get; init; }

    public ColorArgbValue? IlluminationColor { get; init; }

    public required IReadOnlyList<ColorArgbValue> VertexColors { get; init; }

    public int? MultiPosFlags { get; init; }

    public int? MultiSizeFlags { get; init; }

    public required IReadOnlyList<FieldValueSummary> Fields { get; init; }
}

public sealed class Vector2Value
{
    public required float X { get; init; }

    public required float Y { get; init; }
}

public sealed class ColorArgbValue
{
    public required byte A { get; init; }

    public required byte R { get; init; }

    public required byte G { get; init; }

    public required byte B { get; init; }

    public string Hex => $"#{A:X2}{R:X2}{G:X2}{B:X2}";
}

public sealed class Vector3Value
{
    public required float X { get; init; }

    public required float Y { get; init; }

    public required float Z { get; init; }
}

public sealed class CameraInfo
{
    public required long Offset { get; init; }

    public required string Path { get; init; }

    public string? Name { get; init; }

    public Vector3Value? Position { get; init; }

    public Vector3Value? Target { get; init; }

    public int? Flags { get; init; }

    public float? NearClip { get; init; }

    public float? FarClip { get; init; }

    public required IReadOnlyList<FieldValueSummary> Fields { get; init; }
}

public sealed class NodeGroupInfo
{
    public required string Name { get; init; }

    public required int Count { get; init; }

    public required IReadOnlyList<string> NodeNames { get; init; }
}

public sealed class AnimationInfo
{
    public required int Index { get; init; }

    public required long Offset { get; init; }

    public required string Path { get; init; }

    public string? Name { get; init; }

    public required IReadOnlyList<FieldValueSummary> StringFields { get; init; }

    public required IReadOnlyList<FieldValueSummary> NumericFields { get; init; }

    public required IReadOnlyList<MotionInfo> Motions { get; init; }
}

public sealed class MotionInfo
{
    public required int Index { get; init; }

    public required long Offset { get; init; }

    public required string Path { get; init; }

    public string? Name { get; init; }

    public string? TargetName { get; init; }

    public int? TargetIndex { get; init; }

    public int? CastIndex { get; init; }

    public int? DeclaredTrackCount { get; init; }

    public required IReadOnlyList<FieldValueSummary> StringFields { get; init; }

    public required IReadOnlyList<FieldValueSummary> NumericFields { get; init; }

    public required IReadOnlyList<TrackInfo> Tracks { get; init; }
}

public sealed class TrackInfo
{
    public required int Index { get; init; }

    public required long Offset { get; init; }

    public required string Path { get; init; }

    public string? Name { get; init; }

    public int? TrackId { get; init; }

    public int? TrackType { get; init; }

    public string? TrackTypeName { get; init; }

    public int? ValueType { get; init; }

    public string? ValueTypeName { get; init; }

    public int? DeclaredKeyCountFromTrack { get; init; }

    public int DeclaredKeyCountFromKeyBlock { get; init; }

    public bool? KeyCountMatchesDeclaration { get; init; }

    public int? Flags { get; init; }

    public string? KeyValueStorage { get; init; }

    public int? TargetIndex { get; init; }

    public int? FirstFrame { get; init; }

    public int? LastFrame { get; init; }

    public int DeclaredKeyCount { get; init; }

    public required bool IsLikelyStateTrack { get; init; }

    public required IReadOnlyList<FieldValueSummary> StringFields { get; init; }

    public required IReadOnlyList<FieldValueSummary> NumericFields { get; init; }

    public required IReadOnlyList<KeyframeInfo> Keyframes { get; init; }
}

public sealed class AnimationBindingInfo
{
    public required int NodeIndex { get; init; }

    public string? NodeName { get; init; }

    public required string AnimationName { get; init; }

    public required int AnimationIndex { get; init; }

    public required int MotionIndex { get; init; }

    public required int TrackCount { get; init; }

    public required int KeyCount { get; init; }

    public required IReadOnlyList<int> TrackTypes { get; init; }

    public required IReadOnlyList<string> TrackTypeNames { get; init; }
}

public sealed class KeyframeInfo
{
    public required int Index { get; init; }

    public required long Offset { get; init; }

    public required string Path { get; init; }

    public required IReadOnlyList<FieldValueSummary> Fields { get; init; }

    public int? KeyFrame { get; init; }

    public double? ScalarValue { get; init; }

    public bool? BoolValue { get; init; }

    public int? PackedAngleRaw { get; init; }

    public double? PackedAngleDegreesCandidate { get; init; }

    public string? KeyValueTypeHex { get; init; }

    public string? KeyValueTypeName { get; init; }

    public string? KeyValueKind { get; init; }

    public int? Interpolation { get; init; }

    public string? InterpolationName { get; init; }

    public double? TangentIn { get; init; }

    public double? TangentOut { get; init; }

    public required IReadOnlyList<double> TimeCandidates { get; init; }

    public required IReadOnlyList<double> ValueCandidates { get; init; }

    public string? Preview { get; init; }
}

public sealed class VariantHint
{
    public required string Category { get; init; }

    public required string SourceKind { get; init; }

    public required string Name { get; init; }

    public required double Confidence { get; init; }

    public required string Reason { get; init; }

    public string? AnimationName { get; init; }

    public string? NodeGroup { get; init; }

    public string? TrackPath { get; init; }
}

public sealed class UnknownFieldInfo
{
    public required string OwnerTag { get; init; }

    public required string OwnerPath { get; init; }

    public required long Offset { get; init; }

    public required string IdHex { get; init; }

    public required string TypeHex { get; init; }

    public required int Count { get; init; }

    public required int Stride { get; init; }

    public string? Preview { get; init; }
}

public sealed class FieldValueSummary
{
    public required string IdHex { get; init; }

    public required string TypeHex { get; init; }

    public required string TypeName { get; init; }

    public string? Preview { get; init; }

    public long[]? Int64Values { get; init; }

    public double[]? Float64Values { get; init; }

    public string? StringValue { get; init; }
}
