using System.Text.Json.Serialization;

namespace SbScene.Core.Vtbf;

public sealed class VtbfDocument
{
    public required string Magic { get; init; }

    public required long Length { get; init; }

    public required IReadOnlyList<VtbfBlock> Blocks { get; init; }

    public required IReadOnlyDictionary<string, int> BlockCounts { get; init; }

    public required IReadOnlyList<string> Warnings { get; init; }
}

public sealed class VtbfBlock
{
    public required string Tag { get; init; }

    public required long Offset { get; init; }

    public required long ContentOffset { get; init; }

    public required long EndOffset { get; init; }

    public required int Length { get; init; }

    public required int PropertyCount { get; init; }

    public required int ChildCount { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ParamLow { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ParamHigh { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ParamRawHex { get; init; }

    public required string Path { get; init; }

    public required IReadOnlyList<VtbfField> Fields { get; init; }

    public required IReadOnlyList<VtbfBlock> Children { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte[]? TrailingBytes { get; init; }
}

public sealed class VtbfField
{
    public required long Offset { get; init; }

    public required long PayloadOffset { get; init; }

    public required int Id { get; init; }

    public string IdHex => $"0x{Id:X4}";

    public required int TypeCode { get; init; }

    public string TypeHex => $"0x{TypeCode:X4}";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TypeNameOverride { get; init; }

    public string TypeName => TypeNameOverride ?? VtbfFieldTypes.GetName(TypeCode);

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsKnownTypeOverride { get; init; }

    public bool IsKnownType => IsKnownTypeOverride ?? VtbfFieldTypes.IsKnown(TypeCode);

    public required int Count { get; init; }

    public required int Stride { get; init; }

    public required byte[] Raw { get; init; }

    public required string DecodedKind { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StringValue { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long[]? Int64Values { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double[]? Float64Values { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Preview { get; init; }
}

public static class VtbfFieldTypes
{
    public const int String = 0x0001;
    public const int Int32 = 0x0002;
    public const int Float32 = 0x0003;
    public const int Bytes = 0x0004;
    public const int Int16 = 0x0005;
    public const int Int8 = 0x0006;
    public const int Float64 = 0x0007;
    public const int Int64 = 0x0008;

    private static readonly IReadOnlyDictionary<int, string> Names = new Dictionary<int, string>
    {
        [String] = "String",
        [Int32] = "Int32",
        [Float32] = "Float32",
        [Bytes] = "Bytes",
        [Int16] = "Int16",
        [Int8] = "Int8",
        [Float64] = "Float64",
        [Int64] = "Int64",
    };

    public static bool IsKnown(int typeCode) => Names.ContainsKey(typeCode);

    public static string GetName(int typeCode)
    {
        return Names.TryGetValue(typeCode, out var name)
            ? name
            : "Unknown";
    }
}
