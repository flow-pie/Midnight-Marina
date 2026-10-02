using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// The MCP pack still carries shaders URP cannot execute: four legacy
// "#pragma surface" shaders and a handful of built-in particle/sprite shaders.
// Every renderer using one of those materials draws magenta. This swaps them
// onto URP replacements under Assets/Shaders/URP, preserving each material's
// existing property values and render queue.
public static class MCPShaderRemap
{
    private const string MenuRoot = "Tools/Modernize MCP/";

    private static readonly Dictionary<string, string> ShaderReplacements = new Dictionary<string, string>
    {
        { "Glass Reflective", "MCP/URP/Glass Reflective" },
        { "Reflective/Transparent/Diffuse", "MCP/URP/Reflective Transparent Diffuse" },
        { "Doublesided Bumped Specular", "MCP/URP/Bumped Specular DoubleSided" },
        { "Bumped Diffuse DoubleSided", "MCP/URP/Bumped Diffuse DoubleSided" },
        { "Legacy Shaders/Particles/Alpha Blended Premultiply", "Universal Render Pipeline/Particles/Unlit" },
        { "Legacy Shaders/Particles/Alpha Blended", "Universal Render Pipeline/Particles/Unlit" },
        { "Legacy Shaders/Particles/Alpha Dotted", "Universal Render Pipeline/Particles/Unlit" },
        { "Legacy Shaders/Particles/Additive", "Universal Render Pipeline/Particles/Unlit" },
        { "Particles/Standard Unlit", "Universal Render Pipeline/Particles/Unlit" },
        { "Sprites/Default", "Universal Render Pipeline/Particles/Unlit" }
    };

    private static readonly Dictionary<string, string> PropertyRenames = new Dictionary<string, string>
    {
        { "_MainTex", "_BaseMap" },
        { "_TintColor", "_BaseColor" }
    };

    [MenuItem(MenuRoot + "Remap Shaders (Dry Run)", priority = 2)]
    public static void DryRun()
    {
        Report(collect: true, apply: false);
    }

    [MenuItem(MenuRoot + "Remap Shaders", priority = 3)]
    public static void Apply()
    {
        Report(collect: false, apply: true);
    }

    private static void Report(bool collect, bool apply)
    {
        var materials = AssetDatabase.FindAssets("t:Material", MCPPack.PrefabSearchFolders())
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => !string.IsNullOrEmpty(p))
            .Select(AssetDatabase.LoadAssetAtPath<Material>)
            .Where(m => m != null)
            .ToList();

        var plan = new SortedDictionary<string, int>(StringComparer.Ordinal);
        int remapped = 0, skipped = 0;
        var failures = new List<string>();

        try
        {
            if (apply) AssetDatabase.StartAssetEditing();

            foreach (var mat in materials)
            {
                string current = mat.shader != null ? mat.shader.name : "<null>";
                if (!ShaderReplacements.TryGetValue(current, out string target))
                {
                    if (current == "<null>") plan["<null shader>"] = Bump(plan, "<null shader>");
                    skipped++;
                    continue;
                }

                var replacement = Shader.Find(target);
                if (replacement == null)
                {
                    failures.Add(AssetDatabase.GetAssetPath(mat) + " :: shader not found: " + target);
                    continue;
                }

                if (!apply) { remapped++; plan[current] = Bump(plan, current); continue; }

                try
                {
                    mat.shader = replacement;
                    CopyProperties(mat);
                    EditorUtility.SetDirty(mat);
                    remapped++;
                    plan[current] = Bump(plan, current);
                }
                catch (Exception e)
                {
                    failures.Add(AssetDatabase.GetAssetPath(mat) + " :: " + e.Message);
                }
            }
        }
        finally
        {
            if (apply)
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine(apply
            ? $"[MCP Shader Remap] remapped {remapped}, unchanged {skipped}, failed {failures.Count}"
            : $"[MCP Shader Remap] DRY RUN - nothing modified. would remap {remapped}, unchanged {skipped}");
        foreach (var kv in plan.OrderByDescending(k => k.Value))
            sb.AppendLine($"    {kv.Value,4}  {kv.Key} -> {ShaderReplacements.GetValueOrDefault(kv.Key, "?")}");
        foreach (var f in failures.Take(25)) sb.AppendLine("  FAIL " + f);
        Debug.Log(sb.ToString());
    }

    private static int Bump(SortedDictionary<string, int> map, string key)
    {
        if (!map.TryGetValue(key, out int n)) n = 0;
        map[key] = n + 1;
        return map[key];
    }

    private static void CopyProperties(Material mat)
    {
        foreach (var pair in PropertyRenames)
        {
            if (!mat.HasProperty(pair.Key) || !mat.HasProperty(pair.Value)) continue;

            if (mat.GetTexture(pair.Value) != null) continue;

            if (IsTextureShaded(mat, pair.Key))
                mat.SetTexture(pair.Value, mat.GetTexture(pair.Key));
            else
                mat.SetColor(pair.Value, mat.GetColor(pair.Key));
        }

        // A particle/sprite material that was blending in the built-in pipeline has to
        // stay transparent under URP; mapping it to an opaque surface would make smoke
        // and water render as solid quads.
        bool wasTransparent = mat.renderQueue >= (int)UnityEngine.Rendering.RenderQueue.Transparent;
        if (!wasTransparent || !mat.HasProperty("_Surface")) return;

        mat.SetFloat("_Surface", wasTransparent ? 1f : 0f);
        mat.SetFloat("_Blend", 0f);

        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);

        mat.DisableKeyword("_ALPHATEST_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    private static bool IsTextureShaded(Material mat, string property)
    {
        var shader = mat.shader;
        int index = shader.FindPropertyIndex(property);
        if (index < 0) return false;
        return shader.GetPropertyType(index) == UnityEngine.Rendering.ShaderPropertyType.Texture;
    }
}
