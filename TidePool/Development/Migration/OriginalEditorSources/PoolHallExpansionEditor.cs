// Project-scoped editor tool. Builds a separate scene; the learning scene stays intact.
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class PoolHallExpansionEditor
{
    const string AssetsRoot = "Assets/_Game/HallExpansion";
    const string ScenePath = "Assets/_Game/Scenes/PoolHall_Expanded_01.unity";
    const string OutputRoot = "D:/awestlakeselfstudy/unity_hall_expansion";
    static double nextTestTime;
    static bool releaseTestKey;

    [InitializeOnLoadMethod]
    static void RegisterLocalCommands()
    {
        EditorApplication.update -= PollLocalCommand;
        EditorApplication.update += PollLocalCommand;
        nextTestTime = EditorApplication.timeSinceStartup + 2;
    }

    static void PollLocalCommand()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        string commandFile = OutputRoot + "/unity-command.txt";
        if (File.Exists(commandFile))
        {
            string command = File.ReadAllText(commandFile).Trim();
            File.Delete(commandFile);
            try
            {
                if(command=="build") Build();
                else if(command=="polish") Polish();
                else if(command=="play") EditorApplication.isPlaying=true;
                else if(command=="near-switch") {
                    GameObject p=Need("Player");CharacterController c=p.GetComponent<CharacterController>();
                    c.enabled=false;p.transform.position=V(2.5f,1,-3.3f);c.enabled=true;
                }
                else if(command=="lower") SetWaterTarget(-1.8f,true);
                else if(command=="raise") SetWaterTarget(-0.3f,false);
                else if(command=="hero") {
                    GameObject p=Need("Player");CharacterController c=p.GetComponent<CharacterController>();
                    c.enabled=false;p.transform.position=V(0,1,-3);c.enabled=true;
                    Type gameView=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                    EditorWindow.GetWindow(gameView).maximized=true;
                }
                else if(command=="editor-view") {
                    Type gameView=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                    EditorWindow.GetWindow(gameView).maximized=false;
                    SceneView view=EditorWindow.GetWindow<SceneView>();
                    view.LookAt(V(0,3,16),Quaternion.Euler(24,-24,0),38f);view.Focus();
                    Selection.activeObject=AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
                    EditorGUIUtility.PingObject(Selection.activeObject);
                }
                else if(command=="snapshot") WriteSnapshot();
                else if(command=="capture") CapturePreview();
                else if(command=="test") {
                    if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play before test.");
                    SessionState.SetInt("HallExpansionTest",1);
                    EditorApplication.isPlaying=true;
                }
                else if(command=="stop") { SessionState.SetInt("HallExpansionTest",0);EditorApplication.isPlaying=false; }
                else throw new InvalidOperationException("Unknown project command: "+command);
            }
            catch(Exception error) { File.WriteAllText(OutputRoot+"/command-error.txt",error.ToString());Debug.LogException(error); }
        }
        if(!EditorApplication.isPlaying)return;
        if(releaseTestKey) {
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());releaseTestKey=false;
        }
        if(EditorApplication.timeSinceStartup<nextTestTime)return;
        int stage=SessionState.GetInt("HallExpansionTest",0);
        try {
            if(stage==1) {
                GameObject p=Need("Player");CharacterController c=p.GetComponent<CharacterController>();
                c.enabled=false;p.transform.position=V(1.4f,1,-2);c.enabled=true;
                SnapshotNamed("initial");PressTestKey(Key.Space);AdvanceTest(2,2);
            } else if(stage==2) {
                SnapshotNamed("after-jump");PressTestKey(Key.E);AdvanceTest(3,5);
            } else if(stage==3) {
                SnapshotNamed("low-water");AdvanceTest(4,9);
            } else if(stage==4) {
                SnapshotNamed("drying");PressTestKey(Key.E);AdvanceTest(5,5);
            } else if(stage==5) {
                SnapshotNamed("restored");
                GameObject p=Need("Player");CharacterController c=p.GetComponent<CharacterController>();
                c.enabled=false;p.transform.position=V(0,1,-3);c.enabled=true;
                AdvanceTest(6,2);
            } else if(stage==6) {
                CapturePreview();SessionState.SetInt("HallExpansionTest",0);
                File.WriteAllText(OutputRoot+"/test-complete.txt",DateTime.Now.ToString("s"));
            }
        } catch(Exception error) {
            SessionState.SetInt("HallExpansionTest",0);File.WriteAllText(OutputRoot+"/test-error.txt",error.ToString());Debug.LogException(error);
        }
    }

    static void AdvanceTest(int stage,double delay) {SessionState.SetInt("HallExpansionTest",stage);nextTestTime=EditorApplication.timeSinceStartup+delay;}
    static void SetWaterTarget(float height,bool activated) {
        WaterControlSwitch c=Need("WaterControlSwitch").GetComponent<WaterControlSwitch>();
        var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
        typeof(WaterControlSwitch).GetField("isActivated",flags).SetValue(c,activated);
        typeof(WaterControlSwitch).GetField("targetHeight",flags).SetValue(c,height);
    }
    static void PressTestKey(Key key) {InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(key));releaseTestKey=true;}
    static void SnapshotNamed(string name) {WriteSnapshot();File.Copy(OutputRoot+"/validation-latest.json",OutputRoot+"/validation-"+name+".json",true);}

    static void Polish()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play before adjusting the saved scene.");
        if(SceneManager.GetActiveScene().path!=ScenePath)throw new InvalidOperationException("Open the expanded scene.");
        GameObject camera=Need("Main Camera");
        camera.transform.rotation=Quaternion.Euler(10,0,0);
        camera.transform.position=V(0,2.64f,-8.91f);
        camera.GetComponent<Camera>().fieldOfView=65;
        SerializedObject follow=new SerializedObject(camera.GetComponent<CameraFollow>());
        follow.FindProperty("distance").floatValue=6f;
        follow.FindProperty("lookHeight").floatValue=0.6f;
        follow.ApplyModifiedPropertiesWithoutUndo();
        Component volume=Need("Frame (WaterVolume)").GetComponents<Component>().First(c=>c.GetType().Name=="WaterVolume");
        SerializedObject water=new SerializedObject(volume);
        water.FindProperty("windWaveSettings.windSpeed").floatValue=1f;
        water.FindProperty("windWaveSettings.waveHeightMeters").floatValue=0.015f;
        water.FindProperty("waterFogSettings.fogDensity").floatValue=0.06f;
        water.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        WriteSnapshot();
    }

    [MenuItem("Tools/TidePool/Build Expanded Hall")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play mode before building the hall.");
        Scene source = SceneManager.GetActiveScene();
        if (!source.path.EndsWith("Park_Entrance_01.unity"))
            throw new InvalidOperationException("Open Park_Entrance_01 before the first build.");
        if (File.Exists(ScenePath))
            throw new InvalidOperationException("Expanded scene already exists; refusing to overwrite it.");
        if (!EditorSceneManager.SaveScene(source)) throw new IOException("Source save failed.");
        if (!EditorSceneManager.SaveScene(source, ScenePath, true)) throw new IOException("Scene copy failed.");
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Material originalDeck = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Shaders/M_WetTiles.mat");
        Shader hallShader = AssetDatabase.LoadAssetAtPath<Shader>(AssetsRoot + "/SG_WetTiles_Hall.shadergraph");
        if (!originalDeck || !hallShader) throw new InvalidOperationException("Hall shader or source material missing.");
        Material deck = new Material(originalDeck) { name = "M_WetTiles_Hall", shader = hallShader };
        deck.SetFloat("_TileSize", 0.4f);
        deck.SetFloat("_WetRange", 3f);
        deck.SetColor("_DryColor", new Color(0.83f, 0.86f, 0.85f));
        deck.SetColor("_WetColor", new Color(0.61f, 0.72f, 0.73f));
        deck.SetColor("_GroutColor", new Color(0.49f, 0.56f, 0.55f));
        AssetDatabase.CreateAsset(deck, AssetsRoot + "/M_WetTiles_Hall.mat");
        Material wall = Lit("M_WarmWhite", new Color(0.89f, 0.89f, 0.84f), 0.18f);
        Material coping = Lit("M_Coping", new Color(0.81f, 0.86f, 0.83f), 0.4f);
        Material metal = Lit("M_BrushedMetal", new Color(0.6f, 0.68f, 0.68f), 0.65f, 0.8f);
        Material pool = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/M_Pool.mat"));
        pool.name = "M_Pool_Hall";
        pool.SetColor("_BaseColor", new Color(0.6f, 0.81f, 0.79f));
        pool.SetFloat("_Smoothness", 0.45f);
        AssetDatabase.CreateAsset(pool, AssetsRoot + "/M_Pool_Hall.mat");

        Set("Deck_South", V(0,-0.25f,-4.5f), V(40,0.5f,11), deck);
        Set("Deck_North", V(0,-0.25f,36.5f), V(40,0.5f,11), deck);
        Set("Deck_West", V(-15.5f,-0.25f,16), V(9,0.5f,30), deck);
        Set("Deck_East", V(15.5f,-0.25f,16), V(9,0.5f,30), deck);
        Set("Pool_Floor", V(0,-3.25f,16), V(22,0.5f,30), pool);
        Set("Pool_Wall_South", V(0,-1.75f,0.75f), V(22,2.5f,0.5f), pool);
        Set("Pool_Wall_North", V(0,-1.75f,31.25f), V(22,2.5f,0.5f), pool);
        Set("Pool_Wall_West", V(-11.25f,-1.75f,16), V(0.5f,2.5f,31), pool);
        Set("Pool_Wall_East", V(11.25f,-1.75f,16), V(0.5f,2.5f,31), pool);

        Set("Wall_West", V(-20.2f,5.5f,16), V(0.4f,11,52.8f), wall);
        Set("Wall_East", V(20.2f,5.5f,16), V(0.4f,11,52.8f), wall);
        Set("Wall_South", V(0,5.5f,-10.2f), V(40.8f,11,0.4f), wall);
        // Reuse the original north wall as the left portion of an offset doorway.
        Set("Wall_North", V(-7,5.5f,42.2f), V(26,11,0.4f), wall);

        Transform architecture = Need("Architecture").transform;
        GameObject additions = new GameObject("Hall_Details");
        additions.transform.SetParent(architecture, false);
        Transform root = additions.transform;
        Box("NorthWall_Right", V(15,5.5f,42.2f), V(10,11,0.4f), wall, root);
        Box("NorthWall_DoorLintel", V(8,7.1f,42.2f), V(4,7.8f,0.4f), wall, root);

        // Sparse repeating columns: nine-metre circulation strips remain open beside the pool.
        foreach (float x in new[] {-16.5f,16.5f})
        foreach (float z in new[] {4f,16f,28f})
        {
            string suffix = (x < 0 ? "West_" : "East_") + z;
            Box("Column_" + suffix, V(x,5.5f,z), V(1.1f,11,1.5f), wall, root);
            Box("ColumnPlinth_" + suffix, V(x,0.075f,z), V(1.25f,0.15f,1.65f), coping, root);
        }
        // A real roof with three open skylight bands; no opaque glass blocking sunlight.
        Box("Roof_West", V(-16.25f,11.2f,16), V(7.5f,0.4f,52), wall, root);
        Box("Roof_East", V(16.25f,11.2f,16), V(7.5f,0.4f,52), wall, root);
        Box("Roof_Front", V(0,11.2f,-2), V(25,0.4f,16), wall, root);
        Box("Roof_Middle", V(0,11.2f,16), V(25,0.4f,12), wall, root);
        Box("Roof_Back", V(0,11.2f,31), V(25,0.4f,10), wall, root);
        Box("Roof_End", V(0,11.2f,40.5f), V(25,0.4f,3), wall, root);
        foreach (float z in new[] {6f,10f,22f,26f,36f,39f})
            Box("SkylightLip_"+z, V(0,11.47f,z), V(25.5f,0.14f,0.15f), coping, root);

        // A small, low passage gives the far wall a readable destination.
        Box("Passage_Floor", V(8,-0.25f,46), V(4,0.5f,8), deck, root);
        Box("Passage_West", V(5.8f,1.6f,46), V(0.4f,3.2f,8), wall, root);
        Box("Passage_East", V(10.2f,1.6f,46), V(0.4f,3.2f,8), wall, root);
        Box("Passage_Ceiling", V(8,3.4f,46), V(4.8f,0.4f,8), wall, root);
        Box("Passage_End", V(8,1.6f,50.2f), V(4.8f,3.2f,0.4f), wall, root);

        // Broad coping and a real walkable staircase, each rise below the controller's step offset.
        Box("Coping_South", V(0,0.04f,0.88f), V(22.5f,0.08f,0.24f), coping, root);
        Box("Coping_North", V(0,0.04f,31.12f), V(22.5f,0.08f,0.24f), coping, root);
        Box("Coping_West", V(-11.12f,0.04f,16), V(0.24f,0.08f,30), coping, root);
        Box("Coping_East", V(11.12f,0.04f,16), V(0.24f,0.08f,30), coping, root);
        GameObject stairs = new GameObject("Pool_Entry_Stairs");
        stairs.transform.SetParent(root, false);
        for (int i=0;i<12;i++)
        {
            float top = -0.25f*i;
            float h = top + 3f;
            Box("Step_"+i.ToString("00"), V(8,-3f+h*0.5f,1.2f+i*0.4f), V(2.4f,h,0.4f), coping, stairs.transform);
        }
        foreach (float x in new[] {6.65f,9.35f})
        {
            Rod("Stair_Handrail", V(x,0.95f,0.65f), V(x,-1.8f,5.45f),0.035f,metal,stairs.transform);
            Rod("Stair_Post_Top",V(x,0,0.7f),V(x,0.95f,0.7f),0.035f,metal,stairs.transform);
            Rod("Stair_Post_Bottom",V(x,-3,5.45f),V(x,-1.8f,5.45f),0.035f,metal,stairs.transform);
        }

        GameObject frame = Need("Frame (WaterVolume)");
        frame.transform.position = V(0,-0.3f,16);
        Component volume = frame.GetComponents<Component>().First(c=>c.GetType().Name=="WaterVolume");
        SerializedObject water = new SerializedObject(volume);
        water.FindProperty("volumeExtent").vector3Value = V(11,2.7f,15);
        water.FindProperty("rippleSettings.rippleRadius").floatValue = 0.12f;
        water.FindProperty("rippleSettings.rippleStrength").floatValue = 0.018f;
        water.FindProperty("windWaveSettings.waveHeightMeters").floatValue = 0.025f;
        water.FindProperty("waterFogSettings.fogDensity").floatValue = 0.12f;
        water.ApplyModifiedPropertiesWithoutUndo();

        GameObject player = Need("Player");
        player.transform.position = V(0,1,-3);
        Set("WaterControlSwitch",V(2.5f,0.5f,-1.5f),V(0.65f,1,0.65f),coping);
        GameObject camera = Need("Main Camera");
        camera.transform.position = V(0,4,-9);
        camera.transform.rotation = Quaternion.Euler(25,0,0);
        camera.GetComponent<Camera>().farClipPlane = 180f;
        camera.GetComponent<Camera>().backgroundColor = new Color(0.8f,0.86f,0.88f);
        Light sun = Need("Sun").GetComponent<Light>();
        sun.transform.rotation = Quaternion.Euler(55,-25,0);
        sun.color = new Color(1f,0.96f,0.88f);
        sun.intensity = 1.8f;
        sun.shadows = LightShadows.Soft;
        RenderSettings.sun = sun;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.76f,0.84f,0.88f);
        RenderSettings.ambientEquatorColor = new Color(0.6f,0.67f,0.69f);
        RenderSettings.ambientGroundColor = new Color(0.4f,0.46f,0.45f);
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.fog = false;

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = architecture.gameObject;
        if (SceneView.lastActiveSceneView)
            SceneView.lastActiveSceneView.LookAt(V(0,3,15),Quaternion.Euler(36,-30,0),43f);
        WriteSnapshot();
        Debug.Log("Expanded hall created: 40 x 52 x 11 m; pool 22 x 30 m. Source scene preserved.");
    }

    [MenuItem("Tools/TidePool/Write Validation Snapshot")]
    public static void WriteSnapshot()
    {
        GameObject frame = Need("Frame (WaterVolume)");
        Component volume = frame.GetComponents<Component>().First(c=>c.GetType().Name=="WaterVolume");
        SerializedObject water = new SerializedObject(volume);
        GameObject switchGo = Need("WaterControlSwitch");
        GameObject wetGo = Need("PoolWetnessController");
        SerializedObject wet = new SerializedObject(wetGo.GetComponent<PoolWetnessController>());
        var snapshot = new Snapshot {
            scene = SceneManager.GetActiveScene().path,
            isPlaying = EditorApplication.isPlaying,
            waterPosition = frame.transform.position,
            waterExtent = water.FindProperty("volumeExtent").vector3Value,
            playerPosition = Need("Player").transform.position,
            consolePosition = switchGo.transform.position,
            currentWetness = wet.FindProperty("currentWetness").floatValue,
            waterPaused = (bool)volume.GetType().GetField("_paused",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(volume),
            wetRendererCount = wet.FindProperty("deckRenderers").arraySize,
            time = DateTime.Now.ToString("s"),
            missingScripts = SceneManager.GetActiveScene().GetRootGameObjects().Sum(
                g=>g.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)))
        };
        Directory.CreateDirectory(OutputRoot);
        File.WriteAllText(OutputRoot+"/validation-latest.json",JsonUtility.ToJson(snapshot,true));
        Debug.Log("Hall validation snapshot written: " + snapshot.scene);
    }

    [MenuItem("Tools/TidePool/Capture Game Preview")]
    public static void CapturePreview()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Capture in Play mode.");
        WriteSnapshot();
        ScreenCapture.CaptureScreenshot(OutputRoot+"/poolhall-preview.png");
    }

    static Vector3 V(float x,float y,float z) { return new Vector3(x,y,z); }
    static GameObject Need(string name) {
        GameObject g = GameObject.Find(name);
        if (!g) throw new InvalidOperationException("Required object missing: " + name);
        return g;
    }
    static void Set(string name,Vector3 position,Vector3 scale,Material material) {
        GameObject g=Need(name);g.transform.position=position;g.transform.localScale=scale;
        g.transform.rotation=Quaternion.identity;
        if(material)g.GetComponent<Renderer>().sharedMaterial=material;
    }
    static Material Lit(string name,Color color,float smooth,float metallic=0) {
        Material m=new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.name=name;m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smooth);m.SetFloat("_Metallic",metallic);
        AssetDatabase.CreateAsset(m,AssetsRoot+"/"+name+".mat");return m;
    }
    static GameObject Box(string name,Vector3 position,Vector3 size,Material material,Transform parent) {
        GameObject g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;
        g.transform.SetParent(parent,false);g.transform.position=position;g.transform.localScale=size;
        g.GetComponent<Renderer>().sharedMaterial=material;return g;
    }
    static GameObject Rod(string name,Vector3 a,Vector3 b,float radius,Material material,Transform parent) {
        GameObject g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name=name;
        g.transform.SetParent(parent,false);g.transform.position=(a+b)*0.5f;
        g.transform.up=(b-a).normalized;g.transform.localScale=V(radius*2,Vector3.Distance(a,b)*0.5f,radius*2);
        g.GetComponent<Renderer>().sharedMaterial=material;return g;
    }
    [Serializable] class Snapshot {
        public string scene,time; public bool isPlaying,waterPaused;
        public Vector3 waterPosition,waterExtent,playerPosition,consolePosition;
        public float currentWetness; public int wetRendererCount,missingScripts;
    }
}
