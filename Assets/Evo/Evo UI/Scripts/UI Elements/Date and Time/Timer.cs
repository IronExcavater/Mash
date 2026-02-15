using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HELP_URL + "ui-elements/timer")]
    [AddComponentMenu("Evo/UI/UI Elements/Timer")]
    public class Timer : MonoBehaviour
    {
        [EvoHeader("Timer", Constants.CUSTOM_EDITOR_ID)]
        public float currentTime = 30;
        public float duration = 60;
        public float timeMultiplier = 1;
        public string textFormat = "{0}";
        public DisplayFormat displayFormat = DisplayFormat.Time_MM_SS;

        [EvoHeader("Settings", Constants.CUSTOM_EDITOR_ID)]
        [SerializeField] private bool autoStart = false;
        public bool countDown = true;
        public bool loop = false;
        [SerializeField] private bool isVertical;

        [EvoHeader("Animation", Constants.CUSTOM_EDITOR_ID)]
        public bool enableSmoothing = true;
        public bool updateBarOnSecondsOnly = false;
        public float smoothingDuration = 0.25f;

        [EvoHeader("References", Constants.CUSTOM_EDITOR_ID)]
        public RectTransform fillRect;
        public TMP_Text valueText;

        [EvoHeader("Events", Constants.CUSTOM_EDITOR_ID)]
        public TimerEvent onValueChanged = new();
        public UnityEvent onTimerComplete = new();
        public UnityEvent onTimerStart = new();
        public UnityEvent onTimerStop = new();
        public UnityEvent onTimerReset = new();

        public enum DisplayFormat
        {
            [InspectorName("Fixed - No Decimals (0)")] Fixed0,
            [InspectorName("Fixed - 1 Decimal (0.0)")] Fixed1,
            [InspectorName("Fixed - 2 Decimals (0.00)")] Fixed2,
            [InspectorName("Fixed - 3 Decimals (0.000)")] Fixed3,
            [InspectorName("Fixed - 4 Decimals (0.0000)")] Fixed4,
            [InspectorName("Fixed - 5 Decimals (0.00000)")] Fixed5,
            [InspectorName("Time - S")] Time_S,
            [InspectorName("Time - MM:SS")] Time_MM_SS,
            [InspectorName("Time - HH:MM:SS")] Time_HH_MM_SS,
            [InspectorName("Time - M:SS")] Time_M_SS,
            [InspectorName("Time - H:MM:SS")] Time_H_MM_SS
        }

        // Timer state
        [HideInInspector] public bool isRunning;
        [HideInInspector] public bool isCompleted;

        // Cache
        Image fillImage;
        float lastSecondUpdate;
        float cachedDisplayValue;
        string cachedFormattedText;
        bool displayValueDirty = true;

        // Animation helpers
        bool isAnimating;
        float animationStartValue;
        float animationTargetValue;
        float animationStartTime;

        [System.Serializable] public class TimerEvent : UnityEvent<float> { }

        public float DisplayValue
        {
            get
            {
                if (displayValueDirty)
                {
                    cachedDisplayValue = countDown ? currentTime : duration - currentTime;
                    displayValueDirty = false;
                }
                return cachedDisplayValue;
            }
        }

        void OnEnable()
        {
            MarkDisplayValueDirty();
            UpdateDisplay();
        }

        void Start()
        {
            MarkDisplayValueDirty();
            UpdateDisplay();
            if (autoStart) { StartTimer(); }
        }

        void Update()
        {
            if (isAnimating) { UpdateAnimation(); }
            if (!isRunning || isCompleted) { return; }

            // Store previous time for change detection
            float previousTime = currentTime;

            // Update timer
            float deltaTime = Time.unscaledDeltaTime * timeMultiplier;
            if (countDown) { currentTime -= deltaTime; }
            else { currentTime += deltaTime; }

            // Clamp to valid range
            currentTime = Mathf.Clamp(currentTime, 0f, duration);

            // Check if time actually changed
            if (Mathf.Approximately(currentTime, previousTime))
                return;

            MarkDisplayValueDirty();

            // Check completion
            bool shouldComplete = countDown ? currentTime <= 0f : currentTime >= duration;
            if (shouldComplete) { CompleteTimer(); }
            else { HandleRegularUpdate(); }

            // Fire value changed event
            onValueChanged?.Invoke(DisplayValue);
        }

        void CompleteTimer()
        {
            // Ensure exact final value
            currentTime = countDown ? 0f : duration;
            MarkDisplayValueDirty();
            isCompleted = true;
            isRunning = false;

            // Update display immediately
            UpdateDisplay();
            onTimerComplete?.Invoke();

            // Handle looping
            if (loop)
            {
                ResetTimer();
                StartTimer();
            }
        }

        void HandleRegularUpdate()
        {
            if (!updateBarOnSecondsOnly) { UpdateVisuals(); }
            else
            {
                float currentSecond = Mathf.Floor(DisplayValue);

                if (!Mathf.Approximately(currentSecond, lastSecondUpdate))
                { 
                    lastSecondUpdate = currentSecond;
                    UpdateVisuals(); 
                }
                else
                {
                    if (enableSmoothing && isAnimating) { UpdateText(GetCurrentAnimatedValue()); }
                    else { UpdateText(Mathf.Floor(DisplayValue)); }
                }
            }
        }

        void UpdateVisuals()
        {
            if (enableSmoothing) { StartAnimationIfNeeded(); }
            else { UpdateDisplay(); }
        }

        void StartAnimationIfNeeded()
        {
            float targetValue = updateBarOnSecondsOnly ? Mathf.Floor(DisplayValue) : DisplayValue;
            if (!isAnimating || !Mathf.Approximately(animationTargetValue, targetValue))
            {
                animationStartValue = isAnimating ? GetCurrentAnimatedValue() : GetCurrentBarValue();
                animationTargetValue = targetValue;
                animationStartTime = Time.unscaledTime;
                isAnimating = true;
            }
        }

        void UpdateAnimation()
        {
            float elapsed = Time.unscaledTime - animationStartTime;
            float t = Mathf.Clamp01(elapsed / smoothingDuration);
            t = 1f - (1f - t) * (1f - t); // Ease out

            float currentAnimatedValue = Mathf.Lerp(animationStartValue, animationTargetValue, t);

            UpdateFillRect(currentAnimatedValue);
            UpdateText(currentAnimatedValue);

            if (t >= 1f) { isAnimating = false; }
        }

        void UpdateDisplay()
        {
            if (fillRect != null && fillImage == null) { fillImage = fillRect.GetComponent<Image>(); }
            UpdateFillRect(DisplayValue);
            UpdateText(DisplayValue);
        }

        void UpdateFillRect(float displayValue)
        {
            float normalizedProgress = 0f;
            if (duration > 0f) { normalizedProgress = countDown ? displayValue / duration : 1f - (displayValue / duration); }
            normalizedProgress = Mathf.Clamp01(normalizedProgress);

            // Snap to exact values for precision
            if (normalizedProgress < 0.001f) normalizedProgress = 0f;
            if (normalizedProgress > 0.999f) normalizedProgress = 1f;

            if (fillImage != null && fillImage.type == Image.Type.Filled) { fillImage.fillAmount = normalizedProgress; }
            else if (fillRect != null)
            {
                if (isVertical) { fillRect.anchorMax = new Vector2(fillRect.anchorMax.x, normalizedProgress); }
                else { fillRect.anchorMax = new Vector2(normalizedProgress, fillRect.anchorMax.y); }
            }
        }

        void UpdateText(float displayValue)
        {
            if (valueText == null)
                return;

            string formattedText = FormatValue(displayValue);

            // Only update if text actually changed
            if (cachedFormattedText != formattedText)
            {
                cachedFormattedText = formattedText;
                valueText.text = formattedText;
            }
        }

        void MarkDisplayValueDirty()
        {
            displayValueDirty = true;
            cachedFormattedText = null; // Reset cached text as well
        }

        float GetCurrentBarValue()
        {
            if (fillImage != null && fillImage.type == Image.Type.Filled)
            {
                float progress = fillImage.fillAmount;
                return countDown ? progress * duration : duration - (progress * duration);
            }
            else if (fillRect != null)
            {
                float progress = isVertical ? fillRect.anchorMax.y: fillRect.anchorMax.x;
                return countDown ? progress * duration : duration - (progress * duration);
            }

            return DisplayValue;
        }

        float GetCurrentAnimatedValue()
        {
            if (!isAnimating) { return DisplayValue; }
            float elapsed = Time.unscaledTime - animationStartTime;
            float t = Mathf.Clamp01(elapsed / smoothingDuration);
            t = 1f - (1f - t) * (1f - t);
            return Mathf.Lerp(animationStartValue, animationTargetValue, t);
        }

        string GetFormatString()
        {
            return displayFormat switch
            {
                DisplayFormat.Fixed0 => "F0",
                DisplayFormat.Fixed1 => "F1",
                DisplayFormat.Fixed2 => "F2",
                DisplayFormat.Fixed3 => "F3",
                DisplayFormat.Fixed4 => "F4",
                DisplayFormat.Fixed5 => "F5",
                DisplayFormat.Time_S or
                DisplayFormat.Time_MM_SS or
                DisplayFormat.Time_HH_MM_SS or
                DisplayFormat.Time_M_SS or
                DisplayFormat.Time_H_MM_SS => "TIME",
                _ => "F0",
            };
        }

        string FormatValue(float sValue)
        {
            string formatString = GetFormatString();
            string formattedNumber;

            if (formatString == "TIME") { formattedNumber = FormatTimeValue(sValue); }
            else
            {
                try { formattedNumber = sValue.ToString(formatString); }
                catch { formattedNumber = sValue.ToString("F2"); }
            }

            if (string.IsNullOrEmpty(textFormat)) return formattedNumber;
            string finalTextFormat = textFormat.Contains("{0}") ? textFormat : textFormat + " {0}";

            try { return string.Format(finalTextFormat, formattedNumber); }
            catch (System.FormatException) { return textFormat + " " + formattedNumber; }
        }

        string FormatTimeValue(float timeValue)
        {
            int totalSeconds = Mathf.FloorToInt(Mathf.Abs(timeValue));
            int hours = totalSeconds / 3600;
            int minutes = (totalSeconds % 3600) / 60;
            int seconds = totalSeconds % 60;

            return displayFormat switch
            {
                DisplayFormat.Time_S => $"{seconds:D1}",
                DisplayFormat.Time_MM_SS => $"{minutes:D2}:{seconds:D2}",
                DisplayFormat.Time_HH_MM_SS => $"{hours:D2}:{minutes:D2}:{seconds:D2}",
                DisplayFormat.Time_M_SS => $"{minutes}:{seconds:D2}",
                DisplayFormat.Time_H_MM_SS => $"{hours}:{minutes:D2}:{seconds:D2}",
                _ => $"{minutes:D2}:{seconds:D2}"
            };
        }

        public void StartTimer()
        {
            if (isCompleted && !loop) { ResetTimer(); }
            isRunning = true;
            isCompleted = false;
            onTimerStart?.Invoke();
        }

        public void StopTimer()
        {
            isRunning = false;
            onTimerStop?.Invoke();
        }

        public void PauseTimer() => isRunning = false;

        public void ResumeTimer()
        {
            if (!isCompleted) { isRunning = true; }
        }

        public void ResetTimer()
        {
            isRunning = false;
            isCompleted = false;
            currentTime = countDown ? duration : 0f;
            MarkDisplayValueDirty();
            lastSecondUpdate = Mathf.Floor(DisplayValue);

            if (enableSmoothing) { StartAnimationIfNeeded(); }
            else { UpdateDisplay(); }

            onTimerReset?.Invoke();
        }

        public void SetCurrentTime(float newTime)
        {
            float clampedTime = Mathf.Clamp(newTime, 0f, duration);

            if (!Mathf.Approximately(currentTime, clampedTime))
            {
                currentTime = clampedTime;
                MarkDisplayValueDirty();
                lastSecondUpdate = Mathf.Floor(DisplayValue);

                if (enableSmoothing) { StartAnimationIfNeeded(); }
                else { UpdateDisplay(); }

                onValueChanged?.Invoke(DisplayValue);
            }
        }

        public void SetCurrentTimeWithoutNotify(float newTime)
        {
            float clampedTime = Mathf.Clamp(newTime, 0f, duration);
            currentTime = clampedTime;
            MarkDisplayValueDirty();
            lastSecondUpdate = Mathf.Floor(DisplayValue);

            if (enableSmoothing) { StartAnimationIfNeeded(); }
            else
            {
                isAnimating = false;
                UpdateDisplay();
            }
        }

        public void AddTime(float timeToAdd) => SetCurrentTime(currentTime + timeToAdd);
        public void SubtractTime(float timeToSubtract) => SetCurrentTime(currentTime - timeToSubtract);

        public float GetRemainingTime() => countDown ? currentTime : duration - currentTime;
        public float GetElapsedTime() => countDown ? duration - currentTime : currentTime;

#if UNITY_EDITOR
        [HideInInspector] public bool objectFoldout = true;
        [HideInInspector] public bool settingsFoldout = true;
        [HideInInspector] public bool referencesFoldout = false;
        [HideInInspector] public bool eventsFoldout = false;

        void OnValidate()
        {
            if (Application.isPlaying)
                return;

            duration = Mathf.Max(0f, duration);
            currentTime = Mathf.Clamp(currentTime, 0f, duration);
            timeMultiplier = Mathf.Max(0f, timeMultiplier);

            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null)
                {
                    MarkDisplayValueDirty();
                    UnityEditor.EditorApplication.delayCall += UpdateDisplay;
                }
            };
        }
#endif
    }
}