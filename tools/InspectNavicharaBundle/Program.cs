using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using System.Text.Json.Serialization;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using BCnEncoder.Decoder;
using BCnEncoder.Shared;
using SbScene.Core.Images;

var cabOnly = false;
string? extractTexturesDir = null;
var pathList = new List<string>();
for (var argIndex = 0; argIndex < args.Length; argIndex++)
{
    switch (args[argIndex])
    {
        case "--cab-only":
            cabOnly = true;
            break;
        case "--extract-textures":
            if (argIndex + 1 >= args.Length)
            {
                Console.Error.WriteLine("missing output directory after --extract-textures");
                return 1;
            }

            extractTexturesDir = args[++argIndex];
            break;
        default:
            pathList.Add(args[argIndex]);
            break;
    }
}

var paths = pathList.ToArray();

if (paths.Length == 0)
{
    Console.Error.WriteLine("usage: InspectNavicharaBundle [--cab-only] [--extract-textures <outdir>] <bundle.ab> [more.ab...]");
    return 1;
}

var options = new JsonSerializerOptions
{
    WriteIndented = true,
    NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
};
var reports = new List<object>();
foreach (var path in paths)
{
    reports.Add(Inspect(path, cabOnly, extractTexturesDir));
}

Console.WriteLine(JsonSerializer.Serialize(reports, options));
return 0;

static object Inspect(string path, bool cabOnly, string? extractTexturesDir)
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
            if (isAssetsFile && !cabOnly)
            {
                var assetsInst = manager.LoadAssetsFileFromBundle(bundle, dirIndex, false);
                assetsReport = InspectAssets(manager, bundle, dirIndex, assetsInst, fullPath, extractTexturesDir);
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

static object InspectAssets(
    AssetsManager manager,
    BundleFileInstance bundle,
    int assetsDirIndex,
    AssetsFileInstance assetsInst,
    string bundlePath,
    string? extractTexturesDir)
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
        .Where(info => info.TypeId is (int)AssetClassID.GameObject or (int)AssetClassID.Transform or (int)AssetClassID.RectTransform or (int)AssetClassID.Animator or (int)AssetClassID.AnimationClip or (int)AssetClassID.AnimatorController or (int)AssetClassID.Material or (int)AssetClassID.Sprite or (int)AssetClassID.Texture2D)
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

    var texture2ds = file.GetAssetsOfType(AssetClassID.Texture2D)
        .Select(info => InspectTexture2D(manager, bundle, assetsDirIndex, assetsInst, info, bundlePath, extractTexturesDir))
        .ToList();

    var sprites = file.GetAssetsOfType(AssetClassID.Sprite)
        .Select(info => InspectSprite(manager, assetsInst, info))
        .ToList();

    var specialAssets = file.AssetInfos
        .Where(info => info.TypeId == 290)
        .Select(info =>
        {
            var field = manager.GetBaseField(assetsInst, info, AssetReadFlags.None);
            return new
            {
                info.PathId,
                info.TypeId,
                info.ByteSize,
                fields = FlattenFields(field, maxDepth: 6).ToList(),
                pptrs = FindPPtrs(field, maxDepth: 8).ToList(),
            };
        })
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
        texture2ds,
        sprites,
        namedAssets,
        specialAssets,
    };
}

static object InspectTexture2D(
    AssetsManager manager,
    BundleFileInstance bundle,
    int assetsDirIndex,
    AssetsFileInstance assetsInst,
    AssetFileInfo info,
    string bundlePath,
    string? extractTexturesDir)
{
    var field = manager.GetBaseField(assetsInst, info, AssetReadFlags.None);
    var name = ReadString(field, "m_Name");
    var width = ReadInt(field, "m_Width");
    var height = ReadInt(field, "m_Height");
    var completeImageSize = ReadInt(field, "m_CompleteImageSize");
    var textureFormat = ReadInt(field, "m_TextureFormat");
    var streamData = InspectStreamData(field["m_StreamData"]);
    var exportedPath = TryExportTexture(
        bundle,
        assetsDirIndex,
        field,
        bundlePath,
        extractTexturesDir,
        name,
        width,
        height,
        textureFormat);
    return new
    {
        pathId = info.PathId,
        info.ByteSize,
        name,
        width,
        height,
        completeImageSize,
        textureFormat,
        mipCount = ReadInt(field, "m_MipCount"),
        isReadable = ReadBool(field, "m_IsReadable"),
        imageDataSize = ReadArraySize(field["image data"]),
        streamData,
        exportedPath,
    };
}

static object InspectSprite(AssetsManager manager, AssetsFileInstance assetsInst, AssetFileInfo info)
{
    var field = manager.GetBaseField(assetsInst, info, AssetReadFlags.None);
    return new
    {
        pathId = info.PathId,
        info.ByteSize,
        name = ReadString(field, "m_Name"),
        rect = InspectRect(field["m_Rect"]),
        offset = InspectVector2(field["m_Offset"]),
        border = InspectVector4(field["m_Border"]),
        pixelsToUnits = ReadFloat(field, "m_PixelsToUnits"),
        pivot = InspectVector2(field["m_Pivot"]),
        extrude = ReadUInt(field, "m_Extrude"),
        texture = InspectPPtr(field["m_RD"]["texture"]),
        alphaTexture = InspectPPtr(field["m_RD"]["alphaTexture"]),
    };
}

static string? TryExportTexture(
    BundleFileInstance bundle,
    int assetsDirIndex,
    AssetTypeValueField textureField,
    string bundlePath,
    string? extractTexturesDir,
    string? textureName,
    int? width,
    int? height,
    int? textureFormat)
{
    if (extractTexturesDir == null || width is null || height is null || textureFormat is null)
    {
        return null;
    }

    var imageBytes = ReadTextureBytes(bundle, assetsDirIndex, textureField);
    if (imageBytes == null)
    {
        return null;
    }

    Directory.CreateDirectory(extractTexturesDir);
    var safeName = MakeSafeFileName(string.IsNullOrWhiteSpace(textureName) ? Path.GetFileNameWithoutExtension(bundlePath) : textureName);
    var ddsPath = Path.Combine(extractTexturesDir, safeName + ".dds");
    var pngPath = Path.Combine(extractTexturesDir, safeName + ".png");

    var pngWritten = textureFormat == 25 && imageBytes.Length == width.Value * height.Value
        ? TryWriteBc7Png(imageBytes, width.Value, height.Value, pngPath)
        : false;
    var ddsBytes = textureFormat == 25 && imageBytes.Length == width.Value * height.Value
        ? BuildDds(width.Value, height.Value, "BC7 ", imageBytes)
        : null;
    if (ddsBytes == null)
    {
        return null;
    }

    File.WriteAllBytes(ddsPath, ddsBytes);
    if (!pngWritten)
    {
        TryConvertDdsToPng(ddsPath, pngPath);
    }

    return File.Exists(pngPath) ? pngPath : ddsPath;
}

static bool TryWriteBc7Png(byte[] imageBytes, int width, int height, string pngPath)
{
    try
    {
        var decoder = new BcDecoder();
        var pixels = decoder.DecodeRaw(imageBytes, width, height, CompressionFormat.Bc7);
        var rgba = new byte[width * height * 4];
        for (var index = 0; index < pixels.Length; index++)
        {
            rgba[index * 4] = pixels[index].r;
            rgba[index * 4 + 1] = pixels[index].g;
            rgba[index * 4 + 2] = pixels[index].b;
            rgba[index * 4 + 3] = pixels[index].a;
        }

        PngWriter.Write(pngPath, new RgbaImage(width, height, rgba));
        return true;
    }
    catch
    {
        return false;
    }
}

static byte[]? ReadTextureBytes(BundleFileInstance bundle, int assetsDirIndex, AssetTypeValueField textureField)
{
    var imageData = textureField["image data"];
    var imageSize = ReadArraySize(imageData);
    if (imageSize > 0)
    {
        var bytes = new byte[imageSize.Value];
        for (var index = 0; index < bytes.Length; index++)
        {
            bytes[index] = imageData["Array"][index].AsByte;
        }

        return bytes;
    }

    var streamData = textureField["m_StreamData"];
    if (streamData.IsDummy)
    {
        return null;
    }

    var streamPath = ReadString(streamData, "path");
    var offset = ReadULong(streamData, "offset");
    var size = ReadUInt(streamData, "size");
    if (streamPath == null || offset is null || size is null || size == 0)
    {
        return null;
    }

    var resDirIndex = FindBundleDirectoryIndex(bundle, assetsDirIndex, streamPath);
    if (resDirIndex < 0)
    {
        return null;
    }

    bundle.file.GetFileRange(resDirIndex, out var fileOffset, out var fileSize);
    if (offset.Value + size.Value > (ulong)fileSize)
    {
        return null;
    }

    var reader = bundle.file.DataReader;
    lock (reader)
    {
        reader.Position = fileOffset + (long)offset.Value;
        return reader.ReadBytes((int)size.Value);
    }
}

static int FindBundleDirectoryIndex(BundleFileInstance bundle, int assetsDirIndex, string streamPath)
{
    var streamName = streamPath.Replace('\\', '/').Split('/').Last();
    if (string.IsNullOrEmpty(streamName))
    {
        return -1;
    }

    var assetName = bundle.file.GetFileName(assetsDirIndex);
    var assetPrefix = assetName.Contains('/')
        ? assetName[..(assetName.LastIndexOf('/') + 1)]
        : string.Empty;
    var preferredName = assetPrefix + streamName;
    var preferredIndex = bundle.file.GetFileIndex(preferredName);
    if (preferredIndex >= 0)
    {
        return preferredIndex;
    }

    var allNames = bundle.file.GetAllFileNames();
    for (var index = 0; index < allNames.Count; index++)
    {
        if (string.Equals(allNames[index], streamName, StringComparison.Ordinal) ||
            allNames[index].EndsWith("/" + streamName, StringComparison.Ordinal))
        {
            return index;
        }
    }

    return -1;
}

static byte[] BuildDds(int width, int height, string fourCc, byte[] payload)
{
    var output = new byte[128 + payload.Length];
    using var stream = new MemoryStream(output);
    using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
    writer.Write(Encoding.ASCII.GetBytes("DDS "));
    writer.Write(124u);
    writer.Write(0x0002100Fu);
    writer.Write((uint)height);
    writer.Write((uint)width);
    writer.Write((uint)Math.Max(1, payload.Length));
    writer.Write(0u);
    writer.Write(1u);
    for (var index = 0; index < 11; index++)
    {
        writer.Write(0u);
    }

    writer.Write(32u);
    writer.Write(0x00000004u);
    writer.Write(Encoding.ASCII.GetBytes(fourCc));
    writer.Write(0u);
    writer.Write(0u);
    writer.Write(0u);
    writer.Write(0u);
    writer.Write(0u);
    writer.Write(0x00001000u);
    writer.Write(0u);
    writer.Write(0u);
    writer.Write(0u);
    writer.Write(0u);
    Buffer.BlockCopy(payload, 0, output, 128, payload.Length);
    return output;
}

static void TryConvertDdsToPng(string ddsPath, string pngPath)
{
    var tools = new[] { "magick", "texconv" };
    foreach (var tool in tools)
    {
        try
        {
            var info = new System.Diagnostics.ProcessStartInfo
            {
                FileName = tool,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };
            if (tool == "magick")
            {
                info.ArgumentList.Add(ddsPath);
                info.ArgumentList.Add(pngPath);
            }
            else
            {
                info.ArgumentList.Add("-y");
                info.ArgumentList.Add("-ft");
                info.ArgumentList.Add("png");
                info.ArgumentList.Add("-o");
                info.ArgumentList.Add(Path.GetDirectoryName(pngPath) ?? ".");
                info.ArgumentList.Add(ddsPath);
            }

            using var process = System.Diagnostics.Process.Start(info);
            if (process == null)
            {
                continue;
            }

            process.WaitForExit(30000);
            if (process.ExitCode == 0 && File.Exists(pngPath))
            {
                return;
            }
        }
        catch
        {
        }
    }
}

static string MakeSafeFileName(string name)
{
    var invalid = Path.GetInvalidFileNameChars();
    var builder = new StringBuilder(name.Length);
    foreach (var ch in name)
    {
        builder.Append(invalid.Contains(ch) ? '_' : ch);
    }

    return builder.ToString();
}

static object? InspectStreamData(AssetTypeValueField field)
{
    if (field.IsDummy)
    {
        return null;
    }

    return new
    {
        offset = ReadULong(field, "offset"),
        size = ReadUInt(field, "size"),
        path = ReadString(field, "path"),
    };
}

static object? InspectRect(AssetTypeValueField field)
{
    if (field.IsDummy)
    {
        return null;
    }

    return new
    {
        x = ReadFloat(field, "x"),
        y = ReadFloat(field, "y"),
        width = ReadFloat(field, "width"),
        height = ReadFloat(field, "height"),
    };
}

static object? InspectVector2(AssetTypeValueField field)
{
    if (field.IsDummy)
    {
        return null;
    }

    return new
    {
        x = ReadFloat(field, "x"),
        y = ReadFloat(field, "y"),
    };
}

static object? InspectVector4(AssetTypeValueField field)
{
    if (field.IsDummy)
    {
        return null;
    }

    return new
    {
        x = ReadFloat(field, "x"),
        y = ReadFloat(field, "y"),
        z = ReadFloat(field, "z"),
        w = ReadFloat(field, "w"),
    };
}

static object? InspectPPtr(AssetTypeValueField field)
{
    if (field.IsDummy)
    {
        return null;
    }

    return new
    {
        fileId = field["m_FileID"].IsDummy ? (int?)null : field["m_FileID"].AsInt,
        pathId = field["m_PathID"].IsDummy ? (long?)null : field["m_PathID"].AsLong,
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

static int? ReadInt(AssetTypeValueField field, string name)
{
    var child = field[name];
    return child.IsDummy ? null : child.AsInt;
}

static uint? ReadUInt(AssetTypeValueField field, string name)
{
    var child = field[name];
    return child.IsDummy ? null : child.AsUInt;
}

static ulong? ReadULong(AssetTypeValueField field, string name)
{
    var child = field[name];
    return child.IsDummy ? null : child.AsULong;
}

static float? ReadFloat(AssetTypeValueField field, string name)
{
    var child = field[name];
    return child.IsDummy ? null : child.AsFloat;
}

static bool? ReadBool(AssetTypeValueField field, string name)
{
    var child = field[name];
    return child.IsDummy ? null : child.AsBool;
}

static int? ReadArraySize(AssetTypeValueField field)
{
    if (field.IsDummy || field.Children.Count == 0)
    {
        return null;
    }

    var size = field["size"];
    return size.IsDummy ? null : size.AsInt;
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
