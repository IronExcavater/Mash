using UnityEngine;

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

        var velocity = helicopterBody.linearVelocity;
        var planarVel = new Vector3(velocity.x, 0f, velocity.z);
        var predictedPos = pos + planarVel * Mathf.Max(0f, anticipatoryLookAheadSeconds);
        var predictedDelta = predictedPos - center;
        predictedDelta.y = 0f;
        var predictedDist = predictedDelta.magnitude;
        var effectiveDist = Mathf.Max(dist, Mathf.Lerp(dist, predictedDist, Mathf.Clamp01(anticipatoryDistanceWeight)));
        if (effectiveDist <= softRadius) return;

        var outward = dist > 0.001f ? toPos / dist : Vector3.zero;
        var inward = -outward;
        var overSoft = effectiveDist - softRadius;
        var softRange = Mathf.Max(0.001f, hardRadius - softRadius);
        var force01 = Mathf.Clamp01(overSoft / softRange);
        var curvedForce = Mathf.Pow(force01, boundaryForceExponent);
        var totalInwardForce = inwardForce * curvedForce;
        if (force01 > 0.85f)
            totalInwardForce += hardBoundaryForceBoost * ((force01 - 0.85f) / 0.15f);
        if (force01 > 0.6f)
            totalInwardForce += nearHardBoundaryInwardBoost * ((force01 - 0.6f) / 0.4f);
        helicopterBody.AddForce(inward * totalInwardForce, ForceMode.Acceleration);

        var outwardSpeed = Vector3.Dot(planarVel, outward);
        if (outwardSpeed > 0f)
        {
            var damp = outward * outwardSpeed * outwardVelocityDamping * force01;
            helicopterBody.AddForce(-damp, ForceMode.Acceleration);

            var maxSoftOutward = Mathf.Lerp(softBoundaryMaxOutwardVelocity, hardBoundaryMaxOutwardVelocity, force01);
            if (outwardSpeed > maxSoftOutward)
            {
                var corrected = velocity - outward * (outwardSpeed - maxSoftOutward);
                helicopterBody.linearVelocity = corrected;
                velocity = corrected;
                planarVel = new Vector3(corrected.x, 0f, corrected.z);
                outwardSpeed = maxSoftOutward;
            }
        }

        var tangential = planarVel - outward * outwardSpeed;
        if (tangential.sqrMagnitude > 0.0001f)
        {
            var tangentialDamp = tangential * tangentialVelocityDamping * force01;
            helicopterBody.AddForce(-tangentialDamp, ForceMode.Acceleration);
        }

        if (dist <= hardRadius) return;
        var snapRadius = hardRadius * (1f - Mathf.Clamp01(hardBoundarySnapInwardBias));
        var clamped = center + outward * snapRadius;
        clamped.y = pos.y;
        helicopterBody.position = clamped;
        var velocityAfterClamp = helicopterBody.linearVelocity;
        var outwardAfterClamp = Vector3.Dot(velocityAfterClamp, outward);
        if (outwardAfterClamp > 0f)
            velocityAfterClamp -= outward * outwardAfterClamp;
        var inwardSpeed = Vector3.Dot(velocityAfterClamp, inward);
        if (inwardSpeed < minimumInwardRecoverySpeed)
            velocityAfterClamp += inward * (minimumInwardRecoverySpeed - inwardSpeed) * 0.35f;
        helicopterBody.linearVelocity = velocityAfterClamp;
    }

    private void Update()
    {
        if (Application.isPlaying) return;
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
        if (helicopterBody == null) helicopterBody = GetComponent<Rigidbody>();
        if (helicopterCrash == null) helicopterCrash = GetComponent<HelicopterCollisionHandler>();
        if (baseGenerator == null) baseGenerator = FindFirstObjectByType<MilitaryBaseGenerator>();
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
        if (forcefieldRenderer == null) return;
        forcefieldMaterial = forcefieldRenderer.sharedMaterial;
        if (forcefieldMaterial == null)
        {
            var fallback = ResolveForcefieldShader();
            if (fallback != null) forcefieldMaterial = new Material(fallback);
            forcefieldRenderer.sharedMaterial = forcefieldMaterial;
        }
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

        var center = BoundsCenter;
        var pulse = 1f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseScale;
        // Visual transform is controlled by MapBoundsVisualController on the map-bounds object.

        var reveal = ComputeRevealFactor();
        var c = forcefieldColor;
        c.a = Mathf.Clamp01(Mathf.Lerp(farInteriorAlpha, forcefieldColor.a, reveal) + Mathf.Sin(Time.unscaledTime * pulseSpeed * 1.4f) * pulseAlpha * reveal);

        ApplyForcefieldMaterialSettings(c);
    }

    private float ComputeRevealFactor()
    {
        if (helicopterBody == null) return 1f;
        var center = BoundsCenter;
        var delta = helicopterBody.position - center;
        delta.y = 0f;
        var dist = delta.magnitude;
        var innerDistanceToBoundary = SoftRadius - dist;
        if (innerDistanceToBoundary <= 0f) return 1f;
        return 1f - Mathf.Clamp01(innerDistanceToBoundary / Mathf.Max(0.01f, revealDistanceFromBoundary));
    }

    public float ComputeRevealFactor(Vector3 worldPosition, float revealDistance)
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
        get
        {
            if (helicopterBody != null) return helicopterBody.transform;
            return transform;
        }
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

    private static Shader ResolveForcefieldShader()
    {
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Shader Graphs/Respawn");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        return shader;
    }

    private void ApplyForcefieldMaterialSettings(Color color)
    {
        if (forcefieldMaterial == null) return;
        forcefieldMaterial.color = color;
        if (forcefieldMaterial.HasProperty("_BaseColor")) forcefieldMaterial.SetColor("_BaseColor", color);
        if (forcefieldMaterial.HasProperty("_EmissionColor"))
        {
            forcefieldMaterial.EnableKeyword("_EMISSION");
            forcefieldMaterial.SetColor("_EmissionColor", new Color(color.r, color.g, color.b, 1f) * 0.9f);
        }

        // Force visible from inside + outside where shader supports culling controls.
        if (forcefieldMaterial.HasProperty("_Cull")) forcefieldMaterial.SetFloat("_Cull", 0f);
        if (forcefieldMaterial.HasProperty("_CullMode")) forcefieldMaterial.SetFloat("_CullMode", 0f);
        if (forcefieldMaterial.HasProperty("_RenderFace")) forcefieldMaterial.SetFloat("_RenderFace", 2f);
        if (forcefieldMaterial.HasProperty("_DoubleSidedEnable")) forcefieldMaterial.SetFloat("_DoubleSidedEnable", 1f);
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
