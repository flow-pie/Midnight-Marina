using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// One entry point that removes every cause of magenta from the MCP pack and the
// day scene, so this no longer has to be driven one bug at a time.
//
// Fixes applied to the currently open scene, with before/after counts:
//  1. Materials on built-in pipeline shaders are moved onto URP equivalents,
//     mutating the material in place. In place matters: replacing a material on
//     a renderer destroys the reference, because an embedded prefab material is
//     not an asset and serializes as {fileID: 0}.
//  2. Null material slots whose source material is missing from the project are
//     given an explicit fallback, so they stop drawing magenta.
//
// Nothing is deleted and no prefab is rewritten unless a material actually
// changed, so a dry run is a faithful preview.
public static class MCPFixAllPink
{
    private const string MenuRoot = "Tools/Modernize MCP/";
    private const string FallbackMaterialPath = "Assets/GameAssets/Materials/MCP_Fallback.mat";

    private static readonly (string builtIn, string urp)[] ShaderMap =
    {
        ("Nature/Bark", "MCP/URP/Tree Bark"),
        ("Nature/Tree Creator Bark Optimized", "MCP/URP/Tree Bark"),
        ("Hidden/Nature/Tree Creator Bark Optimized", "MCP/URP/Tree Bark"),
        ("Optimized/Bark", "MCP/URP/Tree Bark"),
        ("Nature/SpeedTreeBillboard", "MCP/URP/Tree Leaves Optimized"),
        ("Nature/Tree Creator Leaves Optimized", "MCP/URP/Tree Leaves Optimized"),
        ("Hidden/Nature/Tree Creator Leaves Optimized", "MCP/URP/Tree Leaves Optimized"),
        ("Nature/Tree Soft Occlusion Leaves", "MCP/URP/Tree Leaves Optimized"),
        ("Optimized/Leaves", "MCP/URP/Tree Leaves Optimized"),
        ("Nature/SpeedTree", "MCP/URP/Tree Leaves Optimized"),
        ("Legacy Shaders/Particles/Alpha Blended Premultiply", "Universal Render Pipeline/Particles/Unlit"),
        ("Legacy Shaders/Particles/Alpha Blended", "Universal Render Pipeline/Particles/Unlit"),
        ("Legacy Shaders/Particles/Alpha Dotted", "Universal Render Pipeline/Particles/Unlit"),
        ("Legacy Shaders/Particles/Additive", "Universal Render Pipeline/Particles/Unlit"),
        ("Particles/Standard Unlit", "Universal Render Pipeline/Particles/Unlit"),
        ("Sprites/Default", "Universal Render Pipeline/Particles/Unlit"),
        ("Reflective/Transparent/Diffuse", "MCP/URP/Reflective Transparent Diffuse"),
        ("Glass Reflective", "MCP/URP/Glass Reflective"),
        ("Doublesided Bumped Specular", "MCP/URP/Bumped Specular DoubleSided"),
        ("Bumped Diffuse DoubleSided", "MCP/URP/Bumped Diffuse DoubleSided")
    };

    [MenuItem(MenuRoot + "Fix All Pink (Dry Run)", priority = 20)]
    public static void DryRun() => Run(apply: false);

    [MenuItem(MenuRoot + "Fix All Pink", priority = 21)]
    public static void Apply() => Run(apply: true);

    private static void Run(bool apply)
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("[MCP Fix All Pink] no scene open.");
            return;
        }

        var sb = new System.Text.StringBuilder();
        var dirtyAssets = new HashSet<string>();
        var seenMaterials = new Dictionary<Material, int>();

        int nullBefore = 0, badShaderBefore = 0, shaderFixed = 0, nullFilled = 0;

        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                bool changed = false;

                for (int i = 0; i < materials.Length; i++)
                {
                    var mat = materials[i];

                    if (mat == null)
                    {
                        nullBefore++;
                        if (!apply) continue;

                        var fallback = GetFallback();
                        if (fallback == null) continue;

                        materials[i] = fallback;
                        changed = true;
                        nullFilled++;
                        sb.AppendLine($"    null slot -> fallback on {Hierarchy(renderer.transform)}");
                        continue;
                    }

                    if (mat.shader == null) { badShaderBefore++; continue; }

                    if (mat.shader.name == "Hidden/InternalErrorShader")
                    {
                        badShaderBefore++;
                        sb.AppendLine($"    SHADER COMPILE ERROR: {mat.name} on {Hierarchy(renderer.transform)}");
                        continue;
                    }

                    if (IsUrp(mat.shader.name) || IsBuiltInShader(mat.shader)) continue;

                    badShaderBefore++;
                    sb.AppendLine($"    unsupported [{mat.shader.name}] {mat.name} on {Hierarchy(renderer.transform)}");

                    if (!apply) continue;

                    var replacement = FindReplacement(mat.shader.name);
                    if (replacement == null) continue;

                    // Mutate in place. Never assign a fresh Material here.
                    mat.shader = replacement;
                    mat.SetOverrideTag("RenderType", "TransparentCutout");
                    mat.renderQueue = (int)RenderQueue.AlphaTest;
                    if (mat.HasProperty("_Cutoff"))
                        mat.SetFloat("_Cutoff", replacement.name.Contains("Leaves") ? 0.35f : 0.333f);

                    EditorUtility.SetDirty(mat);
                    RegisterAsset(mat, dirtyAssets);
                    shaderFixed++;
                }

                if (changed) renderer.sharedMaterials = materials;
            }
        }

        if (apply && dirtyAssets.Count > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        int nullAfter = 0, badAfter = 0;
        if (apply)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (var mat in renderer.sharedMaterials)
                    {
                        if (mat == null) { nullAfter++; continue; }
                        if (mat.shader == null || !IsUrp(mat.shader.name)) badAfter++;
                    }
                }
            }
        }

        sb.Insert(0,
            $"[MCP Fix All Pink] {(apply ? "APPLIED" : "DRY RUN")} on {scene.name}\n" +
            $"  null material slots:  {nullBefore}" + (apply ? $"  ->  {nullAfter}" : "") + $"\n" +
            $"  bad shader slots:     {badShaderBefore}" + (apply ? $"  ->  {badAfter}" : "") + $"\n" +
            $"  shaders remapped:     {shaderFixed}\n" +
            $"  null slots filled:    {nullFilled}\n" +
            $"  assets written:       {dirtyAssets.Count}\n");

        Debug.Log(sb.ToString());
    }

    private static void RegisterAsset(Material mat, HashSet<string> dirty)
    {
        string path = AssetDatabase.GetAssetPath(mat);
        if (!string.IsNullOrEmpty(path)) dirty.Add(path);
    }

    private static Material GetFallback()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(FallbackMaterialPath);
        if (existing != null) return existing;

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) return null;

        Directory.CreateDirectory(Path.GetDirectoryName(FallbackMaterialPath));

        var mat = new Material(shader)
        {
            name = "MCP_Fallback",
            color = new Color(0.35f, 0.35f, 0.38f, 1f)
        };
        AssetDatabase.CreateAsset(mat, FallbackMaterialPath);
        AssetDatabase.SaveAssets();
        return mat;
    }

    private static bool IsUrp(string shaderName)
    {
        return shaderName.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal) ||
               shaderName.StartsWith("MCP/URP/", StringComparison.Ordinal) ||
               shaderName.StartsWith("Shader Graphs/", StringComparison.Ordinal);
    }

    private static bool IsBuiltInShader(Shader shader)
    {
        return string.IsNullOrEmpty(AssetDatabase.GetAssetPath(shader));
    }

    private static Shader FindReplacement(string builtInName)
    {
        foreach (var (builtIn, urp) in ShaderMap)
        {
            if (!string.Equals(builtIn, builtInName, StringComparison.OrdinalIgnoreCase)) continue;
            var s = Shader.Find(urp);
            if (s != null) return s;
        }
        return null;
    }

    private static string Hierarchy(Transform t)
    {
        var parts = new Stack<string>();
        for (var cur = t; cur != null; cur = cur.parent) parts.Push(cur.name);
        return string.Join("/", parts);
    }
}