using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("Gameplay/UI/UI Shadow")]
public class UIShadow : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform sourceRoot;
    [SerializeField] private RectTransform shadowRoot;

    [Header("Shadow")]
    [SerializeField] private Vector2 shadowOffset = new Vector2(1.8f, -1.8f);
    [SerializeField] private Color shadowTint = new Color(0f, 0f, 0f, 0.88f);
    [SerializeField] private bool includeChildren = true;

    private readonly Dictionary<RectTransform, RectTransform> shadowBySource = new Dictionary<RectTransform, RectTransform>();
    private readonly List<Graphic> sourceGraphics = new List<Graphic>();
    private bool rebuildRequested = true;

    private void Awake()
    {
        FindReferencesIfMissing();
        rebuildRequested = true;
        SyncNow();
    }

    private void OnEnable()
    {
        FindReferencesIfMissing();
        rebuildRequested = true;
        SyncNow();
    }

    private void OnValidate()
    {
        FindReferencesIfMissing();
        rebuildRequested = true;
    }

    private void LateUpdate()
    {
        SyncNow();
    }

    private void FindReferencesIfMissing()
    {
        if (sourceRoot == null)
        {
            var t = transform.Find("Main");
            if (t != null) sourceRoot = t as RectTransform;
        }

        if (shadowRoot == null)
        {
            var t = transform.Find("Shadow");
            if (t != null) shadowRoot = t as RectTransform;
        }
    }

    private void SyncNow()
    {
        if (sourceRoot == null || shadowRoot == null) return;

        if (rebuildRequested)
        {
            RebuildShadowHierarchy();
            rebuildRequested = false;
        }

        SyncHierarchyRecursive(sourceRoot, shadowRoot, true);
        SyncGraphicsRecursive(sourceRoot, shadowRoot);
    }

    private void RebuildShadowHierarchy()
    {
        shadowBySource.Clear();
        shadowBySource[sourceRoot] = shadowRoot;

        if (!includeChildren) return;
        RebuildChildren(sourceRoot, shadowRoot);
    }

    private void RebuildChildren(RectTransform srcParent, RectTransform dstParent)
    {
        foreach (Transform child in srcParent)
        {
            var src = child as RectTransform;
            if (src == null) continue;

            var dst = FindChildByName(dstParent, src.name);
            if (dst == null)
            {
                var go = new GameObject(src.name, typeof(RectTransform), typeof(CanvasRenderer));
                dst = go.GetComponent<RectTransform>();
                dst.SetParent(dstParent, false);
            }

            shadowBySource[src] = dst;
            RebuildChildren(src, dst);
        }
    }

    private static RectTransform FindChildByName(RectTransform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child as RectTransform;
        }
        return null;
    }

    private void SyncHierarchyRecursive(RectTransform src, RectTransform dst, bool isRoot)
    {
        CopyRect(src, dst, isRoot ? shadowOffset : Vector2.zero);
        dst.gameObject.SetActive(src.gameObject.activeSelf);
        dst.localScale = src.localScale;
        dst.localRotation = src.localRotation;

        if (!includeChildren) return;
        foreach (Transform child in src)
        {
            var srcChild = child as RectTransform;
            if (srcChild == null) continue;
            if (!shadowBySource.TryGetValue(srcChild, out var dstChild) || dstChild == null) continue;
            SyncHierarchyRecursive(srcChild, dstChild, false);
        }
    }

    private static void CopyRect(RectTransform src, RectTransform dst, Vector2 anchoredOffset)
    {
        dst.anchorMin = src.anchorMin;
        dst.anchorMax = src.anchorMax;
        dst.pivot = src.pivot;
        dst.sizeDelta = src.sizeDelta;
        dst.anchoredPosition = src.anchoredPosition + anchoredOffset;
    }

    private void SyncGraphicsRecursive(RectTransform src, RectTransform dst)
    {
        src.GetComponents(sourceGraphics);
        for (int i = 0; i < sourceGraphics.Count; i++)
        {
            var srcGraphic = sourceGraphics[i];
            if (srcGraphic == null) continue;
            if (!(srcGraphic is Image) && !(srcGraphic is RawImage) && !(srcGraphic is TMP_Text)) continue;

            var dstGraphic = GetOrCreateShadowGraphic(dst, srcGraphic.GetType());
            if (dstGraphic == null) continue;
            CopyGraphicCommon(srcGraphic, dstGraphic);

            if (srcGraphic is Image srcImage && dstGraphic is Image dstImage)
                CopyImage(srcImage, dstImage);
            else if (srcGraphic is RawImage srcRaw && dstGraphic is RawImage dstRaw)
                CopyRawImage(srcRaw, dstRaw);
            else if (srcGraphic is TMP_Text srcTmp && dstGraphic is TMP_Text dstTmp)
                CopyTmp(srcTmp, dstTmp);
        }

        if (!includeChildren) return;
        foreach (Transform child in src)
        {
            var srcChild = child as RectTransform;
            if (srcChild == null) continue;
            if (!shadowBySource.TryGetValue(srcChild, out var dstChild) || dstChild == null) continue;
            SyncGraphicsRecursive(srcChild, dstChild);
        }
    }

    private Graphic GetOrCreateShadowGraphic(RectTransform dst, System.Type type)
    {
        var existing = dst.GetComponent(type) as Graphic;
        if (existing != null) return existing;
        return dst.gameObject.AddComponent(type) as Graphic;
    }

    private void CopyGraphicCommon(Graphic src, Graphic dst)
    {
        var c = src.color;
        c *= shadowTint;
        c.a = src.color.a * shadowTint.a;

        dst.color = c;
        dst.material = src.material;
        dst.enabled = src.enabled;
        dst.raycastTarget = false;

        var srcMaskable = src as MaskableGraphic;
        var dstMaskable = dst as MaskableGraphic;
        if (srcMaskable != null && dstMaskable != null)
            dstMaskable.maskable = srcMaskable.maskable;
    }

    private static void CopyImage(Image src, Image dst)
    {
        dst.sprite = src.sprite;
        dst.overrideSprite = src.overrideSprite;
        dst.type = src.type;
        dst.fillCenter = src.fillCenter;
        dst.fillMethod = src.fillMethod;
        dst.fillAmount = src.fillAmount;
        dst.fillClockwise = src.fillClockwise;
        dst.fillOrigin = src.fillOrigin;
        dst.preserveAspect = src.preserveAspect;
        dst.useSpriteMesh = src.useSpriteMesh;
        dst.pixelsPerUnitMultiplier = src.pixelsPerUnitMultiplier;
    }

    private static void CopyRawImage(RawImage src, RawImage dst)
    {
        dst.texture = src.texture;
        dst.uvRect = src.uvRect;
    }

    private static void CopyTmp(TMP_Text src, TMP_Text dst)
    {
        dst.text = src.text;
        dst.font = src.font;
        dst.fontSize = src.fontSize;
        dst.enableAutoSizing = src.enableAutoSizing;
        dst.fontSizeMin = src.fontSizeMin;
        dst.fontSizeMax = src.fontSizeMax;
        dst.lineSpacing = src.lineSpacing;
        dst.characterSpacing = src.characterSpacing;
        dst.wordSpacing = src.wordSpacing;
        dst.paragraphSpacing = src.paragraphSpacing;
        dst.alignment = src.alignment;
        dst.textWrappingMode = src.textWrappingMode;
        dst.overflowMode = src.overflowMode;
        dst.fontStyle = src.fontStyle;
        dst.extraPadding = src.extraPadding;
        dst.margin = src.margin;
        dst.horizontalMapping = src.horizontalMapping;
        dst.verticalMapping = src.verticalMapping;
        dst.richText = src.richText;
        dst.isRightToLeftText = src.isRightToLeftText;
    }

}
