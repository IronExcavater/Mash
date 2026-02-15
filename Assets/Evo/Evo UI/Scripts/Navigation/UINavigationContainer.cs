using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HELP_URL + "navigation/ui-navigation")]
    [AddComponentMenu("Evo/UI/Navigation/UI Navigation Container")]
    public class UINavigationContainer : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("If true, automatically populates the list with child selectables when enabled.")]
        public bool autoFetchOnEnable = true;
        [Tooltip("If true, navigation will be clamped to objects within this container. You cannot navigate out via keyboard/controller.")]
        public bool restrictNavigation = true;

        [Header("References")]
        [Tooltip("If set, this object will be selected automatically when the container becomes enabled.")]
        public Selectable defaultSelection;
        [Tooltip("The object to select when this container is disabled, IF one of the container's elements was currently selected. Useful for returning focus to a main menu or previous screen.")]
        public Selectable fallbackSelection;

        [Header("Data")]
        [Tooltip("The list of objects allowed to be interacted with.")]
        public List<Selectable> interactiveElements = new();

        // Store original navigation data to restore it when this component is disabled
        readonly Dictionary<int, Navigation> originalNavigations = new();

        void OnEnable()
        {
            if (autoFetchOnEnable) { FetchInteractables(); }
            if (restrictNavigation) { StartCoroutine(ApplyNavigationRestrictionsRoutine()); }
            if (defaultSelection != null && defaultSelection.gameObject.activeInHierarchy) { StartCoroutine(SelectDefaultRoutine()); }
        }

        void OnDisable()
        {
            // Attempt to select the fallback before we restore navigation or lose context
            TrySelectFallback();
            if (restrictNavigation) { RestoreNavigation(); }
        }

        void TrySelectFallback()
        {
            if (fallbackSelection == null || EventSystem.current == null) { return; }
            if (!fallbackSelection.gameObject.activeInHierarchy || !fallbackSelection.interactable) { return; }

            // If nothing is selected, we don't need to do anything
            GameObject currentObj = EventSystem.current.currentSelectedGameObject;
            if (currentObj == null) { return; }

            // Check if the currently selected object is one of the elements managed by this container
            Selectable currentSel = currentObj.GetComponent<Selectable>();
            if (currentSel != null && interactiveElements.Contains(currentSel))
            {
                EventSystem.current.SetSelectedGameObject(fallbackSelection.gameObject);
            }
        }

        public void RestoreNavigation()
        {
            foreach (var sel in interactiveElements)
            {
                if (sel == null) { continue; }
                if (originalNavigations.TryGetValue(sel.GetInstanceID(), out Navigation originalNav)) { sel.navigation = originalNav; }
            }
            originalNavigations.Clear();
        }

        public void FetchInteractables()
        {
            interactiveElements.Clear();

            Selectable[] allSelectables = GetComponentsInChildren<Selectable>(true);
            foreach (var sel in allSelectables)
            {
                if (sel.gameObject == this.gameObject || sel.navigation.mode == Navigation.Mode.None || !sel.interactable)
                    continue;

                interactiveElements.Add(sel);
            }
        }

        public void ApplyNavigationRestrictions()
        {
            originalNavigations.Clear();

            foreach (var sel in interactiveElements)
            {
                if (sel == null)
                    continue;

                // Save original state
                originalNavigations[sel.GetInstanceID()] = sel.navigation;

                // Create new explicit navigation
                Navigation newNav = new()
                {
                    mode = Navigation.Mode.Explicit,

                    // Calculate connections
                    // Use the element's existing finding logic to see where it wants to go,
                    // then we check if that destination is allowed (inside our list).
                    selectOnUp = ValidateNeighbor(sel.FindSelectableOnUp()),
                    selectOnDown = ValidateNeighbor(sel.FindSelectableOnDown()),
                    selectOnLeft = ValidateNeighbor(sel.FindSelectableOnLeft()),
                    selectOnRight = ValidateNeighbor(sel.FindSelectableOnRight())
                };

                // Apply
                sel.navigation = newNav;
            }
        }

        IEnumerator SelectDefaultRoutine()
        {
            yield return null; // Wait one frame for EventSystem to be ready
            if (defaultSelection != null) { Utilities.SetSelectedObject(defaultSelection.gameObject); }
        }

        IEnumerator ApplyNavigationRestrictionsRoutine()
        {
            yield return new WaitForEndOfFrame(); // Wait for LayoutGroups to rebuild so FindSelectable works correctly
            ApplyNavigationRestrictions();
        }

        /// <summary>
        /// Helper to check if a potential neighbor is valid (inside the container).
        /// </summary>
        Selectable ValidateNeighbor(Selectable potentialNeighbor)
        {
            if (potentialNeighbor == null) { return null; }
            if (interactiveElements.Contains(potentialNeighbor)) { return potentialNeighbor; }
            return null;
        }

#if UNITY_EDITOR
        [UnityEngine.ContextMenu("Fetch Selectables")]
        void ContextFetch()
        {
            FetchInteractables();
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"Fetched {interactiveElements.Count} interactables.", this);
        }
#endif
    }
}