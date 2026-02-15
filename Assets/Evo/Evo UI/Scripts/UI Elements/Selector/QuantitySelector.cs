using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HELP_URL + "ui-elements/quantity-selector")]
    [AddComponentMenu("Evo/UI/UI Elements/Quantity Selector")]
    public class QuantitySelector : MonoBehaviour
    {
        [EvoHeader("Settings", Constants.CUSTOM_EDITOR_ID)]
        [SerializeField] private int startQuantity = 1;
        public int minQuantity = 1;
        public int maxQuantity = 99;

        [EvoHeader("Animation", Constants.CUSTOM_EDITOR_ID)]
        [SerializeField] private float slideOffset = 40f;
        [SerializeField, Range(0, 1)] private float animationDuration = 0.2f;
        [SerializeField] private AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [EvoHeader("References", Constants.CUSTOM_EDITOR_ID)]
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private Button increaseButton;
        [SerializeField] private Button decreaseButton;

        [EvoHeader("Events", Constants.CUSTOM_EDITOR_ID)]
        public UnityEvent<int> onValueChanged = new();

        // Public Properties
        public int CurrentQuantity => currentQuantity;

        // Helpers
        bool isManualInput = false; // Helper flag to prevent animation when manually typing
        int currentQuantity;
        Color baseTextColor;
        Vector2 originalTextPosition;
        Coroutine animationCoroutine;
        RectTransform textRectTransform;
        TMP_Text activeGhostText;

        void Awake()
        {
            if (inputField == null)
            {
                Debug.LogError("[Quantity Selector] Input Field is missing!", this);
                enabled = false;
                return;
            }

            // Setup Button Listeners
            if (increaseButton) { increaseButton.onClick.AddListener(Increase); }
            if (decreaseButton) { decreaseButton.onClick.AddListener(Decrease); }

            // Setup Input Listeners
            inputField.onValueChanged.AddListener(OnTextInputChanged);
            inputField.onEndEdit.AddListener(OnTextInputEndEdit);

            // Cache the text component's RectTransform for animation
            if (inputField.textComponent != null)
            {
                textRectTransform = inputField.textComponent.rectTransform;
                originalTextPosition = textRectTransform.anchoredPosition;
                baseTextColor = inputField.textComponent.color;
            }

            // Set Character Limit based on Max Quantity digits
            inputField.characterLimit = maxQuantity.ToString().Length;

            // Initialize
            SetQuantity(startQuantity, false);
        }

        void OnDisable()
        {
            // Reset text position in case the object is disabled mid-animation
            if (textRectTransform != null)
            {
                textRectTransform.anchoredPosition = originalTextPosition;
                if (inputField.textComponent != null) { inputField.textComponent.color = baseTextColor; }
            }

            // Cleanup ghost text
            if (activeGhostText != null) { Destroy(activeGhostText.gameObject); }
        }

        void OnTextInputChanged(string text)
        {
            if (isManualInput) { return; } // Ignore programmatic changes
            if (int.TryParse(text, out int result))
            {
                // Clamping visually while typing can be annoying, so just track the valid number
                // Only clamp rigidly on EndEdit
                currentQuantity = result;
                UpdateButtonsState();
            }
        }

        void OnTextInputEndEdit(string text)
        {
            if (int.TryParse(text, out int result)) { SetQuantity(result, false, true); }
            else { SetQuantity(currentQuantity, false, true); }
        }

        void UpdateButtonsState()
        {
            if (increaseButton != null) { increaseButton.SetInteractable(currentQuantity < maxQuantity); }
            if (decreaseButton != null) { decreaseButton.SetInteractable(currentQuantity > minQuantity); }
        }

        void SetQuantity(int newValue, bool animate, bool isIncreasing)
        {
            int oldQuantity = currentQuantity;
            currentQuantity = Mathf.Clamp(newValue, minQuantity, maxQuantity);

            // Update UI state
            UpdateButtonsState();

            // Update Input Field text
            // We set isManualInput to true briefly so the OnValueChanged listener ignores this update
            isManualInput = true;
            inputField.text = currentQuantity.ToString();
            isManualInput = false;

            // Trigger Event if changed
            if (oldQuantity != currentQuantity)
            {
                onValueChanged?.Invoke(currentQuantity);

                // Trigger Animation only if requested and component is active
                if (animate && gameObject.activeInHierarchy && textRectTransform != null)
                {
                    if (animationCoroutine != null)
                    {
                        StopCoroutine(animationCoroutine);

                        // Reset state to base to prevent "faded text" bug on rapid clicks
                        inputField.textComponent.color = baseTextColor;
                        inputField.textComponent.rectTransform.anchoredPosition = originalTextPosition;

                        // Cleanup any lingering ghost from interrupted animation
                        if (activeGhostText != null) { Destroy(activeGhostText.gameObject); }
                    }
                    animationCoroutine = StartCoroutine(AnimateTextChange(oldQuantity.ToString(), isIncreasing));
                }
            }
        }

        public void Increase()
        {
            if (currentQuantity < maxQuantity)
            {
                SetQuantity(currentQuantity + 1, true, true);
            }
        }

        public void Decrease()
        {
            if (currentQuantity > minQuantity)
            {
                SetQuantity(currentQuantity - 1, true, false);
            }
        }

        public void SetQuantity(int value, bool animate = true)
        {
            // Determine direction for animation based on new vs old value
            bool isIncrease = value > currentQuantity;
            SetQuantity(value, animate, isIncrease);
        }

        IEnumerator AnimateTextChange(string oldTextString, bool isIncreasing)
        {
            // Create a dummy text object to represent the old number moving out
            TMP_Text ghostText = Instantiate(inputField.textComponent, inputField.textComponent.transform.parent);
            activeGhostText = ghostText;

            ghostText.text = oldTextString;
            ghostText.rectTransform.anchoredPosition = originalTextPosition;
            ghostText.raycastTarget = false;

            // Define positions
            // If increasing: New comes from Bottom (-offset), Old goes to Top (+offset)
            // If decreasing: New comes from Top (+offset), Old goes to Bottom (-offset)
            float directionMultiplier = isIncreasing ? 1f : -1f;

            Vector2 startPosOld = originalTextPosition;
            Vector2 endPosOld = originalTextPosition + new Vector2(0, slideOffset * directionMultiplier);

            Vector2 startPosNew = originalTextPosition + new Vector2(0, -slideOffset * directionMultiplier);
            Vector2 endPosNew = originalTextPosition;

            // Prepare the InputField's actual text (aka new value)
            textRectTransform.anchoredPosition = startPosNew;

            // Fade logic vars
            Color originalColor = baseTextColor;
            Color transparentColor = new(originalColor.r, originalColor.g, originalColor.b, 0);

            // Ensure ghost starts at base color
            ghostText.color = originalColor;

            float elapsed = 0f;
            while (elapsed < animationDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / animationDuration);
                float curveT = animationCurve.Evaluate(t);

                // Move ghost (old value) out
                if (ghostText != null)
                {
                    ghostText.rectTransform.anchoredPosition = Vector2.Lerp(startPosOld, endPosOld, curveT);
                    ghostText.color = Color.Lerp(originalColor, transparentColor, curveT * 2); // Fade out faster
                }

                // Move actual input (new value) in
                textRectTransform.anchoredPosition = Vector2.Lerp(startPosNew, endPosNew, curveT);
                inputField.textComponent.color = Color.Lerp(transparentColor, originalColor, curveT);

                yield return null;
            }

            // Cleanup and reset
            if (ghostText != null) { Destroy(ghostText.gameObject); }
            activeGhostText = null;

            textRectTransform.anchoredPosition = originalTextPosition;
            inputField.textComponent.color = originalColor;
            animationCoroutine = null;
        }

#if UNITY_EDITOR
        [HideInInspector] public bool objectFoldout = true;
        [HideInInspector] public bool settingsFoldout = false;
        [HideInInspector] public bool referencesFoldout = false;
        [HideInInspector] public bool eventsFoldout = false;

        void OnValidate()
        {
            maxQuantity = Mathf.Max(maxQuantity, minQuantity);
            startQuantity = Mathf.Clamp(startQuantity, minQuantity, maxQuantity);
            if (inputField != null) { inputField.text = startQuantity.ToString(); }
        }
#endif
    }
}