using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class HelicopterFlightController : MonoBehaviour
{
    [System.Serializable]
    private class SimpleControlSettings
    {
        public float planarVelocityGain = 3.5f;
        public float planarBrakeGain = 2.7f;
        public float maxPlanarAcceleration = 18f;
        public float maxHorizontalSpeed = 33f;
        public bool holdHeading;
        public float yawToVelocityMinSpeed = 1.2f;
        public float yawFollowSpeed = 9f;
        [Range(0f, 1f)] public float liftUprightBlend = 0.9f;
    }

    [System.Serializable]
    private class ComplexControlSettings
    {
        [Range(0f, 35f)] public float maxPitch = 16f;
        [Range(0f, 35f)] public float maxRoll = 16f;
        public float yawRate = 110f;
        public float planarDamping = 0.15f;
        public float cyclicAssistAcceleration = 12f;
        public float lateralStrafeAssistAcceleration = 1.8f;
        public bool autoYawToVelocity = true;
        public float yawToVelocityMinSpeed = 1.2f;
        public float yawFollowSpeed = 9f;
        [Range(0f, 1f)] public float liftUprightBlend = 0.15f;
    }

    [System.Serializable]
    private class ZLockSettings
    {
        public bool enabled = true;
        public float groundClearance = 2.2f;
        public float groundClearanceProbeDistance = 150f;
        public float lookAheadTime = 1.05f;
        public float lookAheadMaxDistance = 28f;
        public float sampleRadius = 4.5f;
        public float holdResponse = 2.8f;
        public float holdDamping = 1.8f;
        public float maxCorrectionSpeed = 12f;
    }

    [System.Serializable]
    private class InputSettings
    {
        public bool autoEnableActions = true;
        public bool keyboardFallback = false;
        [Range(0.1f, 4f)] public float mouseSensitivity = 1.7f;
        public InputActionReference moveAction;
        public InputActionReference ascendAction;
        public InputActionReference descendAction;
    }

    [System.Serializable]
    private class EngineSettings
    {
        public bool startOn = true;
        public float spoolUpRate = 1f;
        public float spoolDownRate = 0.4f;
    }

    [System.Serializable]
    private class LiftSettings
    {
        public float maxAcceleration = 26f;
        public float hoverThrottle = 0.55f;
        public float collectiveResponse = 0.4f;
        public float verticalInputResponseSpeed = 2.5f;
        public float verticalVelocityGain = 3.4f;
        public float maxVerticalSpeed = 10f;
        public float descentSpeedMultiplier = 0.55f;
        public float minLiftUpDot = 0.45f;
        public float maxLiftCompensation = 1.35f;
    }

    [System.Serializable]
    private class AltitudeHoldSettings
    {
        public bool enabled = true;
        public float strength = 2f;
        public float damping = 1.4f;
        public float maxSpeed = 4f;
        public float guidanceBlend = 0.6f;
        public float hoverCollectiveTrim = -0.04f;
    }

    [System.Serializable]
    private class AttitudeSettings
    {
        public float yawStiffness = 26f;
        public float yawDamping = 8f;
        public float maxYawTorque = 28f;
        public float tiltStiffness = 22f;
        public float tiltDamping = 7f;
        public float maxTiltTorque = 30f;
        public float emergencyUprightTorque = 36f;
        public float maxPitchRollRate = 2.5f;
    }

    [System.Serializable]
    private class WindSettings
    {
        public bool enabled = true;
        public float horizontalAcceleration = 1f;
        public float verticalAcceleration = 0.35f;
        public float frequency = 0.2f;
    }

    [System.Serializable]
    private class GroundContactSettings
    {
        public float landingProbeDistance = 2f;
        public float probeSkin = 0.08f;
        public float landingMaxVerticalSpeed = 1.1f;
        public float landingMaxPlanarSpeed = 1.4f;
        public LayerMask groundLayers = ~0;
        public float landedLockForce = 8f;
        public float landedLockTorque = 10f;
    }

    [System.Serializable]
    private class GroundTagFilterSettings
    {
        public bool useGroundTagFiltering = true;
        public bool allowUntaggedWhenFiltering = true;
        public string[] allowedGroundTags = { "Terrain", "Ground", "GroundSurface", "Runway", "HoverGround" };
        public string[] ignoredGroundTags = { };
    }

    [System.Serializable]
    private class HoverZoneSettings
    {
        public bool enabled = true;
        public bool requireTagMatch = false;
        public string[] acceptedZoneTags = { "HoverHeightZone", "HelicopterHoverZone" };
    }

    [System.Serializable]
    private class SpawnSettings
    {
        public float clearanceAboveGround = 10f;
        public float groundProbeDistance = 300f;
    }

    [System.Serializable]
    private class AutoShutdownSettings
    {
        public bool enabled = true;
        public float delay = 1.75f;
        public float maxVerticalSpeed = 1.75f;
        public float maxPlanarSpeed = 2.2f;
        public float groundProbeDistance = 4f;
        public float moveDeadzone = 0.12f;
        public float verticalDeadzone = 0.12f;
    }

    [System.Serializable]
    private class EngineRestartSettings
    {
        public bool enabled = true;
        public float liftInputThreshold = 0.25f;
    }

    [System.Serializable]
    private class RigidbodySettings
    {
        public bool applySettings;
        public bool ensureCanSimulate = true;
        public float mass = 2600f;
        public float linearDamping = 0.35f;
        public float angularDamping = 2.5f;
        public float maxAngularSpeed = 8f;
        public Vector3 centerOfMassOffset = new Vector3(0f, -0.2f, 0f);
    }

    public enum ControlScheme
    {
        Simple = 0,
        Complex = 1
    }

    [SerializeField] private ControlScheme controlScheme = ControlScheme.Simple;
    [FieldHeader("Simple Control", "controlScheme", (int)ControlScheme.Simple)]
    [SerializeField] private SimpleControlSettings simpleControl = new SimpleControlSettings();
    [FieldHeader("Complex Control", "controlScheme", (int)ControlScheme.Complex)]
    [SerializeField] private ComplexControlSettings complexControl = new ComplexControlSettings();
    [FieldHeader("Ground Clearance Z Lock")]
    [SerializeField] private ZLockSettings zLock = new ZLockSettings();

    [FieldHeader("Input")]
    [SerializeField] private InputSettings inputSettings = new InputSettings();

    [FieldHeader("Engine")]
    [SerializeField] private EngineSettings engineSettings = new EngineSettings();

    [FieldHeader("Lift")]
    [SerializeField] private LiftSettings liftSettings = new LiftSettings();

    [FieldHeader("Altitude Hold")]
    [SerializeField] private AltitudeHoldSettings altitudeHold = new AltitudeHoldSettings();
    [FieldHeader("Attitude")]
    [SerializeField] private AttitudeSettings attitude = new AttitudeSettings();
    [FieldHeader("Wind")]
    [SerializeField] private WindSettings wind = new WindSettings();
    [FieldHeader("Ground")]
    [SerializeField] private GroundContactSettings ground = new GroundContactSettings();
    [FieldHeader("Ground Tag Filtering")]
    [SerializeField] private GroundTagFilterSettings groundTagFilter = new GroundTagFilterSettings();
    [FieldHeader("Hover Zones")]
    [SerializeField] private HoverZoneSettings hoverZones = new HoverZoneSettings();
    [FieldHeader("Spawn")]
    [SerializeField] private SpawnSettings spawn = new SpawnSettings();
    [FieldHeader("Startup Placement")]
    [SerializeField] private bool reapplyPlacementAfterTerrainGeneration = true;
    [ConditionalField("reapplyPlacementAfterTerrainGeneration", true)]
    [SerializeField, Min(1)] private int placementRetryFramesAfterGeneration = 24;
    [FieldHeader("Ground Auto Engine Off")]
    [SerializeField] private AutoShutdownSettings autoShutdown = new AutoShutdownSettings();
    [FieldHeader("Engine Restart")]
    [SerializeField] private EngineRestartSettings engineRestart = new EngineRestartSettings();
    [FieldHeader("Rigidbody")]
    [SerializeField] private RigidbodySettings physicsSettings = new RigidbodySettings();

    private Rigidbody body;
    private InputAction resolvedMoveAction;
    private InputAction resolvedAscendAction;
    private InputAction resolvedDescendAction;

    private bool inputEnabled = true;
    private bool engineOn;
    private bool isGrounded;
    private bool isLanded;
    private float enginePower;
    private Vector2 currentMoveInput;
    private Vector2 currentLiftTiltInput;
    private Vector3 headingForward = Vector3.forward;
    private float heldAltitude;
    private bool hasHeldAltitude;
    private float currentVerticalInput;
    private float landedShutdownTimer;
    private float windSeedX;
    private float windSeedY;
    private float windSeedZ;
    private Collider[] cachedColliders;
    private readonly RaycastHit[] groundHitBuffer = new RaycastHit[16];
    private bool startupPlacementComplete;
    private bool controlLockActive;
    private TerrainGenerator terrainGenerator;
    private Coroutine deferredPlacementRoutine;
    private struct HoverZoneInfluence
    {
        public float clearanceOffset;
        public float minimumWorldAltitude;
        public float fixedWorldAltitude;
    }

    private readonly Dictionary<int, HoverZoneInfluence> activeHoverZones = new Dictionary<int, HoverZoneInfluence>();

    public bool IsInputEnabled => inputEnabled;
    public bool IsEngineOn => engineOn;
    public bool IsGrounded => isGrounded;
    public bool IsLanded => isLanded;
    public float CurrentVerticalSpeed => body != null ? body.linearVelocity.y : 0f;
    public float CurrentPlanarSpeed
    {
        get
        {
            if (body == null) return 0f;
            var velocity = body.linearVelocity;
            return new Vector2(velocity.x, velocity.z).magnitude;
        }
    }
    public float EnginePower01 => enginePower;
    public Vector2 CurrentMoveInput => currentMoveInput;
    public Vector2 CurrentLiftTiltInput => currentLiftTiltInput;
    public float MouseSensitivity => inputSettings.mouseSensitivity;
    public float HorizontalSpeed01 { get; private set; }
    public ControlScheme CurrentControlScheme => controlScheme;
    private bool IsSimpleControl => controlScheme == ControlScheme.Simple;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        CacheColliders();
        ConfigureRigidbody();

        engineOn = engineSettings.startOn;
        enginePower = engineOn ? 1f : 0f;
        heldAltitude = body.position.y;
        hasHeldAltitude = altitudeHold.enabled;
        startupPlacementComplete = false;

        var planarForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (planarForward.sqrMagnitude > 0.0001f) headingForward = planarForward.normalized;

        windSeedX = Random.value * 100f;
        windSeedY = Random.value * 100f;
        windSeedZ = Random.value * 100f;
    }

    private void OnEnable()
    {
        ResolveActions();
        if (inputSettings.autoEnableActions) SetActionsEnabled(true);
        SubscribeGenerationCallbacks();
    }

    private void OnDisable()
    {
        if (inputSettings.autoEnableActions) SetActionsEnabled(false);
        UnsubscribeGenerationCallbacks();
        if (deferredPlacementRoutine != null)
        {
            StopCoroutine(deferredPlacementRoutine);
            deferredPlacementRoutine = null;
        }
    }

    private void FixedUpdate()
    {
        EnsurePhysicsCanSimulate();
        if (!TryCompleteStartupPlacement()) return;

        var dt = Time.fixedDeltaTime;
        if (controlLockActive)
        {
            UpdateEnginePower(dt);
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            currentMoveInput = Vector2.zero;
            currentLiftTiltInput = Vector2.zero;
            currentVerticalInput = 0f;
            return;
        }

        UpdateGroundState();
        UpdateEnginePower(dt);

        var moveInput = inputEnabled ? ReadMoveInput() : Vector2.zero;
        var verticalInputRaw = inputEnabled ? ReadVerticalInput() : 0f;

        if (!engineOn)
        {
            if (engineRestart.enabled && verticalInputRaw > engineRestart.liftInputThreshold)
            {
                engineOn = true;
                landedShutdownTimer = 0f;
                heldAltitude = body.position.y;
                hasHeldAltitude = true;
            }
            else
            {
                moveInput = Vector2.zero;
                verticalInputRaw = 0f;
            }
        }

        if (zLock.enabled)
            verticalInputRaw = 0f;

        var verticalStep = liftSettings.verticalInputResponseSpeed <= 0f ? 1f : liftSettings.verticalInputResponseSpeed * dt;
        currentVerticalInput = Mathf.MoveTowards(currentVerticalInput, verticalInputRaw, verticalStep);
        currentMoveInput = moveInput;

        var velocity = body.linearVelocity;
        var planarVelocity = new Vector3(velocity.x, 0f, velocity.z);

        HorizontalSpeed01 = IsSimpleControl
            ? Mathf.Clamp01(planarVelocity.magnitude / Mathf.Max(0.001f, simpleControl.maxHorizontalSpeed))
            : Mathf.Clamp01(planarVelocity.magnitude / Mathf.Max(0.001f, 20f));

        UpdateAutoShutdown(moveInput, verticalInputRaw, dt);
        UpdateHeldAltitude(currentVerticalInput, planarVelocity);
        ApplyLiftForce(currentVerticalInput, velocity.y);
        ApplyModePlanarForces(moveInput, planarVelocity);
        ApplyWindForce();
        UpdateHeading(moveInput, planarVelocity, dt);
        ApplyYawTorque();
        ApplyTiltTorque(moveInput);
        ApplyAngularRateLimit();
        ApplyLandedLock(planarVelocity);
    }

    private bool TryCompleteStartupPlacement()
    {
        if (startupPlacementComplete) return true;
        if (body == null)
        {
            startupPlacementComplete = true;
            return true;
        }

        var probeDistance = Mathf.Max(spawn.groundProbeDistance, zLock.groundClearanceProbeDistance);
        if (TryGetNearestGroundHeightAround(body.position, probeDistance, out var nearestGroundY))
        {
            var clearance = (zLock.enabled ? zLock.groundClearance : spawn.clearanceAboveGround) + GetHoverZoneClearanceOffset();
            var targetY = nearestGroundY + clearance;
            targetY = GetForcedHoverZoneAltitude(targetY);
            targetY = Mathf.Max(targetY, GetHoverZoneMinimumWorldAltitude());
            heldAltitude = targetY;
            hasHeldAltitude = altitudeHold.enabled;
            ApplyStartupPlacement(targetY);

            startupPlacementComplete = true;
            return true;
        }

        if (TrySampleTerrainY(body.position, out var terrainY))
        {
            var clearance = (zLock.enabled ? zLock.groundClearance : spawn.clearanceAboveGround) + GetHoverZoneClearanceOffset();
            var targetY = terrainY + clearance;
            targetY = GetForcedHoverZoneAltitude(targetY);
            targetY = Mathf.Max(targetY, GetHoverZoneMinimumWorldAltitude());
            heldAltitude = targetY;
            hasHeldAltitude = altitudeHold.enabled;
            ApplyStartupPlacement(targetY);

            startupPlacementComplete = true;
            return true;
        }

        return false;
    }

    private void ApplyStartupPlacement(float targetY)
    {
        var position = body.position;
        position.y = targetY;

        var yawForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (yawForward.sqrMagnitude <= 0.0001f)
            yawForward = Vector3.ProjectOnPlane(headingForward, Vector3.up);
        if (yawForward.sqrMagnitude <= 0.0001f)
            yawForward = Vector3.forward;
        yawForward.Normalize();

        var uprightYawRotation = Quaternion.LookRotation(yawForward, Vector3.up);
        headingForward = yawForward;

        body.position = position;
        body.rotation = uprightYawRotation;
        transform.SetPositionAndRotation(position, uprightYawRotation);

        if (body.isKinematic) return;

        body.linearVelocity = new Vector3(body.linearVelocity.x, 0f, body.linearVelocity.z);
        body.angularVelocity = Vector3.zero;
    }

    public void SetInputEnabled(bool isEnabled)
    {
        inputEnabled = isEnabled;
    }

    public void SetControlScheme(ControlScheme scheme)
    {
        if (controlScheme == scheme) return;
        controlScheme = scheme;
    }

    public void SetControlLock(bool isLocked)
    {
        controlLockActive = isLocked;
        if (!isLocked || body == null) return;
        if (!body.isKinematic)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
        currentMoveInput = Vector2.zero;
        currentLiftTiltInput = Vector2.zero;
        currentVerticalInput = 0f;
    }

    public void SetEngineOn(bool value)
    {
        if (engineOn == value) return;
        engineOn = value;
        landedShutdownTimer = 0f;
        if (!engineOn) return;

        heldAltitude = body != null ? body.position.y : transform.position.y;
        hasHeldAltitude = true;
    }

    public void ApplyGroundHoldProfile(float clearance, float response, float damping, float maxCorrectionSpeed)
    {
        zLock.groundClearance = Mathf.Clamp(clearance, 0.5f, 1.35f);
        zLock.holdResponse = Mathf.Max(0.01f, response);
        zLock.holdDamping = Mathf.Max(0.01f, damping);
        zLock.maxCorrectionSpeed = Mathf.Max(0.01f, maxCorrectionSpeed);
    }

    public void ReapplyStartupPlacementNow()
    {
        if (body == null) body = GetComponent<Rigidbody>();
        startupPlacementComplete = false;
        TryCompleteStartupPlacement();
    }

    private void SubscribeGenerationCallbacks()
    {
        if (!reapplyPlacementAfterTerrainGeneration) return;
        if (terrainGenerator == null)
            terrainGenerator = FindFirstObjectByType<TerrainGenerator>();
        if (terrainGenerator == null) return;

        terrainGenerator.GenerationCompleted -= HandleTerrainGenerated;
        terrainGenerator.GenerationCompleted += HandleTerrainGenerated;
    }

    private void UnsubscribeGenerationCallbacks()
    {
        if (terrainGenerator == null) return;
        terrainGenerator.GenerationCompleted -= HandleTerrainGenerated;
    }

    private void HandleTerrainGenerated()
    {
        if (!reapplyPlacementAfterTerrainGeneration) return;

        if (deferredPlacementRoutine != null)
            StopCoroutine(deferredPlacementRoutine);
        deferredPlacementRoutine = StartCoroutine(DeferredStartupPlacementRetry());
    }

    private IEnumerator DeferredStartupPlacementRetry()
    {
        var attempts = Mathf.Max(1, placementRetryFramesAfterGeneration);
        for (var i = 0; i < attempts; i++)
        {
            startupPlacementComplete = false;
            if (TryCompleteStartupPlacement())
            {
                deferredPlacementRoutine = null;
                yield break;
            }

            yield return null;
        }

        deferredPlacementRoutine = null;
    }

    public void RegisterHoverZone(HelicopterHoverHeightZone zone)
    {
        if (zone == null || !hoverZones.enabled) return;
        var influence = new HoverZoneInfluence
        {
            clearanceOffset = Mathf.Max(0f, zone.AdditionalClearance),
            minimumWorldAltitude = zone.TryGetMinimumWorldAltitude(out var minY) ? minY : float.NegativeInfinity,
            fixedWorldAltitude = zone.TryGetFixedWorldAltitude(out var fixedY) ? fixedY : float.NegativeInfinity
        };
        activeHoverZones[zone.GetInstanceID()] = influence;
    }

    public void UnregisterHoverZone(HelicopterHoverHeightZone zone)
    {
        if (zone == null) return;
        activeHoverZones.Remove(zone.GetInstanceID());
    }

    private void UpdateGroundState()
    {
        isGrounded = IsNearGround(ground.landingProbeDistance);

        var v = body.linearVelocity;
        var planarSpeed = new Vector2(v.x, v.z).magnitude;
        isLanded = isGrounded && Mathf.Abs(v.y) <= ground.landingMaxVerticalSpeed && planarSpeed <= ground.landingMaxPlanarSpeed;
    }

    private void UpdateEnginePower(float dt)
    {
        var target = engineOn ? 1f : 0f;
        var rate = engineOn ? engineSettings.spoolUpRate : engineSettings.spoolDownRate;
        enginePower = Mathf.MoveTowards(enginePower, target, rate * dt);
    }

    private void UpdateHeldAltitude(float verticalInput, Vector3 planarVelocity)
    {
        if (!altitudeHold.enabled)
        {
            hasHeldAltitude = false;
            return;
        }

        if (zLock.enabled && engineOn && !isLanded)
        {
            var forcedHoverAltitude = GetHoverZoneFixedWorldAltitude();
            if (!float.IsNegativeInfinity(forcedHoverAltitude))
            {
                heldAltitude = forcedHoverAltitude;
                hasHeldAltitude = true;
                return;
            }

            if (TryGetPredictiveGroundHeight(planarVelocity, out var groundY))
            {
                var desiredHoldAltitude = groundY + zLock.groundClearance + GetHoverZoneClearanceOffset();
                desiredHoldAltitude = Mathf.Max(desiredHoldAltitude, GetHoverZoneMinimumWorldAltitude());
                heldAltitude = desiredHoldAltitude;

                hasHeldAltitude = true;
                return;
            }

            if (TryGetGroundHeightBelow(body.position, zLock.groundClearanceProbeDistance, out var fallbackGroundY))
            {
                heldAltitude = fallbackGroundY + zLock.groundClearance + GetHoverZoneClearanceOffset();
                heldAltitude = Mathf.Max(heldAltitude, GetHoverZoneMinimumWorldAltitude());
                hasHeldAltitude = true;
                return;
            }
        }

        if (!engineOn || isLanded || Mathf.Abs(verticalInput) > 0.01f)
        {
            heldAltitude = body.position.y;
            hasHeldAltitude = true;
        }

        var minAltitude = GetHoverZoneMinimumWorldAltitude();
        if (!float.IsNegativeInfinity(minAltitude))
        {
            heldAltitude = Mathf.Max(heldAltitude, minAltitude);
            hasHeldAltitude = true;
        }
    }

    private void ApplyLiftForce(float verticalInput, float verticalVelocity)
    {
        if (!engineOn && enginePower <= 0.001f) return;

        var downwardScale = verticalInput < 0f ? liftSettings.descentSpeedMultiplier : 1f;
        var targetVerticalSpeed = verticalInput * liftSettings.maxVerticalSpeed * downwardScale;
        if (altitudeHold.enabled && Mathf.Abs(verticalInput) <= 0.01f && hasHeldAltitude)
        {
            var altitudeError = heldAltitude - body.position.y;
            if (zLock.enabled)
            {
                var guidedHoldSpeed = altitudeError * zLock.holdResponse - verticalVelocity * zLock.holdDamping;
                guidedHoldSpeed = Mathf.Clamp(guidedHoldSpeed, -zLock.maxCorrectionSpeed, zLock.maxCorrectionSpeed);
                targetVerticalSpeed = guidedHoldSpeed;
            }
            else
            {
                var holdSpeed = altitudeError * altitudeHold.strength - verticalVelocity * altitudeHold.damping;
                var guidedHoldSpeed = Mathf.Clamp(holdSpeed, -altitudeHold.maxSpeed, altitudeHold.maxSpeed);
                targetVerticalSpeed = Mathf.Lerp(targetVerticalSpeed, guidedHoldSpeed, altitudeHold.guidanceBlend);
            }
        }

        var verticalError = targetVerticalSpeed - verticalVelocity;
        var verticalAssist = verticalError * liftSettings.verticalVelocityGain;
        var upDot = Mathf.Max(liftSettings.minLiftUpDot, Vector3.Dot(transform.up, Vector3.up));
        var compensation = Mathf.Min(1f / upDot, liftSettings.maxLiftCompensation);
        var gravityHoverCollective = Mathf.Clamp01(Physics.gravity.magnitude / Mathf.Max(0.001f, liftSettings.maxAcceleration * Mathf.Max(0.08f, enginePower) * compensation));
        var hoverBias = (liftSettings.hoverThrottle - 0.5f) * 0.2f;
        var collective = Mathf.Clamp01(gravityHoverCollective + hoverBias + altitudeHold.hoverCollectiveTrim + verticalInput * liftSettings.collectiveResponse);
        var thrustAccel = enginePower * liftSettings.maxAcceleration * collective * compensation + verticalAssist;

        var uprightBlend = IsSimpleControl ? simpleControl.liftUprightBlend : complexControl.liftUprightBlend;
        var liftDirection = Vector3.Slerp(transform.up, Vector3.up, uprightBlend).normalized;
        if (Vector3.Dot(liftDirection, Vector3.up) < 0f) liftDirection = Vector3.up;
        body.AddForce(liftDirection * thrustAccel, ForceMode.Acceleration);
    }

    private void ApplyModePlanarForces(Vector2 moveInput, Vector3 planarVelocity)
    {
        if (!engineOn && isLanded)
        {
            currentLiftTiltInput = Vector2.zero;
            return;
        }

        if (IsSimpleControl)
        {
            var desired = new Vector3(moveInput.x, 0f, moveInput.y) * simpleControl.maxHorizontalSpeed;
            var error = desired - planarVelocity;
            var gain = desired.sqrMagnitude > 0.0001f ? simpleControl.planarVelocityGain : simpleControl.planarBrakeGain;
            var accel = Vector3.ClampMagnitude(error * gain, simpleControl.maxPlanarAcceleration);
            body.AddForce(accel, ForceMode.Acceleration);

            var local = transform.InverseTransformDirection(accel);
            currentLiftTiltInput = simpleControl.maxPlanarAcceleration <= 0.001f
                ? Vector2.zero
                : new Vector2(
                    Mathf.Clamp(local.x / simpleControl.maxPlanarAcceleration, -1f, 1f),
                    Mathf.Clamp(local.z / simpleControl.maxPlanarAcceleration, -1f, 1f)
                );
            return;
        }

        var localInput = moveInput;
        var desiredTilt = new Vector2(localInput.x, localInput.y);
        currentLiftTiltInput = desiredTilt;

        if (complexControl.cyclicAssistAcceleration > 0.001f)
        {
            var assistLocal = new Vector3(
                desiredTilt.x * complexControl.lateralStrafeAssistAcceleration,
                0f,
                desiredTilt.y * complexControl.cyclicAssistAcceleration
            );
            var assistWorld = transform.TransformDirection(assistLocal);
            body.AddForce(assistWorld * enginePower, ForceMode.Acceleration);
        }
        body.AddForce(-planarVelocity * complexControl.planarDamping, ForceMode.Acceleration);
    }

    private void ApplyWindForce()
    {
        if (!wind.enabled) return;
        if (enginePower <= 0.001f) return;

        var t = Time.time * wind.frequency;
        var windX = Mathf.PerlinNoise(windSeedX, t) * 2f - 1f;
        var windY = Mathf.PerlinNoise(windSeedY, t) * 2f - 1f;
        var windZ = Mathf.PerlinNoise(windSeedZ, t) * 2f - 1f;
        body.AddForce(new Vector3(windX * wind.horizontalAcceleration, windY * wind.verticalAcceleration, windZ * wind.horizontalAcceleration), ForceMode.Acceleration);
    }

    private void UpdateHeading(Vector2 moveInput, Vector3 planarVelocity, float dt)
    {
        if (IsSimpleControl)
        {
            if (simpleControl.holdHeading) return;
            if (planarVelocity.sqrMagnitude <= simpleControl.yawToVelocityMinSpeed * simpleControl.yawToVelocityMinSpeed) return;
            var simpleVelHeading = planarVelocity.normalized;
            headingForward = Vector3.Slerp(headingForward, simpleVelHeading, 1f - Mathf.Exp(-simpleControl.yawFollowSpeed * dt));
            return;
        }

        if (Mathf.Abs(moveInput.x) > 0.0004f)
        {
            if (headingForward.sqrMagnitude <= 0.0001f)
                headingForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

            var yawDelta = moveInput.x * complexControl.yawRate * dt;
            headingForward = Quaternion.AngleAxis(yawDelta, Vector3.up) * headingForward;
            headingForward = Vector3.ProjectOnPlane(headingForward, Vector3.up).normalized;
            return;
        }

        if (moveInput.sqrMagnitude > 0.0004f) return;
        if (!complexControl.autoYawToVelocity) return;
        if (planarVelocity.sqrMagnitude <= complexControl.yawToVelocityMinSpeed * complexControl.yawToVelocityMinSpeed) return;
        if (Vector3.Dot(planarVelocity.normalized, transform.forward) < 0f) return;

        var velHeading = planarVelocity.normalized;
        headingForward = Vector3.Slerp(headingForward, velHeading, 1f - Mathf.Exp(-complexControl.yawFollowSpeed * dt));
    }

    private void ApplyYawTorque()
    {
        if (headingForward.sqrMagnitude <= 0.0001f) return;

        var currentForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (currentForward.sqrMagnitude <= 0.0001f) return;
        currentForward.Normalize();

        var yawError = Vector3.SignedAngle(currentForward, headingForward, Vector3.up) * Mathf.Deg2Rad;
        var yawRate = Vector3.Dot(body.angularVelocity, Vector3.up);
        var yawTorque = Mathf.Clamp(yawError * attitude.yawStiffness - yawRate * attitude.yawDamping, -attitude.maxYawTorque, attitude.maxYawTorque);
        body.AddTorque(Vector3.up * yawTorque, ForceMode.Acceleration);
    }

    private void ApplyTiltTorque(Vector2 moveInput)
    {
        var targetPitch = 0f;
        var targetRoll = 0f;

        if (IsSimpleControl)
        {
            targetPitch = currentLiftTiltInput.y * complexControl.maxPitch;
            targetRoll = -currentLiftTiltInput.x * complexControl.maxRoll;
        }
        else
        {
            targetPitch = moveInput.y * complexControl.maxPitch;
            targetRoll = -moveInput.x * complexControl.maxRoll;
        }

        var currentPitch = Mathf.Asin(Mathf.Clamp(Vector3.Dot(transform.forward, Vector3.up), -1f, 1f)) * Mathf.Rad2Deg;
        var currentRoll = -Mathf.Asin(Mathf.Clamp(Vector3.Dot(transform.right, Vector3.up), -1f, 1f)) * Mathf.Rad2Deg;
        var pitchError = (targetPitch - currentPitch) * Mathf.Deg2Rad;
        var rollError = (targetRoll - currentRoll) * Mathf.Deg2Rad;

        var pitchRate = Vector3.Dot(body.angularVelocity, transform.right);
        var rollRate = Vector3.Dot(body.angularVelocity, transform.forward);

        var pitchTorque = Mathf.Clamp(pitchError * attitude.tiltStiffness - pitchRate * attitude.tiltDamping, -attitude.maxTiltTorque, attitude.maxTiltTorque);
        var rollTorque = Mathf.Clamp(rollError * attitude.tiltStiffness - rollRate * attitude.tiltDamping, -attitude.maxTiltTorque, attitude.maxTiltTorque);
        body.AddTorque(transform.right * pitchTorque, ForceMode.Acceleration);
        body.AddTorque(transform.forward * rollTorque, ForceMode.Acceleration);

        var uprightError = Vector3.Cross(transform.up, Vector3.up);
        body.AddTorque(uprightError * attitude.emergencyUprightTorque, ForceMode.Acceleration);
    }

    private void ApplyLandedLock(Vector3 planarVelocity)
    {
        if (!isLanded) return;
        if (engineOn) return;

        body.AddForce(-planarVelocity * ground.landedLockForce, ForceMode.Acceleration);
        body.AddTorque(-body.angularVelocity * ground.landedLockTorque, ForceMode.Acceleration);
    }

    private void ApplyAngularRateLimit()
    {
        if (attitude.maxPitchRollRate <= 0.001f) return;

        var rightRate = Vector3.Dot(body.angularVelocity, transform.right);
        var forwardRate = Vector3.Dot(body.angularVelocity, transform.forward);

        var rightExcess = Mathf.Abs(rightRate) - attitude.maxPitchRollRate;
        if (rightExcess > 0f)
        {
            var correction = -Mathf.Sign(rightRate) * rightExcess * 10f;
            body.AddTorque(transform.right * correction, ForceMode.Acceleration);
        }

        var forwardExcess = Mathf.Abs(forwardRate) - attitude.maxPitchRollRate;
        if (forwardExcess > 0f)
        {
            var correction = -Mathf.Sign(forwardRate) * forwardExcess * 10f;
            body.AddTorque(transform.forward * correction, ForceMode.Acceleration);
        }
    }

    private Vector2 ReadMoveInput()
    {
        var input = resolvedMoveAction != null ? resolvedMoveAction.ReadValue<Vector2>() : Vector2.zero;
        if (input.sqrMagnitude > 0.0001f)
            input *= Mathf.Max(0.1f, inputSettings.mouseSensitivity);
        if (input.sqrMagnitude > 1f) input.Normalize();
        if (input.sqrMagnitude > 0.0001f) return input;
        if (!inputSettings.keyboardFallback) return input;
        return ReadKeyboardMoveInput();
    }

    public void SetMouseSensitivity(float value)
    {
        inputSettings.mouseSensitivity = Mathf.Clamp(value, 0.1f, 4f);
    }

    private float ReadVerticalInput()
    {
        var ascendPressed = resolvedAscendAction != null && resolvedAscendAction.IsPressed();
        var descendPressed = resolvedDescendAction != null && resolvedDescendAction.IsPressed();

        if (!ascendPressed && !descendPressed && inputSettings.keyboardFallback)
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                ascendPressed = keyboard.spaceKey.isPressed || keyboard.eKey.isPressed;
                descendPressed = keyboard.leftCtrlKey.isPressed || keyboard.qKey.isPressed || keyboard.cKey.isPressed;
            }
        }

        if (ascendPressed == descendPressed) return 0f;
        return ascendPressed ? 1f : -1f;
    }

    private static Vector2 ReadKeyboardMoveInput()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return Vector2.zero;

        var x = 0f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) x -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x += 1f;

        var y = 0f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) y -= 1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) y += 1f;

        var input = new Vector2(x, y);
        if (input.sqrMagnitude > 1f) input.Normalize();
        return input;
    }

    private void ResolveActions()
    {
        resolvedMoveAction = inputSettings.moveAction != null ? inputSettings.moveAction.action : null;
        resolvedAscendAction = inputSettings.ascendAction != null ? inputSettings.ascendAction.action : null;
        resolvedDescendAction = inputSettings.descendAction != null ? inputSettings.descendAction.action : null;
    }

    private void SetActionsEnabled(bool enabled)
    {
        SetActionEnabled(resolvedMoveAction, enabled);
        SetActionEnabled(resolvedAscendAction, enabled);
        SetActionEnabled(resolvedDescendAction, enabled);
    }

    private static void SetActionEnabled(InputAction action, bool enabled)
    {
        if (action == null) return;
        if (enabled) action.Enable();
        else action.Disable();
    }

    private void ConfigureRigidbody()
    {
        if (body == null) return;

        body.useGravity = true;
        if (!physicsSettings.applySettings) return;

        body.mass = physicsSettings.mass;
        body.linearDamping = physicsSettings.linearDamping;
        body.angularDamping = physicsSettings.angularDamping;
        body.maxAngularVelocity = physicsSettings.maxAngularSpeed;
        body.centerOfMass = physicsSettings.centerOfMassOffset;
        body.ResetInertiaTensor();
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    private void EnsurePhysicsCanSimulate()
    {
        if (!physicsSettings.ensureCanSimulate || body == null) return;

        if (body.isKinematic) body.isKinematic = false;

        var freezeMask =
            RigidbodyConstraints.FreezePositionX |
            RigidbodyConstraints.FreezePositionY |
            RigidbodyConstraints.FreezePositionZ |
            RigidbodyConstraints.FreezeRotationX |
            RigidbodyConstraints.FreezeRotationY |
            RigidbodyConstraints.FreezeRotationZ;

        if ((body.constraints & freezeMask) != 0) body.constraints &= ~freezeMask;
    }

    private void Reset()
    {
        body = GetComponent<Rigidbody>();
        ConfigureRigidbody();
    }

    private void UpdateAutoShutdown(Vector2 moveInput, float verticalInputRaw, float dt)
    {
        if (!autoShutdown.enabled || !engineOn)
        {
            landedShutdownTimer = 0f;
            return;
        }

        if (!CanShutdownEngine())
        {
            landedShutdownTimer = 0f;
            return;
        }

        var hasHorizontalIntent = moveInput.sqrMagnitude > autoShutdown.moveDeadzone * autoShutdown.moveDeadzone;
        var hasUpwardIntent = verticalInputRaw > autoShutdown.verticalDeadzone;
        var hasControlIntent = hasHorizontalIntent || hasUpwardIntent;
        if (hasControlIntent)
        {
            landedShutdownTimer = 0f;
            return;
        }

        landedShutdownTimer += dt;
        if (landedShutdownTimer < autoShutdown.delay) return;

        engineOn = false;
        landedShutdownTimer = 0f;
    }

    private bool CanShutdownEngine()
    {
        UpdateGroundState();
        if (!isLanded) return false;
        if (!IsNearGroundForShutdown()) return false;
        if (body == null) return isLanded;

        var v = body.linearVelocity;
        var planarSpeed = new Vector2(v.x, v.z).magnitude;
        return Mathf.Abs(v.y) <= autoShutdown.maxVerticalSpeed &&
               planarSpeed <= autoShutdown.maxPlanarSpeed;
    }

    private bool IsNearGroundForShutdown()
    {
        if (isGrounded || isLanded) return true;
        return IsNearGround(Mathf.Max(ground.landingProbeDistance, autoShutdown.groundProbeDistance));
    }

    private bool IsNearGround(float probeDistance)
    {
        if (body == null) return false;
        var throwawayY = float.NegativeInfinity;

        if (TryGetColliderBounds(out var bounds))
        {
            var originY = bounds.min.y + ground.probeSkin;
            var distance = probeDistance + ground.probeSkin;
            var center = new Vector3(bounds.center.x, originY, bounds.center.z);
            if (RaycastGroundY(center, distance, ref throwawayY))
                return true;

            var offsetX = Mathf.Max(0.1f, bounds.extents.x * 0.5f);
            var offsetZ = Mathf.Max(0.1f, bounds.extents.z * 0.5f);

            var p1 = center + new Vector3(offsetX, 0f, offsetZ);
            var p2 = center + new Vector3(-offsetX, 0f, offsetZ);
            var p3 = center + new Vector3(offsetX, 0f, -offsetZ);
            var p4 = center + new Vector3(-offsetX, 0f, -offsetZ);

            if (RaycastGroundY(p1, distance, ref throwawayY)) return true;
            if (RaycastGroundY(p2, distance, ref throwawayY)) return true;
            if (RaycastGroundY(p3, distance, ref throwawayY)) return true;
            if (RaycastGroundY(p4, distance, ref throwawayY)) return true;
            return false;
        }

        var fallbackOrigin = body.worldCenterOfMass + Vector3.up * ground.probeSkin;
        return RaycastGroundY(fallbackOrigin, probeDistance + ground.probeSkin, ref throwawayY);
    }

    private bool TryGetGroundHeightBelow(Vector3 referencePosition, float probeDistance, out float groundY)
    {
        groundY = 0f;
        var found = false;
        var bestY = float.NegativeInfinity;

        if (TryGetColliderBounds(out var bounds))
        {
            var castHeight = bounds.extents.y + 2f;
            var distance = probeDistance + castHeight;
            var center = new Vector3(referencePosition.x, referencePosition.y + castHeight, referencePosition.z);
            var offsetX = Mathf.Max(0.15f, bounds.extents.x * 0.55f);
            var offsetZ = Mathf.Max(0.15f, bounds.extents.z * 0.55f);

            if (RaycastGroundY(center, distance, ref bestY)) found = true;
            if (RaycastGroundY(center + new Vector3(offsetX, 0f, offsetZ), distance, ref bestY)) found = true;
            if (RaycastGroundY(center + new Vector3(-offsetX, 0f, offsetZ), distance, ref bestY)) found = true;
            if (RaycastGroundY(center + new Vector3(offsetX, 0f, -offsetZ), distance, ref bestY)) found = true;
            if (RaycastGroundY(center + new Vector3(-offsetX, 0f, -offsetZ), distance, ref bestY)) found = true;
        }
        else
        {
            var origin = referencePosition + Vector3.up * 2f;
            if (RaycastGroundY(origin, probeDistance + 2f, ref bestY)) found = true;
        }

        groundY = bestY;
        return found;
    }

    private bool TryGetGroundHeightAbove(Vector3 referencePosition, float probeDistance, out float groundY)
    {
        groundY = 0f;
        var found = false;
        var bestY = float.PositiveInfinity;

        if (TryGetColliderBounds(out var bounds))
        {
            var castHeight = bounds.extents.y + 2f;
            var distance = probeDistance + castHeight;
            var center = new Vector3(referencePosition.x, referencePosition.y - castHeight, referencePosition.z);
            var offsetX = Mathf.Max(0.15f, bounds.extents.x * 0.55f);
            var offsetZ = Mathf.Max(0.15f, bounds.extents.z * 0.55f);

            if (RaycastGroundYUp(center, distance, referencePosition.y, ref bestY)) found = true;
            if (RaycastGroundYUp(center + new Vector3(offsetX, 0f, offsetZ), distance, referencePosition.y, ref bestY)) found = true;
            if (RaycastGroundYUp(center + new Vector3(-offsetX, 0f, offsetZ), distance, referencePosition.y, ref bestY)) found = true;
            if (RaycastGroundYUp(center + new Vector3(offsetX, 0f, -offsetZ), distance, referencePosition.y, ref bestY)) found = true;
            if (RaycastGroundYUp(center + new Vector3(-offsetX, 0f, -offsetZ), distance, referencePosition.y, ref bestY)) found = true;
        }
        else
        {
            var origin = referencePosition - Vector3.up * 2f;
            if (RaycastGroundYUp(origin, probeDistance + 2f, referencePosition.y, ref bestY)) found = true;
        }

        if (!found) return false;
        groundY = bestY;
        return true;
    }

    private bool TryGetNearestGroundHeightAround(Vector3 referencePosition, float probeDistance, out float groundY)
    {
        groundY = 0f;
        var hasBelow = TryGetGroundHeightBelow(referencePosition, probeDistance, out var belowY);
        var hasAbove = TryGetGroundHeightAbove(referencePosition, probeDistance, out var aboveY);

        if (!hasBelow && !hasAbove) return false;
        if (hasBelow && !hasAbove) { groundY = belowY; return true; }
        if (!hasBelow && hasAbove) { groundY = aboveY; return true; }

        var belowDistance = Mathf.Abs(referencePosition.y - belowY);
        var aboveDistance = Mathf.Abs(aboveY - referencePosition.y);
        groundY = belowDistance <= aboveDistance ? belowY : aboveY;
        return true;
    }

    private bool TryGetPredictiveGroundHeight(Vector3 planarVelocity, out float groundY)
    {
        groundY = 0f;
        if (body == null) return false;

        var speed = planarVelocity.magnitude;
        var forward = speed > 0.15f
            ? planarVelocity.normalized
            : Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        var right = Vector3.Cross(Vector3.up, forward).normalized;

        var lookAheadDistance = Mathf.Min(zLock.lookAheadMaxDistance, speed * zLock.lookAheadTime);
        var center = body.position + forward * lookAheadDistance;

        var found = false;
        var bestY = float.NegativeInfinity;

        if (TryGetGroundHeightBelow(center, zLock.groundClearanceProbeDistance, out var y0)) { bestY = Mathf.Max(bestY, y0); found = true; }
        if (TryGetGroundHeightBelow(center + forward * zLock.sampleRadius, zLock.groundClearanceProbeDistance, out var y1)) { bestY = Mathf.Max(bestY, y1); found = true; }
        if (TryGetGroundHeightBelow(center + right * zLock.sampleRadius, zLock.groundClearanceProbeDistance, out var y2)) { bestY = Mathf.Max(bestY, y2); found = true; }
        if (TryGetGroundHeightBelow(center - right * zLock.sampleRadius, zLock.groundClearanceProbeDistance, out var y3)) { bestY = Mathf.Max(bestY, y3); found = true; }
        if (TryGetGroundHeightBelow(center - forward * (zLock.sampleRadius * 0.5f), zLock.groundClearanceProbeDistance, out var y4)) { bestY = Mathf.Max(bestY, y4); found = true; }

        groundY = bestY;
        return found;
    }

    private bool RaycastGroundY(Vector3 origin, float distance, ref float bestY)
    {
        var hitCount = Physics.RaycastNonAlloc(origin, Vector3.down, groundHitBuffer, distance, ground.groundLayers, QueryTriggerInteraction.Ignore);
        if (hitCount <= 0 && ground.groundLayers != ~0)
            hitCount = Physics.RaycastNonAlloc(origin, Vector3.down, groundHitBuffer, distance, ~0, QueryTriggerInteraction.Ignore);
        if (hitCount <= 0) return false;

        var found = false;
        for (var i = 0; i < hitCount; i++)
        {
            var hit = groundHitBuffer[i];
            if (hit.collider == null) continue;
            if (IsSelfCollider(hit.collider)) continue;
            if (!IsValidGroundCollider(hit.collider)) continue;
            if (!found || hit.point.y > bestY) bestY = hit.point.y;
            found = true;
        }

        return found;
    }

    private bool RaycastGroundYUp(Vector3 origin, float distance, float minY, ref float bestY)
    {
        var hitCount = Physics.RaycastNonAlloc(origin, Vector3.up, groundHitBuffer, distance, ground.groundLayers, QueryTriggerInteraction.Ignore);
        if (hitCount <= 0 && ground.groundLayers != ~0)
            hitCount = Physics.RaycastNonAlloc(origin, Vector3.up, groundHitBuffer, distance, ~0, QueryTriggerInteraction.Ignore);
        if (hitCount <= 0) return false;

        var found = false;
        for (var i = 0; i < hitCount; i++)
        {
            var hit = groundHitBuffer[i];
            if (hit.collider == null) continue;
            if (IsSelfCollider(hit.collider)) continue;
            if (!IsValidGroundCollider(hit.collider)) continue;
            if (hit.point.y < minY) continue;
            if (!found || hit.point.y < bestY) bestY = hit.point.y;
            found = true;
        }

        return found;
    }

    private static bool TrySampleTerrainY(Vector3 position, out float y)
    {
        y = 0f;
        var terrain = Terrain.activeTerrain;
        if (terrain == null) return false;
        y = terrain.SampleHeight(position) + terrain.transform.position.y;
        return true;
    }

    private bool IsSelfCollider(Collider col)
    {
        if (col == null) return false;
        return col.transform.IsChildOf(transform);
    }

    private float GetHoverZoneClearanceOffset()
    {
        if (!hoverZones.enabled || activeHoverZones.Count == 0) return 0f;

        var maxOffset = 0f;
        foreach (var kv in activeHoverZones)
            if (kv.Value.clearanceOffset > maxOffset) maxOffset = kv.Value.clearanceOffset;
        return maxOffset;
    }

    private float GetHoverZoneFixedWorldAltitude()
    {
        if (!hoverZones.enabled || activeHoverZones.Count == 0) return float.NegativeInfinity;

        var fixedAltitude = float.NegativeInfinity;
        foreach (var kv in activeHoverZones)
            if (kv.Value.fixedWorldAltitude > fixedAltitude)
                fixedAltitude = kv.Value.fixedWorldAltitude;
        return fixedAltitude;
    }

    private float GetForcedHoverZoneAltitude(float fallbackAltitude)
    {
        var forced = GetHoverZoneFixedWorldAltitude();
        if (float.IsNegativeInfinity(forced)) return fallbackAltitude;
        return forced;
    }

    private float GetHoverZoneMinimumWorldAltitude()
    {
        if (!hoverZones.enabled || activeHoverZones.Count == 0) return float.NegativeInfinity;

        var maxMinimumAltitude = float.NegativeInfinity;
        foreach (var kv in activeHoverZones)
            if (kv.Value.minimumWorldAltitude > maxMinimumAltitude)
                maxMinimumAltitude = kv.Value.minimumWorldAltitude;
        return maxMinimumAltitude;
    }

    private bool IsValidGroundCollider(Collider col)
    {
        if (col == null) return false;
        if (col.GetComponentInParent<HelicopterHoverHeightZone>() != null) return false;

        if (groundTagFilter.ignoredGroundTags != null)
        {
            for (var i = 0; i < groundTagFilter.ignoredGroundTags.Length; i++)
            {
                var ignoredTag = groundTagFilter.ignoredGroundTags[i];
                if (string.IsNullOrWhiteSpace(ignoredTag)) continue;
                if (string.Equals(col.tag, ignoredTag)) return false;
            }
        }

        if (!groundTagFilter.useGroundTagFiltering) return true;
        if (string.Equals(col.tag, "Untagged"))
            return groundTagFilter.allowUntaggedWhenFiltering;

        return MatchesAnyTag(col.gameObject, groundTagFilter.allowedGroundTags);
    }

    private static bool MatchesAnyTag(GameObject go, string[] tags)
    {
        if (go == null || tags == null || tags.Length == 0) return false;
        var currentTag = go.tag;
        for (var i = 0; i < tags.Length; i++)
        {
            var allowedTag = tags[i];
            if (string.IsNullOrWhiteSpace(allowedTag)) continue;
            if (string.Equals(currentTag, allowedTag)) return true;
        }

        return false;
    }

    private void CacheColliders()
    {
        cachedColliders = GetComponentsInChildren<Collider>(true);
    }

    private bool TryGetColliderBounds(out Bounds bounds)
    {
        if (cachedColliders == null || cachedColliders.Length == 0) CacheColliders();

        var hasBounds = false;
        bounds = default;
        for (var i = 0; i < cachedColliders.Length; i++)
        {
            var col = cachedColliders[i];
            if (col == null || !col.enabled || col.isTrigger) continue;

            if (!hasBounds)
            {
                bounds = col.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(col.bounds);
            }
        }

        return hasBounds;
    }
}


