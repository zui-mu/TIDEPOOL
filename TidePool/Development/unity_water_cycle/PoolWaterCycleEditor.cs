using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using AbstractOcclusion.WebGpuWater;

public static class PoolWaterCycleEditor
{
    const string Source = "Assets/_Game/Scenes/PoolHall_Dream_01.unity";
    const string Target = "Assets/_Game/Scenes/PoolHall_WaterCycle_01.unity";
    const string Root = "Assets/_Game/WaterCycle";
    const string Out = "D:/awestlakeselfstudy/unity_water_cycle";
    static double nextStatus, nextStage;
    static bool releaseKey;
    [InitializeOnLoadMethod]
    static void Init()
    {
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        Directory.CreateDirectory(Out);
        File.WriteAllText(Out + "/ready.txt", DateTime.Now.ToString("s"));
        nextStage = EditorApplication.timeSinceStartup + 4;
    }
    static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        string commandPath = Out + "/command.txt";
        try
        {
            if (File.Exists(commandPath))
            {
                string command = File.ReadAllText(commandPath).Trim(); File.Delete(commandPath);
                if (command == "build") Build();
                else if (command == "test") BeginTest();
                else if (command == "stop") { SessionState.SetInt("WaterCycleTest", 0); EditorApplication.isPlaying = false; }
                else if (command == "restore") Restore();
                else if (command == "tune") Tune();
                else if (command == "status") Status();
                else if (command == "play") EditorApplication.isPlaying = true;
                else throw new InvalidOperationException(command);
            }
            if (EditorApplication.timeSinceStartup > nextStatus) { nextStatus = EditorApplication.timeSinceStartup + 5; Status(); }
            if (EditorApplication.isPlaying)
            {
                if (releaseKey) { InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState()); releaseKey = false; }
                if (SessionState.GetInt("WaterCycleTest", 0) > 0 && EditorApplication.timeSinceStartup >= nextStage) TestStage();
            }
        }
        catch (Exception e)
        {
            SessionState.SetInt("WaterCycleTest", 0);
            File.WriteAllText(Out + "/error.txt", e.ToString()); Debug.LogException(e);
        }
    }
    [MenuItem("Tools/TidePool/Create Water Cycle Study")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play first.");
        if (File.Exists(Target)) throw new InvalidOperationException("Water cycle study already exists; refusing overwrite.");
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != Source) throw new InvalidOperationException("Open the existing cold Dream hall first.");
        // Save the user's current hall, then create an independent scene/material variant.
        EditorSceneManager.SaveScene(scene);
        if (!EditorSceneManager.SaveScene(scene, Target, true)) throw new IOException("Scene copy failed.");
        scene = EditorSceneManager.OpenScene(Target, OpenSceneMode.Single);
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(Root + "/SG_WetTiles_WaterCycle.shadergraph");
        if (!shader || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Wet tile shader is not ready.");
        Material material = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/HallDream/M_WetTiles_Dream.mat"));
        material.name = "M_WetTiles_WaterCycle"; material.shader = shader;
        material.SetFloat("_BaseWetness", .48f); material.SetFloat("_WetRange", 6f); material.SetFloat("_Wetness", 1f);
        AssetDatabase.CreateAsset(material, Root + "/M_WetTiles_WaterCycle.mat");
        foreach (string n in new[] { "Deck_South", "Deck_North", "Deck_West", "Deck_East", "Passage_Floor" })
        {
            Renderer r = Need(n).GetComponent<Renderer>(); r.sharedMaterial = material; r.SetPropertyBlock(null);
        }
        foreach (var old in UnityEngine.Object.FindObjectsByType<PoolWetnessController>(FindObjectsSortMode.None)) old.enabled = false;
        var console = Need("WaterControlSwitch");
        var oldSwitch = console.GetComponent<WaterControlSwitch>(); if (oldSwitch) oldSwitch.enabled = false;
        Transform water = Need("Frame (WaterVolume)").transform;
        water.position = new Vector3(0, -.3f, 16);
        var volume = water.GetComponent<WaterVolume>();
        var serialized = new SerializedObject(volume);
        // Keep this footprint fixed before the simulation starts. Below deck level only the
        // pool opening is visible; above deck level the very same continuous surface fills the hall.
        serialized.FindProperty("volumeExtent").vector3Value = new Vector3(20.2f, 24f, 26.2f);
        serialized.FindProperty("enableLargeBodyWindow").boolValue = false;
        serialized.FindProperty("configureCamera").boolValue = false;
        serialized.FindProperty("targetCamera").objectReferenceValue = Camera.main;
        serialized.FindProperty("waterFogSettings.fogDensity").floatValue = .12f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        var dry = new GameObject("Dry_Air_Pocket");
        dry.transform.position = new Vector3(8, 10, 39.5f);
        var exclusion = dry.AddComponent<WaterExclusionVolume>();
        exclusion.shape = WaterExclusionVolume.Shape.Box;
        exclusion.size = new Vector3(4, 24, 4);
        exclusion.castsSunShadow = false; exclusion.drawWaterWalls = true;
        exclusion.edgeColor = new Color(.1f, .23f, .29f); exclusion.edgeIntensity = .15f; exclusion.edgeSpread = .06f;
        exclusion.wallScatterBoost = 1.1f;
        var es = new SerializedObject(exclusion);
        Shader wallShader = Shader.Find("AbstractOcclusion/WebGpuWater/WaterExclusionWall");
        if (!wallShader) throw new InvalidOperationException("Water wall shader is missing.");
        es.FindProperty("wallShader").objectReferenceValue = wallShader; es.ApplyModifiedPropertiesWithoutUndo();
        var cycle = console.AddComponent<PoolWaterCycle>();
        cycle.player = Need("Player").transform; cycle.waterFrame = water; cycle.dryRegion = dry.transform;
        var guard = Camera.main.gameObject.AddComponent<DryRegionCameraGuard>(); guard.player = cycle.player; guard.dryRegion = exclusion;
        // The dry area is air and an existing walkable floor, with no solid transparent box.
        Application.runInBackground = true;
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Restore(); Validate();
        File.WriteAllText(Out + "/built.txt", Target);
    }
    static GameObject Need(string n)
    {
        GameObject o = GameObject.Find(n); if (!o) throw new InvalidOperationException("Missing object: " + n); return o;
    }
    static PoolWaterCycle Cycle => Need("WaterControlSwitch").GetComponent<PoolWaterCycle>();
    static void Tune()
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Tune in Edit mode.");
        var s=new SerializedObject(Need("Frame (WaterVolume)").GetComponent<WaterVolume>());
        s.FindProperty("waterFogSettings.fogDensity").floatValue=.12f;
        s.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Validate();
    }
    static void Restore()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play before restoring.");
        if (SceneManager.GetActiveScene().path != Target) EditorSceneManager.OpenScene(Target, OpenSceneMode.Single);
        Type viewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        EditorWindow.GetWindow(viewType).maximized = false;
        Camera.main.GetComponent<CameraFollow>().enabled = true;
        Camera.main.GetComponent<DryRegionCameraGuard>().enabled = true;
        SceneView view = EditorWindow.GetWindow<SceneView>();
        view.LookAt(new Vector3(0, 5, 17), Quaternion.Euler(20, -25, 0), 35f);
        Selection.activeGameObject = Need("WaterControlSwitch"); view.Focus();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene()); Status();
    }
    static void Status()
    {
        var c = UnityEngine.Object.FindFirstObjectByType<PoolWaterCycle>();
        var cam = Camera.main;
        File.WriteAllText(Out + "/status.json", JsonUtility.ToJson(new StatusData {
            scene=SceneManager.GetActiveScene().path, time=DateTime.Now.ToString("s"), playing=EditorApplication.isPlaying,
            phase=c ? c.CurrentPhase.ToString() : "none", waterHeight=c ? c.WaterHeight : -999, elapsed=c ? c.Elapsed : 0,
            player=c && c.player ? c.player.position : Vector3.zero, camera=cam ? cam.transform.position : Vector3.zero,
            testStage=SessionState.GetInt("WaterCycleTest",0), lightmaps=LightmapSettings.lightmaps.Length
        },true));
    }
    [Serializable] class StatusData { public string scene,time,phase; public bool playing; public float waterHeight,elapsed; public Vector3 player,camera; public int testStage,lightmaps; }
    static void Validate()
    {
        var roots = SceneManager.GetActiveScene().GetRootGameObjects();
        int missing = roots.SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
        Assert(missing == 0,"No missing scripts");
        Assert(!Need("WaterControlSwitch").GetComponent<WaterControlSwitch>().enabled,"Legacy switch disabled");
        Assert(!Need("PoolWetnessController").GetComponent<PoolWetnessController>().enabled,"Legacy drying disabled");
        Assert(WaterExclusionVolume.Active.Any(v => v.name == "Dry_Air_Pocket"),"Dry exclusion registered");
        var material = Need("Deck_South").GetComponent<Renderer>().sharedMaterial;
        Assert(!ShaderUtil.ShaderHasError(material.shader),"Wet tile shader compiled");
        Assert(material.GetFloat("_WetRange") == 6 && material.GetFloat("_BaseWetness") > .4f,"Permanent damp floor configured");
        Assert(Physics.Raycast(new Vector3(8,2,39.5f),Vector3.down,out RaycastHit hit,3) && Mathf.Abs(hit.point.y)<.05f,"Dry region has walkable floor");
        File.WriteAllText(Out + "/validation.txt", "PASS: independent scene; no missing scripts; old drivers disabled; shader compiled; permanent damp floor; 4x4 dry exclusion and floor.\n" + DateTime.Now.ToString("s"));
    }
    static void Assert(bool condition,string message) { if(!condition) throw new InvalidOperationException("Validation failed: " + message); }
    static void BeginTest()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop before test.");
        Validate(); SessionState.SetBool("WaterCycleCaptureDone",false); SessionState.SetInt("WaterCycleTest",1); EditorApplication.isPlaying=true;
        nextStage=EditorApplication.timeSinceStartup+5;
    }
    static void Step(int stage,double wait) { SessionState.SetInt("WaterCycleTest",stage);nextStage=EditorApplication.timeSinceStartup+wait; }
    static void Press(Key key) { InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(key));releaseKey=true; }
    static void PlayerAt(Vector3 p)
    {
        Transform player=Cycle.player;var cc=player.GetComponent<CharacterController>();cc.enabled=false;player.position=p;cc.enabled=true;
    }
    static void Photo(string name,Vector3 p,Vector3 look)
    {
        Camera cam=Camera.main;cam.GetComponent<CameraFollow>().enabled=false;cam.GetComponent<DryRegionCameraGuard>().enabled=false;
        cam.transform.SetPositionAndRotation(p,Quaternion.LookRotation(look-p));cam.fieldOfView=68;
        Cycle.showHints=false;
        Type viewType=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");EditorWindow.GetWindow(viewType).maximized=true;
        SessionState.SetString("WaterCyclePhoto",name);
    }
    static void Capture() { ScreenCapture.CaptureScreenshot(Out+"/"+SessionState.GetString("WaterCyclePhoto", "test")+".png");Status(); }
    static void TestStage()
    {
        PoolWaterCycle c=Cycle; int stage=SessionState.GetInt("WaterCycleTest",0);
        // CaptureScreenshot writes at end-of-frame. Do not move water/camera in that frame.
        if(new[] {2,4,6,8,9,10}.Contains(stage))
        {
            if(!SessionState.GetBool("WaterCycleCaptureDone",false))
            {
                Capture();SessionState.SetBool("WaterCycleCaptureDone",true);
                nextStage=EditorApplication.timeSinceStartup+1;return;
            }
            SessionState.SetBool("WaterCycleCaptureDone",false);
        }
        if(stage==1)
        {
            PlayerAt(new Vector3(2.5f,1,-3.3f));Assert(c.PlayerInRange(),"Player can reach switch");
            Photo("water-initial",new Vector3(-5,2.4f,-3),new Vector3(1,2.7f,22));Step(2,3);
        }
        else if(stage==2) { Press(Key.E);Step(3,6); }
        else if(stage==3)
        {
            Assert(c.CurrentPhase==PoolWaterCycle.Phase.Draining,"E started draining");
            Assert(c.WaterHeight<-.4f && c.WaterHeight>-3.35f,"Realtime water moves downward");
            // Repeated interaction and jumping must not stop waves / toggle water state.
            Assert(!c.TryInteract(),"Busy sequence ignores repeated interaction");Press(Key.Space);
            Photo("water-draining",new Vector3(-5,2.4f,-3),new Vector3(1,.1f,17));Step(4,3);
        }
        else if(stage==4) { c.Advance(30);Step(5,2); }
        else if(stage==5)
        {
            Assert(c.CurrentPhase==PoolWaterCycle.Phase.Empty && Mathf.Abs(c.WaterHeight+3.35f)<.001f,"Pool drains below actual floor");
            Photo("water-empty",new Vector3(-5,2.4f,-3),new Vector3(1,.1f,17));Step(6,3);
        }
        else if(stage==6) { Press(Key.E);Step(7,5); }
        else if(stage==7)
        {
            Assert(c.CurrentPhase==PoolWaterCycle.Phase.Filling && c.WaterHeight>-3.35f,"Second E starts refill");
            c.Advance(c.refillSeconds-c.Elapsed+12);
            Photo("water-overflow",new Vector3(-5,2.4f,-3),new Vector3(1,2.7f,22));Step(8,3);
        }
        else if(stage==8)
        {
            c.Advance(c.refillSeconds+c.platformFloodSeconds+8-c.Elapsed);
            PlayerAt(new Vector3(8,1,39.5f));
            Photo("water-dry-pocket",new Vector3(8,1.75f,39.3f),new Vector3(-2,1.5f,15));Step(9,4);
        }
        else if(stage==9)
        {
            // One-time monotonicity / endpoints check on the real controller, then render final state.
            float previous=c.WaterHeight;
            while(c.CurrentPhase==PoolWaterCycle.Phase.Filling)
            {
                c.Advance(.1f);Assert(c.WaterHeight>=previous-.0001f,"Fill is monotonic");previous=c.WaterHeight;
            }
            Assert(Mathf.Abs(c.WaterHeight-c.floodedHeight)<.001f,"Hall reaches final flood height");
            Photo("water-final-pocket",new Vector3(8,1.75f,39.3f),new Vector3(-2,2,15));Step(10,4);
        }
        else if(stage==10)
        {
            var guard=Camera.main.GetComponent<DryRegionCameraGuard>();guard.enabled=true;
            Camera.main.GetComponent<CameraFollow>().enabled=true;Step(11,3);
        }
        else if(stage==11)
        {
            var guard=Camera.main.GetComponent<DryRegionCameraGuard>();
            Assert(guard.IsPlayerInside,"Player stands in dry pocket");
            Vector3 p=guard.dryRegion.transform.InverseTransformPoint(Camera.main.transform.position);Vector3 h=guard.dryRegion.size*.5f;
            Assert(Mathf.Abs(p.x)<h.x && Mathf.Abs(p.y)<h.y && Mathf.Abs(p.z)<h.z,"Follow camera stays in dry air");
            Press(Key.R);Step(12,3);
        }
        else if(stage==12)
        {
            Assert(c.CurrentPhase==PoolWaterCycle.Phase.Pool && Mathf.Abs(c.WaterHeight+.3f)<.001f,"R resets water");
            Assert(Vector3.Distance(c.player.position,new Vector3(0,1,-1.8f))<.5f,"R resets player");
            File.WriteAllText(Out+"/test-pass.txt","PASS: real Input System E drain, Space during drain, empty below floor, second E refill, monotonic fill, overflow, ceiling-level flood, walkable dry pocket, camera in dry air, R reset.\n"+DateTime.Now.ToString("s"));
            SessionState.SetInt("WaterCycleTest",0);EditorApplication.isPlaying=false;
        }
        Status();
    }
}
