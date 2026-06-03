using System.Text.Json;

namespace SbScene.Core.Unity;

public static class UnityNavicharaConstants
{
    public const string Schema = "sbscene.unityNavicharaExport.v1";
    public const int SampleRate = 60;

    public static readonly IReadOnlyList<string> CoreClipNames =
    [
        "Navi_Default",
        "Navi_Welcom",
        "Navi_Fun_Start",
        "Navi_Fun_Loop_01",
        "Navi_Fun_End",
        "Navi_Sad_01",
    ];

    public static bool IsCoreClip(string name)
    {
        return CoreClipNames.Contains(name, StringComparer.Ordinal);
    }

    public static bool DefaultLoop(string name)
    {
        return name is "Navi_Default" or "Navi_Fun_Loop_01" or "Navi_Sad_01";
    }
}

public sealed class UnityNavicharaExportOptions
{
    public int CharacterId { get; init; }

    public UnityNavicharaExportProfile? Profile { get; init; }

    public IReadOnlyList<UnityNavicharaAnimationMap> Maps { get; init; } = Array.Empty<UnityNavicharaAnimationMap>();

    public int? FashionFrame { get; init; }

    public int? AccessoryFrame { get; init; }

    public int? PositionFrame { get; init; }

    public bool AllowPlaceholderClips { get; init; }

    public bool BakeSampledCurves { get; init; }

    public bool ExtractSprites { get; init; }

    public bool WriteValidationFrames { get; init; }

    public bool Strict { get; init; }
}

public sealed record UnityNavicharaAnimationMap(string SourceAnimation, string TargetClip);

public sealed class UnityNavicharaExportResult
{
    public required UnityNavicharaExport Export { get; init; }

    public required IReadOnlyList<UnityNavicharaDiagnostic> Diagnostics { get; init; }

    public required bool Failed { get; init; }
}

public sealed class UnityNavicharaExport
{
    public string Schema { get; init; } = UnityNavicharaConstants.Schema;

    public required UnityNavicharaSource Source { get; init; }

    public required UnityNavicharaSettings Settings { get; init; }

    public required UnityNavicharaCharacter Character { get; init; }

    public required IReadOnlyList<UnityNavicharaNode> Nodes { get; init; }

    public required IReadOnlyList<UnityNavicharaSprite> Sprites { get; init; }

    public required IReadOnlyList<UnityNavicharaClip> Clips { get; init; }

    public required UnityNavicharaValidation Validation { get; init; }

    public required UnityNavicharaAnimator Animator { get; init; }

    public required IReadOnlyList<UnityNavicharaDiagnostic> Diagnostics { get; init; }
}

public sealed class UnityNavicharaSource
{
    public required string Sbscene { get; init; }

    public required string Svo { get; init; }

    public required string SceneHash { get; init; }

    public required string ExporterVersion { get; init; }
}

public sealed class UnityNavicharaSettings
{
    public int SampleRate { get; init; } = UnityNavicharaConstants.SampleRate;

    public string CoordinateSystem { get; init; } = "sbscene-y-down-to-unity-y-up";

    public double RotationZMultiplier { get; init; } = 1.0;

    public double PixelsPerUnit { get; init; } = 1.0;

    public string CurveBakeMode { get; init; } = "keyed";

    public bool PreserveSourceCoordinates { get; init; }

    public UnityNavicharaRootTransform RootTransform { get; init; } = new();
}

public sealed class UnityNavicharaRootTransform
{
    public double Scale { get; init; } = 1.0;

    public UnityNavicharaVector2 Offset { get; init; } = new();
}

public sealed class UnityNavicharaCharacter
{
    public required int Id { get; init; }

    public required string PrefabName { get; init; }

    public required string ControllerName { get; init; }
}

public sealed class UnityNavicharaNode
{
    public required int Id { get; init; }

    public string? SbsceneName { get; init; }

    public required string UnityName { get; init; }

    public required string UnityPath { get; init; }

    public required int ParentId { get; init; }

    public required bool IsImageCast { get; init; }

    public required UnityNavicharaNodeStatic Static { get; init; }

    public UnityNavicharaNodeImage? Image { get; init; }
}

public sealed class UnityNavicharaNodeStatic
{
    public required UnityNavicharaVector2 AnchoredPosition { get; init; }

    public required double RotationZ { get; init; }

    public required UnityNavicharaVector2 Scale { get; init; }

    public required bool Display { get; init; }

    public required UnityNavicharaVector2 Size { get; init; }

    public required UnityNavicharaVector2 PivotPixels { get; init; }

    public required UnityNavicharaVector2 PivotNormalized { get; init; }

    public required string MaterialColor { get; init; }
}

public sealed class UnityNavicharaNodeImage
{
    public required string Component { get; init; }

    public required int DrawMode { get; init; }

    public required bool AdditiveBlend { get; init; }

    public required IReadOnlyList<string> PrimarySprites { get; init; }

    public required IReadOnlyList<string> SecondarySprites { get; init; }

    public required int DefaultPrimaryIndex { get; init; }

    public required int DefaultSecondaryIndex { get; init; }
}

public sealed class UnityNavicharaSprite
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string File { get; init; }

    public string? SourceTexture { get; init; }

    public required int CropIndex { get; init; }

    public required int NodeId { get; init; }

    public required string Slot { get; init; }

    public required int SlotIndex { get; init; }

    public required UnityNavicharaRect Rect { get; init; }

    public required UnityNavicharaVector2 PivotPixels { get; init; }

    public required UnityNavicharaVector2 PivotNormalized { get; init; }
}

public sealed class UnityNavicharaClip
{
    public required string Name { get; init; }

    public required IReadOnlyList<UnityNavicharaSourceSlot> SourceSlots { get; init; }

    public int SampleRate { get; init; } = UnityNavicharaConstants.SampleRate;

    public required int DurationFrames { get; init; }

    public required bool Loop { get; init; }

    public required IReadOnlyList<int> ValidationFrames { get; init; }

    public required IReadOnlyList<UnityNavicharaCurve> Curves { get; init; }

    public required IReadOnlyList<UnityNavicharaUnsupportedTrack> UnsupportedTracks { get; init; }

    public bool Placeholder { get; init; }
}

public sealed class UnityNavicharaSourceSlot
{
    public required string Animation { get; init; }

    public required object Frame { get; init; }

    public bool? Repeat { get; init; }
}

public sealed class UnityNavicharaCurve
{
    public required int NodeId { get; init; }

    public required string Path { get; init; }

    public required int SbsceneTrackType { get; init; }

    public required UnityNavicharaCurveBinding Unity { get; init; }

    public required IReadOnlyList<UnityNavicharaCurveKey> Keys { get; init; }
}

public sealed class UnityNavicharaCurveBinding
{
    public required string Component { get; init; }

    public required string Property { get; init; }

    public required string CurveKind { get; init; }
}

public sealed class UnityNavicharaCurveKey
{
    public required int Frame { get; init; }

    public required double Time { get; init; }

    public required double Value { get; init; }

    public required string Interp { get; init; }

    public required bool HasInTangent { get; init; }

    public required bool HasOutTangent { get; init; }

    public double? InTangent { get; init; }

    public double? OutTangent { get; init; }
}

public sealed class UnityNavicharaUnsupportedTrack
{
    public required string SourceAnimation { get; init; }

    public required int NodeId { get; init; }

    public string? NodeName { get; init; }

    public required int TrackType { get; init; }

    public required string Reason { get; init; }
}

public sealed class UnityNavicharaDiagnostic
{
    public required string Severity { get; init; }

    public required string Code { get; init; }

    public string? TargetClip { get; init; }

    public string? SourceAnimation { get; init; }

    public int? NodeId { get; init; }

    public string? NodeName { get; init; }

    public int? TrackType { get; init; }

    public required string Message { get; init; }

    public string? Suggestion { get; init; }
}

public sealed class UnityNavicharaValidation
{
    public string FrameStrategy { get; init; } = "autoQuarters";

    public string? ReferenceImageDirectory { get; init; }
}

public sealed class UnityNavicharaAnimator
{
    public required IReadOnlyList<string> Parameters { get; init; }

    public required IReadOnlyList<UnityNavicharaAnimatorState> States { get; init; }
}

public sealed class UnityNavicharaAnimatorState
{
    public required string Name { get; init; }

    public required string Motion { get; init; }

    public required bool Loop { get; init; }
}

public sealed class UnityNavicharaVector2
{
    public double X { get; init; }

    public double Y { get; init; }
}

public sealed class UnityNavicharaRect
{
    public required int X { get; init; }

    public required int Y { get; init; }

    public required int W { get; init; }

    public required int H { get; init; }
}

public sealed class UnityNavicharaExportProfile
{
    public UnityNavicharaProfileSettings Settings { get; init; } = new();

    public Dictionary<string, UnityNavicharaProfileClip> Clips { get; init; } = new(StringComparer.Ordinal);
}

public sealed class UnityNavicharaProfileSettings
{
    public double? PixelsPerUnit { get; init; }

    public string? CurveBakeMode { get; init; }

    public double? RotationZMultiplier { get; init; }

    public UnityNavicharaRootTransform? RootTransform { get; init; }
}

public sealed class UnityNavicharaProfileClip
{
    public bool? Loop { get; init; }

    public object? DurationFrames { get; init; }

    public IReadOnlyList<int>? ValidationFrames { get; init; }

    public List<UnityNavicharaSourceSlot> SourceSlots { get; init; } = [];
}

public sealed class UnityNavicharaProfileTemplate
{
    public required UnityNavicharaProfileSettings Settings { get; init; }

    public required IReadOnlyList<UnityNavicharaProfileTemplateAnimation> Animations { get; init; }

    public required Dictionary<string, UnityNavicharaProfileClip> Clips { get; init; }
}

public sealed class UnityNavicharaProfileTemplateAnimation
{
    public required string Name { get; init; }

    public required int Index { get; init; }

    public required int EndFrame { get; init; }

    public required bool DefaultRepeat { get; init; }

    public required IReadOnlyList<UnityNavicharaProfileTemplateTrack> Tracks { get; init; }

    public string? CandidateTargetClip { get; init; }
}

public sealed class UnityNavicharaProfileTemplateTrack
{
    public required int NodeId { get; init; }

    public string? NodeName { get; init; }

    public required int TrackType { get; init; }

    public string? TrackTypeName { get; init; }

    public int? FirstFrame { get; init; }

    public int? LastFrame { get; init; }

    public required int KeyCount { get; init; }
}

public static class UnityNavicharaProfileLoader
{
    public static UnityNavicharaExportProfile Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var profile = new UnityNavicharaExportProfile
        {
            Settings = ReadSettings(root.TryGetProperty("settings", out var settings) ? settings : default),
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
        var result = new List<UnityNavicharaSourceSlot>();
        if (!clipElement.TryGetProperty("sourceSlots", out var slots) || slots.ValueKind != JsonValueKind.Array)
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
