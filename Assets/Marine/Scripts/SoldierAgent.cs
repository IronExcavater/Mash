using UnityEngine;
using UnityEngine.AI;

[AddComponentMenu("Gameplay/Soldiers/Soldier Agent")]
public class SoldierAgent : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3.5f;

    [Header("Follow")]
    [SerializeField, Min(1f)] private float followActivationDistance = 3f;
    [SerializeField, Min(1f)] private float followDropoffDistance = 4f;
    [SerializeField] private bool autoFindHelicopterIfMissing = true;

    [Header("Navigation")]
    [SerializeField] private bool useNavMeshAgentIfPresent = true;

    [Header("Collision")]
    [SerializeField] private bool autoAddBoardingCollider = true;
    [ConditionalField("autoAddBoardingCollider", true)]
    [SerializeField, Min(0.1f)] private float boardingColliderHeight = 1.8f;
    [ConditionalField("autoAddBoardingCollider", true)]
    [SerializeField, Min(0.05f)] private float boardingColliderRadius = 0.35f;
    [ConditionalField("autoAddBoardingCollider", true)]
    [SerializeField] private Vector3 boardingColliderCenter = new Vector3(0f, 0.9f, 0f);

    [Header("Visuals")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private bool autoAssignAnimator = true;
    [ConditionalField("autoAssignAnimator", true)]
    [SerializeField] private bool autoAddAnimatorIfMissing = true;
    [ConditionalField("autoAssignAnimator", false)]
    [SerializeField] private Animator animator;
    [SerializeField] private RuntimeAnimatorController defaultAnimatorController;
    [SerializeField] private Avatar overrideAvatar;
    [SerializeField] private bool applyRootMotion = false;
    [SerializeField] private AnimatorCullingMode cullingMode = AnimatorCullingMode.CullUpdateTransforms;
    [SerializeField] private bool driveAnimatorParameters = true;
    [ConditionalField("driveAnimatorParameters", true)]
    [SerializeField] private string speedParameter = "Speed";
    [ConditionalField("driveAnimatorParameters", true)]
    [SerializeField] private string isMovingParameter = "IsMoving";
    [ConditionalField("driveAnimatorParameters", true)]
    [SerializeField, Min(0f)] private float movingThreshold = 0.05f;
    [Header("Combat Audio")]
    [SerializeField] private bool enableGunfireAudio = true;
    [ConditionalField("enableGunfireAudio", true)]
    [SerializeField] private AudioSource gunfireAudioSource;
    [ConditionalField("enableGunfireAudio", true)]
    [SerializeField] private AudioClip[] gunfireClips;
    [ConditionalField("enableGunfireAudio", true)]
    [SerializeField, Min(0.1f)] private float minGunfireInterval = 1.3f;
    [ConditionalField("enableGunfireAudio", true)]
    [SerializeField, Min(0.1f)] private float maxGunfireInterval = 3.2f;
    [ConditionalField("enableGunfireAudio", true)]
    [SerializeField, Range(0f, 1f)] private float gunfireVolume = 0.34f;

    private NavMeshAgent navMeshAgent;
    private Vector3 targetPoint;
    private bool hasTargetPoint;
    private HelicopterCapacity assignedHelicopter;
    private Vector3 fallbackHoldPoint;
    private bool isFollowingHelicopter;
    private float nextGunfireAt;
    private int lastGunfireClipIndex = -1;

    public bool IsBoarded { get; private set; }
    public HelicopterCapacity AssignedHelicopter => assignedHelicopter;

    private void Awake()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        EnsureBoardingCollider();
        fallbackHoldPoint = transform.position;
        EnsureAnimator();
        EnsureGunfireAudioSource();
        ScheduleNextGunfire();

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
            var pickupPoint = assignedHelicopter.GetPickupPointWorld();
            var toHelicopter = pickupPoint - transform.position;
            var sqrPlanarDistanceToHelicopter = new Vector2(toHelicopter.x, toHelicopter.z).sqrMagnitude;
            var effectiveActivation = Mathf.Clamp(followActivationDistance, 1f, 3f);
            var effectiveDropoff = Mathf.Max(effectiveActivation, Mathf.Clamp(followDropoffDistance, 1f, 4f));
            var activateSqr = effectiveActivation * effectiveActivation;
            var dropoffSqr = effectiveDropoff;
            dropoffSqr *= dropoffSqr;

            if (!isFollowingHelicopter && sqrPlanarDistanceToHelicopter <= activateSqr)
                isFollowingHelicopter = true;
            else if (isFollowingHelicopter && sqrPlanarDistanceToHelicopter > dropoffSqr)
                isFollowingHelicopter = false;

            if (isFollowingHelicopter)
            {
                targetPoint = pickupPoint;
                targetPoint.y = transform.position.y;
                hasTargetPoint = true;
                moved = MoveToTarget();
            }
            else if (!hasTargetPoint)
            {
                targetPoint = fallbackHoldPoint;
                hasTargetPoint = true;
                moved = MoveToTarget();
            }
        }

        UpdateAnimation(moved ? moveSpeed : 0f);
        HandleGunfireAudio();
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

    public void ConfigureAnimation(RuntimeAnimatorController controller, Avatar avatar = null)
    {
        defaultAnimatorController = controller;
        if (avatar != null) overrideAvatar = avatar;
        EnsureAnimator();
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

    private void EnsureBoardingCollider()
    {
        if (!autoAddBoardingCollider) return;
        if (GetComponent<Collider>() != null) return;

        var capsule = gameObject.AddComponent<CapsuleCollider>();
        capsule.height = boardingColliderHeight;
        capsule.radius = boardingColliderRadius;
        capsule.center = boardingColliderCenter;
        capsule.direction = 1;
    }

    private void EnsureAnimator()
    {
        if (!autoAssignAnimator) return;

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (animator == null && autoAddAnimatorIfMissing)
            animator = gameObject.AddComponent<Animator>();

        if (animator == null) return;

        if (defaultAnimatorController != null && animator.runtimeAnimatorController == null)
            animator.runtimeAnimatorController = defaultAnimatorController;

        if (overrideAvatar != null)
            animator.avatar = overrideAvatar;

        animator.applyRootMotion = applyRootMotion;
        animator.cullingMode = cullingMode;
    }

    private void EnsureGunfireAudioSource()
    {
        if (!enableGunfireAudio) return;
        if (gunfireAudioSource == null)
        {
            var child = transform.Find("GunfireAudioSource");
            if (child == null)
            {
                var go = new GameObject("GunfireAudioSource");
                go.transform.SetParent(transform, false);
                child = go.transform;
            }

            gunfireAudioSource = child.GetComponent<AudioSource>();
            if (gunfireAudioSource == null) gunfireAudioSource = child.gameObject.AddComponent<AudioSource>();
        }

        if (gunfireAudioSource == null) return;
        gunfireAudioSource.playOnAwake = false;
        gunfireAudioSource.loop = false;
        gunfireAudioSource.spatialBlend = 1f;
    }

    private void HandleGunfireAudio()
    {
        if (!enableGunfireAudio) return;
        if (IsBoarded) return;
        if (gunfireAudioSource == null) return;
        if (gunfireClips == null || gunfireClips.Length == 0) return;
        if (Time.time < nextGunfireAt) return;

        var clip = ChooseClip(gunfireClips, ref lastGunfireClipIndex);
        if (clip != null)
            gunfireAudioSource.PlayOneShot(clip, gunfireVolume);
        ScheduleNextGunfire();
    }

    private void ScheduleNextGunfire()
    {
        nextGunfireAt = Time.time + Random.Range(Mathf.Min(minGunfireInterval, maxGunfireInterval), Mathf.Max(minGunfireInterval, maxGunfireInterval));
    }

    private static AudioClip ChooseClip(AudioClip[] clips, ref int lastIndex)
    {
        if (clips == null || clips.Length == 0) return null;
        if (clips.Length == 1)
        {
            lastIndex = 0;
            return clips[0];
        }

        var idx = Random.Range(0, clips.Length);
        if (idx == lastIndex) idx = (idx + 1) % clips.Length;
        lastIndex = idx;
        return clips[idx];
    }

}
