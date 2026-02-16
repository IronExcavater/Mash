using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
[AddComponentMenu("Gameplay/Helicopter/Hover Height Zone")]
public class HelicopterHoverHeightZone : MonoBehaviour
{
    [Header("Height")]
    [SerializeField, Min(0f)] private float additionalClearance = 5f;
    [Header("Trigger Shape")]
    [SerializeField] private bool autoApplyBoxShape = true;
    [SerializeField] private bool autoFitToMilitaryBaseRadius = true;
    [SerializeField, Min(0f)] private float baseRadiusPadding = 0f;
    [SerializeField, Min(0.1f)] private float fittedZoneHeight = 18f;
    [SerializeField] private float fittedCenterY = 9f;
    [SerializeField] private Vector3 triggerCenter = new Vector3(0f, 9f, 0f);
    [SerializeField] private Vector3 triggerSize = new Vector3(130f, 18f, 130f);
    [SerializeField] private bool setColliderTriggerOnValidate = true;

    public float AdditionalClearance => Mathf.Max(0f, additionalClearance);

    private void Reset()
    {
        EnsureTriggerCollider();
    }

    private void OnValidate()
    {
        if (setColliderTriggerOnValidate)
            EnsureTriggerCollider();
    }

    private void OnEnable()
    {
        EnsureTriggerCollider();
    }

    private void OnTriggerEnter(Collider other)
    {
        var controller = other.GetComponentInParent<HelicopterFlightController>();
        if (controller == null) return;
        controller.RegisterHoverZone(this);
    }

    private void OnTriggerExit(Collider other)
    {
        var controller = other.GetComponentInParent<HelicopterFlightController>();
        if (controller == null) return;
        controller.UnregisterHoverZone(this);
    }

    private void OnTriggerStay(Collider other)
    {
        var controller = other.GetComponentInParent<HelicopterFlightController>();
        if (controller == null) return;
        controller.RegisterHoverZone(this);
    }

    private void EnsureTriggerCollider()
    {
        var col = GetComponent<Collider>();
        if (col == null) return;
        col.isTrigger = true;

        if (!autoApplyBoxShape) return;
        if (col is BoxCollider box)
        {
            if (autoFitToMilitaryBaseRadius)
                ApplyBaseFittedShape();

            box.center = triggerCenter;
            box.size = new Vector3(
                Mathf.Max(0.1f, triggerSize.x),
                Mathf.Max(0.1f, triggerSize.y),
                Mathf.Max(0.1f, triggerSize.z));
        }
    }

    private void ApplyBaseFittedShape()
    {
        var baseGenerator = FindFirstObjectByType<MilitaryBaseGenerator>();
        if (baseGenerator == null) return;

        var radius = Mathf.Max(6f, baseGenerator.ProtectedRadius + baseRadiusPadding);
        triggerCenter = new Vector3(0f, fittedCenterY, 0f);
        triggerSize = new Vector3(radius * 2f, Mathf.Max(0.1f, fittedZoneHeight), radius * 2f);
    }
}
