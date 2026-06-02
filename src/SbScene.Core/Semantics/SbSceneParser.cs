using System.Text.RegularExpressions;
using SbScene.Core.Resources;
using SbScene.Core.Vtbf;

namespace SbScene.Core.Semantics;

public sealed partial class SbSceneParser
{
    private static readonly ISet<string> StructuralTags = new HashSet<string>(StringComparer.Ordinal)
    {
        "SRFF",
        "SRCK",
        "PROJ",
        "SCN ",
        "SCN",
        "LAYR",
        "CAST",
    };

    public SbSceneFile ParseFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var bytes = File.ReadAllBytes(path);
        var vtbf = new VtbfParser().Parse(bytes);
        var surfboard = Analyze(vtbf);
        var totalBlocks = vtbf.BlockCounts.Values.Sum();
        var summary = new ParseSummary
        {
            RootBlockCount = vtbf.Blocks.Count,
            TotalBlockCount = totalBlocks,
            NodeCount = surfboard.Nodes.Count,
            AnimationCount = surfboard.Animations.Count,
            VariantHintCount = surfboard.VariantHints.Count,
            BlockCounts = vtbf.BlockCounts,
            Warnings = vtbf.Warnings,
        };

        return new SbSceneFile
        {
            SourcePath = path,
            SourceSize = bytes.Length,
            Vtbf = vtbf,
            Surfboard = surfboard,
            Summary = summary,
        };
    }

    public SurfboardModel Analyze(VtbfDocument document)
    {
        var blocks = document.Blocks.SelectMany(Flatten).ToArray();
        var objects = blocks
            .Where(static block => StructuralTags.Contains(block.Tag))
            .Select(ToSceneObject)
            .ToArray();

        var transform2DRecords = BuildTransform2DRecords(blocks);
        var nodeCategoryRecords = BuildNodeCategoryRecords(blocks);
        var nodeCategoryDetails = BuildNodeCategoryDetails(blocks);
        var nodes = BuildNodes(blocks, transform2DRecords, nodeCategoryRecords);
        var resources = SbSceneTextureParser.ParseResourceMap(document, nodes);
        var camera = BuildCamera(blocks);

        var nodeGroups = nodes
            .GroupBy(static node => node.Group, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(static group => new NodeGroupInfo
            {
                Name = group.Key,
                Count = group.Count(),
                NodeNames = group.Select(static node => node.Name ?? $"NODE@0x{node.Offset:X}").Take(64).ToArray(),
            })
            .ToArray();

        var animations = BuildAnimations(blocks, nodes);
        var animationBindings = BuildAnimationBindings(nodes, animations);

        var hints = VariantHintAnalyzer.Build(nodes, animations);
        var unknownFields = blocks
            .SelectMany(static block => block.Fields
                .Where(static field => !field.IsKnownType)
                .Select(field => new UnknownFieldInfo
                {
                    OwnerTag = block.Tag,
                    OwnerPath = block.Path,
                    Offset = field.Offset,
                    IdHex = field.IdHex,
                    TypeHex = field.TypeHex,
                    Count = field.Count,
                    Stride = field.Stride,
                    Preview = field.Preview,
                }))
            .ToArray();

        return new SurfboardModel
        {
            Objects = objects,
            Nodes = nodes,
            Transform2DRecords = transform2DRecords,
            NodeCategoryRecords = nodeCategoryRecords,
            NodeCategoryDetails = nodeCategoryDetails,
            NodeGroups = nodeGroups,
            Resources = resources,
            Camera = camera,
            Animations = animations,
            AnimationBindings = animationBindings,
            VariantHints = hints,
            UnknownFields = unknownFields,
        };
    }

    private static SceneObjectInfo ToSceneObject(VtbfBlock block)
    {
        var stringFields = GetStringFields(block);
        return new SceneObjectInfo
        {
            Tag = block.Tag,
            Offset = block.Offset,
            Path = block.Path,
            Name = SelectName(stringFields),
            StringFields = stringFields,
            NumericFields = GetNumericFields(block),
            ChildTags = block.Children.Select(static child => child.Tag).Distinct(StringComparer.Ordinal).ToArray(),
        };
    }

    private static NodeInfo ToNode(VtbfBlock block, int index, IReadOnlyList<int> nodeCategoryRecords)
    {
        var stringFields = GetStringFields(block);
        var name = SelectName(stringFields);
        var childTags = block.Children.Select(static child => child.Tag).ToArray();
        return new NodeInfo
        {
            Index = index,
            Offset = block.Offset,
            Path = block.Path,
            Name = name,
            Flags = GetInt(block.Fields, 0x30),
            FlagBits = BuildFlagBits(GetInt(block.Fields, 0x30)),
            ChildIndex = GetInt(block.Fields, 0x3B),
            SiblingIndex = GetInt(block.Fields, 0x3C),
            Comment = SelectComment(stringFields),
            CategoryId = index < nodeCategoryRecords.Count ? nodeCategoryRecords[index] : null,
            Group = ClassifyNodeGroup(name),
            Transform2D = null,
            HasTransform2 = childTags.Contains("TRS2", StringComparer.Ordinal),
            HasTransform3 = childTags.Contains("TRS3", StringComparer.Ordinal),
            HasData = childTags.Contains("DATA", StringComparer.Ordinal),
            HasCategory = childTags.Contains("NCAT", StringComparer.Ordinal),
            StringFields = stringFields,
            NumericFields = GetNumericFields(block),
            ChildTags = childTags,
        };
    }

    private static IReadOnlyList<NodeInfo> BuildNodes(
        IReadOnlyList<VtbfBlock> blocks,
        IReadOnlyList<Transform2DInfo> transform2DRecords,
        IReadOnlyList<int> nodeCategoryRecords)
    {
        var recordNodeBlocks = blocks
            .Where(static block => block.Tag == "NODE" && block.Fields.Any(IsRecordMarker))
            .ToArray();
        if (recordNodeBlocks.Length > 0)
        {
            var recordNodes = new List<NodeInfo>();
            foreach (var block in recordNodeBlocks)
            {
                recordNodes.AddRange(ToNodeRecords(block, recordNodes.Count, transform2DRecords, nodeCategoryRecords));
            }

            return recordNodes;
        }

        return blocks
            .Where(static block => block.Tag == "NODE")
            .Select((block, index) => ToNode(block, index, nodeCategoryRecords))
            .ToArray();
    }

    private static IEnumerable<NodeInfo> ToNodeRecords(
        VtbfBlock block,
        int indexBase,
        IReadOnlyList<Transform2DInfo> transform2DRecords,
        IReadOnlyList<int> nodeCategoryRecords)
    {
        var groups = SplitRecords(block.Fields).ToArray();
        for (var i = 0; i < groups.Length; i++)
        {
            var fields = groups[i];
            var stringFields = fields
                .Where(static field => !string.IsNullOrWhiteSpace(field.StringValue))
                .Select(ToFieldValue)
                .ToArray();
            var numericFields = fields
                .Where(static field => field.Int64Values is { Length: > 0 } || field.Float64Values is { Length: > 0 })
                .Select(ToFieldValue)
                .ToArray();
            var name = SelectName(stringFields);
            var offset = fields.Count > 0 ? fields[0].Offset : block.Offset;
            var index = indexBase + i;

            yield return new NodeInfo
            {
                Index = index,
                Offset = offset,
                Path = $"{block.Path}/record[{index}]",
                Name = name,
                Flags = GetInt(fields, 0x30),
                FlagBits = BuildFlagBits(GetInt(fields, 0x30)),
                ChildIndex = GetInt(fields, 0x3B),
                SiblingIndex = GetInt(fields, 0x3C),
                Comment = SelectComment(stringFields),
                CategoryId = index < nodeCategoryRecords.Count ? nodeCategoryRecords[index] : null,
                Group = ClassifyNodeGroup(name),
                Transform2D = index < transform2DRecords.Count ? transform2DRecords[index] : null,
                HasTransform2 = true,
                HasTransform3 = false,
                HasData = true,
                HasCategory = false,
                StringFields = stringFields,
                NumericFields = numericFields,
                ChildTags = Array.Empty<string>(),
            };
        }
    }

    private static IReadOnlyList<int> BuildNodeCategoryRecords(IReadOnlyList<VtbfBlock> blocks)
    {
        var result = new List<int>();
        foreach (var block in blocks.Where(static block => block.Tag == "NCAT"))
        {
            var source = block.Fields.Any(IsRecordMarker)
                ? SplitRecords(block.Fields).SelectMany(static record => record)
                : block.Fields;

            result.AddRange(source
                .Where(static field => field.Id == 0x0E)
                .Select(static field => field.Int64Values?.FirstOrDefault())
                .Where(static value => value is >= int.MinValue and <= int.MaxValue)
                .Select(static value => (int)value!.Value));
        }

        return result;
    }

    private static IReadOnlyList<NodeCategoryInfo> BuildNodeCategoryDetails(IReadOnlyList<VtbfBlock> blocks)
    {
        var result = new List<NodeCategoryInfo>();
        foreach (var block in blocks.Where(static block => block.Tag == "NCAT"))
        {
            var records = block.Fields.Any(IsRecordMarker)
                ? SplitRecords(block.Fields).ToArray()
                : block.Fields.Select(static field => (IReadOnlyList<VtbfField>)[field]).ToArray();

            for (var i = 0; i < records.Length; i++)
            {
                var fields = records[i];
                var parameterField = fields.FirstOrDefault(static field => field.Id == 0x0F);
                result.Add(new NodeCategoryInfo
                {
                    Index = result.Count,
                    Offset = fields.Count > 0 ? fields[0].Offset : block.Offset,
                    KindName = fields.FirstOrDefault(static field => field.Id == 0x03 && field.StringValue is not null)?.StringValue,
                    TypeByte = GetInt(fields, 0x0D),
                    CategoryId = GetInt(fields, 0x0E),
                    ParameterPreview = parameterField?.Preview,
                    ParameterString = parameterField?.StringValue,
                    Fields = fields.Select(ToFieldValue).ToArray(),
                });
            }
        }

        return result;
    }

    private static IReadOnlyList<Transform2DInfo> BuildTransform2DRecords(IReadOnlyList<VtbfBlock> blocks)
    {
        var result = new List<Transform2DInfo>();
        foreach (var block in blocks
            .Where(static block => block.Tag == "TRS2" && block.Fields.Any(IsRecordMarker))
            .ToArray())
        {
            result.AddRange(ToTransform2DRecords(block, result.Count));
        }

        return result;
    }

    private static IEnumerable<Transform2DInfo> ToTransform2DRecords(VtbfBlock block, int indexBase)
    {
        var groups = SplitRecords(block.Fields).ToArray();
        for (var i = 0; i < groups.Length; i++)
        {
            var fields = groups[i];
            var offset = fields.Count > 0 ? fields[0].Offset : block.Offset;
            var index = indexBase + i;
            var rotationZRaw = GetInt(fields, 0x32);
            double? rotationZDegrees = rotationZRaw is null ? null : ToPackedAngleDegrees(rotationZRaw.Value);
            var vertexColors = fields
                .Where(field => field.Id == 0x39)
                .Select(ParseColor)
                .Where(static color => color is not null)
                .Cast<ColorArgbValue>()
                .ToArray();

            yield return new Transform2DInfo
            {
                Index = index,
                Offset = offset,
                Path = $"{block.Path}/record[{index}]",
                Translation = GetVector2(fields, 0x31),
                RotationZ = rotationZDegrees is null ? GetFloat(fields, 0x32) : (float)rotationZDegrees.Value,
                RotationZRaw = rotationZRaw,
                RotationZDegreesCandidate = rotationZDegrees,
                Scale = GetVector2(fields, 0x33),
                Display = GetInt(fields, 0x3A) switch
                {
                    0 => false,
                    1 => true,
                    _ => null,
                },
                MaterialColor = ParseColor(fields.FirstOrDefault(field => field.Id == 0x37)),
                IlluminationColor = ParseColor(fields.FirstOrDefault(field => field.Id == 0x38)),
                VertexColors = vertexColors,
                MultiPosFlags = GetInt(fields, 0x3D),
                MultiSizeFlags = GetInt(fields, 0x3E),
                Fields = fields.Select(ToFieldValue).ToArray(),
            };
        }
    }

    private static CameraInfo? BuildCamera(IReadOnlyList<VtbfBlock> blocks)
    {
        var block = blocks.FirstOrDefault(static block => block.Tag == "CAM ");
        if (block is null)
        {
            return null;
        }

        var stringFields = GetStringFields(block);
        return new CameraInfo
        {
            Offset = block.Offset,
            Path = block.Path,
            Name = SelectName(stringFields),
            Position = GetVector3(block.Fields, 0x12),
            Target = GetVector3(block.Fields, 0x13),
            Flags = GetInt(block.Fields, 0x14),
            NearClip = GetFloat(block.Fields, 0x15),
            FarClip = GetFloat(block.Fields, 0x16),
            Fields = GetValueFields(block),
        };
    }

    private static AnimationInfo ToAnimation(VtbfBlock block, int index, IReadOnlyList<NodeInfo> nodes)
    {
        var stringFields = GetStringFields(block);
        var motionBlocks = block.Children.Where(static child => child.Tag == "MOT ").Concat(block.Children.Where(static child => child.Tag == "MOT")).ToArray();
        return new AnimationInfo
        {
            Index = index,
            Offset = block.Offset,
            Path = block.Path,
            Name = NormalizeAnimationName(SelectName(stringFields)),
            StringFields = stringFields,
            NumericFields = GetNumericFields(block),
            Motions = motionBlocks.Select((motion, motionIndex) => ToMotion(motion, motionIndex, nodes)).ToArray(),
        };
    }

    private static IReadOnlyList<AnimationInfo> BuildAnimations(IReadOnlyList<VtbfBlock> blocks, IReadOnlyList<NodeInfo> nodes)
    {
        var animationBlocks = blocks.Where(static block => block.Tag == "ANIM").OrderBy(static block => block.Offset).ToArray();
        if (animationBlocks.Length == 0)
        {
            return Array.Empty<AnimationInfo>();
        }

        if (animationBlocks.Any(static block => block.Children.Any(static child => child.Tag is "MOT " or "MOT")))
        {
            return animationBlocks.Select((block, index) => ToAnimation(block, index, nodes)).ToArray();
        }

        var ordered = blocks.OrderBy(static block => block.Offset).ToArray();
        var animations = new List<AnimationInfo>(animationBlocks.Length);
        for (var i = 0; i < animationBlocks.Length; i++)
        {
            var animation = animationBlocks[i];
            var nextOffset = i + 1 < animationBlocks.Length ? animationBlocks[i + 1].Offset : long.MaxValue;
            var range = ordered
                .Where(block => block.Offset > animation.Offset && block.Offset < nextOffset)
                .ToArray();

            var motions = BuildMotionsFromRange(range, nodes);
            animations.Add(ToAnimationWithMotions(animation, i, motions));
        }

        return animations;
    }

    private static IReadOnlyList<MotionInfo> BuildMotionsFromRange(IReadOnlyList<VtbfBlock> range, IReadOnlyList<NodeInfo> nodes)
    {
        var motions = new List<MotionInfo>();
        var motionBlocks = range.Where(static block => block.Tag is "MOT " or "MOT").OrderBy(static block => block.Offset).ToArray();
        for (var i = 0; i < motionBlocks.Length; i++)
        {
            var motion = motionBlocks[i];
            var nextOffset = i + 1 < motionBlocks.Length ? motionBlocks[i + 1].Offset : long.MaxValue;
            var trackBlocks = range
                .Where(block => block.Offset > motion.Offset && block.Offset < nextOffset && block.Tag is "TRK " or "TRK")
                .OrderBy(static block => block.Offset)
                .ToArray();

            var tracks = new List<TrackInfo>(trackBlocks.Length);
            for (var trackIndex = 0; trackIndex < trackBlocks.Length; trackIndex++)
            {
                var track = trackBlocks[trackIndex];
                var nextTrackOffset = trackIndex + 1 < trackBlocks.Length ? trackBlocks[trackIndex + 1].Offset : nextOffset;
                var keyBlocks = range
                    .Where(block => block.Offset > track.Offset && block.Offset < nextTrackOffset && block.Tag is "KEY " or "KEY")
                    .OrderBy(static block => block.Offset)
                    .ToArray();
                tracks.Add(ToTrackWithKeys(track, trackIndex, keyBlocks));
            }

            motions.Add(ToMotionWithTracks(motion, i, tracks, nodes));
        }

        return motions;
    }

    private static AnimationInfo ToAnimationWithMotions(VtbfBlock block, int index, IReadOnlyList<MotionInfo> motions)
    {
        var stringFields = GetStringFields(block);
        return new AnimationInfo
        {
            Index = index,
            Offset = block.Offset,
            Path = block.Path,
            Name = NormalizeAnimationName(SelectName(stringFields)),
            StringFields = stringFields,
            NumericFields = GetNumericFields(block),
            Motions = motions,
        };
    }

    private static MotionInfo ToMotion(VtbfBlock block, int index, IReadOnlyList<NodeInfo> nodes)
    {
        var trackBlocks = block.Children.Where(static child => child.Tag == "TRK ").Concat(block.Children.Where(static child => child.Tag == "TRK")).ToArray();
        var tracks = trackBlocks.Select((track, trackIndex) => ToTrack(track, trackIndex)).ToArray();
        return ToMotionWithTracks(block, index, tracks, nodes);
    }

    private static MotionInfo ToMotionWithTracks(VtbfBlock block, int index, IReadOnlyList<TrackInfo> tracks, IReadOnlyList<NodeInfo> nodes)
    {
        var stringFields = GetStringFields(block);
        var numericFields = GetNumericFields(block);
        var ints = FlattenInts(numericFields).ToArray();
        var name = SelectName(stringFields);
        var castIndex = GetInt(block.Fields, 0x51);
        var targetIndex = castIndex ?? (ints.Length > 0 ? ToNullableInt(ints[0]) : null);
        var targetNameFromString = stringFields.Skip(1).Select(static field => field.StringValue).FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)) ?? name;
        var targetName = ResolveNodeName(nodes, targetIndex) ?? targetNameFromString;

        return new MotionInfo
        {
            Index = index,
            Offset = block.Offset,
            Path = block.Path,
            Name = name,
            TargetName = targetName,
            TargetIndex = targetIndex,
            CastIndex = castIndex,
            DeclaredTrackCount = GetInt(block.Fields, 0x52),
            StringFields = stringFields,
            NumericFields = numericFields,
            Tracks = tracks,
        };
    }

    private static TrackInfo ToTrack(VtbfBlock block, int index)
    {
        var keyBlocks = block.Children.Where(static child => child.Tag == "KEY ").Concat(block.Children.Where(static child => child.Tag == "KEY")).ToArray();
        return ToTrackWithKeys(block, index, keyBlocks);
    }

    private static TrackInfo ToTrackWithKeys(VtbfBlock block, int index, IReadOnlyList<VtbfBlock> keyBlocks)
    {
        var stringFields = GetStringFields(block);
        var numericFields = GetNumericFields(block);
        var ints = FlattenInts(numericFields).ToArray();
        var keyframes = keyBlocks.SelectMany(ToKeyframes).ToArray();
        var name = SelectName(stringFields);
        var trackType = GetInt(block.Fields, 0x53);
        var valueType = GetInt(block.Fields, 0x57);
        var flags = GetInt(block.Fields, 0x54);
        var firstFrame = GetInt(block.Fields, 0x58);
        var lastFrame = GetInt(block.Fields, 0x59);
        var keyBlockDeclaredCount = GetDeclaredKeyCount(keyBlocks, keyframes.Length);
        var track = new TrackInfo
        {
            Index = index,
            Offset = block.Offset,
            Path = block.Path,
            Name = name,
            TrackId = trackType ?? (ints.Length > 0 ? ToNullableInt(ints[0]) : null),
            TrackType = trackType,
            TrackTypeName = GetTrackTypeName(trackType),
            ValueType = valueType,
            ValueTypeName = "DeclaredKeyCount",
            DeclaredKeyCountFromTrack = valueType,
            DeclaredKeyCountFromKeyBlock = keyBlockDeclaredCount,
            KeyCountMatchesDeclaration = valueType is null ? null : valueType == keyBlockDeclaredCount && valueType == keyframes.Length,
            Flags = flags ?? (ints.Length > 1 ? ToNullableInt(ints[1]) : null),
            KeyValueStorage = GetKeyValueStorage(flags ?? (ints.Length > 1 ? ToNullableInt(ints[1]) : null)),
            TargetIndex = ints.Length > 2 ? ToNullableInt(ints[2]) : null,
            FirstFrame = firstFrame,
            LastFrame = lastFrame,
            DeclaredKeyCount = valueType ?? keyBlockDeclaredCount,
            StringFields = stringFields,
            NumericFields = numericFields,
            Keyframes = keyframes,
            IsLikelyStateTrack = false,
        };

        return new TrackInfo
        {
            Index = track.Index,
            Offset = track.Offset,
            Path = track.Path,
            Name = track.Name,
            TrackId = track.TrackId,
            TrackType = track.TrackType,
            TrackTypeName = track.TrackTypeName,
            ValueType = track.ValueType,
            ValueTypeName = track.ValueTypeName,
            DeclaredKeyCountFromTrack = track.DeclaredKeyCountFromTrack,
            DeclaredKeyCountFromKeyBlock = track.DeclaredKeyCountFromKeyBlock,
            KeyCountMatchesDeclaration = track.KeyCountMatchesDeclaration,
            Flags = track.Flags,
            KeyValueStorage = track.KeyValueStorage,
            TargetIndex = track.TargetIndex,
            FirstFrame = track.FirstFrame,
            LastFrame = track.LastFrame,
            DeclaredKeyCount = track.DeclaredKeyCount,
            StringFields = track.StringFields,
            NumericFields = track.NumericFields,
            Keyframes = track.Keyframes,
            IsLikelyStateTrack = IsLikelyStateTrack(track),
        };
    }

    private static IReadOnlyList<AnimationBindingInfo> BuildAnimationBindings(
        IReadOnlyList<NodeInfo> nodes,
        IReadOnlyList<AnimationInfo> animations)
    {
        var bindings = new List<AnimationBindingInfo>();
        foreach (var animation in animations)
        {
            for (var motionIndex = 0; motionIndex < animation.Motions.Count; motionIndex++)
            {
                var motion = animation.Motions[motionIndex];
                var nodeIndex = ResolveNodeIndex(nodes, motion);
                if (nodeIndex is null)
                {
                    continue;
                }

                var tracks = motion.Tracks;
                var trackTypes = tracks
                    .Select(static track => track.TrackType)
                    .Where(static type => type is not null)
                    .Select(static type => type!.Value)
                    .Distinct()
                    .OrderBy(static type => type)
                    .ToArray();
                var trackTypeNames = trackTypes
                    .Select(static type => GetTrackTypeName(type) ?? $"TrackType{type}")
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();

                bindings.Add(new AnimationBindingInfo
                {
                    NodeIndex = nodeIndex.Value,
                    NodeName = nodes[nodeIndex.Value].Name,
                    AnimationName = animation.Name ?? $"ANIM@0x{animation.Offset:X}",
                    AnimationIndex = animation.Index,
                    MotionIndex = motion.Index,
                    TrackCount = tracks.Count,
                    KeyCount = tracks.Sum(static track => track.Keyframes.Count),
                    TrackTypes = trackTypes,
                    TrackTypeNames = trackTypeNames,
                });
            }
        }

        return bindings
            .OrderBy(static binding => binding.AnimationIndex)
            .ThenBy(static binding => binding.MotionIndex)
            .ToArray();
    }

    private static int? ResolveNodeIndex(IReadOnlyList<NodeInfo> nodes, MotionInfo motion)
    {
        if (motion.CastIndex is int castIndex && castIndex >= 0 && castIndex < nodes.Count)
        {
            return castIndex;
        }

        if (motion.TargetIndex is int targetIndex && targetIndex >= 0 && targetIndex < nodes.Count)
        {
            return targetIndex;
        }

        if (string.IsNullOrWhiteSpace(motion.TargetName))
        {
            return null;
        }

        var index = nodes.FirstOrDefault(node => string.Equals(node.Name, motion.TargetName, StringComparison.OrdinalIgnoreCase))?.Index;
        return index is >= 0 ? index.Value : null;
    }

    private static string? ResolveNodeName(IReadOnlyList<NodeInfo> nodes, int? index)
    {
        return index is >= 0 && index < nodes.Count
            ? nodes[index.Value].Name
            : null;
    }

    private static string? GetTrackTypeName(int? trackType)
    {
        return trackType switch
        {
            0 => "TranslateX",
            1 => "TranslateY",
            2 => "TranslateZCandidate",
            3 => "RotateXCandidate",
            4 => "RotateYCandidate",
            5 => "RotateZ",
            6 => "ScaleX",
            7 => "ScaleY",
            8 => "ScaleZCandidate",
            11 => "Display",
            12 => "ImageWidthCandidate",
            13 => "ImageHeightCandidate",
            18 => "PrimaryImageVariantIndexCandidate",
            19 => "SecondaryImageVariantIndexCandidate",
            21 => "MaterialColorRCandidate",
            22 => "MaterialColorGCandidate",
            23 => "MaterialColorBCandidate",
            24 => "MaterialAlpha",
            25 => "IlluminationColorRCandidate",
            26 => "IlluminationColorGCandidate",
            27 => "IlluminationColorBCandidate",
            28 => "IlluminationAlphaCandidate",
            _ => trackType is null ? null : $"TrackType{trackType.Value}",
        };
    }

    private static int GetDeclaredKeyCount(IReadOnlyList<VtbfBlock> keyBlocks, int fallback)
    {
        var count = 0;
        foreach (var block in keyBlocks)
        {
            if (block.ParamHigh is not int fieldCount || fieldCount <= 0 || fieldCount % 5 != 0)
            {
                continue;
            }

            count += fieldCount / 5;
        }

        return count > 0 ? count : fallback;
    }

    private static string? GetKeyValueStorage(int? flags)
    {
        if (flags is null)
        {
            return null;
        }

        var baseCode = flags.Value & 0xFF;
        var extra = flags.Value & ~0xFF;
        var storage = baseCode switch
        {
            0x13 => "Float32Curve",
            0x23 => "Int32State",
            0x33 => "BoolState",
            0x43 => "PackedAngleCandidateCurve",
            _ => $"FlagsBase0x{baseCode:X}",
        };

        return extra == 0 ? storage : $"{storage}+Extra0x{extra:X}";
    }

    private static string? GetKeyValueKind(FieldValueSummary? valueField)
    {
        return valueField?.TypeHex switch
        {
            "0x0001" => "Bool",
            "0x0008" => "Int32",
            "0x000A" => "Float32",
            "0x000B" => "PackedAngleCandidate",
            null => null,
            _ => valueField.TypeName,
        };
    }

    private static string? GetInterpolationName(int? interpolation)
    {
        return interpolation switch
        {
            0 => "StepOrConstant",
            1 => "Linear",
            2 => "Spline",
            _ => interpolation is null ? null : $"Interpolation{interpolation.Value}",
        };
    }

    private static IEnumerable<KeyframeInfo> ToKeyframes(VtbfBlock block)
    {
        var fields = GetValueFields(block);
        var groups = new List<List<FieldValueSummary>>();
        List<FieldValueSummary>? current = null;

        foreach (var field in fields)
        {
            if (field.IdHex == "0x005A")
            {
                if (current is { Count: > 0 })
                {
                    groups.Add(current);
                }

                current = [];
            }

            current ??= [];
            current.Add(field);
        }

        if (current is { Count: > 0 })
        {
            groups.Add(current);
        }

        if (groups.Count == 0)
        {
            groups.Add(fields.ToList());
        }

        for (var i = 0; i < groups.Count; i++)
        {
            yield return ToKeyframe(block, i, groups[i]);
        }
    }

    private static KeyframeInfo ToKeyframe(VtbfBlock block, int index, IReadOnlyList<FieldValueSummary>? fieldGroup = null)
    {
        var fields = fieldGroup ?? GetValueFields(block);
        var valueField = fields.FirstOrDefault(static field => field.IdHex == "0x005B");
        var interpolation = GetInt(fields, 0x5C);
        var packedAngleRaw = valueField?.TypeHex == "0x000B" ? GetInt(fields, 0x5B) : null;
        var numericValues = fields
            .SelectMany(GetNumericCandidates)
            .Where(static value => !double.IsNaN(value) && !double.IsInfinity(value))
            .ToArray();

        var timeCandidates = numericValues
            .Where(static value => value >= 0 && value < 86400)
            .Take(4)
            .ToArray();

        return new KeyframeInfo
        {
            Index = index,
            Offset = block.Offset,
            Path = block.Path,
            Fields = fields,
            KeyFrame = GetInt(fields, 0x5A),
            ScalarValue = GetScalar(fields, 0x5B),
            BoolValue = valueField?.TypeHex == "0x0001" ? GetInt(fields, 0x5B) switch
            {
                0 => false,
                1 => true,
                _ => null,
            } : null,
            PackedAngleRaw = packedAngleRaw,
            PackedAngleDegreesCandidate = packedAngleRaw is null ? null : ToPackedAngleDegrees(packedAngleRaw.Value),
            KeyValueTypeHex = valueField?.TypeHex,
            KeyValueTypeName = valueField?.TypeName,
            KeyValueKind = GetKeyValueKind(valueField),
            Interpolation = interpolation,
            InterpolationName = GetInterpolationName(interpolation),
            TangentIn = GetScalar(fields, 0x5D),
            TangentOut = GetScalar(fields, 0x5E),
            TimeCandidates = timeCandidates,
            ValueCandidates = numericValues.Take(16).ToArray(),
            Preview = fields.Select(static field => field.Preview).FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)),
        };
    }

    private static bool IsLikelyStateTrack(TrackInfo track)
    {
        if (track.Name is not null && StateTrackNameRegex().IsMatch(track.Name))
        {
            return true;
        }

        var values = track.Keyframes
            .SelectMany(static key => key.ValueCandidates)
            .Take(64)
            .ToArray();
        return values.Length > 0 && values.All(static value => Math.Abs(value) < 0.001 || Math.Abs(value - 1) < 0.001);
    }

    private static IReadOnlyList<FieldValueSummary> GetStringFields(VtbfBlock block)
    {
        return block.Fields
            .Where(static field => !string.IsNullOrWhiteSpace(field.StringValue))
            .Select(ToFieldValue)
            .ToArray();
    }

    private static IReadOnlyList<FieldValueSummary> GetNumericFields(VtbfBlock block)
    {
        return block.Fields
            .Where(static field => field.Int64Values is { Length: > 0 } || field.Float64Values is { Length: > 0 })
            .Select(ToFieldValue)
            .ToArray();
    }

    private static IReadOnlyList<FieldValueSummary> GetValueFields(VtbfBlock block)
    {
        return block.Fields.Select(ToFieldValue).ToArray();
    }

    private static FieldValueSummary ToFieldValue(VtbfField field)
    {
        return new FieldValueSummary
        {
            IdHex = field.IdHex,
            TypeHex = field.TypeHex,
            TypeName = field.TypeName,
            Preview = field.Preview,
            Int64Values = field.Int64Values,
            Float64Values = field.Float64Values,
            StringValue = field.StringValue,
        };
    }

    private static string? SelectName(IEnumerable<FieldValueSummary> stringFields)
    {
        return stringFields
            .Select(static field => field.StringValue?.Trim())
            .FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value));
    }

    private static string? SelectComment(IReadOnlyList<FieldValueSummary> stringFields)
    {
        return stringFields
            .Select(static field => field.StringValue?.Trim())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Skip(1)
            .FirstOrDefault();
    }

    private static string? NormalizeAnimationName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        return name.EndsWith('V') ? name[..^1] : name;
    }

    private static string ClassifyNodeGroup(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "(unnamed)";
        }

        var match = NodePrefixRegex().Match(name);
        if (name.Contains("present", StringComparison.OrdinalIgnoreCase))
        {
            return "present";
        }

        if (name.Contains("mouth", StringComparison.OrdinalIgnoreCase))
        {
            return "mouth";
        }

        return match.Success
            ? match.Groups["prefix"].Value.ToLowerInvariant()
            : "(ungrouped)";
    }

    private static IEnumerable<long> FlattenInts(IEnumerable<FieldValueSummary> fields)
    {
        foreach (var field in fields)
        {
            if (field.Int64Values is null)
            {
                continue;
            }

            foreach (var value in field.Int64Values)
            {
                yield return value;
            }
        }
    }

    private static int? GetInt(IEnumerable<VtbfField> fields, int id)
    {
        var value = fields.FirstOrDefault(field => field.Id == id)?.Int64Values?.FirstOrDefault();
        return value is >= int.MinValue and <= int.MaxValue ? (int)value.Value : null;
    }

    private static int? GetInt(IEnumerable<FieldValueSummary> fields, int id)
    {
        var idHex = $"0x{id:X4}";
        var value = fields.FirstOrDefault(field => field.IdHex == idHex)?.Int64Values?.FirstOrDefault();
        return value is >= int.MinValue and <= int.MaxValue ? (int)value.Value : null;
    }

    private static float? GetFloat(IEnumerable<VtbfField> fields, int id)
    {
        var field = fields.FirstOrDefault(field => field.Id == id);
        var value = field?.Float64Values?.FirstOrDefault();
        return value is not null && value >= float.MinValue && value <= float.MaxValue
            ? (float)value.Value
            : null;
    }

    private static double? GetScalar(IEnumerable<FieldValueSummary> fields, int id)
    {
        var idHex = $"0x{id:X4}";
        var field = fields.FirstOrDefault(field => field.IdHex == idHex);
        if (field?.TypeHex == "0x000B" && field.Int64Values is { Length: > 0 })
        {
            return field.Int64Values[0];
        }

        if (field?.Float64Values is { Length: > 0 })
        {
            return field.Float64Values[0];
        }

        if (field?.Int64Values is { Length: > 0 })
        {
            return field.Int64Values[0];
        }

        return null;
    }

    private static IEnumerable<double> GetNumericCandidates(FieldValueSummary field)
    {
        if (field.TypeHex == "0x000B")
        {
            return field.Int64Values?.Select(static value => (double)value) ?? [];
        }

        if (field.Float64Values is { Length: > 0 })
        {
            return field.Float64Values;
        }

        return field.Int64Values?.Select(static value => (double)value) ?? [];
    }

    private static double ToPackedAngleDegrees(int raw)
    {
        return raw * (180.0 / 32768.0);
    }

    private static IReadOnlyList<int> BuildFlagBits(int? flags)
    {
        if (flags is null)
        {
            return Array.Empty<int>();
        }

        return Enumerable.Range(0, 32)
            .Where(bit => ((uint)flags.Value & (1u << bit)) != 0)
            .ToArray();
    }

    private static Vector2Value? GetVector2(IEnumerable<VtbfField> fields, int id)
    {
        var values = fields.FirstOrDefault(field => field.Id == id)?.Float64Values;
        if (values is not { Length: >= 2 })
        {
            return null;
        }

        return new Vector2Value
        {
            X = (float)values[0],
            Y = (float)values[1],
        };
    }

    private static Vector3Value? GetVector3(IEnumerable<VtbfField> fields, int id)
    {
        var values = fields.FirstOrDefault(field => field.Id == id)?.Float64Values;
        if (values is not { Length: >= 3 })
        {
            return null;
        }

        return new Vector3Value
        {
            X = (float)values[0],
            Y = (float)values[1],
            Z = (float)values[2],
        };
    }

    private static ColorArgbValue? ParseColor(VtbfField? field)
    {
        if (field?.Raw is not { Length: >= 4 } raw)
        {
            return null;
        }

        return new ColorArgbValue
        {
            A = raw[0],
            R = raw[1],
            G = raw[2],
            B = raw[3],
        };
    }

    private static IReadOnlyList<IReadOnlyList<VtbfField>> SplitRecords(IReadOnlyList<VtbfField> fields)
    {
        var records = new List<IReadOnlyList<VtbfField>>();
        List<VtbfField>? current = null;

        foreach (var field in fields)
        {
            if (IsRecordMarker(field))
            {
                if (field.TypeCode == 0xFD)
                {
                    if (current is { Count: > 0 })
                    {
                        records.Add(current);
                        current = null;
                    }

                    continue;
                }

                if (current is { Count: > 0 })
                {
                    records.Add(current);
                }

                current = [];
                continue;
            }

            current ??= [];
            current.Add(field);
        }

        if (current is { Count: > 0 })
        {
            records.Add(current);
        }

        return records;
    }

    private static bool IsRecordMarker(VtbfField field)
    {
        return field.TypeCode is 0xFC or 0xFD or 0xFE;
    }

    private static int? ToNullableInt(long value)
    {
        return value is >= int.MinValue and <= int.MaxValue ? (int)value : null;
    }

    private static IEnumerable<VtbfBlock> Flatten(VtbfBlock block)
    {
        yield return block;
        foreach (var child in block.Children)
        {
            foreach (var nested in Flatten(child))
            {
                yield return nested;
            }
        }
    }

    [GeneratedRegex("^(?<prefix>[A-Za-z0-9]+)_", RegexOptions.CultureInvariant)]
    private static partial Regex NodePrefixRegex();

    [GeneratedRegex("visible|visibility|display|alpha|opacity|hide|show|state|enable|disable|onoff", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex StateTrackNameRegex();
}
