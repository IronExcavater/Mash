using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
[AddComponentMenu("Gameplay/Helicopter/Hover Height Zone")]
public class HelicopterHoverHeightZone : MonoBehaviour
{
    [Header("Height")]
    [SerializeField, Min(0f)] private float additionalClearance = 0.75f;
    [SerializeField] private bool enforceFixedWorldAltitude;
    [SerializeField] private float fixedWorldAltitude = 28f;
    [SerializeField] private bool enforceMinimumWorldAltitude;
    [SerializeField] private float minimumWorldAltitude = 24f;
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
    public bool HasMinimumWorldAltitude => enforceMinimumWorldAltitude;
    public float MinimumWorldAltitude => minimumWorldAltitude;
    public bool HasFixedWorldAltitude => enforceFixedWorldAltitude;
    public float FixedWorldAltitude => fixedWorldAltitude;

    public void SetAdditionalClearance(float clearance)
    {
        additionalClearance = Mathf.Max(0f, clearance);
    }

    public void SetMinimumWorldAltitude(float worldY)
    {
        enforceMinimumWorldAltitude = true;
        minimumWorldAltitude = worldY;
    }

    public void SetFixedWorldAltitude(float worldY)
    {
        enforceFixedWorldAltitude = true;
        fixedWorldAltitude = worldY;
    }

    public void ClearFixedWorldAltitude()
    {
        enforceFixedWorldAltitude = false;
    }

    public void ClearMinimumWorldAltitude()
    {
        enforceMinimumWorldAltitude = false;
    }

    public bool TryGetMinimumWorldAltitude(out float worldY)
    {
        worldY = minimumWorldAltitude;
        return enforceMinimumWorldAltitude;
    }

    public bool TryGetFixedWorldAltitude(out float worldY)
    {
        worldY = fixedWorldAltitude;
        return enforceFixedWorldAltitude;
    }

    public void ConfigureBoxShape(Vector3 center, Vector3 size, bool autoFitToBase)
    {
        autoApplyBoxShape = true;
        autoFitToMilitaryBaseRadius = autoFitToBase;
        triggerCenter = center;
        triggerSize = new Vector3(
            Mathf.Max(0.1f, size.x),
            Mathf.Max(0.1f, size.y),
            Mathf.Max(0.1f, size.z));
        EnsureTriggerCollider();
    }

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
        if (col == null && autoApplyBoxShape)
            col = gameObject.AddComponent<BoxCollider>();
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

    private void OnDrawGizmosSelected()
    {
        DrawZoneGizmos(0.28f, 0.95f);
    }

    private void OnDrawGizmos()
    {
        DrawZoneGizmos(0.12f, 0.5f);
    }

    private void DrawZoneGizmos(float fillAlpha, float wireAlpha)
    {
        var col = GetComponent<Collider>();
        if (col == null) return;

        Gizmos.color = new Color(0.2f, 0.95f, 1f, fillAlpha);
        if (col is BoxCollider box)
        {
            var worldCenter = transform.TransformPoint(box.center);
            var worldSize = Vector3.Scale(box.size, transform.lossyScale);
            Gizmos.matrix = Matrix4x4.TRS(worldCenter, transform.rotation, Vector3.one);
            Gizmos.DrawCube(Vector3.zero, worldSize);
            Gizmos.color = new Color(0.2f, 0.95f, 1f, wireAlpha);
            Gizmos.DrawWireCube(Vector3.zero, worldSize);
            Gizmos.matrix = Matrix4x4.identity;
            return;
        }

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        Gizmos.matrix = Matrix4x4.identity;
    }
}
