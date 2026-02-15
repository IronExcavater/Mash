using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HELP_URL + "layout/scroll-rect")]
    [AddComponentMenu("Evo/UI/Layout/Scroll Rect")]
    public class ScrollRect : UnityEngine.UI.ScrollRect
    {
        [EvoHeader("Snapping", Constants.CUSTOM_EDITOR_ID)]
        public bool enableSnapping = false;
        [Range(0.05f, 2f)] public float snapDuration = 0.3f;
        public AnimationCurve snapCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private bool disableUnfocused = false;

        [EvoHeader("Scaling", Constants.CUSTOM_EDITOR_ID)]
        public bool enableScaling = false;
        [Range(0, 1)] public float minScale = 0.7f;
        public float scaleDistance = 200;

        [EvoHeader("Fading", Constants.CUSTOM_EDITOR_ID)]
        public bool enableFading = false;
        [Range(0, 1)] public float minAlpha = 0.3f;
        public float fadeDistance = 150;

        [EvoHeader("Events", Constants.CUSTOM_EDITOR_ID)]
        public UnityEvent<int> onItemFocused = new();

        // Constants
        const float MOVEMENT_THRESHOLD = 0.01f;

        // Helpers
        readonly List<ItemData> items = new();
        readonly int startingIndex = 0;
        int focusedIndex = -1;
        int lastFocusedIndex = -1;
        bool isDragging;
        bool isSnapping;
        Vector2 snapTarget;
        Vector2 snapStartPosition;
        float snapStartTime;
        Vector2 lastContentPosition;

        // Cached values
        bool hasVisualEffects;
        float invScaleDistance;
        float invFadeDistance;
        Coroutine snapCoroutine;

        private class ItemData
        {
            public RectTransform rectTransform;
            public CanvasGroup canvasGroup;
            public float distance;
            public float distReposition;
        }

        protected override void Awake()
        {
            base.Awake();
            if (Application.isPlaying)
            {
                InitializeEnhancedFeatures();
                CacheInverseDistances();
            }
        }

        protected override void Start()
        {
            base.Start();
            if (Application.isPlaying)
            {
                RefreshItems();
                if (enableSnapping && items.Count > 0 && startingIndex >= 0 && startingIndex < items.Count)
                {
                    inertia = false;
                    movementType = MovementType.Unrestricted;
                    StartCoroutine(SnapToStartingElement());
                }
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (snapCoroutine != null)
            {
                StopCoroutine(snapCoroutine);
                snapCoroutine = null;
            }
        }

        void InitializeEnhancedFeatures()
        {
            var eventTrigger = gameObject.GetComponent<UnityEngine.EventSystems.EventTrigger>();
            if (!eventTrigger)
            {
                eventTrigger = gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            }
            else
            {
                eventTrigger.triggers.Clear();
            }

            // Begin drag
            var beginDragEntry = new UnityEngine.EventSystems.EventTrigger.Entry
            {
                eventID = UnityEngine.EventSystems.EventTriggerType.BeginDrag
            };
            beginDragEntry.callback.AddListener((data) => { OnBeginDragInternal(); });
            eventTrigger.triggers.Add(beginDragEntry);

            // End drag
            var endDragEntry = new UnityEngine.EventSystems.EventTrigger.Entry
            {
                eventID = UnityEngine.EventSystems.EventTriggerType.EndDrag
            };
            endDragEntry.callback.AddListener((data) => { OnEndDragInternal(); });
            eventTrigger.triggers.Add(endDragEntry);

            onValueChanged.AddListener(OnScrollValueChanged);
        }

        void CacheInverseDistances()
        {
            invScaleDistance = scaleDistance > 0 ? 1f / scaleDistance : 0f;
            invFadeDistance = fadeDistance > 0 ? 1f / fadeDistance : 0f;
            hasVisualEffects = enableFading || enableScaling;
        }

        void OnBeginDragInternal()
        {
            isDragging = true;
            StopSnapCoroutine();
        }

        void OnEndDragInternal()
        {
            isDragging = false;
            if (enableSnapping)
            {
                StartSnapCoroutine();
            }
        }

        void OnScrollValueChanged(Vector2 position)
        {
            // Only update if content has moved significantly
            if (Vector2.SqrMagnitude(content.anchoredPosition - lastContentPosition) > MOVEMENT_THRESHOLD)
            {
                UpdateItemStates();
                lastContentPosition = content.anchoredPosition;
            }
        }

        void StartSnapCoroutine()
        {
            snapCoroutine ??= StartCoroutine(HandleSnappingCoroutine());
        }

        void StopSnapCoroutine()
        {
            if (snapCoroutine != null)
            {
                StopCoroutine(snapCoroutine);
                snapCoroutine = null;
            }
        }

        IEnumerator SnapToStartingElement()
        {
            yield return new WaitForEndOfFrame();
            SnapToElementInstant(startingIndex);
        }

        IEnumerator HandleSnappingCoroutine()
        {
            if (!enableSnapping || items.Count == 0)
            {
                snapCoroutine = null;
                yield break;
            }

            // Handle external snapping (smooth snap to specific element)
            if (isSnapping)
            {
                snapStartTime = Time.unscaledTime;
                snapStartPosition = content.anchoredPosition;

                while (isSnapping)
                {
                    float elapsed = Time.unscaledTime - snapStartTime;
                    float t = Mathf.Clamp01(elapsed / snapDuration);
                    float curveValue = snapCurve.Evaluate(t);

                    content.anchoredPosition = Vector2.Lerp(
                        snapStartPosition,
                        snapTarget,
                        curveValue
                    );

                    if (t >= 1f)
                    {
                        content.anchoredPosition = snapTarget;
                        isSnapping = false;
                    }

                    UpdateItemStates();
                    yield return null;
                }
            }
            // Regular snapping to nearest element after drag
            else
            {
                yield return null;

                if (focusedIndex < 0 || focusedIndex >= items.Count)
                {
                    snapCoroutine = null;
                    yield break;
                }

                var item = items[focusedIndex];
                float targetPosition = vertical ?
                    content.anchoredPosition.y + item.distReposition :
                    content.anchoredPosition.x + item.distReposition;

                Vector2 startPosition = content.anchoredPosition;
                Vector2 endPosition = vertical ?
                    new Vector2(content.anchoredPosition.x, targetPosition) :
                    new Vector2(targetPosition, content.anchoredPosition.y);

                snapStartTime = Time.unscaledTime;

                while (true)
                {
                    float elapsed = Time.unscaledTime - snapStartTime;
                    float t = Mathf.Clamp01(elapsed / snapDuration);
                    float curveValue = snapCurve.Evaluate(t);

                    content.anchoredPosition = Vector2.Lerp(
                        startPosition,
                        endPosition,
                        curveValue
                    );

                    UpdateItemStates();

                    if (t >= 1f)
                    {
                        content.anchoredPosition = endPosition;
                        break;
                    }

                    yield return null;
                }
            }

            snapCoroutine = null;
        }

        void UpdateItemStates()
        {
            if (items.Count == 0 || viewport == null)
                return;

            // Get viewport center in world space first
            Vector3 viewportWorldCenter = viewport.TransformPoint(viewport.rect.center);
            float minDistance = float.MaxValue;
            int newFocusedIndex = -1;

            // Single loop to calculate distances and find minimum
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];

                // Transform.position uses the pivot, but we need the center
                Vector3 itemWorldCenter = item.rectTransform.TransformPoint(item.rectTransform.rect.center);

                // Convert both to content's local space for accurate distance calculation
                Vector2 viewportLocalPos = content.InverseTransformPoint(viewportWorldCenter);
                Vector2 itemLocalCenter = content.InverseTransformPoint(itemWorldCenter);

                // Calculate distance based on orientation
                if (vertical) { item.distReposition = viewportLocalPos.y - itemLocalCenter.y; }
                else { item.distReposition = viewportLocalPos.x - itemLocalCenter.x; }

                item.distance = Mathf.Abs(item.distReposition);

                // Find minimum distance in same loop
                if (item.distance < minDistance)
                {
                    minDistance = item.distance;
                    newFocusedIndex = i;
                }
            }

            // Apply visual effects only if enabled
            if (hasVisualEffects || disableUnfocused)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    bool isFocused = (i == newFocusedIndex);

                    // Apply fading
                    if (enableFading)
                    {
                        ApplyFadeEffect(item, item.distance);
                    }

                    // Apply scaling
                    if (enableScaling)
                    {
                        float normalizedDistance = Mathf.Clamp01(item.distance * invScaleDistance);
                        float scale = Mathf.Lerp(1f, minScale, normalizedDistance);
                        item.rectTransform.localScale = new Vector3(scale, scale, scale);
                    }

                    // Set interactability
                    if (disableUnfocused && item.canvasGroup != null)
                    {
                        item.canvasGroup.interactable = isFocused;
                    }
                }
            }

            // Update focused index
            if (!isSnapping)
            {
                focusedIndex = newFocusedIndex;
            }

            // Trigger event only when focus changes and not dragging
            if (!isDragging && focusedIndex != lastFocusedIndex && focusedIndex >= 0)
            {
                lastFocusedIndex = focusedIndex;
                onItemFocused?.Invoke(focusedIndex);
            }
        }

        void ApplyFadeEffect(ItemData item, float distance)
        {
            if (item.canvasGroup != null)
            {
                float normalizedDistance = Mathf.Clamp01(distance * invFadeDistance);
                item.canvasGroup.alpha = Mathf.Lerp(1f, minAlpha, normalizedDistance);
            }
        }

        public void RefreshItems()
        {
            items.Clear();

            if (content == null) { return; }
            for (int i = 0; i < content.childCount; i++)
            {
                var child = content.GetChild(i);
                if (child.gameObject.activeInHierarchy)
                {
                    var rectTransform = child.GetComponent<RectTransform>();
                    if (rectTransform)
                    {
                        var itemData = new ItemData
                        {
                            rectTransform = rectTransform
                        };

                        // Pre-cache or create CanvasGroup if visual effects are enabled
                        if (enableFading || (enableScaling && disableUnfocused))
                        {
                            itemData.canvasGroup = rectTransform.GetComponent<CanvasGroup>();
                            if (itemData.canvasGroup == null)
                            {
                                itemData.canvasGroup = rectTransform.gameObject.AddComponent<CanvasGroup>();
                            }
                        }

                        items.Add(itemData);
                    }
                }
            }

            CacheInverseDistances();
            lastContentPosition = content.anchoredPosition;

            // Apply initial visual effects after refreshing
            if (Application.isPlaying && items.Count > 0)
            {
                StartCoroutine(ApplyInitialEffects());
            }
        }

        IEnumerator ApplyInitialEffects()
        {
            yield return new WaitForEndOfFrame();
            UpdateItemStates();
        }

        public void AddTransform(RectTransform rectTransform)
        {
            if (rectTransform == null) return;

            var itemData = new ItemData
            {
                rectTransform = rectTransform
            };

            if (enableFading || disableUnfocused)
            {
                itemData.canvasGroup = rectTransform.GetComponent<CanvasGroup>();
                if (itemData.canvasGroup == null)
                {
                    itemData.canvasGroup = rectTransform.gameObject.AddComponent<CanvasGroup>();
                }
            }

            items.Add(itemData);
        }

        public void InstantiateObject(GameObject rectObject, Transform parent)
        {
            if (rectObject == null)
                return;

            GameObject createdObj = Instantiate(rectObject, parent);
            var rectTransform = createdObj.GetComponent<RectTransform>();
            if (rectTransform) { AddTransform(rectTransform); }
        }

        public void SnapToElement(int index, float offset = 0)
        {
            if (!enableSnapping || index < 0 || index >= items.Count || viewport == null)
                return;

            var item = items[index];

            // Store current content position
            Vector2 currentContentPos = content.anchoredPosition;

            // Temporarily set content to zero to get absolute positions
            content.anchoredPosition = Vector2.zero;

            // Get viewport center in world space
            Vector3 viewportWorldCenter = viewport.TransformPoint(viewport.rect.center);

            // Get item center in world space
            Vector3 itemWorldCenter = item.rectTransform.TransformPoint(item.rectTransform.rect.center);

            // Convert to content local space
            Vector2 viewportLocalPos = content.InverseTransformPoint(viewportWorldCenter);
            Vector2 itemLocalCenter = content.InverseTransformPoint(itemWorldCenter);

            // Calculate the absolute offset needed
            Vector2 calculatedOffset = viewportLocalPos - itemLocalCenter;

            if (vertical) { snapTarget = new Vector2(currentContentPos.x, calculatedOffset.y + offset); }
            else { snapTarget = new Vector2(calculatedOffset.x + offset, currentContentPos.y); }

            // Restore original position before starting animation
            content.anchoredPosition = currentContentPos;

            isSnapping = true;
            focusedIndex = index;

            StopSnapCoroutine();
            StartSnapCoroutine();
        }

        public void SnapToElementInstant(int index, float offset = 0)
        {
            if (index < 0 || index >= items.Count || viewport == null)
                return;

            var item = items[index];

            // Store current content position
            Vector2 currentContentPos = content.anchoredPosition;

            // Temporarily set content to zero to get absolute positions
            content.anchoredPosition = Vector2.zero;

            // Get viewport center in world space
            Vector3 viewportWorldCenter = viewport.TransformPoint(viewport.rect.center);

            // Get item center in world space
            Vector3 itemWorldCenter = item.rectTransform.TransformPoint(item.rectTransform.rect.center);

            // Convert to content local space
            Vector2 viewportLocalPos = content.InverseTransformPoint(viewportWorldCenter);
            Vector2 itemLocalCenter = content.InverseTransformPoint(itemWorldCenter);

            // Calculate the absolute offset needed
            Vector2 calculatedOffset = viewportLocalPos - itemLocalCenter;

            if (vertical) { content.anchoredPosition = new Vector2(currentContentPos.x, calculatedOffset.y + offset); }
            else { content.anchoredPosition = new Vector2(calculatedOffset.x + offset, currentContentPos.y); }

            focusedIndex = index;
            isSnapping = false;
            lastContentPosition = content.anchoredPosition;
            UpdateItemStates();
        }

        public void SnapToElement(RectTransform targetRect)
        {
            if (!targetRect) { return; }
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].rectTransform == targetRect)
                {
                    SnapToElement(i);
                    return;
                }
            }
        }

        public void ScrollToNext()
        {
            if (enableSnapping && focusedIndex < items.Count - 1)
            {
                SnapToElement(focusedIndex + 1);
            }
        }

        public void ScrollToPrevious()
        {
            if (enableSnapping && focusedIndex > 0)
            {
                SnapToElement(focusedIndex - 1);
            }
        }

#if UNITY_EDITOR
        [HideInInspector] public bool settingsFoldout = true;
        [HideInInspector] public bool referencesFoldout = true;
        [HideInInspector] public bool styleFoldout = true;
        [HideInInspector] public bool eventsFoldout = false;
#endif
    }
}