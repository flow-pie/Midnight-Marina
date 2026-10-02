using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Renders the demo scene from the game camera and from a top-down overview,
// writes PNGs to /tmp/kilo, and logs every renderer that cannot draw correctly.
// This exists so the remaining magenta can be identified from an actual image
// instead of by guessing which material is on screen.
public static class MCPSceneCapture
{
    private const string OutputDir = "/tmp/kilo";

    private static string ScenePath
    {
        get
        {
            if (!MCPPack.TryResolve("Demo/Scenes/mcp_day.unity", out string p))
            {
                MCPPack.LogMissingRoot("Scene Capture");
                return null;
            }
            return p;
        }
    }

    [MenuItem("Tools/Modernize MCP/Capture Scene Render", priority = 9)]
    public static void Capture()
    {
        string scenePath = ScenePath;
        if (scenePath == null || !File.Exists(scenePath))
        {
            Debug.LogError("[MCP Capture] scene not found: " + scenePath);
            return;
        }

        Directory.CreateDirectory(OutputDir);

        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("[MCP Capture] scene: " + scenePath);

        ReportRenderers(sb);

        var cameras = new List<Camera>(UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None));
        foreach (var cam in cameras)
            sb.Append("  camera " + cam.name).AppendLine();

        int shot = 0;
        foreach (var cam in cameras)
        {
            string file = Path.Combine(OutputDir, $"mcp_day_{cam.name}.png");
            if (Render(cam, 1600, 900, file)) { shot++; sb.AppendLine("  wrote " + file); }
        }

        if (RenderOverview(scene, Path.Combine(OutputDir, "mcp_day_overview.png")))
        {
            shot++;
            sb.AppendLine("  wrote " + Path.Combine(OutputDir, "mcp_day_overview.png"));
        }

        sb.AppendLine($"  images written: {shot}");
        Debug.Log(sb.ToString());
    }

    private static void ReportRenderers(System.Text.StringBuilder sb)
    {
        var problems = new SortedDictionary<string, int>(StringComparer.Ordinal);
        int renderers = 0, slots = 0, nullMat = 0, badShader = 0;

        foreach (var root in sceneRoots())
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderers++;
                var materials = renderer.sharedMaterials;

                if (materials.Length == 0) continue;

                foreach (var mat in materials)
                {
                    slots++;
                    if (mat == null)
                    {
                        nullMat++;
                        Bump(problems, "NULL material on " + Hierarchy(renderer.transform));
                        continue;
                    }

                    if (mat.shader == null)
                    {
                        badShader++;
                        Bump(problems, "NULL shader: " + mat.name + " on " + Hierarchy(renderer.transform));
                        continue;
                    }

                    if (mat.shader.name == "Hidden/InternalErrorShader")
                    {
                        badShader++;
                        Bump(problems, "SHADER COMPILE ERROR: " + mat.name + " on " + Hierarchy(renderer.transform));
                        continue;
                    }

                    if (IsNonUrp(mat))
                    {
                        badShader++;
                        Bump(problems, mat.shader.name + "  on " + Hierarchy(renderer.transform));
                    }
                }
            }
        }

        sb.AppendLine($"  renderers: {renderers}, material slots: {slots}");
        sb.AppendLine($"  null materials: {nullMat}, non-URP/errored shaders: {badShader}");
        sb.AppendLine("  problem renderers:");
        foreach (var kv in problems) sb.AppendLine($"    {kv.Value,4}  {kv.Key}");
    }

    private static bool IsNonUrp(Material mat)
    {
        string n = mat.shader.name;
        if (n.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal)) return false;
        if (n.StartsWith("MCP/URP/", StringComparison.Ordinal)) return false;
        if (n.StartsWith("Shader Graphs/", StringComparison.Ordinal)) return false;
        return string.IsNullOrEmpty(AssetDatabase.GetAssetPath(mat.shader));
    }

    private static IEnumerable<GameObject> sceneRoots()
    {
        return new List<GameObject>(SceneManager.GetActiveScene().GetRootGameObjects());
    }

    private static string Hierarchy(Transform t)
    {
        var parts = new Stack<string>();
        for (var cur = t; cur != null; cur = cur.parent) parts.Push(cur.name);
        return string.Join("/", parts);
    }

    private static bool Render(Camera cam, int width, int height, string path)
    {
        try
        {
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 1;
            var previous = cam.targetTexture;
            var previousActive = RenderTexture.active;

            cam.targetTexture = rt;
            cam.Render();

            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            File.WriteAllBytes(path, tex.EncodeToPNG());

            cam.targetTexture = previous;
            RenderTexture.active = previousActive;
            rt.Release();
            UnityEngine.Object.DestroyImmediate(tex);
            UnityEngine.Object.DestroyImmediate(rt);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("[MCP Capture] render " + cam.name + " failed: " + e.Message);
            return false;
        }
    }

    private static bool RenderOverview(Scene scene, string path)
    {
        var bounds = new Bounds();
        bool first = true;

        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.sharedMaterials.Length == 0) continue;
                if (first) { bounds = renderer.bounds; first = false; }
                else bounds.Encapsulate(renderer.bounds);
            }
        }

        if (first) return false;

        float radius = Mathf.Max(bounds.extents.magnitude, 1f);
        var camObj = new GameObject("MCPCaptureOverview") { hideFlags = HideFlags.HideAndDontSave };
        var cam = camObj.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.13f, 0.13f, 0.16f, 1f);
        cam.fieldOfView = 60f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = radius * 10f;

        cam.transform.position = bounds.center + Vector3.up * radius * 1.6f;
        cam.transform.rotation = Quaternion.Euler(72f, 30f, 0f);

        bool ok;
        try { ok = Render(cam, 1600, 900, path); }
        finally { UnityEngine.Object.DestroyImmediate(camObj); }

        sb.AppendLine("  scene bounds: " + bounds.size);
        return ok;
    }

    private static readonly System.Text.StringBuilder sb = new System.Text.StringBuilder();

    private static void Bump(SortedDictionary<string, int> map, string key)
    {
        if (!map.TryGetValue(key, out int n)) n = 0;
        map[key] = n + 1;
    }
}
