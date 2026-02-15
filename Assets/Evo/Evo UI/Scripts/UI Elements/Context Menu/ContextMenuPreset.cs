using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HELP_URL + "ui-elements/context-menu")]
    [RequireComponent(typeof(CanvasGroup))]
    [RequireComponent(typeof(RectTransform))]
    public class ContextMenuPreset : MonoBehaviour
    {
        [Header("References")]
        public Transform itemContainer;
        public GameObject buttonPrefab;
        public GameObject separatorPrefab;
        public GameObject sectionPrefab;
        public CanvasGroup canvasGroup;

        // Instance variables
        ContextMenu sourceMenu;
        readonly List<GameObject> instantiatedItems = new();
        readonly Dictionary<ContextMenu.Item, ContextMenuSection> sectionInstances = new();

        void Awake()
        {
            if (canvasGroup == null) { canvasGroup = GetComponent<CanvasGroup>(); }
            if (itemContainer == null) { itemContainer = transform; }
            foreach (Transform t in itemContainer) { Destroy(t.gameObject); }
        }

        void OnDestroy()
        {
            foreach (var item in instantiatedItems)
            {
                if (item != null)
                {
                    Destroy(item);
                }
            }

            instantiatedItems.Clear();
            sectionInstances.Clear();
        }

        public void Setup(ContextMenu source, List<ContextMenu.Item> items)
        {
            sourceMenu = source;

            // Create items
            CreateMenuItems(items);

            // Force layout update
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
        }

        public void Setup(ContextMenu source, List<ContextMenu.SectionItem> sectionItems)
        {
            sourceMenu = source;
            CreateMenuItems(ConvertSectionItemsToItems(sectionItems));
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
        }

        public void CollapseAllSections(bool immediate = false)
        {
            foreach (var section in sectionInstances.Values)
            {
                if (section != null && section.IsExpanded)
                {
                    section.CollapseSubmenu(immediate);
                }
            }
        }

        void CreateMenuItems(List<ContextMenu.Item> items)
        {
            foreach (var item in items)
            {
                if (!item.IsActive)
                    continue;

                CreateMenuItem(item);
            }
        }

        GameObject CreateMenuItem(ContextMenu.Item item)
        {
            GameObject itemGO = null;

            switch (item.itemType)
            {
                case ContextMenu.Item.ItemType.Button:
                    itemGO = CreateButtonItem(item);
                    break;

                case ContextMenu.Item.ItemType.Separator:
                    itemGO = CreateSeparatorItem();
                    break;

                case ContextMenu.Item.ItemType.Section:
                    itemGO = CreateSectionItem(item);
                    break;

                case ContextMenu.Item.ItemType.CustomObject:
                    itemGO = CreateCustomItem(item);
                    break;
            }

            if (itemGO != null) { instantiatedItems.Add(itemGO); }
            return itemGO;
        }

        GameObject CreateButtonItem(ContextMenu.Item item)
        {
            if (buttonPrefab == null)
                return null;

            GameObject buttonGO = Instantiate(buttonPrefab, itemContainer);
            if (buttonGO.TryGetComponent<Button>(out var btn))
            {
                btn.onClick.AddListener(() => {
                    item.onClick?.Invoke();
                    if (sourceMenu != null) { sourceMenu.OnItemClicked(item); }
                });
                btn.SetText(item.itemName);
                btn.SetIcon(item.icon);
            }

            return buttonGO;
        }

        GameObject CreateSeparatorItem()
        {
            if (separatorPrefab == null)
                return null;

            GameObject separatorGO = Instantiate(separatorPrefab, itemContainer);
            return separatorGO;
        }

        GameObject CreateSectionItem(ContextMenu.Item item)
        {
            if (sectionPrefab == null)
                return null;

            GameObject sectionGO = Instantiate(sectionPrefab, itemContainer);
            if (sectionGO.TryGetComponent<ContextMenuSection>(out var sectionComponent))
            {
                sectionComponent.Setup(sourceMenu, item);
                sectionInstances[item] = sectionComponent;
            }

            return sectionGO;
        }

        GameObject CreateCustomItem(ContextMenu.Item item)
        {
            if (item.customPrefab == null)
                return null;

            GameObject customGO = Instantiate(item.customPrefab, itemContainer);
            if (customGO.TryGetComponent<Button>(out var customButton))
            {
                customButton.onClick.AddListener(() => {
                    item.onClick?.Invoke();
                    if (sourceMenu != null) { sourceMenu.OnItemClicked(item); }
                });
            }

            return customGO;
        }

        List<ContextMenu.Item> ConvertSectionItemsToItems(List<ContextMenu.SectionItem> sectionItems)
        {
            List<ContextMenu.Item> convertedItems = new();

            foreach (var sectionItem in sectionItems)
            {
                if (!sectionItem.IsActive)
                    continue;

                var item = new ContextMenu.Item
                {
                    itemName = sectionItem.itemName,
                    icon = sectionItem.icon,
                    onClick = sectionItem.onClick,
                    customPrefab = sectionItem.customPrefab,
                    itemType = sectionItem.itemType switch
                    {
                        ContextMenu.SectionItem.ItemType.Button => ContextMenu.Item.ItemType.Button,
                        ContextMenu.SectionItem.ItemType.Separator => ContextMenu.Item.ItemType.Separator,
                        ContextMenu.SectionItem.ItemType.CustomObject => ContextMenu.Item.ItemType.CustomObject,
                        _ => ContextMenu.Item.ItemType.Button
                    }
                };

                convertedItems.Add(item);
            }

            return convertedItems;
        }
    }
}