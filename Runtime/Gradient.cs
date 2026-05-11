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
        private const int GradientTextureWidth = 256;

        private static readonly int GradientTextureId = Shader.PropertyToID("_GradientTex");
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
        [NonSerialized] private Texture2D runtimeGradientTexture;
        [NonSerialized] private bool materialPropertiesDirty = true;
        [NonSerialized] private bool refreshRuntimeMaterialForMask;
        [NonSerialized] private int version;

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

        public int Version => version;

        public override Color color
        {
            get => base.color;
            set
            {
                if (base.color == value)
                    return;

                base.color = value;
                MarkMaterialPropertiesDirty();
            }
        }

        public override Material materialForRendering
        {
            get
            {
                EnsureRuntimeMaterial();
                SyncMaterialProperties();

                Material modifiedMaterial = base.materialForRendering;
                ApplyMaterialProperties(modifiedMaterial);
                return modifiedMaterial;
            }
        }

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
            Vector2 oldStart = start;
            Vector2 oldEnd = end;
            Vector2 oldSquash = squash;
            Vector2 middle = (start + end) * 0.5f;
            Vector2 newStart = middle + RotateClockwise(start - middle);
            Vector2 newEnd = middle + RotateClockwise(end - middle);

            start = newStart;
            end = newEnd;
            squash = TransformSquashForAxisChange(oldStart, oldEnd, oldSquash, newStart, newEnd, rectTransform.rect);
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

        public float EvaluateAlphaAtNormalizedPoint(Vector2 normalizedPoint)
        {
            EnsureGradientExists();
            return gradient.Evaluate(Mathf.Clamp01(EvaluateGradientTime(normalizedPoint))).a * color.a;
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

        public override Material GetModifiedMaterial(Material baseMaterial)
        {
            Material modifiedMaterial = base.GetModifiedMaterial(baseMaterial);
            ApplyMaterialProperties(modifiedMaterial);
            return modifiedMaterial;
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

            if (runtimeGradientTexture == null)
                runtimeGradientTexture = CreateGradientTexture();

            if (runtimeMaterial != null && m_Material != runtimeMaterial)
                m_Material = runtimeMaterial;
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
            {
                DestroyGradientTexture();
                return;
            }

            if (m_Material == runtimeMaterial)
                m_Material = null;

            if (Application.isPlaying)
                Destroy(runtimeMaterial);
            else
                DestroyImmediate(runtimeMaterial);

            runtimeMaterial = null;
            DestroyGradientTexture();
            materialPropertiesDirty = true;
            refreshRuntimeMaterialForMask = false;
        }

        private void SyncMaterialProperties()
        {
            if (!materialPropertiesDirty)
                return;

            EnsureGradientExists();

            if (runtimeMaterial == null)
                return;

            RefreshRuntimeMaterialForMaskIfNeeded();
            UpdateGradientTexture();
            ApplyMaterialProperties(runtimeMaterial);

            materialPropertiesDirty = false;
            refreshRuntimeMaterialForMask = false;
        }

        private void ApplyMaterialProperties(Material targetMaterial)
        {
            if (targetMaterial == null || gradient == null || runtimeGradientTexture == null)
                return;

            targetMaterial.SetTexture(GradientTextureId, runtimeGradientTexture);
            targetMaterial.SetFloat(GradientTypeId, (float)type);
            targetMaterial.SetVector(GradientStartId, start);
            targetMaterial.SetVector(GradientEndId, end);
            targetMaterial.SetVector(GradientSquashId, squash);
            targetMaterial.SetColor(ColorAId, gradient.Evaluate(0f));
            targetMaterial.SetColor(ColorBId, gradient.Evaluate(1f));
            targetMaterial.SetColor(MaterialTintId, Color.white);
        }

        private void RefreshRuntimeMaterialForMaskIfNeeded()
        {
            if (!refreshRuntimeMaterialForMask || !ShouldRefreshRuntimeMaterialForMask())
                return;

            Material oldMaterial = runtimeMaterial;
            Material newMaterial = CreateRuntimeMaterial();
            if (newMaterial == null)
                return;

            runtimeMaterial = newMaterial;
            if (m_Material == oldMaterial)
                m_Material = runtimeMaterial;

            DestroyMaterial(oldMaterial);
        }

        private bool ShouldRefreshRuntimeMaterialForMask()
        {
            if (isMaskingGraphic)
                return true;

            Mask mask = GetComponent<Mask>();
            return mask != null && mask.MaskEnabled() && mask.graphic == this;
        }

        private static void DestroyMaterial(Material targetMaterial)
        {
            if (targetMaterial == null)
                return;

            if (Application.isPlaying)
                Destroy(targetMaterial);
            else
                DestroyImmediate(targetMaterial);
        }

        private Texture2D CreateGradientTexture()
        {
            Texture2D texture = new(GradientTextureWidth, 1, TextureFormat.RGBA32, false, true)
            {
                name = $"{nameof(Gradient)} Texture ({GetInstanceID()})",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            return texture;
        }

        private void UpdateGradientTexture()
        {
            if (runtimeGradientTexture == null)
                runtimeGradientTexture = CreateGradientTexture();

            for (int x = 0; x < GradientTextureWidth; x++)
            {
                float time = GradientTextureWidth > 1 ? x / (float)(GradientTextureWidth - 1) : 0f;
                runtimeGradientTexture.SetPixel(x, 0, gradient.Evaluate(time));
            }

            runtimeGradientTexture.Apply(false, false);
        }

        private void DestroyGradientTexture()
        {
            if (runtimeGradientTexture == null)
                return;

            if (Application.isPlaying)
                Destroy(runtimeGradientTexture);
            else
                DestroyImmediate(runtimeGradientTexture);

            runtimeGradientTexture = null;
        }

        private void MarkMaterialPropertiesDirty()
        {
            version++;
            materialPropertiesDirty = true;
            refreshRuntimeMaterialForMask = true;
            SetMaterialDirty();
        }

        private float EvaluateGradientTime(Vector2 normalizedPoint)
        {
            return type switch
            {
                GradientType.Radial => EvaluateRadial(normalizedPoint),
                GradientType.Angular => EvaluateAngular(normalizedPoint),
                GradientType.Diamond => EvaluateDiamond(normalizedPoint),
                _ => EvaluateLinear(normalizedPoint)
            };
        }

        private float EvaluateLinear(Vector2 normalizedPoint)
        {
            return GetGradientCoordinates(normalizedPoint).x;
        }

        private float EvaluateRadial(Vector2 normalizedPoint)
        {
            return GetGradientCoordinates(normalizedPoint).magnitude;
        }

        private float EvaluateAngular(Vector2 normalizedPoint)
        {
            Vector2 coordinates = GetGradientCoordinates(normalizedPoint);
            if (coordinates.sqrMagnitude <= 0.000001f)
                return 0f;

            return Mathf.Repeat(Mathf.Atan2(coordinates.y, coordinates.x) / (Mathf.PI * 2f), 1f);
        }

        private float EvaluateDiamond(Vector2 normalizedPoint)
        {
            Vector2 coordinates = GetGradientCoordinates(normalizedPoint);
            return Mathf.Abs(coordinates.x) + Mathf.Abs(coordinates.y);
        }

        private Vector2 GetGradientCoordinates(Vector2 normalizedPoint)
        {
            Vector2 anchorA = start;
            Vector2 axisX = end - anchorA;
            Vector2 axisY = squash - anchorA;
            float axisXLength = Mathf.Max(axisX.magnitude, 0.000001f);
            float determinant = Cross(axisX, axisY);

            if (Mathf.Abs(determinant) <= 0.000001f)
            {
                axisY = new Vector2(-axisX.y, axisX.x) / axisXLength;
                axisY *= axisXLength;
                determinant = Cross(axisX, axisY);
            }

            Vector2 offset = normalizedPoint - anchorA;
            return new Vector2(Cross(offset, axisY) / determinant, Cross(axisX, offset) / determinant);
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
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
