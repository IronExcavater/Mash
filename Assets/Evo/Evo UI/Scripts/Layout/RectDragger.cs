using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [HelpURL(Constants.HELP_URL + "layout/rect-dragger")]
    [AddComponentMenu("Evo/UI/Layout/Rect Dragger")]
    public class RectDragger : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [EvoHeader("Settings", Constants.CUSTOM_EDITOR_ID)]
        public bool isDraggable = true;

        [EvoHeader("Boundary", Constants.CUSTOM_EDITOR_ID)]
        public BoundaryType boundaryType = BoundaryType.ScreenBounds;
        public RectTransform boundaryRect;

        [EvoHeader("Smooth Return", Constants.CUSTOM_EDITOR_ID)]
        public bool allowOutOfBounds = false;
        [SerializeField] private float returnDuration = 0.25f;
        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField] private AnimationCurve returnCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [EvoHeader("Multiple Sources", Constants.CUSTOM_EDITOR_ID)]
        public List<RectTransform> dragSources = new();

        public enum BoundaryType
        {
            NoBounds,
            ScreenBounds,
            RectBounds
        }

        // Helpers
        bool isDragging;
        Canvas canvas;
        RectTransform rectTransform;
        Vector2 lastMousePosition;
        Vector2 originalPosition;

        void Start()
        {
            if (rectTransform == null) { rectTransform = GetComponent<RectTransform>(); }
            if (canvas == null) { canvas = GetComponentInParent<Canvas>(); }
            if (boundaryType == BoundaryType.RectBounds && boundaryRect == null && transform.parent != null) 
            {
                boundaryRect = transform.parent.GetComponent<RectTransform>(); 
            }
            if (dragSources != null && dragSources.Count > 0)
            {
                foreach (var target in dragSources)
                {
                    if (target != null && target != rectTransform)
                    {
                        var listener = target.gameObject.AddComponent<DragListener>();
                        listener.Initialize(this);
                    }
                }
            }

            originalPosition = rectTransform.anchoredPosition;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            // If targets are assigned, disable direct dragging of this object
            // (Unless this object is explicitly in the list)
            if (dragSources != null && dragSources.Count > 0 && !dragSources.Contains(rectTransform))
                return;

            HandlePointerDown(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (dragSources != null && dragSources.Count > 0 && !dragSources.Contains(rectTransform))
                return;

            HandleDrag(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (dragSources != null && dragSources.Count > 0 && !dragSources.Contains(rectTransform))
                return;

            HandlePointerUp(eventData);
        }

        public void HandlePointerDown(PointerEventData eventData)
        {
            if (!isDraggable)
                return;

            isDragging = true;

            RectTransform parentRect = rectTransform.parent as RectTransform;
            RectTransform referenceRect = parentRect != null ? parentRect : rectTransform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                referenceRect,
                eventData.position,
                eventData.pressEventCamera,
                out lastMousePosition
            );
        }

        public void HandleDrag(PointerEventData eventData)
        {
            if (!isDraggable || !isDragging)
                return;

            RectTransform parentRect = rectTransform.parent as RectTransform;
            RectTransform referenceRect = parentRect != null ? parentRect : rectTransform;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                referenceRect,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localMousePosition))
            {
                Vector2 deltaPosition = localMousePosition - lastMousePosition;
                Vector2 newPosition = rectTransform.anchoredPosition + deltaPosition;

                // Only constrain during drag if not allowing to go out of bounds
                if (boundaryType != BoundaryType.NoBounds && !allowOutOfBounds)
                {
                    Vector2 previousPosition = rectTransform.anchoredPosition;
                    newPosition = ConstrainToBounds(newPosition);

                    // If position was clamped, adjust lastMousePosition to match
                    if (newPosition != previousPosition + deltaPosition)
                    {
                        Vector2 actualDelta = newPosition - previousPosition;
                        lastMousePosition = localMousePosition - (deltaPosition - actualDelta);
                    }
                    else
                    {
                        lastMousePosition = localMousePosition;
                    }
                }
                else
                {
                    lastMousePosition = localMousePosition;
                }

                rectTransform.anchoredPosition = newPosition;
            }
        }

        public void HandlePointerUp(PointerEventData eventData)
        {
            isDragging = false;

            if (boundaryType != BoundaryType.NoBounds && !allowOutOfBounds)
            {
                rectTransform.anchoredPosition = ConstrainToBounds(rectTransform.anchoredPosition);
            }
            else if (boundaryType != BoundaryType.NoBounds && allowOutOfBounds)
            {
                Vector2 constrainedPosition = ConstrainToBounds(rectTransform.anchoredPosition);
                if (Vector2.Distance(rectTransform.anchoredPosition, constrainedPosition) > 0.1f)
                {
                    StartCoroutine(SmoothReturnToBounds(constrainedPosition));
                }
            }
        }

        Vector2 ConstrainToBounds(Vector2 position)
        {
            return boundaryType switch
            {
                BoundaryType.ScreenBounds => ConstrainToScreenBounds(position),
                BoundaryType.RectBounds => ConstrainToRectBounds(position),
                _ => position,
            };
        }

        Vector2 ConstrainToScreenBounds(Vector2 anchoredPos)
        {
            if (canvas == null)
                return anchoredPos;

            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            RectTransform parentRect = rectTransform.parent as RectTransform;
            if (canvasRect == null || parentRect == null) { return anchoredPos; }

            // Store current anchored position and set new one to test
            Vector2 originalPos = rectTransform.anchoredPosition;
            rectTransform.anchoredPosition = anchoredPos;

            // Work in local space
            Vector3 localPos = rectTransform.localPosition;

            // Get canvas rect in parent's local space
            Rect canvasRectLocal;

            // Parent is the canvas
            if (canvasRect == parentRect) { canvasRectLocal = canvasRect.rect; }
            else
            {
                // Canvas is higher in hierarchy
                Vector3[] canvasCorners = new Vector3[4];
                canvasRect.GetWorldCorners(canvasCorners);
               
                Vector2 min = parentRect.InverseTransformPoint(canvasCorners[0]);
                Vector2 max = parentRect.InverseTransformPoint(canvasCorners[2]);

                canvasRectLocal = new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
            }

            // Get source rect
            Rect sourceRect = rectTransform.rect;

            // Clamp using the proven logic
            Vector3 minPosition = canvasRectLocal.min - sourceRect.min;
            Vector3 maxPosition = canvasRectLocal.max - sourceRect.max;

            localPos.x = Mathf.Clamp(localPos.x, minPosition.x, maxPosition.x);
            localPos.y = Mathf.Clamp(localPos.y, minPosition.y, maxPosition.y);

            // Apply the clamped local position
            rectTransform.localPosition = localPos;

            // Read back the resulting anchored position
            Vector2 clampedAnchoredPos = rectTransform.anchoredPosition;

            // Restore original position before returning
            rectTransform.anchoredPosition = originalPos;

            return clampedAnchoredPos;
        }

        Vector2 ConstrainToRectBounds(Vector2 anchoredPos)
        {
            if (boundaryRect == null) 
                return anchoredPos;

            RectTransform parentRect = rectTransform.parent as RectTransform;
            if (parentRect == null) { return anchoredPos; }

            // Store current anchored position and set new one to test
            Vector2 originalPos = rectTransform.anchoredPosition;
            rectTransform.anchoredPosition = anchoredPos;

            // Now work in local space (which works consistently across all canvas modes)
            Vector3 localPos = rectTransform.localPosition;

            // Determine the boundary rect in parent's local space
            Rect boundaryRectLocal;

            // Parent is the boundary
            if (boundaryRect == parentRect) { boundaryRectLocal = boundaryRect.rect; }
            else
            {
                // Boundary is somewhere else in hierarchy
                // Transform boundary corners to parent's local space
                Vector3[] boundaryCorners = new Vector3[4];
                boundaryRect.GetWorldCorners(boundaryCorners);
               
                Vector2 min = parentRect.InverseTransformPoint(boundaryCorners[0]);
                Vector2 max = parentRect.InverseTransformPoint(boundaryCorners[2]);
                
                boundaryRectLocal = new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
            }

            // Get source rect
            Rect sourceRect = rectTransform.rect;

            // Clamp using the proven WindowDragger logic
            Vector3 minPosition = boundaryRectLocal.min - sourceRect.min;
            Vector3 maxPosition = boundaryRectLocal.max - sourceRect.max;

            localPos.x = Mathf.Clamp(localPos.x, minPosition.x, maxPosition.x);
            localPos.y = Mathf.Clamp(localPos.y, minPosition.y, maxPosition.y);

            // Apply the clamped local position
            rectTransform.localPosition = localPos;
           
            // Read back the resulting anchored position
            Vector2 clampedAnchoredPos = rectTransform.anchoredPosition;
            
            // Restore original position before returning
            rectTransform.anchoredPosition = originalPos;

            return clampedAnchoredPos;
        }

        IEnumerator SmoothReturnToBounds(Vector2 targetPosition)
        {
            Vector2 startPosition = rectTransform.anchoredPosition;
            float elapsedTime = 0f;

            while (elapsedTime < returnDuration)
            {
                elapsedTime += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float normalizedTime = Mathf.Clamp01(elapsedTime / returnDuration);
                float curveValue = returnCurve.Evaluate(normalizedTime);

                rectTransform.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, curveValue);
                yield return null;
            }

            rectTransform.anchoredPosition = targetPosition;
        }

        public void ResetToOriginalPosition()
        {
            if (allowOutOfBounds && boundaryType != BoundaryType.NoBounds) { StartCoroutine(SmoothReturnToBounds(originalPosition)); }
            else { rectTransform.anchoredPosition = originalPosition; }
        }

        // Helper class to forward events
        class DragListener : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
        {
            RectDragger _dragger;

            public void Initialize(RectDragger dragger) { _dragger = dragger; }
            public void OnPointerDown(PointerEventData eventData) => _dragger.HandlePointerDown(eventData);
            public void OnDrag(PointerEventData eventData) => _dragger.HandleDrag(eventData);
            public void OnPointerUp(PointerEventData eventData) => _dragger.HandlePointerUp(eventData);
        }

#if UNITY_EDITOR
        [HideInInspector] public bool settingsFoldout = true;
        [HideInInspector] public bool referencesFoldout = false;
#endif
    }
}