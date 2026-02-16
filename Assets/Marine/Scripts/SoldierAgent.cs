using UnityEngine;
using UnityEngine.AI;

[AddComponentMenu("Gameplay/Soldiers/Soldier Agent")]
public class SoldierAgent : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3.5f;

    [Header("Boarding")]
    [SerializeField] private float boardDistance = 6f;
    [SerializeField, Min(0f)] private float boardHeightTolerance = 8f;

    [Header("Follow")]
    [SerializeField, Min(1f)] private float followActivationDistance = 24f;
    [SerializeField, Min(1f)] private float followDropoffDistance = 30f;
    [SerializeField] private bool autoFindHelicopterIfMissing = true;

    [Header("Navigation")]
    [SerializeField] private bool useNavMeshAgentIfPresent = true;

    [Header("Visuals")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private bool autoAssignAnimator = true;
    [ConditionalField("autoAssignAnimator", false)]
    [SerializeField] private Animator animator;
    [SerializeField] private bool driveAnimatorParameters = true;
    [ConditionalField("driveAnimatorParameters", true)]
    [SerializeField] private string speedParameter = "Speed";
    [ConditionalField("driveAnimatorParameters", true)]
    [SerializeField] private string isMovingParameter = "IsMoving";
    [ConditionalField("driveAnimatorParameters", true)]
    [SerializeField, Min(0f)] private float movingThreshold = 0.05f;

    private NavMeshAgent navMeshAgent;
    private Vector3 targetPoint;
    private bool hasTargetPoint;
    private HelicopterCapacity assignedHelicopter;
    private Vector3 fallbackHoldPoint;
    private bool isFollowingHelicopter;

    public bool IsBoarded { get; private set; }
    public HelicopterCapacity AssignedHelicopter => assignedHelicopter;

    private void Awake()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        fallbackHoldPoint = transform.position;
        if (autoAssignAnimator && animator == null)
            animator = GetComponentInChildren<Animator>();

        if (navMeshAgent != null)
            navMeshAgent.speed = Mathf.Max(0.1f, moveSpeed);
    }

    private void Update()
    {
        if (IsBoarded)
        {
            UpdateAnimation(0f);
            return;
        }

        var moved = false;
        if (assignedHelicopter == null && autoFindHelicopterIfMissing)
            assignedHelicopter = FindFirstObjectByType<HelicopterCapacity>();

        if (assignedHelicopter != null)
        {
            var helicopterPos = assignedHelicopter.transform.position;
            var toHelicopter = helicopterPos - transform.position;
            var planarToHelicopter = new Vector2(toHelicopter.x, toHelicopter.z);
            var sqrPlanarDistanceToHelicopter = planarToHelicopter.sqrMagnitude;
            var absHeightDelta = Mathf.Abs(toHelicopter.y);
            var activateSqr = followActivationDistance * followActivationDistance;
            var dropoffSqr = Mathf.Max(followActivationDistance, followDropoffDistance);
            dropoffSqr *= dropoffSqr;

            if (!isFollowingHelicopter && sqrPlanarDistanceToHelicopter <= activateSqr)
                isFollowingHelicopter = true;
            else if (isFollowingHelicopter && sqrPlanarDistanceToHelicopter > dropoffSqr)
                isFollowingHelicopter = false;

            if (isFollowingHelicopter)
            {
                targetPoint = helicopterPos;
                hasTargetPoint = true;
                moved = MoveToTarget();

                if (sqrPlanarDistanceToHelicopter <= boardDistance * boardDistance && absHeightDelta <= boardHeightTolerance)
                    assignedHelicopter.TryBoardSoldier(this);
            }
            else if (!hasTargetPoint)
            {
                targetPoint = fallbackHoldPoint;
                hasTargetPoint = true;
                moved = MoveToTarget();
            }
        }

        UpdateAnimation(moved ? moveSpeed : 0f);
    }

    public void AssignHelicopter(HelicopterCapacity helicopter)
    {
        assignedHelicopter = helicopter;
        isFollowingHelicopter = false;
        if (!IsBoarded) fallbackHoldPoint = transform.position;
    }

    public void SetTargetPoint(Vector3 worldPoint)
    {
        targetPoint = worldPoint;
        hasTargetPoint = true;
    }

    public void Board()
    {
        IsBoarded = true;
        isFollowingHelicopter = false;
        SetAgentEnabled(false);
        UpdateAnimation(0f);
        if (visualRoot != null) visualRoot.gameObject.SetActive(false);
        else gameObject.SetActive(false);
    }

    private bool MoveToTarget()
    {
        if (!hasTargetPoint) return false;

        if (useNavMeshAgentIfPresent && navMeshAgent != null && navMeshAgent.isOnNavMesh)
        {
            SetAgentEnabled(true);
            navMeshAgent.SetDestination(targetPoint);
            var velocity = navMeshAgent.velocity;
            velocity.y = 0f;
            return velocity.sqrMagnitude > movingThreshold * movingThreshold;
        }

        var delta = targetPoint - transform.position;
        delta.y = 0f;
        if (delta.sqrMagnitude < movingThreshold * movingThreshold) return false;

        var step = moveSpeed * Time.deltaTime;
        var next = transform.position + delta.normalized * step;
        next.y = transform.position.y;
        transform.position = next;
        transform.forward = Vector3.Lerp(transform.forward, delta.normalized, Time.deltaTime * 8f);
        return true;
    }

    private void SetAgentEnabled(bool enabled)
    {
        if (navMeshAgent == null) return;
        if (navMeshAgent.enabled == enabled) return;
        navMeshAgent.enabled = enabled;
    }

    private void UpdateAnimation(float speed)
    {
        if (!driveAnimatorParameters) return;
        if (animator == null) return;
        var clampedSpeed = Mathf.Max(0f, speed);
        if (!string.IsNullOrWhiteSpace(speedParameter))
            animator.SetFloat(speedParameter, clampedSpeed);
        if (!string.IsNullOrWhiteSpace(isMovingParameter))
            animator.SetBool(isMovingParameter, clampedSpeed > movingThreshold);
    }
}
