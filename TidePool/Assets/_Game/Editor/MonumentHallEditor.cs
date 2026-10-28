using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using AbstractOcclusion.WebGpuWater;

// An independent architectural variant. Existing playable water-cycle scene remains available.
public static class MonumentHallEditor
{
    static readonly string Out = TidePoolDevelopmentPaths.Folder("unity_monument");
    const string Root="Assets/_Game/MonumentHall";
    const string Target="Assets/_Game/Scenes/PoolHall_Monument_01.unity";
    static double at,nextStatus;
    static Transform architecture;
    static List<string> checks=new List<string>();
    static int routeIndex,routeSteps,orbitIndex;
    static float routeDistance;
    static readonly Vector3[] route={new Vector3(8,0,.35f),new Vector3(12.5f,0,.35f),new Vector3(12.5f,0,44.5f),new Vector3(8,0,51.5f)};
    static PoolWaterCycle C=>UnityEngine.Object.FindFirstObjectByType<PoolWaterCycle>();
    static Camera Cam=>Camera.main;

    [InitializeOnLoadMethod]static void Init()
    {
        EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        Lightmapping.bakeCompleted-=Baked;Lightmapping.bakeCompleted+=Baked;
        File.WriteAllText(Out+"/ready.txt",DateTime.Now.ToString("s"));at=EditorApplication.timeSinceStartup+4;
    }
    static void Tick()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        try
        {
            string file=Out+"/command.txt";
            if(File.Exists(file))
            {
                string cmd;try{cmd=File.ReadAllText(file).Trim();File.Delete(file);}catch(IOException){return;}
                if(cmd=="build")Build();
                else if(cmd=="bake")Bake();
                else if(cmd=="tune-light")
                {
                    Check(!EditorApplication.isPlaying,"Tune lighting in edit mode");
                    foreach(var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))if(light.type==LightType.Rectangle)light.range=100;
                    var water=new SerializedObject(C.waterFrame.GetComponent<WaterVolume>());
                    water.FindProperty("waterFogSettings.fogDensity").floatValue=.35f;
                    water.FindProperty("waterFogSettings.waterOpacity").floatValue=.025f;
                    water.FindProperty("waterFogSettings.lightScatter").floatValue=.15f;
                    water.ApplyModifiedPropertiesWithoutUndo();
                    var o=Need("Monument_Architecture").GetComponent<MonumentRenderPipeline>();if(o)UnityEngine.Object.DestroyImmediate(o);
                    Save();
                }
                else if(cmd=="crossing-test")
                {
                    Check(!EditorApplication.isPlaying,"Start crossing test in edit mode");SessionState.SetInt("MonumentTest",401);EditorApplication.isPlaying=true;at=EditorApplication.timeSinceStartup+5;
                }
                else if(cmd=="repeat-drain")
                {
                    Check(!EditorApplication.isPlaying && !Lightmapping.isRunning,"Repeat drain in edit mode after baking");
                    Check(!ShaderUtil.anythingCompiling,"Shaders idle before repeated drain test");
                    File.WriteAllText(Out+"/repeat-drain.txt",DateTime.Now.ToString("s")+" shaders idle\n");
                    SessionState.SetInt("MonumentTest",501);EditorApplication.isPlaying=true;at=EditorApplication.timeSinceStartup+5;
                }
                else if(cmd=="shader-status")
                {
                    var messages=new List<string>{DateTime.Now.ToString("s")+" compiling="+ShaderUtil.anythingCompiling};
                    foreach(var shader in Resources.FindObjectsOfTypeAll<Shader>())
                        foreach(var message in ShaderUtil.GetShaderMessages(shader))
                            messages.Add(shader.name+": "+message.severity+" "+message.message);
                    File.WriteAllLines(Out+"/shader-status.txt",messages);
                }
                else if(cmd=="water-inspect")
                {
                    Check(!EditorApplication.isPlaying,"Inspect water from edit mode");SessionState.SetInt("MonumentTest",301);EditorApplication.isPlaying=true;at=EditorApplication.timeSinceStartup+5;
                }
                else if(cmd=="reflections")Reflections();
                else if(cmd=="test")BeginTest();
                else if(cmd=="camera-test")
                {
                    Check(!EditorApplication.isPlaying,"Start camera test in edit mode");orbitIndex=0;
                    SessionState.SetInt("MonumentTest",201);EditorApplication.isPlaying=true;at=EditorApplication.timeSinceStartup+5;
                }
                else if(cmd=="restore")Restore();
                else if(cmd=="refresh")AssetDatabase.Refresh();
                else if(cmd=="stop"){SessionState.SetInt("MonumentTest",0);EditorApplication.isPlaying=false;}
                else throw new Exception("Unknown command "+cmd);
            }
            if(EditorApplication.timeSinceStartup>nextStatus)
            {
                nextStatus=EditorApplication.timeSinceStartup+4;
                File.WriteAllText(Out+"/status.txt",$"{DateTime.Now:s} scene={SceneManager.GetActiveScene().name} playing={EditorApplication.isPlaying} baking={Lightmapping.isRunning} shaders={ShaderUtil.anythingCompiling} stage={SessionState.GetInt("MonumentTest",0)} height={(C?C.WaterHeight:0)} route={routeIndex}/{routeSteps}");
            }
            if(EditorApplication.isPlaying && SessionState.GetInt("MonumentTest",0)>0 && EditorApplication.timeSinceStartup>=at)Test();
        }
        catch(Exception e)
        {
            SessionState.SetInt("MonumentTest",0);File.WriteAllText(Out+"/error.txt",DateTime.Now.ToString("s")+"\n"+e);Debug.LogException(e);
            if(EditorApplication.isPlaying)EditorApplication.isPlaying=false;
        }
    }
    static void Check(bool yes,string message){if(!yes)throw new Exception(message);checks.Add(message);}
    static GameObject Need(string name){var g=GameObject.Find(name);if(!g)throw new Exception("Missing "+name);return g;}
    static Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);
    static void Save()
    {
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/TidePool/Build Monumental Pool Hall")]
    static void Build()
    {
        Check(!EditorApplication.isPlaying && !Lightmapping.isRunning,"Edit mode required");
        Check(SceneManager.GetActiveScene().path.EndsWith("PoolHall_WaterCycle_01.unity"),"Source is existing playable hall");
        Check(!File.Exists(Target),"New hall will not overwrite an existing scene");
        Save();File.Copy(SceneManager.GetActiveScene().path,Out+"/BeforeMonument.unity",true);
        Check(EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),Target,true),"Scene copy saved");
        EditorSceneManager.OpenScene(Target,OpenSceneMode.Single);
        Directory.CreateDirectory(Root+"/Meshes");Directory.CreateDirectory(Root+"/Lighting");AssetDatabase.Refresh();
        Need("Dream_Architecture").SetActive(false);
        architecture=new GameObject("Monument_Architecture").transform;
        architecture.SetParent(Need("Architecture").transform,false);

        var wall=WallMaterial();
        var ceiling=Material("M_QuietCeiling",new Color(.82f,.88f,.91f),.14f);
        var pool=CloneMaterial("Assets/_Game/HallDream/M_PoolCeramic.mat","M_PoolCeramic_Monument");
        pool.SetColor("_BaseColor",new Color(.65f,.82f,.87f));pool.SetFloat("_Smoothness",.45f);
        var deck=CloneMaterial("Assets/_Game/WaterCycle/M_WetTiles_WaterCycle.mat","M_WetTiles_Monument");
        deck.shader=AssetDatabase.LoadAssetAtPath<Shader>(Root+"/SG_WetTiles_Monument.shadergraph");
        Check(deck.shader && !ShaderUtil.ShaderHasError(deck.shader),"Independent extended floor shader compiled");
        deck.SetColor("_DryColor",new Color(.78f,.85f,.88f));deck.SetColor("_WetColor",new Color(.53f,.68f,.74f));
        deck.SetColor("_GroutColor",new Color(.65f,.72f,.76f));deck.SetFloat("_WetRange",6);deck.SetFloat("_BaseWetness",.48f);
        SetFloor("Deck_North",V(0,-.25f,48.5f),V(40,.5f,11),deck);
        SetFloor("Deck_South",V(0,-.25f,-4.5f),V(40,.5f,11),deck);
        SetFloor("Deck_West",V(-15.5f,-.25f,22),V(9,.5f,42),deck);
        SetFloor("Deck_East",V(15.5f,-.25f,22),V(9,.5f,42),deck);
        SetSolid("Pool_Floor",V(0,-3.25f,22),V(22,.5f,42),pool);
        SetSolid("Pool_Wall_North",V(0,-1.75f,43.25f),V(22,2.5f,.5f),pool);
        SetSolid("Pool_Wall_South",V(0,-1.75f,.75f),V(22,2.5f,.5f),pool);
        SetSolid("Pool_Wall_West",V(-11.25f,-1.75f,22),V(.5f,2.5f,43),pool);
        SetSolid("Pool_Wall_East",V(11.25f,-1.75f,22),V(.5f,2.5f,43),pool);
        // Coping sits flush with the deck. Continuous runs meet once, without doubled corners.
        SetSolid("Coping_South",V(0,.015f,.88f),V(22.5f,.03f,.24f),wall);
        SetSolid("Coping_North",V(0,.015f,43.12f),V(22.5f,.03f,.24f),wall);
        SetSolid("Coping_West",V(-11.12f,.015f,22),V(.24f,.03f,42),wall);
        SetSolid("Coping_East",V(11.12f,.015f,22),V(.24f,.03f,42),wall);

        Box("Shell_West",V(-20.3f,12,22),V(.6f,24.1f,65.2f),wall);
        Box("Shell_East",V(20.3f,12,22),V(.6f,24.1f,65.2f),wall);
        Box("Shell_South",V(0,12,-10.3f),V(40,24.1f,.6f),wall);
        // One renderer for the end wall; its opening is deliberately small against the huge shell.
        Compound("Shell_North",new[]{V(-6.75f,12,54.3f),V(14.75f,12,54.3f),V(8,13.6f,54.3f)},new[]{V(26.5f,24.1f,.6f),V(10.5f,24.1f,.6f),V(3,20.8f,.6f)},wall);
        foreach(float x in new[]{-14.6f,14.6f})foreach(float z in new[]{2f,14f,26f,38f})
            Box("Giant_Pier_"+(x<0?"W_":"E_")+z,V(x,12,z),V(2,24.12f,2),wall,.035f);

        // Single lofty ceiling, with one long aperture. No intermediate storeys or arcade headers.
        Box("Roof_West",V(-12.65f,24.3f,22),V(15.9f,.6f,65.2f),ceiling);
        Box("Roof_East",V(12.65f,24.3f,22),V(15.9f,.6f,65.2f),ceiling);
        Box("Roof_Entry",V(0,24.3f,-2.15f),V(9.4f,.6f,16.3f),ceiling);
        Box("Roof_Far",V(0,24.3f,46.15f),V(9.4f,.6f,16.3f),ceiling);
        // High reveals finish the aperture above the ceiling rather than hanging into the room.
        Box("Aperture_W",V(-4.85f,24.75f,22),V(.3f,1.5f,32.6f),ceiling);
        Box("Aperture_E",V(4.85f,24.75f,22),V(.3f,1.5f,32.6f),ceiling);
        Box("Aperture_S",V(0,24.75f,5.85f),V(9.4f,1.5f,.3f),ceiling);
        Box("Aperture_N",V(0,24.75f,38.15f),V(9.4f,1.5f,.3f),ceiling);

        Box("Passage_Floor",V(8,-.25f,58.5f),V(3,.5f,9),deck);
        Box("Passage_Wall_W",V(6.2f,1.6f,58.5f),V(.6f,3.3f,9.6f),wall);
        Box("Passage_Wall_E",V(9.8f,1.6f,58.5f),V(.6f,3.3f,9.6f),wall);
        Box("Passage_Ceiling",V(8,3.5f,58.5f),V(4.2f,.6f,9.6f),ceiling);
        Box("Passage_End",V(8,1.6f,63.3f),V(4.2f,3.3f,.6f),wall);

        C.floodedHeight=24.5f;C.refillSeconds=18;C.platformFloodSeconds=24;C.hallFloodSeconds=30;
        C.waterFrame.position=V(0,C.poolHeight,22);
        var water=new SerializedObject(C.waterFrame.GetComponent<WaterVolume>());
        water.FindProperty("volumeExtent").vector3Value=V(20.3f,30,32.3f);water.ApplyModifiedPropertiesWithoutUndo();
        var dry=C.dryRegion.GetComponent<WaterExclusionVolume>();dry.transform.position=V(8,12,51.5f);dry.size=V(4,30,4);
        C.seepage.dryRegion=dry;C.seepage.poolCenter=new Vector2(0,22);C.seepage.poolHalfSize=new Vector2(11,21);
        // Aim every sample at actual solid roof; skip the skylight and piers.
        Physics.SyncTransforms();var points=new List<Vector3>();
        for(float x=-18;x<=18;x+=3)for(float z=-8;z<=52;z+=3)
            if(!Physics.CheckSphere(V(x,1,z),.2f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore) && Physics.Raycast(V(x,1,z),Vector3.up,out RaycastHit hit,27) && hit.point.y>23.5f)points.Add(hit.point-Vector3.up*.06f);
        C.seepage.ceilingPoints=points.ToArray();
        ConfigureLight(ceiling);
        foreach(var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if(r.transform.IsChildOf(Need("WebGPU Water").transform)||r.name=="Player"||r.name=="WaterControlSwitch"||r.transform.IsChildOf(C.letter.transform))continue;
            GameObjectUtility.SetStaticEditorFlags(r.gameObject,StaticEditorFlags.ContributeGI|StaticEditorFlags.ReflectionProbeStatic);
            r.receiveGI=ReceiveGI.Lightmaps;r.stitchLightmapSeams=true;r.scaleInLightmap=r.name.StartsWith("Roof_")?.5f:1;
        }
        var ls=new LightingSettings{ name="LS_Monument",bakedGI=true,realtimeGI=false,lightmapper=LightingSettings.Lightmapper.ProgressiveCPU,
            lightmapResolution=5,lightmapMaxSize=2048,lightmapPadding=6,directSampleCount=48,indirectSampleCount=192,environmentSampleCount=128,maxBounces=4,
            mixedBakeMode=MixedLightingMode.IndirectOnly,ao=false};
        AssetDatabase.CreateAsset(ls,Root+"/Lighting/LS_Monument.lighting");Lightmapping.lightingSettings=ls;Lightmapping.lightingDataAsset=null;
        Camera.main.farClipPlane=220;Save();Validate();Restore();
        File.WriteAllText(Out+"/built.txt",Target+"\n24m hall; 22x42m pool; 8 continuous 2m piers; 72s flood; dry region z51.5");
    }
    static void ConfigureLight(Material ceiling)
    {
        var sun=Need("Sun").GetComponent<Light>();sun.transform.rotation=Quaternion.Euler(72,-24,0);sun.color=new Color(.9f,.96f,1);
        sun.intensity=1.8f;sun.shadowBias=.015f;sun.shadowNormalBias=.025f;sun.useViewFrustumForShadowCasterCull=false;
        sun.GetUniversalAdditionalLightData().usePipelineSettings=false;sun.shadows=LightShadows.Soft;sun.lightmapBakeType=LightmapBakeType.Mixed;
        RenderSettings.sun=sun;RenderSettings.fog=false;RenderSettings.ambientIntensity=.85f;
        Area("Skylight_Bounce",V(0,23.8f,22),new Vector2(8,30),new Color(.83f,.92f,1),13);
        Area("Passage_Bounce",V(8,3.1f,60),new Vector2(2,3),new Color(.76f,.88f,1),2);
        var profile=UnityEngine.Object.Instantiate(Need("Global Volume").GetComponent<Volume>().sharedProfile);profile.name="VP_Monument";
        // Instantiate the components too: no source-profile edits or runtime asset sharing.
        profile.components=profile.components.Select(c=>UnityEngine.Object.Instantiate(c)).ToList();
        AssetDatabase.CreateAsset(profile,Root+"/Lighting/VP_Monument.asset");foreach(var component in profile.components)AssetDatabase.AddObjectToAsset(component,profile);
        if(profile.TryGet<ColorAdjustments>(out var color)){color.postExposure.Override(.35f);color.contrast.Override(1);color.saturation.Override(-8);}
        Need("Global Volume").GetComponent<Volume>().sharedProfile=profile;
        var probeGO=new GameObject("Monument_LightProbes");probeGO.transform.SetParent(architecture,false);var probes=probeGO.AddComponent<LightProbeGroup>();var p=new List<Vector3>();
        foreach(float x in new[]{-18f,-12f,0f,8f,12f,18f})foreach(float z in new[]{-6f,0f,8f,20f,32f,44f,51f})foreach(float y in new[]{.4f,1.8f,6f,13f,22f})p.Add(V(x,y,z));
        foreach(float z in new[]{2f,6f,8f})p.Add(V(8,-1.8f,z));probes.probePositions=p.ToArray();
        foreach(float z in new[]{10f,38f})
        {
            var go=new GameObject("Monument_Reflection_"+z);go.transform.SetParent(architecture,false);go.transform.position=V(0,3,z);
            var rp=go.AddComponent<ReflectionProbe>();rp.mode=ReflectionProbeMode.Baked;rp.resolution=256;rp.boxProjection=true;rp.size=V(42,28,42);rp.center=V(0,9,0);
            rp.intensity=.85f;rp.blendDistance=8;rp.cullingMask=~(1<<4);rp.nearClipPlane=.1f;rp.farClipPlane=150;
        }
    }
    static void Area(string name,Vector3 pos,Vector2 size,Color color,float intensity)
    {
        var g=new GameObject(name);g.transform.SetParent(architecture,false);g.transform.position=pos;g.transform.rotation=Quaternion.Euler(90,0,0);
        var light=g.AddComponent<Light>();light.type=LightType.Rectangle;light.lightmapBakeType=LightmapBakeType.Baked;light.areaSize=size;light.color=color;light.intensity=intensity;
    }
    static Material Material(string name,Color color,float smooth)
    {
        var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};mat.SetColor("_BaseColor",color);mat.SetFloat("_Smoothness",smooth);
        AssetDatabase.CreateAsset(mat,Root+"/"+name+".mat");return mat;
    }
    static Material CloneMaterial(string source,string name)
    {
        var mat=new Material(AssetDatabase.LoadAssetAtPath<Material>(source)){name=name};AssetDatabase.CreateAsset(mat,Root+"/"+name+".mat");return mat;
    }
    static Material WallMaterial()
    {
        const int n=256;var image=new Texture2D(n,n,TextureFormat.RGBA32,true);var pixels=new Color[n*n];
        for(int y=0;y<n;y++)for(int x=0;x<n;x++)
        {
            float edge=Mathf.Min(Mathf.Min(x,n-1-x),Mathf.Min(y,n-1-y));float c=Mathf.Lerp(.855f,.97f,Mathf.SmoothStep(0,1,edge/1.8f));pixels[y*n+x]=new Color(c,c,c,1);
        }
        image.SetPixels(pixels);image.Apply();string path=Root+"/PaleTile.png";File.WriteAllBytes(path,image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
        AssetDatabase.ImportAsset(path);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.wrapMode=TextureWrapMode.Repeat;importer.filterMode=FilterMode.Trilinear;
        importer.anisoLevel=8;importer.mipmapEnabled=true;importer.SaveAndReimport();
        var wall=Material("M_ContinuousCeramic",new Color(.88f,.93f,.96f),.3f);wall.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(path));wall.SetTextureScale("_BaseMap",Vector2.one/.5f);return wall;
    }
    static void SetFloor(string name,Vector3 pos,Vector3 size,Material mat)
    {
        var go=Need(name);go.transform.position=pos;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;go.GetComponent<Renderer>().SetPropertyBlock(null);
    }
    static void SetSolid(string name,Vector3 pos,Vector3 size,Material mat)
    {
        var go=Need(name);go.transform.SetPositionAndRotation(pos,Quaternion.identity);go.transform.localScale=Vector3.one;
        go.GetComponent<MeshFilter>().sharedMesh=Mesh(name,size,pos,0);go.GetComponent<BoxCollider>().size=size;go.GetComponent<Renderer>().sharedMaterial=mat;
    }
    static GameObject Box(string name,Vector3 pos,Vector3 size,Material mat,float bevel=0)
    {
        var go=new GameObject(name);go.transform.SetParent(architecture,false);go.transform.position=pos;
        go.AddComponent<MeshFilter>().sharedMesh=Mesh(name,size,pos,bevel);go.AddComponent<MeshRenderer>().sharedMaterial=mat;go.AddComponent<BoxCollider>().size=size;return go;
    }
    static void Compound(string name,Vector3[] positions,Vector3[] sizes,Material mat)
    {
        var combines=new CombineInstance[positions.Length];var go=new GameObject(name);go.transform.SetParent(architecture,false);
        for(int i=0;i<positions.Length;i++)
        {
            combines[i]=new CombineInstance{mesh=Mesh(name+"_part"+i,sizes[i],positions[i],0),transform=Matrix4x4.TRS(positions[i],Quaternion.identity,Vector3.one)};
            var c=go.AddComponent<BoxCollider>();c.center=positions[i];c.size=sizes[i];
        }
        var mesh=new Mesh{name=name};mesh.CombineMeshes(combines);Unwrapping.GenerateSecondaryUVSet(mesh);AssetDatabase.CreateAsset(mesh,Root+"/Meshes/"+name+".asset");
        go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=mat;
    }
    static Mesh Mesh(string name,Vector3 size,Vector3 worldOffset,float bevel)
    {
        // Regularly subdivided faces avoid huge triangles at tight shadow transitions.
        var vertices=new List<Vector3>();var normals=new List<Vector3>();var uvs=new List<Vector2>();var triangles=new List<int>();
        Action<Vector3[],Vector3> face=(corners,normal)=>
        {
            if(Vector3.Dot(Vector3.Cross(corners[1]-corners[0],corners[2]-corners[0]),normal)<0)Array.Reverse(corners);
            int start=vertices.Count;Vector3 a=new Vector3(Mathf.Abs(normal.x),Mathf.Abs(normal.y),Mathf.Abs(normal.z));
            foreach(var v in corners){vertices.Add(v);normals.Add(normal);Vector3 w=v+worldOffset;uvs.Add(a.y>=a.x && a.y>=a.z?new Vector2(w.x,w.z):a.x>=a.z?new Vector2(w.z,w.y):new Vector2(w.x,w.y));}
            triangles.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
        };
        Vector3 h=size*.5f;
        // A single extruded octagonal footprint gives each giant pier an uninterrupted height.
        if(bevel>0)
        {
            Vector2[] ring={new Vector2(-h.x+bevel,-h.z),new Vector2(h.x-bevel,-h.z),new Vector2(h.x,-h.z+bevel),new Vector2(h.x,h.z-bevel),new Vector2(h.x-bevel,h.z),new Vector2(-h.x+bevel,h.z),new Vector2(-h.x,h.z-bevel),new Vector2(-h.x,-h.z+bevel)};
            for(int i=0;i<8;i++)
            {
                var a=ring[i];var b=ring[(i+1)%8];var n=V(b.y-a.y,0,a.x-b.x).normalized;
                for(int y=0;y<13;y++)face(new[]{V(a.x,Mathf.Lerp(-h.y,h.y,y/13f),a.y),V(b.x,Mathf.Lerp(-h.y,h.y,y/13f),b.y),V(b.x,Mathf.Lerp(-h.y,h.y,(y+1)/13f),b.y),V(a.x,Mathf.Lerp(-h.y,h.y,(y+1)/13f),a.y)},n);
            }
            // Closed octagonal caps prevent shadow leaks at the joins.
            for(int sign=-1;sign<=1;sign+=2)for(int k=0;k<8;k++)
            {
                Vector3 a=V(0,sign*h.y,0),b=V(ring[k].x,sign*h.y,ring[k].y),c=V(ring[(k+1)%8].x,sign*h.y,ring[(k+1)%8].y);
                if(Vector3.Dot(Vector3.Cross(b-a,c-a),Vector3.up*sign)<0){var t=b;b=c;c=t;}
                int start=vertices.Count;foreach(var p in new[]{a,b,c}){vertices.Add(p);normals.Add(Vector3.up*sign);uvs.Add(new Vector2(p.x+worldOffset.x,p.z+worldOffset.z));}
                triangles.AddRange(new[]{start,start+1,start+2});
            }
        }
        else for(int axis=0;axis<3;axis++)for(int sign=-1;sign<=1;sign+=2)
        {
            int u=(axis+1)%3,v=(axis+2)%3;int nu=Mathf.Max(1,Mathf.CeilToInt(size[u]/3)),nv=Mathf.Max(1,Mathf.CeilToInt(size[v]/3));
            for(int j=0;j<nu;j++)for(int k=0;k<nv;k++)
            {
                var corners=new Vector3[4];int[,] pat={{0,0},{1,0},{1,1},{0,1}};
                for(int t=0;t<4;t++){Vector3 p=Vector3.zero;p[axis]=sign*h[axis];p[u]=Mathf.Lerp(-h[u],h[u],(j+pat[t,0])/(float)nu);p[v]=Mathf.Lerp(-h[v],h[v],(k+pat[t,1])/(float)nv);corners[t]=p;}
                Vector3 n=Vector3.zero;n[axis]=sign;face(corners,n);
            }
        }
        var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uvs);mesh.SetTriangles(triangles,0);mesh.RecalculateTangents();mesh.RecalculateBounds();
        Unwrapping.GenerateSecondaryUVSet(mesh);AssetDatabase.CreateAsset(mesh,Root+"/Meshes/"+name+".asset");return mesh;
    }
    static void Validate()
    {
        Physics.SyncTransforms();
        var all=SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true));
        Check(all.Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject))==0,"No missing scripts");
        Check(!Need("Architecture").transform.Find("Dream_Architecture").gameObject.activeSelf,"Old layered hall disabled");
        Check(UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Count(r=>r.name.StartsWith("Giant_Pier_"))==8,"Eight uninterrupted piers");
        Check(C.seepage.ceilingPoints.Length>120,"Seepage samples rebuilt from real 24m roof");
        Check(C.seepage.ceilingPoints.All(p=>p.y>23.5f),"No obsolete low-roof seepage");
        Check(Physics.Raycast(V(8,2,51.5f),Vector3.down,out var hit,3) && Mathf.Abs(hit.point.y)<.05f,"Dry pocket on walkable deck");
        Check(!Physics.Raycast(V(8,1.5f,53),Vector3.forward,8),"Small exit connects to corridor");
        Check(C.floodedHeight>24 && C.dryRegion.GetComponent<WaterExclusionVolume>().size.y*.5f+C.dryRegion.position.y>C.floodedHeight,"Final water fills hall, exclusion reaches above water");
        Check(C.refillSeconds+C.platformFloodSeconds+C.hallFloodSeconds==72,"72-second flood window");
        File.WriteAllLines(Out+"/validation.txt",checks);
    }
    static void Bake()
    {
        Check(SceneManager.GetActiveScene().path==Target && !EditorApplication.isPlaying && !Lightmapping.isRunning,"Bake correct hall in edit mode");
        Save();SessionState.SetBool("MonumentBake",true);Check(Lightmapping.BakeAsync(),"Lighting bake started");
    }
    static void Baked()
    {
        if(!SessionState.GetBool("MonumentBake",false))return;SessionState.SetBool("MonumentBake",false);
        if(EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path!=Target){File.WriteAllText(Out+"/bake-interrupted.txt",DateTime.Now.ToString("s"));return;}Save();
        File.WriteAllText(Out+"/baked.txt",DateTime.Now.ToString("s")+" lightmaps="+LightmapSettings.lightmaps.Length);
    }
    static void Reflections()
    {
        Check(!EditorApplication.isPlaying && !Lightmapping.isRunning,"Bake finished before reflection captures");
        foreach(var p in UnityEngine.Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None))
            Lightmapping.BakeReflectionProbe(p,Root+"/Lighting/"+p.name+".exr");
        Save();File.WriteAllText(Out+"/reflections.txt",DateTime.Now.ToString("s"));
    }
    static void Restore()
    {
        Check(!EditorApplication.isPlaying,"Restore in edit mode");
        EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).maximized=false;
        var view=EditorWindow.GetWindow<SceneView>();view.LookAt(V(0,9,23),Quaternion.Euler(18,-23,0),47);view.Focus();
        Selection.activeGameObject=Need("Monument_Architecture");
    }
    static void BeginTest()
    {
        Check(!EditorApplication.isPlaying && !Lightmapping.isRunning,"Tests start in edit mode");Validate();
        checks.Clear();SessionState.SetInt("MonumentTest",1);EditorApplication.isPlaying=true;at=EditorApplication.timeSinceStartup+5;
    }
    static void Next(int n,double delay){SessionState.SetInt("MonumentTest",n);at=EditorApplication.timeSinceStartup+delay;}
    static void Photo(Vector3 position,Vector3 look,float fov=68)
    {
        Cam.GetComponent<CameraFollow>().enabled=false;Cam.GetComponent<DryRegionCameraGuard>().enabled=false;
        Cam.transform.SetPositionAndRotation(position,Quaternion.LookRotation(look-position));Cam.fieldOfView=fov;C.showHints=false;
        EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).maximized=true;
    }
    static void Capture(string name)=>ScreenCapture.CaptureScreenshot(Out+"/"+name+".png");
    static void Place(Vector3 p)
    {
        var cc=C.player.GetComponent<CharacterController>();cc.enabled=false;C.player.position=p;cc.enabled=true;
    }
    static void Test()
    {
        int stage=SessionState.GetInt("MonumentTest",0);
        if(stage>=501 && stage<=520)
        {
            int round=(stage-501)/4, step=(stage-501)%4;
            if(ShaderUtil.anythingCompiling){File.AppendAllText(Out+"/repeat-drain.txt",DateTime.Now.ToString("s")+" wait shaders stage="+stage+"\n");at=EditorApplication.timeSinceStartup+1;return;}
            if(step==0)
            {
                C.player.GetComponent<PlayerMovement>().enabled=false;C.ResetSequence(true);Place(V(2.5f,1.05f,-3));
                Photo(V(-3,2.1f,-4),V(0,9,32));C.showHints=true;C.TryInteract();Next(stage+1,4);
            }
            else if(step==1){Capture("repeat-"+round+"-mid");Next(stage+1,5);}
            else if(step==2)
            {
                Check(C.CurrentPhase==PoolWaterCycle.Phase.Empty,"Real-time drain completes round "+round);
                Capture("repeat-"+round+"-empty");File.AppendAllText(Out+"/repeat-drain.txt",DateTime.Now.ToString("s")+" round="+round+" empty height="+C.WaterHeight+" camera="+Cam.transform.position+" shaders="+ShaderUtil.anythingCompiling+"\n");Next(stage+1,1);
            }
            else if(stage==520){File.AppendAllText(Out+"/repeat-drain.txt","PASS five real-time drains; inspect screenshots for rendering\n");SessionState.SetInt("MonumentTest",0);EditorApplication.isPlaying=false;}
            else Next(stage+1,.5);
        }
        else if(stage>=401 && stage<=412)
        {
            int index=(stage-401)/2;
            if((stage-401)%2==0)
            {
                C.player.GetComponent<PlayerMovement>().enabled=false;C.ResetSequence(false);C.enabled=false;
                if(index>=3){C.TryInteract();C.Advance(index==5?100:4);}
                float[] ys={.2f,-.32f,-1.8f,-1f,-1.82f,-1.8f};
                Vector3 eye=V(0,ys[index],16);Photo(eye,eye+V(0,.05f,15));Next(stage+1,3);
            }
            else
            {
                string name="crossing-"+index;Capture(name);
                File.WriteAllText(Out+"/"+name+".txt","phase="+C.CurrentPhase+" height="+C.WaterHeight+" eye="+Cam.transform.position+" fogArmed="+Shader.GetGlobalFloat("_UnderwaterFogArmed")+" under="+Shader.GetGlobalFloat("_CameraUnderwater")+" density="+Shader.GetGlobalFloat("_WaterFogDensity")+" opacity="+Shader.GetGlobalFloat("_WaterOpacity")+" sun="+Shader.GetGlobalVector("_SunColor"));
                if(stage==412){SessionState.SetInt("MonumentTest",0);EditorApplication.isPlaying=false;}else Next(stage+1,1);
            }
        }
        else if(stage==301)
        {
            C.player.GetComponent<PlayerMovement>().enabled=false;C.TryInteract();C.Advance(100);C.BeginFloodFromLetter();C.Advance(100);
            Photo(V(8,1.7f,51.3f),V(0,4,22));Next(302,5);
        }
        else if(stage==302)
        {
            var lines=new List<string>{"frame="+C.waterFrame.position+" globalCenter="+Shader.GetGlobalVector("_VolumeCenter")+" globalExtent="+Shader.GetGlobalVector("_VolumeExtent")+" primary="+WaterVolume.Primary+" noBodies="+Shader.GetGlobalFloat("_NoWaterBodies")+" fogArmed="+Shader.GetGlobalFloat("_UnderwaterFogArmed")+" fogDensity="+Shader.GetGlobalFloat("_WaterFogDensity")+" fogEnabled="+Shader.GetGlobalFloat("_WaterFogEnabled")+" cameraUnder="+Shader.GetGlobalFloat("_CameraUnderwater")+" quality="+GraphicsSettings.currentRenderPipeline.name};
            foreach(var r in Need("WebGPU Water").GetComponentsInChildren<Renderer>())
            {
                var block=new MaterialPropertyBlock();r.GetPropertyBlock(block);lines.Add(r.name+" enabled="+r.enabled+" center="+block.GetVector("_VolumeCenter")+" extent="+block.GetVector("_VolumeExtent"));
            }
            var water=new SerializedObject(C.waterFrame.GetComponent<WaterVolume>());lines.Add(EditorJsonUtility.ToJson(water.targetObject,true));
            File.WriteAllLines(Out+"/water-inspect.txt",lines);Capture("07-inspected-flood-pocket");Next(303,1);
        }
        else if(stage==303){Photo(V(0,1.7f,-2),V(0,5,30));Next(304,4);}
        else if(stage==304){Capture("08-inspected-submerged");Next(305,1);}
        else if(stage==305){SessionState.SetInt("MonumentTest",0);EditorApplication.isPlaying=false;}
        else if(stage==201){C.player.GetComponent<PlayerMovement>().enabled=false;orbitIndex=0;Next(202,.1);}
        else if(stage==202)
        {
            if(orbitIndex>=192)
            {
                File.WriteAllText(Out+"/camera-pass.txt","PASS: 192 orbit cases against new giant piers, outer walls, far wall, pool floor.\n"+DateTime.Now.ToString("s"));
                SessionState.SetInt("MonumentTest",0);EditorApplication.isPlaying=false;return;
            }
            Vector3[] positions={V(-19.1f,1.08f,16),V(19.1f,1.08f,16),V(0,1.08f,-9),V(0,1.08f,53),V(12.7f,1.08f,14),V(0,1.08f,-7.5f),V(0,1.08f,47),V(8,-1.92f,8)};
            Place(positions[orbitIndex/24]);int angle=orbitIndex%24;var f=Cam.GetComponent<CameraFollow>();
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            typeof(CameraFollow).GetField("yaw",flags).SetValue(f,(angle%8)*45f);
            typeof(CameraFollow).GetField("pitch",flags).SetValue(f,new[]{-10f,15f,65f}[angle/8]);
            Physics.SyncTransforms();Next(203,.08);
        }
        else if(stage==203)
        {
            var f=Cam.GetComponent<CameraFollow>();
            var hits=Physics.OverlapSphere(Cam.transform.position,f.CollisionRadius*.95f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore).Where(f.IsCameraObstacle).ToArray();
            Check(hits.Length==0,"Camera clear at case "+orbitIndex+": "+string.Join(",",hits.Select(h=>h.name)));
            Check(Mathf.Abs(Cam.transform.position.x)<20 && Cam.transform.position.z>-10.05f && Cam.transform.position.z<54.05f,"Camera stays in the revised hall");
            orbitIndex++;Next(202,.02);
        }
        else if(stage==1){C.player.GetComponent<PlayerMovement>().enabled=false;Photo(V(-3,2.1f,-4),V(0,9,32));Next(2,4);}
        else if(stage==2){Capture("01-entry");Next(3,1);}
        else if(stage==3){Photo(V(-18,1.8f,9),V(-13,9,38));Next(4,3);}
        else if(stage==4){Capture("02-aisle");Next(5,1);}
        else if(stage==5){Photo(V(0,1.7f,21),V(0,20,25));Next(6,3);}
        else if(stage==6){Capture("03-height");Next(7,1);}
        else if(stage==7){Place(V(2.5f,1.05f,-3));Check(C.TryInteract(),"Console starts drain");Next(8,9);}
        else if(stage==8)
        {
            Check(C.CurrentPhase==PoolWaterCycle.Phase.Empty && C.WaterHeight<=-3.34f,"Drain completes and exposes letter");
            Place(V(8,-1.98f,8));Check(C.letter.TryPickUp(),"Pool-floor letter starts flood");C.letter.CloseLetter();
            C.player.GetComponent<PlayerMovement>().enabled=false;routeIndex=0;routeSteps=0;routeDistance=0;Next(9,.02);
        }
        else if(stage==9)
        {
            // Walk the actual CharacterController up the stairs and along the east deck.
            var cc=C.player.GetComponent<CharacterController>();
            for(int i=0;i<5 && routeIndex<route.Length;i++)
            {
                Vector3 delta=route[routeIndex]-C.player.position;delta.y=0;
                if(delta.magnitude<.14f){routeIndex++;continue;}
                var before=C.player.position;cc.Move(Vector3.ClampMagnitude(delta,.12f)+Vector3.down*.15f);routeDistance+=Vector3.Distance(before,C.player.position);routeSteps++;
                Check(routeSteps<1600,"Escape route makes forward progress");
            }
            if(routeIndex<route.Length){Next(9,.02);return;}
            Check(C.player.position.y>.7f,"Actual capsule climbed pool stairs and reached dry deck");
            Check(routeDistance/3f+8<40,"Walk plus 8 seconds reading fits before head-height flooding");
            Photo(V(-5,2.2f,-4),V(0,2,30));Next(10,3);
        }
        else if(stage==10){Capture("04-seeping");C.Advance(C.refillSeconds+C.platformFloodSeconds*.5f-C.Elapsed);Next(11,2);}
        else if(stage==11)
        {
            Check(C.seepage.LiveParticles>0,"New roof produces airborne seepage");
            Check(C.GetComponent<FloodSurfaceState>().FloodBlend>.999f,"Overflow removes pool-edge wetness boundary");
            Place(V(8,1.05f,51.5f));Photo(V(8,1.7f,51.3f),V(-1,6,22));Next(12,3);
        }
        else if(stage==12){Capture("05-rising-from-pocket");C.Advance(100);Next(13,3);}
        else if(stage==13)
        {
            Check(C.CurrentPhase==PoolWaterCycle.Phase.Flooded && C.WaterHeight==24.5f,"Full hall flood reaches 24.5m");
            Check(C.seepage.LiveParticles==0 && C.seepage.VisibleStreams==0,"No leaking animation below final water");
            Check(C.GetComponent<FloodAtmosphere>().Blend>.999f,"Deep blue atmosphere remains linked to flood");
            Capture("06-final-dry-pocket");Next(14,1);
        }
        else if(stage==14)
        {
            C.ResetSequence(true);Cam.GetComponent<CameraFollow>().enabled=true;Cam.GetComponent<DryRegionCameraGuard>().enabled=true;
            Place(V(8,1.05f,51.5f));Next(15,1);
        }
        else if(stage==15)
        {
            Check(Cam.GetComponent<DryRegionCameraGuard>().IsPlayerInside,"Dry camera guard follows relocated pocket");
            Check(C.seepage.Intensity==0 && C.GetComponent<FloodAtmosphere>().Blend==0,"Reset clears seepage and atmosphere");
            C.ResetSequence(true);File.WriteAllText(Out+"/test-pass.txt","PASS\n"+string.Join("\n",checks.Distinct())+"\nRoute distance="+routeDistance+"m; walk estimate="+(routeDistance/3f)+"s\n"+DateTime.Now.ToString("s"));
            SessionState.SetInt("MonumentTest",0);EditorApplication.isPlaying=false;
        }
    }
}
