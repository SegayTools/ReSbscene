using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

if (args.Length == 0)
{
    Console.Error.WriteLine("usage: InspectNavicharaBundle <bundle.ab> [more.ab...]");
    return 1;
}

var options = new JsonSerializerOptions
{
    WriteIndented = true,
    NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
};
var reports = new List<object>();
foreach (var path in args)
{
    reports.Add(Inspect(path));
}

Console.WriteLine(JsonSerializer.Serialize(reports, options));
return 0;

static object Inspect(string path)
{
    var fullPath = Path.GetFullPath(path);
    var bytes = File.ReadAllBytes(fullPath);
    using var sha = SHA256.Create();
    var manager = new AssetsManager();
    try
    {
        var bundle = manager.LoadBundleFile(new MemoryStream(bytes), fullPath, true);
        var dirs = bundle.file.BlockAndDirInfo.DirectoryInfos;
        var dirReports = new List<object>();
        for (var dirIndex = 0; dirIndex < dirs.Count; dirIndex++)
        {
            var dir = dirs[dirIndex];
            var isAssetsFile = bundle.file.IsAssetsFile(dirIndex);
            object? assetsReport = null;
            if (isAssetsFile)
            {
                var assetsInst = manager.LoadAssetsFileFromBundle(bundle, dirIndex, false);
                assetsReport = InspectAssets(manager, assetsInst);
            }

            dirReports.Add(new
            {
                index = dirIndex,
                name = bundle.file.GetFileName(dirIndex),
                isAssetsFile,
                dir.DecompressedSize,
                dir.Flags,
                assets = assetsReport,
            });
        }

        return new
        {
            path = fullPath,
            size = bytes.Length,
            sha256 = Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant(),
            compression = bundle.file.GetCompressionType().ToString(),
            directoryCount = dirs.Count,
            directories = dirReports,
        };
    }
    finally
    {
        manager.UnloadAll(true);
    }
}

static object InspectAssets(AssetsManager manager, AssetsFileInstance assetsInst)
{
    var file = assetsInst.file;
    var typeCounts = file.AssetInfos
        .GroupBy(info => info.TypeId)
        .OrderBy(group => group.Key)
        .ToDictionary(group => group.Key.ToString(), group => group.Count());

    var monoScripts = file.GetAssetsOfType(AssetClassID.MonoScript)
        .Select(info =>
        {
            var baseField = manager.GetBaseField(assetsInst, info, AssetReadFlags.None);
            return new MonoScriptReport(
                info.PathId,
                info.ByteSize,
                ReadString(baseField, "m_ClassName"),
                ReadString(baseField, "m_Namespace"),
                ReadString(baseField, "m_AssemblyName"),
                ReadString(baseField, "m_Name"));
        })
        .OrderBy(script => script.className)
        .ThenBy(script => script.pathId)
        .ToList();

    var monoBehaviours = file.GetAssetsOfType(AssetClassID.MonoBehaviour)
        .Select(info => InspectMonoBehaviour(manager, assetsInst, info, monoScripts.ToDictionary(s => s.pathId)))
        .OrderBy(beh => beh.pathId)
        .ToList();

    var namedAssets = file.AssetInfos
        .Where(info => info.TypeId is (int)AssetClassID.GameObject or (int)AssetClassID.Transform or (int)AssetClassID.RectTransform or (int)AssetClassID.Animator or (int)AssetClassID.AnimationClip or (int)AssetClassID.AnimatorController or (int)AssetClassID.Material or (int)AssetClassID.Sprite)
        .Select(info =>
        {
            string? name = null;
            try
            {
                var field = manager.GetBaseField(assetsInst, info, AssetReadFlags.None);
                name = ReadString(field, "m_Name");
            }
            catch
            {
            }

            return new { info.PathId, info.TypeId, type = ((AssetClassID)info.TypeId).ToString(), info.ByteSize, name };
        })
        .OrderBy(asset => asset.TypeId)
        .ThenBy(asset => asset.name)
        .ThenBy(asset => asset.PathId)
        .ToList();

    var animators = file.GetAssetsOfType(AssetClassID.Animator)
        .Select(info => InspectAnimator(manager, assetsInst, info))
        .ToList();

    return new
    {
        unityVersion = file.Metadata.UnityVersion,
        typeTreeEnabled = file.Metadata.TypeTreeEnabled,
        assetCount = file.AssetInfos.Count,
        typeCounts,
        externals = file.Metadata.Externals.Select(ex => new { ex.PathName, ex.VirtualAssetPathName, ex.OriginalPathName }).ToList(),
        monoScripts,
        monoBehaviours,
        animators,
        namedAssets,
    };
}

static MonoBehaviourReport InspectMonoBehaviour(
    AssetsManager manager,
    AssetsFileInstance assetsInst,
    AssetFileInfo info,
    IReadOnlyDictionary<long, MonoScriptReport> scripts)
{
    var baseField = manager.GetBaseField(assetsInst, info, AssetReadFlags.None);
    var script = baseField["m_Script"];
    var scriptFileId = script.IsDummy ? 0 : script["m_FileID"].AsInt;
    var scriptPathId = script.IsDummy ? 0 : script["m_PathID"].AsLong;
    var scriptName = scripts.TryGetValue(scriptPathId, out var scriptInfo)
        ? scriptInfo.className
        : null;

    return new MonoBehaviourReport(
        info.PathId,
        info.ByteSize,
        ReadString(baseField, "m_Name"),
        scriptFileId,
        scriptPathId,
        scriptName,
        FlattenFields(baseField, maxDepth: 4)
            .Where(field => field.path is not "Base.m_GameObject" and not "Base.m_Enabled" and not "Base.m_Script" and not "Base.m_Name")
            .ToList(),
        FindPPtrs(baseField, maxDepth: 8).ToList());
}

static AnimatorReport InspectAnimator(AssetsManager manager, AssetsFileInstance assetsInst, AssetFileInfo info)
{
    var baseField = manager.GetBaseField(assetsInst, info, AssetReadFlags.None);
    return new AnimatorReport(
        info.PathId,
        info.ByteSize,
        ReadString(baseField, "m_Name"),
        FlattenFields(baseField, maxDepth: 4).ToList(),
        FindPPtrs(baseField, maxDepth: 8).ToList());
}

static string? ReadString(AssetTypeValueField field, string name)
{
    var child = field[name];
    return child.IsDummy ? null : child.AsString;
}

static IEnumerable<FieldReport> FlattenFields(AssetTypeValueField field, int maxDepth, string path = "Base", int depth = 0)
{
    if (depth > maxDepth)
    {
        yield break;
    }

    if (field.Children.Count == 0)
    {
        yield return new FieldReport(path, field.TypeName, ReadScalar(field));
        yield break;
    }

    if (field.Value != null && field.TypeName != "Array")
    {
        var scalar = ReadScalar(field);
        if (scalar != null)
        {
            yield return new FieldReport(path, field.TypeName, scalar);
        }
    }

    foreach (var child in field.Children)
    {
        var childPath = string.IsNullOrEmpty(child.FieldName) ? path : path + "." + child.FieldName;
        foreach (var entry in FlattenFields(child, maxDepth, childPath, depth + 1))
        {
            yield return entry;
        }
    }
}

static IEnumerable<object> FindPPtrs(AssetTypeValueField field, int maxDepth, string path = "Base", int depth = 0)
{
    if (depth > maxDepth)
    {
        yield break;
    }

    var fileId = field["m_FileID"];
    var pathId = field["m_PathID"];
    if (!fileId.IsDummy && !pathId.IsDummy)
    {
        yield return new { path, fileId = fileId.AsInt, pathId = pathId.AsLong };
        yield break;
    }

    foreach (var child in field.Children)
    {
        var childPath = string.IsNullOrEmpty(child.FieldName) ? path : path + "." + child.FieldName;
        foreach (var entry in FindPPtrs(child, maxDepth, childPath, depth + 1))
        {
            yield return entry;
        }
    }
}

static object? ReadScalar(AssetTypeValueField field)
{
    try
    {
        return field.TypeName switch
        {
            "string" => field.AsString,
            "bool" => field.AsBool,
            "SInt8" => field.AsSByte,
            "UInt8" => field.AsByte,
            "char" => field.AsByte,
            "SInt16" => field.AsShort,
            "UInt16" => field.AsUShort,
            "SInt32" => field.AsInt,
            "int" => field.AsInt,
            "UInt32" => field.AsUInt,
            "SInt64" => field.AsLong,
            "UInt64" => field.AsULong,
            "float" => field.AsFloat,
            "double" => field.AsDouble,
            _ => null,
        };
    }
    catch
    {
        return null;
    }
}

internal sealed record MonoScriptReport(
    long pathId,
    uint byteSize,
    string? className,
    string? namespaceName,
    string? assemblyName,
    string? name);

internal sealed record FieldReport(string path, string typeName, object? value);

internal sealed record MonoBehaviourReport(
    long pathId,
    uint byteSize,
    string? name,
    int scriptFileId,
    long scriptPathId,
    string? scriptName,
    List<FieldReport> fields,
    List<object> pptrs);

internal sealed record AnimatorReport(
    long pathId,
    uint byteSize,
    string? name,
    List<FieldReport> fields,
    List<object> pptrs);
