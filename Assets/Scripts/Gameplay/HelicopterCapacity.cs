using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using System;

[DisallowMultipleComponent]
[AddComponentMenu("Gameplay/Helicopter/Capacity")]
public class HelicopterCapacity : MonoBehaviour
{
    [Header("Capacity")]
    [SerializeField] private int maxSeats = 3;

    [Header("Boarding Rules")]
    [SerializeField] private bool requireLandedToBoard = true;
    [ConditionalField("requireLandedToBoard", true)]
    [SerializeField, Min(0f)] private float boardMaxVerticalSpeed = 2.25f;
    [ConditionalField("requireLandedToBoard", true)]
    [SerializeField, Min(0f)] private float boardMaxPlanarSpeed = 4.25f;

    [Header("Assisted Boarding")]
    [SerializeField] private bool allowAssistedCloseRangeBoarding = true;
    [ConditionalField("allowAssistedCloseRangeBoarding", true)]
    [SerializeField, Min(0f)] private float assistedBoardingPlanarDistance = 6.5f;
    [ConditionalField("allowAssistedCloseRangeBoarding", true)]
    [SerializeField, Min(0f)] private float assistedBoardingHeightDelta = 16f;
    [ConditionalField("allowAssistedCloseRangeBoarding", true)]
    [SerializeField] private Vector3 pickupPointLocalOffset = new Vector3(0f, -2.75f, 0f);
    [ConditionalField("allowAssistedCloseRangeBoarding", true)]
    [SerializeField, Min(0.1f)] private float pickupDetectionRadius = 3.5f;
    [ConditionalField("allowAssistedCloseRangeBoarding", true)]
    [SerializeField, Min(0.1f)] private float pickupDetectionHeight = 10f;

    [Header("References")]
    [SerializeField] private bool autoAssignReferences = true;
    [ConditionalField("autoAssignReferences", false)]
    [SerializeField] private HelicopterFlightController flightController;

    [Header("Seats")]
    [SerializeField] private Transform[] seatPoints;

    [Header("Audio")]
    [SerializeField] private bool autoAssignAudioSource = true;
    [ConditionalField("autoAssignAudioSource", false)]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] soldierPickupClips;
    [SerializeField] private AudioClip[] capacityFullClips;
    [SerializeField] private AudioClip[] rescueUnloadClips;
    [SerializeField, Range(0f, 1f)] private float pickupVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float fullCapacityVolume = 0.85f;
    [SerializeField, Range(0f, 1f)] private float rescueVolume = 1f;

    [Header("Events")]
    [SerializeField] private UnityEvent<int, int> onCapacityChanged;
    [SerializeField] private UnityEvent onCapacityFull;

    private readonly List<SoldierAgent> boardedSoldiers = new List<SoldierAgent>();
    private int totalBoardedCount;
    private int totalRescuedCount;
    private readonly Collider[] pickupBuffer = new Collider[64];
    public event Action<SoldierAgent> SoldierBoarded;
    public event Action<int> SoldiersRescuedAtHelipad;
    public event Action CapacityFullReached;
    private int lastPickupClipIndex = -1;
    private int lastFullClipIndex = -1;
    private int lastRescueClipIndex = -1;

    public int MaxSeats => Mathf.Max(1, maxSeats);
    public int BoardedCount => boardedSoldiers.Count;
    public int TotalBoardedCount => totalBoardedCount;
    public int TotalRescuedCount => totalRescuedCount;
    public int RemainingSeats => Mathf.Max(0, MaxSeats - boardedSoldiers.Count);
    public bool IsFull => boardedSoldiers.Count >= MaxSeats;

    private void Awake()
    {
        if (autoAssignReferences && flightController == null)
            flightController = GetComponent<HelicopterFlightController>();
        EnsureAudioSource();
    }

    private void Update()
    {
        if (!allowAssistedCloseRangeBoarding) return;
        if (IsFull) return;
        TryBoardNearbySoldiers();
    }

    public bool CanBoard()
    {
        EnsureReferences();
        if (IsFull) return false;
        if (!requireLandedToBoard) return true;
        if (flightController == null) return true;
        if (flightController.IsLanded) return true;
        if (!flightController.IsGrounded) return false;
        return Mathf.Abs(flightController.CurrentVerticalSpeed) <= boardMaxVerticalSpeed &&
               flightController.CurrentPlanarSpeed <= boardMaxPlanarSpeed;
    }

    public bool TryBoardSoldier(SoldierAgent soldier)
    {
        EnsureReferences();
        if (soldier == null) return false;
        if (boardedSoldiers.Contains(soldier)) return false;
        if (!CanBoard() && !CanAssistBoard(soldier)) return false;

        boardedSoldiers.Add(soldier);
        totalBoardedCount++;
        SnapSoldierToSeat(soldier, boardedSoldiers.Count - 1);
        soldier.Board();
        SoldierBoarded?.Invoke(soldier);
        PlayRandomClip(soldierPickupClips, pickupVolume, ref lastPickupClipIndex);

        onCapacityChanged?.Invoke(BoardedCount, MaxSeats);
        if (IsFull)
        {
            onCapacityFull?.Invoke();
            CapacityFullReached?.Invoke();
            PlayRandomClip(capacityFullClips, fullCapacityVolume, ref lastFullClipIndex);
        }
        return true;
    }

    private void TryBoardNearbySoldiers()
    {
        var pickupPoint = GetPickupPointWorld();
        var hitCount = Physics.OverlapSphereNonAlloc(
            pickupPoint,
            Mathf.Max(0.1f, pickupDetectionRadius),
            pickupBuffer,
            ~0,
            QueryTriggerInteraction.Collide);

        for (var i = 0; i < hitCount; i++)
        {
            if (IsFull) break;
            var col = pickupBuffer[i];
            pickupBuffer[i] = null;
            if (col == null) continue;

            var soldier = col.GetComponentInParent<SoldierAgent>();
            if (soldier == null || soldier.IsBoarded) continue;
            if (soldier.AssignedHelicopter != null && soldier.AssignedHelicopter != this) continue;

            var delta = pickupPoint - soldier.transform.position;
            var planar = new Vector2(delta.x, delta.z).magnitude;
            if (planar > pickupDetectionRadius) continue;
            if (Mathf.Abs(delta.y) > pickupDetectionHeight) continue;

            if (soldier.AssignedHelicopter != this)
                soldier.AssignHelicopter(this);

            TryBoardSoldier(soldier);
        }
    }

    private bool CanAssistBoard(SoldierAgent soldier)
    {
        if (!allowAssistedCloseRangeBoarding) return false;
        if (soldier == null) return false;

        var pickupPoint = GetPickupPointWorld();
        var delta = pickupPoint - soldier.transform.position;
        var planarDelta = new Vector2(delta.x, delta.z);
        var planarDistance = planarDelta.magnitude;
        var heightDelta = Mathf.Abs(delta.y);
        return planarDistance <= assistedBoardingPlanarDistance &&
               heightDelta <= assistedBoardingHeightDelta;
    }

    private void EnsureReferences()
    {
        if (!autoAssignReferences) return;
        if (flightController == null) flightController = GetComponent<HelicopterFlightController>();
    }

    public Vector3 GetPickupPointWorld()
    {
        return transform.TransformPoint(pickupPointLocalOffset);
    }

    public void DisembarkAll(Vector3 aroundPoint, float radius = 6f)
    {
        var rescuedNow = boardedSoldiers.Count;
        var hadPassengers = rescuedNow > 0;
        for (var i = 0; i < boardedSoldiers.Count; i++)
        {
            var soldier = boardedSoldiers[i];
            if (soldier == null) continue;
            soldier.AssignHelicopter(null);
        }

        boardedSoldiers.Clear();
        if (rescuedNow > 0)
        {
            totalRescuedCount += rescuedNow;
            SoldiersRescuedAtHelipad?.Invoke(rescuedNow);
        }
        onCapacityChanged?.Invoke(BoardedCount, MaxSeats);
        if (hadPassengers)
            PlayRandomClip(rescueUnloadClips, rescueVolume, ref lastRescueClipIndex);
    }

    private void SnapSoldierToSeat(SoldierAgent soldier, int seatIndex)
    {
        if (seatPoints == null || seatPoints.Length == 0) return;
        if (seatIndex < 0 || seatIndex >= seatPoints.Length) return;

        var seat = seatPoints[seatIndex];
        if (seat == null) return;

        soldier.transform.position = seat.position;
        soldier.transform.rotation = seat.rotation;
    }

    private void EnsureAudioSource()
    {
        if (autoAssignAudioSource && audioSource == null)
        {
            var child = transform.Find("CapacityAudioSource");
            if (child == null)
            {
                var go = new GameObject("CapacityAudioSource");
                go.transform.SetParent(transform, false);
                child = go.transform;
            }

            audioSource = child.GetComponent<AudioSource>();
            if (audioSource == null) audioSource = child.gameObject.AddComponent<AudioSource>();
        }

        if (audioSource == null) return;
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
    }

    private void PlayRandomClip(AudioClip[] clips, float volume, ref int lastIndex)
    {
        if (clips == null || clips.Length == 0) return;
        if (audioSource == null) return;

        var clip = ChooseClip(clips, ref lastIndex);
        if (clip == null) return;
        audioSource.pitch = 1f;
        audioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    private static AudioClip ChooseClip(AudioClip[] clips, ref int lastIndex)
    {
        if (clips == null || clips.Length == 0) return null;
        if (clips.Length == 1)
        {
            lastIndex = 0;
            return clips[0];
        }

        var index = UnityEngine.Random.Range(0, clips.Length);
        if (index == lastIndex) index = (index + 1) % clips.Length;
        lastIndex = index;
        return clips[index];
    }

}
