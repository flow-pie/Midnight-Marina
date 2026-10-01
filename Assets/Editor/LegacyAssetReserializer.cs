using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// One-off migration for the legacy MCP (Midnight City Pack) assets, which were
// authored in Unity 4.3/5.1 and are still stored in the pre-2019.1 binary
// format. Unity logs "Serialized files [version N] before 2019.1 are
// deprecated" for each one it touches, and flags stale occlusion culling data
// baked by those old editors.
public static class LegacyAssetReserializer
{
    private static readonly string[] AssetExtensions = {
        ".mat", ".asset", ".prefab", ".controller", ".physicMaterial", ".physicsMaterial",
        ".flare", ".guiskin", ".fontsettings", ".cubemap", ".renderTexture", ".mask",
        ".anim", ".overrideController", ".playable", ".mixer", ".shadervariants",
        ".terrainlayer", ".lighting", ".giparams", ".renderSettings", ".preset",
        ".spriteatlas", ".terrainData", ".brush", ".signal"
    };

    [MenuItem("Tools/Legacy Assets/Dry Run")]
    public static void DryRun()
    {
        var assets = FindBinaryAssets();
        var scenes = FindBinaryScenes();

        Debug.Log($"[Legacy Assets] DRY RUN (no files modified)\n" +
                  $"  legacy binary assets: {assets.Count}\n" +
                  $"  legacy binary scenes: {scenes.Count}\n" +
                  $"  enabled build scenes: {string.Join(", ", EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path))}");

        foreach (var a in assets) Debug.Log("  asset: " + a);
        foreach (var s in scenes) Debug.Log("  scene: " + s);
    }

    [MenuItem("Tools/Legacy Assets/Reserialize")]
    public static void Reserialize()
    {
        var assets = FindBinaryAssets();
        var scenes = FindBinaryScenes();

        if (assets.Count > 0)
        {
            AssetDatabase.ForceReserializeAssets(assets, ForceReserializeAssetsOptions.ReserializeAssetsAndMetadata);
            Debug.Log($"[Legacy Assets] reserialized {assets.Count} assets");
        }

        foreach (var path in scenes)
        {
            ClearStaleOcclusionData(path);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Legacy Assets] done");
    }

    // Re-saving the scene in the current editor already drops the old baked
    // data. Clearing it explicitly first makes the intent unambiguous and
    // guarantees the "out of date, please rebake" warning cannot survive.
    private static void ClearStaleOcclusionData(string path)
    {
        var existing = SceneManager.GetSceneByPath(path);
        bool wasLoaded = existing.IsValid() && existing.isLoaded;

        var scene = wasLoaded ? existing : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            StaticOcclusionCulling.Clear();
            Debug.Log($"[Legacy Assets] cleared stale occlusion data in {path}");
        }
        finally
        {
            EditorSceneManager.SaveScene(scene);
            if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static List<string> FindBinaryAssets()
    {
        var extensions = new HashSet<string>(AssetExtensions, StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();

        foreach (var path in EnumerateAssetFiles())
        {
            if (extensions.Contains(Path.GetExtension(path)))
                result.Add(path);
        }
        return result;
    }

    private static List<string> FindBinaryScenes()
    {
        return EnumerateAssetFiles()
            .Where(p => p.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static IEnumerable<string> EnumerateAssetFiles()
    {
        if (!Directory.Exists("Assets")) return Enumerable.Empty<string>();
        return Directory.EnumerateFiles("Assets", "*", SearchOption.AllDirectories)
            .Where(IsBinarySerialized);
    }

    // Unity text-serialized assets begin with '%' (the "%YAML 1.1" header).
    // Anything else is legacy binary and triggers the version 6/8/9/15 warnings.
    private static bool IsBinarySerialized(string path)
    {
        if (path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) return false;
        if (!File.Exists(path)) return false;

        try
        {
            using (var stream = File.OpenRead(path))
            {
                return stream.ReadByte() != '%';
            }
        }
        catch (IOException)
        {
            return false;
        }
    }
}
