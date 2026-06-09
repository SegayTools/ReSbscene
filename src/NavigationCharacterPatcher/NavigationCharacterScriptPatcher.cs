using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace NavigationCharacterPatcher;

/// <summary>
/// 提供 NavigationCharacter 脚本引用修补器，用于改写 AssetBundle 中 MonoBehaviour 的脚本 PathID。
/// </summary>
public sealed class NavigationCharacterScriptPatcher
{
    /// <summary>
    /// 表示默认Script路径标识，用于定位输入输出资源或记录来源，保证后续读写指向正确对象。
    /// </summary>
    public const long DefaultScriptPathId = 1119486627253801066L;

    /// <summary>
    /// 表示默认ScriptClass名称，用于识别格式、语义类别或序列化字段身份，帮助处理流程选择正确分支。
    /// </summary>
    public const string DefaultScriptClassName = "NavigationCharacter";

    private readonly Action<string> _log;

    /// <summary>
    /// 初始化Navigation角色信息ScriptPatcher 实例，并保存调用方提供的核心数据。
    /// </summary>
    /// <param name="log">接收诊断日志或非致命警告的回调。</param>
    public NavigationCharacterScriptPatcher(Action<string>? log = null)
    {
        _log = log ?? (_ => { });
    }

    /// <summary>
    /// 改写 AssetBundle 中 NavigationCharacter 的 MonoBehaviour 脚本引用，并返回写出统计。
    /// </summary>
    /// <param name="options">控制本次处理行为的选项。</param>
    /// <returns>包含目标脚本 PathID、改写数量、输出状态和压缩方式的修补结果。</returns>
    /// <example>
    /// <code>
    /// var patcher = new NavigationCharacterScriptPatcher(Console.WriteLine);
    /// var result = patcher.Patch(new PatchOptions
    /// {
    ///     InputPath = "UI_Navichara_27.ab",
    ///     OutputPath = "UI_Navichara_27.patched.ab",
    /// });
    /// Console.WriteLine(result.ModifiedCount);
    /// </code>
    /// </example>
    public PatchResult Patch(PatchOptions options)
    {
        if (!File.Exists(options.InputPath))
        {
            throw new FileNotFoundException("输入 ab 文件不存在: " + options.InputPath, options.InputPath);
        }

        var manager = new AssetsManager();
        BundleFileInstance bundle;

        // 把整个 bundle 读进内存后再操作，避免持有输入文件句柄（便于 -o 指向同一文件时替换）。
        byte[] inputBytes = File.ReadAllBytes(options.InputPath);
        try
        {
            bundle = manager.LoadBundleFile(new MemoryStream(inputBytes), options.InputPath, true);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("无法解析为 AssetBundle: " + options.InputPath + " (" + ex.Message + ")", ex);
        }

        var originalCompression = bundle.file.GetCompressionType();
        _log($"已加载 bundle: {Path.GetFileName(options.InputPath)} (原始压缩: {originalCompression})");

        var directoryInfos = bundle.file.BlockAndDirInfo.DirectoryInfos;
        int totalModified = 0;
        long scriptPathId = 0;
        bool foundScript = false;

        for (var dirIndex = 0; dirIndex < directoryInfos.Count; dirIndex++)
        {
            if (!bundle.file.IsAssetsFile(dirIndex))
            {
                continue;
            }

            var assetsInst = manager.LoadAssetsFileFromBundle(bundle, dirIndex, false);
            var assetsFile = assetsInst.file;

            if (!assetsFile.Metadata.TypeTreeEnabled)
            {
                throw new InvalidOperationException(
                    "该 ab 未内嵌 TypeTree，当前实现无法在缺少 ClassDatabase 的情况下安全改写 MonoBehaviour: "
                    + bundle.file.GetFileName(dirIndex));
            }

            if (!TryFindScriptPathId(manager, assetsInst, options.ScriptClassName, out var localScriptPathId))
            {
                continue;
            }

            foundScript = true;
            scriptPathId = localScriptPathId;
            _log($"在 {bundle.file.GetFileName(dirIndex)} 找到脚本 {options.ScriptClassName}: m_PathID={localScriptPathId}");

            int modifiedInFile = PatchMonoBehaviours(manager, assetsInst, localScriptPathId, options.NewScriptPathId);
            totalModified += modifiedInFile;
            _log($"  改写 MonoBehaviour 数量: {modifiedInFile} -> m_Script.m_PathID={options.NewScriptPathId}");

            if (modifiedInFile > 0 && !options.DryRun)
            {
                directoryInfos[dirIndex].SetNewData(WriteAssetsFile(assetsFile));
            }
        }

        if (!foundScript)
        {
            manager.UnloadAll(true);
            throw new PatchTargetNotFoundException(
                $"未在 ab 内找到名为 {options.ScriptClassName} 的 MonoScript: {options.InputPath}");
        }

        if (totalModified == 0)
        {
            manager.UnloadAll(true);
            throw new PatchTargetNotFoundException(
                $"找到了脚本 {options.ScriptClassName}，但没有任何 MonoBehaviour 引用它，无需修改。");
        }

        if (options.DryRun)
        {
            manager.UnloadAll(true);
            _log("dry-run: 未写出文件。");
            return new PatchResult
            {
                ScriptPathId = scriptPathId,
                ModifiedCount = totalModified,
                WroteOutput = false,
            };
        }

        var targetCompression = ResolveCompression(options.Compression, originalCompression);
        byte[] uncompressedBundle = WriteBundle(bundle.file);
        manager.UnloadAll(true);

        byte[] finalBundle = targetCompression == AssetBundleCompressionType.None
            ? uncompressedBundle
            : PackBundle(uncompressedBundle, targetCompression);

        WriteOutputFile(options.OutputPath, finalBundle);
        _log($"已写出: {options.OutputPath} ({finalBundle.Length} 字节, 压缩: {targetCompression})");

        int verified = VerifyOutput(finalBundle, options.NewScriptPathId);
        if (verified != totalModified)
        {
            throw new InvalidOperationException(
                $"校验失败: 期望 {totalModified} 个 MonoBehaviour 指向新 PathID，实际 {verified} 个。");
        }

        _log($"校验通过: {verified} 个 MonoBehaviour 已指向 m_Script.m_PathID={options.NewScriptPathId}");

        return new PatchResult
        {
            ScriptPathId = scriptPathId,
            ModifiedCount = totalModified,
            WroteOutput = true,
            OutputSize = finalBundle.Length,
            OutputCompression = targetCompression,
        };
    }

    private static bool TryFindScriptPathId(
        AssetsManager manager,
        AssetsFileInstance assetsInst,
        string scriptClassName,
        out long scriptPathId)
    {
        foreach (var info in assetsInst.file.GetAssetsOfType(AssetClassID.MonoScript))
        {
            var baseField = manager.GetBaseField(assetsInst, info, AssetReadFlags.None);
            var classNameField = baseField["m_ClassName"];
            if (!classNameField.IsDummy &&
                string.Equals(classNameField.AsString, scriptClassName, StringComparison.Ordinal))
            {
                scriptPathId = info.PathId;
                return true;
            }
        }

        scriptPathId = 0;
        return false;
    }

    private static int PatchMonoBehaviours(
        AssetsManager manager,
        AssetsFileInstance assetsInst,
        long currentScriptPathId,
        long newScriptPathId)
    {
        int modified = 0;
        foreach (var info in assetsInst.file.GetAssetsOfType(AssetClassID.MonoBehaviour))
        {
            var baseField = manager.GetBaseField(assetsInst, info, AssetReadFlags.None);
            var scriptPtr = baseField["m_Script"];
            if (scriptPtr.IsDummy)
            {
                continue;
            }

            // m_FileID == 0 表示脚本在同一序列化文件内（本地引用）。
            if (scriptPtr["m_FileID"].AsInt != 0 || scriptPtr["m_PathID"].AsLong != currentScriptPathId)
            {
                continue;
            }

            scriptPtr["m_PathID"].AsLong = newScriptPathId;
            info.SetNewData(baseField);
            modified++;
        }

        return modified;
    }

    private static AssetBundleCompressionType ResolveCompression(
        BundleCompression compression,
        AssetBundleCompressionType original)
    {
        return compression switch
        {
            BundleCompression.Keep => original,
            BundleCompression.None => AssetBundleCompressionType.None,
            BundleCompression.Lz4 => AssetBundleCompressionType.LZ4,
            BundleCompression.Lzma => AssetBundleCompressionType.LZMA,
            _ => original,
        };
    }

    private static byte[] WriteAssetsFile(AssetsFile assetsFile)
    {
        using var stream = new MemoryStream();
        using (var writer = new AssetsFileWriter(stream))
        {
            assetsFile.Write(writer, 0);
        }

        return stream.ToArray();
    }

    private static byte[] WriteBundle(AssetBundleFile bundleFile)
    {
        // bundleFile.Write 会应用 DirectoryInfo.SetNewData 的替换，得到未压缩的 bundle。
        using var stream = new MemoryStream();
        using (var writer = new AssetsFileWriter(stream))
        {
            bundleFile.Write(writer, 0);
        }

        return stream.ToArray();
    }

    private static byte[] PackBundle(byte[] uncompressedBundle, AssetBundleCompressionType compression)
    {
        // Pack 直接从原 DataReader 读取，不会应用替换，因此先把已应用替换的未压缩 bundle 重新读入再 Pack。
        var bundleFile = new AssetBundleFile();
        bundleFile.Read(new AssetsFileReader(new MemoryStream(uncompressedBundle)));
        try
        {
            using var stream = new MemoryStream();
            using (var writer = new AssetsFileWriter(stream))
            {
                bundleFile.Pack(writer, compression, false, null);
            }

            return stream.ToArray();
        }
        finally
        {
            bundleFile.Close();
        }
    }

    private static int VerifyOutput(byte[] bundleBytes, long expectedScriptPathId)
    {
        var manager = new AssetsManager();
        try
        {
            var bundle = manager.LoadBundleFile(new MemoryStream(bundleBytes), "verify.ab", true);
            int hits = 0;
            var directoryInfos = bundle.file.BlockAndDirInfo.DirectoryInfos;
            for (var dirIndex = 0; dirIndex < directoryInfos.Count; dirIndex++)
            {
                if (!bundle.file.IsAssetsFile(dirIndex))
                {
                    continue;
                }

                var assetsInst = manager.LoadAssetsFileFromBundle(bundle, dirIndex, false);
                foreach (var info in assetsInst.file.GetAssetsOfType(AssetClassID.MonoBehaviour))
                {
                    var scriptPtr = manager.GetBaseField(assetsInst, info, AssetReadFlags.None)["m_Script"];
                    if (!scriptPtr.IsDummy && scriptPtr["m_PathID"].AsLong == expectedScriptPathId)
                    {
                        hits++;
                    }
                }
            }

            return hits;
        }
        finally
        {
            manager.UnloadAll(true);
        }
    }

    private static void WriteOutputFile(string outputPath, byte[] bytes)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // 先写临时文件再移动，避免输出与输入同路径时半路截断原文件。
        var tempPath = outputPath + ".tmp";
        File.WriteAllBytes(tempPath, bytes);
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        File.Move(tempPath, outputPath);
    }
}
