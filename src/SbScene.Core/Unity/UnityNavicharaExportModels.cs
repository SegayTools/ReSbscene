using System.Text.Json;

namespace SbScene.Core.Unity;

/// <summary>
/// 提供Unity NaviCharaConstants，集中保存 schema、默认值和固定名称。
/// </summary>
public static class UnityNavicharaConstants
{
    /// <summary>
    /// 表示导出 schema 标识，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public const string Schema = "sbscene.unityNavicharaExport.v1";
    /// <summary>
    /// 表示动画采样率，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
    /// </summary>
    public const int SampleRate = 60;

    /// <summary>
    /// 表示NaviChara 核心剪辑名称集合，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public static readonly IReadOnlyList<string> CoreClipNames =
    [
        "Navi_Default",
        "Navi_Welcom",
        "Navi_Fun_Start",
        "Navi_Fun_Loop_01",
        "Navi_Fun_End",
        "Navi_Sad_01",
    ];

    /// <summary>
    /// 判断名称是否属于 NaviChara 导出清单中的核心动画剪辑。
    /// </summary>
    /// <param name="name">要查找、匹配或写入输出的名称。</param>
    /// <returns>如果名称是核心 NaviChara 剪辑则为 true；否则为 false。</returns>
    public static bool IsCoreClip(string name)
    {
        return CoreClipNames.Contains(name, StringComparer.Ordinal);
    }

    /// <summary>
    /// 判断 NaviChara 剪辑是否应按默认规则循环播放。
    /// </summary>
    /// <param name="name">要查找、匹配或写入输出的名称。</param>
    /// <returns>如果剪辑名称按默认导出规则需要循环播放则为 true；否则为 false。</returns>
    public static bool DefaultLoop(string name)
    {
        return name is "Navi_Default" or "Navi_Fun_Loop_01" or "Navi_Sad_01";
    }
}

/// <summary>
/// 表示Unity NaviChara 导出清单选项，集中描述调用方可配置的输入、开关和默认策略。
/// </summary>
public sealed class UnityNavicharaExportOptions
{
    /// <summary>
    /// 获取或设置NaviChara 角色标识，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public int CharacterId { get; init; }

    /// <summary>
    /// 获取或设置配置，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public UnityNavicharaExportProfile? Profile { get; init; }

    /// <summary>
    /// 表示源动画到目标剪辑的映射集合，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public IReadOnlyList<UnityNavicharaAnimationMap> Maps { get; init; } = Array.Empty<UnityNavicharaAnimationMap>();

    /// <summary>
    /// 获取或设置服装状态采样帧，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
    /// </summary>
    public int? FashionFrame { get; init; }

    /// <summary>
    /// 获取或设置配件状态采样帧，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
    /// </summary>
    public int? AccessoryFrame { get; init; }

    /// <summary>
    /// 获取或设置位置状态采样帧，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
    /// </summary>
    public int? PositionFrame { get; init; }

    /// <summary>
    /// 获取或设置占位剪辑开关，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
    /// </summary>
    public bool AllowPlaceholderClips { get; init; }

    /// <summary>
    /// 获取或设置采样曲线烘焙开关，用于控制对应功能开关，调用方可据此改变解析、渲染或导出策略。
    /// </summary>
    public bool BakeSampledCurves { get; init; }

    /// <summary>
    /// 获取或设置精灵资源导出开关，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public bool ExtractSprites { get; init; }

    /// <summary>
    /// 获取或设置校验帧写出开关，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
    /// </summary>
    public bool WriteValidationFrames { get; init; }

    /// <summary>
    /// 获取或设置严格校验开关，用于控制对应功能开关，调用方可据此改变解析、渲染或导出策略。
    /// </summary>
    public bool Strict { get; init; }

    /// <summary>
    /// 表示自动居中开关，用于控制导出时是否把角色可见内容平移到原点附近。
    /// </summary>
    public bool AutoCenter { get; init; } = true;
}

/// <summary>
/// 表示 Unity NaviChara 动画映射，记录源动画到目标剪辑的对应关系。
/// </summary>
/// <param name="SourceAnimation">参与本次处理的来源信息动画。</param>
/// <param name="TargetClip">参与本次处理的目标剪辑。</param>
public sealed record UnityNavicharaAnimationMap(string SourceAnimation, string TargetClip);

/// <summary>
/// 表示Unity NaviChara 导出清单结果，封装处理产物、统计信息和诊断状态。
/// </summary>
public sealed class UnityNavicharaExportResult
{
    /// <summary>
    /// 获取或设置导出清单，用于返回导出或处理产物及其统计、校验和诊断信息。
    /// </summary>
    public required UnityNavicharaExport Export { get; init; }

    /// <summary>
    /// 获取或设置诊断信息列表，用于把非致命问题返回给调用方，便于诊断解析、渲染或导出过程。
    /// </summary>
    public required IReadOnlyList<UnityNavicharaDiagnostic> Diagnostics { get; init; }

    /// <summary>
    /// 获取或设置失败状态，用于控制对应功能开关，调用方可据此改变解析、渲染或导出策略。
    /// </summary>
    public required bool Failed { get; init; }
}

/// <summary>
/// 表示 Unity NaviChara 导出清单，描述角色、节点、精灵、剪辑和诊断输出。
/// </summary>
public sealed class UnityNavicharaExport
{
    /// <summary>
    /// 表示导出 schema 标识，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public string Schema { get; init; } = UnityNavicharaConstants.Schema;

    /// <summary>
    /// 获取或设置来源信息，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public required UnityNavicharaSource Source { get; init; }

    /// <summary>
    /// 获取或设置导出设置，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public required UnityNavicharaSettings Settings { get; init; }

    /// <summary>
    /// 获取或设置角色信息，用于描述导出角色的标识、名称和默认状态。
    /// </summary>
    public required UnityNavicharaCharacter Character { get; init; }

    /// <summary>
    /// 获取或设置节点集合，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required IReadOnlyList<UnityNavicharaNode> Nodes { get; init; }

    /// <summary>
    /// 获取或设置精灵集合，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required IReadOnlyList<UnityNavicharaSprite> Sprites { get; init; }

    /// <summary>
    /// 获取或设置动画剪辑集合，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
    /// </summary>
    public required IReadOnlyList<UnityNavicharaClip> Clips { get; init; }

    /// <summary>
    /// 获取或设置校验信息，用于返回导出或处理产物及其统计、校验和诊断信息。
    /// </summary>
    public required UnityNavicharaValidation Validation { get; init; }

    /// <summary>
    /// 获取或设置 Animator 描述信息，用于生成 Unity 状态机和参数配置。
    /// </summary>
    public required UnityNavicharaAnimator Animator { get; init; }

    /// <summary>
    /// 获取或设置诊断信息列表，用于把非致命问题返回给调用方，便于诊断解析、渲染或导出过程。
    /// </summary>
    public required IReadOnlyList<UnityNavicharaDiagnostic> Diagnostics { get; init; }
}

/// <summary>
/// 表示 Unity NaviChara 导出的来源文件信息。
/// </summary>
public sealed class UnityNavicharaSource
{
    /// <summary>
    /// 获取或设置sbscene 场景，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public required string Sbscene { get; init; }

    /// <summary>
    /// 获取或设置SVO，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public required string Svo { get; init; }

    /// <summary>
    /// 获取或设置场景哈希，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public required string SceneHash { get; init; }

    /// <summary>
    /// 获取或设置导出器版本，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public required string ExporterVersion { get; init; }
}

/// <summary>
/// 表示 Unity NaviChara 导出设置，保存坐标、采样、严格校验和资源写出策略。
/// </summary>
public sealed class UnityNavicharaSettings
{
    /// <summary>
    /// 表示动画采样率，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
    /// </summary>
    public int SampleRate { get; init; } = UnityNavicharaConstants.SampleRate;

    /// <summary>
    /// 表示坐标系说明，用于描述导出坐标系和根节点变换策略，保证 Unity 侧还原位置与方向。
    /// </summary>
    public string CoordinateSystem { get; init; } = "sbscene-y-down-to-unity-y-up";

    /// <summary>
    /// 表示 Z 轴旋转转换倍率，用于在 sbscene 与 Unity 坐标系之间转换角度方向。
    /// </summary>
    public double RotationZMultiplier { get; init; } = 1.0;

    /// <summary>
    /// 表示Unity 单位像素比例，用于在像素坐标和 Unity 单位之间换算导出尺寸。
    /// </summary>
    public double PixelsPerUnit { get; init; } = 1.0;

    /// <summary>
    /// 表示曲线烘焙模式，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public string CurveBakeMode { get; init; } = "keyed";

    /// <summary>
    /// 获取或设置Preserve来源信息Coordinates，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public bool PreserveSourceCoordinates { get; init; }

    /// <summary>
    /// 表示根节点变换设置，用于描述导出坐标系和根节点变换策略，保证 Unity 侧还原位置与方向。
    /// </summary>
    public UnityNavicharaRootTransform RootTransform { get; init; } = new();
}

/// <summary>
/// 表示 Unity NaviChara 根节点变换配置。
/// </summary>
public sealed class UnityNavicharaRootTransform
{
    /// <summary>
    /// 表示输出缩放比例，用于确定渲染区域、裁剪范围、采样质量或输出尺寸。
    /// </summary>
    public double Scale { get; init; } = 1.0;

    /// <summary>
    /// 表示文件内偏移，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public UnityNavicharaVector2 Offset { get; init; } = new();
}

/// <summary>
/// 表示 Unity NaviChara 角色信息，保存 prefab、根节点和默认动画。
/// </summary>
public sealed class UnityNavicharaCharacter
{
    /// <summary>
    /// 获取或设置标识，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required int Id { get; init; }

    /// <summary>
    /// 获取或设置Prefab名称，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public required string PrefabName { get; init; }

    /// <summary>
    /// 获取或设置Controller名称，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public required string ControllerName { get; init; }
}

/// <summary>
/// 表示 Unity NaviChara 节点，保存层级、静态变换和图像绑定。
/// </summary>
public sealed class UnityNavicharaNode
{
    /// <summary>
    /// 获取或设置标识，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required int Id { get; init; }

    /// <summary>
    /// 获取或设置sbscene 场景名称，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public string? SbsceneName { get; init; }

    /// <summary>
    /// 获取或设置Unity名称，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public required string UnityName { get; init; }

    /// <summary>
    /// 获取或设置Unity路径，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public required string UnityPath { get; init; }

    /// <summary>
    /// 获取或设置父级标识，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required int ParentId { get; init; }

    /// <summary>
    /// 获取或设置Is图像Cast，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required bool IsImageCast { get; init; }

    /// <summary>
    /// 获取或设置Unity NaviChara节点Static，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required UnityNavicharaNodeStatic Static { get; init; }

    /// <summary>
    /// 获取或设置图像，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public UnityNavicharaNodeImage? Image { get; init; }
}

/// <summary>
/// 表示 Unity NaviChara 节点的静态变换和显示状态。
/// </summary>
public sealed class UnityNavicharaNodeStatic
{
    /// <summary>
    /// 获取或设置Anchored位置，用于描述位置、旋转、缩放或矩阵状态，参与渲染坐标和导出坐标计算。
    /// </summary>
    public required UnityNavicharaVector2 AnchoredPosition { get; init; }

    /// <summary>
    /// 获取或设置旋转Z，用于描述位置、旋转、缩放或矩阵状态，参与渲染坐标和导出坐标计算。
    /// </summary>
    public required double RotationZ { get; init; }

    /// <summary>
    /// 获取或设置输出缩放比例，用于确定渲染区域、裁剪范围、采样质量或输出尺寸。
    /// </summary>
    public required UnityNavicharaVector2 Scale { get; init; }

    /// <summary>
    /// 获取或设置Display，用于描述位置、旋转、缩放或矩阵状态，参与渲染坐标和导出坐标计算。
    /// </summary>
    public required bool Display { get; init; }

    /// <summary>
    /// 获取或设置大小，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required UnityNavicharaVector2 Size { get; init; }

    /// <summary>
    /// 获取或设置轴心RGBA 像素缓冲区，用于确定渲染区域、裁剪范围、采样质量或输出尺寸。
    /// </summary>
    public required UnityNavicharaVector2 PivotPixels { get; init; }

    /// <summary>
    /// 获取或设置轴心Normalized，用于确定渲染区域、裁剪范围、采样质量或输出尺寸。
    /// </summary>
    public required UnityNavicharaVector2 PivotNormalized { get; init; }

    /// <summary>
    /// 获取或设置材质颜色，用于参与颜色、透明度、照明或混合计算。
    /// </summary>
    public required string MaterialColor { get; init; }
}

/// <summary>
/// 表示 Unity NaviChara 节点关联的 sprite 图像信息。
/// </summary>
public sealed class UnityNavicharaNodeImage
{
    /// <summary>
    /// 获取或设置Component，用于标识分类、组件、属性或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public required string Component { get; init; }

    /// <summary>
    /// 获取或设置Draw模式，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required int DrawMode { get; init; }

    /// <summary>
    /// 获取或设置AdditiveBlend，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required bool AdditiveBlend { get; init; }

    /// <summary>
    /// 获取或设置水平UV翻转开关，用于在 Unity 端还原 CIMG 纹理坐标语义。
    /// </summary>
    public required bool FlipX { get; init; }

    /// <summary>
    /// 获取或设置垂直UV翻转开关，用于在 Unity 端还原 CIMG 纹理坐标语义。
    /// </summary>
    public required bool FlipY { get; init; }

    /// <summary>
    /// 获取或设置UV排列模式，用于在 Unity 端还原 CIMG 纹理坐标语义。
    /// </summary>
    public required int UvMode { get; init; }

    /// <summary>
    /// 获取或设置Primary精灵集合，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required IReadOnlyList<string> PrimarySprites { get; init; }

    /// <summary>
    /// 获取或设置Secondary精灵集合，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required IReadOnlyList<string> SecondarySprites { get; init; }

    /// <summary>
    /// 获取或设置默认Primary索引，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required int DefaultPrimaryIndex { get; init; }

    /// <summary>
    /// 获取或设置默认Secondary索引，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required int DefaultSecondaryIndex { get; init; }
}

/// <summary>
/// 表示 Unity NaviChara sprite 资源，保存 atlas、rect、pivot 和输出路径。
/// </summary>
public sealed class UnityNavicharaSprite
{
    /// <summary>
    /// 获取或设置标识，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 获取或设置名称，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 获取或设置文件，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public required string File { get; init; }

    /// <summary>
    /// 获取或设置来源信息纹理，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public string? SourceTexture { get; init; }

    /// <summary>
    /// 获取或设置Crop索引，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required int CropIndex { get; init; }

    /// <summary>
    /// 获取或设置节点标识，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required int NodeId { get; init; }

    /// <summary>
    /// 获取或设置Slot，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required string Slot { get; init; }

    /// <summary>
    /// 获取或设置Slot索引，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required int SlotIndex { get; init; }

    /// <summary>
    /// 获取或设置矩形，用于确定渲染区域、裁剪范围、采样质量或输出尺寸。
    /// </summary>
    public required UnityNavicharaRect Rect { get; init; }

    /// <summary>
    /// 获取或设置轴心RGBA 像素缓冲区，用于确定渲染区域、裁剪范围、采样质量或输出尺寸。
    /// </summary>
    public required UnityNavicharaVector2 PivotPixels { get; init; }

    /// <summary>
    /// 获取或设置轴心Normalized，用于确定渲染区域、裁剪范围、采样质量或输出尺寸。
    /// </summary>
    public required UnityNavicharaVector2 PivotNormalized { get; init; }
}

/// <summary>
/// 表示 Unity NaviChara 动画剪辑，保存采样率、循环策略、曲线和 unsupported 轨道。
/// </summary>
public sealed class UnityNavicharaClip
{
    /// <summary>
    /// 获取或设置名称，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 获取或设置来源信息Slots，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public required IReadOnlyList<UnityNavicharaSourceSlot> SourceSlots { get; init; }

    /// <summary>
    /// 表示动画采样率，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
    /// </summary>
    public int SampleRate { get; init; } = UnityNavicharaConstants.SampleRate;

    /// <summary>
    /// 获取或设置Duration输出帧序列，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
    /// </summary>
    public required int DurationFrames { get; init; }

    /// <summary>
    /// 获取或设置循环，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
    /// </summary>
    public required bool Loop { get; init; }

    /// <summary>
    /// 获取或设置校验信息输出帧序列，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
    /// </summary>
    public required IReadOnlyList<int> ValidationFrames { get; init; }

    /// <summary>
    /// 获取或设置曲线集合，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public required IReadOnlyList<UnityNavicharaCurve> Curves { get; init; }

    /// <summary>
    /// 获取或设置Unsupported轨道集合，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
    /// </summary>
    public required IReadOnlyList<UnityNavicharaUnsupportedTrack> UnsupportedTracks { get; init; }

    /// <summary>
    /// 获取或设置Placeholder，用于标记占位剪辑或占位资源，供导出校验和补全逻辑判断。
    /// </summary>
    public bool Placeholder { get; init; }
}

/// <summary>
/// 表示 Unity NaviChara 导出的来源文件信息。
/// </summary>
public sealed class UnityNavicharaSourceSlot
{
    /// <summary>
    /// 获取或设置动画，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
    /// </summary>
    public required string Animation { get; init; }

    /// <summary>
    /// 获取或设置帧，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
    /// </summary>
    public required object Frame { get; init; }

    /// <summary>
    /// 获取或设置Repeat，用于描述动画时间轴、关键帧值或插值方式，影响采样、渲染和导出曲线。
    /// </summary>
    public bool? Repeat { get; init; }
}

/// <summary>
/// 表示 Unity 动画曲线，保存绑定信息、关键帧和来源轨道。
/// </summary>
public sealed class UnityNavicharaCurve
{
    /// <summary>
    /// 获取或设置节点标识，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required int NodeId { get; init; }

    /// <summary>
    /// 获取或设置路径，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// 获取或设置sbscene 场景轨道类型，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public required int SbsceneTrackType { get; init; }

    /// <summary>
    /// 获取或设置Unity，用于描述位置、旋转、缩放或矩阵状态，参与渲染坐标和导出坐标计算。
    /// </summary>
    public required UnityNavicharaCurveBinding Unity { get; init; }

    /// <summary>
    /// 获取或设置Keys，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public required IReadOnlyList<UnityNavicharaCurveKey> Keys { get; init; }
}

/// <summary>
/// 表示 Unity 曲线绑定，记录目标组件和属性路径。
/// </summary>
public sealed class UnityNavicharaCurveBinding
{
    /// <summary>
    /// 获取或设置Component，用于标识分类、组件、属性或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public required string Component { get; init; }

    /// <summary>
    /// 获取或设置Property，用于描述位置、旋转、缩放或矩阵状态，参与渲染坐标和导出坐标计算。
    /// </summary>
    public required string Property { get; init; }

    /// <summary>
    /// 获取或设置曲线类别，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public required string CurveKind { get; init; }
}

/// <summary>
/// 表示 Unity 动画曲线关键帧，保存帧、时间、值、插值和切线。
/// </summary>
public sealed class UnityNavicharaCurveKey
{
    /// <summary>
    /// 获取或设置帧，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
    /// </summary>
    public required int Frame { get; init; }

    /// <summary>
    /// 获取或设置Time，用于描述动画时间轴、关键帧值或插值方式，影响采样、渲染和导出曲线。
    /// </summary>
    public required double Time { get; init; }

    /// <summary>
    /// 获取或设置值，用于描述动画时间轴、关键帧值或插值方式，影响采样、渲染和导出曲线。
    /// </summary>
    public required double Value { get; init; }

    /// <summary>
    /// 获取或设置Interp，用于描述动画时间轴、关键帧值或插值方式，影响采样、渲染和导出曲线。
    /// </summary>
    public required string Interp { get; init; }

    /// <summary>
    /// 获取或设置HasInTangent，用于控制对应功能开关，调用方可据此改变解析、渲染或导出策略。
    /// </summary>
    public required bool HasInTangent { get; init; }

    /// <summary>
    /// 获取或设置HasOutTangent，用于控制对应功能开关，调用方可据此改变解析、渲染或导出策略。
    /// </summary>
    public required bool HasOutTangent { get; init; }

    /// <summary>
    /// 获取或设置InTangent，用于描述动画时间轴、关键帧值或插值方式，影响采样、渲染和导出曲线。
    /// </summary>
    public double? InTangent { get; init; }

    /// <summary>
    /// 获取或设置OutTangent，用于描述动画时间轴、关键帧值或插值方式，影响采样、渲染和导出曲线。
    /// </summary>
    public double? OutTangent { get; init; }
}

/// <summary>
/// 表示暂不支持导出的轨道信息和原因。
/// </summary>
public sealed class UnityNavicharaUnsupportedTrack
{
    /// <summary>
    /// 获取或设置来源信息动画，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public required string SourceAnimation { get; init; }

    /// <summary>
    /// 获取或设置节点标识，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required int NodeId { get; init; }

    /// <summary>
    /// 获取或设置节点名称，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public string? NodeName { get; init; }

    /// <summary>
    /// 获取或设置轨道类型，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public required int TrackType { get; init; }

    /// <summary>
    /// 获取或设置Reason，用于保存源字段文本或诊断说明，便于展示、校验和导出报告。
    /// </summary>
    public required string Reason { get; init; }
}

/// <summary>
/// 表示 Unity NaviChara 导出诊断项，保存严重级别、代码和提示。
/// </summary>
public sealed class UnityNavicharaDiagnostic
{
    /// <summary>
    /// 获取或设置Severity，用于描述位置、旋转、缩放或矩阵状态，参与渲染坐标和导出坐标计算。
    /// </summary>
    public required string Severity { get; init; }

    /// <summary>
    /// 获取或设置代码，用于标识分类、组件、属性或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// 获取或设置目标剪辑，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public string? TargetClip { get; init; }

    /// <summary>
    /// 获取或设置来源信息动画，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public string? SourceAnimation { get; init; }

    /// <summary>
    /// 获取或设置节点标识，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public int? NodeId { get; init; }

    /// <summary>
    /// 获取或设置节点名称，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public string? NodeName { get; init; }

    /// <summary>
    /// 获取或设置轨道类型，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public int? TrackType { get; init; }

    /// <summary>
    /// 获取或设置Message，用于保存源字段文本或诊断说明，便于展示、校验和导出报告。
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// 获取或设置Suggestion，用于保存源字段文本或诊断说明，便于展示、校验和导出报告。
    /// </summary>
    public string? Suggestion { get; init; }
}

/// <summary>
/// 表示 Unity NaviChara 导出校验摘要。
/// </summary>
public sealed class UnityNavicharaValidation
{
    /// <summary>
    /// 表示帧Strategy，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
    /// </summary>
    public string FrameStrategy { get; init; } = "autoQuarters";

    /// <summary>
    /// 获取或设置引用图像目录，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public string? ReferenceImageDirectory { get; init; }
}

/// <summary>
/// 表示 Animator 描述信息，保存状态集合和默认状态。
/// </summary>
public sealed class UnityNavicharaAnimator
{
    /// <summary>
    /// 获取或设置Parameters，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public required IReadOnlyList<string> Parameters { get; init; }

    /// <summary>
    /// 获取或设置States，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public required IReadOnlyList<UnityNavicharaAnimatorState> States { get; init; }
}

/// <summary>
/// 表示 Animator 状态描述，记录状态名称、motion 和默认状态标记。
/// </summary>
public sealed class UnityNavicharaAnimatorState
{
    /// <summary>
    /// 获取或设置名称，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 获取或设置Motion，用于描述动画时间轴、关键帧值或插值方式，影响采样、渲染和导出曲线。
    /// </summary>
    public required string Motion { get; init; }

    /// <summary>
    /// 获取或设置循环，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
    /// </summary>
    public required bool Loop { get; init; }
}

/// <summary>
/// 表示 Unity NaviChara 二维向量。
/// </summary>
public sealed class UnityNavicharaVector2
{
    /// <summary>
    /// 获取或设置X，用于描述位置、旋转、缩放或矩阵状态，参与渲染坐标和导出坐标计算。
    /// </summary>
    public double X { get; init; }

    /// <summary>
    /// 获取或设置Y，用于表示坐标、尺寸或向量分量，参与变换和导出计算。
    /// </summary>
    public double Y { get; init; }
}

/// <summary>
/// 表示 Unity NaviChara 矩形区域。
/// </summary>
public sealed class UnityNavicharaRect
{
    /// <summary>
    /// 获取或设置X，用于描述位置、旋转、缩放或矩阵状态，参与渲染坐标和导出坐标计算。
    /// </summary>
    public required int X { get; init; }

    /// <summary>
    /// 获取或设置Y，用于表示坐标、尺寸或向量分量，参与变换和导出计算。
    /// </summary>
    public required int Y { get; init; }

    /// <summary>
    /// 获取或设置W，用于表示坐标、尺寸或向量分量，参与变换和导出计算。
    /// </summary>
    public required int W { get; init; }

    /// <summary>
    /// 获取或设置H，用于表示坐标、尺寸或向量分量，参与变换和导出计算。
    /// </summary>
    public required int H { get; init; }
}

/// <summary>
/// 表示 Unity NaviChara 配置文件，保存导出设置和剪辑映射。
/// </summary>
public sealed class UnityNavicharaExportProfile
{
    /// <summary>
    /// 表示导出设置，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public UnityNavicharaProfileSettings Settings { get; init; } = new();

    /// <summary>
    /// 表示CommonBase来源信息Slots，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public List<UnityNavicharaSourceSlot> CommonBaseSourceSlots { get; init; } = [];

    /// <summary>
    /// 表示动画剪辑集合，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
    /// </summary>
    public Dictionary<string, UnityNavicharaProfileClip> Clips { get; init; } = new(StringComparer.Ordinal);
}

/// <summary>
/// 表示 Unity NaviChara 配置中的导出设置覆盖项。
/// </summary>
public sealed class UnityNavicharaProfileSettings
{
    /// <summary>
    /// 获取或设置Unity 单位像素比例，用于在像素坐标和 Unity 单位之间换算导出尺寸。
    /// </summary>
    public double? PixelsPerUnit { get; init; }

    /// <summary>
    /// 获取或设置曲线烘焙模式，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public string? CurveBakeMode { get; init; }

    /// <summary>
    /// 获取或设置 Z 轴旋转转换倍率，用于在 sbscene 与 Unity 坐标系之间转换角度方向。
    /// </summary>
    public double? RotationZMultiplier { get; init; }

    /// <summary>
    /// 获取或设置根节点变换设置，用于描述导出坐标系和根节点变换策略，保证 Unity 侧还原位置与方向。
    /// </summary>
    public UnityNavicharaRootTransform? RootTransform { get; init; }
}

/// <summary>
/// 表示 Unity NaviChara 配置中的单个剪辑映射。
/// </summary>
public sealed class UnityNavicharaProfileClip
{
    /// <summary>
    /// 获取或设置循环，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
    /// </summary>
    public bool? Loop { get; init; }

    /// <summary>
    /// 获取或设置Duration输出帧序列，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
    /// </summary>
    public object? DurationFrames { get; init; }

    /// <summary>
    /// 获取或设置校验信息输出帧序列，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
    /// </summary>
    public IReadOnlyList<int>? ValidationFrames { get; init; }

    /// <summary>
    /// 表示来源信息Slots，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public List<UnityNavicharaSourceSlot> SourceSlots { get; init; } = [];
}

/// <summary>
/// 表示 Unity NaviChara 配置模板，供用户生成或补全配置文件。
/// </summary>
public sealed class UnityNavicharaProfileTemplate
{
    /// <summary>
    /// 获取或设置导出设置，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
    /// </summary>
    public required UnityNavicharaProfileSettings Settings { get; init; }

    /// <summary>
    /// 获取或设置CommonBase来源信息Slots，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public required IReadOnlyList<UnityNavicharaSourceSlot> CommonBaseSourceSlots { get; init; }

    /// <summary>
    /// 获取或设置动画集合，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
    /// </summary>
    public required IReadOnlyList<UnityNavicharaProfileTemplateAnimation> Animations { get; init; }

    /// <summary>
    /// 获取或设置动画剪辑集合，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
    /// </summary>
    public required Dictionary<string, UnityNavicharaProfileClip> Clips { get; init; }
}

/// <summary>
/// 表示配置模板中的候选动画条目。
/// </summary>
public sealed class UnityNavicharaProfileTemplateAnimation
{
    /// <summary>
    /// 获取或设置名称，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 获取或设置索引，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required int Index { get; init; }

    /// <summary>
    /// 获取或设置结束帧，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
    /// </summary>
    public required int EndFrame { get; init; }

    /// <summary>
    /// 获取或设置默认Repeat，用于描述动画时间轴、关键帧值或插值方式，影响采样、渲染和导出曲线。
    /// </summary>
    public required bool DefaultRepeat { get; init; }

    /// <summary>
    /// 获取或设置轨道集合，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
    /// </summary>
    public required IReadOnlyList<UnityNavicharaProfileTemplateTrack> Tracks { get; init; }

    /// <summary>
    /// 获取或设置Candidate目标剪辑，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public string? CandidateTargetClip { get; init; }
}

/// <summary>
/// 表示配置模板中的候选轨道条目。
/// </summary>
public sealed class UnityNavicharaProfileTemplateTrack
{
    /// <summary>
    /// 获取或设置节点标识，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
    /// </summary>
    public required int NodeId { get; init; }

    /// <summary>
    /// 获取或设置节点名称，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public string? NodeName { get; init; }

    /// <summary>
    /// 获取或设置轨道类型，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public required int TrackType { get; init; }

    /// <summary>
    /// 获取或设置轨道类型名称，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public string? TrackTypeName { get; init; }

    /// <summary>
    /// 获取或设置First帧，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
    /// </summary>
    public int? FirstFrame { get; init; }

    /// <summary>
    /// 获取或设置上次使用的帧，用于恢复 Viewer 上次使用的导出设置，减少重复输入。
    /// </summary>
    public int? LastFrame { get; init; }

    /// <summary>
    /// 获取或设置Key数量，用于报告数量或统计值，便于调用方校验结构规模和处理结果。
    /// </summary>
    public required int KeyCount { get; init; }
}

/// <summary>
/// 提供 Unity NaviChara 配置加载器，负责读取和解析配置 JSON。
/// </summary>
public static class UnityNavicharaProfileLoader
{
    /// <summary>
    /// 加载持久化设置或资源；读取失败时由调用方使用默认状态。
    /// </summary>
    /// <param name="path">要读取、写入或记录的文件或目录路径。</param>
    /// <returns>加载后的设置或资源对象。</returns>
    public static UnityNavicharaExportProfile Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var profile = new UnityNavicharaExportProfile
        {
            Settings = ReadSettings(root.TryGetProperty("settings", out var settings) ? settings : default),
            CommonBaseSourceSlots = ReadSourceSlotArray(root, "commonBaseSourceSlots"),
            Clips = new Dictionary<string, UnityNavicharaProfileClip>(StringComparer.Ordinal),
        };

        if (!root.TryGetProperty("clips", out var clips) || clips.ValueKind != JsonValueKind.Object)
        {
            return profile;
        }

        foreach (var clipProperty in clips.EnumerateObject())
        {
            if (clipProperty.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var clipElement = clipProperty.Value;
            var clip = new UnityNavicharaProfileClip
            {
                Loop = ReadOptionalBool(clipElement, "loop"),
                DurationFrames = ReadDurationFrames(clipElement),
                ValidationFrames = ReadIntArray(clipElement, "validationFrames"),
                SourceSlots = ReadSourceSlots(clipElement),
            };
            profile.Clips[clipProperty.Name] = clip;
        }

        return profile;
    }

    private static UnityNavicharaProfileSettings ReadSettings(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return new UnityNavicharaProfileSettings();
        }

        return new UnityNavicharaProfileSettings
        {
            PixelsPerUnit = ReadOptionalDouble(element, "pixelsPerUnit"),
            CurveBakeMode = ReadOptionalString(element, "curveBakeMode"),
            RotationZMultiplier = ReadOptionalDouble(element, "rotationZMultiplier"),
            RootTransform = ReadRootTransform(element),
        };
    }

    private static UnityNavicharaRootTransform? ReadRootTransform(JsonElement element)
    {
        if (!element.TryGetProperty("rootTransform", out var rootTransform) || rootTransform.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var offset = new UnityNavicharaVector2();
        if (rootTransform.TryGetProperty("offset", out var offsetElement) && offsetElement.ValueKind == JsonValueKind.Object)
        {
            offset = new UnityNavicharaVector2
            {
                X = ReadOptionalDouble(offsetElement, "x") ?? 0,
                Y = ReadOptionalDouble(offsetElement, "y") ?? 0,
            };
        }

        return new UnityNavicharaRootTransform
        {
            Scale = ReadOptionalDouble(rootTransform, "scale") ?? 1.0,
            Offset = offset,
        };
    }

    private static List<UnityNavicharaSourceSlot> ReadSourceSlots(JsonElement clipElement)
    {
        return ReadSourceSlotArray(clipElement, "sourceSlots");
    }

    private static List<UnityNavicharaSourceSlot> ReadSourceSlotArray(JsonElement element, string propertyName)
    {
        var result = new List<UnityNavicharaSourceSlot>();
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(propertyName, out var slots)
            || slots.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var slot in slots.EnumerateArray())
        {
            if (slot.ValueKind != JsonValueKind.Object || !slot.TryGetProperty("animation", out var animationElement))
            {
                continue;
            }

            var animation = animationElement.GetString();
            if (string.IsNullOrWhiteSpace(animation))
            {
                continue;
            }

            object frame = "curve";
            if (slot.TryGetProperty("frame", out var frameElement))
            {
                frame = frameElement.ValueKind switch
                {
                    JsonValueKind.Number when frameElement.TryGetInt32(out var intValue) => intValue,
                    JsonValueKind.Number => frameElement.GetDouble(),
                    JsonValueKind.String => frameElement.GetString() ?? "curve",
                    _ => "curve",
                };
            }

            result.Add(new UnityNavicharaSourceSlot
            {
                Animation = animation,
                Frame = frame,
                Repeat = ReadOptionalBool(slot, "repeat"),
            });
        }

        return result;
    }

    private static string? ReadOptionalString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static bool? ReadOptionalBool(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;
    }

    private static int? ReadOptionalInt(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return value.TryGetInt32(out var intValue) ? intValue : null;
    }

    private static double? ReadOptionalDouble(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return value.TryGetDouble(out var doubleValue) ? doubleValue : null;
    }

    private static IReadOnlyList<int>? ReadIntArray(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var result = new List<int>();
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out var value))
            {
                result.Add(value);
            }
        }

        return result;
    }

    private static object? ReadDurationFrames(JsonElement element)
    {
        if (!element.TryGetProperty("durationFrames", out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var intValue) => intValue,
            JsonValueKind.Number => value.GetDouble(),
            JsonValueKind.String => value.GetString(),
            _ => null,
        };
    }
}
