using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HELP_URL + "ui-elements/menu-bar")]
    [AddComponentMenu("Evo/UI/UI Elements/Menu Bar")]
    public class MenuBar : MonoBehaviour
    {
        [EvoHeader("Item List", Constants.CUSTOM_EDITOR_ID)]
        public List<BarItem> items = new();

        [EvoHeader("Settings", Constants.CUSTOM_EDITOR_ID)]
        public bool openMenuOnHover;
        public bool blockUIWhileOpen;
        public Vector2 itemMenuOffset = new(0, -5);

        [EvoHeader("Animation", Constants.CUSTOM_EDITOR_ID)]
        [SerializeField] private ContextMenu.AnimationType animationType = ContextMenu.AnimationType.Slide;
        [SerializeField, Range(0.01f, 1)] private float animationDuration = 0.1f;
        [SerializeField] private AnimationCurve animationCurve = new(new Keyframe(0, 0, 0, 2), new Keyframe(1, 1, 0, 0));
        [SerializeField, Range(0f, 1f)] private float scaleFrom = 0.8f;
        [SerializeField] private Vector2 slideOffset = new(0, -20);

#if EVO_LOCALIZATION
        [EvoHeader("Localization", Constants.CUSTOM_EDITOR_ID)]
        public bool enableLocalization = true;
        public Localization.LocalizedObject localizedObject;
#endif

        [EvoHeader("References", Constants.CUSTOM_EDITOR_ID)]
        [SerializeField] private Transform itemParent;
        [SerializeField] private GameObject itemPreset;
        [SerializeField] private GameObject contextMenuPreset;

        // Constants
        const int SORT_ORDER = 30002;

        // Cache
        Canvas menuCanvas;

        // Current state
        bool isOpen;
        BarItem currentBarItem;

        [System.Serializable]
        public class BarItem
        {
            // [Header("Basic Properties")]
            public string label;
            public Sprite icon;

#if EVO_LOCALIZATION
            [Header("Localization")]
            public string tableKey;
#endif

            [Header("Context Menu")]
            public List<ContextMenu.Item> menuBarItems = new();

            [Header("Events")]
            public UnityEvent onClick = new();

            // Runtime cache
            [HideInInspector] public Button generatedButton;
            [HideInInspector] public ContextMenu generatedMenu;
            bool isActive = true;

            /// <summary>
            /// Sets the active state of the item.
            /// </summary>
            public bool IsActive
            {
                get => isActive;
                set
                {
                    isActive = value;
                    if (generatedButton != null) { generatedButton.gameObject.SetActive(value); }
                }
            }

            /// <summary>
            /// Finds a child item by its entry name.
            /// </summary>
            public ContextMenu.Item GetChildItem(string itemName)
            {
                return menuBarItems.Find(x => x.itemName == itemName);
            }
        }

        void Awake()
        {
            if (itemParent == null) { itemParent = transform; }
            Initialize();
        }

#if EVO_LOCALIZATION
        void Start()
        {
            if (enableLocalization)
            {
                localizedObject = Localization.LocalizedObject.Check(gameObject);
                if (localizedObject != null)
                {
                    Localization.LocalizationManager.OnLanguageSet += UpdateLocalization;
                    UpdateLocalization();
                }
            }
        }

        void OnDestroy()
        {
            if (enableLocalization && localizedObject != null)
            {
                Localization.LocalizationManager.OnLanguageSet -= UpdateLocalization;
            }
        }
#endif

        public void Initialize()
        {
            // Prepare list
            foreach (Transform go in itemParent) { Destroy(go.gameObject); }
            foreach (var item in items)
            {
                GameObject itemGO = Instantiate(itemPreset, itemParent);
                itemGO.name = item.label;

                // Initialize button
                Button btn = itemGO.GetComponent<Button>();
                btn.SetText(item.label);
                btn.SetIcon(item.icon);
                btn.onClick.AddListener(() =>
                {
                    item.onClick.Invoke();
                    OpenMenu(item);
                });
                btn.onPointerEnter.AddListener(() =>
                {
                    if (item.menuBarItems.Count == 0) { CloseMenu(); }
                    else if (openMenuOnHover || isOpen) { OpenMenu(item); }
                });
                item.generatedButton = btn;

                // Initialize context menu
                if (item.menuBarItems.Count > 0)
                {
                    ContextMenu cm = itemGO.AddComponent<ContextMenu>();
                    if (contextMenuPreset != null) { cm.menuPreset = contextMenuPreset; }
                    cm.triggerButton = ContextMenu.MouseButton.None;
                    cm.offsetPosition = OffsetPosition.Custom;
                    cm.blockUIWhileOpen = blockUIWhileOpen;
                    cm.closeOnOutsideClick = true;
                    cm.usePointerPosition = false;
                    cm.animationType = animationType;
                    cm.animationDuration = animationDuration;
                    cm.animationCurve = animationCurve;
                    cm.slideOffset = slideOffset;
                    cm.scaleFrom = scaleFrom;
                    cm.menuItems = item.menuBarItems;
                    item.generatedMenu = cm;
                }
            }

            // Add canvas and override its sorting so that it's interactable with context menu
            menuCanvas = itemParent.gameObject.AddComponent<Canvas>();
            itemParent.gameObject.AddComponent<GraphicRaycaster>();
            menuCanvas.vertexColorAlwaysGammaSpace = true;
            menuCanvas.overrideSorting = false;
        }

        public void OpenMenu(BarItem targetBarItem)
        {
            // Return early if trying to open the same item
            if (targetBarItem == currentBarItem || 
                (targetBarItem.generatedMenu != null && targetBarItem.generatedMenu.IsVisible())) 
            { 
                return; 
            }

            // Close other item's menu
            if (currentBarItem != null) { CloseMenu(); }

            // Only show menu if there are available items
            if (targetBarItem.menuBarItems.Count != 0)
            {
                currentBarItem = targetBarItem;

                // Calculate menu position
                if (targetBarItem.generatedButton.TryGetComponent<RectTransform>(out var btnRect))
                {
                    // Calculate the base alignment (Left/Bottom) in screen pixels
                    float scaledWidth = btnRect.rect.width * btnRect.lossyScale.x;
                    float scaledHeight = btnRect.rect.height * btnRect.lossyScale.y;
                    Vector2 baseOffset = new(-scaledWidth / 2f, -scaledHeight / 2f);

                    // Add the custom Menu Offset on top
                    targetBarItem.generatedMenu.customOffset = baseOffset + itemMenuOffset;
                }

                // Set button state to selected for visual feedback
                targetBarItem.generatedButton.SetState(InteractionState.Selected);

                // Initialize and show menu
                targetBarItem.generatedMenu.onHide.RemoveAllListeners();
                targetBarItem.generatedMenu.onHide.AddListener(() => CloseMenu());
                targetBarItem.generatedMenu.Show();

                menuCanvas.overrideSorting = true;
                menuCanvas.sortingOrder = SORT_ORDER;
                isOpen = true;
            }
        }

        public void CloseMenu()
        {
            if (!isOpen)
                return;

            // Hide menu
            currentBarItem.generatedMenu.onHide.RemoveAllListeners();
            currentBarItem.generatedMenu.Hide();

            // Set button state
            currentBarItem.generatedButton.SetState(InteractionState.Normal);

            // Set current state
            menuCanvas.overrideSorting = false;
            menuCanvas.sortingOrder = SORT_ORDER;
            currentBarItem = null;
            isOpen = false;
        }

        /// <summary>
        /// Finds a top-level menu item by its name.
        /// </summary>
        public BarItem GetItem(string label)
        {
            return items.Find(x => x.label == label);
        }

#if EVO_LOCALIZATION
        void UpdateLocalization(Localization.LocalizationLanguage language = null)
        {
            foreach (BarItem item in items)
            {
                if (!string.IsNullOrEmpty(item.tableKey) && item.generatedButton != null)
                {
                    // Bar button
                    item.label = localizedObject.GetString(item.tableKey);
                    item.generatedButton.SetText(item.label);

                    // Bar menu items
                    if (item.menuBarItems.Count > 0)
                    {
                        foreach (ContextMenu.Item mBarItem in item.menuBarItems)
                        {
                            mBarItem.itemName = localizedObject.GetString(item.tableKey);
                        }
                    }
                }
            }
        }
#endif

#if UNITY_EDITOR
        [HideInInspector] public bool contentFoldout = true;
        [HideInInspector] public bool settingsFoldout = false;
        [HideInInspector] public bool referencesFoldout = false;
#endif
    }
}