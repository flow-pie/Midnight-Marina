using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Unity's Tree Creator meshes embed their bark and leaf materials inside the
// prefab (serialized as local fileIDs such as {fileID: 2100000}). Those
// materials reference built-in shaders, which URP cannot render, so the trees
// show magenta.
//
// The embedded material must be MUTATED IN PLACE. Replacing it with a new
// Material instance cannot be persisted into a prefab asset: the new object is
// not an asset, so the reference serializes as {fileID: 0} and the slot becomes
// a null material, which renders magenta all over again.
//
// Earlier versions of this tool used new Material(mat) and did exactly that,
// breaking mcp_tree_01, mcp_tree_02 and BigTree.
public static class MCPTreeShaderFix
{
    private const string MenuRoot = "Tools/Modernize MCP/";

    private static readonly string[] BarkPatterns = { "bark" };
    private static readonly string[] LeafPatterns =
        { "speedtree", "leaves", "foliage", "optimized/tree", "tree soft occlusion" };

    [MenuItem(MenuRoot + "Fix Tree Shaders (Dry Run)", priority = 6)]
    public static void DryRun() => Run(apply: false);

    [MenuItem(MenuRoot + "Fix Tree Shaders", priority = 7)]
    public static void Apply() => Run(apply: true);

    private static void Run(bool apply)
    {
        var paths = AssetDatabase.FindAssets("t:Prefab", MCPPack.PrefabSearchFolders())
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => !string.IsNullOrEmpty(p))
            .ToList();

        var sb = new System.Text.StringBuilder();
        var fixedPaths = new HashSet<string>();
        int fixedSlots = 0, alreadyOk = 0, skippedNonPersistent = 0;

        foreach (var path in paths)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null) continue;

            bool touched = false;

            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;

                foreach (var mat in materials)
                {
                    if (mat == null || mat.shader == null) continue;

                    string shaderName = mat.shader.name;
                    string replacement = Classify(shaderName);
                    if (replacement == null) { alreadyOk++; continue; }

                    if (!apply)
                    {
                        fixedSlots++;
                        sb.AppendLine($"    would fix {mat.name} [{shaderName}] -> {replacement}  in {path}");
                        continue;
                    }

                    var target = Shader.Find(replacement);
                    if (target == null)
                    {
                        Debug.LogError("[MCP Tree Shaders] shader not found: " + replacement);
                        continue;
                    }

                    // A material embedded in a prefab asset reports the PREFAB's path from
// AssetDatabase.GetAssetPath, so an empty-path test wrongly classifies it as a
// standalone material. PrefabUtility is the reliable test.
                    bool embedded = PrefabUtility.IsPartOfPrefabAsset(mat) ||
                                    string.IsNullOrEmpty(AssetDatabase.GetAssetPath(mat));
                    if (!embedded)
                    {
                        sb.AppendLine($"    skip (standalone material, use Remap Shaders) {mat.name} in {path}");
                        skippedNonPersistent++;
                        continue;
                    }

                    mat.shader = target;
                    mat.SetOverrideTag("RenderType", "TransparentCutout");
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                    if (mat.HasProperty("_Cutoff"))
                        mat.SetFloat("_Cutoff", replacement.Contains("Leaves") ? 0.5f : 0.333f);

                    EditorUtility.SetDirty(mat);
                    fixedSlots++;
                    touched = true;
                    sb.AppendLine($"    fixed {mat.name} [{shaderName}] -> {replacement}  in {path}");
                }
            }

            if (touched) fixedPaths.Add(path);
        }

        if (apply && fixedPaths.Count > 0)
        {
            foreach (var p in fixedPaths)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (go != null) EditorUtility.SetDirty(go);
                PrefabUtility.SavePrefabAsset(go);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        Debug.Log($"[MCP Tree Shaders] {(apply ? "applied" : "DRY RUN")}: {fixedSlots} slots, " +
                  $"{fixedPaths.Count} prefabs written, {alreadyOk} already correct, " +
                  $"{skippedNonPersistent} standalone skipped.\n" + sb);
    }

    private static string Classify(string shaderName)
    {
        string n = shaderName.ToLowerInvariant();

        foreach (var p in LeafPatterns)
            if (n.Contains(p)) return "MCP/URP/Tree Leaves Optimized";

        foreach (var p in BarkPatterns)
            if (n.Contains(p)) return "MCP/URP/Tree Bark";

        return null;
    }
}