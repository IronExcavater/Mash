using System.Collections.Generic;
using UnityEngine;

public class HelicopterRotorController : MonoBehaviour
{
    private static readonly string[] RotorKeywords = { "rotor", "blade", "prop" };
    private static readonly string[] TailKeywords = { "tail", "rear" };

    [Header("Rotors")]
    [SerializeField] private bool autoAssignRotors = true;
    [ConditionalField("autoAssignRotors", false)]
    [SerializeField] private Transform mainRotorTransform;
    [ConditionalField("autoAssignRotors", false)]
    [SerializeField] private Transform tailRotorTransform;

    [Header("Center Spin")]
    [SerializeField] private bool spinMainAroundMeshCenter = true;
    [SerializeField] private bool spinTailAroundMeshCenter = true;

    [Header("Axes (Local)")]
    [SerializeField] private Vector3 mainRotorLocalAxis = Vector3.up;
    [SerializeField] private Vector3 tailRotorLocalAxis = Vector3.right;

    [Header("Spin Speeds")]
    [SerializeField, MinMaxRange(0f, 5000f)] private MinMaxFloat mainRotorSpeedRange = new MinMaxFloat(650f, 1700f);
    [SerializeField, MinMaxRange(0f, 5000f)] private MinMaxFloat tailRotorSpeedRange = new MinMaxFloat(800f, 2600f);
    [SerializeField, Min(10f)] private float spoolUpRate = 2800f;
    [SerializeField, Min(10f)] private float spoolDownRate = 950f;

    [Header("Audio")]
    [SerializeField] private bool autoAssignAudioSource = true;
    [ConditionalField("autoAssignAudioSource", false)]
    [SerializeField] private AudioSource rotorAudioSource;
    [SerializeField] private AudioClip rotorLoopClip;
    [SerializeField, MinMaxRange(0f, 2f)] private MinMaxFloat rotorPitchRange = new MinMaxFloat(0.75f, 1.35f);
    [SerializeField, MinMaxRange(0f, 1f)] private MinMaxFloat rotorVolumeRange = new MinMaxFloat(0.2f, 0.85f);

    private HelicopterFlightController flightController;
    private Rigidbody body;

    private Transform mainSpinTransform;
    private Transform tailSpinTransform;
    private Quaternion mainBaseRotation;
    private Quaternion tailBaseRotation;

    private float mainRotorAngle;
    private float tailRotorAngle;
    private float mainRotorSpeed;
    private float tailRotorSpeed;

    private void Awake()
    {
        if (autoAssignRotors) TryAutoAssignRotors();

        flightController = GetComponent<HelicopterFlightController>();
        body = GetComponent<Rigidbody>();

        mainSpinTransform = CreateSpinTransform(mainRotorTransform, spinMainAroundMeshCenter);
        tailSpinTransform = CreateSpinTransform(tailRotorTransform, spinTailAroundMeshCenter);

        if (mainSpinTransform != null) mainBaseRotation = mainSpinTransform.localRotation;
        if (tailSpinTransform != null) tailBaseRotation = tailSpinTransform.localRotation;
        ConfigureAudioSource();
    }

    private void LateUpdate()
    {
        var dt = Time.deltaTime;
        var engineOn = flightController == null || flightController.IsEngineOn;
        var motion01 = GetMotionAmount01();
        var accel = engineOn ? spoolUpRate : spoolDownRate;

        var targetMain = engineOn ? mainRotorSpeedRange.Lerp(motion01) : 0f;
        var targetTail = engineOn ? tailRotorSpeedRange.Lerp(motion01) : 0f;

        mainRotorSpeed = Mathf.MoveTowards(mainRotorSpeed, targetMain, accel * dt);
        tailRotorSpeed = Mathf.MoveTowards(tailRotorSpeed, targetTail, accel * dt);

        mainRotorAngle = Mathf.Repeat(mainRotorAngle + mainRotorSpeed * dt, 360f);
        tailRotorAngle = Mathf.Repeat(tailRotorAngle + tailRotorSpeed * dt, 360f);

        ApplySpin(mainSpinTransform, mainBaseRotation, mainRotorLocalAxis, mainRotorAngle);
        ApplySpin(tailSpinTransform, tailBaseRotation, tailRotorLocalAxis, tailRotorAngle);
        UpdateRotorAudio();
    }

    [ContextMenu("Auto Assign Rotors")]
    public void TryAutoAssignRotors()
    {
        var rotorCandidates = GetRotorCandidates();
        mainRotorTransform = FindMainRotor(rotorCandidates);
        tailRotorTransform = FindTailRotor(rotorCandidates, mainRotorTransform);
    }

    private static void ApplySpin(Transform spinTransform, Quaternion baseRotation, Vector3 localAxis, float angle)
    {
        if (spinTransform == null) return;
        if (localAxis.sqrMagnitude <= 0.0001f) return;

        var spin = Quaternion.AngleAxis(angle, localAxis.normalized);
        spinTransform.localRotation = baseRotation * spin;
    }

    private float GetMotionAmount01()
    {
        if (flightController != null)
            return Mathf.Clamp01(Mathf.Max(flightController.EnginePower01, flightController.HorizontalSpeed01));

        if (body == null) return 0f;
        var speed = new Vector2(body.linearVelocity.x, body.linearVelocity.z).magnitude;
        return Mathf.Clamp01(speed / 20f);
    }

    private void ConfigureAudioSource()
    {
        if (autoAssignAudioSource)
        {
            rotorAudioSource = GetComponent<AudioSource>();
            if (rotorAudioSource == null) rotorAudioSource = gameObject.AddComponent<AudioSource>();
        }

        if (rotorAudioSource == null) return;

        rotorAudioSource.playOnAwake = false;
        rotorAudioSource.loop = true;
        rotorAudioSource.spatialBlend = 1f;
        rotorAudioSource.clip = rotorLoopClip;
    }

    private void UpdateRotorAudio()
    {
        if (rotorAudioSource == null) return;

        var rpm01 = mainRotorSpeedRange.max <= 0.001f ? 0f : Mathf.Clamp01(mainRotorSpeed / mainRotorSpeedRange.max);
        rotorAudioSource.pitch = rotorPitchRange.Lerp(rpm01);
        rotorAudioSource.volume = rotorVolumeRange.Lerp(rpm01);

        if (rotorAudioSource.clip == null && rotorLoopClip != null)
            rotorAudioSource.clip = rotorLoopClip;

        if (rotorAudioSource.clip != null && !rotorAudioSource.isPlaying && rotorAudioSource.volume > 0.01f)
            rotorAudioSource.Play();

        if (rotorAudioSource.isPlaying && rotorAudioSource.volume <= 0.01f)
            rotorAudioSource.Stop();
    }

    private Transform CreateSpinTransform(Transform rotor, bool spinAroundCenter)
    {
        if (rotor == null) return null;
        if (!spinAroundCenter) return rotor;

        var renderer = FindRotorRenderer(rotor);
        if (renderer == null) return rotor;

        var center = renderer.bounds.center;
        var pivotObject = new GameObject($"{rotor.name}_CenterPivotRuntime");
        var pivot = pivotObject.transform;
        pivot.SetParent(rotor.parent, true);
        pivot.position = center;
        pivot.rotation = rotor.rotation;

        rotor.SetParent(pivot, true);
        return pivot;
    }

    private static Renderer FindRotorRenderer(Transform rotor)
    {
        if (rotor == null) return null;
        var direct = rotor.GetComponent<Renderer>();
        if (direct != null) return direct;
        return rotor.GetComponentInChildren<Renderer>(true);
    }

    private Transform[] GetRotorCandidates()
    {
        var all = GetComponentsInChildren<Transform>(true);
        var list = new List<Transform>(all.Length);
        for (var i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (!LooksLikeRotor(t.name)) continue;
            list.Add(t);
        }
        return list.ToArray();
    }

    private static Transform FindMainRotor(Transform[] candidates)
    {
        Transform first = null;
        for (var i = 0; i < candidates.Length; i++)
        {
            var rotor = candidates[i];
            if (first == null) first = rotor;
            if (!LooksLikeTailRotor(rotor.name)) return rotor;
        }
        return first;
    }

    private static Transform FindTailRotor(Transform[] candidates, Transform mainRotor)
    {
        for (var i = 0; i < candidates.Length; i++)
        {
            var rotor = candidates[i];
            if (rotor == mainRotor) continue;
            if (LooksLikeTailRotor(rotor.name)) return rotor;
        }
        return null;
    }

    private static bool LooksLikeRotor(string name)
    {
        var lower = name.ToLowerInvariant();
        return ContainsAny(lower, RotorKeywords);
    }

    private static bool LooksLikeTailRotor(string name)
    {
        var lower = name.ToLowerInvariant();
        return ContainsAny(lower, TailKeywords);
    }

    private static bool ContainsAny(string value, string[] keywords)
    {
        for (var i = 0; i < keywords.Length; i++)
            if (value.Contains(keywords[i])) return true;

        return false;
    }

    private void OnValidate()
    {
        if (mainRotorTransform != null && tailRotorTransform == mainRotorTransform)
            tailRotorTransform = null;

        mainRotorSpeedRange.ClampAndOrder(0f, 5000f);
        tailRotorSpeedRange.ClampAndOrder(0f, 5000f);
        rotorPitchRange.ClampAndOrder(0f, 2f);
        rotorVolumeRange.ClampAndOrder(0f, 1f);
    }
}
