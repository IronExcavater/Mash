using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Gameplay/Audio/Battlefield Ambience Controller")]
public class BattlefieldAmbienceController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private bool autoAssignReferences = true;
    [ConditionalField("autoAssignReferences", false)]
    [SerializeField] private GameFlowController gameFlow;

    [Header("Audio")]
    [SerializeField] private bool autoAssignAudioSource = true;
    [ConditionalField("autoAssignAudioSource", false)]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] randomBattlefieldClips;
    [SerializeField, Min(0f)] private float minDelayBetweenClips = 2.2f;
    [SerializeField, Min(0f)] private float maxDelayBetweenClips = 6.5f;
    [SerializeField, Range(0f, 1f)] private float volume = 0.42f;
    [SerializeField, Min(0.01f)] private float fadeOutDuration = 0.45f;
    [SerializeField, Min(0.01f)] private float fadeInDuration = 0.28f;

    private float nextPlayAt;
    private int lastClipIndex = -1;
    private float currentGain = 1f;
    private bool targetActive = true;

    private void Awake()
    {
        ResolveReferences();
        EnsureAudioSource();
        ScheduleNextClip();
    }

    private void OnEnable()
    {
        ResolveReferences();
        if (gameFlow != null) gameFlow.SessionStateChanged += HandleSessionStateChanged;
        targetActive = IsActiveState();
        currentGain = targetActive ? 1f : 0f;
        ScheduleNextClip();
    }

    private void OnDisable()
    {
        if (gameFlow != null) gameFlow.SessionStateChanged -= HandleSessionStateChanged;
        StopImmediate();
    }

    private void Update()
    {
        targetActive = IsActiveState();
        UpdateFade();

        if (!IsActiveState())
        {
            if (audioSource != null && audioSource.isPlaying && currentGain <= 0.001f)
                audioSource.Stop();
            return;
        }

        if (audioSource == null) return;
        if (audioSource.isPlaying) return;
        if (Time.unscaledTime < nextPlayAt) return;

        var clip = ChooseClip(randomBattlefieldClips, ref lastClipIndex);
        if (clip != null)
            audioSource.PlayOneShot(clip, Mathf.Clamp01(volume * currentGain));

        ScheduleNextClip();
    }

    private bool IsActiveState()
    {
        return gameFlow == null || gameFlow.State == GameFlowController.SessionState.Playing;
    }

    private void HandleSessionStateChanged(GameFlowController.SessionState _, GameFlowController.SessionState next)
    {
        targetActive = next == GameFlowController.SessionState.Playing;
        if (targetActive) ScheduleNextClip();
    }

    private void StopImmediate()
    {
        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();
    }

    private void UpdateFade()
    {
        var dt = Time.unscaledDeltaTime;
        var target = targetActive ? 1f : 0f;
        if (targetActive)
            currentGain = Mathf.MoveTowards(currentGain, target, dt / Mathf.Max(0.01f, fadeInDuration));
        else
            currentGain = Mathf.MoveTowards(currentGain, target, dt / Mathf.Max(0.01f, fadeOutDuration));
    }

    private void ResolveReferences()
    {
        if (!autoAssignReferences) return;
        if (gameFlow == null) gameFlow = FindFirstObjectByType<GameFlowController>();
    }

    private void EnsureAudioSource()
    {
        if (autoAssignAudioSource && audioSource == null)
        {
            var child = transform.Find("BattlefieldAmbienceAudio");
            if (child == null)
            {
                var go = new GameObject("BattlefieldAmbienceAudio");
                go.transform.SetParent(transform, false);
                child = go.transform;
            }

            audioSource = child.GetComponent<AudioSource>();
            if (audioSource == null) audioSource = child.gameObject.AddComponent<AudioSource>();
        }

        if (audioSource == null) return;
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
    }

    private void ScheduleNextClip()
    {
        var minDelay = Mathf.Max(0f, minDelayBetweenClips);
        var maxDelay = Mathf.Max(minDelay, maxDelayBetweenClips);
        nextPlayAt = Time.unscaledTime + Random.Range(minDelay, maxDelay);
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
