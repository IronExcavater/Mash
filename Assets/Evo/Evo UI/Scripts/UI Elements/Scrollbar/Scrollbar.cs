using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HELP_URL + "ui-elements/scrollbar")]
    [AddComponentMenu("Evo/UI/UI Elements/Scrollbar")]
    public class Scrollbar : UnityEngine.UI.Scrollbar, IPointerEnterHandler, IPointerExitHandler
    {
        [EvoHeader("Animation", Constants.CUSTOM_EDITOR_ID)]
        [Range(0.1f, 3f)] public float smoothDuration = 0.5f;
        [SerializeField] private AnimationCurve smoothCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [EvoHeader("Auto Hide", Constants.CUSTOM_EDITOR_ID)]
        [SerializeField] private bool autoHide = false;
        [SerializeField, Range(0, 1)] private float hideAlpha = 0;
        [SerializeField, Range(0.05f, 2)] private float hideDuration = 0.2f;
        [SerializeField, Range(0.1f, 10)] private float hideTimer = 1;

        [EvoHeader("References", Constants.CUSTOM_EDITOR_ID)]
        [SerializeField] private Button minArrow;
        [SerializeField] private Button maxArrow;

        // Helpers
        bool isHovered;
        bool isPointerDown;
        CanvasGroup canvasGroup;
        Coroutine scrollCoroutine;
        Coroutine fadeCoroutine;
        Coroutine autoHideCoroutine;

        protected override void Awake()
        {
            base.Awake();

            if (!Application.isPlaying)
                return;

            if (autoHide)
            {
                if (TryGetComponent<CanvasGroup>(out var cg)) { canvasGroup = cg; }
                else { canvasGroup = gameObject.AddComponent<CanvasGroup>(); }

                onValueChanged.AddListener(delegate { FadeTo(false); });
            }

            if (minArrow != null && maxArrow != null)
            {
                HandleArrows(value);
                minArrow.onClick.AddListener(ScrollToMin);
                maxArrow.onClick.AddListener(ScrollToMax);
                onValueChanged.AddListener(HandleArrows);
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            if (Application.isPlaying && autoHide && canvasGroup != null) 
            { 
                canvasGroup.alpha = hideAlpha; 
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            if (scrollCoroutine != null)
            {
                StopCoroutine(scrollCoroutine);
                scrollCoroutine = null;
            }
            if (autoHideCoroutine != null)
            {
                StopCoroutine(autoHideCoroutine);
                autoHideCoroutine = null;
            }
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
                fadeCoroutine = null;
            }
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            if (scrollCoroutine != null)
            {
                StopCoroutine(scrollCoroutine);
                scrollCoroutine = null;
            }

            base.OnPointerDown(eventData);          
            isPointerDown = true;
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);
            isPointerDown = false;
            if (autoHide && !isHovered) { FadeTo(false); }
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
           
            isHovered = true;
            if (autoHide) { FadeTo(false); }
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);

            isHovered = false;
            if (autoHide && !isPointerDown) { FadeTo(true); }
        }

        void HandleArrows(float value)
        {
            if (direction == Direction.BottomToTop || direction == Direction.LeftToRight)
            {
                minArrow.SetInteractable(value >= 0.01f);
                maxArrow.SetInteractable(value <= 0.99f);
            }
            else
            {
                minArrow.SetInteractable(value <= 0.99f);
                maxArrow.SetInteractable(value >= 0.01f);
            }
        }

        void FadeTo(bool isFadeOut)
        {
            if (!interactable || !gameObject.activeInHierarchy) { return; }
            if (autoHideCoroutine != null)
            {
                StopCoroutine(autoHideCoroutine);
                autoHideCoroutine = null;
            }
            autoHideCoroutine = StartCoroutine(AutoHideCoroutine(isFadeOut));
        }

        IEnumerator ScrollCoroutine(float targetPosition, float duration)
        {
            float startValue = value;
            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.unscaledDeltaTime;
                float t = elapsedTime / duration;
                float curveValue = smoothCurve.Evaluate(t);

                value = Mathf.Lerp(startValue, targetPosition, curveValue);

                yield return null;
            }

            value = targetPosition;
        }

        IEnumerator AutoHideCoroutine(bool isFadeOut)
        {
            yield return new WaitForSecondsRealtime(isFadeOut ? hideTimer : 0);
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
                fadeCoroutine = null;
            }
            fadeCoroutine = StartCoroutine(Utilities.CrossFadeCanvasGroup(canvasGroup, isFadeOut ? hideAlpha : 1, hideDuration));
            if (!isFadeOut && !isHovered) { FadeTo(true); }
        }

        /// <summary>
        /// Smoothly scroll to the min position.
        /// </summary>
        public void ScrollToMin()
        {
            float target = (direction == Direction.BottomToTop || direction == Direction.LeftToRight) ? 0 : 1;
            ScrollTo(target, false, smoothDuration);
        }

        /// <summary>
        /// Smoothly scroll to the max position.
        /// </summary>
        public void ScrollToMax()
        {
            float target = (direction == Direction.BottomToTop || direction == Direction.LeftToRight) ? 1 : 0;
            ScrollTo(target, false, smoothDuration);
        }


        /// <summary>
        /// Smoothly scroll to a specific position (0 = bottom, 1 = top for vertical scrollbar).
        /// </summary>
        public void ScrollTo(float targetValue)
        {
            ScrollTo(targetValue, false, smoothDuration);
        }

        /// <summary>
        /// Smoothly scroll to a specific position with custom duration.
        /// </summary>
        public void ScrollTo(float targetValue, bool clamp, float duration)
        {
            targetValue = clamp ? Mathf.Clamp01(targetValue) : targetValue;

            if (scrollCoroutine != null) { StopCoroutine(scrollCoroutine); }
            scrollCoroutine = StartCoroutine(ScrollCoroutine(targetValue, duration));
        }

        /// <summary>
        /// Smoothly scroll by a relative amount.
        /// </summary>
        public void ScrollBy(float amount)
        {
            ScrollTo(value + amount);
        }

        /// <summary>
        /// Smoothly scroll by a relative amount with custom duration.
        /// </summary>
        public void ScrollBy(float amount, bool clamp, float duration)
        {
            ScrollTo(value + amount, clamp, duration);
        }

#if UNITY_EDITOR
        [HideInInspector] public bool settingsFoldout = true;
        [HideInInspector] public bool styleFoldout = false;
        [HideInInspector] public bool navigationFoldout = false;
        [HideInInspector] public bool referencesFoldout = false;
        [HideInInspector] public bool eventsFoldout = false;
#endif
    }
}