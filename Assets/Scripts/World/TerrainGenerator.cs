using System;
using System.Collections;
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
    [SerializeField] private bool useAsyncGenerationInPlayMode = true;
    [SerializeField, Min(8)] private int asyncHeightRowsPerFrame = 192;
    [SerializeField, Min(4)] private int asyncEnvironmentSpawnsPerFrame = 96;
    [SerializeField, Min(128)] private int runtimeAlphamapResolutionCap = 384;
    [SerializeField, Range(0, 3)] private int runtimeMaxBlendSmoothIterations = 0;
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
    public event System.Action GenerationStarted;
    public event System.Action GenerationCompleted;
    public event System.Action<string> GenerationFailed;
    public bool IsGenerating { get; private set; }
    private Coroutine generationRoutine;

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
        if (Application.isPlaying && useAsyncGenerationInPlayMode)
        {
            if (generationRoutine != null)
            {
                StopCoroutine(generationRoutine);
                generationRoutine = null;
                IsGenerating = false;
            }
            generationRoutine = StartCoroutine(GenerateTerrainAsyncRoutine());
            return;
        }

        GenerateTerrainImmediate();
    }

    private void GenerateTerrainImmediate()
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

    private IEnumerator GenerateTerrainAsyncRoutine()
    {
        if (biomeProfile == null)
        {
            RaiseFailed("No TerrainBiomeProfile assigned.");
            generationRoutine = null;
            yield break;
        }

        RaiseStarted();
        activeGenerationSeed = seed;
        lastGeneratedSeed = activeGenerationSeed;
        var terrain = ResolveTerrainForGeneration(createIfMissing: true);
        if (terrain == null)
        {
            RaiseFailed("No terrain available to generate.");
            generationRoutine = null;
            yield break;
        }

        var data = EnsureTerrainData(terrain);
        var resolution = Mathf.ClosestPowerOfTwo(Mathf.Clamp(biomeProfile.heightmapResolution - 1, 32, 4096)) + 1;
        data.heightmapResolution = resolution;
        data.size = new Vector3(biomeProfile.terrainWidth, biomeProfile.terrainHeight, biomeProfile.terrainLength);

        var heights = new float[resolution, resolution];
        yield return StartCoroutine(BuildHeightMapAsync(resolution, heights));
        data.SetHeights(0, 0, heights);

        var layers = ResolveTerrainLayers();
        data.terrainLayers = layers;
        PaintLayerBlending(data);

        SyncTerrainColliderData(terrain);
        ApplyTerrainTransform(terrain);
        ApplyTerrainDrawDistances(terrain);
        ApplyTerrainMaterialTemplate(terrain);
        terrain.Flush();

        if (ShouldGenerateEnvironment())
            yield return StartCoroutine(GenerateEnvironmentAsync(terrain));

        RaiseCompleted();
        generationRoutine = null;
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

        ApplyCentralBaseFlattening(heights, resolution);

        return heights;
    }

    private IEnumerator BuildHeightMapAsync(int resolution, float[,] heights)
    {
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
        var rowsPerFrame = Mathf.Max(8, asyncHeightRowsPerFrame);
        var rowCounter = 0;

        for (var y = 0; y < resolution; y++)
        {
            for (var x = 0; x < resolution; x++)
            {
                var nx = (float)x / (resolution - 1);
                var ny = (float)y / (resolution - 1);
                var worldX = nx * biomeProfile.terrainWidth;
                var worldY = ny * biomeProfile.terrainLength;

                var dunes = ComputeDunes(worldX, worldY, duneDir, biomeProfile.duneFrequency, duneVariation, sx, sy);
                dunes *= duneAmplitude;
                dunes *= 1f - ComputeEdgeMask(nx, ny) * edgeFlattening;

                var edgeDistance = Mathf.Min(Mathf.Min(nx, 1f - nx), Mathf.Min(ny, 1f - ny));
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

            rowCounter++;
            if (rowCounter >= rowsPerFrame)
            {
                rowCounter = 0;
                yield return null;
            }
        }

        if (biomeProfile.lowestPointAtWorldYZero)
        {
            var normalizeRowCounter = 0;
            for (var y = 0; y < resolution; y++)
            {
                for (var x = 0; x < resolution; x++)
                    heights[y, x] = Mathf.Max(0f, heights[y, x] - minHeight);
                normalizeRowCounter++;
                if (normalizeRowCounter >= rowsPerFrame)
                {
                    normalizeRowCounter = 0;
                    yield return null;
                }
            }
            maxHeight -= minHeight;
        }

        if (maxHeight > 1f)
        {
            var inv = 1f / maxHeight;
            var scaleRowCounter = 0;
            for (var y = 0; y < resolution; y++)
            {
                for (var x = 0; x < resolution; x++)
                    heights[y, x] = Mathf.Clamp01(heights[y, x] * inv);
                scaleRowCounter++;
                if (scaleRowCounter >= rowsPerFrame)
                {
                    scaleRowCounter = 0;
                    yield return null;
                }
            }
        }
        else
        {
            var clampRowCounter = 0;
            for (var y = 0; y < resolution; y++)
            {
                for (var x = 0; x < resolution; x++)
                    heights[y, x] = Mathf.Clamp01(heights[y, x]);
                clampRowCounter++;
                if (clampRowCounter >= rowsPerFrame)
                {
                    clampRowCounter = 0;
                    yield return null;
                }
            }
        }

        ApplyCentralBaseFlattening(heights, resolution);
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

    private void ApplyCentralBaseFlattening(float[,] heights, int resolution)
    {
        if (!biomeProfile.reserveCenterForBase) return;

        var width = Mathf.Max(1f, biomeProfile.terrainWidth);
        var length = Mathf.Max(1f, biomeProfile.terrainLength);
        var center = new Vector2(
            Mathf.Clamp01(biomeProfile.baseCenterNormalized.x) * width,
            Mathf.Clamp01(biomeProfile.baseCenterNormalized.y) * length);

        var maxRadius = Mathf.Min(width, length) * 0.45f;
        var radius = Mathf.Clamp(biomeProfile.baseRadius, 1f, maxRadius);
        var blend = Mathf.Clamp(biomeProfile.baseBlendDistance, 0f, maxRadius);
        var flattenStrength = Mathf.Clamp01(biomeProfile.baseFlattenStrength);
        var innerFlatRatio = Mathf.Clamp(biomeProfile.baseInnerFlatRatio, 0.1f, 1f);
        if (flattenStrength <= 0f) return;

        var centerX = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(biomeProfile.baseCenterNormalized.x) * (resolution - 1)), 0, resolution - 1);
        var centerY = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(biomeProfile.baseCenterNormalized.y) * (resolution - 1)), 0, resolution - 1);
        var targetHeight = ComputeAverageHeightAroundCenter(heights, centerX, centerY, Mathf.RoundToInt(radius / Mathf.Max(width, 0.001f) * resolution * 0.24f));
        targetHeight = Mathf.Clamp01(targetHeight + biomeProfile.baseHeightOffset01);
        var outerRadius = radius + blend;
        var innerRadius = radius * innerFlatRatio;
        var hardFlatRadius = radius * Mathf.Clamp(Mathf.Lerp(innerFlatRatio, 1f, 0.7f), innerFlatRatio, 1f);

        for (var y = 0; y < resolution; y++)
        {
            var ny = (float)y / (resolution - 1);
            var worldY = ny * length;
            for (var x = 0; x < resolution; x++)
            {
                var nx = (float)x / (resolution - 1);
                var worldX = nx * width;
                var distance = Vector2.Distance(new Vector2(worldX, worldY), center);
                if (distance > outerRadius) continue;

                float weight;
                if (distance <= hardFlatRadius)
                {
                    // Keep the military base footprint very flat.
                    weight = 1f;
                }
                else if (distance <= radius)
                {
                    var tInner = Mathf.InverseLerp(radius, hardFlatRadius, distance);
                    weight = Mathf.SmoothStep(0.72f, 0.98f, tInner);
                }
                else
                {
                    var t = Mathf.InverseLerp(outerRadius, radius, distance);
                    weight = Mathf.SmoothStep(0f, 0.45f, t);
                }

                heights[y, x] = Mathf.Lerp(heights[y, x], targetHeight, weight * flattenStrength);
            }
        }
    }

    private static float ComputeAverageHeightAroundCenter(float[,] heights, int centerX, int centerY, int radius)
    {
        var width = heights.GetLength(1);
        var height = heights.GetLength(0);
        var clampedRadius = Mathf.Clamp(radius, 1, 64);
        var sum = 0f;
        var count = 0;
        for (var y = centerY - clampedRadius; y <= centerY + clampedRadius; y++)
        {
            if (y < 0 || y >= height) continue;
            for (var x = centerX - clampedRadius; x <= centerX + clampedRadius; x++)
            {
                if (x < 0 || x >= width) continue;
                var dx = x - centerX;
                var dy = y - centerY;
                if (dx * dx + dy * dy > clampedRadius * clampedRadius) continue;
                sum += heights[y, x];
                count++;
            }
        }

        if (count <= 0) return heights[Mathf.Clamp(centerY, 0, height - 1), Mathf.Clamp(centerX, 0, width - 1)];
        return sum / count;
    }

    private TerrainLayer[] ResolveTerrainLayers()
    {
        var activeTileSize = biomeProfile.tileSize;
        var baseLayer = biomeProfile.baseLayer;
        var midLayer = biomeProfile.midLayer;
        var steepLayer = biomeProfile.steepLayer;
        var accentLayer = biomeProfile.accentLayer;
        var detailLayer = biomeProfile.detailLayer;
        var activeBaseFallbackColor = biomeProfile.fallbackBaseColor;
        var activeMidFallbackColor = biomeProfile.fallbackMidColor;
        var activeSteepFallbackColor = biomeProfile.fallbackSteepColor;
        var activeAccentFallbackColor = biomeProfile.fallbackAccentColor;
        var activeDetailFallbackColor = biomeProfile.fallbackDetailColor;

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
        if (accentLayer == null)
        {
            accentLayer = new TerrainLayer
            {
                diffuseTexture = BuildSolidTexture(activeAccentFallbackColor),
                tileSize = activeTileSize * 0.72f
            };
        }
        if (detailLayer == null)
        {
            detailLayer = new TerrainLayer
            {
                diffuseTexture = BuildSolidTexture(activeDetailFallbackColor),
                tileSize = activeTileSize * 0.6f
            };
        }
        baseLayer.tileSize = activeTileSize;
        midLayer.tileSize = activeTileSize * 0.9f;
        steepLayer.tileSize = activeTileSize * 0.8f;
        accentLayer.tileSize = activeTileSize * 0.72f;
        detailLayer.tileSize = activeTileSize * 0.6f;
        ApplyLayerColorRemap(baseLayer, activeBaseFallbackColor);
        ApplyLayerColorRemap(midLayer, activeMidFallbackColor);
        ApplyLayerColorRemap(steepLayer, activeSteepFallbackColor);
        ApplyLayerColorRemap(accentLayer, activeAccentFallbackColor);
        ApplyLayerColorRemap(detailLayer, activeDetailFallbackColor);
        return new[] { baseLayer, midLayer, steepLayer, accentLayer, detailLayer };
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
        var macroNoiseScale = Mathf.Max(0.00001f, biomeProfile.blendMacroNoiseScale);
        var macroNoiseStrength = Mathf.Clamp01(biomeProfile.blendMacroNoiseStrength);
        var warpDistance = Mathf.Max(0f, biomeProfile.blendWarpDistance);
        var warpScale = Mathf.Max(0.00001f, biomeProfile.blendWarpScale);
        var bandStrength = Mathf.Clamp01(biomeProfile.blendBandStrength);
        var smoothIterations = Mathf.Clamp(biomeProfile.blendSmoothIterations, 0, 3);
        if (Application.isPlaying)
            smoothIterations = Mathf.Min(smoothIterations, Mathf.Clamp(runtimeMaxBlendSmoothIterations, 0, 3));

        var alphaRes = Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.Max(128, biomeProfile.alphamapResolution)), 128, 2048);
        if (Application.isPlaying)
            alphaRes = Mathf.Min(alphaRes, Mathf.Clamp(runtimeAlphamapResolutionCap, 128, 2048));
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
                var warpX = (Mathf.PerlinNoise(seedOffset * 2.1f + worldX * warpScale, seedOffset * 2.1f + worldZ * warpScale) - 0.5f) * warpDistance;
                var warpZ = (Mathf.PerlinNoise(seedOffset * 3.3f + worldX * warpScale, seedOffset * 3.3f + worldZ * warpScale) - 0.5f) * warpDistance;
                var mixedX = worldX + warpX;
                var mixedZ = worldZ + warpZ;

                var noise = (Mathf.PerlinNoise(seedOffset + mixedX * noiseScale, seedOffset + mixedZ * noiseScale) - 0.5f) * activeBlendNoiseStrength * 0.2f;
                var roughA = Mathf.PerlinNoise(seedOffset * 1.7f + mixedX * noiseScale * 1.4f, seedOffset * 1.7f + mixedZ * noiseScale * 1.4f) - 0.5f;
                var roughB = Mathf.PerlinNoise(seedOffset * 2.9f + mixedX * noiseScale * 3.0f, seedOffset * 2.9f + mixedZ * noiseScale * 3.0f) - 0.5f;
                var roughOverlay = Mathf.Clamp01(0.5f + roughA * 0.65f + roughB * 0.35f);
                var macroPatch = Mathf.PerlinNoise(seedOffset * 0.9f + mixedX * macroNoiseScale, seedOffset * 0.9f + mixedZ * macroNoiseScale);
                var macroPatchB = Mathf.PerlinNoise(seedOffset * 1.4f + mixedX * macroNoiseScale * 0.45f, seedOffset * 1.4f + mixedZ * macroNoiseScale * 0.45f);
                var macroBlend = Mathf.Clamp01(Mathf.Lerp(macroPatch, macroPatchB, 0.35f));
                var bands = Mathf.Sin((mixedX + mixedZ) * 0.013f + seedOffset * 4200f) * 0.5f + 0.5f;
                var patternBlend = Mathf.Clamp01(Mathf.Lerp(macroBlend, bands, bandStrength * 0.65f));

                var slopeMask = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(activeSlopeBlendMin, activeSlopeBlendMax, slope01 + noise));
                var heightMask = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(activeHeightBlendMin, activeHeightBlendMax, height01 + noise * 0.6f));

                // 5-layer blend: base/mid/steep/accent/detail with explicit random breakup.
                var wSteep = Mathf.Clamp01(slopeMask * 0.68f + (1f - patternBlend) * macroNoiseStrength * 0.2f);
                var wMid = Mathf.Clamp01(0.16f + roughOverlay * 0.3f + patternBlend * 0.22f + heightMask * 0.1f);
                var wAccent = Mathf.Clamp01(0.1f + macroBlend * 0.48f + bands * 0.2f - wSteep * 0.24f);
                var wDetail = Mathf.Clamp01(0.06f + roughA * roughA * 0.55f + roughB * roughB * 0.32f);
                var wBase = Mathf.Clamp01(1f - (wSteep * 0.85f + wMid * 0.65f + wAccent * 0.45f + wDetail * 0.38f));

                wBase += 0.12f;
                wMid += 0.09f;
                wSteep += 0.03f;
                wAccent += 0.06f;
                wDetail += 0.05f;

                if (layerCount >= 5)
                {
                    var sum5 = Mathf.Max(0.0001f, wBase + wMid + wSteep + wAccent + wDetail);
                    map[y, x, 0] = wBase / sum5;
                    map[y, x, 1] = wMid / sum5;
                    map[y, x, 2] = wSteep / sum5;
                    map[y, x, 3] = wAccent / sum5;
                    map[y, x, 4] = wDetail / sum5;
                    for (var layer = 5; layer < layerCount; layer++) map[y, x, layer] = 0f;
                }
                else
                {
                    var sum3 = Mathf.Max(0.0001f, wBase + wMid + wSteep);
                    map[y, x, 0] = wBase / sum3;
                    map[y, x, 1] = wMid / sum3;
                    map[y, x, 2] = wSteep / sum3;
                    for (var layer = 3; layer < layerCount; layer++) map[y, x, layer] = 0f;
                }
            }
        }

        if (smoothIterations > 0 && layerCount > 1)
            SmoothAlphamap(map, layerCount, smoothIterations);

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
        var activeTreeCount = Mathf.RoundToInt(biomeProfile.treeCount * 1.15f);
        var activeObjectPrefabs = biomeProfile.objectPrefabs;
        var activeObjectCount = biomeProfile.objectCount;

        if (activeGenerateRoad) SpawnRoad(terrain, root, activeRoadWidth);
        if (activeGenerateTrees) ScatterPrefabs(terrain, root, activeTreePrefabs, activeTreeCount, "Trees");
        if (activeGenerateObjects) ScatterPrefabs(terrain, root, activeObjectPrefabs, activeObjectCount, "Objects");
    }

    private IEnumerator GenerateEnvironmentAsync(Terrain terrain)
    {
        if (biomeProfile == null) yield break;
        var root = FindOrCreateGeneratedRoot();
        ClearChildren(root);
        yield return null;

        var activeGenerateRoad = biomeProfile.generateRoad;
        var activeGenerateTrees = biomeProfile.generateTrees;
        var activeGenerateObjects = biomeProfile.generateObjects;
        var activeRoadWidth = biomeProfile.roadWidth;
        var activeTreePrefabs = biomeProfile.treePrefabs;
        var activeTreeCount = Mathf.RoundToInt(biomeProfile.treeCount * 1.15f);
        var activeObjectPrefabs = biomeProfile.objectPrefabs;
        var activeObjectCount = biomeProfile.objectCount;

        if (activeGenerateRoad)
        {
            SpawnRoad(terrain, root, activeRoadWidth);
            yield return null;
        }
        if (activeGenerateTrees)
            yield return StartCoroutine(ScatterPrefabsAsync(terrain, root, activeTreePrefabs, activeTreeCount, "Trees"));
        if (activeGenerateObjects)
            yield return StartCoroutine(ScatterPrefabsAsync(terrain, root, activeObjectPrefabs, activeObjectCount, "Objects"));
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
        var maxAttempts = Mathf.Max(1, count * 12);
        var spawned = 0;
        var attempts = 0;

        var isTreeContainer = string.Equals(containerName, "Trees", StringComparison.Ordinal);
        while (spawned < count && attempts < maxAttempts)
        {
            attempts++;
            var px = 0f;
            var pz = 0f;
            if (isTreeContainer && (float)rng.NextDouble() < Mathf.Clamp01(biomeProfile.treeCenterDensity))
            {
                var centerX = Mathf.Clamp01(biomeProfile.baseCenterNormalized.x) * biomeProfile.terrainWidth;
                var centerZ = Mathf.Clamp01(biomeProfile.baseCenterNormalized.y) * biomeProfile.terrainLength;
                var maxRadius = Mathf.Min(biomeProfile.terrainWidth, biomeProfile.terrainLength) * 0.48f;
                var angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                var radius01 = Mathf.Pow((float)rng.NextDouble(), Mathf.Max(1f, biomeProfile.treeCenterBiasExponent));
                var radius = maxRadius * radius01;
                px = centerX + Mathf.Cos(angle) * radius;
                pz = centerZ + Mathf.Sin(angle) * radius;
                px = Mathf.Clamp(px, 0.5f, biomeProfile.terrainWidth - 0.5f);
                pz = Mathf.Clamp(pz, 0.5f, biomeProfile.terrainLength - 0.5f);
            }
            else
            {
                px = (float)rng.NextDouble() * biomeProfile.terrainWidth;
                pz = (float)rng.NextDouble() * biomeProfile.terrainLength;
            }

            var world = terrain.transform.position + new Vector3(px, 0f, pz);
            if (IsInsideBaseExclusionZone(world, isTreeContainer)) continue;
            world.y = terrain.SampleHeight(world) + terrain.transform.position.y;
            var prefab = prefabs[rng.Next(0, prefabs.Length)];
            if (prefab == null) continue;
            var go = Instantiate(prefab, world, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f), container);
            var s = isTreeContainer
                ? Mathf.Lerp(1.0f, 1.5f, (float)rng.NextDouble())
                : Mathf.Lerp(0.85f, 1.2f, (float)rng.NextDouble());
            go.transform.localScale *= s;
            if (isTreeContainer)
                EnsureTreeHazardCollider(go);
            spawned++;
        }
    }

    private IEnumerator ScatterPrefabsAsync(Terrain terrain, Transform root, GameObject[] prefabs, int count, string containerName)
    {
        if (prefabs == null || prefabs.Length == 0 || count <= 0) yield break;
        var container = new GameObject(containerName).transform;
        container.SetParent(root, false);
        var rng = new System.Random(activeGenerationSeed + containerName.GetHashCode());
        var maxAttempts = Mathf.Max(1, count * 12);
        var spawned = 0;
        var attempts = 0;
        var spawnBatch = Mathf.Max(4, asyncEnvironmentSpawnsPerFrame);
        var spawnedSinceYield = 0;
        var attemptsSinceYield = 0;

        var isTreeContainer = string.Equals(containerName, "Trees", StringComparison.Ordinal);
        while (spawned < count && attempts < maxAttempts)
        {
            attempts++;
            attemptsSinceYield++;
            var px = 0f;
            var pz = 0f;
            if (isTreeContainer && (float)rng.NextDouble() < Mathf.Clamp01(biomeProfile.treeCenterDensity))
            {
                var centerX = Mathf.Clamp01(biomeProfile.baseCenterNormalized.x) * biomeProfile.terrainWidth;
                var centerZ = Mathf.Clamp01(biomeProfile.baseCenterNormalized.y) * biomeProfile.terrainLength;
                var maxRadius = Mathf.Min(biomeProfile.terrainWidth, biomeProfile.terrainLength) * 0.48f;
                var angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                var radius01 = Mathf.Pow((float)rng.NextDouble(), Mathf.Max(1f, biomeProfile.treeCenterBiasExponent));
                var radius = maxRadius * radius01;
                px = centerX + Mathf.Cos(angle) * radius;
                pz = centerZ + Mathf.Sin(angle) * radius;
                px = Mathf.Clamp(px, 0.5f, biomeProfile.terrainWidth - 0.5f);
                pz = Mathf.Clamp(pz, 0.5f, biomeProfile.terrainLength - 0.5f);
            }
            else
            {
                px = (float)rng.NextDouble() * biomeProfile.terrainWidth;
                pz = (float)rng.NextDouble() * biomeProfile.terrainLength;
            }

            var world = terrain.transform.position + new Vector3(px, 0f, pz);
            if (IsInsideBaseExclusionZone(world, isTreeContainer)) continue;
            world.y = terrain.SampleHeight(world) + terrain.transform.position.y;
            var prefab = prefabs[rng.Next(0, prefabs.Length)];
            if (prefab == null) continue;
            var go = Instantiate(prefab, world, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f), container);
            var s = isTreeContainer
                ? Mathf.Lerp(1.0f, 1.5f, (float)rng.NextDouble())
                : Mathf.Lerp(0.85f, 1.2f, (float)rng.NextDouble());
            go.transform.localScale *= s;
            if (isTreeContainer)
                EnsureTreeHazardCollider(go);
            spawned++;
            spawnedSinceYield++;
            if (spawnedSinceYield >= spawnBatch || attemptsSinceYield >= spawnBatch * 3)
            {
                spawnedSinceYield = 0;
                attemptsSinceYield = 0;
                yield return null;
            }
        }
    }

    private static void EnsureTreeHazardCollider(GameObject treeRoot)
    {
        if (treeRoot == null) return;

        var colliders = treeRoot.GetComponentsInChildren<Collider>(true);
        if (colliders != null && colliders.Length > 0)
        {
            for (var i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] == null) continue;
                colliders[i].enabled = true;
                colliders[i].isTrigger = false;
            }
        }
        else
        {
            var renderer = treeRoot.GetComponentInChildren<Renderer>();
            var capsule = treeRoot.GetComponent<CapsuleCollider>();
            if (capsule == null) capsule = treeRoot.AddComponent<CapsuleCollider>();
            capsule.isTrigger = false;
            capsule.direction = 1;

            if (renderer != null)
            {
                var b = renderer.bounds;
                var h = Mathf.Max(1f, b.size.y);
                capsule.height = h;
                capsule.radius = Mathf.Max(0.35f, Mathf.Min(b.extents.x, b.extents.z));
                var localCenter = treeRoot.transform.InverseTransformPoint(b.center);
                capsule.center = localCenter;
            }
            else
            {
                capsule.height = 3.5f;
                capsule.radius = 0.8f;
                capsule.center = new Vector3(0f, 1.7f, 0f);
            }
        }

        try
        {
            treeRoot.tag = "Tree";
        }
        catch
        {
            // Ignore if the tag does not exist in this project.
        }
    }

    private bool IsInsideBaseExclusionZone(Vector3 worldPosition, bool isTreeSpawn)
    {
        if (biomeProfile == null) return false;
        if (!biomeProfile.reserveCenterForBase || !biomeProfile.excludeEnvironmentFromBase) return false;

        var baseCenter = targetTerrain != null
            ? targetTerrain.transform.position + new Vector3(
                Mathf.Clamp01(biomeProfile.baseCenterNormalized.x) * biomeProfile.terrainWidth,
                0f,
                Mathf.Clamp01(biomeProfile.baseCenterNormalized.y) * biomeProfile.terrainLength)
            : new Vector3(
                Mathf.Clamp01(biomeProfile.baseCenterNormalized.x) * biomeProfile.terrainWidth,
                0f,
                Mathf.Clamp01(biomeProfile.baseCenterNormalized.y) * biomeProfile.terrainLength);

        var paddingMultiplier = isTreeSpawn ? Mathf.Clamp01(biomeProfile.treeBaseExclusionPaddingMultiplier) : 1f;
        var adjustedPadding = biomeProfile.baseNoSpawnPadding * paddingMultiplier;
        var treeRadiusFactor = isTreeSpawn ? 0.55f : 1f;
        var exclusionRadius = Mathf.Max(1f, biomeProfile.baseRadius * treeRadiusFactor + adjustedPadding);
        var delta = worldPosition - baseCenter;
        delta.y = 0f;
        return delta.sqrMagnitude <= exclusionRadius * exclusionRadius;
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
        IsGenerating = true;
        InvokeSafe(GenerationStarted, "GenerationStarted");
        onGenerationStarted?.Invoke();
    }

    private void RaiseCompleted()
    {
        IsGenerating = false;
        InvokeSafe(GenerationCompleted, "GenerationCompleted");
        onGenerationCompleted?.Invoke();
    }

    private void RaiseFailed(string reason)
    {
        IsGenerating = false;
        InvokeSafe(GenerationFailed, reason, "GenerationFailed");
        onGenerationFailed?.Invoke(reason);
    }

    private void InvokeSafe(System.Action action, string eventName)
    {
        if (action == null) return;
        var delegates = action.GetInvocationList();
        for (var i = 0; i < delegates.Length; i++)
        {
            try
            {
                ((System.Action)delegates[i])();
            }
            catch (Exception ex)
            {
                Debug.LogException(new Exception($"TerrainGenerator {eventName} listener failed.", ex), this);
            }
        }
    }

    private void InvokeSafe(System.Action<string> action, string arg, string eventName)
    {
        if (action == null) return;
        var delegates = action.GetInvocationList();
        for (var i = 0; i < delegates.Length; i++)
        {
            try
            {
                ((System.Action<string>)delegates[i])(arg);
            }
            catch (Exception ex)
            {
                Debug.LogException(new Exception($"TerrainGenerator {eventName} listener failed.", ex), this);
            }
        }
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
