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
        public required string Input { get; init; }

        public string? Filter { get; init; }

        public required IReadOnlyList<SceneSurveyRow> Scenes { get; init; }

        public required IReadOnlyList<SvoSurveyRow> Svos { get; init; }

        public required SceneSurveyAggregate SceneAggregate { get; init; }

        public required SvoSurveyAggregate SvoAggregate { get; init; }
    }

    internal sealed class SceneSurveyRow
    {
        public required string Path { get; init; }

        public required string RelativePath { get; init; }

        public required long Size { get; init; }

        public string? Error { get; init; }

        public string? RootParamRaw { get; init; }

        public int? RootParamLow { get; init; }

        public int? RootParamHigh { get; init; }

        public int TotalBlocks { get; init; }

        public IReadOnlyDictionary<string, int> VtbfTagCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> VtbfTagParamRawCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> VtbfTagParamLowHighCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> VtbfTagPropertyCountCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> VtbfTagParamHighPropertyCountCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> VtbfTagTrailingByteCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> VtbfKeyParamHighModulo5Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> VtbfFieldDirectoryCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> VtbfFieldDirectoryBlockCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> VtbfFieldCountValueCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> VtbfFieldStrideValueCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> SharedPackedStateOwnerCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> SharedPackedStateOwnerRawCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> SharedPackedStateOwnerBitCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> SharedPackedStateOwnerLowNibbleCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> SharedPackedStateOwnerMaskF0Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> SharedPackedStateOwnerMaskF00Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> SharedPackedStateOwnerUpperMaskCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CatrField03Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CatrField0DCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CatrField0ECounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CatrField0FTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CatrField0FPreviewCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CatrFieldSequenceCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CatrFieldSetCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> ProjectField00Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> ProjectField01Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> ProjectField05Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> ProjectField55Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> ProjectField56Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> ProjectField56TrackLastRelationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> ProjectField56KeyMaxRelationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> ProjectField56DeltaToTrackLastCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> ProjectField56DeltaToKeyMaxCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> ProjectFieldSequenceCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> ProjectFieldSetCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> ScnNameCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> ScnField04RawHexCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> ScnField10Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> ScnField11Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> ScnField40Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> ScnField41Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> ScnField10Field11Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> ScnField40Field41Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> ScnParamLowLayerCountDeltaCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> ScnParamLowField10DeltaCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> ScnField10LayerCountDeltaCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> ScnFieldSequenceCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> ScnFieldSetCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> LayerNameCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> LayerField20Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> LayerField20BitCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> LayerField21Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> LayerField21BitCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> LayerField22Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> LayerField22BitCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> LayerField21SceneNodeCountDeltaCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> LayerParamLowField22DeltaCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> LayerFieldSequenceCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> LayerFieldSetCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CameraNameCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CameraField12VectorCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CameraField13VectorCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CameraField14Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CameraField14BitCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CameraField15Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CameraField16Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CameraFieldSequenceCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CameraFieldSetCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> AnimationFieldSequenceCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> AnimationFieldSetCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> AnimationParamLowMotionDeltaCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> AnimationField50MotionDeltaCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> AnimationField50MaxMotionTrackDeltaCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> AnimationField50MotionOrMaxTrackRelationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> AnimationParamLowField50DeltaCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> AnimationField5FCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> AnimationField5FMotionPresenceCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> AnimationField5FAnimationNameCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> AnimationField5FParamLowMotionDeltaCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> AnimationField5FField50MotionDeltaCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> AnimationField5FField50RelationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> AnimationField5FEndFrameRelationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> AnimationEndFrameRelationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> AnimationEndFrameDeltaToTrackLastCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> AnimationEndFrameDeltaToKeyMaxCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> MotionFieldSequenceCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> MotionFieldSetCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> MotionParamLowTrackDeltaCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> MotionField52TrackDeltaCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> MotionParamLowField52DeltaCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> MotionTargetIndexRangeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> UnknownTypeCodeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyList<string> Warnings { get; init; } = [];

        public int NodeCount { get; init; }

        public int Transform2DCount { get; init; }

        public int ImageCastCount { get; init; }

        public int CnumCount { get; init; }

        public int CnumCropReferenceCount { get; init; }

        public int CnumField44Matches { get; init; }

        public int CnumField44Mismatches { get; init; }

        public int CnumField44Missing { get; init; }

        public int CnumField51InRange { get; init; }

        public int CnumField51OutOfRange { get; init; }

        public int CnumField51Missing { get; init; }

        public IReadOnlyDictionary<string, int> CnumField44Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CnumZeroMarkerFieldCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CnumFieldA1Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CnumField48Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CnumFieldA0Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CnumFieldA1RawLengthCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CnumFieldA1ContentLengthCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CnumFieldA1Utf8StatusCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CnumFieldA1ShiftJisByteShapeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CnumFieldA1RawPreviewCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CnumFieldA1Field44Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CnumFieldA1CropReferenceCountCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CnumFieldA1ZeroMarkerFieldCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CnumFieldA1NodeFlagCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CnumFieldA1NodeGroupCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CnumFieldA1DisplayCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CnumFieldA1CimgTargetCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CnumFieldA1AnimatedTargetCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CnumFieldSequenceCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CnumFieldSetCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public int CrfdCount { get; init; }

        public int CrfdField51InRange { get; init; }

        public int CrfdField51OutOfRange { get; init; }

        public int CrfdField51Missing { get; init; }

        public IReadOnlyDictionary<string, int> CrfdField90Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CrfdField91Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CrfdField90Field91Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CrfdField90Field91Field92Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CrfdStringFieldRelationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CrfdStringFieldTargetTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CrfdField90Field91RelationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CrfdField90Field91EqualityCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CrfdField90Field91Field92RelationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CrfdField92Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CrfdField93Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CrfdField94Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public int CrfdField94NonZero { get; init; }

        public IReadOnlyDictionary<string, int> CrfdField95Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public int TextCount { get; init; }

        public int TextField7APresent { get; init; }

        public IReadOnlyDictionary<string, int> TextZeroMarkerFieldCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TextField41Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TextField78Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TextField79Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TextField7CCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TextField7AStringCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TextField7ARawLengthCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TextField7AContentLengthCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TextField7AUtf8StatusCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TextField7AShiftJisByteShapeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TextField7AShiftJisDecodeStatusCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TextField7AShiftJisStringCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TextField7ARawPreviewCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TextField7AField41Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TextField7AField78Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TextField7AField79Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TextField7AField7CCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TextField33VectorCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TextField33RawHexCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TextField7BPackedValuesCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TextField7BRawHexCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TextField78Field79Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TextZeroMarkerField7ACounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TextFieldSequenceCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TextFieldSetCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public int SliceCastCount { get; init; }

        public int SliceRecordCount { get; init; }

        public int SliceCropReferenceCount { get; init; }

        public int SliceField44SlicRecordMatches { get; init; }

        public int SliceField44SlicRecordMismatches { get; init; }

        public int SliceField44CropReferenceMatches { get; init; }

        public int SliceField44CropReferenceMismatches { get; init; }

        public int SliceTargetIndexInRange { get; init; }

        public int SliceTargetIndexOutOfRange { get; init; }

        public IReadOnlyDictionary<string, int> SliceField83Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> SliceCastField40Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> SliceCastField41Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> SliceCastField42Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> SliceCastField43Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> SliceCastField80Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> SliceCastField81Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> SliceCastField82Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> SliceCastField84Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> SliceCastField85Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> SliceCastField86Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> SliceCastField87Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> SliceCastTargetNodeFlagCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> SliceCastTargetNodeGroupCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> SliceCastTargetDisplayCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> SliceCastTargetCimgTargetCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> SliceCastFieldSequenceCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> SliceCastFieldSetCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> SliceRecordField40Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> SliceRecordField41Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> SliceRecordField45Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> SliceRecordField37ColorCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> SliceRecordField38ColorCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> SliceRecordField39ColorCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> SliceRecordField39ColorCountCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> SliceRecordField83Field40Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> SliceRecordField83Field41Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> SliceRecordField83Field45Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> SliceRecordFieldSequenceCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> SliceRecordFieldSetCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> SliceRecordShapeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public int DataBlockCount { get; init; }

        public IReadOnlyList<int> DataParamLowValues { get; init; } = [];

        public IReadOnlyList<int> DataFollowingImageCastCounts { get; init; } = [];

        public IReadOnlyList<int> DataFollowingCimgCrfdCounts { get; init; } = [];

        public IReadOnlyList<int> DataFollowingCimgCnumCrfdCounts { get; init; } = [];

        public IReadOnlyList<int> DataFollowingCimgCnumCrfdCsliCounts { get; init; } = [];

        public IReadOnlyList<IReadOnlyDictionary<string, int>> DataFollowingTagCounts { get; init; } = [];

        public int DataFields { get; init; }

        public int DataTrailingBytes { get; init; }

        public bool? DataParamLowMatchesImageCasts { get; init; }

        public bool? DataParamLowMatchesFollowingImageCasts { get; init; }

        public bool? DataParamLowMatchesFollowingCimgCrfd { get; init; }

        public bool? DataParamLowMatchesFollowingCimgCnumCrfd { get; init; }

        public bool? DataParamLowMatchesFollowingCimgCnumCrfdCsli { get; init; }

        public int NcatRecordCount { get; init; }

        public int NcatDetailRecordCount { get; init; }

        public int NcatNonZeroCount { get; init; }

        public bool? NcatMatchesNodes { get; init; }

        public int NcatRecordsWithCategory { get; init; }

        public int NcatRecordsWithoutCategory { get; init; }

        public IReadOnlyDictionary<string, int> NcatKindCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> NcatTypeByteCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> NcatCategoryCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> NcatKindTypeByteCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> NcatKindCategoryCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> NcatTypeByteCategoryCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> NcatKindParameterPresenceCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> NcatParameterStringCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> NcatParameterFieldTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> NcatKindParameterFieldTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> NcatCategoryParameterFieldTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> NcatParameterFieldTypePreviewCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> NcatKindNodeFlagCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> NcatKindNodeFlagBitCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> NcatKindNodeGroupCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> NcatKindDisplayCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> NcatKindCimgTargetCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> NcatKindAnimatedNodeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> NodeFlagCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> NodeFlagBitCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> NodeFlagBitDisplayFalseNodeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> NodeFlagBitCimgTargetNodeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> NodeFlagBitAnimatedNodeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> NodeFlagBitDataNodeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> NodeFlagBitCategoryRecordNodeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> NodeFlagBitCategoryNonZeroNodeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> NodeFlagBitExactFlagCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> NodeFlagBitGroupCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> NodeFlagBitImageCastFlagBitCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> NodeFlagBitTrackTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> NodeFlagBitPairCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public int Cimg44Matches { get; init; }

        public int Cimg44Mismatches { get; init; }

        public int Cimg44Unknown { get; init; }

        public IReadOnlyDictionary<string, int> Cimg44CountTupleCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> Cimg44PrimaryCountCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> Cimg44SecondaryCountCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyList<Cimg44SecondaryNonZeroSample> Cimg44SecondaryNonZeroSamples { get; init; } = [];

        public int Cimg45ActiveGroups { get; init; }

        public int Cimg45InRangeGroups { get; init; }

        public int Cimg45OutOfRangeGroups { get; init; }

        public int Cimg45EmptyGroupNonZero { get; init; }

        public int Cimg45NonZeroIndices { get; init; }

        public int Cimg45NonZeroImageCasts { get; init; }

        public IReadOnlyDictionary<string, int> Cimg45GroupIndexCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> Cimg45GroupCountIndexCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> Cimg45NonZeroGroupCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyList<Cimg45NonZeroSample> Cimg45NonZeroSamples { get; init; } = [];

        public IReadOnlyDictionary<string, int> CimgFlagCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CimgFlagBitCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CimgFlagBitDisplayFalseCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CimgFlagBitMultiReferenceCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CimgFlagBitSecondaryReferenceCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CimgFlagBitNonZeroReferenceIndexCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CimgFlagBitMissingNodeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CimgFlagBitNodeFlagCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CimgFlagBitGroupCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CimgFlagBitPairCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public int TextureAtlasCount { get; init; }

        public IReadOnlyDictionary<string, int> TextureAtlasField62Counts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TextureAtlasField62BitCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TextureAtlasField62CropCountCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TextureAtlasField62SizeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CropKindCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CrefKindCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public int CropRectCount { get; init; }

        public int CropAtlasDeclaredCountMatches { get; init; }

        public int CropAtlasDeclaredCountMismatches { get; init; }

        public int CropRectInAtlasBounds { get; init; }

        public int CropRectOutOfAtlasBounds { get; init; }

        public int CropRectNonPositiveSize { get; init; }

        public int CropReferenceCount { get; init; }

        public IReadOnlyDictionary<string, int> CropReferenceKindCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CropReferenceOwnerCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CropReferenceOwnerKindCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CropReferenceTextureListIndexCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CropReferenceTextureIndexRangeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CropReferenceCropIndexRangeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CropRectOutOfAtlasBoundsReasonCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> CropReferenceOutOfRangeOwnerCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyList<CropRectBoundsSample> CropRectOutOfAtlasBoundsSamples { get; init; } = [];

        public IReadOnlyList<CropReferenceRangeSample> CropReferenceOutOfRangeSamples { get; init; } = [];

        public int TrackCount { get; init; }

        public int TrackKeyCountMismatches { get; init; }

        public IReadOnlyDictionary<string, int> TrackFlagCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TrackFlagBaseCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TrackFlagExtraCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TrackFlagExtraSceneCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TrackFlagExtraBaseCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TrackFlagExtraAnimationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TrackFlagExtraTrackTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TrackFlagExtraKeyValueTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TrackFlagExtraNodeFlagCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TrackFlagExtraNodeFlagBitCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TrackFlagExtraGroupCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TrackFlagExtraCimgTargetCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TrackFlagExtraInitialDisplayCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TrackFlagExtraCimgFlagCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TrackFlagExtraCimgFlagBitCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TrackFlagExtraCimgReferenceCountCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TrackTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> KeyValueTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public int KeyTangentPresent { get; init; }

        public int KeyTangentNonZero { get; init; }

        public int KeyTangentMismatch { get; init; }

        public IReadOnlyDictionary<string, int> KeyInterpolationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> KeyInterpolationTrackTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> KeyInterpolationKeyValueTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> KeyTangentPresentInterpolationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> KeyTangentPresentTrackTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> KeyTangentNonZeroInterpolationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> KeyTangentNonZeroTrackTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> KeyTangentMismatchInterpolationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> KeyTangentMismatchTrackTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> KeyTangentMismatchAnimationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> KeyTangentMismatchNodeFlagCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> KeyTangentMismatchGroupCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> KeyTangentMismatchTrackExtraCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> KeyTangentMismatchTangentPairCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> KeyTangentNonZeroFramePositionCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> KeyTangentMismatchFramePositionCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> KeyTangentDeltaSignCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TrackKeyStorageMatrixCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TrackFieldSequenceCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> KeyFieldSequenceCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TrackFrameRangeRelationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TrackKeyFrameOrderCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TrackKeyFrameDuplicateCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TrackFirstFrameDeltaCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TrackLastFrameDeltaCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public int TransformTrackCount { get; init; }

        public int TransformTrackKeyCount { get; init; }

        public int TransformTracksWithInitialChannel { get; init; }

        public int TransformTracksMissingInitialChannel { get; init; }

        public int TransformTrackInitialValueMatches { get; init; }

        public int TransformTrackInitialValueMismatches { get; init; }

        public int TransformTrackKeysMissingValue { get; init; }

        public IReadOnlyDictionary<string, int> TransformTrackTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TransformTrackKeyTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TransformTrackStorageCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TransformTrackKeyValueKindCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TransformTrackInitialMatchTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TransformTrackValueRangeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> TransformCandidateDefaultKeyCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public int PackedAngleTrackCount { get; init; }

        public int PackedAngleKeyCount { get; init; }

        public IReadOnlyDictionary<string, int> PackedAngleTrackTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> PackedAngleKeyTrackTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> PackedAngleRawCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> PackedAngleDegreeCandidateCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public int ImageVariantTrackCount { get; init; }

        public int ImageVariantKeyCount { get; init; }

        public int ImageVariantTracksWithCimg { get; init; }

        public int ImageVariantTracksMissingCimg { get; init; }

        public int ImageVariantTrackRangeMatches { get; init; }

        public int ImageVariantTrackRangeMismatches { get; init; }

        public int ImageVariantKeysInRange { get; init; }

        public int ImageVariantKeysOutOfRange { get; init; }

        public int ImageVariantKeysMissingCimg { get; init; }

        public int ImageVariantKeysNonInteger { get; init; }

        public int ImageVariantKeysMissingValue { get; init; }

        public IReadOnlyDictionary<string, int> ImageVariantReferenceCountCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> ImageVariantValueCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> ImageVariantGroupTrackCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> ImageVariantGroupKeyCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> ImageVariantGroupTracksWithCimgCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> ImageVariantGroupTracksMissingCimgCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> ImageVariantGroupTrackRangeMatchCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> ImageVariantGroupTrackRangeMismatchCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> ImageVariantGroupKeysInRangeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> ImageVariantGroupKeysOutOfRangeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> ImageVariantGroupKeysMissingCimgCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> ImageVariantGroupKeysNonIntegerCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> ImageVariantGroupKeysMissingValueCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> ImageVariantGroupReferenceCountCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> ImageVariantGroupValueCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> ImageVariantGroupCimg45FirstKeyRelationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> ImageVariantGroupCimg45FirstKeyDeltaCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> ImageVariantGroupCimg45FirstKeyPairCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public int ColorTrackCount { get; init; }

        public int ColorTrackKeyCount { get; init; }

        public int ColorTracksWithInitialChannel { get; init; }

        public int ColorTracksMissingInitialChannel { get; init; }

        public int ColorTrackInitialValueMatches { get; init; }

        public int ColorTrackInitialValueMismatches { get; init; }

        public int ColorTrackKeysInUnitRange { get; init; }

        public int ColorTrackKeysOutOfUnitRange { get; init; }

        public int ColorTrackKeysMissingValue { get; init; }

        public IReadOnlyDictionary<string, int> ColorTrackTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> ColorTrackKeyTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> ColorTrackInitialMatchTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public int AlphaOpacityTrackCount { get; init; }

        public int AlphaOpacityKeyCount { get; init; }

        public int AlphaOpacityTracksWithMaterialAlpha { get; init; }

        public int AlphaOpacityTracksMissingMaterialAlpha { get; init; }

        public int AlphaOpacityInitialAlphaMatches { get; init; }

        public int AlphaOpacityInitialAlphaMismatches { get; init; }

        public int AlphaOpacityCimgTargets { get; init; }

        public int AlphaOpacityDisplayFalseTargets { get; init; }

        public int AlphaOpacityKeysInUnitRange { get; init; }

        public int AlphaOpacityKeysOutOfUnitRange { get; init; }

        public int AlphaOpacityKeysMissingValue { get; init; }
    }

    internal sealed class SceneSurveyAggregate
    {
        public required int Total { get; init; }

        public required int Parsed { get; init; }

        public required int Failed { get; init; }

        public required IReadOnlyDictionary<string, int> RootParamRawCounts { get; init; }

        public required IReadOnlyDictionary<string, int> VtbfTagCounts { get; init; }

        public required IReadOnlyDictionary<string, int> VtbfTagParamRawCounts { get; init; }

        public required IReadOnlyDictionary<string, int> VtbfTagParamLowHighCounts { get; init; }

        public required IReadOnlyDictionary<string, int> VtbfTagPropertyCountCounts { get; init; }

        public required IReadOnlyDictionary<string, int> VtbfTagParamHighPropertyCountCounts { get; init; }

        public required IReadOnlyDictionary<string, int> VtbfTagTrailingByteCounts { get; init; }

        public required IReadOnlyDictionary<string, int> VtbfKeyParamHighModulo5Counts { get; init; }

        public required IReadOnlyDictionary<string, int> VtbfFieldDirectoryCounts { get; init; }

        public required IReadOnlyDictionary<string, int> VtbfFieldDirectoryBlockCounts { get; init; }

        public required IReadOnlyDictionary<string, int> VtbfFieldCountValueCounts { get; init; }

        public required IReadOnlyDictionary<string, int> VtbfFieldStrideValueCounts { get; init; }

        public required IReadOnlyDictionary<string, int> SharedPackedStateOwnerCounts { get; init; }

        public required IReadOnlyDictionary<string, int> SharedPackedStateOwnerRawCounts { get; init; }

        public required IReadOnlyDictionary<string, int> SharedPackedStateOwnerBitCounts { get; init; }

        public required IReadOnlyDictionary<string, int> SharedPackedStateOwnerLowNibbleCounts { get; init; }

        public required IReadOnlyDictionary<string, int> SharedPackedStateOwnerMaskF0Counts { get; init; }

        public required IReadOnlyDictionary<string, int> SharedPackedStateOwnerMaskF00Counts { get; init; }

        public required IReadOnlyDictionary<string, int> SharedPackedStateOwnerUpperMaskCounts { get; init; }

        public required IReadOnlyDictionary<string, int> CatrField03Counts { get; init; }

        public required IReadOnlyDictionary<string, int> CatrField0DCounts { get; init; }

        public required IReadOnlyDictionary<string, int> CatrField0ECounts { get; init; }

        public required IReadOnlyDictionary<string, int> CatrField0FTypeCounts { get; init; }

        public required IReadOnlyDictionary<string, int> CatrField0FPreviewCounts { get; init; }

        public required IReadOnlyDictionary<string, int> CatrFieldSequenceCounts { get; init; }

        public required IReadOnlyDictionary<string, int> CatrFieldSetCounts { get; init; }

        public required IReadOnlyDictionary<string, int> ProjectField00Counts { get; init; }

        public required IReadOnlyDictionary<string, int> ProjectField01Counts { get; init; }

        public required IReadOnlyDictionary<string, int> ProjectField05Counts { get; init; }

        public required IReadOnlyDictionary<string, int> ProjectField55Counts { get; init; }

        public required IReadOnlyDictionary<string, int> ProjectField56Counts { get; init; }

        public required IReadOnlyDictionary<string, int> ProjectField56TrackLastRelationCounts { get; init; }

        public required IReadOnlyDictionary<string, int> ProjectField56KeyMaxRelationCounts { get; init; }

        public required IReadOnlyDictionary<string, int> ProjectField56DeltaToTrackLastCounts { get; init; }

        public required IReadOnlyDictionary<string, int> ProjectField56DeltaToKeyMaxCounts { get; init; }

        public required IReadOnlyDictionary<string, int> ProjectFieldSequenceCounts { get; init; }

        public required IReadOnlyDictionary<string, int> ProjectFieldSetCounts { get; init; }

        public required IReadOnlyDictionary<string, int> ScnNameCounts { get; init; }

        public required IReadOnlyDictionary<string, int> ScnField04RawHexCounts { get; init; }

        public required IReadOnlyDictionary<string, int> ScnField10Counts { get; init; }

        public required IReadOnlyDictionary<string, int> ScnField11Counts { get; init; }

        public required IReadOnlyDictionary<string, int> ScnField40Counts { get; init; }

        public required IReadOnlyDictionary<string, int> ScnField41Counts { get; init; }

        public required IReadOnlyDictionary<string, int> ScnField10Field11Counts { get; init; }

        public required IReadOnlyDictionary<string, int> ScnField40Field41Counts { get; init; }

        public required IReadOnlyDictionary<string, int> ScnParamLowLayerCountDeltaCounts { get; init; }

        public required IReadOnlyDictionary<string, int> ScnParamLowField10DeltaCounts { get; init; }

        public required IReadOnlyDictionary<string, int> ScnField10LayerCountDeltaCounts { get; init; }

        public required IReadOnlyDictionary<string, int> ScnFieldSequenceCounts { get; init; }

        public required IReadOnlyDictionary<string, int> ScnFieldSetCounts { get; init; }

        public required IReadOnlyDictionary<string, int> LayerNameCounts { get; init; }

        public required IReadOnlyDictionary<string, int> LayerField20Counts { get; init; }

        public required IReadOnlyDictionary<string, int> LayerField20BitCounts { get; init; }

        public required IReadOnlyDictionary<string, int> LayerField21Counts { get; init; }

        public required IReadOnlyDictionary<string, int> LayerField21BitCounts { get; init; }

        public required IReadOnlyDictionary<string, int> LayerField22Counts { get; init; }

        public required IReadOnlyDictionary<string, int> LayerField22BitCounts { get; init; }

        public required IReadOnlyDictionary<string, int> LayerField21SceneNodeCountDeltaCounts { get; init; }

        public required IReadOnlyDictionary<string, int> LayerParamLowField22DeltaCounts { get; init; }

        public required IReadOnlyDictionary<string, int> LayerFieldSequenceCounts { get; init; }

        public required IReadOnlyDictionary<string, int> LayerFieldSetCounts { get; init; }

        public required IReadOnlyDictionary<string, int> CameraNameCounts { get; init; }

        public required IReadOnlyDictionary<string, int> CameraField12VectorCounts { get; init; }

        public required IReadOnlyDictionary<string, int> CameraField13VectorCounts { get; init; }

        public required IReadOnlyDictionary<string, int> CameraField14Counts { get; init; }

        public required IReadOnlyDictionary<string, int> CameraField14BitCounts { get; init; }

        public required IReadOnlyDictionary<string, int> CameraField15Counts { get; init; }

        public required IReadOnlyDictionary<string, int> CameraField16Counts { get; init; }

        public required IReadOnlyDictionary<string, int> CameraFieldSequenceCounts { get; init; }

        public required IReadOnlyDictionary<string, int> CameraFieldSetCounts { get; init; }

        public required IReadOnlyDictionary<string, int> AnimationFieldSequenceCounts { get; init; }

        public required IReadOnlyDictionary<string, int> AnimationFieldSetCounts { get; init; }

        public required IReadOnlyDictionary<string, int> AnimationParamLowMotionDeltaCounts { get; init; }

        public required IReadOnlyDictionary<string, int> AnimationField50MotionDeltaCounts { get; init; }

        public required IReadOnlyDictionary<string, int> AnimationField50MaxMotionTrackDeltaCounts { get; init; }

        public required IReadOnlyDictionary<string, int> AnimationField50MotionOrMaxTrackRelationCounts { get; init; }

        public required IReadOnlyDictionary<string, int> AnimationParamLowField50DeltaCounts { get; init; }

        public required IReadOnlyDictionary<string, int> AnimationField5FCounts { get; init; }

        public required IReadOnlyDictionary<string, int> AnimationField5FMotionPresenceCounts { get; init; }

        public required IReadOnlyDictionary<string, int> AnimationField5FAnimationNameCounts { get; init; }

        public required IReadOnlyDictionary<string, int> AnimationField5FParamLowMotionDeltaCounts { get; init; }

        public required IReadOnlyDictionary<string, int> AnimationField5FField50MotionDeltaCounts { get; init; }

        public required IReadOnlyDictionary<string, int> AnimationField5FField50RelationCounts { get; init; }

        public required IReadOnlyDictionary<string, int> AnimationField5FEndFrameRelationCounts { get; init; }

        public required IReadOnlyDictionary<string, int> AnimationEndFrameRelationCounts { get; init; }

        public required IReadOnlyDictionary<string, int> AnimationEndFrameDeltaToTrackLastCounts { get; init; }

        public required IReadOnlyDictionary<string, int> AnimationEndFrameDeltaToKeyMaxCounts { get; init; }

        public required IReadOnlyDictionary<string, int> MotionFieldSequenceCounts { get; init; }

        public required IReadOnlyDictionary<string, int> MotionFieldSetCounts { get; init; }

        public required IReadOnlyDictionary<string, int> MotionParamLowTrackDeltaCounts { get; init; }

        public required IReadOnlyDictionary<string, int> MotionField52TrackDeltaCounts { get; init; }

        public required IReadOnlyDictionary<string, int> MotionParamLowField52DeltaCounts { get; init; }

        public required IReadOnlyDictionary<string, int> MotionTargetIndexRangeCounts { get; init; }

        public required int DataParamLowMatchesImageCasts { get; init; }

        public required int DataParamLowMatchesFollowingImageCasts { get; init; }

        public required int DataParamLowMatchesFollowingCimgCrfd { get; init; }

        public required int DataParamLowMatchesFollowingCimgCnumCrfd { get; init; }

        public required int DataParamLowMatchesFollowingCimgCnumCrfdCsli { get; init; }

        public required int DataBlocksWithFields { get; init; }

        public required int DataBlocksWithTrailingBytes { get; init; }

        public required int NcatMatchesNodes { get; init; }

        public required int NcatNonZeroRecords { get; init; }

        public required int NcatDetailRecords { get; init; }

        public required int NcatRecordsWithCategory { get; init; }

        public required int NcatRecordsWithoutCategory { get; init; }

        public required IReadOnlyDictionary<string, int> NcatKindCounts { get; init; }

        public required IReadOnlyDictionary<string, int> NcatTypeByteCounts { get; init; }

        public required IReadOnlyDictionary<string, int> NcatCategoryCounts { get; init; }

        public required IReadOnlyDictionary<string, int> NcatKindTypeByteCounts { get; init; }

        public required IReadOnlyDictionary<string, int> NcatKindCategoryCounts { get; init; }

        public required IReadOnlyDictionary<string, int> NcatTypeByteCategoryCounts { get; init; }

        public required IReadOnlyDictionary<string, int> NcatKindParameterPresenceCounts { get; init; }

        public required IReadOnlyDictionary<string, int> NcatParameterStringCounts { get; init; }

        public required IReadOnlyDictionary<string, int> NcatParameterFieldTypeCounts { get; init; }

        public required IReadOnlyDictionary<string, int> NcatKindParameterFieldTypeCounts { get; init; }

        public required IReadOnlyDictionary<string, int> NcatCategoryParameterFieldTypeCounts { get; init; }

        public required IReadOnlyDictionary<string, int> NcatParameterFieldTypePreviewCounts { get; init; }

        public required IReadOnlyDictionary<string, int> NcatKindNodeFlagCounts { get; init; }

        public required IReadOnlyDictionary<string, int> NcatKindNodeFlagBitCounts { get; init; }

        public required IReadOnlyDictionary<string, int> NcatKindNodeGroupCounts { get; init; }

        public required IReadOnlyDictionary<string, int> NcatKindDisplayCounts { get; init; }

        public required IReadOnlyDictionary<string, int> NcatKindCimgTargetCounts { get; init; }

        public required IReadOnlyDictionary<string, int> NcatKindAnimatedNodeCounts { get; init; }

        public required int ScenesWithWarnings { get; init; }

        public required IReadOnlyDictionary<string, int> WarningKindCounts { get; init; }

        public required int Cimg44Matches { get; init; }

        public required int Cimg44Mismatches { get; init; }

        public required IReadOnlyDictionary<string, int> Cimg44CountTupleCounts { get; init; }

        public required IReadOnlyDictionary<string, int> Cimg44PrimaryCountCounts { get; init; }

        public required IReadOnlyDictionary<string, int> Cimg44SecondaryCountCounts { get; init; }

        public required int Cimg45ActiveGroups { get; init; }

        public required int Cimg45InRangeGroups { get; init; }

        public required int Cimg45OutOfRangeGroups { get; init; }

        public required int Cimg45EmptyGroupNonZero { get; init; }

        public required int Cimg45NonZeroIndices { get; init; }

        public required int Cimg45NonZeroImageCasts { get; init; }

        public required IReadOnlyDictionary<string, int> Cimg45GroupIndexCounts { get; init; }

        public required IReadOnlyDictionary<string, int> Cimg45GroupCountIndexCounts { get; init; }

        public required IReadOnlyDictionary<string, int> Cimg45NonZeroGroupCounts { get; init; }

        public required int CnumCount { get; init; }

        public required int CnumCropReferenceCount { get; init; }

        public required int CnumField44Matches { get; init; }

        public required int CnumField44Mismatches { get; init; }

        public required int CnumField44Missing { get; init; }

        public required int CnumField51InRange { get; init; }

        public required int CnumField51OutOfRange { get; init; }

        public required int CnumField51Missing { get; init; }

        public required IReadOnlyDictionary<string, int> CnumField44Counts { get; init; }

        public required IReadOnlyDictionary<string, int> CnumZeroMarkerFieldCounts { get; init; }

        public required IReadOnlyDictionary<string, int> CnumFieldA1Counts { get; init; }

        public required IReadOnlyDictionary<string, int> CnumField48Counts { get; init; }

        public required IReadOnlyDictionary<string, int> CnumFieldA0Counts { get; init; }

        public required IReadOnlyDictionary<string, int> CnumFieldA1RawLengthCounts { get; init; }

        public required IReadOnlyDictionary<string, int> CnumFieldA1ContentLengthCounts { get; init; }

        public required IReadOnlyDictionary<string, int> CnumFieldA1Utf8StatusCounts { get; init; }

        public required IReadOnlyDictionary<string, int> CnumFieldA1ShiftJisByteShapeCounts { get; init; }

        public required IReadOnlyDictionary<string, int> CnumFieldA1RawPreviewCounts { get; init; }

        public required IReadOnlyDictionary<string, int> CnumFieldA1Field44Counts { get; init; }

        public required IReadOnlyDictionary<string, int> CnumFieldA1CropReferenceCountCounts { get; init; }

        public required IReadOnlyDictionary<string, int> CnumFieldA1ZeroMarkerFieldCounts { get; init; }

        public required IReadOnlyDictionary<string, int> CnumFieldA1NodeFlagCounts { get; init; }

        public required IReadOnlyDictionary<string, int> CnumFieldA1NodeGroupCounts { get; init; }

        public required IReadOnlyDictionary<string, int> CnumFieldA1DisplayCounts { get; init; }

        public required IReadOnlyDictionary<string, int> CnumFieldA1CimgTargetCounts { get; init; }

        public required IReadOnlyDictionary<string, int> CnumFieldA1AnimatedTargetCounts { get; init; }

        public required IReadOnlyDictionary<string, int> CnumFieldSequenceCounts { get; init; }

        public required IReadOnlyDictionary<string, int> CnumFieldSetCounts { get; init; }

        public required int CrfdCount { get; init; }

        public required int CrfdField51InRange { get; init; }

        public required int CrfdField51OutOfRange { get; init; }

        public required int CrfdField51Missing { get; init; }

        public required IReadOnlyDictionary<string, int> CrfdField90Counts { get; init; }

        public required IReadOnlyDictionary<string, int> CrfdField91Counts { get; init; }

        public required IReadOnlyDictionary<string, int> CrfdField90Field91Counts { get; init; }

        public required IReadOnlyDictionary<string, int> CrfdField90Field91Field92Counts { get; init; }

        public required IReadOnlyDictionary<string, int> CrfdStringFieldRelationCounts { get; init; }

        public required IReadOnlyDictionary<string, int> CrfdStringFieldTargetTypeCounts { get; init; }

        public required IReadOnlyDictionary<string, int> CrfdField90Field91RelationCounts { get; init; }

        public required IReadOnlyDictionary<string, int> CrfdField90Field91EqualityCounts { get; init; }

        public required IReadOnlyDictionary<string, int> CrfdField90Field91Field92RelationCounts { get; init; }

        public required IReadOnlyDictionary<string, int> CrfdField92Counts { get; init; }

        public required IReadOnlyDictionary<string, int> CrfdField93Counts { get; init; }

        public required IReadOnlyDictionary<string, int> CrfdField94Counts { get; init; }

        public required int CrfdField94NonZero { get; init; }

        public required IReadOnlyDictionary<string, int> CrfdField95Counts { get; init; }

        public required int TextCount { get; init; }

        public required int TextField7APresent { get; init; }

        public required IReadOnlyDictionary<string, int> TextZeroMarkerFieldCounts { get; init; }

        public required IReadOnlyDictionary<string, int> TextField41Counts { get; init; }

        public required IReadOnlyDictionary<string, int> TextField78Counts { get; init; }

        public required IReadOnlyDictionary<string, int> TextField79Counts { get; init; }

        public required IReadOnlyDictionary<string, int> TextField7CCounts { get; init; }

        public required IReadOnlyDictionary<string, int> TextField7AStringCounts { get; init; }

        public required IReadOnlyDictionary<string, int> TextField7ARawLengthCounts { get; init; }

        public required IReadOnlyDictionary<string, int> TextField7AContentLengthCounts { get; init; }

        public required IReadOnlyDictionary<string, int> TextField7AUtf8StatusCounts { get; init; }

        public required IReadOnlyDictionary<string, int> TextField7AShiftJisByteShapeCounts { get; init; }

        public required IReadOnlyDictionary<string, int> TextField7AShiftJisDecodeStatusCounts { get; init; }

        public required IReadOnlyDictionary<string, int> TextField7AShiftJisStringCounts { get; init; }

        public required IReadOnlyDictionary<string, int> TextField7ARawPreviewCounts { get; init; }

        public required IReadOnlyDictionary<string, int> TextField7AField41Counts { get; init; }

        public required IReadOnlyDictionary<string, int> TextField7AField78Counts { get; init; }

        public required IReadOnlyDictionary<string, int> TextField7AField79Counts { get; init; }

        public required IReadOnlyDictionary<string, int> TextField7AField7CCounts { get; init; }

        public required IReadOnlyDictionary<string, int> TextField33VectorCounts { get; init; }

        public required IReadOnlyDictionary<string, int> TextField33RawHexCounts { get; init; }

        public required IReadOnlyDictionary<string, int> TextField7BPackedValuesCounts { get; init; }

        public required IReadOnlyDictionary<string, int> TextField7BRawHexCounts { get; init; }

        public required IReadOnlyDictionary<string, int> TextField78Field79Counts { get; init; }

        public required IReadOnlyDictionary<string, int> TextZeroMarkerField7ACounts { get; init; }

        public required IReadOnlyDictionary<string, int> TextFieldSequenceCounts { get; init; }

        public required IReadOnlyDictionary<string, int> TextFieldSetCounts { get; init; }

        public required int SliceCasts { get; init; }

        public required int SliceRecords { get; init; }

        public required int SliceCropReferences { get; init; }

        public required int SliceField44SlicRecordMatches { get; init; }

        public required int SliceField44SlicRecordMismatches { get; init; }

        public required int SliceField44CropReferenceMatches { get; init; }

        public required int SliceField44CropReferenceMismatches { get; init; }

        public required int SliceTargetIndexInRange { get; init; }

        public required int SliceTargetIndexOutOfRange { get; init; }

        public required IReadOnlyDictionary<string, int> SliceField83Counts { get; init; }

        public required IReadOnlyDictionary<string, int> SliceCastField40Counts { get; init; }

        public required IReadOnlyDictionary<string, int> SliceCastField41Counts { get; init; }

        public required IReadOnlyDictionary<string, int> SliceCastField42Counts { get; init; }

        public required IReadOnlyDictionary<string, int> SliceCastField43Counts { get; init; }

        public required IReadOnlyDictionary<string, int> SliceCastField80Counts { get; init; }

        public required IReadOnlyDictionary<string, int> SliceCastField81Counts { get; init; }

        public required IReadOnlyDictionary<string, int> SliceCastField82Counts { get; init; }

        public required IReadOnlyDictionary<string, int> SliceCastField84Counts { get; init; }

        public required IReadOnlyDictionary<string, int> SliceCastField85Counts { get; init; }

        public required IReadOnlyDictionary<string, int> SliceCastField86Counts { get; init; }

        public required IReadOnlyDictionary<string, int> SliceCastField87Counts { get; init; }

        public required IReadOnlyDictionary<string, int> SliceCastTargetNodeFlagCounts { get; init; }

        public required IReadOnlyDictionary<string, int> SliceCastTargetNodeGroupCounts { get; init; }

        public required IReadOnlyDictionary<string, int> SliceCastTargetDisplayCounts { get; init; }

        public required IReadOnlyDictionary<string, int> SliceCastTargetCimgTargetCounts { get; init; }

        public required IReadOnlyDictionary<string, int> SliceCastFieldSequenceCounts { get; init; }

        public required IReadOnlyDictionary<string, int> SliceCastFieldSetCounts { get; init; }

        public required IReadOnlyDictionary<string, int> SliceRecordField40Counts { get; init; }

        public required IReadOnlyDictionary<string, int> SliceRecordField41Counts { get; init; }

        public required IReadOnlyDictionary<string, int> SliceRecordField45Counts { get; init; }

        public required IReadOnlyDictionary<string, int> SliceRecordField37ColorCounts { get; init; }

        public required IReadOnlyDictionary<string, int> SliceRecordField38ColorCounts { get; init; }

        public required IReadOnlyDictionary<string, int> SliceRecordField39ColorCounts { get; init; }

        public required IReadOnlyDictionary<string, int> SliceRecordField39ColorCountCounts { get; init; }

        public required IReadOnlyDictionary<string, int> SliceRecordField83Field40Counts { get; init; }

        public required IReadOnlyDictionary<string, int> SliceRecordField83Field41Counts { get; init; }

        public required IReadOnlyDictionary<string, int> SliceRecordField83Field45Counts { get; init; }

        public required IReadOnlyDictionary<string, int> SliceRecordFieldSequenceCounts { get; init; }

        public required IReadOnlyDictionary<string, int> SliceRecordFieldSetCounts { get; init; }

        public required IReadOnlyDictionary<string, int> SliceRecordShapeCounts { get; init; }

        public required int TrackKeyCountMismatches { get; init; }

        public required int KeyTangentPresent { get; init; }

        public required int KeyTangentNonZero { get; init; }

        public required int KeyTangentMismatch { get; init; }

        public required int KeyTangentNonZeroScenes { get; init; }

        public required int KeyTangentMismatchScenes { get; init; }

        public required IReadOnlyDictionary<string, int> UnknownTypeCodeCounts { get; init; }

        public required IReadOnlyDictionary<string, int> NodeFlagCounts { get; init; }

        public required IReadOnlyDictionary<string, int> NodeFlagBitCounts { get; init; }

        public required IReadOnlyDictionary<string, int> NodeFlagBitDisplayFalseNodeCounts { get; init; }

        public required IReadOnlyDictionary<string, int> NodeFlagBitCimgTargetNodeCounts { get; init; }

        public required IReadOnlyDictionary<string, int> NodeFlagBitAnimatedNodeCounts { get; init; }

        public required IReadOnlyDictionary<string, int> NodeFlagBitDataNodeCounts { get; init; }

        public required IReadOnlyDictionary<string, int> NodeFlagBitCategoryRecordNodeCounts { get; init; }

        public required IReadOnlyDictionary<string, int> NodeFlagBitCategoryNonZeroNodeCounts { get; init; }

        public required IReadOnlyDictionary<string, int> NodeFlagBitExactFlagCounts { get; init; }

        public required IReadOnlyDictionary<string, int> NodeFlagBitGroupCounts { get; init; }

        public required IReadOnlyDictionary<string, int> NodeFlagBitImageCastFlagBitCounts { get; init; }

        public required IReadOnlyDictionary<string, int> NodeFlagBitTrackTypeCounts { get; init; }

        public required IReadOnlyDictionary<string, int> NodeFlagBitPairCounts { get; init; }

        public required IReadOnlyDictionary<string, int> CimgFlagCounts { get; init; }

        public required IReadOnlyDictionary<string, int> CimgFlagBitCounts { get; init; }

        public required IReadOnlyDictionary<string, int> CimgFlagBitDisplayFalseCounts { get; init; }

        public required IReadOnlyDictionary<string, int> CimgFlagBitMultiReferenceCounts { get; init; }

        public required IReadOnlyDictionary<string, int> CimgFlagBitSecondaryReferenceCounts { get; init; }

        public required IReadOnlyDictionary<string, int> CimgFlagBitNonZeroReferenceIndexCounts { get; init; }

        public required IReadOnlyDictionary<string, int> CimgFlagBitMissingNodeCounts { get; init; }

        public required IReadOnlyDictionary<string, int> CimgFlagBitNodeFlagCounts { get; init; }

        public required IReadOnlyDictionary<string, int> CimgFlagBitGroupCounts { get; init; }

        public required IReadOnlyDictionary<string, int> CimgFlagBitPairCounts { get; init; }

        public required int TextureAtlasCount { get; init; }

        public required IReadOnlyDictionary<string, int> TextureAtlasField62Counts { get; init; }

        public required IReadOnlyDictionary<string, int> TextureAtlasField62BitCounts { get; init; }

        public required IReadOnlyDictionary<string, int> TextureAtlasField62CropCountCounts { get; init; }

        public required IReadOnlyDictionary<string, int> TextureAtlasField62SizeCounts { get; init; }

        public required IReadOnlyDictionary<string, int> CropKindCounts { get; init; }

        public required IReadOnlyDictionary<string, int> CrefKindCounts { get; init; }

        public required int CropRectCount { get; init; }

        public required int CropAtlasDeclaredCountMatches { get; init; }

        public required int CropAtlasDeclaredCountMismatches { get; init; }

        public required int CropRectInAtlasBounds { get; init; }

        public required int CropRectOutOfAtlasBounds { get; init; }

        public required int CropRectNonPositiveSize { get; init; }

        public required int CropReferenceCount { get; init; }

        public required IReadOnlyDictionary<string, int> CropReferenceKindCounts { get; init; }

        public required IReadOnlyDictionary<string, int> CropReferenceOwnerCounts { get; init; }

        public required IReadOnlyDictionary<string, int> CropReferenceOwnerKindCounts { get; init; }

        public required IReadOnlyDictionary<string, int> CropReferenceTextureListIndexCounts { get; init; }

        public required IReadOnlyDictionary<string, int> CropReferenceTextureIndexRangeCounts { get; init; }

        public required IReadOnlyDictionary<string, int> CropReferenceCropIndexRangeCounts { get; init; }

        public required IReadOnlyDictionary<string, int> CropRectOutOfAtlasBoundsReasonCounts { get; init; }

        public required IReadOnlyDictionary<string, int> CropReferenceOutOfRangeOwnerCounts { get; init; }

        public required IReadOnlyDictionary<string, int> TrackFlagCounts { get; init; }

        public required IReadOnlyDictionary<string, int> TrackFlagBaseCounts { get; init; }

        public required IReadOnlyDictionary<string, int> TrackFlagExtraCounts { get; init; }

        public required IReadOnlyDictionary<string, int> TrackFlagExtraSceneCounts { get; init; }

        public required IReadOnlyDictionary<string, int> TrackFlagExtraBaseCounts { get; init; }

        public required IReadOnlyDictionary<string, int> TrackFlagExtraAnimationCounts { get; init; }

        public required IReadOnlyDictionary<string, int> TrackFlagExtraTrackTypeCounts { get; init; }

        public required IReadOnlyDictionary<string, int> TrackFlagExtraKeyValueTypeCounts { get; init; }

        public required IReadOnlyDictionary<string, int> TrackFlagExtraNodeFlagCounts { get; init; }

        public required IReadOnlyDictionary<string, int> TrackFlagExtraNodeFlagBitCounts { get; init; }

        public required IReadOnlyDictionary<string, int> TrackFlagExtraGroupCounts { get; init; }

        public required IReadOnlyDictionary<string, int> TrackFlagExtraCimgTargetCounts { get; init; }

        public required IReadOnlyDictionary<string, int> TrackFlagExtraInitialDisplayCounts { get; init; }

        public required IReadOnlyDictionary<string, int> TrackFlagExtraCimgFlagCounts { get; init; }

        public required IReadOnlyDictionary<string, int> TrackFlagExtraCimgFlagBitCounts { get; init; }

        public required IReadOnlyDictionary<string, int> TrackFlagExtraCimgReferenceCountCounts { get; init; }

        public required IReadOnlyDictionary<string, int> TrackTypeCounts { get; init; }

        public required IReadOnlyDictionary<string, int> KeyValueTypeCounts { get; init; }

        public required IReadOnlyDictionary<string, int> KeyInterpolationCounts { get; init; }

        public required IReadOnlyDictionary<string, int> KeyInterpolationTrackTypeCounts { get; init; }

        public required IReadOnlyDictionary<string, int> KeyInterpolationKeyValueTypeCounts { get; init; }

        public required IReadOnlyDictionary<string, int> KeyTangentPresentInterpolationCounts { get; init; }

        public required IReadOnlyDictionary<string, int> KeyTangentPresentTrackTypeCounts { get; init; }

        public required IReadOnlyDictionary<string, int> KeyTangentNonZeroInterpolationCounts { get; init; }

        public required IReadOnlyDictionary<string, int> KeyTangentNonZeroTrackTypeCounts { get; init; }

        public required IReadOnlyDictionary<string, int> KeyTangentMismatchInterpolationCounts { get; init; }

        public required IReadOnlyDictionary<string, int> KeyTangentMismatchTrackTypeCounts { get; init; }

        public required IReadOnlyDictionary<string, int> KeyTangentMismatchAnimationCounts { get; init; }

        public required IReadOnlyDictionary<string, int> KeyTangentMismatchNodeFlagCounts { get; init; }

        public required IReadOnlyDictionary<string, int> KeyTangentMismatchGroupCounts { get; init; }

        public required IReadOnlyDictionary<string, int> KeyTangentMismatchTrackExtraCounts { get; init; }

        public required IReadOnlyDictionary<string, int> KeyTangentMismatchTangentPairCounts { get; init; }

        public required IReadOnlyDictionary<string, int> KeyTangentNonZeroFramePositionCounts { get; init; }

        public required IReadOnlyDictionary<string, int> KeyTangentMismatchFramePositionCounts { get; init; }

        public required IReadOnlyDictionary<string, int> KeyTangentDeltaSignCounts { get; init; }

        public required IReadOnlyDictionary<string, int> TrackKeyStorageMatrixCounts { get; init; }

        public required IReadOnlyDictionary<string, int> TrackFieldSequenceCounts { get; init; }

        public required IReadOnlyDictionary<string, int> KeyFieldSequenceCounts { get; init; }

        public required IReadOnlyDictionary<string, int> TrackFrameRangeRelationCounts { get; init; }

        public required IReadOnlyDictionary<string, int> TrackKeyFrameOrderCounts { get; init; }

        public required IReadOnlyDictionary<string, int> TrackKeyFrameDuplicateCounts { get; init; }

        public required IReadOnlyDictionary<string, int> TrackFirstFrameDeltaCounts { get; init; }

        public required IReadOnlyDictionary<string, int> TrackLastFrameDeltaCounts { get; init; }

        public required int TransformTrackCount { get; init; }

        public required int TransformTrackKeyCount { get; init; }

        public required int TransformTracksWithInitialChannel { get; init; }

        public required int TransformTracksMissingInitialChannel { get; init; }

        public required int TransformTrackInitialValueMatches { get; init; }

        public required int TransformTrackInitialValueMismatches { get; init; }

        public required int TransformTrackKeysMissingValue { get; init; }

        public required IReadOnlyDictionary<string, int> TransformTrackTypeCounts { get; init; }

        public required IReadOnlyDictionary<string, int> TransformTrackKeyTypeCounts { get; init; }

        public required IReadOnlyDictionary<string, int> TransformTrackStorageCounts { get; init; }

        public required IReadOnlyDictionary<string, int> TransformTrackKeyValueKindCounts { get; init; }

        public required IReadOnlyDictionary<string, int> TransformTrackInitialMatchTypeCounts { get; init; }

        public required IReadOnlyDictionary<string, int> TransformTrackValueRangeCounts { get; init; }

        public required IReadOnlyDictionary<string, int> TransformCandidateDefaultKeyCounts { get; init; }

        public required int PackedAngleTrackCount { get; init; }

        public required int PackedAngleKeyCount { get; init; }

        public required IReadOnlyDictionary<string, int> PackedAngleTrackTypeCounts { get; init; }

        public required IReadOnlyDictionary<string, int> PackedAngleKeyTrackTypeCounts { get; init; }

        public required IReadOnlyDictionary<string, int> PackedAngleRawCounts { get; init; }

        public required IReadOnlyDictionary<string, int> PackedAngleDegreeCandidateCounts { get; init; }

        public required int ImageVariantTrackCount { get; init; }

        public required int ImageVariantKeyCount { get; init; }

        public required int ImageVariantTracksWithCimg { get; init; }

        public required int ImageVariantTracksMissingCimg { get; init; }

        public required int ImageVariantTrackRangeMatches { get; init; }

        public required int ImageVariantTrackRangeMismatches { get; init; }

        public required int ImageVariantKeysInRange { get; init; }

        public required int ImageVariantKeysOutOfRange { get; init; }

        public required int ImageVariantKeysMissingCimg { get; init; }

        public required int ImageVariantKeysNonInteger { get; init; }

        public required int ImageVariantKeysMissingValue { get; init; }

        public required IReadOnlyDictionary<string, int> ImageVariantReferenceCountCounts { get; init; }

        public required IReadOnlyDictionary<string, int> ImageVariantValueCounts { get; init; }

        public required IReadOnlyDictionary<string, int> ImageVariantGroupTrackCounts { get; init; }

        public required IReadOnlyDictionary<string, int> ImageVariantGroupKeyCounts { get; init; }

        public required IReadOnlyDictionary<string, int> ImageVariantGroupTracksWithCimgCounts { get; init; }

        public required IReadOnlyDictionary<string, int> ImageVariantGroupTracksMissingCimgCounts { get; init; }

        public required IReadOnlyDictionary<string, int> ImageVariantGroupTrackRangeMatchCounts { get; init; }

        public required IReadOnlyDictionary<string, int> ImageVariantGroupTrackRangeMismatchCounts { get; init; }

        public required IReadOnlyDictionary<string, int> ImageVariantGroupKeysInRangeCounts { get; init; }

        public required IReadOnlyDictionary<string, int> ImageVariantGroupKeysOutOfRangeCounts { get; init; }

        public required IReadOnlyDictionary<string, int> ImageVariantGroupKeysMissingCimgCounts { get; init; }

        public required IReadOnlyDictionary<string, int> ImageVariantGroupKeysNonIntegerCounts { get; init; }

        public required IReadOnlyDictionary<string, int> ImageVariantGroupKeysMissingValueCounts { get; init; }

        public required IReadOnlyDictionary<string, int> ImageVariantGroupReferenceCountCounts { get; init; }

        public required IReadOnlyDictionary<string, int> ImageVariantGroupValueCounts { get; init; }

        public required IReadOnlyDictionary<string, int> ImageVariantGroupCimg45FirstKeyRelationCounts { get; init; }

        public required IReadOnlyDictionary<string, int> ImageVariantGroupCimg45FirstKeyDeltaCounts { get; init; }

        public required IReadOnlyDictionary<string, int> ImageVariantGroupCimg45FirstKeyPairCounts { get; init; }

        public required int ColorTrackCount { get; init; }

        public required int ColorTrackKeyCount { get; init; }

        public required int ColorTracksWithInitialChannel { get; init; }

        public required int ColorTracksMissingInitialChannel { get; init; }

        public required int ColorTrackInitialValueMatches { get; init; }

        public required int ColorTrackInitialValueMismatches { get; init; }

        public required int ColorTrackKeysInUnitRange { get; init; }

        public required int ColorTrackKeysOutOfUnitRange { get; init; }

        public required int ColorTrackKeysMissingValue { get; init; }

        public required IReadOnlyDictionary<string, int> ColorTrackTypeCounts { get; init; }

        public required IReadOnlyDictionary<string, int> ColorTrackKeyTypeCounts { get; init; }

        public required IReadOnlyDictionary<string, int> ColorTrackInitialMatchTypeCounts { get; init; }

        public required int AlphaOpacityTrackCount { get; init; }

        public required int AlphaOpacityKeyCount { get; init; }

        public required int AlphaOpacityTracksWithMaterialAlpha { get; init; }

        public required int AlphaOpacityTracksMissingMaterialAlpha { get; init; }

        public required int AlphaOpacityInitialAlphaMatches { get; init; }

        public required int AlphaOpacityInitialAlphaMismatches { get; init; }

        public required int AlphaOpacityCimgTargets { get; init; }

        public required int AlphaOpacityDisplayFalseTargets { get; init; }

        public required int AlphaOpacityKeysInUnitRange { get; init; }

        public required int AlphaOpacityKeysOutOfUnitRange { get; init; }

        public required int AlphaOpacityKeysMissingValue { get; init; }
    }

    internal sealed class SvoSurveyRow
    {
        public required string Path { get; init; }

        public required string RelativePath { get; init; }

        public required long Size { get; init; }

        public string? Error { get; init; }

        public int DirectoryCount { get; init; }

        public int HeaderUnknownNonZeroBytes { get; init; }

        public IReadOnlyDictionary<string, int> HeaderUnknownWordClassCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> HeaderUnknownNonZeroOffsetCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> HeaderUnknownWordValueCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> HeaderUnknownWordOffsetValueCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> HeaderUnknownWordOffsetClassCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> HeaderUnknownWordRelationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> HeaderUnknownWordOffsetRelationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> HeaderUnknownWordPayloadLocationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> HeaderUnknownWordOffsetPayloadLocationCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public int DdsCount { get; init; }

        public int DirectoryReservedEntriesWithNonZero { get; init; }

        public int DirectoryReservedNonZeroBytes { get; init; }

        public bool YabxPresent { get; init; }

        public string? YabxHeaderHashCandidate { get; init; }

        public bool? YabxDeclaredPayloadLengthMatchesEntryLength { get; init; }

        public string? YabxReferenceBase { get; init; }

        public int? YabxObjectCount { get; init; }

        public int? YabxExpectedObjectCountFromDds { get; init; }

        public bool? YabxObjectCountMatchesDdsSkeleton { get; init; }

        public bool? YabxObjectTypeOrderMatchesDdsSkeleton { get; init; }

        public int? YabxUnparsedBytes { get; init; }

        public IReadOnlyDictionary<string, int> YabxObjectTypeCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> YabxDescriptorRawCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> YabxDescriptorFlagsCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> YabxDescriptorValueKindCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> YabxDescriptorReservedCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> YabxDescriptorUsageCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> YabxDescriptorRawUsageCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> YabxDescriptorRawObjectKindCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public int YabxResourceRecordCount { get; init; }

        public bool? YabxResourceRecordCountMatchesDds { get; init; }

        public int YabxResourceTextureImageReferenceMatches { get; init; }

        public int YabxResourceTextureImageReferenceMismatches { get; init; }

        public int YabxResourceTextureImageReferenceMissing { get; init; }

        public int YabxResourceDataSizeMatchesDirectory { get; init; }

        public int YabxResourceDataSizeMismatchesDirectory { get; init; }

        public int YabxResourceDataSizeMissing { get; init; }

        public int YabxResourceDimensionsMatchDds { get; init; }

        public int YabxResourceDimensionsMismatchDds { get; init; }

        public int YabxResourceDimensionsMissing { get; init; }

        public IReadOnlyDictionary<string, int> TextureFormatCounts { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);
    }

    internal sealed class SvoSurveyAggregate
    {
        public required int Total { get; init; }

        public required int Parsed { get; init; }

        public required int Failed { get; init; }

        public required int DirectoryReservedEntriesWithNonZero { get; init; }

        public required int DirectoryReservedNonZeroBytes { get; init; }

        public required int YabxWithUnparsedBytes { get; init; }

        public required int YabxUnparsedBytes { get; init; }

        public required int YabxExpectedObjectCountFromDds { get; init; }

        public required int YabxObjectCountDdsSkeletonMatches { get; init; }

        public required int YabxObjectCountDdsSkeletonMismatches { get; init; }

        public required int YabxObjectTypeOrderDdsSkeletonMatches { get; init; }

        public required int YabxObjectTypeOrderDdsSkeletonMismatches { get; init; }

        public required IReadOnlyDictionary<string, int> HeaderUnknownWordClassCounts { get; init; }

        public required IReadOnlyDictionary<string, int> HeaderUnknownNonZeroOffsetCounts { get; init; }

        public required IReadOnlyDictionary<string, int> HeaderUnknownWordValueCounts { get; init; }

        public required IReadOnlyDictionary<string, int> HeaderUnknownWordOffsetValueCounts { get; init; }

        public required IReadOnlyDictionary<string, int> HeaderUnknownWordOffsetClassCounts { get; init; }

        public required IReadOnlyDictionary<string, int> HeaderUnknownWordRelationCounts { get; init; }

        public required IReadOnlyDictionary<string, int> HeaderUnknownWordOffsetRelationCounts { get; init; }

        public required IReadOnlyDictionary<string, int> HeaderUnknownWordPayloadLocationCounts { get; init; }

        public required IReadOnlyDictionary<string, int> HeaderUnknownWordOffsetPayloadLocationCounts { get; init; }

        public required IReadOnlyDictionary<string, int> YabxHeaderHashCandidateCounts { get; init; }

        public required int YabxDeclaredPayloadLengthMatchesEntryLength { get; init; }

        public required int YabxDeclaredPayloadLengthMismatchesEntryLength { get; init; }

        public required IReadOnlyDictionary<string, int> YabxReferenceBaseCounts { get; init; }

        public required IReadOnlyDictionary<string, int> YabxDescriptorRawCounts { get; init; }

        public required IReadOnlyDictionary<string, int> YabxDescriptorFlagsCounts { get; init; }

        public required IReadOnlyDictionary<string, int> YabxDescriptorValueKindCounts { get; init; }

        public required IReadOnlyDictionary<string, int> YabxDescriptorReservedCounts { get; init; }

        public required IReadOnlyDictionary<string, int> YabxDescriptorUsageCounts { get; init; }

        public required IReadOnlyDictionary<string, int> YabxDescriptorRawUsageCounts { get; init; }

        public required IReadOnlyDictionary<string, int> YabxDescriptorRawObjectKindCounts { get; init; }

        public required IReadOnlyDictionary<string, int> TextureFormatCounts { get; init; }

        public required IReadOnlyDictionary<string, int> YabxObjectTypeCounts { get; init; }

        public required int YabxResourceRecordCount { get; init; }

        public required int YabxResourceRecordCountDdsMatches { get; init; }

        public required int YabxResourceRecordCountDdsMismatches { get; init; }

        public required int YabxResourceTextureImageReferenceMatches { get; init; }

        public required int YabxResourceTextureImageReferenceMismatches { get; init; }

        public required int YabxResourceTextureImageReferenceMissing { get; init; }

        public required int YabxResourceDataSizeMatchesDirectory { get; init; }

        public required int YabxResourceDataSizeMismatchesDirectory { get; init; }

        public required int YabxResourceDataSizeMissing { get; init; }

        public required int YabxResourceDimensionsMatchDds { get; init; }

        public required int YabxResourceDimensionsMismatchDds { get; init; }

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
