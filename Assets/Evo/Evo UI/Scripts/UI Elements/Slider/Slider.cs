using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HELP_URL + "ui-elements/slider")]
    [AddComponentMenu("Evo/UI/UI Elements/Slider")]
    public class Slider : UnityEngine.UI.Slider
    {
        [EvoHeader("References", Constants.CUSTOM_EDITOR_ID)]
        public TMP_Text valueText;
        public TMP_InputField valueInput;
        [SerializeField] private CanvasGroup highlightedCG;

        [EvoHeader("Settings", Constants.CUSTOM_EDITOR_ID)]
        [SerializeField] private bool invokeAtStart;

        [EvoHeader("Formatting", Constants.CUSTOM_EDITOR_ID)]
        [Range(-1000, 1000)] public float displayMultiplier = 1;
        public DisplayFormat displayFormat = DisplayFormat.Fixed0;
        public string textFormat = "{0}";

        [EvoHeader("Animation", Constants.CUSTOM_EDITOR_ID)]
        [SerializeField] private AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField, Range(0, 2)] private float transitionDuration = 0.1f;
        [SerializeField, Range(0, 2)] private float highlightedScale = 1.25f;
        [SerializeField, Range(0, 2)] private float pressedScale = 1.15f;

        // State tracking
        bool isPointerOver;
        bool isPressed;
        Coroutine scaleCoroutine;
        Coroutine highlightCoroutine;
        Vector3 originalScale = Vector3.one;

        public enum DisplayFormat
        {
            // Fixed-point (F)
            [InspectorName("Fixed - No Decimals (0)")] Fixed0,
            [InspectorName("Fixed - 1 Decimal (0.0)")] Fixed1,
            [InspectorName("Fixed - 2 Decimals (0.00)")] Fixed2,
            [InspectorName("Fixed - 3 Decimals (0.000)")] Fixed3,
            [InspectorName("Fixed - 4 Decimals (0.0000)")] Fixed4,
            [InspectorName("Fixed - 5 Decimals (0.00000)")] Fixed5,

            // Number with thousands separator (N)
            [InspectorName("Number - No Decimals (1,234)")] Number0,
            [InspectorName("Number - 1 Decimal (1,234.5)")] Number1,
            [InspectorName("Number - 2 Decimals (1,234.56)")] Number2,
            [InspectorName("Number - 3 Decimals (1,234.567)")] Number3
        }

        protected override void Awake()
        {
            base.Awake();

            if (!Application.isPlaying) { return; }
            if (handleRect != null) { originalScale = handleRect.localScale; }

            onValueChanged.AddListener(SetText);
            Utilities.AddRaycastGraphic(gameObject);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (highlightedCG != null) { highlightedCG.alpha = 0; }
        }

        protected override void Start()
        {
            // Update text on start
            SetText(value);

            // Invoke on start if enabled
            if (valueInput != null) { valueInput.onEndEdit.AddListener(delegate { value = float.Parse(valueInput.text); }); }
            if (invokeAtStart) { onValueChanged?.Invoke(value); }
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
          
            if (!IsInteractable()) { return; }
            isPointerOver = true;

            UpdateAnimationState();
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
        
            if (!IsInteractable()) { return; }
            isPointerOver = false;

            UpdateAnimationState();
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
        
            if (!IsInteractable()) { return; }
            isPressed = true;

            UpdateAnimationState();
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);
        
            if (!IsInteractable()) { return; }
            isPressed = false;
         
            UpdateAnimationState();
        }

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
           
            if (!IsInteractable()) { return; }
            isPointerOver = true;

            UpdateAnimationState();
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            base.OnDeselect(eventData);
        
            if (!IsInteractable()) { return; }
            isPointerOver = false;

            UpdateAnimationState();
        }

        void UpdateAnimationState()
        {
            float targetScale = isPressed ? pressedScale : (isPointerOver ? highlightedScale : 1f);
            float targetAlpha = (isPressed || isPointerOver) ? 1f : 0f;

            AnimateHandleScale(targetScale);
            AnimateHighlight(targetAlpha);
        }

        void AnimateHandleScale(float targetScale)
        {
            if (handleRect == null) { return; }
            if (scaleCoroutine != null) { StopCoroutine(scaleCoroutine); }
            scaleCoroutine = StartCoroutine(ScaleHandleCoroutine(targetScale));
        }

        void AnimateHighlight(float targetAlpha)
        {
            if (highlightedCG == null) { return; }
            if (highlightCoroutine != null) { StopCoroutine(highlightCoroutine); }
            highlightCoroutine = StartCoroutine(Utilities.CrossFadeCanvasGroup(highlightedCG, targetAlpha, transitionDuration));
        }

        IEnumerator ScaleHandleCoroutine(float targetScale)
        {
            Vector3 startScale = handleRect.localScale;
            Vector3 endScale = originalScale * targetScale;
            float elapsed = 0f;

            while (elapsed < transitionDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = animationCurve.Evaluate(Mathf.Clamp01(elapsed / transitionDuration));

                handleRect.localScale = Vector3.LerpUnclamped(startScale, endScale, t);
                yield return null;
            }

            handleRect.localScale = endScale;
        }

        public void SetText(float val)
        {
            if (valueInput == null && valueText == null)
                return;

            string formattedText = FormatValue(val);

            if (valueText != null) { valueText.text = formattedText; }
            if (valueInput != null) { valueInput.text = formattedText; }
        }

        public string FormatValue(float sValue)
        {
            // Apply display multiplier
            float displayValue = sValue * displayMultiplier;

            // Get the format string
            string formatString = GetFormatString();

            // Format the number
            string formattedNumber;

            // If invalid format string, use default
            try { formattedNumber = displayValue.ToString(formatString); }
            catch { formattedNumber = displayValue.ToString("F2"); }

            // If textFormat is empty, use just the number
            if (string.IsNullOrEmpty(textFormat)) { return formattedNumber; }

            // Try to use string.Format, if it fails just append the number
            // If format fails, just append the number to the text
            try { return string.Format(textFormat, formattedNumber); }
            catch (System.FormatException) { return textFormat + " " + formattedNumber; }
        }

        string GetFormatString()
        {
            return displayFormat switch
            {
                // Fixed-point
                DisplayFormat.Fixed0 => "F0",
                DisplayFormat.Fixed1 => "F1",
                DisplayFormat.Fixed2 => "F2",
                DisplayFormat.Fixed3 => "F3",
                DisplayFormat.Fixed4 => "F4",
                DisplayFormat.Fixed5 => "F5",
                // Number with thousands
                DisplayFormat.Number0 => "N0",
                DisplayFormat.Number1 => "N1",
                DisplayFormat.Number2 => "N2",
                DisplayFormat.Number3 => "N3",
                // Default
                _ => "F0",
            };
        }

#if UNITY_EDITOR
        [HideInInspector] public bool objectFoldout = true;
        [HideInInspector] public bool settingsFoldout = true;
        [HideInInspector] public bool navigationFoldout = false;
        [HideInInspector] public bool referencesFoldout = false;
        [HideInInspector] public bool eventsFoldout = false;

        protected override void OnValidate()
        {
            base.OnValidate();

            // Update display in editor when values change
            if (valueText != null || valueInput != null) { SetText(value); }

            // Clamp multiplier to reasonable values
            displayMultiplier = Mathf.Clamp(displayMultiplier, -1000, 1000);
        }
#endif
    }
}