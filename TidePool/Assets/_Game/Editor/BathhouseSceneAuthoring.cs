using System;
using System.Linq;
using AbstractOcclusion.WebGpuWater;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Offline authoring only. The build loads the saved scene without invoking this tool.
public static class BathhouseSceneAuthoring
{
    public const string ScenePath = "Assets/_Game/Scenes/Bathhouse_02.unity";

    public static void BuildBatch()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Generate the bathhouse in a separate batch editor.");

        var hall = EditorSceneManager.OpenScene(GirlPlayerIntegration.ScenePath, OpenSceneMode.Single);
        var player = hall.GetRootGameObjects().Single(root => root.GetComponent<PlayerMovement>());
        var view = hall.GetRootGameObjects().Single(root => root.GetComponent<CameraFollow>());
        var bath = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(bath);

        // Move both roots in memory to keep character prefab overrides, camera and rig bindings.
        // The source hall is closed without saving any of these authoring operations.
        UnityEngine.Object.DestroyImmediate(view.GetComponent<DryRegionCameraGuard>());
        UnityEngine.Object.DestroyImmediate(player.GetComponent<WaterInteractable>());
        var sounds = new SerializedObject(player.GetComponent<PlayerSfx>());
        sounds.FindProperty("water").objectReferenceValue = null;
        sounds.FindProperty("dryAirPocket").objectReferenceValue = null;
        sounds.ApplyModifiedPropertiesWithoutUndo();
        SceneManager.MoveGameObjectToScene(player, bath);
        SceneManager.MoveGameObjectToScene(view, bath);
        Bind(player.GetComponent<PlayerMovement>(), "cameraTransform", view.transform);
        Bind(view.GetComponent<CameraFollow>(), "target", player.transform);
        EditorSceneManager.CloseScene(hall, true);

        player.transform.SetPositionAndRotation(new Vector3(1.6f, 1.025f, -.20f), Quaternion.identity);
        view.transform.SetPositionAndRotation(new Vector3(1.6f, 1.56f, -2.06f), Quaternion.Euler(4f, 0f, 0f));
        view.GetComponent<Camera>().fieldOfView = 68f;
        var follow = new SerializedObject(view.GetComponent<CameraFollow>());
        follow.FindProperty("distance").floatValue = 3.95f;
        follow.ApplyModifiedPropertiesWithoutUndo();
        PinkBathhouseInterior.Build();
        if (!EditorSceneManager.SaveScene(bath, ScenePath))
            throw new InvalidOperationException("Could not save " + ScenePath);

        var builds = EditorBuildSettings.scenes.ToList();
        int index = builds.FindIndex(item => item.path == ScenePath);
        if (index < 0) builds.Add(new EditorBuildSettingsScene(ScenePath, true));
        else builds[index].enabled = true;
        EditorBuildSettings.scenes = builds.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("Saved playable Bathhouse_02 and registered the scene.");
    }

    static void Bind(Component component, string field, Transform target)
    {
        var serialized = new SerializedObject(component);
        serialized.FindProperty(field).objectReferenceValue = target;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
