using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

// Modernises the purchased MCP (Midnight City Pack) assets, which were authored
// for the Built-In Render Pipeline and therefore render magenta under URP.
//
// This drives Unity's own migration path (MaterialUpgrader from
// com.unity.render-pipelines.core) rather than hand-mapping properties, so
// texture, colour, float and keyword translation matches Unity's supported
// Built-In -> URP conversion exactly. It is the same machinery behind
// Edit > Rendering > Materials > Convert All Built-In Materials to Current SRP,
// scoped to the MCP pack so your own materials are never touched.
public static class MCPPipelineModernizer
{
    private const string MenuRoot = "Tools/Modernize MCP/";
    private const string Mesh2DLitShaderName = "Universal Render Pipeline/2D/Mesh2D-Lit-Default";
    private const string BuiltInStandardShaderName = "Standard";

    [MenuItem(MenuRoot + "Dry Run", priority = 0)]
    public static void DryRun()
    {
        var upgraders = FetchUpgraders();
        if (upgraders.Count == 0)
        {
            Debug.LogError("[MCP Modernize] No URP material upgraders available. " +
                           "Is a Universal Render Pipeline asset assigned to the active quality level?");
            return;
        }

        var materials = FindMcpMaterials();
        var convertible = 0;
        var blocked = new SortedDictionary<string, int>(StringComparer.Ordinal);

        foreach (var mat in materials)
        {
            if (FindUpgrader(upgraders, mat) != null) { convertible++; continue; }

            string name = mat.shader != null ? mat.shader.name : "<missing shader>";
            if (!blocked.TryGetValue(name, out int n)) n = 0;
            blocked[name] = n + 1;
        }

        int broken = materials.Count(m => IsMesh2DLit(m));

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("[MCP Modernize] DRY RUN - nothing was modified");
        sb.AppendLine($"  URP upgraders registered: {upgraders.Count} (2D upgraders excluded)");
        sb.AppendLine($"  MCP materials found:      {materials.Count}");
        sb.AppendLine($"  already on URP/Lit:       {materials.Count(m => IsUrpLit(m))}");
        sb.AppendLine($"  stuck on the 2D shader:   {broken}");
        sb.AppendLine($"  auto-convertible:         {convertible}");
        sb.AppendLine($"  needing a hand port:      {materials.Count - convertible}");
        if (blocked.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("  Shaders with no built-in mapping (these keep the pink until ported):");
            foreach (var kv in blocked.OrderByDescending(k => k.Value))
                sb.AppendLine($"    {kv.Value,4}  {kv.Key}");
        }
        Debug.Log(sb.ToString());
    }

    [MenuItem(MenuRoot + "Apply", priority = 1)]
    public static void Apply()
    {
        var upgraders = FetchUpgraders();
        if (upgraders.Count == 0)
        {
            Debug.LogError("[MCP Modernize] No URP material upgraders available.");
            return;
        }

        var materials = FindMcpMaterials();
        var failures = new List<string>();
        int repaired = 0, converted = 0, skipped = 0;

        var standardShader = Shader.Find(BuiltInStandardShaderName);
        if (standardShader == null)
            Debug.LogError("[MCP Modernize] Built-in Standard shader not found; " +
                           "materials on the 2D shader cannot be reset for a clean re-upgrade.");

        try
        {
            AssetDatabase.StartAssetEditing();
            for (int i = 0; i < materials.Count; i++)
            {
                var mat = materials[i];
                if (mat == null) continue;

                try
                {
                    if (IsMesh2DLit(mat) && standardShader != null)
                    {
                        mat.shader = standardShader;
                        EditorUtility.SetDirty(mat);
                        repaired++;
                    }

                    string message = null;
                    if (MaterialUpgrader.Upgrade(mat, upgraders, MaterialUpgrader.UpgradeFlags.None, ref message))
                    {
                        EditorUtility.SetDirty(mat);
                        converted++;
                    }
                    else
                    {
                        skipped++;
                    }
                }
                catch (Exception e)
                {
                    failures.Add(AssetDatabase.GetAssetPath(mat) + " :: " + e.Message);
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[MCP Modernize] reset {repaired} materials from the 2D shader, " +
                      $"converted {converted}, left alone {skipped}, failed {failures.Count}");
        foreach (var f in failures.Take(25)) sb.AppendLine("  FAIL " + f);
        sb.AppendLine("Open the scene and check the Game view; water, glass and sprite-based foliage still need a manual port.");
        Debug.Log(sb.ToString());
    }

    private static List<MaterialUpgrader> FetchUpgraders()
    {
        var pipelineType = GraphicsSettings.currentRenderPipelineAssetType;
        if (pipelineType == null)
        {
            Debug.LogError("[MCP Modernize] No active scriptable render pipeline. " +
                           "Assign a URP asset in Project Settings > Graphics, or on the active quality level.");
            return new List<MaterialUpgrader>();
        }

        return MaterialUpgrader.FetchAllUpgradersForPipeline(pipelineType).Where(u => !Is2DUpgrader(u)).ToList();
    }

    // URP registers 2D converter upgraders for the same pipeline asset, and they
    // claim "Standard" (and "Universal Render Pipeline/Lit") as a source shader
    // for "Universal Render Pipeline/2D/Mesh2D-Lit-Default". Those targets only
    // render with a 2D renderer and 2D lights, so a 3D scene using them shows up
    // as magenta patches. Only the 3D upgraders are wanted here.
    private static bool Is2DUpgrader(MaterialUpgrader upgrader)
    {
        return Contains2DPath(upgrader.OldShaderPath) || Contains2DPath(upgrader.NewShaderPath);
    }

    private static bool Contains2DPath(string shaderPath)
    {
        return shaderPath != null && shaderPath.IndexOf("/2D/", StringComparison.Ordinal) >= 0;
    }

    private static bool IsMesh2DLit(Material mat)
    {
        return mat != null && mat.shader != null && mat.shader.name == Mesh2DLitShaderName;
    }

    private static bool IsUrpLit(Material mat)
    {
        return mat != null && mat.shader != null && mat.shader.name == "Universal Render Pipeline/Lit";
    }

    private static MaterialUpgrader FindUpgrader(List<MaterialUpgrader> upgraders, Material material)
    {
        if (material == null || material.shader == null) return null;
        string current = material.shader.name;
        return upgraders.FirstOrDefault(u => u.OldShaderPath == current || u.NewShaderPath == current);
    }

    private static List<Material> FindMcpMaterials()
    {
        string root = MCPPack.Root;
        if (root == null)
        {
            MCPPack.LogMissingRoot("FindMcpMaterials");
            return new List<Material>();
        }

        return AssetDatabase.FindAssets("t:Material", new[] { root })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => !string.IsNullOrEmpty(p))
            .Select(AssetDatabase.LoadAssetAtPath<Material>)
            .Where(m => m != null)
            .ToList();
    }
}
