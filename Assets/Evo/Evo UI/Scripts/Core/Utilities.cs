using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace Evo.UI
{
    public class Utilities
    {
        #region Animation
        /// <summary>
        /// Smoothly cross fade graphic color to target color.
        /// </summary>
        public static IEnumerator CrossFadeGraphic(Graphic graphic, Color targetColor, float duration, bool unscaledTime = true, bool destroyOnCull = false)
        {
            if (graphic == null) { yield break; }
            if (duration <= 0f)
            {
                graphic.color = targetColor;
                yield break;
            }

            Color startColor = graphic.color;

            Vector4 startP = new(
                startColor.r * startColor.a,
                startColor.g * startColor.a,
                startColor.b * startColor.a,
                startColor.a
            );
            Vector4 targetP = new(
                targetColor.r * targetColor.a,
                targetColor.g * targetColor.a,
                targetColor.b * targetColor.a,
                targetColor.a
            );

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = 1f - Mathf.Pow(1f - t, 2f); // Apply easing

                Vector4 lerped = Vector4.Lerp(startP, targetP, t);

                float a = lerped.w;
                graphic.color = a > 0f
                    ? new(lerped.x / a, lerped.y / a, lerped.z / a, a)
                    : new(0f, 0f, 0f, 0f);

                yield return null;
            }

            graphic.color = targetColor;
            if (destroyOnCull && graphic.color.a == 0) { GameObject.Destroy(graphic.gameObject); }
        }

        /// <summary>
        /// Smoothly cross fade graphic color to target color.
        /// </summary>
        public static IEnumerator CrossFadeAlpha(Graphic graphic, float targetAlpha, float duration, bool unscaledTime = true, bool destroyOnCull = false)
        {
            if (graphic == null || duration <= 0f)
            {
                if (graphic != null) { graphic.color = new Color(graphic.color.r, graphic.color.g, graphic.color.b, targetAlpha); }
                yield break;
            }

            float startAlpha = graphic.color.a;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = 1f - Mathf.Pow(1f - t, 2f); // Apply easing

                float alphaOut = Mathf.Lerp(startAlpha, targetAlpha, t);
                graphic.color = new Color(graphic.color.r, graphic.color.g, graphic.color.b, alphaOut);

                yield return null;
            }

            graphic.color = new Color(graphic.color.r, graphic.color.g, graphic.color.b, targetAlpha);
            if (destroyOnCull && graphic.color.a == 0) { GameObject.Destroy(graphic.gameObject); }
        }

        /// <summary>
        /// Animate a single CanvasGroup to a target alpha value.
        /// </summary>
        public static IEnumerator CrossFadeCanvasGroup(CanvasGroup canvasGroup, float targetAlpha, float duration, bool unscaledTime = true)
        {
            if (canvasGroup == null || duration <= 0f)
            {
                if (canvasGroup != null) { canvasGroup.alpha = targetAlpha; }
                yield break;
            }

            float startAlpha = canvasGroup.alpha;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = 1f - Mathf.Pow(1f - t, 2f); // Apply easing
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                yield return null;
            }

            canvasGroup.alpha = targetAlpha;
        }

        /// <summary>
        /// Animate multiple CanvasGroups to target alpha values.
        /// </summary>
        public static IEnumerator CrossFadeCanvasGroup(Dictionary<CanvasGroup, float> targets, float duration, bool unscaledTime = true)
        {
            // Filter out null canvas groups upfront
            var validTargets = new Dictionary<CanvasGroup, float>();
            foreach (var kvp in targets)
            {
                if (kvp.Key != null)
                {
                    validTargets[kvp.Key] = kvp.Value;
                }
            }

            // Early exit if no valid targets
            if (validTargets.Count == 0)
                yield break;

            // Instant transition if duration is zero or negative
            if (duration <= 0f)
            {
                foreach (var kvp in validTargets) { kvp.Key.alpha = kvp.Value; }
                yield break;
            }

            // Store starting alpha values
            var starts = new Dictionary<CanvasGroup, float>(validTargets.Count);
            foreach (var kvp in validTargets) { starts[kvp.Key] = kvp.Key.alpha; }

            // Animate
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = 1f - Mathf.Pow(1f - t, 2f); // Apply easing
                foreach (var kvp in validTargets) { kvp.Key.alpha = Mathf.Lerp(starts[kvp.Key], kvp.Value, t); }
                yield return null;
            }

            // Ensure final state
            foreach (var kvp in validTargets)
            {
                kvp.Key.alpha = kvp.Value;
            }
        }

        /// <summary>
        /// Return the length of the specified clip.
        /// </summary>
        public static float GetAnimationClipLength(Animator animator, string clipName)
        {
            RuntimeAnimatorController ac = animator.runtimeAnimatorController;

            foreach (AnimationClip clip in ac.animationClips)
            {
                if (clip.name == clipName)
                {
                    return clip.length;
                }
            }

            return 0f;
        }
        #endregion

        #region Input
        /// <summary>
        /// Returns the current mouse position based on the active input handler.
        /// </summary>
        public static Vector2 GetPointerPosition()
        {
#if ENABLE_INPUT_SYSTEM
            return Pointer.current?.position.value ?? Vector2.zero;
#else
            return Input.mousePosition;
#endif
        }

        /// <summary>
        /// Checks if the current mouse was pressed based on the active input handler.
        /// </summary>
        public static bool WasPointerPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return (Pointer.current?.press.wasPressedThisFrame ?? false) || (Mouse.current?.rightButton.wasPressedThisFrame ?? false);
#else
            return Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1);
#endif
        }

        /// <summary>
        /// Checks if the specified mouse key was pressed based on the active input handler.
        /// </summary>
        public static bool WasMouseKeyPressed(int targetButton)
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null) { return false; }
            return targetButton switch
            {
                0 => mouse.leftButton.wasPressedThisFrame,
                1 => mouse.rightButton.wasPressedThisFrame,
                2 => mouse.middleButton.wasPressedThisFrame,
                _ => false
            };
#else
            return Input.GetMouseButtonDown(targetButton);
#endif
        }

        /// <summary>
        /// Checks if the ESC key was pressed based on the active input handler.
        /// </summary>
        public static bool WasEscapeKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current?.escapeKey.wasPressedThisFrame ?? false;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        /// <summary>
        /// Checks if the ESC key was pressed based on the active input handler.
        /// </summary>
        public static bool WasEnterKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard == null) { return false; }
            return keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#endif
        }

        /// <summary>
        /// Checks if the Shift key was pressed based on the active input handler.
        /// </summary>
        public static bool WasShiftKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard == null) { return false; }
            return keyboard.shiftKey.isPressed;
#else
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#endif
        }

        /// <summary>
        /// Checks if the submit button is pressed.
        /// </summary>
        public static bool WasSubmitPressed()
        {
            bool isSubmitPressed = false;

#if ENABLE_INPUT_SYSTEM
            if (EventSystem.current != null && EventSystem.current.currentInputModule is InputSystemUIInputModule inputModule)
            {
                if (inputModule.submit != null && inputModule.submit.action != null)
                {
                    isSubmitPressed = inputModule.submit.action.IsPressed();
                }
            }
#else
            isSubmitPressed = Input.GetButton("Submit");
#endif

            return isSubmitPressed;
        }

        /// <summary>
        /// Checks for tab navigation combos.
        /// </summary>
        public static bool HandleTabNavigation(out bool reverseTab)
        {
#if ENABLE_INPUT_SYSTEM
            bool tabPressed = Keyboard.current?.tabKey.wasPressedThisFrame ?? false;
            bool shiftHeld = (Keyboard.current?.leftShiftKey.isPressed ?? false) || (Keyboard.current?.rightShiftKey.isPressed ?? false);
#else
            bool tabPressed = Input.GetKeyDown(KeyCode.Tab);
            bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#endif

            reverseTab = tabPressed && shiftHeld;
            return tabPressed;
        }

        #endregion

        #region Graphics
        /// <summary>
        /// Helper method to create Raycast Graphic (Image) to interactable objects.
        /// </summary>
        public static void AddRaycastGraphic(GameObject targetObject)
        {
            if (!targetObject.TryGetComponent<Graphic>(out var _))
            {
                Image raycastGraphic = targetObject.AddComponent<Image>();
                raycastGraphic.color = Color.clear;
                raycastGraphic.raycastTarget = true;
            }
        }
        #endregion

        #region Navigation
        /// <summary>
        /// Helper method to select target object as selected event system object.
        /// </summary>
        public static GameObject GetSelectedObject()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null) { return null; }
            return eventSystem.currentSelectedGameObject;
        }

        /// <summary>
        /// Helper method to select target object as selected event system object.
        /// </summary>
        public static void SetSelectedObject(GameObject targetObject)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem != null && !eventSystem.alreadySelecting) { eventSystem.SetSelectedGameObject(targetObject); }
        }
        #endregion
    }
}