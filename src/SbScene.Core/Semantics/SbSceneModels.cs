using SbScene.Core.Vtbf;
using SbScene.Core.Resources;

namespace SbScene.Core.Semantics;

/// <summary>
/// 表示一次 sbscene 解析结果，汇集源文件信息、VTBF 文档、Surfboard 模型和摘要。
/// </summary>
public sealed class SbSceneFile
{
    /// <summary>
    /// 获取或设置来源信息路径，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public required string SourcePath { get; init; }

    /// <summary>
    /// 获取或设置来源信息大小，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required long SourceSize { get; init; }

    /// <summary>
    /// 获取或设置VTBF，用于保留解析后的 VTBF 块和字段层级，支撑语义分析、inspect 和 JSON 输出。
    /// </summary>
    public required VtbfDocument Vtbf { get; init; }

    /// <summary>
    /// 获取或设置Surfboard，用于保存从 VTBF 推断出的 surfboard 语义模型，供渲染、导出和诊断使用。
    /// </summary>
    public required SurfboardModel Surfboard { get; init; }

    /// <summary>
    /// 获取或设置Summary，用于报告数量或统计值，便于调用方校验结构规模和处理结果。
    /// </summary>
    public required ParseSummary Summary { get; init; }
}

/// <summary>
/// 表示 sbscene 解析摘要，记录块、节点、动画、变体提示和警告统计。
/// </summary>
public sealed class ParseSummary
{
    /// <summary>
    /// 获取或设置根块数量，用于报告数量或统计值，便于调用方校验结构规模和处理结果。
    /// </summary>
    public required int RootBlockCount { get; init; }

    /// <summary>
    /// 获取或设置Total块数量，用于报告数量或统计值，便于调用方校验结构规模和处理结果。
    /// </summary>
    public required int TotalBlockCount { get; init; }

    /// <summary>
    /// 获取或设置节点数量，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required int NodeCount { get; init; }

    /// <summary>
    /// 获取或设置动画数量，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
    /// </summary>
    public required int AnimationCount { get; init; }

    /// <summary>
    /// 获取或设置VariantHint数量，用于报告数量或统计值，便于调用方校验结构规模和处理结果。
    /// </summary>
    public required int VariantHintCount { get; init; }

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
/// 表示从 VTBF 推断出的 Surfboard 语义模型，包含节点、资源、动画和相机信息。
/// </summary>
public sealed class SurfboardModel
{
    /// <summary>
    /// 获取或设置Objects，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required IReadOnlyList<SceneObjectInfo> Objects { get; init; }

    /// <summary>
    /// 获取或设置节点集合，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required IReadOnlyList<NodeInfo> Nodes { get; init; }

    /// <summary>
    /// 获取或设置Transform2DRecords，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public required IReadOnlyList<Transform2DInfo> Transform2DRecords { get; init; }

    /// <summary>
    /// 获取或设置节点CategoryRecords，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required IReadOnlyList<int> NodeCategoryRecords { get; init; }

    /// <summary>
    /// 获取或设置节点CategoryDetails，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required IReadOnlyList<NodeCategoryInfo> NodeCategoryDetails { get; init; }

    /// <summary>
    /// 获取或设置节点Groups，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required IReadOnlyList<NodeGroupInfo> NodeGroups { get; init; }

    /// <summary>
    /// 获取或设置资源集合，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public required SbSceneResourceMap Resources { get; init; }

    /// <summary>
    /// 获取或设置Camera，用于保存场景相机信息，供 inspect、导出和诊断输出引用。
    /// </summary>
    public required CameraInfo? Camera { get; init; }

    /// <summary>
    /// 获取或设置动画集合，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
    /// </summary>
    public required IReadOnlyList<AnimationInfo> Animations { get; init; }

    /// <summary>
    /// 获取或设置动画Bindings，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
    /// </summary>
    public required IReadOnlyList<AnimationBindingInfo> AnimationBindings { get; init; }

    /// <summary>
    /// 获取或设置VariantHints，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public required IReadOnlyList<VariantHint> VariantHints { get; init; }

    /// <summary>
    /// 获取或设置Unknown字段明细集合，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public required IReadOnlyList<UnknownFieldInfo> UnknownFields { get; init; }
}

/// <summary>
/// 表示解析出的场景对象块，保留标签、路径、字段和子块关系。
/// </summary>
public sealed class SceneObjectInfo
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
    /// 获取或设置路径，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// 获取或设置名称，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// 获取或设置字符串字段类型代码字段明细集合，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public required IReadOnlyList<FieldValueSummary> StringFields { get; init; }

    /// <summary>
    /// 获取或设置Numeric字段明细集合，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public required IReadOnlyList<FieldValueSummary> NumericFields { get; init; }

    /// <summary>
    /// 获取或设置子级Tags，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public required IReadOnlyList<string> ChildTags { get; init; }
}

/// <summary>
/// 表示场景节点，保存节点索引、层级关系、分组、类别和变换信息。
/// </summary>
public sealed class NodeInfo
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
    /// 获取或设置路径，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// 获取或设置名称，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// 获取或设置Flags，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public int? Flags { get; init; }

    /// <summary>
    /// 获取或设置FlagBits，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public required IReadOnlyList<int> FlagBits { get; init; }

    /// <summary>
    /// 获取或设置子级索引，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public int? ChildIndex { get; init; }

    /// <summary>
    /// 获取或设置Sibling索引，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public int? SiblingIndex { get; init; }

    /// <summary>
    /// 获取或设置Comment，用于保存源字段文本或诊断说明，便于展示、校验和导出报告。
    /// </summary>
    public string? Comment { get; init; }

    /// <summary>
    /// 获取或设置Category标识，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public int? CategoryId { get; init; }

    /// <summary>
    /// 获取或设置Group，用于标识分类、组件、属性或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public required string Group { get; init; }

    /// <summary>
    /// 获取或设置Transform2D，用于描述位置、旋转、缩放或矩阵状态，参与渲染坐标和导出坐标计算。
    /// </summary>
    public Transform2DInfo? Transform2D { get; init; }

    /// <summary>
    /// 获取或设置HasTransform2，用于控制对应功能开关，调用方可据此改变解析、渲染或导出策略。
    /// </summary>
    public required bool HasTransform2 { get; init; }

    /// <summary>
    /// 获取或设置HasTransform3，用于控制对应功能开关，调用方可据此改变解析、渲染或导出策略。
    /// </summary>
    public required bool HasTransform3 { get; init; }

    /// <summary>
    /// 获取或设置Has数据，用于参与颜色、透明度、照明或混合计算。
    /// </summary>
    public required bool HasData { get; init; }

    /// <summary>
    /// 获取或设置HasCategory，用于控制对应功能开关，调用方可据此改变解析、渲染或导出策略。
    /// </summary>
    public required bool HasCategory { get; init; }

    /// <summary>
    /// 获取或设置字符串字段类型代码字段明细集合，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public required IReadOnlyList<FieldValueSummary> StringFields { get; init; }

    /// <summary>
    /// 获取或设置Numeric字段明细集合，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public required IReadOnlyList<FieldValueSummary> NumericFields { get; init; }

    /// <summary>
    /// 获取或设置子级Tags，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public required IReadOnlyList<string> ChildTags { get; init; }
}

/// <summary>
/// 表示节点类别记录，保存类别 ID、参数预览和来源路径。
/// </summary>
public sealed class NodeCategoryInfo
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
    /// 获取或设置类别名称，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public string? KindName { get; init; }

    /// <summary>
    /// 获取或设置类型Byte，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public int? TypeByte { get; init; }

    /// <summary>
    /// 获取或设置Category标识，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public int? CategoryId { get; init; }

    /// <summary>
    /// 获取或设置Parameter诊断预览文本，用于保留源块参数或字段参数，便于 inspect 输出和后续格式推断。
    /// </summary>
    public string? ParameterPreview { get; init; }

    /// <summary>
    /// 获取或设置Parameter字符串字段类型代码，用于参与颜色、透明度、照明或混合计算。
    /// </summary>
    public string? ParameterString { get; init; }

    /// <summary>
    /// 获取或设置字段明细集合，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public required IReadOnlyList<FieldValueSummary> Fields { get; init; }
}

/// <summary>
/// 表示二维变换信息，保存平移、旋转、缩放、显示状态和颜色数据。
/// </summary>
public sealed class Transform2DInfo
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
    /// 获取或设置路径，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// 获取或设置平移，用于描述位置、旋转、缩放或矩阵状态，参与渲染坐标和导出坐标计算。
    /// </summary>
    public Vector2Value? Translation { get; init; }

    /// <summary>
    /// 获取或设置旋转Z，用于描述位置、旋转、缩放或矩阵状态，参与渲染坐标和导出坐标计算。
    /// </summary>
    public float? RotationZ { get; init; }

    /// <summary>
    /// 获取或设置旋转Z原始字节内容，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public int? RotationZRaw { get; init; }

    /// <summary>
    /// 获取或设置旋转ZDegreesCandidate，用于描述位置、旋转、缩放或矩阵状态，参与渲染坐标和导出坐标计算。
    /// </summary>
    public double? RotationZDegreesCandidate { get; init; }

    /// <summary>
    /// 获取或设置输出缩放比例，用于确定渲染区域、裁剪范围、采样质量或输出尺寸。
    /// </summary>
    public Vector2Value? Scale { get; init; }

    /// <summary>
    /// 获取或设置Display，用于描述位置、旋转、缩放或矩阵状态，参与渲染坐标和导出坐标计算。
    /// </summary>
    public bool? Display { get; init; }

    /// <summary>
    /// 获取或设置材质颜色，用于参与颜色、透明度、照明或混合计算。
    /// </summary>
    public ColorArgbValue? MaterialColor { get; init; }

    /// <summary>
    /// 获取或设置照明颜色，用于参与颜色、透明度、照明或混合计算。
    /// </summary>
    public ColorArgbValue? IlluminationColor { get; init; }

    /// <summary>
    /// 获取或设置Vertex颜色集合，用于参与颜色、透明度、照明或混合计算。
    /// </summary>
    public required IReadOnlyList<ColorArgbValue> VertexColors { get; init; }

    /// <summary>
    /// 获取或设置MultiPosFlags，用于保存源格式中的标志位，供解析诊断和语义推断使用。
    /// </summary>
    public int? MultiPosFlags { get; init; }

    /// <summary>
    /// 获取或设置Multi大小Flags，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public int? MultiSizeFlags { get; init; }

    /// <summary>
    /// 获取或设置字段明细集合，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public required IReadOnlyList<FieldValueSummary> Fields { get; init; }
}

/// <summary>
/// 表示二维数值向量，用于坐标、尺寸或缩放分量。
/// </summary>
public sealed class Vector2Value
{
    /// <summary>
    /// 获取或设置X，用于描述位置、旋转、缩放或矩阵状态，参与渲染坐标和导出坐标计算。
    /// </summary>
    public required float X { get; init; }

    /// <summary>
    /// 获取或设置Y，用于表示坐标、尺寸或向量分量，参与变换和导出计算。
    /// </summary>
    public required float Y { get; init; }
}

/// <summary>
/// 表示 ARGB 颜色值，提供通道值和十六进制显示文本。
/// </summary>
public sealed class ColorArgbValue
{
    /// <summary>
    /// 获取或设置Alpha 透明度通道值，用于参与颜色、透明度、照明或混合计算。
    /// </summary>
    public required byte A { get; init; }

    /// <summary>
    /// 获取或设置红色通道值，用于参与颜色、透明度、照明或混合计算。
    /// </summary>
    public required byte R { get; init; }

    /// <summary>
    /// 获取或设置绿色通道值，用于参与颜色、透明度、照明或混合计算。
    /// </summary>
    public required byte G { get; init; }

    /// <summary>
    /// 获取或设置蓝色通道值，用于参与颜色、透明度、照明或混合计算。
    /// </summary>
    public required byte B { get; init; }

    /// <summary>
    /// 表示Hex，用于以十六进制形式展示源格式标识，便于诊断和与原始字节对照。
    /// </summary>
    public string Hex => $"#{A:X2}{R:X2}{G:X2}{B:X2}";
}

/// <summary>
/// 表示三维数值向量，用于相机或三维坐标分量。
/// </summary>
public sealed class Vector3Value
{
    /// <summary>
    /// 获取或设置X，用于描述位置、旋转、缩放或矩阵状态，参与渲染坐标和导出坐标计算。
    /// </summary>
    public required float X { get; init; }

    /// <summary>
    /// 获取或设置Y，用于表示坐标、尺寸或向量分量，参与变换和导出计算。
    /// </summary>
    public required float Y { get; init; }

    /// <summary>
    /// 获取或设置Z，用于表示坐标、尺寸或向量分量，参与变换和导出计算。
    /// </summary>
    public required float Z { get; init; }
}

/// <summary>
/// 表示场景相机信息，保存位置、裁剪面和源字段摘要。
/// </summary>
public sealed class CameraInfo
{
    /// <summary>
    /// 获取或设置文件内偏移，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required long Offset { get; init; }

    /// <summary>
    /// 获取或设置路径，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// 获取或设置名称，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// 获取或设置位置，用于描述位置、旋转、缩放或矩阵状态，参与渲染坐标和导出坐标计算。
    /// </summary>
    public Vector3Value? Position { get; init; }

    /// <summary>
    /// 获取或设置目标，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public Vector3Value? Target { get; init; }

    /// <summary>
    /// 获取或设置Flags，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public int? Flags { get; init; }

    /// <summary>
    /// 获取或设置Near剪辑，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
    /// </summary>
    public float? NearClip { get; init; }

    /// <summary>
    /// 获取或设置Far剪辑，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
    /// </summary>
    public float? FarClip { get; init; }

    /// <summary>
    /// 获取或设置字段明细集合，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public required IReadOnlyList<FieldValueSummary> Fields { get; init; }
}

/// <summary>
/// 表示节点分组统计，用于摘要和 inspect 输出。
/// </summary>
public sealed class NodeGroupInfo
{
    /// <summary>
    /// 获取或设置名称，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 获取或设置数量，用于报告数量或统计值，便于调用方校验结构规模和处理结果。
    /// </summary>
    public required int Count { get; init; }

    /// <summary>
    /// 获取或设置节点名称集合，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public required IReadOnlyList<string> NodeNames { get; init; }
}

/// <summary>
/// 表示动画剪辑信息，保存名称、索引、motion 集合和源字段摘要。
/// </summary>
public sealed class AnimationInfo
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
    /// 获取或设置路径，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// 获取或设置名称，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// 获取或设置字符串字段类型代码字段明细集合，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public required IReadOnlyList<FieldValueSummary> StringFields { get; init; }

    /// <summary>
    /// 获取或设置Numeric字段明细集合，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public required IReadOnlyList<FieldValueSummary> NumericFields { get; init; }

    /// <summary>
    /// 获取或设置Motions，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public required IReadOnlyList<MotionInfo> Motions { get; init; }
}

/// <summary>
/// 表示动画 motion，记录目标节点、轨道集合和声明的轨道数量。
/// </summary>
public sealed class MotionInfo
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
    /// 获取或设置路径，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// 获取或设置名称，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// 获取或设置目标名称，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public string? TargetName { get; init; }

    /// <summary>
    /// 获取或设置目标索引，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public int? TargetIndex { get; init; }

    /// <summary>
    /// 获取或设置Cast索引，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public int? CastIndex { get; init; }

    /// <summary>
    /// 获取或设置Declared轨道数量，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
    /// </summary>
    public int? DeclaredTrackCount { get; init; }

    /// <summary>
    /// 获取或设置字符串字段类型代码字段明细集合，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public required IReadOnlyList<FieldValueSummary> StringFields { get; init; }

    /// <summary>
    /// 获取或设置Numeric字段明细集合，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public required IReadOnlyList<FieldValueSummary> NumericFields { get; init; }

    /// <summary>
    /// 获取或设置轨道集合，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
    /// </summary>
    public required IReadOnlyList<TrackInfo> Tracks { get; init; }
}

/// <summary>
/// 表示动画轨道，保存轨道类型、关键帧集合和源字段摘要。
/// </summary>
public sealed class TrackInfo
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
    /// 获取或设置路径，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// 获取或设置名称，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// 获取或设置轨道标识，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
    /// </summary>
    public int? TrackId { get; init; }

    /// <summary>
    /// 获取或设置轨道类型，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public int? TrackType { get; init; }

    /// <summary>
    /// 获取或设置轨道类型名称，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public string? TrackTypeName { get; init; }

    /// <summary>
    /// 获取或设置值类型，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public int? ValueType { get; init; }

    /// <summary>
    /// 获取或设置值类型名称，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public string? ValueTypeName { get; init; }

    /// <summary>
    /// 获取或设置DeclaredKey数量From轨道，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
    /// </summary>
    public int? DeclaredKeyCountFromTrack { get; init; }

    /// <summary>
    /// 获取或设置DeclaredKey数量FromKey块，用于报告数量或统计值，便于调用方校验结构规模和处理结果。
    /// </summary>
    public int DeclaredKeyCountFromKeyBlock { get; init; }

    /// <summary>
    /// 获取或设置Key数量MatchesDeclaration，用于报告数量或统计值，便于调用方校验结构规模和处理结果。
    /// </summary>
    public bool? KeyCountMatchesDeclaration { get; init; }

    /// <summary>
    /// 获取或设置Flags，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public int? Flags { get; init; }

    /// <summary>
    /// 获取或设置Key值Storage，用于描述动画时间轴、关键帧值或插值方式，影响采样、渲染和导出曲线。
    /// </summary>
    public string? KeyValueStorage { get; init; }

    /// <summary>
    /// 获取或设置目标索引，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public int? TargetIndex { get; init; }

    /// <summary>
    /// 获取或设置First帧，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
    /// </summary>
    public int? FirstFrame { get; init; }

    /// <summary>
    /// 获取或设置上次使用的帧，用于恢复 Viewer 上次使用的导出设置，减少重复输入。
    /// </summary>
    public int? LastFrame { get; init; }

    /// <summary>
    /// 获取或设置DeclaredKey数量，用于报告数量或统计值，便于调用方校验结构规模和处理结果。
    /// </summary>
    public int DeclaredKeyCount { get; init; }

    /// <summary>
    /// 获取或设置IsLikelyState轨道，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
    /// </summary>
    public required bool IsLikelyStateTrack { get; init; }

    /// <summary>
    /// 获取或设置字符串字段类型代码字段明细集合，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public required IReadOnlyList<FieldValueSummary> StringFields { get; init; }

    /// <summary>
    /// 获取或设置Numeric字段明细集合，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public required IReadOnlyList<FieldValueSummary> NumericFields { get; init; }

    /// <summary>
    /// 获取或设置关键帧集合，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
    /// </summary>
    public required IReadOnlyList<KeyframeInfo> Keyframes { get; init; }
}

/// <summary>
/// 表示动画绑定关系，记录 motion、目标节点和轨道数量。
/// </summary>
public sealed class AnimationBindingInfo
{
    /// <summary>
    /// 获取或设置节点索引，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required int NodeIndex { get; init; }

    /// <summary>
    /// 获取或设置节点名称，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public string? NodeName { get; init; }

    /// <summary>
    /// 获取或设置动画名称，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public required string AnimationName { get; init; }

    /// <summary>
    /// 获取或设置动画索引，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
    /// </summary>
    public required int AnimationIndex { get; init; }

    /// <summary>
    /// 获取或设置Motion索引，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required int MotionIndex { get; init; }

    /// <summary>
    /// 获取或设置轨道数量，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
    /// </summary>
    public required int TrackCount { get; init; }

    /// <summary>
    /// 获取或设置Key数量，用于报告数量或统计值，便于调用方校验结构规模和处理结果。
    /// </summary>
    public required int KeyCount { get; init; }

    /// <summary>
    /// 获取或设置轨道类型集合，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public required IReadOnlyList<int> TrackTypes { get; init; }

    /// <summary>
    /// 获取或设置轨道类型名称集合，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public required IReadOnlyList<string> TrackTypeNames { get; init; }
}

/// <summary>
/// 表示动画关键帧，保存帧号、值、插值、切线和诊断预览。
/// </summary>
public sealed class KeyframeInfo
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
    /// 获取或设置路径，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// 获取或设置字段明细集合，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public required IReadOnlyList<FieldValueSummary> Fields { get; init; }

    /// <summary>
    /// 获取或设置Key帧，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
    /// </summary>
    public int? KeyFrame { get; init; }

    /// <summary>
    /// 获取或设置Scalar值，用于描述动画时间轴、关键帧值或插值方式，影响采样、渲染和导出曲线。
    /// </summary>
    public double? ScalarValue { get; init; }

    /// <summary>
    /// 获取或设置Bool值，用于描述动画时间轴、关键帧值或插值方式，影响采样、渲染和导出曲线。
    /// </summary>
    public bool? BoolValue { get; init; }

    /// <summary>
    /// 获取或设置PackedAngle原始字节内容，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public int? PackedAngleRaw { get; init; }

    /// <summary>
    /// 获取或设置PackedAngleDegreesCandidate，用于表达该模型在解析、渲染或导出流程中的具体业务含义。
    /// </summary>
    public double? PackedAngleDegreesCandidate { get; init; }

    /// <summary>
    /// 获取或设置Key值类型Hex，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public string? KeyValueTypeHex { get; init; }

    /// <summary>
    /// 获取或设置Key值类型名称，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public string? KeyValueTypeName { get; init; }

    /// <summary>
    /// 获取或设置Key值类别，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public string? KeyValueKind { get; init; }

    /// <summary>
    /// 获取或设置Interpolation，用于描述动画时间轴、关键帧值或插值方式，影响采样、渲染和导出曲线。
    /// </summary>
    public int? Interpolation { get; init; }

    /// <summary>
    /// 获取或设置Interpolation名称，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public string? InterpolationName { get; init; }

    /// <summary>
    /// 获取或设置TangentIn，用于描述动画时间轴、关键帧值或插值方式，影响采样、渲染和导出曲线。
    /// </summary>
    public double? TangentIn { get; init; }

    /// <summary>
    /// 获取或设置TangentOut，用于描述动画时间轴、关键帧值或插值方式，影响采样、渲染和导出曲线。
    /// </summary>
    public double? TangentOut { get; init; }

    /// <summary>
    /// 获取或设置TimeCandidates，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public required IReadOnlyList<double> TimeCandidates { get; init; }

    /// <summary>
    /// 获取或设置值Candidates，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public required IReadOnlyList<double> ValueCandidates { get; init; }

    /// <summary>
    /// 获取或设置诊断预览文本，用于保存源字段文本或诊断说明，便于展示、校验和导出报告。
    /// </summary>
    public string? Preview { get; init; }
}

/// <summary>
/// 表示可导出变体提示，记录推断类别、来源、名称和置信度。
/// </summary>
public sealed class VariantHint
{
    /// <summary>
    /// 获取或设置Category，用于描述位置、旋转、缩放或矩阵状态，参与渲染坐标和导出坐标计算。
    /// </summary>
    public required string Category { get; init; }

    /// <summary>
    /// 获取或设置来源信息类别，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public required string SourceKind { get; init; }

    /// <summary>
    /// 获取或设置名称，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 获取或设置Confidence，用于表达该模型在解析、渲染或导出流程中的具体业务含义。
    /// </summary>
    public required double Confidence { get; init; }

    /// <summary>
    /// 获取或设置Reason，用于保存源字段文本或诊断说明，便于展示、校验和导出报告。
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>
    /// 获取或设置动画名称，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public string? AnimationName { get; init; }

    /// <summary>
    /// 获取或设置节点Group，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public string? NodeGroup { get; init; }

    /// <summary>
    /// 获取或设置轨道路径，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public string? TrackPath { get; init; }
}

/// <summary>
/// 表示未知字段摘要，保留所有者、字段 ID、类型和预览内容。
/// </summary>
public sealed class UnknownFieldInfo
{
    /// <summary>
    /// 获取或设置OwnerTag，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public required string OwnerTag { get; init; }

    /// <summary>
    /// 获取或设置Owner路径，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public required string OwnerPath { get; init; }

    /// <summary>
    /// 获取或设置文件内偏移，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required long Offset { get; init; }

    /// <summary>
    /// 获取或设置字段 ID 的十六进制文本，用于以十六进制形式展示源格式标识，便于诊断和与原始字节对照。
    /// </summary>
    public required string IdHex { get; init; }

    /// <summary>
    /// 获取或设置字段类型代码的十六进制文本，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public required string TypeHex { get; init; }

    /// <summary>
    /// 获取或设置数量，用于报告数量或统计值，便于调用方校验结构规模和处理结果。
    /// </summary>
    public required int Count { get; init; }

    /// <summary>
    /// 获取或设置Stride，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required int Stride { get; init; }

    /// <summary>
    /// 获取或设置诊断预览文本，用于保存源字段文本或诊断说明，便于展示、校验和导出报告。
    /// </summary>
    public string? Preview { get; init; }
}

/// <summary>
/// 表示字段值摘要，保存字段 ID、类型、解码值和预览文本。
/// </summary>
public sealed class FieldValueSummary
{
    /// <summary>
    /// 获取或设置字段 ID 的十六进制文本，用于以十六进制形式展示源格式标识，便于诊断和与原始字节对照。
    /// </summary>
    public required string IdHex { get; init; }

    /// <summary>
    /// 获取或设置字段类型代码的十六进制文本，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public required string TypeHex { get; init; }

    /// <summary>
    /// 获取或设置字段类型名称，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public required string TypeName { get; init; }

    /// <summary>
    /// 获取或设置诊断预览文本，用于保存源字段文本或诊断说明，便于展示、校验和导出报告。
    /// </summary>
    public string? Preview { get; init; }

    /// <summary>
    /// 获取或设置字段解码后的整数数值集合，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public long[]? Int64Values { get; init; }

    /// <summary>
    /// 获取或设置字段解码后的 Float64 数值集合，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public double[]? Float64Values { get; init; }

    /// <summary>
    /// 获取或设置字段解码后的字符串值，用于保存源字段文本或诊断说明，便于展示、校验和导出报告。
    /// </summary>
    public string? StringValue { get; init; }
}
