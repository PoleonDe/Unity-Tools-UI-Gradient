using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Control
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Control/UI Soft Maskable")]
    public sealed class SoftMaskable : UIBehaviour, IMaterialModifier
    {
        private const string SoftMaskMaterialResourcePath = "Materials/UISoftMask";
        private const string SoftMaskShaderName = "Control/UI/SoftMask";
        private const string DefaultUiShaderName = "UI/Default";

        private static readonly int SoftMaskTextureId = Shader.PropertyToID("_ControlSoftMaskTex");
        private static readonly int SoftMaskEnabledId = Shader.PropertyToID("_ControlSoftMaskEnabled");
        private static readonly int SoftMaskCanvasToMaskId = Shader.PropertyToID("_ControlSoftMaskCanvasToMask");

        [SerializeField]
        private SoftMask softMask;

        [NonSerialized] private Graphic graphic;
        [NonSerialized] private Material baseMaterial;
        [NonSerialized] private Material modifiedMaterial;
        [NonSerialized] private Shader softMaskShader;
        [NonSerialized] private SoftMask registeredSoftMask;
        [NonSerialized] private bool warnedUnsupportedShader;

        public SoftMask SoftMask
        {
            get => softMask;
            set
            {
                if (softMask == value)
                    return;

                UnregisterFromSoftMask();
                softMask = value;
                RegisterWithSoftMask();
                SetMaterialDirty();
            }
        }

        public Material GetModifiedMaterial(Material baseMaterial)
        {
            SoftMask activeSoftMask = ResolveSoftMask();
            if (!isActiveAndEnabled || activeSoftMask == null || !activeSoftMask.isActiveAndEnabled)
                return baseMaterial;

            Texture2D maskTexture = activeSoftMask.MaskTexture;
            if (maskTexture == null)
                return baseMaterial;

            Material material = GetOrCreateModifiedMaterial(baseMaterial);
            if (material == null)
                return baseMaterial;

            ApplySoftMaskProperties(material, activeSoftMask, maskTexture);
            return material;
        }

        public void SetMaterialDirty()
        {
            Graphic targetGraphic = Graphic;
            if (targetGraphic != null)
                targetGraphic.SetMaterialDirty();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            RegisterWithSoftMask();
            SetMaterialDirty();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            UnregisterFromSoftMask();
            DestroyModifiedMaterial();
            SetMaterialDirty();
        }

        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();
            if (softMask == null)
            {
                RegisterWithSoftMask();
                SetMaterialDirty();
            }
        }

        protected override void OnCanvasHierarchyChanged()
        {
            base.OnCanvasHierarchyChanged();
            SetMaterialDirty();
        }

        private void Update()
        {
            UpdateSoftMaskProperties();
        }

        private void LateUpdate()
        {
            ApplyMaterialToCanvasRenderer();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            SetMaterialDirty();
        }
#endif

        private Graphic Graphic => graphic != null ? graphic : graphic = GetComponent<Graphic>();

        private SoftMask ResolveSoftMask()
        {
            return softMask;
        }

        private Material GetOrCreateModifiedMaterial(Material sourceMaterial)
        {
            if (sourceMaterial == null)
                return null;

            if (modifiedMaterial != null && baseMaterial == sourceMaterial)
                return modifiedMaterial;

            DestroyModifiedMaterial();

            modifiedMaterial = new Material(sourceMaterial)
            {
                name = $"{nameof(SoftMaskable)} ({sourceMaterial.name})",
                hideFlags = HideFlags.HideAndDontSave
            };
            baseMaterial = sourceMaterial;

            if (!SupportsSoftMask(modifiedMaterial))
            {
                if (CanReplaceWithSoftMaskShader(sourceMaterial))
                    modifiedMaterial.shader = GetSoftMaskShader();
            }

            if (!SupportsSoftMask(modifiedMaterial))
            {
                WarnUnsupportedShader(sourceMaterial);
                DestroyModifiedMaterial();
                return null;
            }

            warnedUnsupportedShader = false;
            return modifiedMaterial;
        }

        private void UpdateSoftMaskProperties()
        {
            if (modifiedMaterial == null)
                return;

            SoftMask activeSoftMask = ResolveSoftMask();
            if (activeSoftMask == null || !activeSoftMask.isActiveAndEnabled)
                return;

            Texture2D maskTexture = activeSoftMask.MaskTexture;
            if (maskTexture == null)
                return;

            ApplySoftMaskProperties(modifiedMaterial, activeSoftMask, maskTexture);
        }

        private void ApplyMaterialToCanvasRenderer()
        {
            Graphic targetGraphic = Graphic;
            if (targetGraphic == null || targetGraphic.canvasRenderer == null || targetGraphic.canvasRenderer.cull)
                return;

            Material material = targetGraphic.materialForRendering;
            if (material == null)
                return;

            ApplySoftMaskProperties(material);
            targetGraphic.canvasRenderer.materialCount = 1;
            targetGraphic.canvasRenderer.SetMaterial(material, 0);
            targetGraphic.canvasRenderer.SetTexture(targetGraphic.mainTexture);
        }

        private void ApplySoftMaskProperties(Material material)
        {
            if (!SupportsSoftMask(material))
                return;

            SoftMask activeSoftMask = ResolveSoftMask();
            if (activeSoftMask == null || !activeSoftMask.isActiveAndEnabled)
                return;

            Texture2D maskTexture = activeSoftMask.MaskTexture;
            if (maskTexture == null)
                return;

            ApplySoftMaskProperties(material, activeSoftMask, maskTexture);
        }

        private void ApplySoftMaskProperties(Material material, SoftMask activeSoftMask, Texture2D maskTexture)
        {
            material.SetTexture(SoftMaskTextureId, maskTexture);
            material.SetFloat(SoftMaskEnabledId, 1f);
            material.SetMatrix(SoftMaskCanvasToMaskId, activeSoftMask.CanvasToMaskMatrix);
        }

        private void RegisterWithSoftMask()
        {
            SoftMask activeSoftMask = ResolveSoftMask();
            if (registeredSoftMask == activeSoftMask)
                return;

            UnregisterFromSoftMask();
            registeredSoftMask = activeSoftMask;
            if (registeredSoftMask != null)
                registeredSoftMask.Register(this);
        }

        private void UnregisterFromSoftMask()
        {
            if (registeredSoftMask == null)
                return;

            registeredSoftMask.Unregister(this);
            registeredSoftMask = null;
        }

        private bool SupportsSoftMask(Material material)
        {
            return material != null
                && material.HasProperty(SoftMaskTextureId)
                && material.HasProperty(SoftMaskEnabledId);
        }

        private bool CanReplaceWithSoftMaskShader(Material sourceMaterial)
        {
            return sourceMaterial != null
                && sourceMaterial.shader != null
                && sourceMaterial.shader.name == DefaultUiShaderName
                && GetSoftMaskShader() != null;
        }

        private Shader GetSoftMaskShader()
        {
            if (softMaskShader != null)
                return softMaskShader;

            Material softMaskMaterial = Resources.Load<Material>(SoftMaskMaterialResourcePath);
            softMaskShader = softMaskMaterial != null
                ? softMaskMaterial.shader
                : Shader.Find(SoftMaskShaderName);

            return softMaskShader;
        }

        private void WarnUnsupportedShader(Material sourceMaterial)
        {
            if (warnedUnsupportedShader || sourceMaterial == null || sourceMaterial.shader == null)
                return;

            Debug.LogWarning(
                $"Material '{sourceMaterial.name}' uses shader '{sourceMaterial.shader.name}', which does not support Control UI soft masks.",
                this);
            warnedUnsupportedShader = true;
        }

        private void DestroyModifiedMaterial()
        {
            if (modifiedMaterial == null)
                return;

            if (Application.isPlaying)
                Destroy(modifiedMaterial);
            else
                DestroyImmediate(modifiedMaterial);

            modifiedMaterial = null;
            baseMaterial = null;
        }
    }
}
