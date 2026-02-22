using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[RequireComponent(typeof(Rigidbody))]
[AddComponentMenu("Gameplay/World/Map Bounds")]
public class MapBounds : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private bool autoAssignReferences = true;
    [ConditionalField("autoAssignReferences", false)]
    [SerializeField] private MilitaryBaseGenerator baseGenerator;
    [ConditionalField("autoAssignReferences", false)]
    [SerializeField] private Rigidbody helicopterBody;
    [ConditionalField("autoAssignReferences", false)]
    [SerializeField] private HelicopterCollisionHandler helicopterCrash;
    [ConditionalField("autoAssignReferences", false)]
    [SerializeField] private Transform forcefieldVisual;

    [Header("Bounds")]
    [SerializeField, Min(80f)] private float fallbackRadius = 560f;
    [SerializeField, Min(0f)] private float softBoundaryPadding = 340f;
    [SerializeField, Min(0f)] private float hardBoundaryPadding = 500f;
    [SerializeField, Min(0f)] private float inwardForce = 12f;
    [SerializeField, Min(0f)] private float outwardVelocityDamping = 2.6f;
    [SerializeField, Min(0f)] private float hardBoundaryForceBoost = 18f;
    [SerializeField, Min(1f)] private float boundaryForceExponent = 1.55f;
    [SerializeField, Min(0f)] private float minimumInwardRecoverySpeed = 2f;
    [SerializeField, Min(0f)] private float anticipatoryLookAheadSeconds = 1.1f;
    [SerializeField, Min(0f)] private float anticipatoryDistanceWeight = 0.9f;
    [SerializeField, Min(0f)] private float tangentialVelocityDamping = 0.8f;
    [SerializeField, Min(0f)] private float nearHardBoundaryInwardBoost = 10f;
    [SerializeField, Range(0f, 1f)] private float hardBoundarySnapInwardBias = 0.01f;
    [SerializeField, Min(0f)] private float hardBoundaryMaxOutwardVelocity = 0.75f;
    [SerializeField, Min(0f)] private float softBoundaryMaxOutwardVelocity = 2.2f;

    [Header("Forcefield")]
    [SerializeField] private bool manageVisualsOnThisComponent = false;
    [ConditionalField("manageVisualsOnThisComponent", true)]
    [SerializeField] private bool showForcefieldVisual = true;
    [ConditionalField("manageVisualsOnThisComponent", true)]
    [SerializeField] private Color forcefieldColor = new Color(0.14f, 0.65f, 1f, 0.16f);
    [ConditionalField("manageVisualsOnThisComponent", true)]
    [SerializeField, Min(1f)] private float revealDistanceFromBoundary = 110f;
    [ConditionalField("manageVisualsOnThisComponent", true)]
    [SerializeField, Range(0f, 1f)] private float farInteriorAlpha = 0f;
    [ConditionalField("manageVisualsOnThisComponent", true)]
    [SerializeField, Min(0f)] private float pulseSpeed = 1.6f;
    [ConditionalField("manageVisualsOnThisComponent", true)]
    [SerializeField, Min(0f)] private float pulseScale = 0.03f;
    [ConditionalField("manageVisualsOnThisComponent", true)]
    [SerializeField, Min(0f)] private float pulseAlpha = 0.08f;

    private Renderer forcefieldRenderer;
    private Material forcefieldMaterial;

    public Vector3 BoundsCenter => GetBoundsCenter();
    public float SoftRadius => Mathf.Max(10f, GetBaseRadius() + softBoundaryPadding);
    public float HardRadius => Mathf.Max(SoftRadius + 5f, GetBaseRadius() + hardBoundaryPadding);

    private void Awake()
    {
        ResolveReferences();
        RefreshVisualBinding();
    }

    private void OnEnable()
    {
        ResolveReferences();
        RefreshVisualBinding();
    }

    private void OnValidate()
    {
        softBoundaryPadding = Mathf.Max(0f, softBoundaryPadding);
        hardBoundaryPadding = Mathf.Max(softBoundaryPadding + 5f, hardBoundaryPadding);
        if (!Application.isPlaying) return;
        ResolveReferences();
        RefreshVisualBinding();
    }

    private void FixedUpdate()
    {
        if (helicopterBody == null && autoAssignReferences)
            ResolveReferences();
        if (helicopterBody == null) return;
        if (helicopterCrash != null && (helicopterCrash.IsCrashing || helicopterCrash.IsCrashComplete))
            return;
        if (manageVisualsOnThisComponent) UpdateVisual();

        var center = BoundsCenter;
        var softRadius = SoftRadius;
        var hardRadius = HardRadius;

        var pos = helicopterBody.position;
        var toPos = pos - center;
        toPos.y = 0f;
        var dist = toPos.magnitude;
        if (dist <= softRadius) return;

        var velocity = helicopterBody.linearVelocity;
        var planarVel = new Vector3(velocity.x, 0f, velocity.z);
        var outward = dist > 0.001f ? toPos / dist : Vector3.zero;
        var inward = -outward;

        var zoneWidth = Mathf.Max(0.001f, hardRadius - softRadius);
        var edge01 = Mathf.Clamp01((dist - softRadius) / zoneWidth);

        var predictedPos = pos + planarVel * Mathf.Max(0f, anticipatoryLookAheadSeconds);
        var predictedDelta = predictedPos - center;
        predictedDelta.y = 0f;
        var predictedDist = predictedDelta.magnitude;
        if (predictedDist > dist)
        {
            var predictedEdge = Mathf.Clamp01((predictedDist - softRadius) / zoneWidth);
            edge01 = Mathf.Max(edge01, Mathf.Lerp(edge01, predictedEdge, Mathf.Clamp01(anticipatoryDistanceWeight)));
        }

        var outwardSpeed = Vector3.Dot(planarVel, outward);
        var edgeCurve = Mathf.Pow(edge01, boundaryForceExponent);

        var pushAccel = inwardForce * (0.35f + edgeCurve);
        pushAccel += hardBoundaryForceBoost * edgeCurve;
        pushAccel += nearHardBoundaryInwardBoost * Mathf.SmoothStep(0f, 1f, edge01);

        var desiredOutwardSpeed = Mathf.Lerp(0f, -minimumInwardRecoverySpeed, edge01);
        var speedError = desiredOutwardSpeed - outwardSpeed;
        var speedCorrectionAccel = speedError * outwardVelocityDamping;
        var finalInwardAccel = Mathf.Max(0f, pushAccel + speedCorrectionAccel);
        helicopterBody.AddForce(inward * finalInwardAccel, ForceMode.Acceleration);

        var tangential = planarVel - outward * outwardSpeed;
        if (tangential.sqrMagnitude > 0.0001f)
        {
            var tangentialDamp = tangential * tangentialVelocityDamping * (0.2f + edge01);
            helicopterBody.AddForce(-tangentialDamp, ForceMode.Acceleration);
        }

        if (dist > hardRadius)
        {
            var clampRadius = hardRadius * (1f - Mathf.Clamp01(hardBoundarySnapInwardBias));
            var clampedPos = center + outward * clampRadius;
            clampedPos.y = pos.y;
            helicopterBody.position = clampedPos;

            var clampedVelocity = helicopterBody.linearVelocity;
            var planarClamped = new Vector3(clampedVelocity.x, 0f, clampedVelocity.z);
            var clampedOutwardSpeed = Vector3.Dot(planarClamped, outward);
            if (clampedOutwardSpeed > hardBoundaryMaxOutwardVelocity)
                clampedVelocity -= outward * (clampedOutwardSpeed - hardBoundaryMaxOutwardVelocity);

            var clampedInwardSpeed = -Vector3.Dot(new Vector3(clampedVelocity.x, 0f, clampedVelocity.z), outward);
            if (clampedInwardSpeed < minimumInwardRecoverySpeed * 0.5f)
                clampedVelocity += inward * (minimumInwardRecoverySpeed * 0.5f - clampedInwardSpeed);

            helicopterBody.linearVelocity = clampedVelocity;
        }
        else if (outwardSpeed > softBoundaryMaxOutwardVelocity)
        {
            helicopterBody.linearVelocity = velocity - outward * (outwardSpeed - softBoundaryMaxOutwardVelocity);
        }
    }

    private void Update()
    {
        if (Application.isPlaying) return;
        if (autoAssignReferences && (baseGenerator == null || helicopterBody == null || forcefieldVisual == null))
            ResolveReferences();
        if (manageVisualsOnThisComponent) UpdateVisual();
    }

    public bool IsWithinSoftBounds(Vector3 worldPosition, float inset = 0f)
    {
        var delta = worldPosition - BoundsCenter;
        delta.y = 0f;
        var r = Mathf.Max(5f, SoftRadius - Mathf.Max(0f, inset));
        return delta.sqrMagnitude <= r * r;
    }

    public Vector3 ClampToSoftBounds(Vector3 worldPosition, float inset = 0f)
    {
        var center = BoundsCenter;
        var delta = worldPosition - center;
        delta.y = 0f;
        var r = Mathf.Max(5f, SoftRadius - Mathf.Max(0f, inset));
        var mag = delta.magnitude;
        if (mag <= r || mag <= 0.001f) return worldPosition;

        var clamped = center + delta / mag * r;
        clamped.y = worldPosition.y;
        return clamped;
    }

    private void ResolveReferences()
    {
        if (!autoAssignReferences) return;
        helicopterBody ??= GetComponent<Rigidbody>();
        helicopterCrash ??= GetComponent<HelicopterCollisionHandler>();
        baseGenerator ??= FindFirstObjectByType<MilitaryBaseGenerator>();
        var existing = GameObject.Find("Map Bounds");
        if (existing != null && forcefieldVisual != existing.transform)
            forcefieldVisual = existing.transform;
        // If no "Map Bounds" object exists, keep current explicit assignment as-is.
    }

    private void RefreshVisualBinding()
    {
        if (forcefieldVisual == null)
        {
            forcefieldRenderer = null;
            forcefieldMaterial = null;
            return;
        }

        forcefieldRenderer = forcefieldVisual.GetComponent<Renderer>();
        forcefieldMaterial = forcefieldRenderer?.sharedMaterial;
        if (forcefieldMaterial == null) return;
        ApplyForcefieldMaterialSettings(forcefieldColor);
    }

    private void UpdateVisual()
    {
        if (forcefieldVisual == null || forcefieldRenderer == null || forcefieldMaterial == null)
        {
            RefreshVisualBinding();
            if (forcefieldVisual == null || forcefieldRenderer == null || forcefieldMaterial == null) return;
        }

        forcefieldVisual.gameObject.SetActive(showForcefieldVisual);
        if (!showForcefieldVisual) return;

        var reveal = ComputeRevealFactor();
        var c = forcefieldColor;
        var pulse = Mathf.Sin(Time.unscaledTime * pulseSpeed * 1.4f) * pulseAlpha * (1f + pulseScale);
        c.a = Mathf.Clamp01(Mathf.Lerp(farInteriorAlpha, forcefieldColor.a, reveal) + pulse * reveal);

        ApplyForcefieldMaterialSettings(c);
    }

    private float ComputeRevealFactor()
    {
        if (helicopterBody == null) return 1f;
        return ComputeRevealFactorAtPosition(helicopterBody.position, revealDistanceFromBoundary);
    }

    public float ComputeRevealFactor(Vector3 worldPosition, float revealDistance)
    {
        return ComputeRevealFactorAtPosition(worldPosition, revealDistance);
    }

    private float ComputeRevealFactorAtPosition(Vector3 worldPosition, float revealDistance)
    {
        var center = BoundsCenter;
        var delta = worldPosition - center;
        delta.y = 0f;
        var dist = delta.magnitude;
        var innerDistanceToBoundary = SoftRadius - dist;
        if (innerDistanceToBoundary <= 0f) return 1f;
        return 1f - Mathf.Clamp01(innerDistanceToBoundary / Mathf.Max(0.01f, revealDistance));
    }

    public Transform RevealTargetTransform
    {
        get => helicopterBody != null ? helicopterBody.transform : transform;
    }

    private Vector3 GetBoundsCenter()
    {
        if (baseGenerator != null) return baseGenerator.BaseCenterWorld;
        return transform.position;
    }

    private float GetBaseRadius()
    {
        if (baseGenerator != null) return baseGenerator.ProtectedRadius;
        return fallbackRadius;
    }

    private void ApplyForcefieldMaterialSettings(Color color)
    {
        if (forcefieldMaterial == null) return;
        forcefieldMaterial.color = color;
        if (forcefieldMaterial.HasProperty("_BaseColor")) forcefieldMaterial.SetColor("_BaseColor", color);
        ApplyTransparentRenderState(forcefieldMaterial);
        if (forcefieldMaterial.HasProperty("_EmissionColor"))
        {
            forcefieldMaterial.EnableKeyword("_EMISSION");
            forcefieldMaterial.SetColor("_EmissionColor", new Color(color.r, color.g, color.b, 1f) * 0.9f);
        }

        ApplyDoubleSided(forcefieldMaterial);
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

    private void OnDrawGizmosSelected()
    {
        var center = BoundsCenter;
        Gizmos.color = new Color(0.25f, 0.7f, 1f, 0.7f);
        Gizmos.DrawWireSphere(center, SoftRadius);
        Gizmos.color = new Color(1f, 0.45f, 0.2f, 0.85f);
        Gizmos.DrawWireSphere(center, HardRadius);
    }
}
