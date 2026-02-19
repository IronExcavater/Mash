using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
[AddComponentMenu("Gameplay/Soldiers/Soldier Outline Effect")]
public class SoldierOutlineEffect : MonoBehaviour
{
    [SerializeField] private Color outlineColor = Color.white;
    [SerializeField, Range(0.001f, 0.08f)] private float outlineThickness = 0.026f;

    private const string OutlineShaderPath = "Mash/SoldierDepthOutline";
    private Material outlineMaterialInstance;
    private readonly Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();

    private void OnEnable()
    {
        ApplyOutline();
    }

    private void OnDisable()
    {
        RemoveOutline();
    }

    public void Rebuild()
    {
        RemoveOutline();
        ApplyOutline();
    }

    private void ApplyOutline()
    {
        EnsureOutlineMaterial();
        if (outlineMaterialInstance == null) return;

        outlineMaterialInstance.SetColor("_OutlineColor", outlineColor);
        outlineMaterialInstance.SetFloat("_OutlineWidth", outlineThickness);

        var renderers = GetComponentsInChildren<Renderer>(true);
        for (var i = 0; i < renderers.Length; i++)
        {
            var renderer = renderers[i];
            if (renderer == null) continue;
            if (!renderer.enabled) continue;
            if (renderer is ParticleSystemRenderer) continue;
            if (renderer.sharedMaterials == null || renderer.sharedMaterials.Length == 0) continue;
            if (originalMaterials.ContainsKey(renderer)) continue;

            var existing = renderer.sharedMaterials;
            var expanded = new Material[existing.Length + 1];
            for (var m = 0; m < existing.Length; m++) expanded[m] = existing[m];
            expanded[expanded.Length - 1] = outlineMaterialInstance;
            originalMaterials[renderer] = existing;
            renderer.sharedMaterials = expanded;
        }
    }

    private void RemoveOutline()
    {
        foreach (var kvp in originalMaterials)
        {
            if (kvp.Key == null) continue;
            kvp.Key.sharedMaterials = kvp.Value;
        }
        originalMaterials.Clear();

        if (outlineMaterialInstance != null)
        {
            if (Application.isPlaying) Destroy(outlineMaterialInstance);
            else DestroyImmediate(outlineMaterialInstance);
        }
        outlineMaterialInstance = null;
    }

    private void EnsureOutlineMaterial()
    {
        if (outlineMaterialInstance != null) return;
        var shader = Shader.Find(OutlineShaderPath);
        if (shader == null) return;
        outlineMaterialInstance = new Material(shader)
        {
            name = "SoldierDepthOutlineMaterial",
            hideFlags = HideFlags.DontSave
        };
    }
}
