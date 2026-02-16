using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[AddComponentMenu("Gameplay/Helicopter/Capacity")]
public class HelicopterCapacity : MonoBehaviour
{
    [Header("Capacity")]
    [SerializeField] private int maxSeats = 8;

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
    [SerializeField, Min(0f)] private float assistedBoardingHeightDelta = 8f;

    [Header("References")]
    [SerializeField] private bool autoAssignReferences = true;
    [ConditionalField("autoAssignReferences", false)]
    [SerializeField] private HelicopterFlightController flightController;

    [Header("Seats")]
    [SerializeField] private Transform[] seatPoints;

    [Header("Events")]
    [SerializeField] private UnityEvent<int, int> onCapacityChanged;
    [SerializeField] private UnityEvent onCapacityFull;

    private readonly List<SoldierAgent> boardedSoldiers = new List<SoldierAgent>();

    public int MaxSeats => Mathf.Max(1, maxSeats);
    public int BoardedCount => boardedSoldiers.Count;
    public int RemainingSeats => Mathf.Max(0, MaxSeats - boardedSoldiers.Count);
    public bool IsFull => boardedSoldiers.Count >= MaxSeats;

    private void Awake()
    {
        if (autoAssignReferences && flightController == null)
            flightController = GetComponent<HelicopterFlightController>();
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
        SnapSoldierToSeat(soldier, boardedSoldiers.Count - 1);
        soldier.Board();

        onCapacityChanged?.Invoke(BoardedCount, MaxSeats);
        if (IsFull) onCapacityFull?.Invoke();
        return true;
    }

    private bool CanAssistBoard(SoldierAgent soldier)
    {
        if (!allowAssistedCloseRangeBoarding) return false;
        if (soldier == null) return false;

        var delta = transform.position - soldier.transform.position;
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

    public void DisembarkAll(Vector3 aroundPoint, float radius = 6f)
    {
        for (var i = 0; i < boardedSoldiers.Count; i++)
        {
            var soldier = boardedSoldiers[i];
            if (soldier == null) continue;

            var angle = (360f / Mathf.Max(1, boardedSoldiers.Count)) * i * Mathf.Deg2Rad;
            var offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            soldier.transform.position = aroundPoint + offset;
            soldier.gameObject.SetActive(true);
            soldier.AssignHelicopter(null);
        }

        boardedSoldiers.Clear();
        onCapacityChanged?.Invoke(BoardedCount, MaxSeats);
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
}
