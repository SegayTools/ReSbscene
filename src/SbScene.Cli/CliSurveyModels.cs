internal static partial class CliApp
{
    static string FormatCrfdField90Field91Field92Relation(SbSceneCrfdRecord record)
    {
        var has90 = !string.IsNullOrWhiteSpace(record.Field90);
        var has91 = !string.IsNullOrWhiteSpace(record.Field91);
        var has92 = record.Field92 is not null;
        if (!has90 || !has91 || !has92)
        {
            return $"0x90={(has90 ? "present" : "empty")}|0x91={(has91 ? "present" : "empty")}|0x92={(has92 ? "present" : "empty")}";
        }

        var field92 = record.Field92!.Value.ToString(CultureInfo.InvariantCulture);
        var equal90And91 = string.Equals(record.Field90, record.Field91, StringComparison.Ordinal);
        var equal90And92 = string.Equals(record.Field90, field92, StringComparison.Ordinal);
        var equal91And92 = string.Equals(record.Field91, field92, StringComparison.Ordinal);

        return (equal90And91, equal90And92, equal91And92) switch
        {
            (true, true, true) => "0x90==0x91==0x92",
            (true, false, false) => "0x90==0x91!=0x92",
            (false, true, false) => "0x90==0x92!=0x91",
            (false, false, true) => "0x91==0x92!=0x90",
            _ => "all-present-all-distinct",
        };
    }

    static string FormatSliceRecordShape(SbSceneSliceRecord record)
    {
        return string.Join("|", new[]
        {
            FormatNullableHex(record.Field83),
            FormatNullableInt(record.Field40),
            FormatNullableInt(record.Field41),
            FormatNullableInt(record.Field45),
            FormatNullableInt(record.Field39Colors.Count),
            record.Field37Color?.Hex ?? "?",
            record.Field38Color?.Hex ?? "?",
        });
    }

    static string? FormatCnumOwnerName(SbSceneCnumRecord record)
    {
        if (!string.IsNullOrEmpty(record.NodeName) && !string.IsNullOrEmpty(record.FieldA1))
        {
            return $"{record.NodeName}|{record.FieldA1}";
        }

        return record.NodeName ?? record.FieldA1;
    }

    internal sealed class SurveyResult
    {
        /// <summary>
        /// 获取或设置输入，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public required string Input { get; init; }

        /// <summary>
        /// 获取或设置Filter，用于记录 survey 筛选条件或匹配范围，便于解释统计结果。
        /// </summary>
        public string? Filter { get; init; }

        /// <summary>
        /// 获取或设置Scenes，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyList<SceneSurveyRow> Scenes { get; init; }

        /// <summary>
        /// 获取或设置Svos，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public required IReadOnlyList<SvoSurveyRow> Svos { get; init; }

        /// <summary>
        /// 获取或设置场景Aggregate，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public required SceneSurveyAggregate SceneAggregate { get; init; }

        /// <summary>
        /// 获取或设置SVOAggregate，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public required SvoSurveyAggregate SvoAggregate { get; init; }
    }

    internal sealed class SceneSurveyRow
    {
        /// <summary>
        /// 获取或设置路径，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public required string Path { get; init; }

        /// <summary>
        /// 获取或设置Relative路径，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public required string RelativePath { get; init; }

        /// <summary>
        /// 获取或设置大小，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public required long Size { get; init; }

        /// <summary>
        /// 获取或设置Error，用于记录解析或 survey 过程中的错误信息，便于 CLI 报告失败原因。
        /// </summary>
        public string? Error { get; init; }

        /// <summary>
        /// 获取或设置根Param原始字节内容，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public string? RootParamRaw { get; init; }

        /// <summary>
        /// 获取或设置根ParamLow，用于保留源块参数或字段参数，便于 inspect 输出和后续格式推断。
        /// </summary>
        public int? RootParamLow { get; init; }

        /// <summary>
        /// 获取或设置根ParamHigh，用于保留源块参数或字段参数，便于 inspect 输出和后续格式推断。
        /// </summary>
        public int? RootParamHigh { get; init; }

        /// <summary>
        /// 获取或设置TotalVTBF 根块集合，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public int TotalBlocks { get; init; }

        /// <summary>
        /// 表示VTBFTag数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> VtbfTagCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示VTBFTagParam原始字节内容数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public IReadOnlyDictionary<string, int> VtbfTagParamRawCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示VTBFTagParamLowHigh数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> VtbfTagParamLowHighCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示VTBFTagProperty数量数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> VtbfTagPropertyCountCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示VTBFTagParamHighProperty数量数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> VtbfTagParamHighPropertyCountCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示VTBFTagTrailingByte数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> VtbfTagTrailingByteCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示VTBFKeyParamHighModulo5数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> VtbfKeyParamHighModulo5Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示VTBF 字段目录数量统计，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public IReadOnlyDictionary<string, int> VtbfFieldDirectoryCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示VTBF 字段目录块数量统计，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public IReadOnlyDictionary<string, int> VtbfFieldDirectoryBlockCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示VTBF 字段数量值数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> VtbfFieldCountValueCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示VTBF 字段Stride值数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public IReadOnlyDictionary<string, int> VtbfFieldStrideValueCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示SharedPackedStateOwner数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> SharedPackedStateOwnerCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示SharedPackedStateOwner原始字节内容数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public IReadOnlyDictionary<string, int> SharedPackedStateOwnerRawCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示SharedPackedStateOwnerBit数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> SharedPackedStateOwnerBitCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示SharedPackedStateOwnerLowNibble数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> SharedPackedStateOwnerLowNibbleCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示SharedPackedStateOwnerMaskF0数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> SharedPackedStateOwnerMaskF0Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示SharedPackedStateOwnerMaskF00数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> SharedPackedStateOwnerMaskF00Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示SharedPackedStateOwnerUpperMask数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> SharedPackedStateOwnerUpperMaskCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示CatrField03数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> CatrField03Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示CatrField0D数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> CatrField0DCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示CatrField0E数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> CatrField0ECounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示CatrField0F类型数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> CatrField0FTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示CatrField0F诊断预览文本数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> CatrField0FPreviewCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Catr字段Sequence数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> CatrFieldSequenceCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Catr字段Set数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> CatrFieldSetCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示ProjectField00数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> ProjectField00Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示ProjectField01数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> ProjectField01Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示ProjectField05数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> ProjectField05Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示ProjectField55数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> ProjectField55Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示ProjectField56数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> ProjectField56Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示ProjectField56轨道上次使用的Relation数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public IReadOnlyDictionary<string, int> ProjectField56TrackLastRelationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示ProjectField56Key最大值Relation数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> ProjectField56KeyMaxRelationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示ProjectField56DeltaTo轨道上次使用的数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public IReadOnlyDictionary<string, int> ProjectField56DeltaToTrackLastCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示ProjectField56DeltaToKey最大值数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> ProjectField56DeltaToKeyMaxCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Project字段Sequence数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> ProjectFieldSequenceCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Project字段Set数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> ProjectFieldSetCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Scn名称数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> ScnNameCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示ScnField04原始字节内容Hex数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public IReadOnlyDictionary<string, int> ScnField04RawHexCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示ScnField10数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> ScnField10Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示ScnField11数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> ScnField11Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示ScnField40数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> ScnField40Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示ScnField41数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> ScnField41Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示ScnField10Field11数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> ScnField10Field11Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示ScnField40Field41数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> ScnField40Field41Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示ScnParamLowLayer数量Delta数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> ScnParamLowLayerCountDeltaCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示ScnParamLowField10Delta数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> ScnParamLowField10DeltaCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示ScnField10Layer数量Delta数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> ScnField10LayerCountDeltaCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Scn字段Sequence数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> ScnFieldSequenceCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Scn字段Set数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> ScnFieldSetCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Layer名称数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> LayerNameCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示LayerField20数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> LayerField20Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示LayerField20Bit数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> LayerField20BitCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示LayerField21数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> LayerField21Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示LayerField21Bit数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> LayerField21BitCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示LayerField22数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> LayerField22Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示LayerField22Bit数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> LayerField22BitCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示LayerField21场景节点数量Delta数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public IReadOnlyDictionary<string, int> LayerField21SceneNodeCountDeltaCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示LayerParamLowField22Delta数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> LayerParamLowField22DeltaCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Layer字段Sequence数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> LayerFieldSequenceCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Layer字段Set数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> LayerFieldSetCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Camera名称数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> CameraNameCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示CameraField12Vector数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> CameraField12VectorCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示CameraField13Vector数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> CameraField13VectorCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示CameraField14数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> CameraField14Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示CameraField14Bit数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> CameraField14BitCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示CameraField15数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> CameraField15Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示CameraField16数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> CameraField16Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Camera字段Sequence数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> CameraFieldSequenceCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Camera字段Set数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> CameraFieldSetCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示动画字段Sequence数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public IReadOnlyDictionary<string, int> AnimationFieldSequenceCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示动画字段Set数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public IReadOnlyDictionary<string, int> AnimationFieldSetCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示动画ParamLowMotionDelta数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public IReadOnlyDictionary<string, int> AnimationParamLowMotionDeltaCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示动画Field50MotionDelta数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public IReadOnlyDictionary<string, int> AnimationField50MotionDeltaCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示动画Field50最大值Motion轨道Delta数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public IReadOnlyDictionary<string, int> AnimationField50MaxMotionTrackDeltaCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示动画Field50MotionOr最大值轨道Relation数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public IReadOnlyDictionary<string, int> AnimationField50MotionOrMaxTrackRelationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示动画ParamLowField50Delta数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public IReadOnlyDictionary<string, int> AnimationParamLowField50DeltaCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示动画Field5F数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public IReadOnlyDictionary<string, int> AnimationField5FCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示动画Field5FMotionPresence数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public IReadOnlyDictionary<string, int> AnimationField5FMotionPresenceCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示动画Field5F动画名称数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> AnimationField5FAnimationNameCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示动画Field5FParamLowMotionDelta数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public IReadOnlyDictionary<string, int> AnimationField5FParamLowMotionDeltaCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示动画Field5FField50MotionDelta数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public IReadOnlyDictionary<string, int> AnimationField5FField50MotionDeltaCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示动画Field5FField50Relation数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public IReadOnlyDictionary<string, int> AnimationField5FField50RelationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示动画Field5F结束帧Relation数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public IReadOnlyDictionary<string, int> AnimationField5FEndFrameRelationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示动画结束帧Relation数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public IReadOnlyDictionary<string, int> AnimationEndFrameRelationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示动画结束帧DeltaTo轨道上次使用的数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public IReadOnlyDictionary<string, int> AnimationEndFrameDeltaToTrackLastCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示动画结束帧DeltaToKey最大值数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public IReadOnlyDictionary<string, int> AnimationEndFrameDeltaToKeyMaxCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Motion字段Sequence数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> MotionFieldSequenceCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Motion字段Set数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> MotionFieldSetCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示MotionParamLow轨道Delta数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public IReadOnlyDictionary<string, int> MotionParamLowTrackDeltaCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示MotionField52轨道Delta数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public IReadOnlyDictionary<string, int> MotionField52TrackDeltaCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示MotionParamLowField52Delta数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> MotionParamLowField52DeltaCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Motion目标索引范围数量统计，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public IReadOnlyDictionary<string, int> MotionTargetIndexRangeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Unknown类型代码数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> UnknownTypeCodeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示非致命警告列表，用于把非致命问题返回给调用方，便于诊断解析、渲染或导出过程。
        /// </summary>
        public IReadOnlyList<string> Warnings { get; init; } = [];

        /// <summary>
        /// 获取或设置节点数量，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public int NodeCount { get; init; }

        /// <summary>
        /// 获取或设置Transform2D数量，用于报告数量或统计值，便于调用方校验结构规模和处理结果。
        /// </summary>
        public int Transform2DCount { get; init; }

        /// <summary>
        /// 获取或设置图像Cast数量，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public int ImageCastCount { get; init; }

        /// <summary>
        /// 获取或设置Cnum数量，用于报告数量或统计值，便于调用方校验结构规模和处理结果。
        /// </summary>
        public int CnumCount { get; init; }

        /// <summary>
        /// 获取或设置CnumCrop引用数量，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public int CnumCropReferenceCount { get; init; }

        /// <summary>
        /// 获取或设置CnumField44Matches，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public int CnumField44Matches { get; init; }

        /// <summary>
        /// 获取或设置CnumField44Mismatches，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public int CnumField44Mismatches { get; init; }

        /// <summary>
        /// 获取或设置CnumField44Missing，用于统计缺失字段或记录的样本，帮助定位格式差异。
        /// </summary>
        public int CnumField44Missing { get; init; }

        /// <summary>
        /// 获取或设置CnumField51In范围，用于记录统计或范围信息，便于校验结构规模、覆盖率和异常样本。
        /// </summary>
        public int CnumField51InRange { get; init; }

        /// <summary>
        /// 获取或设置CnumField51OutOf范围，用于记录统计或范围信息，便于校验结构规模、覆盖率和异常样本。
        /// </summary>
        public int CnumField51OutOfRange { get; init; }

        /// <summary>
        /// 获取或设置CnumField51Missing，用于统计缺失字段或记录的样本，帮助定位格式差异。
        /// </summary>
        public int CnumField51Missing { get; init; }

        /// <summary>
        /// 表示CnumField44数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> CnumField44Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示CnumZeroMarker字段数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> CnumZeroMarkerFieldCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Cnum字段A1数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> CnumFieldA1Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示CnumField48数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> CnumField48Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Cnum字段A0数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> CnumFieldA0Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Cnum字段A1原始字节内容字节长度数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public IReadOnlyDictionary<string, int> CnumFieldA1RawLengthCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Cnum字段A1内容字节长度数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public IReadOnlyDictionary<string, int> CnumFieldA1ContentLengthCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Cnum字段A1Utf8Status数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> CnumFieldA1Utf8StatusCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Cnum字段A1ShiftJisByteShape数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public IReadOnlyDictionary<string, int> CnumFieldA1ShiftJisByteShapeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Cnum字段A1原始字节内容诊断预览文本数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public IReadOnlyDictionary<string, int> CnumFieldA1RawPreviewCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Cnum字段A1Field44数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> CnumFieldA1Field44Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Cnum字段A1Crop引用数量数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public IReadOnlyDictionary<string, int> CnumFieldA1CropReferenceCountCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Cnum字段A1ZeroMarker字段数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> CnumFieldA1ZeroMarkerFieldCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Cnum字段A1节点Flag数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public IReadOnlyDictionary<string, int> CnumFieldA1NodeFlagCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Cnum字段A1节点Group数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public IReadOnlyDictionary<string, int> CnumFieldA1NodeGroupCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Cnum字段A1Display数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> CnumFieldA1DisplayCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Cnum字段A1Cimg目标数量统计，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public IReadOnlyDictionary<string, int> CnumFieldA1CimgTargetCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Cnum字段A1Animated目标数量统计，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public IReadOnlyDictionary<string, int> CnumFieldA1AnimatedTargetCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Cnum字段Sequence数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> CnumFieldSequenceCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Cnum字段Set数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> CnumFieldSetCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 获取或设置Crfd数量，用于报告数量或统计值，便于调用方校验结构规模和处理结果。
        /// </summary>
        public int CrfdCount { get; init; }

        /// <summary>
        /// 获取或设置CrfdField51In范围，用于记录统计或范围信息，便于校验结构规模、覆盖率和异常样本。
        /// </summary>
        public int CrfdField51InRange { get; init; }

        /// <summary>
        /// 获取或设置CrfdField51OutOf范围，用于记录统计或范围信息，便于校验结构规模、覆盖率和异常样本。
        /// </summary>
        public int CrfdField51OutOfRange { get; init; }

        /// <summary>
        /// 获取或设置CrfdField51Missing，用于统计缺失字段或记录的样本，帮助定位格式差异。
        /// </summary>
        public int CrfdField51Missing { get; init; }

        /// <summary>
        /// 表示CrfdField90数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> CrfdField90Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示CrfdField91数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> CrfdField91Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示CrfdField90Field91数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> CrfdField90Field91Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示CrfdField90Field91Field92数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> CrfdField90Field91Field92Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Crfd字符串字段类型代码字段Relation数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> CrfdStringFieldRelationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Crfd字符串字段类型代码字段目标类型数量统计，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public IReadOnlyDictionary<string, int> CrfdStringFieldTargetTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示CrfdField90Field91Relation数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> CrfdField90Field91RelationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示CrfdField90Field91Equality数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> CrfdField90Field91EqualityCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示CrfdField90Field91Field92Relation数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> CrfdField90Field91Field92RelationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示CrfdField92数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> CrfdField92Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示CrfdField93数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> CrfdField93Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示CrfdField94数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> CrfdField94Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 获取或设置CrfdField94NonZero，用于表示状态开关或检测结果，调用方据此选择显示、解析、导出或诊断分支。
        /// </summary>
        public int CrfdField94NonZero { get; init; }

        /// <summary>
        /// 表示CrfdField95数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> CrfdField95Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 获取或设置文本数量，用于报告数量或统计值，便于调用方校验结构规模和处理结果。
        /// </summary>
        public int TextCount { get; init; }

        /// <summary>
        /// 获取或设置文本Field7Alpha 透明度通道值Present，用于保存源字段文本或诊断说明，便于展示、校验和导出报告。
        /// </summary>
        public int TextField7APresent { get; init; }

        /// <summary>
        /// 表示文本ZeroMarker字段数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> TextZeroMarkerFieldCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示文本Field41数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> TextField41Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示文本Field78数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> TextField78Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示文本Field79数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> TextField79Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示文本Field7C数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> TextField7CCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示文本Field7Alpha 透明度通道值字符串字段类型代码数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> TextField7AStringCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示文本Field7Alpha 透明度通道值原始字节内容字节长度数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public IReadOnlyDictionary<string, int> TextField7ARawLengthCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示文本Field7Alpha 透明度通道值内容字节长度数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public IReadOnlyDictionary<string, int> TextField7AContentLengthCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示文本Field7Alpha 透明度通道值Utf8Status数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> TextField7AUtf8StatusCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示文本Field7Alpha 透明度通道值ShiftJisByteShape数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public IReadOnlyDictionary<string, int> TextField7AShiftJisByteShapeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示文本Field7Alpha 透明度通道值ShiftJisDecodeStatus数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> TextField7AShiftJisDecodeStatusCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示文本Field7Alpha 透明度通道值ShiftJis字符串字段类型代码数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> TextField7AShiftJisStringCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示文本Field7Alpha 透明度通道值原始字节内容诊断预览文本数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public IReadOnlyDictionary<string, int> TextField7ARawPreviewCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示文本Field7Alpha 透明度通道值Field41数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> TextField7AField41Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示文本Field7Alpha 透明度通道值Field78数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> TextField7AField78Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示文本Field7Alpha 透明度通道值Field79数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> TextField7AField79Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示文本Field7Alpha 透明度通道值Field7C数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> TextField7AField7CCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示文本Field33Vector数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> TextField33VectorCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示文本Field33原始字节内容Hex数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public IReadOnlyDictionary<string, int> TextField33RawHexCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示文本Field7蓝色通道值Packed值集合数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> TextField7BPackedValuesCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示文本Field7蓝色通道值原始字节内容Hex数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public IReadOnlyDictionary<string, int> TextField7BRawHexCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示文本Field78Field79数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> TextField78Field79Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示文本ZeroMarkerField7Alpha 透明度通道值数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> TextZeroMarkerField7ACounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示文本字段Sequence数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> TextFieldSequenceCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示文本字段Set数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> TextFieldSetCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 获取或设置SliceCast数量，用于报告数量或统计值，便于调用方校验结构规模和处理结果。
        /// </summary>
        public int SliceCastCount { get; init; }

        /// <summary>
        /// 获取或设置SliceRecord数量，用于报告数量或统计值，便于调用方校验结构规模和处理结果。
        /// </summary>
        public int SliceRecordCount { get; init; }

        /// <summary>
        /// 获取或设置SliceCrop引用数量，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public int SliceCropReferenceCount { get; init; }

        /// <summary>
        /// 获取或设置SliceField44SlicRecordMatches，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public int SliceField44SlicRecordMatches { get; init; }

        /// <summary>
        /// 获取或设置SliceField44SlicRecordMismatches，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public int SliceField44SlicRecordMismatches { get; init; }

        /// <summary>
        /// 获取或设置SliceField44Crop引用Matches，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public int SliceField44CropReferenceMatches { get; init; }

        /// <summary>
        /// 获取或设置SliceField44Crop引用Mismatches，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public int SliceField44CropReferenceMismatches { get; init; }

        /// <summary>
        /// 获取或设置Slice目标索引In范围，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public int SliceTargetIndexInRange { get; init; }

        /// <summary>
        /// 获取或设置Slice目标索引OutOf范围，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public int SliceTargetIndexOutOfRange { get; init; }

        /// <summary>
        /// 表示SliceField83数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> SliceField83Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示SliceCastField40数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> SliceCastField40Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示SliceCastField41数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> SliceCastField41Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示SliceCastField42数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> SliceCastField42Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示SliceCastField43数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> SliceCastField43Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示SliceCastField80数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> SliceCastField80Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示SliceCastField81数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> SliceCastField81Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示SliceCastField82数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> SliceCastField82Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示SliceCastField84数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> SliceCastField84Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示SliceCastField85数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> SliceCastField85Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示SliceCastField86数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> SliceCastField86Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示SliceCastField87数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> SliceCastField87Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示SliceCast目标节点Flag数量统计，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public IReadOnlyDictionary<string, int> SliceCastTargetNodeFlagCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示SliceCast目标节点Group数量统计，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public IReadOnlyDictionary<string, int> SliceCastTargetNodeGroupCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示SliceCast目标Display数量统计，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public IReadOnlyDictionary<string, int> SliceCastTargetDisplayCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示SliceCast目标Cimg目标数量统计，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public IReadOnlyDictionary<string, int> SliceCastTargetCimgTargetCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示SliceCast字段Sequence数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> SliceCastFieldSequenceCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示SliceCast字段Set数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> SliceCastFieldSetCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示SliceRecordField40数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> SliceRecordField40Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示SliceRecordField41数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> SliceRecordField41Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示SliceRecordField45数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> SliceRecordField45Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示SliceRecordField37颜色数量统计，用于参与颜色、透明度、照明或混合计算。
        /// </summary>
        public IReadOnlyDictionary<string, int> SliceRecordField37ColorCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示SliceRecordField38颜色数量统计，用于参与颜色、透明度、照明或混合计算。
        /// </summary>
        public IReadOnlyDictionary<string, int> SliceRecordField38ColorCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示SliceRecordField39颜色数量统计，用于参与颜色、透明度、照明或混合计算。
        /// </summary>
        public IReadOnlyDictionary<string, int> SliceRecordField39ColorCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示SliceRecordField39颜色数量数量统计，用于参与颜色、透明度、照明或混合计算。
        /// </summary>
        public IReadOnlyDictionary<string, int> SliceRecordField39ColorCountCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示SliceRecordField83Field40数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> SliceRecordField83Field40Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示SliceRecordField83Field41数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> SliceRecordField83Field41Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示SliceRecordField83Field45数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> SliceRecordField83Field45Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示SliceRecord字段Sequence数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> SliceRecordFieldSequenceCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示SliceRecord字段Set数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> SliceRecordFieldSetCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示SliceRecordShape数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> SliceRecordShapeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 获取或设置数据块数量，用于报告数量或统计值，便于调用方校验结构规模和处理结果。
        /// </summary>
        public int DataBlockCount { get; init; }

        /// <summary>
        /// 表示数据ParamLow值集合，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyList<int> DataParamLowValues { get; init; } = [];

        /// <summary>
        /// 表示数据Following图像Cast数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public IReadOnlyList<int> DataFollowingImageCastCounts { get; init; } = [];

        /// <summary>
        /// 表示数据FollowingCimgCrfd数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyList<int> DataFollowingCimgCrfdCounts { get; init; } = [];

        /// <summary>
        /// 表示数据FollowingCimgCnumCrfd数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyList<int> DataFollowingCimgCnumCrfdCounts { get; init; } = [];

        /// <summary>
        /// 表示数据FollowingCimgCnumCrfdCsli数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyList<int> DataFollowingCimgCnumCrfdCsliCounts { get; init; } = [];

        /// <summary>
        /// 表示数据FollowingTag数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyList<IReadOnlyDictionary<string, int>> DataFollowingTagCounts { get; init; } = [];

        /// <summary>
        /// 获取或设置数据字段明细集合，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public int DataFields { get; init; }

        /// <summary>
        /// 获取或设置数据Trailing字节字段类型代码，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public int DataTrailingBytes { get; init; }

        /// <summary>
        /// 获取或设置数据ParamLowMatches图像Casts，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public bool? DataParamLowMatchesImageCasts { get; init; }

        /// <summary>
        /// 获取或设置数据ParamLowMatchesFollowing图像Casts，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public bool? DataParamLowMatchesFollowingImageCasts { get; init; }

        /// <summary>
        /// 获取或设置数据ParamLowMatchesFollowingCimgCrfd，用于保留源块参数或字段参数，便于 inspect 输出和后续格式推断。
        /// </summary>
        public bool? DataParamLowMatchesFollowingCimgCrfd { get; init; }

        /// <summary>
        /// 获取或设置数据ParamLowMatchesFollowingCimgCnumCrfd，用于保留源块参数或字段参数，便于 inspect 输出和后续格式推断。
        /// </summary>
        public bool? DataParamLowMatchesFollowingCimgCnumCrfd { get; init; }

        /// <summary>
        /// 获取或设置数据ParamLowMatchesFollowingCimgCnumCrfdCsli，用于保留源块参数或字段参数，便于 inspect 输出和后续格式推断。
        /// </summary>
        public bool? DataParamLowMatchesFollowingCimgCnumCrfdCsli { get; init; }

        /// <summary>
        /// 获取或设置NcatRecord数量，用于报告数量或统计值，便于调用方校验结构规模和处理结果。
        /// </summary>
        public int NcatRecordCount { get; init; }

        /// <summary>
        /// 获取或设置NcatDetailRecord数量，用于报告数量或统计值，便于调用方校验结构规模和处理结果。
        /// </summary>
        public int NcatDetailRecordCount { get; init; }

        /// <summary>
        /// 获取或设置NcatNonZero数量，用于报告数量或统计值，便于调用方校验结构规模和处理结果。
        /// </summary>
        public int NcatNonZeroCount { get; init; }

        /// <summary>
        /// 获取或设置NcatMatches节点集合，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public bool? NcatMatchesNodes { get; init; }

        /// <summary>
        /// 获取或设置NcatRecordsWithCategory，用于描述位置、旋转、缩放或矩阵状态，参与渲染坐标和导出坐标计算。
        /// </summary>
        public int NcatRecordsWithCategory { get; init; }

        /// <summary>
        /// 获取或设置NcatRecordsWithoutCategory，用于描述位置、旋转、缩放或矩阵状态，参与渲染坐标和导出坐标计算。
        /// </summary>
        public int NcatRecordsWithoutCategory { get; init; }

        /// <summary>
        /// 表示Ncat类别数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> NcatKindCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Ncat类型Byte数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> NcatTypeByteCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示NcatCategory数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> NcatCategoryCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Ncat类别类型Byte数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> NcatKindTypeByteCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Ncat类别Category数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> NcatKindCategoryCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Ncat类型ByteCategory数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> NcatTypeByteCategoryCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Ncat类别ParameterPresence数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> NcatKindParameterPresenceCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示NcatParameter字符串字段类型代码数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> NcatParameterStringCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示NcatParameter字段类型数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> NcatParameterFieldTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Ncat类别Parameter字段类型数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> NcatKindParameterFieldTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示NcatCategoryParameter字段类型数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> NcatCategoryParameterFieldTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示NcatParameter字段类型诊断预览文本数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> NcatParameterFieldTypePreviewCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Ncat类别节点Flag数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> NcatKindNodeFlagCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Ncat类别节点FlagBit数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> NcatKindNodeFlagBitCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Ncat类别节点Group数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> NcatKindNodeGroupCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Ncat类别Display数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> NcatKindDisplayCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Ncat类别Cimg目标数量统计，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public IReadOnlyDictionary<string, int> NcatKindCimgTargetCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Ncat类别Animated节点数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> NcatKindAnimatedNodeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示节点Flag数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public IReadOnlyDictionary<string, int> NodeFlagCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示节点FlagBit数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public IReadOnlyDictionary<string, int> NodeFlagBitCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示节点FlagBitDisplayFalse节点数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public IReadOnlyDictionary<string, int> NodeFlagBitDisplayFalseNodeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示节点FlagBitCimg目标节点数量统计，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public IReadOnlyDictionary<string, int> NodeFlagBitCimgTargetNodeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示节点FlagBitAnimated节点数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public IReadOnlyDictionary<string, int> NodeFlagBitAnimatedNodeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示节点FlagBit数据节点数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public IReadOnlyDictionary<string, int> NodeFlagBitDataNodeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示节点FlagBitCategoryRecord节点数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public IReadOnlyDictionary<string, int> NodeFlagBitCategoryRecordNodeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示节点FlagBitCategoryNonZero节点数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public IReadOnlyDictionary<string, int> NodeFlagBitCategoryNonZeroNodeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示节点FlagBitExactFlag数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public IReadOnlyDictionary<string, int> NodeFlagBitExactFlagCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示节点FlagBitGroup数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public IReadOnlyDictionary<string, int> NodeFlagBitGroupCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示节点FlagBit图像CastFlagBit数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public IReadOnlyDictionary<string, int> NodeFlagBitImageCastFlagBitCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示节点FlagBit轨道类型数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> NodeFlagBitTrackTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示节点FlagBitPair数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public IReadOnlyDictionary<string, int> NodeFlagBitPairCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 获取或设置Cimg44Matches，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public int Cimg44Matches { get; init; }

        /// <summary>
        /// 获取或设置Cimg44Mismatches，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public int Cimg44Mismatches { get; init; }

        /// <summary>
        /// 获取或设置Cimg44Unknown，用于表达该模型在解析、渲染或导出流程中的具体业务含义。
        /// </summary>
        public int Cimg44Unknown { get; init; }

        /// <summary>
        /// 表示Cimg44数量Tuple数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> Cimg44CountTupleCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Cimg44Primary数量数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> Cimg44PrimaryCountCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Cimg44Secondary数量数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> Cimg44SecondaryCountCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Cimg44SecondaryNonZeroSamples，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyList<Cimg44SecondaryNonZeroSample> Cimg44SecondaryNonZeroSamples { get; init; } = [];

        /// <summary>
        /// 获取或设置Cimg45ActiveGroups，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public int Cimg45ActiveGroups { get; init; }

        /// <summary>
        /// 获取或设置Cimg45In范围Groups，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public int Cimg45InRangeGroups { get; init; }

        /// <summary>
        /// 获取或设置Cimg45OutOf范围Groups，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public int Cimg45OutOfRangeGroups { get; init; }

        /// <summary>
        /// 获取或设置Cimg45EmptyGroupNonZero，用于表示状态开关或检测结果，调用方据此选择显示、解析、导出或诊断分支。
        /// </summary>
        public int Cimg45EmptyGroupNonZero { get; init; }

        /// <summary>
        /// 获取或设置Cimg45NonZero索引集合，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public int Cimg45NonZeroIndices { get; init; }

        /// <summary>
        /// 获取或设置Cimg45NonZero图像Casts，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public int Cimg45NonZeroImageCasts { get; init; }

        /// <summary>
        /// 表示Cimg45Group索引数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public IReadOnlyDictionary<string, int> Cimg45GroupIndexCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Cimg45Group数量索引数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public IReadOnlyDictionary<string, int> Cimg45GroupCountIndexCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Cimg45NonZeroGroup数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> Cimg45NonZeroGroupCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Cimg45NonZeroSamples，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyList<Cimg45NonZeroSample> Cimg45NonZeroSamples { get; init; } = [];

        /// <summary>
        /// 表示CimgFlag数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> CimgFlagCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示CimgFlagBit数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> CimgFlagBitCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示CimgFlagBitDisplayFalse数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> CimgFlagBitDisplayFalseCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示CimgFlagBitMulti引用数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public IReadOnlyDictionary<string, int> CimgFlagBitMultiReferenceCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示CimgFlagBitSecondary引用数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public IReadOnlyDictionary<string, int> CimgFlagBitSecondaryReferenceCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示CimgFlagBitNonZero引用索引数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public IReadOnlyDictionary<string, int> CimgFlagBitNonZeroReferenceIndexCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示CimgFlagBitMissing节点数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public IReadOnlyDictionary<string, int> CimgFlagBitMissingNodeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示CimgFlagBit节点Flag数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public IReadOnlyDictionary<string, int> CimgFlagBitNodeFlagCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示CimgFlagBitGroup数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> CimgFlagBitGroupCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示CimgFlagBitPair数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> CimgFlagBitPairCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 获取或设置纹理Atlas数量，用于报告数量或统计值，便于调用方校验结构规模和处理结果。
        /// </summary>
        public int TextureAtlasCount { get; init; }

        /// <summary>
        /// 表示纹理AtlasField62数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> TextureAtlasField62Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示纹理AtlasField62Bit数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> TextureAtlasField62BitCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示纹理AtlasField62Crop数量数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> TextureAtlasField62CropCountCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示纹理AtlasField62大小数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public IReadOnlyDictionary<string, int> TextureAtlasField62SizeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Crop类别数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> CropKindCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Cref类别数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> CrefKindCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 获取或设置Crop矩形数量，用于确定渲染区域、裁剪范围、采样质量或输出尺寸。
        /// </summary>
        public int CropRectCount { get; init; }

        /// <summary>
        /// 获取或设置CropAtlasDeclared数量Matches，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public int CropAtlasDeclaredCountMatches { get; init; }

        /// <summary>
        /// 获取或设置CropAtlasDeclared数量Mismatches，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public int CropAtlasDeclaredCountMismatches { get; init; }

        /// <summary>
        /// 获取或设置Crop矩形InAtlas边界，用于确定渲染区域、裁剪范围、采样质量或输出尺寸。
        /// </summary>
        public int CropRectInAtlasBounds { get; init; }

        /// <summary>
        /// 获取或设置Crop矩形OutOfAtlas边界，用于确定渲染区域、裁剪范围、采样质量或输出尺寸。
        /// </summary>
        public int CropRectOutOfAtlasBounds { get; init; }

        /// <summary>
        /// 获取或设置Crop矩形NonPositive大小，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public int CropRectNonPositiveSize { get; init; }

        /// <summary>
        /// 获取或设置Crop引用数量，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public int CropReferenceCount { get; init; }

        /// <summary>
        /// 表示Crop引用类别数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> CropReferenceKindCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Crop引用Owner数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public IReadOnlyDictionary<string, int> CropReferenceOwnerCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Crop引用Owner类别数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> CropReferenceOwnerKindCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Crop引用纹理List索引数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public IReadOnlyDictionary<string, int> CropReferenceTextureListIndexCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Crop引用纹理索引范围数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public IReadOnlyDictionary<string, int> CropReferenceTextureIndexRangeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Crop引用Crop索引范围数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public IReadOnlyDictionary<string, int> CropReferenceCropIndexRangeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Crop矩形OutOfAtlas边界Reason数量统计，用于确定渲染区域、裁剪范围、采样质量或输出尺寸。
        /// </summary>
        public IReadOnlyDictionary<string, int> CropRectOutOfAtlasBoundsReasonCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Crop引用OutOf范围Owner数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public IReadOnlyDictionary<string, int> CropReferenceOutOfRangeOwnerCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Crop矩形OutOfAtlas边界Samples，用于确定渲染区域、裁剪范围、采样质量或输出尺寸。
        /// </summary>
        public IReadOnlyList<CropRectBoundsSample> CropRectOutOfAtlasBoundsSamples { get; init; } = [];

        /// <summary>
        /// 表示Crop引用OutOf范围Samples，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public IReadOnlyList<CropReferenceRangeSample> CropReferenceOutOfRangeSamples { get; init; } = [];

        /// <summary>
        /// 获取或设置轨道数量，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public int TrackCount { get; init; }

        /// <summary>
        /// 获取或设置轨道Key数量Mismatches，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public int TrackKeyCountMismatches { get; init; }

        /// <summary>
        /// 表示轨道Flag数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public IReadOnlyDictionary<string, int> TrackFlagCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示轨道FlagBase数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public IReadOnlyDictionary<string, int> TrackFlagBaseCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示轨道FlagExtra数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public IReadOnlyDictionary<string, int> TrackFlagExtraCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示轨道FlagExtra场景数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public IReadOnlyDictionary<string, int> TrackFlagExtraSceneCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示轨道FlagExtraBase数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public IReadOnlyDictionary<string, int> TrackFlagExtraBaseCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示轨道FlagExtra动画数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public IReadOnlyDictionary<string, int> TrackFlagExtraAnimationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示轨道FlagExtra轨道类型数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> TrackFlagExtraTrackTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示轨道FlagExtraKey值类型数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> TrackFlagExtraKeyValueTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示轨道FlagExtra节点Flag数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public IReadOnlyDictionary<string, int> TrackFlagExtraNodeFlagCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示轨道FlagExtra节点FlagBit数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public IReadOnlyDictionary<string, int> TrackFlagExtraNodeFlagBitCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示轨道FlagExtraGroup数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public IReadOnlyDictionary<string, int> TrackFlagExtraGroupCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示轨道FlagExtraCimg目标数量统计，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public IReadOnlyDictionary<string, int> TrackFlagExtraCimgTargetCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示轨道FlagExtraInitialDisplay数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public IReadOnlyDictionary<string, int> TrackFlagExtraInitialDisplayCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示轨道FlagExtraCimgFlag数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public IReadOnlyDictionary<string, int> TrackFlagExtraCimgFlagCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示轨道FlagExtraCimgFlagBit数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public IReadOnlyDictionary<string, int> TrackFlagExtraCimgFlagBitCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示轨道FlagExtraCimg引用数量数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public IReadOnlyDictionary<string, int> TrackFlagExtraCimgReferenceCountCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示轨道类型数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> TrackTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Key值类型数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> KeyValueTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 获取或设置KeyTangentPresent，用于描述动画时间轴、关键帧值或插值方式，影响采样、渲染和导出曲线。
        /// </summary>
        public int KeyTangentPresent { get; init; }

        /// <summary>
        /// 获取或设置KeyTangentNonZero，用于描述动画时间轴、关键帧值或插值方式，影响采样、渲染和导出曲线。
        /// </summary>
        public int KeyTangentNonZero { get; init; }

        /// <summary>
        /// 获取或设置KeyTangentMismatch，用于描述动画时间轴、关键帧值或插值方式，影响采样、渲染和导出曲线。
        /// </summary>
        public int KeyTangentMismatch { get; init; }

        /// <summary>
        /// 表示KeyInterpolation数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> KeyInterpolationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示KeyInterpolation轨道类型数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> KeyInterpolationTrackTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示KeyInterpolationKey值类型数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> KeyInterpolationKeyValueTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示KeyTangentPresentInterpolation数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> KeyTangentPresentInterpolationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示KeyTangentPresent轨道类型数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> KeyTangentPresentTrackTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示KeyTangentNonZeroInterpolation数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> KeyTangentNonZeroInterpolationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示KeyTangentNonZero轨道类型数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> KeyTangentNonZeroTrackTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示KeyTangentMismatchInterpolation数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> KeyTangentMismatchInterpolationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示KeyTangentMismatch轨道类型数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> KeyTangentMismatchTrackTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示KeyTangentMismatch动画数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public IReadOnlyDictionary<string, int> KeyTangentMismatchAnimationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示KeyTangentMismatch节点Flag数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public IReadOnlyDictionary<string, int> KeyTangentMismatchNodeFlagCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示KeyTangentMismatchGroup数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> KeyTangentMismatchGroupCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示KeyTangentMismatch轨道Extra数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public IReadOnlyDictionary<string, int> KeyTangentMismatchTrackExtraCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示KeyTangentMismatchTangentPair数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> KeyTangentMismatchTangentPairCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示KeyTangentNonZero帧位置数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public IReadOnlyDictionary<string, int> KeyTangentNonZeroFramePositionCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示KeyTangentMismatch帧位置数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public IReadOnlyDictionary<string, int> KeyTangentMismatchFramePositionCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示KeyTangentDeltaSign数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> KeyTangentDeltaSignCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示轨道KeyStorageMatrix数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public IReadOnlyDictionary<string, int> TrackKeyStorageMatrixCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示轨道字段Sequence数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public IReadOnlyDictionary<string, int> TrackFieldSequenceCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示Key字段Sequence数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> KeyFieldSequenceCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示轨道帧范围Relation数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public IReadOnlyDictionary<string, int> TrackFrameRangeRelationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示轨道Key帧Order数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public IReadOnlyDictionary<string, int> TrackKeyFrameOrderCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示轨道Key帧Duplicate数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public IReadOnlyDictionary<string, int> TrackKeyFrameDuplicateCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示轨道First帧Delta数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public IReadOnlyDictionary<string, int> TrackFirstFrameDeltaCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示轨道上次使用的帧Delta数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public IReadOnlyDictionary<string, int> TrackLastFrameDeltaCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 获取或设置变换轨道数量，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public int TransformTrackCount { get; init; }

        /// <summary>
        /// 获取或设置变换轨道Key数量，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public int TransformTrackKeyCount { get; init; }

        /// <summary>
        /// 获取或设置变换轨道集合WithInitialChannel，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public int TransformTracksWithInitialChannel { get; init; }

        /// <summary>
        /// 获取或设置变换轨道集合MissingInitialChannel，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public int TransformTracksMissingInitialChannel { get; init; }

        /// <summary>
        /// 获取或设置变换轨道Initial值Matches，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public int TransformTrackInitialValueMatches { get; init; }

        /// <summary>
        /// 获取或设置变换轨道Initial值Mismatches，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public int TransformTrackInitialValueMismatches { get; init; }

        /// <summary>
        /// 获取或设置变换轨道KeysMissing值，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public int TransformTrackKeysMissingValue { get; init; }

        /// <summary>
        /// 表示变换轨道类型数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> TransformTrackTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示变换轨道Key类型数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> TransformTrackKeyTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示变换轨道Storage数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public IReadOnlyDictionary<string, int> TransformTrackStorageCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示变换轨道Key值类别数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> TransformTrackKeyValueKindCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示变换轨道InitialMatch类型数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> TransformTrackInitialMatchTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示变换轨道值范围数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public IReadOnlyDictionary<string, int> TransformTrackValueRangeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示变换Candidate默认Key数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> TransformCandidateDefaultKeyCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 获取或设置PackedAngle轨道数量，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public int PackedAngleTrackCount { get; init; }

        /// <summary>
        /// 获取或设置PackedAngleKey数量，用于报告数量或统计值，便于调用方校验结构规模和处理结果。
        /// </summary>
        public int PackedAngleKeyCount { get; init; }

        /// <summary>
        /// 表示PackedAngle轨道类型数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> PackedAngleTrackTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示PackedAngleKey轨道类型数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> PackedAngleKeyTrackTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示PackedAngle原始字节内容数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public IReadOnlyDictionary<string, int> PackedAngleRawCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示PackedAngleDegreeCandidate数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> PackedAngleDegreeCandidateCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 获取或设置图像Variant轨道数量，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public int ImageVariantTrackCount { get; init; }

        /// <summary>
        /// 获取或设置图像VariantKey数量，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public int ImageVariantKeyCount { get; init; }

        /// <summary>
        /// 获取或设置图像Variant轨道集合WithCimg，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public int ImageVariantTracksWithCimg { get; init; }

        /// <summary>
        /// 获取或设置图像Variant轨道集合MissingCimg，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public int ImageVariantTracksMissingCimg { get; init; }

        /// <summary>
        /// 获取或设置图像Variant轨道范围Matches，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public int ImageVariantTrackRangeMatches { get; init; }

        /// <summary>
        /// 获取或设置图像Variant轨道范围Mismatches，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public int ImageVariantTrackRangeMismatches { get; init; }

        /// <summary>
        /// 获取或设置图像VariantKeysIn范围，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public int ImageVariantKeysInRange { get; init; }

        /// <summary>
        /// 获取或设置图像VariantKeysOutOf范围，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public int ImageVariantKeysOutOfRange { get; init; }

        /// <summary>
        /// 获取或设置图像VariantKeysMissingCimg，用于统计缺少 CIMG 关联的 variant key 样本，帮助定位资源映射缺口。
        /// </summary>
        public int ImageVariantKeysMissingCimg { get; init; }

        /// <summary>
        /// 获取或设置图像VariantKeysNonInteger，用于统计无法按整数解析的 variant key 样本，帮助定位格式异常。
        /// </summary>
        public int ImageVariantKeysNonInteger { get; init; }

        /// <summary>
        /// 获取或设置图像VariantKeysMissing值，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public int ImageVariantKeysMissingValue { get; init; }

        /// <summary>
        /// 表示图像Variant引用数量数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public IReadOnlyDictionary<string, int> ImageVariantReferenceCountCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示图像Variant值数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public IReadOnlyDictionary<string, int> ImageVariantValueCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示图像VariantGroup轨道数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public IReadOnlyDictionary<string, int> ImageVariantGroupTrackCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示图像VariantGroupKey数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public IReadOnlyDictionary<string, int> ImageVariantGroupKeyCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示图像VariantGroup轨道集合WithCimg数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public IReadOnlyDictionary<string, int> ImageVariantGroupTracksWithCimgCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示图像VariantGroup轨道集合MissingCimg数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public IReadOnlyDictionary<string, int> ImageVariantGroupTracksMissingCimgCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示图像VariantGroup轨道范围Match数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public IReadOnlyDictionary<string, int> ImageVariantGroupTrackRangeMatchCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示图像VariantGroup轨道范围Mismatch数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public IReadOnlyDictionary<string, int> ImageVariantGroupTrackRangeMismatchCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示图像VariantGroupKeysIn范围数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public IReadOnlyDictionary<string, int> ImageVariantGroupKeysInRangeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示图像VariantGroupKeysOutOf范围数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public IReadOnlyDictionary<string, int> ImageVariantGroupKeysOutOfRangeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示图像VariantGroupKeysMissingCimg数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public IReadOnlyDictionary<string, int> ImageVariantGroupKeysMissingCimgCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示图像VariantGroupKeysNonInteger数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public IReadOnlyDictionary<string, int> ImageVariantGroupKeysNonIntegerCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示图像VariantGroupKeysMissing值数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public IReadOnlyDictionary<string, int> ImageVariantGroupKeysMissingValueCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示图像VariantGroup引用数量数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public IReadOnlyDictionary<string, int> ImageVariantGroupReferenceCountCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示图像VariantGroup值数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public IReadOnlyDictionary<string, int> ImageVariantGroupValueCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示图像VariantGroupCimg45FirstKeyRelation数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public IReadOnlyDictionary<string, int> ImageVariantGroupCimg45FirstKeyRelationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示图像VariantGroupCimg45FirstKeyDelta数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public IReadOnlyDictionary<string, int> ImageVariantGroupCimg45FirstKeyDeltaCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示图像VariantGroupCimg45FirstKeyPair数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public IReadOnlyDictionary<string, int> ImageVariantGroupCimg45FirstKeyPairCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 获取或设置颜色轨道数量，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public int ColorTrackCount { get; init; }

        /// <summary>
        /// 获取或设置颜色轨道Key数量，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public int ColorTrackKeyCount { get; init; }

        /// <summary>
        /// 获取或设置颜色轨道集合WithInitialChannel，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public int ColorTracksWithInitialChannel { get; init; }

        /// <summary>
        /// 获取或设置颜色轨道集合MissingInitialChannel，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public int ColorTracksMissingInitialChannel { get; init; }

        /// <summary>
        /// 获取或设置颜色轨道Initial值Matches，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public int ColorTrackInitialValueMatches { get; init; }

        /// <summary>
        /// 获取或设置颜色轨道Initial值Mismatches，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public int ColorTrackInitialValueMismatches { get; init; }

        /// <summary>
        /// 获取或设置颜色轨道KeysInUnit范围，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public int ColorTrackKeysInUnitRange { get; init; }

        /// <summary>
        /// 获取或设置颜色轨道KeysOutOfUnit范围，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public int ColorTrackKeysOutOfUnitRange { get; init; }

        /// <summary>
        /// 获取或设置颜色轨道KeysMissing值，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public int ColorTrackKeysMissingValue { get; init; }

        /// <summary>
        /// 表示颜色轨道类型数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> ColorTrackTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示颜色轨道Key类型数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> ColorTrackKeyTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示颜色轨道InitialMatch类型数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> ColorTrackInitialMatchTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 获取或设置透明度不透明度轨道数量，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public int AlphaOpacityTrackCount { get; init; }

        /// <summary>
        /// 获取或设置透明度不透明度Key数量，用于统计透明度轨道解析结果，帮助判断动画不透明度是否可信。
        /// </summary>
        public int AlphaOpacityKeyCount { get; init; }

        /// <summary>
        /// 获取或设置透明度不透明度轨道集合With材质透明度，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public int AlphaOpacityTracksWithMaterialAlpha { get; init; }

        /// <summary>
        /// 获取或设置透明度不透明度轨道集合Missing材质透明度，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public int AlphaOpacityTracksMissingMaterialAlpha { get; init; }

        /// <summary>
        /// 获取或设置透明度不透明度Initial透明度Matches，用于统计透明度轨道解析结果，帮助判断动画不透明度是否可信。
        /// </summary>
        public int AlphaOpacityInitialAlphaMatches { get; init; }

        /// <summary>
        /// 获取或设置透明度不透明度Initial透明度Mismatches，用于统计透明度轨道解析结果，帮助判断动画不透明度是否可信。
        /// </summary>
        public int AlphaOpacityInitialAlphaMismatches { get; init; }

        /// <summary>
        /// 获取或设置透明度不透明度CimgTargets，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public int AlphaOpacityCimgTargets { get; init; }

        /// <summary>
        /// 获取或设置透明度不透明度DisplayFalseTargets，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public int AlphaOpacityDisplayFalseTargets { get; init; }

        /// <summary>
        /// 获取或设置透明度不透明度KeysInUnit范围，用于统计透明度轨道解析结果，帮助判断动画不透明度是否可信。
        /// </summary>
        public int AlphaOpacityKeysInUnitRange { get; init; }

        /// <summary>
        /// 获取或设置透明度不透明度KeysOutOfUnit范围，用于统计透明度轨道解析结果，帮助判断动画不透明度是否可信。
        /// </summary>
        public int AlphaOpacityKeysOutOfUnitRange { get; init; }

        /// <summary>
        /// 获取或设置透明度不透明度KeysMissing值，用于统计透明度轨道解析结果，帮助判断动画不透明度是否可信。
        /// </summary>
        public int AlphaOpacityKeysMissingValue { get; init; }
    }

    internal sealed class SceneSurveyAggregate
    {
        /// <summary>
        /// 获取或设置Total，用于报告数量或统计值，便于调用方校验结构规模和处理结果。
        /// </summary>
        public required int Total { get; init; }

        /// <summary>
        /// 获取或设置Parsed，用于表示状态开关或检测结果，调用方据此选择显示、解析、导出或诊断分支。
        /// </summary>
        public required int Parsed { get; init; }

        /// <summary>
        /// 获取或设置失败状态，用于控制对应功能开关，调用方可据此改变解析、渲染或导出策略。
        /// </summary>
        public required int Failed { get; init; }

        /// <summary>
        /// 获取或设置根Param原始字节内容数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public required IReadOnlyDictionary<string, int> RootParamRawCounts { get; init; }

        /// <summary>
        /// 获取或设置VTBFTag数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> VtbfTagCounts { get; init; }

        /// <summary>
        /// 获取或设置VTBFTagParam原始字节内容数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public required IReadOnlyDictionary<string, int> VtbfTagParamRawCounts { get; init; }

        /// <summary>
        /// 获取或设置VTBFTagParamLowHigh数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> VtbfTagParamLowHighCounts { get; init; }

        /// <summary>
        /// 获取或设置VTBFTagProperty数量数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> VtbfTagPropertyCountCounts { get; init; }

        /// <summary>
        /// 获取或设置VTBFTagParamHighProperty数量数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> VtbfTagParamHighPropertyCountCounts { get; init; }

        /// <summary>
        /// 获取或设置VTBFTagTrailingByte数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> VtbfTagTrailingByteCounts { get; init; }

        /// <summary>
        /// 获取或设置VTBFKeyParamHighModulo5数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> VtbfKeyParamHighModulo5Counts { get; init; }

        /// <summary>
        /// 获取或设置VTBF 字段目录数量统计，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public required IReadOnlyDictionary<string, int> VtbfFieldDirectoryCounts { get; init; }

        /// <summary>
        /// 获取或设置VTBF 字段目录块数量统计，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public required IReadOnlyDictionary<string, int> VtbfFieldDirectoryBlockCounts { get; init; }

        /// <summary>
        /// 获取或设置VTBF 字段数量值数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> VtbfFieldCountValueCounts { get; init; }

        /// <summary>
        /// 获取或设置VTBF 字段Stride值数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public required IReadOnlyDictionary<string, int> VtbfFieldStrideValueCounts { get; init; }

        /// <summary>
        /// 获取或设置SharedPackedStateOwner数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> SharedPackedStateOwnerCounts { get; init; }

        /// <summary>
        /// 获取或设置SharedPackedStateOwner原始字节内容数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public required IReadOnlyDictionary<string, int> SharedPackedStateOwnerRawCounts { get; init; }

        /// <summary>
        /// 获取或设置SharedPackedStateOwnerBit数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> SharedPackedStateOwnerBitCounts { get; init; }

        /// <summary>
        /// 获取或设置SharedPackedStateOwnerLowNibble数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> SharedPackedStateOwnerLowNibbleCounts { get; init; }

        /// <summary>
        /// 获取或设置SharedPackedStateOwnerMaskF0数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> SharedPackedStateOwnerMaskF0Counts { get; init; }

        /// <summary>
        /// 获取或设置SharedPackedStateOwnerMaskF00数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> SharedPackedStateOwnerMaskF00Counts { get; init; }

        /// <summary>
        /// 获取或设置SharedPackedStateOwnerUpperMask数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> SharedPackedStateOwnerUpperMaskCounts { get; init; }

        /// <summary>
        /// 获取或设置CatrField03数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CatrField03Counts { get; init; }

        /// <summary>
        /// 获取或设置CatrField0D数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CatrField0DCounts { get; init; }

        /// <summary>
        /// 获取或设置CatrField0E数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CatrField0ECounts { get; init; }

        /// <summary>
        /// 获取或设置CatrField0F类型数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CatrField0FTypeCounts { get; init; }

        /// <summary>
        /// 获取或设置CatrField0F诊断预览文本数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CatrField0FPreviewCounts { get; init; }

        /// <summary>
        /// 获取或设置Catr字段Sequence数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CatrFieldSequenceCounts { get; init; }

        /// <summary>
        /// 获取或设置Catr字段Set数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CatrFieldSetCounts { get; init; }

        /// <summary>
        /// 获取或设置ProjectField00数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> ProjectField00Counts { get; init; }

        /// <summary>
        /// 获取或设置ProjectField01数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> ProjectField01Counts { get; init; }

        /// <summary>
        /// 获取或设置ProjectField05数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> ProjectField05Counts { get; init; }

        /// <summary>
        /// 获取或设置ProjectField55数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> ProjectField55Counts { get; init; }

        /// <summary>
        /// 获取或设置ProjectField56数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> ProjectField56Counts { get; init; }

        /// <summary>
        /// 获取或设置ProjectField56轨道上次使用的Relation数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required IReadOnlyDictionary<string, int> ProjectField56TrackLastRelationCounts { get; init; }

        /// <summary>
        /// 获取或设置ProjectField56Key最大值Relation数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> ProjectField56KeyMaxRelationCounts { get; init; }

        /// <summary>
        /// 获取或设置ProjectField56DeltaTo轨道上次使用的数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required IReadOnlyDictionary<string, int> ProjectField56DeltaToTrackLastCounts { get; init; }

        /// <summary>
        /// 获取或设置ProjectField56DeltaToKey最大值数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> ProjectField56DeltaToKeyMaxCounts { get; init; }

        /// <summary>
        /// 获取或设置Project字段Sequence数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> ProjectFieldSequenceCounts { get; init; }

        /// <summary>
        /// 获取或设置Project字段Set数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> ProjectFieldSetCounts { get; init; }

        /// <summary>
        /// 获取或设置Scn名称数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> ScnNameCounts { get; init; }

        /// <summary>
        /// 获取或设置ScnField04原始字节内容Hex数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public required IReadOnlyDictionary<string, int> ScnField04RawHexCounts { get; init; }

        /// <summary>
        /// 获取或设置ScnField10数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> ScnField10Counts { get; init; }

        /// <summary>
        /// 获取或设置ScnField11数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> ScnField11Counts { get; init; }

        /// <summary>
        /// 获取或设置ScnField40数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> ScnField40Counts { get; init; }

        /// <summary>
        /// 获取或设置ScnField41数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> ScnField41Counts { get; init; }

        /// <summary>
        /// 获取或设置ScnField10Field11数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> ScnField10Field11Counts { get; init; }

        /// <summary>
        /// 获取或设置ScnField40Field41数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> ScnField40Field41Counts { get; init; }

        /// <summary>
        /// 获取或设置ScnParamLowLayer数量Delta数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> ScnParamLowLayerCountDeltaCounts { get; init; }

        /// <summary>
        /// 获取或设置ScnParamLowField10Delta数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> ScnParamLowField10DeltaCounts { get; init; }

        /// <summary>
        /// 获取或设置ScnField10Layer数量Delta数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> ScnField10LayerCountDeltaCounts { get; init; }

        /// <summary>
        /// 获取或设置Scn字段Sequence数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> ScnFieldSequenceCounts { get; init; }

        /// <summary>
        /// 获取或设置Scn字段Set数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> ScnFieldSetCounts { get; init; }

        /// <summary>
        /// 获取或设置Layer名称数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> LayerNameCounts { get; init; }

        /// <summary>
        /// 获取或设置LayerField20数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> LayerField20Counts { get; init; }

        /// <summary>
        /// 获取或设置LayerField20Bit数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> LayerField20BitCounts { get; init; }

        /// <summary>
        /// 获取或设置LayerField21数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> LayerField21Counts { get; init; }

        /// <summary>
        /// 获取或设置LayerField21Bit数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> LayerField21BitCounts { get; init; }

        /// <summary>
        /// 获取或设置LayerField22数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> LayerField22Counts { get; init; }

        /// <summary>
        /// 获取或设置LayerField22Bit数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> LayerField22BitCounts { get; init; }

        /// <summary>
        /// 获取或设置LayerField21场景节点数量Delta数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required IReadOnlyDictionary<string, int> LayerField21SceneNodeCountDeltaCounts { get; init; }

        /// <summary>
        /// 获取或设置LayerParamLowField22Delta数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> LayerParamLowField22DeltaCounts { get; init; }

        /// <summary>
        /// 获取或设置Layer字段Sequence数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> LayerFieldSequenceCounts { get; init; }

        /// <summary>
        /// 获取或设置Layer字段Set数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> LayerFieldSetCounts { get; init; }

        /// <summary>
        /// 获取或设置Camera名称数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CameraNameCounts { get; init; }

        /// <summary>
        /// 获取或设置CameraField12Vector数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CameraField12VectorCounts { get; init; }

        /// <summary>
        /// 获取或设置CameraField13Vector数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CameraField13VectorCounts { get; init; }

        /// <summary>
        /// 获取或设置CameraField14数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CameraField14Counts { get; init; }

        /// <summary>
        /// 获取或设置CameraField14Bit数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CameraField14BitCounts { get; init; }

        /// <summary>
        /// 获取或设置CameraField15数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CameraField15Counts { get; init; }

        /// <summary>
        /// 获取或设置CameraField16数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CameraField16Counts { get; init; }

        /// <summary>
        /// 获取或设置Camera字段Sequence数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CameraFieldSequenceCounts { get; init; }

        /// <summary>
        /// 获取或设置Camera字段Set数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CameraFieldSetCounts { get; init; }

        /// <summary>
        /// 获取或设置动画字段Sequence数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required IReadOnlyDictionary<string, int> AnimationFieldSequenceCounts { get; init; }

        /// <summary>
        /// 获取或设置动画字段Set数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required IReadOnlyDictionary<string, int> AnimationFieldSetCounts { get; init; }

        /// <summary>
        /// 获取或设置动画ParamLowMotionDelta数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required IReadOnlyDictionary<string, int> AnimationParamLowMotionDeltaCounts { get; init; }

        /// <summary>
        /// 获取或设置动画Field50MotionDelta数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required IReadOnlyDictionary<string, int> AnimationField50MotionDeltaCounts { get; init; }

        /// <summary>
        /// 获取或设置动画Field50最大值Motion轨道Delta数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required IReadOnlyDictionary<string, int> AnimationField50MaxMotionTrackDeltaCounts { get; init; }

        /// <summary>
        /// 获取或设置动画Field50MotionOr最大值轨道Relation数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required IReadOnlyDictionary<string, int> AnimationField50MotionOrMaxTrackRelationCounts { get; init; }

        /// <summary>
        /// 获取或设置动画ParamLowField50Delta数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required IReadOnlyDictionary<string, int> AnimationParamLowField50DeltaCounts { get; init; }

        /// <summary>
        /// 获取或设置动画Field5F数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required IReadOnlyDictionary<string, int> AnimationField5FCounts { get; init; }

        /// <summary>
        /// 获取或设置动画Field5FMotionPresence数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required IReadOnlyDictionary<string, int> AnimationField5FMotionPresenceCounts { get; init; }

        /// <summary>
        /// 获取或设置动画Field5F动画名称数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> AnimationField5FAnimationNameCounts { get; init; }

        /// <summary>
        /// 获取或设置动画Field5FParamLowMotionDelta数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required IReadOnlyDictionary<string, int> AnimationField5FParamLowMotionDeltaCounts { get; init; }

        /// <summary>
        /// 获取或设置动画Field5FField50MotionDelta数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required IReadOnlyDictionary<string, int> AnimationField5FField50MotionDeltaCounts { get; init; }

        /// <summary>
        /// 获取或设置动画Field5FField50Relation数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required IReadOnlyDictionary<string, int> AnimationField5FField50RelationCounts { get; init; }

        /// <summary>
        /// 获取或设置动画Field5F结束帧Relation数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public required IReadOnlyDictionary<string, int> AnimationField5FEndFrameRelationCounts { get; init; }

        /// <summary>
        /// 获取或设置动画结束帧Relation数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public required IReadOnlyDictionary<string, int> AnimationEndFrameRelationCounts { get; init; }

        /// <summary>
        /// 获取或设置动画结束帧DeltaTo轨道上次使用的数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public required IReadOnlyDictionary<string, int> AnimationEndFrameDeltaToTrackLastCounts { get; init; }

        /// <summary>
        /// 获取或设置动画结束帧DeltaToKey最大值数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public required IReadOnlyDictionary<string, int> AnimationEndFrameDeltaToKeyMaxCounts { get; init; }

        /// <summary>
        /// 获取或设置Motion字段Sequence数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> MotionFieldSequenceCounts { get; init; }

        /// <summary>
        /// 获取或设置Motion字段Set数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> MotionFieldSetCounts { get; init; }

        /// <summary>
        /// 获取或设置MotionParamLow轨道Delta数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required IReadOnlyDictionary<string, int> MotionParamLowTrackDeltaCounts { get; init; }

        /// <summary>
        /// 获取或设置MotionField52轨道Delta数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required IReadOnlyDictionary<string, int> MotionField52TrackDeltaCounts { get; init; }

        /// <summary>
        /// 获取或设置MotionParamLowField52Delta数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> MotionParamLowField52DeltaCounts { get; init; }

        /// <summary>
        /// 获取或设置Motion目标索引范围数量统计，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public required IReadOnlyDictionary<string, int> MotionTargetIndexRangeCounts { get; init; }

        /// <summary>
        /// 获取或设置数据ParamLowMatches图像Casts，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required int DataParamLowMatchesImageCasts { get; init; }

        /// <summary>
        /// 获取或设置数据ParamLowMatchesFollowing图像Casts，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required int DataParamLowMatchesFollowingImageCasts { get; init; }

        /// <summary>
        /// 获取或设置数据ParamLowMatchesFollowingCimgCrfd，用于保留源块参数或字段参数，便于 inspect 输出和后续格式推断。
        /// </summary>
        public required int DataParamLowMatchesFollowingCimgCrfd { get; init; }

        /// <summary>
        /// 获取或设置数据ParamLowMatchesFollowingCimgCnumCrfd，用于保留源块参数或字段参数，便于 inspect 输出和后续格式推断。
        /// </summary>
        public required int DataParamLowMatchesFollowingCimgCnumCrfd { get; init; }

        /// <summary>
        /// 获取或设置数据ParamLowMatchesFollowingCimgCnumCrfdCsli，用于保留源块参数或字段参数，便于 inspect 输出和后续格式推断。
        /// </summary>
        public required int DataParamLowMatchesFollowingCimgCnumCrfdCsli { get; init; }

        /// <summary>
        /// 获取或设置数据VTBF 根块集合With字段明细集合，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required int DataBlocksWithFields { get; init; }

        /// <summary>
        /// 获取或设置数据VTBF 根块集合WithTrailing字节字段类型代码，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public required int DataBlocksWithTrailingBytes { get; init; }

        /// <summary>
        /// 获取或设置NcatMatches节点集合，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required int NcatMatchesNodes { get; init; }

        /// <summary>
        /// 获取或设置NcatNonZeroRecords，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required int NcatNonZeroRecords { get; init; }

        /// <summary>
        /// 获取或设置NcatDetailRecords，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required int NcatDetailRecords { get; init; }

        /// <summary>
        /// 获取或设置NcatRecordsWithCategory，用于描述位置、旋转、缩放或矩阵状态，参与渲染坐标和导出坐标计算。
        /// </summary>
        public required int NcatRecordsWithCategory { get; init; }

        /// <summary>
        /// 获取或设置NcatRecordsWithoutCategory，用于描述位置、旋转、缩放或矩阵状态，参与渲染坐标和导出坐标计算。
        /// </summary>
        public required int NcatRecordsWithoutCategory { get; init; }

        /// <summary>
        /// 获取或设置Ncat类别数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> NcatKindCounts { get; init; }

        /// <summary>
        /// 获取或设置Ncat类型Byte数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> NcatTypeByteCounts { get; init; }

        /// <summary>
        /// 获取或设置NcatCategory数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> NcatCategoryCounts { get; init; }

        /// <summary>
        /// 获取或设置Ncat类别类型Byte数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> NcatKindTypeByteCounts { get; init; }

        /// <summary>
        /// 获取或设置Ncat类别Category数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> NcatKindCategoryCounts { get; init; }

        /// <summary>
        /// 获取或设置Ncat类型ByteCategory数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> NcatTypeByteCategoryCounts { get; init; }

        /// <summary>
        /// 获取或设置Ncat类别ParameterPresence数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> NcatKindParameterPresenceCounts { get; init; }

        /// <summary>
        /// 获取或设置NcatParameter字符串字段类型代码数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> NcatParameterStringCounts { get; init; }

        /// <summary>
        /// 获取或设置NcatParameter字段类型数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> NcatParameterFieldTypeCounts { get; init; }

        /// <summary>
        /// 获取或设置Ncat类别Parameter字段类型数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> NcatKindParameterFieldTypeCounts { get; init; }

        /// <summary>
        /// 获取或设置NcatCategoryParameter字段类型数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> NcatCategoryParameterFieldTypeCounts { get; init; }

        /// <summary>
        /// 获取或设置NcatParameter字段类型诊断预览文本数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> NcatParameterFieldTypePreviewCounts { get; init; }

        /// <summary>
        /// 获取或设置Ncat类别节点Flag数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> NcatKindNodeFlagCounts { get; init; }

        /// <summary>
        /// 获取或设置Ncat类别节点FlagBit数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> NcatKindNodeFlagBitCounts { get; init; }

        /// <summary>
        /// 获取或设置Ncat类别节点Group数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> NcatKindNodeGroupCounts { get; init; }

        /// <summary>
        /// 获取或设置Ncat类别Display数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> NcatKindDisplayCounts { get; init; }

        /// <summary>
        /// 获取或设置Ncat类别Cimg目标数量统计，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public required IReadOnlyDictionary<string, int> NcatKindCimgTargetCounts { get; init; }

        /// <summary>
        /// 获取或设置Ncat类别Animated节点数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> NcatKindAnimatedNodeCounts { get; init; }

        /// <summary>
        /// 获取或设置ScenesWith非致命警告列表，用于把非致命问题返回给调用方，便于诊断解析、渲染或导出过程。
        /// </summary>
        public required int ScenesWithWarnings { get; init; }

        /// <summary>
        /// 获取或设置警告类别数量统计，用于把非致命问题返回给调用方，便于诊断解析、渲染或导出过程。
        /// </summary>
        public required IReadOnlyDictionary<string, int> WarningKindCounts { get; init; }

        /// <summary>
        /// 获取或设置Cimg44Matches，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required int Cimg44Matches { get; init; }

        /// <summary>
        /// 获取或设置Cimg44Mismatches，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required int Cimg44Mismatches { get; init; }

        /// <summary>
        /// 获取或设置Cimg44数量Tuple数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> Cimg44CountTupleCounts { get; init; }

        /// <summary>
        /// 获取或设置Cimg44Primary数量数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> Cimg44PrimaryCountCounts { get; init; }

        /// <summary>
        /// 获取或设置Cimg44Secondary数量数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> Cimg44SecondaryCountCounts { get; init; }

        /// <summary>
        /// 获取或设置Cimg45ActiveGroups，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required int Cimg45ActiveGroups { get; init; }

        /// <summary>
        /// 获取或设置Cimg45In范围Groups，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required int Cimg45InRangeGroups { get; init; }

        /// <summary>
        /// 获取或设置Cimg45OutOf范围Groups，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required int Cimg45OutOfRangeGroups { get; init; }

        /// <summary>
        /// 获取或设置Cimg45EmptyGroupNonZero，用于表示状态开关或检测结果，调用方据此选择显示、解析、导出或诊断分支。
        /// </summary>
        public required int Cimg45EmptyGroupNonZero { get; init; }

        /// <summary>
        /// 获取或设置Cimg45NonZero索引集合，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required int Cimg45NonZeroIndices { get; init; }

        /// <summary>
        /// 获取或设置Cimg45NonZero图像Casts，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required int Cimg45NonZeroImageCasts { get; init; }

        /// <summary>
        /// 获取或设置Cimg45Group索引数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required IReadOnlyDictionary<string, int> Cimg45GroupIndexCounts { get; init; }

        /// <summary>
        /// 获取或设置Cimg45Group数量索引数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required IReadOnlyDictionary<string, int> Cimg45GroupCountIndexCounts { get; init; }

        /// <summary>
        /// 获取或设置Cimg45NonZeroGroup数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> Cimg45NonZeroGroupCounts { get; init; }

        /// <summary>
        /// 获取或设置Cnum数量，用于报告数量或统计值，便于调用方校验结构规模和处理结果。
        /// </summary>
        public required int CnumCount { get; init; }

        /// <summary>
        /// 获取或设置CnumCrop引用数量，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required int CnumCropReferenceCount { get; init; }

        /// <summary>
        /// 获取或设置CnumField44Matches，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required int CnumField44Matches { get; init; }

        /// <summary>
        /// 获取或设置CnumField44Mismatches，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required int CnumField44Mismatches { get; init; }

        /// <summary>
        /// 获取或设置CnumField44Missing，用于统计缺失字段或记录的样本，帮助定位格式差异。
        /// </summary>
        public required int CnumField44Missing { get; init; }

        /// <summary>
        /// 获取或设置CnumField51In范围，用于记录统计或范围信息，便于校验结构规模、覆盖率和异常样本。
        /// </summary>
        public required int CnumField51InRange { get; init; }

        /// <summary>
        /// 获取或设置CnumField51OutOf范围，用于记录统计或范围信息，便于校验结构规模、覆盖率和异常样本。
        /// </summary>
        public required int CnumField51OutOfRange { get; init; }

        /// <summary>
        /// 获取或设置CnumField51Missing，用于统计缺失字段或记录的样本，帮助定位格式差异。
        /// </summary>
        public required int CnumField51Missing { get; init; }

        /// <summary>
        /// 获取或设置CnumField44数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CnumField44Counts { get; init; }

        /// <summary>
        /// 获取或设置CnumZeroMarker字段数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CnumZeroMarkerFieldCounts { get; init; }

        /// <summary>
        /// 获取或设置Cnum字段A1数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CnumFieldA1Counts { get; init; }

        /// <summary>
        /// 获取或设置CnumField48数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CnumField48Counts { get; init; }

        /// <summary>
        /// 获取或设置Cnum字段A0数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CnumFieldA0Counts { get; init; }

        /// <summary>
        /// 获取或设置Cnum字段A1原始字节内容字节长度数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CnumFieldA1RawLengthCounts { get; init; }

        /// <summary>
        /// 获取或设置Cnum字段A1内容字节长度数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CnumFieldA1ContentLengthCounts { get; init; }

        /// <summary>
        /// 获取或设置Cnum字段A1Utf8Status数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CnumFieldA1Utf8StatusCounts { get; init; }

        /// <summary>
        /// 获取或设置Cnum字段A1ShiftJisByteShape数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CnumFieldA1ShiftJisByteShapeCounts { get; init; }

        /// <summary>
        /// 获取或设置Cnum字段A1原始字节内容诊断预览文本数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CnumFieldA1RawPreviewCounts { get; init; }

        /// <summary>
        /// 获取或设置Cnum字段A1Field44数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CnumFieldA1Field44Counts { get; init; }

        /// <summary>
        /// 获取或设置Cnum字段A1Crop引用数量数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CnumFieldA1CropReferenceCountCounts { get; init; }

        /// <summary>
        /// 获取或设置Cnum字段A1ZeroMarker字段数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CnumFieldA1ZeroMarkerFieldCounts { get; init; }

        /// <summary>
        /// 获取或设置Cnum字段A1节点Flag数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CnumFieldA1NodeFlagCounts { get; init; }

        /// <summary>
        /// 获取或设置Cnum字段A1节点Group数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CnumFieldA1NodeGroupCounts { get; init; }

        /// <summary>
        /// 获取或设置Cnum字段A1Display数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CnumFieldA1DisplayCounts { get; init; }

        /// <summary>
        /// 获取或设置Cnum字段A1Cimg目标数量统计，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CnumFieldA1CimgTargetCounts { get; init; }

        /// <summary>
        /// 获取或设置Cnum字段A1Animated目标数量统计，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CnumFieldA1AnimatedTargetCounts { get; init; }

        /// <summary>
        /// 获取或设置Cnum字段Sequence数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CnumFieldSequenceCounts { get; init; }

        /// <summary>
        /// 获取或设置Cnum字段Set数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CnumFieldSetCounts { get; init; }

        /// <summary>
        /// 获取或设置Crfd数量，用于报告数量或统计值，便于调用方校验结构规模和处理结果。
        /// </summary>
        public required int CrfdCount { get; init; }

        /// <summary>
        /// 获取或设置CrfdField51In范围，用于记录统计或范围信息，便于校验结构规模、覆盖率和异常样本。
        /// </summary>
        public required int CrfdField51InRange { get; init; }

        /// <summary>
        /// 获取或设置CrfdField51OutOf范围，用于记录统计或范围信息，便于校验结构规模、覆盖率和异常样本。
        /// </summary>
        public required int CrfdField51OutOfRange { get; init; }

        /// <summary>
        /// 获取或设置CrfdField51Missing，用于统计缺失字段或记录的样本，帮助定位格式差异。
        /// </summary>
        public required int CrfdField51Missing { get; init; }

        /// <summary>
        /// 获取或设置CrfdField90数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CrfdField90Counts { get; init; }

        /// <summary>
        /// 获取或设置CrfdField91数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CrfdField91Counts { get; init; }

        /// <summary>
        /// 获取或设置CrfdField90Field91数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CrfdField90Field91Counts { get; init; }

        /// <summary>
        /// 获取或设置CrfdField90Field91Field92数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CrfdField90Field91Field92Counts { get; init; }

        /// <summary>
        /// 获取或设置Crfd字符串字段类型代码字段Relation数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CrfdStringFieldRelationCounts { get; init; }

        /// <summary>
        /// 获取或设置Crfd字符串字段类型代码字段目标类型数量统计，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CrfdStringFieldTargetTypeCounts { get; init; }

        /// <summary>
        /// 获取或设置CrfdField90Field91Relation数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CrfdField90Field91RelationCounts { get; init; }

        /// <summary>
        /// 获取或设置CrfdField90Field91Equality数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CrfdField90Field91EqualityCounts { get; init; }

        /// <summary>
        /// 获取或设置CrfdField90Field91Field92Relation数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CrfdField90Field91Field92RelationCounts { get; init; }

        /// <summary>
        /// 获取或设置CrfdField92数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CrfdField92Counts { get; init; }

        /// <summary>
        /// 获取或设置CrfdField93数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CrfdField93Counts { get; init; }

        /// <summary>
        /// 获取或设置CrfdField94数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CrfdField94Counts { get; init; }

        /// <summary>
        /// 获取或设置CrfdField94NonZero，用于表示状态开关或检测结果，调用方据此选择显示、解析、导出或诊断分支。
        /// </summary>
        public required int CrfdField94NonZero { get; init; }

        /// <summary>
        /// 获取或设置CrfdField95数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CrfdField95Counts { get; init; }

        /// <summary>
        /// 获取或设置文本数量，用于报告数量或统计值，便于调用方校验结构规模和处理结果。
        /// </summary>
        public required int TextCount { get; init; }

        /// <summary>
        /// 获取或设置文本Field7Alpha 透明度通道值Present，用于保存源字段文本或诊断说明，便于展示、校验和导出报告。
        /// </summary>
        public required int TextField7APresent { get; init; }

        /// <summary>
        /// 获取或设置文本ZeroMarker字段数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TextZeroMarkerFieldCounts { get; init; }

        /// <summary>
        /// 获取或设置文本Field41数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TextField41Counts { get; init; }

        /// <summary>
        /// 获取或设置文本Field78数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TextField78Counts { get; init; }

        /// <summary>
        /// 获取或设置文本Field79数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TextField79Counts { get; init; }

        /// <summary>
        /// 获取或设置文本Field7C数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TextField7CCounts { get; init; }

        /// <summary>
        /// 获取或设置文本Field7Alpha 透明度通道值字符串字段类型代码数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TextField7AStringCounts { get; init; }

        /// <summary>
        /// 获取或设置文本Field7Alpha 透明度通道值原始字节内容字节长度数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TextField7ARawLengthCounts { get; init; }

        /// <summary>
        /// 获取或设置文本Field7Alpha 透明度通道值内容字节长度数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TextField7AContentLengthCounts { get; init; }

        /// <summary>
        /// 获取或设置文本Field7Alpha 透明度通道值Utf8Status数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TextField7AUtf8StatusCounts { get; init; }

        /// <summary>
        /// 获取或设置文本Field7Alpha 透明度通道值ShiftJisByteShape数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TextField7AShiftJisByteShapeCounts { get; init; }

        /// <summary>
        /// 获取或设置文本Field7Alpha 透明度通道值ShiftJisDecodeStatus数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TextField7AShiftJisDecodeStatusCounts { get; init; }

        /// <summary>
        /// 获取或设置文本Field7Alpha 透明度通道值ShiftJis字符串字段类型代码数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TextField7AShiftJisStringCounts { get; init; }

        /// <summary>
        /// 获取或设置文本Field7Alpha 透明度通道值原始字节内容诊断预览文本数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TextField7ARawPreviewCounts { get; init; }

        /// <summary>
        /// 获取或设置文本Field7Alpha 透明度通道值Field41数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TextField7AField41Counts { get; init; }

        /// <summary>
        /// 获取或设置文本Field7Alpha 透明度通道值Field78数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TextField7AField78Counts { get; init; }

        /// <summary>
        /// 获取或设置文本Field7Alpha 透明度通道值Field79数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TextField7AField79Counts { get; init; }

        /// <summary>
        /// 获取或设置文本Field7Alpha 透明度通道值Field7C数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TextField7AField7CCounts { get; init; }

        /// <summary>
        /// 获取或设置文本Field33Vector数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TextField33VectorCounts { get; init; }

        /// <summary>
        /// 获取或设置文本Field33原始字节内容Hex数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TextField33RawHexCounts { get; init; }

        /// <summary>
        /// 获取或设置文本Field7蓝色通道值Packed值集合数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TextField7BPackedValuesCounts { get; init; }

        /// <summary>
        /// 获取或设置文本Field7蓝色通道值原始字节内容Hex数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TextField7BRawHexCounts { get; init; }

        /// <summary>
        /// 获取或设置文本Field78Field79数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TextField78Field79Counts { get; init; }

        /// <summary>
        /// 获取或设置文本ZeroMarkerField7Alpha 透明度通道值数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TextZeroMarkerField7ACounts { get; init; }

        /// <summary>
        /// 获取或设置文本字段Sequence数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TextFieldSequenceCounts { get; init; }

        /// <summary>
        /// 获取或设置文本字段Set数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TextFieldSetCounts { get; init; }

        /// <summary>
        /// 获取或设置SliceCasts，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required int SliceCasts { get; init; }

        /// <summary>
        /// 获取或设置SliceRecords，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required int SliceRecords { get; init; }

        /// <summary>
        /// 获取或设置SliceCrop引用集合，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required int SliceCropReferences { get; init; }

        /// <summary>
        /// 获取或设置SliceField44SlicRecordMatches，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required int SliceField44SlicRecordMatches { get; init; }

        /// <summary>
        /// 获取或设置SliceField44SlicRecordMismatches，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required int SliceField44SlicRecordMismatches { get; init; }

        /// <summary>
        /// 获取或设置SliceField44Crop引用Matches，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required int SliceField44CropReferenceMatches { get; init; }

        /// <summary>
        /// 获取或设置SliceField44Crop引用Mismatches，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required int SliceField44CropReferenceMismatches { get; init; }

        /// <summary>
        /// 获取或设置Slice目标索引In范围，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public required int SliceTargetIndexInRange { get; init; }

        /// <summary>
        /// 获取或设置Slice目标索引OutOf范围，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public required int SliceTargetIndexOutOfRange { get; init; }

        /// <summary>
        /// 获取或设置SliceField83数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> SliceField83Counts { get; init; }

        /// <summary>
        /// 获取或设置SliceCastField40数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> SliceCastField40Counts { get; init; }

        /// <summary>
        /// 获取或设置SliceCastField41数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> SliceCastField41Counts { get; init; }

        /// <summary>
        /// 获取或设置SliceCastField42数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> SliceCastField42Counts { get; init; }

        /// <summary>
        /// 获取或设置SliceCastField43数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> SliceCastField43Counts { get; init; }

        /// <summary>
        /// 获取或设置SliceCastField80数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> SliceCastField80Counts { get; init; }

        /// <summary>
        /// 获取或设置SliceCastField81数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> SliceCastField81Counts { get; init; }

        /// <summary>
        /// 获取或设置SliceCastField82数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> SliceCastField82Counts { get; init; }

        /// <summary>
        /// 获取或设置SliceCastField84数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> SliceCastField84Counts { get; init; }

        /// <summary>
        /// 获取或设置SliceCastField85数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> SliceCastField85Counts { get; init; }

        /// <summary>
        /// 获取或设置SliceCastField86数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> SliceCastField86Counts { get; init; }

        /// <summary>
        /// 获取或设置SliceCastField87数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> SliceCastField87Counts { get; init; }

        /// <summary>
        /// 获取或设置SliceCast目标节点Flag数量统计，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public required IReadOnlyDictionary<string, int> SliceCastTargetNodeFlagCounts { get; init; }

        /// <summary>
        /// 获取或设置SliceCast目标节点Group数量统计，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public required IReadOnlyDictionary<string, int> SliceCastTargetNodeGroupCounts { get; init; }

        /// <summary>
        /// 获取或设置SliceCast目标Display数量统计，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public required IReadOnlyDictionary<string, int> SliceCastTargetDisplayCounts { get; init; }

        /// <summary>
        /// 获取或设置SliceCast目标Cimg目标数量统计，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public required IReadOnlyDictionary<string, int> SliceCastTargetCimgTargetCounts { get; init; }

        /// <summary>
        /// 获取或设置SliceCast字段Sequence数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> SliceCastFieldSequenceCounts { get; init; }

        /// <summary>
        /// 获取或设置SliceCast字段Set数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> SliceCastFieldSetCounts { get; init; }

        /// <summary>
        /// 获取或设置SliceRecordField40数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> SliceRecordField40Counts { get; init; }

        /// <summary>
        /// 获取或设置SliceRecordField41数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> SliceRecordField41Counts { get; init; }

        /// <summary>
        /// 获取或设置SliceRecordField45数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> SliceRecordField45Counts { get; init; }

        /// <summary>
        /// 获取或设置SliceRecordField37颜色数量统计，用于参与颜色、透明度、照明或混合计算。
        /// </summary>
        public required IReadOnlyDictionary<string, int> SliceRecordField37ColorCounts { get; init; }

        /// <summary>
        /// 获取或设置SliceRecordField38颜色数量统计，用于参与颜色、透明度、照明或混合计算。
        /// </summary>
        public required IReadOnlyDictionary<string, int> SliceRecordField38ColorCounts { get; init; }

        /// <summary>
        /// 获取或设置SliceRecordField39颜色数量统计，用于参与颜色、透明度、照明或混合计算。
        /// </summary>
        public required IReadOnlyDictionary<string, int> SliceRecordField39ColorCounts { get; init; }

        /// <summary>
        /// 获取或设置SliceRecordField39颜色数量数量统计，用于参与颜色、透明度、照明或混合计算。
        /// </summary>
        public required IReadOnlyDictionary<string, int> SliceRecordField39ColorCountCounts { get; init; }

        /// <summary>
        /// 获取或设置SliceRecordField83Field40数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> SliceRecordField83Field40Counts { get; init; }

        /// <summary>
        /// 获取或设置SliceRecordField83Field41数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> SliceRecordField83Field41Counts { get; init; }

        /// <summary>
        /// 获取或设置SliceRecordField83Field45数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> SliceRecordField83Field45Counts { get; init; }

        /// <summary>
        /// 获取或设置SliceRecord字段Sequence数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> SliceRecordFieldSequenceCounts { get; init; }

        /// <summary>
        /// 获取或设置SliceRecord字段Set数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> SliceRecordFieldSetCounts { get; init; }

        /// <summary>
        /// 获取或设置SliceRecordShape数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> SliceRecordShapeCounts { get; init; }

        /// <summary>
        /// 获取或设置轨道Key数量Mismatches，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required int TrackKeyCountMismatches { get; init; }

        /// <summary>
        /// 获取或设置KeyTangentPresent，用于描述动画时间轴、关键帧值或插值方式，影响采样、渲染和导出曲线。
        /// </summary>
        public required int KeyTangentPresent { get; init; }

        /// <summary>
        /// 获取或设置KeyTangentNonZero，用于描述动画时间轴、关键帧值或插值方式，影响采样、渲染和导出曲线。
        /// </summary>
        public required int KeyTangentNonZero { get; init; }

        /// <summary>
        /// 获取或设置KeyTangentMismatch，用于描述动画时间轴、关键帧值或插值方式，影响采样、渲染和导出曲线。
        /// </summary>
        public required int KeyTangentMismatch { get; init; }

        /// <summary>
        /// 获取或设置KeyTangentNonZeroScenes，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required int KeyTangentNonZeroScenes { get; init; }

        /// <summary>
        /// 获取或设置KeyTangentMismatchScenes，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required int KeyTangentMismatchScenes { get; init; }

        /// <summary>
        /// 获取或设置Unknown类型代码数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> UnknownTypeCodeCounts { get; init; }

        /// <summary>
        /// 获取或设置节点Flag数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required IReadOnlyDictionary<string, int> NodeFlagCounts { get; init; }

        /// <summary>
        /// 获取或设置节点FlagBit数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required IReadOnlyDictionary<string, int> NodeFlagBitCounts { get; init; }

        /// <summary>
        /// 获取或设置节点FlagBitDisplayFalse节点数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required IReadOnlyDictionary<string, int> NodeFlagBitDisplayFalseNodeCounts { get; init; }

        /// <summary>
        /// 获取或设置节点FlagBitCimg目标节点数量统计，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public required IReadOnlyDictionary<string, int> NodeFlagBitCimgTargetNodeCounts { get; init; }

        /// <summary>
        /// 获取或设置节点FlagBitAnimated节点数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required IReadOnlyDictionary<string, int> NodeFlagBitAnimatedNodeCounts { get; init; }

        /// <summary>
        /// 获取或设置节点FlagBit数据节点数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required IReadOnlyDictionary<string, int> NodeFlagBitDataNodeCounts { get; init; }

        /// <summary>
        /// 获取或设置节点FlagBitCategoryRecord节点数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required IReadOnlyDictionary<string, int> NodeFlagBitCategoryRecordNodeCounts { get; init; }

        /// <summary>
        /// 获取或设置节点FlagBitCategoryNonZero节点数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required IReadOnlyDictionary<string, int> NodeFlagBitCategoryNonZeroNodeCounts { get; init; }

        /// <summary>
        /// 获取或设置节点FlagBitExactFlag数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required IReadOnlyDictionary<string, int> NodeFlagBitExactFlagCounts { get; init; }

        /// <summary>
        /// 获取或设置节点FlagBitGroup数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required IReadOnlyDictionary<string, int> NodeFlagBitGroupCounts { get; init; }

        /// <summary>
        /// 获取或设置节点FlagBit图像CastFlagBit数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required IReadOnlyDictionary<string, int> NodeFlagBitImageCastFlagBitCounts { get; init; }

        /// <summary>
        /// 获取或设置节点FlagBit轨道类型数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> NodeFlagBitTrackTypeCounts { get; init; }

        /// <summary>
        /// 获取或设置节点FlagBitPair数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required IReadOnlyDictionary<string, int> NodeFlagBitPairCounts { get; init; }

        /// <summary>
        /// 获取或设置CimgFlag数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CimgFlagCounts { get; init; }

        /// <summary>
        /// 获取或设置CimgFlagBit数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CimgFlagBitCounts { get; init; }

        /// <summary>
        /// 获取或设置CimgFlagBitDisplayFalse数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CimgFlagBitDisplayFalseCounts { get; init; }

        /// <summary>
        /// 获取或设置CimgFlagBitMulti引用数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CimgFlagBitMultiReferenceCounts { get; init; }

        /// <summary>
        /// 获取或设置CimgFlagBitSecondary引用数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CimgFlagBitSecondaryReferenceCounts { get; init; }

        /// <summary>
        /// 获取或设置CimgFlagBitNonZero引用索引数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CimgFlagBitNonZeroReferenceIndexCounts { get; init; }

        /// <summary>
        /// 获取或设置CimgFlagBitMissing节点数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CimgFlagBitMissingNodeCounts { get; init; }

        /// <summary>
        /// 获取或设置CimgFlagBit节点Flag数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CimgFlagBitNodeFlagCounts { get; init; }

        /// <summary>
        /// 获取或设置CimgFlagBitGroup数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CimgFlagBitGroupCounts { get; init; }

        /// <summary>
        /// 获取或设置CimgFlagBitPair数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CimgFlagBitPairCounts { get; init; }

        /// <summary>
        /// 获取或设置纹理Atlas数量，用于报告数量或统计值，便于调用方校验结构规模和处理结果。
        /// </summary>
        public required int TextureAtlasCount { get; init; }

        /// <summary>
        /// 获取或设置纹理AtlasField62数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TextureAtlasField62Counts { get; init; }

        /// <summary>
        /// 获取或设置纹理AtlasField62Bit数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TextureAtlasField62BitCounts { get; init; }

        /// <summary>
        /// 获取或设置纹理AtlasField62Crop数量数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TextureAtlasField62CropCountCounts { get; init; }

        /// <summary>
        /// 获取或设置纹理AtlasField62大小数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TextureAtlasField62SizeCounts { get; init; }

        /// <summary>
        /// 获取或设置Crop类别数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CropKindCounts { get; init; }

        /// <summary>
        /// 获取或设置Cref类别数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CrefKindCounts { get; init; }

        /// <summary>
        /// 获取或设置Crop矩形数量，用于确定渲染区域、裁剪范围、采样质量或输出尺寸。
        /// </summary>
        public required int CropRectCount { get; init; }

        /// <summary>
        /// 获取或设置CropAtlasDeclared数量Matches，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required int CropAtlasDeclaredCountMatches { get; init; }

        /// <summary>
        /// 获取或设置CropAtlasDeclared数量Mismatches，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required int CropAtlasDeclaredCountMismatches { get; init; }

        /// <summary>
        /// 获取或设置Crop矩形InAtlas边界，用于确定渲染区域、裁剪范围、采样质量或输出尺寸。
        /// </summary>
        public required int CropRectInAtlasBounds { get; init; }

        /// <summary>
        /// 获取或设置Crop矩形OutOfAtlas边界，用于确定渲染区域、裁剪范围、采样质量或输出尺寸。
        /// </summary>
        public required int CropRectOutOfAtlasBounds { get; init; }

        /// <summary>
        /// 获取或设置Crop矩形NonPositive大小，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public required int CropRectNonPositiveSize { get; init; }

        /// <summary>
        /// 获取或设置Crop引用数量，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required int CropReferenceCount { get; init; }

        /// <summary>
        /// 获取或设置Crop引用类别数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CropReferenceKindCounts { get; init; }

        /// <summary>
        /// 获取或设置Crop引用Owner数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CropReferenceOwnerCounts { get; init; }

        /// <summary>
        /// 获取或设置Crop引用Owner类别数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CropReferenceOwnerKindCounts { get; init; }

        /// <summary>
        /// 获取或设置Crop引用纹理List索引数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CropReferenceTextureListIndexCounts { get; init; }

        /// <summary>
        /// 获取或设置Crop引用纹理索引范围数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CropReferenceTextureIndexRangeCounts { get; init; }

        /// <summary>
        /// 获取或设置Crop引用Crop索引范围数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CropReferenceCropIndexRangeCounts { get; init; }

        /// <summary>
        /// 获取或设置Crop矩形OutOfAtlas边界Reason数量统计，用于确定渲染区域、裁剪范围、采样质量或输出尺寸。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CropRectOutOfAtlasBoundsReasonCounts { get; init; }

        /// <summary>
        /// 获取或设置Crop引用OutOf范围Owner数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required IReadOnlyDictionary<string, int> CropReferenceOutOfRangeOwnerCounts { get; init; }

        /// <summary>
        /// 获取或设置轨道Flag数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TrackFlagCounts { get; init; }

        /// <summary>
        /// 获取或设置轨道FlagBase数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TrackFlagBaseCounts { get; init; }

        /// <summary>
        /// 获取或设置轨道FlagExtra数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TrackFlagExtraCounts { get; init; }

        /// <summary>
        /// 获取或设置轨道FlagExtra场景数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TrackFlagExtraSceneCounts { get; init; }

        /// <summary>
        /// 获取或设置轨道FlagExtraBase数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TrackFlagExtraBaseCounts { get; init; }

        /// <summary>
        /// 获取或设置轨道FlagExtra动画数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TrackFlagExtraAnimationCounts { get; init; }

        /// <summary>
        /// 获取或设置轨道FlagExtra轨道类型数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TrackFlagExtraTrackTypeCounts { get; init; }

        /// <summary>
        /// 获取或设置轨道FlagExtraKey值类型数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TrackFlagExtraKeyValueTypeCounts { get; init; }

        /// <summary>
        /// 获取或设置轨道FlagExtra节点Flag数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TrackFlagExtraNodeFlagCounts { get; init; }

        /// <summary>
        /// 获取或设置轨道FlagExtra节点FlagBit数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TrackFlagExtraNodeFlagBitCounts { get; init; }

        /// <summary>
        /// 获取或设置轨道FlagExtraGroup数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TrackFlagExtraGroupCounts { get; init; }

        /// <summary>
        /// 获取或设置轨道FlagExtraCimg目标数量统计，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TrackFlagExtraCimgTargetCounts { get; init; }

        /// <summary>
        /// 获取或设置轨道FlagExtraInitialDisplay数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TrackFlagExtraInitialDisplayCounts { get; init; }

        /// <summary>
        /// 获取或设置轨道FlagExtraCimgFlag数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TrackFlagExtraCimgFlagCounts { get; init; }

        /// <summary>
        /// 获取或设置轨道FlagExtraCimgFlagBit数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TrackFlagExtraCimgFlagBitCounts { get; init; }

        /// <summary>
        /// 获取或设置轨道FlagExtraCimg引用数量数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TrackFlagExtraCimgReferenceCountCounts { get; init; }

        /// <summary>
        /// 获取或设置轨道类型数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TrackTypeCounts { get; init; }

        /// <summary>
        /// 获取或设置Key值类型数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> KeyValueTypeCounts { get; init; }

        /// <summary>
        /// 获取或设置KeyInterpolation数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> KeyInterpolationCounts { get; init; }

        /// <summary>
        /// 获取或设置KeyInterpolation轨道类型数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> KeyInterpolationTrackTypeCounts { get; init; }

        /// <summary>
        /// 获取或设置KeyInterpolationKey值类型数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> KeyInterpolationKeyValueTypeCounts { get; init; }

        /// <summary>
        /// 获取或设置KeyTangentPresentInterpolation数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> KeyTangentPresentInterpolationCounts { get; init; }

        /// <summary>
        /// 获取或设置KeyTangentPresent轨道类型数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> KeyTangentPresentTrackTypeCounts { get; init; }

        /// <summary>
        /// 获取或设置KeyTangentNonZeroInterpolation数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> KeyTangentNonZeroInterpolationCounts { get; init; }

        /// <summary>
        /// 获取或设置KeyTangentNonZero轨道类型数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> KeyTangentNonZeroTrackTypeCounts { get; init; }

        /// <summary>
        /// 获取或设置KeyTangentMismatchInterpolation数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> KeyTangentMismatchInterpolationCounts { get; init; }

        /// <summary>
        /// 获取或设置KeyTangentMismatch轨道类型数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> KeyTangentMismatchTrackTypeCounts { get; init; }

        /// <summary>
        /// 获取或设置KeyTangentMismatch动画数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required IReadOnlyDictionary<string, int> KeyTangentMismatchAnimationCounts { get; init; }

        /// <summary>
        /// 获取或设置KeyTangentMismatch节点Flag数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required IReadOnlyDictionary<string, int> KeyTangentMismatchNodeFlagCounts { get; init; }

        /// <summary>
        /// 获取或设置KeyTangentMismatchGroup数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> KeyTangentMismatchGroupCounts { get; init; }

        /// <summary>
        /// 获取或设置KeyTangentMismatch轨道Extra数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required IReadOnlyDictionary<string, int> KeyTangentMismatchTrackExtraCounts { get; init; }

        /// <summary>
        /// 获取或设置KeyTangentMismatchTangentPair数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> KeyTangentMismatchTangentPairCounts { get; init; }

        /// <summary>
        /// 获取或设置KeyTangentNonZero帧位置数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required IReadOnlyDictionary<string, int> KeyTangentNonZeroFramePositionCounts { get; init; }

        /// <summary>
        /// 获取或设置KeyTangentMismatch帧位置数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required IReadOnlyDictionary<string, int> KeyTangentMismatchFramePositionCounts { get; init; }

        /// <summary>
        /// 获取或设置KeyTangentDeltaSign数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> KeyTangentDeltaSignCounts { get; init; }

        /// <summary>
        /// 获取或设置轨道KeyStorageMatrix数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TrackKeyStorageMatrixCounts { get; init; }

        /// <summary>
        /// 获取或设置轨道字段Sequence数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TrackFieldSequenceCounts { get; init; }

        /// <summary>
        /// 获取或设置Key字段Sequence数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> KeyFieldSequenceCounts { get; init; }

        /// <summary>
        /// 获取或设置轨道帧范围Relation数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TrackFrameRangeRelationCounts { get; init; }

        /// <summary>
        /// 获取或设置轨道Key帧Order数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TrackKeyFrameOrderCounts { get; init; }

        /// <summary>
        /// 获取或设置轨道Key帧Duplicate数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TrackKeyFrameDuplicateCounts { get; init; }

        /// <summary>
        /// 获取或设置轨道First帧Delta数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TrackFirstFrameDeltaCounts { get; init; }

        /// <summary>
        /// 获取或设置轨道上次使用的帧Delta数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TrackLastFrameDeltaCounts { get; init; }

        /// <summary>
        /// 获取或设置变换轨道数量，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required int TransformTrackCount { get; init; }

        /// <summary>
        /// 获取或设置变换轨道Key数量，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required int TransformTrackKeyCount { get; init; }

        /// <summary>
        /// 获取或设置变换轨道集合WithInitialChannel，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required int TransformTracksWithInitialChannel { get; init; }

        /// <summary>
        /// 获取或设置变换轨道集合MissingInitialChannel，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required int TransformTracksMissingInitialChannel { get; init; }

        /// <summary>
        /// 获取或设置变换轨道Initial值Matches，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required int TransformTrackInitialValueMatches { get; init; }

        /// <summary>
        /// 获取或设置变换轨道Initial值Mismatches，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required int TransformTrackInitialValueMismatches { get; init; }

        /// <summary>
        /// 获取或设置变换轨道KeysMissing值，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required int TransformTrackKeysMissingValue { get; init; }

        /// <summary>
        /// 获取或设置变换轨道类型数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TransformTrackTypeCounts { get; init; }

        /// <summary>
        /// 获取或设置变换轨道Key类型数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TransformTrackKeyTypeCounts { get; init; }

        /// <summary>
        /// 获取或设置变换轨道Storage数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TransformTrackStorageCounts { get; init; }

        /// <summary>
        /// 获取或设置变换轨道Key值类别数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TransformTrackKeyValueKindCounts { get; init; }

        /// <summary>
        /// 获取或设置变换轨道InitialMatch类型数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TransformTrackInitialMatchTypeCounts { get; init; }

        /// <summary>
        /// 获取或设置变换轨道值范围数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TransformTrackValueRangeCounts { get; init; }

        /// <summary>
        /// 获取或设置变换Candidate默认Key数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TransformCandidateDefaultKeyCounts { get; init; }

        /// <summary>
        /// 获取或设置PackedAngle轨道数量，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required int PackedAngleTrackCount { get; init; }

        /// <summary>
        /// 获取或设置PackedAngleKey数量，用于报告数量或统计值，便于调用方校验结构规模和处理结果。
        /// </summary>
        public required int PackedAngleKeyCount { get; init; }

        /// <summary>
        /// 获取或设置PackedAngle轨道类型数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> PackedAngleTrackTypeCounts { get; init; }

        /// <summary>
        /// 获取或设置PackedAngleKey轨道类型数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> PackedAngleKeyTrackTypeCounts { get; init; }

        /// <summary>
        /// 获取或设置PackedAngle原始字节内容数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public required IReadOnlyDictionary<string, int> PackedAngleRawCounts { get; init; }

        /// <summary>
        /// 获取或设置PackedAngleDegreeCandidate数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> PackedAngleDegreeCandidateCounts { get; init; }

        /// <summary>
        /// 获取或设置图像Variant轨道数量，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required int ImageVariantTrackCount { get; init; }

        /// <summary>
        /// 获取或设置图像VariantKey数量，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required int ImageVariantKeyCount { get; init; }

        /// <summary>
        /// 获取或设置图像Variant轨道集合WithCimg，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required int ImageVariantTracksWithCimg { get; init; }

        /// <summary>
        /// 获取或设置图像Variant轨道集合MissingCimg，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required int ImageVariantTracksMissingCimg { get; init; }

        /// <summary>
        /// 获取或设置图像Variant轨道范围Matches，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required int ImageVariantTrackRangeMatches { get; init; }

        /// <summary>
        /// 获取或设置图像Variant轨道范围Mismatches，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required int ImageVariantTrackRangeMismatches { get; init; }

        /// <summary>
        /// 获取或设置图像VariantKeysIn范围，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required int ImageVariantKeysInRange { get; init; }

        /// <summary>
        /// 获取或设置图像VariantKeysOutOf范围，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required int ImageVariantKeysOutOfRange { get; init; }

        /// <summary>
        /// 获取或设置图像VariantKeysMissingCimg，用于统计缺少 CIMG 关联的 variant key 样本，帮助定位资源映射缺口。
        /// </summary>
        public required int ImageVariantKeysMissingCimg { get; init; }

        /// <summary>
        /// 获取或设置图像VariantKeysNonInteger，用于统计无法按整数解析的 variant key 样本，帮助定位格式异常。
        /// </summary>
        public required int ImageVariantKeysNonInteger { get; init; }

        /// <summary>
        /// 获取或设置图像VariantKeysMissing值，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required int ImageVariantKeysMissingValue { get; init; }

        /// <summary>
        /// 获取或设置图像Variant引用数量数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required IReadOnlyDictionary<string, int> ImageVariantReferenceCountCounts { get; init; }

        /// <summary>
        /// 获取或设置图像Variant值数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required IReadOnlyDictionary<string, int> ImageVariantValueCounts { get; init; }

        /// <summary>
        /// 获取或设置图像VariantGroup轨道数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required IReadOnlyDictionary<string, int> ImageVariantGroupTrackCounts { get; init; }

        /// <summary>
        /// 获取或设置图像VariantGroupKey数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required IReadOnlyDictionary<string, int> ImageVariantGroupKeyCounts { get; init; }

        /// <summary>
        /// 获取或设置图像VariantGroup轨道集合WithCimg数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required IReadOnlyDictionary<string, int> ImageVariantGroupTracksWithCimgCounts { get; init; }

        /// <summary>
        /// 获取或设置图像VariantGroup轨道集合MissingCimg数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required IReadOnlyDictionary<string, int> ImageVariantGroupTracksMissingCimgCounts { get; init; }

        /// <summary>
        /// 获取或设置图像VariantGroup轨道范围Match数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required IReadOnlyDictionary<string, int> ImageVariantGroupTrackRangeMatchCounts { get; init; }

        /// <summary>
        /// 获取或设置图像VariantGroup轨道范围Mismatch数量统计，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required IReadOnlyDictionary<string, int> ImageVariantGroupTrackRangeMismatchCounts { get; init; }

        /// <summary>
        /// 获取或设置图像VariantGroupKeysIn范围数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required IReadOnlyDictionary<string, int> ImageVariantGroupKeysInRangeCounts { get; init; }

        /// <summary>
        /// 获取或设置图像VariantGroupKeysOutOf范围数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required IReadOnlyDictionary<string, int> ImageVariantGroupKeysOutOfRangeCounts { get; init; }

        /// <summary>
        /// 获取或设置图像VariantGroupKeysMissingCimg数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required IReadOnlyDictionary<string, int> ImageVariantGroupKeysMissingCimgCounts { get; init; }

        /// <summary>
        /// 获取或设置图像VariantGroupKeysNonInteger数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required IReadOnlyDictionary<string, int> ImageVariantGroupKeysNonIntegerCounts { get; init; }

        /// <summary>
        /// 获取或设置图像VariantGroupKeysMissing值数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required IReadOnlyDictionary<string, int> ImageVariantGroupKeysMissingValueCounts { get; init; }

        /// <summary>
        /// 获取或设置图像VariantGroup引用数量数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required IReadOnlyDictionary<string, int> ImageVariantGroupReferenceCountCounts { get; init; }

        /// <summary>
        /// 获取或设置图像VariantGroup值数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required IReadOnlyDictionary<string, int> ImageVariantGroupValueCounts { get; init; }

        /// <summary>
        /// 获取或设置图像VariantGroupCimg45FirstKeyRelation数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required IReadOnlyDictionary<string, int> ImageVariantGroupCimg45FirstKeyRelationCounts { get; init; }

        /// <summary>
        /// 获取或设置图像VariantGroupCimg45FirstKeyDelta数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required IReadOnlyDictionary<string, int> ImageVariantGroupCimg45FirstKeyDeltaCounts { get; init; }

        /// <summary>
        /// 获取或设置图像VariantGroupCimg45FirstKeyPair数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required IReadOnlyDictionary<string, int> ImageVariantGroupCimg45FirstKeyPairCounts { get; init; }

        /// <summary>
        /// 获取或设置颜色轨道数量，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required int ColorTrackCount { get; init; }

        /// <summary>
        /// 获取或设置颜色轨道Key数量，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required int ColorTrackKeyCount { get; init; }

        /// <summary>
        /// 获取或设置颜色轨道集合WithInitialChannel，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required int ColorTracksWithInitialChannel { get; init; }

        /// <summary>
        /// 获取或设置颜色轨道集合MissingInitialChannel，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required int ColorTracksMissingInitialChannel { get; init; }

        /// <summary>
        /// 获取或设置颜色轨道Initial值Matches，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required int ColorTrackInitialValueMatches { get; init; }

        /// <summary>
        /// 获取或设置颜色轨道Initial值Mismatches，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required int ColorTrackInitialValueMismatches { get; init; }

        /// <summary>
        /// 获取或设置颜色轨道KeysInUnit范围，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required int ColorTrackKeysInUnitRange { get; init; }

        /// <summary>
        /// 获取或设置颜色轨道KeysOutOfUnit范围，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required int ColorTrackKeysOutOfUnitRange { get; init; }

        /// <summary>
        /// 获取或设置颜色轨道KeysMissing值，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required int ColorTrackKeysMissingValue { get; init; }

        /// <summary>
        /// 获取或设置颜色轨道类型数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> ColorTrackTypeCounts { get; init; }

        /// <summary>
        /// 获取或设置颜色轨道Key类型数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> ColorTrackKeyTypeCounts { get; init; }

        /// <summary>
        /// 获取或设置颜色轨道InitialMatch类型数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> ColorTrackInitialMatchTypeCounts { get; init; }

        /// <summary>
        /// 获取或设置透明度不透明度轨道数量，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required int AlphaOpacityTrackCount { get; init; }

        /// <summary>
        /// 获取或设置透明度不透明度Key数量，用于统计透明度轨道解析结果，帮助判断动画不透明度是否可信。
        /// </summary>
        public required int AlphaOpacityKeyCount { get; init; }

        /// <summary>
        /// 获取或设置透明度不透明度轨道集合With材质透明度，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required int AlphaOpacityTracksWithMaterialAlpha { get; init; }

        /// <summary>
        /// 获取或设置透明度不透明度轨道集合Missing材质透明度，用于选择、采样或描述动画时间轴，影响渲染帧和导出剪辑生成。
        /// </summary>
        public required int AlphaOpacityTracksMissingMaterialAlpha { get; init; }

        /// <summary>
        /// 获取或设置透明度不透明度Initial透明度Matches，用于统计透明度轨道解析结果，帮助判断动画不透明度是否可信。
        /// </summary>
        public required int AlphaOpacityInitialAlphaMatches { get; init; }

        /// <summary>
        /// 获取或设置透明度不透明度Initial透明度Mismatches，用于统计透明度轨道解析结果，帮助判断动画不透明度是否可信。
        /// </summary>
        public required int AlphaOpacityInitialAlphaMismatches { get; init; }

        /// <summary>
        /// 获取或设置透明度不透明度CimgTargets，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public required int AlphaOpacityCimgTargets { get; init; }

        /// <summary>
        /// 获取或设置透明度不透明度DisplayFalseTargets，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public required int AlphaOpacityDisplayFalseTargets { get; init; }

        /// <summary>
        /// 获取或设置透明度不透明度KeysInUnit范围，用于统计透明度轨道解析结果，帮助判断动画不透明度是否可信。
        /// </summary>
        public required int AlphaOpacityKeysInUnitRange { get; init; }

        /// <summary>
        /// 获取或设置透明度不透明度KeysOutOfUnit范围，用于统计透明度轨道解析结果，帮助判断动画不透明度是否可信。
        /// </summary>
        public required int AlphaOpacityKeysOutOfUnitRange { get; init; }

        /// <summary>
        /// 获取或设置透明度不透明度KeysMissing值，用于统计透明度轨道解析结果，帮助判断动画不透明度是否可信。
        /// </summary>
        public required int AlphaOpacityKeysMissingValue { get; init; }
    }

    internal sealed class SvoSurveyRow
    {
        /// <summary>
        /// 获取或设置路径，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public required string Path { get; init; }

        /// <summary>
        /// 获取或设置Relative路径，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public required string RelativePath { get; init; }

        /// <summary>
        /// 获取或设置大小，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public required long Size { get; init; }

        /// <summary>
        /// 获取或设置Error，用于记录解析或 survey 过程中的错误信息，便于 CLI 报告失败原因。
        /// </summary>
        public string? Error { get; init; }

        /// <summary>
        /// 获取或设置目录数量，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public int DirectoryCount { get; init; }

        /// <summary>
        /// 获取或设置HeaderUnknownNonZero字节字段类型代码，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public int HeaderUnknownNonZeroBytes { get; init; }

        /// <summary>
        /// 表示HeaderUnknownWordClass数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public IReadOnlyDictionary<string, int> HeaderUnknownWordClassCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示HeaderUnknownNonZero文件内偏移数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public IReadOnlyDictionary<string, int> HeaderUnknownNonZeroOffsetCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示HeaderUnknownWord值数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public IReadOnlyDictionary<string, int> HeaderUnknownWordValueCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示HeaderUnknownWord文件内偏移值数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public IReadOnlyDictionary<string, int> HeaderUnknownWordOffsetValueCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示HeaderUnknownWord文件内偏移Class数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public IReadOnlyDictionary<string, int> HeaderUnknownWordOffsetClassCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示HeaderUnknownWordRelation数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public IReadOnlyDictionary<string, int> HeaderUnknownWordRelationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示HeaderUnknownWord文件内偏移Relation数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public IReadOnlyDictionary<string, int> HeaderUnknownWordOffsetRelationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示HeaderUnknownWord载荷Location数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public IReadOnlyDictionary<string, int> HeaderUnknownWordPayloadLocationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示HeaderUnknownWord文件内偏移载荷Location数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public IReadOnlyDictionary<string, int> HeaderUnknownWordOffsetPayloadLocationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 获取或设置DDS数量，用于报告数量或统计值，便于调用方校验结构规模和处理结果。
        /// </summary>
        public int DdsCount { get; init; }

        /// <summary>
        /// 获取或设置目录ReservedEntriesWithNonZero，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public int DirectoryReservedEntriesWithNonZero { get; init; }

        /// <summary>
        /// 获取或设置目录ReservedNonZero字节字段类型代码，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public int DirectoryReservedNonZeroBytes { get; init; }

        /// <summary>
        /// 获取或设置YabxPresent，用于表示状态开关或检测结果，调用方据此选择显示、解析、导出或诊断分支。
        /// </summary>
        public bool YabxPresent { get; init; }

        /// <summary>
        /// 获取或设置YabxHeader哈希Candidate，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public string? YabxHeaderHashCandidate { get; init; }

        /// <summary>
        /// 获取或设置YabxDeclared载荷字节长度MatchesEntry字节长度，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public bool? YabxDeclaredPayloadLengthMatchesEntryLength { get; init; }

        /// <summary>
        /// 获取或设置Yabx引用Base，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public string? YabxReferenceBase { get; init; }

        /// <summary>
        /// 获取或设置YabxObject数量，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public int? YabxObjectCount { get; init; }

        /// <summary>
        /// 获取或设置YabxExpectedObject数量FromDDS，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public int? YabxExpectedObjectCountFromDds { get; init; }

        /// <summary>
        /// 获取或设置YabxObject数量MatchesDDSSkeleton，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public bool? YabxObjectCountMatchesDdsSkeleton { get; init; }

        /// <summary>
        /// 获取或设置YabxObject类型OrderMatchesDDSSkeleton，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public bool? YabxObjectTypeOrderMatchesDdsSkeleton { get; init; }

        /// <summary>
        /// 获取或设置YabxUnparsed字节字段类型代码，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public int? YabxUnparsedBytes { get; init; }

        /// <summary>
        /// 表示YabxObject类型数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> YabxObjectTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示YabxDescriptor原始字节内容数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public IReadOnlyDictionary<string, int> YabxDescriptorRawCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示YabxDescriptorFlags数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> YabxDescriptorFlagsCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示YabxDescriptor值类别数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> YabxDescriptorValueKindCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示YabxDescriptorReserved数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public IReadOnlyDictionary<string, int> YabxDescriptorReservedCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示YabxDescriptorUsage数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public IReadOnlyDictionary<string, int> YabxDescriptorUsageCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示YabxDescriptor原始字节内容Usage数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public IReadOnlyDictionary<string, int> YabxDescriptorRawUsageCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 表示YabxDescriptor原始字节内容Object类别数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public IReadOnlyDictionary<string, int> YabxDescriptorRawObjectKindCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 获取或设置Yabx资源Record数量，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public int YabxResourceRecordCount { get; init; }

        /// <summary>
        /// 获取或设置Yabx资源Record数量MatchesDDS，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public bool? YabxResourceRecordCountMatchesDds { get; init; }

        /// <summary>
        /// 获取或设置Yabx资源纹理图像引用Matches，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public int YabxResourceTextureImageReferenceMatches { get; init; }

        /// <summary>
        /// 获取或设置Yabx资源纹理图像引用Mismatches，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public int YabxResourceTextureImageReferenceMismatches { get; init; }

        /// <summary>
        /// 获取或设置Yabx资源纹理图像引用Missing，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public int YabxResourceTextureImageReferenceMissing { get; init; }

        /// <summary>
        /// 获取或设置Yabx资源数据大小Matches目录，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public int YabxResourceDataSizeMatchesDirectory { get; init; }

        /// <summary>
        /// 获取或设置Yabx资源数据大小Mismatches目录，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public int YabxResourceDataSizeMismatchesDirectory { get; init; }

        /// <summary>
        /// 获取或设置Yabx资源数据大小Missing，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public int YabxResourceDataSizeMissing { get; init; }

        /// <summary>
        /// 获取或设置Yabx资源DimensionsMatchDDS，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public int YabxResourceDimensionsMatchDds { get; init; }

        /// <summary>
        /// 获取或设置Yabx资源DimensionsMismatchDDS，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public int YabxResourceDimensionsMismatchDds { get; init; }

        /// <summary>
        /// 获取或设置Yabx资源DimensionsMissing，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public int YabxResourceDimensionsMissing { get; init; }

        /// <summary>
        /// 表示纹理格式数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public IReadOnlyDictionary<string, int> TextureFormatCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);
    }

    internal sealed class SvoSurveyAggregate
    {
        /// <summary>
        /// 获取或设置Total，用于报告数量或统计值，便于调用方校验结构规模和处理结果。
        /// </summary>
        public required int Total { get; init; }

        /// <summary>
        /// 获取或设置Parsed，用于表示状态开关或检测结果，调用方据此选择显示、解析、导出或诊断分支。
        /// </summary>
        public required int Parsed { get; init; }

        /// <summary>
        /// 获取或设置失败状态，用于控制对应功能开关，调用方可据此改变解析、渲染或导出策略。
        /// </summary>
        public required int Failed { get; init; }

        /// <summary>
        /// 获取或设置目录ReservedEntriesWithNonZero，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public required int DirectoryReservedEntriesWithNonZero { get; init; }

        /// <summary>
        /// 获取或设置目录ReservedNonZero字节字段类型代码，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public required int DirectoryReservedNonZeroBytes { get; init; }

        /// <summary>
        /// 获取或设置YabxWithUnparsed字节字段类型代码，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public required int YabxWithUnparsedBytes { get; init; }

        /// <summary>
        /// 获取或设置YabxUnparsed字节字段类型代码，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public required int YabxUnparsedBytes { get; init; }

        /// <summary>
        /// 获取或设置YabxExpectedObject数量FromDDS，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required int YabxExpectedObjectCountFromDds { get; init; }

        /// <summary>
        /// 获取或设置YabxObject数量DDSSkeletonMatches，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required int YabxObjectCountDdsSkeletonMatches { get; init; }

        /// <summary>
        /// 获取或设置YabxObject数量DDSSkeletonMismatches，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required int YabxObjectCountDdsSkeletonMismatches { get; init; }

        /// <summary>
        /// 获取或设置YabxObject类型OrderDDSSkeletonMatches，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required int YabxObjectTypeOrderDdsSkeletonMatches { get; init; }

        /// <summary>
        /// 获取或设置YabxObject类型OrderDDSSkeletonMismatches，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required int YabxObjectTypeOrderDdsSkeletonMismatches { get; init; }

        /// <summary>
        /// 获取或设置HeaderUnknownWordClass数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public required IReadOnlyDictionary<string, int> HeaderUnknownWordClassCounts { get; init; }

        /// <summary>
        /// 获取或设置HeaderUnknownNonZero文件内偏移数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public required IReadOnlyDictionary<string, int> HeaderUnknownNonZeroOffsetCounts { get; init; }

        /// <summary>
        /// 获取或设置HeaderUnknownWord值数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public required IReadOnlyDictionary<string, int> HeaderUnknownWordValueCounts { get; init; }

        /// <summary>
        /// 获取或设置HeaderUnknownWord文件内偏移值数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public required IReadOnlyDictionary<string, int> HeaderUnknownWordOffsetValueCounts { get; init; }

        /// <summary>
        /// 获取或设置HeaderUnknownWord文件内偏移Class数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public required IReadOnlyDictionary<string, int> HeaderUnknownWordOffsetClassCounts { get; init; }

        /// <summary>
        /// 获取或设置HeaderUnknownWordRelation数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public required IReadOnlyDictionary<string, int> HeaderUnknownWordRelationCounts { get; init; }

        /// <summary>
        /// 获取或设置HeaderUnknownWord文件内偏移Relation数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public required IReadOnlyDictionary<string, int> HeaderUnknownWordOffsetRelationCounts { get; init; }

        /// <summary>
        /// 获取或设置HeaderUnknownWord载荷Location数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public required IReadOnlyDictionary<string, int> HeaderUnknownWordPayloadLocationCounts { get; init; }

        /// <summary>
        /// 获取或设置HeaderUnknownWord文件内偏移载荷Location数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public required IReadOnlyDictionary<string, int> HeaderUnknownWordOffsetPayloadLocationCounts { get; init; }

        /// <summary>
        /// 获取或设置YabxHeader哈希Candidate数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public required IReadOnlyDictionary<string, int> YabxHeaderHashCandidateCounts { get; init; }

        /// <summary>
        /// 获取或设置YabxDeclared载荷字节长度MatchesEntry字节长度，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public required int YabxDeclaredPayloadLengthMatchesEntryLength { get; init; }

        /// <summary>
        /// 获取或设置YabxDeclared载荷字节长度MismatchesEntry字节长度，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public required int YabxDeclaredPayloadLengthMismatchesEntryLength { get; init; }

        /// <summary>
        /// 获取或设置Yabx引用Base数量统计，用于关联场景节点、资源引用、导出实体或原始文件中的对应关系。
        /// </summary>
        public required IReadOnlyDictionary<string, int> YabxReferenceBaseCounts { get; init; }

        /// <summary>
        /// 获取或设置YabxDescriptor原始字节内容数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public required IReadOnlyDictionary<string, int> YabxDescriptorRawCounts { get; init; }

        /// <summary>
        /// 获取或设置YabxDescriptorFlags数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> YabxDescriptorFlagsCounts { get; init; }

        /// <summary>
        /// 获取或设置YabxDescriptor值类别数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> YabxDescriptorValueKindCounts { get; init; }

        /// <summary>
        /// 获取或设置YabxDescriptorReserved数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public required IReadOnlyDictionary<string, int> YabxDescriptorReservedCounts { get; init; }

        /// <summary>
        /// 获取或设置YabxDescriptorUsage数量统计，用于保存一组结构化条目，供调用方遍历、序列化或继续处理。
        /// </summary>
        public required IReadOnlyDictionary<string, int> YabxDescriptorUsageCounts { get; init; }

        /// <summary>
        /// 获取或设置YabxDescriptor原始字节内容Usage数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public required IReadOnlyDictionary<string, int> YabxDescriptorRawUsageCounts { get; init; }

        /// <summary>
        /// 获取或设置YabxDescriptor原始字节内容Object类别数量统计，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public required IReadOnlyDictionary<string, int> YabxDescriptorRawObjectKindCounts { get; init; }

        /// <summary>
        /// 获取或设置纹理格式数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> TextureFormatCounts { get; init; }

        /// <summary>
        /// 获取或设置YabxObject类型数量统计，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
        /// </summary>
        public required IReadOnlyDictionary<string, int> YabxObjectTypeCounts { get; init; }

        /// <summary>
        /// 获取或设置Yabx资源Record数量，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public required int YabxResourceRecordCount { get; init; }

        /// <summary>
        /// 获取或设置Yabx资源Record数量DDSMatches，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public required int YabxResourceRecordCountDdsMatches { get; init; }

        /// <summary>
        /// 获取或设置Yabx资源Record数量DDSMismatches，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public required int YabxResourceRecordCountDdsMismatches { get; init; }

        /// <summary>
        /// 获取或设置Yabx资源纹理图像引用Matches，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public required int YabxResourceTextureImageReferenceMatches { get; init; }

        /// <summary>
        /// 获取或设置Yabx资源纹理图像引用Mismatches，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public required int YabxResourceTextureImageReferenceMismatches { get; init; }

        /// <summary>
        /// 获取或设置Yabx资源纹理图像引用Missing，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public required int YabxResourceTextureImageReferenceMissing { get; init; }

        /// <summary>
        /// 获取或设置Yabx资源数据大小Matches目录，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public required int YabxResourceDataSizeMatchesDirectory { get; init; }

        /// <summary>
        /// 获取或设置Yabx资源数据大小Mismatches目录，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public required int YabxResourceDataSizeMismatchesDirectory { get; init; }

        /// <summary>
        /// 获取或设置Yabx资源数据大小Missing，用于对应原始二进制范围、格式标记或载荷内容，支撑解析校验、定位和 inspect 输出。
        /// </summary>
        public required int YabxResourceDataSizeMissing { get; init; }

        /// <summary>
        /// 获取或设置Yabx资源DimensionsMatchDDS，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public required int YabxResourceDimensionsMatchDds { get; init; }

        /// <summary>
        /// 获取或设置Yabx资源DimensionsMismatchDDS，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public required int YabxResourceDimensionsMismatchDds { get; init; }

        /// <summary>
        /// 获取或设置Yabx资源DimensionsMissing，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
        /// </summary>
        public required int YabxResourceDimensionsMissing { get; init; }
    }

    internal sealed record CimgIndexSurvey(
        int ActiveGroups,
        int InRangeGroups,
        int OutOfRangeGroups,
        int EmptyGroupNonZeroIndices,
        int NonZeroIndices,
        int NonZeroImageCasts,
        IReadOnlyDictionary<string, int> CountTupleCounts,
        IReadOnlyDictionary<string, int> PrimaryCountCounts,
        IReadOnlyDictionary<string, int> SecondaryCountCounts,
        IReadOnlyList<Cimg44SecondaryNonZeroSample> SecondaryNonZeroSamples,
        IReadOnlyDictionary<string, int> GroupIndexCounts,
        IReadOnlyDictionary<string, int> GroupCountIndexCounts,
        IReadOnlyDictionary<string, int> NonZeroGroupCounts,
        IReadOnlyList<Cimg45NonZeroSample> NonZeroSamples);

    internal sealed record Cimg44SecondaryNonZeroSample(
        int ImageCastIndex,
        string ImageCastOffset,
        string? NodeName,
        int? PrimaryDeclaredCount,
        int? SecondaryDeclaredCount,
        int PrimaryActualReferenceCount,
        int SecondaryActualReferenceCount,
        int? PrimaryIndex,
        int? SecondaryIndex,
        IReadOnlyList<string> SecondaryRawHex);

    internal sealed record Cimg45NonZeroSample(
        int ImageCastIndex,
        string ImageCastOffset,
        string? NodeName,
        string GroupName,
        int? DeclaredCount,
        int ActualReferenceCount,
        int GroupIndex,
        string? IndexedRawHex,
        int? TextureListIndex,
        int? TextureIndex,
        int? CropIndex,
        string? AtlasName,
        string? CropPath);

    internal sealed record CropReferenceOwner(
        string OwnerKind,
        int OwnerIndex,
        long OwnerOffset,
        string? OwnerName,
        SbSceneCropReference Reference);

    internal sealed record CropRectBoundsSample(
        int AtlasIndex,
        string AtlasName,
        int AtlasWidth,
        int AtlasHeight,
        int CropIndex,
        string RawHex,
        int Left,
        int Top,
        int Width,
        int Height,
        int Right,
        int Bottom);

    internal sealed record CropReferenceRangeSample(
        string OwnerKind,
        int OwnerIndex,
        string OwnerOffset,
        string? OwnerName,
        int ReferenceIndex,
        string RawHex,
        int Kind,
        int TextureListIndex,
        int TextureIndex,
        int CropIndex,
        string TextureIndexRange,
        string CropIndexRange,
        int? AtlasIndex,
        string? AtlasName,
        int? AtlasCropCount);

    internal sealed record TextureAtlasSurvey(
        int AtlasCount,
        IReadOnlyDictionary<string, int> Field62Counts,
        IReadOnlyDictionary<string, int> Field62BitCounts,
        IReadOnlyDictionary<string, int> Field62CropCountCounts,
        IReadOnlyDictionary<string, int> Field62SizeCounts);

    internal sealed record CropPackedSurvey(
        int CropRectCount,
        int AtlasDeclaredCountMatches,
        int AtlasDeclaredCountMismatches,
        int CropRectInAtlasBounds,
        int CropRectOutOfAtlasBounds,
        int CropRectNonPositiveSize,
        int CropReferenceCount,
        IReadOnlyDictionary<string, int> ReferenceKindCounts,
        IReadOnlyDictionary<string, int> ReferenceOwnerCounts,
        IReadOnlyDictionary<string, int> ReferenceOwnerKindCounts,
        IReadOnlyDictionary<string, int> ReferenceTextureListIndexCounts,
        IReadOnlyDictionary<string, int> ReferenceTextureIndexRangeCounts,
        IReadOnlyDictionary<string, int> ReferenceCropIndexRangeCounts,
        IReadOnlyDictionary<string, int> CropRectOutOfAtlasBoundsReasonCounts,
        IReadOnlyDictionary<string, int> ReferenceOutOfRangeOwnerCounts,
        IReadOnlyList<CropRectBoundsSample> CropRectOutOfAtlasBoundsSamples,
        IReadOnlyList<CropReferenceRangeSample> ReferenceOutOfRangeSamples);

    internal sealed record PackedAngleSurveyKey(TrackInfo Track, KeyframeInfo Key);

    internal sealed record ImageVariantSurvey(
        int TrackCount,
        int KeyCount,
        int TracksWithCimg,
        int TracksMissingCimg,
        int TrackRangeMatches,
        int TrackRangeMismatches,
        int KeysInRange,
        int KeysOutOfRange,
        int KeysMissingCimg,
        int KeysNonInteger,
        int KeysMissingValue,
        IReadOnlyDictionary<string, int> ReferenceCountCounts,
        IReadOnlyDictionary<string, int> ValueCounts,
        IReadOnlyDictionary<string, int> GroupTrackCounts,
        IReadOnlyDictionary<string, int> GroupKeyCounts,
        IReadOnlyDictionary<string, int> GroupTracksWithCimgCounts,
        IReadOnlyDictionary<string, int> GroupTracksMissingCimgCounts,
        IReadOnlyDictionary<string, int> GroupTrackRangeMatchCounts,
        IReadOnlyDictionary<string, int> GroupTrackRangeMismatchCounts,
        IReadOnlyDictionary<string, int> GroupKeysInRangeCounts,
        IReadOnlyDictionary<string, int> GroupKeysOutOfRangeCounts,
        IReadOnlyDictionary<string, int> GroupKeysMissingCimgCounts,
        IReadOnlyDictionary<string, int> GroupKeysNonIntegerCounts,
        IReadOnlyDictionary<string, int> GroupKeysMissingValueCounts,
        IReadOnlyDictionary<string, int> GroupReferenceCountCounts,
        IReadOnlyDictionary<string, int> GroupValueCounts,
        IReadOnlyDictionary<string, int> GroupCimg45FirstKeyRelationCounts,
        IReadOnlyDictionary<string, int> GroupCimg45FirstKeyDeltaCounts,
        IReadOnlyDictionary<string, int> GroupCimg45FirstKeyPairCounts);

    internal sealed record ColorAlphaSurvey(
        int ColorTrackCount,
        int ColorTrackKeyCount,
        int ColorTracksWithInitialChannel,
        int ColorTracksMissingInitialChannel,
        int ColorTrackInitialValueMatches,
        int ColorTrackInitialValueMismatches,
        int ColorTrackKeysInUnitRange,
        int ColorTrackKeysOutOfUnitRange,
        int ColorTrackKeysMissingValue,
        IReadOnlyDictionary<string, int> ColorTrackTypeCounts,
        IReadOnlyDictionary<string, int> ColorTrackKeyTypeCounts,
        IReadOnlyDictionary<string, int> ColorTrackInitialMatchTypeCounts,
        int AlphaOpacityTrackCount,
        int AlphaOpacityKeyCount,
        int AlphaOpacityTracksWithMaterialAlpha,
        int AlphaOpacityTracksMissingMaterialAlpha,
        int AlphaOpacityInitialAlphaMatches,
        int AlphaOpacityInitialAlphaMismatches,
        int AlphaOpacityCimgTargets,
        int AlphaOpacityDisplayFalseTargets,
        int AlphaOpacityKeysInUnitRange,
        int AlphaOpacityKeysOutOfUnitRange,
        int AlphaOpacityKeysMissingValue);

    internal sealed record TransformTrackSurvey(
        int TrackCount,
        int KeyCount,
        int TracksWithInitialChannel,
        int TracksMissingInitialChannel,
        int InitialValueMatches,
        int InitialValueMismatches,
        int KeysMissingValue,
        IReadOnlyDictionary<string, int> TrackTypeCounts,
        IReadOnlyDictionary<string, int> KeyTypeCounts,
        IReadOnlyDictionary<string, int> StorageCounts,
        IReadOnlyDictionary<string, int> KeyValueKindCounts,
        IReadOnlyDictionary<string, int> InitialMatchTypeCounts,
        IReadOnlyDictionary<string, int> ValueRangeCounts,
        IReadOnlyDictionary<string, int> CandidateDefaultKeyCounts);

    internal sealed record SharedPackedStateSurvey(
        IReadOnlyDictionary<string, int> OwnerCounts,
        IReadOnlyDictionary<string, int> OwnerRawCounts,
        IReadOnlyDictionary<string, int> OwnerBitCounts,
        IReadOnlyDictionary<string, int> OwnerLowNibbleCounts,
        IReadOnlyDictionary<string, int> OwnerMaskF0Counts,
        IReadOnlyDictionary<string, int> OwnerMaskF00Counts,
        IReadOnlyDictionary<string, int> OwnerUpperMaskCounts);

    internal sealed record SurveySceneNameIndex(
        IReadOnlySet<string> DirectoryNames,
        IReadOnlySet<string> FileStems,
        IReadOnlySet<string> ScenePrefixes,
        IReadOnlySet<string> SceneSuffixes,
        IReadOnlyDictionary<string, IReadOnlySet<string>> FileStemsByDirectory,
        IReadOnlyDictionary<string, IReadOnlySet<string>> SceneSuffixesByDirectory);

    internal sealed record CrfdReferenceSurvey(
        IReadOnlyDictionary<string, int> StringFieldRelationCounts,
        IReadOnlyDictionary<string, int> StringFieldTargetTypeCounts,
        IReadOnlyDictionary<string, int> Field90Field91RelationCounts,
        IReadOnlyDictionary<string, int> Field90Field91EqualityCounts,
        IReadOnlyDictionary<string, int> Field90Field91Field92RelationCounts);

    internal sealed record CrfdReferenceContext(
        string OwnerDirectoryName,
        string OwnerFileStem,
        string OwnerScenePrefix,
        string? OwnerSceneSuffix,
        string? OwnerSceneName,
        SurveySceneNameIndex SceneNameIndex,
        IReadOnlySet<string> SiblingFileStems,
        IReadOnlySet<string> SiblingSceneSuffixes,
        IReadOnlySet<string> LocalTextureListNames,
        IReadOnlySet<string> LocalTextureNames,
        IReadOnlySet<string> LocalImageCastNames,
        IReadOnlySet<string> LocalCnumNames,
        IReadOnlySet<string> LocalSliceCastNames,
        IReadOnlySet<string> LocalNodeNames,
        IReadOnlySet<string> LocalCrefAtlasNames,
        IReadOnlySet<string> LocalCrefCropPaths);

    internal sealed record CatrSurvey(
        IReadOnlyDictionary<string, int> Field03Counts,
        IReadOnlyDictionary<string, int> Field0DCounts,
        IReadOnlyDictionary<string, int> Field0ECounts,
        IReadOnlyDictionary<string, int> Field0FTypeCounts,
        IReadOnlyDictionary<string, int> Field0FPreviewCounts,
        IReadOnlyDictionary<string, int> FieldSequenceCounts,
        IReadOnlyDictionary<string, int> FieldSetCounts);

    internal sealed record ProjectSurvey(
        IReadOnlyDictionary<string, int> Field00Counts,
        IReadOnlyDictionary<string, int> Field01Counts,
        IReadOnlyDictionary<string, int> Field05Counts,
        IReadOnlyDictionary<string, int> Field55Counts,
        IReadOnlyDictionary<string, int> Field56Counts,
        IReadOnlyDictionary<string, int> Field56TrackLastRelationCounts,
        IReadOnlyDictionary<string, int> Field56KeyMaxRelationCounts,
        IReadOnlyDictionary<string, int> Field56DeltaToTrackLastCounts,
        IReadOnlyDictionary<string, int> Field56DeltaToKeyMaxCounts,
        IReadOnlyDictionary<string, int> FieldSequenceCounts,
        IReadOnlyDictionary<string, int> FieldSetCounts);

    internal sealed record ScnSurvey(
        IReadOnlyDictionary<string, int> NameCounts,
        IReadOnlyDictionary<string, int> Field04RawHexCounts,
        IReadOnlyDictionary<string, int> Field10Counts,
        IReadOnlyDictionary<string, int> Field11Counts,
        IReadOnlyDictionary<string, int> Field40Counts,
        IReadOnlyDictionary<string, int> Field41Counts,
        IReadOnlyDictionary<string, int> Field10Field11Counts,
        IReadOnlyDictionary<string, int> Field40Field41Counts,
        IReadOnlyDictionary<string, int> ParamLowLayerCountDeltaCounts,
        IReadOnlyDictionary<string, int> ParamLowField10DeltaCounts,
        IReadOnlyDictionary<string, int> Field10LayerCountDeltaCounts,
        IReadOnlyDictionary<string, int> FieldSequenceCounts,
        IReadOnlyDictionary<string, int> FieldSetCounts);

    internal sealed record LayerSurvey(
        IReadOnlyDictionary<string, int> NameCounts,
        IReadOnlyDictionary<string, int> Field20Counts,
        IReadOnlyDictionary<string, int> Field20BitCounts,
        IReadOnlyDictionary<string, int> Field21Counts,
        IReadOnlyDictionary<string, int> Field21BitCounts,
        IReadOnlyDictionary<string, int> Field22Counts,
        IReadOnlyDictionary<string, int> Field22BitCounts,
        IReadOnlyDictionary<string, int> Field21SceneNodeCountDeltaCounts,
        IReadOnlyDictionary<string, int> ParamLowField22DeltaCounts,
        IReadOnlyDictionary<string, int> FieldSequenceCounts,
        IReadOnlyDictionary<string, int> FieldSetCounts);

    internal sealed record CameraSurvey(
        IReadOnlyDictionary<string, int> NameCounts,
        IReadOnlyDictionary<string, int> Field12VectorCounts,
        IReadOnlyDictionary<string, int> Field13VectorCounts,
        IReadOnlyDictionary<string, int> Field14Counts,
        IReadOnlyDictionary<string, int> Field14BitCounts,
        IReadOnlyDictionary<string, int> Field15Counts,
        IReadOnlyDictionary<string, int> Field16Counts,
        IReadOnlyDictionary<string, int> FieldSequenceCounts,
        IReadOnlyDictionary<string, int> FieldSetCounts);

    internal sealed record NcatSurvey(
        IReadOnlyDictionary<string, int> KindTypeByteCounts,
        IReadOnlyDictionary<string, int> KindCategoryCounts,
        IReadOnlyDictionary<string, int> TypeByteCategoryCounts,
        IReadOnlyDictionary<string, int> KindParameterPresenceCounts,
        IReadOnlyDictionary<string, int> ParameterStringCounts,
        IReadOnlyDictionary<string, int> ParameterFieldTypeCounts,
        IReadOnlyDictionary<string, int> KindParameterFieldTypeCounts,
        IReadOnlyDictionary<string, int> CategoryParameterFieldTypeCounts,
        IReadOnlyDictionary<string, int> ParameterFieldTypePreviewCounts,
        IReadOnlyDictionary<string, int> KindNodeFlagCounts,
        IReadOnlyDictionary<string, int> KindNodeFlagBitCounts,
        IReadOnlyDictionary<string, int> KindNodeGroupCounts,
        IReadOnlyDictionary<string, int> KindDisplayCounts,
        IReadOnlyDictionary<string, int> KindCimgTargetCounts,
        IReadOnlyDictionary<string, int> KindAnimatedNodeCounts);

    internal sealed record VtbfStructureSurvey(
        IReadOnlyDictionary<string, int> TagCounts,
        IReadOnlyDictionary<string, int> TagParamRawCounts,
        IReadOnlyDictionary<string, int> TagParamLowHighCounts,
        IReadOnlyDictionary<string, int> TagPropertyCountCounts,
        IReadOnlyDictionary<string, int> TagParamHighPropertyCountCounts,
        IReadOnlyDictionary<string, int> TagTrailingByteCounts,
        IReadOnlyDictionary<string, int> KeyParamHighModulo5Counts,
        IReadOnlyDictionary<string, int> FieldDirectoryCounts,
        IReadOnlyDictionary<string, int> FieldDirectoryBlockCounts,
        IReadOnlyDictionary<string, int> FieldCountValueCounts,
        IReadOnlyDictionary<string, int> FieldStrideValueCounts);

    internal sealed record AnimationMotionStructureSurvey(
        IReadOnlyDictionary<string, int> AnimationFieldSequenceCounts,
        IReadOnlyDictionary<string, int> AnimationFieldSetCounts,
        IReadOnlyDictionary<string, int> AnimationParamLowMotionDeltaCounts,
        IReadOnlyDictionary<string, int> AnimationField50MotionDeltaCounts,
        IReadOnlyDictionary<string, int> AnimationField50MaxMotionTrackDeltaCounts,
        IReadOnlyDictionary<string, int> AnimationField50MotionOrMaxTrackRelationCounts,
        IReadOnlyDictionary<string, int> AnimationParamLowField50DeltaCounts,
        IReadOnlyDictionary<string, int> AnimationField5FCounts,
        IReadOnlyDictionary<string, int> AnimationField5FMotionPresenceCounts,
        IReadOnlyDictionary<string, int> AnimationField5FAnimationNameCounts,
        IReadOnlyDictionary<string, int> AnimationField5FParamLowMotionDeltaCounts,
        IReadOnlyDictionary<string, int> AnimationField5FField50MotionDeltaCounts,
        IReadOnlyDictionary<string, int> AnimationField5FField50RelationCounts,
        IReadOnlyDictionary<string, int> AnimationField5FEndFrameRelationCounts,
        IReadOnlyDictionary<string, int> AnimationEndFrameRelationCounts,
        IReadOnlyDictionary<string, int> AnimationEndFrameDeltaToTrackLastCounts,
        IReadOnlyDictionary<string, int> AnimationEndFrameDeltaToKeyMaxCounts,
        IReadOnlyDictionary<string, int> MotionFieldSequenceCounts,
        IReadOnlyDictionary<string, int> MotionFieldSetCounts,
        IReadOnlyDictionary<string, int> MotionParamLowTrackDeltaCounts,
        IReadOnlyDictionary<string, int> MotionField52TrackDeltaCounts,
        IReadOnlyDictionary<string, int> MotionParamLowField52DeltaCounts,
        IReadOnlyDictionary<string, int> MotionTargetIndexRangeCounts);

    internal sealed record CompactTailSurvey(
        IReadOnlyDictionary<string, int> CnumField48Counts,
        IReadOnlyDictionary<string, int> CnumFieldA0Counts,
        IReadOnlyDictionary<string, int> CnumFieldA1Field44Counts,
        IReadOnlyDictionary<string, int> CnumFieldA1CropReferenceCountCounts,
        IReadOnlyDictionary<string, int> CnumFieldA1ZeroMarkerFieldCounts,
        IReadOnlyDictionary<string, int> CnumFieldA1NodeFlagCounts,
        IReadOnlyDictionary<string, int> CnumFieldA1NodeGroupCounts,
        IReadOnlyDictionary<string, int> CnumFieldA1DisplayCounts,
        IReadOnlyDictionary<string, int> CnumFieldA1CimgTargetCounts,
        IReadOnlyDictionary<string, int> CnumFieldA1AnimatedTargetCounts,
        IReadOnlyDictionary<string, int> CnumFieldSequenceCounts,
        IReadOnlyDictionary<string, int> CnumFieldSetCounts,
        IReadOnlyDictionary<string, int> CnumFieldA1RawLengthCounts,
        IReadOnlyDictionary<string, int> CnumFieldA1ContentLengthCounts,
        IReadOnlyDictionary<string, int> CnumFieldA1Utf8StatusCounts,
        IReadOnlyDictionary<string, int> CnumFieldA1ShiftJisByteShapeCounts,
        IReadOnlyDictionary<string, int> CnumFieldA1RawPreviewCounts,
        IReadOnlyDictionary<string, int> TextField7AStringCounts,
        IReadOnlyDictionary<string, int> TextField7ARawLengthCounts,
        IReadOnlyDictionary<string, int> TextField7AContentLengthCounts,
        IReadOnlyDictionary<string, int> TextField7AUtf8StatusCounts,
        IReadOnlyDictionary<string, int> TextField7AShiftJisByteShapeCounts,
        IReadOnlyDictionary<string, int> TextField7AShiftJisDecodeStatusCounts,
        IReadOnlyDictionary<string, int> TextField7AShiftJisStringCounts,
        IReadOnlyDictionary<string, int> TextField7ARawPreviewCounts,
        IReadOnlyDictionary<string, int> TextField7AField41Counts,
        IReadOnlyDictionary<string, int> TextField7AField78Counts,
        IReadOnlyDictionary<string, int> TextField7AField79Counts,
        IReadOnlyDictionary<string, int> TextField7AField7CCounts,
        IReadOnlyDictionary<string, int> TextField33VectorCounts,
        IReadOnlyDictionary<string, int> TextField33RawHexCounts,
        IReadOnlyDictionary<string, int> TextField7BPackedValuesCounts,
        IReadOnlyDictionary<string, int> TextField7BRawHexCounts,
        IReadOnlyDictionary<string, int> TextField78Field79Counts,
        IReadOnlyDictionary<string, int> TextZeroMarkerField7ACounts,
        IReadOnlyDictionary<string, int> TextFieldSequenceCounts,
        IReadOnlyDictionary<string, int> TextFieldSetCounts,
        IReadOnlyDictionary<string, int> SliceCastField40Counts,
        IReadOnlyDictionary<string, int> SliceCastField41Counts,
        IReadOnlyDictionary<string, int> SliceCastField42Counts,
        IReadOnlyDictionary<string, int> SliceCastField43Counts,
        IReadOnlyDictionary<string, int> SliceCastField80Counts,
        IReadOnlyDictionary<string, int> SliceCastField81Counts,
        IReadOnlyDictionary<string, int> SliceCastField82Counts,
        IReadOnlyDictionary<string, int> SliceCastField84Counts,
        IReadOnlyDictionary<string, int> SliceCastField85Counts,
        IReadOnlyDictionary<string, int> SliceCastField86Counts,
        IReadOnlyDictionary<string, int> SliceCastField87Counts,
        IReadOnlyDictionary<string, int> SliceCastTargetNodeFlagCounts,
        IReadOnlyDictionary<string, int> SliceCastTargetNodeGroupCounts,
        IReadOnlyDictionary<string, int> SliceCastTargetDisplayCounts,
        IReadOnlyDictionary<string, int> SliceCastTargetCimgTargetCounts,
        IReadOnlyDictionary<string, int> SliceCastFieldSequenceCounts,
        IReadOnlyDictionary<string, int> SliceCastFieldSetCounts,
        IReadOnlyDictionary<string, int> SliceRecordField40Counts,
        IReadOnlyDictionary<string, int> SliceRecordField41Counts,
        IReadOnlyDictionary<string, int> SliceRecordField45Counts,
        IReadOnlyDictionary<string, int> SliceRecordField37ColorCounts,
        IReadOnlyDictionary<string, int> SliceRecordField38ColorCounts,
        IReadOnlyDictionary<string, int> SliceRecordField39ColorCounts,
        IReadOnlyDictionary<string, int> SliceRecordField39ColorCountCounts,
        IReadOnlyDictionary<string, int> SliceRecordField83Field40Counts,
        IReadOnlyDictionary<string, int> SliceRecordField83Field41Counts,
        IReadOnlyDictionary<string, int> SliceRecordField83Field45Counts,
        IReadOnlyDictionary<string, int> SliceRecordFieldSequenceCounts,
        IReadOnlyDictionary<string, int> SliceRecordFieldSetCounts,
        IReadOnlyDictionary<string, int> SliceRecordShapeCounts);

    internal sealed record NodeFlagBitSurvey(
        IReadOnlyDictionary<string, int> DisplayFalseNodeCounts,
        IReadOnlyDictionary<string, int> CimgTargetNodeCounts,
        IReadOnlyDictionary<string, int> AnimatedNodeCounts,
        IReadOnlyDictionary<string, int> DataNodeCounts,
        IReadOnlyDictionary<string, int> CategoryRecordNodeCounts,
        IReadOnlyDictionary<string, int> CategoryNonZeroNodeCounts,
        IReadOnlyDictionary<string, int> ExactFlagCounts,
        IReadOnlyDictionary<string, int> GroupCounts,
        IReadOnlyDictionary<string, int> ImageCastFlagBitCounts,
        IReadOnlyDictionary<string, int> TrackTypeCounts,
        IReadOnlyDictionary<string, int> PairCounts);

    internal sealed record TrackFlagExtraSurvey(
        IReadOnlyDictionary<string, int> BaseCounts,
        IReadOnlyDictionary<string, int> TrackTypeCounts,
        IReadOnlyDictionary<string, int> KeyValueTypeCounts,
        IReadOnlyDictionary<string, int> NodeFlagCounts,
        IReadOnlyDictionary<string, int> NodeFlagBitCounts,
        IReadOnlyDictionary<string, int> GroupCounts,
        IReadOnlyDictionary<string, int> CimgTargetCounts,
        IReadOnlyDictionary<string, int> InitialDisplayCounts,
        IReadOnlyDictionary<string, int> CimgFlagCounts,
        IReadOnlyDictionary<string, int> CimgFlagBitCounts,
        IReadOnlyDictionary<string, int> CimgReferenceCountCounts,
        IReadOnlyDictionary<string, int> AnimationCounts);

    internal sealed record KeyInterpolationTangentSurvey(
        IReadOnlyDictionary<string, int> InterpolationCounts,
        IReadOnlyDictionary<string, int> InterpolationTrackTypeCounts,
        IReadOnlyDictionary<string, int> InterpolationKeyValueTypeCounts,
        IReadOnlyDictionary<string, int> TangentPresentInterpolationCounts,
        IReadOnlyDictionary<string, int> TangentPresentTrackTypeCounts,
        IReadOnlyDictionary<string, int> TangentNonZeroInterpolationCounts,
        IReadOnlyDictionary<string, int> TangentNonZeroTrackTypeCounts,
        IReadOnlyDictionary<string, int> TangentMismatchInterpolationCounts,
        IReadOnlyDictionary<string, int> TangentMismatchTrackTypeCounts,
        IReadOnlyDictionary<string, int> TangentMismatchAnimationCounts,
        IReadOnlyDictionary<string, int> TangentMismatchNodeFlagCounts,
        IReadOnlyDictionary<string, int> TangentMismatchGroupCounts,
        IReadOnlyDictionary<string, int> TangentMismatchTrackExtraCounts,
        IReadOnlyDictionary<string, int> TangentMismatchTangentPairCounts,
        IReadOnlyDictionary<string, int> TangentNonZeroFramePositionCounts,
        IReadOnlyDictionary<string, int> TangentMismatchFramePositionCounts,
        IReadOnlyDictionary<string, int> TangentDeltaSignCounts);

    internal sealed record TrackKeyStructureSurvey(
        IReadOnlyDictionary<string, int> StorageMatrixCounts,
        IReadOnlyDictionary<string, int> TrackFieldSequenceCounts,
        IReadOnlyDictionary<string, int> KeyFieldSequenceCounts,
        IReadOnlyDictionary<string, int> FrameRangeRelationCounts,
        IReadOnlyDictionary<string, int> KeyFrameOrderCounts,
        IReadOnlyDictionary<string, int> KeyFrameDuplicateCounts,
        IReadOnlyDictionary<string, int> FirstFrameDeltaCounts,
        IReadOnlyDictionary<string, int> LastFrameDeltaCounts);

    internal sealed record CimgFlagBitSurvey(
        IReadOnlyDictionary<string, int> DisplayFalseCounts,
        IReadOnlyDictionary<string, int> MultiReferenceCounts,
        IReadOnlyDictionary<string, int> SecondaryReferenceCounts,
        IReadOnlyDictionary<string, int> NonZeroReferenceIndexCounts,
        IReadOnlyDictionary<string, int> MissingNodeCounts,
        IReadOnlyDictionary<string, int> NodeFlagCounts,
        IReadOnlyDictionary<string, int> GroupCounts,
        IReadOnlyDictionary<string, int> PairCounts);
}
