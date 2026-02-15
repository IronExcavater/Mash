using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HELP_URL + "ui-elements/tooltip")]
    [AddComponentMenu("Evo/UI/UI Elements/Tooltip")]
    public class Tooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [EvoHeader("Content", Constants.CUSTOM_EDITOR_ID)]
        public GameObject tooltipPreset;
        public Sprite icon;
        public string title;
        [TextArea(2, 4)] public string description;

#if EVO_LOCALIZATION
        [EvoHeader("Localization", Constants.CUSTOM_EDITOR_ID)]
        public bool enableLocalization = true;
        public Localization.LocalizedObject localizedObject;
        public string titleKey;
        public string descriptionKey;
#endif

        [EvoHeader("Settings", Constants.CUSTOM_EDITOR_ID)]
        public bool followCursor = true;
        public bool is3DObject = false;
        [SerializeField] private float maxWidth = 400;
        [SerializeField, Range(0, 10)] private float showDelay = 0;
        [SerializeField, Range(0, 40)] private float movementSmoothing = 0;

        [EvoHeader("Animation", Constants.CUSTOM_EDITOR_ID)]
        public AnimationType animationType = AnimationType.Slide;
        [Range(0.01f, 1)] public float animationDuration = 0.1f;
        public AnimationCurve animationCurve = new(new Keyframe(0, 0, 0, 2), new Keyframe(1, 1, 0, 0));
        [SerializeField, Range(0f, 1f)] private float scaleFrom = 0.7f;
        [SerializeField] private Vector2 slideOffset = new(0, -20);

        [EvoHeader("Position & Offset", Constants.CUSTOM_EDITOR_ID)]
        [SerializeField] private OffsetPosition offsetPosition = OffsetPosition.BottomRight;
        [SerializeField] private Vector2 customOffset = new(10, 10);
        [SerializeField] private float offsetDistance = 30;
        [SerializeField] private float screenEdgePadding = 10;

        [EvoHeader("References", Constants.CUSTOM_EDITOR_ID)]
        [SerializeField] private Canvas tooltipCanvas;
        public GameObject customContent;

        [EvoHeader("Events", Constants.CUSTOM_EDITOR_ID)]
        public UnityEvent onShow = new();
        public UnityEvent onHide = new();

        public enum AnimationType
        {
            None = 0,
            Fade = 1,
            Scale = 2,
            Slide = 3
        }

        // Cache
        TooltipPreset tooltipInstance;
        Coroutine showCoroutine;
        Coroutine positionCoroutine;
        Coroutine animationCoroutine;

        // Helpers
        bool isCustomLogGiven;

        // Public Properties
        public TooltipPreset Instance => tooltipInstance;
        public bool IsVisible() => tooltipInstance != null;

        // Helper Properties
        Canvas ActiveCanvas
        {
            get
            {
                if (tooltipCanvas != null) { return tooltipCanvas; }
                else
                {
                    var tempCanvas = GetComponentInParent<Canvas>();
                    if (tempCanvas != null)
                    {
                        tooltipCanvas = tempCanvas;
                        return tooltipCanvas;
                    }
                    return Globals.GetCanvas();
                }
            }
        }

#if EVO_LOCALIZATION
        void Start()
        {
            if (enableLocalization && customContent == null)
            {
                localizedObject = Localization.LocalizedObject.Check(gameObject);
                if (localizedObject != null)
                {
                    Localization.LocalizationManager.OnLanguageSet += UpdateLocalization;
                    UpdateLocalization();
                }
            }
        }
#endif

        void OnDisable()
        {
            Hide();
        }

        void OnDestroy()
        {
#if EVO_LOCALIZATION
            if (enableLocalization && localizedObject != null) { Localization.LocalizationManager.OnLanguageSet -= UpdateLocalization; }
#endif
            Hide();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!is3DObject)
            {
                Show();
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!is3DObject)
            {
                Hide();
            }
        }

        void OnMouseEnter()
        {
            if (is3DObject)
            {
                Show();
            }
        }

        void OnMouseExit()
        {
            if (is3DObject)
            {
                Hide();
            }
        }

        void StopAllTooltipCoroutines()
        {
            if (showCoroutine != null)
            {
                StopCoroutine(showCoroutine);
                showCoroutine = null;
            }
            if (positionCoroutine != null)
            {
                StopCoroutine(positionCoroutine);
                positionCoroutine = null;
            }
            if (animationCoroutine != null)
            {
                StopCoroutine(animationCoroutine);
                animationCoroutine = null;
            }
        }

        void SetInitialAnimationState()
        {
            if (tooltipInstance == null)
                return;

            switch (animationType)
            {
                case AnimationType.Fade:
                    tooltipInstance.canvasGroup.alpha = 0f;
                    break;

                case AnimationType.Scale:
                    tooltipInstance.canvasGroup.alpha = 0f;
                    tooltipInstance.transform.localScale = Vector3.one * scaleFrom;
                    break;

                case AnimationType.Slide:
                    tooltipInstance.canvasGroup.alpha = 0f;
                    // Store the current position as base, then apply offset
                    Vector3 currentPos = tooltipInstance.tooltipRect.anchoredPosition;
                    tooltipInstance.tooltipRect.anchoredPosition = currentPos + (Vector3)slideOffset;
                    break;
            }
        }

        void ApplyAnimationState(float progress, bool isIn, Vector3 startScale = default)
        {
            if (tooltipInstance == null)
                return;

            switch (animationType)
            {
                case AnimationType.Fade:
                    tooltipInstance.canvasGroup.alpha = progress;
                    break;

                case AnimationType.Scale:
                    tooltipInstance.canvasGroup.alpha = progress;
                    if (isIn)
                    {
                        tooltipInstance.transform.localScale = Vector3.Lerp(Vector3.one * scaleFrom, Vector3.one, progress);
                    }
                    else
                    {
                        tooltipInstance.transform.localScale = Vector3.Lerp(startScale, Vector3.one * scaleFrom, 1f - progress);
                    }
                    break;

                case AnimationType.Slide:
                    tooltipInstance.canvasGroup.alpha = progress;
                    if (isIn)
                    {
                        Vector2 currentTarget = CalculateCurrentTargetPosition();
                        Vector2 slideStartPos = currentTarget + slideOffset;
                        tooltipInstance.tooltipRect.anchoredPosition = Vector2.Lerp(slideStartPos, currentTarget, progress);
                    }
                    break;
            }
        }

        Vector2 CalculateTargetPosition(Camera targetCamera)
        {
            Canvas canvas = ActiveCanvas;
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();

            Vector2 screenPoint;

            // Get screen point based on object type
            if (followCursor) { screenPoint = Utilities.GetPointerPosition(); }
            else if (TryGetComponent<RectTransform>(out var rectTransform))
            {
                // For UI elements
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay) { screenPoint = rectTransform.position; }
                else
                {
                    Camera cam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
                    if (cam != null) { screenPoint = RectTransformUtility.WorldToScreenPoint(cam, rectTransform.position); }
                    else { screenPoint = rectTransform.position; }
                }

                // Adjust for non-centered pivot
                Vector2 pivot = rectTransform.pivot;
                Vector2 pivotOffset = new(
                    (pivot.x - 0.5f) * rectTransform.rect.width * rectTransform.lossyScale.x,
                    (pivot.y - 0.5f) * rectTransform.rect.height * rectTransform.lossyScale.y
                );

                screenPoint.x -= pivotOffset.x;
                screenPoint.y -= pivotOffset.y;
            }
            else
            {
                // For 3D objects
                if (targetCamera == null) { targetCamera = Camera.main; }
                screenPoint = targetCamera != null ? targetCamera.WorldToScreenPoint(transform.position) : Vector2.zero;
            }

            // Convert screen point to canvas anchored position
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPoint,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                out Vector2 anchoredPosition
            );

            return anchoredPosition;
        }

        Camera GetTargetCamera()
        {
            Canvas canvas = ActiveCanvas;
            if (canvas.worldCamera != null) { return canvas.worldCamera; }
            if (TryGetComponent<RectTransform>(out _))
            {
                Canvas parentCanvas = GetComponentInParent<Canvas>();
                if (parentCanvas != null && parentCanvas.worldCamera != null) { return parentCanvas.worldCamera; }
            }
            return Camera.main;
        }

        Vector2 GetOffsetVector()
        {
            if (offsetPosition == OffsetPosition.Custom) { return customOffset; }
            if (tooltipInstance == null) { return Vector2.zero; }
            if (tooltipInstance.tooltipRect == null) { return Vector2.zero; }

            // Force layout update to get accurate size
            Canvas.ForceUpdateCanvases();

            float width = tooltipInstance.tooltipRect.rect.width;
            float height = tooltipInstance.tooltipRect.rect.height;

            Vector2 offset = Vector2.zero;

            switch (offsetPosition)
            {
                case OffsetPosition.TopLeft:
                    offset = new Vector2(-width * 0.5f - offsetDistance, height * 0.5f + offsetDistance);
                    break;
                case OffsetPosition.TopRight:
                    offset = new Vector2(width * 0.5f + offsetDistance, height * 0.5f + offsetDistance);
                    break;
                case OffsetPosition.BottomLeft:
                    offset = new Vector2(-width * 0.5f - offsetDistance, -height * 0.5f - offsetDistance);
                    break;
                case OffsetPosition.BottomRight:
                    offset = new Vector2(width * 0.5f + offsetDistance, -height * 0.5f - offsetDistance);
                    break;
                case OffsetPosition.Top:
                    offset = new Vector2(0, height * 0.5f + offsetDistance);
                    break;
                case OffsetPosition.Bottom:
                    offset = new Vector2(0, -height * 0.5f - offsetDistance);
                    break;
                case OffsetPosition.Left:
                    offset = new Vector2(-width * 0.5f - offsetDistance, 0);
                    break;
                case OffsetPosition.Right:
                    offset = new Vector2(width * 0.5f + offsetDistance, 0);
                    break;
            }

            return offset;
        }

        Vector2 CalculateCurrentTargetPosition()
        {
            if (tooltipInstance == null)
                return Vector2.zero;

            Camera targetCamera = GetTargetCamera();
            Vector2 currentTarget = CalculateTargetPosition(targetCamera);
            currentTarget += GetOffsetVector();
            return ClampToCanvasBounds(currentTarget, tooltipInstance.tooltipRect);
        }

        Vector2 ClampToCanvasBounds(Vector2 anchoredPosition, RectTransform tooltipRect)
        {
            Canvas canvas = ActiveCanvas;
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();

            float width = tooltipRect.rect.width;
            float height = tooltipRect.rect.height;

            // Calculate canvas bounds in local space
            float canvasWidth = canvasRect.rect.width;
            float canvasHeight = canvasRect.rect.height;

            // Account for padding
            float minX = -canvasWidth * 0.5f + width * 0.5f + screenEdgePadding;
            float maxX = canvasWidth * 0.5f - width * 0.5f - screenEdgePadding;
            float minY = -canvasHeight * 0.5f + height * 0.5f + screenEdgePadding;
            float maxY = canvasHeight * 0.5f - height * 0.5f - screenEdgePadding;

            // Clamp position
            anchoredPosition.x = Mathf.Clamp(anchoredPosition.x, minX, maxX);
            anchoredPosition.y = Mathf.Clamp(anchoredPosition.y, minY, maxY);

            return anchoredPosition;
        }

        IEnumerator ShowTooltip()
        {
            yield return new WaitForSeconds(showDelay);

            // Verify object is still active and valid
            if (this == null || !gameObject.activeInHierarchy)
                yield break;

            // Determine which prefab to use
            GameObject prefabToUse = customContent != null ? customContent : tooltipPreset;

            if (prefabToUse == null)
            {
                Debug.LogWarning($"No Tooltip Preset or Custom Content assigned to {gameObject.name}.", this);
                yield break;
            }

            // Instantiate tooltip
            GameObject toGo = Instantiate(prefabToUse, ActiveCanvas.transform);
            bool isCustom = customContent != null;

            // Try to get existing TooltipPreset, or add one if missing
            if (!toGo.TryGetComponent<TooltipPreset>(out tooltipInstance))
            {
                tooltipInstance = toGo.AddComponent<TooltipPreset>();
                if (!isCustomLogGiven)
                {
                    Debug.Log($"<b>Tooltip Preset</b> component is not attached to <b>{gameObject.name}</b>, " +
                        $"but it's assigned as Tooltip Preset." +
                        $"\nThis tooltip will be treated as Custom Content, and the default content will be skipped.\n", this);
                    isCustomLogGiven = true;
                }
                isCustom = true;
            }

            // Setup tooltip content
            tooltipInstance.Setup(title, description, icon, maxWidth, isCustom);

            // Start position updates immediately for all animations
            positionCoroutine = StartCoroutine(UpdateTooltipPosition());

            // Start animations
            if (tooltipInstance != null && animationType != AnimationType.None) { animationCoroutine = StartCoroutine(AnimateTooltipIn()); }

            // Set coroutine
            showCoroutine = null;
        }

        IEnumerator UpdateTooltipPosition()
        {
            if (tooltipInstance.tooltipRect == null)
                yield break;

            // Cache camera reference
            Camera targetCamera = GetTargetCamera();

            while (tooltipInstance != null && this != null && gameObject.activeInHierarchy)
            {
                Vector2 newTargetPosition = CalculateTargetPosition(targetCamera);
                newTargetPosition += GetOffsetVector();
                newTargetPosition = ClampToCanvasBounds(newTargetPosition, tooltipInstance.tooltipRect);

                if (movementSmoothing == 0) { tooltipInstance.tooltipRect.anchoredPosition = newTargetPosition; }
                else
                {
                    Vector2 currentPosition = tooltipInstance.tooltipRect.anchoredPosition;
                    Vector2 smoothedPosition = Vector2.Lerp(currentPosition, newTargetPosition, movementSmoothing * Time.unscaledDeltaTime);
                    tooltipInstance.tooltipRect.anchoredPosition = smoothedPosition;
                }

                yield return null;
            }
        }

        IEnumerator AnimateTooltipIn()
        {
            if (tooltipInstance == null)
                yield break;

            SetInitialAnimationState();

            float elapsedTime = 0f;

            while (elapsedTime < animationDuration)
            {
                elapsedTime += Time.unscaledDeltaTime;
                float progress = elapsedTime / animationDuration;
                float curveValue = animationCurve.Evaluate(progress);

                ApplyAnimationState(curveValue, true);

                yield return null;
            }

            // Ensure final state
            ApplyAnimationState(1f, true);

            // Set coroutine
            animationCoroutine = null;
        }

        IEnumerator AnimateTooltipOut()
        {
            if (tooltipInstance == null)
            {
                HideImmediate();
                yield break;
            }

            // Store initial values
            float startingAlpha = tooltipInstance.canvasGroup.alpha;
            Vector3 startingScale = tooltipInstance.transform.localScale;
            Vector2 startPosition = tooltipInstance.tooltipRect.anchoredPosition;

            float elapsedTime = 0f;

            while (elapsedTime < animationDuration)
            {
                elapsedTime += Time.unscaledDeltaTime;
                float progress = elapsedTime / animationDuration;

                switch (animationType)
                {
                    case AnimationType.Fade:
                        tooltipInstance.canvasGroup.alpha = Mathf.Lerp(startingAlpha, 0f, progress);
                        break;

                    case AnimationType.Scale:
                        tooltipInstance.canvasGroup.alpha = Mathf.Lerp(startingAlpha, 0f, progress);
                        tooltipInstance.transform.localScale = Vector3.Lerp(startingScale, Vector3.one * scaleFrom, progress);
                        break;

                    case AnimationType.Slide:
                        tooltipInstance.canvasGroup.alpha = Mathf.Lerp(startingAlpha, 0f, progress);
                        Vector2 endPos = startPosition + slideOffset;
                        tooltipInstance.tooltipRect.anchoredPosition = Vector2.Lerp(startPosition, endPos, progress);
                        break;
                }

                yield return null;
            }

            HideImmediate();
        }

        public void Show()
        {
            // Check if we have either a tooltip preset or custom content
            if (tooltipPreset == null && customContent == null)
            {
                Debug.LogWarning($"No tooltip preset or custom content assigned to {gameObject.name}. " +
                    $"Please assign a prefab with 'Tooltip Preset' component or a custom content GameObject.", this);
                return;
            }

            // Cancel existing operations
            StopAllTooltipCoroutines();

            // Hide any existing tooltip
            HideImmediate();

            // Start delayed show
            showCoroutine = StartCoroutine(ShowTooltip());

            // Invoke events
            onShow?.Invoke();
        }

        public void Hide()
        {
            // Cancel existing operations
            StopAllTooltipCoroutines();

            // Hide the tooltip
            if (!gameObject.activeInHierarchy || animationType == AnimationType.None) { HideImmediate(); }
            else if (tooltipInstance != null) { animationCoroutine = StartCoroutine(AnimateTooltipOut()); }

            // Invoke events
            onHide?.Invoke();
        }

        public void HideImmediate()
        {
            if (tooltipInstance != null)
            {
                Destroy(tooltipInstance.gameObject);
                tooltipInstance = null;
            }
        }

        public void SetContent(string newTitle, string newDescription, Sprite newIcon = null, GameObject newCustomContent = null)
        {
            if (newCustomContent != null)
            {
                customContent = newCustomContent;
                if (tooltipInstance != null)
                {
                    HideImmediate();
                    Show();
                }
                return;
            }

            title = newTitle;
            description = newDescription;
            icon = newIcon;
            customContent = newCustomContent;

            if (tooltipInstance != null) 
            {
                tooltipInstance.Setup(title, description, icon, maxWidth); 
            }
        }

        public void SetContent(GameObject newCustomContent)
        {
            SetContent(null, null, null, newCustomContent);
        }

        public void SetTitle(string newTitle)
        {
            title = newTitle;
            if (tooltipInstance != null) { tooltipInstance.Setup(title, description, icon, maxWidth); }
        }

        public void SetDescription(string newDescription)
        {
            description = newDescription;
            if (tooltipInstance != null) { tooltipInstance.Setup(title, description, icon, maxWidth); }
        }

        public void SetIcon(Sprite newIcon)
        {
            icon = newIcon;
            if (tooltipInstance != null) { tooltipInstance.Setup(title, description, icon, maxWidth); }
        }

#if EVO_LOCALIZATION
        void UpdateLocalization(Localization.LocalizationLanguage language = null)
        {
            if (!string.IsNullOrEmpty(titleKey)) { title = localizedObject.GetString(titleKey); }
            if (!string.IsNullOrEmpty(descriptionKey)) { description = localizedObject.GetString(descriptionKey); }
        }
#endif

#if UNITY_EDITOR
        [HideInInspector] public bool contentFoldout = true;
        [HideInInspector] public bool settingsFoldout = false;
        [HideInInspector] public bool referencesFoldout = false;
        [HideInInspector] public bool eventsFoldout = false;
#endif
    }
}