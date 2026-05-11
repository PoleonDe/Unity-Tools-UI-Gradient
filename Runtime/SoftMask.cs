using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Control
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Control/UI Soft Mask")]
    public sealed class SoftMask : MonoBehaviour
    {
        private const int MinimumResolution = 16;
        private const int MaximumResolution = 2048;

        [SerializeField]
        private Gradient sourceGradient;

        [SerializeField]
        private bool showMaskGraphic = true;

        [SerializeField]
        private bool invert;

        [NonSerialized] private RectTransform rectTransformCache;
        [NonSerialized] private Canvas canvasCache;
        [NonSerialized] private Texture2D maskTexture;
        [NonSerialized] private bool textureDirty = true;
        [NonSerialized] private int lastGradientVersion = -1;
        [NonSerialized] private int lastResolution;
        private readonly HashSet<SoftMaskable> registeredMaskables = new();

        public Gradient SourceGradient
        {
            get => sourceGradient;
            set
            {
                if (sourceGradient == value)
                    return;

                sourceGradient = value;
                ApplyShowMaskGraphic();
                MarkTextureDirty();
            }
        }

        public bool ShowMaskGraphic
        {
            get => showMaskGraphic;
            set
            {
                if (showMaskGraphic == value)
                    return;

                showMaskGraphic = value;
                ApplyShowMaskGraphic();
            }
        }

        public bool Invert
        {
            get => invert;
            set
            {
                if (invert == value)
                    return;

                invert = value;
                MarkTextureDirty();
            }
        }

        public Texture2D MaskTexture
        {
            get
            {
                RefreshIfNeeded();
                return maskTexture;
            }
        }

        public Matrix4x4 WorldToMaskMatrix
        {
            get
            {
                Rect rect = RectTransform.rect;
                float width = Mathf.Abs(rect.width) > Mathf.Epsilon ? rect.width : 1f;
                float height = Mathf.Abs(rect.height) > Mathf.Epsilon ? rect.height : 1f;

                Matrix4x4 localToMask = Matrix4x4.TRS(
                    new Vector3(-rect.xMin / width, -rect.yMin / height, 0f),
                    Quaternion.identity,
                    new Vector3(1f / width, 1f / height, 1f));

                return localToMask * RectTransform.worldToLocalMatrix;
            }
        }

        public Matrix4x4 CanvasToMaskMatrix
        {
            get
            {
                Canvas targetCanvas = Canvas;
                if (targetCanvas == null)
                    return WorldToMaskMatrix;

                return WorldToMaskMatrix * targetCanvas.rootCanvas.transform.localToWorldMatrix;
            }
        }

        private RectTransform RectTransform => rectTransformCache != null
            ? rectTransformCache
            : rectTransformCache = GetComponent<RectTransform>();

        private Canvas Canvas => canvasCache != null
            ? canvasCache
            : canvasCache = GetComponentInParent<Canvas>();

        public void Register(SoftMaskable maskable)
        {
            if (maskable == null || !registeredMaskables.Add(maskable))
                return;

            maskable.SetMaterialDirty();
        }

        public void Unregister(SoftMaskable maskable)
        {
            if (maskable != null)
                registeredMaskables.Remove(maskable);
        }

        private void OnEnable()
        {
            EnsureSourceGradient();
            ApplyShowMaskGraphic();
            MarkTextureDirty();
        }

        private void OnDisable()
        {
            RestoreMaskGraphicVisibility();
            DestroyMaskTexture();
            NotifyMaskables();
        }

        private void OnDestroy()
        {
            RestoreMaskGraphicVisibility();
            DestroyMaskTexture();
        }

        private void Update()
        {
            RefreshIfNeeded();
        }

        private void OnRectTransformDimensionsChange()
        {
            MarkTextureDirty();
        }

        private void OnTransformParentChanged()
        {
            canvasCache = null;
            MarkTextureDirty();
        }

        private void OnCanvasHierarchyChanged()
        {
            canvasCache = null;
            MarkTextureDirty();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            EnsureSourceGradient();
            ApplyShowMaskGraphic();
            MarkTextureDirty();
        }
#endif

        private void RefreshIfNeeded()
        {
            EnsureSourceGradient();
            if (sourceGradient == null)
                return;

            int sourceVersion = sourceGradient.Version;
            if (sourceVersion != lastGradientVersion)
                textureDirty = true;

            int clampedResolution = GetAutomaticResolution();
            if (clampedResolution != lastResolution)
                textureDirty = true;

            if (!textureDirty)
                return;

            EnsureMaskTexture(clampedResolution);
            RebuildMaskTexture();
            lastGradientVersion = sourceVersion;
            lastResolution = clampedResolution;
            textureDirty = false;
            NotifyMaskablesIfSafe();
        }

        private void EnsureSourceGradient()
        {
            if (sourceGradient == null)
                sourceGradient = GetComponent<Gradient>();
        }

        private void EnsureMaskTexture(int textureResolution)
        {
            if (maskTexture != null && maskTexture.width == textureResolution && maskTexture.height == textureResolution)
                return;

            DestroyMaskTexture();
            maskTexture = new Texture2D(textureResolution, textureResolution, TextureFormat.RGBA32, false, true)
            {
                name = $"{nameof(SoftMask)} Texture ({GetInstanceID()})",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
        }

        private int GetAutomaticResolution()
        {
            Rect rect = RectTransform.rect;
            float largestAxis = Mathf.Max(Mathf.Abs(rect.width), Mathf.Abs(rect.height));
            float scaleFactor = Canvas != null ? Canvas.scaleFactor : 1f;
            return Mathf.Clamp(Mathf.CeilToInt(largestAxis * scaleFactor), MinimumResolution, MaximumResolution);
        }

        private void RebuildMaskTexture()
        {
            if (maskTexture == null || sourceGradient == null)
                return;

            int width = maskTexture.width;
            int height = maskTexture.height;
            for (int y = 0; y < height; y++)
            {
                float v = (y + 0.5f) / height;
                for (int x = 0; x < width; x++)
                {
                    float u = (x + 0.5f) / width;
                    float alpha = sourceGradient.EvaluateAlphaAtNormalizedPoint(new Vector2(u, v));
                    if (invert)
                        alpha = 1f - alpha;

                    maskTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            maskTexture.Apply(false, false);
        }

        private void MarkTextureDirty()
        {
            textureDirty = true;
            NotifyMaskables();
        }

        private void ApplyShowMaskGraphic()
        {
            if (sourceGradient == null || sourceGradient.canvasRenderer == null)
                return;

            sourceGradient.canvasRenderer.SetAlpha(showMaskGraphic ? 1f : 0f);
        }

        private void RestoreMaskGraphicVisibility()
        {
            if (sourceGradient == null || sourceGradient.canvasRenderer == null)
                return;

            sourceGradient.canvasRenderer.SetAlpha(1f);
        }

        private void NotifyMaskablesIfSafe()
        {
            if (CanvasUpdateRegistry.IsRebuildingGraphics())
                return;

            NotifyMaskables();
        }

        private void NotifyMaskables()
        {
            foreach (SoftMaskable maskable in registeredMaskables)
            {
                if (maskable != null)
                    maskable.SetMaterialDirty();
            }
        }

        private void DestroyMaskTexture()
        {
            if (maskTexture == null)
                return;

            if (Application.isPlaying)
                Destroy(maskTexture);
            else
                DestroyImmediate(maskTexture);

            maskTexture = null;
            lastResolution = 0;
            textureDirty = true;
        }
    }
}
