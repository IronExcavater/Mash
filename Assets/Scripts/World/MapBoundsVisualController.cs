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
        cachedRenderer ??= GetComponent<Renderer>();
        forcefieldMaterial = cachedRenderer?.sharedMaterial;

        if (!autoAssignReferences) return;
        mapBounds ??= FindFirstObjectByType<MapBounds>();
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
        ApplyTransparentRenderState(forcefieldMaterial);
        if (forcefieldMaterial.HasProperty("_EmissionColor"))
        {
            forcefieldMaterial.EnableKeyword("_EMISSION");
            var emission = Mathf.Lerp(0.85f, nearBoundaryEmissionMultiplier, near);
            forcefieldMaterial.SetColor("_EmissionColor", new Color(color.r, color.g, color.b, 1f) * emission);
        }

        if (!forceDoubleSided) return;
        ApplyDoubleSided(forcefieldMaterial);
    }

    private float ComputeRevealFactor()
    {
        if (mapBounds == null || revealTarget == null) return 1f;
        return mapBounds.ComputeRevealFactor(revealTarget.position, revealDistanceFromBoundary);
    }

    private static void ApplyTransparentRenderState(Material mat)
    {
        if (mat == null) return;

        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
        if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (mat.HasProperty("_SrcBlendAlpha")) mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        if (mat.HasProperty("_DstBlendAlpha")) mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);

        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = (int)RenderQueue.Transparent;
    }

    private static void ApplyDoubleSided(Material mat)
    {
        if (mat == null) return;
        if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
        if (mat.HasProperty("_CullMode")) mat.SetFloat("_CullMode", 0f);
        if (mat.HasProperty("_RenderFace")) mat.SetFloat("_RenderFace", 2f);
        if (mat.HasProperty("_DoubleSidedEnable")) mat.SetFloat("_DoubleSidedEnable", 1f);
    }
}
