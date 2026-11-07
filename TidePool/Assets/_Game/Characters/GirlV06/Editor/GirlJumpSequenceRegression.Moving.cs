using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

// Uses real movement, Animator, keyboard input and temporary collision geometry.
public static partial class GirlJumpSequenceRegression
{
    [Serializable] public sealed class MovingResult
    {
        public string name, samplesPath, continuousFramesDirectory;
        public bool passed;
        public int launches, preparationSamples, recoverySamples, continuousFrameCount, blockedSamples, slidingSamples;
        public float minimumPreparationSpeed = 999f, takeoffSpeed = -1f, minimumRecoverySpeed = 999f;
        public float maximumSpeed, maximumRise, maximumForwardDistance, launchTime = -1f;
        public List<string> checks = new List<string>(), errors = new List<string>();
    }
    static readonly string[] MovingNames = {
        "moving-run-held", "moving-walk-held", "direction-and-space-same-frame",
        "opposing-keys-then-late-direction", "moving-release-and-resume",
        "moving-wall-block", "moving-wall-slide", "moving-off-ledge-before-takeoff", "reset-moving-then-stationary"
    };
    static MovingResult moving;
    static List<Sample> movingSamples;
    static GameObject movingWall;
    static int movingIndex, movingStartFrame;
    static uint movingInitialSequence;
    static Vector3 movingOrigin, movingForward, movingRight;
    static float movingPreviousSpeed, movingWallDistance;
    static bool movingSawAir, movingSawRecovery;

    [MenuItem("Tools/Girl V06/9 - Test moving jumps")]
    public static void BeginMovingTest() { Begin("moving"); }

    static void DestroyMovingWall()
    {
        if (movingWall) UnityEngine.Object.DestroyImmediate(movingWall);
        movingWall = null;
    }
    static void BeginMovingCase(int index)
    {
        DestroyMovingWall();
        if (index >= MovingNames.Length)
        {
            caseIndex = 4 + MovingNames.Length;
            if (SessionState.GetString(Mode, "test") == "moving") Finish(); else BeginScenario(0);
            return;
        }
        movingIndex = index; caseIndex = 4 + index;
        if (ceiling) { UnityEngine.Object.DestroyImmediate(ceiling); ceiling = null; }
        if (support) { UnityEngine.Object.DestroyImmediate(support); support = null; }
        current = null; samples = null; scenarioSamples = null;
        movement.ResetMotion(); Keys();
        float height = index == 7 ? 1.5f : 0f;
        Teleport(new Vector3(50f, height + .02f - (controller.center.y - controller.height * .5f), 0f));
        bridge.visualRoot.localRotation = visualRotation;
        view.transform.position = new Vector3(54f, 2.6f, -5.6f);
        view.transform.LookAt(new Vector3(50f, 1.25f, .9f));
        movingForward = Vector3.ProjectOnPlane(view.transform.forward, Vector3.up).normalized;
        movingRight = Vector3.Cross(Vector3.up, movingForward);
        if (index == 7)
        {
            support = GameObject.CreatePrimitive(PrimitiveType.Cube);
            support.name = "Temporary moving jump ledge"; support.hideFlags = HideFlags.DontSave;
            support.transform.position = new Vector3(50f, 1.25f, 0f);
            support.transform.localScale = new Vector3(10f, .5f, 10f);
        }
        Physics.SyncTransforms();
        moving = new MovingResult { name = MovingNames[index] };
        report.movingJumps.Add(moving); movingSamples = new List<Sample>();
        movingSawAir = movingSawRecovery = false; stage = 0; frames = 0; Save();
    }
    static void MovingKeys(bool space = false, bool freshStationary = false, float time = -1f)
    {
        var keys = new List<Key>();
        if (space) keys.Add(Key.Space);
        if (freshStationary || movingIndex == 3)
        {
            if (space && movingIndex == 3) keys.AddRange(new[] { Key.W, Key.S, Key.A, Key.D });
            // A later direction must not reclassify an already accepted stationary request.
            else if (time >= .10f && time < .50f) keys.Add(Key.W);
        }
        else if (!(movingIndex == 4 && time >= .15f && time < .35f))
        {
            keys.Add(Key.W);
            if (movingIndex != 1 && movingIndex != 2) keys.Add(Key.LeftShift);
            if (movingIndex == 6) keys.Add(Key.D);
        }
        Keys(keys.ToArray());
    }
    static void PressMovingJump(bool freshStationary = false)
    {
        movingStartFrame = Time.frameCount; movingInitialSequence = movement.JumpLaunchSequence;
        movingOrigin = movement.transform.position; scenarioBaseY = movingOrigin.y;
        movingPreviousSpeed = movement.PlanarVelocity.magnitude;
        previousLeftKnee = leftKnee.localRotation; previousRightKnee = rightKnee.localRotation;
        if (!freshStationary && (movingIndex == 5 || movingIndex == 6))
        {
            movingWallDistance = controller.radius + .8f;
            movingWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            movingWall.name = "Temporary moving jump wall"; movingWall.hideFlags = HideFlags.DontSave;
            Vector3 center = movingOrigin + movingForward * movingWallDistance;
            center.y = movingOrigin.y + controller.center.y + 2f;
            movingWall.transform.SetPositionAndRotation(center, Quaternion.LookRotation(movingForward));
            movingWall.transform.localScale = new Vector3(12f, 6f, .2f);
        }
        if (movingIndex == 7)
        {
            // Cross this edge before the existing anticipation can finish.
            Vector3 center = movingOrigin + movingForward * (.75f - 3f); center.y = 1.25f;
            support.transform.SetPositionAndRotation(center, Quaternion.LookRotation(movingForward));
            support.transform.localScale = new Vector3(8f, .5f, 6f);
        }
        Physics.SyncTransforms();
        if (movingIndex == 0)
        {
            moving.continuousFramesDirectory = Path.Combine(DirectoryPath, "moving-run-held-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(moving.continuousFramesDirectory);
        }
        MovingKeys(true, freshStationary); stage = freshStationary ? 4 : 2; frames = 0;
    }
    static void TickMoving()
    {
        if (stage == 0)
        {
            Keys(); if (frames < 35) return;
            MovingCheck(movement.IsGrounded && animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion") && !animator.IsInTransition(0),
                "Moving case starts grounded in stable Locomotion.");
            if (movingIndex == 2 || movingIndex == 3) PressMovingJump(); else { stage = 1; frames = 0; }
        }
        else if (stage == 1)
        {
            MovingKeys(); if (frames < 45) return;
            MovingCheck(movement.PlanarVelocity.magnitude > (movingIndex == 1 ? 1.4f : 3.4f), "Normal movement speed is reached before Space.");
            PressMovingJump();
        }
        else if (stage == 3)
        {
            Keys();
            MovingCheck(movement.JumpLaunchSequence == movingInitialSequence, "Reset cancels moving preparation without a delayed launch.");
            if (frames < 35) return;
            MovingCheck(movement.IsGrounded && !animator.GetBool("JumpActive"), "Reset releases the old jump before a fresh request.");
            PressMovingJump(true);
        }
        else ObserveMoving();
    }
    static void ObserveMoving()
    {
        float time = (Time.frameCount - movingStartFrame) * Step;
        bool freshStationary = stage == 4;
        Sample sample = ReadSample(time); movingSamples.Add(sample); MovingKeys(false, freshStationary, time);
        moving.maximumSpeed = Mathf.Max(moving.maximumSpeed, sample.planarSpeed);
        moving.maximumRise = Mathf.Max(moving.maximumRise, sample.rise);
        float forwardDistance = Vector3.Dot(sample.position - movingOrigin, movingForward);
        moving.maximumForwardDistance = Mathf.Max(moving.maximumForwardDistance, forwardDistance);
        bool stationary = movingIndex == 3 || freshStationary;
        if (time > .06f && time < .60f && sample.grounded)
        {
            moving.preparationSamples++;
            moving.minimumPreparationSpeed = Mathf.Min(moving.minimumPreparationSpeed, sample.planarSpeed);
            if (stationary) MovingCheck(sample.planarSpeed < .02f && Vector3.ProjectOnPlane(sample.position - movingOrigin, Vector3.up).magnitude < .02f,
                "Stationary request stays planted despite a later direction press.");
            else if (movingIndex == 0 || movingIndex == 1) MovingCheck(sample.planarSpeed > (movingIndex == 0 ? 3.3f : 1.2f),
                "Held movement stays continuous during anticipation instead of being forced to zero.");
            else if (movingIndex == 2 && time > .30f) MovingCheck(sample.planarSpeed > 1.2f && sample.planarSpeed < 1.7f,
                "Simultaneous direction and Space accelerates from rest at walking speed.");
        }
        if (movingIndex == 4 && time > .06f && time < .60f) MovingCheck(Mathf.Abs(sample.planarSpeed - movingPreviousSpeed) < .24f,
            "Release and resume use normal acceleration without an instant speed clamp.");
        movingPreviousSpeed = sample.planarSpeed;
        if (movement.JumpLaunchSequence > movingInitialSequence && moving.launchTime < 0f)
        {
            moving.launchTime = time; moving.takeoffSpeed = sample.planarSpeed;
            if (movingIndex == 0 || movingIndex == 1) MovingCheck(sample.planarSpeed > (movingIndex == 0 ? 3.3f : 1.2f), "Physical takeoff preserves horizontal speed.");
        }
        if (!sample.grounded) movingSawAir = true;
        if (sample.jumpLanding)
        {
            movingSawRecovery = true; moving.recoverySamples++;
            moving.minimumRecoverySpeed = Mathf.Min(moving.minimumRecoverySpeed, sample.planarSpeed);
            if (movingIndex == 0 || movingIndex == 1) MovingCheck(sample.planarSpeed > (movingIndex == 0 ? 3.3f : 1.2f), "Landing recovery preserves held movement.");
        }
        if (movingIndex == 5 || movingIndex == 6)
        {
            MovingCheck(forwardDistance <= movingWallDistance - .1f - controller.radius + .08f, "The character does not cross the wall.");
            if (time > .5f)
            {
                float normalSpeed = Mathf.Abs(Vector3.Dot(movement.PlanarVelocity, movingForward));
                if (normalSpeed < .1f) moving.blockedSamples++;
                if (movingIndex == 6 && normalSpeed < .1f && Vector3.Dot(movement.PlanarVelocity, movingRight) > 1.5f) moving.slidingSamples++;
            }
        }
        if (movingIndex == 7)
        {
            MovingCheck(movement.JumpLaunchSequence == movingInitialSequence, "Leaving support during preparation never creates an airborne launch.");
            MovingCheck(sample.rise < .06f, "Support loss falls rather than creating a replacement jump.");
        }
        if (movingIndex == 8 && !freshStationary && time >= .20f)
        {
            movement.ResetMotion(); Keys(); stage = 3; frames = 0; return;
        }
        if (movingIndex == 0 && (Time.frameCount - movingStartFrame) % 2 == 0)
        {
            Vector3 center = controller.bounds.center;
            view.transform.position = center + new Vector3(4f, 2.6f, -6.5f);
            view.transform.LookAt(center);
            RenderPng(Path.Combine(moving.continuousFramesDirectory, "frame-" + moving.continuousFrameCount.ToString("D4") + ".png"), 768, 480);
            moving.continuousFrameCount++;
        }
        if (time > .9f && sample.grounded && !sample.jumpActive && sample.state == "Locomotion" && !sample.transitioning)
        {
            moving.launches = (int)(movement.JumpLaunchSequence - movingInitialSequence);
            MovingCheck(moving.launches == (movingIndex == 7 ? 0 : 1), "Exactly the expected number of physical launches occurs.");
            MovingCheck(movingSawAir && movingSawRecovery && moving.recoverySamples >= 10, "Motion includes flight or falling, contact, and full recovery.");
            if (movingIndex != 7) MovingCheck(moving.maximumRise > .95f && moving.maximumRise < 1.35f, "Horizontal movement preserves jump height.");
            if (movingIndex == 5 || movingIndex == 6) MovingCheck(moving.blockedSamples >= 10, "The wall actually blocks normal movement.");
            if (movingIndex == 6) MovingCheck(moving.slidingSamples >= 10, "Diagonal movement slides tangentially along the wall.");
            moving.passed = moving.errors.Count == 0;
            SaveMovingSamples(); Save(); BeginMovingCase(movingIndex + 1);
        }
        else if (time > 4f) MovingCheck(false, "Moving sequence returns to Locomotion within four seconds.");
    }
    static void MovingCheck(bool condition, string message)
    {
        if (condition) { if (!moving.checks.Contains(message)) moving.checks.Add(message); return; }
        if (!moving.errors.Contains(message)) moving.errors.Add(message);
        if (!captureOnly) throw new InvalidOperationException(message);
    }
    static void SaveMovingSamples()
    {
        if (moving == null || movingSamples == null) return;
        moving.samplesPath = Path.Combine(DirectoryPath, moving.name + "-samples.json");
        File.WriteAllText(moving.samplesPath, JsonUtility.ToJson(new SampleFile { name = moving.name, samples = movingSamples }, true));
    }
}
