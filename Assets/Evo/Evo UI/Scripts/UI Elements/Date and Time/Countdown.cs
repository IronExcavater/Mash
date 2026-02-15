using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HELP_URL + "ui-elements/countdown")]
    [AddComponentMenu("Evo/UI/UI Elements/Countdown")]
    [RequireComponent(typeof(RectTransform))]
    public class Countdown : MonoBehaviour, IStylerHandler
    {
        [EvoHeader("Timer", Constants.CUSTOM_EDITOR_ID)]
        [Range(0, 23)] public int hours = 2;
        [Range(0, 59)] public int minutes = 30;
        [Range(0, 59)] public int seconds = 15;
        public string separator = ":";

        [EvoHeader("Settings", Constants.CUSTOM_EDITOR_ID)]
        public bool autoStart = true;
        public bool showHours = true;
        public bool showMinutes = true;
        public bool showSeconds = true;
        public bool useUnscaledTime = true;

        [EvoHeader("Spacing", Constants.CUSTOM_EDITOR_ID)]
        public bool useDynamicSpacing = true;
        public float separatorSpacing = 15;
        public float digitSpacing = 2;

        [EvoHeader("Animation", Constants.CUSTOM_EDITOR_ID)]
        [Range(0, 0.99f)] public float animationDuration = 0.3f;
        public float slideDistance = 40;
        public AnimationCurve animationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [EvoHeader("Styling", Constants.CUSTOM_EDITOR_ID)]
        public StylingSource stylingSource = StylingSource.Custom;
        public StylerPreset stylerPreset;
        public ColorMapping fontColor = new() { stylerID = "Primary" };
        public FontMapping timerFont = new() { stylerID = "Regular" };
        public static string[] GetColorFields() => new[] { "fontColor" };
        public static string[] GetFontFields() => new[] { "timerFont" };

        [EvoHeader("Font Settings", Constants.CUSTOM_EDITOR_ID)]
        public float fontSize = 38;
        public FontStyles fontStyle = FontStyles.Normal;

        [EvoHeader("Events", Constants.CUSTOM_EDITOR_ID)]
        public UnityEvent onTimerComplete = new();
        public UnityEvent<int, int, int> onTimeChanged = new();

        // Cache
        RectTransform rectTransform;
        readonly List<DigitDisplay> allDigits = new();
        readonly List<TextMeshProUGUI> separators = new();
        readonly Dictionary<int, Coroutine> activeAnimations = new();

        // Helpers
        bool isRunning;
        double currentTime;
        int lastDisplayedHours = -1, lastDisplayedMinutes = -1, lastDisplayedSeconds = -1;
        bool pendingRedraw;

        [System.Serializable]
        public class DigitDisplay
        {
            public GameObject container;
            public TextMeshProUGUI mainText;
            public TextMeshProUGUI animText;
            public string currentValue = "0";
            public bool isAnimating = false;
        }

        // Styler Interface
        public StylerPreset Preset
        {
            get => stylerPreset;
            set
            {
                if (stylerPreset == value) { return; }
                stylerPreset = value;
                UpdateStyler();
            }
        }

        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            // Add 0.999 seconds buffer so it displays the full second before counting down
            currentTime = (double)(hours * 3600 + minutes * 60 + seconds) + 0.999;

            CreateTimerDisplay();
            UpdateDisplayImmediate();
        }

        void OnEnable()
        {
            if (autoStart)
            {
                StartTimer();
                CreateTimerDisplay();
                UpdateDisplayImmediate();
            }
        }

        void OnDisable()
        {
            if (isRunning)
            {
                PauseTimer();
            }
        }

        void OnRectTransformDimensionsChange()
        {
            if (gameObject.activeInHierarchy && !Application.isPlaying)
            {
                RequestRedraw();
            }
        }

        void Update()
        {
            if (!isRunning)
                return;

            // Store previous values to detect changes
            int prevHours = hours;
            int prevMinutes = minutes;
            int prevSeconds = seconds;

            currentTime -= useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

            if (currentTime <= 0.0)
            {
                currentTime = 0.0;
                hours = minutes = seconds = 0;
                isRunning = false;
                onTimerComplete?.Invoke();
                UpdateDisplay();
                return;
            }

            int totalSeconds = (int)currentTime;
            hours = totalSeconds / 3600;
            minutes = (totalSeconds % 3600) / 60;
            seconds = totalSeconds % 60;
            if (hours != prevHours || minutes != prevMinutes || seconds != prevSeconds) { UpdateDisplay(); }
        }

        void RequestRedraw()
        {
#if UNITY_EDITOR
            if (pendingRedraw)
                return;

            pendingRedraw = true;

            UnityEditor.EditorApplication.delayCall += () =>
            {
                pendingRedraw = false;
                if (this != null)
                {
                    currentTime = (double)(hours * 3600 + minutes * 60 + seconds);
                    CreateTimerDisplay();
                    UpdateDisplayImmediate();
                }
            };
#else
            CreateTimerDisplay();
            UpdateDisplayImmediate();
#endif
        }

        void CreateTimerDisplay()
        {
            rectTransform = rectTransform != null ? rectTransform : GetComponent<RectTransform>();
            ClearDisplay();

            Vector2 bounds = rectTransform.rect.size;
            int digitCount = GetDigitCount();
            int separatorCount = GetSeparatorCount();

            if (useDynamicSpacing) { CreateDynamicLayout(bounds, digitCount, separatorCount); }
            else { CreateFixedLayout(bounds, digitCount, separatorCount); }
        }

        void ClearDisplay()
        {
            foreach (var anim in activeAnimations.Values) { if (anim != null) StopCoroutine(anim); }
            activeAnimations.Clear();

            int childCount = transform.childCount;
            for (int i = childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child != null)
                {
#if UNITY_EDITOR
                    if (Application.isPlaying) { Destroy(child.gameObject); }
                    else { DestroyImmediate(child.gameObject); }
#else
                    Destroy(child.gameObject);
#endif
                }
            }

            allDigits.Clear();
            separators.Clear();
        }

        void UpdateDisplay()
        {
            int digitIndex = 0;

            if (showHours)
            {
                CheckAndAnimateDigit(digitIndex++, (hours / 10).ToString());
                CheckAndAnimateDigit(digitIndex++, (hours % 10).ToString());
            }

            if (showMinutes)
            {
                CheckAndAnimateDigit(digitIndex++, (minutes / 10).ToString());
                CheckAndAnimateDigit(digitIndex++, (minutes % 10).ToString());
            }

            if (showSeconds)
            {
                CheckAndAnimateDigit(digitIndex++, (seconds / 10).ToString());
                CheckAndAnimateDigit(digitIndex++, (seconds % 10).ToString());
            }

            if (hours != lastDisplayedHours || minutes != lastDisplayedMinutes || seconds != lastDisplayedSeconds)
            {
                lastDisplayedHours = hours;
                lastDisplayedMinutes = minutes;
                lastDisplayedSeconds = seconds;
                onTimeChanged?.Invoke(hours, minutes, seconds);
            }
        }

        void UpdateDisplayImmediate()
        {
            int digitIndex = 0;

            if (showHours)
            {
                if (digitIndex < allDigits.Count)
                {
                    allDigits[digitIndex].mainText.text = (hours / 10).ToString();
                    allDigits[digitIndex].currentValue = (hours / 10).ToString();
                    digitIndex++;
                }

                if (digitIndex < allDigits.Count)
                {
                    allDigits[digitIndex].mainText.text = (hours % 10).ToString();
                    allDigits[digitIndex].currentValue = (hours % 10).ToString();
                    digitIndex++;
                }
            }

            if (showMinutes)
            {
                if (digitIndex < allDigits.Count)
                {
                    allDigits[digitIndex].mainText.text = (minutes / 10).ToString();
                    allDigits[digitIndex].currentValue = (minutes / 10).ToString();
                    digitIndex++;
                }

                if (digitIndex < allDigits.Count)
                {
                    allDigits[digitIndex].mainText.text = (minutes % 10).ToString();
                    allDigits[digitIndex].currentValue = (minutes % 10).ToString();
                    digitIndex++;
                }
            }

            if (showSeconds)
            {
                if (digitIndex < allDigits.Count)
                {
                    allDigits[digitIndex].mainText.text = (seconds / 10).ToString();
                    allDigits[digitIndex].currentValue = (seconds / 10).ToString();
                    digitIndex++;
                }

                if (digitIndex < allDigits.Count)
                {
                    allDigits[digitIndex].mainText.text = (seconds % 10).ToString();
                    allDigits[digitIndex].currentValue = (seconds % 10).ToString();
                }
            }
        }

        void CheckAndAnimateDigit(int digitIndex, string newValue)
        {
            if (digitIndex >= allDigits.Count) { return; }
            if (allDigits[digitIndex].currentValue != newValue) { AnimateDigitChange(digitIndex, newValue); }
        }

        void AnimateDigitChange(int digitIndex, string newValue)
        {
            if (allDigits[digitIndex].isAnimating) { return; }
            if (activeAnimations.ContainsKey(digitIndex))
            {
                StopCoroutine(activeAnimations[digitIndex]);
                activeAnimations.Remove(digitIndex);
            }

            Coroutine anim = StartCoroutine(AnimateDigit(digitIndex, newValue));
            activeAnimations[digitIndex] = anim;
        }

        int GetDigitCount()
        {
            int count = 0;
            if (showHours) count += 2;
            if (showMinutes) count += 2;
            if (showSeconds) count += 2;
            return count;
        }

        int GetSeparatorCount()
        {
            int count = 0;
            if (showHours && (showMinutes || showSeconds)) count++;
            if (showMinutes && showSeconds) count++;
            return count;
        }

        void CreateDynamicLayout(Vector2 bounds, int digitCount, int separatorCount)
        {
            float totalSeparatorWidth = separatorCount * (fontSize * 0.3f);
            float digitWidth = (bounds.x - totalSeparatorWidth) / digitCount;
            float currentX = -bounds.x * 0.5f;

            CreateTimeUnits(currentX, digitWidth, false);
        }

        void CreateFixedLayout(Vector2 bounds, int digitCount, int separatorCount)
        {
            float digitWidth = fontSize * 0.7f;
            float separatorWidth = separatorSpacing;
            float totalWidth = digitCount * digitWidth + separatorCount * separatorWidth + (digitCount - 1) * digitSpacing;
            float currentX = -totalWidth * 0.5f;

            CreateTimeUnits(currentX, digitWidth, true);
        }

        float CreateTimeUnits(float startX, float digitWidth, bool useSpacing)
        {
            float currentX = startX;

            if (showHours)
            {
                currentX = CreateDigitPair(currentX, digitWidth, useSpacing);
                if (showMinutes || showSeconds) { currentX = CreateSeparatorAtX(currentX, useSpacing); }
            }

            if (showMinutes)
            {
                currentX = CreateDigitPair(currentX, digitWidth, useSpacing);
                if (showSeconds) { currentX = CreateSeparatorAtX(currentX, useSpacing); }
            }

            if (showSeconds)
            {
                currentX = CreateDigitPair(currentX, digitWidth, useSpacing);
            }

            return currentX;
        }

        float CreateDigitPair(float startX, float digitWidth, bool useSpacing)
        {
            float currentX = startX;
            currentX = CreateSingleDigit(currentX, digitWidth);
            if (useSpacing) { currentX += digitSpacing; }
            currentX = CreateSingleDigit(currentX, digitWidth);
            return currentX;
        }

        float CreateSeparatorAtX(float startX, bool useSpacing)
        {
            if (useSpacing) { startX += digitSpacing; }
            float width = useSpacing ? separatorSpacing : fontSize * 0.3f;
            startX = CreateSeparatorObject(startX, width);
            if (useSpacing) { startX += digitSpacing; }
            return startX;
        }

        float CreateSingleDigit(float startX, float width)
        {
            GameObject containerObj = new($"Digit {allDigits.Count}") { hideFlags = HideFlags.DontSave };
            containerObj.transform.SetParent(transform, false);
            containerObj.transform.localScale = Vector3.one;

            RectTransform containerRect = containerObj.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.5f, 0f);
            containerRect.anchorMax = new Vector2(0.5f, 1f);
            containerRect.sizeDelta = new Vector2(width, 0);
            containerRect.anchoredPosition = new Vector2(startX + width * 0.5f, 0);

            // Create main and animation text objects
            TextMeshProUGUI mainText = CreateTextMeshPro(containerObj, "Main Digit");
            TextMeshProUGUI animText = CreateTextMeshPro(containerObj, "Anim Digit");

            // Get styled color for animation text
            Color styledFontColor = Styler.GetColor(stylingSource, fontColor, stylerPreset);
            animText.color = new Color(styledFontColor.r, styledFontColor.g, styledFontColor.b, 0);
            animText.rectTransform.anchoredPosition = new Vector2(0, slideDistance);

            // Store in digit display system
            allDigits.Add(new DigitDisplay
            {
                container = containerObj,
                mainText = mainText,
                animText = animText,
                currentValue = "0",
                isAnimating = false
            });

            return startX + width;
        }

        float CreateSeparatorObject(float startX, float width)
        {
            GameObject sepObj = new($"Separator {separators.Count}") { hideFlags = HideFlags.DontSave };
            sepObj.transform.SetParent(transform, false);
            sepObj.transform.localScale = Vector3.one;

            RectTransform sepRect = sepObj.AddComponent<RectTransform>();
            sepRect.anchorMin = new Vector2(0.5f, 0f);
            sepRect.anchorMax = new Vector2(0.5f, 1f);
            sepRect.sizeDelta = new Vector2(width, 0);
            sepRect.anchoredPosition = new Vector2(startX + width * 0.5f, 0);

            TextMeshProUGUI sepText = CreateTextMeshPro(sepObj, "Separator Text");
            sepText.text = separator;
            separators.Add(sepText);

            return startX + width;
        }

        TextMeshProUGUI CreateTextMeshPro(GameObject parent, string name)
        {
            GameObject textObj = new(name) { hideFlags = HideFlags.DontSave };
            textObj.transform.SetParent(parent.transform, false);
            textObj.transform.localScale = Vector3.one;

            TextMeshProUGUI tmpText = textObj.AddComponent<TextMeshProUGUI>();

            RectTransform rect = tmpText.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;

            // Get styled font and color
            TMP_FontAsset styledFont = Styler.GetFont(stylingSource, timerFont, stylerPreset);
            Color styledFontColor = Styler.GetColor(stylingSource, fontColor, stylerPreset);

            if (styledFont != null) { tmpText.font = styledFont; }

            tmpText.fontSize = fontSize;
            tmpText.color = styledFontColor;
            tmpText.fontStyle = fontStyle;
            tmpText.alignment = TextAlignmentOptions.Center;
            tmpText.enableAutoSizing = false;
            tmpText.overflowMode = TextOverflowModes.Overflow;
            tmpText.text = "0";

            return tmpText;
        }

        IEnumerator AnimateDigit(int digitIndex, string newValue)
        {
            DigitDisplay digit = allDigits[digitIndex];
            digit.isAnimating = true;

            // Get styled color
            Color styledFontColor = Styler.GetColor(stylingSource, fontColor, stylerPreset);

            digit.animText.text = newValue;
            digit.animText.color = new Color(styledFontColor.r, styledFontColor.g, styledFontColor.b, 0);
            digit.animText.rectTransform.anchoredPosition = new Vector2(0, slideDistance);

            Vector2 mainStartPos = Vector2.zero;
            Color mainStartColor = digit.mainText.color;

            float elapsed = 0f;

            while (elapsed < animationDuration)
            {
                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float t = elapsed / animationDuration;
                float curve = animationCurve.Evaluate(t);

                digit.mainText.rectTransform.anchoredPosition = Vector2.Lerp(mainStartPos, new Vector2(0, -slideDistance), curve);
                digit.mainText.color = Color.Lerp(mainStartColor, new Color(mainStartColor.r, mainStartColor.g, mainStartColor.b, 0), curve);

                digit.animText.rectTransform.anchoredPosition = Vector2.Lerp(new Vector2(0, slideDistance), Vector2.zero, curve);
                digit.animText.color = Color.Lerp(new Color(styledFontColor.r, styledFontColor.g, styledFontColor.b, 0), styledFontColor, curve);

                yield return null;
            }

            digit.mainText.text = newValue;
            digit.mainText.rectTransform.anchoredPosition = Vector2.zero;
            digit.mainText.color = styledFontColor;

            digit.animText.color = new Color(styledFontColor.r, styledFontColor.g, styledFontColor.b, 0);
            digit.animText.rectTransform.anchoredPosition = new Vector2(0, slideDistance);

            digit.currentValue = newValue;
            digit.isAnimating = false;

            if (activeAnimations.ContainsKey(digitIndex)) { activeAnimations.Remove(digitIndex); }
        }

        public void ResetTimer()
        {
            isRunning = false;
            currentTime = (double)(hours * 3600 + minutes * 60 + seconds);
            lastDisplayedHours = lastDisplayedMinutes = lastDisplayedSeconds = -1;
            foreach (var anim in activeAnimations.Values) { if (anim != null) StopCoroutine(anim); }
            activeAnimations.Clear();
            UpdateDisplayImmediate();
        }

        public void SetTime(int newHours, int newMinutes, int newSeconds)
        {
            hours = newHours;
            minutes = newMinutes;
            seconds = newSeconds;
            currentTime = (double)(hours * 3600 + minutes * 60 + seconds);
            UpdateDisplayImmediate();
        }

        public void RefreshDisplay()
        {
            RequestRedraw();
        }

        public void UpdateStyler()
        {
            RequestRedraw();
        }

        public void StartTimer() => isRunning = true;
        public void PauseTimer() => isRunning = false;
        public double GetCurrentTime() => currentTime;
        public bool IsRunning() => isRunning;

#if UNITY_EDITOR
        [HideInInspector] public bool objectFoldout = true;
        [HideInInspector] public bool settingsFoldout = false;
        [HideInInspector] public bool styleFoldout = false;
        [HideInInspector] public bool eventsFoldout = false;
        [HideInInspector] public bool stylingFoldout = false;

        void OnValidate()
        {
            if (!Application.isPlaying && gameObject.activeInHierarchy)
            {
                RequestRedraw();
            }
        }
#endif
    }
}