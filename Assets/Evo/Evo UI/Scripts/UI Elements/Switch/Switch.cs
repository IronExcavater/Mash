using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HELP_URL + "ui-elements/switch")]
    [AddComponentMenu("Evo/UI/UI Elements/Switch")]
    public class Switch : Interactive
    {
        [EvoHeader("Settings", Constants.CUSTOM_EDITOR_ID)]
        public bool isOn;
        public bool invokeAtStart;
        [Range(0, 3)] public float handleDuration = 0.2f;
        public AnimationCurve handleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [EvoHeader("References", Constants.CUSTOM_EDITOR_ID)]
        [SerializeField] private RectTransform switchHandle;
        [SerializeField] private CanvasGroup[] offCG;
        [SerializeField] private CanvasGroup[] onCG;

        [EvoHeader("Events", Constants.CUSTOM_EDITOR_ID)]
        public BooleanEvent onValueChanged = new();
        public UnityEvent onSwitchOn = new();
        public UnityEvent onSwitchOff = new();

        [System.Serializable] public class BooleanEvent : UnityEvent<bool> { }

        // Helpers
        float targetHandlePosition;
        Vector2 targetPivot;
        Vector2 initialHandlePosition;
        Coroutine currentStateAnimation;
        Coroutine currentSwitchAnimation;

        public bool IsOn
        {
            get { return isOn; }
            set { SetValue(value); }
        }

        protected override void Awake()
        {
            base.Awake();
            InitializeHandle();
        }

        protected override void Start()
        {
            base.Start();
            if (invokeAtStart)
            {
                onValueChanged?.Invoke(isOn);
                if (isOn) { onSwitchOn?.Invoke(); }
                else { onSwitchOff.Invoke(); }
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            UpdateStates();
            UpdateState(false);
        }

        public override void OnPointerClick(PointerEventData eventData)
        {
            base.OnPointerClick(eventData);
            if (IsInteractable()) { Toggle(); }
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);
            if (IsInteractable()) { UpdateStates(); }
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            if (IsInteractable()) { UpdateStates(); }
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
            if (IsInteractable()) { UpdateStates(); }
        }

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
            if (IsInteractable()) { UpdateStates(); }
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            base.OnDeselect(eventData);
            if (IsInteractable()) { UpdateStates(); }
        }

        public override void OnSubmit(BaseEventData eventData)
        {
            base.OnSubmit(eventData);
            if (IsInteractable()) { Toggle(); }
        }

        void InitializeHandle()
        {
            if (switchHandle != null)
            {
                initialHandlePosition = switchHandle.anchoredPosition;
                var (position, pivot) = GetPositionAndPivot();
                targetHandlePosition = position;
                targetPivot = pivot;
                switchHandle.pivot = targetPivot;
                switchHandle.anchoredPosition = new Vector2(targetHandlePosition, initialHandlePosition.y);
            }
        }

        void UpdateState(bool animate)
        {
            var (position, pivot) = GetPositionAndPivot();
            targetHandlePosition = position;
            targetPivot = pivot;

            if (animate && Application.isPlaying)
            {
                if (currentSwitchAnimation != null) { StopCoroutine(currentSwitchAnimation); }
                currentSwitchAnimation = StartCoroutine(AnimateHandle());
            }
            else if (switchHandle != null)
            {
                switchHandle.pivot = targetPivot;
                switchHandle.anchoredPosition = new Vector2(targetHandlePosition, switchHandle.anchoredPosition.y);
            }
        }

        void UpdateStates()
        {
            if (!gameObject.activeInHierarchy)
                return;

            if (currentStateAnimation != null) { StopCoroutine(currentStateAnimation); }
            currentStateAnimation = StartCoroutine(AnimateStates());
        }

        (float position, Vector2 pivot) GetPositionAndPivot()
        {
            if (switchHandle == null) { return (0f, Vector2.zero); }
            var parentRect = switchHandle.parent as RectTransform;
            if (parentRect == null) { return (0f, Vector2.zero); }

            if (isOn)
            {
                // On state: pivot to right edge, position to right edge
                var pivot = new Vector2(1f, switchHandle.pivot.y);
                var position = parentRect.rect.width * 0.5f;
                return (position, pivot);
            }

            else
            {
                // Off state: pivot to left edge, position to left edge
                var pivot = new Vector2(0f, switchHandle.pivot.y);
                var position = -parentRect.rect.width * 0.5f;
                return (position, pivot);
            }
        }

        IEnumerator AnimateHandle()
        {
            if (switchHandle == null)
                yield break;

            Vector2 startPos = switchHandle.anchoredPosition;
            Vector2 startPivot = switchHandle.pivot;
            Vector2 targetPos = new(targetHandlePosition, startPos.y);

            float elapsed = 0f;

            while (elapsed < handleDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = elapsed / handleDuration;
                if (handleCurve != null && handleCurve.keys.Length > 0) { progress = handleCurve.Evaluate(progress); }

                // Smoothly interpolate both pivot and position
                switchHandle.pivot = Vector2.Lerp(startPivot, targetPivot, progress);
                switchHandle.anchoredPosition = Vector2.Lerp(startPos, targetPos, progress);
                yield return null;
            }

            // Ensure final values are set exactly
            switchHandle.pivot = targetPivot;
            switchHandle.anchoredPosition = targetPos;
        }

        IEnumerator AnimateStates()
        {
            var targets = new Dictionary<CanvasGroup, float>();

            // Add all off canvas groups
            if (offCG != null)
            {
                foreach (var cg in offCG)
                {
                    if (cg != null)
                    {
                        targets[cg] = isOn ? 0 : 1;
                    }
                }
            }

            // Add all on canvas groups
            if (onCG != null)
            {
                foreach (var cg in onCG)
                {
                    if (cg != null)
                    {
                        targets[cg] = isOn ? 1 : 0;
                    }
                }
            }

            yield return Utilities.CrossFadeCanvasGroup(targets, Mathf.Max(0f, transitionDuration));
        }

        public void SetValue(bool value)
        {
            SetValue(value, true);
        }

        public void SetValue(bool value, bool sendCallback)
        {
            if (isOn == value)
                return;

            isOn = value;

            if (!gameObject.activeInHierarchy)
                return;

            UpdateState(true);
            UpdateStates();

            if (isOn) { AudioManager.PlayClip(Styler.GetAudio(sfxSource, selectedSFX, stylerPreset)); }
            if (sendCallback)
            {
                onValueChanged?.Invoke(isOn);
                if (isOn) { onSwitchOn?.Invoke(); }
                else { onSwitchOff?.Invoke(); }
            }
        }

        public void Toggle()
        {
            SetValue(!isOn);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            UpdateStatesValidate();
        }

        void UpdateStatesValidate()
        {
            if (!Application.isPlaying)
            {
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (this == null)
                        return;

                    // Set handle
                    if (switchHandle != null)
                    {
                        var (position, pivot) = GetPositionAndPivot();
                        targetHandlePosition = position;
                        targetPivot = pivot;

                        switchHandle.pivot = targetPivot;
                        switchHandle.anchoredPosition = new Vector2(targetHandlePosition, switchHandle.anchoredPosition.y);
                    }

                    // Calculate states directly
                    bool isDisabled = !interactable;

                    // Set on/off states for all off canvas groups
                    if (offCG != null)
                    {
                        foreach (var cg in offCG)
                        {
                            if (cg != null)
                            {
                                cg.alpha = !isOn ? 1 : 0;
                            }
                        }
                    }

                    // Set on/off states for all on canvas groups
                    if (onCG != null)
                    {
                        foreach (var cg in onCG)
                        {
                            if (cg != null)
                            {
                                cg.alpha = isOn ? 1 : 0;
                            }
                        }
                    }
                };
            }
        }
#endif
    }
}