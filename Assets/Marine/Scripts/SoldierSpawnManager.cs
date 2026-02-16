using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[AddComponentMenu("Gameplay/Soldiers/Soldier Spawner")]
public class SoldierSpawnManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private bool autoAssignReferences = true;
    [ConditionalField("autoAssignReferences", false)]
    [SerializeField] private GameObject marinePrefab;
    [ConditionalField("autoAssignReferences", false)]
    [SerializeField] private HelipadZone helipadZone;
    [ConditionalField("autoAssignReferences", false)]
    [SerializeField] private HelicopterCapacity helicopterCapacity;
    [ConditionalField("autoAssignReferences", false)]
    [SerializeField] private Terrain terrain;
    [ConditionalField("autoAssignReferences", false)]
    [SerializeField] private MapBounds mapBounds;
    [ConditionalField("autoAssignReferences", false)]
    [SerializeField] private Transform soldierParent;

    [Header("Spawn")]
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField, Min(1)] private int soldierCount = 8;

    [Header("Placement")]
    [SerializeField] private bool spawnAcrossMapBounds = true;
    [ConditionalField("spawnAcrossMapBounds", false)]
    [SerializeField, Min(2f)] private float minSpawnRadius = 26f;
    [ConditionalField("spawnAcrossMapBounds", false)]
    [SerializeField, Min(3f)] private float maxSpawnRadius = 65f;
    [ConditionalField("spawnAcrossMapBounds", true)]
    [SerializeField, Min(1f)] private float mapBoundsInset = 20f;

    [Header("Safety")]
    [SerializeField, Min(0f)] private float heightOffset = 0.1f;
    [SerializeField, Min(1f)] private float minTreeDistance = 10f;
    [SerializeField, Min(1f)] private float minSoldierDistance = 3f;
    [SerializeField, Min(5)] private int maxAttemptsPerSoldier = 40;
    [SerializeField] private string treeTag = "Tree";

    [Header("Events")]
    [SerializeField] private UnityEvent onSpawnCompleted;

    private readonly List<SoldierAgent> spawnedSoldiers = new List<SoldierAgent>();
    private readonly List<Transform> treeTransforms = new List<Transform>();

    public IReadOnlyList<SoldierAgent> SpawnedSoldiers => spawnedSoldiers;

    private void Start()
    {
        AutoResolveReferences();
        if (spawnOnStart) SpawnSoldiers();
    }

    [ContextMenu("Spawn Soldiers")]
    public void SpawnSoldiers()
    {
        AutoResolveReferences();
        if (marinePrefab == null)
        {
            Debug.LogWarning("SoldierSpawnManager: Missing marine prefab reference.", this);
            return;
        }

        if (soldierParent == null)
        {
            var parent = new GameObject("Soldiers");
            parent.transform.SetParent(transform, false);
            soldierParent = parent.transform;
        }

        ClearSpawnedSoldiers();
        CacheTreeTransforms();

        for (var i = 0; i < Mathf.Max(1, soldierCount); i++)
        {
            if (!TryFindSpawnPosition(out var spawnPosition))
            {
                Debug.LogWarning($"SoldierSpawnManager: Could not find safe spawn point for soldier {i}.", this);
                continue;
            }

            var instance = Instantiate(marinePrefab, spawnPosition, Quaternion.identity, soldierParent);
            instance.name = $"Marine_{i + 1:00}";

            var soldier = instance.GetComponent<SoldierAgent>();
            if (soldier == null) soldier = instance.AddComponent<SoldierAgent>();
            if (helicopterCapacity != null) soldier.AssignHelicopter(helicopterCapacity);
            spawnedSoldiers.Add(soldier);
        }

        onSpawnCompleted?.Invoke();
    }

    [ContextMenu("Clear Spawned Soldiers")]
    public void ClearSpawnedSoldiers()
    {
        for (var i = 0; i < spawnedSoldiers.Count; i++)
        {
            if (spawnedSoldiers[i] == null) continue;
            if (Application.isPlaying) Destroy(spawnedSoldiers[i].gameObject);
            else DestroyImmediate(spawnedSoldiers[i].gameObject);
        }

        spawnedSoldiers.Clear();
    }

    private void AutoResolveReferences()
    {
        if (!autoAssignReferences) return;
        if (helipadZone == null) helipadZone = FindFirstObjectByType<HelipadZone>();
        if (helicopterCapacity == null) helicopterCapacity = FindFirstObjectByType<HelicopterCapacity>();
        if (terrain == null) terrain = FindFirstObjectByType<Terrain>();
        if (mapBounds == null) mapBounds = FindFirstObjectByType<MapBounds>();
    }

    private void CacheTreeTransforms()
    {
        treeTransforms.Clear();
        GameObject[] trees;
        try
        {
            trees = GameObject.FindGameObjectsWithTag(treeTag);
        }
        catch
        {
            trees = null;
        }

        if (trees == null) return;
        for (var i = 0; i < trees.Length; i++)
            if (trees[i] != null) treeTransforms.Add(trees[i].transform);
    }

    private bool TryFindSpawnPosition(out Vector3 spawnPosition)
    {
        var minR = Mathf.Min(minSpawnRadius, maxSpawnRadius);
        var maxR = Mathf.Max(minSpawnRadius, maxSpawnRadius);
        var minTreeDistSqr = minTreeDistance * minTreeDistance;
        var minSoldierDistSqr = minSoldierDistance * minSoldierDistance;
        var center = helipadZone != null ? helipadZone.transform.position : transform.position;

        for (var attempt = 0; attempt < Mathf.Max(1, maxAttemptsPerSoldier); attempt++)
        {
            var candidate = BuildCandidate(center, minR, maxR);
            if (mapBounds != null && !mapBounds.IsWithinSoftBounds(candidate, mapBoundsInset))
                continue;
            candidate.y = SampleGroundY(candidate) + heightOffset;

            if (!IsAwayFromTrees(candidate, minTreeDistSqr)) continue;
            if (!IsAwayFromSoldiers(candidate, minSoldierDistSqr)) continue;

            spawnPosition = candidate;
            return true;
        }

        spawnPosition = center;
        spawnPosition.y = SampleGroundY(spawnPosition) + heightOffset;
        return false;
    }

    private Vector3 BuildCandidate(Vector3 fallbackCenter, float minR, float maxR)
    {
        if (spawnAcrossMapBounds && mapBounds != null)
        {
            var angle = Random.Range(0f, Mathf.PI * 2f);
            var radius = Mathf.Sqrt(Random.Range(0f, 1f)) * Mathf.Max(1f, mapBounds.SoftRadius - mapBoundsInset);
            var center = mapBounds.BoundsCenter;
            var world = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            return world;
        }

        var nearAngle = Random.Range(0f, Mathf.PI * 2f);
        var nearRadius = Random.Range(minR, maxR);
        return fallbackCenter + new Vector3(Mathf.Cos(nearAngle) * nearRadius, 0f, Mathf.Sin(nearAngle) * nearRadius);
    }

    private float SampleGroundY(Vector3 worldPoint)
    {
        if (terrain != null)
            return terrain.SampleHeight(worldPoint) + terrain.transform.position.y;

        if (Physics.Raycast(worldPoint + Vector3.up * 1000f, Vector3.down, out var hit, 2000f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point.y;

        return worldPoint.y;
    }

    private bool IsAwayFromTrees(Vector3 candidate, float minTreeDistSqr)
    {
        for (var i = 0; i < treeTransforms.Count; i++)
        {
            var tree = treeTransforms[i];
            if (tree == null) continue;
            var delta = tree.position - candidate;
            delta.y = 0f;
            if (delta.sqrMagnitude < minTreeDistSqr) return false;
        }

        return true;
    }

    private bool IsAwayFromSoldiers(Vector3 candidate, float minSoldierDistSqr)
    {
        for (var i = 0; i < spawnedSoldiers.Count; i++)
        {
            var soldier = spawnedSoldiers[i];
            if (soldier == null) continue;
            var delta = soldier.transform.position - candidate;
            delta.y = 0f;
            if (delta.sqrMagnitude < minSoldierDistSqr) return false;
        }

        return true;
    }
}
