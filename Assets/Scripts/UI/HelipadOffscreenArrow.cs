using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("Gameplay/UI/Helipad Offscreen Arrow")]
public class HelipadOffscreenArrow : MonoBehaviour
{
    [SerializeField] private RectTransform canvasRect;
    [SerializeField] private RectTransform arrowRect;
    [SerializeField] private Image arrowImage;
    [SerializeField] private Transform target;
    [SerializeField] private Camera worldCamera;
    [SerializeField, Min(0f)] private float edgePadding = 54f;
    [SerializeField] private bool hideWhenOnScreen = true;

    public void SetTarget(Transform targetTransform, Camera camera, RectTransform canvasRoot)
    {
        target = targetTransform;
        worldCamera = camera;
        if (canvasRoot != null) canvasRect = canvasRoot;
    }

    private void Awake()
    {
        if (arrowRect == null) arrowRect = transform as RectTransform;
        if (arrowImage == null) arrowImage = GetComponent<Image>();
        if (canvasRect == null)
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null) canvasRect = canvas.GetComponent<RectTransform>();
        }
    }

    private void LateUpdate()
    {
        if (arrowRect == null || canvasRect == null || target == null || worldCamera == null)
        {
            SetVisible(false);
            return;
        }

        var targetWorld = target.position + Vector3.up * 1.25f;
        var targetScreen = worldCamera.WorldToScreenPoint(targetWorld);
        var behind = targetScreen.z < 0f;

        var viewport = new Vector2(targetScreen.x / Screen.width, targetScreen.y / Screen.height);
        var onScreen = !behind && viewport.x > 0f && viewport.x < 1f && viewport.y > 0f && viewport.y < 1f;
        if (hideWhenOnScreen && onScreen)
        {
            SetVisible(false);
            return;
        }

        SetVisible(true);

        var dir = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f);
        if (behind) dir = -dir;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;
        dir.Normalize();

        var canvasSize = canvasRect.rect.size;
        if (canvasSize.x < 2f || canvasSize.y < 2f)
            canvasSize = new Vector2(Screen.width, Screen.height);

        var half = canvasSize * 0.5f - Vector2.one * edgePadding;
        half.x = Mathf.Max(24f, half.x);
        half.y = Mathf.Max(24f, half.y);
        var tx = Mathf.Abs(dir.x) > 0.0001f ? half.x / Mathf.Abs(dir.x) : float.PositiveInfinity;
        var ty = Mathf.Abs(dir.y) > 0.0001f ? half.y / Mathf.Abs(dir.y) : float.PositiveInfinity;
        var t = Mathf.Min(tx, ty);

        arrowRect.anchorMin = new Vector2(0.5f, 0.5f);
        arrowRect.anchorMax = new Vector2(0.5f, 0.5f);
        arrowRect.pivot = new Vector2(0.5f, 0.5f);
        arrowRect.anchoredPosition = dir * t;

        var angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        arrowRect.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void SetVisible(bool visible)
    {
        if (arrowRect != null && !arrowRect.gameObject.activeSelf)
            arrowRect.gameObject.SetActive(true);

        if (arrowImage != null)
            arrowImage.enabled = visible;
    }
}
