using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[RequireComponent(typeof(SphereCollider))]
[AddComponentMenu("Gameplay/Helicopter/Boarding Zone")]
public class HelipadZone : MonoBehaviour
{
    [Header("Zone")]
    [SerializeField, Min(1f)] private float zoneRadius = 14f;
    [SerializeField] private bool autoConfigureTrigger = true;
    [ConditionalField("autoConfigureTrigger", true)]
    [SerializeField] private float triggerCenterY = 1.2f;

    [Header("Boarding")]
    [SerializeField] private bool autoAssignSoldiersToHelicopter = true;
    [SerializeField] private bool autoBoardOnLanding = true;

    [Header("Events")]
    [SerializeField] private UnityEvent onHelicopterEntered;
    [SerializeField] private UnityEvent onHelicopterExited;
    [SerializeField] private UnityEvent onBoardingTick;

    private readonly HashSet<SoldierAgent> soldiersInZone = new HashSet<SoldierAgent>();
    private HelicopterCapacity helicopterInZone;
    private SphereCollider cachedSphere;

    public HelicopterCapacity HelicopterInZone => helicopterInZone;
    public int SoldiersWaitingCount => soldiersInZone.Count;

    private void Reset()
    {
        EnsureTriggerVolume();
    }

    private void Awake()
    {
        EnsureTriggerVolume();
    }

    private void Update()
    {
        if (helicopterInZone == null) return;
        if (!autoBoardOnLanding) return;
        if (!helicopterInZone.CanBoard()) return;

        TryBoardWaitingSoldiers();
    }

    public void SetZoneRadius(float radius)
    {
        zoneRadius = Mathf.Max(1f, radius);
        EnsureTriggerVolume();
    }

    public void TryBoardWaitingSoldiers()
    {
        if (helicopterInZone == null) return;

        var removal = ListPool<SoldierAgent>.Get();
        foreach (var soldier in soldiersInZone)
        {
            if (soldier == null)
            {
                removal.Add(soldier);
                continue;
            }

            if (autoAssignSoldiersToHelicopter && soldier.AssignedHelicopter != helicopterInZone)
                soldier.AssignHelicopter(helicopterInZone);

            if (!helicopterInZone.CanBoard()) continue;
            var boarded = helicopterInZone.TryBoardSoldier(soldier);
            if (boarded) removal.Add(soldier);
        }

        for (var i = 0; i < removal.Count; i++)
            soldiersInZone.Remove(removal[i]);
        ListPool<SoldierAgent>.Release(removal);

        onBoardingTick?.Invoke();
    }

    private void OnTriggerEnter(Collider other)
    {
        var helicopter = other.GetComponentInParent<HelicopterCapacity>();
        if (helicopter != null)
        {
            helicopterInZone = helicopter;
            onHelicopterEntered?.Invoke();
        }

        var soldier = other.GetComponentInParent<SoldierAgent>();
        if (soldier != null && !soldier.IsBoarded)
        {
            soldiersInZone.Add(soldier);
            if (autoAssignSoldiersToHelicopter && helicopterInZone != null)
                soldier.AssignHelicopter(helicopterInZone);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var helicopter = other.GetComponentInParent<HelicopterCapacity>();
        if (helicopter != null && helicopter == helicopterInZone)
        {
            helicopterInZone = null;
            onHelicopterExited?.Invoke();
        }

        var soldier = other.GetComponentInParent<SoldierAgent>();
        if (soldier != null) soldiersInZone.Remove(soldier);
    }

    private void EnsureTriggerVolume()
    {
        cachedSphere = GetComponent<SphereCollider>();
        if (cachedSphere == null)
        {
            cachedSphere = gameObject.AddComponent<SphereCollider>();
        }

        if (autoConfigureTrigger) cachedSphere.isTrigger = true;
        cachedSphere.radius = Mathf.Max(1f, zoneRadius);
        cachedSphere.center = Vector3.up * triggerCenterY;
    }

    private static class ListPool<T>
    {
        private static readonly Stack<List<T>> Pool = new Stack<List<T>>();

        public static List<T> Get()
        {
            return Pool.Count > 0 ? Pool.Pop() : new List<T>();
        }

        public static void Release(List<T> list)
        {
            list.Clear();
            Pool.Push(list);
        }
    }
}
