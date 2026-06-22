#if UNITY_EDITOR
using System;
using System.Collections.Generic;
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
    private const float ClipPreviewStripHeight = 156f;
    private const float ClipPreviewSize = 128f;
    private const int MaxDisplayedBatchIds = 8;
    private static readonly string[] ClipTargetLabels = { "PartnerResult", "Partner" };
    private static readonly Color PartnerResultClipColor = new Color(0.25f, 0.85f, 1f, 1f);
    private static readonly Color PartnerClipColor = new Color(1f, 0.66f, 0.20f, 1f);

    private readonly List<SelectedPrefab> _selectedPrefabs = new List<SelectedPrefab>();
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
        DrawClipPreviews();
    }

    private void DrawHeader()
    {
        EditorGUILayout.Space();
        EditorGUI.BeginChangeCheck();
        var selected = (GameObject)EditorGUILayout.ObjectField("Prefab", PreviewPrefabObject, typeof(GameObject), false);
        if (EditorGUI.EndChangeCheck())
        {
            SelectPrefab(selected);
        }

        DrawPrefabBatchDropZone();

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
            var previewPrefab = PreviewPrefab;
            EditorGUILayout.LabelField(
                "Selected",
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} prefab(s), preview {1}  ID {2:D6}",
                    _selectedPrefabs.Count,
                    previewPrefab.AssetPath,
                    previewPrefab.Id));
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

    private void DrawPrefabBatchDropZone()
    {
        var dropRect = GUILayoutUtility.GetRect(
            240f,
            100000f,
            42f,
            42f,
            GUILayout.ExpandWidth(true),
            GUILayout.ExpandHeight(false));
        var isDragOver = dropRect.Contains(Event.current.mousePosition)
            && (Event.current.type == EventType.DragUpdated || Event.current.type == EventType.DragPerform);
        EditorGUI.DrawRect(dropRect, isDragOver ? new Color(0.20f, 0.26f, 0.32f, 1f) : new Color(0.16f, 0.16f, 0.16f, 1f));
        DrawBorder(dropRect, isDragOver ? new Color(0.45f, 0.72f, 1f, 1f) : new Color(0.32f, 0.32f, 0.32f, 1f), 1f);

        var style = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            clipping = TextClipping.Clip,
            wordWrap = false,
        };
        GUI.Label(dropRect, BuildBatchDropZoneText(), style);
        HandlePrefabDrag(dropRect);
    }

    private string BuildBatchDropZoneText()
    {
        if (!HasValidPrefab)
        {
            return "Batch Prefab Drop Zone: drop one or more UI_Navichara prefabs";
        }

        var builder = new StringBuilder();
        builder.AppendFormat(CultureInfo.InvariantCulture, "Batch: {0} prefab(s)", _selectedPrefabs.Count);
        builder.Append(" | IDs ");
        for (var index = 0; index < _selectedPrefabs.Count && index < MaxDisplayedBatchIds; index++)
        {
            if (index > 0)
            {
                builder.Append(", ");
            }

            builder.AppendFormat(CultureInfo.InvariantCulture, "{0:D6}", _selectedPrefabs[index].Id);
        }

        if (_selectedPrefabs.Count > MaxDisplayedBatchIds)
        {
            builder.Append(", ...");
        }

        builder.AppendFormat(CultureInfo.InvariantCulture, " | Preview {0:D6}", PreviewPrefab.Id);
        return builder.ToString();
    }

    private void SelectPrefab(GameObject prefab)
    {
        _errorMessage = null;
        ClearPreviewTexture();
        _selectedPrefabs.Clear();
        if (prefab == null)
        {
            Repaint();
            return;
        }

        if (!TryCreateSelectedPrefab(prefab, out var selectedPrefab, out var error))
        {
            _errorMessage = error;
            Repaint();
            return;
        }

        _selectedPrefabs.Add(selectedPrefab);
        ResetView();
        RefreshPreview();
    }

    private void SelectPrefabs(List<SelectedPrefab> prefabs)
    {
        _errorMessage = null;
        ClearPreviewTexture();
        _selectedPrefabs.Clear();
        if (prefabs == null || prefabs.Count == 0)
        {
            _errorMessage = "Drop at least one valid UI_Navichara prefab.";
            Repaint();
            return;
        }

        _selectedPrefabs.AddRange(prefabs);
        ResetView();
        RefreshPreview();
    }

    private void RefreshPreview()
    {
        if (!HasValidPrefab)
        {
            return;
        }

        var previewPrefab = PreviewPrefab;
        ClearPreviewTexture();
        try
        {
            var preview = SbScenePartnerResultBuilder.RenderPartnerResultPreview(previewPrefab.AssetPath, PreviewTextureSize);
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

        var results = new List<PrefabGenerationResult>();
        var selectedPrefabs = new List<SelectedPrefab>(_selectedPrefabs);
        selectedPrefabs.Sort(CompareSelectedPrefabs);
        foreach (var selectedPrefab in selectedPrefabs)
        {
            try
            {
                var result = SbScenePartnerResultBuilder.GeneratePartnerAndResultFromClips(
                    selectedPrefab.AssetPath,
                    _partnerResultClipRect,
                    _partnerClipRect);
                results.Add(PrefabGenerationResult.CreateSummary(selectedPrefab, result));
            }
            catch (Exception ex)
            {
                results.Add(PrefabGenerationResult.CreateFailure(selectedPrefab, ex.Message));
                Debug.LogException(ex);
            }
        }

        _errorMessage = HasGenerationFailure(results) ? BuildGenerationError(results) : null;
        EditorUtility.DisplayDialog(
            "PartnerResult Clip Editor",
            BuildGenerationMessage(results),
            "OK");
    }

    private static string BuildGenerationMessage(List<PrefabGenerationResult> results)
    {
        var builder = new StringBuilder();
        var failedCount = CountGenerationFailures(results);
        if (failedCount > 0)
        {
            builder.AppendFormat(
                CultureInfo.InvariantCulture,
                "Finished batch with failures: {0}/{1} prefab(s) succeeded.",
                results.Count - failedCount,
                results.Count);
        }
        else
        {
            builder.AppendFormat(
                CultureInfo.InvariantCulture,
                "Generated resources for {0} prefab(s).",
                results.Count);
        }

        foreach (var result in results)
        {
            builder.AppendLine();
            builder.AppendLine();
            AppendPrefabGenerationResult(builder, result);
        }

        return builder.ToString();
    }

    private static string BuildGenerationError(List<PrefabGenerationResult> results)
    {
        var builder = new StringBuilder();
        foreach (var result in results)
        {
            if (result.ExceptionMessage != null)
            {
                builder.AppendFormat(CultureInfo.InvariantCulture, "{0:D6}: {1}", result.Prefab.Id, result.ExceptionMessage);
                builder.AppendLine();
                continue;
            }

            if (!result.Summary.PartnerResult.Success)
            {
                builder.AppendFormat(
                    CultureInfo.InvariantCulture,
                    "{0:D6} {1}: {2}",
                    result.Prefab.Id,
                    result.Summary.PartnerResult.Label,
                    result.Summary.PartnerResult.ErrorMessage);
                builder.AppendLine();
            }

            if (!result.Summary.Partner.Success)
            {
                builder.AppendFormat(
                    CultureInfo.InvariantCulture,
                    "{0:D6} {1}: {2}",
                    result.Prefab.Id,
                    result.Summary.Partner.Label,
                    result.Summary.Partner.ErrorMessage);
                builder.AppendLine();
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static void AppendPrefabGenerationResult(StringBuilder builder, PrefabGenerationResult result)
    {
        builder.AppendFormat(
            CultureInfo.InvariantCulture,
            "[{0:D6}] {1}",
            result.Prefab.Id,
            result.Prefab.AssetPath);
        builder.AppendLine();
        if (result.ExceptionMessage != null)
        {
            builder.Append("Failed: ").Append(result.ExceptionMessage);
            return;
        }

        AppendGenerationItem(builder, result.Summary.PartnerResult);
        builder.AppendLine();
        AppendGenerationItem(builder, result.Summary.Partner);
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

    private void DrawClipPreviews()
    {
        var stripRect = GUILayoutUtility.GetRect(
            240f,
            100000f,
            ClipPreviewStripHeight,
            ClipPreviewStripHeight,
            GUILayout.ExpandWidth(true),
            GUILayout.ExpandHeight(false));
        EditorGUI.DrawRect(stripRect, new Color(0.10f, 0.10f, 0.10f, 1f));

        var gap = 16f;
        var totalWidth = ClipPreviewSize * 2f + gap;
        var startX = stripRect.center.x - totalWidth / 2f;
        var previewY = stripRect.yMin + 20f;
        DrawClipPreviewTile(
            new Rect(startX, previewY, ClipPreviewSize, ClipPreviewSize),
            ClipTarget.PartnerResult,
            _partnerResultClipRect);
        DrawClipPreviewTile(
            new Rect(startX + ClipPreviewSize + gap, previewY, ClipPreviewSize, ClipPreviewSize),
            ClipTarget.Partner,
            _partnerClipRect);
    }

    private void DrawClipPreviewTile(
        Rect tileRect,
        ClipTarget target,
        SbScenePartnerResultBuilder.PartnerResultClipRect clipRect)
    {
        var color = GetClipColor(target);
        EditorGUI.DrawRect(tileRect, new Color(0.16f, 0.16f, 0.16f, 1f));
        if (_previewTexture == null || _previewFrameRect.width <= 0f || _previewFrameRect.height <= 0f)
        {
            DrawCenteredText(tileRect, "No preview");
        }
        else
        {
            var uvRect = WorldToTextureUvRect(clipRect.Rect);
            GUI.DrawTextureWithTexCoords(tileRect, _previewTexture, uvRect, true);
        }

        DrawBorder(tileRect, color, target == _selectedClipTarget ? 2f : 1f);
        DrawClipPreviewLabel(tileRect, target, color);
    }

    private static void DrawClipPreviewLabel(Rect tileRect, ClipTarget target, Color color)
    {
        var labelRect = new Rect(tileRect.xMin, tileRect.yMin - 18f, tileRect.width, 16f);
        var style = new GUIStyle(EditorStyles.miniBoldLabel)
        {
            normal = { textColor = color },
            alignment = TextAnchor.MiddleCenter,
            clipping = TextClipping.Clip,
        };
        GUI.Label(labelRect, ClipTargetLabels[(int)target], style);
    }

    private Rect WorldToTextureUvRect(Rect worldRect)
    {
        var xMin = Mathf.InverseLerp(_previewFrameRect.xMin, _previewFrameRect.xMax, worldRect.xMin);
        var xMax = Mathf.InverseLerp(_previewFrameRect.xMin, _previewFrameRect.xMax, worldRect.xMax);
        var yMin = Mathf.InverseLerp(_previewFrameRect.yMin, _previewFrameRect.yMax, worldRect.yMin);
        var yMax = Mathf.InverseLerp(_previewFrameRect.yMin, _previewFrameRect.yMax, worldRect.yMax);
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
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

        var draggedPrefabs = FindDraggedPrefabs(out _);
        if (draggedPrefabs.Count == 0)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
            if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                SelectPrefabs(draggedPrefabs);
                evt.Use();
            }

            return;
        }

        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        if (evt.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            SelectPrefabs(FindDraggedPrefabs(out _));
        }

        evt.Use();
    }

    private List<SelectedPrefab> FindDraggedPrefabs(out int invalidCount)
    {
        invalidCount = 0;
        var candidates = new List<SelectedPrefab>();
        foreach (var draggedObject in DragAndDrop.objectReferences)
        {
            var gameObject = draggedObject as GameObject;
            if (gameObject == null)
            {
                invalidCount++;
                continue;
            }

            if (TryCreateSelectedPrefab(gameObject, out var selectedPrefab, out _))
            {
                candidates.Add(selectedPrefab);
                continue;
            }

            invalidCount++;
        }

        candidates.Sort(CompareSelectedPrefabs);
        var selectedPrefabs = new List<SelectedPrefab>();
        var seenIds = new HashSet<int>();
        foreach (var candidate in candidates)
        {
            if (seenIds.Add(candidate.Id))
            {
                selectedPrefabs.Add(candidate);
            }
        }

        return selectedPrefabs;
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
        var previewPrefab = PreviewPrefab;
        var defaultName = HasValidPrefab
            ? string.Format(CultureInfo.InvariantCulture, "PartnerResultClipRect_{0:D6}", previewPrefab.Id)
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

    private static bool TryCreateSelectedPrefab(GameObject prefab, out SelectedPrefab selectedPrefab, out string error)
    {
        selectedPrefab = null;
        error = null;
        if (prefab == null)
        {
            error = "Select a prefab.";
            return false;
        }

        var assetPath = AssetDatabase.GetAssetPath(prefab);
        if (!SbScenePartnerResultBuilder.TryGetNavicharaPrefabInfo(assetPath, out var id, out error))
        {
            return false;
        }

        selectedPrefab = new SelectedPrefab(prefab, assetPath, id);
        return true;
    }

    private static int CompareSelectedPrefabs(SelectedPrefab left, SelectedPrefab right)
    {
        var idComparison = left.Id.CompareTo(right.Id);
        if (idComparison != 0)
        {
            return idComparison;
        }

        return string.Compare(left.AssetPath, right.AssetPath, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasGenerationFailure(List<PrefabGenerationResult> results)
    {
        return CountGenerationFailures(results) > 0;
    }

    private static int CountGenerationFailures(List<PrefabGenerationResult> results)
    {
        var count = 0;
        foreach (var result in results)
        {
            if (result.HasFailure)
            {
                count++;
            }
        }

        return count;
    }

    private static string ProjectRoot
    {
        get { return Path.GetFullPath(Path.GetDirectoryName(Application.dataPath) ?? ".").TrimEnd('/', '\\'); }
    }

    private SelectedPrefab PreviewPrefab
    {
        get { return _selectedPrefabs.Count > 0 ? _selectedPrefabs[0] : null; }
    }

    private GameObject PreviewPrefabObject
    {
        get { return PreviewPrefab != null ? PreviewPrefab.Prefab : null; }
    }

    private bool HasValidPrefab
    {
        get { return PreviewPrefab != null; }
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

    private sealed class SelectedPrefab
    {
        public SelectedPrefab(GameObject prefab, string assetPath, int id)
        {
            Prefab = prefab;
            AssetPath = assetPath;
            Id = id;
        }

        public GameObject Prefab { get; }

        public string AssetPath { get; }

        public int Id { get; }
    }

    private sealed class PrefabGenerationResult
    {
        private PrefabGenerationResult(
            SelectedPrefab prefab,
            SbScenePartnerResultBuilder.PartnerClipGenerationSummary summary,
            string exceptionMessage)
        {
            Prefab = prefab;
            Summary = summary;
            ExceptionMessage = exceptionMessage;
        }

        public SelectedPrefab Prefab { get; }

        public SbScenePartnerResultBuilder.PartnerClipGenerationSummary Summary { get; }

        public string ExceptionMessage { get; }

        public bool HasFailure
        {
            get { return ExceptionMessage != null || Summary.HasFailure; }
        }

        public static PrefabGenerationResult CreateSummary(
            SelectedPrefab prefab,
            SbScenePartnerResultBuilder.PartnerClipGenerationSummary summary)
        {
            return new PrefabGenerationResult(prefab, summary, null);
        }

        public static PrefabGenerationResult CreateFailure(SelectedPrefab prefab, string exceptionMessage)
        {
            return new PrefabGenerationResult(prefab, null, exceptionMessage);
        }
    }
}
#endif
