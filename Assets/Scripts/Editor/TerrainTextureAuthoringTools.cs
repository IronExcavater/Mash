#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class TerrainTextureAuthoringTools
{
    private const string SourceRoot = "Assets/Terrain/Textures";
    private const string MaterialOutRoot = "Assets/Terrain/Materials";
    private const string LayerOutRoot = "Assets/Terrain/Layers";

    [MenuItem("Tools/Terrain/Generate Terrain Materials + Layers")]
    public static void GenerateTerrainAssets()
    {
        EnsureFolder("Assets/Terrain");
        EnsureFolder(MaterialOutRoot);
        EnsureFolder(LayerOutRoot);

        var sets = FindTextureSets();
        var materialCount = 0;
        var layerCount = 0;

        foreach (var set in sets)
        {
            if (CreateOrUpdateMaterial(set) != null) materialCount++;
            if (CreateOrUpdateTerrainLayer(set) != null) layerCount++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Terrain authoring complete. Materials: {materialCount}, Layers: {layerCount}.");
    }

    private static List<TextureSet> FindTextureSets()
    {
        var sets = new List<TextureSet>();
        if (!Directory.Exists(SourceRoot)) return sets;

        var guids = AssetDatabase.FindAssets("*_basecolor t:Texture2D", new[] { SourceRoot });
        for (var i = 0; i < guids.Length; i++)
        {
            var basePath = AssetDatabase.GUIDToAssetPath(guids[i]);
            var dir = Path.GetDirectoryName(basePath)?.Replace("\\", "/");
            if (string.IsNullOrEmpty(dir)) continue;

            var baseName = Path.GetFileNameWithoutExtension(basePath);
            var prefix = baseName.Replace("_basecolor", string.Empty);
            if (string.IsNullOrEmpty(prefix)) continue;

            var set = new TextureSet
            {
                Name = prefix,
                BaseColor = LoadTexture($"{dir}/{prefix}_basecolor.png"),
                Normal = LoadTexture($"{dir}/{prefix}_normal.png")
            };

            if (set.BaseColor != null) sets.Add(set);
        }

        return sets;
    }

    private static Material CreateOrUpdateMaterial(TextureSet set)
    {
        var shader = GetShader();
        if (shader == null) return null;

        var path = $"{MaterialOutRoot}/{set.Name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.shader = shader;
        }

        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", set.BaseColor);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", set.BaseColor);

        if (set.Normal != null)
        {
            SetTextureTypeNormal(set.Normal);
            if (mat.HasProperty("_BumpMap")) mat.SetTexture("_BumpMap", set.Normal);
            mat.EnableKeyword("_NORMALMAP");
        }

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);

        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static TerrainLayer CreateOrUpdateTerrainLayer(TextureSet set)
    {
        var path = $"{LayerOutRoot}/{set.Name}.terrainlayer";
        var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        if (layer == null)
        {
            layer = new TerrainLayer();
            AssetDatabase.CreateAsset(layer, path);
        }

        layer.diffuseTexture = set.BaseColor;
        layer.normalMapTexture = set.Normal;
        layer.tileSize = new Vector2(22f, 22f);
        layer.tileOffset = Vector2.zero;

        EditorUtility.SetDirty(layer);
        return layer;
    }

    private static Shader GetShader()
    {
        var srp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;
        if (srp)
        {
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit != null && urpLit.isSupported) return urpLit;
            var urpSimple = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (urpSimple != null && urpSimple.isSupported) return urpSimple;
            return null;
        }

        var standard = Shader.Find("Standard");
        return standard != null && standard.isSupported ? standard : null;
    }

    private static void SetTextureTypeNormal(Texture2D texture)
    {
        var path = AssetDatabase.GetAssetPath(texture);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        if (importer.textureType == TextureImporterType.NormalMap) return;
        importer.textureType = TextureImporterType.NormalMap;
        importer.SaveAndReimport();
    }

    private static Texture2D LoadTexture(string path)
    {
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath)) return;
        var parent = Path.GetDirectoryName(folderPath)?.Replace("\\", "/");
        var name = Path.GetFileName(folderPath);
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name)) return;
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    private sealed class TextureSet
    {
        public string Name;
        public Texture2D BaseColor;
        public Texture2D Normal;
    }
}
#endif
