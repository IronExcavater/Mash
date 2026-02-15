using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Evo.UI
{
    [HelpURL(Constants.HELP_URL + "ui-elements/tooltip")]
    [RequireComponent(typeof(CanvasGroup))]
    [RequireComponent(typeof(RectTransform))]
    public class TooltipPreset : MonoBehaviour
    {
        [Header("Required References")]
        public RectTransform tooltipRect;
        public CanvasGroup canvasGroup;

        [Header("Content References")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private ContentSizeFitter contentSizeFitter;
        [SerializeField] private GameObject contentContainer;
        [SerializeField] private LayoutGroup layoutGroup;
        public LayoutElement layoutElement;

        public void Setup(string title, string description, Sprite icon, float maxWidth, bool isCustom = false)
        {
            CheckForReferences();

            if (isCustom)
                return;

            if (string.IsNullOrEmpty(title) && icon == null && titleText != null) { titleText.transform.parent.gameObject.SetActive(false); }
            else
            {
                SetIcon(icon);
                SetTitle(title);
            }

            SetDescription(description);
            ForceLayoutUpdate(maxWidth);
        }

        void CheckForReferences()
        {
            if (canvasGroup == null) { canvasGroup = GetComponent<CanvasGroup>(); }
            if (tooltipRect == null) { tooltipRect = GetComponent<RectTransform>(); }

            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        void SetTitle(string title)
        {
            if (titleText == null)
                return;

            titleText.text = title;
            titleText.gameObject.SetActive(!string.IsNullOrEmpty(title));
        }

        void SetDescription(string description)
        {
            if (descriptionText == null)
                return;

            descriptionText.text = description;
            descriptionText.gameObject.SetActive(!string.IsNullOrEmpty(description));
        }

        void SetIcon(Sprite icon)
        {
            if (iconImage == null)
                return;

            bool hasIcon = icon != null;
            iconImage.sprite = icon;
            iconImage.gameObject.SetActive(hasIcon);
        }

        void ForceLayoutUpdate(float maxWidth)
        {
            if (contentSizeFitter != null)
            {
                contentSizeFitter.SetLayoutHorizontal();
                contentSizeFitter.SetLayoutVertical();
            }

            if (layoutGroup != null)
            {
                layoutGroup.SetLayoutHorizontal();
                layoutGroup.SetLayoutVertical();
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRect);
            Canvas.ForceUpdateCanvases();

            if (layoutElement != null)
            {
                layoutElement.enabled = tooltipRect.sizeDelta.x > maxWidth;
                layoutElement.preferredWidth = maxWidth;
            }
        }
    }
}