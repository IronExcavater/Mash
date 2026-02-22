using UnityEngine;

[ExecuteAlways]
[AddComponentMenu("World/World Generation Callbacks")]
public class WorldGenerationCallbacks : MonoBehaviour
{
    [SerializeField] private TerrainGenerator terrainGenerator;
    [SerializeField] private MilitaryBaseGenerator militaryBaseGenerator;
    [SerializeField] private HelicopterFlightController helicopterFlight;
    [SerializeField] private bool applyStartupHeightAfterTerrainGeneration = true;
    [SerializeField] private bool applyStartupHeightAfterBaseBuild = true;

    private void OnEnable()
    {
        CacheReferences();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnValidate()
    {
        CacheReferences();
    }

    private void Subscribe()
    {
        if (terrainGenerator != null)
            terrainGenerator.GenerationCompleted += HandleTerrainGenerationCompleted;
        if (militaryBaseGenerator != null)
            militaryBaseGenerator.BuildCompleted += HandleBaseBuildCompleted;
    }

    private void Unsubscribe()
    {
        if (terrainGenerator != null)
            terrainGenerator.GenerationCompleted -= HandleTerrainGenerationCompleted;
        if (militaryBaseGenerator != null)
            militaryBaseGenerator.BuildCompleted -= HandleBaseBuildCompleted;
    }

    private void HandleTerrainGenerationCompleted()
    {
        if (!applyStartupHeightAfterTerrainGeneration) return;
        ApplyHelicopterStartupPlacement();
    }

    private void HandleBaseBuildCompleted()
    {
        if (!applyStartupHeightAfterBaseBuild) return;
        ApplyHelicopterStartupPlacement();
    }

    private void ApplyHelicopterStartupPlacement()
    {
        helicopterFlight ??= FindFirstObjectByType<HelicopterFlightController>();
        if (helicopterFlight == null) return;
        helicopterFlight.ReapplyStartupPlacementNow();
    }

    private void CacheReferences()
    {
        terrainGenerator ??= FindFirstObjectByType<TerrainGenerator>();
        militaryBaseGenerator ??= FindFirstObjectByType<MilitaryBaseGenerator>();
        helicopterFlight ??= FindFirstObjectByType<HelicopterFlightController>();
    }
}
