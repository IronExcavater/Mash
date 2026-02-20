using TMPro;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("Gameplay/UI/Text Shadow")]
public class TextShadow : MonoBehaviour
{
    [SerializeField] private TMP_Text sourceText;
    [SerializeField] private TMP_Text shadowText;
    [SerializeField] private Vector2 shadowOffset = new Vector2(1.8f, -1.8f);
    [SerializeField] private Color shadowColor = new Color(0f, 0f, 0f, 0.88f);

    private void Awake()
    {
        ResolveReferences();
        SyncShadow(true);
    }

    private void OnEnable()
    {
        ResolveReferences();
        SyncShadow(true);
    }

    private void LateUpdate()
    {
        SyncShadow(true);
    }

    private void OnValidate()
    {
        ResolveReferences();
        SyncShadow(false);
    }

    private void ResolveReferences()
    {
        if (sourceText == null)
        {
            var main = transform.Find("MainText");
            if (main != null) sourceText = main.GetComponent<TMP_Text>();
        }
        // Do not fallback to TMP on the parent: the parent must be a pure wrapper.

        if (shadowText == null)
        {
            var shadowTransform = transform.Find("ShadowText");
            if (shadowTransform == null) shadowTransform = transform.Find("ShadowCopy");
            if (shadowTransform != null) shadowText = shadowTransform.GetComponent<TMP_Text>();
        }
    }

    private void SyncShadow(bool includeLayout)
    {
        if (sourceText == null || shadowText == null) return;
        shadowText.text = sourceText.text;
        shadowText.font = sourceText.font;
        try
        {
            if (sourceText.fontSharedMaterial != null)
                shadowText.fontSharedMaterial = sourceText.fontSharedMaterial;
        }
        catch
        {
            // TMP can be mid-initialization in editor validation passes.
        }
        shadowText.fontSize = sourceText.fontSize;
        shadowText.enableAutoSizing = sourceText.enableAutoSizing;
        shadowText.fontSizeMin = sourceText.fontSizeMin;
        shadowText.fontSizeMax = sourceText.fontSizeMax;
        shadowText.lineSpacing = sourceText.lineSpacing;
        shadowText.characterSpacing = sourceText.characterSpacing;
        shadowText.wordSpacing = sourceText.wordSpacing;
        shadowText.paragraphSpacing = sourceText.paragraphSpacing;
        shadowText.isRightToLeftText = sourceText.isRightToLeftText;
        shadowText.richText = sourceText.richText;
        shadowText.alignment = sourceText.alignment;
        shadowText.textWrappingMode = sourceText.textWrappingMode;
        shadowText.overflowMode = sourceText.overflowMode;
        shadowText.fontStyle = sourceText.fontStyle;
        shadowText.extraPadding = sourceText.extraPadding;
        shadowText.margin = sourceText.margin;
        shadowText.horizontalMapping = sourceText.horizontalMapping;
        shadowText.verticalMapping = sourceText.verticalMapping;
        shadowText.color = shadowColor;
        shadowText.alpha = sourceText.alpha * shadowColor.a;
        shadowText.enabled = sourceText.enabled;
        shadowText.gameObject.SetActive(sourceText.gameObject.activeSelf);
        shadowText.raycastTarget = false;

        if (!includeLayout) return;

        var sourceRect = sourceText.rectTransform;
        var shadowRect = shadowText.rectTransform;
        if (sourceRect != null && shadowRect != null)
        {
            shadowRect.anchorMin = sourceRect.anchorMin;
            shadowRect.anchorMax = sourceRect.anchorMax;
            shadowRect.pivot = sourceRect.pivot;
            shadowRect.sizeDelta = sourceRect.sizeDelta;
            shadowRect.localScale = sourceRect.localScale;
            shadowRect.localRotation = sourceRect.localRotation;
            shadowRect.anchoredPosition = sourceRect.anchoredPosition + shadowOffset;
        }
    }
}
