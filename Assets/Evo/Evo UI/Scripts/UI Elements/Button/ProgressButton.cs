using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HELP_URL + "ui-elements/progress-button")]
    [AddComponentMenu("Evo/UI/UI Elements/Progress Button")]
    public class ProgressButton : Interactive
    {
        [EvoHeader("Progress Settings", Constants.CUSTOM_EDITOR_ID)]
        [Range(0.1f, 30)] public float holdDuration = 2f;
        [Range(0, 3)] public float resetDuration = 0.5f;
        [SerializeField] private AnimationCurve progressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve resetCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [EvoHeader("Completion Settings", Constants.CUSTOM_EDITOR_ID)]
        public bool stayOnComplete = false;
        [Range(0f, 10f)] public float completeStateDuration = 1f;

        [EvoHeader("Progress Indicator", Constants.CUSTOM_EDITOR_ID)]
        [SerializeField] private Image progressFill;
        [SerializeField] private Image.FillMethod fillMethod = Image.FillMethod.Horizontal;
        [SerializeField] private FillOrigin fillOrigin = FillOrigin.Left;
        [SerializeField] private bool clockwise = true;

        [EvoHeader("State Animation", Constants.CUSTOM_EDITOR_ID)]
        [SerializeField, Range(0, 1)] private float progressTransitionDuration = 0.15f;
        [SerializeField] private AnimationType animationType = AnimationType.Slide;
        [SerializeField] private AnimationCurve animationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField, Range(0f, 1f)] private float scaleFrom = 0.94f;
        [SerializeField] private Vector2 slideOffset = new(0, 6);

        [EvoHeader("Canvas Group States", Constants.CUSTOM_EDITOR_ID)]
        [SerializeField] private CanvasGroup normalStateCG;
        [SerializeField] private CanvasGroup inProgressStateCG;
        [SerializeField] private CanvasGroup completeStateCG;

        [EvoHeader("Events", Constants.CUSTOM_EDITOR_ID)]
        public UnityEvent onProgressStart = new();
        public UnityEvent<float> onProgressUpdate = new();
        public UnityEvent onComplete = new();
        public UnityEvent onCancel = new();

        public enum ProgressState
        {
            Normal,
            InProgress,
            Complete
        }

        public enum AnimationType
        {
            None = 0, // Acts as Sequential Fade
            Scale = 1,
            Slide = 2
        }

        public enum FillOrigin
        {
            Bottom = 0,
            Right = 1,
            Top = 2,
            Left = 3
        }

        // Helpers
        bool isHolding = false;
        float currentProgress = 0f;
        Coroutine progressCoroutine;
        Coroutine stateAnimationCoroutine;
        Coroutine completeTimerCoroutine;
        ProgressState currentProgressState = ProgressState.Normal;

        // Caching for performance
        readonly Dictionary<CanvasGroup, Vector2> originalAnchoredPositions = new();
        readonly Dictionary<CanvasGroup, RectTransform> cachedRectTransforms = new();

        // Properties
        public ProgressState CurrentProgressState => currentProgressState;
        public float CurrentProgress => currentProgress;
        public bool IsHolding => isHolding;

        protected override void Awake()
        {
            base.Awake();
            InitializeProgressFill();
            CacheStateInfo();
            SetProgressState(ProgressState.Normal, true);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (currentProgressState != ProgressState.Normal) { ResetProgress(true); }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            StopAllProgressCoroutines();
            isHolding = false;
        }

        void Update()
        {
            if (isHolding && !isPressedDown && Utilities.GetSelectedObject() == gameObject && !Utilities.WasSubmitPressed())
            {
                CancelProgress();
            }
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);

            if (!IsInteractable() || eventData.button != PointerEventData.InputButton.Left)
                return;

            StartProgress();
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);

            if (!IsInteractable())
                return;

            CancelProgress();
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);

            if (!IsInteractable()) { return; }
            if (isHolding) { CancelProgress(); }
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            base.OnDeselect(eventData);

            if (isHolding && IsInteractable())
            {
                CancelProgress();
            }
        }

        public override void OnSubmit(BaseEventData eventData)
        {
            base.OnSubmit(eventData);

            if (!IsInteractable()) { return; }
            if (!isHolding)
            {
                base.OnSubmit(eventData);
                StartProgress();
            }
        }

        void InitializeProgressFill()
        {
            if (progressFill != null)
            {
                progressFill.fillMethod = fillMethod;
                progressFill.fillOrigin = (int)fillOrigin;
                progressFill.fillClockwise = clockwise;
                progressFill.fillAmount = 0f;
                progressFill.type = Image.Type.Filled;
            }
        }

        void CacheStateInfo()
        {
            CacheSingleState(normalStateCG);
            CacheSingleState(inProgressStateCG);
            CacheSingleState(completeStateCG);
        }

        void CacheSingleState(CanvasGroup cg)
        {
            if (cg == null) 
                return;

            RectTransform rect = cg.GetComponent<RectTransform>();
            cachedRectTransforms[cg] = rect;
            if (!originalAnchoredPositions.ContainsKey(cg)) { originalAnchoredPositions[cg] = rect.anchoredPosition; }
        }

        IEnumerator ProgressCoroutine()
        {
            float elapsed = 0f;
            float startProgress = currentProgress;

            while (elapsed < holdDuration && isHolding)
            {
                elapsed += Time.unscaledDeltaTime;
                float rawProgress = Mathf.Clamp01(elapsed / holdDuration);

                float curvedProgress = progressCurve != null && progressCurve.keys.Length > 0
                    ? progressCurve.Evaluate(rawProgress)
                    : rawProgress;

                currentProgress = Mathf.Lerp(startProgress, 1f, curvedProgress);
                UpdateProgressFill(currentProgress);
                onProgressUpdate?.Invoke(currentProgress);

                yield return null;
            }

            if (isHolding && currentProgress >= 0.99f)
            {
                currentProgress = 1f;
                UpdateProgressFill(1f);
                CompleteProgress();
            }
        }

        IEnumerator ResetProgressCoroutine()
        {
            float elapsed = 0f;
            float startProgress = currentProgress;
            float duration = Mathf.Max(0.01f, resetDuration);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float rawProgress = Mathf.Clamp01(elapsed / duration);

                float curvedProgress = resetCurve != null && resetCurve.keys.Length > 0
                    ? resetCurve.Evaluate(rawProgress)
                    : rawProgress;

                currentProgress = Mathf.Lerp(startProgress, 0f, curvedProgress);
                UpdateProgressFill(currentProgress);

                yield return null;
            }

            currentProgress = 0f;
            UpdateProgressFill(0f);
            SetProgressState(ProgressState.Normal);
        }

        IEnumerator CompleteTimerCoroutine()
        {
            yield return new WaitForSecondsRealtime(completeStateDuration);
            ResetProgress(false);
        }

        void UpdateProgressFill(float progress)
        {
            if (progressFill != null)
            {
                progressFill.fillAmount = Mathf.Clamp01(progress);
            }
        }

        CanvasGroup GetCanvasGroupForState(ProgressState state)
        {
            return state switch
            {
                ProgressState.Normal => normalStateCG,
                ProgressState.InProgress => inProgressStateCG,
                ProgressState.Complete => completeStateCG,
                _ => null,
            };
        }

        RectTransform GetRectForState(CanvasGroup cg)
        {
            if (cg != null && cachedRectTransforms.TryGetValue(cg, out var rect)) { return rect; }
            return null;
        }

        void SetProgressState(ProgressState state, bool instant = false)
        {
            if (currentProgressState == state && !instant)
                return;

            ProgressState previousState = currentProgressState;
            currentProgressState = state;

            if (stateAnimationCoroutine != null)
            {
                StopCoroutine(stateAnimationCoroutine);
                stateAnimationCoroutine = null;
            }

            if (instant || !Application.isPlaying || !gameObject.activeInHierarchy) { SetStateImmediate(state); }
            else { stateAnimationCoroutine = StartCoroutine(AnimateProgressState(previousState, state)); }
        }

        void SetStateImmediate(ProgressState state)
        {
            ResetSingleState(normalStateCG, state == ProgressState.Normal);
            ResetSingleState(inProgressStateCG, state == ProgressState.InProgress);
            ResetSingleState(completeStateCG, state == ProgressState.Complete);
        }

        void ResetSingleState(CanvasGroup cg, bool isActive)
        {
            if (cg == null)
                return;

            cg.alpha = isActive ? 1f : 0f;
            cg.blocksRaycasts = isActive; // Ensure hidden states don't block input
            cg.transform.localScale = Vector3.one;

            if (originalAnchoredPositions.ContainsKey(cg) && cachedRectTransforms.ContainsKey(cg))
            {
                cachedRectTransforms[cg].anchoredPosition = originalAnchoredPositions[cg];
            }
        }

        void ResetStateTransform(CanvasGroup cg)
        {
            if (cg == null)
                return;

            cg.transform.localScale = Vector3.one;
            if (originalAnchoredPositions.ContainsKey(cg) && cachedRectTransforms.ContainsKey(cg))
            {
                cachedRectTransforms[cg].anchoredPosition = originalAnchoredPositions[cg];
            }
        }

        IEnumerator AnimateProgressState(ProgressState fromState, ProgressState toState)
        {
            CanvasGroup outgoing = GetCanvasGroupForState(fromState);
            CanvasGroup incoming = GetCanvasGroupForState(toState);

            RectTransform outgoingRect = GetRectForState(outgoing);
            RectTransform incomingRect = GetRectForState(incoming);

            float duration = Mathf.Max(0.01f, progressTransitionDuration);
            float outgoingTargetScale = 2f - scaleFrom;

            if (incoming != null)
            {
                incoming.alpha = 0f;
                incoming.blocksRaycasts = true; // Activate raycasts for incoming
                Vector2 incomingOriginalPos = originalAnchoredPositions.ContainsKey(incoming) ? originalAnchoredPositions[incoming] : Vector2.zero;

                if (animationType == AnimationType.Scale) { incoming.transform.localScale = Vector3.one * scaleFrom; }
                else if (animationType == AnimationType.Slide && incomingRect != null) 
                {
                    incomingRect.anchoredPosition = incomingOriginalPos - slideOffset; 
                }
            }

            // Pre-Setup outgoing
            if (outgoing != null)
            {
                outgoing.alpha = 1f;
                outgoing.blocksRaycasts = false; // Deactivate raycasts for outgoing
                ResetStateTransform(outgoing);
            }

            // Outgoing animation
            if (outgoing != null)
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    float curveValue = animationCurve.Evaluate(t);

                    Vector2 originalPos = originalAnchoredPositions.ContainsKey(outgoing) ? originalAnchoredPositions[outgoing] : Vector2.zero;

                    outgoing.alpha = 1f - t;

                    if (animationType == AnimationType.Scale)
                    {
                        outgoing.transform.localScale = Vector3.LerpUnclamped(Vector3.one, Vector3.one * outgoingTargetScale, curveValue);
                    }
                    else if (animationType == AnimationType.Slide && outgoingRect != null)
                    {
                        outgoingRect.anchoredPosition = Vector2.LerpUnclamped(originalPos, originalPos + slideOffset, curveValue);
                    }

                    yield return null;
                }
                outgoing.alpha = 0f;
            }

            // Incoming Animation
            if (incoming != null)
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    float curveValue = animationCurve.Evaluate(t);

                    Vector2 originalPos = originalAnchoredPositions.ContainsKey(incoming) ? originalAnchoredPositions[incoming] : Vector2.zero;

                    incoming.alpha = t;

                    if (animationType == AnimationType.Scale)
                    {
                        incoming.transform.localScale = Vector3.LerpUnclamped(Vector3.one * scaleFrom, Vector3.one, curveValue);
                    }
                    else if (animationType == AnimationType.Slide && incomingRect != null)
                    {
                        incomingRect.anchoredPosition = Vector2.LerpUnclamped(originalPos - slideOffset, originalPos, curveValue);
                    }

                    yield return null;
                }
            }

            // Finalize
            SetStateImmediate(toState);
        }

        void StopAllProgressCoroutines()
        {
            if (progressCoroutine != null)
            {
                StopCoroutine(progressCoroutine);
                progressCoroutine = null;
            }

            if (completeTimerCoroutine != null)
            {
                StopCoroutine(completeTimerCoroutine);
                completeTimerCoroutine = null;
            }
        }

        public void StartProgress()
        {
            if (currentProgressState == ProgressState.Complete && stayOnComplete)
                return;

            if (isHolding)
                return;

            isHolding = true;
            StopAllProgressCoroutines();

            SetProgressState(ProgressState.InProgress);
            onProgressStart?.Invoke();

            progressCoroutine = StartCoroutine(ProgressCoroutine());
        }

        public void CancelProgress()
        {
            if (!isHolding)
                return;

            isHolding = false;
            StopAllProgressCoroutines();

            onCancel?.Invoke();
            ResetProgress(false);
        }

        public void CompleteProgress()
        {
            isHolding = false;
            StopAllProgressCoroutines();

            SetProgressState(ProgressState.Complete);
            onComplete?.Invoke();

            if (!stayOnComplete)
            {
                completeTimerCoroutine = StartCoroutine(CompleteTimerCoroutine());
            }
        }

        public void ResetProgress(bool instant)
        {
            StopAllProgressCoroutines();

            if (instant || !Application.isPlaying || !gameObject.activeInHierarchy)
            {
                currentProgress = 0f;
                UpdateProgressFill(0f);
                SetProgressState(ProgressState.Normal, true);
            }
            else
            {
                progressCoroutine = StartCoroutine(ResetProgressCoroutine());
            }
        }

        public void SetProgress(float progress) 
        {
            currentProgress = Mathf.Clamp01(progress); UpdateProgressFill(currentProgress); 
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null) 
                    return;

                InitializeProgressFill();
                CacheStateInfo(); // Cache in editor for preview
                if (!Application.isPlaying) SetStateImmediate(currentProgressState);
            };
        }

        [HideInInspector] public bool progressFoldout = true;
#endif
    }
}