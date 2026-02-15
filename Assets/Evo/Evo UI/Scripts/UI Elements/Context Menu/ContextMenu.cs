using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HELP_URL + "ui-elements/context-menu")]
    [AddComponentMenu("Evo/UI/UI Elements/Context Menu")]
    public class ContextMenu : MonoBehaviour, IPointerClickHandler
    {
        [EvoHeader("Content", Constants.CUSTOM_EDITOR_ID)]
        public List<Item> menuItems = new();

        [EvoHeader("Settings", Constants.CUSTOM_EDITOR_ID)]
        public bool is3DObject = false;
        public bool usePointerPosition = true;
        public bool blockUIWhileOpen = false;
        public bool closeOnItemClick = true;
        public bool closeOnOutsideClick = true;
        public MouseButton triggerButton = MouseButton.Right;

        [EvoHeader("Animation", Constants.CUSTOM_EDITOR_ID)]
        public AnimationType animationType = AnimationType.Slide;
        [Range(0.01f, 1)] public float animationDuration = 0.1f;
        public AnimationCurve animationCurve = new(new Keyframe(0, 0, 0, 2), new Keyframe(1, 1, 0, 0));
        [Range(0f, 1f)] public float scaleFrom = 0.8f;
        public Vector2 slideOffset = new(0, -20);

        [EvoHeader("Position & Offset", Constants.CUSTOM_EDITOR_ID)]
        public OffsetPosition offsetPosition = OffsetPosition.BottomRight;
        public Vector2 customOffset = new(10, 10);
        public float offsetDistance = 10;
        public float screenEdgePadding = 10;

#if EVO_LOCALIZATION
        [EvoHeader("Localization", Constants.CUSTOM_EDITOR_ID)]
        public bool enableLocalization = true;
        public Localization.LocalizedObject localizedObject;
#endif

        [EvoHeader("References", Constants.CUSTOM_EDITOR_ID)]
        public GameObject menuPreset;
        public Canvas targetCanvas;

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

        public enum MouseButton
        {
            Left = 0,
            Right = 1,
            Middle = 2,
            None = 9
        }

        // Constants
        const int SORTING_ORDER = 30000;

        // Instance variables
        ContextMenuPreset menuInstance;
        Coroutine animationCoroutine;
        Coroutine outsideCoroutine;
        Vector3 lastClickPosition;
        Vector3 targetPosition;
        Canvas rootCanvas;
        Canvas blocker;
        Canvas menuCanvas;
        readonly static List<ContextMenu> activeMenus = new();

        [System.Serializable]
        public class Item
        {
            [Header("Basic Properties")]
            public string itemName = "Menu Item";
            public ItemType itemType = ItemType.Button;
            public Sprite icon;

#if EVO_LOCALIZATION
            [Header("Localization")]
            public string tableKey;
#endif

            [Header("Button Settings")]
            public UnityEvent onClick = new();

            [Header("Section Settings")]
            public bool expandOnHover = true;
            public List<SectionItem> sectionItems = new();

            [Header("Custom Object")]
            public GameObject customPrefab;

            public enum ItemType
            {
                Button = 0,
                Separator = 1,
                Section = 2,
                CustomObject = 3
            }

            /// <summary>
            /// Sets the active state of the item.
            /// </summary>
            public bool IsActive { get; set; } = true;

            /// <summary>
            /// Finds a sub-item by its entry name.
            /// </summary>
            public SectionItem GetSectionItem(string name)
            {
                return sectionItems.Find(x => x.itemName == name);
            }
        }

        [System.Serializable]
        public class SectionItem
        {
            [Header("Basic Properties")]
            public string itemName = "Menu Item";
            public ItemType itemType = ItemType.Button;
            public Sprite icon;

#if EVO_LOCALIZATION
            [Header("Localization")]
            public string tableKey;
#endif

            [Header("Button Settings")]
            public UnityEvent onClick = new();

            [Header("Custom Object")]
            public GameObject customPrefab;

            public enum ItemType
            {
                Button = 0,
                Separator = 1,
                CustomObject = 2
            }

            /// <summary>
            /// Sets the active state of the item.
            /// </summary>
            public bool IsActive { get; set; } = true;
        }

        void Start()
        {
            if (menuPreset == null)
            {
                Debug.LogError("[Context Menu] 'Menu Preset' is missing. Please select the object and assign a valid context menu preset via the References tab.", this);
                return;
            }

#if EVO_LOCALIZATION
            if (enableLocalization)
            {
                localizedObject = Localization.LocalizedObject.Check(gameObject);
                if (localizedObject != null)
                {
                    Localization.LocalizationManager.OnLanguageSet += UpdateLocalization;
                    UpdateLocalization();
                }
            }
#endif
        }

        void OnDisable()
        {
            Hide();
        }

        void OnDestroy()
        {
            activeMenus.Remove(this);
            DestroyBlocker();
#if EVO_LOCALIZATION
            if (enableLocalization && localizedObject != null)
            {
                Localization.LocalizationManager.OnLanguageSet -= UpdateLocalization;
            }
#endif
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!is3DObject && IsCorrectMouseButton(eventData))
            {
                lastClickPosition = eventData.position;
                Show();
            }
        }

        void OnMouseDown()
        {
            if (is3DObject && Utilities.WasMouseKeyPressed((int)triggerButton))
            {
                lastClickPosition = Utilities.GetPointerPosition();
                Show();
            }
        }

        void SetupMenu()
        {
            if (menuInstance == null)
                return;

            menuInstance.Setup(this, menuItems);
        }

        void PositionMenu()
        {
            if (menuInstance == null) { return; }
            if (!menuInstance.TryGetComponent<RectTransform>(out var menuRect)) { return; }

            // Setup initial pivot and layout using the configured settings
            SetMenuPivot(menuRect, offsetPosition);

            // Force layout update to get accurate size for calculations
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(menuRect);

            // Calculate base position (Screen Space) without offsets
            Vector3 basePosition = GetBasePosition();

            // Determine final offset and handle flipping
            // Check if the default configuration would push the menu off-screen
            Vector3 finalOffset = GetOffsetVector(offsetPosition);
            Vector3 projectedPos = basePosition + finalOffset;

            if (ShouldFlipVertical(projectedPos, menuRect))
            {
                if (offsetPosition == OffsetPosition.Custom)
                {
                    // Handle Custom Flip: Invert pivot Y and Y offset
                    menuRect.pivot = new Vector2(menuRect.pivot.x, 1f - menuRect.pivot.y);
                    finalOffset.y = -finalOffset.y;
                }
                else
                {
                    // Handle Standard Flip: Switch to opposite type (e.g., Bottom -> Top)
                    OffsetPosition flippedType = GetOppositeOffset(offsetPosition);
                    SetMenuPivot(menuRect, flippedType);
                    finalOffset = GetOffsetVector(flippedType);
                }
            }

            // Calculate final target position
            targetPosition = basePosition + finalOffset;

            // Clamp to screen
            // This ensures it never goes off-screen even after flipping
            targetPosition = ClampToScreen(targetPosition, menuRect);

            // Apply Position
            if (animationType != AnimationType.Slide)
            {
                menuRect.position = GetWorldPositionFromScreen(menuRect, targetPosition);
            }
        }

        Vector3 GetBasePosition()
        {
            if (menuInstance == null)
                return Vector3.zero;

            Vector3 basePosition;

            if (usePointerPosition) { basePosition = (Vector3)Utilities.GetPointerPosition(); }
            else
            {
                // Calculate position based on the UI element's RectTransform
                Canvas canvas = ActiveCanvas;

                if (TryGetComponent<RectTransform>(out var rectTransform))
                {
                    // For UI elements
                    if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                    {
                        basePosition = rectTransform.position;
                    }
                    else
                    {
                        Camera cam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
                        if (cam != null) { basePosition = RectTransformUtility.WorldToScreenPoint(cam, rectTransform.position); }
                        else { basePosition = rectTransform.position; }
                    }

                    // Adjust for non-centered pivot to ensure targeting the center/corner accurately
                    Vector2 pivot = rectTransform.pivot;
                    Vector2 pivotOffset = new(
                        (pivot.x - 0.5f) * rectTransform.rect.width * rectTransform.lossyScale.x,
                        (pivot.y - 0.5f) * rectTransform.rect.height * rectTransform.lossyScale.y
                    );

                    basePosition.x -= pivotOffset.x;
                    basePosition.y -= pivotOffset.y;
                }
                else if (is3DObject)
                {
                    Camera targetCamera = Camera.main;
                    basePosition = targetCamera != null ? targetCamera.WorldToScreenPoint(transform.position) : Vector3.zero;
                }
                else
                {
                    basePosition = lastClickPosition;
                }
            }

            return basePosition;
        }

        Vector3 GetWorldPositionFromScreen(RectTransform target, Vector3 screenPos)
        {
            Canvas canvas = ActiveCanvas;

            // If overlay, World Space == Screen Space
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay) { return screenPos; }

            // For Camera/World modes, we must convert
            Camera cam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
            if (cam == null) { return screenPos; } // Fallback

            // Convert screen point to world point on the plane of the RectTransform
            RectTransformUtility.ScreenPointToWorldPointInRectangle(
                target.parent as RectTransform,
                screenPos,
                cam,
                out Vector3 worldPos
            );

            return worldPos;
        }

        void SetMenuPivot(RectTransform menuRect, OffsetPosition type)
        {
            Vector2 pivot = Vector2.zero;

            switch (type)
            {
                case OffsetPosition.TopLeft:
                    pivot = new Vector2(1, 0); // Menu grows from bottom-right corner
                    break;
                case OffsetPosition.TopRight:
                    pivot = new Vector2(0, 0); // Menu grows from bottom-left corner
                    break;
                case OffsetPosition.BottomLeft:
                    pivot = new Vector2(1, 1); // Menu grows from top-right corner
                    break;
                case OffsetPosition.BottomRight:
                    pivot = new Vector2(0, 1); // Menu grows from top-left corner
                    break;
                case OffsetPosition.Top:
                    pivot = new Vector2(0.5f, 0); // Menu grows from bottom-center
                    break;
                case OffsetPosition.Bottom:
                    pivot = new Vector2(0.5f, 1); // Menu grows from top-center
                    break;
                case OffsetPosition.Left:
                    pivot = new Vector2(1, 0.5f); // Menu grows from right-center
                    break;
                case OffsetPosition.Right:
                    pivot = new Vector2(0, 0.5f); // Menu grows from left-center
                    break;
                case OffsetPosition.Custom:
                    pivot = new Vector2(0, 1); // Default to top-left for custom
                    break;
            }

            menuRect.pivot = pivot;
        }

        bool ShouldFlipVertical(Vector3 targetScreenPos, RectTransform menuRect)
        {
            Vector2 size = GetScreenSize(menuRect);

            // Pivot Y: 1 = Top (Grows Down), 0 = Bottom (Grows Up)
            if (menuRect.pivot.y > 0.5f) // Currently growing down (e.g. BottomRight)
            {
                // Check if the bottom edge is off-screen
                float bottomY = targetScreenPos.y - (size.y * menuRect.pivot.y);
                if (bottomY < screenEdgePadding) { return true; }
            }
            else if (menuRect.pivot.y < 0.5f) // Currently Growing Up (e.g. TopRight)
            {
                // Check if the top edge is off-screen
                float topY = targetScreenPos.y + (size.y * (1f - menuRect.pivot.y));
                if (topY > Screen.height - screenEdgePadding) { return true; }
            }

            return false;
        }

        OffsetPosition GetOppositeOffset(OffsetPosition current)
        {
            return current switch
            {
                OffsetPosition.TopLeft => OffsetPosition.BottomLeft,
                OffsetPosition.TopRight => OffsetPosition.BottomRight,
                OffsetPosition.BottomLeft => OffsetPosition.TopLeft,
                OffsetPosition.BottomRight => OffsetPosition.TopRight,
                OffsetPosition.Top => OffsetPosition.Bottom,
                OffsetPosition.Bottom => OffsetPosition.Top,
                _ => current,
            };
        }

        void SetInitialAnimationState()
        {
            if (menuInstance == null)
                return;

            RectTransform menuRect = menuInstance.GetComponent<RectTransform>();

            switch (animationType)
            {
                case AnimationType.Fade:
                    menuInstance.canvasGroup.alpha = 0f;
                    menuInstance.canvasGroup.blocksRaycasts = true;
                    menuInstance.canvasGroup.interactable = true;
                    menuRect.position = GetWorldPositionFromScreen(menuRect, targetPosition);
                    break;

                case AnimationType.Scale:
                    menuInstance.canvasGroup.alpha = 0f;
                    menuInstance.canvasGroup.blocksRaycasts = true;
                    menuInstance.canvasGroup.interactable = true;
                    menuInstance.transform.localScale = Vector3.one * scaleFrom;
                    menuRect.position = GetWorldPositionFromScreen(menuRect, targetPosition);
                    break;

                case AnimationType.Slide:
                    menuInstance.canvasGroup.alpha = 0f;
                    menuInstance.canvasGroup.blocksRaycasts = true;
                    menuInstance.canvasGroup.interactable = true;
                    Vector3 startPosScreen = targetPosition + (Vector3)slideOffset;
                    menuRect.position = GetWorldPositionFromScreen(menuRect, startPosScreen);
                    break;
            }
        }

        void ApplyAnimationState(float progress, bool isIn)
        {
            if (menuInstance == null)
                return;

            RectTransform menuRect = menuInstance.GetComponent<RectTransform>();

            switch (animationType)
            {
                case AnimationType.Fade:
                    menuInstance.canvasGroup.alpha = progress;
                    break;

                case AnimationType.Scale:
                    menuInstance.canvasGroup.alpha = progress;
                    menuInstance.transform.localScale = Vector3.one * progress;
                    break;

                case AnimationType.Slide:
                    menuInstance.canvasGroup.alpha = progress;
                    if (isIn)
                    {
                        Vector3 startPos = targetPosition + (Vector3)slideOffset;
                        Vector3 currentScreenPos = Vector3.Lerp(startPos, targetPosition, progress);
                        menuRect.position = GetWorldPositionFromScreen(menuRect, currentScreenPos);
                    }
                    else
                    {
                        Vector3 endPos = targetPosition + (Vector3)slideOffset;
                        Vector3 currentScreenPos = Vector3.Lerp(targetPosition, endPos, 1f - progress);
                        menuRect.position = GetWorldPositionFromScreen(menuRect, currentScreenPos);
                    }
                    break;
            }
        }

        void CreateMenuCanvas()
        {
            if (menuInstance == null || menuCanvas != null)
                return;

            menuCanvas = menuInstance.gameObject.AddComponent<Canvas>();
            menuInstance.gameObject.AddComponent<GraphicRaycaster>();
            menuCanvas.vertexColorAlwaysGammaSpace = true;
            menuCanvas.overrideSorting = true;
            menuCanvas.sortingOrder = SORTING_ORDER;
        }

        void CreateBlocker()
        {
            if (blocker != null) { return; }
            if (rootCanvas == null) { rootCanvas = GetComponentInParent<Canvas>().rootCanvas; }
            if (rootCanvas == null) { return; }

            GameObject blockerGO = new("Context Menu Blocker");
            blocker = blockerGO.AddComponent<Canvas>();
            blocker.overrideSorting = true;
            blocker.sortingOrder = SORTING_ORDER - 1;
            blockerGO.AddComponent<GraphicRaycaster>();

            RectTransform blockerRT = blockerGO.GetComponent<RectTransform>();
            blockerRT.SetParent(rootCanvas.transform, false);
            blockerRT.anchorMin = Vector2.zero;
            blockerRT.anchorMax = Vector2.one;
            blockerRT.sizeDelta = Vector2.zero;
            blockerRT.anchoredPosition = Vector2.zero;

            Image blockerImage = blockerGO.AddComponent<Image>();
            blockerImage.color = Color.clear;

            UnityEngine.UI.Button button = blockerGO.AddComponent<UnityEngine.UI.Button>();
            button.onClick.AddListener(() => { if (closeOnOutsideClick) { Hide(); } });

            Navigation nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
        }

        void DestroyBlocker()
        {
            if (blocker == null)
                return;

            Destroy(blocker.gameObject);
            blocker = null;
        }

        public void Show()
        {
            if (menuPreset == null || IsVisible())
                return;

            // Hide other active menus first
            HideAllActiveMenus();

            // Hide any existing menu
            HideImmediate();

            // Instantiate menu
            GameObject menuGO = Instantiate(menuPreset, ActiveCanvas.transform);
            menuInstance = menuGO.GetComponent<ContextMenuPreset>();

            if (menuInstance == null)
            {
                Destroy(menuGO);
                return;
            }

            // Invoke events
            onShow?.Invoke();

            // Setup menu
            SetupMenu();

            // Position menu
            PositionMenu();

            // Check if blocking is requested
            if (blockUIWhileOpen)
            {
                // Create canvas for proper layering
                CreateMenuCanvas();

                // Create blocker if needed
                CreateBlocker();
            }

            // Add to active menus
            activeMenus.Add(this);

            // Check for outside detection
            if (closeOnOutsideClick)
            {
                if (outsideCoroutine != null) { StopCoroutine(outsideCoroutine); outsideCoroutine = null; }
                outsideCoroutine = StartCoroutine(DetectOutsideClick());
            }

            // Start animation
            if (animationType != AnimationType.None)
            {
                if (animationCoroutine != null) { StopCoroutine(animationCoroutine); }
                animationCoroutine = StartCoroutine(AnimateMenuIn());
            }
        }

        public void Hide()
        {
            if (animationCoroutine != null)
            {
                StopCoroutine(animationCoroutine);
                animationCoroutine = null;
            }

            // Check for outside detection
            if (closeOnOutsideClick && outsideCoroutine != null)
            {
                StopCoroutine(outsideCoroutine);
                outsideCoroutine = null;
            }

            // Destroy blocker
            DestroyBlocker();

            // Start animation
            if (menuInstance != null && animationType != AnimationType.None && gameObject.activeInHierarchy)
            {
                if (animationCoroutine != null) { StopCoroutine(animationCoroutine); }
                animationCoroutine = StartCoroutine(AnimateMenuOut());

                menuInstance.CollapseAllSections();
            }
            else { HideImmediate(); }

            // Remove from active menus
            activeMenus.Remove(this);

            // Invoke events
            onHide?.Invoke();
        }

        public void HideImmediate()
        {
            if (menuInstance != null)
            {
                // Destroy all submenus first
                ContextMenuSection[] sections = menuInstance.GetComponentsInChildren<ContextMenuSection>();
                foreach (var section in sections) { section.DestroySubmenuImmediate(); }

                Destroy(menuInstance.gameObject);
                menuInstance = null;
            }

            menuCanvas = null;
        }

        public void OnItemClicked(Item item)
        {
            if (closeOnItemClick && item.itemType != Item.ItemType.Section)
            {
                Hide();
            }
        }

        public bool IsVisible()
        {
            return menuInstance != null;
        }

        public void SetTriggerButton(int index)
        {
            triggerButton = (MouseButton)index;
        }

        bool IsCorrectMouseButton(PointerEventData eventData)
        {
            return triggerButton switch
            {
                MouseButton.Left => eventData.button == PointerEventData.InputButton.Left,
                MouseButton.Right => eventData.button == PointerEventData.InputButton.Right,
                MouseButton.Middle => eventData.button == PointerEventData.InputButton.Middle,
                _ => false,
            };
        }

        bool IsPointerOverMenuOrSubmenus()
        {
            if (menuInstance == null)
                return false;

            // Get all UI elements under the mouse
            PointerEventData pointerData = new(EventSystem.current) { position = Utilities.GetPointerPosition() };
            List<RaycastResult> results = new();
            EventSystem.current.RaycastAll(pointerData, results);

            // Check if any of the hit elements belong to our menu or any submenu
            foreach (var result in results)
            {
                Transform current = result.gameObject.transform;

                // Check if it's part of the main menu
                if (current.IsChildOf(menuInstance.transform)) { return true; }

                // Check all sections for submenus
                ContextMenuSection[] allSections = menuInstance.GetComponentsInChildren<ContextMenuSection>();
                foreach (var section in allSections)
                {
                    if (section.submenuInstance != null && current.IsChildOf(section.submenuInstance.transform))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        Vector3 GetOffsetVector(OffsetPosition type)
        {
            if (type == OffsetPosition.Custom) { return (Vector3)customOffset; }
            Vector3 offset = Vector3.zero;

            switch (type)
            {
                case OffsetPosition.TopLeft:
                    offset = new Vector3(-offsetDistance, offsetDistance, 0);
                    break;
                case OffsetPosition.TopRight:
                    offset = new Vector3(offsetDistance, offsetDistance, 0);
                    break;
                case OffsetPosition.BottomLeft:
                    offset = new Vector3(-offsetDistance, -offsetDistance, 0);
                    break;
                case OffsetPosition.BottomRight:
                    offset = new Vector3(offsetDistance, -offsetDistance, 0);
                    break;
                case OffsetPosition.Top:
                    offset = new Vector3(0, offsetDistance, 0);
                    break;
                case OffsetPosition.Bottom:
                    offset = new Vector3(0, -offsetDistance, 0);
                    break;
                case OffsetPosition.Left:
                    offset = new Vector3(-offsetDistance, 0, 0);
                    break;
                case OffsetPosition.Right:
                    offset = new Vector3(offsetDistance, 0, 0);
                    break;
            }

            return offset;
        }

        Vector3 ClampToScreen(Vector3 position, RectTransform menuRect)
        {
            // Get menu size in pixels
            Vector2 size = GetScreenSize(menuRect);
            float width = size.x;
            float height = size.y;

            // Get pivot offsets
            float pivotOffsetX = width * menuRect.pivot.x;
            float pivotOffsetY = height * menuRect.pivot.y;

            // Calculate actual bounds of the menu in Screen Space
            float minX = position.x - pivotOffsetX;
            float maxX = position.x + (width - pivotOffsetX);
            float minY = position.y - pivotOffsetY;
            float maxY = position.y + (height - pivotOffsetY);

            // Adjust position if menu goes out of screen bounds
            if (minX < screenEdgePadding) { position.x += (screenEdgePadding - minX); }
            if (maxX > Screen.width - screenEdgePadding) { position.x -= (maxX - (Screen.width - screenEdgePadding)); }
            if (minY < screenEdgePadding) { position.y += (screenEdgePadding - minY); }
            if (maxY > Screen.height - screenEdgePadding) { position.y -= (maxY - (Screen.height - screenEdgePadding)); }

            return position;
        }

        Vector2 GetScreenSize(RectTransform rect)
        {
            Canvas canvas = ActiveCanvas;
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                // In Overlay, lossyScale handles the CanvasScaler scaling
                return Vector2.Scale(rect.rect.size, rect.lossyScale);
            }

            // For Camera/World modes, calculate screen projection
            Camera cam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
            if (cam == null) { return Vector2.Scale(rect.rect.size, rect.lossyScale); } // Fallback

            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);

            // Calculate screen bounds from world corners
            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;

            for (int i = 0; i < 4; i++)
            {
                Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(cam, corners[i]);
                if (screenPos.x < minX) { minX = screenPos.x; }
                if (screenPos.x > maxX) { maxX = screenPos.x; }
                if (screenPos.y < minY) { minY = screenPos.y; }
                if (screenPos.y > maxY) { maxY = screenPos.y; }
            }

            return new Vector2(maxX - minX, maxY - minY);
        }

        IEnumerator DetectOutsideClick()
        {
            while (true)
            {
                // Check for ESC key to close menu
                if (IsVisible() && Utilities.WasEscapeKeyPressed())
                {
                    Hide();
                    yield return null;
                    continue;
                }

                // Check for any mouse button clicks
                if (IsVisible() && Utilities.WasPointerPressed())
                {
                    // Wait one frame to let UI events process
                    yield return null;

                    // Check if the click was outside the menu
                    if (!IsPointerOverMenuOrSubmenus()) { Hide(); }
                }

                yield return null;
            }
        }

        IEnumerator AnimateMenuIn()
        {
            if (menuInstance == null || menuInstance.canvasGroup == null)
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
            animationCoroutine = null;
        }

        IEnumerator AnimateMenuOut()
        {
            if (menuInstance == null || menuInstance.canvasGroup == null)
            {
                HideImmediate();
                yield break;
            }

            menuInstance.canvasGroup.blocksRaycasts = false;
            menuInstance.canvasGroup.interactable = false;

            float elapsedTime = 0f;
            while (elapsedTime < animationDuration)
            {
                elapsedTime += Time.unscaledDeltaTime;
                float progress = elapsedTime / animationDuration;
                float curveValue = 1f - progress; // Reverse for out animation

                ApplyAnimationState(curveValue, false);

                yield return null;
            }

            HideImmediate();
        }

        static void HideAllActiveMenus()
        {
            for (int i = activeMenus.Count - 1; i >= 0; i--)
            {
                if (activeMenus[i] != null) { activeMenus[i].Hide(); }
                else { activeMenus.RemoveAt(i); }
            }
        }

        public Canvas ActiveCanvas
        {
            get
            {
                if (targetCanvas != null) { return targetCanvas; }
                else
                {
                    var tempCanvas = GetComponentInParent<Canvas>();
                    if (tempCanvas != null)
                    {
                        targetCanvas = tempCanvas;
                        return targetCanvas;
                    }
                    return Globals.GetCanvas();
                }
            }
        }

        /// <summary>
        /// Finds a top-level menu item by its entry name.
        /// </summary>
        public Item GetItem(string name)
        {
            return menuItems.Find(x => x.itemName == name);
        }

#if EVO_LOCALIZATION
        void UpdateLocalization(Localization.LocalizationLanguage language = null)
        {
            foreach (Item item in menuItems)
            {
                if (!string.IsNullOrEmpty(item.tableKey))
                {
                    item.itemName = localizedObject.GetString(item.tableKey);
                }
            }
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