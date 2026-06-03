#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;

public sealed class SbSceneNaviCharaImporter : EditorWindow
{
    private const string AdditiveUiShaderSource =
@"Shader ""SbScene/UI/Additive""
{
    Properties
    {
        [PerRendererData] _MainTex (""Sprite Texture"", 2D) = ""white"" {}
        _Color (""Tint"", Color) = (1,1,1,1)
        _StencilComp (""Stencil Comparison"", Float) = 8
        _Stencil (""Stencil ID"", Float) = 0
        _StencilOp (""Stencil Operation"", Float) = 0
        _StencilWriteMask (""Stencil Write Mask"", Float) = 255
        _StencilReadMask (""Stencil Read Mask"", Float) = 255
        _ColorMask (""Color Mask"", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip (""Use Alpha Clip"", Float) = 0
    }

    SubShader
    {
        Tags
        {
            ""Queue""=""Transparent""
            ""IgnoreProjector""=""True""
            ""RenderType""=""Transparent""
            ""PreviewType""=""Plane""
            ""CanUseSpriteAtlas""=""True""
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        BlendOp Add, Max
        Blend SrcAlpha One, One One
        ColorMask [_ColorMask]

        Pass
        {
            Name ""Default""
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include ""UnityCG.cginc""
            #include ""UnityUI.cginc""
            #pragma multi_compile __ UNITY_UI_CLIP_RECT
            #pragma multi_compile __ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _MainTex_ST;
            float4 _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 color = tex2D(_MainTex, i.texcoord) * i.color;
                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif
                return color;
            }
            ENDCG
        }
    }
}";

    private enum ImportMode
    {
        Full,
        ClipsOnly,
    }

    private TextAsset _exportJson;
    private ImportMode _mode = ImportMode.Full;
    private GameObject _clipsOnlyRoot;
    private bool _failOnHighDiagnostics;

    [MenuItem("Tools/SbScene/Import NaviChara Export...")]
    private static void Open()
    {
        GetWindow<SbSceneNaviCharaImporter>("SbScene NaviChara");
    }

    private void OnGUI()
    {
        _exportJson = (TextAsset)EditorGUILayout.ObjectField("Export JSON", _exportJson, typeof(TextAsset), false);
        _mode = (ImportMode)EditorGUILayout.EnumPopup("Mode", _mode);
        if (_mode == ImportMode.ClipsOnly)
        {
            _clipsOnlyRoot = (GameObject)EditorGUILayout.ObjectField("Prefab Root", _clipsOnlyRoot, typeof(GameObject), true);
        }

        _failOnHighDiagnostics = EditorGUILayout.Toggle("Fail on high diagnostics", _failOnHighDiagnostics);
        EditorGUILayout.Space();
        if (GUILayout.Button("Import"))
        {
            ImportSelected();
        }
    }

    private void ImportSelected()
    {
        if (_exportJson == null)
        {
            ShowNotice("SbScene NaviChara", "Select navichara-export.json first.");
            return;
        }

        var jsonPath = AssetDatabase.GetAssetPath(_exportJson);
        if (string.IsNullOrEmpty(jsonPath) || !jsonPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            EditorUtility.DisplayDialog("SbScene NaviChara", "The selected asset is not a JSON file.", "OK");
            return;
        }

        try
        {
            Import(jsonPath);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            ShowNotice("SbScene NaviChara", ex.Message);
        }
    }

    private void Import(string jsonAssetPath)
    {
        var data = JsonUtility.FromJson<ExportDto>(File.ReadAllText(ToAbsoluteAssetPath(jsonAssetPath)));
        if (data == null || data.schema != "sbscene.unityNavicharaExport.v1")
        {
            throw new InvalidDataException("JSON is not an sbscene.unityNavicharaExport.v1 file.");
        }

        var importDiagnostics = new List<string>();
        CheckJsonDiagnostics(data, importDiagnostics);
        if (_failOnHighDiagnostics && HasHighOrErrorDiagnostics(data))
        {
            throw new InvalidDataException("Import stopped because JSON contains high/error diagnostics.");
        }

        var baseDir = NormalizeAssetPath(Path.GetDirectoryName(jsonAssetPath) ?? "Assets");
        var sprites = ImportSprites(data, baseDir, importDiagnostics);
        var clips = CreateClips(data, baseDir, sprites, importDiagnostics);
        var controller = CreateController(data, baseDir, clips);

        if (_mode == ImportMode.Full)
        {
            CreatePrefab(data, baseDir, sprites, clips, controller, importDiagnostics);
        }
        else if (_clipsOnlyRoot != null)
        {
            ValidateExistingRoot(data, _clipsOnlyRoot.transform, importDiagnostics);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        foreach (var diagnostic in importDiagnostics)
        {
            Debug.LogWarning("[SbScene NaviChara] " + diagnostic);
        }

        ShowNotice(
            "SbScene NaviChara",
            string.Format("Imported {0} clips, {1} sprites. Diagnostics: {2}", clips.Count, sprites.Count, importDiagnostics.Count));
    }

    private static void CheckJsonDiagnostics(ExportDto data, List<string> importDiagnostics)
    {
        if (data.diagnostics == null)
        {
            return;
        }

        foreach (var diagnostic in data.diagnostics)
        {
            if (diagnostic == null)
            {
                continue;
            }

            importDiagnostics.Add(string.Format("{0}: {1} - {2}", diagnostic.severity, diagnostic.code, diagnostic.message));
        }
    }

    private static bool HasHighOrErrorDiagnostics(ExportDto data)
    {
        return data.diagnostics != null && data.diagnostics.Any(diagnostic =>
            diagnostic != null
            && (string.Equals(diagnostic.severity, "high", StringComparison.OrdinalIgnoreCase)
                || string.Equals(diagnostic.severity, "error", StringComparison.OrdinalIgnoreCase)));
    }

    private static Dictionary<string, Sprite> ImportSprites(ExportDto data, string baseDir, List<string> diagnostics)
    {
        var result = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        if (data.sprites == null)
        {
            return result;
        }

        foreach (var spriteInfo in data.sprites)
        {
            if (spriteInfo == null || string.IsNullOrEmpty(spriteInfo.id) || string.IsNullOrEmpty(spriteInfo.file))
            {
                continue;
            }

            var assetPath = CombineAssetPath(baseDir, spriteInfo.file);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                diagnostics.Add("Sprite PNG is missing or not under Assets: " + assetPath);
                continue;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = data.settings != null && data.settings.pixelsPerUnit > 0 ? (float)data.settings.pixelsPerUnit : 1f;
            importer.spritePivot = ToVector2(spriteInfo.pivotNormalized, new Vector2(0.5f, 0.5f));
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null)
            {
                diagnostics.Add("Sprite import did not produce a Sprite asset: " + assetPath);
                continue;
            }

            result[spriteInfo.id] = sprite;
        }

        return result;
    }

    private static Dictionary<string, AnimationClip> CreateClips(
        ExportDto data,
        string baseDir,
        Dictionary<string, Sprite> sprites,
        List<string> diagnostics)
    {
        var result = new Dictionary<string, AnimationClip>(StringComparer.Ordinal);
        if (data.clips == null)
        {
            return result;
        }

        var animationDir = EnsureAssetFolder(baseDir, "Animations");
        var multipleImageType = FindTypeByName("MultipleImage");
        var nodeById = data.nodes == null
            ? new Dictionary<int, NodeDto>()
            : data.nodes.Where(node => node != null).ToDictionary(node => node.id);
        foreach (var clipInfo in data.clips)
        {
            if (clipInfo == null || string.IsNullOrEmpty(clipInfo.name))
            {
                continue;
            }

            var clipPath = CombineAssetPath(animationDir, clipInfo.name + ".anim");
            AssetDatabase.DeleteAsset(clipPath);
            var clip = new AnimationClip();
            clip.frameRate = clipInfo.sampleRate > 0 ? clipInfo.sampleRate : 60;
            SetClipLoop(clip, clipInfo.loop);

            if (clipInfo.curves != null)
            {
                foreach (var curveInfo in clipInfo.curves)
                {
                    if (curveInfo == null || curveInfo.unity == null || curveInfo.keys == null || curveInfo.keys.Length == 0)
                    {
                        continue;
                    }

                    if (curveInfo.sbsceneTrackType == 18)
                    {
                        NodeDto node = null;
                        if (nodeById.TryGetValue(curveInfo.nodeId, out var resolvedNode))
                        {
                            node = resolvedNode;
                        }

                        var useSpriteFallback = multipleImageType == null
                            || node == null
                            || node.image == null
                            || node.image.primarySprites == null
                            || node.image.primarySprites.Length <= 1
                            || string.Equals(node.image.component, "Image", StringComparison.OrdinalIgnoreCase);
                        if (useSpriteFallback)
                        {
                            SetSpriteCurve(data, clip, curveInfo, sprites, diagnostics);
                            continue;
                        }
                    }

                    var bindingType = ResolveBindingType(curveInfo.unity.component, multipleImageType);
                    if (bindingType == null)
                    {
                        diagnostics.Add("Cannot resolve binding component: " + curveInfo.unity.component);
                        continue;
                    }

                    var binding = EditorCurveBinding.FloatCurve(curveInfo.path, bindingType, curveInfo.unity.property);
                    AnimationUtility.SetEditorCurve(clip, binding, BuildAnimationCurve(curveInfo.keys));
                    if (curveInfo.sbsceneTrackType == 18)
                    {
                        SetSpriteCurve(data, clip, curveInfo, sprites, diagnostics);
                    }
                }
            }

            AssetDatabase.CreateAsset(clip, clipPath);
            result[clipInfo.name] = clip;
        }

        return result;
    }

    private static AnimationCurve BuildAnimationCurve(CurveKeyDto[] keys)
    {
        var unityKeys = new Keyframe[keys.Length];
        for (var i = 0; i < keys.Length; i++)
        {
            var key = keys[i];
            var unityKey = new Keyframe((float)key.time, (float)key.value);
            if (key.hasInTangent)
            {
                unityKey.inTangent = (float)key.inTangent;
            }

            if (key.hasOutTangent)
            {
                unityKey.outTangent = (float)key.outTangent;
            }

            if (string.Equals(key.interp, "step", StringComparison.OrdinalIgnoreCase))
            {
                unityKey.inTangent = float.PositiveInfinity;
                unityKey.outTangent = float.PositiveInfinity;
            }

            unityKeys[i] = unityKey;
        }

        return new AnimationCurve(unityKeys);
    }

    private static void SetSpriteCurve(
        ExportDto data,
        AnimationClip clip,
        CurveDto curveInfo,
        Dictionary<string, Sprite> sprites,
        List<string> diagnostics)
    {
        var node = data.nodes == null ? null : data.nodes.FirstOrDefault(item => item != null && item.id == curveInfo.nodeId);
        if (node == null || node.image == null || node.image.primarySprites == null)
        {
            diagnostics.Add("Cannot build Image.sprite curve; node image metadata is missing for node " + curveInfo.nodeId);
            return;
        }

        var keyframes = new ObjectReferenceKeyframe[curveInfo.keys.Length];
        for (var i = 0; i < curveInfo.keys.Length; i++)
        {
            var key = curveInfo.keys[i];
            var index = Mathf.Clamp(Mathf.RoundToInt((float)key.value), 0, node.image.primarySprites.Length - 1);
            Sprite sprite = null;
            sprites.TryGetValue(node.image.primarySprites[index], out sprite);
            keyframes[i] = new ObjectReferenceKeyframe
            {
                time = (float)key.time,
                value = sprite,
            };
        }

        var binding = EditorCurveBinding.PPtrCurve(curveInfo.path, typeof(Image), "m_Sprite");
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);
    }

    private static AnimatorController CreateController(
        ExportDto data,
        string baseDir,
        Dictionary<string, AnimationClip> clips)
    {
        var controllerDir = EnsureAssetFolder(baseDir, "Controllers");
        var controllerName = data.character != null && !string.IsNullOrEmpty(data.character.controllerName)
            ? data.character.controllerName
            : "UI_NaviChara";
        var path = CombineAssetPath(controllerDir, controllerName + ".controller");
        AssetDatabase.DeleteAsset(path);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("IsClear", AnimatorControllerParameterType.Bool);

        var stateMachine = controller.layers[0].stateMachine;
        var states = new Dictionary<string, AnimatorState>(StringComparer.Ordinal);
        AddState(stateMachine, states, "Navi_Default", clips, "Navi_Default", new Vector3(240, 0, 0));
        AddState(stateMachine, states, "Navi_Welcom", clips, "Navi_Welcom", new Vector3(240, 80, 0));
        AddState(stateMachine, states, "Navi_Fun_Start", clips, "Navi_Fun_Start", new Vector3(240, 160, 0));
        AddState(stateMachine, states, "Navi_Fun_Loop_01", clips, "Navi_Fun_Loop_01", new Vector3(520, 160, 0));
        AddState(stateMachine, states, "Navi_Fun_End", clips, "Navi_Fun_End", new Vector3(800, 160, 0));
        AddState(stateMachine, states, "Navi_Sad_01", clips, "Navi_Sad_01", new Vector3(1080, 80, 0));
        AddState(stateMachine, states, "Navi_Fun_Loop_02", clips, "Navi_Fun_Loop_01", new Vector3(1080, 220, 0));
        if (states.ContainsKey("Navi_Default"))
        {
            stateMachine.defaultState = states["Navi_Default"];
        }

        AddExitTransition(states, "Navi_Fun_Start", "Navi_Fun_Loop_01");
        AddExitTransition(states, "Navi_Fun_Loop_01", "Navi_Fun_End");
        AddConditionalTransition(states, "Navi_Fun_End", "Navi_Sad_01", AnimatorConditionMode.IfNot, "IsClear");
        AddConditionalTransition(states, "Navi_Fun_End", "Navi_Fun_Loop_02", AnimatorConditionMode.If, "IsClear");
        return controller;
    }

    private static void AddState(
        AnimatorStateMachine stateMachine,
        Dictionary<string, AnimatorState> states,
        string stateName,
        Dictionary<string, AnimationClip> clips,
        string clipName,
        Vector3 position)
    {
        var state = stateMachine.AddState(stateName, position);
        AnimationClip clip;
        if (clips.TryGetValue(clipName, out clip))
        {
            state.motion = clip;
        }

        states[stateName] = state;
    }

    private static void AddExitTransition(Dictionary<string, AnimatorState> states, string from, string to)
    {
        if (!states.ContainsKey(from) || !states.ContainsKey(to))
        {
            return;
        }

        var transition = states[from].AddTransition(states[to]);
        transition.hasExitTime = true;
        transition.exitTime = 1f;
        transition.duration = 0f;
    }

    private static void AddConditionalTransition(
        Dictionary<string, AnimatorState> states,
        string from,
        string to,
        AnimatorConditionMode mode,
        string parameter)
    {
        if (!states.ContainsKey(from) || !states.ContainsKey(to))
        {
            return;
        }

        var transition = states[from].AddTransition(states[to]);
        transition.hasExitTime = true;
        transition.exitTime = 1f;
        transition.duration = 0f;
        transition.AddCondition(mode, 0, parameter);
    }

    private static void CreatePrefab(
        ExportDto data,
        string baseDir,
        Dictionary<string, Sprite> sprites,
        Dictionary<string, AnimationClip> clips,
        AnimatorController controller,
        List<string> diagnostics)
    {
        var prefabName = data.character != null && !string.IsNullOrEmpty(data.character.prefabName)
            ? data.character.prefabName
            : "UI_Navichara";
        var root = new GameObject(prefabName, typeof(RectTransform));
        try
        {
            SetupRectTransform(root.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, Vector2.one, 0);
            var animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;

            var characterId = data.character != null ? data.character.id : 0;
            var nullUi = CreateChild(root.transform, "Null_UI_Navichara_" + characterId);
            var moveObject = CreateChild(nullUi.transform, "MoveObject");
            var emotion = CreateChild(root.transform, "Null_EFF_Emotion");
            if (data.settings != null && data.settings.rootTransform != null)
            {
                var rect = nullUi.GetComponent<RectTransform>();
                rect.localScale = Vector3.one * (float)data.settings.rootTransform.scale;
                rect.anchoredPosition = ToVector2(data.settings.rootTransform.offset, Vector2.zero);
            }

            var nodes = data.nodes == null ? new NodeDto[0] : data.nodes.Where(node => node != null).ToArray();
            var nodeById = nodes.ToDictionary(node => node.id);
            var objectsById = new Dictionary<int, GameObject>();
            foreach (var node in nodes)
            {
                CreateNode(node, nodeById, objectsById, moveObject.transform, baseDir, sprites, diagnostics);
            }

            BindNavigationCharacter(root, animator, emotion, clips, diagnostics);
            var prefabDir = EnsureAssetFolder(baseDir, "Prefabs");
            var prefabPath = CombineAssetPath(prefabDir, prefabName + ".prefab");
            AssetDatabase.DeleteAsset(prefabPath);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            DestroyImmediate(root);
        }
    }

    private static GameObject CreateNode(
        NodeDto node,
        Dictionary<int, NodeDto> nodeById,
        Dictionary<int, GameObject> objectsById,
        Transform moveObject,
        string baseDir,
        Dictionary<string, Sprite> sprites,
        List<string> diagnostics)
    {
        GameObject existing;
        if (objectsById.TryGetValue(node.id, out existing))
        {
            return existing;
        }

        Transform parent = moveObject;
        if (node.parentId >= 0 && nodeById.ContainsKey(node.parentId))
        {
            parent = CreateNode(nodeById[node.parentId], nodeById, objectsById, moveObject, baseDir, sprites, diagnostics).transform;
        }

        var go = CreateChild(parent, string.IsNullOrEmpty(node.unityName) ? "node_" + node.id : node.unityName);
        objectsById[node.id] = go;
        var rect = go.GetComponent<RectTransform>();
        var stat = node.@static;
        if (stat != null)
        {
            SetupRectTransform(
                rect,
                ToVector2(stat.pivotNormalized, new Vector2(0.5f, 0.5f)),
                ToVector2(stat.anchoredPosition, Vector2.zero),
                ToVector2(stat.size, Vector2.zero),
                ToVector2(stat.scale, Vector2.one),
                (float)stat.rotationZ);
            go.SetActive(stat.display);
        }

        SetupCanvasGroup(go, stat);

        if (node.image != null)
        {
            SetupImageComponent(go, node, baseDir, sprites, diagnostics);
        }

        return go;
    }

    private static void SetupImageComponent(
        GameObject go,
        NodeDto node,
        string baseDir,
        Dictionary<string, Sprite> sprites,
        List<string> diagnostics)
    {
        var multipleImageType = FindTypeByName("MultipleImage");
        var useMultipleImage = multipleImageType != null && typeof(Image).IsAssignableFrom(multipleImageType) && node.image.primarySprites != null && node.image.primarySprites.Length > 1;
        var component = useMultipleImage ? go.AddComponent(multipleImageType) : go.AddComponent<Image>();
        var image = component as Image;
        if (image == null)
        {
            diagnostics.Add("MultipleImage does not inherit Image; using no static sprite for node " + node.unityName);
            return;
        }

        var primarySprites = ResolveSprites(node.image.primarySprites, sprites);
        var defaultIndex = primarySprites.Length == 0 ? 0 : Mathf.Clamp(node.image.defaultPrimaryIndex, 0, primarySprites.Length - 1);
        if (primarySprites.Length > 0)
        {
            image.sprite = primarySprites[defaultIndex];
        }

        if (node.@static != null)
        {
            var color = ParseColor(node.@static.materialColor, Color.white);
            color.a = 1f;
            image.color = color;
        }

        if (node.image.additiveBlend)
        {
            var material = GetOrCreateAdditiveMaterial(baseDir, diagnostics);
            if (material != null)
            {
                image.material = material;
            }
        }

        if (useMultipleImage)
        {
            var serialized = new SerializedObject(component);
            SetInt(serialized, "_selectSpriteIndex", defaultIndex);
            SetObjectArray(serialized, "MultiSprites", primarySprites);
            serialized.ApplyModifiedProperties();
        }
    }

    private static void SetupCanvasGroup(GameObject go, NodeStaticDto stat)
    {
        var group = go.AddComponent<CanvasGroup>();
        group.alpha = stat == null ? 1f : ParseColor(stat.materialColor, Color.white).a;
        group.interactable = true;
        group.blocksRaycasts = false;
        group.ignoreParentGroups = false;
    }

    private static Material GetOrCreateAdditiveMaterial(string baseDir, List<string> diagnostics)
    {
        var shaderDir = EnsureAssetFolder(baseDir, "Shaders");
        var shaderPath = CombineAssetPath(shaderDir, "SbSceneUIAdditive.shader");
        var shaderAbsolutePath = ToAbsoluteAssetPath(shaderPath);
        if (!File.Exists(shaderAbsolutePath) || File.ReadAllText(shaderAbsolutePath) != AdditiveUiShaderSource)
        {
            File.WriteAllText(shaderAbsolutePath, AdditiveUiShaderSource, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(shaderPath, ImportAssetOptions.ForceUpdate);
        }

        var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
        if (shader == null)
        {
            shader = Shader.Find("SbScene/UI/Additive");
        }

        if (shader == null)
        {
            diagnostics.Add("Cannot create additive material; shader did not import: " + shaderPath);
            return null;
        }

        var materialDir = EnsureAssetFolder(baseDir, "Materials");
        var materialPath = CombineAssetPath(materialDir, "SbSceneUIAdditive.mat");
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(shader)
            {
                name = "SbSceneUIAdditive",
            };
            AssetDatabase.CreateAsset(material, materialPath);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
            EditorUtility.SetDirty(material);
        }

        return material;
    }

    private static Sprite[] ResolveSprites(string[] ids, Dictionary<string, Sprite> sprites)
    {
        if (ids == null)
        {
            return new Sprite[0];
        }

        var result = new List<Sprite>();
        foreach (var id in ids)
        {
            Sprite sprite;
            if (sprites.TryGetValue(id, out sprite) && sprite != null)
            {
                result.Add(sprite);
            }
        }

        return result.ToArray();
    }

    private static void BindNavigationCharacter(
        GameObject root,
        Animator animator,
        GameObject emotion,
        Dictionary<string, AnimationClip> clips,
        List<string> diagnostics)
    {
        var type = FindTypeByName("NavigationCharacter");
        if (type == null || !typeof(Component).IsAssignableFrom(type))
        {
            diagnostics.Add("NavigationCharacter type was not found; prefab was generated without that component.");
            return;
        }

        var component = root.AddComponent(type);
        var serialized = new SerializedObject(component);
        SetObjectArray(serialized, "_characterNaviAnimator", new UnityEngine.Object[] { animator });
        SetInt(serialized, "_animationLayerIndex", 0);
        SetObject(serialized, "_emotionObject", emotion);
        SetObject(serialized, "_default", GetClip(clips, "Navi_Default"));
        SetObject(serialized, "_funStart", GetClip(clips, "Navi_Fun_Start"));
        SetObject(serialized, "_funLoop", GetClip(clips, "Navi_Fun_Loop_01"));
        SetObject(serialized, "_funEnd", GetClip(clips, "Navi_Fun_End"));
        SetObject(serialized, "_sad", GetClip(clips, "Navi_Sad_01"));
        SetInt(serialized, "HashDefault", Animator.StringToHash("Navi_Default"));
        SetInt(serialized, "HashWelcom", Animator.StringToHash("Navi_Welcom"));
        SetInt(serialized, "HashFunStart", Animator.StringToHash("Navi_Fun_Start"));
        SetInt(serialized, "HashSad01", Animator.StringToHash("Navi_Sad_01"));
        SetInt(serialized, "HashFunLoop", Animator.StringToHash("Navi_Fun_Loop_02"));
        serialized.ApplyModifiedProperties();
    }

    private static AnimationClip GetClip(Dictionary<string, AnimationClip> clips, string name)
    {
        AnimationClip clip;
        clips.TryGetValue(name, out clip);
        return clip;
    }

    private static void ValidateExistingRoot(ExportDto data, Transform root, List<string> diagnostics)
    {
        if (data.nodes == null)
        {
            return;
        }

        var multipleImageType = FindTypeByName("MultipleImage");
        foreach (var node in data.nodes)
        {
            if (node == null || string.IsNullOrEmpty(node.unityPath))
            {
                continue;
            }

            var transform = root.Find(node.unityPath);
            if (transform == null)
            {
                diagnostics.Add("Existing root is missing path: " + node.unityPath);
                continue;
            }

            if (node.image != null)
            {
                var useMultipleImage = multipleImageType != null
                    && node.image.primarySprites != null
                    && node.image.primarySprites.Length > 1;
                var hasExpectedComponent = useMultipleImage
                    ? transform.GetComponent(multipleImageType) != null
                    : transform.GetComponent<Image>() != null;
                if (!hasExpectedComponent)
                {
                    diagnostics.Add((useMultipleImage ? "Existing image node has no MultipleImage component: " : "Existing image node has no Image component: ") + node.unityPath);
                }
            }
        }
    }

    private static void SetupRectTransform(RectTransform rect, Vector2 pivot, Vector2 anchoredPosition, Vector2 size, Vector2 scale, float rotationZ)
    {
        var parentRect = rect.parent as RectTransform;
        var anchor = parentRect != null ? parentRect.pivot : new Vector2(0.5f, 0.5f);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        rect.localScale = new Vector3(scale.x, scale.y, 1f);
        rect.localEulerAngles = new Vector3(0f, 0f, rotationZ);
    }

    private static GameObject CreateChild(Transform parent, string name)
    {
        var child = new GameObject(name, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        SetupRectTransform(child.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, Vector2.one, 0f);
        return child;
    }

    private static void SetClipLoop(AnimationClip clip, bool loop)
    {
        var serialized = new SerializedObject(clip);
        var loopProperty = serialized.FindProperty("m_AnimationClipSettings.m_LoopTime");
        if (loopProperty != null)
        {
            loopProperty.boolValue = loop;
            serialized.ApplyModifiedProperties();
        }
    }

    private static Type ResolveBindingType(string component, Type multipleImageType)
    {
        if (string.Equals(component, "RectTransform", StringComparison.Ordinal))
        {
            return typeof(RectTransform);
        }

        if (string.Equals(component, "Transform", StringComparison.Ordinal))
        {
            return typeof(Transform);
        }

        if (string.Equals(component, "GameObject", StringComparison.Ordinal))
        {
            return typeof(GameObject);
        }

        if (string.Equals(component, "CanvasGroup", StringComparison.Ordinal))
        {
            return typeof(CanvasGroup);
        }

        if (string.Equals(component, "Graphic", StringComparison.Ordinal))
        {
            return typeof(Image);
        }

        if (string.Equals(component, "MultipleImage", StringComparison.Ordinal))
        {
            return multipleImageType;
        }

        return FindTypeByName(component);
    }

    private static Type FindTypeByName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch
            {
                continue;
            }

            foreach (var type in types)
            {
                if (string.Equals(type.Name, name, StringComparison.Ordinal) || string.Equals(type.FullName, name, StringComparison.Ordinal))
                {
                    return type;
                }
            }
        }

        return null;
    }

    private static void SetObject(SerializedObject serialized, string name, UnityEngine.Object value)
    {
        var property = serialized.FindProperty(name);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    private static void SetInt(SerializedObject serialized, string name, int value)
    {
        var property = serialized.FindProperty(name);
        if (property != null)
        {
            property.intValue = value;
        }
    }

    private static void SetObjectArray(SerializedObject serialized, string name, UnityEngine.Object[] values)
    {
        var property = serialized.FindProperty(name);
        if (property == null || !property.isArray)
        {
            return;
        }

        property.arraySize = values.Length;
        for (var i = 0; i < values.Length; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }

    private static string EnsureAssetFolder(string parent, string name)
    {
        var path = CombineAssetPath(parent, name);
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, name);
        }

        return path;
    }

    private static string CombineAssetPath(string left, string right)
    {
        return NormalizeAssetPath(Path.Combine(left, right));
    }

    private static string NormalizeAssetPath(string path)
    {
        return path.Replace("\\", "/");
    }

    private static string ToAbsoluteAssetPath(string assetPath)
    {
        var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static Vector2 ToVector2(Vector2Dto value, Vector2 fallback)
    {
        return value == null ? fallback : new Vector2((float)value.x, (float)value.y);
    }

    private static Color ParseColor(string text, Color fallback)
    {
        if (string.IsNullOrEmpty(text))
        {
            return fallback;
        }

        if (text.StartsWith("#", StringComparison.Ordinal))
        {
            text = text.Substring(1);
        }

        if (text.Length != 8)
        {
            return fallback;
        }

        uint value;
        if (!uint.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out value))
        {
            return fallback;
        }

        var a = ((value >> 24) & 0xFF) / 255f;
        var r = ((value >> 16) & 0xFF) / 255f;
        var g = ((value >> 8) & 0xFF) / 255f;
        var b = (value & 0xFF) / 255f;
        return new Color(r, g, b, a);
    }

    private static void ShowNotice(string title, string message)
    {
        if (Application.isBatchMode)
        {
            Debug.Log(string.Format("[{0}] {1}", title, message));
            return;
        }

        EditorUtility.DisplayDialog(title, message, "OK");
    }

    [Serializable]
    private sealed class ExportDto
    {
        public string schema;
        public SettingsDto settings;
        public CharacterDto character;
        public NodeDto[] nodes;
        public SpriteDto[] sprites;
        public ClipDto[] clips;
        public DiagnosticDto[] diagnostics;
    }

    [Serializable]
    private sealed class SettingsDto
    {
        public float pixelsPerUnit = 1f;
        public RootTransformDto rootTransform;
    }

    [Serializable]
    private sealed class RootTransformDto
    {
        public float scale = 1f;
        public Vector2Dto offset;
    }

    [Serializable]
    private sealed class CharacterDto
    {
        public int id;
        public string prefabName;
        public string controllerName;
    }

    [Serializable]
    private sealed class NodeDto
    {
        public int id;
        public string unityName;
        public string unityPath;
        public int parentId = -1;
        public NodeStaticDto @static;
        public NodeImageDto image;
    }

    [Serializable]
    private sealed class NodeStaticDto
    {
        public Vector2Dto anchoredPosition;
        public float rotationZ;
        public Vector2Dto scale;
        public bool display = true;
        public Vector2Dto size;
        public Vector2Dto pivotNormalized;
        public string materialColor;
    }

    [Serializable]
    private sealed class NodeImageDto
    {
        public string component;
        public int drawMode;
        public bool additiveBlend;
        public string[] primarySprites;
        public string[] secondarySprites;
        public int defaultPrimaryIndex;
        public int defaultSecondaryIndex;
    }

    [Serializable]
    private sealed class SpriteDto
    {
        public string id;
        public string file;
        public Vector2Dto pivotNormalized;
    }

    [Serializable]
    private sealed class ClipDto
    {
        public string name;
        public int sampleRate;
        public bool loop;
        public CurveDto[] curves;
    }

    [Serializable]
    private sealed class CurveDto
    {
        public int nodeId;
        public string path;
        public int sbsceneTrackType;
        public CurveBindingDto unity;
        public CurveKeyDto[] keys;
    }

    [Serializable]
    private sealed class CurveBindingDto
    {
        public string component;
        public string property;
        public string curveKind;
    }

    [Serializable]
    private sealed class CurveKeyDto
    {
        public int frame;
        public float time;
        public float value;
        public string interp;
        public bool hasInTangent;
        public bool hasOutTangent;
        public float inTangent;
        public float outTangent;
    }

    [Serializable]
    private sealed class DiagnosticDto
    {
        public string severity;
        public string code;
        public string message;
    }

    [Serializable]
    private sealed class Vector2Dto
    {
        public float x;
        public float y;
    }
}
#endif
