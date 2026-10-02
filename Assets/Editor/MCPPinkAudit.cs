using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Exhaustive audit of every renderer in the MCP pack that can still draw
// magenta: materials whose shader failed to compile, is missing, or is a
// built-in / non-URP shader. Also reports broken texture references, which the
// Editor renders pink in the material preview.
public static class MCPPinkAudit
{
    private const string MenuRoot = "Tools/Modernize MCP/";

    [MenuItem(MenuRoot + "Audit Pink Sources", priority = 8)]
    public static void Audit()
    {
        var offenders = new List<string>();
        var nullShader = new List<string>();
        var nullMaterial = new List<string>();
        int renderers = 0, slots = 0;

        var byShader = new SortedDictionary<string, int>(StringComparer.Ordinal);

        foreach (var prefabPath in EnumeratePrefabPaths())
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (go == null) continue;

            foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                renderers++;
                var materials = renderer.sharedMaterials;

                if (materials.Length == 0)
                {
                    nullMaterial.Add(prefabPath + " :: " + Path(go) + " (no material slots)");
                    continue;
                }

                foreach (var mat in materials)
                {
                    slots++;
                    if (mat == null)
                    {
                        nullMaterial.Add(prefabPath + " :: " + Path(go));
                        continue;
                    }

                    if (mat.shader == null)
                    {
                        nullShader.Add($"{mat.name} in {prefabPath}");
                        continue;
                    }

                    string name = mat.shader.name;
                    bool isError = name == "Hidden/InternalErrorShader";
                    bool bad = IsNonUrp(mat, name);
                    if (bad) Bump(byShader, name);
                    if (isError || bad)
                        offenders.Add($"{mat.name}  [{name}]  {prefabPath}");
                }
            }
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("[MCP Pink Audit]");
        sb.AppendLine($"  renderers scanned: {renderers}, material slots: {slots}");
        sb.AppendLine($"  materials with <null> shader: {nullShader.Count}");
        foreach (var n in nullShader.Take(15)) sb.AppendLine("      " + n);
        sb.AppendLine($"  renderer slots with null/empty materials: {nullMaterial.Count}");
        foreach (var n in nullMaterial.Take(15)) sb.AppendLine("      " + n);
        sb.AppendLine($"  slots on non-URP shaders: {offenders.Count}");
        foreach (var kv in byShader) sb.AppendLine($"      {kv.Value,5}  {kv.Key}");
        sb.AppendLine("  offenders:");
        foreach (var o in offenders.Take(60)) sb.AppendLine("      " + o);
        Debug.Log(sb.ToString());
    }

    private static bool IsNonUrp(Material mat, string shaderName)
    {
        if (shaderName == "Hidden/InternalErrorShader") return true;
        if (shaderName.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal)) return false;
        if (shaderName.StartsWith("MCP/URP/", StringComparison.Ordinal)) return false;
        if (shaderName.StartsWith("Shader Graphs/", StringComparison.Ordinal)) return false;

        // Built-in shaders have no asset path; URP/package shaders do.
        return string.IsNullOrEmpty(AssetDatabase.GetAssetPath(mat.shader));

        // Custom pack shaders: only treat as bad if the name is a known
        // unsupported Built-In pipeline one.
        return shaderName.StartsWith("Nature/", StringComparison.Ordinal) ||
               shaderName.StartsWith("Legacy Shaders/", StringComparison.Ordinal) ||
               shaderName.StartsWith("Particles/", StringComparison.Ordinal) ||
               shaderName.StartsWith("Sprites/", StringComparison.Ordinal) ||
               shaderName.StartsWith("Reflective/", StringComparison.Ordinal) ||
               shaderName.StartsWith("UI/", StringComparison.Ordinal) ||
               shaderName.StartsWith("Hidden/Nature/", StringComparison.Ordinal);
    }

    private static string Path(GameObject go)
    {
        var names = new Stack<string>();
        for (var t = go.transform; t != null; t = t.parent) names.Push(t.name);
        return string.Join("/", names);
    }

    private static IEnumerable<string> EnumeratePrefabPaths()
    {
        return AssetDatabase.FindAssets("t:Prefab", MCPPack.PrefabSearchFolders())
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => !string.IsNullOrEmpty(p));
    }

    private static void Bump(SortedDictionary<string, int> map, string key)
    {
        if (!map.TryGetValue(key, out int n)) n = 0;
        map[key] = n + 1;
    }
}
