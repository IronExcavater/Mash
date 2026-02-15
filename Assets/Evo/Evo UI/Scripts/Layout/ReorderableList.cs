using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HELP_URL + "layout/reorderable-list")]
    [AddComponentMenu("Evo/UI/Layout/Reorderable List")]
    [RequireComponent(typeof(RectTransform))]
    public class ReorderableList : MonoBehaviour
    {
        [EvoHeader("Settings", Constants.CUSTOM_EDITOR_ID)]
        [SerializeField] private bool instantSnap = false;
        [SerializeField] private float itemSpacing = 10; // Used if there is no layout group attached
        [SerializeField, Range(0.05f, 2)] private float animationDuration = 0.3f;
        [SerializeField] private AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [EvoHeader("Drag Settings", Constants.CUSTOM_EDITOR_ID)]
        [Range(0.1f, 1)] public float dragAlpha = 1;
        [Range(1, 2)] public float dragScale = 1.2f;

        [EvoHeader("References", Constants.CUSTOM_EDITOR_ID)]
        [SerializeField] private RectTransform listContainer;
        [SerializeField] private Canvas canvas;

        [EvoHeader("Events", Constants.CUSTOM_EDITOR_ID)]
        public OrderChangedEvent onOrderChanged = new();

        // Cache
        Camera uiCamera;
        LayoutGroup layoutGroup;
        GridLayoutGroup gridLayout;
        ContentSizeFitter contentSizeFitter;
        ReorderableListItem draggedItem;
        Coroutine layoutCoroutine;

        readonly List<ReorderableListItem> items = new();
        readonly List<Coroutine> activeAnimations = new();

        // State Restoration for OnEnable
        ReorderableListItem pendingResortItem;
        int pendingResortIndex = -1;

        // Layout group alignment cache
        TextAnchor childAlignment = TextAnchor.MiddleCenter;
        RectOffset layoutPadding;

        // Helpers
        int draggedFromIndex = -1;
        int previewInsertIndex = -1;
        LayoutType layoutType = LayoutType.Horizontal;

        // Properties
        public DockMagnifier DockMagnifier { get; set; }
        public bool IsDraggingActive => draggedItem != null;
        public bool IsAnimating { get; private set; }

        public enum LayoutType { Horizontal, Vertical, Grid }

        [System.Serializable] public class OrderChangedEvent : UnityEvent<ReorderableListItem, int, int> { }

        void Awake()
        {
            // Set default refs if not assigned
            if (canvas == null) { canvas = GetComponentInParent<Canvas>(); }
            if (listContainer == null) { listContainer = GetComponent<RectTransform>(); }

            // Cache UI camera
            uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

            // Detect layout group and content size fitter
            layoutGroup = listContainer.GetComponent<LayoutGroup>();
            contentSizeFitter = listContainer.GetComponent<ContentSizeFitter>();
            gridLayout = listContainer.GetComponent<GridLayoutGroup>();

            if (layoutGroup != null)
            {
                if (layoutGroup is GridLayoutGroup)
                {
                    layoutType = LayoutType.Grid;
                    itemSpacing = 0; // Handled by grid cellSize/spacing
                }
                else if (layoutGroup is HorizontalLayoutGroup group)
                {
                    layoutType = LayoutType.Horizontal;
                    itemSpacing = group.spacing;
                }
                else
                {
                    layoutType = LayoutType.Vertical;
                    itemSpacing = ((VerticalLayoutGroup)layoutGroup).spacing;
                }

                childAlignment = layoutGroup.childAlignment;
                layoutPadding = layoutGroup.padding;
            }
            else
            {
                // Default to Horizontal if no group, or stick to configured defaults
                layoutType = LayoutType.Horizontal;
            }

            // Initialize padding
            layoutPadding ??= new RectOffset(0, 0, 0, 0);
        }

        void OnEnable()
        {
            RefreshItemsList();

            // If there's a pending restoration that was skipped in OnDisable (due to deactivation), apply it now
            if (pendingResortItem != null && pendingResortIndex >= 0 && pendingResortIndex < listContainer.childCount)
            {
                pendingResortItem.transform.SetSiblingIndex(pendingResortIndex);
                pendingResortItem = null;
                pendingResortIndex = -1;
            }
        }

        void OnDisable()
        {
            // Reset Drag State visuals and index
            if (draggedItem != null)
            {
                // Revert visual changes instantly
                draggedItem.transform.localScale = Vector3.one;
                draggedItem.canvasGroup.alpha = 1f;

                // Restore the item to its original sibling index
                if (draggedFromIndex >= 0 && draggedFromIndex < listContainer.childCount)
                {
                    if (gameObject.activeInHierarchy) { draggedItem.transform.SetSiblingIndex(draggedFromIndex); }
                    else
                    {
                        pendingResortItem = draggedItem;
                        pendingResortIndex = draggedFromIndex;
                    }
                }
            }

            // Stop all active routines so they don't try to run on disabled object
            StopAllActiveAnimations();
            if (layoutCoroutine != null) { StopCoroutine(layoutCoroutine); }

            // Reset internal state variables
            draggedItem = null;
            draggedFromIndex = -1;
            previewInsertIndex = -1;
            IsAnimating = false;

            // Force Layout Group back on so items aren't stuck in animation limbo
            if (layoutGroup != null) { layoutGroup.enabled = true; }
            if (contentSizeFitter != null) { contentSizeFitter.enabled = true; }
        }

        /// <summary>
        /// Adds an existing RectTransform items to the list.
        /// </summary>
        public void AddExistingItems(List<RectTransform> items)
        {
            foreach (var item in items)
            {
                item.SetParent(listContainer, false);
                item.gameObject.SetActive(true);
            }

            // Refresh internal list
            RefreshItemsList();

            // Sync with DockMagnifier if present
            if (DockMagnifier != null) { DockMagnifier.RefreshTargets(); }
        }

        /// <summary>
        /// Adds an existing RectTransform to the list.
        /// </summary>
        public void AddExistingItem(RectTransform item)
        {
            item.SetParent(listContainer, false);
            item.gameObject.SetActive(true);

            // Refresh internal list
            RefreshItemsList();

            // Sync with DockMagnifier if present
            if (DockMagnifier != null) { DockMagnifier.RefreshTargets(); }
        }

        /// <summary>
        /// Adds an existing object to the list.
        /// </summary>
        public void AddExistingItem(GameObject item)
        {
            AddExistingItem(item.GetComponent<RectTransform>());
        }

        /// <summary>
        /// Removes an item from the list and destroys its GameObject.
        /// </summary>
        public void RemoveItem(ReorderableListItem item)
        {
            if (items.Contains(item))
            {
                items.Remove(item);

                if (item != null && item.gameObject != null)
                {
                    // Unparent immediately
                    // This ensures DockMagnifier or other components don't 'see' this object 
                    // in their child loops while waiting for the Destroy() to actually happen.
                    item.transform.SetParent(null);
                    Destroy(item.gameObject);
                }

                if (gameObject.activeInHierarchy)
                {
                    StartCoroutine(DelayedLayoutRefresh());
                    if (DockMagnifier != null) { StartCoroutine(DelayedDockRefresh()); }
                }
                else
                {
                    // If inactive, don't start coroutines. 
                    // The layout will be fixed by RefreshItemsList() in OnEnable()
                    if (DockMagnifier != null) { DockMagnifier.RefreshTargets(); }
                }
            }
        }

        /// <summary>
        /// Removes a specific GameObject from the list and destroys it.
        /// </summary>
        public void RemoveItem(GameObject itemGo)
        {
            if (itemGo != null && itemGo.TryGetComponent<ReorderableListItem>(out var item))
            {
                RemoveItem(item);
            }
        }

        /// <summary>
        /// Clears all items from the list and destroys their GameObjects.
        /// </summary>
        public void ClearItems()
        {
            // Create a copy to iterate safely
            var itemsCopy = new List<ReorderableListItem>(items);
            foreach (var item in itemsCopy)
            {
                if (item != null && item.gameObject != null)
                {
                    // Same fix as RemoveItem: unparent immediately
                    item.transform.SetParent(null);
                    Destroy(item.gameObject);
                }
            }
            items.Clear();

            if (DockMagnifier != null)
            {
                if (gameObject.activeInHierarchy) { StartCoroutine(DelayedDockRefresh()); }
                else { DockMagnifier.RefreshTargets(); }
            }
        }

        IEnumerator DelayedDockRefresh()
        {
            yield return new WaitForEndOfFrame();
            if (DockMagnifier != null) { DockMagnifier.RefreshTargets(); }
        }

        void StopAllActiveAnimations()
        {
            for (int i = activeAnimations.Count - 1; i >= 0; i--)
            {
                if (activeAnimations[i] != null)
                {
                    StopCoroutine(activeAnimations[i]);
                }
            }
            activeAnimations.Clear();
        }

        public void ResetState()
        {
            draggedItem = null;
            draggedFromIndex = -1;
            previewInsertIndex = -1;
        }

        public void RefreshItemsList()
        {
            items.Clear();

            // Fetch childs
            for (int i = 0; i < listContainer.childCount; i++)
            {
                Transform child = listContainer.GetChild(i);
                if (!child.gameObject.activeInHierarchy) { continue; }
                if (!child.TryGetComponent<ReorderableListItem>(out var item)) { item = child.gameObject.AddComponent<ReorderableListItem>(); }
                item.Initialize(this);
                items.Add(item);
            }

            // Update drag indices if we're currently dragging
            if (draggedItem != null && items.Contains(draggedItem))
            {
                int newDraggedIndex = items.IndexOf(draggedItem);
                draggedFromIndex = newDraggedIndex;
                previewInsertIndex = Mathf.Clamp(previewInsertIndex, 0, items.Count - 1);
            }

            // Only start coroutine if active
            if (gameObject.activeInHierarchy) { StartCoroutine(DelayedLayoutRefresh()); }
        }

        public void RefreshLayout()
        {
            // Stop conflicting layout routines
            if (layoutCoroutine != null)
            {
                StopCoroutine(layoutCoroutine);
                IsAnimating = false;
            }

            if (!gameObject.activeInHierarchy)
                return;

            if (instantSnap)
            {
                if (draggedItem == null) { layoutCoroutine = StartCoroutine(SnapToNormalPositions()); }
                else { layoutCoroutine = StartCoroutine(SnapToPreviewPositions()); }
            }
            else
            {
                if (draggedItem == null) { layoutCoroutine = StartCoroutine(AnimateToNormalPositions()); }
                else { layoutCoroutine = StartCoroutine(AnimateToPreviewPositions()); }
            }
        }

        public void CancelDrag()
        {
            if (draggedItem == null) { return; }
            if (draggedFromIndex >= 0 && draggedFromIndex < listContainer.childCount)
            {
                // Restore index here as well for consistency
                draggedItem.transform.SetSiblingIndex(draggedFromIndex);
            }

            // Reset visual properties
            if (instantSnap)
            {
                draggedItem.transform.localScale = Vector3.one;
                draggedItem.canvasGroup.alpha = 1f;
            }
            else
            {
                if (gameObject.activeInHierarchy)
                {
                    StartCoroutine(AnimateScale(draggedItem.transform, Vector3.one, 0.2f));
                    StartCoroutine(AnimateFade(draggedItem.canvasGroup, 1f, 0.2f));
                }
                else
                {
                    draggedItem.transform.localScale = Vector3.one;
                    draggedItem.canvasGroup.alpha = 1f;
                }
            }

            // Reset state and refresh
            ResetState();
            RefreshItemsList();
            RefreshLayout();
        }

        public void OnItemBeginDrag(ReorderableListItem item)
        {
            if (item == null || !items.Contains(item))
                return;

            draggedItem = item;
            draggedFromIndex = items.IndexOf(item);
            previewInsertIndex = draggedFromIndex;

            // Store current position before any changes
            Vector3 currentPosition = item.transform.localPosition;

            // Immediately disable layout group and content size fitter to prevent fighting with manual positioning
            if (layoutGroup != null) { layoutGroup.enabled = false; }
            if (contentSizeFitter != null) { contentSizeFitter.enabled = false; }

            // Bring dragged item to front
            item.transform.SetAsLastSibling();

            // Immediately restore position to prevent flicker
            item.transform.localPosition = currentPosition;

            // Start drag animations
            if (instantSnap)
            {
                // Set scale and alpha instantly
                item.transform.localScale = Vector3.one * dragScale;
                item.canvasGroup.alpha = dragAlpha;
            }
            else
            {
                StartCoroutine(AnimateScale(item.transform, Vector3.one * dragScale, 0.1f));
                StartCoroutine(AnimateFade(item.canvasGroup, dragAlpha, 0.1f));
            }

            RefreshLayout();
        }

        public void OnItemDrag(ReorderableListItem item, Vector2 screenPosition, Vector2 offset)
        {
            if (item != draggedItem)
                return;

            // Convert screen position to local position
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(listContainer, screenPosition, uiCamera, out Vector2 localPoint))
            {
                localPoint += offset;

                if (layoutType == LayoutType.Grid)
                {
                    // Grid Layout: Allow 2D movement clamped to container
                    float itemWidth = (gridLayout != null) ? gridLayout.cellSize.x : GetItemSize(item.CachedRectTransform);
                    float itemHeight = (gridLayout != null) ? gridLayout.cellSize.y : GetItemSize(item.CachedRectTransform);

                    float containerWidth = listContainer.rect.width;
                    float containerHeight = listContainer.rect.height;

                    // Calculate min and max positions based on all items
                    float minX = -containerWidth * 0.5f + layoutPadding.left + itemWidth * 0.5f;
                    float maxX = containerWidth * 0.5f - layoutPadding.right - itemWidth * 0.5f;
                    float minY = -containerHeight * 0.5f + layoutPadding.bottom + itemHeight * 0.5f;
                    float maxY = containerHeight * 0.5f - layoutPadding.top - itemHeight * 0.5f;

                    // Clamp position to stay within bounds
                    float clampedX = Mathf.Clamp(localPoint.x, minX, maxX);
                    float clampedY = Mathf.Clamp(localPoint.y, minY, maxY);

                    item.transform.localPosition = new Vector3(clampedX, clampedY, 0);
                }
                else if (layoutType == LayoutType.Horizontal)
                {
                    // Calculate Y position based on vertical alignment
                    float yPos = 0f;
                    float containerHeight = listContainer.rect.height;
                    float itemHeight = GetItemSize(item.CachedRectTransform);

                    switch (childAlignment)
                    {
                        case TextAnchor.UpperLeft:
                        case TextAnchor.UpperCenter:
                        case TextAnchor.UpperRight:
                            yPos = containerHeight * 0.5f - layoutPadding.top - itemHeight * 0.5f;
                            break;
                        case TextAnchor.MiddleLeft:
                        case TextAnchor.MiddleCenter:
                        case TextAnchor.MiddleRight:
                            yPos = 0f; // Center
                            break;
                        case TextAnchor.LowerLeft:
                        case TextAnchor.LowerCenter:
                        case TextAnchor.LowerRight:
                            yPos = -containerHeight * 0.5f + layoutPadding.bottom + itemHeight * 0.5f;
                            break;
                    }

                    // Calculate bounds for horizontal dragging
                    float itemWidth = GetItemSize(item.CachedRectTransform);
                    float containerWidth = listContainer.rect.width;
                    float minPos = -containerWidth * 0.5f + layoutPadding.left + itemWidth * 0.5f;
                    float maxPos = containerWidth * 0.5f - layoutPadding.right - itemWidth * 0.5f;

                    // Clamp the X position to stay within bounds
                    float clampedX = Mathf.Clamp(localPoint.x, minPos, maxPos);

                    item.transform.localPosition = new Vector3(clampedX, yPos, 0);
                }
                else // Vertical
                {
                    // Calculate X position based on horizontal alignment
                    float xPos = 0f;
                    float containerWidth = listContainer.rect.width;
                    float itemWidth = GetItemSize(item.CachedRectTransform);

                    switch (childAlignment)
                    {
                        case TextAnchor.UpperLeft:
                        case TextAnchor.MiddleLeft:
                        case TextAnchor.LowerLeft:
                            xPos = -containerWidth * 0.5f + layoutPadding.left + itemWidth * 0.5f;
                            break;
                        case TextAnchor.UpperCenter:
                        case TextAnchor.MiddleCenter:
                        case TextAnchor.LowerCenter:
                            xPos = 0f;
                            break;
                        case TextAnchor.UpperRight:
                        case TextAnchor.MiddleRight:
                        case TextAnchor.LowerRight:
                            xPos = containerWidth * 0.5f - layoutPadding.right - itemWidth * 0.5f;
                            break;
                    }

                    // Calculate bounds for vertical dragging
                    float itemHeight = GetItemSize(item.CachedRectTransform);
                    float containerHeight = listContainer.rect.height;

                    // Calculate min and max positions based on all items
                    float maxPos = containerHeight * 0.5f - layoutPadding.top - itemHeight * 0.5f;
                    float minPos = -containerHeight * 0.5f + layoutPadding.bottom + itemHeight * 0.5f;

                    // Clamp the Y position to stay within bounds
                    float clampedY = Mathf.Clamp(localPoint.y, minPos, maxPos);
                    item.transform.localPosition = new Vector3(xPos, clampedY, 0);
                }
            }

            // Check for insertion point
            int newInsertIndex = GetInsertionIndex();
            if (newInsertIndex != previewInsertIndex)
            {
                previewInsertIndex = newInsertIndex;
                RefreshLayout();
            }
        }

        public void OnItemEndDrag(ReorderableListItem item)
        {
            if (item != draggedItem)
                return;

            // Store original index before changes
            int originalIndex = draggedFromIndex;

            // Clamp insert index to valid range
            int finalInsertIndex = Mathf.Clamp(previewInsertIndex, 0, items.Count - 1);

            // Remove from current position
            items.Remove(item);

            // Insert at final position
            if (finalInsertIndex >= 0 && finalInsertIndex <= items.Count)
            {
                items.Insert(finalInsertIndex, item);
                item.transform.SetSiblingIndex(finalInsertIndex);
            }

            // Reset visual properties (respect instant snap setting)
            if (instantSnap)
            {
                // Reset scale and alpha instantly
                item.transform.localScale = Vector3.one;
                item.canvasGroup.alpha = 1f;
            }
            else
            {
                StartCoroutine(AnimateScale(item.transform, Vector3.one, 0.2f));
                StartCoroutine(AnimateFade(item.canvasGroup, 1f, 0.2f));
            }

            // Fire event only if order actually changed
            if (originalIndex != finalInsertIndex) { onOrderChanged?.Invoke(item, originalIndex, finalInsertIndex); }

            // Reset drag state
            ResetState();
            RefreshLayout();
        }

        float CalculateStartPosition()
        {
            // Only used for Horizontal/Vertical layouts
            float totalSize = 0f;

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == null)
                    continue;

                RectTransform itemRect = items[i].CachedRectTransform;
                totalSize += GetItemSize(itemRect);
                if (i < items.Count - 1) { totalSize += itemSpacing; }
            }

            // Get container size
            float containerSize = (layoutType == LayoutType.Horizontal) ? listContainer.rect.width : listContainer.rect.height;

            // Calculate start position based on alignment
            float startPos = 0f;

            if (layoutType == LayoutType.Horizontal)
            {
                // Horizontal alignment
                switch (childAlignment)
                {
                    case TextAnchor.UpperLeft:
                    case TextAnchor.MiddleLeft:
                    case TextAnchor.LowerLeft:
                        startPos = -containerSize * 0.5f + layoutPadding.left;
                        break;
                    case TextAnchor.UpperCenter:
                    case TextAnchor.MiddleCenter:
                    case TextAnchor.LowerCenter:
                        startPos = -totalSize * 0.5f;
                        break;
                    case TextAnchor.UpperRight:
                    case TextAnchor.MiddleRight:
                    case TextAnchor.LowerRight:
                        startPos = containerSize * 0.5f - layoutPadding.right - totalSize;
                        break;
                }
            }
            else
            {
                // Vertical alignment
                switch (childAlignment)
                {
                    case TextAnchor.UpperLeft:
                    case TextAnchor.UpperCenter:
                    case TextAnchor.UpperRight:
                        startPos = containerSize * 0.5f - layoutPadding.top;
                        break;
                    case TextAnchor.MiddleLeft:
                    case TextAnchor.MiddleCenter:
                    case TextAnchor.MiddleRight:
                        startPos = totalSize * 0.5f;
                        break;
                    case TextAnchor.LowerLeft:
                    case TextAnchor.LowerCenter:
                    case TextAnchor.LowerRight:
                        startPos = -containerSize * 0.5f + layoutPadding.bottom + totalSize;
                        break;
                }
            }

            return startPos;
        }

        // This method checks DockMagnifier for the correct resting size
        float GetItemSize(RectTransform itemRect)
        {
            if (layoutType == LayoutType.Grid && gridLayout != null)
            {
                // Grids enforce cell size, typically
                return (gridLayout.startAxis == GridLayoutGroup.Axis.Horizontal) ? gridLayout.cellSize.x : gridLayout.cellSize.y;
            }

            if (DockMagnifier != null && DockMagnifier.isActiveAndEnabled)
            {
                Vector2 restingSize = DockMagnifier.GetRestingSize(itemRect);
                return (layoutType == LayoutType.Horizontal) ? restingSize.x : restingSize.y;
            }

            return (layoutType == LayoutType.Horizontal) ? itemRect.rect.width : itemRect.rect.height;
        }

        int GetInsertionIndex()
        {
            if (items.Count == 0 || draggedItem == null)
                return 0;

            if (layoutType == LayoutType.Grid)
            {
                // Grid Logic: Find closest index based on distance
                float minDistance = float.MaxValue;
                Vector3 draggedPos = draggedItem.transform.localPosition;
                int bestIndex = 0;

                // Simulate "If I put it at index K, where would it be?"
                // and compare draggedPos to that target position
                int totalItems = items.Count; // Total items including dragged
                for (int k = 0; k < totalItems; k++)
                {
                    Vector3 slotPos = GetGridPosition(k, totalItems);
                    float d = Vector3.SqrMagnitude(draggedPos - slotPos);
                    if (d < minDistance)
                    {
                        minDistance = d;
                        bestIndex = k;
                    }
                }
                return bestIndex;
            }

            // Get the dragged item's position and size
            float draggedItemSize = GetItemSize(draggedItem.CachedRectTransform);
            float draggedItemCenter = (layoutType == LayoutType.Horizontal) ? draggedItem.transform.localPosition.x : draggedItem.transform.localPosition.y;

            float startPos = CalculateStartPosition();
            float currentPos = startPos;

            // Calculate center points of each item
            for (int i = 0; i < items.Count; i++)
            {
                RectTransform itemRect = items[i].CachedRectTransform;
                float itemSize = GetItemSize(itemRect);

                // Skip the dragged item in position calculations
                if (items[i] == draggedItem)
                {
                    if (layoutType == LayoutType.Horizontal) { currentPos += itemSize + itemSpacing; }
                    else { currentPos -= itemSize + itemSpacing; }
                    continue;
                }

                // Calculate this item's center position
                float itemCenter = currentPos + ((layoutType == LayoutType.Horizontal) ? itemSize * 0.5f : -itemSize * 0.5f);

                // Determine which edge to check based on drag direction
                bool shouldInsertHere;
                if (layoutType == LayoutType.Horizontal)
                {
                    if (draggedFromIndex < i)
                    {
                        // Dragging left to right: use right edge of dragged item
                        float draggedRightEdge = draggedItemCenter + (draggedItemSize * 0.5f);
                        shouldInsertHere = draggedRightEdge < itemCenter;
                    }
                    else
                    {
                        // Dragging right to left: use left edge of dragged item
                        float draggedLeftEdge = draggedItemCenter - (draggedItemSize * 0.5f);
                        shouldInsertHere = draggedLeftEdge < itemCenter;
                    }
                }
                else
                {
                    if (draggedFromIndex < i)
                    {
                        // Dragging top to bottom: use bottom edge of dragged item
                        float draggedBottomEdge = draggedItemCenter - (draggedItemSize * 0.5f);
                        shouldInsertHere = draggedBottomEdge > itemCenter;
                    }
                    else
                    {
                        // Dragging bottom to top: use top edge of dragged item
                        float draggedTopEdge = draggedItemCenter + (draggedItemSize * 0.5f);
                        shouldInsertHere = draggedTopEdge > itemCenter;
                    }
                }

                if (shouldInsertHere)
                {
                    int insertIndex = i;
                    if (draggedFromIndex < i && draggedFromIndex >= 0) { insertIndex = i - 1; }
                    return Mathf.Clamp(insertIndex, 0, items.Count);
                }

                if (layoutType == LayoutType.Horizontal) { currentPos += itemSize + itemSpacing; }
                else { currentPos -= itemSize + itemSpacing; }
            }

            // If we're past all items, insert at the end
            int finalIndex = items.Count;
            if (draggedFromIndex >= 0) { finalIndex = items.Count - 1; }

            return Mathf.Clamp(finalIndex, 0, items.Count);
        }

        Vector3 GetLinearPosition(float currentPos, RectTransform itemRect)
        {
            float yPos = 0f;
            float xPos = 0f;

            if (layoutType == LayoutType.Horizontal)
            {
                // Use integrated size
                float itemWidth = GetItemSize(itemRect);
                xPos = currentPos + itemWidth * 0.5f;

                // Calculate Y position based on vertical alignment
                float containerHeight = listContainer.rect.height;
                float itemHeight = GetItemSize(itemRect); // Use integrated size for consistency

                switch (childAlignment)
                {
                    case TextAnchor.UpperLeft:
                    case TextAnchor.UpperCenter:
                    case TextAnchor.UpperRight:
                        yPos = containerHeight * 0.5f - layoutPadding.top - itemHeight * 0.5f;
                        break;
                    case TextAnchor.MiddleLeft:
                    case TextAnchor.MiddleCenter:
                    case TextAnchor.MiddleRight:
                        yPos = 0f; // Center
                        break;
                    case TextAnchor.LowerLeft:
                    case TextAnchor.LowerCenter:
                    case TextAnchor.LowerRight:
                        yPos = -containerHeight * 0.5f + layoutPadding.bottom + itemHeight * 0.5f;
                        break;
                }
                return new Vector3(xPos, yPos, 0);
            }
            else // Vertical
            {
                float itemHeight = GetItemSize(itemRect);
                yPos = currentPos - itemHeight * 0.5f;

                // Calculate X position based on horizontal alignment
                float containerWidth = listContainer.rect.width;
                float itemWidth = GetItemSize(itemRect);

                switch (childAlignment)
                {
                    case TextAnchor.UpperLeft:
                    case TextAnchor.MiddleLeft:
                    case TextAnchor.LowerLeft:
                        xPos = -containerWidth * 0.5f + layoutPadding.left + itemWidth * 0.5f;
                        break;
                    case TextAnchor.UpperCenter:
                    case TextAnchor.MiddleCenter:
                    case TextAnchor.LowerCenter:
                        xPos = 0f; // Center
                        break;
                    case TextAnchor.UpperRight:
                    case TextAnchor.MiddleRight:
                    case TextAnchor.LowerRight:
                        xPos = containerWidth * 0.5f - layoutPadding.right - itemWidth * 0.5f;
                        break;
                }
                return new Vector3(xPos, yPos, 0);
            }
        }

        Vector3 GetGridPosition(int index, int totalCount)
        {
            if (gridLayout == null)
                return Vector3.zero;

            // Extract Grid Settings
            int constraintCount = gridLayout.constraintCount;
            Vector2 cellSize = gridLayout.cellSize;
            Vector2 spacing = gridLayout.spacing;
            GridLayoutGroup.Constraint constraint = gridLayout.constraint;
            GridLayoutGroup.Corner startCorner = gridLayout.startCorner;
            GridLayoutGroup.Axis startAxis = gridLayout.startAxis;

            // Calculate Cells Per Line
            int cellCountX;
            int cellCountY;

            float width = listContainer.rect.width;
            float height = listContainer.rect.height;

            if (constraint == GridLayoutGroup.Constraint.FixedColumnCount)
            {
                cellCountX = constraintCount;
                cellCountY = Mathf.CeilToInt(totalCount / (float)cellCountX);
            }
            else if (constraint == GridLayoutGroup.Constraint.FixedRowCount)
            {
                cellCountY = constraintCount;
                cellCountX = Mathf.CeilToInt(totalCount / (float)cellCountY);
            }
            else // Flexible
            {
                if (startAxis == GridLayoutGroup.Axis.Horizontal)
                {
                    cellCountX = Mathf.FloorToInt((width - layoutPadding.horizontal + spacing.x + 0.001f) / (cellSize.x + spacing.x));
                    cellCountX = Mathf.Max(1, cellCountX);
                    cellCountY = Mathf.CeilToInt(totalCount / (float)cellCountX);
                }
                else
                {
                    cellCountY = Mathf.FloorToInt((height - layoutPadding.vertical + spacing.y + 0.001f) / (cellSize.y + spacing.y));
                    cellCountY = Mathf.Max(1, cellCountY);
                    cellCountX = Mathf.CeilToInt(totalCount / (float)cellCountY);
                }
            }

            // Calculate Row and Column for the specific index
            int row, col;
            if (startAxis == GridLayoutGroup.Axis.Horizontal)
            {
                col = index % cellCountX;
                row = index / cellCountX;
            }
            else
            {
                row = index % cellCountY;
                col = index / cellCountY;
            }

            // Total size of the grid content
            // Used for Alignment
            float totalGridWidth = cellCountX * cellSize.x + (cellCountX - 1) * spacing.x;
            float totalGridHeight = cellCountY * cellSize.y + (cellCountY - 1) * spacing.y;

            // Handle Start Corner (re-map col/row)
            if (startCorner == GridLayoutGroup.Corner.UpperRight || startCorner == GridLayoutGroup.Corner.LowerRight) { col = cellCountX - 1 - col; }
            if (startCorner == GridLayoutGroup.Corner.LowerLeft || startCorner == GridLayoutGroup.Corner.LowerRight) { row = cellCountY - 1 - row; }

            // Standardize to offsets from Top-Left of the content area
            float xPos = col * (cellSize.x + spacing.x) + cellSize.x * 0.5f;
            float yPos = row * (cellSize.y + spacing.y) + cellSize.y * 0.5f;

            // Invert Y because rows go down usually (unless Lower corner start)
            if (startCorner == GridLayoutGroup.Corner.UpperLeft || startCorner == GridLayoutGroup.Corner.UpperRight)
            {
                yPos = -yPos; // Go down
            }

            // Calculate Alignment Offset
            float startX;
            float startY;

            // Horizontal Alignment
            if (childAlignment == TextAnchor.UpperLeft || childAlignment == TextAnchor.MiddleLeft || childAlignment == TextAnchor.LowerLeft)
            {
                startX = -width * 0.5f + layoutPadding.left;
            }
            else if (childAlignment == TextAnchor.UpperCenter || childAlignment == TextAnchor.MiddleCenter || childAlignment == TextAnchor.LowerCenter)
            {
                startX = -totalGridWidth * 0.5f; // Center the grid block
            }
            else // Right
            {
                startX = width * 0.5f - layoutPadding.right - totalGridWidth;
            }

            // Vertical Alignment
            if (childAlignment == TextAnchor.UpperLeft || childAlignment == TextAnchor.UpperCenter || childAlignment == TextAnchor.UpperRight)
            {
                startY = height * 0.5f - layoutPadding.top;
            }
            else if (childAlignment == TextAnchor.MiddleLeft || childAlignment == TextAnchor.MiddleCenter || childAlignment == TextAnchor.MiddleRight)
            {
                startY = totalGridHeight * 0.5f;
            }
            else // Bottom
            {
                startY = -height * 0.5f + layoutPadding.bottom + totalGridHeight;
            }

            return new Vector3(startX + xPos, startY + yPos, 0);
        }

        IEnumerator DelayedLayoutRefresh()
        {
            yield return new WaitForEndOfFrame();
            RefreshLayout();
        }

        IEnumerator AnimateToNormalPositions()
        {
            IsAnimating = true;

            if (layoutGroup != null) { layoutGroup.enabled = false; }
            if (contentSizeFitter != null) { contentSizeFitter.enabled = false; }

            StopAllActiveAnimations();

            if (items.Count == 0)
            {
                if (layoutGroup != null) { layoutGroup.enabled = true; }
                if (contentSizeFitter != null) { contentSizeFitter.enabled = true; }
                IsAnimating = false;
                yield break;
            }

            // Calculate normal positions
            float currentPos = (layoutType == LayoutType.Grid) ? 0 : CalculateStartPosition();

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == null)
                    continue;

                RectTransform itemRect = items[i].CachedRectTransform;
                Vector3 targetPos;

                if (layoutType == LayoutType.Grid) { targetPos = GetGridPosition(i, items.Count); }
                else
                {
                    targetPos = GetLinearPosition(currentPos, itemRect);
                    if (layoutType == LayoutType.Horizontal) { currentPos += GetItemSize(itemRect) + itemSpacing; }
                    else { currentPos -= GetItemSize(itemRect) + itemSpacing; }
                }

                Coroutine anim = StartCoroutine(SmoothMove(items[i].transform, targetPos, animationDuration));
                activeAnimations.Add(anim);
            }

            yield return new WaitForSecondsRealtime(animationDuration);

            if (layoutGroup != null) { layoutGroup.enabled = true; }
            if (contentSizeFitter != null) { contentSizeFitter.enabled = true; }
            IsAnimating = false;
        }

        IEnumerator AnimateToPreviewPositions()
        {
            IsAnimating = true;

            if (layoutGroup != null) { layoutGroup.enabled = false; }
            if (contentSizeFitter != null) { contentSizeFitter.enabled = false; }

            StopAllActiveAnimations();

            if (items.Count == 0)
            {
                if (layoutGroup != null) { layoutGroup.enabled = true; }
                if (contentSizeFitter != null) { contentSizeFitter.enabled = true; }
                IsAnimating = false;
                yield break;
            }

            // Create preview list with dragged item inserted at preview position
            List<ReorderableListItem> previewItems = new(items);

            // Remove dragged item from its current position
            if (draggedItem != null && previewItems.Contains(draggedItem)) { previewItems.Remove(draggedItem); }

            // Insert at preview position
            if (draggedItem != null && previewInsertIndex >= 0 && previewInsertIndex <= previewItems.Count) { previewItems.Insert(previewInsertIndex, draggedItem); }

            float currentPos = (layoutType == LayoutType.Grid) ? 0 : CalculateStartPosition();

            // Animate all items except the dragged one to their preview positions
            for (int i = 0; i < previewItems.Count; i++)
            {
                if (previewItems[i] == null) { continue; }
                if (previewItems[i] == draggedItem)
                {
                    // Skip dragged item but reserve space
                    if (layoutType != LayoutType.Grid)
                    {
                        RectTransform draggedRect = draggedItem.CachedRectTransform;
                        if (layoutType == LayoutType.Horizontal) { currentPos += GetItemSize(draggedRect) + itemSpacing; }
                        else { currentPos -= GetItemSize(draggedRect) + itemSpacing; }
                    }
                    continue;
                }

                RectTransform itemRect = previewItems[i].CachedRectTransform;
                Vector3 targetPos;

                if (layoutType == LayoutType.Grid) { targetPos = GetGridPosition(i, previewItems.Count); }
                else
                {
                    targetPos = GetLinearPosition(currentPos, itemRect);
                    if (layoutType == LayoutType.Horizontal) { currentPos += GetItemSize(itemRect) + itemSpacing; }
                    else { currentPos -= GetItemSize(itemRect) + itemSpacing; }
                }

                Coroutine anim = StartCoroutine(SmoothMove(previewItems[i].transform, targetPos, animationDuration * 0.5f));
                activeAnimations.Add(anim);
            }

            yield return new WaitForSecondsRealtime(animationDuration * 0.5f);
            IsAnimating = false;
        }

        IEnumerator SnapToNormalPositions()
        {
            IsAnimating = true;

            if (layoutGroup != null) { layoutGroup.enabled = false; }
            if (contentSizeFitter != null) { contentSizeFitter.enabled = false; }

            StopAllActiveAnimations();

            if (items.Count == 0)
            {
                if (layoutGroup != null) { layoutGroup.enabled = true; }
                if (contentSizeFitter != null) { contentSizeFitter.enabled = true; }
                IsAnimating = false;
                yield break;
            }

            // Calculate and set positions instantly
            float currentPos = (layoutType == LayoutType.Grid) ? 0 : CalculateStartPosition();

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == null)
                    continue;

                RectTransform itemRect = items[i].CachedRectTransform;
                Vector3 targetPos;

                if (layoutType == LayoutType.Grid) { targetPos = GetGridPosition(i, items.Count); }
                else
                {
                    targetPos = GetLinearPosition(currentPos, itemRect);
                    if (layoutType == LayoutType.Horizontal) { currentPos += GetItemSize(itemRect) + itemSpacing; }
                    else { currentPos -= GetItemSize(itemRect) + itemSpacing; }
                }

                // Set position instantly
                items[i].transform.localPosition = targetPos;
            }

            // Wait one frame then re-enable layout components
            yield return null;

            if (layoutGroup != null) { layoutGroup.enabled = true; }
            if (contentSizeFitter != null) { contentSizeFitter.enabled = true; }
            IsAnimating = false;
        }

        IEnumerator SnapToPreviewPositions()
        {
            IsAnimating = true;

            if (layoutGroup != null) { layoutGroup.enabled = false; }
            if (contentSizeFitter != null) { contentSizeFitter.enabled = false; }

            StopAllActiveAnimations();

            if (items.Count == 0)
            {
                if (layoutGroup != null) { layoutGroup.enabled = true; }
                if (contentSizeFitter != null) { contentSizeFitter.enabled = true; }
                IsAnimating = false;
                yield break;
            }

            // Create preview list with dragged item inserted at preview position
            List<ReorderableListItem> previewItems = new(items);

            // Remove dragged item from its current position
            if (draggedItem != null && previewItems.Contains(draggedItem)) { previewItems.Remove(draggedItem); }

            // Insert at preview position
            if (draggedItem != null && previewInsertIndex >= 0 && previewInsertIndex <= previewItems.Count) { previewItems.Insert(previewInsertIndex, draggedItem); }

            float currentPos = (layoutType == LayoutType.Grid) ? 0 : CalculateStartPosition();

            // Set all positions instantly except the dragged item
            for (int i = 0; i < previewItems.Count; i++)
            {
                if (previewItems[i] == null) { continue; }
                if (previewItems[i] == draggedItem)
                {
                    // Skip dragged item but reserve space
                    if (layoutType != LayoutType.Grid)
                    {
                        RectTransform draggedRect = draggedItem.CachedRectTransform;
                        if (layoutType == LayoutType.Horizontal) { currentPos += GetItemSize(draggedRect) + itemSpacing; }
                        else { currentPos -= GetItemSize(draggedRect) + itemSpacing; }
                    }
                    continue;
                }

                RectTransform itemRect = previewItems[i].CachedRectTransform;
                Vector3 targetPos;

                if (layoutType == LayoutType.Grid) { targetPos = GetGridPosition(i, previewItems.Count); }
                else
                {
                    targetPos = GetLinearPosition(currentPos, itemRect);
                    if (layoutType == LayoutType.Horizontal) { currentPos += GetItemSize(itemRect) + itemSpacing; }
                    else { currentPos -= GetItemSize(itemRect) + itemSpacing; }
                }

                // Set position instantly
                previewItems[i].transform.localPosition = targetPos;
            }

            // Wait one frame
            yield return null;
            IsAnimating = false;
        }

        IEnumerator SmoothMove(Transform target, Vector3 destination, float duration)
        {
            if (target == null)
                yield break;

            Vector3 startPos = target.localPosition;
            float elapsed = 0f;
            float invDuration = 1f / duration; // Cache division

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed * invDuration;
                t = animationCurve.Evaluate(t);
                target.localPosition = Vector3.Lerp(startPos, destination, t);
                yield return null;
            }

            target.localPosition = destination;
        }

        IEnumerator AnimateScale(Transform target, Vector3 targetScale, float duration)
        {
            if (target == null)
                yield break;

            Vector3 startScale = target.localScale;
            Vector3 endScale = targetScale;
            float elapsed = 0f;
            float invDuration = 1f / duration;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed * invDuration;
                t = animationCurve.Evaluate(t);
                target.localScale = Vector3.Lerp(startScale, endScale, t);
                yield return null;
            }

            target.localScale = endScale;
        }

        IEnumerator AnimateFade(CanvasGroup target, float targetAlpha, float duration)
        {
            if (target == null)
                yield break;

            float startAlpha = target.alpha;
            float elapsed = 0f;
            float invDuration = 1f / duration;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed * invDuration;
                t = animationCurve.Evaluate(t);
                target.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                yield return null;
            }

            target.alpha = targetAlpha;
        }

        public bool IsDragging(ReorderableListItem item) => draggedItem == item;
        public List<ReorderableListItem> Items() => items;
    }

    [RequireComponent(typeof(CanvasGroup))]
    [RequireComponent(typeof(RectTransform))]
    public class ReorderableListItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        // References
        public CanvasGroup canvasGroup;
        public RectTransform CachedRectTransform { get; private set; }

        // Helpers
        bool isDragging;
        ReorderableList targetList;
        Vector2 dragOffset;

        void OnEnable()
        {
            if (targetList != null && !targetList.Items().Contains(this))
            {
                targetList.RefreshItemsList();
            }
        }

        void OnDisable()
        {
            if (targetList != null && targetList.gameObject.activeInHierarchy)
            {
                if (targetList.IsDragging(this)) { targetList.CancelDrag(); }
                else if (transform.parent != null && transform.parent.gameObject.activeInHierarchy)
                {
                    transform.SetAsLastSibling();
                    targetList.RefreshItemsList();
                    targetList.RefreshLayout();
                }
            }
        }

        public void Initialize(ReorderableList parentList)
        {
            targetList = parentList;
            CachedRectTransform = GetComponent<RectTransform>();
            canvasGroup = GetComponent<CanvasGroup>();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (targetList == null)
                return;

            isDragging = true;

            // Calculate offset between pointer and item position
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                transform.parent as RectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPointerPos);

            dragOffset = (Vector2)transform.localPosition - localPointerPos;
            targetList.OnItemBeginDrag(this);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isDragging || targetList == null)
                return;

            targetList.OnItemDrag(this, eventData.position, dragOffset);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!isDragging || targetList == null)
                return;

            isDragging = false;
            targetList.OnItemEndDrag(this);
        }
    }
}