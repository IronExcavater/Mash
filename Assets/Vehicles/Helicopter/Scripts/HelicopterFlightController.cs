using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class HelicopterFlightController : MonoBehaviour
{
    public enum ControlScheme
    {
        Simple = 0,
        Complex = 1
    }

    [Header("Mode")]
    [SerializeField] private ControlScheme controlScheme = ControlScheme.Simple;

    [ConditionalField("controlScheme", (int)ControlScheme.Simple, "Simple Control")]
    [SerializeField, Min(0f)] private float simplePlanarVelocityGain = 3f;
    [ConditionalField("controlScheme", (int)ControlScheme.Simple)]
    [SerializeField, Min(0f)] private float simplePlanarBrakeGain = 2.4f;
    [ConditionalField("controlScheme", (int)ControlScheme.Simple)]
    [SerializeField, Min(0f)] private float simpleMaxPlanarAcceleration = 18f;
    [ConditionalField("controlScheme", (int)ControlScheme.Simple)]
    [SerializeField, Min(1f)] private float simpleMaxHorizontalSpeed = 28f;
    [ConditionalField("controlScheme", (int)ControlScheme.Simple)]
    [SerializeField] private bool simpleHoldHeading;
    [ConditionalField("controlScheme", (int)ControlScheme.Simple)]
    [SerializeField, Min(0f)] private float simpleYawToVelocityMinSpeed = 1.2f;
    [ConditionalField("controlScheme", (int)ControlScheme.Simple)]
    [SerializeField, Min(0f)] private float simpleYawFollowSpeed = 9f;

    [ConditionalField("controlScheme", (int)ControlScheme.Complex, "Complex Control")]
    [SerializeField, Range(0f, 35f)] private float maxComplexPitch = 16f;
    [ConditionalField("controlScheme", (int)ControlScheme.Complex)]
    [SerializeField, Range(0f, 35f)] private float maxComplexRoll = 16f;
    [ConditionalField("controlScheme", (int)ControlScheme.Complex)]
    [SerializeField, Min(0f)] private float complexYawRate = 110f;
    [ConditionalField("controlScheme", (int)ControlScheme.Complex)]
    [SerializeField, Min(0f)] private float complexPlanarDamping = 0.15f;
    [ConditionalField("controlScheme", (int)ControlScheme.Complex)]
    [SerializeField, Min(0f)] private float complexCyclicAssistAcceleration = 10f;
    [ConditionalField("controlScheme", (int)ControlScheme.Complex)]
    [SerializeField] private bool autoYawToVelocity = true;
    [ConditionalField("controlScheme", (int)ControlScheme.Complex)]
    [SerializeField, Min(0f)] private float complexYawToVelocityMinSpeed = 1.2f;
    [ConditionalField("controlScheme", (int)ControlScheme.Complex)]
    [SerializeField, Min(0f)] private float complexYawFollowSpeed = 9f;

    [Header("Input")]
    [SerializeField] private bool autoEnableInputActions = true;
    [SerializeField] private bool keyboardFallbackInput = true;
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference ascendAction;
    [SerializeField] private InputActionReference descendAction;
    [SerializeField] private InputActionReference engineToggleAction;

    [Header("Engine")]
    [SerializeField] private bool startEngineOn = true;
    [SerializeField, Min(0.1f)] private float engineSpoolUpRate = 1f;
    [SerializeField, Min(0.1f)] private float engineSpoolDownRate = 0.4f;

    [Header("Lift")]
    [SerializeField, Min(1f)] private float maxLiftAcceleration = 26f;
    [SerializeField, Range(0f, 1.2f)] private float hoverThrottle = 0.55f;
    [SerializeField, Range(0f, 1f)] private float collectiveResponse = 0.4f;
    [SerializeField, Min(0f)] private float verticalVelocityGain = 3.4f;
    [SerializeField, Min(1f)] private float maxVerticalSpeed = 10f;
    [SerializeField, Min(0.1f)] private float minLiftUpDot = 0.45f;
    [ConditionalField("controlScheme", (int)ControlScheme.Simple)]
    [SerializeField, Range(0f, 1f)] private float simpleLiftUprightBlend = 0.9f;
    [ConditionalField("controlScheme", (int)ControlScheme.Complex)]
    [SerializeField, Range(0f, 1f)] private float complexLiftUprightBlend = 0.15f;
    [SerializeField, Min(1f)] private float maxLiftCompensation = 1.35f;

    [Header("Altitude Hold")]
    [SerializeField] private bool autoHoldAltitude = true;
    [SerializeField, Min(0f)] private float altitudeHoldStrength = 2f;
    [SerializeField, Min(0f)] private float altitudeHoldDamping = 1.4f;
    [SerializeField, Min(0f)] private float maxAltitudeHoldSpeed = 4f;

    [Header("Attitude")]
    [SerializeField, Min(0f)] private float yawStiffness = 26f;
    [SerializeField, Min(0f)] private float yawDamping = 8f;
    [SerializeField, Min(0f)] private float maxYawTorque = 28f;
    [SerializeField, Min(0f)] private float tiltStiffness = 22f;
    [SerializeField, Min(0f)] private float tiltDamping = 7f;
    [SerializeField, Min(0f)] private float maxTiltTorque = 30f;
    [SerializeField, Min(0f)] private float emergencyUprightTorque = 36f;
    [SerializeField, Min(0f)] private float maxPitchRollRate = 2.5f;

    [Header("Wind")]
    [SerializeField] private bool enableWind = true;
    [SerializeField, Min(0f)] private float windHorizontalAcceleration = 1f;
    [SerializeField, Min(0f)] private float windVerticalAcceleration = 0.35f;
    [SerializeField, Min(0f)] private float windFrequency = 0.2f;

    [Header("Ground")]
    [SerializeField, Min(0.1f)] private float landingProbeDistance = 2f;
    [SerializeField, Min(0f)] private float landingMaxVerticalSpeed = 1.1f;
    [SerializeField, Min(0f)] private float landingMaxPlanarSpeed = 1.4f;
    [SerializeField] private LayerMask groundLayers = ~0;
    [SerializeField, Min(0f)] private float landedLockForce = 8f;
    [SerializeField, Min(0f)] private float landedLockTorque = 10f;

    [Header("Rigidbody")]
    [SerializeField] private bool applyRigidbodySettings;
    [SerializeField] private bool ensureRigidbodyCanSimulate = true;
    [ConditionalField("applyRigidbodySettings")]
    [SerializeField, Min(1f)] private float rigidbodyMass = 2600f;
    [ConditionalField("applyRigidbodySettings")]
    [SerializeField, Min(0f)] private float linearDamping = 0.35f;
    [ConditionalField("applyRigidbodySettings")]
    [SerializeField, Min(0f)] private float angularDamping = 2.5f;
    [ConditionalField("applyRigidbodySettings")]
    [SerializeField, Min(0.1f)] private float maxAngularSpeed = 8f;
    [ConditionalField("applyRigidbodySettings")]
    [SerializeField] private Vector3 centerOfMassOffset = new Vector3(0f, -0.2f, 0f);

    private Rigidbody body;
    private InputAction resolvedMoveAction;
    private InputAction resolvedAscendAction;
    private InputAction resolvedDescendAction;
    private InputAction resolvedEngineToggleAction;

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
    private float windSeedX;
    private float windSeedY;
    private float windSeedZ;

    public bool IsInputEnabled => inputEnabled;
    public bool IsEngineOn => engineOn;
    public bool IsGrounded => isGrounded;
    public bool IsLanded => isLanded;
    public float EnginePower01 => enginePower;
    public Vector2 CurrentMoveInput => currentMoveInput;
    public Vector2 CurrentLiftTiltInput => currentLiftTiltInput;
    public float HorizontalSpeed01 { get; private set; }
    public ControlScheme CurrentControlScheme => controlScheme;
    private bool IsSimpleControl => controlScheme == ControlScheme.Simple;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        ConfigureRigidbody();

        engineOn = startEngineOn;
        enginePower = engineOn ? 1f : 0f;
        heldAltitude = body.position.y;
        hasHeldAltitude = true;

        var planarForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (planarForward.sqrMagnitude > 0.0001f) headingForward = planarForward.normalized;

        windSeedX = Random.value * 100f;
        windSeedY = Random.value * 100f;
        windSeedZ = Random.value * 100f;
    }

    private void OnEnable()
    {
        ResolveActions();
        if (autoEnableInputActions) SetActionsEnabled(true);

        if (resolvedEngineToggleAction != null) resolvedEngineToggleAction.performed += OnEngineTogglePerformed;
    }

    private void OnDisable()
    {
        if (resolvedEngineToggleAction != null) resolvedEngineToggleAction.performed -= OnEngineTogglePerformed;
        if (autoEnableInputActions) SetActionsEnabled(false);
    }

    private void Update()
    {
        if (!keyboardFallbackInput) return;
        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (!keyboard.tKey.wasPressedThisFrame) return;
        TryToggleEngine();
    }

    private void FixedUpdate()
    {
        EnsurePhysicsCanSimulate();

        var dt = Time.fixedDeltaTime;
        UpdateGroundState();
        UpdateEnginePower(dt);

        var moveInput = inputEnabled ? ReadMoveInput() : Vector2.zero;
        var verticalInput = inputEnabled ? ReadVerticalInput() : 0f;
        currentMoveInput = moveInput;

        var velocity = body.linearVelocity;
        var planarVelocity = new Vector3(velocity.x, 0f, velocity.z);

        HorizontalSpeed01 = IsSimpleControl
            ? Mathf.Clamp01(planarVelocity.magnitude / Mathf.Max(0.001f, simpleMaxHorizontalSpeed))
            : Mathf.Clamp01(planarVelocity.magnitude / Mathf.Max(0.001f, 20f));

        UpdateHeldAltitude(verticalInput);
        ApplyLiftForce(verticalInput, velocity.y);
        ApplyModePlanarForces(moveInput, planarVelocity);
        ApplyWindForce();
        UpdateHeading(moveInput, planarVelocity, dt);
        ApplyYawTorque();
        ApplyTiltTorque(moveInput);
        ApplyAngularRateLimit();
        ApplyLandedLock(planarVelocity);
    }

    public void SetInputEnabled(bool isEnabled)
    {
        inputEnabled = isEnabled;
    }

    public void SetEngineOn(bool value)
    {
        if (!value && !isLanded) return;
        engineOn = value;
    }

    private void OnEngineTogglePerformed(InputAction.CallbackContext _)
    {
        TryToggleEngine();
    }

    private void TryToggleEngine()
    {
        if (!inputEnabled) return;
        if (engineOn && !isLanded) return;
        engineOn = !engineOn;
        heldAltitude = body.position.y;
        hasHeldAltitude = true;
    }

    private void UpdateGroundState()
    {
        var origin = body.worldCenterOfMass + Vector3.up * 0.1f;
        isGrounded = Physics.Raycast(origin, Vector3.down, landingProbeDistance, groundLayers, QueryTriggerInteraction.Ignore);

        var v = body.linearVelocity;
        var planarSpeed = new Vector2(v.x, v.z).magnitude;
        isLanded = isGrounded && Mathf.Abs(v.y) <= landingMaxVerticalSpeed && planarSpeed <= landingMaxPlanarSpeed;
    }

    private void UpdateEnginePower(float dt)
    {
        var target = engineOn ? 1f : 0f;
        var rate = engineOn ? engineSpoolUpRate : engineSpoolDownRate;
        enginePower = Mathf.MoveTowards(enginePower, target, rate * dt);
    }

    private void UpdateHeldAltitude(float verticalInput)
    {
        if (!autoHoldAltitude)
        {
            hasHeldAltitude = false;
            return;
        }

        if (!engineOn || isLanded || Mathf.Abs(verticalInput) > 0.01f)
        {
            heldAltitude = body.position.y;
            hasHeldAltitude = true;
        }
    }

    private void ApplyLiftForce(float verticalInput, float verticalVelocity)
    {
        if (!engineOn && enginePower <= 0.001f) return;

        var targetVerticalSpeed = verticalInput * maxVerticalSpeed;
        if (autoHoldAltitude && Mathf.Abs(verticalInput) <= 0.01f && hasHeldAltitude)
        {
            var altitudeError = heldAltitude - body.position.y;
            var holdSpeed = altitudeError * altitudeHoldStrength - verticalVelocity * altitudeHoldDamping;
            targetVerticalSpeed = Mathf.Clamp(holdSpeed, -maxAltitudeHoldSpeed, maxAltitudeHoldSpeed);
        }

        var verticalError = targetVerticalSpeed - verticalVelocity;
        var verticalAssist = verticalError * verticalVelocityGain;
        var collective = Mathf.Clamp01(hoverThrottle + verticalInput * collectiveResponse);
        var upDot = Mathf.Max(minLiftUpDot, Vector3.Dot(transform.up, Vector3.up));
        var compensation = Mathf.Min(1f / upDot, maxLiftCompensation);
        var thrustAccel = enginePower * maxLiftAcceleration * collective * compensation + verticalAssist;

        var uprightBlend = IsSimpleControl ? simpleLiftUprightBlend : complexLiftUprightBlend;
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
            var desired = new Vector3(moveInput.x, 0f, moveInput.y) * simpleMaxHorizontalSpeed;
            var error = desired - planarVelocity;
            var gain = desired.sqrMagnitude > 0.0001f ? simplePlanarVelocityGain : simplePlanarBrakeGain;
            var accel = Vector3.ClampMagnitude(error * gain, simpleMaxPlanarAcceleration);
            body.AddForce(accel, ForceMode.Acceleration);

            var local = transform.InverseTransformDirection(accel);
            currentLiftTiltInput = simpleMaxPlanarAcceleration <= 0.001f
                ? Vector2.zero
                : new Vector2(
                    Mathf.Clamp(local.x / simpleMaxPlanarAcceleration, -1f, 1f),
                    Mathf.Clamp(local.z / simpleMaxPlanarAcceleration, -1f, 1f)
                );
            return;
        }

        var localInput = moveInput;
        var desiredTilt = new Vector2(localInput.x, localInput.y);
        currentLiftTiltInput = desiredTilt;

        if (complexCyclicAssistAcceleration > 0.001f)
        {
            var assistLocal = new Vector3(0f, 0f, desiredTilt.y) * complexCyclicAssistAcceleration;
            var assistWorld = transform.TransformDirection(assistLocal);
            body.AddForce(assistWorld * enginePower, ForceMode.Acceleration);
        }
        body.AddForce(-planarVelocity * complexPlanarDamping, ForceMode.Acceleration);
    }

    private void ApplyWindForce()
    {
        if (!enableWind) return;
        if (enginePower <= 0.001f) return;

        var t = Time.time * windFrequency;
        var windX = Mathf.PerlinNoise(windSeedX, t) * 2f - 1f;
        var windY = Mathf.PerlinNoise(windSeedY, t) * 2f - 1f;
        var windZ = Mathf.PerlinNoise(windSeedZ, t) * 2f - 1f;
        body.AddForce(new Vector3(windX * windHorizontalAcceleration, windY * windVerticalAcceleration, windZ * windHorizontalAcceleration), ForceMode.Acceleration);
    }

    private void UpdateHeading(Vector2 moveInput, Vector3 planarVelocity, float dt)
    {
        if (IsSimpleControl)
        {
            if (simpleHoldHeading) return;
            if (planarVelocity.sqrMagnitude <= simpleYawToVelocityMinSpeed * simpleYawToVelocityMinSpeed) return;
            var simpleVelHeading = planarVelocity.normalized;
            headingForward = Vector3.Slerp(headingForward, simpleVelHeading, 1f - Mathf.Exp(-simpleYawFollowSpeed * dt));
            return;
        }

        if (Mathf.Abs(moveInput.x) > 0.0004f)
        {
            if (headingForward.sqrMagnitude <= 0.0001f)
                headingForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

            var yawDelta = moveInput.x * complexYawRate * dt;
            headingForward = Quaternion.AngleAxis(yawDelta, Vector3.up) * headingForward;
            headingForward = Vector3.ProjectOnPlane(headingForward, Vector3.up).normalized;
            return;
        }

        if (moveInput.sqrMagnitude > 0.0004f) return;
        if (!autoYawToVelocity) return;
        if (planarVelocity.sqrMagnitude <= complexYawToVelocityMinSpeed * complexYawToVelocityMinSpeed) return;
        if (Vector3.Dot(planarVelocity.normalized, transform.forward) < 0f) return;

        var velHeading = planarVelocity.normalized;
        headingForward = Vector3.Slerp(headingForward, velHeading, 1f - Mathf.Exp(-complexYawFollowSpeed * dt));
    }

    private void ApplyYawTorque()
    {
        if (headingForward.sqrMagnitude <= 0.0001f) return;

        var currentForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (currentForward.sqrMagnitude <= 0.0001f) return;
        currentForward.Normalize();

        var yawError = Vector3.SignedAngle(currentForward, headingForward, Vector3.up) * Mathf.Deg2Rad;
        var yawRate = Vector3.Dot(body.angularVelocity, Vector3.up);
        var yawTorque = Mathf.Clamp(yawError * yawStiffness - yawRate * yawDamping, -maxYawTorque, maxYawTorque);
        body.AddTorque(Vector3.up * yawTorque, ForceMode.Acceleration);
    }

    private void ApplyTiltTorque(Vector2 moveInput)
    {
        var targetPitch = 0f;
        var targetRoll = 0f;

        if (IsSimpleControl)
        {
            targetPitch = currentLiftTiltInput.y * maxComplexPitch;
            targetRoll = -currentLiftTiltInput.x * maxComplexRoll;
        }
        else
        {
            targetPitch = moveInput.y * maxComplexPitch;
            targetRoll = -moveInput.x * maxComplexRoll;
        }

        var currentPitch = Mathf.Asin(Mathf.Clamp(Vector3.Dot(transform.forward, Vector3.up), -1f, 1f)) * Mathf.Rad2Deg;
        var currentRoll = -Mathf.Asin(Mathf.Clamp(Vector3.Dot(transform.right, Vector3.up), -1f, 1f)) * Mathf.Rad2Deg;
        var pitchError = (targetPitch - currentPitch) * Mathf.Deg2Rad;
        var rollError = (targetRoll - currentRoll) * Mathf.Deg2Rad;

        var pitchRate = Vector3.Dot(body.angularVelocity, transform.right);
        var rollRate = Vector3.Dot(body.angularVelocity, transform.forward);

        var pitchTorque = Mathf.Clamp(pitchError * tiltStiffness - pitchRate * tiltDamping, -maxTiltTorque, maxTiltTorque);
        var rollTorque = Mathf.Clamp(rollError * tiltStiffness - rollRate * tiltDamping, -maxTiltTorque, maxTiltTorque);
        body.AddTorque(transform.right * pitchTorque, ForceMode.Acceleration);
        body.AddTorque(transform.forward * rollTorque, ForceMode.Acceleration);

        var uprightError = Vector3.Cross(transform.up, Vector3.up);
        body.AddTorque(uprightError * emergencyUprightTorque, ForceMode.Acceleration);
    }

    private void ApplyLandedLock(Vector3 planarVelocity)
    {
        if (!isLanded) return;
        if (engineOn) return;

        body.AddForce(-planarVelocity * landedLockForce, ForceMode.Acceleration);
        body.AddTorque(-body.angularVelocity * landedLockTorque, ForceMode.Acceleration);
    }

    private void ApplyAngularRateLimit()
    {
        if (maxPitchRollRate <= 0.001f) return;

        var rightRate = Vector3.Dot(body.angularVelocity, transform.right);
        var forwardRate = Vector3.Dot(body.angularVelocity, transform.forward);

        var rightExcess = Mathf.Abs(rightRate) - maxPitchRollRate;
        if (rightExcess > 0f)
        {
            var correction = -Mathf.Sign(rightRate) * rightExcess * 10f;
            body.AddTorque(transform.right * correction, ForceMode.Acceleration);
        }

        var forwardExcess = Mathf.Abs(forwardRate) - maxPitchRollRate;
        if (forwardExcess > 0f)
        {
            var correction = -Mathf.Sign(forwardRate) * forwardExcess * 10f;
            body.AddTorque(transform.forward * correction, ForceMode.Acceleration);
        }
    }

    private Vector2 ReadMoveInput()
    {
        var input = resolvedMoveAction != null ? resolvedMoveAction.ReadValue<Vector2>() : Vector2.zero;
        if (input.sqrMagnitude > 1f) input.Normalize();
        if (input.sqrMagnitude > 0.0001f) return input;
        if (!keyboardFallbackInput) return input;
        return ReadKeyboardMoveInput();
    }

    private float ReadVerticalInput()
    {
        var ascendPressed = resolvedAscendAction != null && resolvedAscendAction.IsPressed();
        var descendPressed = resolvedDescendAction != null && resolvedDescendAction.IsPressed();

        if (!ascendPressed && !descendPressed && keyboardFallbackInput)
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
        resolvedMoveAction = moveAction != null ? moveAction.action : null;
        resolvedAscendAction = ascendAction != null ? ascendAction.action : null;
        resolvedDescendAction = descendAction != null ? descendAction.action : null;
        resolvedEngineToggleAction = engineToggleAction != null ? engineToggleAction.action : null;
    }

    private void SetActionsEnabled(bool enabled)
    {
        SetActionEnabled(resolvedMoveAction, enabled);
        SetActionEnabled(resolvedAscendAction, enabled);
        SetActionEnabled(resolvedDescendAction, enabled);
        SetActionEnabled(resolvedEngineToggleAction, enabled);
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
        if (!applyRigidbodySettings) return;

        body.mass = rigidbodyMass;
        body.linearDamping = linearDamping;
        body.angularDamping = angularDamping;
        body.maxAngularVelocity = maxAngularSpeed;
        body.centerOfMass = centerOfMassOffset;
        body.ResetInertiaTensor();
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    private void EnsurePhysicsCanSimulate()
    {
        if (!ensureRigidbodyCanSimulate || body == null) return;

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
}
