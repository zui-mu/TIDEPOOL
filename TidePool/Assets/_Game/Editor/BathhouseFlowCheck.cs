using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class BathhouseFlowCheck
{
    const string State = "TidePool.BathhouseCheck";
    static IEnumerator sequence;
    static int lastFrame;
    static double deadline;
    static Keyboard keyboard;
    static bool passed;

    static BathhouseFlowCheck() => EditorApplication.playModeStateChanged += OnPlayMode;

    public static void RunBatch() => Start(false);
    public static void RunDirectBatch() => Start(true);

    static void Start(bool direct)
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch editor.");
        EditorSceneManager.OpenScene(direct ? BathhouseSceneAuthoring.ScenePath : GirlPlayerIntegration.ScenePath);
        SessionState.SetInt(State, direct ? 2 : 1);
        EditorApplication.EnterPlaymode();
    }

    static void OnPlayMode(PlayModeStateChange mode)
    {
        int state = SessionState.GetInt(State, 0);
        if (state == 0) return;
        if (mode == PlayModeStateChange.EnteredPlayMode)
        {
            sequence = Run(state == 2);
            lastFrame = -1;
            deadline = EditorApplication.timeSinceStartup + 300;
            EditorApplication.update += Tick;
        }
        if (mode == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.EraseInt(State);
            EditorApplication.Exit(state == 3 ? 0 : 1);
        }
    }

    static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Bathhouse flow timed out.");
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            if (sequence.MoveNext()) return;
            passed = true;
        }
        catch (Exception e) { Debug.LogException(e); }
        EditorApplication.update -= Tick;
        (sequence as IDisposable)?.Dispose();
        SessionState.SetInt(State, passed ? 3 : 4);
        if (passed) Debug.Log("Bathhouse flow passed");
        EditorApplication.ExitPlaymode();
    }

    static IEnumerator Run(bool direct)
    {
        var previousKeyboard = Keyboard.current;
        var input = InputSystem.settings;
        var oldBackground = input.backgroundBehavior;
        var oldEditorInput = input.editorInputBehaviorInPlayMode;
        float oldDelta = Time.captureDeltaTime;
        try
        {
            input.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            input.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>("BathhouseTestKeyboard");
            keyboard.MakeCurrent();
            Time.captureDeltaTime = 1f / 60f;
            for (int i = 0; i < 8; i++) yield return null;
            if (!direct)
            {
                var cycle = Object.FindAnyObjectByType<PoolWaterCycle>();
                var controller = cycle.player.GetComponent<CharacterController>();
                // Walk through the actual trigger in each invalid state, then during the open window.
                foreach (var expected in new[] { PoolWaterCycle.Phase.Pool, PoolWaterCycle.Phase.Empty, PoolWaterCycle.Phase.Flooded, PoolWaterCycle.Phase.Filling })
                {
                    if (expected == PoolWaterCycle.Phase.Empty)
                        Drain(cycle);
                    if (expected == PoolWaterCycle.Phase.Flooded || expected == PoolWaterCycle.Phase.Filling)
                    {
                        if (expected == PoolWaterCycle.Phase.Filling)
                        {
                            cycle.ResetSequence(true);
                            Drain(cycle);
                        }
                        Teleport(controller, cycle.letter.transform.position + Vector3.up * .8f);
                        Require(cycle.letter.TryPickUp(), "letter pickup starts the flood");
                        cycle.letter.CloseLetter();
                        if (expected == PoolWaterCycle.Phase.Flooded)
                            cycle.Advance(cycle.refillSeconds + cycle.platformFloodSeconds + cycle.hallFloodSeconds);
                    }
                    Require(cycle.CurrentPhase == expected, "starting gate state " + expected);
                    if (expected == PoolWaterCycle.Phase.Filling)
                    {
                        // Walk the real capsule out of the sunken pool via its southern steps and the east deck.
                        var waypoints = new[] { new Vector2(8f, .30f), new Vector2(12.5f, .30f),
                            new Vector2(12.5f, 44.5f), new Vector2(7.2f, 51.5f), new Vector2(7.2f, 59.1f) };
                        float speed = controller.GetComponent<PlayerMovement>().RunSpeed;
                        Time.captureDeltaTime = 1f / 30f;
                        foreach (Vector2 point in waypoints)
                        {
                            int steps = 0;
                            while (true)
                            {
                                var pos = controller.transform.position;
                                var delta = new Vector3(point.x - pos.x, 0, point.y - pos.z);
                                if (delta.magnitude < .12f) break;
                                if (++steps > 550) throw new InvalidOperationException("Blocked on escape route at " + pos + " toward " + point);
                                controller.Move(Vector3.ClampMagnitude(delta, speed * Time.deltaTime) + Vector3.down * .10f);
                                yield return null;
                            }
                        }
                        Require(controller.transform.position.y > .65f && cycle.CurrentPhase == expected,
                            "capsule climbs from the letter to the passage before the flood is full");
                        Time.captureDeltaTime = 1f / 60f;
                    }
                    else Teleport(controller, new Vector3(7.2f, 1.02f, 59.1f));
                    for (int i = 0; i < 6; i++) yield return null;
                    for (int i = 0; i < 65 && controller.transform.position.z < 61.9f; i++)
                    {
                        controller.Move(.065f * Vector3.forward);
                        yield return null;
                    }
                    Require(controller.transform.position.z > 61.6f, "player fits past the screen");
                    for (int i = 0; i < 42 && controller && controller.transform.position.x < 8.9f; i++)
                    {
                        controller.Move(.065f * Vector3.right);
                        yield return null;
                    }
                    for (int i = 0; i < 10; i++) yield return null;
                    if (expected != PoolWaterCycle.Phase.Filling)
                    {
                        Require(SceneManager.GetActiveScene().path == GirlPlayerIntegration.ScenePath && controller.transform.position.x > 8.65f,
                            "gate remains closed on arrival during " + expected);
                    }
                }
                while (SceneManager.GetActiveScene().path != BathhouseSceneAuthoring.ScenePath) yield return null;
                Require(!cycle, "first level unloaded");
            }

            for (int i = 0; i < 12; i++) yield return null;
            Require(SceneManager.GetActiveScene().path == BathhouseSceneAuthoring.ScenePath, "active bathhouse");
            var objects = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            Require(objects.All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0),
                "no missing scripts in the bathhouse");
            Require(objects.Select(t => t.GetComponent<MeshFilter>())
                .Count(filter => filter && filter.sharedMesh && filter.sharedMesh.name == "ShowerStation") == 11,
                "eleven complete shower stations");
            var character = Object.FindObjectsByType<PlayerMovement>().Single();
            var view = Camera.main;
            var girl = character.GetComponent<GirlPlayerAnimation>();
            Require(view && view.GetComponent<CameraFollow>() && girl && girl.animator && girl.animator.isHuman, "playable girl and follow camera");
            Require(!Object.FindAnyObjectByType<PoolWaterCycle>() && !GameObject.Find("GiantHand_Controls"), "pool objects unloaded");
            float startZ = character.transform.position.z;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            for (int i = 0; i < 140; i++) yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            for (int i = 0; i < 4; i++) yield return null;
            Require(character.transform.position.z > startZ + 2f && character.IsGrounded, "keyboard walks from entrance onto the tiled floor");
            Require(Physics.Raycast(new Vector3(3f, 1.5f, 5f), Vector3.right, out var wall, 3.2f) && wall.distance > 2f,
                "clear aisle and solid side wall");
            Require(!Physics.Raycast(new Vector3(1.4f, 1.3f, 8f), Vector3.forward, 3f), "open doorway to warm alcove");
            SaveCamera(view, "game");
            var shot = new GameObject("Temporary bathhouse composition camera").AddComponent<Camera>();
            shot.CopyFrom(view);
            shot.enabled = false;
            shot.transform.position = new Vector3(1.10f, 1.58f, .66f);
            shot.transform.LookAt(new Vector3(4.78f, 1.45f, 9.6f));
            shot.fieldOfView = 65f;
            // Move the player out of the architectural shot after capturing the real game camera.
            Teleport(character.GetComponent<CharacterController>(), new Vector3(1.6f, 1.03f, -.4f));
            SaveCamera(shot, "interior");
            shot.transform.position = new Vector3(4.52f, 1.55f, .64f);
            shot.transform.LookAt(new Vector3(5.92f, 1.43f, 2.70f));
            shot.fieldOfView = 59f;
            SaveCamera(shot, "shower");
            Object.Destroy(shot.gameObject);
            var body = character.GetComponent<CharacterController>();
            Teleport(body, new Vector3(1.52f, 1.02f, 8.8f));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            for (int i = 0; i < 160; i++) yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            float reachedZ = character.transform.position.z;
            Require(reachedZ > 10.4f && reachedZ < 11.7f && character.IsGrounded, "walk through the warm doorway and stop at the bench");
            var follow = view.GetComponent<CameraFollow>();
            Require(!Physics.OverlapSphere(view.transform.position, follow.CollisionRadius * .95f)
                .Any(follow.IsCameraObstacle), "third-person camera stays clear at the bench");
        }
        finally
        {
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
            Time.captureDeltaTime = oldDelta;
            input.editorInputBehaviorInPlayMode = oldEditorInput;
            input.backgroundBehavior = oldBackground;
        }
    }

    static void Drain(PoolWaterCycle cycle)
    {
        Require(cycle.TryInteract(), "drain switch");
        cycle.Advance(cycle.drainSeconds);
    }

    static void Teleport(CharacterController controller, Vector3 p)
    {
        controller.enabled = false;
        controller.transform.position = p;
        controller.enabled = true;
        Physics.SyncTransforms();
    }

    static void Require(bool ok, string label)
    {
        if (!ok) throw new InvalidOperationException("Bathhouse check failed: " + label);
        Debug.Log("Bathhouse check: " + label);
    }

    static void SaveCamera(Camera view, string name)
    {
        string folder = Path.Combine(Application.dataPath, "../Logs/Bathhouse");
        Directory.CreateDirectory(folder);
        var target = RenderTexture.GetTemporary(1440, 810, 24);
        var previous = RenderTexture.active;
        var previousTarget = view.targetTexture;
        var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        try
        {
            view.targetTexture = target;
            view.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), pixels.EncodeToPNG());
        }
        finally
        {
            view.targetTexture = previousTarget;
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            Object.DestroyImmediate(pixels);
        }
    }
}
