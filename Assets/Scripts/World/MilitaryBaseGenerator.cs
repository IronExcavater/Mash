using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[ExecuteAlways]
[AddComponentMenu("World/Military Base Generator")]
public class MilitaryBaseGenerator : MonoBehaviour
{
    [Header("Terrain")]
    [SerializeField] private bool autoAssignReferences = true;
    [ConditionalField("autoAssignReferences", false)]
    [SerializeField] private Terrain targetTerrain;
    [SerializeField] private Vector2 baseCenterNormalized = new Vector2(0.5f, 0.5f);
    [SerializeField, Min(40f)] private float baseRadius = 76f;
    [SerializeField, Min(20f)] private float blendDistance = 90f;
    [SerializeField, Range(0f, 1f)] private float terrainFlattenStrength = 0.72f;
    [SerializeField] private float baseHeightOffset = 0f;

    [Header("Build Lifecycle")]
    [SerializeField] private bool rebuildOnStart = true;
    [SerializeField] private bool buildInEditMode = true;
    [SerializeField] private bool flattenTerrain = true;
    [SerializeField] private bool clearExistingChildren = true;
    [SerializeField] private Transform baseRoot;

    [Header("Perimeter")]
    [SerializeField] private GameObject wallPrefab;
    [SerializeField] private GameObject watchTowerPrefab;
    [SerializeField, Min(60f)] private float wallWidth = 108f;
    [SerializeField, Min(60f)] private float wallLength = 102f;
    [SerializeField, Min(0f)] private float wallYOffset = 0f;
    [SerializeField, Min(0f)] private float towerYBoost = 0f;
    [SerializeField, Min(0f)] private float watchTowerInset = 2.5f;
    [SerializeField] private bool placeSideWatchTowers = true;

    [Header("Main Buildings (Pattern)")]
    [SerializeField] private GameObject[] mainBuildingPrefabs;
    [SerializeField, Min(0.6f)] private float mainBuildingScale = 0.9f;
    [SerializeField, Min(6f)] private float buildingInnerInset = 16f;
    [SerializeField, Min(4f)] private float buildingRoadOffset = 14f;
    [SerializeField, Min(8f)] private float buildingRowSpacing = 22f;
    [SerializeField] private float buildingFacingYaw = 90f;

    [Header("Junk (Randomized)")]
    [SerializeField] private GameObject[] junkPrefabs;
    [SerializeField, Min(0)] private int junkCount = 16;
    [SerializeField, Min(8f)] private float junkInnerRadius = 22f;
    [SerializeField, Min(16f)] private float junkOuterRadius = 48f;
    [SerializeField, Min(1f)] private float junkMinSpacing = 3.2f;
    [SerializeField] private Vector2 junkScaleRange = new Vector2(0.9f, 1.1f);

    [Header("Helipad")]
    [SerializeField] private string helipadName = "CentralHelipad";
    [SerializeField] private bool useHelipadVisualPrefab = false;
    [ConditionalField("useHelipadVisualPrefab", true)]
    [SerializeField] private GameObject helipadVisualPrefab;
    [SerializeField, Min(0.6f)] private float helipadElevation = 2.4f;
    [SerializeField, Min(6f)] private float helipadRadius = 12.5f;
    [SerializeField, Min(5f)] private float helipadZoneRadius = 11f;
    [SerializeField] private Color helipadCrossColor = new Color(0.85f, 0.08f, 0.08f, 1f);
    [SerializeField] private Color helipadConcreteColor = new Color(0.40f, 0.40f, 0.42f, 1f);
    [SerializeField] private Color helipadDarkConcreteColor = new Color(0.20f, 0.20f, 0.22f, 1f);
    [SerializeField] private Color helipadAccentColor = new Color(0.6f, 0.6f, 0.6f, 1f);

    private const string DefaultBaseRootName = "MilitaryBase";
    private readonly List<Vector3> junkPoints = new List<Vector3>();
    private float cachedWallTileLength = -1f;

    public Vector3 BaseCenterWorld => targetTerrain == null ? transform.position : GetBaseCenterWorld(targetTerrain);
    public float ProtectedRadius => Mathf.Max(baseRadius, Mathf.Max(wallWidth, wallLength) * 0.5f + 18f);

    private void Start()
    {
        if (!Application.isPlaying || !rebuildOnStart) return;
        BuildOrRefreshBase();
    }

#if UNITY_EDITOR
    private void OnEnable()
    {
        if (Application.isPlaying || !buildInEditMode || !gameObject.scene.IsValid()) return;
        if (autoAssignReferences) AutoAssignPrefabsFromPack();
        BuildOrRefreshBase();
    }
#endif

    [ContextMenu("Build Or Refresh Base")]
    public void BuildOrRefreshBase()
    {
        ResolveTerrain();
        if (targetTerrain == null)
        {
            Debug.LogWarning("MilitaryBaseGenerator: No terrain found.", this);
            return;
        }

        var center = GetBaseCenterWorld(targetTerrain);
        var centerGroundY = targetTerrain.SampleHeight(center) + targetTerrain.transform.position.y + baseHeightOffset;

        if (flattenTerrain)
            FlattenBaseArea(targetTerrain, center, centerGroundY);

        ResolveBaseRoot();
        if (baseRoot == null) return;
        if (clearExistingChildren) ClearChildren(baseRoot);

        cachedWallTileLength = -1f;
        BuildPerimeter(center);
        BuildWatchTowers(center);
        BuildPatternBuildings(center);
        BuildHelipad(center, centerGroundY);
        ScatterJunk(center);
    }

    private void ResolveTerrain()
    {
        if (!autoAssignReferences) return;
        if (targetTerrain == null) targetTerrain = FindFirstObjectByType<Terrain>();
    }

    private void ResolveBaseRoot()
    {
        if (baseRoot != null) return;
        var existing = transform.Find(DefaultBaseRootName);
        if (existing != null)
        {
            baseRoot = existing;
            return;
        }

        var root = new GameObject(DefaultBaseRootName);
        root.transform.SetParent(transform, false);
        baseRoot = root.transform;
    }

    private Vector3 GetBaseCenterWorld(Terrain terrain)
    {
        var data = terrain.terrainData;
        var localCenter = new Vector3(
            Mathf.Clamp01(baseCenterNormalized.x) * data.size.x,
            0f,
            Mathf.Clamp01(baseCenterNormalized.y) * data.size.z);
        var world = terrain.transform.TransformPoint(localCenter);
        world.y = terrain.SampleHeight(world) + terrain.transform.position.y;
        return world;
    }

    private void FlattenBaseArea(Terrain terrain, Vector3 centerWorld, float targetY)
    {
        var data = terrain.terrainData;
        if (data == null) return;

        var resolution = data.heightmapResolution;
        var heights = data.GetHeights(0, 0, resolution, resolution);
        var terrainPosition = terrain.transform.position;
        var localCenter = centerWorld - terrainPosition;
        var centerHeight = Mathf.Clamp01(targetY / Mathf.Max(0.001f, data.size.y));
        var radiusN = Mathf.Max(0.001f, baseRadius / Mathf.Max(0.001f, data.size.x));
        var blendN = Mathf.Max(0.001f, blendDistance / Mathf.Max(0.001f, data.size.x));
        var innerRadiusN = radiusN * 0.42f;
        var totalRadiusN = radiusN + blendN;
        var flatten = Mathf.Clamp01(terrainFlattenStrength);

        for (var y = 0; y < resolution; y++)
        {
            var ny = (float)y / (resolution - 1);
            for (var x = 0; x < resolution; x++)
            {
                var nx = (float)x / (resolution - 1);
                var wx = nx * data.size.x;
                var wz = ny * data.size.z;
                var distN = Vector2.Distance(new Vector2(wx, wz), new Vector2(localCenter.x, localCenter.z)) / Mathf.Max(0.001f, data.size.x);
                if (distN > totalRadiusN) continue;

                float w;
                if (distN <= innerRadiusN)
                {
                    var coreNoise = Mathf.PerlinNoise((wx + 67f) * 0.02f, (wz + 149f) * 0.02f);
                    w = Mathf.Lerp(0.68f, 0.92f, coreNoise);
                }
                else if (distN <= radiusN)
                {
                    var tInner = Mathf.InverseLerp(radiusN, innerRadiusN, distN);
                    w = Mathf.SmoothStep(0.45f, 0.9f, tInner);
                }
                else
                {
                    var t = Mathf.InverseLerp(totalRadiusN, radiusN, distN);
                    w = Mathf.SmoothStep(0f, 0.45f, t);
                }

                heights[y, x] = Mathf.Lerp(heights[y, x], centerHeight, w * flatten);
            }
        }

        data.SetHeights(0, 0, heights);
    }

    private void BuildPerimeter(Vector3 center)
    {
        var halfW = wallWidth * 0.5f;
        var halfL = wallLength * 0.5f;
        var nw = center + new Vector3(-halfW, 0f, halfL);
        var ne = center + new Vector3(halfW, 0f, halfL);
        var sw = center + new Vector3(-halfW, 0f, -halfL);
        var se = center + new Vector3(halfW, 0f, -halfL);

        BuildWallLine("NorthWall", nw, ne);
        BuildWallLine("SouthWall", sw, se);
        BuildWallLine("WestWall", sw, nw);
        BuildWallLine("EastWall", se, ne);
    }

    private void BuildWallLine(string name, Vector3 start, Vector3 end)
    {
        var group = new GameObject(name).transform;
        group.SetParent(baseRoot, false);

        var tileLen = GetWallTileLength();
        var sideLength = Vector3.Distance(new Vector3(start.x, 0f, start.z), new Vector3(end.x, 0f, end.z));
        var segments = Mathf.Max(1, Mathf.RoundToInt(sideLength / Mathf.Max(0.5f, tileLen)));
        var dir = (end - start).normalized;
        dir.y = 0f;
        var rot = Quaternion.LookRotation(dir, Vector3.up);

        for (var i = 0; i < segments; i++)
        {
            var t = (i + 0.5f) / segments;
            var pos = Vector3.Lerp(start, end, t);
            pos.y = SampleGroundY(pos) + wallYOffset;

            var wall = CreateWallInstance();
            wall.name = $"Wall_{name}_{i:00}";
            wall.transform.SetParent(group, true);
            wall.transform.SetPositionAndRotation(pos, rot);
            SnapObjectToGround(wall.transform, wallYOffset);
        }
    }

    private GameObject CreateWallInstance()
    {
        if (wallPrefab != null) return Instantiate(wallPrefab);
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.localScale = new Vector3(5f, 2f, 1f);
        return wall;
    }

    private float GetWallTileLength()
    {
        if (cachedWallTileLength > 0f) return cachedWallTileLength;
        if (wallPrefab == null)
        {
            cachedWallTileLength = 5f;
            return cachedWallTileLength;
        }

        GameObject probe = null;
        try
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                probe = (GameObject)PrefabUtility.InstantiatePrefab(wallPrefab, baseRoot);
            else
                probe = Instantiate(wallPrefab, baseRoot);
#else
            probe = Instantiate(wallPrefab, baseRoot);
#endif
            if (probe == null)
            {
                cachedWallTileLength = 5f;
                return cachedWallTileLength;
            }

            var bounds = CalculateHierarchyBounds(probe.transform);
            cachedWallTileLength = Mathf.Max(0.5f, Mathf.Max(bounds.size.x, bounds.size.z));
            return cachedWallTileLength;
        }
        finally
        {
            if (probe != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(probe);
                else Destroy(probe);
#else
                Destroy(probe);
#endif
            }
        }
    }

    private static Bounds CalculateHierarchyBounds(Transform root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(root.position, Vector3.one);

        var b = renderers[0].bounds;
        for (var i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b;
    }

    private void BuildWatchTowers(Vector3 center)
    {
        var halfW = wallWidth * 0.5f - watchTowerInset;
        var halfL = wallLength * 0.5f - watchTowerInset;

        var points = new List<Vector3>
        {
            center + new Vector3(-halfW, 0f, -halfL),
            center + new Vector3(halfW, 0f, -halfL),
            center + new Vector3(-halfW, 0f, halfL),
            center + new Vector3(halfW, 0f, halfL)
        };

        if (placeSideWatchTowers)
        {
            points.Add(center + new Vector3(0f, 0f, -halfL));
            points.Add(center + new Vector3(0f, 0f, halfL));
            points.Add(center + new Vector3(-halfW, 0f, 0f));
            points.Add(center + new Vector3(halfW, 0f, 0f));
        }

        for (var i = 0; i < points.Count; i++)
        {
            var tower = CreateTowerInstance();
            tower.name = $"WatchTower_{i + 1:00}";
            tower.transform.SetParent(baseRoot, true);
            var p = points[i];
            p.y = SampleGroundY(p) + towerYBoost;
            tower.transform.position = p;
            var toCenter = center - p;
            toCenter.y = 0f;
            tower.transform.rotation = toCenter.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(toCenter.normalized, Vector3.up) * Quaternion.Euler(0f, 180f, 0f)
                : Quaternion.identity;
            SnapObjectToGround(tower.transform, towerYBoost);
        }
    }

    private GameObject CreateTowerInstance()
    {
        if (watchTowerPrefab != null) return Instantiate(watchTowerPrefab);
        var t = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        t.transform.localScale = new Vector3(2.5f, 6.5f, 2.5f);
        return t;
    }

    private void BuildPatternBuildings(Vector3 center)
    {
        var halfL = wallLength * 0.5f - buildingInnerInset;
        var minRoadOffset = Mathf.Max(buildingRoadOffset, 10f);
        var rowHalfSpacing = buildingRowSpacing * 0.5f;

        var hangarPrefab = FindHangarPrefab();
        var supportPrefab = FindSupportBuildingPrefab(hangarPrefab);

        var hangarPositions = new[]
        {
            center + new Vector3(-minRoadOffset, 0f, -rowHalfSpacing),
            center + new Vector3(minRoadOffset, 0f, -rowHalfSpacing),
            center + new Vector3(-minRoadOffset, 0f, rowHalfSpacing),
            center + new Vector3(minRoadOffset, 0f, rowHalfSpacing)
        };

        for (var i = 0; i < hangarPositions.Length; i++)
        {
            var go = SpawnMainBuilding(hangarPrefab, $"Hangar_{i + 1:00}");
            PlaceBuilding(go, hangarPositions[i], GetBuildingRotation(center, hangarPositions[i]), mainBuildingScale);
        }

        var supportPositions = new[]
        {
            center + new Vector3(-minRoadOffset * 0.62f, 0f, -Mathf.Min(halfL, rowHalfSpacing + buildingRowSpacing)),
            center + new Vector3(minRoadOffset * 0.62f, 0f, -Mathf.Min(halfL, rowHalfSpacing + buildingRowSpacing)),
            center + new Vector3(-minRoadOffset * 0.62f, 0f, Mathf.Min(halfL, rowHalfSpacing + buildingRowSpacing)),
            center + new Vector3(minRoadOffset * 0.62f, 0f, Mathf.Min(halfL, rowHalfSpacing + buildingRowSpacing))
        };

        for (var i = 0; i < supportPositions.Length; i++)
        {
            var go = SpawnMainBuilding(supportPrefab, $"SupportBuilding_{i + 1:00}");
            PlaceBuilding(go, supportPositions[i], GetBuildingRotation(center, supportPositions[i]), mainBuildingScale * 0.92f);
        }
    }

    private Quaternion GetBuildingRotation(Vector3 center, Vector3 worldPos)
    {
        var sideYaw = worldPos.x < center.x ? buildingFacingYaw : buildingFacingYaw + 180f;
        return Quaternion.Euler(0f, sideYaw, 0f);
    }

    private void PlaceBuilding(GameObject go, Vector3 worldPos, Quaternion rotation, float scaleMultiplier)
    {
        if (go == null) return;
        go.transform.SetParent(baseRoot, true);
        worldPos.y = SampleGroundY(worldPos);
        go.transform.SetPositionAndRotation(worldPos, rotation);
        go.transform.localScale *= scaleMultiplier;
        SnapObjectToGround(go.transform);
    }

    private GameObject SpawnMainBuilding(GameObject prefab, string name)
    {
        var go = prefab != null ? Instantiate(prefab) : GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        return go;
    }

    private GameObject FindHangarPrefab()
    {
        if (mainBuildingPrefabs == null || mainBuildingPrefabs.Length == 0) return null;
        for (var i = 0; i < mainBuildingPrefabs.Length; i++)
        {
            var prefab = mainBuildingPrefabs[i];
            if (prefab == null) continue;
            var lower = prefab.name.ToLowerInvariant();
            if (lower.Contains("hangar") || lower.Contains("hanger")) return prefab;
        }

        return mainBuildingPrefabs[0];
    }

    private GameObject FindSupportBuildingPrefab(GameObject hangarPrefab)
    {
        if (mainBuildingPrefabs == null || mainBuildingPrefabs.Length == 0) return hangarPrefab;

        for (var i = 0; i < mainBuildingPrefabs.Length; i++)
        {
            var prefab = mainBuildingPrefabs[i];
            if (prefab == null || prefab == hangarPrefab) continue;
            var lower = prefab.name.ToLowerInvariant();
            if (lower.Contains("garage") || lower.Contains("office") || lower.Contains("hq")) return prefab;
        }

        for (var i = 0; i < mainBuildingPrefabs.Length; i++)
        {
            var prefab = mainBuildingPrefabs[i];
            if (prefab != null && prefab != hangarPrefab) return prefab;
        }

        return hangarPrefab;
    }

    private void BuildHelipad(Vector3 center, float centerGroundY)
    {
        var y = centerGroundY + helipadElevation;
        var usingPrefab = useHelipadVisualPrefab && helipadVisualPrefab != null;
        GameObject root;
        if (usingPrefab)
        {
            root = Instantiate(helipadVisualPrefab);
            root.name = helipadName;
            root.transform.SetParent(baseRoot, false);
            root.transform.position = new Vector3(center.x, y, center.z);
        }
        else
        {
            root = new GameObject(helipadName);
            root.transform.SetParent(baseRoot, false);
            root.transform.position = new Vector3(center.x, y, center.z);
            BuildHelipadVisual(root.transform);
        }

        if (!usingPrefab || root.transform.Find("HelipadSupport") == null)
            BuildHelipadSupports(root.transform, centerGroundY, y);
        ApplyHelipadGeometryTuning(root.transform);
        ApplyHelipadMaterialTheme(root.transform);

        var zone = root.GetComponent<HelipadZone>();
        if (zone == null) zone = root.AddComponent<HelipadZone>();
        zone.SetZoneRadius(helipadZoneRadius);
    }

    private void BuildHelipadVisual(Transform root)
    {
        var deck = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        deck.name = "PadDeck";
        deck.transform.SetParent(root, false);
        deck.transform.localPosition = Vector3.zero;
        deck.transform.localScale = new Vector3(helipadRadius, 0.3f, helipadRadius);
        TintRenderer(deck, helipadConcreteColor, 0.05f, 0.4f);

        var landingDisc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        landingDisc.name = "LandingDisc";
        landingDisc.transform.SetParent(root, false);
        landingDisc.transform.localPosition = new Vector3(0f, 0.09f, 0f);
        landingDisc.transform.localScale = new Vector3(helipadRadius * 0.94f, 0.06f, helipadRadius * 0.94f);
        TintRenderer(landingDisc, helipadDarkConcreteColor, 0f, 0.28f);

        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "HelipadRing";
        ring.transform.SetParent(root, false);
        ring.transform.localPosition = new Vector3(0f, 0.14f, 0f);
        ring.transform.localScale = new Vector3(helipadRadius * 1.02f, 0.05f, helipadRadius * 1.02f);
        TintRenderer(ring, helipadAccentColor, 0.02f, 0.35f);

        BuildHelipadCross(root, 0.36f);
        BuildHelipadDetailMarkers(root, 0.33f);
    }

    private void BuildHelipadSupports(Transform helipadRoot, float groundY, float deckY)
    {
        var supportRoot = new GameObject("HelipadSupport").transform;
        supportRoot.SetParent(helipadRoot, false);
        supportRoot.position = helipadRoot.position;

        var height = Mathf.Max(0.6f, deckY - groundY);
        var offsets = new[]
        {
            new Vector3(2.8f, 0f, 2.8f), new Vector3(-2.8f, 0f, 2.8f),
            new Vector3(2.8f, 0f, -2.8f), new Vector3(-2.8f, 0f, -2.8f)
        };
        for (var i = 0; i < offsets.Length; i++)
        {
            var leg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            leg.name = $"Leg_{i + 1}";
            leg.transform.SetParent(supportRoot, false);
            leg.transform.position = new Vector3(helipadRoot.position.x + offsets[i].x, groundY + height * 0.5f, helipadRoot.position.z + offsets[i].z);
            leg.transform.localScale = new Vector3(0.33f, height * 0.5f, 0.33f);
            TintRenderer(leg, helipadAccentColor, 0.1f, 0.35f);
        }
    }

    private void BuildHelipadCross(Transform root, float y)
    {
        var crossLengthFactor = 0.58f;
        var crossThickness = 1.28f;
        var crossY = ResolveHelipadSurfaceY(root) + 0.03f;

        var crossRoot = new GameObject("HelipadCross").transform;
        crossRoot.SetParent(root, false);
        crossRoot.localPosition = new Vector3(0f, crossY, 0f);

        var vertical = GameObject.CreatePrimitive(PrimitiveType.Cube);
        vertical.name = "CrossVertical";
        vertical.transform.SetParent(crossRoot, false);
        vertical.transform.localScale = new Vector3(crossThickness, 0.06f, helipadRadius * crossLengthFactor);
        TintRenderer(vertical, helipadCrossColor, 0f, 0.25f);

        var horizontal = GameObject.CreatePrimitive(PrimitiveType.Cube);
        horizontal.name = "CrossHorizontal";
        horizontal.transform.SetParent(crossRoot, false);
        horizontal.transform.localScale = new Vector3(helipadRadius * crossLengthFactor, 0.06f, crossThickness);
        TintRenderer(horizontal, helipadCrossColor, 0f, 0.25f);
    }

    private void BuildHelipadDetailMarkers(Transform root, float y)
    {
        var markers = 10;
        var markerRadius = helipadRadius * 0.62f;
        var markerY = ResolveHelipadSurfaceY(root) + 0.012f;
        for (var i = 0; i < markers; i++)
        {
            var angle = i * Mathf.PI * 2f / markers;
            var p = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (helipadRadius * 0.5f);
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = $"PadMarker_{i + 1:00}";
            marker.transform.SetParent(root, false);
            marker.transform.localPosition = new Vector3(p.x, markerY, p.z);
            marker.transform.localRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);
            marker.transform.localScale = new Vector3(0.26f, 0.05f, 0.82f);
            TintRenderer(marker, helipadAccentColor, 0f, 0.2f);
        }
    }

    private static void TintRenderer(GameObject go, Color color, float metallic, float smoothness)
    {
        var renderer = go.GetComponent<Renderer>();
        if (renderer == null) return;
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return;

        var mat = new Material(shader) { color = color };
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        renderer.sharedMaterial = mat;
    }

    private void ApplyHelipadMaterialTheme(Transform root)
    {
        if (root == null) return;
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        for (var i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;
            var n = r.gameObject.name.ToLowerInvariant();
            var color = helipadConcreteColor;
            if (n.Contains("landingdisc")) color = helipadDarkConcreteColor;
            else if (n.Contains("ring") || n.Contains("marker") || n.Contains("leg")) color = helipadAccentColor;
            else if (n.Contains("cross")) color = helipadCrossColor;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) continue;
            var mat = new Material(shader) { color = color };
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.02f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.3f);
            r.sharedMaterial = mat;
        }
    }

    private void ApplyHelipadGeometryTuning(Transform root)
    {
        if (root == null) return;

        var deck = root.Find("PadDeck");
        if (deck != null) deck.localScale = new Vector3(helipadRadius, Mathf.Max(0.05f, deck.localScale.y), helipadRadius);

        var disc = root.Find("LandingDisc");
        if (disc != null) disc.localScale = new Vector3(helipadRadius * 0.94f, Mathf.Max(0.04f, disc.localScale.y), helipadRadius * 0.94f);

        var ring = root.Find("HelipadRing");
        if (ring != null) ring.localScale = new Vector3(helipadRadius * 1.02f, Mathf.Max(0.04f, ring.localScale.y), helipadRadius * 1.02f);

        var crossRoot = root.Find("HelipadCross");
        if (crossRoot != null)
        {
            var vertical = crossRoot.Find("CrossVertical");
            if (vertical != null) vertical.localScale = new Vector3(1.28f, Mathf.Max(0.04f, vertical.localScale.y), helipadRadius * 0.58f);

            var horizontal = crossRoot.Find("CrossHorizontal");
            if (horizontal != null) horizontal.localScale = new Vector3(helipadRadius * 0.58f, Mathf.Max(0.04f, horizontal.localScale.y), 1.28f);
            crossRoot.localPosition = new Vector3(0f, ResolveHelipadSurfaceY(root) + 0.03f, 0f);
        }

        var markerIndex = 0;
        foreach (Transform child in root)
        {
            if (!child.name.StartsWith("PadMarker_")) continue;
            var angle = markerIndex * Mathf.PI * 2f / 10f;
            var p = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (helipadRadius * 0.5f);
            child.localPosition = new Vector3(p.x, ResolveHelipadSurfaceY(root) + 0.012f, p.z);
            child.localScale = new Vector3(0.26f, Mathf.Max(0.04f, child.localScale.y), 0.82f);
            markerIndex++;
        }
    }

    private float ResolveHelipadSurfaceY(Transform root)
    {
        if (root == null) return 0.02f;
        var deck = root.Find("PadDeck");
        if (deck == null) return 0.22f;
        return deck.localPosition.y + Mathf.Abs(deck.localScale.y);
    }

    private void ScatterJunk(Vector3 center)
    {
        junkPoints.Clear();
        var allowedJunk = FilterAllowedJunkPrefabs(junkPrefabs);
        if (allowedJunk.Length == 0 || junkCount <= 0) return;

        var minR = Mathf.Min(junkInnerRadius, junkOuterRadius);
        var maxR = Mathf.Max(junkInnerRadius, junkOuterRadius);
        var spacingSqr = Mathf.Max(0.25f, (junkMinSpacing * 2.1f) * (junkMinSpacing * 2.1f));
        var attempts = Mathf.Max(24, junkCount * 20);
        var spawned = 0;

        for (var i = 0; i < attempts && spawned < junkCount; i++)
        {
            var angle = Random.Range(0f, Mathf.PI * 2f);
            var radius = Random.Range(minR, maxR);
            var candidate = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            if (!IsFarEnough(candidate, junkPoints, spacingSqr)) continue;
            if (IsInsideRoadOrBuildingZone(candidate, center)) continue;

            var prefab = allowedJunk[Random.Range(0, allowedJunk.Length)];
            if (prefab == null) continue;
            var p = candidate;
            p.y = SampleGroundY(p);
            var go = Instantiate(prefab, p, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), baseRoot);
            go.name = $"Junk_{spawned + 1:00}";
            var s = Random.Range(Mathf.Min(junkScaleRange.x, junkScaleRange.y), Mathf.Max(junkScaleRange.x, junkScaleRange.y));
            go.transform.localScale *= s;
            SnapObjectToGround(go.transform);
            junkPoints.Add(candidate);
            spawned++;
        }
    }

    private static GameObject[] FilterAllowedJunkPrefabs(GameObject[] source)
    {
        if (source == null || source.Length == 0) return new GameObject[0];
        var list = new List<GameObject>(source.Length);
        for (var i = 0; i < source.Length; i++)
        {
            var prefab = source[i];
            if (prefab == null) continue;
            var n = prefab.name.ToLowerInvariant();
            if (!n.Contains("tree") && !n.Contains("bush"))
                continue;
            if (!list.Contains(prefab)) list.Add(prefab);
        }

        return list.ToArray();
    }

    private bool IsInsideRoadOrBuildingZone(Vector3 candidate, Vector3 center)
    {
        var local = candidate - center;
        var halfRoadZ = wallLength * 0.5f - buildingInnerInset * 0.6f;
        var roadHalfX = 5f;
        if (Mathf.Abs(local.x) <= roadHalfX && Mathf.Abs(local.z) <= halfRoadZ)
            return true;

        var roadOffset = Mathf.Max(buildingRoadOffset, 10f);
        var rowHalf = buildingRowSpacing * 0.5f;

        if (Mathf.Abs(Mathf.Abs(local.x) - roadOffset) <= 8f && Mathf.Abs(Mathf.Abs(local.z) - rowHalf) <= 10f)
            return true;

        var supportX = roadOffset * 0.62f;
        var supportZ = Mathf.Min(wallLength * 0.5f - buildingInnerInset, rowHalf + buildingRowSpacing);
        if (Mathf.Abs(Mathf.Abs(local.x) - supportX) <= 7f && Mathf.Abs(Mathf.Abs(local.z) - supportZ) <= 9f)
            return true;

        return false;
    }

    private void SnapObjectToGround(Transform target, float yOffset = 0f)
    {
        if (targetTerrain == null || target == null) return;
        var p = target.position;
        var groundY = SampleGroundY(p);
        p.y = groundY + yOffset;
        target.position = p;

        var renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0) return;
        var bounds = renderers[0].bounds;
        for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        var delta = groundY - bounds.min.y;
        if (Mathf.Abs(delta) > 0.001f)
            target.position += Vector3.up * delta;
    }

    private float SampleGroundY(Vector3 worldPoint)
    {
        if (targetTerrain != null)
            return targetTerrain.SampleHeight(worldPoint) + targetTerrain.transform.position.y;
        return worldPoint.y;
    }

    private static bool IsFarEnough(Vector3 candidate, List<Vector3> points, float minSpacingSqr)
    {
        for (var i = 0; i < points.Count; i++)
        {
            var d = candidate - points[i];
            d.y = 0f;
            if (d.sqrMagnitude < minSpacingSqr) return false;
        }

        return true;
    }

    private static void ClearChildren(Transform root)
    {
        var toDelete = new List<GameObject>();
        for (var i = 0; i < root.childCount; i++)
            toDelete.Add(root.GetChild(i).gameObject);

        foreach (var child in toDelete)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) Object.DestroyImmediate(child);
            else Object.Destroy(child);
#else
            Object.Destroy(child);
#endif
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Create/Update Helipad Visual Prefab")]
    private void CreateOrUpdateHelipadPrefab()
    {
        var temp = new GameObject("HelipadVisual_Template");
        BuildHelipadVisual(temp.transform);
        var folder = "Assets/Prefabs/Generated";
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets/Prefabs", "Generated");

        var path = $"{folder}/HelipadVisual.prefab";
        helipadVisualPrefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
        DestroyImmediate(temp);
        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private void AutoAssignPrefabsFromPack()
    {
        if (wallPrefab == null)
            wallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Tiny Teacup Studio/Military Base Pack/Prefabs/Ground/Fence.prefab");
        if (watchTowerPrefab == null)
            watchTowerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Tiny Teacup Studio/Military Base Pack/Prefabs/Buildings/Tower1.prefab");
        if (helipadVisualPrefab == null)
            helipadVisualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Generated/HelipadVisual.prefab");

        if (mainBuildingPrefabs == null || mainBuildingPrefabs.Length == 0)
            mainBuildingPrefabs = LoadPrefabsAt("Assets/Tiny Teacup Studio/Military Base Pack/Prefabs/Buildings");

        var extraJunk = LoadPrefabsAtMultiple(new[]
            {
                "Assets/Objects/Trees/Prefabs"
            }, 40);
        junkPrefabs = MergeUniquePrefabs(junkPrefabs, extraJunk, 50);
    }

    private static GameObject[] LoadPrefabsAt(string folder)
    {
        if (!AssetDatabase.IsValidFolder(folder)) return new GameObject[0];
        var guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
        var list = new List<GameObject>(guids.Length);
        for (var i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) list.Add(prefab);
        }

        return list.ToArray();
    }

    private static GameObject[] LoadPrefabsAtMultiple(string[] folders, int maxCount)
    {
        var list = new List<GameObject>();
        for (var i = 0; i < folders.Length; i++)
        {
            var folder = folders[i];
            if (!AssetDatabase.IsValidFolder(folder)) continue;
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
            for (var g = 0; g < guids.Length; g++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[g]);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                var lower = prefab.name.ToLowerInvariant();
                if (!lower.Contains("tree") && !lower.Contains("bush")) continue;
                if (lower.Contains("marine")) continue;

                list.Add(prefab);
                if (list.Count >= maxCount) return list.ToArray();
            }
        }

        return list.ToArray();
    }

    private static GameObject[] MergeUniquePrefabs(GameObject[] existing, GameObject[] additions, int maxCount)
    {
        var merged = new List<GameObject>(maxCount);
        if (existing != null)
        {
            for (var i = 0; i < existing.Length; i++)
            {
                var prefab = existing[i];
                if (prefab == null || merged.Contains(prefab)) continue;
                merged.Add(prefab);
                if (merged.Count >= maxCount) return merged.ToArray();
            }
        }

        if (additions == null) return merged.ToArray();
        for (var i = 0; i < additions.Length; i++)
        {
            var prefab = additions[i];
            if (prefab == null || merged.Contains(prefab)) continue;
            merged.Add(prefab);
            if (merged.Count >= maxCount) break;
        }

        return merged.ToArray();
    }
#endif
}
