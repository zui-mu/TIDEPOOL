using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Fixed, local-only verification commands. No arbitrary code or scene is executed from the request.
[InitializeOnLoad]
public static partial class GirlHairDynamicsRegression
{
    [Serializable] public sealed class Request { public string action, id; }
    [Serializable] public sealed class Result { public string name, error; public bool passed; }
    [Serializable] public sealed class RigBone { public string name; public Vector3 position, localPosition; }
    [Serializable] public sealed class RigInfo
    {
        public string name; public int weightedVertices; public float tipLength, radial90;
        public Vector3 tipAxis, headLocalRoot; public List<RigBone> bones = new List<RigBone>();
    }
    [Serializable] public sealed class Report
    {
        public string id, mode, date, status, scene, frameDirectory;
        public bool passed, playing, compiling, dirty;
        public int frames;
        public float maximumOffset, maximumPenetration, maximumAttachmentError, maximumLengthError, resetError, walkSpeed, runSpeed;
        public List<MotionSample> motion = new List<MotionSample>();
        public List<Result> cases = new List<Result>();
        public List<string> errors = new List<string>();
        public List<RigInfo> rig = new List<RigInfo>();
        public Vector3 scalpCenterInHead, scalpExtents;
    }
    const string CharacterRoot = "Assets/_Game/Characters/GirlV06/";
    static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    public static string RequestPath => Path.Combine(ProjectRoot, "Temp", "GirlHairDynamics", "request.json");
    public static string ReportPath => Path.Combine(ProjectRoot, "Development", "character_integration", "hair-dynamics-report.json");
    static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static Report report;
    static Type HairType => Type.GetType("GirlHairDynamics, Assembly-CSharp");

    static GirlHairDynamicsRegression()
    {
        EditorApplication.update -= Poll; EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged -= PlayChanged; EditorApplication.playModeStateChanged += PlayChanged;
    }
    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(RequestPath)) return;
        Request request;
        try { request = JsonUtility.FromJson<Request>(File.ReadAllText(RequestPath)); File.Delete(RequestPath); }
        catch (IOException) { return; }
        if (request.action == "status")
        {
            var snapshot = new Report
            {
                id = request.id, mode = "status", date = DateTime.Now.ToString("s"), status = "passed", passed = true,
                scene = SceneManager.GetActiveScene().path, playing = EditorApplication.isPlayingOrWillChangePlaymode,
                compiling = EditorApplication.isCompiling,
                dirty = Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty)
            };
            File.WriteAllText(Path.Combine(ProjectRoot, "Temp", "GirlHairDynamics", "status.json"), JsonUtility.ToJson(snapshot, true));
            return;
        }
        report = new Report { id = request.id, mode = request.action, date = DateTime.Now.ToString("s"), status = "running", scene = SceneManager.GetActiveScene().path };
        try
        {
                Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Finish Play Mode before hair setup or deterministic verification.");
                switch (request.action)
                {
                    case "test": RunTests(); break;
                    case "inspect": InspectRig(); break;
                    case "scene": VerifyScene(); break;
                    case "play": BeginPlay(request.id); return;
                    case "install":
                        var editor = Type.GetType("GirlHairDynamicsEditor, Assembly-CSharp-Editor");
                        Require(editor != null, "Hair editor setup is not implemented yet.");
                        editor.GetMethod("InstallCurrentCharacter", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
                        VerifyScene(); break;
                    default: throw new InvalidOperationException("Unsupported hair command: " + request.action);
                }
        }
        catch (Exception e) { report.errors.Add(e.GetBaseException().ToString()); }
        FinishReport();
    }
    [MenuItem("Tools/Girl V06/10 - Test braid dynamics")]
    public static void TestMenu()
    {
        report = new Report { mode = "test", date = DateTime.Now.ToString("s"), status = "running" };
        try { RunTests(); } catch (Exception e) { report.errors.Add(e.GetBaseException().ToString()); }
        FinishReport();
    }
    static void FinishReport()
    {
        report.passed = report.errors.Count == 0 && report.cases.All(c => c.passed);
        report.status = report.passed ? "passed" : "failed";
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
    }
    static void Case(string name, Action action)
    {
        var result = new Result { name = name }; report.cases.Add(result);
        try { action(); result.passed = true; }
        catch (Exception e) { result.error = e.GetBaseException().ToString(); report.errors.Add(name + ": " + result.error); }
    }
    static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
    static object Field(object owner, string name) => owner.GetType().GetField(name, Flags).GetValue(owner);
    static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Flags).SetValue(owner, value);
    static object Call(object owner, string name, params object[] args)
    {
        var method = owner.GetType().GetMethods(Flags).Single(m => m.Name == name && m.GetParameters().Length == args.Length);
        return method.Invoke(owner, args);
    }
    static bool Finite(Vector3 v) => !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));

    sealed class Fixture : IDisposable
    {
        readonly Scene previewScene;
        public readonly GameObject root;
        public readonly Transform head;
        public readonly Component hair;
        public readonly PlayerMovement movement;
        public readonly Transform[] bones;
        public readonly Vector3[] localPositions;
        public readonly Quaternion[] localRotations;
        public readonly Vector3 tipOffset, restTip;
        public Fixture()
        {
            previewScene = EditorSceneManager.NewPreviewScene();
            root = EditorUtility.CreateGameObjectWithHideFlags("Temporary braid spring fixture", HideFlags.HideAndDontSave);
            SceneManager.MoveGameObjectToScene(root, previewScene);
            try
            {
            movement = root.AddComponent<PlayerMovement>();
            head = new GameObject("Head").transform; head.SetParent(root.transform, false); head.localPosition = Vector3.up * 1.5f;
            var all = new List<Transform>();
            foreach (string side in new[] { "Left", "Right" })
            {
                Transform parent = head;
                for (int i = 0; i < 4; i++)
                {
                    var b = new GameObject("Hair_" + side + "_" + (i + 1)).transform; b.SetParent(parent, false);
                    b.localPosition = i == 0 ? new Vector3(side == "Left" ? -.1f : .1f, .10f, -.03f) : Vector3.up * .15f;
                    if (i == 0) b.localRotation = Quaternion.Euler(180f, 0f, 0f);
                    parent = b; all.Add(b);
                }
            }
            bones = all.ToArray(); localPositions = bones.Select(b => b.localPosition).ToArray(); localRotations = bones.Select(b => b.localRotation).ToArray();
            hair = root.AddComponent(HairType); Call(hair, "ConfigureFromHierarchy"); Set(hair, "gravityScale", 0f); Set(hair, "strength", 1f);
            tipOffset = (Vector3)Field(Field(hair, "left"), "tipOffset");
            restTip = head.InverseTransformPoint(bones[3].TransformPoint(tipOffset));
            for (int i = 0; i < 60; i++) Step(1f / 60f);
            }
            catch { Dispose(); throw; }
        }
        public Vector3 Tip => bones[3].TransformPoint(tipOffset);
        public Vector3 Rest => head.TransformPoint(restTip);
        public void Step(float delta, Vector3? position = null, Quaternion? rotation = null)
        {
            Call(hair, "RestoreBasePose");
            if (position.HasValue) head.position = position.Value;
            if (rotation.HasValue) head.rotation = rotation.Value;
            Call(hair, "Simulate", delta);
        }
        public void AssertSkeleton()
        {
            for (int i = 0; i < bones.Length; i++)
            {
                Require(Finite(bones[i].position), "Braid position must remain finite.");
                Require(Vector3.Distance(bones[i].localPosition, localPositions[i]) < .00001f, "Physics must not change bone translations.");
                Require(Vector3.Distance(bones[i].localScale, Vector3.one) < .00001f, "Physics must not stretch bone scales.");
            }
        }
        public void Dispose() { if (root) UnityEngine.Object.DestroyImmediate(root); if (previewScene.IsValid()) EditorSceneManager.ClosePreviewScene(previewScene); }
    }
    static void RunTests()
    {
        Require(HairType != null, "Missing GirlHairDynamics: current project has no braid simulation (expected baseline failure).");
        Case("inertia-root-and-length", () =>
        {
            using (var f = new Fixture())
            {
                float response = 0f;
                for (int n = 1; n <= 30; n++)
                {
                    f.Step(1f / 60f, new Vector3(Mathf.Min(n, 8) * .02f, 1.5f, 0f));
                    f.AssertSkeleton(); response = Mathf.Max(response, f.Rest.x - f.Tip.x);
                    Require(Vector3.Distance(f.bones[0].position, f.head.TransformPoint(f.localPositions[0])) < .00001f, "Left attachment stays pinned to Head.");
                }
                Require(response > .002f && response < .25f, "Head acceleration must cause restrained, measurable inertial lag; observed " + response);
            }
        });
        Case("settles-without-rest-pose-feedback", () =>
        {
            using (var f = new Fixture())
            {
                f.Step(1f / 60f, new Vector3(.15f, 1.5f, 0f));
                for (int n = 0; n < 360; n++) f.Step(1f / 60f);
                Require(Vector3.Distance(f.Tip, f.Rest) < .003f, "With no gravity and a stationary head, the braid must settle back to its authored pose.");
            }
        });
        Case("frame-rate-30-60-120", () =>
        {
            var tips = new List<Vector3>();
            foreach (int fps in new[] { 30, 60, 120 }) using (var f = new Fixture())
            {
                for (int n = 1; n <= fps * 2; n++)
                {
                    float t = n / (float)fps;
                    f.Step(1f / fps, new Vector3(Mathf.Sin(t * 7f) * .08f, 1.5f + Mathf.Sin(t * 4f) * .025f, 0f), Quaternion.Euler(0f, Mathf.Sin(t * 3f) * 12f, 0f));
                    f.AssertSkeleton();
                }
                tips.Add(f.Tip);
            }
            Require(Vector3.Distance(tips[0], tips[1]) < .035f && Vector3.Distance(tips[1], tips[2]) < .035f, "Frame-rate variation must not materially change the braid pose.");
        });
        Case("reset-notification-rebuilds-at-final-pose", () =>
        {
            using (var f = new Fixture())
            {
                for (int n = 0; n < 8; n++) f.Step(1f / 60f, f.head.position + Vector3.right * .025f);
                f.movement.ResetMotion();
                f.Step(1f / 60f, f.head.position + new Vector3(.03f, .02f, 0f));
                Require(Vector3.Distance(f.Tip, f.Rest) < .001f, "ResetMotion must clear history at the final pose even for a sub-threshold teleport.");
            }
        });
        Case("teleport-long-frame-and-zero-delta", () =>
        {
            using (var f = new Fixture())
            {
                f.Step(1f / 60f, new Vector3(4f, 2f, -3f), Quaternion.Euler(0f, 175f, 0f));
                Require(Vector3.Distance(f.Tip, f.Rest) < .001f, "Large teleport must rebuild instead of stretching across the old position.");
                Vector3 old = f.Tip; Call(f.hair, "Simulate", 0f);
                Require(Vector3.Distance(old, f.Tip) < .00001f, "Zero delta must not integrate or reset the output.");
                f.Step(.5f); f.AssertSkeleton(); Require(Vector3.Distance(f.Tip, f.Rest) < .001f, "Long frame must safely rebuild.");
            }
        });
        Case("disable-restores-reference", () =>
        {
            using (var f = new Fixture())
            {
                f.Step(1f / 60f, f.head.position + Vector3.right * .1f); Call(f.hair, "OnDisable");
                for (int n = 0; n < f.bones.Length; n++) Require(Quaternion.Angle(f.bones[n].localRotation, f.localRotations[n]) < .01f, "Disabling restores authored local rotations.");
            }
        });
        Case("live-binding-change-recovers-without-exceptions", () =>
        {
            using (var f = new Fixture())
            {
                f.Step(1f / 60f, f.head.position + Vector3.right * .1f);
                object config = Field(f.hair, "left"); var oldBones = (Transform[])Field(config, "bones");
                Set(config, "bones", Array.Empty<Transform>());
                try { f.Step(1f / 60f); }
                finally { Set(config, "bones", oldBones); }
                for (int n = 0; n < f.bones.Length; n++) Require(Quaternion.Angle(f.bones[n].localRotation, f.localRotations[n]) < .01f,
                    "Invalid live bindings must safely restore the previously bound skeleton.");
                f.Step(1f / 60f); f.AssertSkeleton();
                Require(Vector3.Distance(f.Tip, f.Rest) < .001f, "Restoring valid bindings should safely rebuild the solver.");
            }
        });
        Case("uniform-scale-collision-radius", () =>
        {
            Type proxyType = Type.GetType("GirlHairCollisionProxy, Assembly-CSharp");
            foreach (float scale in new[] { 2f, .5f }) using (var f = new Fixture())
            {
                f.root.transform.localScale = Vector3.one * scale;
                object proxy = Activator.CreateInstance(proxyType);
                Set(proxy, "anchor", f.head); Set(proxy, "localStart", new Vector3(-.055f, -.05f, -.03f));
                Set(proxy, "localEnd", new Vector3(-.055f, -.05f, -.03f)); Set(proxy, "radius", .02f);
                Array proxies = Array.CreateInstance(proxyType, 1); proxies.SetValue(proxy, 0); Set(f.hair, "colliders", proxies);
                for (int n = 0; n < 90; n++) f.Step(1f / 60f);
                float gap = (float)Call(proxy, "SignedDistanceToSegment", f.bones[0].position, f.bones[1].position);
                Require(gap >= .042f * scale, "Uniform scale must scale hair thickness as well as body proxies; scale=" + scale + ", gap=" + gap);
                f.AssertSkeleton();
            }
        });
        Case("capsule-segment-projection", () =>
        {
            Type type = Type.GetType("GirlHairCollisionProxy, Assembly-CSharp"); Require(type != null, "Collision proxy is required.");
            var anchor = new GameObject("Temporary capsule anchor");
            try
            {
                object proxy = Activator.CreateInstance(type); Set(proxy, "anchor", anchor.transform); Set(proxy, "localStart", Vector3.down * .25f); Set(proxy, "localEnd", Vector3.up * .25f); Set(proxy, "radius", .2f);
                object[] args = { new Vector3(.18f, 0f, -.1f), new Vector3(.18f, 0f, .1f), .02f, false, Vector3.right };
                var method = type.GetMethod("ProjectSegment");
                for (int n = 0; n < 8; n++) method.Invoke(proxy, args);
                float distance = (float)Call(proxy, "SignedDistanceToSegment", (Vector3)args[0], (Vector3)args[1]);
                Require(distance >= .019f, "Hair segment must be projected outside the capsule, not just its endpoints.");
                args[0] = Vector3.down * .1f; args[1] = Vector3.up * .1f;
                for (int n = 0; n < 8; n++) method.Invoke(proxy, args);
                Require(Finite((Vector3)args[0]) && Finite((Vector3)args[1]), "Coincident capsule axes need a stable finite fallback normal.");
            }
            finally { UnityEngine.Object.DestroyImmediate(anchor); }
        });
    }
    static void VerifyScene()
    {
        Require(HairType != null, "Hair dynamics type is missing.");
        var player = UnityEngine.Object.FindFirstObjectByType<PlayerMovement>(); Require(player, "Scene player missing.");
        var visual = player.transform.Find("GirlVisual"); Require(visual, "GirlVisual missing.");
        Require(visual.GetComponent(HairType), "ArmLab GirlVisual has no configured hair dynamics yet (expected before installation).");
        Case("scene-hair-component", () => Require(((Behaviour)visual.GetComponent(HairType)).enabled, "Hair component must be enabled."));
    }
    static float Weight(BoneWeight w, int index)
    {
        return (w.boneIndex0 == index ? w.weight0 : 0f) + (w.boneIndex1 == index ? w.weight1 : 0f) + (w.boneIndex2 == index ? w.weight2 : 0f) + (w.boneIndex3 == index ? w.weight3 : 0f);
    }
    static void InspectRig()
    {
        var scene = EditorSceneManager.NewPreviewScene(); GameObject clone = null; Mesh baked = null;
        try
        {
            clone = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CharacterRoot + "GirlV06_Character.prefab"), scene);
            var transforms = clone.GetComponentsInChildren<Transform>(true);
            Transform head = transforms.Single(t => t.name == "Head");
            var renderer = clone.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name.IndexOf("Hair", StringComparison.OrdinalIgnoreCase) >= 0);
            baked = new Mesh(); renderer.BakeMesh(baked);
            Vector3[] vertices = baked.vertices; BoneWeight[] weights = renderer.sharedMesh.boneWeights;
            int headIndex = Array.IndexOf(renderer.bones, head); bool hasScalp = false; Bounds scalp = default;
            for (int i = 0; i < vertices.Length; i++) if (Weight(weights[i], headIndex) >= .95f)
            {
                Vector3 world = renderer.transform.TransformPoint(vertices[i]);
                if (!hasScalp) { scalp = new Bounds(world, Vector3.zero); hasScalp = true; } else scalp.Encapsulate(world);
            }
            if (hasScalp) { report.scalpCenterInHead = head.InverseTransformPoint(scalp.center); report.scalpExtents = scalp.extents; }
            foreach (string side in new[] { "Left", "Right" })
            {
                var info = new RigInfo { name = side };
                var bones = Enumerable.Range(1, 4).Select(n => transforms.Single(t => t.name == "Hair_" + side + "_" + n)).ToArray();
                foreach (var b in bones) info.bones.Add(new RigBone { name = b.name, position = b.position, localPosition = b.localPosition });
                info.headLocalRoot = head.InverseTransformPoint(bones[0].position);
                int index = Array.IndexOf(renderer.bones, bones[3]); Vector3 axis = bones[3].localPosition.normalized; info.tipAxis = axis;
                var radial = new List<float>();
                for (int n = 0; n < vertices.Length; n++) if (Weight(weights[n], index) >= .25f)
                {
                    Vector3 local = bones[3].InverseTransformPoint(renderer.transform.TransformPoint(vertices[n])); float projection = Vector3.Dot(local, axis);
                    info.tipLength = Mathf.Max(info.tipLength, projection); radial.Add((local - axis * projection).magnitude); info.weightedVertices++;
                }
                radial.Sort(); if (radial.Count > 0) info.radial90 = radial[Mathf.Clamp((int)(radial.Count * .9f), 0, radial.Count - 1)];
                report.rig.Add(info);
            }
        }
        finally { if (baked) UnityEngine.Object.DestroyImmediate(baked); if (clone) UnityEngine.Object.DestroyImmediate(clone); EditorSceneManager.ClosePreviewScene(scene); }
    }

    [Serializable] public sealed class MotionSample
    {
        public float time, leftOffset, rightOffset, penetration, planarSpeed;
        public int image = -1;
        public string phase;
        public Vector3 headPosition, leftTip, rightTip;
    }
    const string PlayRunning = "GirlHairDynamicsRegression.PlayRunning";
    const string PlayReport = "GirlHairDynamicsRegression.PlayReport";
    const string PlaySettings = "GirlHairDynamicsRegression.Settings";
    const string InputBackground = "GirlHairDynamicsRegression.InputBackground";
    const string EditorInput = "GirlHairDynamicsRegression.EditorInput";
    const string CaptureDelta = "GirlHairDynamicsRegression.CaptureDelta";
    const string RunBackground = "GirlHairDynamicsRegression.RunBackground";
    const float PlayStep = 1f / 60f;
    static readonly float[] Durations = { .5f, 1.3f, .8f, 1f, 2.3f, .8f, .8f, .2f, .5f };
    static readonly string[] PhaseNames = { "idle", "walk", "stop", "run", "running-jump", "turn", "stop", "reset", "idle" };
    static GirlHairDynamics liveHair;
    static PlayerMovement liveMovement;
    static CharacterController liveController;
    static Keyboard liveKeyboard, oldKeyboard;
    static Camera liveCamera;
    static CameraFollow cameraFollow;
    static DryRegionCameraGuard cameraGuard;
    static GameObject liveFloor;
    static Transform[] liveBones;
    static Vector3[] originalLocalPositions;
    static Vector3 leftRestTip, rightRestTip, leftRoot, rightRoot;
    static Vector3 oldCameraPosition, oldCameraScale;
    static Quaternion oldCameraRotation;
    static bool oldFollow, oldGuard, oldCameraEnabled, playInitialized, sawReset, statusProbeSent, statusProbeObserved;
    static string initialPlayId;
    static float oldFieldOfView;
    static RenderTexture oldTarget;
    static int playStart, playLastFrame, previousStage, resetStart, imageIndex;
    static double playDeadline;

    [MenuItem("Tools/Girl V06/12 - Capture and verify braid dynamics")]
    public static void PlayMenu()
    {
        report = new Report { id = Guid.NewGuid().ToString(), mode = "play", date = DateTime.Now.ToString("s"), status = "running" };
        try { BeginPlay(report.id); } catch (Exception e) { report.errors.Add(e.GetBaseException().ToString()); FinishReport(); }
    }
    static void BeginPlay(string id)
    {
        Require(!SessionState.GetBool(PlayRunning, false), "Hair verification is already running.");
        Require(SceneManager.GetActiveScene().path == GirlPlayerIntegration.ScenePath, "Open ArmLab for actual-character verification.");
        for (int n = 0; n < SceneManager.sceneCount; n++) Require(!SceneManager.GetSceneAt(n).isDirty, "Save scene edits before verification.");
        RunTests(); VerifyScene(); Require(report.errors.Count == 0, "Core and scene checks must pass before Play verification.");
        Directory.CreateDirectory(Path.GetDirectoryName(RequestPath));
        report.mode = "play"; report.id = id; report.status = "running";
        SessionState.SetString(PlayReport, JsonUtility.ToJson(report)); SessionState.SetBool(PlayRunning, true);
        SavePlayReport(); EditorApplication.isPlaying = true;
    }
    static void PlayChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(PlayRunning, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            try { InitializePlay(); } catch (Exception e) { FailPlay(e); }
        }
        if (state == PlayModeStateChange.ExitingPlayMode) CleanupPlay();
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            RestorePlaySettings(); SessionState.SetBool(PlayRunning, false);
            if (report == null && File.Exists(ReportPath)) report = JsonUtility.FromJson<Report>(File.ReadAllText(ReportPath));
            if (report != null && report.status == "running") { report.errors.Add("Play verification ended before completion."); FinishReport(); }
        }
    }
    static void InitializePlay()
    {
        report = JsonUtility.FromJson<Report>(SessionState.GetString(PlayReport, "{}"));
        liveMovement = UnityEngine.Object.FindFirstObjectByType<PlayerMovement>();
        liveController = liveMovement.GetComponent<CharacterController>();
        liveHair = liveMovement.transform.Find("GirlVisual").GetComponent<GirlHairDynamics>();
        Require(liveHair && liveHair.enabled && liveHair.ValidateConfiguration(out _), "Configured live hair component is required.");
        liveCamera = Camera.main; Require(liveCamera, "Main camera missing.");
        oldCameraPosition = liveCamera.transform.position; oldCameraRotation = liveCamera.transform.rotation;
        oldCameraScale = liveCamera.transform.localScale; oldFieldOfView = liveCamera.fieldOfView; oldTarget = liveCamera.targetTexture; oldCameraEnabled = liveCamera.enabled;
        cameraFollow = liveCamera.GetComponent<CameraFollow>(); cameraGuard = liveCamera.GetComponent<DryRegionCameraGuard>();
        if (cameraFollow) { oldFollow = cameraFollow.enabled; cameraFollow.enabled = false; }
        if (cameraGuard) { oldGuard = cameraGuard.enabled; cameraGuard.enabled = false; }
        liveCamera.enabled = true; liveCamera.fieldOfView = 35f;
        SessionState.SetInt(InputBackground, (int)InputSystem.settings.backgroundBehavior);
        SessionState.SetInt(EditorInput, (int)InputSystem.settings.editorInputBehaviorInPlayMode);
        SessionState.SetFloat(CaptureDelta, Time.captureDeltaTime); SessionState.SetBool(RunBackground, Application.runInBackground); SessionState.SetBool(PlaySettings, true);
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        Time.captureDeltaTime = PlayStep; Application.runInBackground = true;
        oldKeyboard = Keyboard.current; liveKeyboard = InputSystem.AddDevice<Keyboard>("GirlHairDynamicsVerificationKeyboard"); InputSystem.EnableDevice(liveKeyboard); liveKeyboard.MakeCurrent();
        liveFloor = GameObject.CreatePrimitive(PrimitiveType.Cube); liveFloor.name = "Temporary hair verification floor"; liveFloor.hideFlags = HideFlags.DontSave;
        liveFloor.transform.position = new Vector3(50f, -.25f, 12f); liveFloor.transform.localScale = new Vector3(30f, .5f, 60f);
        liveMovement.ResetMotion(); liveController.enabled = false;
        liveMovement.transform.position = new Vector3(50f, .02f - (liveController.center.y - liveController.height * .5f), 0f);
        liveController.enabled = true; Physics.SyncTransforms();
        liveBones = liveHair.left.bones.Concat(liveHair.right.bones).ToArray(); originalLocalPositions = liveBones.Select(b => b.localPosition).ToArray();
        leftRestTip = liveHair.head.InverseTransformPoint(liveHair.left.bones[3].TransformPoint(liveHair.left.tipOffset));
        rightRestTip = liveHair.head.InverseTransformPoint(liveHair.right.bones[3].TransformPoint(liveHair.right.tipOffset));
        leftRoot = liveHair.head.InverseTransformPoint(liveHair.left.bones[0].position); rightRoot = liveHair.head.InverseTransformPoint(liveHair.right.bones[0].position);
        report.frameDirectory = Path.Combine(ProjectRoot, "Library", "HairDynamicsImplementation", "frames-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(report.frameDirectory); playInitialized = true; sawReset = false; imageIndex = 0;
        initialPlayId = report.id; statusProbeSent = statusProbeObserved = false;
        previousStage = -1; playStart = Time.frameCount; playLastFrame = -1; playDeadline = EditorApplication.timeSinceStartup + 240;
        Application.logMessageReceived += PlayLog; EditorApplication.update -= PlayTick; EditorApplication.update += PlayTick; SavePlayReport();
    }
    static void PlayTick()
    {
        if (!EditorApplication.isPlaying || !playInitialized) return;
        if (Time.frameCount == playLastFrame) { EditorApplication.QueuePlayerLoopUpdate(); return; }
        playLastFrame = Time.frameCount;
        try
        {
            Require(EditorApplication.timeSinceStartup < playDeadline, "Hair Play verification timed out.");
            Require(report.id == initialPlayId, "A status query must not replace the active capture report.");
            int runtimeFrames = Time.frameCount - playStart;
            if (!statusProbeSent && runtimeFrames >= 3)
            {
                File.WriteAllText(RequestPath, JsonUtility.ToJson(new Request { action = "status", id = "probe-" + initialPlayId })); statusProbeSent = true;
            }
            if (statusProbeSent && !statusProbeObserved && runtimeFrames >= 8)
            {
                string statusFile = Path.Combine(ProjectRoot, "Temp", "GirlHairDynamics", "status.json");
                Require(File.Exists(statusFile), "Status needs a separate response without replacing capture evidence.");
                var status = JsonUtility.FromJson<Report>(File.ReadAllText(statusFile));
                Require(status.id == "probe-" + initialPlayId && status.playing && report.id == initialPlayId,
                    "Read-only status query must preserve the active capture and respond for the correct request.");
                statusProbeObserved = true;
            }
            if (!liveKeyboard.enabled) InputSystem.EnableDevice(liveKeyboard); liveKeyboard.MakeCurrent();
            float time = (Time.frameCount - playStart) * PlayStep, stageStart = 0f; int stage = 0;
            while (stage < Durations.Length && time >= stageStart + Durations[stage]) { stageStart += Durations[stage]; stage++; }
            if (stage >= Durations.Length) { CompletePlay(); return; }
            bool entered = stage != previousStage; previousStage = stage;
            if (stage == 1) PlayKeys(Key.W);
            else if (stage == 3) PlayKeys(Key.W, Key.LeftShift);
            else if (stage == 4) { if (entered) PlayKeys(Key.W, Key.LeftShift, Key.Space); else PlayKeys(Key.W, Key.LeftShift); }
            else if (stage == 5) PlayKeys(Key.D, Key.LeftShift);
            else if (stage == 7 && entered) { PlayKeys(Key.R); resetStart = Time.frameCount; }
            else PlayKeys();
            Vector3 tipL = liveHair.left.bones[3].TransformPoint(liveHair.left.tipOffset), tipR = liveHair.right.bones[3].TransformPoint(liveHair.right.tipOffset);
            Require(Finite(tipL) && Finite(tipR), "Live braid output must remain finite.");
            float offsetL = Vector3.Distance(tipL, liveHair.head.TransformPoint(leftRestTip)), offsetR = Vector3.Distance(tipR, liveHair.head.TransformPoint(rightRestTip));
            report.maximumOffset = Mathf.Max(report.maximumOffset, Mathf.Max(offsetL, offsetR));
            float attachment = Mathf.Max(Vector3.Distance(liveHair.left.bones[0].position, liveHair.head.TransformPoint(leftRoot)), Vector3.Distance(liveHair.right.bones[0].position, liveHair.head.TransformPoint(rightRoot)));
            report.maximumAttachmentError = Mathf.Max(report.maximumAttachmentError, attachment);
            for (int n = 0; n < liveBones.Length; n++) report.maximumLengthError = Mathf.Max(report.maximumLengthError, Vector3.Distance(liveBones[n].localPosition, originalLocalPositions[n]));
            float penetration = 0f;
            foreach (var chain in new[] { liveHair.left, liveHair.right }) for (int n = 0; n < 4; n++)
            {
                Vector3 a = chain.bones[n].position, b = n < 3 ? chain.bones[n + 1].position : chain.bones[3].TransformPoint(chain.tipOffset);
                float radius = Mathf.Lerp(chain.rootRadius, chain.tipRadius, n / 3f) * liveHair.head.lossyScale.x;
                foreach (var proxy in liveHair.colliders) penetration = Mathf.Max(penetration, radius - proxy.SignedDistanceToSegment(a, b));
            }
            report.maximumPenetration = Mathf.Max(report.maximumPenetration, penetration);
            if (stage == 1 && time - stageStart > .5f) report.walkSpeed = Mathf.Max(report.walkSpeed, liveMovement.PlanarVelocity.magnitude);
            if (stage == 3 && time - stageStart > .5f) report.runSpeed = Mathf.Max(report.runSpeed, liveMovement.PlanarVelocity.magnitude);
            if (stage == 7 && Time.frameCount > resetStart && Time.frameCount <= resetStart + 2)
            { report.resetError = Mathf.Max(report.resetError, Mathf.Max(offsetL, offsetR)); sawReset = true; }
            var sample = new MotionSample { time = time, phase = PhaseNames[stage], leftTip = tipL, rightTip = tipR, leftOffset = offsetL, rightOffset = offsetR,
                penetration = penetration, planarSpeed = liveMovement.PlanarVelocity.magnitude, headPosition = liveHair.head.position };
            liveCamera.transform.position = liveHair.head.position + new Vector3(1.2f, .45f, -2.4f);
            liveCamera.transform.LookAt(liveHair.head.position + Vector3.up * .04f);
            if ((Time.frameCount - playStart) % 3 == 0)
            {
                sample.image = imageIndex++; RenderHairFrame(Path.Combine(report.frameDirectory, "frame-" + sample.image.ToString("D4") + ".png"));
            }
            report.motion.Add(sample); report.frames = imageIndex;
            Require(attachment < .001f && report.maximumLengthError < .00001f, "Hair simulation must keep real roots attached and bone lengths unchanged.");
            Require(report.maximumOffset < .35f, "Live motion must stay restrained.");
            if (report.motion.Count % 60 == 0) SavePlayReport();
        }
        catch (Exception e) { FailPlay(e); }
        finally { EditorApplication.QueuePlayerLoopUpdate(); }
    }
    static void CompletePlay()
    {
        Case("status-query-preserves-active-capture", () => Require(statusProbeObserved, "Status query must complete without changing the recording."));
        Case("actual-walk-run-speeds", () => Require(report.walkSpeed > 1.4f && report.walkSpeed < 1.7f && report.runSpeed > 3.5f && report.runSpeed < 3.9f, "Hair must preserve actual configured walking/running behavior."));
        Case("actual-secondary-motion-and-attachment", () => Require(report.maximumOffset > .003f && report.maximumOffset < .35f && report.maximumAttachmentError < .001f && report.maximumLengthError < .00001f, "Actual motion needs measurable sway with pinned roots and unchanged lengths."));
        Case("actual-body-proxy-clearance", () => Require(report.maximumPenetration < .03f, "Body proxy penetration exceeds3cm: " + report.maximumPenetration));
        Case("actual-reset-and-closeup-proof", () => Require(sawReset && report.resetError < .025f && report.frames >= 120, "Reset must discard old motion and close-up proof must cover the full sequence."));
        FinishReport(); EditorApplication.update -= PlayTick; EditorApplication.isPlaying = false;
    }
    static void FailPlay(Exception e)
    {
        if (report == null) report = new Report { mode = "play", date = DateTime.Now.ToString("s") };
        report.errors.Add(e.GetBaseException().ToString()); report.passed = false; report.status = "failed";
        SavePlayReport(); EditorApplication.update -= PlayTick;
        if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = false; else { CleanupPlay(); SessionState.SetBool(PlayRunning, false); }
    }
    static void SavePlayReport() { Directory.CreateDirectory(Path.GetDirectoryName(ReportPath)); File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true)); }
    static void PlayLog(string message, string stack, LogType type)
    {
        if (report != null && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)) report.errors.Add(type + ": " + message + "\n" + stack);
    }
    static void PlayKeys(params Key[] keys) { if (liveKeyboard != null && liveKeyboard.added) InputSystem.QueueStateEvent(liveKeyboard, new KeyboardState(keys)); }
    static void RenderHairFrame(string path)
    {
        var target = new RenderTexture(640, 512, 24); Texture2D image = null;
        RenderTexture active = RenderTexture.active, prior = liveCamera.targetTexture;
        try
        {
            target.Create(); liveCamera.targetTexture = target; liveCamera.Render(); RenderTexture.active = target;
            image = new Texture2D(640, 512, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 640, 512), 0, 0); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally
        {
            liveCamera.targetTexture = prior; RenderTexture.active = active; target.Release(); UnityEngine.Object.DestroyImmediate(target);
            if (image) UnityEngine.Object.DestroyImmediate(image);
        }
    }
    static void RestorePlaySettings()
    {
        if (!SessionState.GetBool(PlaySettings, false)) return;
        InputSystem.settings.backgroundBehavior = (InputSettings.BackgroundBehavior)SessionState.GetInt(InputBackground, 0);
        InputSystem.settings.editorInputBehaviorInPlayMode = (InputSettings.EditorInputBehaviorInPlayMode)SessionState.GetInt(EditorInput, 0);
        Time.captureDeltaTime = SessionState.GetFloat(CaptureDelta, 0f); Application.runInBackground = SessionState.GetBool(RunBackground, false); SessionState.SetBool(PlaySettings, false);
    }
    static void CleanupPlay()
    {
        EditorApplication.update -= PlayTick; Application.logMessageReceived -= PlayLog;
        if (liveKeyboard != null && liveKeyboard.added) InputSystem.RemoveDevice(liveKeyboard);
        liveKeyboard = null; if (oldKeyboard != null && oldKeyboard.added) oldKeyboard.MakeCurrent(); RestorePlaySettings();
        if (liveFloor) UnityEngine.Object.DestroyImmediate(liveFloor);
        if (liveCamera)
        {
            liveCamera.transform.SetPositionAndRotation(oldCameraPosition, oldCameraRotation); liveCamera.transform.localScale = oldCameraScale;
            liveCamera.fieldOfView = oldFieldOfView; liveCamera.targetTexture = oldTarget; liveCamera.enabled = oldCameraEnabled;
        }
        if (cameraFollow) cameraFollow.enabled = oldFollow; if (cameraGuard) cameraGuard.enabled = oldGuard; playInitialized = false;
    }
}
