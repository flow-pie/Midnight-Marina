using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Assets/GameAssets/Environment.prefab contains prefab instances whose source
// prefabs are not in the pack. Unity draws each of those as a magenta box in the
// Scene view, which is a second, independent source of the pink patches.
//
// This relinks the instances whose replacement is unambiguous in the pack and
// deletes the ones with no equivalent asset. A broken instance is a Transform
// with no components, so deleting it loses no geometry or materials.
public static class MCPMissingPrefabRepair
{
    private const string MenuRoot = "Tools/Modernize MCP/";
    private const string EnvironmentPrefab = "Assets/GameAssets/Environment.prefab";

    private static string PropsFolder
    {
        get
        {
            if (!MCPPack.TryResolve("Street Props/- Prefabs/", out string folder))
                MCPPack.LogMissingRoot("Repair Missing Prefabs");
            return folder;
        }
    }

    private static readonly Dictionary<string, string> Replacements =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "FEnse", "mcp_street_fence_01_LOD_0.prefab" },
            { "fire hydrant", "mcp_fire_hydrant_01_a_LOD_0.prefab" },
            { "newsbox", "mcp_newspaper_box_01_LOD_0.prefab" },
            { "traffic sign1", "mcp_one_way_sign_01_LOD_0.prefab" },
            { "traffic sign4", "mcp_one_way_sign_02_LOD_0.prefab" }
        };

    [MenuItem(MenuRoot + "Repair Missing Prefabs (Dry Run)", priority = 4)]
    public static void DryRun()
    {
        Report(apply: false);
    }

    [MenuItem(MenuRoot + "Repair Missing Prefabs", priority = 5)]
    public static void Apply()
    {
        Report(apply: true);
    }

    private static void Report(bool apply)
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(EnvironmentPrefab);
        if (root == null)
        {
            Debug.LogError("[MCP Missing Prefabs] not found: " + EnvironmentPrefab);
            return;
        }

        var broken = new List<GameObject>();
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            var go = t.gameObject;
            if (!PrefabUtility.IsAnyPrefabInstanceRoot(go)) continue;
            if (PrefabUtility.GetCorrespondingObjectFromSource(go) != null) continue;
            broken.Add(go);
        }

        var relinked = new List<string>();
        var removed = new List<string>();
        var unmatched = new SortedDictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var go in broken)
            if (Replacements.TryGetValue(go.name, out string path))
                relinked.Add(go.name + " -> " + Path.GetFileNameWithoutExtension(path));
            else
                unmatched[go.name] = unmatched.TryGetValue(go.name, out int n) ? n + 1 : 1;

        if (apply)
        {
            var toRemove = new List<GameObject>();

            foreach (var go in broken)
            {
                if (!Replacements.TryGetValue(go.name, out string fileName)) { toRemove.Add(go); continue; }

                string path = PropsFolder + fileName;
                var replacement = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (replacement == null)
                {
                    Debug.LogError("[MCP Missing Prefabs] replacement missing, deleting instead: " + path);
                    toRemove.Add(go);
                    continue;
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(replacement);
                try
                {
                    instance.transform.SetParent(go.transform.parent, false);
                    instance.transform.localPosition = go.transform.localPosition;
                    instance.transform.localRotation = go.transform.localRotation;
                    instance.transform.localScale = go.transform.localScale;
                    Undo.RegisterCreatedObjectUndo(instance, "Repair missing prefab");
                }
                catch (Exception e)
                {
                    Debug.LogError("[MCP Missing Prefabs] relink failed for " + go.name + ": " + e.Message);
                    UnityEngine.Object.DestroyImmediate(instance);
                    toRemove.Add(go);
                    continue;
                }

                toRemove.Add(go);
            }

            foreach (var go in toRemove)
            {
                removed.Add(go.name);
                Undo.DestroyObjectImmediate(go);
            }

            try
            {
                PrefabUtility.SaveAsPrefabAsset(root, EnvironmentPrefab);
            }
            catch (Exception e)
            {
                Debug.LogError("[MCP Missing Prefabs] save failed: " + e.Message);
            }
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine(apply
            ? $"[MCP Missing Prefabs] relinked {relinked.Count}, removed {removed.Count}"
            : $"[MCP Missing Prefabs] DRY RUN - nothing modified. {broken.Count} broken instances found.");
        foreach (var r in relinked) sb.AppendLine("    relink " + r);
        if (unmatched.Count > 0)
        {
            sb.AppendLine("    no replacement in the pack, removed on apply:");
            foreach (var kv in unmatched) sb.AppendLine($"      {kv.Value,4}  {kv.Key}");
        }
        Debug.Log(sb.ToString());
    }
}
