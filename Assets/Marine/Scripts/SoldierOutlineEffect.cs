using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Gameplay/Soldiers/Soldier Outline Effect")]
public class SoldierOutlineEffect : MonoBehaviour
{
    [SerializeField] private Color outlineColor = Color.white;
    [SerializeField, Range(0.001f, 0.08f)] private float outlineThickness = 0.026f;

    private const string OutlineShaderPath = "Retro Shaders Pro/Retro Outline";
    private Material outlineMaterialInstance;
    private readonly Dictionary<Renderer, Material[]> originalMaterials = new();

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

        if (outlineMaterialInstance.HasProperty("_BaseColor"))
            outlineMaterialInstance.SetColor("_BaseColor", outlineColor);
        if (outlineMaterialInstance.HasProperty("_Thickness"))
            outlineMaterialInstance.SetFloat("_Thickness", outlineThickness);
        if (outlineMaterialInstance.HasProperty("_SnapsPerUnit"))
            outlineMaterialInstance.SetInt("_SnapsPerUnit", 96);
        if (outlineMaterialInstance.HasProperty("_SnapMode"))
            outlineMaterialInstance.SetFloat("_SnapMode", 2f); // View

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
            existing.CopyTo(expanded, 0);
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
        if (shader == null)
        {
            Debug.LogWarning($"Soldier outline shader not found at '{OutlineShaderPath}'.", this);
            return;
        }
        outlineMaterialInstance = new Material(shader)
        {
            name = "SoldierRetroOutlineMaterial",
            hideFlags = HideFlags.DontSave
        };
    }
}
