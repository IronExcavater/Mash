using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HELP_URL + "effects/ripple")]
    [AddComponentMenu("Evo/UI/Effects/Ripple")]
    public class Ripple : MonoBehaviour, IPointerDownHandler, ISubmitHandler
    {
        [Header("Customization")]
        [SerializeField] private Preset preset;

        [Header("Settings")]
        [SerializeField] private bool createOnClick;
        [SerializeField] private bool createOnSubmit;
        [SerializeField] private Transform rippleParent;

        [System.Serializable]
        public class Preset
        {
            public Sprite shape;
            public Color color = new(1f, 1f, 1f, 0.25f);
            public bool centered = false;

            [Header("Animation")]
            [Range(0.05f, 5)] public float duration = 0.5f;
            [Range(0.1f, 50)] public float size = 6;
        }

        void Awake()
        {
            if (rippleParent == null)
            {
                rippleParent = transform;
            }
        }

        public static void Create(Preset preset, Transform parent, bool manageParent = false, bool forceCentered = false)
        {
            if (parent == null)
                return;

            GameObject rippleObj = new("Ripple Effect");
            rippleObj.transform.SetParent(parent, false);

            RippleHandler handler = rippleObj.AddComponent<RippleHandler>();
            handler.Create(preset, manageParent, forceCentered);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!createOnClick)
                return;

            Create(preset, rippleParent);
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (!createOnSubmit)
                return;

            Create(preset, rippleParent);
        }
    }

    public class RippleHandler : MonoBehaviour
    {
        void OnDisable()
        {
            Destroy(gameObject);
        }

        public void Create(Ripple.Preset preset, bool manageParent, bool forceCentered)
        {
            if (manageParent) { transform.parent.gameObject.SetActive(true); }
            StartCoroutine(CreateHelper(preset, manageParent, forceCentered));
        }

        IEnumerator CreateHelper(Ripple.Preset preset, bool manageParent, bool forceCentered)
        {
            // Add image and pass values
            Image rippleImg = gameObject.AddComponent<Image>();
            rippleImg.raycastTarget = false;
            rippleImg.sprite = preset.shape;

            // Start with zero alpha
            Color currentColor = preset.color;
            currentColor.a = 0f;
            rippleImg.color = currentColor;

            // Position the ripple
            if (forceCentered || preset.centered) { gameObject.transform.localPosition = Vector2.zero; }
            else
            {
                // Get pointer position and convert to local coordinates
                Vector2 screenPosition = Utilities.GetPointerPosition();
                RectTransform parentRect = transform.parent as RectTransform;

                if (parentRect != null)
                {
                    Canvas canvas = parentRect.GetComponentInParent<Canvas>();
                    if (canvas != null)
                    {
                        RectTransformUtility.ScreenPointToLocalPointInRectangle(
                            parentRect,
                            screenPosition,
                            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                            out Vector2 localPos
                        );
                        gameObject.transform.localPosition = localPos;
                    }
                }
            }

            // Store values
            Vector3 startScale = Vector3.zero;
            Vector3 targetScale = new(preset.size, preset.size, preset.size);
            float targetAlpha = preset.color.a;
            float duration = preset.duration;
            float fadeInTime = Mathf.Min(0.1f, duration * 0.2f);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = 1f - Mathf.Pow(1f - t, 2f);

                // Scale up
                gameObject.transform.localScale = Vector3.Lerp(startScale, targetScale, t);

                // Alpha: fade in quickly, then fade out
                float alpha;
                if (elapsed < fadeInTime)
                {
                    // Quick fade in
                    float fadeT = elapsed / fadeInTime;
                    alpha = Mathf.Lerp(0f, targetAlpha, fadeT);
                }
                else
                {
                    // Fade out over remaining time
                    float fadeOutT = (elapsed - fadeInTime) / (duration - fadeInTime);
                    alpha = Mathf.Lerp(targetAlpha, 0f, fadeOutT);
                }

                currentColor.a = alpha;
                rippleImg.color = currentColor;

                yield return null;
            }

            // Final values
            gameObject.transform.localScale = targetScale;
            currentColor.a = 0f;
            rippleImg.color = currentColor;

            Destroy(gameObject);

            if (manageParent && transform.parent.childCount <= 1) { transform.parent.gameObject.SetActive(false); }
        }
    }
}