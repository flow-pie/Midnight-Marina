using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// The MCP pack has been moved more than once (Assets/MCP ->
// Assets/GameAssets/MCP -> Assets/Settings/MCP). Every tool resolves the pack
// through here so a future move is a one-line change instead of a silent
// "scanned 0 renderers" in five separate files.
public static class MCPPack
{
    private static readonly string[] CandidateRoots =
    {
        "Assets/Settings/MCP",
        "Assets/GameAssets/MCP",
        "Assets/MCP"
    };

    private static string _root;

    public static string Root
    {
        get
        {
            if (_root != null && AssetDatabase.IsValidFolder(_root)) return _root;

            foreach (var candidate in CandidateRoots)
                if (AssetDatabase.IsValidFolder(candidate))
                    return _root = candidate;

            return _root = null;
        }
    }

    public static bool TryResolve(string relativePath, out string fullPath)
    {
        string root = Root;
        if (root == null)
        {
            fullPath = null;
            return false;
        }

        fullPath = root + "/" + relativePath;
        return true;
    }

    public static string[] PrefabSearchFolders()
    {
        string root = Root;
        if (root == null)
        {
            Debug.LogError("[MCP] pack folder not found; looked for " + string.Join(", ", CandidateRoots));
            return new string[0];
        }

        if (_lastRoot != root)
            Debug.Log("[MCP] pack root resolved to " + root);

        _lastRoot = root;
        return new[] { root };
    }

    private static string _lastRoot;

    public static void LogMissingRoot(string caller)
    {
        Debug.LogError($"[MCP] {caller}: pack folder not found. Looked for " +
                       string.Join(", ", CandidateRoots) +
                       ". Update MCPPack.CandidateRoots to the current location.");
    }
}
