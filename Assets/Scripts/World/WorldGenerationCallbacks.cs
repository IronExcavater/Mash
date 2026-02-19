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
        AutoResolveReferences();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnValidate()
    {
        AutoResolveReferences();
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
        if (helicopterFlight == null)
            helicopterFlight = FindFirstObjectByType<HelicopterFlightController>();
        if (helicopterFlight == null) return;
        helicopterFlight.ReapplyStartupPlacementNow();
    }

    private void AutoResolveReferences()
    {
        if (terrainGenerator == null)
            terrainGenerator = FindFirstObjectByType<TerrainGenerator>();
        if (militaryBaseGenerator == null)
            militaryBaseGenerator = FindFirstObjectByType<MilitaryBaseGenerator>();
        if (helicopterFlight == null)
            helicopterFlight = FindFirstObjectByType<HelicopterFlightController>();
    }
}
