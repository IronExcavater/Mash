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
    [SerializeField] private MilitaryBaseGenerator militaryBase;
    [ConditionalField("autoAssignReferences", false)]
    [SerializeField] private Transform soldierParent;
    [Header("Animation")]
    [SerializeField] private bool configureSoldierAnimationOnSpawn = true;
    [ConditionalField("configureSoldierAnimationOnSpawn", true)]
    [SerializeField] private RuntimeAnimatorController soldierAnimatorController;
    [ConditionalField("configureSoldierAnimationOnSpawn", true)]
    [SerializeField] private Avatar soldierAvatar;

    [Header("Spawn")]
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField, Min(1)] private int soldierCount = 20;
    [SerializeField, Min(0.5f)] private float spawnedSoldierScale = 2f;

    [Header("Placement")]
    [SerializeField] private bool spawnAcrossMapBounds = true;
    [ConditionalField("spawnAcrossMapBounds", false)]
    [SerializeField, Min(2f)] private float minSpawnRadius = 26f;
    [ConditionalField("spawnAcrossMapBounds", false)]
    [SerializeField, Min(3f)] private float maxSpawnRadius = 65f;
    [ConditionalField("spawnAcrossMapBounds", true)]
    [SerializeField, Min(1f)] private float mapBoundsInset = 20f;
    [SerializeField] private bool excludeMilitaryBaseArea = true;
    [ConditionalField("excludeMilitaryBaseArea", true)]
    [SerializeField, Min(0f)] private float militaryBaseExclusionPadding = 16f;

    [Header("Safety")]
    [SerializeField, Min(0f)] private float heightOffset = 0.1f;
    [SerializeField, Min(1f)] private float minTreeDistance = 10f;
    [SerializeField, Min(1f)] private float minSoldierDistance = 3f;
    [TagSelector]
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

        var requestedCount = Mathf.Max(1, soldierCount);
        var spawnPositions = BuildDeterministicSpawnPositions(requestedCount);
        for (var i = 0; i < spawnPositions.Count; i++)
        {
            var spawnPosition = spawnPositions[i];
            var instance = Instantiate(marinePrefab, spawnPosition, Quaternion.identity, soldierParent);
            instance.name = $"Marine_{i + 1:00}";
            instance.transform.localScale *= spawnedSoldierScale;

            var soldier = instance.GetComponent<SoldierAgent>();
            if (soldier == null) soldier = instance.AddComponent<SoldierAgent>();
            if (configureSoldierAnimationOnSpawn && (soldierAnimatorController != null || soldierAvatar != null))
                soldier.ConfigureAnimation(soldierAnimatorController, soldierAvatar);
            if (helicopterCapacity != null) soldier.AssignHelicopter(helicopterCapacity);
            spawnedSoldiers.Add(soldier);
        }

        if (spawnPositions.Count < requestedCount)
            Debug.LogWarning($"SoldierSpawnManager: Spawned {spawnPositions.Count}/{requestedCount} soldiers. Check map size, bounds inset, and separation settings.", this);

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
        helipadZone ??= FindFirstObjectByType<HelipadZone>();
        helicopterCapacity ??= FindFirstObjectByType<HelicopterCapacity>();
        terrain ??= FindFirstObjectByType<Terrain>();
        mapBounds ??= FindFirstObjectByType<MapBounds>();
        militaryBase ??= FindFirstObjectByType<MilitaryBaseGenerator>();
    }

    private void CacheTreeTransforms()
    {
        treeTransforms.Clear();
        var trees = GameObject.FindGameObjectsWithTag(treeTag);
        if (trees == null) return;
        for (var i = 0; i < trees.Length; i++)
            if (trees[i] != null) treeTransforms.Add(trees[i].transform);
    }

    private List<Vector3> BuildDeterministicSpawnPositions(int requestedCount)
    {
        var result = new List<Vector3>(requestedCount);
        if (requestedCount <= 0) return result;

        var minR = Mathf.Min(minSpawnRadius, maxSpawnRadius);
        var maxR = Mathf.Max(minSpawnRadius, maxSpawnRadius);
        var center = helipadZone != null ? helipadZone.transform.position : transform.position;
        var candidateCount = Mathf.Max(256, requestedCount * 64);
        var candidates = GenerateDeterministicCandidates(center, minR, maxR, candidateCount);

        var required = Mathf.Max(1, requestedCount);
        var minTree = Mathf.Max(0f, minTreeDistance);
        var minSoldier = Mathf.Max(0f, minSoldierDistance);

        for (var relaxStep = 0; relaxStep < 6 && result.Count < required; relaxStep++)
        {
            var minTreeSqr = minTree * minTree;
            var minSoldierSqr = minSoldier * minSoldier;
            for (var i = 0; i < candidates.Count && result.Count < required; i++)
            {
                var candidate = candidates[i];
                if (mapBounds != null && !mapBounds.IsWithinSoftBounds(candidate, mapBoundsInset)) continue;
                if (!IsOutsideMilitaryBase(candidate)) continue;
                candidate.y = SampleGroundY(candidate) + heightOffset;

                if (!IsAwayFromTrees(candidate, minTreeSqr)) continue;
                if (!IsAwayFromPoints(candidate, result, minSoldierSqr)) continue;
                result.Add(candidate);
            }

            minTree *= 0.8f;
            minSoldier *= 0.8f;
        }

        if (result.Count >= required) return result;

        // Deterministic greedy fill: keep widest spacing possible when constraints are tight.
        while (result.Count < required)
        {
            var bestIndex = -1;
            var bestScore = float.NegativeInfinity;
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (mapBounds != null && !mapBounds.IsWithinSoftBounds(candidate, mapBoundsInset)) continue;
                if (!IsOutsideMilitaryBase(candidate)) continue;
                candidate.y = SampleGroundY(candidate) + heightOffset;
                if (!IsAwayFromPoints(candidate, result, 0.25f)) continue;

                var score = ScoreCandidate(candidate, result);
                if (score <= bestScore) continue;
                bestScore = score;
                bestIndex = i;
            }

            if (bestIndex < 0) break;
            var chosen = candidates[bestIndex];
            chosen.y = SampleGroundY(chosen) + heightOffset;
            result.Add(chosen);
            candidates.RemoveAt(bestIndex);
        }

        return result;
    }

    private List<Vector3> GenerateDeterministicCandidates(Vector3 center, float minR, float maxR, int count)
    {
        var points = new List<Vector3>(count);
        const float goldenAngle = 2.39996323f;

        if ((spawnAcrossMapBounds || excludeMilitaryBaseArea) && mapBounds != null)
        {
            var boundsCenter = mapBounds.BoundsCenter;
            var radiusLimit = Mathf.Max(1f, mapBounds.SoftRadius - mapBoundsInset);
            var boundsRingMin = Mathf.Clamp(minR, 0f, radiusLimit - 0.25f);
            if (excludeMilitaryBaseArea && militaryBase != null)
            {
                var exclusionRadius = Mathf.Max(0f, militaryBase.ProtectedRadius + militaryBaseExclusionPadding);
                boundsRingMin = Mathf.Clamp(Mathf.Max(boundsRingMin, exclusionRadius), 0f, radiusLimit - 0.25f);
            }

            for (var i = 0; i < count; i++)
            {
                var t = Mathf.Sqrt((i + 0.5f) / count);
                var radius = Mathf.Lerp(boundsRingMin, radiusLimit, t);
                var angle = i * goldenAngle;
                points.Add(boundsCenter + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }
            return points;
        }

        var ringMin = Mathf.Max(0f, minR);
        var ringMax = Mathf.Max(ringMin + 0.1f, maxR);
        for (var i = 0; i < count; i++)
        {
            var t = (i + 0.5f) / count;
            var radius = Mathf.Lerp(ringMin, ringMax, t);
            var angle = i * goldenAngle;
            points.Add(center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
        }

        return points;
    }

    private float SampleGroundY(Vector3 worldPoint)
    {
        if (terrain != null)
            return terrain.SampleHeight(worldPoint) + terrain.transform.position.y;
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

    private static bool IsAwayFromPoints(Vector3 candidate, List<Vector3> points, float minDistanceSqr)
    {
        for (var i = 0; i < points.Count; i++)
        {
            var delta = points[i] - candidate;
            delta.y = 0f;
            if (delta.sqrMagnitude < minDistanceSqr) return false;
        }

        return true;
    }

    private float ScoreCandidate(Vector3 candidate, List<Vector3> selected)
    {
        var minSelectedDistance = float.PositiveInfinity;
        for (var i = 0; i < selected.Count; i++)
        {
            var delta = selected[i] - candidate;
            delta.y = 0f;
            var d = delta.magnitude;
            if (d < minSelectedDistance) minSelectedDistance = d;
        }

        var minTreeDistanceValue = float.PositiveInfinity;
        for (var i = 0; i < treeTransforms.Count; i++)
        {
            var tree = treeTransforms[i];
            if (tree == null) continue;
            var delta = tree.position - candidate;
            delta.y = 0f;
            var d = delta.magnitude;
            if (d < minTreeDistanceValue) minTreeDistanceValue = d;
        }

        if (float.IsPositiveInfinity(minSelectedDistance)) minSelectedDistance = 1000f;
        if (float.IsPositiveInfinity(minTreeDistanceValue)) minTreeDistanceValue = 1000f;

        return minSelectedDistance * 1.0f + minTreeDistanceValue * 0.35f;
    }

    private bool IsOutsideMilitaryBase(Vector3 candidate)
    {
        if (!excludeMilitaryBaseArea || militaryBase == null) return true;
        var center = militaryBase.BaseCenterWorld;
        var exclusionRadius = Mathf.Max(0f, militaryBase.ProtectedRadius + militaryBaseExclusionPadding);
        if (exclusionRadius <= 0f) return true;

        var delta = candidate - center;
        delta.y = 0f;
        return delta.sqrMagnitude >= exclusionRadius * exclusionRadius;
    }
}
