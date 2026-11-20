using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
public static class PoolWaterCycleRegression
{
    const string Running = "TidePool.WaterRegression.Running";
    const string ExitCode = "TidePool.WaterRegression.ExitCode";
    static IEnumerator<KeyboardState> sequence;
    static Keyboard keyboard;
    static int lastFrame;
    static double deadline;

    static PoolWaterCycleRegression()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Start the water regression in a separate batch editor.");
        EditorSceneManager.OpenScene(GirlPlayerIntegration.ScenePath);
        SessionState.SetInt(ExitCode, 1);
        SessionState.SetBool(Running, true);
        EditorApplication.EnterPlaymode();
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Running, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            lastFrame = -1;
            deadline = EditorApplication.timeSinceStartup + 120;
            sequence = CheckSequence();
            EditorApplication.update += Tick;
        }
        else if (state == PlayModeStateChange.ExitingPlayMode)
        {
            EditorApplication.update -= Tick;
            sequence?.Dispose();
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.EraseBool(Running);
            int result = SessionState.GetInt(ExitCode, 1);
            if (result == 0) Debug.Log("TidePool water regression passed");
            EditorApplication.Exit(result);
        }
    }

    static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline)
                throw new TimeoutException("Timed out while testing the ArmLab water sequence.");
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            if (sequence.MoveNext())
            {
                InputSystem.QueueStateEvent(keyboard, sequence.Current);
                return;
            }
            SessionState.SetInt(ExitCode, 0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
        }
        EditorApplication.update -= Tick;
        EditorApplication.ExitPlaymode();
    }

    // Each yield queues one keyboard state for the next real Play frame.
    static IEnumerator<KeyboardState> CheckSequence()
    {
        var settings = InputSystem.settings;
        var oldBackground = settings.backgroundBehavior;
        var oldEditorInput = settings.editorInputBehaviorInPlayMode;
        var previousKeyboard = Keyboard.current;
        float oldFrameTime = Time.captureDeltaTime;
        try
        {
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>("WaterRegressionKeyboard");
            keyboard.MakeCurrent();
            Time.captureDeltaTime = 1f / 60f;
            yield return default;

            var cycle = UnityEngine.Object.FindAnyObjectByType<PoolWaterCycle>();
            Require(cycle && cycle.isActiveAndEnabled && cycle.waterFrame && cycle.player && cycle.letter, "ArmLab water references");
            var letter = cycle.letter;
            var movement = cycle.player.GetComponent<PlayerMovement>();
            var controller = cycle.player.GetComponent<CharacterController>();
            Require(letter.isActiveAndEnabled && letter.cycle == cycle && movement && movement.enabled && controller && controller.enabled,
                "Enabled player and letter bound to the active water cycle");
            Vector3 spawn = cycle.player.position;
            Require(cycle.CurrentPhase == PoolWaterCycle.Phase.Pool && Mathf.Approximately(cycle.WaterHeight, cycle.poolHeight)
                && !letter.PickedUp && !letter.IsRevealed, "Initial pool height and hidden letter");

            Teleport(controller, cycle.transform.position + Vector3.back * (cycle.interactionDistance * .5f) + Vector3.up * .5f);
            Require(cycle.PlayerInRange(), "Player within drain switch range");
            yield return new KeyboardState(Key.E);
            yield return default;
            Require(cycle.CurrentPhase == PoolWaterCycle.Phase.Draining, "E at the switch starts draining");
            cycle.Advance(cycle.drainSeconds * .5f);
            Require(cycle.emptyHeight < cycle.WaterHeight && cycle.WaterHeight < cycle.poolHeight, "Water recedes during drainage");
            cycle.Advance(cycle.drainSeconds);
            Require(cycle.CurrentPhase == PoolWaterCycle.Phase.Empty && Mathf.Approximately(cycle.WaterHeight, cycle.emptyHeight)
                && letter.IsRevealed, "Empty pool reveals the letter");

            Teleport(controller, letter.transform.position + Vector3.up * .8f);
            Require(letter.IsInRange, "Player within pickup range");
            yield return new KeyboardState(Key.E);
            yield return default;
            Require(cycle.CurrentPhase == PoolWaterCycle.Phase.Filling && letter.PickedUp && letter.IsReading && !movement.enabled,
                "E picks up the letter, starts flooding and pauses movement");
            float readingHeight = cycle.WaterHeight;
            for (int frame = 0; frame < 5; frame++) yield return default;
            Require(letter.IsReading && cycle.WaterHeight > readingHeight, "Water rises during live reading frames");
            yield return new KeyboardState(Key.Escape);
            yield return default;
            Require(!letter.IsReading && movement.enabled, "Closing the letter restores movement");

            cycle.Advance(cycle.refillSeconds + cycle.platformFloodSeconds + cycle.hallFloodSeconds);
            Require(cycle.CurrentPhase == PoolWaterCycle.Phase.Flooded && Mathf.Approximately(cycle.WaterHeight, cycle.floodedHeight),
                "Hall reaches its configured flood height");
            yield return new KeyboardState(Key.R);
            yield return default;
            Require(cycle.CurrentPhase == PoolWaterCycle.Phase.Pool && Mathf.Approximately(cycle.WaterHeight, cycle.poolHeight)
                && !letter.PickedUp && !letter.IsRevealed && !letter.IsReading, "R restores the pool and resets the letter");
            Require(Vector3.Distance(spawn, cycle.player.position) < .1f && movement.enabled, "R restores the player to spawn");
        }
        finally
        {
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
            Time.captureDeltaTime = oldFrameTime;
            settings.editorInputBehaviorInPlayMode = oldEditorInput;
            settings.backgroundBehavior = oldBackground;
        }
    }

    static void Teleport(CharacterController controller, Vector3 position)
    {
        controller.enabled = false;
        controller.transform.position = position;
        controller.enabled = true;
        Physics.SyncTransforms();
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Water regression failed: " + message);
    }
}
