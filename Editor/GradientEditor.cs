#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

using GradientGraphic = Control.Gradient;

[CustomEditor(typeof(GradientGraphic))]
[CanEditMultipleObjects]
public sealed class GradientEditor : Editor
{
    private const float HandleTolerance = 0.001f;

    private static readonly Color LineColor = new(0.2f, 0.75f, 1f, 1f);
    private static readonly Color StartColor = new(0.35f, 0.95f, 1f, 1f);
    private static readonly Color EndColor = new(1f, 0.9f, 0.25f, 1f);
    private static readonly Color SquashColor = new(0.9f, 0.45f, 1f, 1f);

    private SerializedProperty typeProperty;
    private SerializedProperty gradientProperty;
    private SerializedProperty startProperty;
    private SerializedProperty endProperty;
    private SerializedProperty squashProperty;
    private SerializedProperty colorProperty;
    private SerializedProperty raycastTargetProperty;
    private SerializedProperty maskableProperty;
    private SerializedProperty showHandlesProperty;

    private void OnEnable()
    {
        typeProperty = serializedObject.FindProperty("type");
        gradientProperty = serializedObject.FindProperty("gradient");
        startProperty = serializedObject.FindProperty("start");
        endProperty = serializedObject.FindProperty("end");
        squashProperty = serializedObject.FindProperty("squash");
        showHandlesProperty = serializedObject.FindProperty("showHandles");

        colorProperty = serializedObject.FindProperty("m_Color");
        raycastTargetProperty = serializedObject.FindProperty("m_RaycastTarget");
        maskableProperty = serializedObject.FindProperty("m_Maskable");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(typeProperty);
        EditorGUILayout.PropertyField(gradientProperty);

        DrawVectorField(startProperty, "Start", "Change Gradient Start", (gradient, value) => gradient.StartPoint = value);
        DrawVectorField(endProperty, "End", "Change Gradient End", (gradient, value) => gradient.EndPoint = value);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Flip Start / End"))
                ApplyToTargets("Flip Gradient Start / End", gradient => gradient.FlipStartAndEnd());

            if (GUILayout.Button("Rotate 90 Clockwise"))
                ApplyToTargets("Rotate Gradient 90 Clockwise", gradient => gradient.RotateStartAndEndClockwise());
        }

        DrawVectorField(squashProperty, "Squash", "Change Gradient Squash", (gradient, value) => gradient.SquashPoint = value);

        if (GUILayout.Button("Reset Squash Perpendicular"))
            ApplyToTargets("Reset Gradient Squash Perpendicular", gradient => gradient.ResetSquashPerpendicular());

        if (colorProperty != null)
            EditorGUILayout.PropertyField(colorProperty);

        if (raycastTargetProperty != null)
            EditorGUILayout.PropertyField(raycastTargetProperty);

        if (maskableProperty != null)
            EditorGUILayout.PropertyField(maskableProperty);

        serializedObject.ApplyModifiedProperties();

        serializedObject.Update();
        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(showHandlesProperty, new GUIContent("Show Handles"));

        serializedObject.ApplyModifiedProperties();
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

    private static void DrawSquashHandle(GradientGraphic gradient, RectTransform rectTransform)
    {
        if (!TryGetSquashGeometry(gradient, out Vector2 startLocal, out Vector2 squashLocal, out Vector2 perpendicularLocal, out float axisLength))
            return;

        Vector2 projectedSquashLocal = ProjectOntoPerpendicularAxis(startLocal, squashLocal, perpendicularLocal);
        bool squashWasOffAxis = Vector2.Distance(projectedSquashLocal, squashLocal) > HandleTolerance;
        bool squashHasAxisLength = Mathf.Abs(Vector2.Distance(projectedSquashLocal, startLocal) - axisLength) <= HandleTolerance;

        if (gradient.Type == GradientGraphic.GradientType.Linear && !squashWasOffAxis && squashHasAxisLength)
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

    private void DrawVectorField(SerializedProperty property, string label, string undoName, System.Action<GradientGraphic, Vector2> applyValue)
    {
        EditorGUI.BeginChangeCheck();
        EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
        Vector2 value = EditorGUILayout.Vector2Field(label, property.vector2Value);
        EditorGUI.showMixedValue = false;

        if (!EditorGUI.EndChangeCheck())
            return;

        serializedObject.ApplyModifiedProperties();
        ApplyToTargets(undoName, gradient => applyValue(gradient, value));
        serializedObject.Update();
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

    private void ApplyToTargets(string undoName, System.Action<GradientGraphic> action)
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
#endif
