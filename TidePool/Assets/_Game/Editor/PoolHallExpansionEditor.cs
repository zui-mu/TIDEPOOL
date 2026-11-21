using System;
using System.IO;
using System.Linq;
using System.Reflection;
using AbstractOcclusion.WebGpuWater;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Keep the existing preview menu, batch entry points and output locations.
public static class PoolHallExpansionEditor
{
    static string OutputRoot => TidePoolDevelopmentPaths.Folder("unity_hall_expansion");

    [MenuItem("Tools/TidePool/Write Validation Snapshot", true)]
    static bool CanWriteSnapshot()
    {
        return GameObject.Find("Frame (WaterVolume)") && GameObject.Find("PoolWetnessController") &&
               GameObject.Find("WaterControlSwitch") && GameObject.Find("Player");
    }

    [MenuItem("Tools/TidePool/Write Validation Snapshot")]
    public static void WriteSnapshot()
    {
        if (!CanWriteSnapshot()) throw new InvalidOperationException("Open the playable pool hall to inspect its water.");
        var volume = GameObject.Find("Frame (WaterVolume)").GetComponent<WaterVolume>();
        var wetness = GameObject.Find("PoolWetnessController").GetComponent<PoolWetnessController>();
        var scene = SceneManager.GetActiveScene();
        using var water = new SerializedObject(volume);
        using var wet = new SerializedObject(wetness);
        var snapshot = new Snapshot
        {
            scene = scene.path,
            isPlaying = EditorApplication.isPlaying,
            waterPosition = volume.transform.position,
            waterExtent = water.FindProperty("volumeExtent").vector3Value,
            playerPosition = GameObject.Find("Player").transform.position,
            consolePosition = GameObject.Find("WaterControlSwitch").transform.position,
            currentWetness = wet.FindProperty("currentWetness").floatValue,
            waterPaused = (bool)typeof(WaterVolume).GetField("_paused", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(volume),
            wetRendererCount = wet.FindProperty("deckRenderers").arraySize,
            time = DateTime.Now.ToString("s"),
            missingScripts = scene.GetRootGameObjects().Sum(root => root.GetComponentsInChildren<Transform>(true)
                .Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)))
        };
        File.WriteAllText(Path.Combine(OutputRoot, "validation-latest.json"), JsonUtility.ToJson(snapshot, true));
        Debug.Log("Hall validation snapshot written: " + snapshot.scene);
    }

    [MenuItem("Tools/TidePool/Capture Game Preview", true)]
    static bool CanCapturePreview() => EditorApplication.isPlaying;

    [MenuItem("Tools/TidePool/Capture Game Preview")]
    public static void CapturePreview()
    {
        if (!CanCapturePreview()) throw new InvalidOperationException("Capture in Play mode.");
        if (CanWriteSnapshot()) WriteSnapshot();
        ScreenCapture.CaptureScreenshot(Path.Combine(OutputRoot, "poolhall-preview.png"));
    }

    [Serializable]
    class Snapshot
    {
        public string scene, time;
        public bool isPlaying, waterPaused;
        public Vector3 waterPosition, waterExtent, playerPosition, consolePosition;
        public float currentWetness;
        public int wetRendererCount, missingScripts;
    }
}
