using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Renderer))]
[AddComponentMenu("Gameplay/World/Map Bounds Visual")]
public class MapBoundsVisualController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private bool autoAssignReferences = true;
    [ConditionalField("autoAssignReferences", false)]
    [SerializeField] private MapBounds mapBounds;
    [ConditionalField("autoAssignReferences", false)]
    [SerializeField] private Transform revealTarget;

    [Header("Visuals")]
    [SerializeField] private bool showForcefieldVisual = true;
    [SerializeField] private Color forcefieldColor = new Color(0.14f, 0.65f, 1f, 0.16f);
    [SerializeField, Min(1f)] private float revealDistanceFromBoundary = 110f;
    [SerializeField, Range(0f, 1f)] private float farInteriorAlpha = 0.08f;
    [SerializeField, Min(0f)] private float pulseSpeed = 1.6f;
    [SerializeField, Min(0f)] private float pulseAlpha = 0.08f;

    [Header("Near-Boundary Boost")]
    [SerializeField, Range(0.1f, 2f)] private float nearBoundaryRevealExponent = 0.55f;
    [SerializeField, Range(0f, 1f)] private float nearBoundaryAlphaBoost = 0.32f;
    [SerializeField, Min(0f)] private float nearBoundaryPulseMultiplier = 2.2f;
    [SerializeField, Min(0f)] private float nearBoundaryEmissionMultiplier = 2.4f;

    [Header("Render")]
    [SerializeField, Min(0f)] private float insideVisibilityFloor = 0.08f;
    [SerializeField] private bool forceDoubleSided = true;

    private Renderer cachedRenderer;
    private Material forcefieldMaterial;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        UpdateVisual();
    }

    private void OnValidate()
    {
        ResolveReferences();
        if (!Application.isPlaying) UpdateVisual();
    }

    private void Update()
    {
        ResolveReferences();
        UpdateVisual();
    }

    private void ResolveReferences()
    {
        if (cachedRenderer == null) cachedRenderer = GetComponent<Renderer>();
        if (cachedRenderer != null)
            forcefieldMaterial = cachedRenderer.sharedMaterial;

        if (IsRetroLitForcefield(forcefieldMaterial))
        {
            var fallback = ResolveForcefieldShader();
            if (fallback != null && forcefieldMaterial.shader != fallback)
                forcefieldMaterial.shader = fallback;
        }

        if (!autoAssignReferences) return;
        if (mapBounds == null) mapBounds = FindFirstObjectByType<MapBounds>();
        if (revealTarget == null && mapBounds != null) revealTarget = mapBounds.RevealTargetTransform;
    }

    private void UpdateVisual()
    {
        if (cachedRenderer == null || forcefieldMaterial == null) return;
        cachedRenderer.enabled = showForcefieldVisual;
        if (!showForcefieldVisual) return;

        var reveal = ComputeRevealFactor();
        var near = Mathf.Pow(Mathf.Clamp01(reveal), nearBoundaryRevealExponent);
        var pulse = Mathf.Sin(Time.unscaledTime * pulseSpeed * 1.4f) * pulseAlpha * near * nearBoundaryPulseMultiplier;
        var color = forcefieldColor;
        var baseAlpha = Mathf.Lerp(farInteriorAlpha, forcefieldColor.a, near);
        color.a = Mathf.Clamp01(Mathf.Max(insideVisibilityFloor, baseAlpha + nearBoundaryAlphaBoost * near + pulse));

        forcefieldMaterial.color = color;
        if (forcefieldMaterial.HasProperty("_Color")) forcefieldMaterial.SetColor("_Color", color);
        if (forcefieldMaterial.HasProperty("_BaseColor")) forcefieldMaterial.SetColor("_BaseColor", color);
        ApplyTransparentForcefieldRenderState();
        if (forcefieldMaterial.HasProperty("_EmissionColor"))
        {
            forcefieldMaterial.EnableKeyword("_EMISSION");
            var emission = Mathf.Lerp(0.85f, nearBoundaryEmissionMultiplier, near);
            forcefieldMaterial.SetColor("_EmissionColor", new Color(color.r, color.g, color.b, 1f) * emission);
        }

        if (!forceDoubleSided) return;
        if (forcefieldMaterial.HasProperty("_Cull")) forcefieldMaterial.SetFloat("_Cull", 0f);
        if (forcefieldMaterial.HasProperty("_CullMode")) forcefieldMaterial.SetFloat("_CullMode", 0f);
        if (forcefieldMaterial.HasProperty("_RenderFace")) forcefieldMaterial.SetFloat("_RenderFace", 2f);
        if (forcefieldMaterial.HasProperty("_DoubleSidedEnable")) forcefieldMaterial.SetFloat("_DoubleSidedEnable", 1f);
    }

    private void ApplyTransparentForcefieldRenderState()
    {
        if (forcefieldMaterial == null) return;

        if (forcefieldMaterial.HasProperty("_Surface")) forcefieldMaterial.SetFloat("_Surface", 1f);
        if (forcefieldMaterial.HasProperty("_Blend")) forcefieldMaterial.SetFloat("_Blend", 0f);
        if (forcefieldMaterial.HasProperty("_SrcBlend")) forcefieldMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (forcefieldMaterial.HasProperty("_DstBlend")) forcefieldMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (forcefieldMaterial.HasProperty("_SrcBlendAlpha")) forcefieldMaterial.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        if (forcefieldMaterial.HasProperty("_DstBlendAlpha")) forcefieldMaterial.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        if (forcefieldMaterial.HasProperty("_ZWrite")) forcefieldMaterial.SetFloat("_ZWrite", 0f);

        forcefieldMaterial.SetOverrideTag("RenderType", "Transparent");
        forcefieldMaterial.renderQueue = (int)RenderQueue.Transparent;
    }

    private float ComputeRevealFactor()
    {
        if (mapBounds == null || revealTarget == null) return 1f;
        return mapBounds.ComputeRevealFactor(revealTarget.position, revealDistanceFromBoundary);
    }

    private static Shader ResolveForcefieldShader()
    {
        var shader = Shader.Find("Ultimate 10 Plus/Force Field");
        if (shader == null) shader = Shader.Find("Force Field");
        if (shader == null) shader = Shader.Find("Ultimate 10 Plus Shaders/Force Field");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        return shader;
    }

    private static bool IsRetroLitForcefield(Material mat)
    {
        if (mat == null || mat.shader == null) return false;
        var matName = mat.name.ToLowerInvariant();
        if (!matName.Contains("force") && !matName.Contains("field")) return false;
        return mat.shader.name.ToLowerInvariant().Contains("retro shaders pro/retro lit");
    }
}
