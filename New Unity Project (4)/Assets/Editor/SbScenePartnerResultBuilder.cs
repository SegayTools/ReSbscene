#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class SbScenePartnerResultBuilder
{
    private const string MenuPath = "Tools/SbScene/Build Partner(Result) Bundles";
    private const string PrefabDir = "Assets/AssetBundle/navichara/prefab";
    private const string PartnerAssetDir = "Assets/AssetBundle/partner";
    private const string PartnerBackAssetPath = "Assets/AssetBundle/misc/partner_back.png";
    private const string PartnerFrontAssetPath = "Assets/AssetBundle/misc/partner_front.png";
    private const string OutputRelativePath = "out/AssetBundles";
    private const int OutputSize = 512;
    private const int RenderScale = 2;
    private const float PortraitTargetHeadWidth = OutputSize * 0.48f;
    private const float PortraitTargetHeadHeight = OutputSize * 0.50f;
    private const float PortraitTargetUpperWidth = OutputSize * 0.92f;
    private const float PortraitTargetUpperHeight = OutputSize * 1.00f;
    private const float PortraitTargetHeadCenterY = OutputSize * 0.14f;
    private const float PortraitZoomFactor = 1.08f;
    private const float PortraitSafeMargin = 12f;
    private static readonly string[] HeadCenterNameParts =
    {
        "face",
        "eye",
        "eyebrow",
        "mouth",
        "nose",
        "head_l",
        "head_r",
        "front_hair",
        "side_hair",
        "hair_base",
    };

    private static readonly string[] LowerBodyNameParts =
    {
        "boot",
        "leg",
        "pants",
        "foot",
        "shoe",
        "shin",
        "skirt",
        "sune",
        "tights",
        "twintail",
        "tail",
    };

    private static readonly string[] PortraitNonFramingNameParts =
    {
        "hair_back",
    };
    private static readonly string[] PartnerHeadShotNameParts =
    {
        "head",
        "face",
        "eye",
        "eyebrow",
        "brow",
        "mouth",
        "mouse",
        "nose",
        "hair",
        "front",
        "top",
        "tragi",
        "ear",
        "headdress",
        "hat",
        "bonbon",
        "highlight",
    };
    private static readonly PartnerRenderSpec[] PartnerRenderSpecs =
    {
        new PartnerRenderSpec(
            "PartnerResult",
            "UI_PartnerResult",
            "partner/ui_partnerresult",
            OutputSize,
            RenderScale,
            PortraitTargetHeadWidth / (float)OutputSize,
            PortraitTargetHeadHeight / (float)OutputSize,
            PortraitTargetUpperWidth / (float)OutputSize,
            PortraitTargetUpperHeight / (float)OutputSize,
            PortraitTargetHeadCenterY / (float)OutputSize,
            PortraitZoomFactor,
            PortraitSafeMargin,
            true,
            PartnerRenderMode.Portrait),
        new PartnerRenderSpec(
            "Partner",
            "UI_Partner",
            "partner/ui_partner",
            128,
            RenderScale,
            PortraitTargetHeadWidth / (float)OutputSize,
            PortraitTargetHeadHeight / (float)OutputSize,
            PortraitTargetUpperWidth / (float)OutputSize,
            PortraitTargetUpperHeight / (float)OutputSize,
            PortraitTargetHeadCenterY / (float)OutputSize,
            1f,
            0f,
            false,
            PartnerRenderMode.HeadShot),
    };
    private static readonly Regex PrefabNamePattern = new Regex(
        @"^UI_Navichara_(\d{1,6})\.prefab$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    [MenuItem(MenuPath)]
    private static void BuildPartnerBundles()
    {
        BuildPartnerBundlesMenu();
    }

    private static void BuildPartnerBundlesMenu()
    {
        try
        {
            var result = BuildAll();
            var message = string.Format(
                CultureInfo.InvariantCulture,
                "Generated {0} bundle(s).\nFailed: {1}\nOutput: {2}",
                result.SuccessCount,
                result.Failures.Count,
                NormalizeFullPath(Path.Combine(ProjectRoot, OutputRelativePath, "partner")));
            if (result.Failures.Count > 0)
            {
                message += "\n\nFailures:\n" + string.Join("\n", result.Failures.ToArray());
            }

            EditorUtility.DisplayDialog("SbScene Partner", message, "OK");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("SbScene Partner", ex.Message, "OK");
        }
    }

    public static void BuildPartnerBundlesBatch()
    {
        var result = BuildAll();
        var outputPath = NormalizeFullPath(Path.Combine(ProjectRoot, OutputRelativePath, "partner"));
        Debug.Log(string.Format(
            CultureInfo.InvariantCulture,
            "Generated {0} bundle(s). Failed: {1}. Output: {2}",
            result.SuccessCount,
            result.Failures.Count,
            outputPath));
        foreach (var failure in result.Failures)
        {
            Debug.LogError(failure);
        }

        if (result.Failures.Count > 0)
        {
            throw new InvalidOperationException("Partner bundle build failed.");
        }
    }

    public static PartnerResultGenerationResult GeneratePartnerResultFromClip(string prefabAssetPath, PartnerResultClipRect clipRect)
    {
        EnsureScriptsCanBuild();
        if (!TryGetNavicharaPrefabInfo(prefabAssetPath, out var id, out var error))
        {
            throw new InvalidOperationException(error);
        }

        EnsureAssetFolder(PartnerAssetDir);
        var prefab = new PartnerPrefab(id, NormalizeAssetPath(prefabAssetPath));
        var spec = GetPartnerResultSpec();
        var pngAssetPath = RenderPartnerClipPng(prefab, clipRect, spec);
        AssetDatabase.Refresh();
        BuildAssetBundles(new[]
        {
            new GeneratedPartnerPng(prefab.Id, pngAssetPath, spec.BundleNamePrefix),
        });
        AssetDatabase.Refresh();

        return new PartnerResultGenerationResult(
            prefab.Id,
            pngAssetPath,
            NormalizeFullPath(Path.Combine(ProjectRoot, OutputRelativePath, FormatBundleName(spec.BundleNamePrefix, prefab.Id))));
    }

    public static PartnerClipGenerationSummary GeneratePartnerAndResultFromClips(
        string prefabAssetPath,
        PartnerResultClipRect partnerResultClipRect,
        PartnerResultClipRect partnerClipRect)
    {
        EnsureScriptsCanBuild();
        if (!TryGetNavicharaPrefabInfo(prefabAssetPath, out var id, out var error))
        {
            throw new InvalidOperationException(error);
        }

        EnsureAssetFolder(PartnerAssetDir);
        var prefab = new PartnerPrefab(id, NormalizeAssetPath(prefabAssetPath));
        var generatedPngs = new List<GeneratedPartnerPng>();
        var partnerResult = GenerateClipResource(prefab, partnerResultClipRect, GetPartnerResultSpec(), generatedPngs);
        var partner = GenerateClipResource(prefab, partnerClipRect, GetPartnerSpec(), generatedPngs);

        AssetDatabase.Refresh();
        if (generatedPngs.Count > 0)
        {
            BuildAssetBundles(generatedPngs);
            AssetDatabase.Refresh();
        }

        return new PartnerClipGenerationSummary(prefab.Id, partnerResult, partner);
    }

    public static PartnerResultPreview RenderPartnerResultPreview(string prefabAssetPath, int previewSize)
    {
        if (!TryGetNavicharaPrefabInfo(prefabAssetPath, out _, out var error))
        {
            throw new InvalidOperationException(error);
        }

        var source = AssetDatabase.LoadAssetAtPath<GameObject>(prefabAssetPath);
        if (source == null)
        {
            throw new InvalidOperationException("Prefab could not be loaded.");
        }

        previewSize = Mathf.Max(64, previewSize);
        var sceneRoot = new GameObject("SbScenePartnerResultPreviewRoot");
        sceneRoot.hideFlags = HideFlags.HideAndDontSave;
        var cameraObject = new GameObject("SbScenePartnerResultPreviewCamera");
        cameraObject.hideFlags = HideFlags.HideAndDontSave;
        var canvasObject = new GameObject("SbScenePartnerResultPreviewCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.hideFlags = HideFlags.HideAndDontSave;
        var renderTexture = new RenderTexture(previewSize, previewSize, 24, RenderTextureFormat.ARGB32);
        var previousActive = RenderTexture.active;
        Camera camera = null;
        try
        {
            cameraObject.transform.SetParent(sceneRoot.transform, false);
            camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.orthographic = true;
            camera.nearClipPlane = -100f;
            camera.farClipPlane = 100f;
            camera.targetTexture = renderTexture;

            canvasObject.transform.SetParent(sceneRoot.transform, false);
            ConfigureWorldCanvas(canvasObject, camera, previewSize);

            var instance = InstantiatePreviewPrefab(source, canvasObject.transform);
            var frame = CalculatePortraitFrame(instance);
            if (!frame.HasValue || frame.Value.Content.size.x <= 0f || frame.Value.Content.size.y <= 0f)
            {
                throw new InvalidOperationException("No visible UI bounds could be measured.");
            }

            var content = frame.Value.Content;
            var frameSize = Mathf.Max(content.size.x, content.size.y);
            if (float.IsNaN(frameSize) || float.IsInfinity(frameSize) || frameSize <= 0.0001f)
            {
                frameSize = OutputSize;
            }

            frameSize *= 1.12f;
            var frameCenter = content.center;
            camera.orthographicSize = frameSize / 2f;
            camera.transform.position = new Vector3(frameCenter.x, frameCenter.y, -10f);
            Canvas.ForceUpdateCanvases();

            var previewSpec = new PartnerRenderSpec(
                "PartnerResultPreview",
                "UI_PartnerResult",
                GetPartnerResultSpec().BundleNamePrefix,
                previewSize,
                1,
                1f,
                1f,
                1f,
                1f,
                0f,
                1f,
                0f,
                false,
                PartnerRenderMode.Portrait);
            var texture = new Texture2D(previewSize, previewSize, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.SetPixels32(RenderStraightAlphaPixels(camera, renderTexture, previewSpec));
            texture.Apply(false, false);

            var frameRect = new Rect(frameCenter.x - frameSize / 2f, frameCenter.y - frameSize / 2f, frameSize, frameSize);
            return new PartnerResultPreview(texture, frameRect, content);
        }
        finally
        {
            if (camera != null)
            {
                camera.targetTexture = null;
            }

            RenderTexture.active = previousActive;
            renderTexture.Release();
            UnityEngine.Object.DestroyImmediate(renderTexture);
            UnityEngine.Object.DestroyImmediate(sceneRoot);
        }
    }

    public static bool TryGetNavicharaPrefabInfo(string assetPath, out int id, out string error)
    {
        id = 0;
        error = null;
        if (string.IsNullOrEmpty(assetPath))
        {
            error = "Select a prefab.";
            return false;
        }

        var normalized = NormalizeAssetPath(assetPath);
        var normalizedDir = NormalizeAssetPath(PrefabDir).TrimEnd('/') + "/";
        if (!normalized.StartsWith(normalizedDir, StringComparison.OrdinalIgnoreCase))
        {
            error = "Prefab must be under " + PrefabDir + ".";
            return false;
        }

        var match = PrefabNamePattern.Match(Path.GetFileName(normalized));
        if (!match.Success || !int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out id))
        {
            error = "Prefab name must match UI_Navichara_{id}.prefab.";
            return false;
        }

        return true;
    }

    private static BuildSummary BuildAll()
    {
        EnsureScriptsCanBuild();
        EnsureAssetFolder(PartnerAssetDir);

        var prefabs = EnumerateSourcePrefabs().ToArray();
        if (prefabs.Length == 0)
        {
            return new BuildSummary
            {
                SuccessCount = 0,
            };
        }

        var generatedPngs = new List<GeneratedPartnerPng>();
        var failures = new List<string>();
        foreach (var prefab in prefabs)
        {
            foreach (var spec in PartnerRenderSpecs)
            {
                try
                {
                    var pngAssetPath = RenderPartnerPng(prefab, spec);
                    generatedPngs.Add(new GeneratedPartnerPng(prefab.Id, pngAssetPath, spec.BundleNamePrefix));
                }
                catch (Exception ex)
                {
                    failures.Add(string.Format(CultureInfo.InvariantCulture, "{0} [{1}]: {2}", prefab.AssetPath, spec.Label, ex.Message));
                }
            }
        }

        AssetDatabase.Refresh();

        BuildAssetBundles(generatedPngs);

        AssetDatabase.Refresh();
        return new BuildSummary
        {
            SuccessCount = generatedPngs.Count,
            Failures = failures,
        };
    }

    private static IEnumerable<PartnerPrefab> EnumerateSourcePrefabs()
    {
        var absoluteDir = ToAbsoluteAssetPath(PrefabDir);
        if (!Directory.Exists(absoluteDir))
        {
            yield break;
        }

        foreach (var filePath in Directory.EnumerateFiles(absoluteDir, "UI_Navichara_*.prefab", SearchOption.TopDirectoryOnly)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var fileName = Path.GetFileName(filePath);
            var match = PrefabNamePattern.Match(fileName);
            if (!match.Success)
            {
                continue;
            }

            if (!int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
            {
                continue;
            }

            yield return new PartnerPrefab(id, ToAssetPath(NormalizeFullPath(filePath), NormalizeFullPath(Application.dataPath)));
        }
    }

    private static PartnerRenderSpec GetPartnerResultSpec()
    {
        return PartnerRenderSpecs[0];
    }

    private static PartnerRenderSpec GetPartnerSpec()
    {
        return PartnerRenderSpecs[1];
    }

    private static PartnerClipGenerationItemResult GenerateClipResource(
        PartnerPrefab prefab,
        PartnerResultClipRect clipRect,
        PartnerRenderSpec spec,
        List<GeneratedPartnerPng> generatedPngs)
    {
        try
        {
            var pngAssetPath = RenderPartnerClipPng(prefab, clipRect, spec);
            generatedPngs.Add(new GeneratedPartnerPng(prefab.Id, pngAssetPath, spec.BundleNamePrefix));
            return PartnerClipGenerationItemResult.CreateSuccess(
                spec.Label,
                prefab.Id,
                pngAssetPath,
                NormalizeFullPath(Path.Combine(ProjectRoot, OutputRelativePath, FormatBundleName(spec.BundleNamePrefix, prefab.Id))));
        }
        catch (Exception ex)
        {
            return PartnerClipGenerationItemResult.CreateFailure(spec.Label, prefab.Id, ex.Message);
        }
    }

    private static void BuildAssetBundles(IEnumerable<GeneratedPartnerPng> generatedPngs)
    {
        var builds = generatedPngs
            .Select(item => new AssetBundleBuild
            {
                assetBundleName = FormatBundleName(item.BundleNamePrefix, item.Id),
                assetNames = new[] { item.AssetPath },
            })
            .ToArray();
        if (builds.Length == 0)
        {
            return;
        }

        var outputPath = NormalizeFullPath(Path.Combine(ProjectRoot, OutputRelativePath));
        Directory.CreateDirectory(outputPath);
        if (BuildPipeline.BuildAssetBundles(
            outputPath,
            builds,
            BuildAssetBundleOptions.UncompressedAssetBundle,
            EditorUserBuildSettings.activeBuildTarget) == null)
        {
            throw new InvalidOperationException(
                "AssetBundle build failed. If the Unity console shows script compiler errors, fix those C# errors first and run this menu again.");
        }
    }

    private static string FormatBundleName(string bundleNamePrefix, int id)
    {
        return string.Format(CultureInfo.InvariantCulture, "{0}_{1:D6}.ab", bundleNamePrefix, id);
    }

    private static string RenderPartnerPng(PartnerPrefab prefab, PartnerRenderSpec spec)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(prefab.AssetPath);
        if (source == null)
        {
            throw new InvalidOperationException("Prefab could not be loaded.");
        }

        var pngAssetPath = string.Format(
            CultureInfo.InvariantCulture,
            "{0}/{1}_{2:D6}.png",
            PartnerAssetDir,
            spec.AssetNamePrefix,
            prefab.Id);
        var pngAbsolutePath = ToAbsoluteAssetPath(pngAssetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(pngAbsolutePath) ?? ".");

        var sceneRoot = new GameObject("SbScene" + spec.Label + "RenderRoot");
        sceneRoot.hideFlags = HideFlags.HideAndDontSave;
        var cameraObject = new GameObject("SbScene" + spec.Label + "Camera");
        cameraObject.hideFlags = HideFlags.HideAndDontSave;
        var canvasObject = new GameObject("SbScene" + spec.Label + "Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.hideFlags = HideFlags.HideAndDontSave;
        var renderTexture = new RenderTexture(spec.RenderSize, spec.RenderSize, 24, RenderTextureFormat.ARGB32);
        var previousActive = RenderTexture.active;
        Camera camera = null;
        try
        {
            cameraObject.transform.SetParent(sceneRoot.transform, false);
            camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.orthographic = true;
            camera.orthographicSize = spec.OutputSize / 2f;
            camera.nearClipPlane = -100f;
            camera.farClipPlane = 100f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.targetTexture = renderTexture;

            canvasObject.transform.SetParent(sceneRoot.transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            canvas.pixelPerfect = false;
            canvasObject.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            var canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.anchorMin = new Vector2(0.5f, 0.5f);
            canvasRect.anchorMax = new Vector2(0.5f, 0.5f);
            canvasRect.pivot = new Vector2(0.5f, 0.5f);
            canvasRect.sizeDelta = new Vector2(spec.OutputSize, spec.OutputSize);
            canvasRect.anchoredPosition = Vector2.zero;
            canvasRect.localPosition = Vector3.zero;
            canvasRect.localRotation = Quaternion.identity;
            canvasRect.localScale = Vector3.one;

            var instance = InstantiatePreviewPrefab(source, canvasObject.transform);

            var rootRect = instance.GetComponent<RectTransform>();
            if (spec.RenderMode == PartnerRenderMode.HeadShot)
            {
                var headShotBounds = CalculatePartnerHeadShotBounds(instance);
                if (!headShotBounds.HasValue || headShotBounds.Value.size.x <= 0f || headShotBounds.Value.size.y <= 0f)
                {
                    throw new InvalidOperationException("No visible Partner head nodes could be measured.");
                }

                FitToHeadShotFrame(rootRect, headShotBounds.Value, spec);
            }
            else
            {
                var portraitFrame = CalculatePortraitFrame(instance);
                if (!portraitFrame.HasValue || portraitFrame.Value.Content.size.x <= 0f || portraitFrame.Value.Content.size.y <= 0f)
                {
                    throw new InvalidOperationException("No visible UI bounds could be measured.");
                }

                FitToPortraitFrame(rootRect, portraitFrame.Value, spec);
            }

            Canvas.ForceUpdateCanvases();

            var texture = new Texture2D(spec.OutputSize, spec.OutputSize, TextureFormat.RGBA32, false);
            try
            {
                var pixels = RenderStraightAlphaPixels(camera, renderTexture, spec);
                if (spec.RenderMode == PartnerRenderMode.HeadShot)
                {
                    pixels = CompositePartnerFramePixels(pixels, spec);
                }

                texture.SetPixels32(pixels);
                texture.Apply(false, false);
                File.WriteAllBytes(pngAbsolutePath, texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
        finally
        {
            if (camera != null)
            {
                camera.targetTexture = null;
            }

            RenderTexture.active = previousActive;
            renderTexture.Release();
            UnityEngine.Object.DestroyImmediate(renderTexture);
            UnityEngine.Object.DestroyImmediate(sceneRoot);
        }

        AssetDatabase.ImportAsset(pngAssetPath, ImportAssetOptions.ForceUpdate);
        ConfigurePartnerTextureImporter(pngAssetPath, spec);
        return pngAssetPath;
    }

    private static string RenderPartnerClipPng(PartnerPrefab prefab, PartnerResultClipRect clipRect, PartnerRenderSpec spec)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(prefab.AssetPath);
        if (source == null)
        {
            throw new InvalidOperationException("Prefab could not be loaded.");
        }

        var pngAssetPath = string.Format(
            CultureInfo.InvariantCulture,
            "{0}/{1}_{2:D6}.png",
            PartnerAssetDir,
            spec.AssetNamePrefix,
            prefab.Id);
        var pngAbsolutePath = ToAbsoluteAssetPath(pngAssetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(pngAbsolutePath) ?? ".");

        var sceneRoot = new GameObject("SbScene" + spec.Label + "ClipRenderRoot");
        sceneRoot.hideFlags = HideFlags.HideAndDontSave;
        var cameraObject = new GameObject("SbScene" + spec.Label + "ClipCamera");
        cameraObject.hideFlags = HideFlags.HideAndDontSave;
        var canvasObject = new GameObject("SbScene" + spec.Label + "ClipCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.hideFlags = HideFlags.HideAndDontSave;
        var renderTexture = new RenderTexture(spec.RenderSize, spec.RenderSize, 24, RenderTextureFormat.ARGB32);
        var previousActive = RenderTexture.active;
        Camera camera = null;
        try
        {
            cameraObject.transform.SetParent(sceneRoot.transform, false);
            camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.orthographic = true;
            camera.orthographicSize = clipRect.Size / 2f;
            camera.nearClipPlane = -100f;
            camera.farClipPlane = 100f;
            camera.transform.position = new Vector3(clipRect.Center.x, clipRect.Center.y, -10f);
            camera.targetTexture = renderTexture;

            canvasObject.transform.SetParent(sceneRoot.transform, false);
            ConfigureWorldCanvas(canvasObject, camera, OutputSize);
            InstantiatePreviewPrefab(source, canvasObject.transform);
            Canvas.ForceUpdateCanvases();

            var texture = new Texture2D(spec.OutputSize, spec.OutputSize, TextureFormat.RGBA32, false);
            try
            {
                texture.filterMode = FilterMode.Bilinear;
                var pixels = RenderStraightAlphaPixels(camera, renderTexture, spec);
                if (spec.RenderMode == PartnerRenderMode.HeadShot)
                {
                    pixels = CompositePartnerFramePixels(pixels, spec);
                }

                texture.SetPixels32(pixels);
                texture.Apply(false, false);
                File.WriteAllBytes(pngAbsolutePath, texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
        finally
        {
            if (camera != null)
            {
                camera.targetTexture = null;
            }

            RenderTexture.active = previousActive;
            renderTexture.Release();
            UnityEngine.Object.DestroyImmediate(renderTexture);
            UnityEngine.Object.DestroyImmediate(sceneRoot);
        }

        AssetDatabase.ImportAsset(pngAssetPath, ImportAssetOptions.ForceUpdate);
        ConfigurePartnerTextureImporter(pngAssetPath, spec);
        return pngAssetPath;
    }

    private static GameObject InstantiatePreviewPrefab(GameObject source, Transform parent)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        if (instance == null)
        {
            throw new InvalidOperationException("Prefab instance could not be created.");
        }

        // Match the prefab as it appears when dropped into the Scene, before any default animation sampling.
        instance.SetActive(false);
        instance.hideFlags = HideFlags.HideAndDontSave;
        SetHideFlagsRecursively(instance, HideFlags.HideAndDontSave);
        instance.transform.SetParent(parent, false);
        DisableAnimator(instance);
        RestorePrefabStaticState(instance, source);
        DisableAnimator(instance);
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(instance.GetComponent<RectTransform>());
        Canvas.ForceUpdateCanvases();
        return instance;
    }

    private static void RestorePrefabStaticState(GameObject instance, GameObject source)
    {
        var sourceByPath = BuildTransformPathMap(source.transform);
        foreach (var targetTransform in instance.GetComponentsInChildren<Transform>(true))
        {
            var sourceTransform = ResolveSourceTransform(instance.transform, source.transform, sourceByPath, targetTransform);
            if (sourceTransform == null)
            {
                continue;
            }

            RestoreGameObjectStaticState(sourceTransform.gameObject, targetTransform.gameObject);
            RestoreTransformStaticState(sourceTransform, targetTransform);
            RestoreSerializedComponentArray<CanvasGroup>(sourceTransform, targetTransform);
            RestoreSerializedComponentArray<Graphic>(sourceTransform, targetTransform);
        }
    }

    private static Dictionary<string, Transform> BuildTransformPathMap(Transform root)
    {
        var map = new Dictionary<string, Transform>(StringComparer.Ordinal);
        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
        {
            var path = GetTransformPath(root, transform);
            if (!map.ContainsKey(path))
            {
                map.Add(path, transform);
            }
        }

        return map;
    }

    private static Transform ResolveSourceTransform(
        Transform instanceRoot,
        Transform sourceRoot,
        Dictionary<string, Transform> sourceByPath,
        Transform targetTransform)
    {
        var corresponding = PrefabUtility.GetCorrespondingObjectFromSource(targetTransform) as Transform;
        if (corresponding != null && (corresponding == sourceRoot || corresponding.IsChildOf(sourceRoot)))
        {
            return corresponding;
        }

        var path = GetTransformPath(instanceRoot, targetTransform);
        return sourceByPath.TryGetValue(path, out var sourceTransform) ? sourceTransform : null;
    }

    private static string GetTransformPath(Transform root, Transform transform)
    {
        if (root == transform)
        {
            return string.Empty;
        }

        var names = new Stack<string>();
        var current = transform;
        while (current != null && current != root)
        {
            names.Push(current.name);
            current = current.parent;
        }

        return current == root ? string.Join("/", names.ToArray()) : transform.name;
    }

    private static void RestoreGameObjectStaticState(GameObject source, GameObject target)
    {
        target.layer = source.layer;
        target.SetActive(source.activeSelf);
    }

    private static void RestoreTransformStaticState(Transform source, Transform target)
    {
        target.localPosition = source.localPosition;
        target.localRotation = source.localRotation;
        target.localScale = source.localScale;

        var sourceRect = source as RectTransform;
        var targetRect = target as RectTransform;
        if (sourceRect == null || targetRect == null)
        {
            return;
        }

        targetRect.anchorMin = sourceRect.anchorMin;
        targetRect.anchorMax = sourceRect.anchorMax;
        targetRect.anchoredPosition = sourceRect.anchoredPosition;
        targetRect.sizeDelta = sourceRect.sizeDelta;
        targetRect.pivot = sourceRect.pivot;
    }

    private static void RestoreSerializedComponentArray<T>(Transform source, Transform target) where T : Component
    {
        var sourceComponents = source.GetComponents<T>();
        var targetComponents = target.GetComponents<T>();
        var count = Mathf.Min(sourceComponents.Length, targetComponents.Length);
        for (var index = 0; index < count; index++)
        {
            var sourceComponent = sourceComponents[index];
            var targetComponent = targetComponents[index];
            if (sourceComponent == null || targetComponent == null || sourceComponent.GetType() != targetComponent.GetType())
            {
                continue;
            }

            EditorUtility.CopySerialized(sourceComponent, targetComponent);
        }
    }

    private static void ConfigureWorldCanvas(GameObject canvasObject, Camera camera, int size)
    {
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = camera;
        canvas.pixelPerfect = false;
        canvasObject.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        var canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.anchorMin = new Vector2(0.5f, 0.5f);
        canvasRect.anchorMax = new Vector2(0.5f, 0.5f);
        canvasRect.pivot = new Vector2(0.5f, 0.5f);
        canvasRect.sizeDelta = new Vector2(size, size);
        canvasRect.anchoredPosition = Vector2.zero;
        canvasRect.localPosition = Vector3.zero;
        canvasRect.localRotation = Quaternion.identity;
        canvasRect.localScale = Vector3.one;
    }

    private static void DisableAnimator(GameObject root)
    {
        foreach (var animator in root.GetComponentsInChildren<Animator>(true))
        {
            animator.enabled = false;
        }
    }

    private static void SampleDefaultFrame(GameObject root)
    {
        var navigationCharacter = root.GetComponent<NavigationCharacter>() ?? root.GetComponentInChildren<NavigationCharacter>(true);
        if (navigationCharacter == null || navigationCharacter.Default == null)
        {
            throw new InvalidOperationException("NavigationCharacter default clip could not be resolved.");
        }

        var defaultClip = navigationCharacter.Default;
        PlayDefaultAnimatorState(navigationCharacter);
        ApplyClipFrame(root, defaultClip, 0f);
        DisableAnimator(root);
    }

    private static void ApplyClipFrame(GameObject root, AnimationClip clip, float time)
    {
        var activeBindings = new List<KeyValuePair<EditorCurveBinding, float>>();
        foreach (var binding in AnimationUtility.GetCurveBindings(clip))
        {
            var curve = AnimationUtility.GetEditorCurve(clip, binding);
            if (curve == null)
            {
                continue;
            }

            var value = curve.Evaluate(time);
            if (binding.propertyName == "m_IsActive")
            {
                activeBindings.Add(new KeyValuePair<EditorCurveBinding, float>(binding, value));
            }
            else
            {
                ApplyFloatBinding(root, binding, value);
            }
        }

        foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
        {
            var keyframes = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            if (keyframes == null || keyframes.Length == 0)
            {
                continue;
            }

            ApplyObjectBinding(root, binding, EvaluateObjectReference(keyframes, time));
        }

        activeBindings.Sort((left, right) =>
            GetPathDepth(right.Key.path).CompareTo(GetPathDepth(left.Key.path)));
        foreach (var binding in activeBindings)
        {
            ApplyFloatBinding(root, binding.Key, binding.Value);
        }
    }

    private static int GetPathDepth(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return 0;
        }

        var depth = 1;
        for (var index = 0; index < path.Length; index++)
        {
            if (path[index] == '/')
            {
                depth++;
            }
        }

        return depth;
    }

    private static void ApplyFloatBinding(GameObject root, EditorCurveBinding binding, float value)
    {
        var target = FindBoundTransform(root, binding.path);
        if (target == null)
        {
            return;
        }

        switch (binding.propertyName)
        {
            case "m_IsActive":
                target.gameObject.SetActive(value >= 0.5f);
                return;
            case "m_AnchoredPosition.x":
                SetAnchoredPosition(target, value, null);
                return;
            case "m_AnchoredPosition.y":
                SetAnchoredPosition(target, null, value);
                return;
            case "m_SizeDelta.x":
                SetSizeDelta(target, value, null);
                return;
            case "m_SizeDelta.y":
                SetSizeDelta(target, null, value);
                return;
            case "m_LocalScale.x":
            case "localScale.x":
                SetLocalScale(target, value, null, null);
                return;
            case "m_LocalScale.y":
            case "localScale.y":
                SetLocalScale(target, null, value, null);
                return;
            case "m_LocalScale.z":
            case "localScale.z":
                SetLocalScale(target, null, null, value);
                return;
            case "localEulerAnglesRaw.z":
                SetLocalEulerZ(target, value);
                return;
            case "m_Alpha":
                SetCanvasGroupAlpha(target, value);
                return;
            case "m_Color.r":
                SetGraphicColor(target, value, null, null, null);
                return;
            case "m_Color.g":
                SetGraphicColor(target, null, value, null, null);
                return;
            case "m_Color.b":
                SetGraphicColor(target, null, null, value, null);
                return;
            case "m_Color.a":
                SetGraphicColor(target, null, null, null, value);
                return;
        }

        ApplySerializedFloatBinding(target, binding, value);
    }

    private static void ApplyObjectBinding(GameObject root, EditorCurveBinding binding, UnityEngine.Object value)
    {
        var target = FindBoundTransform(root, binding.path);
        if (target == null)
        {
            return;
        }

        if (binding.propertyName == "m_Sprite")
        {
            var image = target.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = value as Sprite;
            }

            return;
        }

        ApplySerializedObjectBinding(target, binding, value);
    }

    private static Transform FindBoundTransform(GameObject root, string path)
    {
        if (root == null)
        {
            return null;
        }

        return string.IsNullOrEmpty(path) ? root.transform : root.transform.Find(path);
    }

    private static UnityEngine.Object EvaluateObjectReference(ObjectReferenceKeyframe[] keyframes, float time)
    {
        var selected = keyframes[0].value;
        for (var index = 0; index < keyframes.Length; index++)
        {
            if (keyframes[index].time > time)
            {
                break;
            }

            selected = keyframes[index].value;
        }

        return selected;
    }

    private static void SetAnchoredPosition(Transform target, float? x, float? y)
    {
        var rect = target as RectTransform;
        if (rect == null)
        {
            return;
        }

        var position = rect.anchoredPosition;
        rect.anchoredPosition = new Vector2(x ?? position.x, y ?? position.y);
    }

    private static void SetSizeDelta(Transform target, float? x, float? y)
    {
        var rect = target as RectTransform;
        if (rect == null)
        {
            return;
        }

        var size = rect.sizeDelta;
        rect.sizeDelta = new Vector2(x ?? size.x, y ?? size.y);
    }

    private static void SetLocalScale(Transform target, float? x, float? y, float? z)
    {
        var scale = target.localScale;
        target.localScale = new Vector3(x ?? scale.x, y ?? scale.y, z ?? scale.z);
    }

    private static void SetLocalEulerZ(Transform target, float z)
    {
        var euler = target.localEulerAngles;
        euler.z = z;
        target.localEulerAngles = euler;
    }

    private static void SetCanvasGroupAlpha(Transform target, float alpha)
    {
        var group = target.GetComponent<CanvasGroup>();
        if (group != null)
        {
            group.alpha = alpha;
        }
    }

    private static void SetGraphicColor(Transform target, float? r, float? g, float? b, float? a)
    {
        var graphic = target.GetComponent<Graphic>();
        if (graphic == null)
        {
            return;
        }

        var color = graphic.color;
        graphic.color = new Color(r ?? color.r, g ?? color.g, b ?? color.b, a ?? color.a);
    }

    private static void ApplySerializedFloatBinding(Transform target, EditorCurveBinding binding, float value)
    {
        var component = GetBoundComponent(target, binding.type);
        if (component == null)
        {
            return;
        }

        var serialized = new SerializedObject(component);
        var property = serialized.FindProperty(binding.propertyName);
        if (property == null)
        {
            return;
        }

        if (property.propertyType == SerializedPropertyType.Float)
        {
            property.floatValue = value;
        }
        else if (property.propertyType == SerializedPropertyType.Integer)
        {
            property.intValue = Mathf.RoundToInt(value);
        }
        else if (property.propertyType == SerializedPropertyType.Boolean)
        {
            property.boolValue = value >= 0.5f;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ApplySerializedObjectBinding(Transform target, EditorCurveBinding binding, UnityEngine.Object value)
    {
        var component = GetBoundComponent(target, binding.type);
        if (component == null)
        {
            return;
        }

        var serialized = new SerializedObject(component);
        var property = serialized.FindProperty(binding.propertyName);
        if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
        {
            return;
        }

        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static UnityEngine.Object GetBoundComponent(Transform target, Type bindingType)
    {
        if (bindingType == typeof(GameObject))
        {
            return target.gameObject;
        }

        if (bindingType == typeof(Transform) || bindingType == typeof(RectTransform))
        {
            return target;
        }

        return target.GetComponent(bindingType);
    }

    private static void PlayDefaultAnimatorState(NavigationCharacter navigationCharacter)
    {
        if (navigationCharacter.NaviAnimator == null)
        {
            return;
        }

        foreach (var animator in navigationCharacter.NaviAnimator)
        {
            if (animator == null)
            {
                continue;
            }

            animator.enabled = true;
            animator.Play(navigationCharacter.HashDefault, 0, 0f);
            animator.Update(0f);
        }
    }

    private static Color32[] RenderStraightAlphaPixels(Camera camera, RenderTexture renderTexture, PartnerRenderSpec spec)
    {
        var blackPixels = RenderPixels(camera, renderTexture, Color.black, spec);
        var whitePixels = RenderPixels(camera, renderTexture, Color.white, spec);
        var output = new Color32[spec.OutputSize * spec.OutputSize];
        for (var outputY = 0; outputY < spec.OutputSize; outputY++)
        {
            for (var outputX = 0; outputX < spec.OutputSize; outputX++)
            {
                var red = 0f;
                var green = 0f;
                var blue = 0f;
                var alpha = 0f;
                for (var sampleY = 0; sampleY < spec.RenderScale; sampleY++)
                {
                    for (var sampleX = 0; sampleX < spec.RenderScale; sampleX++)
                    {
                        var sampleIndex = ((outputY * spec.RenderScale + sampleY) * spec.RenderSize) + (outputX * spec.RenderScale + sampleX);
                        AccumulateStraightAlpha(blackPixels[sampleIndex], whitePixels[sampleIndex], ref red, ref green, ref blue, ref alpha);
                    }
                }

                var sampleCount = spec.RenderScale * spec.RenderScale;
                output[outputY * spec.OutputSize + outputX] = new Color32(
                    ClampByte(Mathf.RoundToInt(red / sampleCount)),
                    ClampByte(Mathf.RoundToInt(green / sampleCount)),
                    ClampByte(Mathf.RoundToInt(blue / sampleCount)),
                    ClampByte(Mathf.RoundToInt(alpha / sampleCount)));
            }
        }

        return output;
    }

    private static Color32[] CompositePartnerFramePixels(Color32[] characterPixels, PartnerRenderSpec spec)
    {
        var backPixels = LoadResizedPngPixels(PartnerBackAssetPath, spec.OutputSize);
        var frontPixels = LoadResizedPngPixels(PartnerFrontAssetPath, spec.OutputSize);
        var output = new Color32[spec.OutputSize * spec.OutputSize];
        for (var index = 0; index < output.Length; index++)
        {
            var pixel = backPixels[index];
            pixel = AlphaOver(characterPixels[index], pixel);
            pixel = AlphaOver(frontPixels[index], pixel);
            output[index] = pixel;
        }

        return output;
    }

    private static Color32[] LoadResizedPngPixels(string assetPath, int outputSize)
    {
        var absolutePath = ToAbsoluteAssetPath(assetPath);
        if (!File.Exists(absolutePath))
        {
            throw new FileNotFoundException("Partner frame PNG was not found.", absolutePath);
        }

        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!texture.LoadImage(File.ReadAllBytes(absolutePath)))
            {
                throw new InvalidOperationException("Partner frame PNG could not be loaded: " + assetPath);
            }

            var sourcePixels = texture.GetPixels32();
            return ResizePixelsBilinear(sourcePixels, texture.width, texture.height, outputSize, outputSize);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    private static Color32[] ResizePixelsBilinear(Color32[] sourcePixels, int sourceWidth, int sourceHeight, int targetWidth, int targetHeight)
    {
        if (sourceWidth == targetWidth && sourceHeight == targetHeight)
        {
            var copy = new Color32[sourcePixels.Length];
            Array.Copy(sourcePixels, copy, sourcePixels.Length);
            return copy;
        }

        var output = new Color32[targetWidth * targetHeight];
        var scaleX = sourceWidth / (float)targetWidth;
        var scaleY = sourceHeight / (float)targetHeight;
        for (var y = 0; y < targetHeight; y++)
        {
            var sourceY = Mathf.Clamp((y + 0.5f) * scaleY - 0.5f, 0f, sourceHeight - 1f);
            var y0 = Mathf.FloorToInt(sourceY);
            var y1 = Mathf.Min(y0 + 1, sourceHeight - 1);
            var yBlend = sourceY - y0;
            for (var x = 0; x < targetWidth; x++)
            {
                var sourceX = Mathf.Clamp((x + 0.5f) * scaleX - 0.5f, 0f, sourceWidth - 1f);
                var x0 = Mathf.FloorToInt(sourceX);
                var x1 = Mathf.Min(x0 + 1, sourceWidth - 1);
                var xBlend = sourceX - x0;
                var top = LerpColor32(sourcePixels[y0 * sourceWidth + x0], sourcePixels[y0 * sourceWidth + x1], xBlend);
                var bottom = LerpColor32(sourcePixels[y1 * sourceWidth + x0], sourcePixels[y1 * sourceWidth + x1], xBlend);
                output[y * targetWidth + x] = LerpColor32(top, bottom, yBlend);
            }
        }

        return output;
    }

    private static Color32 LerpColor32(Color32 left, Color32 right, float t)
    {
        return new Color32(
            ClampByte(Mathf.RoundToInt(Mathf.Lerp(left.r, right.r, t))),
            ClampByte(Mathf.RoundToInt(Mathf.Lerp(left.g, right.g, t))),
            ClampByte(Mathf.RoundToInt(Mathf.Lerp(left.b, right.b, t))),
            ClampByte(Mathf.RoundToInt(Mathf.Lerp(left.a, right.a, t))));
    }

    private static Color32 AlphaOver(Color32 foreground, Color32 background)
    {
        var foregroundAlpha = foreground.a / 255f;
        if (foregroundAlpha <= 0.0001f)
        {
            return background;
        }

        var backgroundAlpha = background.a / 255f;
        var outputAlpha = foregroundAlpha + backgroundAlpha * (1f - foregroundAlpha);
        if (outputAlpha <= 0.0001f)
        {
            return new Color32(0, 0, 0, 0);
        }

        var foregroundWeight = foregroundAlpha / outputAlpha;
        var backgroundWeight = backgroundAlpha * (1f - foregroundAlpha) / outputAlpha;
        return new Color32(
            ClampByte(Mathf.RoundToInt(foreground.r * foregroundWeight + background.r * backgroundWeight)),
            ClampByte(Mathf.RoundToInt(foreground.g * foregroundWeight + background.g * backgroundWeight)),
            ClampByte(Mathf.RoundToInt(foreground.b * foregroundWeight + background.b * backgroundWeight)),
            ClampByte(Mathf.RoundToInt(outputAlpha * 255f)));
    }

    private static Color32[] RenderPixels(Camera camera, RenderTexture renderTexture, Color backgroundColor, PartnerRenderSpec spec)
    {
        camera.backgroundColor = backgroundColor;
        RenderTexture.active = renderTexture;
        GL.Clear(true, true, backgroundColor);
        camera.Render();

        var texture = new Texture2D(spec.RenderSize, spec.RenderSize, TextureFormat.RGBA32, false);
        try
        {
            texture.ReadPixels(new Rect(0, 0, spec.RenderSize, spec.RenderSize), 0, 0);
            texture.Apply(false, false);
            return texture.GetPixels32();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    private static void AccumulateStraightAlpha(
        Color32 blackPixel,
        Color32 whitePixel,
        ref float red,
        ref float green,
        ref float blue,
        ref float alpha)
    {
        var alphaFloat = Mathf.Max(
            1f - (whitePixel.r - blackPixel.r) / 255f,
            1f - (whitePixel.g - blackPixel.g) / 255f,
            1f - (whitePixel.b - blackPixel.b) / 255f);
        alphaFloat = Mathf.Clamp01(alphaFloat);
        if (alphaFloat <= 0.0001f)
        {
            return;
        }

        var invAlpha = 1f / alphaFloat;
        red += Mathf.Clamp(blackPixel.r * invAlpha, 0f, 255f);
        green += Mathf.Clamp(blackPixel.g * invAlpha, 0f, 255f);
        blue += Mathf.Clamp(blackPixel.b * invAlpha, 0f, 255f);
        alpha += alphaFloat * 255f;
    }

    private static byte ClampByte(int value)
    {
        return (byte)Mathf.Clamp(value, 0, 255);
    }

    private static void SetHideFlagsRecursively(GameObject root, HideFlags hideFlags)
    {
        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
        {
            transform.gameObject.hideFlags = hideFlags;
        }
    }

    private static PortraitFrame? CalculatePortraitFrame(GameObject root)
    {
        var corners = new Vector3[4];
        var hasContentBounds = false;
        var hasHeadBounds = false;
        var hasUpperBounds = false;
        var contentBounds = new Bounds();
        var headBounds = new Bounds();
        var upperBounds = new Bounds();
        foreach (var graphic in root.GetComponentsInChildren<Graphic>(false))
        {
            if (graphic == null || !graphic.enabled || graphic.canvasRenderer == null)
            {
                continue;
            }

            var rect = graphic.rectTransform;
            if (rect == null || rect.rect.width <= 0f || rect.rect.height <= 0f)
            {
                continue;
            }

            var image = graphic as Image;
            if (image != null && image.sprite == null)
            {
                continue;
            }

            var color = graphic.color;
            if (color.a <= 0.001f)
            {
                continue;
            }

            if (!HasVisibleCanvasGroupAlpha(graphic.transform))
            {
                continue;
            }

            var isHead = IsHeadCenterGraphic(graphic.transform);
            var isUpper = isHead || IsPortraitFramingGraphic(graphic.transform);
            rect.GetWorldCorners(corners);
            for (var index = 0; index < corners.Length; index++)
            {
                Encapsulate(ref contentBounds, ref hasContentBounds, corners[index]);
                if (isHead)
                {
                    Encapsulate(ref headBounds, ref hasHeadBounds, corners[index]);
                }

                if (isUpper)
                {
                    Encapsulate(ref upperBounds, ref hasUpperBounds, corners[index]);
                }
            }
        }

        if (!hasContentBounds)
        {
            return null;
        }

        if (!hasHeadBounds)
        {
            headBounds = contentBounds;
            hasHeadBounds = true;
        }

        if (!hasUpperBounds)
        {
            upperBounds = contentBounds;
            hasUpperBounds = true;
        }

        _ = hasHeadBounds;
        _ = hasUpperBounds;
        return new PortraitFrame(contentBounds, headBounds, upperBounds);
    }

    private static Bounds? CalculatePartnerHeadShotBounds(GameObject root)
    {
        var corners = new Vector3[4];
        var hasBounds = false;
        var bounds = new Bounds();
        foreach (var graphic in root.GetComponentsInChildren<Graphic>(false))
        {
            if (!IsVisibleGraphic(graphic) || !IsPartnerHeadShotGraphic(graphic.transform))
            {
                continue;
            }

            graphic.rectTransform.GetWorldCorners(corners);
            for (var index = 0; index < corners.Length; index++)
            {
                Encapsulate(ref bounds, ref hasBounds, corners[index]);
            }
        }

        return hasBounds ? (Bounds?)bounds : null;
    }

    private static bool IsVisibleGraphic(Graphic graphic)
    {
        if (graphic == null || !graphic.enabled || graphic.canvasRenderer == null)
        {
            return false;
        }

        var rect = graphic.rectTransform;
        if (rect == null || rect.rect.width <= 0f || rect.rect.height <= 0f)
        {
            return false;
        }

        var image = graphic as Image;
        if (image != null && image.sprite == null)
        {
            return false;
        }

        return graphic.color.a > 0.001f && HasVisibleCanvasGroupAlpha(graphic.transform);
    }

    private static void Encapsulate(ref Bounds bounds, ref bool hasBounds, Vector3 point)
    {
        if (!hasBounds)
        {
            bounds = new Bounds(point, Vector3.zero);
            hasBounds = true;
            return;
        }

        bounds.Encapsulate(point);
    }

    private static bool IsHeadCenterGraphic(Transform transform)
    {
        for (var current = transform; current != null; current = current.parent)
        {
            var normalized = NormalizeName(current.name);
            for (var index = 0; index < HeadCenterNameParts.Length; index++)
            {
                if (normalized.Contains(HeadCenterNameParts[index]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsPartnerHeadShotGraphic(Transform transform)
    {
        for (var current = transform; current != null; current = current.parent)
        {
            var normalized = NormalizeName(current.name);
            if (IsLowerBodyName(normalized) || normalized.Contains("body") || normalized.Contains("arm") || normalized.Contains("hand"))
            {
                return false;
            }

            if (IsPartnerHeadRootName(normalized))
            {
                return true;
            }

            for (var index = 0; index < PartnerHeadShotNameParts.Length; index++)
            {
                if (normalized.Contains(PartnerHeadShotNameParts[index]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsPartnerHeadRootName(string normalized)
    {
        return normalized == "top03" || normalized == "top02";
    }

    private static bool IsLowerBodyGraphic(Transform transform)
    {
        for (var current = transform; current != null; current = current.parent)
        {
            if (IsLowerBodyName(NormalizeName(current.name)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPortraitFramingGraphic(Transform transform)
    {
        for (var current = transform; current != null; current = current.parent)
        {
            var normalized = NormalizeName(current.name);
            if (IsLowerBodyName(normalized))
            {
                return false;
            }

            for (var index = 0; index < PortraitNonFramingNameParts.Length; index++)
            {
                if (normalized.Contains(PortraitNonFramingNameParts[index]))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsLowerBodyName(string normalized)
    {
        for (var index = 0; index < LowerBodyNameParts.Length; index++)
        {
            if (normalized.Contains(LowerBodyNameParts[index]))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeName(string name)
    {
        return string.IsNullOrEmpty(name)
            ? string.Empty
            : name.ToLowerInvariant();
    }

    private static bool HasVisibleCanvasGroupAlpha(Transform transform)
    {
        for (var current = transform; current != null; current = current.parent)
        {
            foreach (var group in current.GetComponents<CanvasGroup>())
            {
                if (group != null && group.enabled && group.alpha <= 0.001f)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static void FitToPortraitFrame(RectTransform root, PortraitFrame frame, PartnerRenderSpec spec)
    {
        if (root == null)
        {
            return;
        }

        var headBounds = frame.Head;
        var upperBounds = frame.Upper;
        var scaleFromHead = Mathf.Min(
            SafeScale(spec.TargetHeadWidth, headBounds.size.x),
            SafeScale(spec.TargetHeadHeight, headBounds.size.y));
        var scaleFromUpper = Mathf.Min(
            SafeScale(spec.TargetUpperWidth, upperBounds.size.x),
            SafeScale(spec.TargetUpperHeight, upperBounds.size.y));
        var scale = Mathf.Min(scaleFromHead, scaleFromUpper);
        if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0f)
        {
            scale = 1f;
        }

        scale *= spec.ZoomFactor;
        if (spec.KeepUpperInsideFrame)
        {
            scale = ClampScaleToUpperMargins(scale, upperBounds, spec);
        }
        var headCenter = headBounds.center;
        var offset = new Vector2(
            -headCenter.x * scale,
            spec.TargetHeadCenterY - headCenter.y * scale);

        root.localScale = root.localScale * scale;
        root.anchoredPosition += offset;
    }

    private static void FitToHeadShotFrame(RectTransform root, Bounds headBounds, PartnerRenderSpec spec)
    {
        if (root == null)
        {
            return;
        }

        var targetSize = spec.OutputSize * 0.94f;
        var scale = Mathf.Min(
            SafeScale(targetSize, headBounds.size.x),
            SafeScale(targetSize, headBounds.size.y));
        if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0f)
        {
            scale = 1f;
        }

        scale *= spec.ZoomFactor;
        var center = headBounds.center;
        var offset = new Vector2(
            -center.x * scale,
            -center.y * scale);

        root.localScale = root.localScale * scale;
        root.anchoredPosition += offset;
    }

    private static void FitToFullFrame(RectTransform root, Bounds contentBounds, PartnerRenderSpec spec)
    {
        if (root == null)
        {
            return;
        }

        var scale = Mathf.Min(
            SafeScale(spec.OutputSize, contentBounds.size.x),
            SafeScale(spec.OutputSize, contentBounds.size.y));
        if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0f)
        {
            scale = 1f;
        }

        var center = contentBounds.center;
        var offset = new Vector2(
            -center.x * scale,
            -center.y * scale);

        root.localScale = root.localScale * scale;
        root.anchoredPosition += offset;
    }

    private static float SafeScale(float targetSize, float sourceSize)
    {
        return sourceSize > 0.0001f ? targetSize / sourceSize : float.PositiveInfinity;
    }

    private static float ClampScaleToUpperMargins(float scale, Bounds upperBounds, PartnerRenderSpec spec)
    {
        var maxScale = float.PositiveInfinity;
        if (upperBounds.size.x > 0.0001f)
        {
            maxScale = Mathf.Min(maxScale, (spec.OutputSize - spec.SafeMargin * 2f) / upperBounds.size.x);
        }

        if (upperBounds.size.y > 0.0001f)
        {
            maxScale = Mathf.Min(maxScale, (spec.OutputSize - spec.SafeMargin * 2f) / upperBounds.size.y);
        }

        if (float.IsInfinity(maxScale) || float.IsNaN(maxScale) || maxScale <= 0f)
        {
            return scale;
        }

        return Mathf.Min(scale, maxScale);
    }

    private static void ConfigurePartnerTextureImporter(string assetPath, PartnerRenderSpec spec)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            throw new InvalidOperationException("Generated PNG did not import as a texture: " + assetPath);
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.spritePivot = new Vector2(0.5f, 0.5f);
        importer.spriteBorder = Vector4.zero;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.isReadable = false;
        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.SetPlatformTextureSettings("Standalone", 2048, TextureImporterFormat.BC7, 50, false);
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteExtrude = 1;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        EnsurePartnerSpriteRect(assetPath, spec);
    }

    private static void EnsurePartnerSpriteRect(string assetPath, PartnerRenderSpec spec)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sprite == null)
        {
            throw new InvalidOperationException("Generated PNG did not import as a sprite: " + assetPath);
        }

        var rect = sprite.rect;
        if (!Mathf.Approximately(rect.width, spec.OutputSize) || !Mathf.Approximately(rect.height, spec.OutputSize))
        {
            throw new InvalidOperationException(string.Format(
                CultureInfo.InvariantCulture,
                "Generated {0} sprite must be {1}x{1}, but imported as {2}x{3}: {4}",
                spec.Label,
                spec.OutputSize,
                rect.width,
                rect.height,
                assetPath));
        }
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

    private static void EnsureAssetFolder(string assetPath)
    {
        var normalized = NormalizeAssetPath(assetPath).Trim('/');
        var parts = normalized.Split('/');
        if (parts.Length == 0 || !string.Equals(parts[0], "Assets", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Asset folder must be under Assets: " + assetPath);
        }

        var current = "Assets";
        for (var index = 1; index < parts.Length; index++)
        {
            var next = current + "/" + parts[index];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[index]);
            }

            current = next;
        }
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
        return NormalizeFullPath(Path.Combine(ProjectRoot, assetPath));
    }

    private static string ProjectRoot
    {
        get { return NormalizeFullPath(Path.GetDirectoryName(Application.dataPath) ?? "."); }
    }

    private static string NormalizeFullPath(string path)
    {
        return Path.GetFullPath(path).TrimEnd('/', '\\');
    }

    private static string NormalizeAssetPath(string path)
    {
        return path.Replace('\\', '/');
    }

    public struct PartnerResultClipRect
    {
        public PartnerResultClipRect(float centerX, float centerY, float size)
        {
            CenterX = centerX;
            CenterY = centerY;
            Size = Mathf.Max(0.0001f, size);
        }

        public float CenterX { get; }

        public float CenterY { get; }

        public float Size { get; }

        public Vector2 Center
        {
            get { return new Vector2(CenterX, CenterY); }
        }

        public Rect Rect
        {
            get { return new Rect(CenterX - Size / 2f, CenterY - Size / 2f, Size, Size); }
        }
    }

    public sealed class PartnerResultPreview
    {
        public PartnerResultPreview(Texture2D texture, Rect frameRect, Bounds contentBounds)
        {
            Texture = texture;
            FrameRect = frameRect;
            ContentBounds = contentBounds;
        }

        public Texture2D Texture { get; }

        public Rect FrameRect { get; }

        public Bounds ContentBounds { get; }
    }

    public sealed class PartnerResultGenerationResult
    {
        public PartnerResultGenerationResult(int id, string pngAssetPath, string assetBundlePath)
        {
            Id = id;
            PngAssetPath = pngAssetPath;
            AssetBundlePath = assetBundlePath;
        }

        public int Id { get; }

        public string PngAssetPath { get; }

        public string AssetBundlePath { get; }
    }

    public sealed class PartnerClipGenerationSummary
    {
        public PartnerClipGenerationSummary(
            int id,
            PartnerClipGenerationItemResult partnerResult,
            PartnerClipGenerationItemResult partner)
        {
            Id = id;
            PartnerResult = partnerResult;
            Partner = partner;
        }

        public int Id { get; }

        public PartnerClipGenerationItemResult PartnerResult { get; }

        public PartnerClipGenerationItemResult Partner { get; }

        public bool HasFailure
        {
            get { return !PartnerResult.Success || !Partner.Success; }
        }
    }

    public sealed class PartnerClipGenerationItemResult
    {
        private PartnerClipGenerationItemResult(
            string label,
            int id,
            bool success,
            string pngAssetPath,
            string assetBundlePath,
            string errorMessage)
        {
            Label = label;
            Id = id;
            Success = success;
            PngAssetPath = pngAssetPath;
            AssetBundlePath = assetBundlePath;
            ErrorMessage = errorMessage;
        }

        public string Label { get; }

        public int Id { get; }

        public bool Success { get; }

        public string PngAssetPath { get; }

        public string AssetBundlePath { get; }

        public string ErrorMessage { get; }

        public static PartnerClipGenerationItemResult CreateSuccess(
            string label,
            int id,
            string pngAssetPath,
            string assetBundlePath)
        {
            return new PartnerClipGenerationItemResult(label, id, true, pngAssetPath, assetBundlePath, null);
        }

        public static PartnerClipGenerationItemResult CreateFailure(string label, int id, string errorMessage)
        {
            return new PartnerClipGenerationItemResult(label, id, false, null, null, errorMessage);
        }
    }

    private sealed class BuildSummary
    {
        public int SuccessCount { get; set; }

        public List<string> Failures { get; set; } = new List<string>();
    }

    private struct PartnerPrefab
    {
        public PartnerPrefab(int id, string assetPath)
        {
            Id = id;
            AssetPath = assetPath;
        }

        public int Id { get; }

        public string AssetPath { get; }
    }

    private struct GeneratedPartnerPng
    {
        public GeneratedPartnerPng(int id, string assetPath, string bundleNamePrefix)
        {
            Id = id;
            AssetPath = assetPath;
            BundleNamePrefix = bundleNamePrefix;
        }

        public int Id { get; }

        public string AssetPath { get; }

        public string BundleNamePrefix { get; }
    }

    private struct PartnerRenderSpec
    {
        public PartnerRenderSpec(
            string label,
            string assetNamePrefix,
            string bundleNamePrefix,
            int outputSize,
            int renderScale,
            float targetHeadWidthRatio,
            float targetHeadHeightRatio,
            float targetUpperWidthRatio,
            float targetUpperHeightRatio,
            float targetHeadCenterYRatio,
            float zoomFactor,
            float safeMargin,
            bool keepUpperInsideFrame,
            PartnerRenderMode renderMode)
        {
            Label = label;
            AssetNamePrefix = assetNamePrefix;
            BundleNamePrefix = bundleNamePrefix;
            OutputSize = outputSize;
            RenderScale = renderScale;
            RenderSize = outputSize * renderScale;
            TargetHeadWidth = outputSize * targetHeadWidthRatio;
            TargetHeadHeight = outputSize * targetHeadHeightRatio;
            TargetUpperWidth = outputSize * targetUpperWidthRatio;
            TargetUpperHeight = outputSize * targetUpperHeightRatio;
            TargetHeadCenterY = outputSize * targetHeadCenterYRatio;
            ZoomFactor = zoomFactor;
            SafeMargin = safeMargin;
            KeepUpperInsideFrame = keepUpperInsideFrame;
            RenderMode = renderMode;
        }

        public string Label { get; }

        public string AssetNamePrefix { get; }

        public string BundleNamePrefix { get; }

        public int OutputSize { get; }

        public int RenderScale { get; }

        public int RenderSize { get; }

        public float TargetHeadWidth { get; }

        public float TargetHeadHeight { get; }

        public float TargetUpperWidth { get; }

        public float TargetUpperHeight { get; }

        public float TargetHeadCenterY { get; }

        public float ZoomFactor { get; }

        public float SafeMargin { get; }

        public bool KeepUpperInsideFrame { get; }

        public PartnerRenderMode RenderMode { get; }
    }

    private enum PartnerRenderMode
    {
        Portrait = 0,
        HeadShot = 1,
    }

    private struct PortraitFrame
    {
        public PortraitFrame(Bounds content, Bounds head, Bounds upper)
        {
            Content = content;
            Head = head;
            Upper = upper;
        }

        public Bounds Content { get; }

        public Bounds Head { get; }

        public Bounds Upper { get; }
    }
}
#endif
