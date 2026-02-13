using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "TerrainBiomeProfile", menuName = "Mash/Terrain Biome Profile")]
public class TerrainBiomeProfile : ScriptableObject
{
    [Header("Terrain Size")]
    [Min(64f)] public float terrainWidth = 3000f;
    [Min(64f)] public float terrainLength = 3000f;
    [Min(16f)] public float terrainHeight = 200f;
    [Range(129, 4097)] public int heightmapResolution = 1025;
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

    [Header("Terrain Layers")]
    public TerrainLayer baseLayer;
    public TerrainLayer midLayer;
    public TerrainLayer steepLayer;
    [Min(0.01f)] public Vector2 tileSize = new Vector2(30f, 30f);
    public Color fallbackBaseColor = new Color(0.80f, 0.70f, 0.45f, 1f);
    public Color fallbackMidColor = new Color(0.63f, 0.56f, 0.38f, 1f);
    public Color fallbackSteepColor = new Color(0.45f, 0.41f, 0.34f, 1f);

    [Header("Texture Blending")]
    [Range(16, 2048)] public int alphamapResolution = 1024;
    [MinMaxInt(0f, 1f)] public MinMaxFloat slopeBlend = new MinMaxFloat(0.08f, 0.5f);
    [MinMaxInt(0f, 1f)] public MinMaxFloat heightBlend = new MinMaxFloat(0.18f, 0.55f);
    [Min(0.00001f)] public float blendNoiseScale = 0.0014f;
    [Range(0f, 1f)] public float blendNoiseStrength = 0.42f;

    [Header("Environment")]
    public bool generateRoad = false;
    public bool generateTrees = false;
    public bool generateObjects = false;
    [Min(1f)] public float roadWidth = 12f;
    [Min(0)] public int treeCount = 220;
    [Min(0)] public int objectCount = 120;
    public GameObject[] treePrefabs;
    public GameObject[] objectPrefabs;
}
