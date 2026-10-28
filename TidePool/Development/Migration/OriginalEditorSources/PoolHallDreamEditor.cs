// Scene-scoped tools for the cold daylight architectural study.
// All generated assets live in HallDream; the expanded hall is copied, never rebuilt in place.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class PoolHallDreamEditor
{
    const string Source = "Assets/_Game/Scenes/PoolHall_Expanded_01.unity";
    const string Target = "Assets/_Game/Scenes/PoolHall_Dream_01.unity";
    const string Root = "Assets/_Game/HallDream";
    const string Out = "D:/awestlakeselfstudy/unity_hall_expansion";
    static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
    static Transform architecture;
    static double nextStatus;
    static int captureStage;
    static double captureAt;
    static string captureName;
    static Vector3 capturePosition, captureLook;

    [InitializeOnLoadMethod]
    static void Initialize()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        Lightmapping.bakeCompleted -= BakeCompleted;
        Lightmapping.bakeCompleted += BakeCompleted;
        Directory.CreateDirectory(Out);
        File.WriteAllText(Out + "/dream-ready.txt", DateTime.Now.ToString("s"));
        string pending=SessionState.GetString("DreamCapture", "");
        if (!string.IsNullOrEmpty(pending))
        {
            CaptureRequest request=JsonUtility.FromJson<CaptureRequest>(pending);
            captureName=request.name;capturePosition=request.position;captureLook=request.look;
            captureStage=1;captureAt=EditorApplication.timeSinceStartup+3;
        }
    }

    static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        string file = Out + "/dream-command.txt";
        if (File.Exists(file))
        {
            string command = File.ReadAllText(file).Trim(); File.Delete(file);
            try
            {
                if (command == "build") Build();
                else if (command == "polish") Polish();
                else if (command == "finish") Finish();
                else if (command == "bake") Bake();
                else if (command == "status") Status();
                else if (command == "play") EditorApplication.isPlaying = true;
                else if (command == "stop") { captureStage = 0; EditorApplication.isPlaying = false; }
                else if (command == "capture-main") Capture("dream-main", V(-4,2.1f,1), V(1,6,25));
                else if (command == "capture-side") Capture("dream-side", V(-17.5f,1.7f,5), V(-9,3.8f,33));
                else if (command == "capture-entry") Capture("dream-entry", V(0,1.7f,-9.3f), V(0,3.6f,24));
                else if (command == "capture-game") Capture("dream-game", Vector3.zero, Vector3.zero);
                else if (command == "restore") Restore();
                else if (command == "validate") Validate();
                else if (command == "reflections") BakeReflections();
                else throw new InvalidOperationException("Unknown dream hall command: " + command);
            }
            catch (Exception e) { File.WriteAllText(Out + "/dream-error.txt", e.ToString()); Debug.LogException(e); }
        }
        if (EditorApplication.timeSinceStartup > nextStatus)
        {
            nextStatus = EditorApplication.timeSinceStartup + 8;
            Status();
        }
        if (captureStage != 0 && EditorApplication.timeSinceStartup >= captureAt && EditorApplication.isPlaying)
        {
            Camera cam = Camera.main;
            if (captureStage == 1)
            {
                var follow = cam.GetComponent<CameraFollow>();
                if(captureName!="dream-game")
                {
                    if (follow) follow.enabled = false;
                    cam.transform.SetPositionAndRotation(capturePosition, Quaternion.LookRotation(captureLook - capturePosition));
                    cam.fieldOfView = 65;
                }
                else if(follow) {follow.enabled=true;}
                Type viewType=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                EditorWindow.GetWindow(viewType).maximized=true;
                Application.runInBackground=true;
                captureStage = 2; captureAt = EditorApplication.timeSinceStartup + 3;
            }
            else
            {
                ScreenCapture.CaptureScreenshot(Out + "/" + captureName + ".png");
                captureStage = 0;
                SessionState.SetString("DreamCapture", "");
                File.WriteAllText(Out + "/capture-requested.txt", captureName + " " + DateTime.Now.ToString("s"));
            }
        }
    }

    [MenuItem("Tools/TidePool/Build Cold Dream Hall")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play first.");
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != Source) throw new InvalidOperationException("Open PoolHall_Expanded_01 first; current: " + scene.path);
        if (File.Exists(Target)) throw new InvalidOperationException("Dream scene already exists. Refusing overwrite.");
        // Save a copy containing the user's current edits, without changing the source scene file.
        if (!EditorSceneManager.SaveScene(scene, Target, true)) throw new IOException("Scene copy failed.");
        scene = EditorSceneManager.OpenScene(Target, OpenSceneMode.Single);
        Directory.CreateDirectory(Root + "/Meshes"); Directory.CreateDirectory(Root + "/Textures");
        AssetDatabase.Refresh();

        foreach (string n in new[] {"Wall_West","Wall_East","Wall_North","Wall_South"}) Need(n).SetActive(false);
        Transform oldDetails = Need("Hall_Details").transform;
        foreach (Transform t in oldDetails)
            if (t.name.StartsWith("Roof_") || t.name.StartsWith("Skylight") || t.name.StartsWith("Column") || t.name.StartsWith("NorthWall") || t.name.StartsWith("Passage_")) t.gameObject.SetActive(false);
        architecture = new GameObject("Dream_Architecture").transform;
        architecture.SetParent(Need("Architecture").transform, false);

        Texture2D tile = TileTexture("Tile_Albedo", false);
        Texture2D normal = TileTexture("Tile_Normal", true);
        Material plaster = Lit("M_ColdPlaster", C(.83f,.86f,.88f), .16f);
        Material ceramic = Lit("M_WhiteCeramic", C(.9f,.94f,.95f), .38f);
        ceramic.SetTexture("_BaseMap",tile); ceramic.SetTextureScale("_BaseMap",Vector2.one/0.16f);
        ceramic.SetTexture("_BumpMap",normal); ceramic.SetTextureScale("_BumpMap",Vector2.one/0.16f);
        ceramic.SetFloat("_BumpScale", .24f); ceramic.EnableKeyword("_NORMALMAP");
        Material pool = new Material(ceramic) { name = "M_PoolCeramic" };
        pool.SetColor("_BaseColor", C(.68f,.84f,.87f)); pool.SetFloat("_Smoothness", .43f);
        pool.SetTextureScale("_BaseMap",Vector2.one/.2f); pool.SetTextureScale("_BumpMap",Vector2.one/.2f);
        AssetDatabase.CreateAsset(pool, Root + "/M_PoolCeramic.mat");
        Material coping = Lit("M_PaleCoping", C(.79f,.86f,.88f), .4f);
        Material metal = Lit("M_SatinSteel", C(.64f,.72f,.77f), .64f, .85f);
        Material deck = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/HallExpansion/M_WetTiles_Hall.mat")) {name="M_WetTiles_Dream"};
        deck.SetColor("_DryColor", C(.79f,.84f,.87f)); deck.SetColor("_WetColor", C(.53f,.67f,.73f));
        deck.SetColor("_GroutColor", C(.64f,.70f,.74f)); deck.SetFloat("_TileSize",.45f); deck.SetFloat("_WetRange",2.6f);
        AssetDatabase.CreateAsset(deck, Root + "/M_WetTiles_Dream.mat");
        foreach (string n in new[] {"Deck_South","Deck_North","Deck_West","Deck_East"}) Need(n).GetComponent<Renderer>().sharedMaterial = deck;
        foreach (string n in new[] {"Pool_Floor","Pool_Wall_South","Pool_Wall_North","Pool_Wall_West","Pool_Wall_East"})
        { GameObject g = Need(n); ReplaceBoxMesh(g, .015f); g.GetComponent<Renderer>().sharedMaterial=pool; }
        foreach (Transform t in oldDetails.GetComponentsInChildren<Transform>(true))
        {
            if(t.name.StartsWith("Coping_") || t.name.StartsWith("Step_")) { ReplaceBoxMesh(t.gameObject,.018f); t.GetComponent<Renderer>().sharedMaterial=coping; }
            if(t.name.StartsWith("Stair_")) t.GetComponent<Renderer>().sharedMaterial=metal;
        }
        Need("WaterControlSwitch").GetComponent<Renderer>().sharedMaterial = coping;

        // Outer shell and lower ceramic wainscot. The eye reads a tall central nave and low side aisles.
        Wall("West",V(-20.25f,9,16),V(.5f,18,52.5f),plaster,ceramic);
        Wall("East",V(20.25f,9,16),V(.5f,18,52.5f),plaster,ceramic);
        Wall("South",V(0,9,-10.25f),V(40.5f,18,.5f),plaster,ceramic);
        // Offset opening on the right; most of the end wall stays uninterrupted.
        Wall("North_Left",V(-7.25f,9,42.25f),V(25.5f,18,.5f),plaster,ceramic);
        Wall("North_Right",V(15.25f,9,42.25f),V(9.5f,18,.5f),plaster,ceramic);
        Box("North_Lintel",V(8,10.6f,42.25f),V(5,14.8f,.5f),plaster,.025f);

        foreach(float x in new[] {-13.5f,13.5f})
        {
            // Continuous low roof creates a genuinely different space alongside the pool.
            Box("Side_Aisle_Roof",V(Mathf.Sign(x)*16.75f,6.8f,16),V(6.5f,.4f,52),plaster,.025f);
            foreach(float z in new[] {-1f,8f,17f,26f,35f})
            {
                Box("Pier_Base",V(x,1.5f,z),V(1.1f,3,1.7f),ceramic,.04f);
                Box("Pier_Upper",V(x,4.75f,z),V(1.1f,3.5f,1.7f),plaster,.04f);
                Box("Arcade_Header",V(x,6.15f,z+4.5f),V(1.1f,.9f,7.4f),plaster,.035f);
            }
            // Clerestory walls above the aisle. Left gets one generous high window.
            if(x<0)
            {
                Box("Clerestory_Left_Front",V(x,12.4f,0),V(.4f,11.2f,20),plaster,.02f);
                Box("Clerestory_Left_Back",V(x,12.4f,32),V(.4f,11.2f,20),plaster,.02f);
                Box("Clerestory_Sill",V(x,8.4f,16),V(.4f,3.2f,12),plaster,.02f);
                Box("Clerestory_Head",V(x,17,16),V(.4f,2,12),plaster,.02f);
            }
            else Box("Clerestory_Right",V(x,12.4f,16),V(.4f,11.2f,52),plaster,.02f);
        }
        // Offset roof aperture: one large luminous opening, rather than evenly spaced strips.
        // Main void x[-8,3], z[12,30].
        Box("High_Roof_West",V(-10.75f,18.25f,16),V(5.5f,.5f,52),plaster,.03f);
        Box("High_Roof_East",V(8.25f,18.25f,16),V(10.5f,.5f,52),plaster,.03f);
        Box("High_Roof_Front",V(-2.5f,18.25f,1),V(11,.5f,22),plaster,.03f);
        Box("High_Roof_Back",V(-2.5f,18.25f,36),V(11,.5f,12),plaster,.03f);
        Box("Aperture_Reveal_West",V(-8.1f,18.75f,21),V(.2f,1,18.4f),coping,.015f);
        Box("Aperture_Reveal_East",V(3.1f,18.75f,21),V(.2f,1,18.4f),coping,.015f);
        Box("Aperture_Reveal_Front",V(-2.5f,18.75f,11.9f),V(11,1,.2f),coping,.015f);
        Box("Aperture_Reveal_Back",V(-2.5f,18.75f,30.1f),V(11,1,.2f),coping,.015f);

        // Low entrance with shoulders framing the reveal. All routes keep player-size clearance.
        Box("Entry_Ceiling",V(0,3.25f,-7.2f),V(9,.5f,5.6f),plaster,.03f);
        Wall("Entry_Left",V(-4.75f,1.5f,-7.2f),V(.5f,3,5.6f),plaster,ceramic);
        Wall("Entry_Right",V(4.75f,1.5f,-7.2f),V(.5f,3,5.6f),plaster,ceramic);

        // A partial-height off-axis volume hides the destination; the aisle continues behind it.
        Wall("Far_Offset_Screen",V(-3.5f,3.5f,36.5f),V(13,7,.65f),plaster,ceramic);
        Box("Far_Screen_Return",V(-9.7f,3.5f,38),V(.6f,7,3.6f),plaster,.035f);
        Box("Passage_Floor",V(8,-.25f,46.5f),V(5,.5f,9),deck,.01f);
        Wall("Passage_West",V(5.25f,1.6f,46.5f),V(.5f,3.2f,9),plaster,ceramic);
        Wall("Passage_East",V(10.75f,1.6f,46.5f),V(.5f,3.2f,9),plaster,ceramic);
        Box("Passage_Roof",V(8,3.45f,46.5f),V(6,.5f,9),plaster,.02f);
        Wall("Passage_End",V(8,1.6f,51.25f),V(6,3.2f,.5f),plaster,ceramic);
        Box("Passage_Offset_Baffle",V(7,1.6f,47.7f),V(3,3.2f,.35f),ceramic,.02f);

        ConfigureLighting(plaster);
        ConfigureCamera();
        Component waterComponent = Need("Frame (WaterVolume)").GetComponents<Component>().First(c=>c.GetType().Name=="WaterVolume");
        SerializedObject water = new SerializedObject(waterComponent);
        SetFloat(water,"windWaveSettings.windSpeed",.65f);
        SetFloat(water,"windWaveSettings.waveHeightMeters",.012f);
        SetFloat(water,"waterFogSettings.fogDensity",.055f);
        SetBool(water,"reflectionSettings.usePlanarReflection",true);
        SetBool(water,"reflectionSettings.useScreenSpaceReflection",true);
        SetFloat(water,"reflectionSettings.planarResolutionScale",.5f);
        water.ApplyModifiedPropertiesWithoutUndo();

        foreach(Renderer r in SceneObjects<Renderer>())
        {
            if(!r.gameObject.activeInHierarchy || r.transform.IsChildOf(Need("WebGPU Water").transform) || r.gameObject.name=="Player" || r.gameObject.name=="WaterControlSwitch") continue;
            GameObjectUtility.SetStaticEditorFlags(r.gameObject,StaticEditorFlags.ContributeGI | StaticEditorFlags.ReflectionProbeStatic);
            if (r is MeshRenderer meshRenderer)
            {
                meshRenderer.receiveGI = ReceiveGI.Lightmaps;
                meshRenderer.scaleInLightmap = r.gameObject.name.Contains("Roof") ? .5f : 1f;
            }
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject=architecture.gameObject;
        if(SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.LookAt(V(0,6,17),Quaternion.Euler(20,-26,0),38);
        File.WriteAllText(Out+"/dream-built.txt",DateTime.Now.ToString("s"));
        Validate();
    }

    static void ConfigureLighting(Material plaster)
    {
        Light sun=Need("Sun").GetComponent<Light>();
        sun.transform.rotation=Quaternion.Euler(53,-36,0); sun.color=C(.80f,.9f,1); sun.intensity=2.4f;
        sun.shadows=LightShadows.Soft; sun.shadowBias=.035f; sun.shadowNormalBias=.18f;
        sun.lightmapBakeType=LightmapBakeType.Mixed; sun.bounceIntensity=1.3f;
        RenderSettings.sun=sun; RenderSettings.fog=false;
        RenderSettings.ambientMode=AmbientMode.Skybox; RenderSettings.ambientIntensity=.8f;
        Material sky=new Material(Shader.Find("Skybox/Procedural")); sky.name="M_ColdDaySky";
        sky.SetColor("_SkyTint",C(.45f,.57f,.7f)); sky.SetColor("_GroundColor",C(.38f,.43f,.48f));
        sky.SetFloat("_AtmosphereThickness",.55f); sky.SetFloat("_Exposure",1.1f);
        AssetDatabase.CreateAsset(sky,Root+"/M_ColdDaySky.mat"); RenderSettings.skybox=sky;
        RenderSettings.reflectionIntensity=.85f; RenderSettings.defaultReflectionResolution=256;

        // Broad baked sources at actual openings support skylight bounce in the roofed interior.
        Area("Skylight_Bounce",V(-2.5f,17.7f,21),Quaternion.Euler(90,0,0),new Vector2(10,16),C(.72f,.85f,1),16);
        Area("Clerestory_Bounce",V(-13.2f,13,16),Quaternion.Euler(0,90,0),new Vector2(10,5),C(.7f,.84f,1),9);
        Area("Passage_Bounce",V(8,2.9f,49),Quaternion.Euler(90,0,0),new Vector2(3,2),C(.73f,.88f,1),4);

        GameObject probesGO=new GameObject("Hall_Light_Probes");probesGO.transform.SetParent(architecture,false);
        var probes=probesGO.AddComponent<LightProbeGroup>();var points=new List<Vector3>();
        foreach(float x in new[]{-18f,-14.5f,-10f,0f,10f,14.5f,18f})
        foreach(float z in new[]{-7f,-1f,5f,13f,21f,29f,35f,40f})
        foreach(float y in new[]{.4f,1.8f,4.5f})
            if(!(Mathf.Abs(x)<11 && z>1 && z<31 && y<0))points.Add(V(x,y,z));
        probes.probePositions=points.ToArray();
        foreach(float z in new[]{8f,28f})
        {
            GameObject go=new GameObject("Hall_Reflection_"+z);go.transform.SetParent(architecture,false);go.transform.position=V(0,3,z);
            ReflectionProbe p=go.AddComponent<ReflectionProbe>();p.mode=ReflectionProbeMode.Baked;p.resolution=256;
            p.boxProjection=true;p.size=V(40,20,30);p.center=V(0,5,0);p.intensity=.85f;p.blendDistance=5;
            p.clearFlags=ReflectionProbeClearFlags.Skybox;p.cullingMask=~(1<<4);p.nearClipPlane=.1f;p.farClipPlane=110;
        }
        var settings=new LightingSettings();settings.name="LS_Dream_Daylight";
        settings.bakedGI=true;settings.realtimeGI=false;settings.lightmapper=LightingSettings.Lightmapper.ProgressiveCPU;
        settings.lightmapResolution=6;settings.lightmapMaxSize=1024;settings.lightmapPadding=4;
        settings.directSampleCount=32;settings.indirectSampleCount=128;settings.environmentSampleCount=128;
        settings.maxBounces=4;settings.mixedBakeMode=MixedLightingMode.IndirectOnly;
        settings.ao=true;settings.aoMaxDistance=.6f;settings.aoExponentIndirect=.6f;settings.aoExponentDirect=0;
        AssetDatabase.CreateAsset(settings,Root+"/LS_Dream_Daylight.lighting"); Lightmapping.lightingSettings=settings;
        LightmapSettings.lightmapsMode=LightmapsMode.CombinedDirectional;
        GameObject global=Need("Global Volume");Volume v=global.GetComponent<Volume>();
        VolumeProfile profile=ScriptableObject.CreateInstance<VolumeProfile>();profile.name="VP_ColdDream";
        AssetDatabase.CreateAsset(profile,Root+"/VP_ColdDream.asset");v.sharedProfile=profile;
        var tone=profile.Add<Tonemapping>(true);tone.mode.Override(TonemappingMode.ACES);
        var color=profile.Add<ColorAdjustments>(true);color.postExposure.Override(.35f);color.contrast.Override(3);color.saturation.Override(-9);
        var balance=profile.Add<WhiteBalance>(true);balance.temperature.Override(-8);balance.tint.Override(1);
        var bloom=profile.Add<Bloom>(true);bloom.threshold.Override(1.3f);bloom.intensity.Override(.08f);bloom.scatter.Override(.55f);
        foreach(var c in profile.components)AssetDatabase.AddObjectToAsset(c,profile);
    }

    static void ConfigureCamera()
    {
        GameObject p=Need("Player");p.transform.position=V(0,1,-1.8f);
        Camera camera=Need("Main Camera").GetComponent<Camera>();camera.fieldOfView=68;camera.farClipPlane=180;
        camera.transform.rotation=Quaternion.Euler(-3,0,0);
        camera.transform.position=V(0,2.3f,-7.8f);
        var follow=new SerializedObject(camera.GetComponent<CameraFollow>());
        follow.FindProperty("distance").floatValue=6;follow.FindProperty("lookHeight").floatValue=1;
        follow.FindProperty("minPitch").floatValue=-15;follow.ApplyModifiedPropertiesWithoutUndo();
        camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
    }

    static void Polish()
    {
        RequireTarget();if(EditorApplication.isPlaying || Lightmapping.isRunning)throw new InvalidOperationException("Polish in Edit mode after baking.");
        architecture=Need("Dream_Architecture").transform;
        Material plaster=AssetDatabase.LoadAssetAtPath<Material>(Root+"/M_ColdPlaster.mat");
        Material ceramic=AssetDatabase.LoadAssetAtPath<Material>(Root+"/M_WhiteCeramic.mat");
        plaster.SetColor("_BaseColor",C(.86f,.88f,.89f));
        // One continuous column removes the joint halfway up; full-height ceramic suits the pool hall.
        foreach(Transform t in architecture.GetComponentsInChildren<Transform>().ToArray())
        {
            if(t.name=="Pier_Upper")t.gameObject.SetActive(false);
            if(t.name=="Pier_Base")
            {
                t.position=V(t.position.x,3.25f,t.position.z);
                t.GetComponent<MeshFilter>().sharedMesh=BoxMesh(V(1.1f,6.5f,1.7f),.025f);
                t.GetComponent<BoxCollider>().size=V(1.1f,6.5f,1.7f);
            }
            if(t.name.EndsWith("_Plaster"))
            {
                MeshFilter filter=t.GetComponent<MeshFilter>();if(!filter)continue;
                Vector3 size=filter.sharedMesh.bounds.size;
                size.y+=3;
                if(size.x<size.z)size.x-=.024f;else size.z-=.024f;
                t.position-=Vector3.up*1.5f;
                filter.sharedMesh=BoxMesh(size,.012f);t.GetComponent<BoxCollider>().size=size;
            }
            if(t.name.StartsWith("High_Roof_")||t.name=="Side_Aisle_Roof")
            {
                MeshFilter filter=t.GetComponent<MeshFilter>();filter.sharedMesh=BoxMesh(filter.sharedMesh.bounds.size,.002f);
            }
        }
        Light sun=Need("Sun").GetComponent<Light>();sun.color=C(.9f,.95f,1);sun.intensity=2.2f;
        RenderSettings.skybox.SetColor("_SkyTint",C(.5f,.5f,.5f));
        RenderSettings.skybox.SetFloat("_AtmosphereThickness",1.15f);RenderSettings.skybox.SetFloat("_Exposure",1.2f);
        foreach(Light l in architecture.GetComponentsInChildren<Light>())
        {
            l.color=C(.84f,.91f,1);
            l.intensity=l.name=="Skylight_Bounce"?12:l.name=="Clerestory_Bounce"?7:3;
        }
        VolumeProfile profile=Need("Global Volume").GetComponent<Volume>().sharedProfile;
        if(profile.TryGet(out WhiteBalance balance))balance.temperature.Override(-4);
        if(profile.TryGet(out ColorAdjustments color))color.postExposure.Override(.25f);
        Lightmapping.lightingSettings.ao=false;
        EditorUtility.SetDirty(plaster);EditorUtility.SetDirty(RenderSettings.skybox);EditorUtility.SetDirty(profile);EditorUtility.SetDirty(Lightmapping.lightingSettings);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        File.WriteAllText(Out+"/dream-polished.txt",DateTime.Now.ToString("s"));
    }

    static void Finish()
    {
        RequireTarget();if(EditorApplication.isPlaying || Lightmapping.isRunning)throw new InvalidOperationException("Finish in Edit mode.");
        architecture=Need("Dream_Architecture").transform;
        foreach(Transform t in architecture.GetComponentsInChildren<Transform>(true))
        {
            MeshFilter filter=t.GetComponent<MeshFilter>();if(!filter)continue;
            Vector3 size=filter.sharedMesh.bounds.size;
            if(t.name.EndsWith("_Plaster"))
            {
                int axis=size.x<size.z?0:2;
                size[axis]=Mathf.Round(size[axis]*20)/20;
                filter.sharedMesh=BoxMesh(size,0);t.GetComponent<BoxCollider>().size=size;
                Transform cladding=architecture.Find(t.name.Replace("_Plaster","_Ceramic"));
                if(cladding)
                {
                    Vector3 cladSize=cladding.GetComponent<MeshFilter>().sharedMesh.bounds.size;
                    cladSize[axis]=size[axis]+.024f;
                    cladding.GetComponent<MeshFilter>().sharedMesh=BoxMesh(cladSize,0);
                    cladding.GetComponent<BoxCollider>().size=cladSize;
                }
            }
            if(t.name=="Arcade_Header")
            {
                t.position=V(t.position.x,6.35f,t.position.z);size.y=1.3f;
                filter.sharedMesh=BoxMesh(size,0);t.GetComponent<BoxCollider>().size=size;
            }
            if(t.name=="North_Lintel" || t.name.StartsWith("High_Roof_") || t.name=="Side_Aisle_Roof")filter.sharedMesh=BoxMesh(size,0);
            if(t.name=="Pier_Base")
            {
                t.position=V(t.position.x,3.5f,t.position.z);size.y=7;
                filter.sharedMesh=BoxMesh(size,.025f);t.GetComponent<BoxCollider>().size=size;
            }
            if(t.name=="Entry_Ceiling" || t.name=="Entry_Left_Ceramic" || t.name=="Entry_Right_Ceramic")
            {
                // Let the third-person camera leave the low ceiling before the player reaches the pool.
                size.z=3;t.position=V(t.position.x,t.position.y,-8.5f);
                filter.sharedMesh=BoxMesh(size,.012f);t.GetComponent<BoxCollider>().size=size;
            }
        }
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        File.WriteAllText(Out+"/dream-finished.txt",DateTime.Now.ToString("s"));
    }

    static void Area(string name,Vector3 pos,Quaternion rotation,Vector2 size,Color color,float intensity)
    {
        GameObject g=new GameObject(name);g.transform.SetParent(architecture,false);g.transform.SetPositionAndRotation(pos,rotation);
        Light l=g.AddComponent<Light>();l.type=LightType.Rectangle;l.lightmapBakeType=LightmapBakeType.Baked;l.areaSize=size;
        l.color=color;l.intensity=intensity;l.range=70;l.shadows=LightShadows.Soft;
    }

    [MenuItem("Tools/TidePool/Bake Cold Dream Hall")]
    public static void Bake()
    {
        RequireTarget();if(EditorApplication.isPlaying || Lightmapping.isRunning)throw new InvalidOperationException("Exit Play / wait for current bake.");
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        File.WriteAllText(Out+"/dream-bake-started.txt",DateTime.Now.ToString("s"));
        if(!Lightmapping.BakeAsync())throw new InvalidOperationException("Unity did not start the lighting bake.");
    }
    static void BakeCompleted()
    {
        if(SceneManager.GetActiveScene().path!=Target)return;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        File.WriteAllText(Out+"/dream-bake-complete.txt",DateTime.Now.ToString("s"));Status();
    }
    static void BakeReflections()
    {
        RequireTarget();if(EditorApplication.isPlaying || Lightmapping.isRunning)throw new InvalidOperationException("Bake reflections in Edit mode after GI.");
        int i=0;foreach(ReflectionProbe p in SceneObjects<ReflectionProbe>())
            Lightmapping.BakeReflectionProbe(p,Root+"/HallReflection_"+(i++)+".exr");
        AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }
    static void Status()
    {
        var s=new State {scene=SceneManager.GetActiveScene().path,time=DateTime.Now.ToString("s"),playing=EditorApplication.isPlaying,
            baking=Lightmapping.isRunning,lightmapCount=LightmapSettings.lightmaps.Length,progress=Lightmapping.buildProgress};
        Camera cam=Camera.main;
        if(cam)
        {
            s.cameraPosition=cam.transform.position;s.cameraAngles=cam.transform.eulerAngles;
            CameraFollow follow=cam.GetComponent<CameraFollow>();
            if(follow)
            {
                s.followEnabled=follow.enabled;
                var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                s.orbitPitch=(float)typeof(CameraFollow).GetField("pitch",flags).GetValue(follow);
            }
        }
        File.WriteAllText(Out+"/dream-status.json",JsonUtility.ToJson(s,true));
    }
    static void Capture(string name,Vector3 position,Vector3 look)
    {
        RequireTarget();captureName=name;capturePosition=position;captureLook=look;captureStage=1;
        SessionState.SetString("DreamCapture",JsonUtility.ToJson(new CaptureRequest{name=name,position=position,look=look}));
        captureAt=EditorApplication.timeSinceStartup+2;
        if(!EditorApplication.isPlaying)EditorApplication.isPlaying=true;
    }
    static void Restore()
    {
        RequireTarget();if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play first.");
        ConfigureCamera();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Type gameView=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");EditorWindow.GetWindow(gameView).maximized=false;
        Selection.activeGameObject=Need("Dream_Architecture");
        if(SceneView.lastActiveSceneView)SceneView.lastActiveSceneView.LookAt(V(0,6,18),Quaternion.Euler(18,-25,0),36);
        Status();
    }
    static void Validate()
    {
        RequireTarget();var scene=SceneManager.GetActiveScene();
        int missing=scene.GetRootGameObjects().Sum(g=>g.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)));
        var wet=new SerializedObject(Need("PoolWetnessController").GetComponent<PoolWetnessController>());
        var report=new Validation {scene=scene.path,missingScripts=missing,wetRenderers=wet.FindProperty("deckRenderers").arraySize,
            player=Need("Player").transform.position,water=Need("Frame (WaterVolume)").transform.position,
            rendererCount=SceneObjects<Renderer>().Count(r=>r.gameObject.activeInHierarchy),lightmaps=LightmapSettings.lightmaps.Length,
            ceiling=18,sideCeiling=6.6f,entryCeiling=3,time=DateTime.Now.ToString("s")};
        Physics.SyncTransforms();
        var points=new[]{V(-2,2,-2),V(-17,2,13),V(17,2,13),V(0,2,34),V(-2,-.5f,16)};
        report.floorChecks=points.Select(p=>Physics.Raycast(p,Vector3.down,out RaycastHit hit,5)?hit.collider.name+" @ "+hit.point.y.ToString("0.###"):"MISSING FLOOR").ToArray();
        File.WriteAllText(Out+"/dream-validation.json",JsonUtility.ToJson(report,true));Status();
    }
    static void RequireTarget(){if(SceneManager.GetActiveScene().path!=Target)throw new InvalidOperationException("Open the dream scene.");}
    static IEnumerable<T> SceneObjects<T>() where T:Component{return SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<T>(true));}
    static GameObject Need(string name){var g=GameObject.Find(name);if(!g)throw new InvalidOperationException("Missing "+name);return g;}
    static Vector3 V(float x,float y,float z){return new Vector3(x,y,z);}
    static Color C(float r,float g,float b){return new Color(r,g,b,1);}
    static void SetFloat(SerializedObject s,string path,float value){var p=s.FindProperty(path);if(p!=null)p.floatValue=value;}
    static void SetBool(SerializedObject s,string path,bool value){var p=s.FindProperty(path);if(p!=null)p.boolValue=value;}
    static Material Lit(string name,Color color,float smooth,float metal=0)
    {var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smooth);m.SetFloat("_Metallic",metal);AssetDatabase.CreateAsset(m,Root+"/"+name+".mat");return m;}
    static void Wall(string name,Vector3 position,Vector3 size,Material plaster,Material ceramic)
    {
        float bottom=position.y-size.y*.5f;float tileHeight=Mathf.Min(size.y,3);
        Box(name+"_Ceramic",V(position.x,bottom+tileHeight*.5f,position.z),V(size.x,tileHeight,size.z),ceramic,.025f);
        if(size.y>tileHeight)Box(name+"_Plaster",V(position.x,bottom+tileHeight+(size.y-tileHeight)*.5f,position.z),V(size.x,size.y-tileHeight,size.z),plaster,.025f);
    }
    static GameObject Box(string name,Vector3 position,Vector3 size,Material material,float bevel)
    {
        var g=new GameObject(name);g.transform.SetParent(architecture,false);g.transform.position=position;
        g.AddComponent<MeshFilter>().sharedMesh=BoxMesh(size,bevel);
        g.AddComponent<MeshRenderer>().sharedMaterial=material;g.AddComponent<BoxCollider>().size=size;return g;
    }
    static void ReplaceBoxMesh(GameObject g,float bevel)
    {
        Vector3 size=g.transform.localScale;g.GetComponent<MeshFilter>().sharedMesh=BoxMesh(size,bevel);
        g.transform.localScale=Vector3.one;var box=g.GetComponent<BoxCollider>();if(box)box.size=size;
    }
    static Mesh BoxMesh(Vector3 size,float bevel)
    {
        string key=string.Format(System.Globalization.CultureInfo.InvariantCulture,"box_{0:0.###}_{1:0.###}_{2:0.###}_{3:0.###}",size.x,size.y,size.z,bevel);
        if(meshes.TryGetValue(key,out Mesh cached))return cached;
        string path=Root+"/Meshes/"+key+".asset";cached=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(cached){meshes[key]=cached;return cached;}
        Vector3 h=size*.5f;float b=Mathf.Min(bevel,Mathf.Min(h.x,Mathf.Min(h.y,h.z))*.45f);
        var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
        Action<Vector3[],Vector3> face=(corners,n)=>
        {
            // Correct winding from the face normal, then give every plane flat normals and metre UVs.
            if(Vector3.Dot(Vector3.Cross(corners[1]-corners[0],corners[2]-corners[0]),n)<0)Array.Reverse(corners);
            int start=vertices.Count;
            Vector3 abs=new Vector3(Mathf.Abs(n.x),Mathf.Abs(n.y),Mathf.Abs(n.z));
            foreach(Vector3 p in corners){vertices.Add(p);normals.Add(n.normalized);uv.Add(abs.y>=abs.x&&abs.y>=abs.z?new Vector2(p.x,p.z):abs.x>=abs.z?new Vector2(p.z,p.y):new Vector2(p.x,p.y));}
            for(int j=1;j<corners.Length-1;j++){triangles.Add(start);triangles.Add(start+j);triangles.Add(start+j+1);}
        };
        for(int axis=0;axis<3;axis++)for(int sign=-1;sign<=1;sign+=2)
        {
            int u=(axis+1)%3,v=(axis+2)%3;var corners=new Vector3[4];int[,] pattern={{-1,-1},{1,-1},{1,1},{-1,1}};
            for(int i=0;i<4;i++){Vector3 p=Vector3.zero;p[axis]=sign*h[axis];p[u]=pattern[i,0]*(h[u]-b);p[v]=pattern[i,1]*(h[v]-b);corners[i]=p;}
            Vector3 n=Vector3.zero;n[axis]=sign;face(corners,n);
        }
        if(b>0)for(int free=0;free<3;free++)for(int sa=-1;sa<=1;sa+=2)for(int sb=-1;sb<=1;sb+=2)
        {
            int a=(free+1)%3,bb=(free+2)%3;var corners=new Vector3[4];
            for(int j=0;j<4;j++){Vector3 p=Vector3.zero;p[free]=(j<2?-1:1)*(h[free]-b);bool first=j==0||j==3;p[a]=sa*(h[a]-(first?0:b));p[bb]=sb*(h[bb]-(first?b:0));corners[j]=p;}
            Vector3 n=Vector3.zero;n[a]=sa;n[bb]=sb;face(corners,n.normalized);
        }
        if(b>0)for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
        {
            Vector3 corner=V(x*(h.x-b),y*(h.y-b),z*(h.z-b));face(new[]{corner+V(x*b,0,0),corner+V(0,y*b,0),corner+V(0,0,z*b)},V(x,y,z).normalized);
        }
        var m=new Mesh{name=key};m.SetVertices(vertices);m.SetNormals(normals);m.SetUVs(0,uv);m.SetTriangles(triangles,0);m.RecalculateTangents();m.RecalculateBounds();
        Unwrapping.GenerateSecondaryUVSet(m);AssetDatabase.CreateAsset(m,path);meshes[key]=m;return m;
    }
    static Texture2D TileTexture(string name,bool normal)
    {
        const int n=256;var tex=new Texture2D(n,n,TextureFormat.RGBA32,true,normal);var pixels=new Color[n*n];
        for(int y=0;y<n;y++)for(int x=0;x<n;x++)
        {
            float dx=Mathf.Min(x,n-x),dy=Mathf.Min(y,n-y);float edge=Mathf.SmoothStep(0,1,Mathf.Min(dx,dy)/3.0f);
            if(normal)
            {
                float nx=dx<4?(x<n/2?-.12f:.12f)*(1-dx/4):0;float ny=dy<4?(y<n/2?-.12f:.12f)*(1-dy/4):0;
                pixels[y*n+x]=new Color(.5f+nx,.5f+ny,1,1);
            }
            else {float grain=Mathf.PerlinNoise(x*.18f,y*.18f)*.008f;float value=Mathf.Lerp(.72f,.94f,edge)+grain;pixels[y*n+x]=new Color(value,value,value,1);}
        }
        tex.SetPixels(pixels);tex.Apply();string path=Root+"/Textures/"+name+".png";File.WriteAllBytes(path,tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.wrapMode=TextureWrapMode.Repeat;
        importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=8;importer.mipmapEnabled=true;
        if(normal){importer.textureType=TextureImporterType.NormalMap;importer.convertToNormalmap=false;}else importer.sRGBTexture=true;
        importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    [Serializable] class State{public string scene,time;public bool playing,baking,followEnabled;public int lightmapCount;public float progress,orbitPitch;public Vector3 cameraPosition,cameraAngles;}
    [Serializable] class CaptureRequest{public string name;public Vector3 position,look;}
    [Serializable] class Validation{public string scene,time;public int missingScripts,wetRenderers,rendererCount,lightmaps;public Vector3 player,water;public float ceiling,sideCeiling,entryCeiling;public string[] floorChecks;}
}
