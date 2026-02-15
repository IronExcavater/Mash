using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HELP_URL + "layout/radial-layout-group")]
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Evo/UI/Layout/Radial Layout Group")]
    public class RadialLayoutGroup : LayoutGroup
    {
        [EvoHeader("Settings", Constants.CUSTOM_EDITOR_ID)]
        [SerializeField, Range(0.1f, 2)] private float radiusScale = 1;
        [SerializeField] private Vector2 centerOffset = Vector2.zero;

        [EvoHeader("Angle Range", Constants.CUSTOM_EDITOR_ID)]
        [SerializeField, Range(0, 360)] private float startAngle = 0;
        [SerializeField, Range(0, 360)] private float endAngle = 360;
        [SerializeField] private bool clockwise = true;

        [EvoHeader("Distribution", Constants.CUSTOM_EDITOR_ID)]
        [SerializeField] private bool evenDistribution = true;
        [SerializeField] private float customSpacing = 0;

        [EvoHeader("Child Control", Constants.CUSTOM_EDITOR_ID)]
        [SerializeField] private bool faceCenter = false;
        [SerializeField] private bool controlChildSize = true;
        [SerializeField] private Vector2 childSize = new(50, 50);

        public float RadiusScale
        {
            get { return radiusScale; }
            set
            {
                float clampedValue = Mathf.Clamp(value, 0.1f, 2f);
                if (!Mathf.Approximately(radiusScale, clampedValue))
                {
                    radiusScale = clampedValue;
                    SetDirty();
                }
            }
        }

        public float Radius
        {
            get
            {
                // Calculate radius based on available space
                Rect rect = rectTransform.rect;
                float availableWidth = rect.width - padding.left - padding.right;
                float availableHeight = rect.height - padding.top - padding.bottom;
                float baseRadius = Mathf.Min(availableWidth, availableHeight) * 0.5f;
                return baseRadius * radiusScale;
            }
        }

        public float StartAngle
        {
            get { return startAngle; }
            set
            {
                if (!Mathf.Approximately(startAngle, value))
                {
                    startAngle = value;
                    SetDirty();
                }
            }
        }

        public float EndAngle
        {
            get { return endAngle; }
            set
            {
                if (!Mathf.Approximately(endAngle, value))
                {
                    endAngle = value;
                    SetDirty();
                }
            }
        }

        public bool Clockwise
        {
            get { return clockwise; }
            set
            {
                if (clockwise != value)
                {
                    clockwise = value;
                    SetDirty();
                }
            }
        }

        public bool EvenDistribution
        {
            get { return evenDistribution; }
            set
            {
                if (evenDistribution != value)
                {
                    evenDistribution = value;
                    SetDirty();
                }
            }
        }

        public float CustomSpacing
        {
            get { return customSpacing; }
            set
            {
                if (!Mathf.Approximately(customSpacing, value))
                {
                    customSpacing = value;
                    SetDirty();
                }
            }
        }

        public bool FaceCenter
        {
            get { return faceCenter; }
            set
            {
                if (faceCenter != value)
                {
                    faceCenter = value;
                    SetDirty();
                }
            }
        }

        public Vector2 CenterOffset
        {
            get { return centerOffset; }
            set
            {
                if (centerOffset != value)
                {
                    centerOffset = value;
                    SetDirty();
                }
            }
        }

        public bool ControlChildSize
        {
            get { return controlChildSize; }
            set
            {
                if (controlChildSize != value)
                {
                    controlChildSize = value;
                    SetDirty();
                }
            }
        }

        public Vector2 ChildSize
        {
            get { return childSize; }
            set
            {
                if (childSize != value)
                {
                    childSize = value;
                    SetDirty();
                }
            }
        }

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();

            float minSize = 0f;
            float preferredSize = 0f;
            float flexibleSize = 1f; // Allow flexible sizing

            if (rectChildren.Count > 0)
            {
                // Minimum size should accommodate at least some basic radius
                minSize = 100f + padding.left + padding.right;
                preferredSize = 200f + padding.left + padding.right;
            }

            SetLayoutInputForAxis(minSize, preferredSize, flexibleSize, 0);
        }

        public override void CalculateLayoutInputVertical()
        {
            float minSize = 0f;
            float preferredSize = 0f;
            float flexibleSize = 1f; // Allow flexible sizing

            if (rectChildren.Count > 0)
            {
                // Minimum size should accommodate at least some basic radius
                minSize = 100f + padding.top + padding.bottom;
                preferredSize = 200f + padding.top + padding.bottom;
            }

            SetLayoutInputForAxis(minSize, preferredSize, flexibleSize, 1);
        }

        public override void SetLayoutHorizontal()
        {
            SetChildrenAlongAxis();
        }

        public override void SetLayoutVertical()
        {
            SetChildrenAlongAxis();
        }

        void SetChildrenAlongAxis()
        {
            if (rectChildren.Count == 0)
                return;

            // Calculate center point with padding applied
            Rect rect = rectTransform.rect;
            Vector2 center = new Vector2(
                rect.x + padding.left + (rect.width - padding.left - padding.right) * 0.5f,
                rect.y + padding.bottom + (rect.height - padding.bottom - padding.top) * 0.5f
            ) + centerOffset;

            // Calculate angles for each child
            List<float> angles = CalculateAngles();

            for (int i = 0; i < rectChildren.Count; i++)
            {
                RectTransform child = rectChildren[i];
                float angle = angles[i];

                // Convert angle to radians
                float radians = angle * Mathf.Deg2Rad;

                // Calculate position on circle using the calculated radius
                Vector2 circlePosition = new(Mathf.Cos(radians) * Radius, Mathf.Sin(radians) * Radius);
                Vector2 targetPosition = center + circlePosition;

                // Set size if controlled
                if (controlChildSize)
                {
                    child.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, childSize.x);
                    child.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, childSize.y);
                }

                // Set anchor and pivot to center (middle center alignment)
                child.anchorMin = new Vector2(0.5f, 0.5f);
                child.anchorMax = new Vector2(0.5f, 0.5f);
                child.pivot = new Vector2(0.5f, 0.5f);

                // Set position - child center will align with circle position
                child.anchoredPosition = targetPosition;

                // Handle rotation for face center
                if (!faceCenter) { child.localRotation = Quaternion.identity; }
                else
                {
                    float rotationAngle = angle + (clockwise ? 180f : 0f);
                    child.localRotation = Quaternion.Euler(0, 0, rotationAngle);
                }
            }
        }

        List<float> CalculateAngles()
        {
            List<float> angles = new();
            int childCount = rectChildren.Count;

            if (childCount == 0) { return angles; }
            if (childCount == 1)
            {
                // Single child goes at start angle
                angles.Add(startAngle);
                return angles;
            }

            float totalAngle = endAngle - startAngle;

            // Handle wrapping around 360 degrees
            if (totalAngle < 0) { totalAngle += 360f; }
            if (evenDistribution)
            {
                // Distribute evenly across the arc
                float angleStep = totalAngle / (childCount == 1 ? 1 : childCount - 1);

                // Special case: if we're doing a full circle, distribute evenly without overlap
                if (Mathf.Approximately(totalAngle, 360f))
                {
                    angleStep = totalAngle / childCount;
                    for (int i = 0; i < childCount; i++)
                    {
                        float angle = startAngle + (angleStep * i);
                        angles.Add(clockwise ? angle : -angle);
                    }
                }
                else
                {
                    for (int i = 0; i < childCount; i++)
                    {
                        float angle = startAngle + (angleStep * i);
                        angles.Add(clockwise ? angle : -angle);
                    }
                }
            }
            else
            {
                // Use custom spacing
                float currentAngle = startAngle;
                for (int i = 0; i < childCount; i++)
                {
                    angles.Add(clockwise ? currentAngle : -currentAngle);
                    currentAngle += customSpacing * (clockwise ? 1 : -1);
                }
            }

            return angles;
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            // Clamp values
            radiusScale = Mathf.Clamp(radiusScale, 0.1f, 2f);
            childSize.x = Mathf.Max(0f, childSize.x);
            childSize.y = Mathf.Max(0f, childSize.y);
        }

        // Gizmos for visual feedback in editor
        void OnDrawGizmosSelected()
        {
            if (!enabled)
                return;

            // Calculate center point EXACTLY the same way as SetChildrenAlongAxis
            Rect rect = rectTransform.rect;
            Vector2 localCenter = new Vector2(
                rect.x + padding.left + (rect.width - padding.left - padding.right) * 0.5f,
                rect.y + padding.bottom + (rect.height - padding.bottom - padding.top) * 0.5f
            ) + centerOffset;

            // For UI, just add to transform.position - don't use TransformPoint
            Vector3 worldCenter = transform.position + (Vector3)localCenter;

            // Draw radius circle
            Gizmos.color = Color.yellow;
            DrawWireCircle(worldCenter, Radius, 32);

            // Draw arc indicators
            Gizmos.color = Color.cyan;
            float startRad = startAngle * Mathf.Deg2Rad;
            float endRad = endAngle * Mathf.Deg2Rad;

            Vector3 startPoint = worldCenter + new Vector3(Mathf.Cos(startRad), Mathf.Sin(startRad), 0) * Radius;
            Vector3 endPoint = worldCenter + new Vector3(Mathf.Cos(endRad), Mathf.Sin(endRad), 0) * Radius;

            Gizmos.DrawLine(worldCenter, startPoint);
            Gizmos.DrawLine(worldCenter, endPoint);

            // Draw center point
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(worldCenter, 2f);
        }

        private void DrawWireCircle(Vector3 center, float radius, int segments)
        {
            float angleStep = 360f / segments;
            Vector3 prevPoint = center + Vector3.right * radius;

            for (int i = 1; i <= segments; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 newPoint = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius;
                Gizmos.DrawLine(prevPoint, newPoint);
                prevPoint = newPoint;
            }
        }
#endif
    }
}