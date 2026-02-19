using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[AddComponentMenu("Gameplay/Audio/Timed Action Sequence Player")]
public class TimedActionSequencePlayer : MonoBehaviour
{
    [System.Serializable]
    public class TimedActionStep
    {
        [Min(0f)] public float delay;
        public AudioSource audioSource;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        public bool waitForClipToFinish = true;
        public UnityEvent onStep;
    }

    [System.Serializable]
    public class TimedActionSequence
    {
        public bool stopPreviousSequence = true;
        public TimedActionStep[] steps;
    }

    private Coroutine activeSequence;

    public void PlaySequence(TimedActionSequence sequence)
    {
        if (sequence == null) return;
        PlaySteps(sequence.steps, sequence.stopPreviousSequence);
    }

    public void PlaySteps(IReadOnlyList<TimedActionStep> steps, bool stopPreviousSequence = true)
    {
        if (steps == null || steps.Count == 0) return;
        if (stopPreviousSequence && activeSequence != null)
        {
            StopCoroutine(activeSequence);
            activeSequence = null;
        }

        activeSequence = StartCoroutine(RunSteps(steps));
    }

    public void StopSequence()
    {
        if (activeSequence == null) return;
        StopCoroutine(activeSequence);
        activeSequence = null;
    }

    private IEnumerator RunSteps(IReadOnlyList<TimedActionStep> steps)
    {
        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            if (step == null) continue;

            if (step.delay > 0f)
                yield return new WaitForSecondsRealtime(step.delay);

            if (step.audioSource != null && step.clip != null)
            {
                step.audioSource.PlayOneShot(step.clip, Mathf.Clamp01(step.volume));
                if (step.waitForClipToFinish)
                {
                    var pitch = step.audioSource.pitch;
                    if (Mathf.Abs(pitch) < 0.001f) pitch = 1f;
                    yield return new WaitForSecondsRealtime(step.clip.length / Mathf.Abs(pitch));
                }
            }

            step.onStep?.Invoke();
        }

        activeSequence = null;
    }
}
