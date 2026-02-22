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
    [SerializeField] private TerrainGenerator terrainGenerator;
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
    [SerializeField] private bool waitForTerrainGeneration = true;
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
    private bool subscribedToTerrainEvents;

    public IReadOnlyList<SoldierAgent> SpawnedSoldiers => spawnedSoldiers;

    private void Start()
    {
        AutoResolveReferences();
        if (!spawnOnStart) return;
        SpawnWhenReady();
    }

    private void OnEnable()
    {
        if (!spawnOnStart || !waitForTerrainGeneration) return;
        AutoResolveReferences();
        if (terrainGenerator != null && terrainGenerator.IsGenerating)
            SubscribeTerrainEvents();
    }

    private void OnDisable()
    {
        UnsubscribeTerrainEvents();
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
        var prefabAnimator = marinePrefab.GetComponentInChildren<Animator>(true);
        for (var i = 0; i < spawnPositions.Count; i++)
        {
            var spawnPosition = spawnPositions[i];
            var instance = Instantiate(marinePrefab, spawnPosition, Quaternion.identity, soldierParent);
            instance.name = $"Marine_{i + 1:00}";
            instance.transform.localScale *= spawnedSoldierScale;
            SnapInstanceToGround(instance);

            var soldier = instance.GetComponent<SoldierAgent>();
            if (soldier == null) soldier = instance.AddComponent<SoldierAgent>();
            ConfigureSpawnedSoldierAnimation(soldier, instance, prefabAnimator);
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
        terrainGenerator ??= FindFirstObjectByType<TerrainGenerator>();
        mapBounds ??= FindFirstObjectByType<MapBounds>();
        militaryBase ??= FindFirstObjectByType<MilitaryBaseGenerator>();
    }

    private void SubscribeTerrainEvents()
    {
        if (subscribedToTerrainEvents) return;
        if (terrainGenerator == null) return;

        terrainGenerator.GenerationCompleted += HandleTerrainGenerationCompleted;
        terrainGenerator.GenerationFailed += HandleTerrainGenerationFailed;
        subscribedToTerrainEvents = true;
    }

    private void UnsubscribeTerrainEvents()
    {
        if (!subscribedToTerrainEvents) return;
        if (terrainGenerator != null)
        {
            terrainGenerator.GenerationCompleted -= HandleTerrainGenerationCompleted;
            terrainGenerator.GenerationFailed -= HandleTerrainGenerationFailed;
        }
        subscribedToTerrainEvents = false;
    }

    private void HandleTerrainGenerationCompleted()
    {
        UnsubscribeTerrainEvents();
        SpawnWhenReady();
    }

    private void HandleTerrainGenerationFailed(string _)
    {
        UnsubscribeTerrainEvents();
        SpawnWhenReady();
    }

    private void CacheTreeTransforms()
    {
        treeTransforms.Clear();
        var trees = GameObject.FindGameObjectsWithTag(treeTag);
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
            if (candidates.Count == 0) break;

            // Deterministic distribution pass with low CPU cost.
            var stride = Mathf.Max(1, candidates.Count / Mathf.Max(1, required));
            var offset = (relaxStep * 17) % candidates.Count;
            for (var i = 0; i < candidates.Count && result.Count < required; i++)
            {
                var index = (offset + i * stride) % candidates.Count;
                if (TryAcceptCandidate(candidates[index], result, minTreeSqr, minSoldierSqr, out var accepted))
                    result.Add(accepted);
            }

            minTree *= 0.8f;
            minSoldier *= 0.8f;
        }

        if (result.Count >= required) return result;

        // Fast fallback fill when strict spacing cannot satisfy requested count.
        while (result.Count < required)
        {
            var chosenIndex = -1;
            var chosen = Vector3.zero;
            for (var i = 0; i < candidates.Count; i++)
            {
                if (!TryAcceptCandidate(candidates[i], result, 0f, 0.25f, out chosen)) continue;
                chosenIndex = i;
                break;
            }

            if (chosenIndex < 0) break;
            result.Add(chosen);
            candidates.RemoveAt(chosenIndex);
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
        terrain ??= Terrain.activeTerrain;
        terrain ??= FindFirstObjectByType<Terrain>();
        if (terrain != null)
            return terrain.SampleHeight(worldPoint) + terrain.transform.position.y;

        if (Physics.Raycast(worldPoint + Vector3.up * 1000f, Vector3.down, out var hit, 2000f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point.y;

        return worldPoint.y;
    }

    private void ConfigureSpawnedSoldierAnimation(SoldierAgent soldier, GameObject instance, Animator prefabAnimator)
    {
        if (!configureSoldierAnimationOnSpawn) return;
        if (soldier == null || instance == null) return;

        RuntimeAnimatorController controller = soldierAnimatorController;
        Avatar avatar = soldierAvatar;
        var instanceAnimator = instance.GetComponentInChildren<Animator>(true);

        controller ??= prefabAnimator != null ? prefabAnimator.runtimeAnimatorController : null;
        avatar ??= prefabAnimator != null ? prefabAnimator.avatar : null;

        if (controller == null && avatar == null && instanceAnimator == null) return;

        soldier.ConfigureAnimation(controller, avatar);
        if (instanceAnimator == null) return;

        instanceAnimator.Rebind();
        instanceAnimator.Update(0f);
    }

    private void SpawnWhenReady()
    {
        if (waitForTerrainGeneration && terrainGenerator != null && terrainGenerator.IsGenerating)
        {
            SubscribeTerrainEvents();
            return;
        }

        SpawnSoldiers();
    }

    private void SnapInstanceToGround(GameObject instance)
    {
        if (instance == null) return;

        var position = instance.transform.position;
        var targetGroundY = SampleGroundY(position) + heightOffset;
        var lowestY = FindLowestPointY(instance);
        if (float.IsInfinity(lowestY) || float.IsNaN(lowestY))
            lowestY = position.y;

        var shiftY = targetGroundY - lowestY;
        if (Mathf.Abs(shiftY) < 0.0001f) return;
        instance.transform.position = position + Vector3.up * shiftY;
    }

    private static float FindLowestPointY(GameObject instance)
    {
        var lowestY = float.PositiveInfinity;

        var renderers = instance.GetComponentsInChildren<Renderer>(true);
        for (var i = 0; i < renderers.Length; i++)
        {
            var renderer = renderers[i];
            if (renderer == null) continue;
            lowestY = Mathf.Min(lowestY, renderer.bounds.min.y);
        }

        var colliders = instance.GetComponentsInChildren<Collider>(true);
        for (var i = 0; i < colliders.Length; i++)
        {
            var collider = colliders[i];
            if (collider == null) continue;
            lowestY = Mathf.Min(lowestY, collider.bounds.min.y);
        }

        return lowestY;
    }

    private bool TryAcceptCandidate(Vector3 candidate, List<Vector3> existing, float minTreeSqr, float minSoldierSqr, out Vector3 accepted)
    {
        accepted = candidate;
        if (mapBounds != null && !mapBounds.IsWithinSoftBounds(candidate, mapBoundsInset))
            return false;
        if (!IsOutsideMilitaryBase(candidate))
            return false;

        accepted.y = SampleGroundY(candidate) + heightOffset;
        if (minTreeSqr > 0f && !IsAwayFromTrees(accepted, minTreeSqr))
            return false;
        if (minSoldierSqr > 0f && !IsAwayFromPoints(accepted, existing, minSoldierSqr))
            return false;
        return true;
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
