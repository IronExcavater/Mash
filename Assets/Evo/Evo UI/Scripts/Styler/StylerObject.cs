using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HELP_URL + "styler")]
    [AddComponentMenu("Evo/UI/Styler/Styler Object")]
    public class StylerObject : MonoBehaviour, IStylerHandler
    {
        [EvoHeader("References", Constants.CUSTOM_EDITOR_ID)]
        public StylerPreset preset;
        public Image targetImage;
        public TextMeshProUGUI targetText;

        [EvoHeader("Settings", Constants.CUSTOM_EDITOR_ID)]
        public ObjectType objectType = ObjectType.Image;
        public string colorID = "Primary";
        public string fontID = "Regular";
        public bool useCustomColor = false;
        public bool overrideAlpha = false;
        [Range(0f, 1f)] public float alphaOverride = 1f;

        [EvoHeader("Interaction", Constants.CUSTOM_EDITOR_ID)]
        public bool enableInteraction = false;
		public Interactive interactableObject;
        public ColorMapping disabledColor = new() { stylerID = "Primary" };
        public ColorMapping normalColor = new() { stylerID = "Primary" };
        public ColorMapping highlightedColor = new() { stylerID = "Primary" };
        public ColorMapping pressedColor = new() { stylerID = "Primary" };
        public ColorMapping selectedColor = new() { stylerID = "Primary" };

        // Styler Interface
        public StylerPreset Preset
        {
            get => preset;
            set
            {
                if (preset == value) { return; }
                preset = value;
                UpdateStyler();
            }
        }

        // Interaction Cache
        InteractionState currentState;
        Coroutine tweenCoroutine;

        public enum ObjectType
        {
            Image = 0,
            TMPText = 1,
        }

        void Awake()
        {
            Styler.RegisteredObjects.Add(this);
        }

        void OnEnable()
        {
            // Subscribe to catch future states
            if (enableInteraction && interactableObject != null)
            {
                interactableObject.OnStateChanged += OnInteractableStateChanged;
                currentState = interactableObject.interactionState;
            }

            // Apply the visuals
            UpdateStyler();
        }

        void OnDisable()
        {
            // Unsubscribe to bypass animating hidden objects
            if (enableInteraction && interactableObject != null)
            {
                interactableObject.OnStateChanged -= OnInteractableStateChanged;
            }

            // Cleanup tweens
            if (tweenCoroutine != null)
            {
                StopCoroutine(tweenCoroutine);
                tweenCoroutine = null;
                if (currentState != InteractionState.Selected && currentState != InteractionState.Disabled) 
                { 
                    currentState = InteractionState.Normal;
                }
            }
        }

        void OnDestroy()
        {
            Styler.RegisteredObjects.Remove(this);
        }

        void CheckComponents()
        {
            if (objectType == ObjectType.Image && targetImage == null)
            {
                targetImage = GetComponent<Image>();
                targetText = null;
            }
            else if (objectType == ObjectType.TMPText && targetText == null)
            {
                targetText = GetComponent<TextMeshProUGUI>();
                targetImage = null;
            }
        }

        void OnInteractableStateChanged(InteractionState newState)
        {
            if (currentState == newState || !gameObject.activeInHierarchy)
                return;

            currentState = newState;
            AnimateToState(newState);
        }

        void AnimateToState(InteractionState state)
        {
            if (objectType == ObjectType.TMPText && targetText == null || objectType == ObjectType.Image && targetImage == null)
                return;

            float tDuration = Mathf.Max(0f, interactableObject != null ? interactableObject.transitionDuration : 0f);
            Color tColor = GetInteractionColor(state);
            Graphic tGraphic = objectType == ObjectType.Image ? targetImage : targetText;

            if (tweenCoroutine != null) { StopCoroutine(tweenCoroutine); }
            tweenCoroutine = StartCoroutine(Utilities.CrossFadeGraphic(tGraphic, tColor, tDuration));
        }

        Color GetTargetColor()
        {
            if (enableInteraction && interactableObject != null) { return GetInteractionColor(interactableObject.interactionState); }
            if (string.IsNullOrEmpty(colorID) || preset == null) { return Color.clear; }
          
            Color baseColor = preset.GetColor(colorID);
         
            if (!useCustomColor && overrideAlpha) { return new Color(baseColor.r, baseColor.g, baseColor.b, alphaOverride); }
            return baseColor;
        }

        Color GetInteractionColor(InteractionState state)
        {
            ColorMapping mapping = GetColorMappingForState(state);

            // Use Styler Preset color
            if (!useCustomColor && preset != null)
            {
                if (string.IsNullOrEmpty(mapping.stylerID) || preset == null) { return Color.clear; }
                Color baseColor = preset.GetColor(mapping.stylerID);
                if (!useCustomColor && overrideAlpha) { return new Color(baseColor.r, baseColor.g, baseColor.b, alphaOverride); }
                return baseColor;
            }

            // Fallback to custom color
            Color customColor = mapping.color;
            if (preset != null && !useCustomColor && overrideAlpha) { return new Color(customColor.r, customColor.g, customColor.b, alphaOverride); }
            return customColor;
        }

        ColorMapping GetColorMappingForState(InteractionState state)
        {
            return state switch
            {
                InteractionState.Disabled => disabledColor,
                InteractionState.Normal => normalColor,
                InteractionState.Highlighted => highlightedColor,
                InteractionState.Pressed => pressedColor,
                InteractionState.Selected => selectedColor,
                _ => null
            };
        }

        [System.Obsolete("Use UpdateStyler() instead. This method will be removed in future versions.")]
        public void UpdateStyle() => UpdateStyler();

        public void UpdateStyler()
        {
            CheckComponents();

            if ((!enableInteraction && preset == null) || (targetImage == null && targetText == null)) { return; }
            if (useCustomColor && !enableInteraction)
            {
                if (objectType == ObjectType.TMPText && targetText != null && preset != null)
                {
                    TMP_FontAsset targetFont = preset.GetFont(fontID);
                    if (targetText.font != targetFont) { targetText.font = targetFont; }
                }
                return;
            }

            Color targetColor = GetTargetColor();

            // Check for active transition
            if (tweenCoroutine != null)
            {
                StopCoroutine(tweenCoroutine);
                tweenCoroutine = null;
            }

            if (objectType == ObjectType.Image && targetImage.color != targetColor) { targetImage.color = targetColor; }
            else if (objectType == ObjectType.TMPText)
            {
                if (preset != null)
                {
                    TMP_FontAsset targetFont = preset.GetFont(fontID);
                    if (targetText.font != targetFont) { targetText.font = targetFont; }
                }
                if (targetText.color != targetColor) { targetText.color = targetColor; }
            }
        }

        public List<string> GetAvailableColorIDs()
        {
            if (preset == null) { return new List<string>(); }
            return preset.colorItems.Select(item => item.itemID).ToList();
        }

        public List<string> GetAvailableFontIDs()
        {
            if (preset == null) { return new List<string>(); }
            return preset.fontItems.Select(item => item.itemID).ToList();
        }

#if UNITY_EDITOR
        [HideInInspector] public bool referencesFoldout = true;
        [HideInInspector] public bool settingsFoldout = true;
        [HideInInspector] public bool interactionFoldout = true;

        void Reset()
        {
            CheckComponents();
            if (preset == null) { preset = Styler.GetDefaultPreset(false); }
        }

        void OnValidate()
        {
            if (!this.enabled) { return; }
            if (preset != null)
            {
                var availableColors = GetAvailableColorIDs();
                var availableFonts = GetAvailableFontIDs();

                if (!string.IsNullOrEmpty(colorID) && !availableColors.Contains(colorID) && availableColors.Count > 0) { colorID = availableColors[0]; }
                if (!string.IsNullOrEmpty(fontID) && !availableFonts.Contains(fontID) && availableFonts.Count > 0) { fontID = availableFonts[0]; }
            }

            UpdateStyler();
        }
#endif
    }
}