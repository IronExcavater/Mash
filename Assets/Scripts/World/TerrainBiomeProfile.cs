using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "TerrainBiomeProfile", menuName = "Mash/Terrain Biome Profile")]
public class TerrainBiomeProfile : ScriptableObject
{
    [Header("Terrain Size")]
    [Min(64f)] public float terrainWidth = 3000f;
    [Min(64f)] public float terrainLength = 3000f;
    [Min(16f)] public float terrainHeight = 200f;
    [Range(129, 4097)] public int heightmapResolution = 513;
    public bool centerAtWorldOrigin = true;
    public bool lowestPointAtWorldYZero = true;

    [Header("Terrain Shape")]
    [FormerlySerializedAs("hilliness")]
    [FormerlySerializedAs("overallHeightScale")]
    [Range(0f, 1f)] public float terrainRelief = 0.5f;
    [FormerlySerializedAs("duneHeightScale")]
    [Range(0f, 1f)] public float duneHeight = 0.5f;
    [FormerlySerializedAs("duneDirectionBiasDegrees")]
    [Range(-180f, 180f)] public float duneDirectionDegrees = 35f;
    [Min(0.0001f)] public float duneFrequency = 0.0035f;
    [FormerlySerializedAs("duneWavinessStrength")]
    [Range(0f, 1f)] public float duneVariation = 0.4f;
    [Range(0f, 1f)] public float edgeFlattening = 0.1f;

    [Header("Perimeter Mountains")]
    [FormerlySerializedAs("sideMountainStrength")]
    [FormerlySerializedAs("sideMountainHeightScale")]
    [Range(0f, 1f)] public float mountainHeight = 0.72f;
    [FormerlySerializedAs("sideMountainEndFromEdge01")]
    [Range(0.05f, 0.5f)] public float mountainWidthFromEdge01 = 0.28f;
    [FormerlySerializedAs("distantMountainCoverage")]
    [FormerlySerializedAs("distantMountainNoise")]
    [FormerlySerializedAs("mountainRidgeSharpness")]
    [Range(0f, 1f)] public float mountainVariation = 0.5f;

    [Header("Central Military Base")]
    public bool reserveCenterForBase = true;
    public Vector2 baseCenterNormalized = new Vector2(0.5f, 0.5f);
    [Min(8f)] public float baseRadius = 140f;
    [Min(0f)] public float baseBlendDistance = 45f;
    [Range(0f, 1f)] public float baseFlattenStrength = 0.74f;
    [Range(0.1f, 1f)] public float baseInnerFlatRatio = 0.5f;
    [Range(-0.25f, 0.25f)] public float baseHeightOffset01 = 0f;
    public bool excludeEnvironmentFromBase = true;
    [Min(0f)] public float baseNoSpawnPadding = 28f;

    [Header("Terrain Layers")]
    public TerrainLayer baseLayer;
    public TerrainLayer midLayer;
    public TerrainLayer steepLayer;
    public TerrainLayer accentLayer;
    public TerrainLayer detailLayer;
    [Min(0.01f)] public Vector2 tileSize = new Vector2(30f, 30f);

    [Header("Texture Blending")]
    [Range(16, 2048)] public int alphamapResolution = 512;
    [MinMaxInt(0f, 1f)] public MinMaxFloat slopeBlend = new MinMaxFloat(0.08f, 0.5f);
    [MinMaxInt(0f, 1f)] public MinMaxFloat heightBlend = new MinMaxFloat(0.18f, 0.55f);
    [Min(0.00001f)] public float blendNoiseScale = 0.0014f;
    [Range(0f, 1f)] public float blendNoiseStrength = 0.42f;
    [Min(0.00001f)] public float blendMacroNoiseScale = 0.00036f;
    [Range(0f, 1f)] public float blendMacroNoiseStrength = 0.58f;
    [Min(0f)] public float blendWarpDistance = 95f;
    [Min(0.00001f)] public float blendWarpScale = 0.0009f;
    [Range(0f, 1f)] public float blendBandStrength = 0.32f;
    [Range(0, 3)] public int blendSmoothIterations = 1;

    [Header("Environment")]
    public bool generateRoad = false;
    public bool generateTrees = false;
    public bool generateObjects = false;
    [Range(0f, 1f)] public float treeCenterDensity = 0.9f;
    [Range(1f, 4f)] public float treeCenterBiasExponent = 1.35f;
    [Range(0f, 1f)] public float treeBaseExclusionPaddingMultiplier = 0.05f;
    [Min(1f)] public float roadWidth = 12f;
    [Min(0)] public int treeCount = 360;
    [Min(0)] public int objectCount = 420;
    public GameObject[] treePrefabs;
    public GameObject[] objectPrefabs;
}
