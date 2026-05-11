#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

using GradientGraphic = Control.Gradient;
using Object = UnityEngine.Object;

[CustomEditor(typeof(GradientGraphic))]
[CanEditMultipleObjects]
public sealed class GradientEditor : Editor
{
    private const float HandleTolerance = 0.001f;
    private const int InspectorPreviewTextureWidth = 256;
    private const int InspectorPreviewTextureHeight = 32;

    private static readonly Color LineColor = new(0.2f, 0.75f, 1f, 1f);
    private static readonly Color StartColor = new(0.35f, 0.95f, 1f, 1f);
    private static readonly Color EndColor = new(1f, 0.9f, 0.25f, 1f);
    private static readonly Color SquashColor = new(0.9f, 0.45f, 1f, 1f);

    private Texture2D inspectorPreviewTexture;
    private VisualElement inspectorPreviewElement;

    public override VisualElement CreateInspectorGUI()
    {
        serializedObject.Update();

        VisualElement root = new();
        root.style.paddingTop = 4;
        root.style.paddingBottom = 4;

        root.Add(CreateInspectorGradientPreview());

        root.Add(new PropertyField(serializedObject.FindProperty("type")));
        root.Add(CreateVector2Field("start", "Start", "Change Gradient Start", (gradient, value) => gradient.StartPoint = value));
        root.Add(CreateVector2Field("end", "End", "Change Gradient End", (gradient, value) => gradient.EndPoint = value));

        VisualElement commandRow = new();
        commandRow.style.flexDirection = FlexDirection.Row;
        commandRow.style.marginTop = 2;
        commandRow.style.marginBottom = 2;

        Button flipButton = new(() => ApplyToTargets("Flip Gradient Start / End", gradient => gradient.FlipStartAndEnd()))
        {
            text = "Flip Start / End"
        };
        flipButton.style.flexGrow = 1;
        flipButton.style.marginRight = 2;

        Button rotateButton = new(() => ApplyToTargets("Rotate Gradient 90 Clockwise", gradient => gradient.RotateStartAndEndClockwise()))
        {
            text = "Rotate 90 Clockwise"
        };
        rotateButton.style.flexGrow = 1;
        rotateButton.style.marginLeft = 2;

        commandRow.Add(flipButton);
        commandRow.Add(rotateButton);
        root.Add(commandRow);

        root.Add(CreateVector2Field("squash", "Squash", "Change Gradient Squash", (gradient, value) => gradient.SquashPoint = value));

        Button resetSquashButton = new(() => ApplyToTargets("Reset Gradient Squash Perpendicular", gradient => gradient.ResetSquashPerpendicular()))
        {
            text = "Reset Squash Perpendicular"
        };
        resetSquashButton.style.height = 22;
        root.Add(resetSquashButton);

        AddPropertyIfFound(root, "m_Color");
        AddPropertyIfFound(root, "m_RaycastTarget");
        AddPropertyIfFound(root, "m_Maskable");
        AddPropertyIfFound(root, "showHandles", "Show Handles");

        root.Bind(serializedObject);
        return root;
    }

    private void OnDisable()
    {
        if (inspectorPreviewTexture == null)
            return;

        DestroyImmediate(inspectorPreviewTexture);
        inspectorPreviewTexture = null;
    }

    private void OnSceneGUI()
    {
        if (target is not GradientGraphic gradient || !gradient.ShowHandles)
            return;

        RectTransform rectTransform = gradient.rectTransform;
        Vector3 startWorld = rectTransform.TransformPoint(gradient.NormalizedToLocalPoint(gradient.StartPoint));
        Vector3 endWorld = rectTransform.TransformPoint(gradient.NormalizedToLocalPoint(gradient.EndPoint));

        Handles.color = LineColor;
        Handles.DrawAAPolyLine(3f, startWorld, endWorld);

        Vector3 movedStartWorld = DrawPointHandle(rectTransform, startWorld, StartColor);
        Vector3 movedEndWorld = DrawPointHandle(rectTransform, endWorld, EndColor);

        bool startChanged = movedStartWorld != startWorld;
        bool endChanged = movedEndWorld != endWorld;

        if (startChanged || endChanged)
        {
            Undo.RecordObject(gradient, "Move Gradient Handle");

            if (startChanged)
                gradient.SetStartPoint(WorldToNormalized(rectTransform, gradient, movedStartWorld));

            if (endChanged)
                gradient.SetEndPoint(WorldToNormalized(rectTransform, gradient, movedEndWorld));

            EditorUtility.SetDirty(gradient);
        }

        DrawSquashHandle(gradient, rectTransform);
    }

    private VisualElement CreateVector2Field(string propertyName, string label, string undoName, Action<GradientGraphic, Vector2> applyValue)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        Vector2Field field = new(label);

        if (property != null)
        {
            field.showMixedValue = property.hasMultipleDifferentValues;
            field.SetValueWithoutNotify(property.vector2Value);
        }

        field.RegisterValueChangedCallback(evt =>
        {
            serializedObject.Update();
            ApplyToTargets(undoName, gradient => applyValue(gradient, evt.newValue));
            serializedObject.Update();

            SerializedProperty refreshedProperty = serializedObject.FindProperty(propertyName);
            if (refreshedProperty == null)
                return;

            field.showMixedValue = refreshedProperty.hasMultipleDifferentValues;
            field.SetValueWithoutNotify(refreshedProperty.vector2Value);
        });

        return field;
    }

    private void AddPropertyIfFound(VisualElement root, string propertyName, string label = null)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            return;

        root.Add(label == null ? new PropertyField(property) : new PropertyField(property, label));
    }

    private void OpenGradientWindow()
    {
        if (target is GradientGraphic gradient)
            GradientEditorWindow.Open(gradient, GetInspectorPreviewScreenRect());
    }

    private VisualElement CreateInspectorGradientPreview()
    {
        inspectorPreviewElement = new VisualElement();
        inspectorPreviewElement.style.height = 40;
        inspectorPreviewElement.style.marginLeft = 1;
        inspectorPreviewElement.style.marginRight = 1;
        inspectorPreviewElement.style.marginBottom = 8;
        inspectorPreviewElement.style.borderTopLeftRadius = 4;
        inspectorPreviewElement.style.borderTopRightRadius = 4;
        inspectorPreviewElement.style.borderBottomLeftRadius = 4;
        inspectorPreviewElement.style.borderBottomRightRadius = 4;
        inspectorPreviewElement.style.overflow = Overflow.Hidden;
        inspectorPreviewElement.RegisterCallback<MouseDownEvent>(evt =>
        {
            if (evt.button != 0)
                return;

            OpenGradientWindow();
            evt.StopPropagation();
        });
        inspectorPreviewElement.RegisterCallback<GeometryChangedEvent>(_ => RefreshInspectorPreview());
        inspectorPreviewElement.schedule.Execute(RefreshInspectorPreview).Every(250);
        RefreshInspectorPreview();
        return inspectorPreviewElement;
    }

    private Rect GetInspectorPreviewScreenRect()
    {
        if (inspectorPreviewElement == null)
            return Rect.zero;

        Rect worldBound = inspectorPreviewElement.worldBound;
        foreach (EditorWindow editorWindow in Resources.FindObjectsOfTypeAll<EditorWindow>())
        {
            if (editorWindow == null || editorWindow.rootVisualElement?.panel != inspectorPreviewElement.panel)
                continue;

            Rect windowRect = editorWindow.position;
            return new Rect(
                windowRect.x + worldBound.xMin,
                windowRect.y + worldBound.yMin,
                worldBound.width,
                worldBound.height
            );
        }

        return Rect.zero;
    }

    private void RefreshInspectorPreview()
    {
        if (inspectorPreviewElement == null || target is not GradientGraphic gradient)
            return;

        if (inspectorPreviewTexture == null)
        {
            inspectorPreviewTexture = new Texture2D(InspectorPreviewTextureWidth, InspectorPreviewTextureHeight, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
        }

        UnityEngine.Gradient colorGradient = gradient.ColorGradient;
        for (int y = 0; y < InspectorPreviewTextureHeight; y++)
        {
            for (int x = 0; x < InspectorPreviewTextureWidth; x++)
            {
                float time = x / (float)(InspectorPreviewTextureWidth - 1);
                Color checkerColor = GradientEditorWindow.GetCheckerColor(x, y);
                Color gradientColor = colorGradient.Evaluate(time);
                Color composited = Color.Lerp(checkerColor, new Color(gradientColor.r, gradientColor.g, gradientColor.b, 1f), gradientColor.a);
                inspectorPreviewTexture.SetPixel(x, y, composited);
            }
        }

        inspectorPreviewTexture.Apply(false, false);
        inspectorPreviewElement.style.backgroundImage = new StyleBackground(inspectorPreviewTexture);
    }

    private static void DrawSquashHandle(GradientGraphic gradient, RectTransform rectTransform)
    {
        if (!TryGetSquashGeometry(gradient, out Vector2 startLocal, out Vector2 squashLocal, out Vector2 perpendicularLocal, out float axisLength))
            return;

        Vector2 projectedSquashLocal = ProjectOntoPerpendicularAxis(startLocal, squashLocal, perpendicularLocal);
        bool squashWasOffAxis = Vector2.Distance(projectedSquashLocal, squashLocal) > HandleTolerance;

        if (gradient.Type == GradientGraphic.GradientType.Linear && !squashWasOffAxis)
            return;

        Vector3 startWorld = rectTransform.TransformPoint(startLocal);
        Vector3 squashWorld = rectTransform.TransformPoint(squashLocal);
        Vector3 projectedSquashWorld = rectTransform.TransformPoint(projectedSquashLocal);

        Handles.color = SquashColor;
        Handles.DrawAAPolyLine(2f, startWorld, squashWorld);

        if (squashWasOffAxis)
            Handles.DrawAAPolyLine(1f, squashWorld, projectedSquashWorld);

        Vector3 movedSquashWorld = DrawAxisHandle(rectTransform, projectedSquashWorld, perpendicularLocal, SquashColor);
        if (movedSquashWorld == projectedSquashWorld)
            return;

        Undo.RecordObject(gradient, "Move Gradient Squash Handle");

        Vector2 movedSquashLocal = rectTransform.InverseTransformPoint(movedSquashWorld);
        Vector2 constrainedSquashLocal = ProjectOntoPerpendicularAxis(startLocal, movedSquashLocal, perpendicularLocal);
        gradient.SquashPoint = gradient.LocalPointToNormalized(constrainedSquashLocal);

        EditorUtility.SetDirty(gradient);
    }

    private static bool TryGetSquashGeometry(GradientGraphic gradient, out Vector2 startLocal, out Vector2 squashLocal, out Vector2 perpendicularLocal, out float axisLength)
    {
        startLocal = gradient.NormalizedToLocalPoint(gradient.StartPoint);
        Vector2 endLocal = gradient.NormalizedToLocalPoint(gradient.EndPoint);
        squashLocal = gradient.NormalizedToLocalPoint(gradient.SquashPoint);

        Vector2 directionLocal = endLocal - startLocal;
        axisLength = directionLocal.magnitude;
        if (axisLength <= Mathf.Epsilon)
        {
            perpendicularLocal = Vector2.zero;
            return false;
        }

        perpendicularLocal = new Vector2(-directionLocal.y / axisLength, directionLocal.x / axisLength);
        if (Vector2.Dot(squashLocal - startLocal, perpendicularLocal) < 0f)
            perpendicularLocal = -perpendicularLocal;

        return true;
    }

    private static Vector2 ProjectOntoPerpendicularAxis(Vector2 startLocal, Vector2 localPoint, Vector2 perpendicularLocal)
    {
        float projectedLength = Vector2.Dot(localPoint - startLocal, perpendicularLocal);
        return startLocal + perpendicularLocal * projectedLength;
    }

    private static Vector3 DrawPointHandle(RectTransform rectTransform, Vector3 worldPosition, Color handleColor)
    {
        float handleSize = HandleUtility.GetHandleSize(worldPosition) * 0.08f;

        Handles.color = handleColor;
        return Handles.Slider2D(
            worldPosition,
            rectTransform.forward,
            rectTransform.right,
            rectTransform.up,
            handleSize,
            Handles.CircleHandleCap,
            Vector2.zero,
            false
        );
    }

    private static Vector3 DrawAxisHandle(RectTransform rectTransform, Vector3 worldPosition, Vector2 localAxis, Color handleColor)
    {
        float handleSize = HandleUtility.GetHandleSize(worldPosition) * 0.08f;
        Vector3 worldAxis = rectTransform.TransformVector(new Vector3(localAxis.x, localAxis.y, 0f)).normalized;

        Handles.color = handleColor;
        return Handles.Slider(
            worldPosition,
            worldAxis,
            handleSize,
            Handles.CircleHandleCap,
            0f
        );
    }

    private static Vector2 WorldToNormalized(RectTransform rectTransform, GradientGraphic gradient, Vector3 worldPosition)
    {
        Vector2 localPoint = rectTransform.InverseTransformPoint(worldPosition);
        return gradient.LocalPointToNormalized(localPoint);
    }

    private void ApplyToTargets(string undoName, Action<GradientGraphic> action)
    {
        foreach (Object selectedTarget in targets)
        {
            if (selectedTarget is not GradientGraphic gradient)
                continue;

            Undo.RecordObject(gradient, undoName);
            action(gradient);
            EditorUtility.SetDirty(gradient);
        }
    }
}

public sealed class GradientEditorWindow : EditorWindow
{
    private const int PreviewTextureWidth = 343;
    private const int PreviewTextureHeight = 48;
    private const float StopMarkerSize = 12f;

    private const string IconDirectory = "Packages/com.control-tools.ui-gradient/Editor/Icons/";
    private const string PlusIconPath = IconDirectory + "PlusIcon.svg";
    private const string MinusIconPath = IconDirectory + "MinusIcon.svg";
    private const string AlphaIconPath = IconDirectory + "AlphaIcon.svg";
    private const string PositionIconPath = IconDirectory + "PositionIcon.svg";

    private static readonly List<string> InterpolationOptions = new() { "Linear" };
    private static readonly Color WindowBackground = ColorFromHex(0x3C, 0x3C, 0x3C);
    private static readonly Color DropdownBackground = ColorFromHex(0x58, 0x58, 0x58);
    private static readonly Color BorderColor = ColorFromHex(0x24, 0x24, 0x24);
    private static readonly Color IconColor = ColorFromHex(0xD9, 0xD9, 0xD9);
    private static readonly Color RowSelectionColor = new(1f, 1f, 1f, 0.08f);
    private static readonly PropertyInfo CursorDefaultCursorIdProperty = typeof(UnityEngine.UIElements.Cursor).GetProperty(
        "defaultCursorId",
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
    );

    [SerializeField] private GradientGraphic gradientTarget;

    private readonly List<GradientStopModel> stops = new();

    private Texture2D previewTexture;
    private VisualElement contentRoot;
    private VisualElement previewElement;
    private VisualElement stopOverlay;
    private VisualElement rowsRoot;
    private PopupField<string> interpolationField;
    private GradientStopModel selectedStop;
    private static Texture2D controlLogoTexture;

    public static void Open(GradientGraphic gradient, Rect anchorScreenRect)
    {
        foreach (GradientEditorWindow openWindow in Resources.FindObjectsOfTypeAll<GradientEditorWindow>())
            openWindow.Close();

        GradientEditorWindow window = CreateInstance<GradientEditorWindow>();
        window.titleContent = new GUIContent("Gradient Editor", GetControlLogoTexture());
        window.minSize = new Vector2(360f, 190f);
        window.position = GetAnchorAdjacentPosition(anchorScreenRect, window.minSize);
        window.SetTarget(gradient);
        window.ShowAuxWindow();
        window.position = GetAnchorAdjacentPosition(anchorScreenRect, window.position.size);
        window.Focus();
    }

    private void OnEnable()
    {
        titleContent = new GUIContent("Gradient Editor", GetControlLogoTexture());
        minSize = new Vector2(360f, 190f);
        BuildWindow();
    }

    private void OnDisable()
    {
        DestroyPreviewTexture();
    }

    private void OnSelectionChange()
    {
        if (gradientTarget != null)
            return;

        GradientGraphic selectedGradient = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponent<GradientGraphic>()
            : null;

        if (selectedGradient != null)
            SetTarget(selectedGradient);
    }

    private void SetTarget(GradientGraphic gradient)
    {
        gradientTarget = gradient;
        LoadStopsFromTarget();
        BuildWindow();
    }

    private void BuildWindow()
    {
        rootVisualElement.Clear();
        rootVisualElement.style.backgroundColor = WindowBackground;

        VisualElement shell = new();
        shell.style.flexGrow = 1;
        shell.style.backgroundColor = WindowBackground;
        SetBorder(shell, new Color(0f, 0f, 0f, 0.5f), 1f);
        rootVisualElement.Add(shell);

        contentRoot = new VisualElement();
        contentRoot.style.flexGrow = 1;
        contentRoot.style.paddingTop = 12;
        contentRoot.style.paddingBottom = 12;
        contentRoot.style.flexDirection = FlexDirection.Column;
        shell.Add(contentRoot);

        if (gradientTarget == null)
        {
            Label missingTarget = CreateText("Select a Control Gradient to edit.", 12, Color.white);
            missingTarget.style.marginLeft = 12;
            missingTarget.style.marginTop = 4;
            contentRoot.Add(missingTarget);
            return;
        }

        contentRoot.Add(CreateModeRow());
        contentRoot.Add(CreatePreviewRow());
        contentRoot.Add(CreateColorsHeader());

        rowsRoot = new VisualElement();
        contentRoot.Add(rowsRoot);

        RefreshPreviewTexture();
        RefreshStopRows();
        RefreshStopMarkers();
    }

    private VisualElement CreateModeRow()
    {
        VisualElement row = CreateAttributeRow();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.paddingLeft = 12;
        row.style.paddingRight = 12;

        Label label = CreateText("Interpolation", 12, Color.white);
        label.style.width = 144;
        row.Add(label);

        interpolationField = new PopupField<string>(InterpolationOptions, 0);
        interpolationField.style.flexGrow = 1;
        interpolationField.style.height = 22;
        interpolationField.style.backgroundColor = DropdownBackground;
        SetBorder(interpolationField, BorderColor, 1f);
        StripFieldChrome(interpolationField);
        row.Add(interpolationField);

        return row;
    }

    private VisualElement CreatePreviewRow()
    {
        VisualElement row = new();
        row.style.height = 48;
        row.style.marginLeft = 9;
        row.style.marginRight = 8;
        row.style.marginTop = 4;
        row.style.marginBottom = 4;
        row.style.position = Position.Relative;
        row.RegisterCallback<GeometryChangedEvent>(_ => RefreshStopMarkers());
        row.RegisterCallback<MouseDownEvent>(OnPreviewMouseDown);

        previewElement = new VisualElement();
        previewElement.pickingMode = PickingMode.Ignore;
        previewElement.style.position = Position.Absolute;
        previewElement.style.left = 0;
        previewElement.style.top = 0;
        previewElement.style.right = 0;
        previewElement.style.bottom = 0;
        previewElement.style.borderTopLeftRadius = 4;
        previewElement.style.borderTopRightRadius = 4;
        previewElement.style.borderBottomLeftRadius = 4;
        previewElement.style.borderBottomRightRadius = 4;
        row.Add(previewElement);

        stopOverlay = new VisualElement();
        stopOverlay.style.position = Position.Absolute;
        stopOverlay.style.left = 0;
        stopOverlay.style.top = 0;
        stopOverlay.style.right = 0;
        stopOverlay.style.bottom = 0;
        row.Add(stopOverlay);

        return row;
    }

    private VisualElement CreateColorsHeader()
    {
        VisualElement row = CreateAttributeRow();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.paddingLeft = 12;
        row.style.paddingRight = 5;

        Label label = CreateText("Colors", 12, Color.white);
        label.style.flexGrow = 1;
        row.Add(label);

        row.Add(CreateIconButton(PlusIconPath, "+", AddStop));
        return row;
    }

    private VisualElement CreateAttributeRow()
    {
        VisualElement row = new();
        row.style.height = 24;
        row.style.backgroundColor = WindowBackground;
        row.style.flexShrink = 0;
        return row;
    }

    private void RefreshStopRows()
    {
        if (rowsRoot == null)
            return;

        rowsRoot.Clear();
        SortStops();

        foreach (GradientStopModel stop in stops)
            rowsRoot.Add(CreateStopRow(stop));
    }

    private VisualElement CreateStopRow(GradientStopModel stop)
    {
        VisualElement row = CreateAttributeRow();
        row.style.position = Position.Relative;
        row.style.backgroundColor = stop == selectedStop ? RowSelectionColor : WindowBackground;
        row.RegisterCallback<MouseDownEvent>(_ => SetSelectedStop(stop));

        VisualElement handle = new();
        handle.style.position = Position.Absolute;
        handle.style.left = 12;
        handle.style.top = 6;
        handle.style.width = 12;
        handle.style.height = 12;
        handle.style.alignItems = Align.Center;
        handle.style.justifyContent = Justify.Center;
        VisualElement positionIcon = CreateIcon(PositionIconPath, "|");
        positionIcon.pickingMode = PickingMode.Ignore;
        handle.Add(positionIcon);
        row.Add(handle);

        FloatField positionField = CreateNumberField(stop.time * 100f);
        positionField.style.left = 28;
        AttachNumberFieldDragger(positionField, handle);
        positionField.RegisterValueChangedCallback(evt =>
        {
            SetSelectedStop(stop, false, false);
            float value = ClampNumberField(positionField, evt.newValue);
            SetStopTime(stop, value / 100f, "Change Gradient Stop Position", false, true);
        });
        positionField.RegisterCallback<MouseUpEvent>(_ => RefreshStopRows(), TrickleDown.TrickleDown);
        positionField.RegisterCallback<FocusOutEvent>(_ => RefreshStopRows(), TrickleDown.TrickleDown);
        row.Add(positionField);

        ColorField colorField = new()
        {
            label = string.Empty,
            showAlpha = false,
            showEyeDropper = true,
            hdr = false,
            value = new Color(stop.color.r, stop.color.g, stop.color.b, 1f)
        };
        colorField.style.position = Position.Absolute;
        colorField.style.left = 83;
        colorField.style.top = 3;
        colorField.style.width = 170;
        colorField.style.height = 18;
        colorField.style.backgroundColor = Color.clear;
        colorField.style.overflow = Overflow.Hidden;
        SetBorder(colorField, BorderColor, 1f);
        StripFieldChrome(colorField);
        colorField.RegisterValueChangedCallback(evt =>
        {
            SetSelectedStop(stop, false, false);
            Color color = evt.newValue;
            color.a = stop.color.a;
            stop.color = color;
            ApplyStopsToTarget("Change Gradient Stop Color");
            RefreshPreviewTexture();
            RefreshStopMarkers();
        });
        row.Add(colorField);

        VisualElement alphaIcon = CreateIcon(AlphaIconPath, "A");
        alphaIcon.style.position = Position.Absolute;
        alphaIcon.style.left = 265;
        alphaIcon.style.top = 6;
        alphaIcon.style.width = 12;
        alphaIcon.style.height = 12;
        row.Add(alphaIcon);

        FloatField alphaField = CreateNumberField(stop.color.a * 100f);
        alphaField.style.left = 281;
        AttachNumberFieldDragger(alphaField, alphaIcon);
        alphaField.RegisterValueChangedCallback(evt =>
        {
            SetSelectedStop(stop, false, false);
            float value = ClampNumberField(alphaField, evt.newValue);
            stop.color = new Color(stop.color.r, stop.color.g, stop.color.b, Mathf.Clamp01(value / 100f));
            ApplyStopsToTarget("Change Gradient Stop Opacity");
            RefreshPreviewTexture();
            RefreshStopMarkers();
        });
        row.Add(alphaField);

        Button removeButton = CreateIconButton(MinusIconPath, "-", () => RemoveStop(stop));
        removeButton.style.position = Position.Absolute;
        removeButton.style.left = 336;
        removeButton.style.top = 4;
        removeButton.SetEnabled(stops.Count > 2);
        row.Add(removeButton);

        return row;
    }

    private FloatField CreateNumberField(float value)
    {
        FloatField field = new();
        field.SetValueWithoutNotify((float)Math.Round(value, 1));
        field.style.position = Position.Absolute;
        field.style.top = 3;
        field.style.width = 43;
        field.style.height = 18;
        return field;
    }

    private static void AttachNumberFieldDragger(FloatField field, VisualElement dragZone)
    {
        SetDefaultCursor(dragZone, MouseCursor.ResizeHorizontal);

        FieldMouseDragger<float> dragger = new(field);
        dragger.SetDragZone(dragZone);
    }

    private static void SetDefaultCursor(VisualElement element, MouseCursor mouseCursor)
    {
        if (CursorDefaultCursorIdProperty == null)
            return;

        object cursor = new UnityEngine.UIElements.Cursor();
        CursorDefaultCursorIdProperty.SetValue(cursor, (int)mouseCursor);
        element.style.cursor = new StyleCursor((UnityEngine.UIElements.Cursor)cursor);
    }

    private static float ClampNumberField(FloatField field, float value)
    {
        float clampedValue = (float)Math.Round(Mathf.Clamp(value, 0f, 100f), 1);
        if (!Mathf.Approximately(clampedValue, value))
            field.SetValueWithoutNotify(clampedValue);

        return clampedValue;
    }

    private void AddStop()
    {
        float time = FindFreeTime(selectedStop != null ? selectedStop.time : 0.5f);
        AddStopAt(time, "Add Gradient Stop");
    }

    private void AddStopAt(float time, string undoName)
    {
        time = Mathf.Clamp01(time);
        GradientStopModel stop = new()
        {
            time = time,
            color = EvaluateStops(time)
        };

        stops.Add(stop);
        selectedStop = stop;

        ApplyStopsToTarget(undoName);
        RefreshPreviewTexture();
        RefreshStopRows();
        RefreshStopMarkers();
    }

    private GradientStopModel DuplicateStop(GradientStopModel source)
    {
        GradientStopModel stop = new()
        {
            time = source.time,
            color = source.color
        };

        stops.Add(stop);
        selectedStop = stop;

        ApplyStopsToTarget("Duplicate Gradient Stop");
        RefreshPreviewTexture();
        return stop;
    }

    private void RemoveStop(GradientStopModel stop)
    {
        if (stops.Count <= 2)
            return;

        int oldIndex = stops.IndexOf(stop);
        stops.Remove(stop);
        selectedStop = stops[Mathf.Clamp(oldIndex, 0, stops.Count - 1)];

        ApplyStopsToTarget("Remove Gradient Stop");
        RefreshPreviewTexture();
        RefreshStopRows();
        RefreshStopMarkers();
    }

    private void SetSelectedStop(GradientStopModel stop, bool refreshRows = true, bool refreshMarkers = true)
    {
        if (selectedStop == stop)
            return;

        selectedStop = stop;

        if (refreshRows)
            RefreshStopRows();

        if (refreshMarkers)
            RefreshStopMarkers();
    }

    private void SetStopTime(GradientStopModel stop, float time, string undoName, bool rebuildRows, bool rebuildMarkers)
    {
        stop.time = Mathf.Clamp01(time);
        selectedStop = stop;

        ApplyStopsToTarget(undoName);
        RefreshPreviewTexture();

        if (rebuildRows)
            RefreshStopRows();

        if (rebuildMarkers)
            RefreshStopMarkers();
    }

    private void OnPreviewMouseDown(MouseDownEvent evt)
    {
        if (evt.button != 0 || stops.Count == 0)
            return;

        float time = PositionToTime(evt.localMousePosition.x);
        AddStopAt(time, "Add Gradient Stop");
        evt.StopPropagation();
    }

    private void RefreshStopMarkers()
    {
        if (stopOverlay == null)
            return;

        stopOverlay.Clear();

        foreach (GradientStopModel stop in stops)
        {
            VisualElement marker = new();
            marker.userData = stop;
            marker.style.position = Position.Absolute;
            marker.style.width = 16;
            marker.style.height = 16;

            VisualElement shadow = new();
            shadow.pickingMode = PickingMode.Ignore;
            shadow.style.position = Position.Absolute;
            shadow.style.left = 1;
            shadow.style.top = 1;
            shadow.style.width = 14;
            shadow.style.height = 14;
            shadow.style.borderTopLeftRadius = 999;
            shadow.style.borderTopRightRadius = 999;
            shadow.style.borderBottomLeftRadius = 999;
            shadow.style.borderBottomRightRadius = 999;
            shadow.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
            marker.Add(shadow);

            VisualElement face = new();
            face.pickingMode = PickingMode.Ignore;
            face.style.position = Position.Absolute;
            face.style.left = 2;
            face.style.top = 2;
            face.style.width = StopMarkerSize;
            face.style.height = StopMarkerSize;
            face.style.borderTopLeftRadius = 999;
            face.style.borderTopRightRadius = 999;
            face.style.borderBottomLeftRadius = 999;
            face.style.borderBottomRightRadius = 999;
            face.style.backgroundColor = stop.color;
            face.style.borderLeftColor = Color.white;
            face.style.borderRightColor = Color.white;
            face.style.borderTopColor = Color.white;
            face.style.borderBottomColor = Color.white;
            face.style.borderLeftWidth = stop == selectedStop ? 2 : 1;
            face.style.borderRightWidth = stop == selectedStop ? 2 : 1;
            face.style.borderTopWidth = stop == selectedStop ? 2 : 1;
            face.style.borderBottomWidth = stop == selectedStop ? 2 : 1;
            marker.Add(face);

            marker.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0)
                    return;

                if (evt.ctrlKey || evt.commandKey)
                {
                    RemoveStop(stop);
                    evt.StopPropagation();
                    return;
                }

                if (evt.altKey)
                {
                    GradientStopModel duplicateStop = DuplicateStop(stop);
                    RefreshStopRows();
                    RefreshStopMarkers();

                    VisualElement duplicateMarker = FindStopMarker(duplicateStop);
                    if (duplicateMarker != null)
                        duplicateMarker.CaptureMouse();

                    evt.StopPropagation();
                    return;
                }

                SetSelectedStop(stop, true, false);
                marker.CaptureMouse();
                evt.StopPropagation();
            });
            marker.RegisterCallback<MouseMoveEvent>(evt =>
            {
                if (!marker.HasMouseCapture())
                    return;

                Vector2 worldPosition = marker.LocalToWorld(evt.localMousePosition);
                Vector2 localPosition = stopOverlay.WorldToLocal(worldPosition);
                SetStopTime(stop, PositionToTime(localPosition.x), "Move Gradient Stop", false, false);
                PositionMarker(marker, stop.time);
                evt.StopPropagation();
            });
            marker.RegisterCallback<MouseUpEvent>(evt =>
            {
                if (marker.HasMouseCapture())
                    marker.ReleaseMouse();

                RefreshStopRows();
                RefreshStopMarkers();
                evt.StopPropagation();
            });

            stopOverlay.Add(marker);
            PositionMarker(marker, stop.time);
        }
    }

    private VisualElement FindStopMarker(GradientStopModel stop)
    {
        if (stopOverlay == null)
            return null;

        foreach (VisualElement child in stopOverlay.Children())
        {
            if (ReferenceEquals(child.userData, stop))
                return child;
        }

        return null;
    }

    private void PositionMarker(VisualElement marker, float time)
    {
        float width = Mathf.Max(stopOverlay?.resolvedStyle.width ?? 0f, PreviewTextureWidth);
        float height = Mathf.Max(stopOverlay?.resolvedStyle.height ?? 0f, PreviewTextureHeight);
        marker.style.left = Mathf.Clamp01(time) * Mathf.Max(0f, width - StopMarkerSize) - 2f;
        marker.style.top = (height - 16f) * 0.5f;
    }

    private float PositionToTime(float localX)
    {
        float width = Mathf.Max(stopOverlay?.resolvedStyle.width ?? 0f, PreviewTextureWidth);
        return Mathf.Clamp01(localX / Mathf.Max(1f, width));
    }

    private float FindFreeTime(float preferredTime)
    {
        float time = Mathf.Clamp01(preferredTime);
        const float step = 0.05f;

        for (int i = 0; i < 20 && HasStopNear(time); i++)
            time = Mathf.Repeat(time + step, 1f);

        return time;
    }

    private bool HasStopNear(float time)
    {
        foreach (GradientStopModel stop in stops)
        {
            if (Mathf.Abs(stop.time - time) <= 0.001f)
                return true;
        }

        return false;
    }

    private void LoadStopsFromTarget()
    {
        stops.Clear();
        selectedStop = null;

        if (gradientTarget == null)
            return;

        UnityEngine.Gradient gradient = gradientTarget.ColorGradient;
        GradientColorKey[] colorKeys = gradient.colorKeys;

        if (colorKeys == null || colorKeys.Length == 0)
        {
            stops.Add(new GradientStopModel { time = 0f, color = Color.white });
            stops.Add(new GradientStopModel { time = 1f, color = Color.black });
        }
        else
        {
            foreach (GradientColorKey colorKey in colorKeys)
            {
                Color color = colorKey.color;
                color.a = gradient.Evaluate(colorKey.time).a;
                stops.Add(new GradientStopModel
                {
                    time = Mathf.Clamp01(colorKey.time),
                    color = color
                });
            }
        }

        while (stops.Count < 2)
        {
            float time = stops.Count == 0 ? 0f : 1f;
            Color color = stops.Count == 0 ? Color.white : stops[0].color;
            stops.Add(new GradientStopModel { time = time, color = color });
        }

        SortStops();
        selectedStop = stops[0];
    }

    private void ApplyStopsToTarget(string undoName)
    {
        if (gradientTarget == null)
            return;

        SortStops();

        Undo.RecordObject(gradientTarget, undoName);

        UnityEngine.Gradient gradient = new()
        {
            mode = gradientTarget.ColorGradient.mode
        };

        GradientColorKey[] colorKeys = new GradientColorKey[stops.Count];
        GradientAlphaKey[] alphaKeys = new GradientAlphaKey[stops.Count];

        for (int i = 0; i < stops.Count; i++)
        {
            GradientStopModel stop = stops[i];
            Color color = stop.color;
            color.a = 1f;
            colorKeys[i] = new GradientColorKey(color, stop.time);
            alphaKeys[i] = new GradientAlphaKey(stop.color.a, stop.time);
        }

        gradient.SetKeys(colorKeys, alphaKeys);
        gradientTarget.ColorGradient = gradient;
        EditorUtility.SetDirty(gradientTarget);
        SceneView.RepaintAll();
    }

    private void RefreshPreviewTexture()
    {
        if (previewElement == null)
            return;

        if (previewTexture == null)
        {
            previewTexture = new Texture2D(PreviewTextureWidth, PreviewTextureHeight, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
        }

        for (int y = 0; y < PreviewTextureHeight; y++)
        {
            for (int x = 0; x < PreviewTextureWidth; x++)
            {
                float time = x / (float)(PreviewTextureWidth - 1);
                Color checkerColor = GetCheckerColor(x, y);
                Color gradientColor = EvaluateStops(time);
                Color composited = Color.Lerp(checkerColor, new Color(gradientColor.r, gradientColor.g, gradientColor.b, 1f), gradientColor.a);
                previewTexture.SetPixel(x, y, composited);
            }
        }

        previewTexture.Apply(false, false);
        previewElement.style.backgroundImage = new StyleBackground(previewTexture);
    }

    private Color EvaluateStops(float time)
    {
        SortStops();

        if (stops.Count == 0)
            return Color.white;

        time = Mathf.Clamp01(time);

        if (time <= stops[0].time)
            return stops[0].color;

        for (int i = 1; i < stops.Count; i++)
        {
            GradientStopModel previous = stops[i - 1];
            GradientStopModel next = stops[i];

            if (time > next.time)
                continue;

            float range = Mathf.Max(next.time - previous.time, 0.0001f);
            float t = Mathf.Clamp01((time - previous.time) / range);
            return Color.Lerp(previous.color, next.color, t);
        }

        return stops[stops.Count - 1].color;
    }

    private void SortStops()
    {
        stops.Sort((left, right) => left.time.CompareTo(right.time));
    }

    public static Color GetCheckerColor(int x, int y)
    {
        bool even = ((x / 8) + (y / 8)) % 2 == 0;
        return even ? ColorFromHex(0x52, 0x52, 0x52) : ColorFromHex(0x65, 0x65, 0x65);
    }

    private static Label CreateText(string text, int fontSize, Color color)
    {
        Label label = new(text);
        label.style.fontSize = fontSize;
        label.style.color = color;
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        return label;
    }

    private static Button CreateIconButton(string iconPath, string fallbackText, Action clickAction)
    {
        Button button = new(clickAction);
        button.style.width = 16;
        button.style.height = 16;
        button.style.paddingLeft = 0;
        button.style.paddingRight = 0;
        button.style.paddingTop = 0;
        button.style.paddingBottom = 0;
        button.style.marginLeft = 0;
        button.style.marginRight = 0;
        button.style.backgroundColor = Color.clear;
        button.style.alignItems = Align.Center;
        button.style.justifyContent = Justify.Center;
        button.style.borderLeftWidth = 0;
        button.style.borderRightWidth = 0;
        button.style.borderTopWidth = 0;
        button.style.borderBottomWidth = 0;
        bool isHovering = false;
        button.RegisterCallback<MouseEnterEvent>(_ =>
        {
            isHovering = true;
            if (button.enabledSelf)
                button.style.backgroundColor = new Color(1f, 1f, 1f, 0.12f);
        });
        button.RegisterCallback<MouseLeaveEvent>(_ =>
        {
            isHovering = false;
            button.style.backgroundColor = Color.clear;
        });
        button.RegisterCallback<MouseDownEvent>(_ =>
        {
            if (button.enabledSelf)
                button.style.backgroundColor = new Color(1f, 1f, 1f, 0.18f);
        });
        button.RegisterCallback<MouseUpEvent>(_ =>
        {
            if (button.enabledSelf && isHovering)
                button.style.backgroundColor = new Color(1f, 1f, 1f, 0.12f);
        });

        VisualElement icon = CreateIcon(iconPath, fallbackText);
        icon.pickingMode = PickingMode.Ignore;
        button.Add(icon);
        return button;
    }

    private static VisualElement CreateIcon(string iconPath, string fallbackText)
    {
        if (!string.IsNullOrEmpty(iconPath))
        {
            VectorImage vectorImage = AssetDatabase.LoadAssetAtPath<VectorImage>(iconPath);
            if (vectorImage != null)
            {
                Image image = new()
                {
                    vectorImage = vectorImage,
                    scaleMode = ScaleMode.ScaleToFit
                };
                image.style.width = 12;
                image.style.height = 12;
                return image;
            }
        }

        Label fallback = CreateText(fallbackText, 12, IconColor);
        fallback.style.width = 12;
        fallback.style.height = 12;
        fallback.style.unityTextAlign = TextAnchor.MiddleCenter;
        return fallback;
    }

    private static void StripFieldChrome(VisualElement field)
    {
        field.RegisterCallback<AttachToPanelEvent>(_ => StripFieldChromeNow(field));
        field.schedule.Execute(() => StripFieldChromeNow(field));
    }

    private static void StripFieldChromeNow(VisualElement field)
    {
        field.Query<VisualElement>().ForEach(element =>
        {
            if (element == field)
                return;

            element.style.borderLeftWidth = 0;
            element.style.borderRightWidth = 0;
            element.style.borderTopWidth = 0;
            element.style.borderBottomWidth = 0;
            element.style.marginLeft = 0;
            element.style.marginRight = 0;
            element.style.marginTop = 0;
            element.style.marginBottom = 0;
        });
    }

    private static void SetBorder(VisualElement element, Color color, float width)
    {
        element.style.borderLeftColor = color;
        element.style.borderRightColor = color;
        element.style.borderTopColor = color;
        element.style.borderBottomColor = color;
        element.style.borderLeftWidth = width;
        element.style.borderRightWidth = width;
        element.style.borderTopWidth = width;
        element.style.borderBottomWidth = width;
    }

    private void DestroyPreviewTexture()
    {
        if (previewTexture == null)
            return;

        DestroyImmediate(previewTexture);
        previewTexture = null;
    }

    private static Rect GetAnchorAdjacentPosition(Rect anchorScreenRect, Vector2 requestedSize)
    {
        const float spacing = 8f;
        Vector2 size = new(Mathf.Max(360f, requestedSize.x), Mathf.Max(190f, requestedSize.y));

        if (anchorScreenRect.width > 0f && anchorScreenRect.height > 0f)
        {
            float x = anchorScreenRect.xMin - size.x - spacing;
            if (x < 0f)
                x = anchorScreenRect.xMax + spacing;

            return new Rect(x, anchorScreenRect.yMin, size.x, size.y);
        }

        return new Rect(100f, 100f, size.x, size.y);
    }

    private static Texture2D GetControlLogoTexture()
    {
        if (controlLogoTexture != null)
            return controlLogoTexture;

        controlLogoTexture = new Texture2D(16, 16, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color transparent = new(0f, 0f, 0f, 0f);
        Vector2 center = new(7.5f, 7.5f);
        Vector2 innerCenter = new(5.95f, 8.9f);

        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                Vector2 pixel = new(x + 0.5f, y + 0.5f);
                float outerDistance = Vector2.Distance(pixel, center);
                float innerDistance = Vector2.Distance(pixel, innerCenter);
                bool outerRing = outerDistance is >= 6.7f and <= 7.6f;
                bool innerRing = innerDistance is >= 3.1f and <= 4.1f;
                controlLogoTexture.SetPixel(x, 15 - y, outerRing || innerRing ? Color.black : transparent);
            }
        }

        controlLogoTexture.Apply(false, false);
        return controlLogoTexture;
    }

    private static Color ColorFromHex(byte red, byte green, byte blue)
    {
        return new Color32(red, green, blue, 255);
    }

    private sealed class GradientStopModel
    {
        public float time;
        public Color color;
    }
}
#endif
