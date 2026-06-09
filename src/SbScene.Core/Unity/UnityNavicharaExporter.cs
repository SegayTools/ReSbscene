using System.Security.Cryptography;
using System.Text;
using SbScene.Core.Images;
using SbScene.Core.Rendering;
using SbScene.Core.Resources;
using SbScene.Core.Semantics;

namespace SbScene.Core.Unity;

/// <summary>
/// 提供Unity NaviChara 导出器，负责把 sbscene 数据转换为目标导出格式。
/// </summary>
public static class UnityNavicharaExporter
{
    private const double Epsilon = 0.000001;
    private const string ExporterVersion = "SbScene.Core unity-navichara-export v1";

    /// <summary>
    /// 导出导出清单，将 sbscene 语义模型转换为目标格式的结构化输出。
    /// </summary>
    /// <param name="sbscenePath">要读取、写入或记录的文件或目录路径。</param>
    /// <param name="svoPath">要读取、写入或记录的文件或目录路径。</param>
    /// <param name="outputDirectory">要读取、写入或记录的文件或目录路径。</param>
    /// <param name="options">控制本次处理行为的选项。</param>
    /// <returns>包含导出清单、诊断信息和失败状态的导出结果。</returns>
    public static UnityNavicharaExportResult Export(
        string sbscenePath,
        string svoPath,
        string outputDirectory,
        UnityNavicharaExportOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sbscenePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(svoPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        var scene = new SbSceneParser().ParseFile(sbscenePath);
        return Export(scene, sbscenePath, svoPath, outputDirectory, options);
    }

    /// <summary>
    /// 导出导出清单，将 sbscene 语义模型转换为目标格式的结构化输出。
    /// </summary>
    /// <param name="scene">已解析的 sbscene 场景模型。</param>
    /// <param name="sbscenePath">要读取、写入或记录的文件或目录路径。</param>
    /// <param name="svoPath">要读取、写入或记录的文件或目录路径。</param>
    /// <param name="outputDirectory">要读取、写入或记录的文件或目录路径。</param>
    /// <param name="options">控制本次处理行为的选项。</param>
    /// <returns>包含导出清单、诊断信息和失败状态的导出结果。</returns>
    public static UnityNavicharaExportResult Export(
        SbSceneFile scene,
        string sbscenePath,
        string svoPath,
        string outputDirectory,
        UnityNavicharaExportOptions options)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentException.ThrowIfNullOrWhiteSpace(sbscenePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(svoPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        var diagnostics = new List<UnityNavicharaDiagnostic>();
        var settings = BuildSettings(options);
        var baseState = BuildCommonBaseFrameState(scene, options, diagnostics);
        settings = ApplyAutoCenter(scene, settings, options, baseState, diagnostics);
        var character = new UnityNavicharaCharacter
        {
            Id = options.CharacterId,
            PrefabName = $"UI_Navichara_{options.CharacterId}",
            ControllerName = $"UI_NaviChara_{options.CharacterId}",
        };
        var names = BuildUnityNodeNames(scene.Surfboard.Nodes);
        var paths = BuildUnityNodePaths(scene.Surfboard.Nodes, names, character);
        var sprites = BuildSprites(scene, svoPath, outputDirectory, options.ExtractSprites, diagnostics);
        var spritesByNodeAndSlot = sprites
            .GroupBy(static sprite => (sprite.NodeId, sprite.Slot), static sprite => sprite, NodeSlotComparer.Instance)
            .ToDictionary(static group => group.Key, static group => (IReadOnlyList<UnityNavicharaSprite>)group.OrderBy(static sprite => sprite.SlotIndex).ToArray(), NodeSlotComparer.Instance);
        var imageCastsByNode = scene.Surfboard.Resources.ImageCasts
            .GroupBy(static imageCast => imageCast.CastIndex)
            .ToDictionary(static group => group.Key, static group => group.First());
        var nodes = BuildNodes(scene, names, paths, imageCastsByNode, spritesByNodeAndSlot, settings, baseState);
        var clipPlans = BuildClipPlans(options, diagnostics);
        var clips = clipPlans
            .Select(plan => BuildClip(scene, plan, paths, imageCastsByNode, settings, diagnostics))
            .ToArray();

        if (options.WriteValidationFrames)
        {
            WriteValidationFrames(scene, svoPath, outputDirectory, clipPlans, clips, diagnostics);
        }

        var export = new UnityNavicharaExport
        {
            Source = new UnityNavicharaSource
            {
                Sbscene = Path.GetFileName(sbscenePath),
                Svo = Path.GetFileName(svoPath),
                SceneHash = ComputeHash(sbscenePath),
                ExporterVersion = ExporterVersion,
            },
            Settings = settings,
            Character = character,
            Nodes = nodes,
            Sprites = sprites,
            Clips = clips,
            Validation = new UnityNavicharaValidation
            {
                ReferenceImageDirectory = options.WriteValidationFrames ? "validation" : null,
            },
            Animator = BuildAnimator(),
            Diagnostics = diagnostics,
        };

        return new UnityNavicharaExportResult
        {
            Export = export,
            Diagnostics = diagnostics,
            Failed = ShouldFail(diagnostics, options.Strict),
        };
    }

    /// <summary>
    /// 构建配置Template，为渲染、导出或诊断流程准备中间状态。
    /// </summary>
    /// <param name="scene">已解析的 sbscene 场景模型。</param>
    /// <returns>根据场景动画推断出的 NaviChara 配置模板。</returns>
    public static UnityNavicharaProfileTemplate BuildProfileTemplate(SbSceneFile scene)
    {
        ArgumentNullException.ThrowIfNull(scene);

        var animations = scene.Surfboard.Animations
            .Select(animation => new UnityNavicharaProfileTemplateAnimation
            {
                Name = AnimationDisplayName(animation),
                Index = animation.Index,
                EndFrame = GetAnimationEndFrame(animation),
                DefaultRepeat = GetAnimationDefaultRepeat(animation),
                Tracks = BuildTemplateTracks(scene, animation),
                CandidateTargetClip = GuessTargetClip(animation.Name),
            })
            .ToArray();
        var clips = new Dictionary<string, UnityNavicharaProfileClip>(StringComparer.Ordinal);
        foreach (var coreClip in UnityNavicharaConstants.CoreClipNames)
        {
            var candidate = animations.FirstOrDefault(animation => string.Equals(animation.CandidateTargetClip, coreClip, StringComparison.Ordinal));
            clips[coreClip] = new UnityNavicharaProfileClip
            {
                Loop = UnityNavicharaConstants.DefaultLoop(coreClip),
                DurationFrames = "autoMax",
                SourceSlots = candidate is null
                    ? []
                    :
                    [
                        new UnityNavicharaSourceSlot
                        {
                            Animation = candidate.Name,
                            Frame = "curve",
                            Repeat = false,
                        },
                    ],
            };
        }

        return new UnityNavicharaProfileTemplate
        {
            Settings = new UnityNavicharaProfileSettings
            {
                PixelsPerUnit = 1.0,
                CurveBakeMode = "keyed",
                RotationZMultiplier = 1.0,
                RootTransform = new UnityNavicharaRootTransform(),
            },
            CommonBaseSourceSlots = [],
            Animations = animations,
            Clips = clips,
        };
    }

    /// <summary>
    /// 格式化诊断信息列表Markdown，将模型转换为可展示、保存或比较的文本内容。
    /// </summary>
    /// <param name="diagnostics">参与本次处理的诊断信息列表。</param>
    /// <returns>格式化后的文本内容。</returns>
    public static string FormatDiagnosticsMarkdown(IReadOnlyList<UnityNavicharaDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        var builder = new StringBuilder();
        builder.AppendLine("# NaviChara export diagnostics");
        builder.AppendLine();
        if (diagnostics.Count == 0)
        {
            builder.AppendLine("No diagnostics.");
            return builder.ToString();
        }

        builder.AppendLine("| Severity | Code | Target clip | Source animation | Node | Track | Message | Suggestion |");
        builder.AppendLine("| --- | --- | --- | --- | --- | ---: | --- | --- |");
        foreach (var diagnostic in diagnostics)
        {
            var node = diagnostic.NodeId is null
                ? string.Empty
                : $"{diagnostic.NodeId} {diagnostic.NodeName ?? string.Empty}".Trim();
            builder.AppendLine(
                $"| {EscapeMarkdown(diagnostic.Severity)} | {EscapeMarkdown(diagnostic.Code)} | {EscapeMarkdown(diagnostic.TargetClip ?? string.Empty)} | {EscapeMarkdown(diagnostic.SourceAnimation ?? string.Empty)} | {EscapeMarkdown(node)} | {diagnostic.TrackType?.ToString() ?? string.Empty} | {EscapeMarkdown(diagnostic.Message)} | {EscapeMarkdown(diagnostic.Suggestion ?? string.Empty)} |");
        }

        return builder.ToString();
    }

    internal static bool ShouldFail(IReadOnlyList<UnityNavicharaDiagnostic> diagnostics, bool strict)
    {
        return diagnostics.Any(diagnostic => strict
            ? diagnostic.Severity is "warning" or "high" or "error"
            : diagnostic.Severity is "error");
    }

    private static UnityNavicharaSettings BuildSettings(UnityNavicharaExportOptions options)
    {
        var profileSettings = options.Profile?.Settings;
        var bakeMode = options.BakeSampledCurves
            ? "sampled60"
            : profileSettings?.CurveBakeMode ?? "keyed";
        return new UnityNavicharaSettings
        {
            PixelsPerUnit = profileSettings?.PixelsPerUnit ?? 1.0,
            CurveBakeMode = bakeMode,
            RotationZMultiplier = profileSettings?.RotationZMultiplier ?? 1.0,
            RootTransform = profileSettings?.RootTransform ?? new UnityNavicharaRootTransform(),
        };
    }

    /// <summary>
    /// 计算 bind 状态可见内容的世界包围盒中心,把它平移到 Unity 原点,结果写入 rootTransform.offset。
    /// renderer 包围盒为 sbscene 场景坐标(Y-down),导出端节点用 Y-up(<see cref="ToUnityY"/> = -y),
    /// 故 offset.X = -(left+right)/2,offset.Y = (top+bottom)/2(Y 翻转抵消取负)。
    /// </summary>
    private static UnityNavicharaSettings ApplyAutoCenter(
        SbSceneFile scene,
        UnityNavicharaSettings settings,
        UnityNavicharaExportOptions options,
        SbSceneAnimationFrameState baseState,
        List<UnityNavicharaDiagnostic> diagnostics)
    {
        if (!options.AutoCenter)
        {
            return settings;
        }

        // profile 显式给了非零 offset 视为手动指定,让位不覆盖。
        var existingOffset = settings.RootTransform.Offset;
        if (Math.Abs(existingOffset.X) > Epsilon || Math.Abs(existingOffset.Y) > Epsilon)
        {
            diagnostics.Add(new UnityNavicharaDiagnostic
            {
                Severity = "info",
                Code = "AutoCenterSkippedExplicitOffset",
                Message = $"Auto-center skipped because profile rootTransform.offset is set to ({existingOffset.X}, {existingOffset.Y}).",
            });
            return settings;
        }

        // 包围盒基于 commonBase 烘焙后的 bind 状态,确保与静态 prefab 实际显示的部件一致。
        var bounds = SbScenePngRenderer.ComputeContentBounds(scene, baseState);
        if (!double.IsFinite(bounds.Width) || !double.IsFinite(bounds.Height) || bounds.Width <= 0 || bounds.Height <= 0)
        {
            diagnostics.Add(new UnityNavicharaDiagnostic
            {
                Severity = "info",
                Code = "AutoCenterNoContent",
                Message = "Auto-center skipped because the bind-pose render produced no measurable visible content.",
            });
            return settings;
        }

        var centerX = (bounds.Left + bounds.Right) / 2.0;
        var centerYScene = (bounds.Top + bounds.Bottom) / 2.0;
        var offset = new UnityNavicharaVector2
        {
            X = -centerX,
            Y = centerYScene,
        };

        diagnostics.Add(new UnityNavicharaDiagnostic
        {
            Severity = "info",
            Code = "AutoCenterApplied",
            Message = $"Auto-centered character: bind bounds x[{bounds.Left:0.#},{bounds.Right:0.#}] y[{bounds.Top:0.#},{bounds.Bottom:0.#}] -> rootTransform.offset ({offset.X:0.#}, {offset.Y:0.#}).",
        });

        return new UnityNavicharaSettings
        {
            SampleRate = settings.SampleRate,
            CoordinateSystem = settings.CoordinateSystem,
            RotationZMultiplier = settings.RotationZMultiplier,
            PixelsPerUnit = settings.PixelsPerUnit,
            CurveBakeMode = settings.CurveBakeMode,
            PreserveSourceCoordinates = settings.PreserveSourceCoordinates,
            RootTransform = new UnityNavicharaRootTransform
            {
                Scale = settings.RootTransform.Scale,
                Offset = offset,
            },
        };
    }

    /// <summary>
    /// 把 profile 的 commonBaseSourceSlots 中的固定帧 slot 应用到 bind frame state,
    /// 作为 prefab 静态 bind 的烘焙结果。curve slot 不参与静态烘焙(它们只在 clip 里驱动)。
    /// 无 commonBase 时返回纯 bind 初始状态。
    /// </summary>
    private static SbSceneAnimationFrameState BuildCommonBaseFrameState(
        SbSceneFile scene,
        UnityNavicharaExportOptions options,
        List<UnityNavicharaDiagnostic> diagnostics)
    {
        var state = SbSceneAnimationFrameBuilder.BuildInitial(scene);
        var selections = BuildCommonBaseSelections(options);
        if (selections.Count == 0)
        {
            return state;
        }

        SbSceneAnimationFrameBuilder.ApplyAnimations(scene, state, selections, warning =>
            diagnostics.Add(new UnityNavicharaDiagnostic
            {
                Severity = "warning",
                Code = "CommonBaseSlotWarning",
                Message = warning,
            }));
        return state;
    }

    /// <summary>
    /// 从 commonBaseSourceSlots 取固定帧 slot,转成 frame state 的动画选择。curve slot 被忽略。
    /// </summary>
    private static IReadOnlyList<SbSceneAnimationSelection> BuildCommonBaseSelections(UnityNavicharaExportOptions options)
    {
        var commonBase = options.Profile?.CommonBaseSourceSlots;
        if (commonBase is null || commonBase.Count == 0)
        {
            return [];
        }

        var selections = new List<SbSceneAnimationSelection>();
        foreach (var slot in commonBase)
        {
            if (IsCurveSlot(slot) || string.IsNullOrWhiteSpace(slot.Animation))
            {
                continue;
            }

            var frame = Convert.ToDouble(slot.Frame, System.Globalization.CultureInfo.InvariantCulture);
            selections.Add(new SbSceneAnimationSelection(slot.Animation, frame) { HasExplicitFrame = true });
        }

        return selections;
    }

    private static IReadOnlyList<UnityNavicharaNode> BuildNodes(
        SbSceneFile scene,
        IReadOnlyDictionary<int, string> names,
        IReadOnlyDictionary<int, string> paths,
        IReadOnlyDictionary<int, SbSceneImageCast> imageCastsByNode,
        IReadOnlyDictionary<(int NodeId, string Slot), IReadOnlyList<UnityNavicharaSprite>> spritesByNodeAndSlot,
        UnityNavicharaSettings settings,
        SbSceneAnimationFrameState baseState)
    {
        var parentByNode = SbSceneRenderTree.BuildParentMap(scene.Surfboard.Nodes);
        return scene.Surfboard.Nodes.Select(node =>
        {
            var transform = node.Transform2D;
            // commonBase 固定帧烘焙后的节点状态:作为 prefab 静态 bind 的依据,
            // 这样不播放任何 clip 时也显示选定的服装/姿势组合。
            var nodeState = node.Index >= 0 && node.Index < baseState.Nodes.Count ? baseState.Nodes[node.Index] : null;
            var imageCast = imageCastsByNode.TryGetValue(node.Index, out var cast) ? cast : null;
            var imageState = imageCast is not null && imageCast.Index >= 0 && imageCast.Index < baseState.ImageCasts.Count
                ? baseState.ImageCasts[imageCast.Index]
                : null;
            var primarySprites = spritesByNodeAndSlot.TryGetValue((node.Index, "primary"), out var primary)
                ? primary.Select(static sprite => sprite.Id).ToArray()
                : [];
            var secondarySprites = spritesByNodeAndSlot.TryGetValue((node.Index, "secondary"), out var secondary)
                ? secondary.Select(static sprite => sprite.Id).ToArray()
                : [];
            var pivotPixels = imageCast is null
                ? new UnityNavicharaVector2 { X = 0, Y = 0 }
                : BuildUnityPivotPixels(imageCast);
            var size = imageCast is null
                ? new UnityNavicharaVector2()
                : new UnityNavicharaVector2
                {
                    X = imageState?.Width ?? imageCast.Width,
                    Y = imageState?.Height ?? imageCast.Height,
                };
            var defaultPrimaryIndex = imageState is not null
                ? ClampReferenceIndex(imageState.PrimaryReferenceIndex, primarySprites.Length)
                : ClampReferenceIndex(imageCast?.PrimaryCropReferenceIndex, primarySprites.Length);
            var defaultSecondaryIndex = imageState is not null
                ? ClampReferenceIndex(imageState.SecondaryReferenceIndex, secondarySprites.Length)
                : ClampReferenceIndex(imageCast?.SecondaryCropReferenceIndex, secondarySprites.Length);
            return new UnityNavicharaNode
            {
                Id = node.Index,
                SbsceneName = node.Name,
                UnityName = names[node.Index],
                UnityPath = paths[node.Index],
                ParentId = parentByNode.TryGetValue(node.Index, out var parentId) ? parentId : -1,
                IsImageCast = imageCast is not null,
                Static = new UnityNavicharaNodeStatic
                {
                    AnchoredPosition = new UnityNavicharaVector2
                    {
                        X = nodeState?.TranslationX ?? transform?.Translation?.X ?? 0,
                        Y = ToUnityY(nodeState?.TranslationY ?? transform?.Translation?.Y ?? 0),
                    },
                    RotationZ = ToUnityRotationZ(nodeState?.RotationDegrees ?? transform?.RotationZDegreesCandidate ?? transform?.RotationZ ?? 0, settings),
                    Scale = new UnityNavicharaVector2
                    {
                        X = nodeState?.ScaleX ?? transform?.Scale?.X ?? 1,
                        Y = nodeState?.ScaleY ?? transform?.Scale?.Y ?? 1,
                    },
                    Display = nodeState?.Display ?? transform?.Display ?? true,
                    Size = size,
                    PivotPixels = pivotPixels,
                    PivotNormalized = imageCast is null
                        ? new UnityNavicharaVector2 { X = 0.5, Y = 0.5 }
                        : BuildUnityPivotNormalized(imageCast),
                    MaterialColor = nodeState is not null
                        ? FormatUnityGraphicColor(nodeState)
                        : FormatUnityGraphicColor(transform),
                },
                Image = imageCast is null
                    ? null
                    : new UnityNavicharaNodeImage
                    {
                        Component = primarySprites.Length > 1 ? "MultiSprites" : "Image",
                        DrawMode = SbSceneImageCastConventions.DecodeDrawMode(imageCast),
                        AdditiveBlend = SbSceneImageCastConventions.HasAdditiveBlendCandidate(imageCast),
                        PrimarySprites = primarySprites,
                        SecondarySprites = secondarySprites,
                        DefaultPrimaryIndex = defaultPrimaryIndex,
                        DefaultSecondaryIndex = defaultSecondaryIndex,
                    },
            };
        }).ToArray();
    }

    private static IReadOnlyList<UnityNavicharaSprite> BuildSprites(
        SbSceneFile scene,
        string svoPath,
        string outputDirectory,
        bool extractSprites,
        List<UnityNavicharaDiagnostic> diagnostics)
    {
        var sprites = new List<UnityNavicharaSprite>();
        var usedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var decodedImages = new Dictionary<int, RgbaImage>();
        var textures = Array.Empty<SvoTextureResource>();
        if (extractSprites)
        {
            textures = SvoResourceParser.ParseFile(svoPath).ToArray();
            Directory.CreateDirectory(Path.Combine(outputDirectory, "sprites"));
        }

        foreach (var imageCast in scene.Surfboard.Resources.ImageCasts.OrderBy(static imageCast => imageCast.Index))
        {
            AddSpriteReferences(scene, imageCast, "primary", imageCast.PrimaryCropReferences, sprites, usedFileNames, textures, decodedImages, outputDirectory, extractSprites, diagnostics);
            AddSpriteReferences(scene, imageCast, "secondary", imageCast.SecondaryCropReferences, sprites, usedFileNames, textures, decodedImages, outputDirectory, extractSprites, diagnostics);
        }

        return sprites;
    }

    private static void AddSpriteReferences(
        SbSceneFile scene,
        SbSceneImageCast imageCast,
        string slot,
        IReadOnlyList<SbSceneCropReference> references,
        List<UnityNavicharaSprite> sprites,
        HashSet<string> usedFileNames,
        IReadOnlyList<SvoTextureResource> textures,
        Dictionary<int, RgbaImage> decodedImages,
        string outputDirectory,
        bool extractSprites,
        List<UnityNavicharaDiagnostic> diagnostics)
    {
        for (var i = 0; i < references.Count; i++)
        {
            var reference = references[i];
            var atlas = reference.TextureIndex >= 0 && reference.TextureIndex < scene.Surfboard.Resources.Atlases.Count
                ? scene.Surfboard.Resources.Atlases[reference.TextureIndex]
                : null;
            var crop = atlas is not null && reference.CropIndex >= 0 && reference.CropIndex < atlas.Crops.Count
                ? atlas.Crops[reference.CropIndex]
                : null;
            if (crop is null)
            {
                diagnostics.Add(new UnityNavicharaDiagnostic
                {
                    Severity = "warning",
                    Code = "SpriteCropMissing",
                    NodeId = imageCast.CastIndex,
                    NodeName = imageCast.NodeName,
                    Message = $"No crop was found for {slot} reference {i} on node {imageCast.NodeName ?? imageCast.CastIndex.ToString()}.",
                });
                continue;
            }

            var nodeName = imageCast.CastIndex >= 0 && imageCast.CastIndex < scene.Surfboard.Nodes.Count
                ? scene.Surfboard.Nodes[imageCast.CastIndex].Name
                : imageCast.NodeName;
            var baseName = MakeSafeFileName($"{(string.IsNullOrWhiteSpace(nodeName) ? $"node_{imageCast.CastIndex:D4}" : nodeName)}_{slot}_{i:D3}");
            var fileName = MakeUniqueFileName(baseName, usedFileNames);
            var sprite = new UnityNavicharaSprite
            {
                Id = $"sprite_{sprites.Count:D6}",
                Name = Path.GetFileNameWithoutExtension(fileName),
                File = $"sprites/{fileName}",
                SourceTexture = atlas?.Name ?? reference.AtlasName,
                CropIndex = reference.CropIndex,
                NodeId = imageCast.CastIndex,
                Slot = slot,
                SlotIndex = i,
                Rect = new UnityNavicharaRect
                {
                    X = crop.Left,
                    Y = crop.Top,
                    W = crop.Width,
                    H = crop.Height,
                },
                PivotPixels = BuildUnityPivotPixels(imageCast),
                PivotNormalized = BuildUnityPivotNormalized(imageCast),
            };
            sprites.Add(sprite);

            if (extractSprites)
            {
                WriteSpritePng(sprite, reference, crop, textures, decodedImages, outputDirectory, diagnostics, imageCast);
            }
        }
    }

    private static void WriteSpritePng(
        UnityNavicharaSprite sprite,
        SbSceneCropReference reference,
        SbSceneCropRect crop,
        IReadOnlyList<SvoTextureResource> textures,
        Dictionary<int, RgbaImage> decodedImages,
        string outputDirectory,
        List<UnityNavicharaDiagnostic> diagnostics,
        SbSceneImageCast imageCast)
    {
        if (reference.TextureIndex < 0 || reference.TextureIndex >= textures.Count)
        {
            diagnostics.Add(new UnityNavicharaDiagnostic
            {
                Severity = "warning",
                Code = "SpriteTextureMissing",
                NodeId = imageCast.CastIndex,
                NodeName = imageCast.NodeName,
                Message = $"No SVO texture exists for texture index {reference.TextureIndex}.",
            });
            return;
        }

        if (!decodedImages.TryGetValue(reference.TextureIndex, out var image))
        {
            image = DdsDecoder.Decode(textures[reference.TextureIndex].DdsBytes);
            decodedImages[reference.TextureIndex] = image;
        }

        var cropped = image.CropWithTransparentPadding(crop.Left, crop.Top, crop.Width, crop.Height);
        PngWriter.Write(Path.Combine(outputDirectory, sprite.File.Replace('/', Path.DirectorySeparatorChar)), cropped);
    }

    private static IReadOnlyDictionary<int, string> BuildUnityNodeNames(IReadOnlyList<NodeInfo> nodes)
    {
        var parentByNode = SbSceneRenderTree.BuildParentMap(nodes);
        var baseNames = nodes.ToDictionary(static node => node.Index, static node => MakeSafeUnityName(node.Name, node.Index));
        var result = new Dictionary<int, string>();
        foreach (var group in nodes.GroupBy(node => parentByNode.TryGetValue(node.Index, out var parent) ? parent : -1))
        {
            foreach (var nameGroup in group.GroupBy(node => baseNames[node.Index], StringComparer.Ordinal))
            {
                var duplicates = nameGroup.ToArray();
                foreach (var node in duplicates)
                {
                    result[node.Index] = duplicates.Length == 1
                        ? baseNames[node.Index]
                        : $"{baseNames[node.Index]}__n{node.Index}";
                }
            }
        }

        return result;
    }

    private static IReadOnlyDictionary<int, string> BuildUnityNodePaths(
        IReadOnlyList<NodeInfo> nodes,
        IReadOnlyDictionary<int, string> names,
        UnityNavicharaCharacter character)
    {
        var parentByNode = SbSceneRenderTree.BuildParentMap(nodes);
        var memo = new Dictionary<int, string>();
        var visiting = new HashSet<int>();
        var prefix = $"Null_UI_Navichara_{character.Id}/MoveObject";

        string Resolve(int nodeIndex)
        {
            if (memo.TryGetValue(nodeIndex, out var cached))
            {
                return cached;
            }

            if (!visiting.Add(nodeIndex))
            {
                return $"{prefix}/{names[nodeIndex]}";
            }

            var path = parentByNode.TryGetValue(nodeIndex, out var parentIndex) && parentIndex >= 0 && parentIndex < nodes.Count
                ? $"{Resolve(parentIndex)}/{names[nodeIndex]}"
                : $"{prefix}/{names[nodeIndex]}";
            visiting.Remove(nodeIndex);
            memo[nodeIndex] = path;
            return path;
        }

        foreach (var node in nodes)
        {
            Resolve(node.Index);
        }

        return memo;
    }

    private static IReadOnlyList<ClipPlan> BuildClipPlans(
        UnityNavicharaExportOptions options,
        List<UnityNavicharaDiagnostic> diagnostics)
    {
        var commonBase = options.Profile?.CommonBaseSourceSlots ?? [];
        var plans = UnityNavicharaConstants.CoreClipNames
            .Select(name =>
            {
                UnityNavicharaProfileClip? profileClip = null;
                options.Profile?.Clips.TryGetValue(name, out profileClip);
                return new ClipPlan(
                    name,
                    profileClip?.Loop ?? UnityNavicharaConstants.DefaultLoop(name),
                    ResolveDurationFrames(profileClip?.DurationFrames),
                    profileClip?.ValidationFrames,
                    MergeCommonBaseSlots(commonBase, profileClip?.SourceSlots));
            })
            .ToDictionary(static plan => plan.Name, StringComparer.Ordinal);

        foreach (var map in options.Maps)
        {
            if (!plans.TryGetValue(map.TargetClip, out var plan))
            {
                diagnostics.Add(new UnityNavicharaDiagnostic
                {
                    Severity = "error",
                    Code = "UnknownTargetClip",
                    TargetClip = map.TargetClip,
                    SourceAnimation = map.SourceAnimation,
                    Message = $"Target clip '{map.TargetClip}' is not one of the NaviChara core clips.",
                    Suggestion = $"Use one of: {string.Join(", ", UnityNavicharaConstants.CoreClipNames)}.",
                });
                continue;
            }

            plan.SourceSlots.Add(new UnityNavicharaSourceSlot
            {
                Animation = map.SourceAnimation,
                Frame = "curve",
                Repeat = false,
            });
        }

        foreach (var plan in plans.Values)
        {
            if (options.FashionFrame is int fashion)
            {
                UpsertFixedSlot(plan.SourceSlots, "Change_Fashion", fashion);
            }

            if (options.PositionFrame is int position)
            {
                UpsertFixedSlot(plan.SourceSlots, "Change_Position", position);
            }

            if (options.AccessoryFrame is int accessory)
            {
                UpsertFixedSlot(plan.SourceSlots, "Change_Accessory", accessory);
            }

            if (!plan.SourceSlots.Any(IsCurveSlot))
            {
                if (options.AllowPlaceholderClips)
                {
                    plan.Placeholder = true;
                    diagnostics.Add(new UnityNavicharaDiagnostic
                    {
                        Severity = "high",
                        Code = "PlaceholderClip",
                        TargetClip = plan.Name,
                        Message = $"Core clip '{plan.Name}' has no curve source slot and will be exported as a 1-frame bind-pose placeholder.",
                        Suggestion = "Provide --profile or --map sourceAnimation=targetClip for this clip.",
                    });
                }
                else
                {
                    diagnostics.Add(new UnityNavicharaDiagnostic
                    {
                        Severity = "error",
                        Code = "MissingCoreClipMapping",
                        TargetClip = plan.Name,
                        Message = $"Core clip '{plan.Name}' has no curve source slot.",
                        Suggestion = "Provide --profile or --map sourceAnimation=targetClip, or pass --allow-placeholder-clips.",
                    });
                }
            }
        }

        return UnityNavicharaConstants.CoreClipNames.Select(name => plans[name]).ToArray();
    }

    private static UnityNavicharaClip BuildClip(
        SbSceneFile scene,
        ClipPlan plan,
        IReadOnlyDictionary<int, string> nodePaths,
        IReadOnlyDictionary<int, SbSceneImageCast> imageCastsByNode,
        UnityNavicharaSettings settings,
        List<UnityNavicharaDiagnostic> diagnostics)
    {
        var sampled = string.Equals(settings.CurveBakeMode, "sampled60", StringComparison.OrdinalIgnoreCase);
        var resolvedSlots = ResolveSourceSlots(scene, plan, diagnostics);
        var duration = plan.Placeholder
            ? 1
            : Math.Max(1, plan.DurationFrames ?? resolvedSlots.Where(static slot => slot.IsCurve).Select(static slot => slot.SourceDurationFrames).DefaultIfEmpty(1).Max());
        var validationFrames = plan.ValidationFrames?.Count > 0
            ? plan.ValidationFrames.Distinct().OrderBy(static frame => frame).ToArray()
            : BuildValidationFrames(duration);
        var unsupported = new List<UnityNavicharaUnsupportedTrack>();
        var assignments = new Dictionary<TrackChannel, PendingCurve>();

        if (!plan.Placeholder)
        {
            foreach (var slot in resolvedSlots)
            {
                foreach (var motion in slot.Animation.Motions)
                {
                    var nodeIndex = ResolveMotionNodeIndex(scene.Surfboard.Nodes, motion);
                    if (nodeIndex is null || nodeIndex.Value < 0 || nodeIndex.Value >= scene.Surfboard.Nodes.Count)
                    {
                        continue;
                    }

                    foreach (var track in motion.Tracks)
                    {
                        if (track.TrackType is null)
                        {
                            continue;
                        }

                        if (!IsSupportedTrack(track.TrackType.Value))
                        {
                            AddUnsupportedTrack(plan, slot, scene.Surfboard.Nodes[nodeIndex.Value], track, diagnostics, unsupported);
                            continue;
                        }

                        if ((track.TrackType is 12 or 13 or 18) && !imageCastsByNode.ContainsKey(nodeIndex.Value))
                        {
                            diagnostics.Add(new UnityNavicharaDiagnostic
                            {
                                Severity = "warning",
                                Code = "ImageTrackWithoutImageCast",
                                TargetClip = plan.Name,
                                SourceAnimation = slot.AnimationName,
                                NodeId = nodeIndex.Value,
                                NodeName = scene.Surfboard.Nodes[nodeIndex.Value].Name,
                                TrackType = track.TrackType,
                                Message = $"Track type {track.TrackType} targets a node without a CIMG image cast.",
                            });
                            continue;
                        }

                        var channel = new TrackChannel(nodeIndex.Value, track.TrackType.Value);
                        assignments[channel] = new PendingCurve(slot, track, nodeIndex.Value);
                    }
                }

                if (slot.IsCurve)
                {
                    AddEndFrameDiagnostics(plan, slot, diagnostics);
                }
            }
        }

        var colorCurves = BuildColorCurves(scene.Surfboard.Nodes, assignments, duration, nodePaths, imageCastsByNode, settings, sampled);
        var curves = assignments
            .Values
            .Where(static curve => !IsColorTrack(curve.Track.TrackType!.Value))
            .OrderBy(static curve => curve.NodeId)
            .ThenBy(static curve => curve.Track.TrackType)
            .Select(curve => BuildCurve(plan, curve, duration, nodePaths, settings, sampled, diagnostics))
            .Where(static curve => curve is not null)
            .Cast<UnityNavicharaCurve>()
            .Concat(colorCurves)
            .OrderBy(static curve => curve.NodeId)
            .ThenBy(static curve => curve.SbsceneTrackType)
            .ToArray();

        return new UnityNavicharaClip
        {
            Name = plan.Name,
            SourceSlots = plan.SourceSlots.ToArray(),
            DurationFrames = duration,
            Loop = plan.Loop,
            ValidationFrames = validationFrames,
            Curves = curves,
            UnsupportedTracks = unsupported,
            Placeholder = plan.Placeholder,
        };
    }

    private static IReadOnlyList<ResolvedSourceSlot> ResolveSourceSlots(
        SbSceneFile scene,
        ClipPlan plan,
        List<UnityNavicharaDiagnostic> diagnostics)
    {
        var animations = scene.Surfboard.Animations
            .Where(static animation => !string.IsNullOrWhiteSpace(animation.Name))
            .GroupBy(static animation => animation.Name!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.OrdinalIgnoreCase);
        var result = new List<ResolvedSourceSlot>();
        foreach (var slot in plan.SourceSlots)
        {
            if (!animations.TryGetValue(slot.Animation, out var animation))
            {
                diagnostics.Add(new UnityNavicharaDiagnostic
                {
                    Severity = "error",
                    Code = "SourceAnimationMissing",
                    TargetClip = plan.Name,
                    SourceAnimation = slot.Animation,
                    Message = $"Source animation '{slot.Animation}' was not found.",
                });
                continue;
            }

            var isCurve = IsCurveSlot(slot);
            var fixedFrame = isCurve ? 0 : Convert.ToDouble(slot.Frame, System.Globalization.CultureInfo.InvariantCulture);
            result.Add(new ResolvedSourceSlot(
                slot.Animation,
                animation,
                isCurve,
                fixedFrame,
                slot.Repeat ?? false,
                GetAnimationEndFrame(animation),
                GetAnimationMaxKeyFrame(animation)));
        }

        return result;
    }

    private static UnityNavicharaCurve? BuildCurve(
        ClipPlan plan,
        PendingCurve pending,
        int targetDurationFrames,
        IReadOnlyDictionary<int, string> nodePaths,
        UnityNavicharaSettings settings,
        bool sampled,
        List<UnityNavicharaDiagnostic> diagnostics)
    {
        var trackType = pending.Track.TrackType!.Value;
        var binding = BuildBinding(trackType);
        if (binding is null)
        {
            return null;
        }

        var keys = pending.Slot.IsCurve
            ? sampled
                ? BuildSampledKeys(pending, targetDurationFrames, settings)
                : BuildKeyedKeys(pending, targetDurationFrames, settings)
            : BuildConstantKeys(pending, targetDurationFrames, settings);
        if (keys.Count == 0)
        {
            diagnostics.Add(new UnityNavicharaDiagnostic
            {
                Severity = "warning",
                Code = "EmptyCurve",
                TargetClip = plan.Name,
                SourceAnimation = pending.Slot.AnimationName,
                NodeId = pending.NodeId,
                TrackType = trackType,
                Message = $"Track type {trackType} produced no importable keys.",
            });
            return null;
        }

        return new UnityNavicharaCurve
        {
            NodeId = pending.NodeId,
            Path = nodePaths[pending.NodeId],
            SbsceneTrackType = trackType,
            Unity = binding,
            Keys = keys,
        };
    }

    private static IReadOnlyList<UnityNavicharaCurve> BuildColorCurves(
        IReadOnlyList<NodeInfo> nodes,
        IReadOnlyDictionary<TrackChannel, PendingCurve> assignments,
        int targetDurationFrames,
        IReadOnlyDictionary<int, string> nodePaths,
        IReadOnlyDictionary<int, SbSceneImageCast> imageCastsByNode,
        UnityNavicharaSettings settings,
        bool sampled)
    {
        var result = new List<UnityNavicharaCurve>();
        var nodeIds = assignments.Keys
            .Where(static channel => IsColorTrack(channel.TrackType))
            .Select(static channel => channel.NodeId)
            .Distinct()
            .Order()
            .ToArray();

        foreach (var nodeId in nodeIds)
        {
            if (nodeId < 0 || nodeId >= nodes.Count)
            {
                continue;
            }

            var hasImage = imageCastsByNode.ContainsKey(nodeId);
            var frames = BuildColorKeyFrames(assignments, nodeId, targetDurationFrames, sampled);
            var rgbKeysByChannel = new List<UnityNavicharaCurveKey>[3];
            for (var channel = 0; channel < rgbKeysByChannel.Length; channel++)
            {
                rgbKeysByChannel[channel] = [];
            }

            var alphaKeys = new List<UnityNavicharaCurveKey>();
            foreach (var frame in frames)
            {
                var color = EvaluateUnityGraphicColor(nodes[nodeId], assignments, nodeId, frame, settings);
                if (hasImage)
                {
                    AddColorKey(rgbKeysByChannel[0], frame, color.R / 255.0);
                    AddColorKey(rgbKeysByChannel[1], frame, color.G / 255.0);
                    AddColorKey(rgbKeysByChannel[2], frame, color.B / 255.0);
                }

                AddColorKey(alphaKeys, frame, color.A / 255.0);
            }

            if (hasImage)
            {
                var bindings = new[]
                {
                    new UnityNavicharaCurveBinding { Component = "Graphic", Property = "m_Color.r", CurveKind = "float" },
                    new UnityNavicharaCurveBinding { Component = "Graphic", Property = "m_Color.g", CurveKind = "float" },
                    new UnityNavicharaCurveBinding { Component = "Graphic", Property = "m_Color.b", CurveKind = "float" },
                };

                for (var channel = 0; channel < rgbKeysByChannel.Length; channel++)
                {
                    result.Add(new UnityNavicharaCurve
                    {
                        NodeId = nodeId,
                        Path = nodePaths[nodeId],
                        SbsceneTrackType = 21 + channel,
                        Unity = bindings[channel],
                        Keys = CoalesceDuplicateFrames(rgbKeysByChannel[channel]),
                    });
                }
            }

            result.Add(new UnityNavicharaCurve
            {
                NodeId = nodeId,
                Path = nodePaths[nodeId],
                SbsceneTrackType = 24,
                Unity = new UnityNavicharaCurveBinding
                {
                    Component = "CanvasGroup",
                    Property = "m_Alpha",
                    CurveKind = "float",
                },
                Keys = CoalesceDuplicateFrames(alphaKeys),
            });
        }

        return result;
    }

    private static IReadOnlyList<int> BuildColorKeyFrames(
        IReadOnlyDictionary<TrackChannel, PendingCurve> assignments,
        int nodeId,
        int targetDurationFrames,
        bool sampled)
    {
        if (sampled)
        {
            return Enumerable.Range(0, targetDurationFrames + 1).ToArray();
        }

        var frames = new SortedSet<int> { 0, targetDurationFrames };
        for (var trackType = 21; trackType <= 28; trackType++)
        {
            if (!assignments.TryGetValue(new TrackChannel(nodeId, trackType), out var pending))
            {
                continue;
            }

            foreach (var keyframe in pending.Track.Keyframes)
            {
                if (keyframe.KeyFrame is int frame && frame >= 0 && frame <= targetDurationFrames)
                {
                    frames.Add(frame);
                }
            }
        }

        return frames.ToArray();
    }

    private static RgbaColor EvaluateUnityGraphicColor(
        NodeInfo node,
        IReadOnlyDictionary<TrackChannel, PendingCurve> assignments,
        int nodeId,
        int targetFrame,
        UnityNavicharaSettings settings)
    {
        var material = ToRgbaColor(node.Transform2D?.MaterialColor, SbSceneColorConventions.OpaqueWhite);
        var illumination = ToRgbaColor(node.Transform2D?.IlluminationColor, SbSceneColorConventions.OpaqueBlack);
        var materialR = EvaluateColorChannel(assignments, nodeId, 21, targetFrame, settings) ?? material.R / 255.0;
        var materialG = EvaluateColorChannel(assignments, nodeId, 22, targetFrame, settings) ?? material.G / 255.0;
        var materialB = EvaluateColorChannel(assignments, nodeId, 23, targetFrame, settings) ?? material.B / 255.0;
        var materialA = EvaluateColorChannel(assignments, nodeId, 24, targetFrame, settings) ?? material.A / 255.0;
        var illuminationR = EvaluateColorChannel(assignments, nodeId, 25, targetFrame, settings) ?? illumination.R / 255.0;
        var illuminationG = EvaluateColorChannel(assignments, nodeId, 26, targetFrame, settings) ?? illumination.G / 255.0;
        var illuminationB = EvaluateColorChannel(assignments, nodeId, 27, targetFrame, settings) ?? illumination.B / 255.0;
        var illuminationA = EvaluateColorChannel(assignments, nodeId, 28, targetFrame, settings) ?? illumination.A / 255.0;

        return new RgbaColor(
            ToByteChannel(materialR + illuminationR * illuminationA),
            ToByteChannel(materialG + illuminationG * illuminationA),
            ToByteChannel(materialB + illuminationB * illuminationA),
            ToByteChannel(materialA));
    }

    private static double? EvaluateColorChannel(
        IReadOnlyDictionary<TrackChannel, PendingCurve> assignments,
        int nodeId,
        int trackType,
        int targetFrame,
        UnityNavicharaSettings settings)
    {
        if (!assignments.TryGetValue(new TrackChannel(nodeId, trackType), out var pending))
        {
            return null;
        }

        var sourceFrame = pending.Slot.IsCurve
            ? ResolveSourceFrame(pending.Slot, targetFrame)
            : pending.Slot.FixedFrame;
        var value = SbSceneAnimationEvaluator.EvaluateTrack(pending.Track, sourceFrame);
        if (value is null)
        {
            return null;
        }

        return TrackValueTransform.Create(pending.Track, settings).Apply(value.Value);
    }

    private static void AddColorKey(List<UnityNavicharaCurveKey> keys, int frame, double value)
    {
        keys.Add(new UnityNavicharaCurveKey
        {
            Frame = frame,
            Time = ToTime(frame),
            Value = value,
            Interp = "linear",
            HasInTangent = false,
            HasOutTangent = false,
        });
    }

    private static IReadOnlyList<UnityNavicharaCurveKey> BuildConstantKeys(
        PendingCurve pending,
        int targetDurationFrames,
        UnityNavicharaSettings settings)
    {
        var value = SbSceneAnimationEvaluator.EvaluateTrack(pending.Track, pending.Slot.FixedFrame);
        if (value is null)
        {
            return [];
        }

        var transform = TrackValueTransform.Create(pending.Track, settings);
        var key = new UnityNavicharaCurveKey
        {
            Frame = 0,
            Time = 0,
            Value = transform.Apply(value.Value),
            Interp = IsStepTrack(pending.Track) ? "step" : "linear",
            HasInTangent = false,
            HasOutTangent = false,
        };
        if (targetDurationFrames == 0)
        {
            return [key];
        }

        return
        [
            key,
            new UnityNavicharaCurveKey
            {
                Frame = targetDurationFrames,
                Time = ToTime(targetDurationFrames),
                Value = key.Value,
                Interp = key.Interp,
                HasInTangent = false,
                HasOutTangent = false,
            },
        ];
    }

    private static IReadOnlyList<UnityNavicharaCurveKey> BuildSampledKeys(
        PendingCurve pending,
        int targetDurationFrames,
        UnityNavicharaSettings settings)
    {
        var keys = new List<UnityNavicharaCurveKey>();
        var transform = TrackValueTransform.Create(pending.Track, settings);
        for (var frame = 0; frame <= targetDurationFrames; frame++)
        {
            var sourceFrame = ResolveSourceFrame(pending.Slot, frame);
            var value = SbSceneAnimationEvaluator.EvaluateTrack(pending.Track, sourceFrame);
            if (value is null)
            {
                continue;
            }

            keys.Add(new UnityNavicharaCurveKey
            {
                Frame = frame,
                Time = ToTime(frame),
                Value = transform.Apply(value.Value),
                Interp = IsStepTrack(pending.Track) ? "step" : "linear",
                HasInTangent = false,
                HasOutTangent = false,
            });
        }

        return CoalesceDuplicateFrames(keys);
    }

    private static IReadOnlyList<UnityNavicharaCurveKey> BuildKeyedKeys(
        PendingCurve pending,
        int targetDurationFrames,
        UnityNavicharaSettings settings)
    {
        var transform = TrackValueTransform.Create(pending.Track, settings);
        var samples = pending.Track.Keyframes
            .Select(key => ToKeySample(pending.Track, key, transform))
            .Where(static key => key is not null)
            .Cast<UnityNavicharaCurveKey>()
            .OrderBy(static key => key.Frame)
            .ToArray();
        if (samples.Length == 0)
        {
            return [];
        }

        if (!pending.Slot.Repeat || pending.Slot.SourceDurationFrames <= 0)
        {
            return BuildNonRepeatingKeyedKeys(pending, samples, targetDurationFrames, transform);
        }

        var period = Math.Max(1, pending.Slot.SourceDurationFrames);
        var byFrame = new SortedDictionary<int, UnityNavicharaCurveKey>();
        for (var offset = 0; offset <= targetDurationFrames; offset += period)
        {
            foreach (var sample in samples)
            {
                var frame = sample.Frame + offset;
                if (frame > targetDurationFrames)
                {
                    continue;
                }

                byFrame[frame] = sample.WithFrame(frame);
            }
        }

        if (!byFrame.ContainsKey(targetDurationFrames))
        {
            var sourceFrame = ResolveSourceFrame(pending.Slot, targetDurationFrames);
            var value = SbSceneAnimationEvaluator.EvaluateTrack(pending.Track, sourceFrame);
            if (value is not null)
            {
                byFrame[targetDurationFrames] = new UnityNavicharaCurveKey
                {
                    Frame = targetDurationFrames,
                    Time = ToTime(targetDurationFrames),
                    Value = transform.Apply(value.Value),
                    Interp = IsStepTrack(pending.Track) ? "step" : "linear",
                    HasInTangent = false,
                    HasOutTangent = false,
                };
            }
        }

        return byFrame.Values.ToArray();
    }

    private static IReadOnlyList<UnityNavicharaCurveKey> BuildNonRepeatingKeyedKeys(
        PendingCurve pending,
        IReadOnlyList<UnityNavicharaCurveKey> samples,
        int targetDurationFrames,
        TrackValueTransform transform)
    {
        var keys = new SortedDictionary<int, UnityNavicharaCurveKey>();
        foreach (var sample in samples)
        {
            if (sample.Frame <= targetDurationFrames)
            {
                keys[sample.Frame] = sample;
            }
        }

        if (!keys.ContainsKey(0))
        {
            var value = SbSceneAnimationEvaluator.EvaluateTrack(pending.Track, 0);
            if (value is not null)
            {
                keys[0] = new UnityNavicharaCurveKey
                {
                    Frame = 0,
                    Time = 0,
                    Value = transform.Apply(value.Value),
                    Interp = IsStepTrack(pending.Track) ? "step" : "linear",
                    HasInTangent = false,
                    HasOutTangent = false,
                };
            }
        }

        if (!keys.ContainsKey(targetDurationFrames))
        {
            var value = SbSceneAnimationEvaluator.EvaluateTrack(pending.Track, targetDurationFrames);
            if (value is not null)
            {
                keys[targetDurationFrames] = new UnityNavicharaCurveKey
                {
                    Frame = targetDurationFrames,
                    Time = ToTime(targetDurationFrames),
                    Value = transform.Apply(value.Value),
                    Interp = IsStepTrack(pending.Track) ? "step" : "linear",
                    HasInTangent = false,
                    HasOutTangent = false,
                };
            }
        }

        return keys.Values.ToArray();
    }

    private static UnityNavicharaCurveKey? ToKeySample(TrackInfo track, KeyframeInfo key, TrackValueTransform transform)
    {
        if (key.KeyFrame is null)
        {
            return null;
        }

        var rawValue = GetRawKeyValue(track, key);
        if (rawValue is null || !double.IsFinite(rawValue.Value))
        {
            return null;
        }

        var interp = IsStepTrack(track) || key.Interpolation == 0
            ? "step"
            : key.Interpolation == 2
                ? "hermite"
                : "linear";
        return new UnityNavicharaCurveKey
        {
            Frame = key.KeyFrame.Value,
            Time = ToTime(key.KeyFrame.Value),
            Value = transform.Apply(rawValue.Value),
            Interp = interp,
            HasInTangent = key.TangentIn is not null,
            HasOutTangent = key.TangentOut is not null,
            InTangent = key.TangentIn is null ? null : transform.ApplyDelta(key.TangentIn.Value),
            OutTangent = key.TangentOut is null ? null : transform.ApplyDelta(key.TangentOut.Value),
        };
    }

    private static double? GetRawKeyValue(TrackInfo track, KeyframeInfo key)
    {
        if (track.TrackType == 5 && key.PackedAngleDegreesCandidate is not null)
        {
            return key.PackedAngleDegreesCandidate;
        }

        if (key.BoolValue is not null)
        {
            return key.BoolValue.Value ? 1.0 : 0.0;
        }

        return key.ScalarValue ?? (key.ValueCandidates.Count > 0 ? key.ValueCandidates[0] : null);
    }

    private static UnityNavicharaCurveBinding? BuildBinding(int trackType)
    {
        return trackType switch
        {
            0 => new UnityNavicharaCurveBinding { Component = "RectTransform", Property = "m_AnchoredPosition.x", CurveKind = "float" },
            1 => new UnityNavicharaCurveBinding { Component = "RectTransform", Property = "m_AnchoredPosition.y", CurveKind = "float" },
            5 => new UnityNavicharaCurveBinding { Component = "Transform", Property = "localEulerAnglesRaw.z", CurveKind = "eulerZ" },
            6 => new UnityNavicharaCurveBinding { Component = "Transform", Property = "m_LocalScale.x", CurveKind = "float" },
            7 => new UnityNavicharaCurveBinding { Component = "Transform", Property = "m_LocalScale.y", CurveKind = "float" },
            11 => new UnityNavicharaCurveBinding { Component = "GameObject", Property = "m_IsActive", CurveKind = "floatStep" },
            12 => new UnityNavicharaCurveBinding { Component = "RectTransform", Property = "m_SizeDelta.x", CurveKind = "float" },
            13 => new UnityNavicharaCurveBinding { Component = "RectTransform", Property = "m_SizeDelta.y", CurveKind = "float" },
            18 => new UnityNavicharaCurveBinding { Component = "MultipleImage", Property = "_selectSpriteIndex", CurveKind = "floatStep" },
            21 => new UnityNavicharaCurveBinding { Component = "Graphic", Property = "m_Color.r", CurveKind = "float" },
            22 => new UnityNavicharaCurveBinding { Component = "Graphic", Property = "m_Color.g", CurveKind = "float" },
            23 => new UnityNavicharaCurveBinding { Component = "Graphic", Property = "m_Color.b", CurveKind = "float" },
            24 => new UnityNavicharaCurveBinding { Component = "CanvasGroup", Property = "m_Alpha", CurveKind = "float" },
            _ => null,
        };
    }

    private static bool IsSupportedTrack(int trackType)
    {
        return trackType is 0 or 1 or 5 or 6 or 7 or 11 or 12 or 13 or 18 or >= 21 and <= 28;
    }

    private static void AddUnsupportedTrack(
        ClipPlan plan,
        ResolvedSourceSlot slot,
        NodeInfo node,
        TrackInfo track,
        List<UnityNavicharaDiagnostic> diagnostics,
        List<UnityNavicharaUnsupportedTrack> unsupported)
    {
        var trackType = track.TrackType ?? -1;
        var severity = trackType is 2 or 3 or 4 or 8 && !TrackHasNonZeroValue(track) ? "info" : "high";
        var code = trackType switch
        {
            19 => "UnsupportedSecondaryImageSlot",
            >= 29 and <= 44 => "UnsupportedVertexColorTrack",
            2 or 3 or 4 or 8 => "Unsupported3DTransformTrack",
            _ => "UnsupportedTrack",
        };
        var reason = trackType switch
        {
            19 => "type 19 secondary image slot is not imported in v1",
            >= 29 and <= 44 => "vertex color channels are not imported in v1",
            2 or 3 or 4 or 8 => "3D transform channels are outside the v1 2D NaviChara surface",
            _ => $"track type {trackType} is not imported in v1",
        };
        unsupported.Add(new UnityNavicharaUnsupportedTrack
        {
            SourceAnimation = slot.AnimationName,
            NodeId = node.Index,
            NodeName = node.Name,
            TrackType = trackType,
            Reason = reason,
        });
        diagnostics.Add(new UnityNavicharaDiagnostic
        {
            Severity = severity,
            Code = code,
            TargetClip = plan.Name,
            SourceAnimation = slot.AnimationName,
            NodeId = node.Index,
            NodeName = node.Name,
            TrackType = trackType,
            Message = reason,
            Suggestion = "Remove this source slot or add a later exporter/importer implementation for this channel.",
        });
    }

    private static void AddEndFrameDiagnostics(ClipPlan plan, ResolvedSourceSlot slot, List<UnityNavicharaDiagnostic> diagnostics)
    {
        if (slot.MaxKeyFrame <= slot.SourceDurationFrames)
        {
            return;
        }

        diagnostics.Add(new UnityNavicharaDiagnostic
        {
            Severity = "warning",
            Code = "KeysBeyondAnimationEndFrame",
            TargetClip = plan.Name,
            SourceAnimation = slot.AnimationName,
            Message = $"Animation '{slot.AnimationName}' has keys up to frame {slot.MaxKeyFrame}, beyond ANIM.0x56/end frame {slot.SourceDurationFrames}.",
            Suggestion = "Use an explicit durationFrames override if those late keys must be included in the Unity clip.",
        });
    }

    private static void WriteValidationFrames(
        SbSceneFile scene,
        string svoPath,
        string outputDirectory,
        IReadOnlyList<ClipPlan> plans,
        IReadOnlyList<UnityNavicharaClip> clips,
        List<UnityNavicharaDiagnostic> diagnostics)
    {
        var clipsByName = clips.ToDictionary(static clip => clip.Name, StringComparer.Ordinal);
        foreach (var plan in plans)
        {
            if (!clipsByName.TryGetValue(plan.Name, out var clip))
            {
                continue;
            }

            var resolvedSlots = ResolveSourceSlots(scene, plan, diagnostics);
            foreach (var frame in clip.ValidationFrames)
            {
                var frameState = SbSceneAnimationFrameBuilder.BuildInitial(scene);
                foreach (var slot in resolvedSlots)
                {
                    var sourceFrame = slot.IsCurve ? ResolveSourceFrame(slot, frame) : slot.FixedFrame;
                    SbSceneAnimationFrameBuilder.ApplyAnimation(scene, frameState, slot.Animation, sourceFrame);
                }

                var render = SbScenePngRenderer.Render(scene, svoPath, frameState, new SbSceneRenderOptions());
                foreach (var warning in render.Warnings)
                {
                    diagnostics.Add(new UnityNavicharaDiagnostic
                    {
                        Severity = "warning",
                        Code = "ValidationRenderWarning",
                        TargetClip = plan.Name,
                        Message = warning,
                    });
                }

                PngWriter.Write(Path.Combine(outputDirectory, "validation", plan.Name, $"f{frame:D3}.png"), render.Image);
            }
        }
    }

    private static IReadOnlyList<UnityNavicharaProfileTemplateTrack> BuildTemplateTracks(SbSceneFile scene, AnimationInfo animation)
    {
        var tracks = new List<UnityNavicharaProfileTemplateTrack>();
        foreach (var motion in animation.Motions)
        {
            var nodeId = ResolveMotionNodeIndex(scene.Surfboard.Nodes, motion) ?? -1;
            var node = nodeId >= 0 && nodeId < scene.Surfboard.Nodes.Count ? scene.Surfboard.Nodes[nodeId] : null;
            tracks.AddRange(motion.Tracks.Select(track => new UnityNavicharaProfileTemplateTrack
            {
                NodeId = nodeId,
                NodeName = node?.Name,
                TrackType = track.TrackType ?? -1,
                TrackTypeName = track.TrackTypeName,
                FirstFrame = track.FirstFrame,
                LastFrame = track.LastFrame,
                KeyCount = track.Keyframes.Count,
            }));
        }

        return tracks
            .OrderBy(static track => track.NodeId)
            .ThenBy(static track => track.TrackType)
            .ToArray();
    }

    private static string? GuessTargetClip(string? animationName)
    {
        if (string.IsNullOrWhiteSpace(animationName))
        {
            return null;
        }

        if (animationName.Contains("Wait", StringComparison.OrdinalIgnoreCase) || animationName.Contains("Idle", StringComparison.OrdinalIgnoreCase))
        {
            return "Navi_Default";
        }

        if (animationName.Contains("Welcom", StringComparison.OrdinalIgnoreCase) || animationName.Contains("Welcome", StringComparison.OrdinalIgnoreCase))
        {
            return "Navi_Welcom";
        }

        if (animationName.Contains("Sad", StringComparison.OrdinalIgnoreCase))
        {
            return "Navi_Sad_01";
        }

        if (animationName.Contains("Joy", StringComparison.OrdinalIgnoreCase) || animationName.Contains("Fun", StringComparison.OrdinalIgnoreCase))
        {
            if (animationName.Contains("Loop", StringComparison.OrdinalIgnoreCase))
            {
                return "Navi_Fun_Loop_01";
            }

            if (animationName.Contains("End", StringComparison.OrdinalIgnoreCase))
            {
                return "Navi_Fun_End";
            }

            return "Navi_Fun_Start";
        }

        return null;
    }

    private static UnityNavicharaAnimator BuildAnimator()
    {
        return new UnityNavicharaAnimator
        {
            Parameters = ["IsClear"],
            States =
            [
                new UnityNavicharaAnimatorState { Name = "Navi_Default", Motion = "Navi_Default.anim", Loop = true },
                new UnityNavicharaAnimatorState { Name = "Navi_Welcom", Motion = "Navi_Welcom.anim", Loop = false },
                new UnityNavicharaAnimatorState { Name = "Navi_Fun_Start", Motion = "Navi_Fun_Start.anim", Loop = false },
                new UnityNavicharaAnimatorState { Name = "Navi_Fun_Loop_01", Motion = "Navi_Fun_Loop_01.anim", Loop = true },
                new UnityNavicharaAnimatorState { Name = "Navi_Fun_End", Motion = "Navi_Fun_End.anim", Loop = false },
                new UnityNavicharaAnimatorState { Name = "Navi_Sad_01", Motion = "Navi_Sad_01.anim", Loop = true },
                new UnityNavicharaAnimatorState { Name = "Navi_Fun_Loop_02", Motion = "Navi_Fun_Loop_01.anim", Loop = true },
            ],
        };
    }

    private static int? ResolveMotionNodeIndex(IReadOnlyList<NodeInfo> nodes, MotionInfo motion)
    {
        if (motion.CastIndex is int castIndex && castIndex >= 0 && castIndex < nodes.Count)
        {
            return castIndex;
        }

        if (motion.TargetIndex is int targetIndex && targetIndex >= 0 && targetIndex < nodes.Count)
        {
            return targetIndex;
        }

        if (!string.IsNullOrWhiteSpace(motion.TargetName))
        {
            var node = nodes.FirstOrDefault(node => string.Equals(node.Name, motion.TargetName, StringComparison.OrdinalIgnoreCase));
            if (node is not null)
            {
                return node.Index;
            }
        }

        return null;
    }

    private static int GetAnimationEndFrame(AnimationInfo animation)
    {
        return SbSceneAnimationTimeline.GetEndFrame(animation);
    }

    private static int GetAnimationMaxKeyFrame(AnimationInfo animation)
    {
        return SbSceneAnimationTimeline.GetMaxKeyFrame(animation);
    }

    private static bool GetAnimationDefaultRepeat(AnimationInfo animation)
    {
        return GetNumericFieldInt(animation.NumericFields, "0x5F") == 1;
    }

    private static int? GetNumericFieldInt(IReadOnlyList<FieldValueSummary> fields, string idHex)
    {
        return SbSceneAnimationTimeline.GetNumericFieldInt(fields, idHex);
    }

    private static IReadOnlyList<int> BuildValidationFrames(int durationFrames)
    {
        return new[]
            {
                0,
                (int)Math.Round(durationFrames * 0.25),
                (int)Math.Round(durationFrames * 0.5),
                (int)Math.Round(durationFrames * 0.75),
                durationFrames,
            }
            .Distinct()
            .OrderBy(static frame => frame)
            .ToArray();
    }

    private static double ResolveSourceFrame(ResolvedSourceSlot slot, int targetFrame)
    {
        if (!slot.IsCurve)
        {
            return slot.FixedFrame;
        }

        if (slot.Repeat && slot.SourceDurationFrames > 0)
        {
            return targetFrame % slot.SourceDurationFrames;
        }

        return Math.Min(targetFrame, Math.Max(0, slot.SourceDurationFrames));
    }

    private static bool IsCurveSlot(UnityNavicharaSourceSlot slot)
    {
        return slot.Frame is string text && string.Equals(text, "curve", StringComparison.OrdinalIgnoreCase);
    }

    private static int? ResolveDurationFrames(object? durationFrames)
    {
        return durationFrames switch
        {
            null => null,
            int value => value,
            long value when value >= int.MinValue && value <= int.MaxValue => (int)value,
            double value when value >= int.MinValue && value <= int.MaxValue => (int)Math.Round(value),
            string value when string.Equals(value, "autoMax", StringComparison.OrdinalIgnoreCase) => null,
            string value when int.TryParse(value, out var parsed) => parsed,
            _ => null,
        };
    }

    /// <summary>
    /// 把全局 commonBaseSourceSlots 合并进单个 clip 的 sourceSlots,基底 slot 置于最前。
    /// clip 自身已显式引用同一动画的 slot 优先(commonBase 中该动画被跳过),避免重复叠加。
    /// </summary>
    private static List<UnityNavicharaSourceSlot> MergeCommonBaseSlots(
        IReadOnlyList<UnityNavicharaSourceSlot> commonBase,
        IReadOnlyList<UnityNavicharaSourceSlot>? clipSlots)
    {
        var result = new List<UnityNavicharaSourceSlot>();
        var clipAnimations = new HashSet<string>(
            (clipSlots ?? []).Select(static slot => slot.Animation),
            StringComparer.OrdinalIgnoreCase);
        foreach (var baseSlot in commonBase)
        {
            if (clipAnimations.Contains(baseSlot.Animation))
            {
                continue;
            }

            result.Add(new UnityNavicharaSourceSlot
            {
                Animation = baseSlot.Animation,
                Frame = baseSlot.Frame,
                Repeat = baseSlot.Repeat,
            });
        }

        if (clipSlots is not null)
        {
            result.AddRange(clipSlots);
        }

        return result;
    }

    private static void UpsertFixedSlot(List<UnityNavicharaSourceSlot> slots, string animation, int frame)
    {
        slots.RemoveAll(slot => string.Equals(slot.Animation, animation, StringComparison.OrdinalIgnoreCase) && !IsCurveSlot(slot));
        var insertIndex = slots.FindIndex(IsCurveSlot);
        var fixedSlot = new UnityNavicharaSourceSlot
        {
            Animation = animation,
            Frame = frame,
        };
        if (insertIndex < 0)
        {
            slots.Add(fixedSlot);
        }
        else
        {
            slots.Insert(insertIndex, fixedSlot);
        }
    }

    private static bool IsStepTrack(TrackInfo track)
    {
        return track.TrackType is 11 or 18 or 19 || (track.Flags & 0xFF) is 0x23 or 0x33;
    }

    private static bool TrackHasNonZeroValue(TrackInfo track)
    {
        return track.Keyframes
            .Select(key => GetRawKeyValue(track, key))
            .Any(value => value is not null && Math.Abs(value.Value) > Epsilon);
    }

    private static IReadOnlyList<UnityNavicharaCurveKey> CoalesceDuplicateFrames(IEnumerable<UnityNavicharaCurveKey> keys)
    {
        return keys
            .GroupBy(static key => key.Frame)
            .Select(static group => group.Last())
            .OrderBy(static key => key.Frame)
            .ToArray();
    }

    private static int ClampReferenceIndex(int? value, int count)
    {
        return value is >= 0 && value < count ? value.Value : 0;
    }

    private static double ToTime(int frame)
    {
        return frame / (double)UnityNavicharaConstants.SampleRate;
    }

    private static string ComputeHash(string path)
    {
        return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    }

    private static string AnimationDisplayName(AnimationInfo animation)
    {
        return string.IsNullOrWhiteSpace(animation.Name) ? $"ANIM_{animation.Index:D3}" : animation.Name!;
    }

    private static string MakeSafeUnityName(string? name, int index)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return $"node_{index:D4}";
        }

        var chars = name.Trim().Select(ch => ch is '/' or '\\' || char.IsControl(ch) ? '_' : ch).ToArray();
        var result = new string(chars);
        return string.IsNullOrWhiteSpace(result) ? $"node_{index:D4}" : result;
    }

    private static string MakeSafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(ch => invalid.Contains(ch) || ch is '/' or '\\' || char.IsControl(ch) ? '_' : ch).ToArray();
        var result = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(result) ? "sprite" : result;
    }

    private static string MakeUniqueFileName(string baseName, HashSet<string> usedFileNames)
    {
        var fileName = $"{baseName}.png";
        if (usedFileNames.Add(fileName))
        {
            return fileName;
        }

        for (var suffix = 2; ; suffix++)
        {
            fileName = $"{baseName}_{suffix}.png";
            if (usedFileNames.Add(fileName))
            {
                return fileName;
            }
        }
    }

    private static string EscapeMarkdown(string text)
    {
        return text.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
    }

    private sealed record ClipPlan(
        string Name,
        bool Loop,
        int? DurationFrames,
        IReadOnlyList<int>? ValidationFrames,
        List<UnityNavicharaSourceSlot> SourceSlots)
    {
        /// <summary>
        /// 获取或设置Placeholder，用于标记占位剪辑或占位资源，供导出校验和补全逻辑判断。
        /// </summary>
        public bool Placeholder { get; set; }
    }

    private sealed record ResolvedSourceSlot(
        string AnimationName,
        AnimationInfo Animation,
        bool IsCurve,
        double FixedFrame,
        bool Repeat,
        int SourceDurationFrames,
        int MaxKeyFrame);

    private sealed record PendingCurve(ResolvedSourceSlot Slot, TrackInfo Track, int NodeId);

    private readonly record struct TrackChannel(int NodeId, int TrackType);

    private sealed class TrackValueTransform
    {
        private readonly double _valueScale;
        private readonly double _valueOffset;

        private TrackValueTransform(double valueScale, double valueOffset = 0)
        {
            _valueScale = valueScale;
            _valueOffset = valueOffset;
        }

        /// <summary>
        /// 创建Create，封装调用方后续复用的配置或数据结构。
        /// </summary>
        /// <param name="track">参与本次处理的轨道。</param>
        /// <param name="settings">参与本次处理的导出设置。</param>
        /// <returns>可继续累积颜色和显示状态变化的曲线构建器。</returns>
        public static TrackValueTransform Create(TrackInfo track, UnityNavicharaSettings settings)
        {
            return track.TrackType switch
            {
                5 => new TrackValueTransform(settings.RotationZMultiplier),
                1 => new TrackValueTransform(-1),
                11 or 18 => new TrackValueTransform(1),
                >= 21 and <= 24 => new TrackValueTransform(UsesByteColorRange(track) ? 1.0 / 255.0 : 1),
                _ => new TrackValueTransform(1),
            };
        }

        /// <summary>
        /// 将一个曲线关键帧应用到构建器当前状态。
        /// </summary>
        /// <param name="value">参与本次处理的值。</param>
        /// <returns>计算得到的数值。</returns>
        public double Apply(double value)
        {
            return value * _valueScale + _valueOffset;
        }

        /// <summary>
        /// 计算当前关键帧相对上一关键帧的状态差异。
        /// </summary>
        /// <param name="value">参与本次处理的值。</param>
        /// <returns>计算得到的数值。</returns>
        public double ApplyDelta(double value)
        {
            return value * _valueScale;
        }

        private static bool UsesByteColorRange(TrackInfo track)
        {
            return track.Keyframes
                .Select(key => GetRawKeyValue(track, key))
                .Any(value => value is > 1.0 + Epsilon);
        }
    }

    private static double ToUnityY(double sourceY)
    {
        return -sourceY;
    }

    private static double ToUnityRotationZ(double sourceDegrees, UnityNavicharaSettings settings)
    {
        return sourceDegrees * settings.RotationZMultiplier;
    }

    private static UnityNavicharaVector2 BuildUnityPivotPixels(SbSceneImageCast imageCast)
    {
        return new UnityNavicharaVector2
        {
            X = imageCast.PivotX,
            Y = imageCast.Height - imageCast.PivotY,
        };
    }

    private static UnityNavicharaVector2 BuildUnityPivotNormalized(SbSceneImageCast imageCast)
    {
        return new UnityNavicharaVector2
        {
            X = imageCast.Width == 0 ? 0.5 : imageCast.PivotX / imageCast.Width,
            Y = imageCast.Height == 0 ? 0.5 : (imageCast.Height - imageCast.PivotY) / imageCast.Height,
        };
    }

    private static bool IsColorTrack(int trackType)
    {
        return trackType is >= 21 and <= 28;
    }

    private static string FormatUnityGraphicColor(Transform2DInfo? transform)
    {
        var material = ToRgbaColor(transform?.MaterialColor, SbSceneColorConventions.OpaqueWhite);
        var illumination = ToRgbaColor(transform?.IlluminationColor, SbSceneColorConventions.OpaqueBlack);
        return ComposeGraphicColor(material, illumination);
    }

    private static string FormatUnityGraphicColor(SbSceneNodeAnimationState nodeState)
    {
        return ComposeGraphicColor(nodeState.MaterialColor, nodeState.IlluminationColor);
    }

    private static string ComposeGraphicColor(RgbaColor material, RgbaColor illumination)
    {
        var illuminationA = illumination.A / 255.0;
        var color = new RgbaColor(
            ToByteChannel(material.R / 255.0 + illumination.R / 255.0 * illuminationA),
            ToByteChannel(material.G / 255.0 + illumination.G / 255.0 * illuminationA),
            ToByteChannel(material.B / 255.0 + illumination.B / 255.0 * illuminationA),
            material.A);
        return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private static RgbaColor ToRgbaColor(ColorArgbValue? color, RgbaColor fallback)
    {
        return color is null
            ? fallback
            : new RgbaColor(color.R, color.G, color.B, color.A);
    }

    private static byte ToByteChannel(double value)
    {
        return (byte)Math.Clamp((int)Math.Round(Math.Clamp(value, 0.0, 1.0) * 255.0), byte.MinValue, byte.MaxValue);
    }

    private sealed class NodeSlotComparer : IEqualityComparer<(int NodeId, string Slot)>
    {
        /// <summary>
        /// 表示Instance，用于表达该模型在解析、渲染或导出流程中的具体业务含义。
        /// </summary>
        public static NodeSlotComparer Instance { get; } = new();

        /// <summary>
        /// 比较两个轨道通道是否引用同一节点和轨道类型，用作字典键匹配。
        /// </summary>
        /// <param name="x">参与几何边界、坐标或变换计算的位置值。</param>
        /// <param name="y">参与几何边界、坐标或变换计算的位置值。</param>
        /// <returns>如果条件成立则为 true；否则为 false。</returns>
        public bool Equals((int NodeId, string Slot) x, (int NodeId, string Slot) y)
        {
            return x.NodeId == y.NodeId && string.Equals(x.Slot, y.Slot, StringComparison.Ordinal);
        }

        /// <summary>
        /// 获取哈希代码，用于展示、比较、索引查找或后续计算。
        /// </summary>
        /// <param name="obj">要生成哈希码的节点轨道键。</param>
        /// <returns>由节点 ID 和轨道槽位组合得到的哈希码。</returns>
        public int GetHashCode((int NodeId, string Slot) obj)
        {
            return HashCode.Combine(obj.NodeId, StringComparer.Ordinal.GetHashCode(obj.Slot));
        }
    }
}

internal static class UnityNavicharaCurveKeyExtensions
{
    /// <summary>
    /// 返回带有指定帧号的动画选择，用于构造导出采样请求。
    /// </summary>
    /// <param name="key">要复制并替换帧号的曲线关键帧。</param>
    /// <param name="frame">要采样或渲染的动画帧位置。</param>
    /// <returns>保留原曲线值和切线信息、但帧号和时间已更新的关键帧。</returns>
    public static UnityNavicharaCurveKey WithFrame(this UnityNavicharaCurveKey key, int frame)
    {
        return new UnityNavicharaCurveKey
        {
            Frame = frame,
            Time = frame / (double)UnityNavicharaConstants.SampleRate,
            Value = key.Value,
            Interp = key.Interp,
            HasInTangent = key.HasInTangent,
            HasOutTangent = key.HasOutTangent,
            InTangent = key.InTangent,
            OutTangent = key.OutTangent,
        };
    }
}
