#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class SbSceneAssetBundleBuilder
{
    private const string MenuPath = "Tools/SbScene/Build AssetBundles From Folder...";
    private const string AssetBundleSourceRoot = "Assets/AssetBundle";
    private const string OutputRelativePath = "out/AssetBundles";

    [MenuItem(MenuPath)]
    private static void BuildFromFolder()
    {
        var selectedPath = EditorUtility.OpenFolderPanel("Select Asset Folder", Application.dataPath, string.Empty);
        if (string.IsNullOrEmpty(selectedPath))
        {
            return;
        }

        try
        {
            BuildFromFolder(selectedPath);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("SbScene AssetBundles", ex.Message, "OK");
        }
    }

    private static void BuildFromFolder(string selectedFolder)
    {
        var selectedFullPath = NormalizeFullPath(selectedFolder);
        var assetsFullPath = NormalizeFullPath(Application.dataPath);
        if (!IsInsideOrSameDirectory(selectedFullPath, assetsFullPath))
        {
            throw new InvalidOperationException("Select a folder inside the current Unity project's Assets directory.");
        }

        var selectedAssetPath = ToAssetPath(selectedFullPath, assetsFullPath);
        if (!IsInsideOrSameAssetPath(selectedAssetPath, AssetBundleSourceRoot))
        {
            throw new InvalidOperationException("Select a folder under Assets/AssetBundle so AssetBundle names stay relative to the game load root.");
        }

        var builds = CollectAssetBundleBuilds(selectedAssetPath, AssetBundleSourceRoot).ToArray();
        if (builds.Length == 0)
        {
            EditorUtility.DisplayDialog("SbScene AssetBundles", "No valid assets were found in the selected folder.", "OK");
            return;
        }

        EnsureScriptsCanBuild();

        var projectRoot = NormalizeFullPath(Path.GetDirectoryName(Application.dataPath) ?? ".");
        var outputPath = Path.Combine(projectRoot, OutputRelativePath);
        RecreateDirectory(outputPath);

        var manifest = BuildPipeline.BuildAssetBundles(
            outputPath,
            builds,
            BuildAssetBundleOptions.UncompressedAssetBundle,
            EditorUserBuildSettings.activeBuildTarget);

        if (manifest == null)
        {
            throw new InvalidOperationException(
                "AssetBundle build failed. If the Unity console shows script compiler errors, fix those C# errors first and run this menu again.");
        }

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(
            "SbScene AssetBundles",
            string.Format(
                "Built {0} AssetBundles.\nOutput: {1}\nTarget: {2}",
                builds.Length,
                outputPath,
                EditorUserBuildSettings.activeBuildTarget),
            "OK");
    }

    private static void EnsureScriptsCanBuild()
    {
        if (EditorApplication.isCompiling)
        {
            throw new InvalidOperationException("Unity is still compiling scripts. Wait for compilation to finish and run this menu again.");
        }

        if (HasScriptCompilationFailed())
        {
            throw new InvalidOperationException("Unity has script compiler errors. Fix the C# errors shown in the Console before building AssetBundles.");
        }
    }

    private static bool HasScriptCompilationFailed()
    {
        var property = typeof(EditorUtility).GetProperty(
            "scriptCompilationFailed",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (property == null || property.PropertyType != typeof(bool))
        {
            return false;
        }

        return (bool)property.GetValue(null, null);
    }

    private static IEnumerable<AssetBundleBuild> CollectAssetBundleBuilds(string selectedAssetPath, string bundleRootAssetPath)
    {
        var usedBundleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var assetPath in EnumerateAssetPaths(selectedAssetPath))
        {
            if (!IsValidMainAsset(assetPath))
            {
                continue;
            }

            var bundleName = CreateUniqueBundleName(CreateBundleName(bundleRootAssetPath, assetPath), usedBundleNames);
            yield return new AssetBundleBuild
            {
                assetBundleName = bundleName,
                assetNames = new[] { assetPath },
            };
        }
    }

    private static IEnumerable<string> EnumerateAssetPaths(string selectedAssetPath)
    {
        var selectedFullPath = ToAbsoluteAssetPath(selectedAssetPath);
        foreach (var filePath in Directory.EnumerateFiles(selectedFullPath, "*", SearchOption.AllDirectories))
        {
            var assetPath = ToAssetPath(NormalizeFullPath(filePath), NormalizeFullPath(Application.dataPath));
            if (ShouldSkipAssetPath(assetPath))
            {
                continue;
            }

            yield return assetPath;
        }
    }

    private static bool ShouldSkipAssetPath(string assetPath)
    {
        var extension = Path.GetExtension(assetPath);
        if (string.Equals(extension, ".meta", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var normalizedPath = NormalizeAssetPath(assetPath);
        return normalizedPath.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalizedPath.EndsWith("/Editor", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsValidMainAsset(string assetPath)
    {
        var mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
        if (mainAsset == null)
        {
            return false;
        }

        return !(mainAsset is MonoScript) && !(mainAsset is DefaultAsset);
    }

    private static string CreateBundleName(string bundleRootAssetPath, string assetPath)
    {
        var selectedPath = NormalizeAssetPath(bundleRootAssetPath).TrimEnd('/');
        var normalizedAssetPath = NormalizeAssetPath(assetPath);
        var relativePath = normalizedAssetPath.Substring(selectedPath.Length).TrimStart('/');
        var withoutExtension = Path.Combine(
            Path.GetDirectoryName(relativePath) ?? string.Empty,
            Path.GetFileNameWithoutExtension(relativePath));

        return SanitizeBundleName(NormalizeAssetPath(withoutExtension).ToLowerInvariant()) + ".ab";
    }

    private static string CreateUniqueBundleName(string bundleName, HashSet<string> usedBundleNames)
    {
        if (usedBundleNames.Add(bundleName))
        {
            return bundleName;
        }

        var extension = Path.GetExtension(bundleName);
        var nameWithoutExtension = bundleName.Substring(0, bundleName.Length - extension.Length);
        for (var index = 2; ; index++)
        {
            var candidate = string.Format("{0}_{1}{2}", nameWithoutExtension, index, extension);
            if (usedBundleNames.Add(candidate))
            {
                return candidate;
            }
        }
    }

    private static string SanitizeBundleName(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if ((ch >= 'a' && ch <= 'z') ||
                (ch >= '0' && ch <= '9') ||
                ch == '/' ||
                ch == '_' ||
                ch == '-' ||
                ch == '.')
            {
                builder.Append(ch);
            }
            else
            {
                builder.Append('_');
            }
        }

        return builder.ToString().Trim('/');
    }

    private static string ToAssetPath(string fullPath, string assetsFullPath)
    {
        var relativePath = fullPath.Length == assetsFullPath.Length
            ? string.Empty
            : fullPath.Substring(assetsFullPath.Length).TrimStart('/', '\\');
        return NormalizeAssetPath(Path.Combine("Assets", relativePath));
    }

    private static string ToAbsoluteAssetPath(string assetPath)
    {
        var projectRoot = NormalizeFullPath(Path.GetDirectoryName(Application.dataPath) ?? ".");
        return NormalizeFullPath(Path.Combine(projectRoot, assetPath));
    }

    private static bool IsInsideOrSameDirectory(string path, string directory)
    {
        return string.Equals(path, directory, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(directory.TrimEnd('/', '\\') + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(directory.TrimEnd('/', '\\') + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsInsideOrSameAssetPath(string path, string directory)
    {
        var normalizedPath = NormalizeAssetPath(path).TrimEnd('/');
        var normalizedDirectory = NormalizeAssetPath(directory).TrimEnd('/');
        return string.Equals(normalizedPath, normalizedDirectory, StringComparison.OrdinalIgnoreCase) ||
            normalizedPath.StartsWith(normalizedDirectory + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static void RecreateDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }

        Directory.CreateDirectory(path);
    }

    private static string NormalizeFullPath(string path)
    {
        return Path.GetFullPath(path).TrimEnd('/', '\\');
    }

    private static string NormalizeAssetPath(string path)
    {
        return path.Replace('\\', '/');
    }
}
#endif
