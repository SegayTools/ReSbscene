#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public sealed class SbScenePartnerResultClipEditorWindow : EditorWindow
{
    private const string MenuPath = "Tools/SbScene/PartnerResult Clip Editor";
    private const string ConfigDir = "Assets/Editor/PartnerResultClipConfigs";
    private const int PreviewTextureSize = 1024;
    private const float OutputSize = 512f;
    private const float DefaultClipCenterX = 0f;
    private const float DefaultPartnerResultClipCenterY = 60f;
    private const float DefaultPartnerResultClipSize = 420f;
    private const float DefaultPartnerClipCenterY = 120f;
    private const float DefaultPartnerClipSize = 180f;
    private const float MinClipSize = DefaultPartnerResultClipSize * 128f / OutputSize;
    private const float HandleSize = 9f;
    private static readonly string[] ClipTargetLabels = { "PartnerResult", "Partner" };
    private static readonly Color PartnerResultClipColor = new Color(0.25f, 0.85f, 1f, 1f);
    private static readonly Color PartnerClipColor = new Color(1f, 0.66f, 0.20f, 1f);

    private GameObject _prefab;
    private string _prefabAssetPath;
    private int _prefabId;
    private string _errorMessage;
    private Texture2D _previewTexture;
    private TextAsset _clipRectJson;
    private Rect _previewFrameRect;
    private ClipTarget _selectedClipTarget = ClipTarget.PartnerResult;
    private SbScenePartnerResultBuilder.PartnerResultClipRect _partnerResultClipRect = DefaultPartnerResultClipRect();
    private SbScenePartnerResultBuilder.PartnerResultClipRect _partnerClipRect = DefaultPartnerClipRect();
    private float _viewZoom = 1f;
    private Vector2 _viewPan;
    private Rect _lastPreviewArea;
    private Rect _lastImageRect;
    private DragMode _dragMode;
    private ClipTarget _dragTarget;
    private int _dragCorner = -1;
    private int _hotControl;
    private Vector2 _dragStartMouse;
    private Vector2 _dragStartPan;
    private SbScenePartnerResultBuilder.PartnerResultClipRect _dragStartClip;

    [MenuItem(MenuPath)]
    private static void Open()
    {
        GetWindow<SbScenePartnerResultClipEditorWindow>("PartnerResult Clip");
    }

    private void OnDisable()
    {
        ClearPreviewTexture();
    }

    private void OnGUI()
    {
        DrawHeader();
        var previewArea = GUILayoutUtility.GetRect(
            240f,
            100000f,
            260f,
            100000f,
            GUILayout.ExpandWidth(true),
            GUILayout.ExpandHeight(true));
        _lastPreviewArea = previewArea;
        DrawPreview(previewArea);
        HandlePrefabDrag(previewArea);
        HandlePreviewInput(previewArea);
    }

    private void DrawHeader()
    {
        EditorGUILayout.Space();
        EditorGUI.BeginChangeCheck();
        var selected = (GameObject)EditorGUILayout.ObjectField("Prefab", _prefab, typeof(GameObject), false);
        if (EditorGUI.EndChangeCheck())
        {
            SelectPrefab(selected);
        }

        EditorGUI.BeginChangeCheck();
        var selectedJson = (TextAsset)EditorGUILayout.ObjectField("Clip Rect JSON", _clipRectJson, typeof(TextAsset), false);
        if (EditorGUI.EndChangeCheck())
        {
            ImportClipRectJson(selectedJson);
        }

        EditorGUI.BeginChangeCheck();
        _selectedClipTarget = (ClipTarget)GUILayout.Toolbar((int)_selectedClipTarget, ClipTargetLabels);
        if (EditorGUI.EndChangeCheck())
        {
            Repaint();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(!HasValidPrefab))
            {
                if (GUILayout.Button("Generate Selected", GUILayout.Height(24f)))
                {
                    GenerateSelected();
                }

                if (GUILayout.Button("Save Rect", GUILayout.Height(24f)))
                {
                    SaveRect();
                }

                if (GUILayout.Button("Set Default Rect", GUILayout.Height(24f)))
                {
                    SetDefaultRect();
                }

                if (GUILayout.Button("Center Horizontally", GUILayout.Height(24f)))
                {
                    CenterRectHorizontally();
                }

                if (GUILayout.Button("Refresh Preview", GUILayout.Height(24f)))
                {
                    RefreshPreview();
                }
            }
        }

        if (HasValidPrefab)
        {
            EditorGUILayout.LabelField(
                "Selected",
                string.Format(CultureInfo.InvariantCulture, "{0}  ID {1:D6}", _prefabAssetPath, _prefabId));
            EditorGUILayout.LabelField(
                "Clip Rect",
                string.Format(
                    CultureInfo.InvariantCulture,
                    "PartnerResult center=({0:0.###}, {1:0.###}) size={2:0.###} | Partner center=({3:0.###}, {4:0.###}) size={5:0.###}",
                    _partnerResultClipRect.CenterX,
                    _partnerResultClipRect.CenterY,
                    _partnerResultClipRect.Size,
                    _partnerClipRect.CenterX,
                    _partnerClipRect.CenterY,
                    _partnerClipRect.Size));
        }

        if (!string.IsNullOrEmpty(_errorMessage))
        {
            EditorGUILayout.HelpBox(_errorMessage, MessageType.Error);
        }
    }

    private void SelectPrefab(GameObject prefab)
    {
        _prefab = prefab;
        _prefabAssetPath = null;
        _prefabId = 0;
        _errorMessage = null;
        ClearPreviewTexture();
        if (_prefab == null)
        {
            return;
        }

        var assetPath = AssetDatabase.GetAssetPath(_prefab);
        if (!SbScenePartnerResultBuilder.TryGetNavicharaPrefabInfo(assetPath, out _prefabId, out var error))
        {
            _errorMessage = error;
            return;
        }

        _prefabAssetPath = assetPath;
        SetBothDefaultRects();
        ResetView();
        RefreshPreview();
    }

    private void RefreshPreview()
    {
        if (!HasValidPrefab)
        {
            return;
        }

        ClearPreviewTexture();
        try
        {
            var preview = SbScenePartnerResultBuilder.RenderPartnerResultPreview(_prefabAssetPath, PreviewTextureSize);
            _previewTexture = preview.Texture;
            _previewFrameRect = preview.FrameRect;
            _errorMessage = null;
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
            Debug.LogException(ex);
        }

        Repaint();
    }

    private void GenerateSelected()
    {
        if (!HasValidPrefab)
        {
            return;
        }

        try
        {
            var result = SbScenePartnerResultBuilder.GeneratePartnerAndResultFromClips(
                _prefabAssetPath,
                _partnerResultClipRect,
                _partnerClipRect);
            _errorMessage = result.HasFailure ? BuildGenerationError(result) : null;
            EditorUtility.DisplayDialog(
                "PartnerResult Clip Editor",
                BuildGenerationMessage(result),
                "OK");
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("PartnerResult Clip Editor", ex.Message, "OK");
        }
    }

    private static string BuildGenerationMessage(SbScenePartnerResultBuilder.PartnerClipGenerationSummary result)
    {
        var builder = new StringBuilder();
        builder.AppendFormat(
            CultureInfo.InvariantCulture,
            result.HasFailure ? "Finished with failures for {0:D6}" : "Generated resources for {0:D6}",
            result.Id);
        builder.AppendLine();
        AppendGenerationItem(builder, result.PartnerResult);
        builder.AppendLine();
        AppendGenerationItem(builder, result.Partner);
        return builder.ToString();
    }

    private static string BuildGenerationError(SbScenePartnerResultBuilder.PartnerClipGenerationSummary result)
    {
        var builder = new StringBuilder();
        if (!result.PartnerResult.Success)
        {
            builder.Append(result.PartnerResult.Label).Append(": ").AppendLine(result.PartnerResult.ErrorMessage);
        }

        if (!result.Partner.Success)
        {
            builder.Append(result.Partner.Label).Append(": ").AppendLine(result.Partner.ErrorMessage);
        }

        return builder.ToString().TrimEnd();
    }

    private static void AppendGenerationItem(StringBuilder builder, SbScenePartnerResultBuilder.PartnerClipGenerationItemResult item)
    {
        builder.Append(item.Label).Append(": ");
        if (!item.Success)
        {
            builder.Append("Failed").AppendLine();
            builder.Append("Error: ").Append(item.ErrorMessage);
            return;
        }

        builder.Append("Success").AppendLine();
        builder.Append("PNG: ").AppendLine(item.PngAssetPath);
        builder.Append("AssetBundle: ").Append(item.AssetBundlePath);
    }

    private void DrawPreview(Rect area)
    {
        EditorGUI.DrawRect(area, new Color(0.12f, 0.12f, 0.12f, 1f));
        if (_previewTexture == null)
        {
            DrawCenteredText(area, HasValidPrefab ? "No preview" : "Drop UI_Navichara prefab here");
            _lastImageRect = Rect.zero;
            return;
        }

        _lastImageRect = CalculateImageRect(area);
        EditorGUI.DrawRect(_lastImageRect, new Color(0.18f, 0.18f, 0.18f, 1f));
        GUI.DrawTexture(_lastImageRect, _previewTexture, ScaleMode.StretchToFill, true);

        var selectedGuiRect = WorldToGuiRect(GetSelectedClipRect().Rect, _lastImageRect);
        DrawClipMask(_lastImageRect, selectedGuiRect);
        DrawClipRect(WorldToGuiRect(_partnerResultClipRect.Rect, _lastImageRect), ClipTarget.PartnerResult);
        DrawClipRect(WorldToGuiRect(_partnerClipRect.Rect, _lastImageRect), ClipTarget.Partner);
    }

    private void DrawCenteredText(Rect rect, string text)
    {
        var style = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
        };
        GUI.Label(rect, text, style);
    }

    private Rect CalculateImageRect(Rect area)
    {
        var side = Mathf.Max(1f, Mathf.Min(area.width, area.height) * Mathf.Clamp(_viewZoom, 0.2f, 8f));
        var center = area.center + _viewPan;
        return new Rect(center.x - side / 2f, center.y - side / 2f, side, side);
    }

    private void DrawClipMask(Rect imageRect, Rect clipRect)
    {
        var maskColor = new Color(0f, 0f, 0f, 0.48f);
        DrawMaskedRect(new Rect(imageRect.xMin, imageRect.yMin, imageRect.width, clipRect.yMin - imageRect.yMin), imageRect, maskColor);
        DrawMaskedRect(new Rect(imageRect.xMin, clipRect.yMax, imageRect.width, imageRect.yMax - clipRect.yMax), imageRect, maskColor);
        DrawMaskedRect(new Rect(imageRect.xMin, clipRect.yMin, clipRect.xMin - imageRect.xMin, clipRect.height), imageRect, maskColor);
        DrawMaskedRect(new Rect(clipRect.xMax, clipRect.yMin, imageRect.xMax - clipRect.xMax, clipRect.height), imageRect, maskColor);
    }

    private void DrawMaskedRect(Rect rect, Rect clipTo, Color color)
    {
        var clipped = Intersect(rect, clipTo);
        if (clipped.width > 0f && clipped.height > 0f)
        {
            EditorGUI.DrawRect(clipped, color);
        }
    }

    private void DrawClipRect(Rect rect, ClipTarget target)
    {
        var borderColor = GetClipColor(target);
        var selected = target == _selectedClipTarget;
        DrawBorder(rect, borderColor, selected ? 2f : 1f);
        DrawClipLabel(rect, target, borderColor);
        if (!selected)
        {
            return;
        }

        var handles = GetCornerHandleRects(rect);
        for (var index = 0; index < handles.Length; index++)
        {
            EditorGUI.DrawRect(handles[index], borderColor);
        }
    }

    private static void DrawClipLabel(Rect rect, ClipTarget target, Color color)
    {
        var labelRect = new Rect(rect.xMin + 4f, rect.yMin + 3f, 90f, 16f);
        var style = new GUIStyle(EditorStyles.miniBoldLabel)
        {
            normal = { textColor = color },
            alignment = TextAnchor.UpperLeft,
            clipping = TextClipping.Clip,
        };
        GUI.Label(labelRect, ClipTargetLabels[(int)target], style);
    }

    private static Color GetClipColor(ClipTarget target)
    {
        return target == ClipTarget.PartnerResult ? PartnerResultClipColor : PartnerClipColor;
    }

    private void DrawBorder(Rect rect, Color color, float thickness)
    {
        EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMin, rect.width, thickness), color);
        EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), color);
        EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMin, thickness, rect.height), color);
        EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.yMin, thickness, rect.height), color);
    }

    private Rect[] GetCornerHandleRects(Rect rect)
    {
        return new[]
        {
            CenteredRect(new Vector2(rect.xMin, rect.yMin), HandleSize),
            CenteredRect(new Vector2(rect.xMax, rect.yMin), HandleSize),
            CenteredRect(new Vector2(rect.xMax, rect.yMax), HandleSize),
            CenteredRect(new Vector2(rect.xMin, rect.yMax), HandleSize),
        };
    }

    private Rect CenteredRect(Vector2 center, float size)
    {
        return new Rect(center.x - size / 2f, center.y - size / 2f, size, size);
    }

    private void HandlePrefabDrag(Rect area)
    {
        var evt = Event.current;
        if (!area.Contains(evt.mousePosition))
        {
            return;
        }

        if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform)
        {
            return;
        }

        var draggedPrefab = FindDraggedPrefab();
        if (draggedPrefab == null)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
            return;
        }

        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        if (evt.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            SelectPrefab(draggedPrefab);
        }

        evt.Use();
    }

    private GameObject FindDraggedPrefab()
    {
        foreach (var draggedObject in DragAndDrop.objectReferences)
        {
            var gameObject = draggedObject as GameObject;
            if (gameObject == null)
            {
                continue;
            }

            if (SbScenePartnerResultBuilder.TryGetNavicharaPrefabInfo(AssetDatabase.GetAssetPath(gameObject), out _, out _))
            {
                return gameObject;
            }
        }

        return null;
    }

    private void HandlePreviewInput(Rect area)
    {
        if (_previewTexture == null || _lastImageRect.width <= 0f || _lastImageRect.height <= 0f)
        {
            return;
        }

        var evt = Event.current;
        switch (evt.type)
        {
            case EventType.ScrollWheel:
                if (area.Contains(evt.mousePosition))
                {
                    ZoomPreview(area, evt);
                }

                break;
            case EventType.MouseDown:
                BeginDrag(evt);
                break;
            case EventType.MouseDrag:
                ContinueDrag(evt);
                break;
            case EventType.MouseUp:
                EndDrag(evt);
                break;
        }
    }

    private void ZoomPreview(Rect area, Event evt)
    {
        var oldImageRect = CalculateImageRect(area);
        var uv = new Vector2(
            oldImageRect.width > 0f ? (evt.mousePosition.x - oldImageRect.xMin) / oldImageRect.width : 0.5f,
            oldImageRect.height > 0f ? (evt.mousePosition.y - oldImageRect.yMin) / oldImageRect.height : 0.5f);
        var zoomFactor = Mathf.Pow(1.1f, -evt.delta.y);
        _viewZoom = Mathf.Clamp(_viewZoom * zoomFactor, 0.25f, 8f);
        var newImageRect = CalculateImageRect(area);
        var sameUvPosition = new Vector2(
            newImageRect.xMin + uv.x * newImageRect.width,
            newImageRect.yMin + uv.y * newImageRect.height);
        _viewPan += evt.mousePosition - sameUvPosition;
        evt.Use();
        Repaint();
    }

    private void BeginDrag(Event evt)
    {
        if (!_lastPreviewArea.Contains(evt.mousePosition))
        {
            return;
        }

        if (evt.button == 1 || evt.button == 2)
        {
            _dragMode = DragMode.Pan;
            _dragStartMouse = evt.mousePosition;
            _dragStartPan = _viewPan;
            CaptureMouse();
            evt.Use();
            return;
        }

        if (evt.button != 0 || !_lastImageRect.Contains(evt.mousePosition))
        {
            return;
        }

        var clipGuiRect = WorldToGuiRect(GetSelectedClipRect().Rect, _lastImageRect);
        var corner = HitTestCorner(clipGuiRect, evt.mousePosition);
        if (corner >= 0)
        {
            _dragMode = DragMode.ResizeClip;
            _dragTarget = _selectedClipTarget;
            _dragCorner = corner;
            _dragStartMouse = evt.mousePosition;
            _dragStartClip = GetSelectedClipRect();
            CaptureMouse();
            evt.Use();
            return;
        }

        if (clipGuiRect.Contains(evt.mousePosition))
        {
            _dragMode = DragMode.MoveClip;
            _dragTarget = _selectedClipTarget;
            _dragStartMouse = evt.mousePosition;
            _dragStartClip = GetSelectedClipRect();
            CaptureMouse();
            evt.Use();
        }
    }

    private void ContinueDrag(Event evt)
    {
        if (_dragMode == DragMode.None)
        {
            return;
        }

        if (_dragMode == DragMode.Pan)
        {
            _viewPan = _dragStartPan + evt.mousePosition - _dragStartMouse;
        }
        else if (_dragMode == DragMode.MoveClip)
        {
            var startWorld = GuiToWorld(_dragStartMouse, _lastImageRect);
            var currentWorld = GuiToWorld(evt.mousePosition, _lastImageRect);
            var delta = currentWorld - startWorld;
            SetClipRect(_dragTarget, new SbScenePartnerResultBuilder.PartnerResultClipRect(
                _dragStartClip.CenterX + delta.x,
                _dragStartClip.CenterY + delta.y,
                _dragStartClip.Size));
        }
        else if (_dragMode == DragMode.ResizeClip)
        {
            ResizeClip(evt.mousePosition);
        }

        evt.Use();
        Repaint();
    }

    private void EndDrag(Event evt)
    {
        if (_dragMode == DragMode.None)
        {
            return;
        }

        _dragMode = DragMode.None;
        _dragTarget = ClipTarget.PartnerResult;
        _dragCorner = -1;
        if (_hotControl != 0)
        {
            GUIUtility.hotControl = 0;
            _hotControl = 0;
        }

        evt.Use();
        Repaint();
    }

    private void CaptureMouse()
    {
        _hotControl = GUIUtility.GetControlID(FocusType.Passive);
        GUIUtility.hotControl = _hotControl;
    }

    private int HitTestCorner(Rect clipGuiRect, Vector2 mousePosition)
    {
        var handles = GetCornerHandleRects(clipGuiRect);
        for (var index = 0; index < handles.Length; index++)
        {
            if (handles[index].Contains(mousePosition))
            {
                return index;
            }
        }

        return -1;
    }

    private void ResizeClip(Vector2 mousePosition)
    {
        var startRect = _dragStartClip.Rect;
        var currentWorld = GuiToWorld(mousePosition, _lastImageRect);
        Vector2 opposite;
        Vector2 direction;
        switch (_dragCorner)
        {
            case 0:
                opposite = new Vector2(startRect.xMax, startRect.yMin);
                direction = new Vector2(-1f, 1f);
                break;
            case 1:
                opposite = new Vector2(startRect.xMin, startRect.yMin);
                direction = new Vector2(1f, 1f);
                break;
            case 2:
                opposite = new Vector2(startRect.xMin, startRect.yMax);
                direction = new Vector2(1f, -1f);
                break;
            default:
                opposite = new Vector2(startRect.xMax, startRect.yMax);
                direction = new Vector2(-1f, -1f);
                break;
        }

        var size = Mathf.Max(
            Mathf.Abs(currentWorld.x - opposite.x),
            Mathf.Abs(currentWorld.y - opposite.y),
            MinClipSize);
        var center = opposite + direction * (size / 2f);
        SetClipRect(_dragTarget, new SbScenePartnerResultBuilder.PartnerResultClipRect(center.x, center.y, size));
    }

    private Rect WorldToGuiRect(Rect worldRect, Rect imageRect)
    {
        var xMin = Mathf.InverseLerp(_previewFrameRect.xMin, _previewFrameRect.xMax, worldRect.xMin);
        var xMax = Mathf.InverseLerp(_previewFrameRect.xMin, _previewFrameRect.xMax, worldRect.xMax);
        var yMin = Mathf.InverseLerp(_previewFrameRect.yMin, _previewFrameRect.yMax, worldRect.yMin);
        var yMax = Mathf.InverseLerp(_previewFrameRect.yMin, _previewFrameRect.yMax, worldRect.yMax);
        var guiXMin = imageRect.xMin + xMin * imageRect.width;
        var guiXMax = imageRect.xMin + xMax * imageRect.width;
        var guiYMin = imageRect.yMin + (1f - yMax) * imageRect.height;
        var guiYMax = imageRect.yMin + (1f - yMin) * imageRect.height;
        return Rect.MinMaxRect(guiXMin, guiYMin, guiXMax, guiYMax);
    }

    private Vector2 GuiToWorld(Vector2 guiPosition, Rect imageRect)
    {
        var x = imageRect.width > 0f ? (guiPosition.x - imageRect.xMin) / imageRect.width : 0.5f;
        var y = imageRect.height > 0f ? 1f - (guiPosition.y - imageRect.yMin) / imageRect.height : 0.5f;
        return new Vector2(
            Mathf.Lerp(_previewFrameRect.xMin, _previewFrameRect.xMax, x),
            Mathf.Lerp(_previewFrameRect.yMin, _previewFrameRect.yMax, y));
    }

    private Rect Intersect(Rect a, Rect b)
    {
        var xMin = Mathf.Max(a.xMin, b.xMin);
        var yMin = Mathf.Max(a.yMin, b.yMin);
        var xMax = Mathf.Min(a.xMax, b.xMax);
        var yMax = Mathf.Min(a.yMax, b.yMax);
        if (xMax <= xMin || yMax <= yMin)
        {
            return Rect.zero;
        }

        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    private void SetDefaultRect()
    {
        SetBothDefaultRects();
        Repaint();
    }

    private void CenterRectHorizontally()
    {
        var clipRect = GetSelectedClipRect();
        SetClipRect(_selectedClipTarget, new SbScenePartnerResultBuilder.PartnerResultClipRect(0f, clipRect.CenterY, clipRect.Size));
        Repaint();
    }

    private void SetBothDefaultRects()
    {
        _partnerResultClipRect = DefaultPartnerResultClipRect();
        _partnerClipRect = DefaultPartnerClipRect();
    }

    private SbScenePartnerResultBuilder.PartnerResultClipRect GetSelectedClipRect()
    {
        return GetClipRect(_selectedClipTarget);
    }

    private SbScenePartnerResultBuilder.PartnerResultClipRect GetClipRect(ClipTarget target)
    {
        return target == ClipTarget.PartnerResult ? _partnerResultClipRect : _partnerClipRect;
    }

    private void SetClipRect(ClipTarget target, SbScenePartnerResultBuilder.PartnerResultClipRect clipRect)
    {
        if (target == ClipTarget.PartnerResult)
        {
            _partnerResultClipRect = clipRect;
            return;
        }

        _partnerClipRect = clipRect;
    }

    private static SbScenePartnerResultBuilder.PartnerResultClipRect DefaultPartnerResultClipRect()
    {
        return new SbScenePartnerResultBuilder.PartnerResultClipRect(
            DefaultClipCenterX,
            DefaultPartnerResultClipCenterY,
            DefaultPartnerResultClipSize);
    }

    private static SbScenePartnerResultBuilder.PartnerResultClipRect DefaultPartnerClipRect()
    {
        return new SbScenePartnerResultBuilder.PartnerResultClipRect(
            DefaultClipCenterX,
            DefaultPartnerClipCenterY,
            DefaultPartnerClipSize);
    }

    private void ImportClipRectJson(TextAsset jsonAsset)
    {
        _clipRectJson = jsonAsset;
        if (_clipRectJson == null)
        {
            return;
        }

        var assetPath = AssetDatabase.GetAssetPath(_clipRectJson);
        if (!string.Equals(Path.GetExtension(assetPath), ".json", StringComparison.OrdinalIgnoreCase))
        {
            _errorMessage = "Clip Rect JSON must be a .json asset.";
            Repaint();
            return;
        }

        try
        {
            ApplyClipRectJson(_clipRectJson.text);
            _errorMessage = null;
        }
        catch (Exception ex)
        {
            _errorMessage = "Clip Rect JSON could not be imported: " + ex.Message;
        }

        Repaint();
    }

    private void SaveRect()
    {
        if (!HasValidPrefab)
        {
            return;
        }

        EnsureAssetFolder(ConfigDir);
        var defaultName = HasValidPrefab
            ? string.Format(CultureInfo.InvariantCulture, "PartnerResultClipRect_{0:D6}", _prefabId)
            : "PartnerResultClipRect";
        var configAssetPath = EditorUtility.SaveFilePanelInProject(
            "Save Clip Rect JSON",
            defaultName,
            "json",
            "Enter a Clip Rect JSON name.",
            ConfigDir);
        if (string.IsNullOrEmpty(configAssetPath))
        {
            return;
        }

        var config = new ClipRectPreset
        {
            partnerResult = CreateClipRectConfig(_partnerResultClipRect),
            partner = CreateClipRectConfig(_partnerClipRect),
        };
        var absolutePath = ToAbsoluteAssetPath(configAssetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath) ?? ".");
        File.WriteAllText(absolutePath, JsonUtility.ToJson(config, true), new UTF8Encoding(false));
        AssetDatabase.ImportAsset(configAssetPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh();
        _clipRectJson = AssetDatabase.LoadAssetAtPath<TextAsset>(configAssetPath);
        _errorMessage = null;
    }

    private void ApplyClipRectJson(string json)
    {
        ApplyClipRectPreset(JsonUtility.FromJson<ClipRectPreset>(json), JsonUtility.FromJson<ClipRectConfig>(json));
    }

    private void ApplyClipRectPreset(ClipRectPreset preset, ClipRectConfig legacyConfig)
    {
        if (preset == null)
        {
            throw new InvalidOperationException("Clip Rect JSON must contain centerX, centerY, and size.");
        }

        var applied = false;
        if (IsValidClipRectConfig(preset.partnerResult))
        {
            _partnerResultClipRect = ToClipRect(preset.partnerResult);
            applied = true;
        }

        if (IsValidClipRectConfig(preset.partner))
        {
            _partnerClipRect = ToClipRect(preset.partner);
            applied = true;
        }

        if (!applied && IsValidClipRectConfig(legacyConfig))
        {
            _partnerResultClipRect = ToClipRect(legacyConfig);
            applied = true;
        }

        if (!applied)
        {
            throw new InvalidOperationException("Clip Rect JSON must contain partnerResult/partner or root centerX, centerY, and size.");
        }
    }

    private static ClipRectConfig CreateClipRectConfig(SbScenePartnerResultBuilder.PartnerResultClipRect clipRect)
    {
        return new ClipRectConfig
        {
            centerX = clipRect.CenterX,
            centerY = clipRect.CenterY,
            size = Mathf.Max(clipRect.Size, MinClipSize),
        };
    }

    private static bool IsValidClipRectConfig(ClipRectConfig config)
    {
        return config != null && config.size > 0f;
    }

    private static SbScenePartnerResultBuilder.PartnerResultClipRect ToClipRect(ClipRectConfig config)
    {
        return new SbScenePartnerResultBuilder.PartnerResultClipRect(
            config.centerX,
            config.centerY,
            Mathf.Max(config.size, MinClipSize));
    }

    private static void EnsureAssetFolder(string assetPath)
    {
        var normalized = assetPath.Replace('\\', '/').Trim('/');
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

    private void ResetView()
    {
        _viewZoom = 1f;
        _viewPan = Vector2.zero;
    }

    private void ClearPreviewTexture()
    {
        if (_previewTexture != null)
        {
            DestroyImmediate(_previewTexture);
            _previewTexture = null;
        }
    }

    private static string ToAbsoluteAssetPath(string assetPath)
    {
        return Path.GetFullPath(Path.Combine(ProjectRoot, assetPath)).TrimEnd('/', '\\');
    }

    private static string ProjectRoot
    {
        get { return Path.GetFullPath(Path.GetDirectoryName(Application.dataPath) ?? ".").TrimEnd('/', '\\'); }
    }

    private bool HasValidPrefab
    {
        get { return _prefab != null && !string.IsNullOrEmpty(_prefabAssetPath) && _prefabId > 0; }
    }

    private enum DragMode
    {
        None = 0,
        Pan = 1,
        MoveClip = 2,
        ResizeClip = 3,
    }

    private enum ClipTarget
    {
        PartnerResult = 0,
        Partner = 1,
    }

    [Serializable]
    private sealed class ClipRectPreset
    {
        public ClipRectConfig partnerResult;
        public ClipRectConfig partner;
    }

    [Serializable]
    private class ClipRectConfig
    {
        public float centerX;
        public float centerY;
        public float size;
    }
}
#endif
