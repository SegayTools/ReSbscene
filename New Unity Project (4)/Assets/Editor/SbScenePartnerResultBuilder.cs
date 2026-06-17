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
    private const string MenuPath = "Tools/SbScene/Build PartnerResult Bundles";
    private const string PrefabDir = "Assets/AssetBundle/navichara/prefab";
    private const string PartnerAssetDir = "Assets/AssetBundle/Partner";
    private const string OutputRelativePath = "out/AssetBundles";
    private const int OutputSize = 512;
    private const int RenderScale = 2;
    private const int RenderSize = OutputSize * RenderScale;
    private static readonly Regex PrefabNamePattern = new Regex(
        @"^UI_Navichara_(\d{1,6})\.prefab$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    [MenuItem(MenuPath)]
    private static void BuildPartnerResultBundles()
    {
        try
        {
            var result = BuildAll();
            var message = string.Format(
                CultureInfo.InvariantCulture,
                "Generated {0} PartnerResult bundle(s).\nSkipped: {1}\nFailed: {2}\nOutput: {3}",
                result.SuccessCount,
                result.SkippedCount,
                result.Failures.Count,
                NormalizeFullPath(Path.Combine(ProjectRoot, OutputRelativePath, "partner")));
            if (result.Failures.Count > 0)
            {
                message += "\n\nFailures:\n" + string.Join("\n", result.Failures.ToArray());
            }

            EditorUtility.DisplayDialog("SbScene PartnerResult", message, "OK");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("SbScene PartnerResult", ex.Message, "OK");
        }
    }

    public static void BuildPartnerResultBundlesBatch()
    {
        var result = BuildAll();
        var outputPath = NormalizeFullPath(Path.Combine(ProjectRoot, OutputRelativePath, "partner"));
        Debug.Log(string.Format(
            CultureInfo.InvariantCulture,
            "Generated {0} PartnerResult bundle(s). Skipped: {1}. Failed: {2}. Output: {3}",
            result.SuccessCount,
            result.SkippedCount,
            result.Failures.Count,
            outputPath));
        foreach (var failure in result.Failures)
        {
            Debug.LogError(failure);
        }

        if (result.Failures.Count > 0)
        {
            throw new InvalidOperationException("PartnerResult bundle build failed.");
        }
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
                SkippedCount = 0,
                SuccessCount = 0,
            };
        }

        var generatedPngs = new List<GeneratedPartnerPng>();
        var failures = new List<string>();
        foreach (var prefab in prefabs)
        {
            try
            {
                var pngAssetPath = RenderPartnerPng(prefab);
                generatedPngs.Add(new GeneratedPartnerPng(prefab.Id, pngAssetPath));
            }
            catch (Exception ex)
            {
                failures.Add(string.Format(CultureInfo.InvariantCulture, "{0}: {1}", prefab.AssetPath, ex.Message));
            }
        }

        AssetDatabase.Refresh();

        var builds = generatedPngs
            .Select(item => new AssetBundleBuild
            {
                assetBundleName = string.Format(CultureInfo.InvariantCulture, "partner/ui_partnerresult_{0:D6}.ab", item.Id),
                assetNames = new[] { item.AssetPath },
            })
            .ToArray();

        if (builds.Length > 0)
        {
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

        AssetDatabase.Refresh();
        return new BuildSummary
        {
            SuccessCount = generatedPngs.Count,
            SkippedCount = prefabs.Length - generatedPngs.Count - failures.Count,
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

    private static string RenderPartnerPng(PartnerPrefab prefab)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(prefab.AssetPath);
        if (source == null)
        {
            throw new InvalidOperationException("Prefab could not be loaded.");
        }

        var pngAssetPath = string.Format(
            CultureInfo.InvariantCulture,
            "{0}/UI_PartnerResult_{1:D6}.png",
            PartnerAssetDir,
            prefab.Id);
        var pngAbsolutePath = ToAbsoluteAssetPath(pngAssetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(pngAbsolutePath) ?? ".");

        var sceneRoot = new GameObject("SbScenePartnerResultRenderRoot");
        sceneRoot.hideFlags = HideFlags.HideAndDontSave;
        var cameraObject = new GameObject("SbScenePartnerResultCamera");
        cameraObject.hideFlags = HideFlags.HideAndDontSave;
        var canvasObject = new GameObject("SbScenePartnerResultCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.hideFlags = HideFlags.HideAndDontSave;
        var renderTexture = new RenderTexture(RenderSize, RenderSize, 24, RenderTextureFormat.ARGB32);
        var previousActive = RenderTexture.active;
        Camera camera = null;
        try
        {
            cameraObject.transform.SetParent(sceneRoot.transform, false);
            camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.orthographic = true;
            camera.orthographicSize = OutputSize / 2f;
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
            canvasRect.sizeDelta = new Vector2(OutputSize, OutputSize);
            canvasRect.anchoredPosition = Vector2.zero;
            canvasRect.localPosition = Vector3.zero;
            canvasRect.localRotation = Quaternion.identity;
            canvasRect.localScale = Vector3.one;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            if (instance == null)
            {
                throw new InvalidOperationException("Prefab instance could not be created.");
            }

            instance.hideFlags = HideFlags.HideAndDontSave;
            SetHideFlagsRecursively(instance, HideFlags.HideAndDontSave);
            instance.transform.SetParent(canvasObject.transform, false);
            instance.SetActive(true);
            SampleDefaultFrame(instance);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(instance.GetComponent<RectTransform>());
            Canvas.ForceUpdateCanvases();

            var bounds = CalculateVisibleBounds(instance);
            if (!bounds.HasValue || bounds.Value.size.x <= 0f || bounds.Value.size.y <= 0f)
            {
                throw new InvalidOperationException("No visible UI bounds could be measured.");
            }

            FitToPartnerFrame(instance.GetComponent<RectTransform>(), bounds.Value);
            Canvas.ForceUpdateCanvases();

            var texture = new Texture2D(OutputSize, OutputSize, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixels32(RenderStraightAlphaPixels(camera, renderTexture));
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
        ConfigurePartnerTextureImporter(pngAssetPath);
        return pngAssetPath;
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

    private static Color32[] RenderStraightAlphaPixels(Camera camera, RenderTexture renderTexture)
    {
        var blackPixels = RenderPixels(camera, renderTexture, Color.black);
        var whitePixels = RenderPixels(camera, renderTexture, Color.white);
        var output = new Color32[OutputSize * OutputSize];
        for (var outputY = 0; outputY < OutputSize; outputY++)
        {
            for (var outputX = 0; outputX < OutputSize; outputX++)
            {
                var red = 0f;
                var green = 0f;
                var blue = 0f;
                var alpha = 0f;
                for (var sampleY = 0; sampleY < RenderScale; sampleY++)
                {
                    for (var sampleX = 0; sampleX < RenderScale; sampleX++)
                    {
                        var sampleIndex = ((outputY * RenderScale + sampleY) * RenderSize) + (outputX * RenderScale + sampleX);
                        AccumulateStraightAlpha(blackPixels[sampleIndex], whitePixels[sampleIndex], ref red, ref green, ref blue, ref alpha);
                    }
                }

                var sampleCount = RenderScale * RenderScale;
                output[outputY * OutputSize + outputX] = new Color32(
                    ClampByte(Mathf.RoundToInt(red / sampleCount)),
                    ClampByte(Mathf.RoundToInt(green / sampleCount)),
                    ClampByte(Mathf.RoundToInt(blue / sampleCount)),
                    ClampByte(Mathf.RoundToInt(alpha / sampleCount)));
            }
        }

        return output;
    }

    private static Color32[] RenderPixels(Camera camera, RenderTexture renderTexture, Color backgroundColor)
    {
        camera.backgroundColor = backgroundColor;
        RenderTexture.active = renderTexture;
        GL.Clear(true, true, backgroundColor);
        camera.Render();

        var texture = new Texture2D(RenderSize, RenderSize, TextureFormat.RGBA32, false);
        try
        {
            texture.ReadPixels(new Rect(0, 0, RenderSize, RenderSize), 0, 0);
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

    private static Bounds? CalculateVisibleBounds(GameObject root)
    {
        var corners = new Vector3[4];
        var hasBounds = false;
        var bounds = new Bounds();
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

            rect.GetWorldCorners(corners);
            for (var index = 0; index < corners.Length; index++)
            {
                if (!hasBounds)
                {
                    bounds = new Bounds(corners[index], Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(corners[index]);
                }
            }
        }

        return hasBounds ? bounds : (Bounds?)null;
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

    private static void FitToPartnerFrame(RectTransform root, Bounds contentBounds)
    {
        if (root == null)
        {
            return;
        }

        var scale = Mathf.Min(OutputSize / contentBounds.size.x, OutputSize / contentBounds.size.y);
        if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0f)
        {
            scale = 1f;
        }

        var center = contentBounds.center;
        var top = contentBounds.max.y;
        var targetCenterX = 0f;
        var targetTopY = OutputSize / 2f;
        var offset = new Vector2(
            targetCenterX - center.x * scale,
            targetTopY - top * scale);

        root.localScale = root.localScale * scale;
        root.anchoredPosition += offset;
    }

    private static void ConfigurePartnerTextureImporter(string assetPath)
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
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.isReadable = false;
        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.SetPlatformTextureSettings("Standalone", 2048, TextureImporterFormat.BC7, 50, false);
        importer.SaveAndReimport();
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

    private sealed class BuildSummary
    {
        public int SuccessCount { get; set; }

        public int SkippedCount { get; set; }

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
        public GeneratedPartnerPng(int id, string assetPath)
        {
            Id = id;
            AssetPath = assetPath;
        }

        public int Id { get; }

        public string AssetPath { get; }
    }
}
#endif
