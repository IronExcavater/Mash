using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Rigidbody))]
public class HelicopterCollisionHandler : MonoBehaviour
{
    [Header("Filtering")]
    [SerializeField] private LayerMask collisionLayers = ~0;
    [TagSelector]
    [SerializeField] private string hazardTag = "Tree";

    [Header("Reactions")]
    [SerializeField] private bool disableMotorOnHazardHit = true;
    [SerializeField] private bool logHitDetails;

    [Header("Events")]
    [SerializeField] private UnityEvent onAnyCollision;
    [SerializeField] private UnityEvent onHazardCollision;

    private HelicopterFlightController flightController;
    private Rigidbody body;

    public Vector3 LastHitPoint { get; private set; }
    public Vector3 LastHitNormal { get; private set; }
    public Vector3 LastIncomingDirection { get; private set; }
    public Vector3 LastLocalHitDirection { get; private set; }
    public float LastImpactSpeed { get; private set; }
    public Collider LastCollider { get; private set; }
    public bool LastHitWasHazard { get; private set; }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        flightController = GetComponent<HelicopterFlightController>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        var otherLayerBit = 1 << collision.gameObject.layer;
        if ((collisionLayers.value & otherLayerBit) == 0) return;

        var hasContact = collision.contactCount > 0;
        var point = hasContact ? collision.GetContact(0).point : transform.position;
        var normal = hasContact ? collision.GetContact(0).normal : (-collision.relativeVelocity).normalized;
        var impactSpeed = collision.relativeVelocity.magnitude;

        if (impactSpeed < 0.0001f && body != null) impactSpeed = body.linearVelocity.magnitude;
        if (normal.sqrMagnitude < 0.0001f) normal = -transform.forward;

        var incoming = -normal;

        LastHitPoint = point;
        LastHitNormal = normal.normalized;
        LastIncomingDirection = incoming.normalized;
        LastLocalHitDirection = transform.InverseTransformDirection(LastIncomingDirection);
        LastImpactSpeed = impactSpeed;
        LastCollider = collision.collider;
        LastHitWasHazard = collision.collider.CompareTag(hazardTag);

        if (logHitDetails)
            Debug.Log($"Hit '{collision.collider.name}', hazard={LastHitWasHazard}, speed={LastImpactSpeed:F2}, localDir={LastLocalHitDirection}", this);

        onAnyCollision?.Invoke();
        if (!LastHitWasHazard) return;

        if (disableMotorOnHazardHit && flightController != null)
            flightController.SetInputEnabled(false);

        onHazardCollision?.Invoke();
    }
}
