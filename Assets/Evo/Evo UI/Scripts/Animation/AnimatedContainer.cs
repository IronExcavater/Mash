using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HELP_URL + "animation/animated-container")]
    [AddComponentMenu("Evo/UI/Animation/Animated Container")]
    public class AnimatedContainer : MonoBehaviour
    {
        [Header("Objects")]
        [SerializeField] private bool getChildObjects = true;
        public List<Transform> containerObjects = new();

        [Header("Animation")]
        public AnimationCurve animationCurve = new(new Keyframe(0.0f, 0.0f), new Keyframe(1.0f, 1.0f));
        [Tooltip("Add a delay on enable for the animation process.")]
        [Range(0, 10)] public float animationDelay = 0;

        [Header("Scale")]
        [Tooltip("Sets the object's initial animation scale.")]
        [Range(0, 0.99f)] public float startScale = 0.5f;
        [Tooltip("How long the scaling animation lasts (seconds).")]
        [Range(0, 4)] public float scaleDuration = 0.25f;

        [Header("Fade")]
        [Tooltip("Start fade-in transition after the specified transform scale.")]
        [Range(0, 0.99f)] public float fadeAfterScale = 0.5f;
        [Tooltip("How long the fade animation lasts (seconds).")]
        [Range(0, 1)] public float fadeDuration = 0.2f;

        [Header("Settings")]
        public bool useUnscaledTime = false;
        public bool playOnEnable = true;
        [Tooltip("Play the animation only once.")]
        public bool playOnce = false;
        [Tooltip("Add a cooldown before skipping to the next item.")]
        [Range(0, 1)] public float itemCooldown = 0.1f;

        // Cache
        bool playedOnce;
        Coroutine animCoroutine;
        readonly List<AnimatedContainerObject> cachedObjects = new();

        void Awake()
        {
            if (containerObjects.Count > 0)
            {
                foreach (Transform child in containerObjects)
                {
                    if (child != null)
                    {
                        AnimatedContainerObject temp = child.gameObject.AddComponent<AnimatedContainerObject>();
                        temp.Initialize(this);
                        cachedObjects.Add(temp);
                    }
                }
            }

            if (getChildObjects)
            {
                foreach (Transform child in transform)
                {
                    AnimatedContainerObject temp = child.gameObject.AddComponent<AnimatedContainerObject>();
                    temp.Initialize(this);
                    cachedObjects.Add(temp);
                }
            }
        }

        void OnEnable()
        {
            if (playOnEnable)
            {
                Animate();
            }
        }

        void OnDisable()
        {
            if (animCoroutine != null)
            {
                StopCoroutine(animCoroutine);
                animCoroutine = null;
            }
        }

        public void Animate()
        {
            if (playOnce && playedOnce)
                return;

            if (animCoroutine != null) { StopCoroutine(animCoroutine); }
            animCoroutine = StartCoroutine(AnimateHelper());
        }

        IEnumerator AnimateHelper()
        {
            yield return useUnscaledTime ? new WaitForSecondsRealtime(animationDelay) : new WaitForSeconds(animationDelay);

            float tempTime = 0;

            if (cachedObjects.Count > 0)
            {
                foreach (AnimatedContainerObject item in cachedObjects)
                {
                    if (item != null)
                    {
                        item.Process(tempTime);
                        tempTime += itemCooldown;
                    }
                }
            }

            playedOnce = true;
        }

        public bool PlayedOnce => playedOnce;
    }

    public class AnimatedContainerObject : MonoBehaviour
    {
        AnimatedContainer container;
        CanvasGroup cg;
        Vector3 defaultScale;
        Coroutine currentAnimation;

        public void Initialize(AnimatedContainer targetContainer)
        {
            container = targetContainer;
            defaultScale = transform.localScale;

            if (!gameObject.TryGetComponent<CanvasGroup>(out var tempCg)) { tempCg = gameObject.AddComponent<CanvasGroup>(); }
            cg = tempCg;
            if (container.playOnce || container.playOnEnable) { cg.alpha = 0; }
        }

        void OnDisable()
        {
            if (currentAnimation != null)
            {
                StopCoroutine(currentAnimation);
                currentAnimation = null;
            }

            if (container.playOnce && container.PlayedOnce)
            {
                if (cg != null) { cg.alpha = 1; }
                transform.localScale = defaultScale;
            }
        }

        public void Process(float time)
        {
            if (!gameObject.activeInHierarchy)
                return;

            if (currentAnimation != null) { StopCoroutine(currentAnimation); }
            currentAnimation = StartCoroutine(ProcessAnimation(time));
        }

        IEnumerator ProcessAnimation(float time)
        {
            transform.localScale = new Vector3(container.startScale, container.startScale, container.startScale);
            cg.interactable = false;
            cg.blocksRaycasts = false;
            cg.alpha = 0;

            yield return container.useUnscaledTime ? new WaitForSecondsRealtime(time) : new WaitForSeconds(time);

            float scaleElapsed = 0f;
            float fadeElapsed = 0f;
            float scaleDuration = Mathf.Max(0.0001f, container.scaleDuration);
            float fadeDuration = Mathf.Max(0.0001f, container.fadeDuration);

            bool fadeStarted = false;
            bool scaleComplete = false;
            bool fadeComplete = false;

            while (!scaleComplete || !fadeComplete)
            {
                float delta = container.useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

                // Handle scale animation
                if (!scaleComplete)
                {
                    scaleElapsed += delta / scaleDuration;
                    float curveValue = container.animationCurve.Evaluate(Mathf.Clamp01(scaleElapsed));
                    float normalizedScale = Mathf.Clamp01(curveValue);

                    float currentScale = Mathf.Lerp(container.startScale, defaultScale.x, normalizedScale);
                    transform.localScale = new Vector3(currentScale, currentScale, currentScale);

                    // Start fade when we reach the threshold
                    if (!fadeStarted && normalizedScale >= container.fadeAfterScale)
                    {
                        fadeStarted = true;
                        cg.interactable = true;
                        cg.blocksRaycasts = true;
                    }

                    if (scaleElapsed >= 1f)
                    {
                        transform.localScale = defaultScale;
                        scaleComplete = true;
                    }
                }

                // Handle fade animation
                if (fadeStarted && !fadeComplete)
                {
                    fadeElapsed += delta;
                    float targetAlpha = Mathf.Clamp01(fadeElapsed / fadeDuration);
                    cg.alpha = targetAlpha;
                    if (fadeElapsed >= fadeDuration) { fadeComplete = true; }
                }

                yield return null;
            }

            transform.localScale = defaultScale;
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;
            currentAnimation = null;
        }
    }
}