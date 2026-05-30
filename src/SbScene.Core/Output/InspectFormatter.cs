using System.Text;
using SbScene.Core.Resources;
using SbScene.Core.Semantics;
using SbScene.Core.Vtbf;

namespace SbScene.Core.Output;

public static class InspectFormatter
{
    public static string Format(SbSceneFile file)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"File: {file.SourcePath}");
        builder.AppendLine($"Size: {file.SourceSize} bytes");
        builder.AppendLine($"Blocks: {file.Summary.TotalBlockCount} total, {file.Summary.RootBlockCount} root");
        builder.AppendLine($"Nodes: {file.Summary.NodeCount}");
        builder.AppendLine($"Transform2D records: {file.Surfboard.Transform2DRecords.Count}");
        builder.AppendLine($"Node category records: {file.Surfboard.NodeCategoryRecords.Count}");
        builder.AppendLine($"Texture atlases: {file.Surfboard.Resources.Atlases.Count}");
        builder.AppendLine($"Image casts: {file.Surfboard.Resources.ImageCasts.Count}");
        builder.AppendLine($"Animations: {file.Summary.AnimationCount}");
        builder.AppendLine($"Animation bindings: {file.Surfboard.AnimationBindings.Count}");
        builder.AppendLine($"Variant hints: {file.Summary.VariantHintCount}");
        builder.AppendLine();

        builder.AppendLine("Block counts:");
        foreach (var pair in file.Summary.BlockCounts.OrderByDescending(static pair => pair.Value).ThenBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            builder.AppendLine($"  {pair.Key}: {pair.Value}");
        }

        AppendBlockParameterSummary(builder, file);
        AppendFieldCatalogSummary(builder, file);

        builder.AppendLine();
        builder.AppendLine("Node groups:");
        foreach (var group in file.Surfboard.NodeGroups.Take(24))
        {
            var examples = string.Join(", ", group.NodeNames.Take(5));
            builder.AppendLine($"  {group.Name}: {group.Count} [{examples}]");
        }

        builder.AppendLine();
        builder.AppendLine("Node hierarchy sample:");
        AppendNodeTree(builder, file.Surfboard.Nodes, 0, 0, 24);

        builder.AppendLine();
        builder.AppendLine("Transform2D sample:");
        foreach (var node in file.Surfboard.Nodes.Where(static node => node.Transform2D is not null).Take(8))
        {
            var transform = node.Transform2D!;
            builder.AppendLine(
                $"  [{node.Index}] {node.Name}: pos={FormatVector(transform.Translation)}, rot={FormatRotation(transform)}, scale={FormatVector(transform.Scale)}, display={FormatBool(transform.Display)}");
        }

        AppendNodeFlagSummary(builder, file);
        AppendTransformFieldSummary(builder, file);
        AppendDataAndCategorySummary(builder, file);

        AppendResourceSummary(builder, file);
        AppendCamera(builder, file);

        builder.AppendLine();
        builder.AppendLine("Animations:");
        foreach (var animation in file.Surfboard.Animations.Take(80))
        {
            var trackCount = animation.Motions.Sum(static motion => motion.Tracks.Count);
            var keyCount = animation.Motions.SelectMany(static motion => motion.Tracks).Sum(static track => track.Keyframes.Count);
            var trackTypes = animation.Motions
                .SelectMany(static motion => motion.Tracks)
                .Where(static track => track.TrackType is not null)
                .GroupBy(static track => track.TrackType!.Value)
                .OrderByDescending(static group => group.Count())
                .Take(5)
                .Select(static group =>
                {
                    var name = group.Select(static track => track.TrackTypeName).FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value));
                    return $"{group.Key}({name ?? "?"}):{group.Count()}";
                });
            builder.AppendLine($"  [{animation.Index}] {animation.Name ?? $"ANIM@0x{animation.Offset:X}"}: motions={animation.Motions.Count}, tracks={trackCount}, keys={keyCount}, trackTypes=[{string.Join(", ", trackTypes)}]");
        }

        if (file.Surfboard.Animations.Count > 80)
        {
            builder.AppendLine($"  ... {file.Surfboard.Animations.Count - 80} more animation(s)");
        }

        AppendTrackStorageSummary(builder, file);
        AppendTrackFlagExtraEvidence(builder, file);
        AppendTrackTypeEvidence(builder, file);
        AppendColorTrackEvidence(builder, file);
        AppendAlphaOpacityEvidence(builder, file);
        AppendPackedAngleCandidateSummary(builder, file);
        AppendTrackKeyCountSummary(builder, file);
        AppendInterpolationSummary(builder, file);

        builder.AppendLine();
        builder.AppendLine("Animation bindings sample:");
        foreach (var binding in SelectBindingSample(file.Surfboard.AnimationBindings).Take(140))
        {
            var typeIds = string.Join(",", binding.TrackTypes);
            var typeNames = string.Join(",", binding.TrackTypeNames);
            builder.AppendLine(
                $"  {binding.AnimationName} -> [{binding.NodeIndex}] {binding.NodeName ?? "(unnamed)"}: motion={binding.MotionIndex}, tracks={binding.TrackCount}, keys={binding.KeyCount}, types=[{typeIds}] names=[{typeNames}]");
        }

        AppendStateTrackSummary(builder, file);

        builder.AppendLine();
        builder.AppendLine("Variant hints:");
        foreach (var hint in file.Surfboard.VariantHints.Take(80))
        {
            builder.AppendLine($"  {hint.Category} / {hint.SourceKind} / {hint.Name} ({hint.Confidence:0.00})");
        }

        if (file.Summary.Warnings.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Warnings:");
            foreach (var warning in file.Summary.Warnings)
            {
                builder.AppendLine($"  {warning}");
            }
        }

        return builder.ToString();
    }

    private static void AppendBlockParameterSummary(StringBuilder builder, SbSceneFile file)
    {
        var roots = file.Vtbf.Blocks.ToArray();
        if (roots.Length > 0)
        {
            var rootSummary = string.Join(", ", roots.Select(static root => $"{root.Tag}@0x{root.Offset:X}: raw=0x{root.ParamRawHex ?? "?"}, low={root.ParamLow?.ToString() ?? "?"}, high={root.ParamHigh?.ToString() ?? "?"}"));
            builder.AppendLine($"  roots=[{rootSummary}]");
        }

        builder.AppendLine("  ParamRawHex and ParamLow/ParamHigh by tag:");
        foreach (var group in FlattenBlocks(file.Vtbf.Blocks)
            .GroupBy(static block => block.Tag, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Take(18))
        {
            var pairs = string.Join(", ", group
                .GroupBy(static block => $"{block.ParamLow?.ToString() ?? "?"}/{block.ParamHigh?.ToString() ?? "?"}")
                .OrderByDescending(static pair => pair.Count())
                .ThenBy(static pair => pair.Key, StringComparer.Ordinal)
                .Take(6)
                .Select(static pair => $"{pair.Key}:{pair.Count()}"));
            var rawValues = string.Join(", ", group
                .Select(static block => block.ParamRawHex)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .GroupBy(static value => value!, StringComparer.Ordinal)
                .OrderByDescending(static rawGroup => rawGroup.Count())
                .ThenBy(static rawGroup => rawGroup.Key, StringComparer.Ordinal)
                .Take(6)
                .Select(static rawGroup => $"0x{rawGroup.Key}:{rawGroup.Count()}"));
            builder.AppendLine($"    {group.Key}: raw=[{rawValues}], pairs=[{pairs}]");
        }
    }

    private static void AppendFieldCatalogSummary(StringBuilder builder, SbSceneFile file)
    {
        var rows = BuildFieldCatalogRows(file).ToArray();
        if (rows.Length == 0)
        {
            return;
        }

        builder.AppendLine("  Field catalog sample:");
        foreach (var row in rows
            .OrderByDescending(static row => row.Occurrences)
            .ThenBy(static row => row.Tag, StringComparer.Ordinal)
            .ThenBy(static row => row.FieldId, StringComparer.Ordinal)
            .Take(80))
        {
            builder.AppendLine($"    {row.Tag}.{row.FieldId} {row.Type}: occurrences={row.Occurrences}, blocks={row.Blocks}, count/stride=[{row.CountStrideDistribution}], values=[{row.ValueDistribution}]");
        }
    }

    private static void AppendNodeFlagSummary(StringBuilder builder, SbSceneFile file)
    {
        var imageCastNodeIndexes = file.Surfboard.Resources.ImageCasts
            .Where(static imageCast => imageCast.CastIndex >= 0)
            .GroupBy(static imageCast => imageCast.CastIndex)
            .ToDictionary(static group => group.Key, static group => group.Count());
        var animatedNodeIndexes = file.Surfboard.AnimationBindings
            .GroupBy(static binding => binding.NodeIndex)
            .ToDictionary(static group => group.Key, static group => group.Count());

        builder.AppendLine();
        builder.AppendLine("Node flags:");
        foreach (var group in file.Surfboard.Nodes
            .GroupBy(static node => node.Flags ?? 0)
            .OrderBy(static group => group.Key))
        {
            var imageCasts = group.Sum(node => imageCastNodeIndexes.TryGetValue(node.Index, out var count) ? count : 0);
            var animatedNodes = group.Count(node => animatedNodeIndexes.ContainsKey(node.Index));
            var hidden = group.Count(static node => node.Transform2D?.Display == false);
            var groups = string.Join("/", group.Select(static node => node.Group).Distinct(StringComparer.OrdinalIgnoreCase).Take(6));
            var examples = string.Join(", ", group.Select(static node => node.Name ?? "?").Take(5));
            builder.AppendLine($"  flags=0x{group.Key:X} bits=[{FormatNodeFlagBits(group.Key)}]: nodes={group.Count()}, imageCasts={imageCasts}, animatedNodes={animatedNodes}, displayFalse={hidden}, groups=[{groups}], examples=[{examples}]");
        }

        var presentBits = file.Surfboard.Nodes
            .SelectMany(static node => node.FlagBits)
            .Distinct()
            .OrderBy(static bit => bit)
            .ToArray();
        if (presentBits.Length > 0)
        {
            builder.AppendLine("Node flag bits:");
            foreach (var bit in presentBits)
            {
                var nodes = file.Surfboard.Nodes.Where(node => node.FlagBits.Contains(bit)).ToArray();
                var imageCasts = nodes.Sum(node => imageCastNodeIndexes.TryGetValue(node.Index, out var count) ? count : 0);
                var animatedNodes = nodes.Count(node => animatedNodeIndexes.ContainsKey(node.Index));
                var hidden = nodes.Count(static node => node.Transform2D?.Display == false);
                var groups = string.Join("/", nodes.Select(static node => node.Group).Distinct(StringComparer.OrdinalIgnoreCase).Take(6));
                var examples = string.Join(", ", nodes.Select(static node => node.Name ?? "?").Take(5));
                builder.AppendLine($"  bit{bit} mask=0x{1u << bit:X8}: nodes={nodes.Length}, imageCasts={imageCasts}, animatedNodes={animatedNodes}, displayFalse={hidden}, groups=[{groups}], candidate=[{DescribeNodeFlagBit(bit)}], examples=[{examples}]");
            }
        }
    }

    private static void AppendTransformFieldSummary(StringBuilder builder, SbSceneFile file)
    {
        var transforms = file.Surfboard.Transform2DRecords;
        if (transforms.Count == 0)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine("TRS2 fields:");
        builder.AppendLine($"  display=[true:{transforms.Count(static item => item.Display == true)}, false:{transforms.Count(static item => item.Display == false)}, unknown:{transforms.Count(static item => item.Display is null)}]");
        builder.AppendLine($"  materialColors=[{FormatColorDistribution(transforms.Select(static item => item.MaterialColor))}]");
        builder.AppendLine($"  illuminationColors=[{FormatColorDistribution(transforms.Select(static item => item.IlluminationColor))}]");
        builder.AppendLine($"  vertexColorCounts=[{FormatCountDistribution(transforms.Select(static item => item.VertexColors.Count))}]");
        builder.AppendLine($"  multiPosFlags=[{FormatNullableIntDistribution(transforms.Select(static item => item.MultiPosFlags))}]");
        builder.AppendLine($"  multiSizeFlags=[{FormatNullableIntDistribution(transforms.Select(static item => item.MultiSizeFlags))}]");
        var fieldSummary = string.Join(", ", transforms
            .SelectMany(static transform => transform.Fields)
            .GroupBy(static field => field.IdHex, StringComparer.Ordinal)
            .OrderBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group => $"{group.Key}:{group.Count()}"));
        builder.AppendLine($"  fieldCounts=[{fieldSummary}]");
    }

    private static void AppendDataAndCategorySummary(StringBuilder builder, SbSceneFile file)
    {
        var blocks = FlattenBlocks(file.Vtbf.Blocks).ToArray();
        var dataBlocks = blocks.Where(static block => block.Tag == "DATA").ToArray();
        var followingImageCastCounts = CountFollowingTags(blocks, "CIMG");
        var followingCimgCrfdCounts = CountFollowingTags(blocks, "CIMG", "CRFD");
        var followingCimgCnumCrfdCounts = CountFollowingTags(blocks, "CIMG", "CNUM", "CRFD");
        var followingCimgCnumCrfdCsliCounts = CountFollowingTags(blocks, "CIMG", "CNUM", "CRFD", "CSLI");
        var followingTagCounts = CountFollowingTagRuns(blocks);
        var imageCastCount = file.Surfboard.Resources.ImageCasts.Count;
        builder.AppendLine();
        builder.AppendLine("DATA/NCAT:");
        if (dataBlocks.Length == 0)
        {
            builder.AppendLine("  DATA: none");
        }

        for (var i = 0; i < dataBlocks.Length; i++)
        {
            var block = dataBlocks[i];
            var followingImageCasts = i < followingImageCastCounts.Count ? followingImageCastCounts[i] : 0;
            var followingCimgCrfd = i < followingCimgCrfdCounts.Count ? followingCimgCrfdCounts[i] : 0;
            var followingCimgCnumCrfd = i < followingCimgCnumCrfdCounts.Count ? followingCimgCnumCrfdCounts[i] : 0;
            var followingCimgCnumCrfdCsli = i < followingCimgCnumCrfdCsliCounts.Count ? followingCimgCnumCrfdCsliCounts[i] : 0;
            var followingTags = i < followingTagCounts.Count ? FormatTagCounts(followingTagCounts[i]) : string.Empty;
            builder.AppendLine($"  DATA@0x{block.Offset:X}: low={block.ParamLow?.ToString() ?? "?"}, high={block.ParamHigh?.ToString() ?? "?"}, imageCasts={imageCastCount}, matchesImageCasts={block.ParamLow == imageCastCount}, followingTags=[{followingTags}], followingCIMG={followingImageCasts}, matchesFollowingCIMG={block.ParamLow == followingImageCasts}, followingCIMGCRFD={followingCimgCrfd}, matchesFollowingCIMGCRFD={block.ParamLow == followingCimgCrfd}, followingCIMGCNUMCRFD={followingCimgCnumCrfd}, matchesFollowingCIMGCNUMCRFD={block.ParamLow == followingCimgCnumCrfd}, followingCIMGCNUMCRFDCSLI={followingCimgCnumCrfdCsli}, matchesFollowingCIMGCNUMCRFDCSLI={block.ParamLow == followingCimgCnumCrfdCsli}, fields={block.Fields.Count}, trailingBytes={block.TrailingBytes?.Length ?? 0}");
        }

        if (file.Surfboard.NodeCategoryRecords.Count > 0)
        {
            builder.AppendLine($"  NCAT records={file.Surfboard.NodeCategoryRecords.Count}, categories=[{FormatCountDistribution(file.Surfboard.NodeCategoryRecords)}]");
        }

        if (file.Surfboard.NodeCategoryDetails.Count > 0)
        {
            var details = file.Surfboard.NodeCategoryDetails;
            var kindSummary = string.Join(", ", details
                .Select(static record => record.KindName ?? "(none)")
                .GroupBy(static value => value, StringComparer.Ordinal)
                .OrderByDescending(static group => group.Count())
                .ThenBy(static group => group.Key, StringComparer.Ordinal)
                .Take(12)
                .Select(static group => $"{group.Key}:{group.Count()}"));
            var typeByteSummary = FormatNullableIntDistribution(details.Select(static record => record.TypeByte));
            builder.AppendLine($"  NCAT detailRecords={details.Count}, withCategory={details.Count(static record => record.CategoryId is not null)}, withoutCategory={details.Count(static record => record.CategoryId is null)}, kinds=[{kindSummary}], typeBytes=[{typeByteSummary}]");
            foreach (var record in details
                .Where(static record => record.KindName is not null || record.ParameterPreview is not null)
                .Take(8))
            {
                builder.AppendLine($"    NCAT[{record.Index}] kind={record.KindName ?? "?"}, typeByte={record.TypeByte?.ToString() ?? "?"}, category={record.CategoryId?.ToString() ?? "?"}, param={record.ParameterPreview ?? ""}");
            }
        }
    }

    private static void AppendTrackStorageSummary(StringBuilder builder, SbSceneFile file)
    {
        var tracks = file.Surfboard.Animations.SelectMany(static animation => animation.Motions).SelectMany(static motion => motion.Tracks).ToArray();
        builder.AppendLine();
        builder.AppendLine("Track storage by flags:");
        foreach (var group in tracks
            .Where(static track => track.Flags is not null)
            .GroupBy(static track => new { track.Flags, track.KeyValueStorage })
            .OrderBy(static group => group.Key.Flags))
        {
            var flags = group.Key.Flags!.Value;
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
            var valueTypes = FormatKeyValueTypeDistribution(group.SelectMany(static track => track.Keyframes));
            builder.AppendLine($"  flags=0x{flags:X} base=0x{GetTrackFlagBaseByte(flags):X2} extra=0x{GetTrackFlagExtraMask(flags):X} low=0x{GetTrackFlagLowNibble(flags):X} storageNibble=0x{GetTrackFlagStorageNibble(flags):X} {group.Key.KeyValueStorage ?? "?"}: tracks={group.Count()}, keyValueTypes=[{valueTypes}], types=[{typeSummary}]");
        }

        builder.AppendLine("Track flags parts:");
        foreach (var group in tracks
            .Where(static track => track.Flags is not null)
            .GroupBy(static track => GetTrackFlagLowNibble(track.Flags!.Value))
            .OrderBy(static group => group.Key))
        {
            builder.AppendLine($"  low=0x{group.Key:X}: tracks={group.Count()}, keyValueTypes=[{FormatKeyValueTypeDistribution(group.SelectMany(static track => track.Keyframes))}]");
        }

        foreach (var group in tracks
            .Where(static track => track.Flags is not null)
            .GroupBy(static track => GetTrackFlagStorageNibble(track.Flags!.Value))
            .OrderBy(static group => group.Key))
        {
            builder.AppendLine($"  storageNibble=0x{group.Key:X}: tracks={group.Count()}, keyValueTypes=[{FormatKeyValueTypeDistribution(group.SelectMany(static track => track.Keyframes))}]");
        }

        foreach (var group in tracks
            .Where(static track => track.Flags is not null)
            .GroupBy(static track => GetTrackFlagExtraMask(track.Flags!.Value))
            .OrderBy(static group => group.Key))
        {
            builder.AppendLine($"  extra=0x{group.Key:X}: tracks={group.Count()}, keyValueTypes=[{FormatKeyValueTypeDistribution(group.SelectMany(static track => track.Keyframes))}]");
        }
    }

    private static void AppendTrackTypeEvidence(StringBuilder builder, SbSceneFile file)
    {
        var contexts = BuildTrackContexts(file);

        builder.AppendLine();
        builder.AppendLine("Track type evidence:");
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
                .Take(4)
                .Select(static interpolationGroup => $"{interpolationGroup.Key.Interpolation}({interpolationGroup.Key.InterpolationName ?? "?"}):{interpolationGroup.Count()}"));
            var examples = string.Join(", ", group
                .Select(static context => $"{context.AnimationName}->{context.NodeName ?? "?"}")
                .Distinct(StringComparer.Ordinal)
                .Take(4));
            builder.AppendLine($"  type={group.Key.TrackType}({group.Key.TrackTypeName ?? "?"}): tracks={tracks.Length}, keys={keys.Length}, flags=[{flags}], keyValueTypes=[{FormatKeyValueTypeDistribution(keys)}], interpolation=[{interpolation}], values=[{FormatTrackValueRange(group.Key.TrackType, keys)}], examples=[{examples}]");
        }
    }

    private static void AppendColorTrackEvidence(StringBuilder builder, SbSceneFile file)
    {
        var rows = BuildColorTrackEvidenceRows(file);
        if (rows.Count == 0)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine("Color track evidence:");
        builder.AppendLine("  TRS2 colors are interpreted as A,R,G,B candidates; initialMatches uses <=0.01 tolerance.");
        foreach (var group in rows
            .GroupBy(static row => new { row.TrackType, row.TrackTypeName })
            .OrderBy(static group => group.Key.TrackType))
        {
            var examples = string.Join("; ", group
                .Take(4)
                .Select(static row => $"{row.AnimationName}->{row.NodeName ?? "?"}: init={FormatDouble(row.InitialChannelValue)}, keys=[{row.KeyValues}]"));
            builder.AppendLine($"  type={group.Key.TrackType}({group.Key.TrackTypeName ?? "?"}): tracks={group.Count()}, keys={group.Sum(static row => row.KeyCount)}, nodes={group.Select(static row => row.NodeIndex).Distinct().Count()}, initialMatches={group.Count(static row => row.InitialValueMatched)}/{group.Count()}, initialValues=[{FormatNumberRange(group.Select(static row => row.InitialChannelValue))}], examples=[{examples}]");
        }
    }

    private static void AppendAlphaOpacityEvidence(StringBuilder builder, SbSceneFile file)
    {
        var rows = BuildAlphaOpacityEvidenceRows(file);
        if (rows.Count == 0)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine("Alpha/Opacity track evidence:");
        builder.AppendLine("  type 24 is compared with target material alpha, display, and CIMG binding; materialAlphaMatches uses <=0.01 tolerance.");
        builder.AppendLine($"  total: tracks={rows.Count}, keys={rows.Sum(static row => row.KeyCount)}, cimgTargets={rows.Count(static row => row.HasImageCast)}, displayFalseTargets={rows.Count(static row => row.InitialDisplay == false)}, materialAlphaMatches={rows.Count(static row => row.InitialAlphaMatched)}/{rows.Count(static row => row.InitialMaterialAlpha is not null)}");
        foreach (var group in rows
            .GroupBy(static row => row.AnimationName, StringComparer.Ordinal)
            .OrderBy(static group => group.Key, StringComparer.Ordinal))
        {
            var initialAlpha = FormatNumberRange(group
                .Select(static row => row.InitialMaterialAlpha)
                .Where(static value => value is not null)
                .Select(static value => value!.Value));
            var examples = string.Join("; ", group
                .Take(4)
                .Select(static row => $"{row.NodeName ?? "?"}: alpha={FormatDouble(row.InitialMaterialAlpha)}, display={FormatBool(row.InitialDisplay)}, cimg={row.HasImageCast}, keys=[{row.KeyValues}]"));
            builder.AppendLine($"  {group.Key}: tracks={group.Count()}, keys={group.Sum(static row => row.KeyCount)}, nodes={group.Select(static row => row.NodeIndex).Distinct().Count()}, cimgNodes={group.Count(static row => row.HasImageCast)}, displayFalse={group.Count(static row => row.InitialDisplay == false)}, materialAlphaMatches={group.Count(static row => row.InitialAlphaMatched)}/{group.Count(static row => row.InitialMaterialAlpha is not null)}, initialAlpha=[{initialAlpha}], examples=[{examples}]");
        }

        builder.AppendLine("  focused samples:");
        foreach (var row in rows
            .Where(static row => IsFocusedStateAnimation(row.AnimationName) || row.AnimationName.StartsWith("Action_", StringComparison.OrdinalIgnoreCase))
            .Take(40))
        {
            builder.AppendLine($"    {row.AnimationName} -> [{row.NodeIndex}] {row.NodeName ?? "?"}: flags={FormatNullableHex(row.NodeFlags)}, group={row.NodeGroup ?? "?"}, cimg={row.HasImageCast}, display={FormatBool(row.InitialDisplay)}, materialAlpha={FormatDouble(row.InitialMaterialAlpha)}, keys=[{row.KeyValues}]");
        }
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

        builder.AppendLine();
        builder.AppendLine("Track flags extra masks:");
        var imageCastNodeIndexes = file.Surfboard.Resources.ImageCasts
            .Select(static imageCast => imageCast.CastIndex)
            .ToHashSet();
        foreach (var group in contexts
            .GroupBy(static context => GetTrackFlagExtraMask(context.Track.Flags!.Value))
            .OrderBy(static group => group.Key))
        {
            var items = group.ToArray();
            var cimgTargets = items.Count(context => context.NodeIndex is int nodeIndex && imageCastNodeIndexes.Contains(nodeIndex));
            var displayFalse = items.Count(context => ResolveNode(file.Surfboard.Nodes, context.NodeIndex ?? -1)?.Transform2D?.Display == false);
            builder.AppendLine($"  extra=0x{group.Key:X}: tracks={items.Length}, flags=[{FormatContextDistribution(items, static context => $"0x{context.Track.Flags!.Value:X}")}], animations=[{FormatContextDistribution(items, static context => context.AnimationName)}], nodes=[{FormatContextDistribution(items, static context => context.NodeName ?? "?")}], nodeFlags=[{FormatTrackContextNodeFlagDistribution(items, file.Surfboard.Nodes)}], groups=[{FormatTrackContextNodeGroupDistribution(items, file.Surfboard.Nodes)}], cimgTargets={cimgTargets}, displayFalse={displayFalse}, types=[{FormatContextDistribution(items, static context => $"{context.Track.TrackType}({context.Track.TrackTypeName ?? "?"})")}]");
        }

        builder.AppendLine("  extra mask samples:");
        foreach (var context in contexts.Take(32))
        {
            var track = context.Track;
            builder.AppendLine($"    {context.AnimationName} -> [{context.NodeIndex?.ToString() ?? "?"}] {context.NodeName ?? "?"}: flags=0x{track.Flags!.Value:X}, type={track.TrackType}({track.TrackTypeName ?? "?"}), frames={track.FirstFrame?.ToString() ?? "?"}..{track.LastFrame?.ToString() ?? "?"}, keys=[{FormatKeyframeSequence(track)}]");
        }
    }

    private static void AppendPackedAngleCandidateSummary(StringBuilder builder, SbSceneFile file)
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

        builder.AppendLine();
        builder.AppendLine("Packed angle candidates:");
        builder.AppendLine("  formula: degrees = raw * 180 / 32768; only applied to rotation contexts, CAM.0x14 remains flags-like/unknown");
        if (transformAngles.Length > 0)
        {
            var rawValues = transformAngles.Select(static transform => transform.RotationZRaw).ToArray();
            builder.AppendLine($"  TRS2.0x32: values={transformAngles.Length}, rawExamples=[{FormatPackedAngleRawExamples(rawValues)}], degreeExamples=[{FormatPackedAngleDegreeExamples(rawValues)}]");
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
            builder.AppendLine($"  KEY.0x5B type 0x0B: keys={packedKeys.Length}, tracks={packedTracks.Length}, types=[{typeSummary}], rawExamples=[{FormatPackedAngleRawExamples(rawValues)}], degreeExamples=[{FormatPackedAngleDegreeExamples(rawValues)}]");
        }

        if (file.Surfboard.Camera?.Flags is int cameraFlags)
        {
            builder.AppendLine($"  CAM.0x14: 0x{cameraFlags:X}, excluded from angle conversion");
        }
    }

    private static void AppendResourceSummary(StringBuilder builder, SbSceneFile file)
    {
        var resources = file.Surfboard.Resources;
        var cropCount = resources.Atlases.Sum(static atlas => atlas.Crops.Count);
        var cropReferenceCount = resources.ImageCasts.Sum(static imageCast => imageCast.CropReferences.Count);
        var primaryCropReferenceCount = resources.ImageCasts.Sum(static imageCast => imageCast.PrimaryCropReferences.Count);
        var secondaryCropReferenceCount = resources.ImageCasts.Sum(static imageCast => imageCast.SecondaryCropReferences.Count);
        var multiReferenceCasts = resources.ImageCasts.Count(static imageCast => imageCast.CropReferences.Count > 1);
        var secondaryReferenceCasts = resources.ImageCasts.Count(static imageCast => imageCast.SecondaryCropReferenceCount is > 0);
        var mismatched = resources.ImageCasts.Count(static imageCast => imageCast.CropReferenceCountMatches == false);

        builder.AppendLine();
        builder.AppendLine("Texture resources:");
        builder.AppendLine($"  TEXL={resources.TextureListName ?? "?"}, declaredTextures={resources.DeclaredTextureCount?.ToString() ?? "?"}, atlases={resources.Atlases.Count}, crops={cropCount}");
        foreach (var atlas in resources.Atlases)
        {
            builder.AppendLine($"  [{atlas.Index}] {atlas.Name}: {atlas.Width}x{atlas.Height}, field62={FormatNullableHex(atlas.Field62)}, bits=[{string.Join(",", atlas.Field62Bits)}], declaredCrops={atlas.DeclaredCropCount}, parsedCrops={atlas.Crops.Count}");
        }

        builder.AppendLine($"  TEX.0x62 shared packed state word=[{FormatNullableHexIntDistribution(resources.Atlases.Select(static atlas => atlas.Field62))}]");
        builder.AppendLine($"  imageCasts={resources.ImageCasts.Count}, cropReferences={cropReferenceCount} (primary={primaryCropReferenceCount}, secondary={secondaryCropReferenceCount}), multiReferenceCasts={multiReferenceCasts}, secondaryReferenceCasts={secondaryReferenceCasts}, countMismatches={mismatched}");
        builder.AppendLine($"  CROP.0x65 kind=[{FormatCountDistribution(resources.Atlases.SelectMany(static atlas => atlas.Crops).Select(static crop => (int)crop.Kind))}]");
        builder.AppendLine($"  CREF.0x49 kind=[{FormatCountDistribution(resources.ImageCasts.SelectMany(static imageCast => imageCast.CropReferences).Select(static reference => (int)reference.Kind))}]");
        var countMatches = resources.ImageCasts.Count(static imageCast => imageCast.CropReferenceCountMatches == true);
        var unknownCountMatches = resources.ImageCasts.Count(static imageCast => imageCast.CropReferenceCountMatches is null);
        var indexValidation = BuildCropReferenceIndexValidation(resources.ImageCasts);
        builder.AppendLine($"  CIMG.0x44 validation: matches={countMatches}/{resources.ImageCasts.Count}, mismatches={mismatched}, unknown={unknownCountMatches}");
        builder.AppendLine($"  CIMG.0x45 group index range validation: activeGroups={indexValidation.ActiveGroups}, inRange={indexValidation.InRangeGroups}, outOfRange={indexValidation.OutOfRangeGroups}, emptyGroupNonZero={indexValidation.EmptyGroupNonZeroIndices}, nonZeroIndices={indexValidation.NonZeroIndices}, nonZeroImageCasts={indexValidation.NonZeroImageCasts}");
        var countTupleSummary = string.Join(", ", resources.ImageCasts
            .GroupBy(static imageCast => $"{imageCast.PrimaryCropReferenceCount ?? 0},{imageCast.SecondaryCropReferenceCount ?? 0}")
            .OrderByDescending(static group => group.Count())
            .Select(static group => $"({group.Key}):{group.Count()}"));
        var indexTupleSummary = string.Join(", ", resources.ImageCasts
            .GroupBy(static imageCast => $"{imageCast.PrimaryCropReferenceIndex ?? 0},{imageCast.SecondaryCropReferenceIndex ?? 0}")
            .OrderByDescending(static group => group.Count())
            .Select(static group => $"({group.Key}):{group.Count()}"));
        builder.AppendLine($"  CIMG.0x44 primary/secondary counts=[{countTupleSummary}]");
        builder.AppendLine($"  CIMG.0x45 primary/secondary indices=[{indexTupleSummary}]");
        var nonZeroReferenceIndexCasts = resources.ImageCasts
            .Where(static imageCast =>
                imageCast.PrimaryCropReferences.Count > 1
                || imageCast.SecondaryCropReferences.Count > 0
                || imageCast.PrimaryCropReferenceIndex is > 0
                || imageCast.SecondaryCropReferenceIndex is > 0)
            .ToArray();
        if (nonZeroReferenceIndexCasts.Length > 0)
        {
            builder.AppendLine("  CIMG.0x45 non-zero group index samples:");
            foreach (var imageCast in nonZeroReferenceIndexCasts.Take(24))
            {
                builder.AppendLine($"    {imageCast.NodeName ?? "?"}: counts=({imageCast.PrimaryCropReferenceCount ?? 0},{imageCast.SecondaryCropReferenceCount ?? 0}), indices=({imageCast.PrimaryCropReferenceIndex ?? 0},{imageCast.SecondaryCropReferenceIndex ?? 0}), primary={FormatIndexedReference(imageCast.PrimaryCropReferences, imageCast.PrimaryCropReferenceIndex)}, secondary={FormatIndexedReference(imageCast.SecondaryCropReferences, imageCast.SecondaryCropReferenceIndex)}");
            }
        }

        builder.AppendLine("  CIMG.0x48 shared packed state word:");
        foreach (var group in resources.ImageCasts
            .GroupBy(static imageCast => imageCast.ImageCastFlags)
            .OrderBy(static group => group.Key))
        {
            var items = group.ToArray();
            var examples = string.Join(", ", items.Select(static imageCast => imageCast.NodeName ?? "?").Take(4));
            builder.AppendLine($"    0x{group.Key:X8}: bits=[{FormatImageCastBits(group.Key)}], casts={items.Length}, nodeFlags=[{FormatImageCastNodeFlagDistribution(items, file.Surfboard.Nodes)}], groups=[{FormatImageCastGroupDistribution(items, file.Surfboard.Nodes)}], displayFalse={CountImageCastsWithDisplayFalse(items, file.Surfboard.Nodes)}, multiRefs={CountMultiReferenceImageCasts(items)}, secondaryRefs={CountSecondaryReferenceImageCasts(items)}, nonZero0x45={CountNonZeroReferenceIndexImageCasts(items)} [{examples}]");
        }

        builder.AppendLine("  CIMG.0x48 packed state bits:");
        foreach (var group in Enumerable.Range(0, 32)
            .Select(bit => new
            {
                Bit = bit,
                Items = resources.ImageCasts.Where(imageCast => ((uint)imageCast.ImageCastFlags & (1u << bit)) != 0).ToArray(),
            })
            .Where(static item => item.Items.Length > 0))
        {
            var examples = string.Join(", ", group.Items.Select(static imageCast => imageCast.NodeName ?? "?").Take(4));
            builder.AppendLine($"    bit{group.Bit} mask=0x{1u << group.Bit:X8}: casts={group.Items.Length}, nodeFlags=[{FormatImageCastNodeFlagDistribution(group.Items, file.Surfboard.Nodes)}], groups=[{FormatImageCastGroupDistribution(group.Items, file.Surfboard.Nodes)}], displayFalse={CountImageCastsWithDisplayFalse(group.Items, file.Surfboard.Nodes)}, multiRefs={CountMultiReferenceImageCasts(group.Items)}, secondaryRefs={CountSecondaryReferenceImageCasts(group.Items)}, nonZero0x45={CountNonZeroReferenceIndexImageCasts(group.Items)} [{DescribeImageCastFlagBit(group.Bit)}] [{examples}]");
        }

        var bitPairs = BuildImageCastFlagBitPairs(resources.ImageCasts);
        if (bitPairs.Count > 0)
        {
            builder.AppendLine("  CIMG.0x48 bit co-occurrence:");
            foreach (var pair in bitPairs)
            {
                builder.AppendLine($"    {pair.Bits}: casts={pair.Count}, {pair.Observation}");
            }
        }

        foreach (var imageCast in resources.ImageCasts.Take(8))
        {
            var refs = string.Join(", ", imageCast.CropReferences.Take(3).Select(static reference => $"{reference.AtlasName}[{reference.CropIndex}]"));
            builder.AppendLine($"  CIMG[{imageCast.Index}] node={imageCast.NodeName ?? "?"}, size={imageCast.Width:0.###}x{imageCast.Height:0.###}, pivot=({imageCast.PivotX:0.###},{imageCast.PivotY:0.###}), refs={imageCast.CropReferences.Count} primary={imageCast.PrimaryCropReferences.Count} secondary={imageCast.SecondaryCropReferences.Count} [{refs}]");
        }

        var secondaryCasts = resources.ImageCasts.Where(static imageCast => imageCast.SecondaryCropReferences.Count > 0).ToArray();
        if (secondaryCasts.Length > 0)
        {
            builder.AppendLine("  secondary CREF casts:");
            foreach (var imageCast in secondaryCasts.Take(16))
            {
                var primary = string.Join(", ", imageCast.PrimaryCropReferences.Select(static reference => $"{reference.TextureIndex}:{reference.CropIndex}"));
                var secondary = string.Join(", ", imageCast.SecondaryCropReferences.Select(static reference => $"{reference.TextureIndex}:{reference.CropIndex}"));
                builder.AppendLine($"    {imageCast.NodeName ?? "?"}: counts=({imageCast.PrimaryCropReferenceCount},{imageCast.SecondaryCropReferenceCount}), indices=({imageCast.PrimaryCropReferenceIndex},{imageCast.SecondaryCropReferenceIndex}), primary=[{primary}], secondary=[{secondary}]");
            }
        }

        if (resources.CnumRecords.Count > 0)
        {
            builder.AppendLine("  CNUM raw resource records:");
            builder.AppendLine($"    records={resources.CnumRecords.Count}, cropRefs={resources.CnumRecords.Sum(static record => record.CropReferences.Count)}, field44Matches={resources.CnumRecords.Count(static record => record.CropReferenceCountMatchesField44 == true)}, field44Mismatches={resources.CnumRecords.Count(static record => record.CropReferenceCountMatchesField44 == false)}, field44Missing={resources.CnumRecords.Count(static record => record.CropReferenceCountMatchesField44 is null)}, field51InRange={resources.CnumRecords.Count(static record => record.NodeName is not null)}");
            builder.AppendLine($"    CNUM.0x48=[{FormatNullableIntDistribution(resources.CnumRecords.Select(static record => record.Field48))}], 0x40=[{FormatNullableFloatDistribution(resources.CnumRecords.Select(static record => record.Field40))}], 0x42=[{FormatNullableFloatDistribution(resources.CnumRecords.Select(static record => record.Field42))}], 0x43=[{FormatNullableFloatDistribution(resources.CnumRecords.Select(static record => record.Field43))}]");
            builder.AppendLine($"    CNUM.0x39Colors=[{FormatNullableColorListDistribution(resources.CnumRecords.Select(static record => record.Field39Colors))}], CNUM.0x39RawHex=[{FormatNullableStringListDistribution(resources.CnumRecords.Select(static record => record.Field39RawHexValues))}]");
            builder.AppendLine($"    CNUM.0x44=[{FormatNullableIntDistribution(resources.CnumRecords.Select(static record => record.Field44Count))}], CNUM.0xA0=[{FormatNullableIntDistribution(resources.CnumRecords.Select(static record => record.FieldA0))}], CNUM.0xA1=[{FormatNullableStringDistribution(resources.CnumRecords.Select(static record => record.FieldA1))}], markers=[{FormatNullableHexIntDistribution(resources.CnumRecords.SelectMany(static record => record.ZeroLengthMarkerFieldIds).Select(static value => (int?)value))}]");
            builder.AppendLine($"    CNUM.0xA2-A5=[{FormatNullableIntTupleDistribution(resources.CnumRecords.Select(static record => new int?[] { record.FieldA2, record.FieldA3, record.FieldA4, record.FieldA5 }))}], CNUM.0xA6-AD=[{FormatNullableIntTupleDistribution(resources.CnumRecords.Select(static record => new int?[] { record.FieldA6, record.FieldA7, record.FieldA8, record.FieldA9, record.FieldAA, record.FieldAB, record.FieldAC, record.FieldAD }))}]");
            builder.AppendLine($"    CNUM.0xAERawHex=[{FormatNullableStringDistribution(resources.CnumRecords.Select(static record => record.FieldAERawHex))}], CNUM.0xAEFloatValues=[{FormatNullableFloatListDistribution(resources.CnumRecords.Select(static record => record.FieldAEFloatValues))}]");
            builder.AppendLine($"    CNUM.0xAFRawHex=[{FormatNullableStringDistribution(resources.CnumRecords.Select(static record => record.FieldAFRawHex))}], CNUM.0xAFPackedValues=[{FormatNullableIntListDistribution(resources.CnumRecords.Select(static record => record.FieldAFPackedValues))}], CNUM.0xA1RawHex=[{FormatNullableStringDistribution(resources.CnumRecords.Select(static record => record.FieldA1RawHex))}]");
            foreach (var record in resources.CnumRecords.Take(8))
            {
                var markerIds = string.Join(",", record.ZeroLengthMarkerFieldIds.Select(static id => $"0x{id:X2}"));
                var refs = string.Join(", ", record.CropReferences.Take(4).Select(static reference => $"{reference.TextureIndex}:{reference.CropIndex}"));
                var fieldA2A5 = $"{FormatNullableInt(record.FieldA2)},{FormatNullableInt(record.FieldA3)},{FormatNullableInt(record.FieldA4)},{FormatNullableInt(record.FieldA5)}";
                var fieldA6AD = $"{FormatNullableInt(record.FieldA6)},{FormatNullableInt(record.FieldA7)},{FormatNullableInt(record.FieldA8)},{FormatNullableInt(record.FieldA9)},{FormatNullableInt(record.FieldAA)},{FormatNullableInt(record.FieldAB)},{FormatNullableInt(record.FieldAC)},{FormatNullableInt(record.FieldAD)}";
                builder.AppendLine($"    CNUM[{record.Index}] node={record.NodeName ?? "?"}, field51={record.Field51?.ToString() ?? "?"}, field48={record.Field48?.ToString() ?? "?"}, fields40/42/43=({FormatFloat(record.Field40)},{FormatFloat(record.Field42)},{FormatFloat(record.Field43)}), field39Colors={FormatColorList(record.Field39Colors)}, field44={record.Field44Count?.ToString() ?? "?"}, cropRefs={record.CropReferences.Count}, field44Match={record.CropReferenceCountMatchesField44?.ToString() ?? "?"}, fieldA0={record.FieldA0?.ToString() ?? "?"}, fieldA2A5=({fieldA2A5}), fieldA6AD=({fieldA6AD}), fieldAEFloatValues={FormatFloatList(record.FieldAEFloatValues)}, fieldAERawHex={record.FieldAERawHex ?? "?"}, fieldAFPackedValues={FormatIntList(record.FieldAFPackedValues)}, fieldAFRawHex={record.FieldAFRawHex ?? "?"}, fieldA1={record.FieldA1 ?? "?"}, fieldA1RawHex={record.FieldA1RawHex ?? "?"}, markers=[{markerIds}], refs=[{refs}]");
            }
        }

        if (resources.CrfdRecords.Count > 0)
        {
            builder.AppendLine("  CRFD raw resource records:");
            builder.AppendLine($"    records={resources.CrfdRecords.Count}, field51InRange={resources.CrfdRecords.Count(static record => record.NodeName is not null)}, field51OutOfRange={resources.CrfdRecords.Count(static record => record.Field51 is not null && record.NodeName is null)}, field51Missing={resources.CrfdRecords.Count(static record => record.Field51 is null)}, field94NonZero={resources.CrfdRecords.Count(static record => IsNonZero(record.Field94))}");
            builder.AppendLine($"    CRFD.0x90=[{FormatNullableStringDistribution(resources.CrfdRecords.Select(static record => record.Field90))}], CRFD.0x91=[{FormatNullableStringDistribution(resources.CrfdRecords.Select(static record => record.Field91))}]");
            builder.AppendLine($"    CRFD.0x90RawHex=[{FormatNullableStringDistribution(resources.CrfdRecords.Select(static record => record.Field90RawHex))}], CRFD.0x91RawHex=[{FormatNullableStringDistribution(resources.CrfdRecords.Select(static record => record.Field91RawHex))}]");
            builder.AppendLine($"    CRFD.0x92=[{FormatNullableIntDistribution(resources.CrfdRecords.Select(static record => record.Field92))}], CRFD.0x93=[{FormatNullableIntDistribution(resources.CrfdRecords.Select(static record => record.Field93))}], CRFD.0x94=[{FormatNullableFloatDistribution(resources.CrfdRecords.Select(static record => record.Field94))}], CRFD.0x95=[{FormatNullableIntDistribution(resources.CrfdRecords.Select(static record => record.Field95))}]");
            foreach (var record in resources.CrfdRecords.Take(8))
            {
                builder.AppendLine($"    CRFD[{record.Index}] node={record.NodeName ?? "?"}, field51={record.Field51?.ToString() ?? "?"}, field90={record.Field90 ?? "?"}, field90RawHex={record.Field90RawHex ?? "?"}, field91={record.Field91 ?? "?"}, field91RawHex={record.Field91RawHex ?? "?"}, field92={record.Field92?.ToString() ?? "?"}, field93={record.Field93?.ToString() ?? "?"}, field94={FormatFloat(record.Field94)}, field95={record.Field95?.ToString() ?? "?"}");
            }
        }

        if (resources.TextRecords.Count > 0)
        {
            builder.AppendLine("  TEXT raw records:");
            builder.AppendLine($"    records={resources.TextRecords.Count}, field7APresent={resources.TextRecords.Count(static record => !string.IsNullOrEmpty(record.Field7A))}, markers=[{FormatNullableHexIntDistribution(resources.TextRecords.SelectMany(static record => record.ZeroLengthMarkerFieldIds).Select(static value => (int?)value))}]");
            builder.AppendLine($"    TEXT.0x33Vector=[{FormatNullableStringDistribution(resources.TextRecords.Select(static record => FormatVector(record.Field33Vector)))}], TEXT.0x33RawHex=[{FormatNullableStringDistribution(resources.TextRecords.Select(static record => record.Field33RawHex))}]");
            builder.AppendLine($"    TEXT.0x41=[{FormatNullableIntDistribution(resources.TextRecords.Select(static record => record.Field41))}], TEXT.0x78=[{FormatNullableIntDistribution(resources.TextRecords.Select(static record => record.Field78))}], TEXT.0x79=[{FormatNullableIntDistribution(resources.TextRecords.Select(static record => record.Field79))}], TEXT.0x7C=[{FormatNullableIntDistribution(resources.TextRecords.Select(static record => record.Field7C))}]");
            builder.AppendLine($"    TEXT.0x7AShiftJis=[{FormatNullableStringDistribution(resources.TextRecords.Select(static record => record.Field7AShiftJis))}]");
            builder.AppendLine($"    TEXT.0x7ARawHex=[{FormatNullableStringDistribution(resources.TextRecords.Select(static record => record.Field7ARawHex))}]");
            builder.AppendLine($"    TEXT.0x7BPackedValues=[{FormatNullableIntListDistribution(resources.TextRecords.Select(static record => record.Field7BPackedValues))}], TEXT.0x7BRawHex=[{FormatNullableStringDistribution(resources.TextRecords.Select(static record => record.Field7BRawHex))}]");
            foreach (var record in resources.TextRecords.Take(8))
            {
                var markerIds = string.Join(",", record.ZeroLengthMarkerFieldIds.Select(static id => $"0x{id:X2}"));
                var shiftJisPreview = record.Field7AShiftJis is { Length: > 48 } shiftJisValue ? shiftJisValue[..48] + "..." : record.Field7AShiftJis ?? "?";
                var utf8Preview = record.Field7A is { Length: > 48 } utf8Value ? utf8Value[..48] + "..." : record.Field7A ?? "?";
                builder.AppendLine($"    TEXT[{record.Index}] field33Vector={FormatVector(record.Field33Vector)}, field33RawHex={record.Field33RawHex ?? "?"}, field41={record.Field41?.ToString() ?? "?"}, field78={record.Field78?.ToString() ?? "?"}, field79={record.Field79?.ToString() ?? "?"}, field7C={record.Field7C?.ToString() ?? "?"}, field7AShiftJis={shiftJisPreview}, field7A={utf8Preview}, field7ARawHex={record.Field7ARawHex ?? "?"}, field7BPackedValues={FormatIntList(record.Field7BPackedValues)}, field7BRawHex={record.Field7BRawHex ?? "?"}, markers=[{markerIds}]");
            }
        }

        if (resources.SliceCasts.Count > 0)
        {
            var sliceRecords = resources.SliceCasts.SelectMany(static sliceCast => sliceCast.Slices).ToArray();
            builder.AppendLine("  CSLI/SLIC slice records:");
            builder.AppendLine($"    sliceCasts={resources.SliceCasts.Count}, records={sliceRecords.Length}, cropRefs={resources.SliceCasts.Sum(static sliceCast => sliceCast.CropReferences.Count)}, field44VsSlicMatches={resources.SliceCasts.Count(static sliceCast => sliceCast.SlicRecordCountMatchesField44 == true)}, field44VsSlicMismatches={resources.SliceCasts.Count(static sliceCast => sliceCast.SlicRecordCountMatchesField44 == false)}, field44VsCrefMatches={resources.SliceCasts.Count(static sliceCast => sliceCast.CropReferenceCountMatchesField44 == true)}, field44VsCrefMismatches={resources.SliceCasts.Count(static sliceCast => sliceCast.CropReferenceCountMatchesField44 == false)}, targetInRange={resources.SliceCasts.Count(static sliceCast => sliceCast.NodeName is not null)}");
            builder.AppendLine($"    CSLI.0x40-43=[{FormatNullableFloatTupleDistribution(resources.SliceCasts.Select(static sliceCast => new float?[] { sliceCast.Field40, sliceCast.Field41, sliceCast.Field42, sliceCast.Field43 }))}]");
            builder.AppendLine($"    CSLI.0x80-87=[{FormatNullableIntFloatTupleDistribution(resources.SliceCasts.Select(static sliceCast => (sliceCast.Field80, sliceCast.Field81, sliceCast.Field82, sliceCast.Field84, sliceCast.Field85, sliceCast.Field86, sliceCast.Field87)))}]");
            builder.AppendLine($"    SLIC.0x83=[{FormatNullableIntDistribution(sliceRecords.Select(static slice => slice.Field83))}], SLIC.0x40=[{FormatNullableIntDistribution(sliceRecords.Select(static slice => slice.Field40))}], SLIC.0x41=[{FormatNullableIntDistribution(sliceRecords.Select(static slice => slice.Field41))}], SLIC.0x45=[{FormatNullableIntDistribution(sliceRecords.Select(static slice => slice.Field45))}]");
            builder.AppendLine($"    SLIC.0x37Color=[{FormatColorDistribution(sliceRecords.Select(static slice => slice.Field37Color))}], SLIC.0x37RawHex=[{FormatNullableStringDistribution(sliceRecords.Select(static slice => slice.Field37RawHex))}]");
            builder.AppendLine($"    SLIC.0x38Color=[{FormatColorDistribution(sliceRecords.Select(static slice => slice.Field38Color))}], SLIC.0x38RawHex=[{FormatNullableStringDistribution(sliceRecords.Select(static slice => slice.Field38RawHex))}]");
            builder.AppendLine($"    SLIC.0x39Colors=[{FormatNullableColorListDistribution(sliceRecords.Select(static slice => slice.Field39Colors))}], SLIC.0x39RawHex=[{FormatNullableStringListDistribution(sliceRecords.Select(static slice => slice.Field39RawHexValues))}]");
            foreach (var sliceCast in resources.SliceCasts.Take(8))
            {
                var slicePreview = string.Join(", ", sliceCast.Slices
                    .Take(6)
                    .Select(static slice => $"#{slice.Index}:83={slice.Field83?.ToString() ?? "?"},40={slice.Field40?.ToString() ?? "?"},41={slice.Field41?.ToString() ?? "?"},45={slice.Field45?.ToString() ?? "?"},37={slice.Field37Color?.Hex ?? "?"},39={FormatColorList(slice.Field39Colors)},38={slice.Field38Color?.Hex ?? "?"}"));
                var csliTail = $"{FormatNullableInt(sliceCast.Field80)},{FormatNullableInt(sliceCast.Field81)},{FormatNullableInt(sliceCast.Field82)},{FormatNullableInt(sliceCast.Field84)},{FormatNullableInt(sliceCast.Field85)},{FormatFloat(sliceCast.Field86)},{FormatFloat(sliceCast.Field87)}";
                builder.AppendLine($"    CSLI[{sliceCast.Index}] node={sliceCast.NodeName ?? "?"}, target={sliceCast.TargetIndex?.ToString() ?? "?"}, field44={sliceCast.Field44Count?.ToString() ?? "?"}, records={sliceCast.Slices.Count}, cropRefs={sliceCast.CropReferences.Count}, field44VsSlic={sliceCast.SlicRecordCountMatchesField44?.ToString() ?? "?"}, field44VsCref={sliceCast.CropReferenceCountMatchesField44?.ToString() ?? "?"}, fields40-43=({FormatFloat(sliceCast.Field40)},{FormatFloat(sliceCast.Field41)},{FormatFloat(sliceCast.Field42)},{FormatFloat(sliceCast.Field43)}), fields80-87=({csliTail}), slices=[{slicePreview}]");
            }
        }

        if (file.Surfboard.NodeCategoryRecords.Count > 0)
        {
            var categorySummary = string.Join(", ", file.Surfboard.NodeCategoryRecords
                .GroupBy(static value => value)
                .OrderBy(static group => group.Key)
                .Select(static group => $"{group.Key}:{group.Count()}"));
            builder.AppendLine($"  NCAT categories=[{categorySummary}]");
        }
    }

    private static void AppendCamera(StringBuilder builder, SbSceneFile file)
    {
        if (file.Surfboard.Camera is not { } camera)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine("Camera:");
        builder.AppendLine($"  {camera.Name ?? "CAM"}: position={FormatVector(camera.Position)}, target={FormatVector(camera.Target)}, flags=0x{(camera.Flags ?? 0):X}, near={FormatFloat(camera.NearClip)}, far={FormatFloat(camera.FarClip)}");
    }

    private static void AppendInterpolationSummary(StringBuilder builder, SbSceneFile file)
    {
        var contexts = BuildTrackContexts(file);
        var tracks = contexts.Select(static context => context.Track).ToArray();
        var keys = tracks.SelectMany(static track => track.Keyframes).ToArray();

        builder.AppendLine();
        builder.AppendLine("Key interpolation:");
        foreach (var group in keys
            .Where(static key => key.Interpolation is not null)
            .GroupBy(static key => new { key.Interpolation, key.InterpolationName })
            .OrderBy(static group => group.Key.Interpolation))
        {
            builder.AppendLine($"  {group.Key.Interpolation}({group.Key.InterpolationName ?? "?"}): keys={group.Count()}");
        }

        var tangentPairs = keys.Where(static key => key.TangentIn is not null || key.TangentOut is not null).ToArray();
        var sameTangents = tangentPairs.Count(static key => key.TangentIn == key.TangentOut);
        var mismatchedTangents = tangentPairs.Where(static key => key.TangentIn != key.TangentOut).ToArray();
        var nonZeroTangents = tangentPairs.Count(static key => IsNonZero(key.TangentIn) || IsNonZero(key.TangentOut));
        builder.AppendLine($"  tangents: present={tangentPairs.Length}, inEqualsOut={sameTangents}, inNotEqualsOut={mismatchedTangents.Length}, nonZero={nonZeroTangents}");
        foreach (var group in keys
            .Where(static key => key.Interpolation is not null)
            .GroupBy(static key => new { key.Interpolation, key.InterpolationName })
            .OrderBy(static group => group.Key.Interpolation))
        {
            builder.AppendLine($"  tangentByInterpolation {group.Key.Interpolation}({group.Key.InterpolationName ?? "?"}): keys={group.Count()}, nonZero={group.Count(static key => IsNonZero(key.TangentIn) || IsNonZero(key.TangentOut))}");
        }

        foreach (var group in tracks
            .Select(track => new
            {
                Track = track,
                NonZero = track.Keyframes.Count(static key => IsNonZero(key.TangentIn) || IsNonZero(key.TangentOut)),
            })
            .Where(static item => item.NonZero > 0)
            .GroupBy(static item => new { item.Track.TrackType, item.Track.TrackTypeName })
            .OrderByDescending(static group => group.Sum(static item => item.NonZero))
            .Take(8))
        {
            builder.AppendLine($"  tangentNonZero type={group.Key.TrackType}({group.Key.TrackTypeName ?? "?"}): keys={group.Sum(static item => item.NonZero)}, tracks={group.Count()}");
        }

        if (mismatchedTangents.Length > 0)
        {
            foreach (var group in mismatchedTangents
                .Where(static key => key.Interpolation is not null)
                .GroupBy(static key => new { key.Interpolation, key.InterpolationName })
                .OrderBy(static group => group.Key.Interpolation))
            {
                builder.AppendLine($"  tangentMismatchByInterpolation {group.Key.Interpolation}({group.Key.InterpolationName ?? "?"}): keys={group.Count()}");
            }

            foreach (var group in contexts
                .Select(context => new
                {
                    Context = context,
                    Mismatched = context.Track.Keyframes.Count(static key => key.TangentIn != key.TangentOut),
                })
                .Where(static item => item.Mismatched > 0)
                .GroupBy(static item => new { item.Context.Track.TrackType, item.Context.Track.TrackTypeName })
                .OrderByDescending(static group => group.Sum(static item => item.Mismatched))
                .Take(8))
            {
                builder.AppendLine($"  tangentMismatch type={group.Key.TrackType}({group.Key.TrackTypeName ?? "?"}): keys={group.Sum(static item => item.Mismatched)}, tracks={group.Count()}");
            }

            builder.AppendLine("  tangent mismatch samples:");
            foreach (var sample in contexts
                .SelectMany(static context => context.Track.Keyframes
                    .Where(static key => key.TangentIn != key.TangentOut)
                    .Select(key => new { Context = context, Key = key }))
                .Take(16))
            {
                builder.AppendLine($"    {sample.Context.AnimationName} -> {sample.Context.NodeName ?? "?"}: type={sample.Context.Track.TrackType}({sample.Context.Track.TrackTypeName ?? "?"}), frame={sample.Key.KeyFrame}, value={FormatKeyValue(sample.Context.Track, sample.Key)}, interp={sample.Key.Interpolation}({sample.Key.InterpolationName ?? "?"}), tan=({FormatDouble(sample.Key.TangentIn)},{FormatDouble(sample.Key.TangentOut)})");
            }
        }

        builder.AppendLine("  nonZero tangent samples:");
        foreach (var sample in contexts
            .SelectMany(static context => context.Track.Keyframes
                .Where(static key => IsNonZero(key.TangentIn) || IsNonZero(key.TangentOut))
                .Select(key => new { Context = context, Key = key }))
            .Take(16))
        {
            builder.AppendLine($"    {sample.Context.AnimationName} -> {sample.Context.NodeName ?? "?"}: type={sample.Context.Track.TrackType}({sample.Context.Track.TrackTypeName ?? "?"}), frame={sample.Key.KeyFrame}, value={FormatKeyValue(sample.Context.Track, sample.Key)}, interp={sample.Key.Interpolation}({sample.Key.InterpolationName ?? "?"}), tan=({FormatDouble(sample.Key.TangentIn)},{FormatDouble(sample.Key.TangentOut)})");
        }
    }

    private static void AppendTrackKeyCountSummary(StringBuilder builder, SbSceneFile file)
    {
        var tracks = file.Surfboard.Animations.SelectMany(static animation => animation.Motions).SelectMany(static motion => motion.Tracks).ToArray();
        var declared = tracks.Where(static track => track.DeclaredKeyCountFromTrack is not null).ToArray();
        var matching = declared.Count(static track => track.KeyCountMatchesDeclaration == true);
        var mismatching = declared.Length - matching;

        builder.AppendLine();
        builder.AppendLine("Track key count declarations:");
        builder.AppendLine($"  TRK.0x57 present={declared.Length}, matching KEY.ParamHigh/5 and parsed keys={matching}, mismatch={mismatching}");
        foreach (var group in declared
            .GroupBy(static track => track.DeclaredKeyCountFromTrack!.Value)
            .OrderBy(static group => group.Key)
            .Take(24))
        {
            builder.AppendLine($"  keyCount={group.Key}: tracks={group.Count()}");
        }
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

    private static void AppendStateTrackSummary(StringBuilder builder, SbSceneFile file)
    {
        var rows = BuildStateTrackRows(file);
        if (rows.Count == 0)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine("State switch tracks:");
        foreach (var group in rows
            .GroupBy(static row => new { row.TrackType, row.TrackTypeName })
            .OrderBy(static group => group.Key.TrackType))
        {
            builder.AppendLine($"  type={group.Key.TrackType}({group.Key.TrackTypeName ?? "?"}): tracks={group.Count()}, keys={group.Sum(static row => row.KeyCount)}");
        }

        builder.AppendLine("  by animation:");
        foreach (var group in rows
            .GroupBy(static row => row.AnimationName, StringComparer.Ordinal)
            .OrderBy(static group => group.First().AnimationIndex))
        {
            builder.AppendLine($"    {group.Key}: display={group.Count(static row => row.TrackType == 11)}, primaryVariant={group.Count(static row => row.TrackType == 18)}, secondaryVariant={group.Count(static row => row.TrackType == 19)}, alpha={group.Count(static row => row.TrackType == 24)}");
        }

        builder.AppendLine("  focused samples:");
        foreach (var row in rows.Where(static row => IsFocusedStateAnimation(row.AnimationName)).Take(80))
        {
            var imageCheck = row.ImageValuesWithinReferenceCount switch
            {
                true => " refs=OK",
                false => " refs=Mismatch",
                null => string.Empty,
            };
            var refs = string.IsNullOrWhiteSpace(row.ImageReferenceSummary) ? string.Empty : $" imageRefs=[{row.ImageReferenceSummary}]";
            builder.AppendLine($"    {row.AnimationName} -> [{row.NodeIndex?.ToString() ?? "?"}] {row.NodeName ?? "?"}: type={row.TrackType}({row.TrackTypeName ?? "?"}), keys={row.KeyCount}, values=[{row.Values}]{refs}{imageCheck}");
        }
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
                $"{group.Key.TypeName} {group.Key.TypeHex}",
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
            .Select(static group => $"{group.Key}:{group.Count()}"));
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
            return text.Length <= 32 ? $"\"{text}\"" : $"\"{text[..32]}...\"";
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
            0 => "renderable/image-cast node candidate",
            8 => "common node attribute; differentiates 0xFxx from 0xExx",
            9 => "common node attribute",
            10 => "common node attribute",
            11 => "common node/control attribute",
            15 => "root/control node candidate",
            16 => "sparse special node candidate",
            _ => "unknown",
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
            return $"{FormatDouble(degrees)}deg(raw={transform.RotationZRaw?.ToString() ?? "?"})";
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

    private static string FormatIndexedReference(IReadOnlyList<SbSceneCropReference> references, int? index)
    {
        if (references.Count == 0)
        {
            return "-";
        }

        var groupIndex = index ?? 0;
        if (groupIndex < 0 || groupIndex >= references.Count)
        {
            return $"out-of-range:{groupIndex}";
        }

        var reference = references[groupIndex];
        return $"{groupIndex}->{reference.TextureIndex}:{reference.CropIndex}";
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

    private static void AppendNodeTree(StringBuilder builder, IReadOnlyList<NodeInfo> nodes, int index, int depth, int maxNodes)
    {
        var emitted = 0;
        AppendNodeTreeCore(builder, nodes, index, depth, maxNodes, ref emitted);
    }

    private static void AppendNodeTreeCore(StringBuilder builder, IReadOnlyList<NodeInfo> nodes, int index, int depth, int maxNodes, ref int emitted)
    {
        while (index >= 0 && index < nodes.Count && emitted < maxNodes)
        {
            var node = nodes[index];
            builder.AppendLine($"{new string(' ', depth * 2)}- [{node.Index}] {node.Name ?? "(unnamed)"} flags=0x{(node.Flags ?? 0):X} child={node.ChildIndex ?? -1} sibling={node.SiblingIndex ?? -1}");
            emitted++;

            if (node.ChildIndex is int childIndex && childIndex >= 0)
            {
                AppendNodeTreeCore(builder, nodes, childIndex, depth + 1, maxNodes, ref emitted);
            }

            if (node.SiblingIndex is not int siblingIndex || siblingIndex < 0)
            {
                break;
            }

            index = siblingIndex;
        }
    }

    private static string FormatVector(Vector2Value? value)
    {
        return value is null ? "?" : $"({value.X:0.###},{value.Y:0.###})";
    }

    private static string FormatVector(Vector3Value? value)
    {
        return value is null ? "?" : $"({value.X:0.###},{value.Y:0.###},{value.Z:0.###})";
    }

    private static string FormatFloat(float? value)
    {
        return value?.ToString("0.###") ?? "?";
    }

    private static string FormatNullableInt(int? value)
    {
        return value?.ToString() ?? "?";
    }

    private static string FormatBool(bool? value)
    {
        return value?.ToString() ?? "?";
    }

    private static string FormatNullableHex(int? value)
    {
        return value is null ? "?" : $"0x{value.Value:X}";
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
            .Take(8)
            .Select(static group => $"{group.Key}:{group.Count()}"));
    }

    private static string FormatNullableColorListDistribution(IEnumerable<IReadOnlyList<ColorArgbValue>?> values)
    {
        return string.Join(", ", values
            .Select(static value => value is { Count: > 0 } ? FormatColorList(value) : "<null>")
            .GroupBy(static value => value, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Take(12)
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
            .Take(12)
            .Select(static group => $"{group.Key}:{group.Count()}"));
    }

    private static string FormatNullableStringListDistribution(IEnumerable<IReadOnlyList<string>?> values)
    {
        return string.Join(", ", values
            .Select(static value => value is { Count: > 0 } ? FormatStringList(value) : "<null>")
            .GroupBy(static value => value, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Take(12)
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
            .Take(12)
            .Select(static group => $"{group.Key}:{group.Count()}"));
    }

    private static string FormatNullableFloatListDistribution(IEnumerable<IReadOnlyList<float>?> values)
    {
        return string.Join(", ", values
            .Select(static value => value is { Count: > 0 } ? FormatFloatList(value) : "<null>")
            .GroupBy(static value => value, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Take(12)
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
            .Take(12)
            .Select(static group => $"{group.Key}:{group.Count()}"));
    }

    private static string FormatNullableIntDistribution(IEnumerable<int?> values)
    {
        return string.Join(", ", values
            .Select(static value => value?.ToString() ?? "<null>")
            .GroupBy(static value => value, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Take(12)
            .Select(static group => $"{group.Key}:{group.Count()}"));
    }

    private static string FormatNullableIntTupleDistribution(IEnumerable<IReadOnlyList<int?>> values)
    {
        return string.Join(", ", values
            .Select(static value => "[" + string.Join(", ", value.Select(static item => item?.ToString() ?? "?")) + "]")
            .GroupBy(static value => value, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Take(12)
            .Select(static group => $"{group.Key}:{group.Count()}"));
    }

    private static string FormatNullableFloatTupleDistribution(IEnumerable<IReadOnlyList<float?>> values)
    {
        return string.Join(", ", values
            .Select(static value => "[" + string.Join(", ", value.Select(static item => item?.ToString("0.###") ?? "?")) + "]")
            .GroupBy(static value => value, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Take(12)
            .Select(static group => $"{group.Key}:{group.Count()}"));
    }

    private static string FormatNullableIntFloatTupleDistribution(IEnumerable<(int?, int?, int?, int?, int?, float?, float?)> values)
    {
        return string.Join(", ", values
            .Select(static value => $"[{value.Item1?.ToString() ?? "?"}, {value.Item2?.ToString() ?? "?"}, {value.Item3?.ToString() ?? "?"}, {value.Item4?.ToString() ?? "?"}, {value.Item5?.ToString() ?? "?"}, {value.Item6?.ToString("0.###") ?? "?"}, {value.Item7?.ToString("0.###") ?? "?"}]")
            .GroupBy(static value => value, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Take(12)
            .Select(static group => $"{group.Key}:{group.Count()}"));
    }

    private static string FormatNullableFloatDistribution(IEnumerable<float?> values)
    {
        return string.Join(", ", values
            .Select(static value => value?.ToString("0.###") ?? "<null>")
            .GroupBy(static value => value, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Take(12)
            .Select(static group => $"{group.Key}:{group.Count()}"));
    }

    private static string FormatCountDistribution(IEnumerable<int> values)
    {
        return string.Join(", ", values
            .GroupBy(static value => value)
            .OrderBy(static group => group.Key)
            .Take(16)
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
            return "both bits always appear together in this sample";
        }

        if (count == leftCount)
        {
            return $"bit {left} is a subset of bit {right} in this sample";
        }

        if (count == rightCount)
        {
            return $"bit {right} is a subset of bit {left} in this sample";
        }

        return "partial overlap in this sample";
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
            0 => "observed bit in CIMG samples; subset of bit 22 in current samples",
            15 => "high-coverage bit in CIMG samples; not required globally",
            20 => "observed high bit; often paired with bit 23",
            21 => "sparse observed high bit",
            22 => "observed high bit; common in CIMG samples",
            23 => "observed high bit; often paired with bit 20",
            _ => "unknown",
        };
    }
}
