using System;
using UnityEngine;
using UnityEngine.Events;
#if UNITY_EDITOR
using UnityEditor;
#endif

[AddComponentMenu("World/Terrain Generator")]
public class TerrainGenerator : MonoBehaviour
{
    public enum GenerationMode
    {
        Manual = 2,
        OnMissing = 1,
        OnSeedChanged = 3,
        OnStart = 0
    }

    [Header("Generation")]
    [SerializeField] private GenerationMode generationMode = GenerationMode.OnMissing;
    [ConditionalField("generationMode", (int)GenerationMode.Manual)]
    [SerializeField] private Terrain targetTerrain;
    [SerializeField] private TerrainBiomeProfile biomeProfile;

    [Header("Seed")]
    [SerializeField] private int seed = 0;
    private int activeGenerationSeed;
    private int lastGeneratedSeed = int.MinValue;

    [Header("Events")]
    [SerializeField] private UnityEvent onGenerationStarted;
    [SerializeField] private UnityEvent onGenerationCompleted;
    [SerializeField] private UnityEvent<string> onGenerationFailed;
    [Header("Actions")]
    [InspectorButton(nameof(GenerateTerrain), "Generate Terrain")]
    [SerializeField] private bool generateTerrainNowButton;
    [InspectorButton(nameof(GenerateNewSeed), "Generate New Seed")]
    [SerializeField] private bool generateNewSeedButton;
    public event System.Action GenerationStarted;
    public event System.Action GenerationCompleted;
    public event System.Action<string> GenerationFailed;

#if UNITY_EDITOR
    private bool pendingEditorSeedRefresh;
#endif

    private void Awake()
    {
        EnsureSeedInitialized();
    }

    private void Reset()
    {
        seed = CreateRandomSeed();
    }

    [ContextMenu("Generate Terrain")]
    public void GenerateTerrain()
    {
        if (biomeProfile == null)
        {
            RaiseFailed("No TerrainBiomeProfile assigned.");
            return;
        }

        RaiseStarted();
        activeGenerationSeed = seed;
        lastGeneratedSeed = activeGenerationSeed;
        var terrain = ResolveTerrainForGeneration(createIfMissing: true);
        if (terrain == null)
        {
            RaiseFailed("No terrain available to generate.");
            return;
        }

        var data = EnsureTerrainData(terrain);
        var resolution = Mathf.ClosestPowerOfTwo(Mathf.Clamp(biomeProfile.heightmapResolution - 1, 32, 4096)) + 1;
        data.heightmapResolution = resolution;
        data.size = new Vector3(biomeProfile.terrainWidth, biomeProfile.terrainHeight, biomeProfile.terrainLength);
        data.SetHeights(0, 0, BuildHeightMap(resolution));
        var layers = ResolveTerrainLayers();
        data.terrainLayers = layers;
        PaintLayerBlending(data);

        SyncTerrainColliderData(terrain);
        ApplyTerrainTransform(terrain);
        ApplyTerrainDrawDistances(terrain);
        ApplyTerrainMaterialTemplate(terrain);
        terrain.Flush();

        if (ShouldGenerateEnvironment())
            GenerateEnvironment(terrain);
        RaiseCompleted();
    }

    private void Start()
    {
        RunConfiguredGenerationMode();
    }

    private void RunConfiguredGenerationMode()
    {
        switch (generationMode)
        {
            case GenerationMode.Manual:
                if (targetTerrain != null) SyncTerrainColliderData(targetTerrain);
                break;
            case GenerationMode.OnMissing:
            {
                var foundTerrain = FindFirstObjectByType<Terrain>();
                if (foundTerrain == null)
                {
                    GenerateTerrain();
                }
                else
                {
                    if (targetTerrain == null) targetTerrain = foundTerrain;
                    if (IsTerrainMissingCriticalData(foundTerrain)) GenerateTerrain();
                    else SyncTerrainColliderData(foundTerrain);
                }
                break;
            }
            case GenerationMode.OnSeedChanged:
            {
                if (targetTerrain == null) targetTerrain = FindFirstObjectByType<Terrain>();
                if (targetTerrain != null) SyncTerrainColliderData(targetTerrain);
                if (targetTerrain == null || IsTerrainUninitialized(targetTerrain) || HasSeedChangedSinceLastGeneration())
                    GenerateTerrain();
                break;
            }
            case GenerationMode.OnStart:
            {
                GenerateTerrain();
                break;
            }
        }
    }

    private Terrain ResolveTerrainForGeneration(bool createIfMissing)
    {
        if (targetTerrain == null)
            targetTerrain = FindFirstObjectByType<Terrain>();

        if (targetTerrain != null) return targetTerrain;
        if (!createIfMissing)
        {
            Debug.LogWarning("TerrainGenerator: No terrain found and creation is disabled for this call.", this);
            return null;
        }

        var terrainObject = new GameObject("Terrain");
        var terrain = terrainObject.AddComponent<Terrain>();
        terrainObject.AddComponent<TerrainCollider>();
        targetTerrain = terrain;
        return targetTerrain;
    }

    private TerrainData EnsureTerrainData(Terrain terrain)
    {
        if (terrain.terrainData != null) return terrain.terrainData;
        var data = new TerrainData();
        terrain.terrainData = data;
        return data;
    }

    private float[,] BuildHeightMap(int resolution)
    {
        var heights = new float[resolution, resolution];
        var rng = new System.Random(activeGenerationSeed);
        var sx = (float)rng.NextDouble() * 1000f;
        var sy = (float)rng.NextDouble() * 1000f;
        var mx = (float)rng.NextDouble() * 1000f;
        var my = (float)rng.NextDouble() * 1000f;
        var radians = biomeProfile.duneDirectionDegrees * Mathf.Deg2Rad;
        var duneDir = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)).normalized;

        var relief = Mathf.Clamp01(biomeProfile.terrainRelief);
        var duneHeight = Mathf.Clamp01(biomeProfile.duneHeight);
        var duneVariation = Mathf.Clamp01(biomeProfile.duneVariation);
        var edgeFlattening = Mathf.Clamp01(biomeProfile.edgeFlattening);
        var mountainHeight = Mathf.Clamp01(biomeProfile.mountainHeight);
        var mountainWidth = Mathf.Clamp(biomeProfile.mountainWidthFromEdge01, 0.05f, 0.5f);
        var mountainVariation = Mathf.Clamp01(biomeProfile.mountainVariation);
        var duneAmplitude = Mathf.Lerp(0.06f, 0.24f, duneHeight) * Mathf.Lerp(0.7f, 1f, relief);
        var mountainBase = Mathf.Lerp(0.1f, 0.32f, mountainHeight);
        var mountainDetailAmplitude = Mathf.Lerp(0.08f, 0.34f, mountainHeight);
        var mountainNoiseFrequency = Mathf.Lerp(0.0009f, 0.0034f, mountainVariation);
        var mountainRidgeSharpness = Mathf.Lerp(2.7f, 1.35f, mountainVariation);

        var minHeight = float.MaxValue;
        var maxHeight = float.MinValue;
        for (var y = 0; y < resolution; y++)
        {
            for (var x = 0; x < resolution; x++)
            {
                var nx = (float)x / (resolution - 1);
                var ny = (float)y / (resolution - 1);
                var worldX = nx * biomeProfile.terrainWidth;
                var worldY = ny * biomeProfile.terrainLength;

                var dunes = ComputeDunes(
                    worldX,
                    worldY,
                    duneDir,
                    biomeProfile.duneFrequency,
                    duneVariation,
                    sx,
                    sy);
                dunes *= duneAmplitude;
                dunes *= 1f - ComputeEdgeMask(nx, ny) * edgeFlattening;

                var edgeDistance = Mathf.Min(Mathf.Min(nx, 1f - nx), Mathf.Min(ny, 1f - ny)); // 0=edge, 0.5=center
                var mountainBandBase = 1f - Mathf.SmoothStep(mountainWidth * 0.2f, mountainWidth, edgeDistance);
                var mountainBand = Mathf.Pow(Mathf.Clamp01(mountainBandBase), 0.75f);
                var n1 = Mathf.PerlinNoise(mx + worldX * mountainNoiseFrequency, my + worldY * mountainNoiseFrequency);
                var n2 = Mathf.PerlinNoise(mx * 0.63f + worldX * mountainNoiseFrequency * 2.8f, my * 0.63f + worldY * mountainNoiseFrequency * 2.8f);
                var mountainNoise = Mathf.Lerp(n1, n2, 0.45f);
                var ridge = Mathf.Pow(Mathf.Abs(mountainNoise * 2f - 1f), mountainRidgeSharpness);
                var borderUplift = Mathf.Pow(1f - Mathf.SmoothStep(0f, mountainWidth * 0.6f, edgeDistance), 1.2f) * mountainHeight * 0.22f;
                var mountains = mountainBand * (mountainBase + mountainDetailAmplitude * ridge) + borderUplift;

                var h = dunes + mountains;
                heights[y, x] = h;
                if (h < minHeight) minHeight = h;
                if (h > maxHeight) maxHeight = h;
            }
        }

        // Ensure terrain bottoms out at y=0 when requested.
        if (biomeProfile.lowestPointAtWorldYZero)
        {
            for (var y = 0; y < resolution; y++)
            for (var x = 0; x < resolution; x++)
                heights[y, x] = Mathf.Max(0f, heights[y, x] - minHeight);
            maxHeight -= minHeight;
        }

        if (maxHeight > 1f)
        {
            var inv = 1f / maxHeight;
            for (var y = 0; y < resolution; y++)
            for (var x = 0; x < resolution; x++)
                heights[y, x] = Mathf.Clamp01(heights[y, x] * inv);
        }
        else
        {
            for (var y = 0; y < resolution; y++)
            for (var x = 0; x < resolution; x++)
                heights[y, x] = Mathf.Clamp01(heights[y, x]);
        }

        return heights;
    }

    private static float ComputeDunes(
        float worldX,
        float worldY,
        Vector2 duneDir,
        float activeDuneFrequency,
        float duneVariation,
        float waveOffsetX,
        float waveOffsetY)
    {
        var projected = worldX * duneDir.x + worldY * duneDir.y;
        var warpNoiseScale = Mathf.Lerp(0.0006f, 0.0022f, duneVariation);
        var warpAmount = Mathf.Lerp(40f, 120f, duneVariation);
        var warpNoise = Mathf.PerlinNoise(
            waveOffsetX + worldX * warpNoiseScale,
            waveOffsetY + worldY * warpNoiseScale) - 0.5f;
        var warp = warpNoise * warpAmount;
        var waveA = Mathf.Sin((projected + warp) * activeDuneFrequency * Mathf.PI * 2f);
        var waveB = Mathf.Sin((projected * 1.8f - warp * 0.35f) * activeDuneFrequency * Mathf.PI * 2f);
        var ridgeA = Mathf.Pow(Mathf.Abs(waveA), 1.7f);
        var ridgeB = Mathf.Pow(Mathf.Abs(waveB), 2.2f) * 0.22f;
        var longWave = (Mathf.PerlinNoise(
            waveOffsetX * 0.37f + worldX * activeDuneFrequency * 0.18f,
            waveOffsetY * 0.37f + worldY * activeDuneFrequency * 0.18f) - 0.5f) * Mathf.Lerp(0.03f, 0.15f, duneVariation);
        var dunes = ridgeA * 0.76f + ridgeB + longWave;
        dunes = 0.5f + (dunes - 0.5f) * Mathf.Lerp(0.35f, 0.8f, duneVariation);
        return Mathf.Clamp01(dunes);
    }

    private static float ComputeEdgeMask(float nx, float ny)
    {
        var ex = Mathf.Min(nx, 1f - nx);
        var ey = Mathf.Min(ny, 1f - ny);
        return 1f - Mathf.SmoothStep(0f, 0.12f, Mathf.Min(ex, ey));
    }

    private TerrainLayer[] ResolveTerrainLayers()
    {
        var activeTileSize = biomeProfile.tileSize;
        var baseLayer = biomeProfile.baseLayer;
        var midLayer = biomeProfile.midLayer;
        var steepLayer = biomeProfile.steepLayer;
        var activeBaseFallbackColor = biomeProfile.fallbackBaseColor;
        var activeMidFallbackColor = biomeProfile.fallbackMidColor;
        var activeSteepFallbackColor = biomeProfile.fallbackSteepColor;

        if (baseLayer == null)
        {
            baseLayer = new TerrainLayer
            {
                diffuseTexture = BuildSolidTexture(activeBaseFallbackColor),
                tileSize = activeTileSize
            };
        }
        if (midLayer == null)
        {
            midLayer = new TerrainLayer
            {
                diffuseTexture = BuildSolidTexture(activeMidFallbackColor),
                tileSize = activeTileSize * 0.9f
            };
        }
        if (steepLayer == null)
        {
            steepLayer = new TerrainLayer
            {
                diffuseTexture = BuildSolidTexture(activeSteepFallbackColor),
                tileSize = activeTileSize * 0.8f
            };
        }
        baseLayer.tileSize = activeTileSize;
        midLayer.tileSize = activeTileSize * 0.9f;
        steepLayer.tileSize = activeTileSize * 0.8f;
        ApplyLayerColorRemap(baseLayer, activeBaseFallbackColor);
        ApplyLayerColorRemap(midLayer, activeMidFallbackColor);
        ApplyLayerColorRemap(steepLayer, activeSteepFallbackColor);
        return new[] { baseLayer, midLayer, steepLayer };
    }

    private static void ApplyLayerColorRemap(TerrainLayer layer, Color tint)
    {
        if (layer == null) return;
        layer.diffuseRemapMin = new Vector4(0f, 0f, 0f, 0f);
        layer.diffuseRemapMax = new Vector4(1f, 1f, 1f, 1f);
    }

    private static bool IsTerrainUninitialized(Terrain terrain)
    {
        if (terrain == null) return true;
        var data = terrain.terrainData;
        if (data == null) return true;
        if (data.heightmapResolution <= 33) return true;
        if (data.size.x < 64f || data.size.z < 64f) return true;

        // Very flat/empty terrain check: sample a few points and see if relief is near-zero.
        var h1 = data.GetInterpolatedHeight(0.2f, 0.2f);
        var h2 = data.GetInterpolatedHeight(0.8f, 0.2f);
        var h3 = data.GetInterpolatedHeight(0.2f, 0.8f);
        var h4 = data.GetInterpolatedHeight(0.8f, 0.8f);
        var min = Mathf.Min(Mathf.Min(h1, h2), Mathf.Min(h3, h4));
        var max = Mathf.Max(Mathf.Max(h1, h2), Mathf.Max(h3, h4));
        return (max - min) < 0.2f;
    }

    private static bool IsTerrainMissingCriticalData(Terrain terrain)
    {
        if (terrain == null) return true;
        if (terrain.terrainData == null) return true;
        if (IsTerrainUninitialized(terrain)) return true;

        var collider = terrain.GetComponent<TerrainCollider>();
        if (collider == null) return true;
        if (collider.terrainData != terrain.terrainData) return true;

        if (terrain.terrainData.terrainLayers == null || terrain.terrainData.terrainLayers.Length == 0) return true;
        if (terrain.materialTemplate == null) return true;

        return false;
    }

    private void ApplyTerrainTransform(Terrain terrain)
    {
        var y = biomeProfile.lowestPointAtWorldYZero ? 0f : terrain.transform.position.y;
        terrain.transform.position = biomeProfile.centerAtWorldOrigin
            ? new Vector3(-biomeProfile.terrainWidth * 0.5f, y, -biomeProfile.terrainLength * 0.5f)
            : new Vector3(0f, y, 0f);
    }

    private static void ApplyTerrainDrawDistances(Terrain terrain)
    {
        terrain.basemapDistance = 8000f;
        terrain.detailObjectDistance = 2500f;
        terrain.treeDistance = 5000f;
        terrain.drawInstanced = true;
    }

    private void PaintLayerBlending(TerrainData data)
    {
        if (data == null || data.terrainLayers == null || data.terrainLayers.Length == 0) return;
        var activeSlopeBlendMin = Mathf.Min(biomeProfile.slopeBlend.min, biomeProfile.slopeBlend.max);
        var activeSlopeBlendMax = Mathf.Max(biomeProfile.slopeBlend.min, biomeProfile.slopeBlend.max);
        var activeHeightBlendMin = Mathf.Min(biomeProfile.heightBlend.min, biomeProfile.heightBlend.max);
        var activeHeightBlendMax = Mathf.Max(biomeProfile.heightBlend.min, biomeProfile.heightBlend.max);
        var activeBlendNoiseScale = biomeProfile.blendNoiseScale;
        var activeBlendNoiseStrength = biomeProfile.blendNoiseStrength;

        var alphaRes = Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.Max(128, biomeProfile.alphamapResolution)), 128, 2048);
        if (data.alphamapResolution != alphaRes) data.alphamapResolution = alphaRes;

        var layerCount = data.terrainLayers.Length;
        var map = new float[alphaRes, alphaRes, layerCount];
        var seedOffset = activeGenerationSeed * 0.000137f;
        var terrainWidth = Mathf.Max(1f, data.size.x);
        var terrainLength = Mathf.Max(1f, data.size.z);
        var noiseScale = Mathf.Max(0.00001f, activeBlendNoiseScale);

        for (var y = 0; y < alphaRes; y++)
        {
            for (var x = 0; x < alphaRes; x++)
            {
                if (layerCount == 1)
                {
                    map[y, x, 0] = 1f;
                    continue;
                }

                var u = (float)x / (alphaRes - 1);
                var v = (float)y / (alphaRes - 1);
                var height01 = data.GetInterpolatedHeight(u, v) / Mathf.Max(0.0001f, data.size.y);
                var normal = data.GetInterpolatedNormal(u, v);
                var slope01 = 1f - Mathf.Clamp01(normal.y);
                var worldX = u * terrainWidth;
                var worldZ = v * terrainLength;
                var noise = (Mathf.PerlinNoise(seedOffset + worldX * noiseScale, seedOffset + worldZ * noiseScale) - 0.5f) * activeBlendNoiseStrength * 0.2f;
                var roughA = Mathf.PerlinNoise(seedOffset * 1.7f + worldX * noiseScale * 1.4f, seedOffset * 1.7f + worldZ * noiseScale * 1.4f) - 0.5f;
                var roughB = Mathf.PerlinNoise(seedOffset * 2.9f + worldX * noiseScale * 3.0f, seedOffset * 2.9f + worldZ * noiseScale * 3.0f) - 0.5f;
                var roughOverlay = Mathf.Clamp01(0.5f + roughA * 0.65f + roughB * 0.35f);

                var slopeMask = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(activeSlopeBlendMin, activeSlopeBlendMax, slope01 + noise));
                var heightMask = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(activeHeightBlendMin, activeHeightBlendMax, height01 + noise * 0.6f));

                // Mid acts as rough layer over most of the surface.
                var wSteep = Mathf.Clamp01(slopeMask * 0.72f);
                var wMid = Mathf.Clamp01(0.28f + roughOverlay * 0.52f + heightMask * 0.16f);
                wMid = Mathf.Clamp01(wMid * (1f - wSteep * 0.65f));
                var wBase = Mathf.Clamp01(1f - wMid - wSteep);

                // Bias toward visible mid coverage while preserving base and steep.
                wBase += 0.10f;
                wMid += 0.24f;
                wSteep += 0.03f;

                var sum = Mathf.Max(0.0001f, wBase + wMid + wSteep);
                map[y, x, 0] = wBase / sum;
                map[y, x, 1] = wMid / sum;
                map[y, x, 2] = wSteep / sum;

                for (var layer = 3; layer < layerCount; layer++) map[y, x, layer] = 0f;
            }
        }

        data.SetAlphamaps(0, 0, map);
    }

    private static void SmoothAlphamap(float[,,] map, int layerCount, int iterations)
    {
        if (map == null || iterations <= 0 || layerCount <= 1) return;
        var height = map.GetLength(0);
        var width = map.GetLength(1);
        if (height < 3 || width < 3) return;

        var temp = new float[height, width, layerCount];
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            for (var y = 1; y < height - 1; y++)
            {
                for (var x = 1; x < width - 1; x++)
                {
                    var sum = 0f;
                    for (var layer = 0; layer < layerCount; layer++)
                    {
                        var center = map[y, x, layer] * 0.4f;
                        var neighbors =
                            map[y - 1, x, layer] * 0.15f +
                            map[y + 1, x, layer] * 0.15f +
                            map[y, x - 1, layer] * 0.15f +
                            map[y, x + 1, layer] * 0.15f;
                        var value = Mathf.Clamp01(center + neighbors);
                        temp[y, x, layer] = value;
                        sum += value;
                    }

                    var inv = 1f / Mathf.Max(0.0001f, sum);
                    for (var layer = 0; layer < layerCount; layer++)
                        temp[y, x, layer] *= inv;
                }
            }

            for (var y = 1; y < height - 1; y++)
            for (var x = 1; x < width - 1; x++)
            for (var layer = 0; layer < layerCount; layer++)
                map[y, x, layer] = temp[y, x, layer];
        }
    }

    private static void ApplyTerrainMaterialTemplate(Terrain terrain)
    {
        if (terrain == null) return;
        var shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
        if (shader == null) shader = Shader.Find("Nature/Terrain/Standard");
        if (shader == null) return;

        if (terrain.materialTemplate == null || terrain.materialTemplate.shader != shader)
        {
            var material = new Material(shader) { name = "TerrainRuntimeMaterial" };
            terrain.materialTemplate = material;
        }
    }

    private static void SyncTerrainColliderData(Terrain terrain)
    {
        if (terrain == null || terrain.terrainData == null) return;
        var collider = terrain.GetComponent<TerrainCollider>();
        if (collider == null) collider = terrain.gameObject.AddComponent<TerrainCollider>();
        collider.terrainData = terrain.terrainData;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureSeedInitialized();
        QueueEditorSeedRefreshIfNeeded();
    }

    private void QueueEditorSeedRefreshIfNeeded()
    {
        if (Application.isPlaying) return;
        if (generationMode != GenerationMode.OnSeedChanged) return;
        if (!HasSeedChangedSinceLastGeneration()) return;
        if (pendingEditorSeedRefresh) return;

        pendingEditorSeedRefresh = true;
        EditorApplication.delayCall += RefreshFromSeedIfNeededInEditor;
    }

    private void RefreshFromSeedIfNeededInEditor()
    {
        pendingEditorSeedRefresh = false;
        if (this == null) return;
        if (generationMode != GenerationMode.OnSeedChanged || !HasSeedChangedSinceLastGeneration()) return;
        GenerateTerrain();
    }
#endif

    private void GenerateEnvironment(Terrain terrain)
    {
        if (biomeProfile == null) return;
        var root = FindOrCreateGeneratedRoot();
        ClearChildren(root);

        var activeGenerateRoad = biomeProfile.generateRoad;
        var activeGenerateTrees = biomeProfile.generateTrees;
        var activeGenerateObjects = biomeProfile.generateObjects;
        var activeRoadWidth = biomeProfile.roadWidth;
        var activeTreePrefabs = biomeProfile.treePrefabs;
        var activeTreeCount = biomeProfile.treeCount;
        var activeObjectPrefabs = biomeProfile.objectPrefabs;
        var activeObjectCount = biomeProfile.objectCount;

        if (activeGenerateRoad) SpawnRoad(terrain, root, activeRoadWidth);
        if (activeGenerateTrees) ScatterPrefabs(terrain, root, activeTreePrefabs, activeTreeCount, "Trees");
        if (activeGenerateObjects) ScatterPrefabs(terrain, root, activeObjectPrefabs, activeObjectCount, "Objects");
    }

    private Transform FindOrCreateGeneratedRoot()
    {
        var existing = transform.Find("GeneratedEnvironment");
        if (existing != null) return existing;
        var root = new GameObject("GeneratedEnvironment").transform;
        root.SetParent(transform, false);
        return root;
    }

    private static void ClearChildren(Transform parent)
    {
        for (var i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
    }

    private void SpawnRoad(Terrain terrain, Transform root, float activeRoadWidth)
    {
        var road = GameObject.CreatePrimitive(PrimitiveType.Cube);
        road.name = "Road";
        road.transform.SetParent(root, false);
        var center = terrain.transform.position + new Vector3(biomeProfile.terrainWidth * 0.5f, 0f, biomeProfile.terrainLength * 0.5f);
        center.y = terrain.SampleHeight(center) + terrain.transform.position.y + 0.05f;
        road.transform.position = center;
        road.transform.localScale = new Vector3(biomeProfile.terrainWidth * 0.7f, 0.1f, activeRoadWidth);
    }

    private void ScatterPrefabs(Terrain terrain, Transform root, GameObject[] prefabs, int count, string containerName)
    {
        if (prefabs == null || prefabs.Length == 0 || count <= 0) return;
        var container = new GameObject(containerName).transform;
        container.SetParent(root, false);
        var rng = new System.Random(activeGenerationSeed + containerName.GetHashCode());

        for (var i = 0; i < count; i++)
        {
            var px = (float)rng.NextDouble() * biomeProfile.terrainWidth;
            var pz = (float)rng.NextDouble() * biomeProfile.terrainLength;
            var world = terrain.transform.position + new Vector3(px, 0f, pz);
            world.y = terrain.SampleHeight(world) + terrain.transform.position.y;
            var prefab = prefabs[rng.Next(0, prefabs.Length)];
            if (prefab == null) continue;
            var go = Instantiate(prefab, world, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f), container);
            var s = Mathf.Lerp(0.85f, 1.2f, (float)rng.NextDouble());
            go.transform.localScale *= s;
        }
    }

    private static Texture2D BuildSolidTexture(Color color)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
        texture.SetPixel(0, 0, color);
        texture.SetPixel(1, 0, color);
        texture.SetPixel(0, 1, color);
        texture.SetPixel(1, 1, color);
        texture.Apply();
        return texture;
    }

    private void RaiseStarted()
    {
        GenerationStarted?.Invoke();
        onGenerationStarted?.Invoke();
    }

    private void RaiseCompleted()
    {
        GenerationCompleted?.Invoke();
        onGenerationCompleted?.Invoke();
    }

    private void RaiseFailed(string reason)
    {
        GenerationFailed?.Invoke(reason);
        onGenerationFailed?.Invoke(reason);
    }

    private bool ShouldGenerateEnvironment()
    {
        if (biomeProfile == null) return false;
        var activeTrees = biomeProfile.generateTrees;
        var activeObjects = biomeProfile.generateObjects;
        var activeRoad = biomeProfile.generateRoad;
        return activeTrees || activeObjects || activeRoad;
    }

    public void GenerateNewSeed()
    {
        seed = CreateRandomSeed();
#if UNITY_EDITOR
        QueueEditorSeedRefreshIfNeeded();
        EditorUtility.SetDirty(this);
#endif
    }

    private static int CreateRandomSeed()
    {
        return Guid.NewGuid().GetHashCode() ^ Environment.TickCount;
    }

    private void EnsureSeedInitialized()
    {
        if (seed == 0)
            seed = CreateRandomSeed();
    }

    private bool HasSeedChangedSinceLastGeneration()
    {
        if (lastGeneratedSeed == int.MinValue) return true;
        return lastGeneratedSeed != seed;
    }
}

[AttributeUsage(AttributeTargets.Field)]
public sealed class InspectorButtonAttribute : PropertyAttribute
{
    public readonly string MethodName;
    public readonly string Label;

    public InspectorButtonAttribute(string methodName, string label = null)
    {
        MethodName = methodName;
        Label = label;
    }
}

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(InspectorButtonAttribute))]
public sealed class InspectorButtonDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var data = (InspectorButtonAttribute)attribute;
        var buttonLabel = string.IsNullOrWhiteSpace(data.Label) ? ObjectNames.NicifyVariableName(property.name) : data.Label;

        if (!GUI.Button(position, buttonLabel)) return;
        var target = property.serializedObject.targetObject;
        if (target == null) return;

        var method = target.GetType().GetMethod(data.MethodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        if (method == null)
        {
            Debug.LogError($"InspectorButton: Method '{data.MethodName}' not found on {target.GetType().Name}.", target);
            return;
        }

        method.Invoke(target, null);
        EditorUtility.SetDirty(target);
    }
}
#endif
