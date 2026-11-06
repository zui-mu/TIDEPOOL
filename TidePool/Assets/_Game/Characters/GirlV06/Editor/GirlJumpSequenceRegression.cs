using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

// Runs the real movement/animation pair, not an animation-only preview. No scene or asset is saved.
// Write "test" (fail fast) or "capture" (record every case, including failures) to RequestPath.
[InitializeOnLoad]
public static class GirlJumpSequenceRegression
{
    const string Running = "GirlJumpSequenceRegression.Running";
    const string Mode = "GirlJumpSequenceRegression.Mode";
    const string SettingsSaved = "GirlJumpSequenceRegression.SettingsSaved";
    const string OldBackground = "GirlJumpSequenceRegression.OldBackground";
    const string OldEditorInput = "GirlJumpSequenceRegression.OldEditorInput";
    const string OldCapture = "GirlJumpSequenceRegression.OldCapture";
    const string OldRunBackground = "GirlJumpSequenceRegression.OldRunBackground";
    const string QuitAfter = "GirlJumpSequenceRegression.QuitAfter";
    const float Step = 1f / 60f;
    const float SourceEndFrame = 50f;

    [Serializable] public sealed class Sample
    {
        public int frame;
        public uint launchSequence;
        public float time, rise, verticalSpeed, planarSpeed, sourceFrame, landingFrame, kneeDelta;
        public Vector3 position;
        public bool grounded, jumpActive, jumpLanding, fallOnly, transitioning;
        public string state;
    }
    [Serializable] public sealed class JumpResult
    {
        public string name, samplesPath, sourceClipName;
        public bool passed, ceiling;
        public int launches, recoveryProgressSamples;
        public float launchTime = -1f, apexTime, apexRise, apexSourceFrame;
        public float contactTime = -1f, contactSourceFrame = -1f;
        public float incomingContactFrame = -1f, contactKneeDelta;
        public bool contactJumpLanding;
        public float recoveryStartSourceFrame = -1f, recoveryEndSourceFrame, locomotionTime = -1f, earlyKneeDelta;
        public float sourceClipLength;
        public string continuousFramesDirectory;
        public int continuousFrameCount;
        public List<string> checks = new List<string>();
        public List<string> errors = new List<string>();
        public List<string> captures = new List<string>();
    }
    [Serializable] public sealed class ScenarioResult
    {
        public string name, samplesPath;
        public bool passed;
        public uint initialLaunchSequence, launchSequenceAtAction;
        public float actionTime, firstLaunchTime = -1f, maxRise, contactTime = -1f;
        public float fallbackLaunchDelay = -1f;
        public float incomingContactFrame = -1f, lastFallFrame = -1f, recoveryEndFrame;
        public float maxKneeDelta, fallDepth, locomotionTime = -1f;
        public int fallingSamples, recoverySamples;
        public List<string> checks = new List<string>(), errors = new List<string>();
    }
    [Serializable] public sealed class Report
    {
        public string date, scene, mode, status, detailDirectory;
        public bool passed;
        public float captureDeltaTime;
        public List<string> checks = new List<string>();
        public List<string> errors = new List<string>();
        public List<JumpResult> jumps = new List<JumpResult>();
        public List<ScenarioResult> scenarios = new List<ScenarioResult>();
    }
    [Serializable] sealed class SampleFile
    {
        public string name;
        public List<Sample> samples;
    }

    static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    public static string DirectoryPath => Path.Combine(ProjectRoot, "Library", "JumpSequenceVerification");
    public static string RequestDirectoryPath => Path.Combine(ProjectRoot, "Temp", "JumpSequenceVerification");
    public static string RequestPath => Path.Combine(RequestDirectoryPath, "request.txt");
    public static string ReportPath => Path.Combine(GirlPlayerIntegration.Out, "jump-sequence-regression.json");
    static Report report;
    static PlayerMovement movement;
    static CharacterController controller;
    static GirlPlayerAnimation bridge;
    static Animator animator;
    static Keyboard keyboard, previousKeyboard;
    static GameObject floor, ceiling, support;
    static PoolWaterCycle cycle;
    static Camera view;
    static CameraFollow follow;
    static DryRegionCameraGuard guard;
    static Vector3 originalPosition, cameraPosition, cameraScale;
    static Quaternion originalRotation, visualRotation, cameraRotation;
    static bool cameraEnabled, followEnabled, guardEnabled;
    static bool movementEnabled, animatorEnabled, bridgeEnabled;
    static float cameraFieldOfView;
    static RenderTexture cameraTarget;
    static Transform leftKnee, rightKnee;
    static Quaternion previousLeftKnee, previousRightKnee;
    static bool hasJumpTime, hasLandingTime, hasFallTime, hasJumpActive, hasJumpLanding, hasFallOnly;
    static bool initialized, captureOnly, baselineChecked, wasGrounded, wasAirborne;
    static bool sawRecovery, savedAnticipation, savedTakeoff, savedApex, savedContact, savedRecovery;
    static int caseIndex, stage, frames, lastFrame, jumpStartFrame;
    static float baseY, previousSourceFrame, previousVerticalSpeed;
    static double deadline;
    static JumpResult current;
    static List<Sample> samples;
    static readonly string[] ScenarioNames = {
        "water-reset-during-preparation", "water-reset-during-flight", "movement-disabled-during-preparation",
        "animator-disabled-immediate-fallback", "ledge-fall-from-locomotion", "ledge-fall-during-preparation", "ledge-fall-during-recovery",
        "movement-reenabled-with-space-same-frame", "animator-disabled-after-launch-queued", "fully-blocked-headroom"
    };
    static ScenarioResult scenario;
    static List<Sample> scenarioSamples;
    static int scenarioIndex, scenarioStage, scenarioStartFrame, actionFrame, freshStartFrame;
    static uint actionLaunchSequence, freshLaunchSequence;
    static Vector3 actionPosition;
    static float scenarioBaseY, frozenOutgoingClock;
    static bool sawFalling, sawFallAir, checkedFreshPreparation, capturedFrozenClock;

    static GirlJumpSequenceRegression()
    {
        EditorApplication.update -= Poll;
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged -= Changed;
        EditorApplication.playModeStateChanged += Changed;
    }

    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(RequestPath)) return;
        string command;
        try
        {
            command = File.ReadAllText(RequestPath).Trim().ToLowerInvariant();
            if (command.Length == 0) return;
            File.Delete(RequestPath);
        }
        catch (IOException) { return; }
        try
        {
            Require(command == "test" || command == "capture" || command == "refresh", "Expected request 'test', 'capture' or 'refresh', got: " + command);
            if (command == "refresh")
            {
                Require(!EditorApplication.isPlayingOrWillChangePlaymode && !SessionState.GetBool(Running, false),
                    "Finish the Play Mode test before requesting an asset refresh.");
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                return;
            }
            Begin(command);
        }
        catch (Exception error)
        {
            Directory.CreateDirectory(RequestDirectoryPath);
            File.WriteAllText(Path.Combine(RequestDirectoryPath, "request-error.txt"), error.ToString());
        }
    }

    [MenuItem("Tools/Girl V06/7 - Test full jump sequence")]
    public static void BeginTest() { Begin("test"); }

    [MenuItem("Tools/Girl V06/8 - Capture full jump sequence")]
    public static void BeginCapture() { Begin("capture"); }

    // Unity -batchmode -executeMethod GirlJumpSequenceRegression.RunBatch -logFile <path>
    // Do not pass -quit: the editor must remain alive through the asynchronous Play Mode run.
    public static void RunBatch()
    {
        Require(Application.isBatchMode, "RunBatch is restricted to a dedicated Unity batch process.");
        SessionState.SetBool(QuitAfter, true);
        try
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Batch setup requires Edit Mode.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                Require(!SceneManager.GetSceneAt(i).isDirty, "Batch setup preserves unsaved open scenes.");
            if (SceneManager.GetActiveScene().path != GirlPlayerIntegration.ScenePath)
                EditorSceneManager.OpenScene(GirlPlayerIntegration.ScenePath, OpenSceneMode.Single);
            Begin("test");
        }
        catch (Exception error)
        {
            report = new Report
            {
                date = DateTime.Now.ToString("s"), scene = SceneManager.GetActiveScene().path,
                mode = "test", status = "failed", passed = false, detailDirectory = DirectoryPath
            };
            report.errors.Add("Batch setup: " + error);
            Save();
            EditorApplication.delayCall += () => EditorApplication.Exit(2);
        }
    }

    static void Begin(string mode)
    {
        if (!Application.isBatchMode) SessionState.SetBool(QuitAfter, false);
        Require(!SessionState.GetBool(Running, false) && !EditorApplication.isPlayingOrWillChangePlaymode,
            "Exit Play Mode before running the jump regression.");
        Require(SceneManager.GetActiveScene().path == GirlPlayerIntegration.ScenePath,
            "Open the integrated ArmLab scene before running the jump regression.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            Require(!SceneManager.GetSceneAt(i).isDirty, "Save all open scenes before running the jump regression.");
        Directory.CreateDirectory(DirectoryPath);
        Directory.CreateDirectory(RequestDirectoryPath);
        string errorPath = Path.Combine(RequestDirectoryPath, "request-error.txt");
        if (File.Exists(errorPath)) File.Delete(errorPath);
        SessionState.SetString(Mode, mode);
        SessionState.SetBool(Running, true);
        EditorApplication.isPlaying = true;
    }

    static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Running, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            try { Initialize(); }
            catch (Exception error) { Fail(error); }
        }
        if (state == PlayModeStateChange.ExitingPlayMode) Cleanup();
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            RestoreSettings();
            SessionState.SetBool(Running, false);
            // Play Mode domain reload may discard the static report before this callback.
            if (report == null && File.Exists(ReportPath))
                report = JsonUtility.FromJson<Report>(File.ReadAllText(ReportPath));
            if (report != null)
            {
                if (report.status == "running")
                {
                    report.errors.Add("Play Mode ended before all jump cases completed.");
                    report.passed = false;
                }
                report.status = report.passed ? "passed" : "failed";
                Save();
            }
            if (Application.isBatchMode && SessionState.GetBool(QuitAfter, false))
            {
                int exitCode = report != null && report.passed ? 0 : 2;
                SessionState.SetBool(QuitAfter, false);
                EditorApplication.delayCall += () => EditorApplication.Exit(exitCode);
            }
        }
    }

    static void Initialize()
    {
        captureOnly = SessionState.GetString(Mode, "test") == "capture";
        report = new Report
        {
            date = DateTime.Now.ToString("s"), scene = SceneManager.GetActiveScene().path,
            mode = captureOnly ? "capture" : "test", status = "running",
            detailDirectory = DirectoryPath, captureDeltaTime = Step
        };
        var players = UnityEngine.Object.FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        Require(players.Length == 1, "Expected one integrated PlayerMovement in ArmLab.");
        movement = players[0]; controller = movement.GetComponent<CharacterController>();
        bridge = movement.GetComponent<GirlPlayerAnimation>();
        Require(controller && bridge && bridge.animator && bridge.visualRoot, "Integrated movement and animation references must exist.");
        animator = bridge.animator;
        movementEnabled = movement.enabled; animatorEnabled = animator.enabled; bridgeEnabled = bridge.enabled;
        Require(animator.isHuman && !animator.applyRootMotion, "Jump regression requires the real Humanoid Animator with code-controlled displacement.");
        originalPosition = movement.transform.position; originalRotation = movement.transform.rotation;
        visualRotation = bridge.visualRoot.localRotation;
        view = Camera.main;
        Require(view, "Main camera missing.");
        cameraPosition = view.transform.position; cameraRotation = view.transform.rotation; cameraScale = view.transform.localScale;
        cameraEnabled = view.enabled; cameraTarget = view.targetTexture;
        cameraFieldOfView = view.fieldOfView;
        initialized = true; // Cleanup must also restore the camera if later setup fails.
        follow = view.GetComponent<CameraFollow>(); guard = view.GetComponent<DryRegionCameraGuard>();
        if (follow) { followEnabled = follow.enabled; follow.enabled = false; }
        if (guard) { guardEnabled = guard.enabled; guard.enabled = false; }
        view.enabled = true;
        view.fieldOfView = 30f;
        // One fixed three-quarter camera for every event, so pose changes remain comparable.
        view.transform.position = new Vector3(54f, 2.6f, -5.6f);
        view.transform.LookAt(new Vector3(50f, 1.25f, .9f));

        // Change these two properties in place. Replacing InputSystem.settings with a temporary
        // HideAndDontSave asset breaks Input System's Play Mode restoration/disposal lifecycle.
        SessionState.SetInt(OldBackground, (int)InputSystem.settings.backgroundBehavior);
        SessionState.SetInt(OldEditorInput, (int)InputSystem.settings.editorInputBehaviorInPlayMode);
        SessionState.SetFloat(OldCapture, Time.captureDeltaTime);
        SessionState.SetBool(OldRunBackground, Application.runInBackground);
        SessionState.SetBool(SettingsSaved, true);
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        Time.captureDeltaTime = Step; Application.runInBackground = true;
        previousKeyboard = Keyboard.current;
        keyboard = InputSystem.AddDevice<Keyboard>("GirlJumpSequenceRegressionKeyboard");
        InputSystem.EnableDevice(keyboard); keyboard.MakeCurrent();

        floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Temporary full jump regression floor"; floor.hideFlags = HideFlags.DontSave;
        floor.transform.position = new Vector3(50f, -.25f, 12f);
        floor.transform.localScale = new Vector3(24f, .5f, 48f);
        hasJumpTime = HasParameter("JumpTime", AnimatorControllerParameterType.Float);
        hasLandingTime = HasParameter("LandingTime", AnimatorControllerParameterType.Float);
        hasFallTime = HasParameter("FallTime", AnimatorControllerParameterType.Float);
        hasJumpActive = HasParameter("JumpActive", AnimatorControllerParameterType.Bool);
        hasJumpLanding = HasParameter("JumpLanding", AnimatorControllerParameterType.Bool);
        hasFallOnly = HasParameter("FallOnly", AnimatorControllerParameterType.Bool);
        cycle = UnityEngine.Object.FindFirstObjectByType<PoolWaterCycle>();
        leftKnee = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        rightKnee = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
        Require(leftKnee && rightKnee, "Humanoid knee bones missing.");
        initialized = true; caseIndex = 0; lastFrame = -1;
        deadline = EditorApplication.timeSinceStartup + 420;
        Application.logMessageReceived += Log;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        ResetCase(); Save();
    }

    static bool HasParameter(string name, AnimatorControllerParameterType type)
    {
        return animator.parameters.Any(p => p.name == name && p.type == type);
    }

    static void ResetCase()
    {
        Keys();
        if (ceiling) { UnityEngine.Object.DestroyImmediate(ceiling); ceiling = null; }
        // Disable/enable only runtime components to clear movement velocity between independent cases.
        movement.enabled = false;
        Teleport(new Vector3(50f, .02f - (controller.center.y - controller.height * .5f), 0f));
        movement.transform.rotation = originalRotation;
        bridge.visualRoot.localRotation = visualRotation;
        movement.enabled = true;
        stage = 0; frames = 0; current = null; samples = null;
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || !initialized) return;
        if (EditorApplication.timeSinceStartup > deadline) { Fail(new TimeoutException("Jump regression timed out at case " + caseIndex + ", stage " + stage)); return; }
        if (Time.frameCount == lastFrame) { EditorApplication.QueuePlayerLoopUpdate(); return; }
        lastFrame = Time.frameCount; frames++;
        try
        {
            // The editor may disable or switch the current device when focus changes.
            if (!keyboard.enabled) InputSystem.EnableDevice(keyboard);
            keyboard.MakeCurrent();
            if (caseIndex >= 4) { TickScenario(); return; }
            if (stage == 0)
            {
                Keys();
                if (frames < 30) return;
                Require(movement.IsGrounded, "Player did not settle on the isolated temporary floor.");
                if (caseIndex == 3)
                {
                    ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    ceiling.name = "Temporary early contact regression ceiling"; ceiling.hideFlags = HideFlags.DontSave;
                    float head = movement.transform.position.y + controller.center.y + controller.height * .5f;
                    ceiling.transform.position = new Vector3(50f, head + .45f, 4f);
                    ceiling.transform.localScale = new Vector3(8f, .2f, 12f);
                    Physics.SyncTransforms();
                }
                stage = caseIndex == 2 ? 1 : 2; frames = 0;
            }
            else if (stage == 1)
            {
                Keys(Key.W, Key.LeftShift);
                if (frames < 45) return;
                Require(movement.PlanarVelocity.magnitude > 3f, "Running case must reach running speed before Space.");
                stage = 2; frames = 0;
            }
            else if (stage == 2) StartJump();
            else if (stage == 3) ObserveJump();
            else if (stage == 4)
            {
                Keys();
                if (frames < 30) return;
                caseIndex++;
                if (caseIndex < 4) ResetCase();
                else BeginScenario(0);
            }
        }
        catch (Exception error) { Fail(error); }
        finally { EditorApplication.QueuePlayerLoopUpdate(); }
    }

    static void StartJump()
    {
        current = new JumpResult
        {
            name = new[] { "stationary-first", "stationary-repeat", "running-w-shift", "low-ceiling-early-contact" }[caseIndex],
            ceiling = caseIndex == 3
        };
        report.jumps.Add(current); samples = new List<Sample>();
        baseY = movement.transform.position.y; jumpStartFrame = Time.frameCount;
        wasGrounded = movement.IsGrounded; wasAirborne = false; previousSourceFrame = -1f;
        previousVerticalSpeed = movement.VerticalSpeed;
        previousLeftKnee = leftKnee.localRotation; previousRightKnee = rightKnee.localRotation;
        baselineChecked = sawRecovery = savedAnticipation = savedTakeoff = savedApex = savedContact = savedRecovery = false;
        if (caseIndex == 0)
        {
            current.continuousFramesDirectory = Path.Combine(DirectoryPath, "stationary-first-frames");
            Directory.CreateDirectory(current.continuousFramesDirectory);
            foreach (string path in Directory.EnumerateFiles(current.continuousFramesDirectory, "frame-*.png")) File.Delete(path);
            File.WriteAllText(Path.Combine(current.continuousFramesDirectory, "frame-times.csv"), "imageIndex,runtimeFrame,secondsAfterSpace\n");
            CaptureContinuousFrame(0);
            File.WriteAllText(Path.Combine(current.continuousFramesDirectory, "timing.txt"), "frameRate=30\nsecondsPerFrame=0.0333333333\nFirst frame is the idle pose immediately before queued Space.\n");
        }
        if (caseIndex == 2) Keys(Key.W, Key.LeftShift, Key.Space); else Keys(Key.Space);
        stage = 3; frames = 0; Save();
    }

    static Sample ReadSample(float time)
    {
        var state = animator.GetCurrentAnimatorStateInfo(0);
        bool transitioning = animator.IsInTransition(0);
        if (transitioning && state.IsName("Locomotion")) state = animator.GetNextAnimatorStateInfo(0);
        string stateName = state.IsName("AirborneHold") ? "AirborneHold" : state.IsName("LandingRecovery") ? "LandingRecovery" : state.IsName("Falling") ? "Falling" : state.IsName("Locomotion") ? "Locomotion" : "Other";
        float source = state.normalizedTime * SourceEndFrame;
        if (stateName == "AirborneHold" && hasJumpTime) source = animator.GetFloat("JumpTime") * SourceEndFrame;
        if (stateName == "LandingRecovery" && hasLandingTime) source = animator.GetFloat("LandingTime") * SourceEndFrame;
        if (stateName == "Falling" && hasFallTime) source = animator.GetFloat("FallTime") * SourceEndFrame;
        float knee = Mathf.Max(Quaternion.Angle(previousLeftKnee, leftKnee.localRotation), Quaternion.Angle(previousRightKnee, rightKnee.localRotation));
        previousLeftKnee = leftKnee.localRotation; previousRightKnee = rightKnee.localRotation;
        return new Sample
        {
            frame = Time.frameCount, launchSequence = movement.JumpLaunchSequence, time = time, position = movement.transform.position,
            rise = movement.transform.position.y - (caseIndex >= 4 ? scenarioBaseY : baseY), verticalSpeed = movement.VerticalSpeed,
            planarSpeed = movement.PlanarVelocity.magnitude, grounded = movement.IsGrounded,
            state = stateName, sourceFrame = source, kneeDelta = knee, transitioning = transitioning,
            landingFrame = hasLandingTime ? animator.GetFloat("LandingTime") * SourceEndFrame : -1f,
            jumpActive = hasJumpActive && animator.GetBool("JumpActive"),
            jumpLanding = hasJumpLanding && animator.GetBool("JumpLanding"),
            fallOnly = hasFallOnly && animator.GetBool("FallOnly")
        };
    }

    static void ObserveJump()
    {
        if (caseIndex == 2) Keys(Key.W, Key.LeftShift); else Keys();
        float elapsed = (Time.frameCount - jumpStartFrame) * Step;
        Sample sample = ReadSample(elapsed); samples.Add(sample);
        if (caseIndex == 0 && (Time.frameCount - jumpStartFrame) % 2 == 0) CaptureContinuousFrame(Time.frameCount - jumpStartFrame);
        if (elapsed <= .12f) current.earlyKneeDelta = Mathf.Max(current.earlyKneeDelta, sample.kneeDelta);

        // The old immediate-impulse code fails here, before other expectations can obscure why.
        // A jump must first show the grounded 0..19 source-frame preparation.
        if (!baselineChecked && elapsed >= .30f)
        {
            baselineChecked = true;
            Capture("anticipation", sample); savedAnticipation = true;
            Check(sample.grounded && sample.rise < .03f,
                "Anticipation holds the collider grounded at 0.30s after Space (observed grounded=" + sample.grounded + ", rise=" + sample.rise.ToString("F3") + "m). Immediate launch skips source frames 0..19.");
        }
        if (wasGrounded && !sample.grounded && sample.verticalSpeed > .2f)
        {
            current.launches++;
            if (current.launchTime < 0f) current.launchTime = elapsed;
            if (!savedTakeoff) { Capture("takeoff", sample); savedTakeoff = true; }
        }
        if (!sample.grounded) wasAirborne = true;
        if (sample.rise > current.apexRise)
        {
            current.apexRise = sample.rise; current.apexTime = elapsed; current.apexSourceFrame = sample.sourceFrame;
        }
        if (wasAirborne && previousVerticalSpeed > 0f && sample.verticalSpeed <= 0f && !savedApex)
        {
            Capture("apex", sample); savedApex = true;
        }
        if (wasAirborne && sample.grounded && current.contactTime < 0f)
        {
            current.contactTime = elapsed; current.contactSourceFrame = sample.sourceFrame;
            current.incomingContactFrame = sample.landingFrame; current.contactJumpLanding = sample.jumpLanding;
            Capture("first-contact", sample); savedContact = true;
        }
        if (current.contactTime >= 0f && elapsed <= current.contactTime + .3f)
        {
            current.contactKneeDelta = Mathf.Max(current.contactKneeDelta, sample.kneeDelta);
            if (sample.state == "Falling" || sample.state == "AirborneHold")
                Check(Mathf.Abs(sample.sourceFrame - current.contactSourceFrame) < .05f,
                    "The actual outgoing airborne pose stays frozen while the separate landing clock blends in.");
        }
        bool jumpState = sample.state == "AirborneHold" || sample.state == "LandingRecovery" || sample.state == "Falling";
        if (jumpState)
        {
            var clips = animator.GetCurrentAnimatorClipInfo(0);
            if (animator.IsInTransition(0)) clips = clips.Concat(animator.GetNextAnimatorClipInfo(0)).ToArray();
            var jumpClip = clips.Where(c => c.clip && c.clip.name.Contains("Jump")).OrderByDescending(c => c.weight).FirstOrDefault();
            if (jumpClip.clip)
            {
                current.sourceClipName = jumpClip.clip.name; current.sourceClipLength = jumpClip.clip.length;
            }
            if (previousSourceFrame >= 0f)
                Check(sample.sourceFrame + .8f >= previousSourceFrame,
                    "Full source time stays monotonic during the jump (" + previousSourceFrame.ToString("F2") + " -> " + sample.sourceFrame.ToString("F2") + ").");
            previousSourceFrame = sample.sourceFrame;
        }
        if (sample.state == "LandingRecovery")
        {
            if (!sawRecovery) current.recoveryStartSourceFrame = sample.sourceFrame;
            sawRecovery = true;
            current.recoveryEndSourceFrame = Mathf.Max(current.recoveryEndSourceFrame, sample.sourceFrame);
            if (sample.sourceFrame > 31f && sample.sourceFrame < 49f) current.recoveryProgressSamples++;
            if (sample.sourceFrame >= 40f && !savedRecovery) { Capture("recovery", sample); savedRecovery = true; }
        }
        // Read the final motion-time value even if the Animator has just completed its transition.
        if (sawRecovery && hasLandingTime)
            current.recoveryEndSourceFrame = Mathf.Max(current.recoveryEndSourceFrame, animator.GetFloat("LandingTime") * SourceEndFrame);
        if (wasAirborne && current.contactTime > 0f && sample.state == "Locomotion" && !sample.transitioning && current.locomotionTime < 0f)
            current.locomotionTime = elapsed;
        wasGrounded = sample.grounded; previousVerticalSpeed = sample.verticalSpeed;

        if (current.locomotionTime > 0f && elapsed >= current.locomotionTime + .2f) CompleteCase();
        else if (elapsed > 4f) { Check(false, "Jump completes anticipation, flight, full landing recovery and returns to Locomotion within four seconds."); CompleteCase(); }
    }

    static void CompleteCase()
    {
        Check(current.launches == 1, "One Space press produces exactly one launch (observed " + current.launches + ").");
        Check(current.launchTime >= .55f && current.launchTime <= .80f,
            "Physical takeoff follows preparation at 0.55..0.80s (observed " + current.launchTime.ToString("F3") + "s).");
        Check(current.sourceClipName == "Girl_Jump" && current.sourceClipLength >= 1.63f && current.sourceClipLength <= 1.71f,
            "Runtime jump states sample the uncropped 0..50 Girl_Jump source (observed " + current.sourceClipName + ", " + current.sourceClipLength.ToString("F3") + "s).");
        if (!current.ceiling)
        {
            Check(current.apexRise >= .95f && current.apexRise <= 1.35f,
                "Jump retains the configured 1.2m/-20 physics (measured rise " + current.apexRise.ToString("F3") + "m).");
            Check(current.apexSourceFrame >= 23f && current.apexSourceFrame <= 27f,
                "The actual physics apex uses the source apex pose near frame 25 (observed " + current.apexSourceFrame.ToString("F2") + ").");
        }
        else
            Check(current.apexRise > .1f && current.apexRise < .65f,
                "Low ceiling shortens the real flight instead of replaying a full-height trajectory (rise " + current.apexRise.ToString("F3") + "m).");
        if (current.ceiling)
            Check(current.contactTime > current.launchTime && current.contactSourceFrame >= 25f && current.contactSourceFrame <= 30f
                && current.contactJumpLanding && current.incomingContactFrame >= 29.5f && current.incomingContactFrame <= 31f,
                "Early contact freezes the actual outgoing fall pose and starts the separate incoming landing at frame 30 (outgoing "
                + current.contactSourceFrame.ToString("F2") + ", incoming " + current.incomingContactFrame.ToString("F2") + ").");
        else
            Check(current.contactTime > current.launchTime && current.contactSourceFrame >= 28f && current.contactSourceFrame <= 32f,
                "First physical floor contact uses the landing pose near source frame 30 (observed " + current.contactSourceFrame.ToString("F2") + ").");
        Check(current.contactKneeDelta < 35f,
            "Contact blending avoids a large adjacent-frame knee snap (largest change " + current.contactKneeDelta.ToString("F1") + " degrees).");
        Check(sawRecovery && current.recoveryStartSourceFrame >= 28f && current.recoveryStartSourceFrame <= 33f
            && current.recoveryProgressSamples >= 10 && current.recoveryEndSourceFrame >= 49.5f,
            "LandingRecovery progresses from contact through at least ten intermediate poses to full source end frame 50 before Locomotion (observed "
            + current.recoveryStartSourceFrame.ToString("F2") + " -> " + current.recoveryEndSourceFrame.ToString("F2")
            + ", intermediate samples=" + current.recoveryProgressSamples + ").");
        Check(current.locomotionTime > current.contactTime + .4f,
            "Ground contact preserves visible landing/recovery before locomotion resumes.");
        Check(current.earlyKneeDelta < 35f,
            "The first preparation frames avoid the cropped-clip knee snap (largest adjacent knee change " + current.earlyKneeDelta.ToString("F1") + " degrees).");
        Check(savedAnticipation && savedTakeoff && savedApex && savedContact && savedRecovery,
            "Fixed-camera proof includes anticipation, takeoff, apex, first contact and recovery.");
        if (caseIndex == 0) Check(current.continuousFrameCount >= 60, "Continuous proof covers the full first jump at 30 frames per second.");
        current.passed = current.errors.Count == 0;
        SaveSamples(); Save(); stage = 4; frames = 0;
    }

    // Interruption cases assert cancellation and re-entry, while ledge cases assert falling.
    // They deliberately do not reuse the normal jump's height/apex/takeoff expectations.
    static void BeginScenario(int index)
    {
        if (index >= ScenarioNames.Length) { Finish(); return; }
        scenarioIndex = index; scenarioStage = 0; frames = 0;
        current = null; samples = null; scenarioSamples = new List<Sample>();
        if (ceiling) { UnityEngine.Object.DestroyImmediate(ceiling); ceiling = null; }
        if (support) { UnityEngine.Object.DestroyImmediate(support); support = null; }
        animator.enabled = true; bridge.enabled = true;
        movement.enabled = true; movement.ResetMotion(); Keys();
        float floorHeight = 0f;
        if (index >= 4 && index <= 6)
        {
            support = GameObject.CreatePrimitive(PrimitiveType.Cube);
            support.name = "Temporary ledge regression support"; support.hideFlags = HideFlags.DontSave;
            support.transform.position = new Vector3(50f, 1.25f, 0f);
            support.transform.localScale = new Vector3(6f, .5f, 6f);
            floorHeight = 1.5f;
        }
        Teleport(new Vector3(50f, floorHeight + .02f - (controller.center.y - controller.height * .5f), 0f));
        bridge.visualRoot.localRotation = visualRotation;
        scenario = new ScenarioResult { name = ScenarioNames[index], initialLaunchSequence = movement.JumpLaunchSequence };
        report.scenarios.Add(scenario);
        sawFalling = sawFallAir = checkedFreshPreparation = capturedFrozenClock = false;
        previousLeftKnee = leftKnee.localRotation; previousRightKnee = rightKnee.localRotation;
        Save();
    }

    static void TickScenario()
    {
        Keys();
        if (scenarioStage == 0)
        {
            if (frames < 45) return;
            ScenarioCheck(movement.IsGrounded && animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"),
                "Scenario starts from a settled player in Locomotion.");
            scenarioStartFrame = Time.frameCount; scenarioBaseY = movement.transform.position.y;
            previousLeftKnee = leftKnee.localRotation; previousRightKnee = rightKnee.localRotation;
            if (scenarioIndex == 9)
            {
                ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ceiling.name = "Temporary fully blocked headroom"; ceiling.hideFlags = HideFlags.DontSave;
                float head = movement.transform.position.y + controller.center.y + controller.height * .5f;
                ceiling.transform.position = new Vector3(50f, head + .105f, 0f);
                ceiling.transform.localScale = new Vector3(6f, .2f, 6f); Physics.SyncTransforms();
            }
            if (scenarioIndex == 3) animator.enabled = false;
            if (scenarioIndex == 4) DropSupport();
            else if (scenarioIndex == 7)
            {
                movement.enabled = false; MarkAction(); CheckCancelledImmediately(); scenarioStage = 6;
            }
            else { Keys(Key.Space); scenarioStage = 1; }
            frames = 0;
            return;
        }
        float time = (Time.frameCount - scenarioStartFrame) * Step;
        Sample sample = ReadSample(time); scenarioSamples.Add(sample);
        scenario.maxRise = Mathf.Max(scenario.maxRise, movement.transform.position.y - scenarioBaseY);
        if (movement.JumpLaunchSequence > scenario.initialLaunchSequence && scenario.firstLaunchTime < 0f)
            scenario.firstLaunchTime = time;
        if (time > 8f && scenarioStage < 4)
        {
            ScenarioCheck(false, "Scenario reaches cancellation or completed fall within eight seconds.");
            CompleteScenario(); return;
        }

        if (scenarioStage == 1)
        {
            if ((scenarioIndex == 0 || scenarioIndex == 2 || scenarioIndex == 5) && time >= .30f)
            {
                ScenarioCheck(sample.grounded && sample.jumpActive && movement.JumpLaunchSequence == scenario.initialLaunchSequence,
                    "Interruption occurs during grounded preparation before any physical launch.");
                if (scenarioIndex == 5) DropSupport();
                else if (scenarioIndex == 2)
                {
                    movement.enabled = false; MarkAction(); scenarioStage = 2;
                    CheckCancelledImmediately();
                }
                else ResetWaterSequence();
            }
            else if (scenarioIndex == 1 && movement.JumpLaunchSequence > scenario.initialLaunchSequence && time >= .80f)
            {
                ScenarioCheck(!sample.grounded && sample.verticalSpeed > 1f, "Water reset occurs during real ascending flight.");
                ResetWaterSequence();
            }
            else if (scenarioIndex == 3 && time >= 1.2f && sample.grounded)
            {
                ScenarioCheck(movement.JumpLaunchSequence == scenario.initialLaunchSequence + 1 && scenario.firstLaunchTime <= .1f,
                    "A disabled Animator falls back to one immediate physical jump without consuming Space.");
                ScenarioCheck(scenario.maxRise >= .95f && scenario.maxRise <= 1.35f,
                    "Animator-disabled fallback retains the original jump height (rise " + scenario.maxRise.ToString("F3") + "m).");
                animator.enabled = true; MarkAction(); scenarioStage = 3;
            }
            else if (scenarioIndex == 6 && sample.jumpLanding && sample.landingFrame >= 35f)
            {
                ScenarioCheck(movement.IsGrounded && movement.JumpLaunchSequence == scenario.initialLaunchSequence + 1,
                    "Support is removed during the actual recovery after one completed physical jump.");
                DropSupport();
            }
            else if (scenarioIndex == 8 && sample.jumpActive && Mathf.Abs(animator.GetFloat("JumpTime") * SourceEndFrame - 19f) < .05f
                && movement.IsGrounded && movement.JumpLaunchSequence == scenario.initialLaunchSequence)
            {
                scenario.checks.Add("Intercepted source frame 19 while grounded, with an accepted preparation and no physical launch yet.");
                animator.enabled = false; MarkAction(); scenarioStage = 7;
            }
            else if (scenarioIndex == 8 && time > .85f)
            {
                ScenarioCheck(false, "The harness observes the grounded frame-19 queued-takeoff boundary before the next movement update.");
                CompleteScenario();
            }
            else if (scenarioIndex == 9 && time >= 2.2f)
            {
                ScenarioCheck(movement.JumpLaunchSequence == scenario.initialLaunchSequence + 1 && scenario.maxRise < .08f,
                    "Fully blocked headroom allows one launch attempt but prevents a physical upward flight (rise " + scenario.maxRise.ToString("F3") + "m).");
                ScenarioCheck(sample.grounded && sample.state == "Locomotion" && !sample.transitioning && !sample.jumpActive && !sample.jumpLanding,
                    "A launch blocked by the ceiling finishes recovery and releases the jump sequence instead of waiting forever for an airborne sample.");
                CompleteScenario();
            }
        }
        else if (scenarioStage == 2)
        {
            if (scenarioIndex >= 4 && scenarioIndex <= 6) ObserveFall(sample);
            else if (scenarioIndex == 2)
            {
                ScenarioCheck(movement.JumpLaunchSequence == actionLaunchSequence,
                    "Disabling movement cancels the queued launch instead of firing it later.");
                if ((Time.frameCount - actionFrame) * Step < .35f) return;
                ScenarioCheck(Vector3.Distance(actionPosition, movement.transform.position) < .001f && !movement.HasMotionSample,
                    "Disabled movement keeps the collider stationary and clears stale motion samples.");
                movement.enabled = true; MarkAction(); scenarioStage = 3;
            }
            else ObserveCancellation();
        }
        else if (scenarioStage == 3)
        {
            ScenarioCheck(movement.JumpLaunchSequence == actionLaunchSequence,
                "Restoring movement/animation does not replay a stale launch request.");
            if ((Time.frameCount - actionFrame) * Step >= 1.2f) StartFreshJump();
        }
        else if (scenarioStage == 4) ObserveFreshJump(sample);
        else if (scenarioStage == 5 && (Time.frameCount - freshStartFrame) >= 5)
        {
            ScenarioCheck(movement.IsGrounded, "The restored player has a fresh grounded sample before requesting another jump.");
            freshStartFrame = Time.frameCount; freshLaunchSequence = movement.JumpLaunchSequence;
            scenarioBaseY = movement.transform.position.y; checkedFreshPreparation = false;
            Keys(Key.Space); scenarioStage = 4;
        }
        else if (scenarioStage == 6 && (Time.frameCount - actionFrame) * Step >= .35f)
        {
            ScenarioCheck(!movement.HasMotionSample && movement.JumpLaunchSequence == actionLaunchSequence,
                "Movement remains disabled long enough for the animation bridge to unregister before re-enabling.");
            // Both operations occur between the same two runtime frames. There is deliberately
            // no empty Update in which the order-200 animation bridge can quietly rebind first.
            movement.enabled = true;
            freshStartFrame = Time.frameCount; freshLaunchSequence = movement.JumpLaunchSequence;
            scenarioBaseY = movement.transform.position.y; checkedFreshPreparation = false;
            Keys(Key.Space); scenarioStage = 4;
        }
        else if (scenarioStage == 7)
        {
            ScenarioCheck(movement.JumpLaunchSequence == actionLaunchSequence && movement.transform.position.y - actionPosition.y < .03f,
                "Disabling Animator after source frame 19 cancels the queued impulse before the next movement update; no ghost takeoff occurs.");
            if ((Time.frameCount - actionFrame) * Step >= .30f && sample.grounded)
            {
                freshStartFrame = Time.frameCount; freshLaunchSequence = movement.JumpLaunchSequence;
                scenarioBaseY = movement.transform.position.y; scenario.maxRise = 0f;
                Keys(Key.Space); scenarioStage = 8;
            }
        }
        else if (scenarioStage == 8)
        {
            float fallbackTime = (Time.frameCount - freshStartFrame) * Step;
            if (movement.JumpLaunchSequence > freshLaunchSequence && scenario.fallbackLaunchDelay < 0f)
                scenario.fallbackLaunchDelay = fallbackTime;
            if (fallbackTime >= 1.2f && sample.grounded)
            {
                ScenarioCheck(movement.JumpLaunchSequence == freshLaunchSequence + 1 && scenario.fallbackLaunchDelay <= .1f,
                    "After cancelling the old queued request, a fresh Space with Animator still disabled produces one immediate fallback jump.");
                ScenarioCheck(scenario.maxRise >= .95f && scenario.maxRise <= 1.35f,
                    "The fresh Animator-disabled fallback retains the original physical jump height.");
                CompleteScenario();
            }
        }
    }

    static void MarkAction()
    {
        actionFrame = Time.frameCount; actionLaunchSequence = movement.JumpLaunchSequence;
        actionPosition = movement.transform.position; scenario.actionTime = (actionFrame - scenarioStartFrame) * Step;
        scenario.launchSequenceAtAction = actionLaunchSequence;
    }

    static void ResetWaterSequence()
    {
        ScenarioCheck(cycle && cycle.player == movement.transform, "The actual scene water reset still targets the integrated player.");
        if (!cycle) { CompleteScenario(); return; }
        cycle.ResetSequence(true); MarkAction(); CheckCancelledImmediately(); scenarioStage = 2;
    }

    static void CheckCancelledImmediately()
    {
        ScenarioCheck(movement.PlanarVelocity.sqrMagnitude < .000001f && Mathf.Abs(movement.VerticalSpeed) < .001f
            && !movement.HasMotionSample && !animator.GetBool("JumpActive") && !animator.GetBool("JumpLanding"),
            "Cancellation immediately clears velocity, stale motion data and active jump/recovery flags.");
    }

    static void ObserveCancellation()
    {
        ScenarioCheck(movement.JumpLaunchSequence == actionLaunchSequence,
            "Water reset prevents a delayed takeoff from the interrupted sequence.");
        if ((Time.frameCount - actionFrame) * Step < 1.2f) return;
        ScenarioCheck(movement.IsGrounded && !animator.GetBool("JumpActive") && !animator.GetBool("JumpLanding"),
            "The reset player settles without resuming preparation, flight or recovery.");
        StartFreshJump();
    }

    static void StartFreshJump()
    {
        // Teleport only after the no-delayed-launch observation. Do not reset movement again:
        // that would hide an interruption that left the bridge or pending impulse stuck.
        Teleport(new Vector3(50f, .025f - (controller.center.y - controller.height * .5f), 0f));
        freshStartFrame = Time.frameCount; freshLaunchSequence = movement.JumpLaunchSequence;
        scenarioBaseY = movement.transform.position.y; checkedFreshPreparation = false;
        scenarioStage = 5;
    }

    static void ObserveFreshJump(Sample sample)
    {
        float time = (Time.frameCount - freshStartFrame) * Step;
        if (!checkedFreshPreparation && time >= .30f)
        {
            checkedFreshPreparation = true;
            ScenarioCheck(movement.IsGrounded && movement.transform.position.y - scenarioBaseY < .03f
                && movement.JumpLaunchSequence == freshLaunchSequence && animator.GetBool("JumpActive"),
                scenarioIndex == 7
                    ? "Re-enabling movement in the same frame as Space preserves grounded anticipation instead of falling back to an immediate impulse."
                    : "After interruption or fallback, a fresh Space request again plays grounded anticipation.");
        }
        if (time >= .8f && time < 1f)
            ScenarioCheck(movement.JumpLaunchSequence == freshLaunchSequence + 1,
                "The restored player accepts exactly one fresh animated jump.");
        if (time >= 2.4f)
        {
            ScenarioCheck(movement.JumpLaunchSequence == freshLaunchSequence + 1 && sample.grounded
                && sample.state == "Locomotion" && !sample.jumpActive && !sample.transitioning,
                "The fresh animated jump completes and returns to grounded locomotion.");
            CompleteScenario();
        }
    }

    static void DropSupport()
    {
        MarkAction();
        support.transform.position += Vector3.right * 30f; Physics.SyncTransforms();
        sawFalling = sawFallAir = capturedFrozenClock = false;
        scenarioStage = 2;
    }

    static void ObserveFall(Sample sample)
    {
        float afterDrop = (Time.frameCount - actionFrame) * Step;
        scenario.maxKneeDelta = Mathf.Max(scenario.maxKneeDelta, sample.kneeDelta);
        scenario.fallDepth = Mathf.Max(scenario.fallDepth, actionPosition.y - movement.transform.position.y);
        ScenarioCheck(movement.JumpLaunchSequence == actionLaunchSequence,
            "Losing support never creates an upward jump impulse or launches a cancelled preparation.");
        if (afterDrop > .1f && scenario.contactTime < 0f)
            ScenarioCheck(sample.verticalSpeed <= .1f, "Support loss produces downward motion without an upward impulse.");
        if (!sample.grounded) sawFallAir = true;
        if (sample.state == "Falling" && !sample.jumpLanding)
        {
            sawFalling = true; scenario.fallingSamples++;
            ScenarioCheck(sample.sourceFrame >= 24.5f && sample.sourceFrame <= 30.5f && sample.fallOnly,
                "A support loss uses the descending source poses in Falling, rather than replaying preparation.");
            if (scenario.lastFallFrame >= 0f)
                ScenarioCheck(sample.sourceFrame + .1f >= scenario.lastFallFrame, "Falling source time stays monotonic until contact.");
            scenario.lastFallFrame = sample.sourceFrame;
            if (scenarioIndex == 5 || scenarioIndex == 6)
            {
                string outgoing = scenarioIndex == 5 ? "JumpTime" : "LandingTime";
                float clock = animator.GetFloat(outgoing);
                if (!capturedFrozenClock) { frozenOutgoingClock = clock; capturedFrozenClock = true; }
                ScenarioCheck(Mathf.Abs(clock - frozenOutgoingClock) < .0001f,
                    "The outgoing " + outgoing + " pose clock stays frozen while the separate fall clock advances.");
            }
        }
        if (sawFallAir && sample.grounded && scenario.contactTime < 0f)
        {
            scenario.contactTime = afterDrop; scenario.incomingContactFrame = sample.landingFrame;
            ScenarioCheck(sample.jumpLanding && sample.landingFrame >= 29.5f && sample.landingFrame <= 31f,
                "Ledge contact begins the separate landing clock at source frame 30.");
        }
        if (scenario.contactTime >= 0f && sample.jumpLanding)
        {
            scenario.recoveryEndFrame = Mathf.Max(scenario.recoveryEndFrame, sample.landingFrame);
            if (sample.landingFrame > 31f && sample.landingFrame < 49f) scenario.recoverySamples++;
        }
        if (scenario.contactTime >= 0f)
            scenario.recoveryEndFrame = Mathf.Max(scenario.recoveryEndFrame, animator.GetFloat("LandingTime") * SourceEndFrame);
        if (sawFalling && scenario.contactTime >= 0f && sample.state == "Locomotion" && !sample.transitioning && !sample.jumpActive)
        {
            scenario.locomotionTime = afterDrop;
            ScenarioCheck(scenario.fallingSamples >= 3 && scenario.fallDepth > 1.2f && scenario.fallDepth < 1.7f,
                "The player actually falls from the 1.5m temporary support to the catch floor.");
            ScenarioCheck(scenario.recoverySamples >= 10 && scenario.recoveryEndFrame >= 49.5f,
                "A ledge fall progresses through the full landing recovery to frame 50.");
            ScenarioCheck(scenario.maxKneeDelta < 35f,
                "Fall/contact transitions avoid a large adjacent-frame knee snap (largest change " + scenario.maxKneeDelta.ToString("F1") + " degrees).");
            CompleteScenario();
        }
    }

    static void ScenarioCheck(bool condition, string message)
    {
        if (condition)
        {
            if (!scenario.checks.Contains(message)) scenario.checks.Add(message);
            return;
        }
        if (!scenario.errors.Contains(message)) scenario.errors.Add(message);
        if (!captureOnly) throw new InvalidOperationException(message);
    }

    static void CompleteScenario()
    {
        scenario.passed = scenario.errors.Count == 0;
        SaveScenarioSamples(); Save(); BeginScenario(scenarioIndex + 1);
    }

    static void SaveScenarioSamples()
    {
        if (scenario == null || scenarioSamples == null) return;
        scenario.samplesPath = Path.Combine(DirectoryPath, scenario.name + "-samples.json");
        File.WriteAllText(scenario.samplesPath, JsonUtility.ToJson(new SampleFile { name = scenario.name, samples = scenarioSamples }, true));
    }

    static void Check(bool condition, string message)
    {
        if (condition)
        {
            // Per-frame monotonic checks should not inflate the persistent summary.
            if (!message.StartsWith("Full source time stays monotonic") && !current.checks.Contains(message)) current.checks.Add(message);
            return;
        }
        current.errors.Add(message); current.passed = false;
        if (!captureOnly) throw new InvalidOperationException(message);
    }

    static void Capture(string label, Sample sample)
    {
        string path = Path.Combine(DirectoryPath, current.name + "-" + label + ".png");
        RenderPng(path, 1280, 800);
        File.WriteAllText(Path.ChangeExtension(path, ".json"), JsonUtility.ToJson(sample, true));
        current.captures.Add(path);
    }

    static void CaptureContinuousFrame(int relativeFrame)
    {
        string path = Path.Combine(current.continuousFramesDirectory, "frame-" + current.continuousFrameCount.ToString("D4") + ".png");
        RenderPng(path, 768, 480);
        File.AppendAllText(Path.Combine(current.continuousFramesDirectory, "frame-times.csv"), current.continuousFrameCount + "," + relativeFrame + "," + (relativeFrame * Step).ToString("F6", System.Globalization.CultureInfo.InvariantCulture) + "\n");
        current.continuousFrameCount++;
    }

    static void RenderPng(string path, int width, int height)
    {
        var target = new RenderTexture(width, height, 24);
        Texture2D image = null; RenderTexture active = RenderTexture.active;
        RenderTexture previousTarget = view.targetTexture;
        try
        {
            target.Create(); view.targetTexture = target; view.Render(); RenderTexture.active = target;
            image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally
        {
            view.targetTexture = previousTarget; RenderTexture.active = active;
            target.Release(); UnityEngine.Object.DestroyImmediate(target);
            if (image) UnityEngine.Object.DestroyImmediate(image);
        }
    }

    static void SaveSamples()
    {
        if (current == null || samples == null) return;
        current.samplesPath = Path.Combine(DirectoryPath, current.name + "-samples.json");
        File.WriteAllText(current.samplesPath, JsonUtility.ToJson(new SampleFile { name = current.name, samples = samples }, true));
    }

    static void Save()
    {
        if (report == null) return;
        Directory.CreateDirectory(GirlPlayerIntegration.Out);
        File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
    }

    static void Finish()
    {
        report.passed = report.errors.Count == 0 && report.jumps.Count == 4 && report.jumps.All(j => j.passed)
            && report.scenarios.Count == ScenarioNames.Length && report.scenarios.All(s => s.passed);
        report.status = report.passed ? "passed" : "failed";
        Save(); EditorApplication.update -= Tick; EditorApplication.isPlaying = false;
    }

    static void Fail(Exception error)
    {
        if (report == null) report = new Report { date = DateTime.Now.ToString("s"), scene = SceneManager.GetActiveScene().path };
        report.passed = false; report.status = "failed";
        string context = caseIndex >= 4 && scenario != null ? scenario.name + ", stage " + scenarioStage : "Case " + caseIndex + ", stage " + stage;
        report.errors.Add(context + ": " + error);
        SaveSamples(); SaveScenarioSamples(); Save(); EditorApplication.update -= Tick;
        if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = false;
        else { Cleanup(); SessionState.SetBool(Running, false); }
    }

    static void Log(string message, string stack, LogType type)
    {
        if (report != null && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
        {
            report.errors.Add(type + ": " + message + "\n" + stack); Save();
        }
    }

    static void Keys(params Key[] keys)
    {
        if (keyboard == null || !keyboard.added) return;
        if (!keyboard.enabled) InputSystem.EnableDevice(keyboard);
        keyboard.MakeCurrent();
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
    }

    static void Teleport(Vector3 position)
    {
        controller.enabled = false; movement.transform.position = position;
        controller.enabled = true; Physics.SyncTransforms();
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    static void RestoreSettings()
    {
        if (!SessionState.GetBool(SettingsSaved, false)) return;
        InputSystem.settings.backgroundBehavior = (InputSettings.BackgroundBehavior)SessionState.GetInt(OldBackground, 0);
        InputSystem.settings.editorInputBehaviorInPlayMode = (InputSettings.EditorInputBehaviorInPlayMode)SessionState.GetInt(OldEditorInput, 0);
        Time.captureDeltaTime = SessionState.GetFloat(OldCapture, 0f);
        Application.runInBackground = SessionState.GetBool(OldRunBackground, false);
        SessionState.SetBool(SettingsSaved, false);
    }

    static void Cleanup()
    {
        EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        if (keyboard != null && keyboard.added) { Keys(); InputSystem.RemoveDevice(keyboard); }
        keyboard = null;
        if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
        RestoreSettings();
        if (floor) UnityEngine.Object.DestroyImmediate(floor);
        if (ceiling) UnityEngine.Object.DestroyImmediate(ceiling);
        if (support) UnityEngine.Object.DestroyImmediate(support);
        if (initialized)
        {
            if (movement && controller)
            {
                Teleport(originalPosition); movement.transform.rotation = originalRotation;
                movement.enabled = movementEnabled;
                if (animator) animator.enabled = animatorEnabled;
                if (bridge) bridge.enabled = bridgeEnabled;
                if (bridge && bridge.visualRoot) bridge.visualRoot.localRotation = visualRotation;
            }
            if (view)
            {
                view.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
                view.transform.localScale = cameraScale; view.targetTexture = cameraTarget; view.enabled = cameraEnabled;
                view.fieldOfView = cameraFieldOfView;
            }
            if (follow) follow.enabled = followEnabled;
            if (guard) guard.enabled = guardEnabled;
        }
        initialized = false;
    }
}
