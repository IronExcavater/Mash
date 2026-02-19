using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Rigidbody))]
public class HelicopterCollisionHandler : MonoBehaviour
{
    [Header("Filtering")]
    [SerializeField] private LayerMask collisionLayers = ~0;
    [TagSelector]
    [SerializeField] private string hazardTag = "Tree";
    [SerializeField] private bool ignoreHazardsInsideMilitaryBase = false;
    [SerializeField, Min(0f)] private float militaryBaseSafePadding = 10f;

    [Header("Reactions")]
    [SerializeField] private bool disableMotorOnHazardHit = true;
    [ConditionalField("disableMotorOnHazardHit", true)]
    [SerializeField] private bool disableFlightControllerComponentOnHazardHit = true;
    [SerializeField] private bool logHitDetails;
    [SerializeField] private bool triggerCrashSequence = true;
    [ConditionalField("triggerCrashSequence", true)]
    [SerializeField, Min(0.1f)] private float crashDuration = 6.5f;
    [ConditionalField("triggerCrashSequence", true)]
    [SerializeField, Min(0.1f)] private float spinPhaseDuration = 2.2f;
    [ConditionalField("triggerCrashSequence", true)]
    [SerializeField, Min(0.05f)] private float liftPhaseDuration = 0.55f;
    [ConditionalField("triggerCrashSequence", true)]
    [SerializeField, Min(0f)] private float initialImpactForce = 12f;
    [ConditionalField("triggerCrashSequence", true)]
    [SerializeField, Min(0f)] private float initialUpKickForce = 2.2f;
    [ConditionalField("triggerCrashSequence", true)]
    [SerializeField, Min(0f)] private float initialSpinTorque = 12f;
    [ConditionalField("triggerCrashSequence", true)]
    [SerializeField, Min(0f)] private float tumbleTorque = 8f;
    [ConditionalField("triggerCrashSequence", true)]
    [SerializeField, Min(0f)] private float downwardAcceleration = 8f;
    [ConditionalField("triggerCrashSequence", true)]
    [SerializeField, Min(0f)] private float upwardLiftAcceleration = 8.5f;
    [ConditionalField("triggerCrashSequence", true)]
    [SerializeField, Min(0f)] private float spinAroundUpTorque = 18f;
    [ConditionalField("triggerCrashSequence", true)]
    [SerializeField, Min(0f)] private float randomTorqueNoise = 5f;
    [ConditionalField("triggerCrashSequence", true)]
    [SerializeField, Min(0f)] private float postCrashLinearDamping = 3.8f;
    [ConditionalField("triggerCrashSequence", true)]
    [SerializeField, Min(0f)] private float postCrashAngularDamping = 7.5f;
    [ConditionalField("triggerCrashSequence", true)]
    [SerializeField, Min(0f)] private float maxCrashAngularSpeed = 5.5f;
    [ConditionalField("triggerCrashSequence", true)]
    [SerializeField, Min(0f)] private float collisionEscapeDistance = 1.1f;
    [ConditionalField("triggerCrashSequence", true)]
    [SerializeField] private bool lockControlsAfterCrash = true;
    [ConditionalField("triggerCrashSequence", true)]
    [SerializeField] private bool ignoreAllHazardCollisionsAfterCrash = true;

    [Header("Events")]
    [SerializeField] private UnityEvent onAnyCollision;
    [SerializeField] private UnityEvent onHazardCollision;
    [SerializeField] private UnityEvent onCrashStarted;
    [SerializeField] private UnityEvent onCrashCompleted;

    [Header("Audio")]
    [SerializeField] private bool autoAssignAudioSource = true;
    [ConditionalField("autoAssignAudioSource", false)]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip[] hazardHitClips;
    [SerializeField] private AudioClip[] crashStartClips;
    [SerializeField] private AudioClip[] crashGroundImpactClips;
    [SerializeField] private AudioClip[] crashCompleteClips;
    [SerializeField] private AudioClip[] crashBurnLoopClips;
    [SerializeField] private AudioClip cockpitAlarmClip;
    [SerializeField, Range(0f, 1f)] private float cockpitAlarmVolume = 0.82f;
    [SerializeField, Range(0f, 1f)] private float hazardHitVolume = 0.9f;
    [SerializeField, Range(0f, 1f)] private float crashStartVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float crashCompleteVolume = 0.95f;
    [SerializeField] private bool useUnityBuiltInCrashParticles = true;
    [SerializeField] private bool spawnTreeImpactParticles = true;
    [ConditionalField("spawnTreeImpactParticles", true)]
    [SerializeField, Min(0f)] private float treeImpactParticleSize = 1.2f;
    [ConditionalField("spawnTreeImpactParticles", true)]
    [SerializeField, Min(0f)] private float treeImpactParticleLifetime = 1.25f;
    [SerializeField] private ParticleSystem smokeEffectPrefab;
    [SerializeField] private ParticleSystem fireEffectPrefab;
    [SerializeField] private Transform crashEffectsAnchor;
    [SerializeField] private bool detachPartsOnCrash = true;
    [ConditionalField("detachPartsOnCrash", true)]
    [SerializeField] private Transform[] detachableParts;
    [ConditionalField("detachPartsOnCrash", true)]
    [SerializeField, Min(0f)] private float detachedPartImpulse = 5f;

    private HelicopterFlightController flightController;
    private HelicopterRotorController rotorController;
    private Rigidbody body;
    private bool isCrashing;
    private bool crashCompleted;
    private float crashTimer;
    private Vector3 crashCenter;
    private Vector3 crashNormal;
    private int lastHazardHitClipIndex = -1;
    private int lastCrashStartClipIndex = -1;
    private int lastCrashGroundImpactClipIndex = -1;
    private int lastCrashCompleteClipIndex = -1;
    private int lastCrashBurnClipIndex = -1;
    private bool wreckEffectsSpawned;
    private AudioSource burnLoopSource;
    private AudioSource cockpitAlarmSource;
    private static Material cachedParticleAdditiveMaterial;
    private static Material cachedParticleAlphaMaterial;

    public Vector3 LastHitPoint { get; private set; }
    public Vector3 LastHitNormal { get; private set; }
    public Vector3 LastIncomingDirection { get; private set; }
    public Vector3 LastLocalHitDirection { get; private set; }
    public float LastImpactSpeed { get; private set; }
    public Collider LastCollider { get; private set; }
    public bool LastHitWasHazard { get; private set; }
    public bool IsCrashing => isCrashing;
    public bool IsCrashComplete => crashCompleted;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        flightController = GetComponent<HelicopterFlightController>();
        rotorController = GetComponent<HelicopterRotorController>();
        EnsureAudioSource();
    }

    private void OnDisable()
    {
        StopCockpitAlarmLoop();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isCrashing) return;

        var otherLayerBit = 1 << collision.gameObject.layer;
        if ((collisionLayers.value & otherLayerBit) == 0) return;

        var hasContact = collision.contactCount > 0;
        var point = hasContact ? collision.GetContact(0).point : transform.position;
        var normal = hasContact ? collision.GetContact(0).normal : (-collision.relativeVelocity).normalized;
        var impactSpeed = collision.relativeVelocity.magnitude;

        if (impactSpeed < 0.0001f && body != null) impactSpeed = body.linearVelocity.magnitude;
        if (normal.sqrMagnitude < 0.0001f) normal = -transform.forward;

        var incoming = -normal;

        LastHitPoint = point;
        LastHitNormal = normal.normalized;
        LastIncomingDirection = incoming.normalized;
        LastLocalHitDirection = transform.InverseTransformDirection(LastIncomingDirection);
        LastImpactSpeed = impactSpeed;
        LastCollider = collision.collider;
        LastHitWasHazard = IsHazard(collision.collider);

        if (logHitDetails)
            Debug.Log($"Hit '{collision.collider.name}', hazard={LastHitWasHazard}, speed={LastImpactSpeed:F2}, localDir={LastLocalHitDirection}", this);

        onAnyCollision?.Invoke();
        if (!LastHitWasHazard) return;

        if (disableMotorOnHazardHit && flightController != null)
            flightController.SetInputEnabled(false);

        if (spawnTreeImpactParticles)
            SpawnTreeImpactParticles(point, normal);

        if (triggerCrashSequence)
            BeginCrashSequence(point, normal, impactSpeed);

        onHazardCollision?.Invoke();
        PlayRandomClip(hazardHitClips, hazardHitVolume, ref lastHazardHitClipIndex);
    }

    private void FixedUpdate()
    {
        if (!isCrashing || crashCompleted || body == null) return;

        crashTimer += Time.fixedDeltaTime;
        var normalized = Mathf.Clamp01(crashTimer / Mathf.Max(0.1f, crashDuration));
        var chaos = 1f - normalized;
        var torqueNoise = Random.onUnitSphere * randomTorqueNoise * chaos;
        var tumbleAxis = Vector3.Cross(crashNormal, Vector3.up);
        if (tumbleAxis.sqrMagnitude < 0.001f) tumbleAxis = transform.right;
        tumbleAxis.Normalize();
        var awayFromImpact = body.worldCenterOfMass - crashCenter;
        awayFromImpact.y = 0f;

        var inSpinPhase = crashTimer < spinPhaseDuration;
        if (inSpinPhase)
        {
            var inLiftPhase = crashTimer < liftPhaseDuration;
            body.AddTorque(Vector3.up * spinAroundUpTorque * Mathf.Lerp(1f, 0.6f, crashTimer / Mathf.Max(0.1f, spinPhaseDuration)), ForceMode.Acceleration);
            body.AddTorque((tumbleAxis * tumbleTorque + torqueNoise) * (inLiftPhase ? 0.4f : 0.55f), ForceMode.Acceleration);
            if (inLiftPhase)
                body.AddForce(Vector3.up * upwardLiftAcceleration * Mathf.Lerp(1f, 0.2f, crashTimer / Mathf.Max(0.05f, liftPhaseDuration)), ForceMode.Acceleration);
            else
                body.AddForce(Vector3.down * downwardAcceleration * 0.62f, ForceMode.Acceleration);
        }
        else
        {
            body.AddTorque((tumbleAxis * tumbleTorque + torqueNoise) * Mathf.Lerp(0.7f, 0.25f, normalized), ForceMode.Acceleration);
            body.AddForce(Vector3.down * downwardAcceleration * Mathf.Lerp(0.8f, 1.3f, normalized), ForceMode.Acceleration);
        }

        if (maxCrashAngularSpeed > 0f)
            body.angularVelocity = Vector3.ClampMagnitude(body.angularVelocity, maxCrashAngularSpeed);

        if (awayFromImpact.sqrMagnitude > 0.0001f)
            body.AddForce(awayFromImpact.normalized * (initialImpactForce * 0.12f * chaos), ForceMode.Acceleration);

        if (!wreckEffectsSpawned && IsNearGround(3f))
        {
            SpawnCrashEffects();
            wreckEffectsSpawned = true;
            PlayGroundImpactAudio();
        }

        if (!wreckEffectsSpawned)
        {
            if (normalized < 1f) return;
            SpawnCrashEffects();
            wreckEffectsSpawned = true;
            PlayGroundImpactAudio();
        }

        crashCompleted = true;
        isCrashing = false;
        body.linearDamping = Mathf.Max(body.linearDamping, postCrashLinearDamping);
        body.angularDamping = Mathf.Max(body.angularDamping, postCrashAngularDamping);
        StopCockpitAlarmLoop();
        onCrashCompleted?.Invoke();
        PlayRandomClip(crashCompleteClips, crashCompleteVolume, ref lastCrashCompleteClipIndex);
    }

    private void BeginCrashSequence(Vector3 point, Vector3 normal, float impactSpeed)
    {
        if (body == null || isCrashing) return;

        isCrashing = true;
        crashCompleted = false;
        crashTimer = 0f;
        wreckEffectsSpawned = false;
        crashCenter = point;
        crashNormal = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;

        if (flightController != null)
        {
            if (lockControlsAfterCrash) flightController.SetInputEnabled(false);
            if (disableFlightControllerComponentOnHazardHit) flightController.enabled = false;
        }
        if (rotorController != null)
            rotorController.SetForceStopped(true);

        var currentVelocity = body.linearVelocity;
        body.linearVelocity = currentVelocity * 0.55f;
        var planarSpeed = new Vector3(currentVelocity.x, 0f, currentVelocity.z).magnitude;
        var incoming = planarSpeed > 0.05f ? new Vector3(currentVelocity.x, 0f, currentVelocity.z).normalized : -transform.forward;
        var impactScale = Mathf.Clamp01(impactSpeed / 20f);
        var impactImpulse = incoming * Mathf.Lerp(initialImpactForce * 0.4f, initialImpactForce, impactScale) + Vector3.up * initialUpKickForce;
        body.AddForceAtPosition(impactImpulse, point, ForceMode.VelocityChange);

        var spinAxis = Vector3.Cross(crashNormal, incoming);
        if (spinAxis.sqrMagnitude < 0.001f) spinAxis = transform.right;
        spinAxis.Normalize();
        body.AddTorque((spinAxis + Random.onUnitSphere * 0.45f) * initialSpinTorque, ForceMode.VelocityChange);
        var separation = -crashNormal + incoming;
        if (separation.sqrMagnitude > 0.0001f)
            body.position += separation.normalized * Mathf.Max(0f, collisionEscapeDistance);
        IgnoreCollisionWithHazard();
        if (ignoreAllHazardCollisionsAfterCrash)
        {
            IgnoreAllHazardTagCollisions();
            IgnoreAllLikelyTreeCollisions();
        }

        if (detachPartsOnCrash)
            DetachConfiguredParts(incoming);

        onCrashStarted?.Invoke();
        PlayRandomClip(crashStartClips, crashStartVolume, ref lastCrashStartClipIndex);
        PlayCockpitAlarmLoop();
    }

    private bool IsHazard(Collider hit)
    {
        if (hit == null) return false;
        if (ignoreHazardsInsideMilitaryBase && IsInsideMilitaryBaseSafeZone(hit.transform.position)) return false;
        if (!string.IsNullOrWhiteSpace(hazardTag))
        {
            if (hit.CompareTag(hazardTag)) return true;
            var root = hit.transform.root;
            if (root != null && root.CompareTag(hazardTag)) return true;
        }

        var lowerName = hit.name.ToLowerInvariant();
        if (lowerName.Contains("tree")) return true;
        if (lowerName.Contains("pine")) return true;
        if (lowerName.Contains("oak")) return true;
        if (lowerName.Contains("palm")) return true;
        if (lowerName.Contains("bush")) return true;
        var rootName = hit.transform.root != null ? hit.transform.root.name.ToLowerInvariant() : string.Empty;
        return rootName.Contains("tree") || rootName.Contains("pine") || rootName.Contains("oak") || rootName.Contains("palm");
    }

    private bool IsInsideMilitaryBaseSafeZone(Vector3 point)
    {
        var baseGen = FindFirstObjectByType<MilitaryBaseGenerator>();
        if (baseGen == null) return false;
        var center = baseGen.BaseCenterWorld;
        var radius = baseGen.ProtectedRadius + militaryBaseSafePadding;
        var delta = point - center;
        delta.y = 0f;
        return delta.sqrMagnitude <= radius * radius;
    }

    private bool IsNearGround(float distance)
    {
        var origin = transform.position + Vector3.up * 1f;
        return Physics.Raycast(origin, Vector3.down, distance, ~0, QueryTriggerInteraction.Ignore);
    }

    private void SpawnCrashEffects()
    {
        var anchor = crashEffectsAnchor != null ? crashEffectsAnchor : transform;

        if (useUnityBuiltInCrashParticles)
        {
            CreateUnityCrashFireAndSmoke(anchor);
        }
        else
        {
            if (smokeEffectPrefab != null)
                Instantiate(smokeEffectPrefab, anchor.position, anchor.rotation, anchor);
            else
                CreateFallbackCrashEffect(anchor.position, new Color(0.18f, 0.18f, 0.18f, 0.8f), 42, 1.3f, 2.8f);

            if (fireEffectPrefab != null)
                Instantiate(fireEffectPrefab, anchor.position + Vector3.up * 0.35f, anchor.rotation, anchor);
            else
                CreateFallbackCrashEffect(anchor.position + Vector3.up * 0.25f, new Color(1f, 0.42f, 0.1f, 0.9f), 32, 0.8f, 1.8f);
        }

        PlayCrashBurnLoop();
    }

    private void SpawnTreeImpactParticles(Vector3 point, Vector3 normal)
    {
        var go = new GameObject("TreeImpactBurst");
        go.transform.position = point + normal * 0.15f;
        go.transform.rotation = Quaternion.LookRotation(normal.sqrMagnitude > 0.001f ? normal : Vector3.up, Vector3.up);

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = Mathf.Max(0.1f, treeImpactParticleLifetime);
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, Mathf.Max(0.35f, treeImpactParticleLifetime));
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.6f, 8.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f * treeImpactParticleSize, 0.42f * treeImpactParticleSize);
        main.maxParticles = 80;
        main.gravityModifier = 1.2f;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.46f, 0.34f, 0.22f, 0.85f),
            new Color(0.18f, 0.18f, 0.18f, 0.75f));

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 42) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 22f;
        shape.radius = 0.2f;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.42f, 0.33f, 0.2f), 0f),
                new GradientColorKey(new Color(0.22f, 0.2f, 0.18f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.9f, 0f),
                new GradientAlphaKey(0.28f, 0.55f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = GetParticleMaterial(additive: false);
        renderer.sortMode = ParticleSystemSortMode.Distance;

        ps.Play();
        Object.Destroy(go, Mathf.Max(2f, treeImpactParticleLifetime + 1.2f));
    }

    private static void CreateUnityCrashFireAndSmoke(Transform anchor)
    {
        if (anchor == null) return;

        var root = new GameObject("CrashParticles");
        root.transform.SetParent(anchor, false);
        root.transform.localPosition = new Vector3(0f, 0.2f, 0f);
        root.transform.localRotation = Quaternion.identity;

        // Fire
        var fireGo = new GameObject("Fire");
        fireGo.transform.SetParent(root.transform, false);
        var fire = fireGo.AddComponent<ParticleSystem>();
        ConfigureFireParticles(fire);

        // Smoke
        var smokeGo = new GameObject("Smoke");
        smokeGo.transform.SetParent(root.transform, false);
        smokeGo.transform.localPosition = new Vector3(0f, 0.2f, 0f);
        var smoke = smokeGo.AddComponent<ParticleSystem>();
        ConfigureSmokeParticles(smoke);
    }

    private static void ConfigureFireParticles(ParticleSystem ps)
    {
        if (ps == null) return;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 1.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 1.25f);
        main.maxParticles = 240;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.12f;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.9f, 0.35f, 0.92f),
            new Color(1f, 0.35f, 0.05f, 0.85f));

        var emission = ps.emission;
        emission.rateOverTime = 74f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 18f;
        shape.radius = 0.9f;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.35f, 1f, 1.25f));

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var fireGradient = new Gradient();
        fireGradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.95f, 0.5f), 0f),
                new GradientColorKey(new Color(1f, 0.4f, 0.08f), 0.45f),
                new GradientColorKey(new Color(0.35f, 0.06f, 0.02f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.95f, 0.12f),
                new GradientAlphaKey(0.45f, 0.75f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = fireGradient;

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.52f;
        noise.frequency = 0.7f;
        noise.scrollSpeed = 0.45f;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = GetParticleMaterial(additive: true);
        renderer.sortMode = ParticleSystemSortMode.Distance;

        ps.Play();
    }

    private static void ConfigureSmokeParticles(ParticleSystem ps)
    {
        if (ps == null) return;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 8f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.6f, 5.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.7f, 3.9f);
        main.maxParticles = 280;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.05f;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.22f, 0.22f, 0.22f, 0.65f),
            new Color(0.1f, 0.1f, 0.1f, 0.55f));

        var emission = ps.emission;
        emission.rateOverTime = 36f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 10f;
        shape.radius = 1.25f;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.55f, 1f, 1.85f));

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var smokeGradient = new Gradient();
        smokeGradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.2f, 0.2f, 0.2f), 0f),
                new GradientColorKey(new Color(0.14f, 0.14f, 0.14f), 0.5f),
                new GradientColorKey(new Color(0.07f, 0.07f, 0.07f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.55f, 0.18f),
                new GradientAlphaKey(0.38f, 0.65f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = smokeGradient;

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.72f;
        noise.frequency = 0.28f;
        noise.scrollSpeed = 0.22f;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = GetParticleMaterial(additive: false);
        renderer.sortMode = ParticleSystemSortMode.Distance;

        ps.Play();
    }

    private static Material GetParticleMaterial(bool additive)
    {
        if (additive && cachedParticleAdditiveMaterial != null) return cachedParticleAdditiveMaterial;
        if (!additive && cachedParticleAlphaMaterial != null) return cachedParticleAlphaMaterial;

        Shader shader = null;
        if (additive)
        {
            shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Legacy Shaders/Particles/Additive");
        }
        else
        {
            shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
        }

        if (shader == null) return null;
        var mat = new Material(shader);
        mat.name = additive ? "CrashParticle_Additive" : "CrashParticle_Alpha";
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", additive ? 0f : 0f);
        if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);

        if (additive) cachedParticleAdditiveMaterial = mat;
        else cachedParticleAlphaMaterial = mat;
        return mat;
    }

    private static void CreateFallbackCrashEffect(Vector3 position, Color color, int maxParticles, float startSize, float lifetime)
    {
        var go = new GameObject("CrashEffect");
        go.transform.position = position;
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startColor = color;
        main.startLifetime = lifetime;
        main.startSpeed = 2.2f;
        main.startSize = startSize;
        main.maxParticles = maxParticles;
        main.loop = true;

        var emission = ps.emission;
        emission.rateOverTime = 22f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 24f;
        shape.radius = 0.8f;

        Object.Destroy(go, 10f);
    }

    private void DetachConfiguredParts(Vector3 incomingDirection)
    {
        if (detachableParts == null || detachableParts.Length == 0) return;
        for (var i = 0; i < detachableParts.Length; i++)
        {
            var part = detachableParts[i];
            if (part == null) continue;
            part.SetParent(null, true);
            var rb = part.GetComponent<Rigidbody>();
            if (rb == null) rb = part.gameObject.AddComponent<Rigidbody>();
            rb.mass = Mathf.Max(0.2f, rb.mass);
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            var col = part.GetComponent<Collider>();
            if (col == null) col = part.gameObject.AddComponent<BoxCollider>();

            var impulseDir = (incomingDirection + Random.onUnitSphere * 0.6f + Vector3.up * 0.4f).normalized;
            rb.AddForce(impulseDir * detachedPartImpulse, ForceMode.VelocityChange);
            rb.AddTorque(Random.onUnitSphere * detachedPartImpulse * 0.75f, ForceMode.VelocityChange);
        }
    }

    private void EnsureAudioSource()
    {
        if (autoAssignAudioSource && sfxSource == null)
        {
            var child = transform.Find("CollisionAudioSource");
            if (child == null)
            {
                var go = new GameObject("CollisionAudioSource");
                go.transform.SetParent(transform, false);
                child = go.transform;
            }

            sfxSource = child.GetComponent<AudioSource>();
            if (sfxSource == null) sfxSource = child.gameObject.AddComponent<AudioSource>();
        }
        if (sfxSource == null) return;
        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.spatialBlend = 1f;
    }

    private void PlayCrashBurnLoop()
    {
        if (crashBurnLoopClips == null || crashBurnLoopClips.Length == 0) return;
        if (burnLoopSource == null)
        {
            var loopGo = new GameObject("CrashBurnLoopSource");
            loopGo.transform.SetParent(transform, false);
            burnLoopSource = loopGo.AddComponent<AudioSource>();
            burnLoopSource.playOnAwake = false;
            burnLoopSource.loop = true;
            burnLoopSource.spatialBlend = 1f;
        }

        var clip = ChooseClip(crashBurnLoopClips, ref lastCrashBurnClipIndex);
        if (clip == null) return;
        burnLoopSource.clip = clip;
        burnLoopSource.volume = 0.85f;
        burnLoopSource.pitch = 1f;
        if (!burnLoopSource.isPlaying) burnLoopSource.Play();
    }

    private void PlayGroundImpactAudio()
    {
        PlayRandomClip(crashGroundImpactClips, Mathf.Clamp01(crashStartVolume), ref lastCrashGroundImpactClipIndex);
        // Layer a second transient for more impact on touchdown.
        PlayRandomClip(crashCompleteClips, Mathf.Clamp01(crashCompleteVolume * 0.7f), ref lastCrashCompleteClipIndex);
    }

    private void PlayCockpitAlarmLoop()
    {
        if (cockpitAlarmClip == null) return;
        if (cockpitAlarmSource == null)
        {
            var loopGo = new GameObject("CockpitAlarmLoopSource");
            loopGo.transform.SetParent(transform, false);
            cockpitAlarmSource = loopGo.AddComponent<AudioSource>();
            cockpitAlarmSource.playOnAwake = false;
            cockpitAlarmSource.loop = true;
            cockpitAlarmSource.spatialBlend = 1f;
        }

        cockpitAlarmSource.clip = cockpitAlarmClip;
        cockpitAlarmSource.volume = Mathf.Clamp01(cockpitAlarmVolume);
        cockpitAlarmSource.pitch = 1f;
        if (!cockpitAlarmSource.isPlaying) cockpitAlarmSource.Play();
    }

    private void StopCockpitAlarmLoop()
    {
        if (cockpitAlarmSource == null) return;
        if (cockpitAlarmSource.isPlaying) cockpitAlarmSource.Stop();
    }

    private void IgnoreCollisionWithHazard()
    {
        var hit = LastCollider;
        if (hit == null) return;
        var myCols = GetComponentsInChildren<Collider>(true);
        var theirCols = hit.transform.root != null ? hit.transform.root.GetComponentsInChildren<Collider>(true) : new[] { hit };
        for (var i = 0; i < myCols.Length; i++)
        {
            var mine = myCols[i];
            if (mine == null) continue;
            for (var j = 0; j < theirCols.Length; j++)
            {
                var theirs = theirCols[j];
                if (theirs == null) continue;
                Physics.IgnoreCollision(mine, theirs, true);
            }
        }
    }

    private void IgnoreAllHazardTagCollisions()
    {
        if (string.IsNullOrWhiteSpace(hazardTag)) return;

        GameObject[] hazards;
        try
        {
            hazards = GameObject.FindGameObjectsWithTag(hazardTag);
        }
        catch
        {
            return;
        }

        if (hazards == null || hazards.Length == 0) return;
        var myCols = GetComponentsInChildren<Collider>(true);
        if (myCols == null || myCols.Length == 0) return;

        for (var i = 0; i < hazards.Length; i++)
        {
            var h = hazards[i];
            if (h == null) continue;
            var theirCols = h.GetComponentsInChildren<Collider>(true);
            if (theirCols == null || theirCols.Length == 0) continue;

            for (var m = 0; m < myCols.Length; m++)
            {
                var mine = myCols[m];
                if (mine == null) continue;
                for (var t = 0; t < theirCols.Length; t++)
                {
                    var theirs = theirCols[t];
                    if (theirs == null) continue;
                    Physics.IgnoreCollision(mine, theirs, true);
                }
            }
        }
    }

    private void IgnoreAllLikelyTreeCollisions()
    {
        var myCols = GetComponentsInChildren<Collider>(true);
        if (myCols == null || myCols.Length == 0) return;

        var sceneCols = FindObjectsByType<Collider>(FindObjectsSortMode.None);
        for (var i = 0; i < sceneCols.Length; i++)
        {
            var other = sceneCols[i];
            if (other == null) continue;
            if (!LooksLikeTreeCollider(other)) continue;

            for (var m = 0; m < myCols.Length; m++)
            {
                var mine = myCols[m];
                if (mine == null) continue;
                Physics.IgnoreCollision(mine, other, true);
            }
        }
    }

    private bool LooksLikeTreeCollider(Collider col)
    {
        if (col == null) return false;
        if (!string.IsNullOrWhiteSpace(hazardTag))
        {
            if (col.CompareTag(hazardTag)) return true;
            var root = col.transform.root;
            if (root != null && root.CompareTag(hazardTag)) return true;
        }

        var lowerName = col.name.ToLowerInvariant();
        if (lowerName.Contains("tree")) return true;
        if (lowerName.Contains("pine")) return true;
        if (lowerName.Contains("oak")) return true;
        if (lowerName.Contains("palm")) return true;
        if (lowerName.Contains("bush")) return true;
        var rootName = col.transform.root != null ? col.transform.root.name.ToLowerInvariant() : string.Empty;
        return rootName.Contains("tree") || rootName.Contains("pine") || rootName.Contains("oak") || rootName.Contains("palm") || rootName.Contains("bush");
    }

    private void PlayRandomClip(AudioClip[] clips, float volume, ref int lastIndex)
    {
        if (sfxSource == null || clips == null || clips.Length == 0) return;
        var clip = ChooseClip(clips, ref lastIndex);
        if (clip == null) return;
        sfxSource.pitch = 1f;
        sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    private static AudioClip ChooseClip(AudioClip[] clips, ref int lastIndex)
    {
        if (clips == null || clips.Length == 0) return null;
        if (clips.Length == 1)
        {
            lastIndex = 0;
            return clips[0];
        }

        var index = Random.Range(0, clips.Length);
        if (index == lastIndex) index = (index + 1) % clips.Length;
        lastIndex = index;
        return clips[index];
    }

}
