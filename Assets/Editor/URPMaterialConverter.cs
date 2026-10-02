using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

// Converts Built-In pipeline materials to the active URP pipeline using Unity's
// own MaterialUpgrader, so property names, surface type, keywords, render queue
// and the SRP batcher CBUFFER all match a genuine URP conversion. This is the
// same machinery behind Edit > Rendering > Materials > Convert All Built-In
// Materials to Current SRP, scoped to whatever the user selects.
//
// Hand-editing the .mat YAML is not used deliberately: getting _Surface, the
// alpha keywords and the blend state right by hand is error prone, and a
// material that looks right can still be wrong for batching.
public static class URPMaterialConverter
{
    [MenuItem("Assets/Convert Material To URP", priority = 2000)]
    private static void ConvertSelected()
    {
        var materials = Selection.GetFiltered<Material>(SelectionMode.Assets);
        if (materials.Length == 0)
        {
            Debug.LogWarning("[URP Convert] select one or more materials first.");
            return;
        }

        var upgraders = FetchUpgraders();
        if (upgraders.Count == 0)
        {
            Debug.LogError("[URP Convert] no URP material upgraders available. " +
                           "Is a URP asset assigned to the active quality level?");
            return;
        }

        int converted = 0, skipped = 0;
        var report = new System.Text.StringBuilder();

        try
        {
            AssetDatabase.StartAssetEditing();

            foreach (var mat in materials)
            {
                if (mat == null) continue;
                string path = AssetDatabase.GetAssetPath(mat);
                string before = mat.shader != null ? mat.shader.name : "<null>";

                if (mat.shader != null && before.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal))
                {
                    skipped++;
                    report.AppendLine($"    skip (already URP) {path}");
                    continue;
                }

                if (mat.shader != null && before.StartsWith("MCP/URP/", StringComparison.Ordinal))
                {
                    skipped++;
                    report.AppendLine($"    skip (custom URP shader) {path}");
                    continue;
                }

                string message = null;
                if (MaterialUpgrader.Upgrade(mat, upgraders, MaterialUpgrader.UpgradeFlags.None, ref message))
                {
                    EditorUtility.SetDirty(mat);
                    converted++;
                    report.AppendLine($"    {before} -> {mat.shader.name}  {path}");
                }
                else
                {
                    skipped++;
                    report.AppendLine($"    no mapping for [{before}]  {path}" +
                                      (string.IsNullOrEmpty(message) ? "" : "  (" + message + ")"));
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        Debug.Log($"[URP Convert] converted {converted}, skipped {skipped}.\n{report}");
        Selection.objects = materials.Cast<UnityEngine.Object>().ToArray();
    }

    [MenuItem("Assets/Convert Material To URP", true)]
    private static bool ValidateConvertSelected()
    {
        return Selection.GetFiltered<Material>(SelectionMode.Assets).Length > 0;
    }

    [MenuItem("Tools/Convert Folder Materials To URP", priority = 30)]
    private static void ConvertAllProjectMaterials()
    {
        var upgraders = FetchUpgraders();
        if (upgraders.Count == 0)
        {
            Debug.LogError("[URP Convert] no URP material upgraders available.");
            return;
        }

        var pending = new List<Material>();
        foreach (var guid in AssetDatabase.FindAssets("t:Material"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) continue;
            if (path.StartsWith("Assets/Editor/", StringComparison.OrdinalIgnoreCase)) continue;

            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null || mat.shader == null) continue;

            string n = mat.shader.name;
            if (n.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal)) continue;
            if (n.StartsWith("MCP/URP/", StringComparison.Ordinal)) continue;
            if (n.StartsWith("Shader Graphs/", StringComparison.Ordinal)) continue;

            pending.Add(mat);
        }

        int converted = 0, skipped = 0;
        var report = new System.Text.StringBuilder();

        try
        {
            AssetDatabase.StartAssetEditing();

            foreach (var mat in pending)
            {
                string path = AssetDatabase.GetAssetPath(mat);
                string before = mat.shader.name;
                string message = null;

                if (MaterialUpgrader.Upgrade(mat, upgraders, MaterialUpgrader.UpgradeFlags.None, ref message))
                {
                    EditorUtility.SetDirty(mat);
                    converted++;
                    report.AppendLine($"    {before} -> {mat.shader.name}  {path}");
                }
                else
                {
                    skipped++;
                    report.AppendLine($"    no mapping for [{before}]  {path}");
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        Debug.Log($"[URP Convert] project-wide: converted {converted}, skipped {skipped}.\n" +
                  Truncate(report, 200));
    }

    // A project-wide sweep can emit hundreds of lines, which floods the Console
    // and buries the summary. Cap the detail and say how much was dropped.
    private static string Truncate(System.Text.StringBuilder report, int maxLines)
    {
        var lines = report.ToString().Split('\n');
        if (lines.Length <= maxLines) return report.ToString();

        var head = new System.Text.StringBuilder();
        for (int i = 0; i < maxLines; i++) head.AppendLine(lines[i]);
        head.AppendLine($"    ... {lines.Length - maxLines} more lines (see full list via Assets > Convert Material To URP per folder)");
        return head.ToString();
    }

    // URP registers 2D converter upgraders for the same pipeline asset, and they
    // claim "Standard" as a source shader for "Universal Render Pipeline/2D/
    // Mesh2D-Lit-Default". That target only renders with a 2D renderer and 2D
    // lights, so a 3D scene using it draws magenta. Only 3D upgraders belong here.
    private static List<MaterialUpgrader> FetchUpgraders()
    {
        var pipelineType = GraphicsSettings.currentRenderPipelineAssetType;
        if (pipelineType == null)
        {
            Debug.LogError("[URP Convert] no active scriptable render pipeline assigned.");
            return new List<MaterialUpgrader>();
        }

        return MaterialUpgrader.FetchAllUpgradersForPipeline(pipelineType)
            .Where(u => !Is2D(u))
            .ToList();
    }

    private static bool Is2D(MaterialUpgrader upgrader)
    {
        return Contains2D(upgrader.OldShaderPath) || Contains2D(upgrader.NewShaderPath);
    }

    private static bool Contains2D(string shaderPath)
    {
        return shaderPath != null && shaderPath.IndexOf("/2D/", StringComparison.Ordinal) >= 0;
    }
}
