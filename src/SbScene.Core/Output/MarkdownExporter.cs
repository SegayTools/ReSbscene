using System.Text;
using SbScene.Core.Resources;
using SbScene.Core.Semantics;
using SbScene.Core.Vtbf;

namespace SbScene.Core.Output;

public static class MarkdownExporter
{
    public static string ToMarkdown(SbSceneFile file)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# sbscene 解析报告");
        builder.AppendLine();
        builder.AppendLine("## 概览");
        builder.AppendLine();
        builder.AppendLine("| 项 | 值 |");
        builder.AppendLine("| --- | ---: |");
        builder.AppendLine($"| 文件大小 | {file.SourceSize} bytes |");
        var rootSummary = string.Join(", ", file.Vtbf.Blocks.Select(static root => $"{root.Tag} raw=0x{root.ParamRawHex ?? "?"} low={root.ParamLow?.ToString() ?? "?"} high={root.ParamHigh?.ToString() ?? "?"}"));
        builder.AppendLine($"| VTBF 根 | {Escape(rootSummary)} |");
        builder.AppendLine($"| 根块数量 | {file.Summary.RootBlockCount} |");
        builder.AppendLine($"| 总块数量 | {file.Summary.TotalBlockCount} |");
        builder.AppendLine($"| NODE 数量 | {file.Summary.NodeCount} |");
        builder.AppendLine($"| TRS2 数量 | {file.Surfboard.Transform2DRecords.Count} |");
        builder.AppendLine($"| NCAT 数量 | {file.Surfboard.NodeCategoryRecords.Count} |");
        builder.AppendLine($"| Texture atlas 数量 | {file.Surfboard.Resources.Atlases.Count} |");
        builder.AppendLine($"| Image cast 数量 | {file.Surfboard.Resources.ImageCasts.Count} |");
        builder.AppendLine($"| ANIM 数量 | {file.Summary.AnimationCount} |");
        builder.AppendLine($"| 动画到节点绑定数量 | {file.Surfboard.AnimationBindings.Count} |");
        builder.AppendLine($"| Variant hints | {file.Summary.VariantHintCount} |");
        builder.AppendLine();

        AppendBlockCounts(builder, file);
        AppendBlockParameters(builder, file);
        AppendFieldCatalog(builder, file);
        AppendNodeGroups(builder, file);
        AppendNodeSample(builder, file);
        AppendNodeFlags(builder, file);
        AppendTransformSample(builder, file);
        AppendTransformStats(builder, file);
        AppendDataAndCategory(builder, file);
        AppendResources(builder, file);
        AppendCamera(builder, file);
        AppendAnimations(builder, file);
        AppendTrackStorage(builder, file);
        AppendTrackFlagExtraEvidence(builder, file);
        AppendTrackTypeEvidence(builder, file);
        AppendColorTrackEvidence(builder, file);
        AppendAlphaOpacityEvidence(builder, file);
        AppendPackedAngleCandidates(builder, file);
        AppendTrackKeyCounts(builder, file);
        AppendKeyInterpolation(builder, file);
        AppendAnimationBindings(builder, file);
        AppendStateTrackSummary(builder, file);
        AppendVariantHints(builder, file);
        AppendUnknownFields(builder, file);
        AppendWarnings(builder, file);

        return builder.ToString();
    }

    private static void AppendBlockCounts(StringBuilder builder, SbSceneFile file)
    {
        builder.AppendLine("## 块统计");
        builder.AppendLine();
        builder.AppendLine("| Tag | Count |");
        builder.AppendLine("| --- | ---: |");
        foreach (var pair in file.Summary.BlockCounts.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            builder.AppendLine($"| `{Escape(pair.Key)}` | {pair.Value} |");
        }

        builder.AppendLine();
    }

    private static void AppendBlockParameters(StringBuilder builder, SbSceneFile file)
    {
        builder.AppendLine("## 块参数分布");
        builder.AppendLine();
        builder.AppendLine("`ParamLow/ParamHigh` 是块级参数，`ParamRawHex` 保留原始 4 字节顺序；只有已验证的 tag 才在格式文档中命名。");
        builder.AppendLine();
        builder.AppendLine("| Tag | Count | ParamRawHex 分布 | ParamLow/ParamHigh 分布 |");
        builder.AppendLine("| --- | ---: | --- | --- |");
        foreach (var group in FlattenBlocks(file.Vtbf.Blocks)
            .GroupBy(static block => block.Tag, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal))
        {
            var pairs = string.Join(", ", group
                .GroupBy(static block => $"{block.ParamLow?.ToString() ?? "?"}/{block.ParamHigh?.ToString() ?? "?"}")
                .OrderByDescending(static pair => pair.Count())
                .ThenBy(static pair => pair.Key, StringComparer.Ordinal)
                .Take(8)
                .Select(static pair => $"`{pair.Key}`:{pair.Count()}"));
            var rawValues = string.Join(", ", group
                .Select(static block => block.ParamRawHex)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .GroupBy(static value => value!, StringComparer.Ordinal)
                .OrderByDescending(static rawGroup => rawGroup.Count())
                .ThenBy(static rawGroup => rawGroup.Key, StringComparer.Ordinal)
                .Take(8)
                .Select(static rawGroup => $"`0x{rawGroup.Key}`:{rawGroup.Count()}"));
            builder.AppendLine($"| `{Escape(group.Key)}` | {group.Count()} | {rawValues} | {pairs} |");
        }

        builder.AppendLine();
    }

    private static void AppendFieldCatalog(StringBuilder builder, SbSceneFile file)
    {
        var rows = BuildFieldCatalogRows(file).ToArray();
        if (rows.Length == 0)
        {
            return;
        }

        builder.AppendLine("## 字段目录");
        builder.AppendLine();
        builder.AppendLine("按 `tag + field id + type` 汇总字段出现次数、count/stride 分布和值样例。该表用于逆向核对字段编码，字段语义仍以对应章节为准。");
        builder.AppendLine();
        builder.AppendLine("| Tag | Field | Type | Occurrences | Blocks | Count/Stride | Values / Preview |");
        builder.AppendLine("| --- | ---: | --- | ---: | ---: | --- | --- |");
        foreach (var row in rows)
        {
            builder.AppendLine($"| `{Escape(row.Tag)}` | `{row.FieldId}` | {Escape(row.Type)} | {row.Occurrences} | {row.Blocks} | {Escape(row.CountStrideDistribution)} | {Escape(row.ValueDistribution)} |");
        }

        builder.AppendLine();
    }

    private static void AppendNodeGroups(StringBuilder builder, SbSceneFile file)
    {
        builder.AppendLine("## 节点分组");
        builder.AppendLine();
        builder.AppendLine("| Group | Count | Examples |");
        builder.AppendLine("| --- | ---: | --- |");
        foreach (var group in file.Surfboard.NodeGroups)
        {
            var examples = string.Join(", ", group.NodeNames.Take(8).Select(Escape));
            builder.AppendLine($"| `{Escape(group.Name)}` | {group.Count} | {examples} |");
        }

        builder.AppendLine();
    }

    private static void AppendNodeSample(StringBuilder builder, SbSceneFile file)
    {
        builder.AppendLine("## 节点样例");
        builder.AppendLine();
        builder.AppendLine("| # | Name | Flags | Child | Sibling | Comment |");
        builder.AppendLine("| ---: | --- | ---: | ---: | ---: | --- |");
        foreach (var node in file.Surfboard.Nodes.Take(40))
        {
            builder.AppendLine(
                $"| {node.Index} | {Escape(node.Name ?? string.Empty)} | `0x{(node.Flags ?? 0):X}` | {node.ChildIndex ?? -1} | {node.SiblingIndex ?? -1} | {Escape(node.Comment ?? string.Empty)} |");
        }

        builder.AppendLine();
    }

    private static void AppendNodeFlags(StringBuilder builder, SbSceneFile file)
    {
        var imageCastNodeIndexes = file.Surfboard.Resources.ImageCasts
            .Where(static imageCast => imageCast.CastIndex >= 0)
            .GroupBy(static imageCast => imageCast.CastIndex)
            .ToDictionary(static group => group.Key, static group => group.Count());
        var animatedNodeIndexes = file.Surfboard.AnimationBindings
            .GroupBy(static binding => binding.NodeIndex)
            .ToDictionary(static group => group.Key, static group => group.Count());

        builder.AppendLine("## NODE flags 分布");
        builder.AppendLine();
        builder.AppendLine("| Flags | Bits | Nodes | Image casts | Animated nodes | Display=false | Groups | Examples |");
        builder.AppendLine("| ---: | --- | ---: | ---: | ---: | ---: | --- | --- |");
        foreach (var group in file.Surfboard.Nodes
            .GroupBy(static node => node.Flags ?? 0)
            .OrderBy(static group => group.Key))
        {
            var imageCasts = group.Sum(node => imageCastNodeIndexes.TryGetValue(node.Index, out var count) ? count : 0);
            var animatedNodes = group.Count(node => animatedNodeIndexes.ContainsKey(node.Index));
            var hidden = group.Count(static node => node.Transform2D?.Display == false);
            var groups = string.Join(", ", group.Select(static node => node.Group).Distinct(StringComparer.OrdinalIgnoreCase).Take(8));
            var examples = string.Join(", ", group.Select(static node => node.Name ?? string.Empty).Take(8));
            builder.AppendLine($"| `0x{group.Key:X}` | {Escape(FormatNodeFlagBits(group.Key))} | {group.Count()} | {imageCasts} | {animatedNodes} | {hidden} | {Escape(groups)} | {Escape(examples)} |");
        }

        var presentBits = file.Surfboard.Nodes
            .SelectMany(static node => node.FlagBits)
            .Distinct()
            .OrderBy(static bit => bit)
            .ToArray();
        if (presentBits.Length > 0)
        {
            builder.AppendLine();
            builder.AppendLine("`NODE.0x30` bit 分布：");
            builder.AppendLine();
            builder.AppendLine("| Bit | Mask | Nodes | Image casts | Animated nodes | Display=false | Groups | 候选语义 | Examples |");
            builder.AppendLine("| ---: | ---: | ---: | ---: | ---: | ---: | --- | --- | --- |");
            foreach (var bit in presentBits)
            {
                var nodes = file.Surfboard.Nodes.Where(node => node.FlagBits.Contains(bit)).ToArray();
                var imageCasts = nodes.Sum(node => imageCastNodeIndexes.TryGetValue(node.Index, out var count) ? count : 0);
                var animatedNodes = nodes.Count(node => animatedNodeIndexes.ContainsKey(node.Index));
                var hidden = nodes.Count(static node => node.Transform2D?.Display == false);
                var groups = string.Join(", ", nodes
                    .Select(static node => node.Group)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(8));
                var examples = string.Join(", ", nodes.Select(static node => node.Name ?? string.Empty).Take(8));
                builder.AppendLine($"| {bit} | `0x{1u << bit:X8}` | {nodes.Length} | {imageCasts} | {animatedNodes} | {hidden} | {Escape(groups)} | {Escape(DescribeNodeFlagBit(bit))} | {Escape(examples)} |");
            }
        }

        builder.AppendLine();
    }

    private static void AppendTransformSample(StringBuilder builder, SbSceneFile file)
    {
        builder.AppendLine("## TRS2 变换样例");
        builder.AppendLine();
        builder.AppendLine("| Node | Translation | RotationZ candidate | Scale | Display | Material |");
        builder.AppendLine("| --- | --- | ---: | --- | --- | --- |");
        foreach (var node in file.Surfboard.Nodes.Where(static node => node.Transform2D is not null).Take(40))
        {
            var transform = node.Transform2D!;
            builder.AppendLine(
                $"| {Escape(node.Name ?? string.Empty)} | {FormatVector(transform.Translation)} | {FormatRotation(transform)} | {FormatVector(transform.Scale)} | {FormatBool(transform.Display)} | `{transform.MaterialColor?.Hex ?? string.Empty}` |");
        }

        builder.AppendLine();
    }

    private static void AppendTransformStats(StringBuilder builder, SbSceneFile file)
    {
        var transforms = file.Surfboard.Transform2DRecords;
        if (transforms.Count == 0)
        {
            return;
        }

        builder.AppendLine("## TRS2 字段统计");
        builder.AppendLine();
        builder.AppendLine($"Display 分布：true {transforms.Count(static item => item.Display == true)}，false {transforms.Count(static item => item.Display == false)}，unknown {transforms.Count(static item => item.Display is null)}。");
        builder.AppendLine();
        builder.AppendLine("| 字段 | 分布 |");
        builder.AppendLine("| --- | --- |");
        builder.AppendLine($"| Material color | {Escape(FormatColorDistribution(transforms.Select(static item => item.MaterialColor)))} |");
        builder.AppendLine($"| Illumination color | {Escape(FormatColorDistribution(transforms.Select(static item => item.IlluminationColor)))} |");
        builder.AppendLine($"| Vertex color count | {Escape(FormatCountDistribution(transforms.Select(static item => item.VertexColors.Count)))} |");
        builder.AppendLine($"| Multi position flags | {Escape(FormatNullableIntDistribution(transforms.Select(static item => item.MultiPosFlags)))} |");
        builder.AppendLine($"| Multi size flags | {Escape(FormatNullableIntDistribution(transforms.Select(static item => item.MultiSizeFlags)))} |");
        builder.AppendLine();
        builder.AppendLine("TRS2 字段出现次数：");
        builder.AppendLine();
        builder.AppendLine("| Field | Count |");
        builder.AppendLine("| --- | ---: |");
        foreach (var group in transforms
            .SelectMany(static transform => transform.Fields)
            .GroupBy(static field => field.IdHex, StringComparer.Ordinal)
            .OrderBy(static group => group.Key, StringComparer.Ordinal))
        {
            builder.AppendLine($"| `{group.Key}` | {group.Count()} |");
        }

        builder.AppendLine();
    }

    private static void AppendDataAndCategory(StringBuilder builder, SbSceneFile file)
    {
        var blocks = FlattenBlocks(file.Vtbf.Blocks).ToArray();
        var dataBlocks = blocks.Where(static block => block.Tag == "DATA").ToArray();
        var followingImageCastCounts = CountFollowingTags(blocks, "CIMG");
        var followingCimgCrfdCounts = CountFollowingTags(blocks, "CIMG", "CRFD");
        var followingCimgCnumCrfdCounts = CountFollowingTags(blocks, "CIMG", "CNUM", "CRFD");
        var followingCimgCnumCrfdCsliCounts = CountFollowingTags(blocks, "CIMG", "CNUM", "CRFD", "CSLI");
        var followingTagCounts = CountFollowingTagRuns(blocks);
        var imageCastCount = file.Surfboard.Resources.ImageCasts.Count;
        builder.AppendLine("## DATA 与 NCAT");
        builder.AppendLine();
        builder.AppendLine("| Block | Offset | ParamLow | ParamHigh | Image casts | Matches image casts | Following tags | Following CIMG | Matches CIMG | Following CIMG+CRFD | Matches CIMG+CRFD | Following CIMG+CNUM+CRFD | Matches CIMG+CNUM+CRFD | Following CIMG+CNUM+CRFD+CSLI | Matches CIMG+CNUM+CRFD+CSLI | Fields | Trailing bytes |");
        builder.AppendLine("| --- | ---: | ---: | ---: | ---: | --- | --- | ---: | --- | ---: | --- | ---: | --- | ---: | --- | ---: | ---: |");
        for (var i = 0; i < dataBlocks.Length; i++)
        {
            var block = dataBlocks[i];
            var followingImageCasts = i < followingImageCastCounts.Count ? followingImageCastCounts[i] : 0;
            var followingCimgCrfd = i < followingCimgCrfdCounts.Count ? followingCimgCrfdCounts[i] : 0;
            var followingCimgCnumCrfd = i < followingCimgCnumCrfdCounts.Count ? followingCimgCnumCrfdCounts[i] : 0;
            var followingCimgCnumCrfdCsli = i < followingCimgCnumCrfdCsliCounts.Count ? followingCimgCnumCrfdCsliCounts[i] : 0;
            var followingTags = i < followingTagCounts.Count ? FormatTagCounts(followingTagCounts[i]) : string.Empty;
            var matchesImageCasts = block.ParamLow == imageCastCount ? "yes" : "no";
            var matchesFollowingImageCasts = block.ParamLow == followingImageCasts ? "yes" : "no";
            var matchesFollowingCimgCrfd = block.ParamLow == followingCimgCrfd ? "yes" : "no";
            var matchesFollowingCimgCnumCrfd = block.ParamLow == followingCimgCnumCrfd ? "yes" : "no";
            var matchesFollowingCimgCnumCrfdCsli = block.ParamLow == followingCimgCnumCrfdCsli ? "yes" : "no";
            builder.AppendLine($"| `DATA` | `0x{block.Offset:X}` | {block.ParamLow?.ToString() ?? string.Empty} | {block.ParamHigh?.ToString() ?? string.Empty} | {imageCastCount} | {matchesImageCasts} | {Escape(followingTags)} | {followingImageCasts} | {matchesFollowingImageCasts} | {followingCimgCrfd} | {matchesFollowingCimgCrfd} | {followingCimgCnumCrfd} | {matchesFollowingCimgCnumCrfd} | {followingCimgCnumCrfdCsli} | {matchesFollowingCimgCnumCrfdCsli} | {block.Fields.Count} | {block.TrailingBytes?.Length ?? 0} |");
        }

        if (dataBlocks.Length == 0)
        {
            builder.AppendLine("| `DATA` |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |");
        }

        builder.AppendLine();
        if (file.Surfboard.NodeCategoryRecords.Count > 0)
        {
            builder.AppendLine($"`NCAT.0x0E` 节点分类记录数 {file.Surfboard.NodeCategoryRecords.Count}，分布：{Escape(FormatCountDistribution(file.Surfboard.NodeCategoryRecords))}。");
            builder.AppendLine();
        }

        if (file.Surfboard.NodeCategoryDetails.Count > 0)
        {
            var details = file.Surfboard.NodeCategoryDetails;
            var kindSummary = string.Join(", ", details
                .Select(static record => record.KindName ?? "(none)")
                .GroupBy(static value => value, StringComparer.Ordinal)
                .OrderByDescending(static group => group.Count())
                .ThenBy(static group => group.Key, StringComparer.Ordinal)
                .Take(16)
                .Select(static group => $"{group.Key}:{group.Count()}"));
            builder.AppendLine($"`NCAT` detail records {details.Count}，带 `0x0E` 分类 {details.Count(static record => record.CategoryId is not null)}，无 `0x0E` 分类 {details.Count(static record => record.CategoryId is null)}；`0x03` kind 分布：{Escape(kindSummary)}。");
            builder.AppendLine();
            builder.AppendLine("| # | Kind | 0x0D raw | 0x0E category | 0x0F preview |");
            builder.AppendLine("| ---: | --- | ---: | ---: | --- |");
            foreach (var record in details
                .Where(static record => record.KindName is not null || record.ParameterPreview is not null)
                .Take(32))
            {
                builder.AppendLine($"| {record.Index} | {Escape(record.KindName ?? string.Empty)} | {record.TypeByte?.ToString() ?? string.Empty} | {record.CategoryId?.ToString() ?? string.Empty} | {Escape(record.ParameterPreview ?? string.Empty)} |");
            }

            builder.AppendLine();
        }
    }

    private static void AppendResources(StringBuilder builder, SbSceneFile file)
    {
        var resources = file.Surfboard.Resources;
        builder.AppendLine("## 纹理与 Image Cast");
        builder.AppendLine();
        builder.AppendLine($"Texture list: `{Escape(resources.TextureListName ?? string.Empty)}`，声明 texture 数 {resources.DeclaredTextureCount?.ToString() ?? "?"}。");
        builder.AppendLine();
        builder.AppendLine("| # | Atlas | Size | 0x62 State | State Bits | Declared Crops | Parsed Crops |");
        builder.AppendLine("| ---: | --- | --- | ---: | --- | ---: | ---: |");
        foreach (var atlas in resources.Atlases)
        {
            builder.AppendLine($"| {atlas.Index} | `{Escape(atlas.Name)}` | {atlas.Width}x{atlas.Height} | `{FormatNullableHex(atlas.Field62)}` | {Escape(string.Join(",", atlas.Field62Bits))} | {atlas.DeclaredCropCount} | {atlas.Crops.Count} |");
        }

        builder.AppendLine();
        builder.AppendLine($"`TEX.0x62` shared packed state word 分布：{Escape(FormatNullableHexIntDistribution(resources.Atlases.Select(static atlas => atlas.Field62)))}。");
        builder.AppendLine();
        var cropReferences = resources.ImageCasts.Sum(static imageCast => imageCast.CropReferences.Count);
        var primaryCropReferences = resources.ImageCasts.Sum(static imageCast => imageCast.PrimaryCropReferences.Count);
        var secondaryCropReferences = resources.ImageCasts.Sum(static imageCast => imageCast.SecondaryCropReferences.Count);
        var multiReferenceCasts = resources.ImageCasts.Count(static imageCast => imageCast.CropReferences.Count > 1);
        var secondaryReferenceCasts = resources.ImageCasts.Count(static imageCast => imageCast.SecondaryCropReferenceCount is > 0);
        var mismatches = resources.ImageCasts.Count(static imageCast => imageCast.CropReferenceCountMatches == false);
        builder.AppendLine($"Image casts: {resources.ImageCasts.Count}；crop references: {cropReferences}（primary {primaryCropReferences}，secondary {secondaryCropReferences}）；multi-reference image casts: {multiReferenceCasts}；secondary CREF image casts: {secondaryReferenceCasts}；count mismatches: {mismatches}。");
        builder.AppendLine();
        builder.AppendLine("Packed record 首字节 kind 分布（只表示已观察 raw value，枚举语义未命名）：");
        builder.AppendLine();
        builder.AppendLine("| Record | Kind distribution |");
        builder.AppendLine("| --- | --- |");
        builder.AppendLine($"| `CROP.0x65` | {Escape(FormatCountDistribution(resources.Atlases.SelectMany(static atlas => atlas.Crops).Select(static crop => (int)crop.Kind)))} |");
        builder.AppendLine($"| `CREF.0x49` | {Escape(FormatCountDistribution(resources.ImageCasts.SelectMany(static imageCast => imageCast.CropReferences).Select(static reference => (int)reference.Kind)))} |");
        builder.AppendLine();
        var countMatches = resources.ImageCasts.Count(static imageCast => imageCast.CropReferenceCountMatches == true);
        var unknownCountMatches = resources.ImageCasts.Count(static imageCast => imageCast.CropReferenceCountMatches is null);
        var indexValidation = BuildCropReferenceIndexValidation(resources.ImageCasts);
        builder.AppendLine("`CIMG.0x44/0x45` 值域校验（`0x45` 为 primary/secondary CREF 组内 raw index）：");
        builder.AppendLine();
        builder.AppendLine("| Field | Check | Result |");
        builder.AppendLine("| --- | --- | --- |");
        builder.AppendLine($"| `0x44` | declared primary/secondary counts vs parsed CREF records | matches {countMatches}/{resources.ImageCasts.Count}, mismatches {mismatches}, unknown {unknownCountMatches} |");
        builder.AppendLine($"| `0x45` | stored group index range in non-empty primary/secondary CREF groups | activeGroups {indexValidation.ActiveGroups}, inRange {indexValidation.InRangeGroups}, outOfRange {indexValidation.OutOfRangeGroups}, emptyGroupNonZero {indexValidation.EmptyGroupNonZeroIndices}, nonZeroIndices {indexValidation.NonZeroIndices}, nonZeroImageCasts {indexValidation.NonZeroImageCasts} |");
        builder.AppendLine();
        builder.AppendLine("`CIMG.0x44` primary/secondary reference count 分布：");
        builder.AppendLine();
        builder.AppendLine("| 0x44 tuple | Image casts |");
        builder.AppendLine("| --- | ---: |");
        foreach (var group in resources.ImageCasts
            .GroupBy(static imageCast => $"{imageCast.PrimaryCropReferenceCount ?? 0},{imageCast.SecondaryCropReferenceCount ?? 0}")
            .OrderByDescending(static group => group.Count()))
        {
            builder.AppendLine($"| `({group.Key})` | {group.Count()} |");
        }

        builder.AppendLine();
        builder.AppendLine("`CIMG.0x45` primary/secondary reference index 分布：");
        builder.AppendLine();
        builder.AppendLine("| 0x45 tuple | Image casts |");
        builder.AppendLine("| --- | ---: |");
        foreach (var group in resources.ImageCasts
            .GroupBy(static imageCast => $"{imageCast.PrimaryCropReferenceIndex ?? 0},{imageCast.SecondaryCropReferenceIndex ?? 0}")
            .OrderByDescending(static group => group.Count()))
        {
            builder.AppendLine($"| `({group.Key})` | {group.Count()} |");
        }

        builder.AppendLine();
        var nonZeroReferenceIndexCasts = resources.ImageCasts
            .Where(static imageCast =>
                imageCast.PrimaryCropReferences.Count > 1
                || imageCast.SecondaryCropReferences.Count > 0
                || imageCast.PrimaryCropReferenceIndex is > 0
                || imageCast.SecondaryCropReferenceIndex is > 0)
            .ToArray();
        if (nonZeroReferenceIndexCasts.Length > 0)
        {
            builder.AppendLine("`CIMG.0x45` non-zero group index samples:");
            builder.AppendLine();
            builder.AppendLine("| Node | 0x44 counts | 0x45 indices | Primary indexed CREF | Secondary indexed CREF |");
            builder.AppendLine("| --- | --- | --- | --- | --- |");
            foreach (var imageCast in nonZeroReferenceIndexCasts.Take(80))
            {
                builder.AppendLine(
                    $"| {Escape(imageCast.NodeName ?? string.Empty)} | `({imageCast.PrimaryCropReferenceCount ?? 0},{imageCast.SecondaryCropReferenceCount ?? 0})` | `({imageCast.PrimaryCropReferenceIndex ?? 0},{imageCast.SecondaryCropReferenceIndex ?? 0})` | {Escape(FormatIndexedReference(imageCast.PrimaryCropReferences, imageCast.PrimaryCropReferenceIndex))} | {Escape(FormatIndexedReference(imageCast.SecondaryCropReferences, imageCast.SecondaryCropReferenceIndex))} |");
            }

            builder.AppendLine();
        }

        builder.AppendLine("`CIMG.0x48` shared packed state word values:");
        builder.AppendLine();
        builder.AppendLine("| Raw value | Bits | Image casts | Node flags | Groups | Display=false | Multi refs | Secondary refs | Non-zero 0x45 index | Examples |");
        builder.AppendLine("| --- | --- | ---: | --- | --- | ---: | ---: | ---: | ---: | --- |");
        foreach (var group in resources.ImageCasts
            .GroupBy(static imageCast => imageCast.ImageCastFlags)
            .OrderBy(static group => group.Key))
        {
            var items = group.ToArray();
            var examples = string.Join(", ", items.Select(static imageCast => imageCast.NodeName ?? string.Empty).Take(5));
            builder.AppendLine($"| `0x{group.Key:X8}` | {Escape(FormatImageCastBits(group.Key))} | {items.Length} | {Escape(FormatImageCastNodeFlagDistribution(items, file.Surfboard.Nodes))} | {Escape(FormatImageCastGroupDistribution(items, file.Surfboard.Nodes))} | {CountImageCastsWithDisplayFalse(items, file.Surfboard.Nodes)} | {CountMultiReferenceImageCasts(items)} | {CountSecondaryReferenceImageCasts(items)} | {CountNonZeroReferenceIndexImageCasts(items)} | {Escape(examples)} |");
        }

        builder.AppendLine();
        builder.AppendLine("`CIMG.0x48` packed state bit 分布：");
        builder.AppendLine();
        builder.AppendLine("| Bit | Mask | Image casts | Node flags | Groups | Display=false | Multi refs | Secondary refs | Non-zero 0x45 index | Observation | Examples |");
        builder.AppendLine("| ---: | ---: | ---: | --- | --- | ---: | ---: | ---: | ---: | --- | --- |");
        foreach (var group in Enumerable.Range(0, 32)
            .Select(bit => new
            {
                Bit = bit,
                Items = resources.ImageCasts.Where(imageCast => ((uint)imageCast.ImageCastFlags & (1u << bit)) != 0).ToArray(),
            })
            .Where(static item => item.Items.Length > 0))
        {
            var examples = string.Join(", ", group.Items.Select(static imageCast => imageCast.NodeName ?? string.Empty).Take(8));
            builder.AppendLine($"| {group.Bit} | `0x{1u << group.Bit:X8}` | {group.Items.Length} | {Escape(FormatImageCastNodeFlagDistribution(group.Items, file.Surfboard.Nodes))} | {Escape(FormatImageCastGroupDistribution(group.Items, file.Surfboard.Nodes))} | {CountImageCastsWithDisplayFalse(group.Items, file.Surfboard.Nodes)} | {CountMultiReferenceImageCasts(group.Items)} | {CountSecondaryReferenceImageCasts(group.Items)} | {CountNonZeroReferenceIndexImageCasts(group.Items)} | {Escape(DescribeImageCastFlagBit(group.Bit))} | {Escape(examples)} |");
        }

        var bitPairs = BuildImageCastFlagBitPairs(resources.ImageCasts);
        if (bitPairs.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("`CIMG.0x48` bit 共现：");
            builder.AppendLine();
            builder.AppendLine("| Bits | Image casts | Observation |");
            builder.AppendLine("| --- | ---: | --- |");
            foreach (var pair in bitPairs)
            {
                builder.AppendLine($"| `{pair.Bits}` | {pair.Count} | {Escape(pair.Observation)} |");
            }
        }

        builder.AppendLine();
        builder.AppendLine("| # | Node | Size | Pivot | Counts | Indices | Actual Refs | Crop Paths |");
        builder.AppendLine("| ---: | --- | --- | --- | --- | --- | ---: | --- |");
        foreach (var imageCast in resources.ImageCasts.Take(40))
        {
            var cropPaths = string.Join(", ", imageCast.CropReferences.Take(3).Select(static reference => reference.CropPath ?? $"{reference.AtlasName}[{reference.CropIndex}]"));
            builder.AppendLine(
                $"| {imageCast.Index} | {Escape(imageCast.NodeName ?? string.Empty)} | {imageCast.Width:0.###}x{imageCast.Height:0.###} | ({imageCast.PivotX:0.###}, {imageCast.PivotY:0.###}) | ({imageCast.PrimaryCropReferenceCount ?? 0},{imageCast.SecondaryCropReferenceCount ?? 0}) | ({imageCast.PrimaryCropReferenceIndex ?? 0},{imageCast.SecondaryCropReferenceIndex ?? 0}) | {imageCast.CropReferences.Count} | {Escape(cropPaths)} |");
        }

        var secondaryCasts = resources.ImageCasts.Where(static imageCast => imageCast.SecondaryCropReferences.Count > 0).ToArray();
        if (secondaryCasts.Length > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Secondary CREF image casts:");
            builder.AppendLine();
            builder.AppendLine("| Node | 0x44 | 0x45 | Primary refs | Secondary refs |");
            builder.AppendLine("| --- | --- | --- | --- | --- |");
            foreach (var imageCast in secondaryCasts)
            {
                var primary = string.Join(", ", imageCast.PrimaryCropReferences.Select(static reference => $"{reference.TextureIndex}:{reference.CropIndex}"));
                var secondary = string.Join(", ", imageCast.SecondaryCropReferences.Select(static reference => $"{reference.TextureIndex}:{reference.CropIndex}"));
                builder.AppendLine($"| {Escape(imageCast.NodeName ?? string.Empty)} | `({imageCast.PrimaryCropReferenceCount ?? 0},{imageCast.SecondaryCropReferenceCount ?? 0})` | `({imageCast.PrimaryCropReferenceIndex ?? 0},{imageCast.SecondaryCropReferenceIndex ?? 0})` | {Escape(primary)} | {Escape(secondary)} |");
            }
        }

        if (resources.CnumRecords.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("`CNUM` raw resource records:");
            builder.AppendLine();
            builder.AppendLine($"CNUM records: {resources.CnumRecords.Count}; CREF records: {resources.CnumRecords.Sum(static record => record.CropReferences.Count)}; `CNUM.0x44` vs following CREF records matches: {resources.CnumRecords.Count(static record => record.CropReferenceCountMatchesField44 == true)}; mismatches: {resources.CnumRecords.Count(static record => record.CropReferenceCountMatchesField44 == false)}; missing: {resources.CnumRecords.Count(static record => record.CropReferenceCountMatchesField44 is null)}; `0x51` node-index in range: {resources.CnumRecords.Count(static record => record.NodeName is not null)}.");
            builder.AppendLine();
            builder.AppendLine("| Field | Distribution |");
            builder.AppendLine("| --- | --- |");
            builder.AppendLine($"| `CNUM.0x48` | {Escape(FormatNullableIntDistribution(resources.CnumRecords.Select(static record => record.Field48)))} |");
            builder.AppendLine($"| `CNUM.0x40` | {Escape(FormatNullableFloatDistribution(resources.CnumRecords.Select(static record => record.Field40)))} |");
            builder.AppendLine($"| `CNUM.0x42` | {Escape(FormatNullableFloatDistribution(resources.CnumRecords.Select(static record => record.Field42)))} |");
            builder.AppendLine($"| `CNUM.0x43` | {Escape(FormatNullableFloatDistribution(resources.CnumRecords.Select(static record => record.Field43)))} |");
            builder.AppendLine($"| `CNUM.0x39 colors` | {Escape(FormatNullableColorListDistribution(resources.CnumRecords.Select(static record => record.Field39Colors)))} |");
            builder.AppendLine($"| `CNUM.0x39 raw hex` | {Escape(FormatNullableStringListDistribution(resources.CnumRecords.Select(static record => record.Field39RawHexValues)))} |");
            builder.AppendLine($"| `CNUM.0x44` | {Escape(FormatNullableIntDistribution(resources.CnumRecords.Select(static record => record.Field44Count)))} |");
            builder.AppendLine($"| `CNUM.0xA0` | {Escape(FormatNullableIntDistribution(resources.CnumRecords.Select(static record => record.FieldA0)))} |");
            builder.AppendLine($"| `CNUM.0xA1` | {Escape(FormatNullableStringDistribution(resources.CnumRecords.Select(static record => record.FieldA1)))} |");
            builder.AppendLine($"| `CNUM.0xA1 raw hex` | {Escape(FormatNullableStringDistribution(resources.CnumRecords.Select(static record => record.FieldA1RawHex)))} |");
            builder.AppendLine($"| `CNUM.0xA2` | {Escape(FormatNullableIntDistribution(resources.CnumRecords.Select(static record => record.FieldA2)))} |");
            builder.AppendLine($"| `CNUM.0xA3` | {Escape(FormatNullableIntDistribution(resources.CnumRecords.Select(static record => record.FieldA3)))} |");
            builder.AppendLine($"| `CNUM.0xA4` | {Escape(FormatNullableIntDistribution(resources.CnumRecords.Select(static record => record.FieldA4)))} |");
            builder.AppendLine($"| `CNUM.0xA5` | {Escape(FormatNullableIntDistribution(resources.CnumRecords.Select(static record => record.FieldA5)))} |");
            builder.AppendLine($"| `CNUM.0xA6` | {Escape(FormatNullableIntDistribution(resources.CnumRecords.Select(static record => record.FieldA6)))} |");
            builder.AppendLine($"| `CNUM.0xA7` | {Escape(FormatNullableIntDistribution(resources.CnumRecords.Select(static record => record.FieldA7)))} |");
            builder.AppendLine($"| `CNUM.0xA8` | {Escape(FormatNullableIntDistribution(resources.CnumRecords.Select(static record => record.FieldA8)))} |");
            builder.AppendLine($"| `CNUM.0xA9` | {Escape(FormatNullableIntDistribution(resources.CnumRecords.Select(static record => record.FieldA9)))} |");
            builder.AppendLine($"| `CNUM.0xAA` | {Escape(FormatNullableIntDistribution(resources.CnumRecords.Select(static record => record.FieldAA)))} |");
            builder.AppendLine($"| `CNUM.0xAB` | {Escape(FormatNullableIntDistribution(resources.CnumRecords.Select(static record => record.FieldAB)))} |");
            builder.AppendLine($"| `CNUM.0xAC` | {Escape(FormatNullableIntDistribution(resources.CnumRecords.Select(static record => record.FieldAC)))} |");
            builder.AppendLine($"| `CNUM.0xAD` | {Escape(FormatNullableIntDistribution(resources.CnumRecords.Select(static record => record.FieldAD)))} |");
            builder.AppendLine($"| `CNUM.0xAE float values` | {Escape(FormatNullableFloatListDistribution(resources.CnumRecords.Select(static record => record.FieldAEFloatValues)))} |");
            builder.AppendLine($"| `CNUM.0xAE raw hex` | {Escape(FormatNullableStringDistribution(resources.CnumRecords.Select(static record => record.FieldAERawHex)))} |");
            builder.AppendLine($"| `CNUM.0xAF packed values` | {Escape(FormatNullableIntListDistribution(resources.CnumRecords.Select(static record => record.FieldAFPackedValues)))} |");
            builder.AppendLine($"| `CNUM.0xAF raw hex` | {Escape(FormatNullableStringDistribution(resources.CnumRecords.Select(static record => record.FieldAFRawHex)))} |");
            builder.AppendLine($"| zero-length marker field ids | {Escape(FormatNullableHexIntDistribution(resources.CnumRecords.SelectMany(static record => record.ZeroLengthMarkerFieldIds).Select(static value => (int?)value)))} |");
            builder.AppendLine();
            builder.AppendLine("| # | Node | 0x51 | 0x48 | 0x40/42/43 | 0x39 colors | 0x44 | CREF records | 0x44 match | 0xA0 | 0xA2..A5 | 0xA6..AD | 0xAE floats | 0xAE raw | 0xAF packed | 0xAF raw | 0xA1 | 0xA1 raw hex | Markers | CREF preview |");
            builder.AppendLine("| ---: | --- | ---: | ---: | --- | --- | ---: | ---: | --- | ---: | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |");
            foreach (var record in resources.CnumRecords.Take(40))
            {
                var markerIds = string.Join(", ", record.ZeroLengthMarkerFieldIds.Select(static id => $"0x{id:X2}"));
                var crefPreview = string.Join(", ", record.CropReferences.Take(4).Select(static reference => $"{reference.TextureIndex}:{reference.CropIndex}"));
                var fieldA2A5 = $"{FormatNullableInt(record.FieldA2)},{FormatNullableInt(record.FieldA3)},{FormatNullableInt(record.FieldA4)},{FormatNullableInt(record.FieldA5)}";
                var fieldA6AD = $"{FormatNullableInt(record.FieldA6)},{FormatNullableInt(record.FieldA7)},{FormatNullableInt(record.FieldA8)},{FormatNullableInt(record.FieldA9)},{FormatNullableInt(record.FieldAA)},{FormatNullableInt(record.FieldAB)},{FormatNullableInt(record.FieldAC)},{FormatNullableInt(record.FieldAD)}";
                builder.AppendLine($"| {record.Index} | {Escape(record.NodeName ?? string.Empty)} | {record.Field51?.ToString() ?? string.Empty} | {record.Field48?.ToString() ?? string.Empty} | `{FormatFloat(record.Field40)},{FormatFloat(record.Field42)},{FormatFloat(record.Field43)}` | `{Escape(FormatColorList(record.Field39Colors))}` | {record.Field44Count?.ToString() ?? string.Empty} | {record.CropReferences.Count} | {Escape(record.CropReferenceCountMatchesField44?.ToString() ?? string.Empty)} | {record.FieldA0?.ToString() ?? string.Empty} | `{Escape(fieldA2A5)}` | `{Escape(fieldA6AD)}` | `{Escape(FormatFloatList(record.FieldAEFloatValues))}` | `{Escape(record.FieldAERawHex ?? string.Empty)}` | `{Escape(FormatIntList(record.FieldAFPackedValues))}` | `{Escape(record.FieldAFRawHex ?? string.Empty)}` | {Escape(record.FieldA1 ?? string.Empty)} | `{Escape(record.FieldA1RawHex ?? string.Empty)}` | {Escape(markerIds)} | {Escape(crefPreview)} |");
            }
        }

        if (resources.CrfdRecords.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("`CRFD` raw resource records:");
            builder.AppendLine();
            builder.AppendLine($"CRFD records: {resources.CrfdRecords.Count}; `0x51` node-index in range: {resources.CrfdRecords.Count(static record => record.NodeName is not null)}; out of range: {resources.CrfdRecords.Count(static record => record.Field51 is not null && record.NodeName is null)}; missing: {resources.CrfdRecords.Count(static record => record.Field51 is null)}; `0x94` non-zero: {resources.CrfdRecords.Count(static record => IsNonZero(record.Field94))}.");
            builder.AppendLine();
            builder.AppendLine("| Field | Distribution |");
            builder.AppendLine("| --- | --- |");
            builder.AppendLine($"| `CRFD.0x90` | {Escape(FormatNullableStringDistribution(resources.CrfdRecords.Select(static record => record.Field90)))} |");
            builder.AppendLine($"| `CRFD.0x90 raw hex` | {Escape(FormatNullableStringDistribution(resources.CrfdRecords.Select(static record => record.Field90RawHex)))} |");
            builder.AppendLine($"| `CRFD.0x91` | {Escape(FormatNullableStringDistribution(resources.CrfdRecords.Select(static record => record.Field91)))} |");
            builder.AppendLine($"| `CRFD.0x91 raw hex` | {Escape(FormatNullableStringDistribution(resources.CrfdRecords.Select(static record => record.Field91RawHex)))} |");
            builder.AppendLine($"| `CRFD.0x92` | {Escape(FormatNullableIntDistribution(resources.CrfdRecords.Select(static record => record.Field92)))} |");
            builder.AppendLine($"| `CRFD.0x93` | {Escape(FormatNullableIntDistribution(resources.CrfdRecords.Select(static record => record.Field93)))} |");
            builder.AppendLine($"| `CRFD.0x94` | {Escape(FormatNullableFloatDistribution(resources.CrfdRecords.Select(static record => record.Field94)))} |");
            builder.AppendLine($"| `CRFD.0x95` | {Escape(FormatNullableIntDistribution(resources.CrfdRecords.Select(static record => record.Field95)))} |");
            builder.AppendLine();
            builder.AppendLine("| # | Node | 0x51 | 0x90 | 0x90 raw hex | 0x91 | 0x91 raw hex | 0x92 | 0x93 | 0x94 | 0x95 |");
            builder.AppendLine("| ---: | --- | ---: | --- | --- | --- | --- | ---: | ---: | ---: | ---: |");
            foreach (var record in resources.CrfdRecords.Take(40))
            {
                builder.AppendLine($"| {record.Index} | {Escape(record.NodeName ?? string.Empty)} | {record.Field51?.ToString() ?? string.Empty} | {Escape(record.Field90 ?? string.Empty)} | `{Escape(record.Field90RawHex ?? string.Empty)}` | {Escape(record.Field91 ?? string.Empty)} | `{Escape(record.Field91RawHex ?? string.Empty)}` | {record.Field92?.ToString() ?? string.Empty} | {record.Field93?.ToString() ?? string.Empty} | {FormatFloat(record.Field94)} | {record.Field95?.ToString() ?? string.Empty} |");
            }
        }

        if (resources.TextRecords.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("`TEXT` raw records:");
            builder.AppendLine();
            builder.AppendLine($"TEXT records: {resources.TextRecords.Count}; `0x7A` string present: {resources.TextRecords.Count(static record => !string.IsNullOrEmpty(record.Field7A))}; zero-length marker ids: {Escape(FormatNullableHexIntDistribution(resources.TextRecords.SelectMany(static record => record.ZeroLengthMarkerFieldIds).Select(static value => (int?)value)))}.");
            builder.AppendLine();
            builder.AppendLine("| Field | Distribution |");
            builder.AppendLine("| --- | --- |");
            builder.AppendLine($"| `TEXT.0x33 vector` | {Escape(FormatNullableStringDistribution(resources.TextRecords.Select(static record => FormatVector(record.Field33Vector))))} |");
            builder.AppendLine($"| `TEXT.0x33 raw hex` | {Escape(FormatNullableStringDistribution(resources.TextRecords.Select(static record => record.Field33RawHex)))} |");
            builder.AppendLine($"| `TEXT.0x41` | {Escape(FormatNullableIntDistribution(resources.TextRecords.Select(static record => record.Field41)))} |");
            builder.AppendLine($"| `TEXT.0x78` | {Escape(FormatNullableIntDistribution(resources.TextRecords.Select(static record => record.Field78)))} |");
            builder.AppendLine($"| `TEXT.0x79` | {Escape(FormatNullableIntDistribution(resources.TextRecords.Select(static record => record.Field79)))} |");
            builder.AppendLine($"| `TEXT.0x7C` | {Escape(FormatNullableIntDistribution(resources.TextRecords.Select(static record => record.Field7C)))} |");
            builder.AppendLine($"| `TEXT.0x7A Shift-JIS preview` | {Escape(FormatNullableStringDistribution(resources.TextRecords.Select(static record => record.Field7AShiftJis)))} |");
            builder.AppendLine($"| `TEXT.0x7A raw hex` | {Escape(FormatNullableStringDistribution(resources.TextRecords.Select(static record => record.Field7ARawHex)))} |");
            builder.AppendLine($"| `TEXT.0x7B packed values` | {Escape(FormatNullableIntListDistribution(resources.TextRecords.Select(static record => record.Field7BPackedValues)))} |");
            builder.AppendLine($"| `TEXT.0x7B raw hex` | {Escape(FormatNullableStringDistribution(resources.TextRecords.Select(static record => record.Field7BRawHex)))} |");
            builder.AppendLine();
            builder.AppendLine("| # | 0x33 vector | 0x33 raw hex | 0x41 | 0x78 | 0x79 | 0x7C | 0x7A Shift-JIS preview | 0x7A UTF-8 preview | 0x7A raw hex | 0x7B packed values | 0x7B raw hex | Markers |");
            builder.AppendLine("| ---: | --- | --- | ---: | ---: | ---: | ---: | --- | --- | --- | --- | --- | --- |");
            foreach (var record in resources.TextRecords.Take(40))
            {
                var markerIds = string.Join(", ", record.ZeroLengthMarkerFieldIds.Select(static id => $"0x{id:X2}"));
                var shiftJisPreview = record.Field7AShiftJis is { Length: > 48 } shiftJisValue ? shiftJisValue[..48] + "..." : record.Field7AShiftJis ?? string.Empty;
                var utf8Preview = record.Field7A is { Length: > 48 } utf8Value ? utf8Value[..48] + "..." : record.Field7A ?? string.Empty;
                var packedValues = FormatIntList(record.Field7BPackedValues);
                builder.AppendLine($"| {record.Index} | `{FormatVector(record.Field33Vector)}` | `{Escape(record.Field33RawHex ?? string.Empty)}` | {record.Field41?.ToString() ?? string.Empty} | {record.Field78?.ToString() ?? string.Empty} | {record.Field79?.ToString() ?? string.Empty} | {record.Field7C?.ToString() ?? string.Empty} | {Escape(shiftJisPreview)} | {Escape(utf8Preview)} | `{Escape(record.Field7ARawHex ?? string.Empty)}` | `{Escape(packedValues)}` | `{Escape(record.Field7BRawHex ?? string.Empty)}` | {Escape(markerIds)} |");
            }
        }

        if (resources.SliceCasts.Count > 0)
        {
            var sliceRecords = resources.SliceCasts.SelectMany(static sliceCast => sliceCast.Slices).ToArray();
            builder.AppendLine();
            builder.AppendLine("`CSLI/SLIC` slice 记录结构：");
            builder.AppendLine();
            builder.AppendLine($"Slice casts: {resources.SliceCasts.Count}；SLIC records: {sliceRecords.Length}；CSLI CREF records: {resources.SliceCasts.Sum(static sliceCast => sliceCast.CropReferences.Count)}；`CSLI.0x44` vs SLIC records matches: {resources.SliceCasts.Count(static sliceCast => sliceCast.SlicRecordCountMatchesField44 == true)}；SLIC mismatches: {resources.SliceCasts.Count(static sliceCast => sliceCast.SlicRecordCountMatchesField44 == false)}；`CSLI.0x44` vs CREF records matches: {resources.SliceCasts.Count(static sliceCast => sliceCast.CropReferenceCountMatchesField44 == true)}；CREF mismatches: {resources.SliceCasts.Count(static sliceCast => sliceCast.CropReferenceCountMatchesField44 == false)}；target index in range: {resources.SliceCasts.Count(static sliceCast => sliceCast.NodeName is not null)}。");
            builder.AppendLine();
            builder.AppendLine("| Field | Distribution |");
            builder.AppendLine("| --- | --- |");
            builder.AppendLine($"| `CSLI.0x40` | {Escape(FormatNullableFloatDistribution(resources.SliceCasts.Select(static sliceCast => sliceCast.Field40)))} |");
            builder.AppendLine($"| `CSLI.0x41` | {Escape(FormatNullableFloatDistribution(resources.SliceCasts.Select(static sliceCast => sliceCast.Field41)))} |");
            builder.AppendLine($"| `CSLI.0x42` | {Escape(FormatNullableFloatDistribution(resources.SliceCasts.Select(static sliceCast => sliceCast.Field42)))} |");
            builder.AppendLine($"| `CSLI.0x43` | {Escape(FormatNullableFloatDistribution(resources.SliceCasts.Select(static sliceCast => sliceCast.Field43)))} |");
            builder.AppendLine($"| `CSLI.0x80` | {Escape(FormatNullableIntDistribution(resources.SliceCasts.Select(static sliceCast => sliceCast.Field80)))} |");
            builder.AppendLine($"| `CSLI.0x81` | {Escape(FormatNullableIntDistribution(resources.SliceCasts.Select(static sliceCast => sliceCast.Field81)))} |");
            builder.AppendLine($"| `CSLI.0x82` | {Escape(FormatNullableIntDistribution(resources.SliceCasts.Select(static sliceCast => sliceCast.Field82)))} |");
            builder.AppendLine($"| `CSLI.0x84` | {Escape(FormatNullableIntDistribution(resources.SliceCasts.Select(static sliceCast => sliceCast.Field84)))} |");
            builder.AppendLine($"| `CSLI.0x85` | {Escape(FormatNullableIntDistribution(resources.SliceCasts.Select(static sliceCast => sliceCast.Field85)))} |");
            builder.AppendLine($"| `CSLI.0x86` | {Escape(FormatNullableFloatDistribution(resources.SliceCasts.Select(static sliceCast => sliceCast.Field86)))} |");
            builder.AppendLine($"| `CSLI.0x87` | {Escape(FormatNullableFloatDistribution(resources.SliceCasts.Select(static sliceCast => sliceCast.Field87)))} |");
            builder.AppendLine($"| `SLIC.0x83` | {Escape(FormatNullableIntDistribution(sliceRecords.Select(static slice => slice.Field83)))} |");
            builder.AppendLine($"| `SLIC.0x40` | {Escape(FormatNullableIntDistribution(sliceRecords.Select(static slice => slice.Field40)))} |");
            builder.AppendLine($"| `SLIC.0x41` | {Escape(FormatNullableIntDistribution(sliceRecords.Select(static slice => slice.Field41)))} |");
            builder.AppendLine($"| `SLIC.0x45` | {Escape(FormatNullableIntDistribution(sliceRecords.Select(static slice => slice.Field45)))} |");
            builder.AppendLine($"| `SLIC.0x37 color` | {Escape(FormatColorDistribution(sliceRecords.Select(static slice => slice.Field37Color)))} |");
            builder.AppendLine($"| `SLIC.0x37 raw hex` | {Escape(FormatNullableStringDistribution(sliceRecords.Select(static slice => slice.Field37RawHex)))} |");
            builder.AppendLine($"| `SLIC.0x38 color` | {Escape(FormatColorDistribution(sliceRecords.Select(static slice => slice.Field38Color)))} |");
            builder.AppendLine($"| `SLIC.0x38 raw hex` | {Escape(FormatNullableStringDistribution(sliceRecords.Select(static slice => slice.Field38RawHex)))} |");
            builder.AppendLine($"| `SLIC.0x39 colors` | {Escape(FormatNullableColorListDistribution(sliceRecords.Select(static slice => slice.Field39Colors)))} |");
            builder.AppendLine($"| `SLIC.0x39 raw hex` | {Escape(FormatNullableStringListDistribution(sliceRecords.Select(static slice => slice.Field39RawHexValues)))} |");
            builder.AppendLine();
            builder.AppendLine("| # | Node | Target | CSLI.0x44 | SLIC records | CREF records | 0x44 vs SLIC | 0x44 vs CREF | CSLI 0x40..0x43 | CSLI 0x80..0x87 | SLIC preview |");
            builder.AppendLine("| ---: | --- | ---: | ---: | ---: | ---: | --- | --- | --- | --- | --- |");
            foreach (var sliceCast in resources.SliceCasts.Take(40))
            {
                var slicePreview = string.Join(", ", sliceCast.Slices
                    .Take(6)
                    .Select(static slice => $"#{slice.Index}:83={slice.Field83?.ToString() ?? "?"},40={slice.Field40?.ToString() ?? "?"},41={slice.Field41?.ToString() ?? "?"},45={slice.Field45?.ToString() ?? "?"},37={slice.Field37Color?.Hex ?? "?"},39={FormatColorList(slice.Field39Colors)},38={slice.Field38Color?.Hex ?? "?"}"));
                var csliTail = $"{FormatNullableInt(sliceCast.Field80)},{FormatNullableInt(sliceCast.Field81)},{FormatNullableInt(sliceCast.Field82)},{FormatNullableInt(sliceCast.Field84)},{FormatNullableInt(sliceCast.Field85)},{FormatFloat(sliceCast.Field86)},{FormatFloat(sliceCast.Field87)}";
                builder.AppendLine($"| {sliceCast.Index} | {Escape(sliceCast.NodeName ?? string.Empty)} | {sliceCast.TargetIndex?.ToString() ?? string.Empty} | {sliceCast.Field44Count?.ToString() ?? string.Empty} | {sliceCast.Slices.Count} | {sliceCast.CropReferences.Count} | {Escape(sliceCast.SlicRecordCountMatchesField44?.ToString() ?? string.Empty)} | {Escape(sliceCast.CropReferenceCountMatchesField44?.ToString() ?? string.Empty)} | `{FormatFloat(sliceCast.Field40)},{FormatFloat(sliceCast.Field41)},{FormatFloat(sliceCast.Field42)},{FormatFloat(sliceCast.Field43)}` | `{Escape(csliTail)}` | {Escape(slicePreview)} |");
            }
        }

        builder.AppendLine();
        if (file.Surfboard.NodeCategoryRecords.Count > 0)
        {
            var categorySummary = string.Join(", ", file.Surfboard.NodeCategoryRecords
                .GroupBy(static value => value)
                .OrderBy(static group => group.Key)
                .Select(static group => $"{group.Key}:{group.Count()}"));
            builder.AppendLine($"`NCAT.0x0E` 节点分类记录数 {file.Surfboard.NodeCategoryRecords.Count}，分布：{Escape(categorySummary)}。");
            builder.AppendLine();
        }
    }

    private static void AppendCamera(StringBuilder builder, SbSceneFile file)
    {
        if (file.Surfboard.Camera is not { } camera)
        {
            return;
        }

        builder.AppendLine("## Camera");
        builder.AppendLine();
        builder.AppendLine("| Name | Position | Target | Flags | Near | Far |");
        builder.AppendLine("| --- | --- | --- | ---: | ---: | ---: |");
        builder.AppendLine($"| {Escape(camera.Name ?? string.Empty)} | {FormatVector(camera.Position)} | {FormatVector(camera.Target)} | `0x{(camera.Flags ?? 0):X}` | {FormatFloat(camera.NearClip)} | {FormatFloat(camera.FarClip)} |");
        builder.AppendLine();
    }

    private static void AppendAnimations(StringBuilder builder, SbSceneFile file)
    {
        builder.AppendLine("## 动画列表");
        builder.AppendLine();
        builder.AppendLine("| # | Name | Motions | Tracks | Keys | Track Types |");
        builder.AppendLine("| ---: | --- | ---: | ---: | ---: | --- |");
        foreach (var animation in file.Surfboard.Animations)
        {
            var tracks = animation.Motions.SelectMany(static motion => motion.Tracks).ToArray();
            var trackCount = tracks.Length;
            var keyCount = tracks.Sum(static track => track.Keyframes.Count);
            var trackTypes = tracks
                .Where(static track => track.TrackType is not null)
                .GroupBy(static track => track.TrackType!.Value)
                .OrderByDescending(static group => group.Count())
                .ThenBy(static group => group.Key)
                .Take(8)
                .Select(static group =>
                {
                    var name = group.Select(static track => track.TrackTypeName).FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)) ?? "?";
                    return $"{group.Key}({name}):{group.Count()}";
                });
            builder.AppendLine(
                $"| {animation.Index} | {Escape(animation.Name ?? $"ANIM@0x{animation.Offset:X}")} | {animation.Motions.Count} | {trackCount} | {keyCount} | {Escape(string.Join(", ", trackTypes))} |");
        }

        builder.AppendLine();
    }

    private static void AppendAnimationBindings(StringBuilder builder, SbSceneFile file)
    {
        builder.AppendLine("## 动画到节点绑定样例");
        builder.AppendLine();
        builder.AppendLine("| Animation | Motion | Node | Tracks | Keys | Track Types | Track Type Names |");
        builder.AppendLine("| --- | ---: | --- | ---: | ---: | --- | --- |");
        foreach (var binding in SelectBindingSample(file.Surfboard.AnimationBindings).Take(120))
        {
            builder.AppendLine(
                $"| {Escape(binding.AnimationName)} | {binding.MotionIndex} | `{binding.NodeIndex}` {Escape(binding.NodeName ?? string.Empty)} | {binding.TrackCount} | {binding.KeyCount} | {Escape(string.Join(", ", binding.TrackTypes))} | {Escape(string.Join(", ", binding.TrackTypeNames))} |");
        }

        builder.AppendLine();
    }

    private static void AppendStateTrackSummary(StringBuilder builder, SbSceneFile file)
    {
        var rows = BuildStateTrackRows(file);
        if (rows.Count == 0)
        {
            return;
        }

        builder.AppendLine("## 状态/开关轨道摘要");
        builder.AppendLine();
        builder.AppendLine("以下表格只整理已经解析出的状态候选轨道，不表示运行时状态机已经完全确认。`ImageRefCheck` 对 type 18/19 分别按 primary/secondary CREF 组检查 key value 是否落在范围内。");
        builder.AppendLine();
        builder.AppendLine("| Track type | Name | Tracks | Keys |");
        builder.AppendLine("| ---: | --- | ---: | ---: |");
        foreach (var group in rows
            .GroupBy(static row => new { row.TrackType, row.TrackTypeName })
            .OrderBy(static group => group.Key.TrackType))
        {
            builder.AppendLine($"| {group.Key.TrackType?.ToString() ?? string.Empty} | {Escape(group.Key.TrackTypeName ?? string.Empty)} | {group.Count()} | {group.Sum(static row => row.KeyCount)} |");
        }

        builder.AppendLine();
        builder.AppendLine("状态轨道按动画分布：");
        builder.AppendLine();
        builder.AppendLine("| Animation | Display | Primary variant | Secondary variant | Alpha/Opacity |");
        builder.AppendLine("| --- | ---: | ---: | ---: | ---: |");
        foreach (var group in rows.GroupBy(static row => row.AnimationName, StringComparer.Ordinal)
            .OrderBy(static group => group.First().AnimationIndex))
        {
            builder.AppendLine(
                $"| {Escape(group.Key)} | {group.Count(static row => row.TrackType == 11)} | {group.Count(static row => row.TrackType == 18)} | {group.Count(static row => row.TrackType == 19)} | {group.Count(static row => row.TrackType == 24)} |");
        }

        builder.AppendLine();
        builder.AppendLine("重点状态轨道样例：");
        builder.AppendLine();
        builder.AppendLine("| Animation | Node | Type | Keys | Values | Image refs | ImageRefCheck |");
        builder.AppendLine("| --- | --- | --- | ---: | --- | --- | --- |");
        foreach (var row in rows
            .Where(static row => IsFocusedStateAnimation(row.AnimationName))
            .Take(160))
        {
            builder.AppendLine(
                $"| {Escape(row.AnimationName)} | `{row.NodeIndex?.ToString() ?? string.Empty}` {Escape(row.NodeName ?? string.Empty)} | {row.TrackType}({Escape(row.TrackTypeName ?? string.Empty)}) | {row.KeyCount} | {Escape(row.Values)} | {Escape(row.ImageReferenceSummary ?? string.Empty)} | {FormatNullableBool(row.ImageValuesWithinReferenceCount)} |");
        }

        builder.AppendLine();
    }

    private static void AppendTrackStorage(StringBuilder builder, SbSceneFile file)
    {
        var tracks = file.Surfboard.Animations.SelectMany(static animation => animation.Motions).SelectMany(static motion => motion.Tracks).ToArray();
        builder.AppendLine("## Track flags 与 key value 存储");
        builder.AppendLine();
        builder.AppendLine("| Flags | Base byte | Extra mask | Low nibble | Storage nibble | Storage | Tracks | KEY.0x5B types | 常见 Track Types |");
        builder.AppendLine("| ---: | ---: | ---: | ---: | ---: | --- | ---: | --- | --- |");
        foreach (var group in tracks
            .Where(static track => track.Flags is not null)
            .GroupBy(static track => new { track.Flags, track.KeyValueStorage })
            .OrderBy(static group => group.Key.Flags))
        {
            var flags = group.Key.Flags!.Value;
            var valueTypes = FormatKeyValueTypeDistribution(group.SelectMany(static track => track.Keyframes));
            var typeSummary = string.Join(", ", group
                .Where(static track => track.TrackType is not null)
                .GroupBy(static track => track.TrackType!.Value)
                .OrderByDescending(static typeGroup => typeGroup.Count())
                .Take(8)
                .Select(static typeGroup =>
                {
                    var name = typeGroup.Select(static track => track.TrackTypeName).FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)) ?? "?";
                    return $"{typeGroup.Key}({name}):{typeGroup.Count()}";
                }));
            builder.AppendLine($"| `0x{flags:X}` | `0x{GetTrackFlagBaseByte(flags):X2}` | `0x{GetTrackFlagExtraMask(flags):X}` | `0x{GetTrackFlagLowNibble(flags):X}` | `0x{GetTrackFlagStorageNibble(flags):X}` | {Escape(group.Key.KeyValueStorage ?? string.Empty)} | {group.Count()} | {Escape(valueTypes)} | {Escape(typeSummary)} |");
        }

        builder.AppendLine();
        builder.AppendLine("`TRK.flags` 拆分分布：");
        builder.AppendLine();
        builder.AppendLine("| Part | Value | Tracks | Storage / KEY.0x5B types |");
        builder.AppendLine("| --- | ---: | ---: | --- |");
        foreach (var group in tracks
            .Where(static track => track.Flags is not null)
            .GroupBy(static track => GetTrackFlagLowNibble(track.Flags!.Value))
            .OrderBy(static group => group.Key))
        {
            var valueTypes = FormatKeyValueTypeDistribution(group.SelectMany(static track => track.Keyframes));
            var storages = string.Join(", ", group
                .Select(static track => track.KeyValueStorage ?? string.Empty)
                .Where(static value => value.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static value => value, StringComparer.Ordinal));
            builder.AppendLine($"| Low nibble | `0x{group.Key:X}` | {group.Count()} | {Escape(string.Join("; ", new[] { storages, valueTypes }.Where(static value => value.Length > 0)))} |");
        }

        foreach (var group in tracks
            .Where(static track => track.Flags is not null)
            .GroupBy(static track => GetTrackFlagStorageNibble(track.Flags!.Value))
            .OrderBy(static group => group.Key))
        {
            var valueTypes = FormatKeyValueTypeDistribution(group.SelectMany(static track => track.Keyframes));
            var storages = string.Join(", ", group
                .Select(static track => track.KeyValueStorage ?? string.Empty)
                .Where(static value => value.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static value => value, StringComparer.Ordinal));
            builder.AppendLine($"| Storage nibble | `0x{group.Key:X}` | {group.Count()} | {Escape(string.Join("; ", new[] { storages, valueTypes }.Where(static value => value.Length > 0)))} |");
        }

        foreach (var group in tracks
            .Where(static track => track.Flags is not null)
            .GroupBy(static track => GetTrackFlagExtraMask(track.Flags!.Value))
            .OrderBy(static group => group.Key))
        {
            var valueTypes = FormatKeyValueTypeDistribution(group.SelectMany(static track => track.Keyframes));
            var storages = string.Join(", ", group
                .Select(static track => track.KeyValueStorage ?? string.Empty)
                .Where(static value => value.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static value => value, StringComparer.Ordinal));
            builder.AppendLine($"| Extra mask | `0x{group.Key:X}` | {group.Count()} | {Escape(string.Join("; ", new[] { storages, valueTypes }.Where(static value => value.Length > 0)))} |");
        }

        builder.AppendLine();
    }

    private static void AppendTrackTypeEvidence(StringBuilder builder, SbSceneFile file)
    {
        var contexts = BuildTrackContexts(file);

        builder.AppendLine("## Track type 证据表");
        builder.AppendLine();
        builder.AppendLine("| Type | Name | Tracks | Keys | Flags | KEY.0x5B types | Interpolation | Value range | Examples |");
        builder.AppendLine("| ---: | --- | ---: | ---: | --- | --- | --- | --- | --- |");
        foreach (var group in contexts
            .Where(static context => context.Track.TrackType is not null)
            .GroupBy(static context => new { context.Track.TrackType, context.Track.TrackTypeName })
            .OrderBy(static group => group.Key.TrackType))
        {
            var tracks = group.Select(static context => context.Track).ToArray();
            var keys = tracks.SelectMany(static track => track.Keyframes).ToArray();
            var flags = string.Join(", ", tracks
                .Where(static track => track.Flags is not null)
                .GroupBy(static track => track.Flags!.Value)
                .OrderByDescending(static flagGroup => flagGroup.Count())
                .ThenBy(static flagGroup => flagGroup.Key)
                .Take(6)
                .Select(static flagGroup => $"0x{flagGroup.Key:X2}:{flagGroup.Count()}"));
            var interpolation = string.Join(", ", keys
                .Where(static key => key.Interpolation is not null)
                .GroupBy(static key => new { key.Interpolation, key.InterpolationName })
                .OrderByDescending(static interpolationGroup => interpolationGroup.Count())
                .ThenBy(static interpolationGroup => interpolationGroup.Key.Interpolation)
                .Take(5)
                .Select(static interpolationGroup => $"{interpolationGroup.Key.Interpolation}({interpolationGroup.Key.InterpolationName ?? "?"}):{interpolationGroup.Count()}"));
            var examples = string.Join(", ", group
                .Select(static context => $"{context.AnimationName}->{context.NodeName ?? "?"}")
                .Distinct(StringComparer.Ordinal)
                .Take(5));

            builder.AppendLine(
                $"| {group.Key.TrackType?.ToString() ?? string.Empty} | {Escape(group.Key.TrackTypeName ?? string.Empty)} | {tracks.Length} | {keys.Length} | {Escape(flags)} | {Escape(FormatKeyValueTypeDistribution(keys))} | {Escape(interpolation)} | {Escape(FormatTrackValueRange(group.Key.TrackType, keys))} | {Escape(examples)} |");
        }

        builder.AppendLine();
    }

    private static void AppendColorTrackEvidence(StringBuilder builder, SbSceneFile file)
    {
        var rows = BuildColorTrackEvidenceRows(file);
        if (rows.Count == 0)
        {
            return;
        }

        builder.AppendLine("## 颜色 track 证据");
        builder.AppendLine();
        builder.AppendLine("`TRS2.0x37/0x38/0x39` 的 4 字节颜色当前按 `A,R,G,B` 候选解释。下表把颜色相关 track 的 key value 与目标节点初始颜色通道交叉验证；`Initial matches` 使用 `<= 0.01` 的近似阈值。");
        builder.AppendLine();
        builder.AppendLine("| Type | Name | Tracks | Keys | Nodes | Initial matches | Initial channel values | Examples |");
        builder.AppendLine("| ---: | --- | ---: | ---: | ---: | ---: | --- | --- |");
        foreach (var group in rows
            .GroupBy(static row => new { row.TrackType, row.TrackTypeName })
            .OrderBy(static group => group.Key.TrackType))
        {
            var examples = string.Join(", ", group
                .Take(5)
                .Select(static row => $"{row.AnimationName}->{row.NodeName ?? "?"}: init={FormatDouble(row.InitialChannelValue)} keys=[{row.KeyValues}]"));
            var initialValues = FormatNumberRange(group.Select(static row => row.InitialChannelValue));
            builder.AppendLine($"| {group.Key.TrackType} | {Escape(group.Key.TrackTypeName ?? string.Empty)} | {group.Count()} | {group.Sum(static row => row.KeyCount)} | {group.Select(static row => row.NodeIndex).Distinct().Count()} | {group.Count(static row => row.InitialValueMatched)} / {group.Count()} | {Escape(initialValues)} | {Escape(examples)} |");
        }

        builder.AppendLine();
    }

    private static void AppendAlphaOpacityEvidence(StringBuilder builder, SbSceneFile file)
    {
        var rows = BuildAlphaOpacityEvidenceRows(file);
        if (rows.Count == 0)
        {
            return;
        }

        builder.AppendLine("## Alpha/Opacity track 证据");
        builder.AppendLine();
        builder.AppendLine("`type 24(AlphaOrOpacity)` 仍按 opacity/alpha 候选保留。下表把它与目标节点初始材质 alpha、display、CIMG 绑定和动画类别交叉统计；`Material alpha matches` 使用 `<= 0.01` 的近似阈值。");
        builder.AppendLine();
        builder.AppendLine($"总计：tracks {rows.Count}，keys {rows.Sum(static row => row.KeyCount)}，CIMG targets {rows.Count(static row => row.HasImageCast)}，display=false targets {rows.Count(static row => row.InitialDisplay == false)}，material alpha matches {rows.Count(static row => row.InitialAlphaMatched)} / {rows.Count(static row => row.InitialMaterialAlpha is not null)}。");
        builder.AppendLine();
        builder.AppendLine("| Animation | Tracks | Keys | Nodes | CIMG nodes | Display=false | Material alpha matches | Initial material alpha | Examples |");
        builder.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- |");
        foreach (var group in rows
            .GroupBy(static row => row.AnimationName, StringComparer.Ordinal)
            .OrderBy(static group => group.Key, StringComparer.Ordinal))
        {
            var initialAlpha = FormatNumberRange(group
                .Select(static row => row.InitialMaterialAlpha)
                .Where(static value => value is not null)
                .Select(static value => value!.Value));
            var examples = string.Join(", ", group
                .Take(5)
                .Select(static row => $"{row.NodeName ?? "?"}: alpha={FormatDouble(row.InitialMaterialAlpha)} display={FormatBool(row.InitialDisplay)} cimg={row.HasImageCast} keys=[{row.KeyValues}]"));
            builder.AppendLine($"| {Escape(group.Key)} | {group.Count()} | {group.Sum(static row => row.KeyCount)} | {group.Select(static row => row.NodeIndex).Distinct().Count()} | {group.Count(static row => row.HasImageCast)} | {group.Count(static row => row.InitialDisplay == false)} | {group.Count(static row => row.InitialAlphaMatched)} / {group.Count(static row => row.InitialMaterialAlpha is not null)} | {Escape(initialAlpha)} | {Escape(examples)} |");
        }

        builder.AppendLine();
        builder.AppendLine("Alpha/Opacity track 样例：");
        builder.AppendLine();
        builder.AppendLine("| Animation | Node | Flags | Group | CIMG | Display | Material alpha | Keys |");
        builder.AppendLine("| --- | --- | ---: | --- | ---: | --- | ---: | --- |");
        foreach (var row in rows
            .Where(static row => IsFocusedStateAnimation(row.AnimationName) || row.AnimationName.StartsWith("Action_", StringComparison.OrdinalIgnoreCase))
            .Take(80))
        {
            builder.AppendLine($"| {Escape(row.AnimationName)} | `{row.NodeIndex}` {Escape(row.NodeName ?? string.Empty)} | {Escape(row.NodeFlags is null ? string.Empty : $"0x{row.NodeFlags.Value:X}")} | {Escape(row.NodeGroup ?? string.Empty)} | {row.HasImageCast} | {Escape(FormatBool(row.InitialDisplay))} | {FormatDouble(row.InitialMaterialAlpha)} | {Escape(row.KeyValues)} |");
        }

        builder.AppendLine();
    }

    private static void AppendTrackFlagExtraEvidence(StringBuilder builder, SbSceneFile file)
    {
        var contexts = BuildTrackContexts(file)
            .Where(static context => context.Track.Flags is not null && GetTrackFlagExtraMask(context.Track.Flags!.Value) != 0)
            .ToArray();
        if (contexts.Length == 0)
        {
            return;
        }

        builder.AppendLine("## Track flags extra mask 候选");
        builder.AppendLine();
        builder.AppendLine("`extra mask = flags & ~0xFF`，当前只作为额外 track flag 保留；base byte 仍决定 `KEY.0x5B` 的存储类型。");
        builder.AppendLine();
        builder.AppendLine("| Extra mask | Tracks | Flags | Animations | Nodes | Node flags | Groups | CIMG targets | Display=false | Track types | Key values |");
        builder.AppendLine("| ---: | ---: | --- | --- | --- | --- | --- | ---: | ---: | --- | --- |");
        var imageCastNodeIndexes = file.Surfboard.Resources.ImageCasts
            .Select(static imageCast => imageCast.CastIndex)
            .ToHashSet();
        foreach (var group in contexts
            .GroupBy(static context => GetTrackFlagExtraMask(context.Track.Flags!.Value))
            .OrderBy(static group => group.Key))
        {
            var items = group.ToArray();
            var flags = FormatContextDistribution(items, static context => $"0x{context.Track.Flags!.Value:X}");
            var animations = FormatContextDistribution(items, static context => context.AnimationName);
            var nodes = FormatContextDistribution(items, static context => context.NodeName ?? "?");
            var nodeFlags = FormatTrackContextNodeFlagDistribution(items, file.Surfboard.Nodes);
            var groups = FormatTrackContextNodeGroupDistribution(items, file.Surfboard.Nodes);
            var cimgTargets = items.Count(context => context.NodeIndex is int nodeIndex && imageCastNodeIndexes.Contains(nodeIndex));
            var displayFalse = items.Count(context => ResolveNode(file.Surfboard.Nodes, context.NodeIndex ?? -1)?.Transform2D?.Display == false);
            var trackTypes = FormatContextDistribution(items, static context => $"{context.Track.TrackType}({context.Track.TrackTypeName ?? "?"})");
            var values = string.Join("; ", items.Take(8).Select(static context => FormatKeyframeSequence(context.Track)));
            builder.AppendLine($"| `0x{group.Key:X}` | {items.Length} | {Escape(flags)} | {Escape(animations)} | {Escape(nodes)} | {Escape(nodeFlags)} | {Escape(groups)} | {cimgTargets} | {displayFalse} | {Escape(trackTypes)} | {Escape(values)} |");
        }

        builder.AppendLine();
        builder.AppendLine("Extra mask track 样例：");
        builder.AppendLine();
        builder.AppendLine("| Animation | Node | Flags | Type | Frames | Keys |");
        builder.AppendLine("| --- | --- | ---: | --- | --- | --- |");
        foreach (var context in contexts.Take(48))
        {
            var track = context.Track;
            var frames = $"{track.FirstFrame?.ToString() ?? "?"}..{track.LastFrame?.ToString() ?? "?"}";
            builder.AppendLine($"| {Escape(context.AnimationName)} | `{context.NodeIndex?.ToString() ?? string.Empty}` {Escape(context.NodeName ?? string.Empty)} | `0x{track.Flags!.Value:X}` | {track.TrackType}({Escape(track.TrackTypeName ?? string.Empty)}) | {Escape(frames)} | {Escape(FormatKeyframeSequence(track))} |");
        }

        builder.AppendLine();
    }

    private static void AppendPackedAngleCandidates(StringBuilder builder, SbSceneFile file)
    {
        var contexts = BuildTrackContexts(file);
        var transformAngles = file.Surfboard.Transform2DRecords
            .Where(static transform => transform.RotationZRaw is not null)
            .ToArray();
        var packedTracks = contexts
            .Select(static context => context.Track)
            .Where(static track => track.Keyframes.Any(static key => key.PackedAngleRaw is not null))
            .ToArray();
        var packedKeys = packedTracks.SelectMany(static track => track.Keyframes)
            .Where(static key => key.PackedAngleRaw is not null)
            .ToArray();
        if (transformAngles.Length == 0 && packedKeys.Length == 0)
        {
            return;
        }

        builder.AppendLine("## Packed angle 候选");
        builder.AppendLine();
        builder.AppendLine("`0x0B` 在旋转上下文中按 signed fixed-angle 候选解释：`degrees = raw * 180 / 32768`。这不是全局确认；例如 `CAM.0x14` 仍按 flags-like/未知处理。");
        builder.AppendLine();
        builder.AppendLine("渲染时还要把 scene/source 角度转换到 2D 屏幕坐标矩阵；当前 CLI PNG renderer 和 Viewer 使用 `-degrees`，这一步与 raw 角度解码分开。");
        builder.AppendLine();
        builder.AppendLine("| Context | Values | Track types | Raw examples | Degree examples |");
        builder.AppendLine("| --- | ---: | --- | --- | --- |");
        if (transformAngles.Length > 0)
        {
            var rawValues = transformAngles.Select(static transform => transform.RotationZRaw).ToArray();
            builder.AppendLine($"| `TRS2.0x32` | {transformAngles.Length} | RotateZ transform | {Escape(FormatPackedAngleRawExamples(rawValues))} | {Escape(FormatPackedAngleDegreeExamples(rawValues))} |");
        }

        if (packedKeys.Length > 0)
        {
            var typeSummary = string.Join(", ", packedTracks
                .Where(static track => track.TrackType is not null)
                .GroupBy(static track => new { track.TrackType, track.TrackTypeName })
                .OrderByDescending(static group => group.Count())
                .ThenBy(static group => group.Key.TrackType)
                .Select(static group => $"{group.Key.TrackType}({group.Key.TrackTypeName ?? "?"}):{group.Count()}"));
            var rawValues = packedKeys.Select(static key => key.PackedAngleRaw).ToArray();
            builder.AppendLine($"| `KEY.0x5B type 0x0B` | {packedKeys.Length} keys / {packedTracks.Length} tracks | {Escape(typeSummary)} | {Escape(FormatPackedAngleRawExamples(rawValues))} | {Escape(FormatPackedAngleDegreeExamples(rawValues))} |");
        }

        if (file.Surfboard.Camera?.Flags is int cameraFlags)
        {
            builder.AppendLine($"| `CAM.0x14` | 1 | excluded | `0x{cameraFlags:X}` | flags-like, not converted |");
        }

        builder.AppendLine();
    }

    private static void AppendKeyInterpolation(StringBuilder builder, SbSceneFile file)
    {
        var contexts = BuildTrackContexts(file);
        var tracks = contexts.Select(static context => context.Track).ToArray();
        var keys = tracks.SelectMany(static track => track.Keyframes).ToArray();

        builder.AppendLine("## KEY 插值候选");
        builder.AppendLine();
        builder.AppendLine("| Value | Name | Keys |");
        builder.AppendLine("| ---: | --- | ---: |");
        foreach (var group in keys
            .Where(static key => key.Interpolation is not null)
            .GroupBy(static key => new { key.Interpolation, key.InterpolationName })
            .OrderBy(static group => group.Key.Interpolation))
        {
            builder.AppendLine($"| {group.Key.Interpolation} | {Escape(group.Key.InterpolationName ?? string.Empty)} | {group.Count()} |");
        }

        builder.AppendLine();
        var tangentPairs = keys.Where(static key => key.TangentIn is not null || key.TangentOut is not null).ToArray();
        var sameTangents = tangentPairs.Count(static key => key.TangentIn == key.TangentOut);
        var mismatchedTangents = tangentPairs.Where(static key => key.TangentIn != key.TangentOut).ToArray();
        var nonZeroTangents = tangentPairs.Count(static key => IsNonZero(key.TangentIn) || IsNonZero(key.TangentOut));
        builder.AppendLine($"Tangent 候选：present {tangentPairs.Length}，`0x5D == 0x5E` {sameTangents}，`0x5D != 0x5E` {mismatchedTangents.Length}，非零 {nonZeroTangents}。");
        builder.AppendLine();

        builder.AppendLine("| Interpolation | Name | Keys | Non-zero tangent keys |");
        builder.AppendLine("| ---: | --- | ---: | ---: |");
        foreach (var group in keys
            .Where(static key => key.Interpolation is not null)
            .GroupBy(static key => new { key.Interpolation, key.InterpolationName })
            .OrderBy(static group => group.Key.Interpolation))
        {
            builder.AppendLine($"| {group.Key.Interpolation} | {Escape(group.Key.InterpolationName ?? string.Empty)} | {group.Count()} | {group.Count(static key => IsNonZero(key.TangentIn) || IsNonZero(key.TangentOut))} |");
        }

        builder.AppendLine();
        builder.AppendLine("| Track type | Track type name | Non-zero tangent keys | Tracks |");
        builder.AppendLine("| ---: | --- | ---: | ---: |");
        foreach (var group in tracks
            .Select(track => new
            {
                Track = track,
                NonZero = track.Keyframes.Count(static key => IsNonZero(key.TangentIn) || IsNonZero(key.TangentOut)),
            })
            .Where(static item => item.NonZero > 0)
            .GroupBy(static item => new { item.Track.TrackType, item.Track.TrackTypeName })
            .OrderByDescending(static group => group.Sum(static item => item.NonZero)))
        {
            builder.AppendLine($"| {group.Key.TrackType?.ToString() ?? string.Empty} | {Escape(group.Key.TrackTypeName ?? string.Empty)} | {group.Sum(static item => item.NonZero)} | {group.Count()} |");
        }

        if (mismatchedTangents.Length > 0)
        {
            builder.AppendLine();
            builder.AppendLine("`0x5D != 0x5E` tangent 样例与分布：");
            builder.AppendLine();
            builder.AppendLine("| Interpolation | Name | Mismatched keys |");
            builder.AppendLine("| ---: | --- | ---: |");
            foreach (var group in mismatchedTangents
                .Where(static key => key.Interpolation is not null)
                .GroupBy(static key => new { key.Interpolation, key.InterpolationName })
                .OrderBy(static group => group.Key.Interpolation))
            {
                builder.AppendLine($"| {group.Key.Interpolation} | {Escape(group.Key.InterpolationName ?? string.Empty)} | {group.Count()} |");
            }

            builder.AppendLine();
            builder.AppendLine("| Track type | Track type name | Mismatched keys | Tracks |");
            builder.AppendLine("| ---: | --- | ---: | ---: |");
            foreach (var group in contexts
                .Select(context => new
                {
                    Context = context,
                    Mismatched = context.Track.Keyframes.Count(static key => key.TangentIn != key.TangentOut),
                })
                .Where(static item => item.Mismatched > 0)
                .GroupBy(static item => new { item.Context.Track.TrackType, item.Context.Track.TrackTypeName })
                .OrderByDescending(static group => group.Sum(static item => item.Mismatched)))
            {
                builder.AppendLine($"| {group.Key.TrackType?.ToString() ?? string.Empty} | {Escape(group.Key.TrackTypeName ?? string.Empty)} | {group.Sum(static item => item.Mismatched)} | {group.Count()} |");
            }

            builder.AppendLine();
            builder.AppendLine("| Animation | Node | Track type | Frame | Value | 0x5C | 0x5D | 0x5E |");
            builder.AppendLine("| --- | --- | --- | ---: | ---: | --- | ---: | ---: |");
            foreach (var sample in contexts
                .SelectMany(static context => context.Track.Keyframes
                    .Where(static key => key.TangentIn != key.TangentOut)
                    .Select(key => new { Context = context, Key = key }))
                .Take(24))
            {
                builder.AppendLine(
                    $"| {Escape(sample.Context.AnimationName)} | {Escape(sample.Context.NodeName ?? string.Empty)} | {sample.Context.Track.TrackType}({Escape(sample.Context.Track.TrackTypeName ?? string.Empty)}) | {sample.Key.KeyFrame?.ToString() ?? string.Empty} | {FormatKeyValue(sample.Context.Track, sample.Key)} | {sample.Key.Interpolation}({Escape(sample.Key.InterpolationName ?? string.Empty)}) | {FormatDouble(sample.Key.TangentIn)} | {FormatDouble(sample.Key.TangentOut)} |");
            }
        }

        builder.AppendLine();
        builder.AppendLine("非零 tangent 样例：");
        builder.AppendLine();
        builder.AppendLine("| Animation | Node | Track type | Frame | Value | 0x5C | 0x5D | 0x5E |");
        builder.AppendLine("| --- | --- | --- | ---: | ---: | --- | ---: | ---: |");
        foreach (var sample in contexts
            .SelectMany(static context => context.Track.Keyframes
                .Where(static key => IsNonZero(key.TangentIn) || IsNonZero(key.TangentOut))
                .Select(key => new { Context = context, Key = key }))
            .Take(24))
        {
            builder.AppendLine(
                $"| {Escape(sample.Context.AnimationName)} | {Escape(sample.Context.NodeName ?? string.Empty)} | {sample.Context.Track.TrackType}({Escape(sample.Context.Track.TrackTypeName ?? string.Empty)}) | {sample.Key.KeyFrame?.ToString() ?? string.Empty} | {FormatKeyValue(sample.Context.Track, sample.Key)} | {sample.Key.Interpolation}({Escape(sample.Key.InterpolationName ?? string.Empty)}) | {FormatDouble(sample.Key.TangentIn)} | {FormatDouble(sample.Key.TangentOut)} |");
        }

        builder.AppendLine();
    }

    private static void AppendTrackKeyCounts(StringBuilder builder, SbSceneFile file)
    {
        var tracks = file.Surfboard.Animations.SelectMany(static animation => animation.Motions).SelectMany(static motion => motion.Tracks).ToArray();
        var declared = tracks.Where(static track => track.DeclaredKeyCountFromTrack is not null).ToArray();
        var matching = declared.Count(static track => track.KeyCountMatchesDeclaration == true);
        var mismatching = declared.Length - matching;

        builder.AppendLine("## TRK.0x57 key count");
        builder.AppendLine();
        builder.AppendLine($"`TRK.0x57` 与 `KEY.ParamHigh / 5` 及实际解析 key 数一致：{matching}/{declared.Length}，不一致 {mismatching}。");
        builder.AppendLine();
        builder.AppendLine("| Key count | Tracks |");
        builder.AppendLine("| ---: | ---: |");
        foreach (var group in declared
            .GroupBy(static track => track.DeclaredKeyCountFromTrack!.Value)
            .OrderBy(static group => group.Key))
        {
            builder.AppendLine($"| {group.Key} | {group.Count()} |");
        }

        builder.AppendLine();
    }

    private static void AppendVariantHints(StringBuilder builder, SbSceneFile file)
    {
        builder.AppendLine("## 疑似开关与状态");
        builder.AppendLine();
        builder.AppendLine("| Category | Source | Name | Confidence | Reason |");
        builder.AppendLine("| --- | --- | --- | ---: | --- |");
        foreach (var hint in file.Surfboard.VariantHints)
        {
            builder.AppendLine(
                $"| {Escape(hint.Category)} | {Escape(hint.SourceKind)} | {Escape(hint.Name)} | {hint.Confidence:0.00} | {Escape(hint.Reason)} |");
        }

        builder.AppendLine();
    }

    private static void AppendUnknownFields(StringBuilder builder, SbSceneFile file)
    {
        builder.AppendLine("## 未知字段");
        builder.AppendLine();
        if (file.Surfboard.UnknownFields.Count == 0)
        {
            builder.AppendLine("当前解析结果没有遇到未知 type code。");
            builder.AppendLine();
            return;
        }

        builder.AppendLine("| Owner | Offset | Field | Type | Count | Stride | Preview |");
        builder.AppendLine("| --- | ---: | --- | --- | ---: | ---: | --- |");
        foreach (var field in file.Surfboard.UnknownFields.Take(200))
        {
            builder.AppendLine(
                $"| `{Escape(field.OwnerTag)}` | `0x{field.Offset:X}` | `{field.IdHex}` | `{field.TypeHex}` | {field.Count} | {field.Stride} | {Escape(field.Preview ?? string.Empty)} |");
        }

        if (file.Surfboard.UnknownFields.Count > 200)
        {
            builder.AppendLine();
            builder.AppendLine($"未知字段较多，表格仅显示前 200 项，共 {file.Surfboard.UnknownFields.Count} 项。");
        }

        builder.AppendLine();
    }

    private static void AppendWarnings(StringBuilder builder, SbSceneFile file)
    {
        if (file.Summary.Warnings.Count == 0)
        {
            return;
        }

        builder.AppendLine("## 解析警告");
        builder.AppendLine();
        foreach (var warning in file.Summary.Warnings)
        {
            builder.AppendLine($"- {Escape(warning)}");
        }

        builder.AppendLine();
    }

    private static IEnumerable<AnimationBindingInfo> SelectBindingSample(IReadOnlyList<AnimationBindingInfo> bindings)
    {
        var focused = bindings
            .Where(static binding =>
                binding.AnimationName.StartsWith("Change_", StringComparison.OrdinalIgnoreCase)
                || binding.AnimationName.StartsWith("Mouth", StringComparison.OrdinalIgnoreCase)
                || binding.AnimationName.StartsWith("DressChange", StringComparison.OrdinalIgnoreCase)
                || binding.AnimationName.Equals("Action_Wait1", StringComparison.OrdinalIgnoreCase)
                || binding.AnimationName.Equals("Action_Change", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return focused.Length > 0
            ? focused.GroupBy(static binding => binding.AnimationName, StringComparer.OrdinalIgnoreCase)
                .SelectMany(static group => group.Take(16))
            : bindings;
    }

    private static IReadOnlyList<StateTrackRow> BuildStateTrackRows(SbSceneFile file)
    {
        var imageCastsByNode = file.Surfboard.Resources.ImageCasts
            .Where(static imageCast => imageCast.CastIndex >= 0)
            .GroupBy(static imageCast => imageCast.CastIndex)
            .ToDictionary(static group => group.Key, static group => group.ToArray());
        var rows = new List<StateTrackRow>();

        foreach (var animation in file.Surfboard.Animations)
        {
            foreach (var motion in animation.Motions)
            {
                foreach (var track in motion.Tracks.Where(static track => track.TrackType is 11 or 18 or 19 or 24))
                {
                    var nodeIndex = motion.TargetIndex;
                    imageCastsByNode.TryGetValue(nodeIndex ?? -1, out var imageCasts);
                    var imageReferences = SelectImageReferencesForStateTrack(track.TrackType, imageCasts);
                    var imageReferenceCount = imageReferences?.Length;
                    var imageReferenceSummary = imageReferences is null
                        ? null
                        : string.Join(", ", imageReferences
                            .Take(10)
                            .Select(static reference => $"{reference.TextureIndex}:{reference.CropIndex}"));
                    var scalarValues = track.Keyframes
                        .Select(static key => key.ScalarValue)
                        .Where(static value => value is not null)
                        .Select(static value => value!.Value)
                        .ToArray();
                    bool? imageValuesWithinReferenceCount = null;
                    if (track.TrackType is 18 or 19 && imageReferenceCount is int refCount)
                    {
                        imageValuesWithinReferenceCount = scalarValues.Length > 0
                            && scalarValues.All(value => value >= 0
                                && value < refCount
                                && Math.Abs(value - Math.Round(value)) <= 0.000001);
                    }

                    rows.Add(new StateTrackRow(
                        animation.Name ?? $"ANIM@0x{animation.Offset:X}",
                        animation.Index,
                        motion.Index,
                        nodeIndex,
                        motion.TargetName,
                        track.Index,
                        track.Offset,
                        track.TrackType,
                        track.TrackTypeName,
                        track.Keyframes.Count,
                        FormatKeyframeSequence(track),
                        imageReferenceCount,
                        imageReferenceSummary,
                        imageValuesWithinReferenceCount));
                }
            }
        }

        return rows;
    }

    private static SbSceneCropReference[]? SelectImageReferencesForStateTrack(
        int? trackType,
        SbSceneImageCast[]? imageCasts)
    {
        if (imageCasts is null)
        {
            return null;
        }

        return trackType switch
        {
            18 => imageCasts.SelectMany(static imageCast => imageCast.PrimaryCropReferences).ToArray(),
            19 => imageCasts.SelectMany(static imageCast => imageCast.SecondaryCropReferences).ToArray(),
            _ => imageCasts.SelectMany(static imageCast => imageCast.CropReferences).ToArray(),
        };
    }

    private static IReadOnlyList<ColorTrackEvidenceRow> BuildColorTrackEvidenceRows(SbSceneFile file)
    {
        var rows = new List<ColorTrackEvidenceRow>();
        foreach (var context in BuildTrackContexts(file).Where(static context => IsColorTrackType(context.Track.TrackType)))
        {
            if (context.NodeIndex is not int nodeIndex || nodeIndex < 0 || nodeIndex >= file.Surfboard.Nodes.Count)
            {
                continue;
            }

            var node = file.Surfboard.Nodes[nodeIndex];
            if (TryGetInitialColorChannel(node.Transform2D, context.Track.TrackType, out var initialValue))
            {
                var scalarValues = context.Track.Keyframes
                    .Select(static key => key.ScalarValue)
                    .Where(static value => value is not null)
                    .Select(static value => value!.Value)
                    .ToArray();
                rows.Add(new ColorTrackEvidenceRow(
                    context.AnimationName,
                    context.AnimationIndex,
                    context.MotionIndex,
                    nodeIndex,
                    context.NodeName,
                    context.Track.TrackType!.Value,
                    context.Track.TrackTypeName,
                    context.Track.Keyframes.Count,
                    initialValue,
                    scalarValues.Any(value => Math.Abs(value - initialValue) <= 0.01),
                    FormatKeyframeSequence(context.Track)));
            }
        }

        return rows;
    }

    private static IReadOnlyList<AlphaOpacityTrackEvidenceRow> BuildAlphaOpacityEvidenceRows(SbSceneFile file)
    {
        var imageCastsByNode = file.Surfboard.Resources.ImageCasts
            .Where(static imageCast => imageCast.CastIndex >= 0)
            .GroupBy(static imageCast => imageCast.CastIndex)
            .ToDictionary(static group => group.Key, static group => group.ToArray());
        var rows = new List<AlphaOpacityTrackEvidenceRow>();
        foreach (var context in BuildTrackContexts(file).Where(static context => context.Track.TrackType == 24))
        {
            if (context.NodeIndex is not int nodeIndex || nodeIndex < 0 || nodeIndex >= file.Surfboard.Nodes.Count)
            {
                continue;
            }

            var node = file.Surfboard.Nodes[nodeIndex];
            imageCastsByNode.TryGetValue(nodeIndex, out var imageCasts);
            var scalarValues = context.Track.Keyframes
                .Select(static key => key.ScalarValue)
                .Where(static value => value is not null)
                .Select(static value => value!.Value)
                .ToArray();
            var initialMaterialAlpha = node.Transform2D?.MaterialColor is { } materialColor
                ? materialColor.A / 255.0
                : (double?)null;
            var initialAlphaMatched = initialMaterialAlpha is not null
                && scalarValues.Any(value => Math.Abs(value - initialMaterialAlpha.Value) <= 0.01);

            rows.Add(new AlphaOpacityTrackEvidenceRow(
                context.AnimationName,
                context.AnimationIndex,
                context.MotionIndex,
                nodeIndex,
                context.NodeName,
                node.Flags,
                node.Group,
                imageCasts is { Length: > 0 },
                node.Transform2D?.Display,
                initialMaterialAlpha,
                initialAlphaMatched,
                context.Track.Keyframes.Count,
                FormatKeyframeSequence(context.Track)));
        }

        return rows;
    }

    private static bool IsColorTrackType(int? trackType)
    {
        return trackType is 21 or 22 or 23 or 25 or 26 or 27 or 28;
    }

    private static bool TryGetInitialColorChannel(Transform2DInfo? transform, int? trackType, out double value)
    {
        value = 0;
        if (transform is null)
        {
            return false;
        }

        value = trackType switch
        {
            21 when transform.MaterialColor is not null => transform.MaterialColor.R / 255.0,
            22 when transform.MaterialColor is not null => transform.MaterialColor.G / 255.0,
            23 when transform.MaterialColor is not null => transform.MaterialColor.B / 255.0,
            25 when transform.IlluminationColor is not null => transform.IlluminationColor.R / 255.0,
            26 when transform.IlluminationColor is not null => transform.IlluminationColor.G / 255.0,
            27 when transform.IlluminationColor is not null => transform.IlluminationColor.B / 255.0,
            28 when transform.IlluminationColor is not null => transform.IlluminationColor.A / 255.0,
            _ => double.NaN,
        };

        return !double.IsNaN(value);
    }

    private sealed record AlphaOpacityTrackEvidenceRow(
        string AnimationName,
        int AnimationIndex,
        int MotionIndex,
        int NodeIndex,
        string? NodeName,
        int? NodeFlags,
        string? NodeGroup,
        bool HasImageCast,
        bool? InitialDisplay,
        double? InitialMaterialAlpha,
        bool InitialAlphaMatched,
        int KeyCount,
        string KeyValues);

    private static IReadOnlyList<FieldCatalogRow> BuildFieldCatalogRows(SbSceneFile file)
    {
        return FlattenBlocks(file.Vtbf.Blocks)
            .SelectMany(static block => block.Fields.Select(field => new { Block = block, Field = field }))
            .Where(static item => item.Field.IdHex != "0x00FC" && item.Field.IdHex != "0x00FD" && item.Field.IdHex != "0x00FE")
            .GroupBy(static item => new
            {
                item.Block.Tag,
                item.Field.IdHex,
                item.Field.TypeHex,
                item.Field.TypeName,
            })
            .OrderBy(static group => group.Key.Tag, StringComparer.Ordinal)
            .ThenBy(static group => group.Key.IdHex, StringComparer.Ordinal)
            .ThenBy(static group => group.Key.TypeHex, StringComparer.Ordinal)
            .Select(static group => new FieldCatalogRow(
                group.Key.Tag,
                group.Key.IdHex,
                $"{group.Key.TypeName} `{group.Key.TypeHex}`",
                group.Count(),
                group.Select(static item => item.Block.Offset).Distinct().Count(),
                FormatCountStrideDistribution(group.Select(static item => item.Field)),
                FormatFieldValueDistribution(group.Select(static item => item.Field))))
            .ToArray();
    }

    private static string FormatCountStrideDistribution(IEnumerable<VtbfField> fields)
    {
        return string.Join(", ", fields
            .GroupBy(static field => $"{field.Count}/{field.Stride}", StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Take(8)
            .Select(static group => $"`{group.Key}`:{group.Count()}"));
    }

    private static string FormatFieldValueDistribution(IEnumerable<VtbfField> fields)
    {
        return string.Join(", ", fields
            .Select(FormatFieldValue)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(static value => value, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Take(8)
            .Select(static group => group.Count() == 1 ? group.Key : $"{group.Key}:{group.Count()}"));
    }

    private static string FormatFieldValue(VtbfField field)
    {
        if (!string.IsNullOrWhiteSpace(field.StringValue))
        {
            var text = field.StringValue.TrimEnd('\0');
            return text.Length <= 40 ? $"\"{text}\"" : $"\"{text[..40]}...\"";
        }

        if (field.Int64Values is { Length: > 0 } ints)
        {
            return IsFlagLikeField(field)
                ? FormatHexArray(ints)
                : FormatNumberArray(ints.Select(static value => (double)value).ToArray());
        }

        if (field.Float64Values is { Length: > 0 } floats)
        {
            return FormatNumberArray(floats);
        }

        return string.IsNullOrWhiteSpace(field.Preview) ? string.Empty : field.Preview;
    }

    private static string FormatNumberArray(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            return string.Empty;
        }

        var rendered = values.Take(4).Select(static value => FormatDouble(value)).ToArray();
        var suffix = values.Count > rendered.Length ? ", ..." : string.Empty;
        return values.Count == 1
            ? rendered[0]
            : $"[{string.Join(", ", rendered)}{suffix}]";
    }

    private static string FormatHexArray(IReadOnlyList<long> values)
    {
        if (values.Count == 0)
        {
            return string.Empty;
        }

        var rendered = values.Take(4).Select(static value => $"0x{value:X}").ToArray();
        var suffix = values.Count > rendered.Length ? ", ..." : string.Empty;
        return values.Count == 1
            ? rendered[0]
            : $"[{string.Join(", ", rendered)}{suffix}]";
    }

    private static bool IsFlagLikeField(VtbfField field)
    {
        return field.Id is 0x30 or 0x48 or 0x54;
    }

    private sealed record FieldCatalogRow(
        string Tag,
        string FieldId,
        string Type,
        int Occurrences,
        int Blocks,
        string CountStrideDistribution,
        string ValueDistribution);

    private static IReadOnlyList<TrackContext> BuildTrackContexts(SbSceneFile file)
    {
        var contexts = new List<TrackContext>();
        foreach (var animation in file.Surfboard.Animations)
        {
            var animationName = animation.Name ?? $"ANIM@0x{animation.Offset:X}";
            foreach (var motion in animation.Motions)
            {
                foreach (var track in motion.Tracks)
                {
                    contexts.Add(new TrackContext(
                        animationName,
                        animation.Index,
                        motion.Index,
                        motion.TargetIndex,
                        motion.TargetName,
                        track));
                }
            }
        }

        return contexts;
    }

    private static string FormatKeyValueTypeDistribution(IEnumerable<KeyframeInfo> keys)
    {
        return string.Join(", ", keys
            .Select(static key => string.Join(" ", new[]
            {
                key.KeyValueTypeHex ?? "?",
                key.KeyValueKind ?? key.KeyValueTypeName ?? "?",
            }.Where(static value => value.Length > 0)))
            .GroupBy(static value => value, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Take(8)
            .Select(static group => $"{group.Key}:{group.Count()}"));
    }

    private static string FormatNodeFlagBits(int flags)
    {
        var bits = Enumerable.Range(0, 32)
            .Where(bit => ((uint)flags & (1u << bit)) != 0)
            .Select(static bit => bit.ToString())
            .ToArray();
        return bits.Length == 0 ? "-" : string.Join(",", bits);
    }

    private static string DescribeNodeFlagBit(int bit)
    {
        return bit switch
        {
            0 => "Renderable/image-cast node candidate; set on CIMG-backed nodes in current samples.",
            8 => "Common node attribute; differentiates 0xFxx from 0xExx and appears on control flags.",
            9 => "Common node attribute; absent from 0x900 control/special flags in current samples.",
            10 => "Common node attribute; absent from 0x900 control/special flags in current samples.",
            11 => "Common node/control attribute; set on all observed NODE flags in current samples.",
            15 => "Root/control node candidate; observed on 0x8F00 control flags.",
            16 => "Sparse special node candidate; observed on rare 0x10F01-style node.",
            _ => "Unknown",
        };
    }

    private static string FormatContextDistribution(IEnumerable<TrackContext> contexts, Func<TrackContext, string> selector)
    {
        return string.Join(", ", contexts
            .Select(selector)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(static value => value, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Take(8)
            .Select(static group => $"{group.Key}:{group.Count()}"));
    }

    private static string FormatTrackContextNodeFlagDistribution(IEnumerable<TrackContext> contexts, IReadOnlyList<NodeInfo> nodes)
    {
        return string.Join(", ", contexts
            .Select(context => ResolveNode(nodes, context.NodeIndex ?? -1)?.Flags)
            .GroupBy(static value => value)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key ?? int.MinValue)
            .Take(8)
            .Select(static group => group.Key is null ? $"<null>:{group.Count()}" : $"0x{group.Key.Value:X}:{group.Count()}"));
    }

    private static string FormatTrackContextNodeGroupDistribution(IEnumerable<TrackContext> contexts, IReadOnlyList<NodeInfo> nodes)
    {
        return string.Join(", ", contexts
            .Select(context => ResolveNode(nodes, context.NodeIndex ?? -1)?.Group ?? "<null>")
            .GroupBy(static value => value, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Take(8)
            .Select(static group => $"{group.Key}:{group.Count()}"));
    }

    private static int GetTrackFlagBaseByte(int flags)
    {
        return flags & 0xFF;
    }

    private static int GetTrackFlagExtraMask(int flags)
    {
        return flags & ~0xFF;
    }

    private static int GetTrackFlagLowNibble(int flags)
    {
        return flags & 0x0F;
    }

    private static int GetTrackFlagStorageNibble(int flags)
    {
        return (flags >> 4) & 0x0F;
    }

    private static string FormatScalarRange(IEnumerable<KeyframeInfo> keys)
    {
        return FormatNumberRange(keys
            .Select(static key => key.ScalarValue)
            .Where(static value => value is not null)
            .Select(static value => value!.Value));
    }

    private static string FormatTrackValueRange(int? trackType, IEnumerable<KeyframeInfo> keys)
    {
        var keyArray = keys.ToArray();
        if (IsRotationTrackType(trackType)
            && keyArray.Any(static key => key.PackedAngleDegreesCandidate is not null))
        {
            var angleRange = FormatNumberRange(keyArray
                .Select(static key => key.PackedAngleDegreesCandidate)
                .Where(static value => value is not null)
                .Select(static value => value!.Value));
            return string.IsNullOrWhiteSpace(angleRange) ? string.Empty : $"{angleRange} deg";
        }

        return FormatScalarRange(keyArray);
    }

    private static string FormatNumberRange(IEnumerable<double> values)
    {
        var valueArray = values.ToArray();
        if (valueArray.Length == 0)
        {
            return string.Empty;
        }

        var min = valueArray.Min();
        var max = valueArray.Max();
        var distinct = valueArray
            .Select(static value => Math.Round(value, 6))
            .Distinct()
            .OrderBy(static value => value)
            .Take(10)
            .ToArray();
        if (distinct.Length <= 8)
        {
            return string.Join(", ", distinct.Select(static value => FormatDouble(value)));
        }

        return $"{FormatDouble(min)}..{FormatDouble(max)}";
    }

    private static string FormatDouble(double? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        return Math.Abs(value.Value - Math.Round(value.Value)) <= 0.000001
            ? Math.Round(value.Value).ToString("0")
            : value.Value.ToString("0.###");
    }

    private sealed record TrackContext(
        string AnimationName,
        int AnimationIndex,
        int MotionIndex,
        int? NodeIndex,
        string? NodeName,
        TrackInfo Track);

    private static string FormatKeyframeSequence(TrackInfo track)
    {
        var parts = track.Keyframes
            .Take(12)
            .Select(key => $"{key.KeyFrame?.ToString() ?? "?"}:{FormatKeyValue(track, key)}")
            .ToList();
        if (track.Keyframes.Count > parts.Count)
        {
            parts.Add("...");
        }

        return string.Join(", ", parts);
    }

    private static string FormatKeyValue(TrackInfo track, KeyframeInfo key)
    {
        if (IsRotationTrackType(track.TrackType) && key.PackedAngleDegreesCandidate is double degrees)
        {
            return $"{FormatDouble(degrees)}deg";
        }

        if (key.BoolValue is bool boolValue)
        {
            return boolValue ? "true" : "false";
        }

        if (key.ScalarValue is double scalar)
        {
            return Math.Abs(scalar - Math.Round(scalar)) <= 0.000001
                ? Math.Round(scalar).ToString("0")
                : scalar.ToString("0.###");
        }

        return "?";
    }

    private static bool IsRotationTrackType(int? trackType)
    {
        return trackType is 3 or 4 or 5;
    }

    private static string FormatRotation(Transform2DInfo transform)
    {
        if (transform.RotationZDegreesCandidate is double degrees)
        {
            return $"{FormatDouble(degrees)} deg (raw={transform.RotationZRaw?.ToString() ?? "?"})";
        }

        return FormatFloat(transform.RotationZ);
    }

    private static string FormatPackedAngleRawExamples(IEnumerable<int?> values)
    {
        return string.Join(", ", SelectPackedAngleSamples(values)
            .Select(static value => value.ToString()));
    }

    private static string FormatPackedAngleDegreeExamples(IEnumerable<int?> values)
    {
        return string.Join(", ", SelectPackedAngleSamples(values)
            .Select(static value => $"{value}->{FormatDouble(value * (180.0 / 32768.0))}deg"));
    }

    private static IEnumerable<int> SelectPackedAngleSamples(IEnumerable<int?> values)
    {
        return values
            .Where(static value => value is not null)
            .Select(static value => value!.Value)
            .GroupBy(static value => value)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => Math.Abs(group.Key))
            .ThenBy(static group => group.Key)
            .Take(8)
            .Select(static group => group.Key);
    }

    private static string FormatNullableBool(bool? value)
    {
        return value switch
        {
            true => "OK",
            false => "Mismatch",
            null => string.Empty,
        };
    }

    private static string FormatIndexedReference(IReadOnlyList<SbSceneCropReference> references, int? index)
    {
        if (references.Count == 0)
        {
            return string.Empty;
        }

        var groupIndex = index ?? 0;
        if (groupIndex < 0 || groupIndex >= references.Count)
        {
            return $"out-of-range:{groupIndex}";
        }

        var reference = references[groupIndex];
        return $"{groupIndex} -> {reference.TextureIndex}:{reference.CropIndex}";
    }

    private static CropReferenceIndexValidation BuildCropReferenceIndexValidation(IReadOnlyList<SbSceneImageCast> imageCasts)
    {
        var activeGroups = 0;
        var inRangeGroups = 0;
        var outOfRangeGroups = 0;
        var emptyGroupNonZeroIndices = 0;
        var nonZeroIndices = 0;
        var nonZeroImageCasts = 0;

        foreach (var imageCast in imageCasts)
        {
            var primaryIndex = imageCast.PrimaryCropReferenceIndex ?? 0;
            var secondaryIndex = imageCast.SecondaryCropReferenceIndex ?? 0;
            if (primaryIndex != 0 || secondaryIndex != 0)
            {
                nonZeroImageCasts++;
            }

            CountCropReferenceIndex(
                imageCast.PrimaryCropReferences,
                primaryIndex,
                ref activeGroups,
                ref inRangeGroups,
                ref outOfRangeGroups,
                ref emptyGroupNonZeroIndices,
                ref nonZeroIndices);
            CountCropReferenceIndex(
                imageCast.SecondaryCropReferences,
                secondaryIndex,
                ref activeGroups,
                ref inRangeGroups,
                ref outOfRangeGroups,
                ref emptyGroupNonZeroIndices,
                ref nonZeroIndices);
        }

        return new CropReferenceIndexValidation(
            activeGroups,
            inRangeGroups,
            outOfRangeGroups,
            emptyGroupNonZeroIndices,
            nonZeroIndices,
            nonZeroImageCasts);
    }

    private static void CountCropReferenceIndex(
        IReadOnlyList<SbSceneCropReference> references,
        int index,
        ref int activeGroups,
        ref int inRangeGroups,
        ref int outOfRangeGroups,
        ref int emptyGroupNonZeroIndices,
        ref int nonZeroIndices)
    {
        if (index != 0)
        {
            nonZeroIndices++;
        }

        if (references.Count == 0)
        {
            if (index != 0)
            {
                emptyGroupNonZeroIndices++;
            }

            return;
        }

        activeGroups++;
        if (index >= 0 && index < references.Count)
        {
            inRangeGroups++;
        }
        else
        {
            outOfRangeGroups++;
        }
    }

    private static bool IsFocusedStateAnimation(string animationName)
    {
        return animationName.StartsWith("Change_", StringComparison.OrdinalIgnoreCase)
            || animationName.StartsWith("Mouth", StringComparison.OrdinalIgnoreCase)
            || animationName.StartsWith("DressChange", StringComparison.OrdinalIgnoreCase)
            || animationName.Equals("Action_Change", StringComparison.OrdinalIgnoreCase)
            || animationName.StartsWith("Fade", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record ColorTrackEvidenceRow(
        string AnimationName,
        int AnimationIndex,
        int MotionIndex,
        int NodeIndex,
        string? NodeName,
        int TrackType,
        string? TrackTypeName,
        int KeyCount,
        double InitialChannelValue,
        bool InitialValueMatched,
        string KeyValues);

    private sealed record StateTrackRow(
        string AnimationName,
        int AnimationIndex,
        int MotionIndex,
        int? NodeIndex,
        string? NodeName,
        int TrackIndex,
        long TrackOffset,
        int? TrackType,
        string? TrackTypeName,
        int KeyCount,
        string Values,
        int? ImageReferenceCount,
        string? ImageReferenceSummary,
        bool? ImageValuesWithinReferenceCount);

    private static string FormatVector(Vector2Value? value)
    {
        return value is null ? string.Empty : $"({value.X:0.###}, {value.Y:0.###})";
    }

    private static string FormatVector(Vector3Value? value)
    {
        return value is null ? string.Empty : $"({value.X:0.###}, {value.Y:0.###}, {value.Z:0.###})";
    }

    private static string FormatFloat(float? value)
    {
        return value?.ToString("0.###") ?? string.Empty;
    }

    private static string FormatNullableInt(int? value)
    {
        return value?.ToString() ?? string.Empty;
    }

    private static string FormatBool(bool? value)
    {
        return value?.ToString() ?? string.Empty;
    }

    private static IEnumerable<VtbfBlock> FlattenBlocks(IEnumerable<VtbfBlock> blocks)
    {
        foreach (var block in blocks)
        {
            yield return block;
            foreach (var child in FlattenBlocks(block.Children))
            {
                yield return child;
            }
        }
    }

    private static IReadOnlyList<int> CountFollowingTags(IReadOnlyList<VtbfBlock> blocks, params string[] countedTags)
    {
        var counted = new HashSet<string>(countedTags, StringComparer.Ordinal);
        var counts = new List<int>();
        for (var i = 0; i < blocks.Count; i++)
        {
            if (blocks[i].Tag != "DATA")
            {
                continue;
            }

            var count = 0;
            for (var j = i + 1; j < blocks.Count; j++)
            {
                var tag = blocks[j].Tag;
                if (counted.Contains(tag))
                {
                    count++;
                    continue;
                }

                if (StopsDataImageCastRun(tag))
                {
                    break;
                }
            }

            counts.Add(count);
        }

        return counts;
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, int>> CountFollowingTagRuns(IReadOnlyList<VtbfBlock> blocks)
    {
        var runs = new List<IReadOnlyDictionary<string, int>>();
        for (var i = 0; i < blocks.Count; i++)
        {
            if (blocks[i].Tag != "DATA")
            {
                continue;
            }

            var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
            for (var j = i + 1; j < blocks.Count; j++)
            {
                var tag = blocks[j].Tag;
                if (StopsDataImageCastRun(tag))
                {
                    break;
                }

                counts[tag] = counts.TryGetValue(tag, out var count) ? count + 1 : 1;
            }

            runs.Add(counts);
        }

        return runs;
    }

    private static bool StopsDataImageCastRun(string tag)
    {
        return tag is "NCAT" or "NODE" or "TRS2" or "TRS3" or "DATA" or "LAYR" or "CAST" or "ANIM" or "MOT " or "TRK " or "KEY " or "CAM " or "SRCK" or "PROJ" or "SCN " or "SCN" or "SRFF";
    }

    private static string FormatTagCounts(IReadOnlyDictionary<string, int> counts)
    {
        return string.Join(", ", counts.Select(static item => $"{item.Key}:{item.Value}"));
    }

    private static string FormatColorDistribution(IEnumerable<ColorArgbValue?> colors)
    {
        return string.Join(", ", colors
            .Select(static color => color?.Hex ?? "<null>")
            .GroupBy(static value => value, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Take(12)
            .Select(static group => $"{group.Key}:{group.Count()}"));
    }

    private static string FormatNullableColorListDistribution(IEnumerable<IReadOnlyList<ColorArgbValue>?> values)
    {
        return string.Join(", ", values
            .Select(static value => value is { Count: > 0 } ? FormatColorList(value) : "<null>")
            .GroupBy(static value => value, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Take(16)
            .Select(static group => $"{group.Key}:{group.Count()}"));
    }

    private static string FormatColorList(IReadOnlyList<ColorArgbValue>? values)
    {
        return values is { Count: > 0 } ? "[" + string.Join(", ", values.Select(static value => value.Hex)) + "]" : string.Empty;
    }

    private static string FormatNullableStringDistribution(IEnumerable<string?> values)
    {
        return string.Join(", ", values
            .Select(static value => string.IsNullOrWhiteSpace(value) ? "<null>" : value)
            .GroupBy(static value => value, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Take(16)
            .Select(static group => $"{group.Key}:{group.Count()}"));
    }

    private static string FormatNullableStringListDistribution(IEnumerable<IReadOnlyList<string>?> values)
    {
        return string.Join(", ", values
            .Select(static value => value is { Count: > 0 } ? FormatStringList(value) : "<null>")
            .GroupBy(static value => value, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Take(16)
            .Select(static group => $"{group.Key}:{group.Count()}"));
    }

    private static string FormatStringList(IReadOnlyList<string>? values)
    {
        return values is { Count: > 0 } ? "[" + string.Join(", ", values) + "]" : string.Empty;
    }

    private static string FormatNullableIntListDistribution(IEnumerable<IReadOnlyList<int>?> values)
    {
        return string.Join(", ", values
            .Select(static value => value is { Count: > 0 } ? FormatIntList(value) : "<null>")
            .GroupBy(static value => value, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Take(16)
            .Select(static group => $"{group.Key}:{group.Count()}"));
    }

    private static string FormatNullableFloatListDistribution(IEnumerable<IReadOnlyList<float>?> values)
    {
        return string.Join(", ", values
            .Select(static value => value is { Count: > 0 } ? FormatFloatList(value) : "<null>")
            .GroupBy(static value => value, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Take(16)
            .Select(static group => $"{group.Key}:{group.Count()}"));
    }

    private static string FormatFloatList(IReadOnlyList<float>? values)
    {
        return values is { Count: > 0 } ? "[" + string.Join(", ", values.Select(static value => value.ToString("0.###"))) + "]" : string.Empty;
    }

    private static string FormatIntList(IReadOnlyList<int>? values)
    {
        return values is { Count: > 0 } ? "[" + string.Join(", ", values) + "]" : string.Empty;
    }

    private static string FormatNullableHexIntDistribution(IEnumerable<int?> values)
    {
        return string.Join(", ", values
            .Select(static value => value is null ? "<null>" : $"0x{value.Value:X}")
            .GroupBy(static value => value, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Take(16)
            .Select(static group => $"{group.Key}:{group.Count()}"));
    }

    private static string FormatNullableHex(int? value)
    {
        return value is null ? string.Empty : $"0x{value.Value:X8}";
    }

    private static string FormatNullableIntDistribution(IEnumerable<int?> values)
    {
        return string.Join(", ", values
            .Select(static value => value?.ToString() ?? "<null>")
            .GroupBy(static value => value, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Take(16)
            .Select(static group => $"{group.Key}:{group.Count()}"));
    }

    private static string FormatNullableFloatDistribution(IEnumerable<float?> values)
    {
        return string.Join(", ", values
            .Select(static value => value?.ToString("0.###") ?? "<null>")
            .GroupBy(static value => value, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Take(16)
            .Select(static group => $"{group.Key}:{group.Count()}"));
    }

    private static string FormatCountDistribution(IEnumerable<int> values)
    {
        return string.Join(", ", values
            .GroupBy(static value => value)
            .OrderBy(static group => group.Key)
            .Take(24)
            .Select(static group => $"{group.Key}:{group.Count()}"));
    }

    private static bool IsNonZero(double? value)
    {
        return Math.Abs(value ?? 0) > 0.000001;
    }

    private static string FormatImageCastBits(int flags)
    {
        var bits = Enumerable.Range(0, 32)
            .Where(bit => ((uint)flags & (1u << bit)) != 0)
            .Select(static bit => bit.ToString())
            .ToArray();
        return bits.Length == 0 ? "-" : string.Join(",", bits);
    }

    private static string FormatImageCastNodeFlagDistribution(IEnumerable<SbSceneImageCast> imageCasts, IReadOnlyList<NodeInfo> nodes)
    {
        return string.Join(", ", imageCasts
            .Select(imageCast => ResolveNode(nodes, imageCast.CastIndex)?.Flags)
            .Where(static value => value is not null)
            .Select(static value => value!.Value)
            .GroupBy(static value => value)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key)
            .Take(6)
            .Select(static group => $"0x{group.Key:X}:{group.Count()}"));
    }

    private static string FormatImageCastGroupDistribution(IEnumerable<SbSceneImageCast> imageCasts, IReadOnlyList<NodeInfo> nodes)
    {
        return string.Join(", ", imageCasts
            .Select(imageCast => ResolveNode(nodes, imageCast.CastIndex)?.Group)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(static value => value!, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .Select(static group => $"{group.Key}:{group.Count()}"));
    }

    private static int CountImageCastsWithDisplayFalse(IEnumerable<SbSceneImageCast> imageCasts, IReadOnlyList<NodeInfo> nodes)
    {
        return imageCasts.Count(imageCast => ResolveNode(nodes, imageCast.CastIndex)?.Transform2D?.Display == false);
    }

    private static int CountMultiReferenceImageCasts(IEnumerable<SbSceneImageCast> imageCasts)
    {
        return imageCasts.Count(static imageCast => imageCast.CropReferences.Count > 1);
    }

    private static int CountSecondaryReferenceImageCasts(IEnumerable<SbSceneImageCast> imageCasts)
    {
        return imageCasts.Count(static imageCast => imageCast.SecondaryCropReferences.Count > 0);
    }

    private static int CountNonZeroReferenceIndexImageCasts(IEnumerable<SbSceneImageCast> imageCasts)
    {
        return imageCasts.Count(static imageCast => imageCast.PrimaryCropReferenceIndex is > 0 || imageCast.SecondaryCropReferenceIndex is > 0);
    }

    private static NodeInfo? ResolveNode(IReadOnlyList<NodeInfo> nodes, int index)
    {
        return index >= 0 && index < nodes.Count ? nodes[index] : null;
    }

    private static IReadOnlyList<ImageCastBitPairRow> BuildImageCastFlagBitPairs(IReadOnlyList<SbSceneImageCast> imageCasts)
    {
        var presentBits = imageCasts
            .SelectMany(static imageCast => imageCast.ImageCastFlagBits)
            .Distinct()
            .OrderBy(static bit => bit)
            .ToArray();
        var rows = new List<ImageCastBitPairRow>();
        for (var i = 0; i < presentBits.Length; i++)
        {
            for (var j = i + 1; j < presentBits.Length; j++)
            {
                var left = presentBits[i];
                var right = presentBits[j];
                var count = imageCasts.Count(imageCast =>
                    imageCast.ImageCastFlagBits.Contains(left)
                    && imageCast.ImageCastFlagBits.Contains(right));
                if (count == 0)
                {
                    continue;
                }

                rows.Add(new ImageCastBitPairRow(
                    $"{left}+{right}",
                    count,
                    DescribeImageCastFlagPair(left, right, count, imageCasts)));
            }
        }

        return rows
            .OrderByDescending(static row => row.Count)
            .ThenBy(static row => row.Bits, StringComparer.Ordinal)
            .ToArray();
    }

    private static string DescribeImageCastFlagPair(int left, int right, int count, IReadOnlyList<SbSceneImageCast> imageCasts)
    {
        var leftCount = imageCasts.Count(imageCast => imageCast.ImageCastFlagBits.Contains(left));
        var rightCount = imageCasts.Count(imageCast => imageCast.ImageCastFlagBits.Contains(right));
        if (count == leftCount && count == rightCount)
        {
            return "Both bits always appear together in this sample.";
        }

        if (count == leftCount)
        {
            return $"bit {left} is a subset of bit {right} in this sample.";
        }

        if (count == rightCount)
        {
            return $"bit {right} is a subset of bit {left} in this sample.";
        }

        return "Partial overlap in this sample.";
    }

    private sealed record ImageCastBitPairRow(string Bits, int Count, string Observation);

    private sealed record CropReferenceIndexValidation(
        int ActiveGroups,
        int InRangeGroups,
        int OutOfRangeGroups,
        int EmptyGroupNonZeroIndices,
        int NonZeroIndices,
        int NonZeroImageCasts);

    private static string DescribeImageCastFlagBit(int bit)
    {
        return bit switch
        {
            0 => "Observed bit in CIMG samples; subset of bit 22 in current samples.",
            15 => "High-coverage bit in CIMG samples; set on most records but not required globally.",
            20 => "Observed high bit; often paired with bit 23 in current samples, but subset direction varies.",
            21 => "Sparse observed high bit in current samples.",
            22 => "Observed high bit; common in CIMG samples.",
            23 => "Observed high bit; often paired with bit 20 in current samples.",
            _ => "Unknown",
        };
    }

    private static string Escape(string value)
    {
        return value.Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
    }
}
