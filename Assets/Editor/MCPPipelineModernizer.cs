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
// scoped to Assets/MCP so your own materials are never touched.
public static class MCPPipelineModernizer
{
    private const string MenuRoot = "Tools/Modernize MCP/";

    [MenuItem(MenuRoot + "Dry Run", priority = 0)]
    public static void DryRun()
    {
        var upgraders = FetchUpgraders();
        if (upgraders == null || upgraders.Count == 0)
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
            // Read-only check: never call MaterialUpgrader.Upgrade here, it mutates.
            if (FindUpgrader(upgraders, mat) != null) { convertible++; continue; }

            string name = mat.shader != null ? mat.shader.name : "<missing shader>";
            if (!blocked.TryGetValue(name, out int n)) n = 0;
            blocked[name] = n + 1;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("[MCP Modernize] DRY RUN - nothing was modified");
        sb.AppendLine($"  URP upgraders registered: {upgraders.Count}");
        sb.AppendLine($"  MCP materials found:      {materials.Count}");
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
        if (upgraders == null || upgraders.Count == 0)
        {
            Debug.LogError("[MCP Modernize] No URP material upgraders available.");
            return;
        }

        var materials = FindMcpMaterials();
        int converted = 0, skipped = 0;
        var failures = new List<string>();

        try
        {
            AssetDatabase.StartAssetEditing();
            for (int i = 0; i < materials.Count; i++)
            {
                var mat = materials[i];
                if (mat == null) continue;

                try
                {
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
        sb.AppendLine($"[MCP Modernize] converted {converted}, left alone {skipped}, failed {failures.Count}");
        foreach (var f in failures.Take(25)) sb.AppendLine("  FAIL " + f);
        sb.AppendLine("Open the scene and check the Game view; water and glass still need a manual port.");
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

        return MaterialUpgrader.FetchAllUpgradersForPipeline(pipelineType);
    }

    private static MaterialUpgrader FindUpgrader(List<MaterialUpgrader> upgraders, Material material)
    {
        if (material == null || material.shader == null) return null;
        string current = material.shader.name;
        return upgraders.FirstOrDefault(u => u.OldShaderPath == current || u.NewShaderPath == current);
    }

    private static List<Material> FindMcpMaterials()
    {
        return AssetDatabase.FindAssets("t:Material", new[] { "Assets/MCP" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => !string.IsNullOrEmpty(p))
            .Select(AssetDatabase.LoadAssetAtPath<Material>)
            .Where(m => m != null)
            .ToList();
    }
}
