using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Gameplay/Audio/Voice Comms Controller")]
public class VoiceCommsController : MonoBehaviour
{
    private struct CommsMessage
    {
        public AudioClip clip;
        public bool useRadioIn;
    }

    [Header("References")]
    [SerializeField] private bool autoAssignReferences = true;
    [ConditionalField("autoAssignReferences", false)]
    [SerializeField] private GameFlowController gameFlow;
    [ConditionalField("autoAssignReferences", false)]
    [SerializeField] private HelicopterCapacity helicopterCapacity;
    [ConditionalField("autoAssignReferences", false)]
    [SerializeField] private HelicopterCollisionHandler collisionHandler;

    [Header("Audio")]
    [SerializeField] private bool autoAssignAudioSource = true;
    [ConditionalField("autoAssignAudioSource", false)]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip radioInSfxClip;
    [SerializeField] private AudioClip[] startGameRadioClips;
    [SerializeField] private AudioClip[] missionCompleteRadioClips;
    [SerializeField] private AudioClip[] gameOverRadioClips;
    [SerializeField] private AudioClip[] soldierPickupRadioClips;
    [SerializeField] private AudioClip[] soldierDropoffRadioClips;
    [SerializeField] private AudioClip[] capacityFullRadioClips;
    [SerializeField, Range(0f, 1f)] private float voiceVolume = 0.78f;
    [SerializeField, Range(0f, 1f)] private float radioInVolume = 0.72f;
    [SerializeField, Min(0f)] private float delayAfterRadioInSeconds = 0.07f;

    private readonly Queue<CommsMessage> messageQueue = new Queue<CommsMessage>();
    private Coroutine sequenceRoutine;
    private int lastStartIndex = -1;
    private int lastCompleteIndex = -1;
    private int lastOverIndex = -1;
    private int lastPickupIndex = -1;
    private int lastDropoffIndex = -1;
    private int lastCapacityFullIndex = -1;
    private bool previousCrashState;

    private void Awake()
    {
        ResolveReferences();
        EnsureAudioSource();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeEvents();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    private void Update()
    {
        if (collisionHandler == null) return;
        var isCrashNow = collisionHandler.IsCrashing || collisionHandler.IsCrashComplete;
        if (isCrashNow && !previousCrashState)
            EnqueueFrom(gameOverRadioClips, ref lastOverIndex, true);
        previousCrashState = isCrashNow;
    }

    private void ResolveReferences()
    {
        if (!autoAssignReferences) return;
        if (gameFlow == null) gameFlow = FindFirstObjectByType<GameFlowController>();
        if (helicopterCapacity == null) helicopterCapacity = FindFirstObjectByType<HelicopterCapacity>();
        if (collisionHandler == null) collisionHandler = FindFirstObjectByType<HelicopterCollisionHandler>();
    }

    private void EnsureAudioSource()
    {
        if (autoAssignAudioSource && audioSource == null)
        {
            var child = transform.Find("VoiceCommsAudio");
            if (child == null)
            {
                var go = new GameObject("VoiceCommsAudio");
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

    private void SubscribeEvents()
    {
        if (gameFlow != null)
            gameFlow.SessionStateChanged += OnSessionStateChanged;
        if (helicopterCapacity != null)
            helicopterCapacity.SoldierBoarded += OnSoldierBoarded;
        if (helicopterCapacity != null)
            helicopterCapacity.SoldiersRescuedAtHelipad += OnSoldiersRescuedAtHelipad;
        if (helicopterCapacity != null)
            helicopterCapacity.CapacityFullReached += OnCapacityFullReached;
    }

    private void UnsubscribeEvents()
    {
        if (gameFlow != null)
            gameFlow.SessionStateChanged -= OnSessionStateChanged;
        if (helicopterCapacity != null)
            helicopterCapacity.SoldierBoarded -= OnSoldierBoarded;
        if (helicopterCapacity != null)
            helicopterCapacity.SoldiersRescuedAtHelipad -= OnSoldiersRescuedAtHelipad;
        if (helicopterCapacity != null)
            helicopterCapacity.CapacityFullReached -= OnCapacityFullReached;
    }

    private void OnSessionStateChanged(GameFlowController.SessionState previous, GameFlowController.SessionState next)
    {
        if (next == GameFlowController.SessionState.Playing && previous != GameFlowController.SessionState.Paused)
        {
            EnqueueFrom(startGameRadioClips, ref lastStartIndex, true);
            return;
        }

        if (next == GameFlowController.SessionState.MissionComplete)
            EnqueueFrom(missionCompleteRadioClips, ref lastCompleteIndex, true);
    }

    private void OnSoldierBoarded(SoldierAgent _)
    {
        // Skip pickup comm when this pickup reaches the final required soldier count.
        if (gameFlow != null && helicopterCapacity != null &&
            helicopterCapacity.TotalBoardedCount >= gameFlow.RequiredSoldierCount)
            return;

        EnqueueFrom(soldierPickupRadioClips, ref lastPickupIndex, true);
    }

    private void OnSoldiersRescuedAtHelipad(int _)
    {
        // On final rescue, suppress dropoff chatter so mission-complete comm is the only line.
        if (gameFlow != null && helicopterCapacity != null &&
            helicopterCapacity.TotalRescuedCount >= gameFlow.RequiredSoldierCount)
            return;

        EnqueueFrom(soldierDropoffRadioClips, ref lastDropoffIndex, true);
    }

    private void OnCapacityFullReached()
    {
        EnqueueFrom(capacityFullRadioClips, ref lastCapacityFullIndex, true);
    }

    private void EnqueueFrom(AudioClip[] clips, ref int lastIndex, bool withRadioIn)
    {
        var clip = ChooseClip(clips, ref lastIndex);
        if (clip == null) return;

        messageQueue.Enqueue(new CommsMessage { clip = clip, useRadioIn = withRadioIn });
        if (sequenceRoutine == null)
            sequenceRoutine = StartCoroutine(PlayQueueSequentially());
    }

    private IEnumerator PlayQueueSequentially()
    {
        while (messageQueue.Count > 0)
        {
            if (audioSource == null)
            {
                yield return null;
                continue;
            }

            var message = messageQueue.Dequeue();

            if (message.useRadioIn && radioInSfxClip != null)
            {
                audioSource.PlayOneShot(radioInSfxClip, Mathf.Clamp01(radioInVolume));
                yield return new WaitForSecondsRealtime(radioInSfxClip.length + Mathf.Max(0f, delayAfterRadioInSeconds));
            }

            if (message.clip != null)
            {
                audioSource.PlayOneShot(message.clip, Mathf.Clamp01(voiceVolume));
                yield return new WaitForSecondsRealtime(message.clip.length);
            }
        }

        sequenceRoutine = null;
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
