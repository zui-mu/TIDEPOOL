using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Editor-only: diagnostic files live beside Assets, never inside the imported asset tree.
[InitializeOnLoad]
public static class TidePoolDevelopmentPaths
{
    public static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "../Development"));

    public static string Folder(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(new[] {'/', '\\'}) >= 0 || name == "..")
            throw new ArgumentException("Expected one development folder name", nameof(name));
        string path = Path.Combine(Root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    static TidePoolDevelopmentPaths()
    {
        EditorApplication.delayCall += WriteHealth;
    }

    static void WriteHealth()
    {
        Scene scene = SceneManager.GetActiveScene();
        var roots = scene.IsValid() && scene.isLoaded ? scene.GetRootGameObjects() : Array.Empty<GameObject>();
        int missing = roots.Sum(root => root.GetComponentsInChildren<Transform>(true)
            .Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)));
        string[] lines = {
            "Imported=" + DateTime.Now.ToString("s"),
            "Project=" + Path.GetFullPath(Path.Combine(Application.dataPath, "..")),
            "Development=" + Root,
            "Scene=" + scene.path,
            "Playing=" + EditorApplication.isPlaying,
            "SceneDirty=" + scene.isDirty,
            "MissingScripts=" + missing,
            "Note=Editor domain loaded after migration; scene/runtime sources were not modified."
        };
        File.WriteAllLines(Path.Combine(Folder("Migration"), "unity-health.txt"), lines);
    }
}
