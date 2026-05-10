using System;
using UnityEngine;
using UnityEngine.UI;

namespace Control
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
    [AddComponentMenu("Control/UI Gradient")]
    public sealed class Gradient : MaskableGraphic
    {
        private const string ShaderName = "Control/UI/Gradient";
        private const string DefaultMaterialResourcePath = "Materials/UIGradient";

        private static readonly int GradientTypeId = Shader.PropertyToID("_GradientType");
        private static readonly int GradientStartId = Shader.PropertyToID("_GradientStart");
        private static readonly int GradientEndId = Shader.PropertyToID("_GradientEnd");
        private static readonly int GradientSquashId = Shader.PropertyToID("_GradientSquash");
        private static readonly int ColorAId = Shader.PropertyToID("_ColorA");
        private static readonly int ColorBId = Shader.PropertyToID("_ColorB");
        private static readonly int MaterialTintId = Shader.PropertyToID("_Color");

        public enum GradientType
        {
            Linear = 0,
            Radial = 1,
            Angular = 2,
            Diamond = 3
        }

        [SerializeField]
        private GradientType type = GradientType.Linear;

        [SerializeField]
        private UnityEngine.Gradient gradient = CreateDefaultGradient();

        [SerializeField]
        private Vector2 start = new(0f, 0.5f);

        [SerializeField]
        private Vector2 end = new(1f, 0.5f);

        [SerializeField]
        private Vector2 squash = new(0f, 1.5f);

        [SerializeField]
        private bool showHandles = true;

        [NonSerialized] private Material runtimeMaterial;
        [NonSerialized] private bool materialPropertiesDirty = true;

        public GradientType Type
        {
            get => type;
            set
            {
                if (type == value)
                    return;

                type = value;
                MarkMaterialPropertiesDirty();
            }
        }

        public UnityEngine.Gradient ColorGradient
        {
            get
            {
                EnsureGradientExists();
                return gradient;
            }
            set
            {
                gradient = value ?? CreateDefaultGradient();
                MarkMaterialPropertiesDirty();
            }
        }

        public Vector2 StartPoint
        {
            get => start;
            set => SetStartPoint(value);
        }

        public Vector2 EndPoint
        {
            get => end;
            set => SetEndPoint(value);
        }

        public bool ShowHandles
        {
            get => showHandles;
            set => showHandles = value;
        }

        public Vector2 SquashPoint
        {
            get => squash;
            set
            {
                if (squash == value)
                    return;

                squash = value;
                MarkMaterialPropertiesDirty();
            }
        }

        public override Texture mainTexture => s_WhiteTexture;

        [ContextMenu("Flip Start / End")]
        public void FlipStartAndEnd()
        {
            Vector2 oldStart = start;
            (start, end) = (end, start);
            squash = start + (squash - oldStart);
            MarkMaterialPropertiesDirty();
        }

        [ContextMenu("Rotate 90 Clockwise")]
        public void RotateStartAndEndClockwise()
        {
            Vector2 middle = (start + end) * 0.5f;
            start = middle + RotateClockwise(start - middle);
            end = middle + RotateClockwise(end - middle);
            squash = middle + RotateClockwise(squash - middle);
            MarkMaterialPropertiesDirty();
        }

        [ContextMenu("Reset Squash Perpendicular")]
        public void ResetSquashPerpendicular()
        {
            Vector2 startLocal = NormalizedToLocalPoint(start);
            Vector2 endLocal = NormalizedToLocalPoint(end);
            Vector2 squashLocal = NormalizedToLocalPoint(squash);
            Vector2 direction = endLocal - startLocal;
            float length = direction.magnitude;
            if (length <= Mathf.Epsilon)
                return;

            Vector2 perpendicular = new(-direction.y / length, direction.x / length);
            if (Vector2.Dot(squashLocal - startLocal, perpendicular) < 0f)
                perpendicular = -perpendicular;

            squash = LocalPointToNormalized(startLocal + perpendicular * length);
            MarkMaterialPropertiesDirty();
        }

        public void SetStartPoint(Vector2 value)
        {
            SetAxis(value, end);
        }

        public void SetEndPoint(Vector2 value)
        {
            SetAxis(start, value);
        }

        public Vector2 NormalizedToLocalPoint(Vector2 normalizedPoint)
        {
            Rect rect = rectTransform.rect;
            return new Vector2(
                rect.xMin + rect.width * normalizedPoint.x,
                rect.yMin + rect.height * normalizedPoint.y
            );
        }

        public Vector2 LocalPointToNormalized(Vector2 localPoint)
        {
            Rect rect = rectTransform.rect;

            return new Vector2(
                Mathf.Abs(rect.width) > Mathf.Epsilon ? (localPoint.x - rect.xMin) / rect.width : 0f,
                Mathf.Abs(rect.height) > Mathf.Epsilon ? (localPoint.y - rect.yMin) / rect.height : 0f
            );
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EnsureRuntimeMaterial();
            MarkMaterialPropertiesDirty();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            DestroyRuntimeMaterial();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            DestroyRuntimeMaterial();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect rect = rectTransform.rect;
            if (rect.width <= Mathf.Epsilon || rect.height <= Mathf.Epsilon)
                return;

            AddQuad(vh, rect);
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            SetVerticesDirty();
        }

        protected override void UpdateMaterial()
        {
            EnsureRuntimeMaterial();
            SyncMaterialProperties();
            base.UpdateMaterial();
        }

        protected override void OnDidApplyAnimationProperties()
        {
            base.OnDidApplyAnimationProperties();
            SetVerticesDirty();
            MarkMaterialPropertiesDirty();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            EnsureGradientExists();
            MarkMaterialPropertiesDirty();
            SetVerticesDirty();
        }
#endif

        private void AddQuad(VertexHelper vh, Rect rect)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;

            vertex.position = new Vector3(rect.xMin, rect.yMin, 0f);
            vertex.uv0 = new Vector2(0f, 0f);
            vh.AddVert(vertex);

            vertex.position = new Vector3(rect.xMin, rect.yMax, 0f);
            vertex.uv0 = new Vector2(0f, 1f);
            vh.AddVert(vertex);

            vertex.position = new Vector3(rect.xMax, rect.yMax, 0f);
            vertex.uv0 = new Vector2(1f, 1f);
            vh.AddVert(vertex);

            vertex.position = new Vector3(rect.xMax, rect.yMin, 0f);
            vertex.uv0 = new Vector2(1f, 0f);
            vh.AddVert(vertex);

            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }

        private void EnsureRuntimeMaterial()
        {
            if (runtimeMaterial == null)
                runtimeMaterial = CreateRuntimeMaterial();

            if (runtimeMaterial != null && material != runtimeMaterial)
                material = runtimeMaterial;
        }

        private Material CreateRuntimeMaterial()
        {
            Material sourceMaterial = Resources.Load<Material>(DefaultMaterialResourcePath);
            Material createdMaterial = sourceMaterial != null
                ? new Material(sourceMaterial)
                : CreateMaterialFromShader();

            if (createdMaterial == null)
                return null;

            createdMaterial.name = $"{nameof(Gradient)} Material ({GetInstanceID()})";
            createdMaterial.hideFlags = HideFlags.HideAndDontSave;
            return createdMaterial;
        }

        private static Material CreateMaterialFromShader()
        {
            Shader shader = Shader.Find(ShaderName);
            return shader != null ? new Material(shader) : null;
        }

        private void DestroyRuntimeMaterial()
        {
            if (runtimeMaterial == null)
                return;

            if (material == runtimeMaterial)
                material = null;

            if (Application.isPlaying)
                Destroy(runtimeMaterial);
            else
                DestroyImmediate(runtimeMaterial);

            runtimeMaterial = null;
            materialPropertiesDirty = true;
        }

        private void SyncMaterialProperties()
        {
            if (!materialPropertiesDirty)
                return;

            EnsureGradientExists();

            if (runtimeMaterial == null)
                return;

            // The shader path is two-color today; this is the upload point for a future
            // sampled multi-stop gradient texture without changing the quad geometry.
            Color colorA = gradient.Evaluate(0f);
            Color colorB = gradient.Evaluate(1f);

            runtimeMaterial.SetFloat(GradientTypeId, (float)type);
            runtimeMaterial.SetVector(GradientStartId, start);
            runtimeMaterial.SetVector(GradientEndId, end);
            runtimeMaterial.SetVector(GradientSquashId, squash);
            runtimeMaterial.SetColor(ColorAId, colorA);
            runtimeMaterial.SetColor(ColorBId, colorB);
            runtimeMaterial.SetColor(MaterialTintId, Color.white);

            materialPropertiesDirty = false;
        }

        private void MarkMaterialPropertiesDirty()
        {
            materialPropertiesDirty = true;
            SetMaterialDirty();
        }

        private void SetAxis(Vector2 newStart, Vector2 newEnd)
        {
            if (start == newStart && end == newEnd)
                return;

            squash = TransformSquashForAxisChange(start, end, squash, newStart, newEnd, rectTransform.rect);
            start = newStart;
            end = newEnd;
            MarkMaterialPropertiesDirty();
        }

        private static Vector2 TransformSquashForAxisChange(Vector2 oldStart, Vector2 oldEnd, Vector2 oldSquash, Vector2 newStart, Vector2 newEnd, Rect rect)
        {
            Vector2 rectSize = new(Mathf.Abs(rect.width), Mathf.Abs(rect.height));
            if (rectSize.x <= Mathf.Epsilon || rectSize.y <= Mathf.Epsilon)
                return newStart + (oldSquash - oldStart);

            Vector2 oldDirection = Vector2.Scale(oldEnd - oldStart, rectSize);
            Vector2 newDirection = Vector2.Scale(newEnd - newStart, rectSize);
            float oldLength = oldDirection.magnitude;
            float newLength = newDirection.magnitude;

            if (oldLength <= Mathf.Epsilon || newLength <= Mathf.Epsilon)
                return newStart + (oldSquash - oldStart);

            Vector2 oldAxis = oldDirection / oldLength;
            Vector2 oldPerpendicular = new(-oldAxis.y, oldAxis.x);
            Vector2 oldOffset = Vector2.Scale(oldSquash - oldStart, rectSize);
            float alignedScale = Vector2.Dot(oldOffset, oldAxis) / oldLength;
            float perpendicularScale = Vector2.Dot(oldOffset, oldPerpendicular) / oldLength;

            Vector2 newAxis = newDirection / newLength;
            Vector2 newPerpendicular = new(-newAxis.y, newAxis.x);
            Vector2 newLocalOffset = (newAxis * alignedScale + newPerpendicular * perpendicularScale) * newLength;
            return newStart + new Vector2(newLocalOffset.x / rectSize.x, newLocalOffset.y / rectSize.y);
        }

        private void EnsureGradientExists()
        {
            gradient ??= CreateDefaultGradient();
        }

        private static Vector2 RotateClockwise(Vector2 value)
        {
            return new Vector2(value.y, -value.x);
        }

        private static UnityEngine.Gradient CreateDefaultGradient()
        {
            UnityEngine.Gradient defaultGradient = new();
            defaultGradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.black, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                }
            );

            return defaultGradient;
        }
    }
}
