using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HELP_URL + "navigation/ui-navigation")]
    [AddComponentMenu("Evo/UI/Navigation/UI Navigation Fallback")]
    public class UINavigationFallback : MonoBehaviour
    {
        [Header("Settings")]
        public FallbackMode fallbackMode = FallbackMode.Nearest;

        [Header("References")]
        public Selectable specifiedSelectable;

        public enum FallbackMode
        {
            FirstActive = 0,
            Nearest = 1,
            Specified = 2
        }

        // Cache
        EventSystem eventSystem;
        GameObject lastSelected;
        Vector2 lastSelectedScreenPosition;

        void Start()
        {
            eventSystem = EventSystem.current;
        }

        void LateUpdate()
        {
            // Skip update if using Specified mode
            if (fallbackMode == FallbackMode.Specified)
                return;

            if (eventSystem.currentSelectedGameObject != null &&
                eventSystem.currentSelectedGameObject.activeInHierarchy)
            {
                lastSelected = eventSystem.currentSelectedGameObject;
                lastSelectedScreenPosition = lastSelected.transform.position;
            }

            if (eventSystem.currentSelectedGameObject == null ||
                !eventSystem.currentSelectedGameObject.activeInHierarchy)
            {
                SelectFallback();
            }
        }

        void OnDisable()
        {
            // Only handle fallback on disable if in Specified mode
            if (fallbackMode != FallbackMode.Specified)
                return;

            // Check if this object was selected when disabled
            if (eventSystem != null && eventSystem.currentSelectedGameObject == gameObject)
            {
                SelectSpecifiedFallback();
            }
        }

        void SelectSpecifiedFallback()
        {
            if (specifiedSelectable != null &&
                specifiedSelectable.gameObject.activeInHierarchy &&
                specifiedSelectable.IsInteractable() &&
                IsNavigable(specifiedSelectable))
            {
                specifiedSelectable.Select();
            }
            else
            {
                // Fallback to finding any active selectable if specified is unavailable
                Selectable toSelect = FindFirstActiveSelectable();
                if (toSelect != null) { toSelect.Select(); }
            }
        }

        void SelectFallback()
        {
            Selectable toSelect;

            if (fallbackMode == FallbackMode.Nearest) { toSelect = FindNearestSelectable(); }
            else { toSelect = FindFirstActiveSelectable(); }

            if (toSelect != null)
            {
                toSelect.Select();
            }
        }

        Selectable FindNearestSelectable()
        {
            Selectable nearest = null;
            float minDistance = float.MaxValue;

            foreach (var selectable in Selectable.allSelectablesArray)
            {
                if (selectable != null &&
                    selectable.gameObject != lastSelected &&
                    selectable.gameObject.activeInHierarchy &&
                    selectable.IsInteractable() &&
                    IsNavigable(selectable))
                {
                    Vector2 selectablePos = selectable.transform.position;
                    float distance = Vector2.Distance(lastSelectedScreenPosition, selectablePos);

                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        nearest = selectable;
                    }
                }
            }

            return nearest;
        }

        Selectable FindFirstActiveSelectable()
        {
            foreach (var selectable in Selectable.allSelectablesArray)
            {
                if (selectable != null &&
                    selectable.gameObject.activeInHierarchy &&
                    selectable.IsInteractable() &&
                    IsNavigable(selectable))
                {
                    return selectable;
                }
            }

            return null;
        }

        bool IsNavigable(Selectable selectable)
        {
            return selectable.navigation.mode != Navigation.Mode.None;
        }
    }
}