using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Evo.UI
{
    /// <summary>
    /// Base class for all interactive UI elements with state management.
    /// Based on Unity's Selectable class, with extended features.
    /// </summary>
    public class Interactive : Selectable, IStylerHandler, IPointerDownHandler, IPointerClickHandler, IPointerUpHandler,
        IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler, ISubmitHandler
    {
        [EvoHeader("Ripple Effect", Constants.CUSTOM_EDITOR_ID)]
        public bool enableRipple = false;
        [SerializeField] private RectTransform rippleParent;
        public Ripple.Preset ripplePreset;

        [EvoHeader("Trail Effect", Constants.CUSTOM_EDITOR_ID)]
        public bool enableTrail = false;
        [SerializeField] private RectTransform trailParent;
        public PointerTrail.Preset trailPreset;

        [EvoHeader("Styling", Constants.CUSTOM_EDITOR_ID)]
        public StylingSource sfxSource = StylingSource.StylerPreset;
        public StylerPreset stylerPreset;

        [EvoHeader("SFX", Constants.CUSTOM_EDITOR_ID)]
        public AudioMapping highlightedSFX = new() { stylerID = "Hover SFX" };
        public AudioMapping pressedSFX = new() { stylerID = "Click SFX" };
        public AudioMapping selectedSFX = new() { stylerID = "" };
        public static string[] GetSFXFields() => new[] { "highlightedSFX", "pressedSFX", "selectedSFX" };

        [EvoHeader("Settings", Constants.CUSTOM_EDITOR_ID)]
        [Range(0, 2)] public float transitionDuration = 0.1f;
        public InteractionState interactionState = InteractionState.Normal;
        public static string[] GetInteractionStateIDs() => new[] { "Disabled", "Normal", "Highlighted", "Pressed", "Selected" };

        [EvoHeader("References", Constants.CUSTOM_EDITOR_ID)]
        [SerializeField] private CanvasGroup disabledCG;
        [SerializeField] private CanvasGroup normalCG;
        [SerializeField] private CanvasGroup highlightedCG;
        [SerializeField] private CanvasGroup pressedCG;
        [SerializeField] private CanvasGroup selectedCG;

        [EvoHeader("Events", Constants.CUSTOM_EDITOR_ID)]
        public UnityEvent onClick = new();
        public UnityEvent onPointerDown = new();
        public UnityEvent onPointerUp = new();
        public UnityEvent onDoubleClick = new();
        public UnityEvent onPointerEnter = new();
        public UnityEvent onPointerExit = new();
        public UnityEvent onSelect = new();
        public UnityEvent onDeselect = new();
        public UnityEvent onSubmit = new();

        // State change event for external listeners
        public event System.Action<InteractionState> OnStateChanged;

        // Cache
        protected bool isPointerOn;
        protected bool isPressedDown;
        protected bool waitingForDoubleClickInput;
        protected Coroutine currentAnimation;

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

        protected override void Awake()
        {
            if (Application.isPlaying) 
            {
                Utilities.AddRaycastGraphic(gameObject); 
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            SetState(interactionState, true);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (IsInteractable() && interactionState != InteractionState.Selected) { interactionState = InteractionState.Normal; }
        }

        protected override void OnCanvasGroupChanged()
        {
            base.OnCanvasGroupChanged();

            if (Time.frameCount == 0 || interactionState == InteractionState.Selected)
                return;

            if (!IsInteractable() && interactionState != InteractionState.Disabled) { SetState(InteractionState.Disabled); }
            else if (IsInteractable() && interactionState == InteractionState.Disabled && (isPointerOn || Utilities.GetSelectedObject() == gameObject)) { SetState(InteractionState.Highlighted); }
            else if (IsInteractable() && interactionState == InteractionState.Disabled) { SetState(InteractionState.Normal); }

#if UNITY_EDITOR
            // Update visuals for editor, pre-runtime
            if (!Application.isPlaying) { UpdateStyler(); }
#endif
        }

        public virtual void OnPointerClick(PointerEventData eventData)
        {
            if (!IsInteractable() || eventData.button != PointerEventData.InputButton.Left)
                return;

            onClick?.Invoke();
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            if (!IsInteractable() || eventData.button != PointerEventData.InputButton.Left)
                return;

            isPressedDown = true;

            if (enableRipple) { Ripple.Create(ripplePreset, rippleParent, true); }
            if (interactionState != InteractionState.Selected) { SetState(InteractionState.Pressed); }
            if (navigation.mode != Navigation.Mode.None) { Utilities.SetSelectedObject(gameObject); }

            AudioManager.PlayClip(Styler.GetAudio(sfxSource, pressedSFX, stylerPreset));
            onPointerDown?.Invoke();
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            if (!IsInteractable()) { return; }
            if (interactionState != InteractionState.Selected) { SetState(isPointerOn ? InteractionState.Highlighted : InteractionState.Normal); }

            isPressedDown = false;
            onPointerUp?.Invoke();
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            isPointerOn = true;

            if (!IsInteractable()) { return; }
            if (interactionState != InteractionState.Selected && !isPressedDown) { SetState(InteractionState.Highlighted); }
            if (enableTrail) { PointerTrail.Create(trailPreset, trailParent, true); }

            AudioManager.PlayClip(Styler.GetAudio(sfxSource, highlightedSFX, stylerPreset));
            onPointerEnter?.Invoke();
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            isPointerOn = false;

            if (!IsInteractable()) { return; }
            if (interactionState != InteractionState.Selected && !isPressedDown) { SetState(InteractionState.Normal); }
            if (enableTrail) { PointerTrail.Hide(trailParent); }

            onPointerExit?.Invoke();
        }

        public override void OnSelect(BaseEventData eventData)
        {
            if (!IsInteractable() || isPressedDown) { return; }
            if (interactionState != InteractionState.Selected) { SetState(InteractionState.Highlighted); }

            AudioManager.PlayClip(Styler.GetAudio(sfxSource, highlightedSFX, stylerPreset));
            onSelect?.Invoke();
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            if (!IsInteractable()) { return; }
            if (interactionState != InteractionState.Selected) { SetState(InteractionState.Normal); }

            onDeselect?.Invoke();
        }

        public virtual void OnSubmit(BaseEventData eventData)
        {
            if (!IsInteractable()) { return; }
            if (enableRipple) { Ripple.Create(ripplePreset, rippleParent, true, true); }
            if (interactionState != InteractionState.Selected && Utilities.GetSelectedObject() != gameObject) { SetState(InteractionState.Normal); }

            AudioManager.PlayClip(Styler.GetAudio(sfxSource, pressedSFX, stylerPreset));
            onClick?.Invoke();
            onSubmit?.Invoke();
        }

        protected virtual IEnumerator AnimateToState(InteractionState state)
        {
            if (state == InteractionState.Selected) { AudioManager.PlayClip(Styler.GetAudio(sfxSource, selectedSFX, stylerPreset)); }
            if (enableTrail && state == InteractionState.Disabled) { PointerTrail.Hide(trailParent); }

            var stateGroups = GetStateCanvasGroups();
            var targets = new Dictionary<CanvasGroup, float>();

            foreach (var kvp in stateGroups)
            {
                if (kvp.Value != null)
                {
                    targets[kvp.Value] = kvp.Key == state ? 1 : 0;
                }
            }

            yield return Utilities.CrossFadeCanvasGroup(targets, Mathf.Max(0f, transitionDuration));
        }

        /// <summary>
        /// Sets the interactable state.
        /// </summary>
        public virtual void SetState(InteractionState state, bool instant = false)
        {
            if (interactionState == state)
                return;

            interactionState = state;
            OnStateChanged?.Invoke(interactionState);

            if (!Application.isPlaying || instant || !gameObject.activeInHierarchy)
            {
                var stateGroups = GetStateCanvasGroups();
                foreach (var kvp in stateGroups)
                {
                    if (kvp.Value != null)
                    {
                        kvp.Value.alpha = kvp.Key == state ? 1 : 0;
                    }
                }
            }
            else
            {
                if (currentAnimation != null) { StopCoroutine(currentAnimation); }
                currentAnimation = StartCoroutine(AnimateToState(state));
            }
        }

        /// <summary>
        /// Helper method to change interactable state and update visual state accordingly.
        /// </summary>
        public virtual void SetInteractable(bool value)
        {
            if (interactable && value)
                return;

            isPressedDown = !value && isPressedDown;
            isPointerOn = !value && isPointerOn;
            interactable = value;

            SetState(value ? InteractionState.Normal : InteractionState.Disabled);
        }

        /// <summary>
        /// Override this to map custom states to canvas groups.
        /// </summary>
        protected virtual Dictionary<InteractionState, CanvasGroup> GetStateCanvasGroups()
        {
            var dict = new Dictionary<InteractionState, CanvasGroup>();

            if (disabledCG != null) { dict[InteractionState.Disabled] = disabledCG; }
            if (normalCG != null) { dict[InteractionState.Normal] = normalCG; }
            if (highlightedCG != null) { dict[InteractionState.Highlighted] = highlightedCG; }
            if (pressedCG != null) { dict[InteractionState.Pressed] = pressedCG; }
            if (selectedCG != null) { dict[InteractionState.Selected] = selectedCG; }

            return dict;
        }

        public void UpdateStyler()
        {
            if (!gameObject.activeInHierarchy)
                return;

            StylerObject[] stylers = GetComponentsInChildren<StylerObject>(true);
            foreach (var styler in stylers)
            {
                if (styler != null && styler.enableInteraction && styler.interactableObject == this)
                {
                    styler.UpdateStyler();
                }
            }
        }

#if UNITY_EDITOR
        [HideInInspector] public bool settingsFoldout = true;
        [HideInInspector] public bool styleFoldout = false;
        [HideInInspector] public bool navigationFoldout = false;
        [HideInInspector] public bool referencesFoldout = false;
        [HideInInspector] public bool eventsFoldout = false;

        protected override void OnValidate()
        {
            base.OnValidate();
            UpdateEditorState();
        }

        protected void UpdateEditorState()
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null)
                {
                    if (!IsInteractable()) { interactionState = InteractionState.Disabled; }
                    else if (interactionState == InteractionState.Disabled) { interactionState = InteractionState.Normal; }

                    SetState(interactionState, true);
                    UpdateStyler();
                }
            };
        }
#endif
    }
}