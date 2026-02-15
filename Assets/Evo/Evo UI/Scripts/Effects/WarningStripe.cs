using UnityEngine;
using UnityEngine.UI;

namespace Evo.UI
{
    [ExecuteAlways]
    [RequireComponent(typeof(Graphic))]
    [AddComponentMenu("Evo/UI/Effects/Warning Lines")]
    public class WarningStripe : MonoBehaviour, IMaterialModifier
    {


        [Header("Settings")]
        [SerializeField, Range(0.01f, 0.5f)] private float lineWidth = 0.05f;
        [SerializeField, Range(0.01f, 1f)] private float lineSpacing = 0.2f;
        [SerializeField, Range(-180f, 180f)] private float lineAngle = 25;
        [SerializeField, Range(-5f, 5f)] private float scrollSpeed = 0.25f;

        // Property IDs
        [SerializeField, HideInInspector] private Shader embeddedShader;
        const string SHADER_NAME = "Evo/UI/Warning Stripe";
        static readonly int LINE_WIDTH_ID = Shader.PropertyToID("_LineWidth");
        static readonly int LINE_SPACING_ID = Shader.PropertyToID("_LineSpacing");
        static readonly int LINE_ANGLE_ID = Shader.PropertyToID("_LineAngle");
        static readonly int SCROLL_SPEED_ID = Shader.PropertyToID("_ScrollSpeed");

        // Cache
        Material uiMaterial;
        Graphic targetGraphic;
        Graphic TargetGraphic
        {
            get
            {
                if (targetGraphic == null) { targetGraphic = GetComponent<Graphic>(); }
                return targetGraphic;
            }
        }

        void OnEnable()
        {
            if (TargetGraphic != null)
            {
                TargetGraphic.SetMaterialDirty();
            }
        }

        void OnDisable()
        {
            if (TargetGraphic != null)
            {
                TargetGraphic.SetMaterialDirty();
            }
        }

        void Update()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
                UnityEditor.SceneView.RepaintAll();
            }
#endif
        }

        void OnDestroy()
        {
            if (uiMaterial != null)
            {
                if (Application.isPlaying) { Destroy(uiMaterial); }
                else { DestroyImmediate(uiMaterial); }
            }
        }

        public Material GetModifiedMaterial(Material baseMaterial)
        {
            // Create the material instance if needed
            if (uiMaterial == null || uiMaterial.shader.name != SHADER_NAME)
            {
                // Try embedded reference first, fall back to Find()
                Shader shader = embeddedShader;
                if (shader == null) { shader = Shader.Find(SHADER_NAME); }
                if (shader == null)
                {
                    Debug.LogError($"Shader '{SHADER_NAME}' not found.", this);
                    return baseMaterial;
                }

                uiMaterial = new Material(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            // Update our custom shader properties
            uiMaterial.SetFloat(LINE_WIDTH_ID, lineWidth);
            uiMaterial.SetFloat(LINE_SPACING_ID, lineSpacing);
            uiMaterial.SetFloat(LINE_ANGLE_ID, lineAngle);
            uiMaterial.SetFloat(SCROLL_SPEED_ID, scrollSpeed);

            // Copy Masking & Stencil properties from the base UI material
            // This makes standard Mask and RectMask2D components work.
            if (baseMaterial != null)
            {
                if (baseMaterial.HasProperty("_Stencil"))
                {
                    uiMaterial.SetFloat("_Stencil", baseMaterial.GetFloat("_Stencil"));
                    uiMaterial.SetFloat("_StencilComp", baseMaterial.GetFloat("_StencilComp"));
                    uiMaterial.SetFloat("_StencilOp", baseMaterial.GetFloat("_StencilOp"));
                    uiMaterial.SetFloat("_StencilReadMask", baseMaterial.GetFloat("_StencilReadMask"));
                    uiMaterial.SetFloat("_StencilWriteMask", baseMaterial.GetFloat("_StencilWriteMask"));
                    uiMaterial.SetFloat("_ColorMask", baseMaterial.GetFloat("_ColorMask"));
                }

                // Enable keywords for RectMask2D clipping
                if (baseMaterial.IsKeywordEnabled("UNITY_UI_CLIP_RECT")) { uiMaterial.EnableKeyword("UNITY_UI_CLIP_RECT"); }
                else { uiMaterial.DisableKeyword("UNITY_UI_CLIP_RECT"); }

                if (baseMaterial.IsKeywordEnabled("UNITY_UI_ALPHACLIP")) { uiMaterial.EnableKeyword("UNITY_UI_ALPHACLIP"); }
                else { uiMaterial.DisableKeyword("UNITY_UI_ALPHACLIP"); }
            }

            return uiMaterial;
        }

#if UNITY_EDITOR
        void Reset()
        {
            embeddedShader = Shader.Find(SHADER_NAME);
        }

        void OnValidate()
        {
            if (embeddedShader == null) { embeddedShader = Shader.Find(SHADER_NAME); }
            if (TargetGraphic != null) { TargetGraphic.SetMaterialDirty(); }
        }
#endif
    }
}