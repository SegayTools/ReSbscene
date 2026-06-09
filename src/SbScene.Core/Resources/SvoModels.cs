using SbScene.Core.Semantics;

namespace SbScene.Core.Resources;

/// <summary>
/// 表示 SVO 中的纹理资源，保存 DDS 字节、尺寸、格式和 atlas 关联信息。
/// </summary>
public sealed class SvoTextureResource
{
    /// <summary>
    /// 获取或设置索引，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required int Index { get; init; }

    /// <summary>
    /// 获取或设置目录索引，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public required int DirectoryIndex { get; init; }

    /// <summary>
    /// 获取或设置文件名称，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public string? FileName { get; init; }

    /// <summary>
    /// 获取或设置Atlas名称，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public string? AtlasName { get; init; }

    /// <summary>
    /// 获取或设置文件内偏移，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required long Offset { get; init; }

    /// <summary>
    /// 获取或设置字节长度，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required int Length { get; init; }

    /// <summary>
    /// 获取或设置宽度，用于确定渲染区域、裁剪范围、采样质量或输出尺寸。
    /// </summary>
    public required int Width { get; init; }

    /// <summary>
    /// 获取或设置高度，用于确定渲染区域、裁剪范围、采样质量或输出尺寸。
    /// </summary>
    public required int Height { get; init; }

    /// <summary>
    /// 获取或设置格式，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public required string Format { get; init; }

    /// <summary>
    /// 获取或设置DDS字节字段类型代码，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required byte[] DdsBytes { get; init; }
}

/// <summary>
/// 表示 SVO AVTS 头部信息，记录目录数量、头部大小和未知字段统计。
/// </summary>
public sealed class SvoHeaderInfo
{
    /// <summary>
    /// 获取或设置文件魔数，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required string Magic { get; init; }

    /// <summary>
    /// 获取或设置目录数量，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public required int DirectoryCount { get; init; }

    /// <summary>
    /// 获取或设置Header大小，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required int HeaderSize { get; init; }

    /// <summary>
    /// 获取或设置目录Table文件内偏移，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required int DirectoryTableOffset { get; init; }

    /// <summary>
    /// 获取或设置目录Entry大小，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required int DirectoryEntrySize { get; init; }

    /// <summary>
    /// 获取或设置HeaderUnknownNonZeroByte数量，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required int HeaderUnknownNonZeroByteCount { get; init; }

    /// <summary>
    /// 获取或设置HeaderUnknownNonZeroByteOffsets，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required IReadOnlyList<int> HeaderUnknownNonZeroByteOffsets { get; init; }

    /// <summary>
    /// 获取或设置UnknownWords，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public required IReadOnlyList<SvoHeaderUnknownWord> UnknownWords { get; init; }
}

/// <summary>
/// 表示 SVO 头部未知 32 位字段，用于保留偏移和值。
/// </summary>
public sealed class SvoHeaderUnknownWord
{
    /// <summary>
    /// 获取或设置文件内偏移，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required int Offset { get; init; }

    /// <summary>
    /// 获取或设置值，用于描述动画时间轴、关键帧值或插值方式，影响采样、渲染和导出曲线。
    /// </summary>
    public required long Value { get; init; }
}

/// <summary>
/// 表示 SVO 目录项，记录资源名称、类型、偏移、长度和边界状态。
/// </summary>
public sealed class SvoDirectoryEntry
{
    /// <summary>
    /// 获取或设置索引，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required int Index { get; init; }

    /// <summary>
    /// 获取或设置Entry文件内偏移，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required long EntryOffset { get; init; }

    /// <summary>
    /// 获取或设置名称，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 获取或设置类别，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public required int Kind { get; init; }

    /// <summary>
    /// 获取或设置Sequence，用于记录统计或范围信息，便于校验结构规模、覆盖率和异常样本。
    /// </summary>
    public required int Sequence { get; init; }

    /// <summary>
    /// 获取或设置数据文件内偏移，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required long DataOffset { get; init; }

    /// <summary>
    /// 获取或设置数据字节长度，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required int DataLength { get; init; }

    /// <summary>
    /// 获取或设置IsIn边界，用于确定渲染区域、裁剪范围、采样质量或输出尺寸。
    /// </summary>
    public required bool IsInBounds { get; init; }

    /// <summary>
    /// 获取或设置IsDDS，用于控制对应功能开关，调用方可据此改变解析、渲染或导出策略。
    /// </summary>
    public required bool IsDds { get; init; }

    /// <summary>
    /// 获取或设置数据文件魔数，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public string? DataMagic { get; init; }

    /// <summary>
    /// 获取或设置ReservedNonZeroByte数量，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required int ReservedNonZeroByteCount { get; init; }

    /// <summary>
    /// 获取或设置ReservedNonZeroByteOffsets，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required IReadOnlyList<int> ReservedNonZeroByteOffsets { get; init; }
}

/// <summary>
/// 表示 SVO YABX 元数据，保存字符串、类型、字段、对象和引用关系。
/// </summary>
public sealed class SvoMetadataInfo
{
    /// <summary>
    /// 获取或设置目录索引，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public required int DirectoryIndex { get; init; }

    /// <summary>
    /// 获取或设置文件内偏移，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required long Offset { get; init; }

    /// <summary>
    /// 获取或设置字节长度，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required int Length { get; init; }

    /// <summary>
    /// 获取或设置文件魔数，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required string Magic { get; init; }

    /// <summary>
    /// 获取或设置版本，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public int? Version { get; init; }

    /// <summary>
    /// 获取或设置Declared载荷字节长度，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public int? DeclaredPayloadLength { get; init; }

    /// <summary>
    /// 获取或设置Header哈希Candidate，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public long? HeaderHashCandidate { get; init; }

    /// <summary>
    /// 获取或设置Strings，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public required IReadOnlyList<SvoMetadataString> Strings { get; init; }

    /// <summary>
    /// 获取或设置类型名称集合，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public required IReadOnlyList<string> TypeNames { get; init; }

    /// <summary>
    /// 获取或设置类型Schemas，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public required IReadOnlyList<SvoMetadataTypeInfo> TypeSchemas { get; init; }

    /// <summary>
    /// 获取或设置ObjectSection文件内偏移，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public int? ObjectSectionOffset { get; init; }

    /// <summary>
    /// 获取或设置DeclaredObject数量，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public int? DeclaredObjectCount { get; init; }

    /// <summary>
    /// 获取或设置Object引用Base，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public int? ObjectReferenceBase { get; init; }

    /// <summary>
    /// 获取或设置Objects，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required IReadOnlyList<SvoMetadataObject> Objects { get; init; }

    /// <summary>
    /// 获取或设置字段名称集合，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public required IReadOnlyList<string> FieldNames { get; init; }

    /// <summary>
    /// 获取或设置资源名称集合，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public required IReadOnlyList<string> ResourceNames { get; init; }

    /// <summary>
    /// 获取或设置资源集合，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public required IReadOnlyList<SvoMetadataResource> Resources { get; init; }
}

/// <summary>
/// 表示 SVO 元数据字符串条目，保存文本和文件内偏移。
/// </summary>
public sealed class SvoMetadataString
{
    /// <summary>
    /// 获取或设置文件内偏移，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required int Offset { get; init; }

    /// <summary>
    /// 获取或设置Absolute文件内偏移，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required long AbsoluteOffset { get; init; }

    /// <summary>
    /// 获取或设置文本，用于保存源字段文本或诊断说明，便于展示、校验和导出报告。
    /// </summary>
    public required string Text { get; init; }
}

/// <summary>
/// 表示 SVO 元数据类型定义，记录类型名称和字段集合。
/// </summary>
public sealed class SvoMetadataTypeInfo
{
    /// <summary>
    /// 获取或设置类型索引，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public int? TypeIndex { get; init; }

    /// <summary>
    /// 获取或设置名称，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 获取或设置字段明细集合，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public required IReadOnlyList<string> Fields { get; init; }

    /// <summary>
    /// 获取或设置FieldDescriptors 字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public required IReadOnlyList<SvoMetadataFieldInfo> FieldDescriptors { get; init; }
}

/// <summary>
/// 表示 SVO 元数据字段定义，记录字段名称、类型和容量信息。
/// </summary>
public sealed class SvoMetadataFieldInfo
{
    /// <summary>
    /// 获取或设置名称，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 获取或设置文件内偏移，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required int Offset { get; init; }

    /// <summary>
    /// 获取或设置Descriptor文件内偏移，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required int DescriptorOffset { get; init; }

    /// <summary>
    /// 获取或设置AbsoluteDescriptor文件内偏移，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required long AbsoluteDescriptorOffset { get; init; }

    /// <summary>
    /// 获取或设置原始字节内容DescriptorHex，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required string RawDescriptorHex { get; init; }

    /// <summary>
    /// 获取或设置Flags，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public required byte Flags { get; init; }

    /// <summary>
    /// 获取或设置值类别，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public required byte ValueKind { get; init; }

    /// <summary>
    /// 获取或设置Reserved，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required byte Reserved { get; init; }

    /// <summary>
    /// 获取或设置值类别名称，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public required string ValueKindName { get; init; }
}

/// <summary>
/// 表示 SVO 元数据对象，保存对象类型、字段集合和原始位置。
/// </summary>
public sealed class SvoMetadataObject
{
    /// <summary>
    /// 获取或设置索引，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required int Index { get; init; }

    /// <summary>
    /// 获取或设置引用标识，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public int? ReferenceId { get; init; }

    /// <summary>
    /// 获取或设置文件内偏移，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required int Offset { get; init; }

    /// <summary>
    /// 获取或设置Absolute文件内偏移，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required long AbsoluteOffset { get; init; }

    /// <summary>
    /// 获取或设置载荷起始偏移，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required int PayloadOffset { get; init; }

    /// <summary>
    /// 获取或设置类型索引，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public required int TypeIndex { get; init; }

    /// <summary>
    /// 获取或设置字段类型名称，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public string? TypeName { get; init; }

    /// <summary>
    /// 获取或设置载荷字节长度，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required int PayloadLength { get; init; }

    /// <summary>
    /// 获取或设置Parsed字段Byte数量，用于报告数量或统计值，便于调用方校验结构规模和处理结果。
    /// </summary>
    public int ParsedFieldByteCount { get; init; }

    /// <summary>
    /// 获取或设置UnparsedByte数量，用于报告数量或统计值，便于调用方校验结构规模和处理结果。
    /// </summary>
    public int UnparsedByteCount { get; init; }

    /// <summary>
    /// 获取或设置Unparsed字节字段类型代码诊断预览文本Hex，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public string? UnparsedBytesPreviewHex { get; init; }

    /// <summary>
    /// 获取或设置Strings，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public required IReadOnlyList<SvoMetadataObjectString> Strings { get; init; }

    /// <summary>
    /// 获取或设置字段明细集合，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public required IReadOnlyList<SvoMetadataObjectField> Fields { get; init; }
}

/// <summary>
/// 表示 SVO 元数据对象中的字符串字段值。
/// </summary>
public sealed class SvoMetadataObjectString
{
    /// <summary>
    /// 获取或设置文件内偏移，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required int Offset { get; init; }

    /// <summary>
    /// 获取或设置文本，用于保存源字段文本或诊断说明，便于展示、校验和导出报告。
    /// </summary>
    public required string Text { get; init; }
}

/// <summary>
/// 表示 SVO 元数据对象字段，保存类型、整数值、字符串值和引用信息。
/// </summary>
public sealed class SvoMetadataObjectField
{
    /// <summary>
    /// 获取或设置名称，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 获取或设置文件内偏移，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required int Offset { get; init; }

    /// <summary>
    /// 获取或设置字节长度，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required int Length { get; init; }

    /// <summary>
    /// 获取或设置类别，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>
    /// 获取或设置原始字节内容Hex，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public string? RawHex { get; init; }

    /// <summary>
    /// 获取或设置整数值，用于描述动画时间轴、关键帧值或插值方式，影响采样、渲染和导出曲线。
    /// </summary>
    public long? IntValue { get; init; }

    /// <summary>
    /// 获取或设置字段解码后的字符串值，用于保存源字段文本或诊断说明，便于展示、校验和导出报告。
    /// </summary>
    public string? StringValue { get; init; }

    /// <summary>
    /// 获取或设置引用标识，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public int? ReferenceId { get; init; }

    /// <summary>
    /// 获取或设置引用目标Object索引，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public int? ReferenceTargetObjectIndex { get; init; }

    /// <summary>
    /// 获取或设置引用目标类型名称，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public string? ReferenceTargetTypeName { get; init; }

    /// <summary>
    /// 获取或设置引用Ids，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public IReadOnlyList<int>? ReferenceIds { get; init; }

    /// <summary>
    /// 获取或设置引用Targets，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public IReadOnlyList<SvoMetadataReferenceTarget>? ReferenceTargets { get; init; }

    /// <summary>
    /// 获取或设置Capacity，用于描述位置、旋转、缩放或矩阵状态，参与渲染坐标和导出坐标计算。
    /// </summary>
    public int? Capacity { get; init; }

    /// <summary>
    /// 获取或设置字符串字段类型代码字节长度WithNull，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public int? StringLengthWithNull { get; init; }
}

/// <summary>
/// 表示 SVO 元数据引用目标，记录被引用对象的类型和索引。
/// </summary>
public sealed class SvoMetadataReferenceTarget
{
    /// <summary>
    /// 获取或设置引用标识，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required int ReferenceId { get; init; }

    /// <summary>
    /// 获取或设置Object索引，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public int? ObjectIndex { get; init; }

    /// <summary>
    /// 获取或设置字段类型名称，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public string? TypeName { get; init; }
}

/// <summary>
/// 表示 SVO 元数据资源项，记录资源名称、atlas 名称和文件关联。
/// </summary>
public sealed class SvoMetadataResource
{
    /// <summary>
    /// 获取或设置Atlas名称，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public required string AtlasName { get; init; }

    /// <summary>
    /// 获取或设置纹理Object索引，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public int? TextureObjectIndex { get; init; }

    /// <summary>
    /// 获取或设置图像Object索引，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public int? ImageObjectIndex { get; init; }

    /// <summary>
    /// 获取或设置纹理引用标识，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public int? TextureReferenceId { get; init; }

    /// <summary>
    /// 获取或设置图像引用标识，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public int? ImageReferenceId { get; init; }

    /// <summary>
    /// 获取或设置纹理图像引用标识，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public int? TextureImageReferenceId { get; init; }

    /// <summary>
    /// 获取或设置文件名称，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public string? FileName { get; init; }

    /// <summary>
    /// 获取或设置Chunk文件名称，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public string? ChunkFileName { get; init; }

    /// <summary>
    /// 获取或设置Metadata宽度，用于确定渲染区域、裁剪范围、采样质量或输出尺寸。
    /// </summary>
    public int? MetadataWidth { get; init; }

    /// <summary>
    /// 获取或设置Metadata高度，用于确定渲染区域、裁剪范围、采样质量或输出尺寸。
    /// </summary>
    public int? MetadataHeight { get; init; }

    /// <summary>
    /// 获取或设置Metadata格式代码，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public int? MetadataFormatCode { get; init; }

    /// <summary>
    /// 获取或设置Metadata数据大小，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public int? MetadataDataSize { get; init; }

    /// <summary>
    /// 获取或设置目录索引，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public int? DirectoryIndex { get; init; }

    /// <summary>
    /// 获取或设置数据文件内偏移，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public long? DataOffset { get; init; }

    /// <summary>
    /// 获取或设置数据字节长度，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public int? DataLength { get; init; }

    /// <summary>
    /// 获取或设置宽度，用于确定渲染区域、裁剪范围、采样质量或输出尺寸。
    /// </summary>
    public int? Width { get; init; }

    /// <summary>
    /// 获取或设置高度，用于确定渲染区域、裁剪范围、采样质量或输出尺寸。
    /// </summary>
    public int? Height { get; init; }

    /// <summary>
    /// 获取或设置格式，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public string? Format { get; init; }
}

/// <summary>
/// 表示 sbscene 纹理 atlas，保存纹理索引、名称、尺寸和裁剪区域。
/// </summary>
public sealed class SbSceneTextureAtlas
{
    /// <summary>
    /// 获取或设置索引，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required int Index { get; init; }

    /// <summary>
    /// 获取或设置文件内偏移，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required long Offset { get; init; }

    /// <summary>
    /// 获取或设置名称，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 获取或设置宽度，用于确定渲染区域、裁剪范围、采样质量或输出尺寸。
    /// </summary>
    public required int Width { get; init; }

    /// <summary>
    /// 获取或设置高度，用于确定渲染区域、裁剪范围、采样质量或输出尺寸。
    /// </summary>
    public required int Height { get; init; }

    /// <summary>
    /// 获取或设置Field62 未命名字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public int? Field62 { get; init; }

    /// <summary>
    /// 获取或设置Field62Bits 字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public required IReadOnlyList<int> Field62Bits { get; init; }

    /// <summary>
    /// 获取或设置DeclaredCrop数量，用于报告数量或统计值，便于调用方校验结构规模和处理结果。
    /// </summary>
    public required int DeclaredCropCount { get; init; }

    /// <summary>
    /// 获取或设置Crops，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public required IReadOnlyList<SbSceneCropRect> Crops { get; init; }
}

/// <summary>
/// 表示 sbscene image cast，保存节点绑定、图像尺寸、pivot、状态标志和 crop 引用。
/// </summary>
public sealed class SbSceneImageCast
{
    /// <summary>
    /// 获取或设置索引，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required int Index { get; init; }

    /// <summary>
    /// 获取或设置文件内偏移，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required long Offset { get; init; }

    /// <summary>
    /// 获取或设置图像CastFlags，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required int ImageCastFlags { get; init; }

    /// <summary>
    /// 获取或设置图像CastFlagBits，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required IReadOnlyList<int> ImageCastFlagBits { get; init; }

    /// <summary>
    /// 获取或设置Cast索引，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required int CastIndex { get; init; }

    /// <summary>
    /// 获取或设置节点名称，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public string? NodeName { get; init; }

    /// <summary>
    /// 获取或设置宽度，用于确定渲染区域、裁剪范围、采样质量或输出尺寸。
    /// </summary>
    public required float Width { get; init; }

    /// <summary>
    /// 获取或设置高度，用于确定渲染区域、裁剪范围、采样质量或输出尺寸。
    /// </summary>
    public required float Height { get; init; }

    /// <summary>
    /// 获取或设置轴心X，用于确定渲染区域、裁剪范围、采样质量或输出尺寸。
    /// </summary>
    public required float PivotX { get; init; }

    /// <summary>
    /// 获取或设置轴心Y，用于确定渲染区域、裁剪范围、采样质量或输出尺寸。
    /// </summary>
    public required float PivotY { get; init; }

    /// <summary>
    /// 获取或设置DeclaredCrop引用数量，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public int? DeclaredCropReferenceCount { get; init; }

    /// <summary>
    /// 获取或设置PrimaryCrop引用数量，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public int? PrimaryCropReferenceCount { get; init; }

    /// <summary>
    /// 获取或设置SecondaryCrop引用数量，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public int? SecondaryCropReferenceCount { get; init; }

    /// <summary>
    /// 获取或设置SecondaryCropFlag，用于标记 secondary 裁剪引用状态，供 SVO 纹理解析和诊断输出使用。
    /// </summary>
    public int? SecondaryCropFlag { get; init; }

    /// <summary>
    /// 获取或设置PrimaryCrop索引，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public int? PrimaryCropIndex { get; init; }

    /// <summary>
    /// 获取或设置SecondaryCrop索引，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public int? SecondaryCropIndex { get; init; }

    /// <summary>
    /// 获取或设置PrimaryCrop引用索引，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public int? PrimaryCropReferenceIndex { get; init; }

    /// <summary>
    /// 获取或设置SecondaryCrop引用索引，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public int? SecondaryCropReferenceIndex { get; init; }

    /// <summary>
    /// 表示ActualCrop引用数量，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public int ActualCropReferenceCount => CropReferences.Count;

    /// <summary>
    /// 表示ActualPrimaryCrop引用数量，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public int ActualPrimaryCropReferenceCount => PrimaryCropReferences.Count;

    /// <summary>
    /// 表示ActualSecondaryCrop引用数量，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public int ActualSecondaryCropReferenceCount => SecondaryCropReferences.Count;

    /// <summary>
    /// 获取或设置Crop引用数量Matches，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public bool? CropReferenceCountMatches { get; init; }

    /// <summary>
    /// 获取或设置Crop索引值集合，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required IReadOnlyList<int> CropIndexValues { get; init; }

    /// <summary>
    /// 获取或设置CropRef数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public required IReadOnlyList<int> CropRefCounts { get; init; }

    /// <summary>
    /// 获取或设置PrimaryCrop引用集合，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required IReadOnlyList<SbSceneCropReference> PrimaryCropReferences { get; init; }

    /// <summary>
    /// 获取或设置SecondaryCrop引用集合，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required IReadOnlyList<SbSceneCropReference> SecondaryCropReferences { get; init; }

    /// <summary>
    /// 获取或设置Crop引用集合，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required IReadOnlyList<SbSceneCropReference> CropReferences { get; init; }
}

/// <summary>
/// 表示 crop 引用，记录 atlas、裁剪索引、纹理索引和原始字段。
/// </summary>
public sealed class SbSceneCropReference
{
    /// <summary>
    /// 获取或设置索引，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required int Index { get; init; }

    /// <summary>
    /// 获取或设置原始字节内容Hex，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required string RawHex { get; init; }

    /// <summary>
    /// 获取或设置类别，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public required byte Kind { get; init; }

    /// <summary>
    /// 获取或设置纹理List索引，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required int TextureListIndex { get; init; }

    /// <summary>
    /// 获取或设置纹理索引，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required int TextureIndex { get; init; }

    /// <summary>
    /// 获取或设置Crop索引，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required int CropIndex { get; init; }

    /// <summary>
    /// 获取或设置Atlas名称，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public string? AtlasName { get; init; }

    /// <summary>
    /// 获取或设置Crop路径，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public string? CropPath { get; init; }
}

/// <summary>
/// 表示 sbscene 资源映射，集中保存 atlas、image cast、slice 和文本资源记录。
/// </summary>
public sealed class SbSceneResourceMap
{
    /// <summary>
    /// 获取或设置纹理List名称，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public string? TextureListName { get; init; }

    /// <summary>
    /// 获取或设置Declared纹理数量，用于报告数量或统计值，便于调用方校验结构规模和处理结果。
    /// </summary>
    public int? DeclaredTextureCount { get; init; }

    /// <summary>
    /// 获取或设置Atlases，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public required IReadOnlyList<SbSceneTextureAtlas> Atlases { get; init; }

    /// <summary>
    /// 获取或设置图像Casts，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required IReadOnlyList<SbSceneImageCast> ImageCasts { get; init; }

    /// <summary>
    /// 获取或设置CnumRecords，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public required IReadOnlyList<SbSceneCnumRecord> CnumRecords { get; init; }

    /// <summary>
    /// 获取或设置CrfdRecords，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public required IReadOnlyList<SbSceneCrfdRecord> CrfdRecords { get; init; }

    /// <summary>
    /// 获取或设置文本Records，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public required IReadOnlyList<SbSceneTextRecord> TextRecords { get; init; }

    /// <summary>
    /// 获取或设置SliceCasts，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public required IReadOnlyList<SbSceneSliceCast> SliceCasts { get; init; }
}

/// <summary>
/// 表示 CNUM 资源记录，保留字段解析结果和关联节点信息。
/// </summary>
public sealed class SbSceneCnumRecord
{
    /// <summary>
    /// 获取或设置索引，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required int Index { get; init; }

    /// <summary>
    /// 获取或设置文件内偏移，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required long Offset { get; init; }

    /// <summary>
    /// 获取或设置Field44Count 字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public int? Field44Count { get; init; }

    /// <summary>
    /// 获取或设置Field48 未命名字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public int? Field48 { get; init; }

    /// <summary>
    /// 获取或设置Field51 未命名字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public int? Field51 { get; init; }

    /// <summary>
    /// 获取或设置节点名称，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public string? NodeName { get; init; }

    /// <summary>
    /// 获取或设置Field40 未命名字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public float? Field40 { get; init; }

    /// <summary>
    /// 获取或设置Field42 未命名字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public float? Field42 { get; init; }

    /// <summary>
    /// 获取或设置Field43 未命名字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public float? Field43 { get; init; }

    /// <summary>
    /// 获取或设置Field39Colors 字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public IReadOnlyList<ColorArgbValue>? Field39Colors { get; init; }

    /// <summary>
    /// 获取或设置Field39RawHexValues 字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public IReadOnlyList<string>? Field39RawHexValues { get; init; }

    /// <summary>
    /// 获取或设置FieldA0 未命名字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public int? FieldA0 { get; init; }

    /// <summary>
    /// 获取或设置FieldA1 未命名字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public string? FieldA1 { get; init; }

    /// <summary>
    /// 获取或设置FieldA1RawHex 字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public string? FieldA1RawHex { get; init; }

    /// <summary>
    /// 获取或设置FieldA2 未命名字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public int? FieldA2 { get; init; }

    /// <summary>
    /// 获取或设置FieldA3 未命名字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public int? FieldA3 { get; init; }

    /// <summary>
    /// 获取或设置FieldA4 未命名字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public int? FieldA4 { get; init; }

    /// <summary>
    /// 获取或设置FieldA5 未命名字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public int? FieldA5 { get; init; }

    /// <summary>
    /// 获取或设置FieldA6 未命名字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public int? FieldA6 { get; init; }

    /// <summary>
    /// 获取或设置FieldA7 未命名字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public int? FieldA7 { get; init; }

    /// <summary>
    /// 获取或设置FieldA8 未命名字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public int? FieldA8 { get; init; }

    /// <summary>
    /// 获取或设置FieldA9 未命名字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public int? FieldA9 { get; init; }

    /// <summary>
    /// 获取或设置FieldAA 未命名字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public int? FieldAA { get; init; }

    /// <summary>
    /// 获取或设置FieldAB 未命名字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public int? FieldAB { get; init; }

    /// <summary>
    /// 获取或设置FieldAC 未命名字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public int? FieldAC { get; init; }

    /// <summary>
    /// 获取或设置FieldAD 未命名字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public int? FieldAD { get; init; }

    /// <summary>
    /// 获取或设置FieldAERawHex 字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public string? FieldAERawHex { get; init; }

    /// <summary>
    /// 获取或设置FieldAEFloatValues 字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public IReadOnlyList<float>? FieldAEFloatValues { get; init; }

    /// <summary>
    /// 获取或设置FieldAFRawHex 字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public string? FieldAFRawHex { get; init; }

    /// <summary>
    /// 获取或设置FieldAFPackedValues 字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public IReadOnlyList<int>? FieldAFPackedValues { get; init; }

    /// <summary>
    /// 获取或设置Crop引用数量MatchesField44，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public bool? CropReferenceCountMatchesField44 { get; init; }

    /// <summary>
    /// 获取或设置Zero字节长度Marker字段Ids，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required IReadOnlyList<int> ZeroLengthMarkerFieldIds { get; init; }

    /// <summary>
    /// 获取或设置Crop引用集合，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required IReadOnlyList<SbSceneCropReference> CropReferences { get; init; }

    /// <summary>
    /// 获取或设置字段明细集合，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public required IReadOnlyList<FieldValueSummary> Fields { get; init; }
}

/// <summary>
/// 表示 CRFD 资源记录，保留裁剪引用字段、节点关联和诊断状态。
/// </summary>
public sealed class SbSceneCrfdRecord
{
    /// <summary>
    /// 获取或设置索引，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required int Index { get; init; }

    /// <summary>
    /// 获取或设置文件内偏移，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required long Offset { get; init; }

    /// <summary>
    /// 获取或设置Field51 未命名字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public int? Field51 { get; init; }

    /// <summary>
    /// 获取或设置节点名称，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public string? NodeName { get; init; }

    /// <summary>
    /// 获取或设置Field90 未命名字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public string? Field90 { get; init; }

    /// <summary>
    /// 获取或设置Field90RawHex 字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public string? Field90RawHex { get; init; }

    /// <summary>
    /// 获取或设置Field91 未命名字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public string? Field91 { get; init; }

    /// <summary>
    /// 获取或设置Field91RawHex 字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public string? Field91RawHex { get; init; }

    /// <summary>
    /// 获取或设置Field92 未命名字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public int? Field92 { get; init; }

    /// <summary>
    /// 获取或设置Field93 未命名字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public int? Field93 { get; init; }

    /// <summary>
    /// 获取或设置Field94 未命名字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public float? Field94 { get; init; }

    /// <summary>
    /// 获取或设置Field95 未命名字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public int? Field95 { get; init; }

    /// <summary>
    /// 获取或设置字段明细集合，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public required IReadOnlyList<FieldValueSummary> Fields { get; init; }
}

/// <summary>
/// 表示文本资源记录，保存文本内容、字段摘要和关联节点信息。
/// </summary>
public sealed class SbSceneTextRecord
{
    /// <summary>
    /// 获取或设置索引，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required int Index { get; init; }

    /// <summary>
    /// 获取或设置文件内偏移，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required long Offset { get; init; }

    /// <summary>
    /// 获取或设置Field7A 未命名字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public string? Field7A { get; init; }

    /// <summary>
    /// 获取或设置Field7AShiftJis 字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public string? Field7AShiftJis { get; init; }

    /// <summary>
    /// 获取或设置Field7ARawHex 字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public string? Field7ARawHex { get; init; }

    /// <summary>
    /// 获取或设置Field7BRawHex 字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public string? Field7BRawHex { get; init; }

    /// <summary>
    /// 获取或设置Field7BPackedValues 字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public IReadOnlyList<int>? Field7BPackedValues { get; init; }

    /// <summary>
    /// 获取或设置Field33RawHex 字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public string? Field33RawHex { get; init; }

    /// <summary>
    /// 获取或设置Field33Vector 字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public Vector2Value? Field33Vector { get; init; }

    /// <summary>
    /// 获取或设置Field41 未命名字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public int? Field41 { get; init; }

    /// <summary>
    /// 获取或设置Field78 未命名字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public int? Field78 { get; init; }

    /// <summary>
    /// 获取或设置Field79 未命名字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public int? Field79 { get; init; }

    /// <summary>
    /// 获取或设置Field7C 未命名字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public int? Field7C { get; init; }

    /// <summary>
    /// 获取或设置Zero字节长度Marker字段Ids，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required IReadOnlyList<int> ZeroLengthMarkerFieldIds { get; init; }

    /// <summary>
    /// 获取或设置字段明细集合，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public required IReadOnlyList<FieldValueSummary> Fields { get; init; }
}

/// <summary>
/// 表示 slice cast 资源记录，保存切片图像和节点关联信息。
/// </summary>
public sealed class SbSceneSliceCast
{
    /// <summary>
    /// 获取或设置索引，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required int Index { get; init; }

    /// <summary>
    /// 获取或设置文件内偏移，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required long Offset { get; init; }

    /// <summary>
    /// 获取或设置Field44Count 字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public int? Field44Count { get; init; }

    /// <summary>
    /// 获取或设置目标索引，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public int? TargetIndex { get; init; }

    /// <summary>
    /// 获取或设置节点名称，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public string? NodeName { get; init; }

    /// <summary>
    /// 获取或设置Field40 未命名字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public float? Field40 { get; init; }

    /// <summary>
    /// 获取或设置Field41 未命名字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public float? Field41 { get; init; }

    /// <summary>
    /// 获取或设置Field42 未命名字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public float? Field42 { get; init; }

    /// <summary>
    /// 获取或设置Field43 未命名字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public float? Field43 { get; init; }

    /// <summary>
    /// 获取或设置Field80 未命名字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public int? Field80 { get; init; }

    /// <summary>
    /// 获取或设置Field81 未命名字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public int? Field81 { get; init; }

    /// <summary>
    /// 获取或设置Field82 未命名字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public int? Field82 { get; init; }

    /// <summary>
    /// 获取或设置Field84 未命名字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public int? Field84 { get; init; }

    /// <summary>
    /// 获取或设置Field85 未命名字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public int? Field85 { get; init; }

    /// <summary>
    /// 获取或设置Field86 未命名字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public float? Field86 { get; init; }

    /// <summary>
    /// 获取或设置Field87 未命名字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public float? Field87 { get; init; }

    /// <summary>
    /// 获取或设置SlicRecord数量MatchesField44，用于报告数量或统计值，便于调用方校验结构规模和处理结果。
    /// </summary>
    public bool? SlicRecordCountMatchesField44 { get; init; }

    /// <summary>
    /// 获取或设置Crop引用数量MatchesField44，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public bool? CropReferenceCountMatchesField44 { get; init; }

    /// <summary>
    /// 获取或设置Slices，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public required IReadOnlyList<SbSceneSliceRecord> Slices { get; init; }

    /// <summary>
    /// 获取或设置Crop引用集合，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required IReadOnlyList<SbSceneCropReference> CropReferences { get; init; }

    /// <summary>
    /// 获取或设置字段明细集合，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public required IReadOnlyList<FieldValueSummary> Fields { get; init; }
}

/// <summary>
/// 表示 slice 资源记录，保存切片、crop 引用和字段摘要。
/// </summary>
public sealed class SbSceneSliceRecord
{
    /// <summary>
    /// 获取或设置索引，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required int Index { get; init; }

    /// <summary>
    /// 获取或设置文件内偏移，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required long Offset { get; init; }

    /// <summary>
    /// 获取或设置Field83 未命名字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public int? Field83 { get; init; }

    /// <summary>
    /// 获取或设置Field40 未命名字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public int? Field40 { get; init; }

    /// <summary>
    /// 获取或设置Field41 未命名字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public int? Field41 { get; init; }

    /// <summary>
    /// 获取或设置Field45 未命名字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public int? Field45 { get; init; }

    /// <summary>
    /// 获取或设置Field37Color 字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public ColorArgbValue? Field37Color { get; init; }

    /// <summary>
    /// 获取或设置Field37RawHex 字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public string? Field37RawHex { get; init; }

    /// <summary>
    /// 获取或设置Field38Color 字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public ColorArgbValue? Field38Color { get; init; }

    /// <summary>
    /// 获取或设置Field38RawHex 字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public string? Field38RawHex { get; init; }

    /// <summary>
    /// 获取或设置Field39Colors 字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public required IReadOnlyList<ColorArgbValue> Field39Colors { get; init; }

    /// <summary>
    /// 获取或设置Field39RawHexValues 字段，用于保留尚未命名的源格式字段，保证 inspect 和 JSON 输出不丢失原始信息。
    /// </summary>
    public IReadOnlyList<string>? Field39RawHexValues { get; init; }

    /// <summary>
    /// 获取或设置字段明细集合，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public required IReadOnlyList<FieldValueSummary> Fields { get; init; }
}

/// <summary>
/// 表示 crop 矩形，记录 atlas 中的裁剪坐标和尺寸。
/// </summary>
public sealed class SbSceneCropRect
{
    /// <summary>
    /// 获取或设置索引，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required int Index { get; init; }

    /// <summary>
    /// 获取或设置原始字节内容Hex，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required string RawHex { get; init; }

    /// <summary>
    /// 获取或设置类别，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public required byte Kind { get; init; }

    /// <summary>
    /// 获取或设置左边界，用于确定渲染区域、裁剪范围、采样质量或输出尺寸。
    /// </summary>
    public required int Left { get; init; }

    /// <summary>
    /// 获取或设置上边界，用于确定渲染区域、裁剪范围、采样质量或输出尺寸。
    /// </summary>
    public required int Top { get; init; }

    /// <summary>
    /// 获取或设置右边界，用于确定渲染区域、裁剪范围、采样质量或输出尺寸。
    /// </summary>
    public required int Right { get; init; }

    /// <summary>
    /// 获取或设置下边界，用于确定渲染区域、裁剪范围、采样质量或输出尺寸。
    /// </summary>
    public required int Bottom { get; init; }

    /// <summary>
    /// 表示宽度，用于确定渲染区域、裁剪范围、采样质量或输出尺寸。
    /// </summary>
    public int Width => Right - Left;

    /// <summary>
    /// 表示高度，用于确定渲染区域、裁剪范围、采样质量或输出尺寸。
    /// </summary>
    public int Height => Bottom - Top;
}

/// <summary>
/// 表示图像Extraction结果，封装处理产物、统计信息和诊断状态。
/// </summary>
public sealed class ImageExtractionResult
{
    /// <summary>
    /// 获取或设置输出目录，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public required string OutputDirectory { get; init; }

    /// <summary>
    /// 获取或设置Atlas数量，用于报告数量或统计值，便于调用方校验结构规模和处理结果。
    /// </summary>
    public required int AtlasCount { get; init; }

    /// <summary>
    /// 获取或设置Crop数量，用于报告数量或统计值，便于调用方校验结构规模和处理结果。
    /// </summary>
    public required int CropCount { get; init; }

    /// <summary>
    /// 获取或设置图像Cast数量，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required int ImageCastCount { get; init; }

    /// <summary>
    /// 获取或设置非致命警告列表，用于把非致命问题返回给调用方，便于诊断解析、渲染或导出过程。
    /// </summary>
    public required IReadOnlyList<string> Warnings { get; init; }
}
