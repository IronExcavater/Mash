using UnityEngine;

[RequireComponent(typeof(Camera))]
public class HelicopterCameraController : MonoBehaviour
{
    public enum CameraModeSource
    {
        FollowFlightController = 0,
        Manual = 1
    }

    [System.Serializable]
    private struct FollowSettings
    {
        [SerializeField, Range(1f, 100f)] public float positionPercent60Fps;
        [SerializeField, Range(1f, 100f)] public float rotationPercent60Fps;
    }

    [System.Serializable]
    private struct LookAheadSettings
    {
        [SerializeField, Min(0f)] public float distance;
        [SerializeField, Min(0f)] public float startSpeed;
        [SerializeField, Min(0.01f)] public float fullSpeed;
        [SerializeField, Min(0f)] public float directionSmoothing;
    }

    [System.Serializable]
    private struct ModeCameraSettings
    {
        [SerializeField] public Vector3 positionOffset;
        [SerializeField] public Vector3 fixedEulerAngles;
        [SerializeField] public LookAheadSettings lookAhead;
        [SerializeField] public FollowSettings follow;
        [SerializeField, Min(1f)] public float fieldOfView;
    }

    [Header("References")]
    [SerializeField] private bool autoAssignReferences = true;
    [ConditionalField("autoAssignReferences", false)]
    [SerializeField] private Transform target;
    [ConditionalField("autoAssignReferences", false)]
    [SerializeField] private HelicopterFlightController flightController;

    [Header("Mode")]
    [SerializeField] private CameraModeSource modeSource = CameraModeSource.FollowFlightController;
    [ConditionalField("modeSource", (int)CameraModeSource.Manual)]
    [SerializeField] private HelicopterFlightController.ControlScheme manualControlScheme = HelicopterFlightController.ControlScheme.Simple;
    [SerializeField] private bool smoothModeSwitch = true;
    [ConditionalField("smoothModeSwitch", true)]
    [SerializeField, Min(0f)] private float modeBlendSpeed = 4f;
    [SerializeField] private bool snapOnEnable = true;

    [ConditionalField("showSimpleModeSettings", true, "Simple Mode")]
    [SerializeField] private ModeCameraSettings simpleSettings = new ModeCameraSettings
    {
        positionOffset = new Vector3(0f, 30f, 2f),
        fixedEulerAngles = new Vector3(74f, 0f, 0f),
        lookAhead = new LookAheadSettings
        {
            distance = 4f,
            startSpeed = 0.8f,
            fullSpeed = 7f,
            directionSmoothing = 8f
        },
        follow = new FollowSettings
        {
            positionPercent60Fps = 18f,
            rotationPercent60Fps = 18f
        },
        fieldOfView = 55f
    };

    [ConditionalField("showComplexModeSettings", true, "Complex Mode")]
    [SerializeField] private ModeCameraSettings complexSettings = new ModeCameraSettings
    {
        positionOffset = new Vector3(0f, 6.5f, -14f),
        fixedEulerAngles = new Vector3(18f, 0f, 0f),
        lookAhead = new LookAheadSettings
        {
            distance = 6f,
            startSpeed = 0.5f,
            fullSpeed = 9f,
            directionSmoothing = 0f
        },
        follow = new FollowSettings
        {
            positionPercent60Fps = 11f,
            rotationPercent60Fps = 12f
        },
        fieldOfView = 65f
    };
    [ConditionalField("showComplexModeSettings", true)]
    [SerializeField] private Vector3 complexLookAtOffset = new Vector3(0f, 1.8f, 6f);

    private Camera attachedCamera;
    private Rigidbody targetBody;
    private float complexBlend;
    private Vector3 simpleFilteredPlanarVelocity;
    [HideInInspector, SerializeField] private bool showSimpleModeSettings = true;
    [HideInInspector, SerializeField] private bool showComplexModeSettings = true;
    private bool loggedInvalidRig;

    private void Awake()
    {
        attachedCamera = GetComponent<Camera>();
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveSectionVisibility();
        if (snapOnEnable) SnapToCurrentMode();
    }

    private void LateUpdate()
    {
        ResolveReferences();
        ResolveSectionVisibility();
        if (target == null) return;
        if (HasInvalidPlacement()) return;

        var dt = Time.deltaTime;
        var targetBlend = ActiveControlScheme == HelicopterFlightController.ControlScheme.Complex ? 1f : 0f;
        complexBlend = smoothModeSwitch
            ? Mathf.MoveTowards(complexBlend, targetBlend, modeBlendSpeed * dt)
            : targetBlend;

        var simpleLookAheadForward = GetSimpleLookAheadForward(dt);
        var complexLookAheadForward = GetComplexLookAheadForward();
        var simpleAnchor = target.position + simpleLookAheadForward * simpleSettings.lookAhead.distance;
        var complexAnchor = target.position + complexLookAheadForward * complexSettings.lookAhead.distance;

        var simplePosition = simpleAnchor + simpleSettings.positionOffset;
        var complexPosition = complexAnchor + target.TransformDirection(complexSettings.positionOffset);
        var desiredPosition = Vector3.Lerp(simplePosition, complexPosition, complexBlend);

        var simpleRotation = Quaternion.Euler(simpleSettings.fixedEulerAngles);
        var lookAtPoint = complexAnchor + target.TransformDirection(complexLookAtOffset);
        var lookDirection = lookAtPoint - desiredPosition;
        if (lookDirection.sqrMagnitude < 0.0001f) lookDirection = target.forward;
        var complexRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        var desiredRotation = Quaternion.Slerp(simpleRotation, complexRotation, complexBlend);

        var positionPercent = Mathf.Lerp(simpleSettings.follow.positionPercent60Fps, complexSettings.follow.positionPercent60Fps, complexBlend);
        var rotationPercent = Mathf.Lerp(simpleSettings.follow.rotationPercent60Fps, complexSettings.follow.rotationPercent60Fps, complexBlend);
        var positionLerp = GetFrameRateIndependentLerpFromPercent60Fps(positionPercent, dt);
        var rotationLerp = GetFrameRateIndependentLerpFromPercent60Fps(rotationPercent, dt);

        transform.position = Vector3.Lerp(transform.position, desiredPosition, positionLerp);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationLerp);

        if (attachedCamera != null)
        {
            var targetFov = Mathf.Lerp(simpleSettings.fieldOfView, complexSettings.fieldOfView, complexBlend);
            attachedCamera.fieldOfView = Mathf.Lerp(attachedCamera.fieldOfView, targetFov, positionLerp);
        }
    }

    [ContextMenu("Snap To Current Mode")]
    public void SnapToCurrentMode()
    {
        ResolveReferences();
        if (target == null) return;
        if (HasInvalidPlacement()) return;

        complexBlend = ActiveControlScheme == HelicopterFlightController.ControlScheme.Complex ? 1f : 0f;
        var simpleLookAheadForward = GetSimpleLookAheadForward(0f);
        var complexLookAheadForward = GetComplexLookAheadForward();

        var simpleAnchor = target.position + simpleLookAheadForward * simpleSettings.lookAhead.distance;
        var complexAnchor = target.position + complexLookAheadForward * complexSettings.lookAhead.distance;
        var desiredPosition = complexBlend < 0.5f
            ? simpleAnchor + simpleSettings.positionOffset
            : complexAnchor + target.TransformDirection(complexSettings.positionOffset);

        var desiredRotation = complexBlend < 0.5f
            ? Quaternion.Euler(simpleSettings.fixedEulerAngles)
            : Quaternion.LookRotation((complexAnchor + target.TransformDirection(complexLookAtOffset) - desiredPosition).normalized, Vector3.up);

        transform.SetPositionAndRotation(desiredPosition, desiredRotation);

        if (attachedCamera != null)
            attachedCamera.fieldOfView = complexBlend < 0.5f ? simpleSettings.fieldOfView : complexSettings.fieldOfView;
    }

    private HelicopterFlightController.ControlScheme ActiveControlScheme =>
        modeSource == CameraModeSource.FollowFlightController && flightController != null
            ? flightController.CurrentControlScheme
            : manualControlScheme;

    private void ResolveReferences()
    {
        if (attachedCamera == null) attachedCamera = GetComponent<Camera>();

        if (!autoAssignReferences) return;

        if (flightController == null && target != null)
            flightController = target.GetComponent<HelicopterFlightController>();

        if (flightController == null)
            flightController = FindFirstObjectByType<HelicopterFlightController>();

        if (target == null && flightController != null)
            target = flightController.transform;

        if (targetBody == null && target != null)
            targetBody = target.GetComponent<Rigidbody>();
    }

    private bool HasInvalidPlacement()
    {
        if (target == null) return false;

        var invalid = target == transform || target.IsChildOf(transform) || transform.IsChildOf(target);
        if (!invalid)
        {
            loggedInvalidRig = false;
            return false;
        }

        if (loggedInvalidRig) return true;

        loggedInvalidRig = true;
        Debug.LogError(
            "HelicopterCameraController must be on an independent Camera object, not on the helicopter hierarchy. " +
            "Place this script on Main Camera and assign Target to the helicopter root.",
            this);

        return true;
    }

    private void OnValidate()
    {
        if (modeBlendSpeed < 0f) modeBlendSpeed = 0f;
        if (simpleSettings.lookAhead.fullSpeed < simpleSettings.lookAhead.startSpeed + 0.01f)
            simpleSettings.lookAhead.fullSpeed = simpleSettings.lookAhead.startSpeed + 0.01f;
        if (complexSettings.lookAhead.fullSpeed < complexSettings.lookAhead.startSpeed + 0.01f)
            complexSettings.lookAhead.fullSpeed = complexSettings.lookAhead.startSpeed + 0.01f;
        if (attachedCamera == null) attachedCamera = GetComponent<Camera>();
        ResolveReferences();
        ResolveSectionVisibility();
    }

    private static float GetFrameRateIndependentLerpFromPercent60Fps(float percent, float dt)
    {
        var t = Mathf.Clamp(percent, 1f, 100f) * 0.01f;
        if (t >= 0.9999f) return 1f;
        return 1f - Mathf.Pow(1f - t, dt * 60f);
    }

    private Vector3 GetSimpleLookAheadForward(float dt)
    {
        var planarForward = GetPlanarTargetForward();
        var planarVelocity = GetPlanarTargetVelocity();

        if (dt <= 0f || simpleSettings.lookAhead.directionSmoothing <= 0f)
        {
            simpleFilteredPlanarVelocity = planarVelocity;
        }
        else
        {
            var filterLerp = 1f - Mathf.Exp(-simpleSettings.lookAhead.directionSmoothing * dt);
            simpleFilteredPlanarVelocity = Vector3.Lerp(simpleFilteredPlanarVelocity, planarVelocity, filterLerp);
        }

        var speed = simpleFilteredPlanarVelocity.magnitude;
        var velocityDirection = speed > 0.0001f ? simpleFilteredPlanarVelocity / speed : planarForward;
        var velocityWeight = Mathf.InverseLerp(simpleSettings.lookAhead.startSpeed, simpleSettings.lookAhead.fullSpeed, speed);
        velocityWeight = velocityWeight * velocityWeight * (3f - 2f * velocityWeight);

        return Vector3.Slerp(planarForward, velocityDirection, velocityWeight).normalized;
    }

    private Vector3 GetComplexLookAheadForward()
    {
        var planarVelocity = GetPlanarTargetVelocity();
        if (planarVelocity.sqrMagnitude > 0.09f) return planarVelocity.normalized;
        return GetPlanarTargetForward();
    }

    private Vector3 GetPlanarTargetVelocity()
    {
        if (targetBody != null)
        {
            return new Vector3(targetBody.linearVelocity.x, 0f, targetBody.linearVelocity.z);
        }

        return Vector3.zero;
    }

    private Vector3 GetPlanarTargetForward()
    {
        var planarForward = Vector3.ProjectOnPlane(target.forward, Vector3.up);
        if (planarForward.sqrMagnitude > 0.0001f) return planarForward.normalized;
        return Vector3.forward;
    }

    private void ResolveSectionVisibility()
    {
        if (modeSource == CameraModeSource.Manual)
        {
            showSimpleModeSettings = manualControlScheme == HelicopterFlightController.ControlScheme.Simple;
            showComplexModeSettings = !showSimpleModeSettings;
            return;
        }

        var followMode = flightController != null
            ? flightController.CurrentControlScheme
            : HelicopterFlightController.ControlScheme.Simple;
        showSimpleModeSettings = followMode == HelicopterFlightController.ControlScheme.Simple;
        showComplexModeSettings = followMode == HelicopterFlightController.ControlScheme.Complex;
    }
}
