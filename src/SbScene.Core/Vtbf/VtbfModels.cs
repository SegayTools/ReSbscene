using System.Text.Json.Serialization;

namespace SbScene.Core.Vtbf;

/// <summary>
/// 表示完整 VTBF 文档，保存根块、块统计、文件长度和解析警告。
/// </summary>
public sealed class VtbfDocument
{
    /// <summary>
    /// 获取或设置文件魔数，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required string Magic { get; init; }

    /// <summary>
    /// 获取或设置字节长度，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required long Length { get; init; }

    /// <summary>
    /// 获取或设置VTBF 根块集合，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public required IReadOnlyList<VtbfBlock> Blocks { get; init; }

    /// <summary>
    /// 获取或设置按块标签统计的数量，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public required IReadOnlyDictionary<string, int> BlockCounts { get; init; }

    /// <summary>
    /// 获取或设置非致命警告列表，用于把非致命问题返回给调用方，便于诊断解析、渲染或导出过程。
    /// </summary>
    public required IReadOnlyList<string> Warnings { get; init; }
}

/// <summary>
/// 表示 VTBF 块，记录块标签、偏移、参数、字段集合和子块层级。
/// </summary>
public sealed class VtbfBlock
{
    /// <summary>
    /// 获取或设置Tag，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public required string Tag { get; init; }

    /// <summary>
    /// 获取或设置文件内偏移，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required long Offset { get; init; }

    /// <summary>
    /// 获取或设置内容起始偏移，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required long ContentOffset { get; init; }

    /// <summary>
    /// 获取或设置结束偏移，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required long EndOffset { get; init; }

    /// <summary>
    /// 获取或设置字节长度，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required int Length { get; init; }

    /// <summary>
    /// 获取或设置Property数量，用于报告数量或统计值，便于调用方校验结构规模和处理结果。
    /// </summary>
    public required int PropertyCount { get; init; }

    /// <summary>
    /// 获取或设置子级数量，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required int ChildCount { get; init; }

    /// <summary>
    /// 获取或设置块参数低位值，用于描述动画时间轴、关键帧值或插值方式，影响采样、渲染和导出曲线。
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ParamLow { get; init; }

    /// <summary>
    /// 获取或设置块参数高位值，用于描述动画时间轴、关键帧值或插值方式，影响采样、渲染和导出曲线。
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ParamHigh { get; init; }

    /// <summary>
    /// 获取或设置块参数原始十六进制文本，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ParamRawHex { get; init; }

    /// <summary>
    /// 获取或设置路径，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// 获取或设置字段明细集合，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public required IReadOnlyList<VtbfField> Fields { get; init; }

    /// <summary>
    /// 获取或设置子块集合，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required IReadOnlyList<VtbfBlock> Children { get; init; }

    /// <summary>
    /// 获取或设置块尾部未解析字节，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte[]? TrailingBytes { get; init; }
}

/// <summary>
/// 表示 VTBF 字段，记录字段 ID、类型、原始载荷和解码后的预览值。
/// </summary>
public sealed class VtbfField
{
    /// <summary>
    /// 获取或设置文件内偏移，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required long Offset { get; init; }

    /// <summary>
    /// 获取或设置载荷起始偏移，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required long PayloadOffset { get; init; }

    /// <summary>
    /// 获取或设置标识，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required int Id { get; init; }

    /// <summary>
    /// 表示字段 ID 的十六进制文本，用于以十六进制形式展示源格式标识，便于诊断和与原始字节对照。
    /// </summary>
    public string IdHex => $"0x{Id:X4}";

    /// <summary>
    /// 获取或设置字段类型代码，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public required int TypeCode { get; init; }

    /// <summary>
    /// 表示字段类型代码的十六进制文本，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public string TypeHex => $"0x{TypeCode:X4}";

    /// <summary>
    /// 获取或设置字段类型名称覆盖值，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TypeNameOverride { get; init; }

    /// <summary>
    /// 表示字段类型名称，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public string TypeName => TypeNameOverride ?? VtbfFieldTypes.GetName(TypeCode);

    /// <summary>
    /// 获取或设置字段类型已知状态覆盖值，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsKnownTypeOverride { get; init; }

    /// <summary>
    /// 表示字段类型是否已知，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public bool IsKnownType => IsKnownTypeOverride ?? VtbfFieldTypes.IsKnown(TypeCode);

    /// <summary>
    /// 获取或设置数量，用于报告数量或统计值，便于调用方校验结构规模和处理结果。
    /// </summary>
    public required int Count { get; init; }

    /// <summary>
    /// 获取或设置Stride，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required int Stride { get; init; }

    /// <summary>
    /// 获取或设置原始字节内容，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required byte[] Raw { get; init; }

    /// <summary>
    /// 获取或设置字段载荷解码类别，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public required string DecodedKind { get; init; }

    /// <summary>
    /// 获取或设置字段解码后的字符串值，用于保存源字段文本或诊断说明，便于展示、校验和导出报告。
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StringValue { get; init; }

    /// <summary>
    /// 获取或设置字段解码后的整数数值集合，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long[]? Int64Values { get; init; }

    /// <summary>
    /// 获取或设置字段解码后的 Float64 数值集合，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double[]? Float64Values { get; init; }

    /// <summary>
    /// 获取或设置诊断预览文本，用于保存源字段文本或诊断说明，便于展示、校验和导出报告。
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Preview { get; init; }
}

/// <summary>
/// 提供 VTBF 字段类型代码和名称映射，用于字段载荷解码和诊断显示。
/// </summary>
public static class VtbfFieldTypes
{
    /// <summary>
    /// 表示字符串字段类型代码，用于把 VTBF 载荷解码为文本。
    /// </summary>
    public const int String = 0x0001;
    /// <summary>
    /// 表示Int32 字段类型代码，用于把 VTBF 字段载荷识别为 32 位整数数组。
    /// </summary>
    public const int Int32 = 0x0002;
    /// <summary>
    /// 表示Float32 字段类型代码，用于把 VTBF 字段载荷识别为单精度浮点数组。
    /// </summary>
    public const int Float32 = 0x0003;
    /// <summary>
    /// 表示字节字段类型代码，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public const int Bytes = 0x0004;
    /// <summary>
    /// 表示Int16 字段类型代码，用于把 VTBF 字段载荷识别为 16 位整数数组。
    /// </summary>
    public const int Int16 = 0x0005;
    /// <summary>
    /// 表示Int8 字段类型代码，用于把 VTBF 字段载荷识别为字节整数数组。
    /// </summary>
    public const int Int8 = 0x0006;
    /// <summary>
    /// 表示Float64 字段类型代码，用于把 VTBF 字段载荷识别为双精度浮点数组。
    /// </summary>
    public const int Float64 = 0x0007;
    /// <summary>
    /// 表示Int64 字段类型代码，用于把 VTBF 字段载荷识别为 64 位整数数组。
    /// </summary>
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

    /// <summary>
    /// 判断字段类型代码是否已知，帮助调用方选择后续解析或诊断分支。
    /// </summary>
    /// <param name="typeCode">VTBF 字段类型代码，用于查找类型名称或判断是否为已知类型。</param>
    /// <returns>如果字段类型代码已在 VTBF 类型表中登记则为 true；否则为 false。</returns>
    public static bool IsKnown(int typeCode) => Names.ContainsKey(typeCode);

    /// <summary>
    /// 获取字段类型名称，用于展示、比较或诊断字段载荷解析方式。
    /// </summary>
    /// <param name="typeCode">VTBF 字段类型代码，用于查找类型名称或判断是否为已知类型。</param>
    /// <returns>字段类型代码对应的可读名称；未知代码返回 Unknown。</returns>
    public static string GetName(int typeCode)
    {
        return Names.TryGetValue(typeCode, out var name)
            ? name
            : "Unknown";
    }
}
