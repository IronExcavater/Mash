using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HELP_URL + "ui-elements/progress-bar")]
    [AddComponentMenu("Evo/UI/UI Elements/Progress Bar")]
    public class ProgressBar : MonoBehaviour
    {
        [EvoHeader("Bar Settings", Constants.CUSTOM_EDITOR_ID)]
        [SerializeField] private float value = 50;
        [SerializeField] private float minValue = 0;
        [SerializeField] private float maxValue = 100;

        [EvoHeader("Settings", Constants.CUSTOM_EDITOR_ID)]
        [SerializeField] private bool invokeAtStart;
        [SerializeField] private bool isVertical;
        [Range(-1000, 1000)] public float displayMultiplier = 1;
        public DisplayFormat displayFormat = DisplayFormat.Fixed0;
        public string textFormat = "{0}";

        [EvoHeader("Animation", Constants.CUSTOM_EDITOR_ID)]
        public bool enableSmoothing = true;
        [Range(0.05f, 4)] public float smoothingDuration = 0.3f;
        [SerializeField] private AnimationCurve smoothingCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [EvoHeader("References", Constants.CUSTOM_EDITOR_ID)]
        public RectTransform fillRect;
        public TMP_Text valueText;

        [EvoHeader("Events", Constants.CUSTOM_EDITOR_ID)]
        public ProgressBarEvent onValueChanged = new();

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

        // Helpers
        bool isAnimating;
        float previousValue;
        float animationStartValue;
        float animationTargetValue;
        Coroutine animationCoroutine;

        // Cache the Image component for fill mode detection
        Image fillImage;
        bool useImageFill;

        [System.Serializable] public class ProgressBarEvent : UnityEvent<float> { }

        // Properties
        public float Value
        {
            get { return value; }
            set { SetValue(value); }
        }

        public float MinValue
        {
            get { return minValue; }
            set
            {
                minValue = value;
                UpdateDisplay();
            }
        }

        public float MaxValue
        {
            get { return maxValue; }
            set
            {
                maxValue = value;
                UpdateDisplay();
            }
        }

        void Awake()
        {
            previousValue = value;
            CacheFillComponents();
        }

        void Start()
        {
            // Update display on start
            UpdateDisplay();

            // Invoke on start if enabled
            if (invokeAtStart) { onValueChanged?.Invoke(value); }
        }

        void OnEnable()
        {
            UpdateDisplay();
        }

        void OnDisable()
        {
            if (animationCoroutine != null)
            {
                StopCoroutine(animationCoroutine);
                animationCoroutine = null;
            }
            isAnimating = false;
        }

        void CacheFillComponents()
        {
            if (fillRect == null || fillImage != null)
                return;

            fillImage = fillRect.GetComponent<Image>();
            useImageFill = fillImage != null && fillImage.type == Image.Type.Filled;
        }

        void SetValue(float newValue)
        {
            // Clamp the value
            float clampedValue = Mathf.Clamp(newValue, minValue, maxValue);

            if (value != clampedValue)
            {
                previousValue = value; // Store the old value
                value = clampedValue;

                if (enableSmoothing && gameObject.activeInHierarchy) { AnimateToValue(value); }
                else
                {
                    isAnimating = false;
                    UpdateDisplay();
                }

                onValueChanged?.Invoke(value);
            }
        }

        void AnimateToValue(float targetValue)
        {
            animationStartValue = previousValue;
            animationTargetValue = targetValue;
            isAnimating = true;
           
            if (animationCoroutine != null)
            { 
                StopCoroutine(animationCoroutine); 
                animationCoroutine = null;
            }
            animationCoroutine = StartCoroutine(AnimateValueCoroutine());
        }

        void UpdateDisplay()
        {
            CacheFillComponents();
            UpdateFillRect();
            UpdateText();
        }

        void UpdateFillRect()
        {
            if (fillRect == null)
                return;

            float displayValue = isAnimating ? GetCurrentAnimatedValue() : value;
            float normalizedDisplay = 0f;
            if (!Mathf.Approximately(MinValue, MaxValue)) { normalizedDisplay = Mathf.InverseLerp(MinValue, MaxValue, displayValue); }

            // Clamp to 0-1 range
            normalizedDisplay = Mathf.Clamp01(normalizedDisplay);

            // Use Image.fillAmount for filled images
            if (useImageFill && fillImage != null) { fillImage.fillAmount = normalizedDisplay; }
            else if (!useImageFill)
            {
                if (isVertical) { fillRect.anchorMax = new Vector2(fillRect.anchorMax.x, normalizedDisplay); }
                else { fillRect.anchorMax = new Vector2(normalizedDisplay, fillRect.anchorMax.y); }
            }
        }

        void UpdateText()
        {
            if (valueText == null)
                return;

            float displayValue = isAnimating ? GetCurrentAnimatedValue() : value;
            string formattedText = FormatValue(displayValue);
            valueText.text = formattedText;
        }

        float GetCurrentAnimatedValue()
        {
            if (!isAnimating) { return value; }
            if (!Mathf.Approximately(MinValue, MaxValue))
            {
                // For Image Fill mode, return value based on fillAmount
                if (useImageFill && fillImage != null) { return Mathf.Lerp(MinValue, MaxValue, fillImage.fillAmount); }
                else if (fillRect != null)
                {
                    // For RectTransform mode, return value based on anchorMax
                    float normalizedDisplay = isVertical ? fillRect.anchorMax.y : fillRect.anchorMax.x;
                    return Mathf.Lerp(MinValue, MaxValue, normalizedDisplay);
                }
            }
            return value;
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

        string FormatValue(float sValue)
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

            // Force {0} placeholder if missing
            string finalTextFormat = textFormat;
            if (!finalTextFormat.Contains("{0}")) { finalTextFormat += " {0}"; }

            // Try to use string.Format with the corrected format
            try { return string.Format(finalTextFormat, formattedNumber); }
            catch (System.FormatException) { return textFormat + " " + formattedNumber; }
        }

        public void SetValueWithoutNotify(float newValue)
        {
            float clampedValue = Mathf.Clamp(newValue, minValue, maxValue);
            value = clampedValue;

            if (enableSmoothing) { AnimateToValue(value); }
            else
            {
                isAnimating = false;
                UpdateDisplay();
            }
        }

        public void SetValueInstant(float newValue)
        {
            float clampedValue = Mathf.Clamp(newValue, minValue, maxValue);
            value = clampedValue;

            if (animationCoroutine != null)
            {
                StopCoroutine(animationCoroutine);
                animationCoroutine = null;
            }

            isAnimating = false;
            UpdateDisplay();
            onValueChanged?.Invoke(value);
        }

        IEnumerator AnimateValueCoroutine()
        {
            float elapsedTime = 0f;

            while (elapsedTime < smoothingDuration)
            {
                elapsedTime += Time.unscaledDeltaTime;
                float normalizedTime = elapsedTime / smoothingDuration;
                float curveValue = smoothingCurve.Evaluate(normalizedTime);

                float currentAnimatedValue = Mathf.Lerp(animationStartValue, animationTargetValue, curveValue);
                float normalizedDisplay = 0f;
                if (!Mathf.Approximately(MinValue, MaxValue)) { normalizedDisplay = Mathf.InverseLerp(MinValue, MaxValue, currentAnimatedValue); }
                normalizedDisplay = Mathf.Clamp01(normalizedDisplay);

                // Update fill rect
                if (fillRect != null)
                {
                    if (useImageFill && fillImage != null) { fillImage.fillAmount = normalizedDisplay; }
                    else if (!useImageFill)
                    {
                        if (isVertical) { fillRect.anchorMax = new Vector2(fillRect.anchorMax.x, normalizedDisplay); }
                        else { fillRect.anchorMax = new Vector2(normalizedDisplay, fillRect.anchorMax.y); }
                    }
                }

                // Update text
                if (valueText != null)
                {
                    string formattedText = FormatValue(currentAnimatedValue);
                    valueText.text = formattedText;
                }

                yield return null;
            }

            isAnimating = false;
            animationCoroutine = null;
            UpdateDisplay();
        }

#if UNITY_EDITOR
        [HideInInspector] public bool objectFoldout = true;
        [HideInInspector] public bool settingsFoldout = true;
        [HideInInspector] public bool referencesFoldout = false;
        [HideInInspector] public bool eventsFoldout = false;

        void OnValidate()
        {
            if (Application.isPlaying)
                return;

            // Ensure min <= max
            if (minValue > maxValue) { minValue = maxValue; }

            // Clamp value to valid range
            value = Mathf.Clamp(value, minValue, maxValue);

            // Clamp multiplier to reasonable values
            displayMultiplier = Mathf.Clamp(displayMultiplier, -1000, 1000);

            // Clamp duration to reasonable values
            smoothingDuration = Mathf.Max(smoothingDuration, 0.01f);

            // Update display in editor when values change
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null)
                {
                    UpdateDisplay();
                }
            };
        }
#endif
    }
}