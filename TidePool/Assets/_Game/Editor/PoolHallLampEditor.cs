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

public static class PoolHallLampEditor
{
    static readonly string Out = TidePoolDevelopmentPaths.Folder("unity_pool_lighting");
    const string Root="Assets/_Game/MonumentHall/LampLighting";
    static double at,nextStatus;
    static PoolWaterCycle C=>UnityEngine.Object.FindFirstObjectByType<PoolWaterCycle>();
    static PoolHallLampRig Rig=>UnityEngine.Object.FindFirstObjectByType<PoolHallLampRig>();
    static Camera Cam=>Camera.main;
    [InitializeOnLoadMethod]static void Init(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;at=EditorApplication.timeSinceStartup+4;File.WriteAllText(Out+"/ready.txt",DateTime.Now.ToString("s"));}
    static void Tick()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        try
        {
            string path=Out+"/command.txt";
            if(File.Exists(path))
            {
                string cmd=File.ReadAllText(path).Trim();File.Delete(path);
                if(cmd=="install")Install();
                else if(cmd=="bright-start")
                {
                    Check(!EditorApplication.isPlaying && SceneManager.GetActiveScene().name=="PoolHall_Monument_01","Adjust current hall in edit mode");
                    Save();if(!File.Exists(Out+"/BeforeBrightStart.unity"))File.Copy(SceneManager.GetActiveScene().path,Out+"/BeforeBrightStart.unity");
                    RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.68f,.8f,.88f);
                    var sh=new SphericalHarmonicsL2();sh.AddAmbientLight(RenderSettings.ambientLight);RenderSettings.ambientProbe=sh;
                    var fill=new List<Light>();
                    var root=GameObject.Find("Normal_Indirect_Fill");
                    if(!root)
                    {
                        root=new GameObject("Normal_Indirect_Fill");root.transform.SetParent(Rig.transform,false);
                        foreach(float z in new[]{-4f,6f,16f,26f,36f,46f})
                            fill.Add(Lamp("Soft_White_Fill",root.transform,V(0,12,z),Vector3.down,LightType.Point,40,30,0,LightShadows.None));
                    }
                    else fill.AddRange(root.GetComponentsInChildren<Light>());
                    Rig.normalFillLights=fill.ToArray();Rig.dimSeconds=12;Rig.tintSeconds=24;
                    EditorUtility.SetDirty(Rig);Save();File.WriteAllText(Out+"/bright-start-installed.txt",DateTime.Now.ToString("s"));
                }
                else if(cmd=="transition-test")
                {
                    Check(!EditorApplication.isPlaying,"Test bright start from edit mode");
                    File.WriteAllText(Out+"/transition.txt",DateTime.Now.ToString("s")+"\n");SessionState.SetInt("LampTest",601);EditorApplication.isPlaying=true;at=EditorApplication.timeSinceStartup+6;
                }
                else if(cmd=="tune")
                {
                    Check(!EditorApplication.isPlaying,"Tune in edit mode");
                    Lightmapping.lightingDataAsset=null;LightmapSettings.lightmaps=Array.Empty<LightmapData>();
                    RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.018f,.03f,.038f);
                    var sh=new SphericalHarmonicsL2();sh.AddAmbientLight(RenderSettings.ambientLight);RenderSettings.ambientProbe=sh;
                    foreach(var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))r.lightProbeUsage=LightProbeUsage.Off;
                    foreach(var light in Rig.hallLamps){light.transform.position+=Vector3.right*(light.transform.position.x<0?3:-3);light.intensity=32;}
                    foreach(var lens in Rig.hallLenses){lens.transform.position+=Vector3.right*(lens.transform.position.x<0?3:-3);}
                    foreach(var rim in Need("PoolHall_LampLighting").GetComponentsInChildren<Transform>())if(rim.name=="Ceiling_Lamp")rim.position+=Vector3.right*(rim.position.x<0?3:-3);
                    foreach(var l in Rig.rippleLights){l.intensity=130;l.spotAngle=72;l.innerSpotAngle=48;}
                    Save();
                }
                else if(cmd=="balance")
                {
                    Check(!EditorApplication.isPlaying,"Balance in edit mode");
                    RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.12f,.16f,.18f);
                    var sh=new SphericalHarmonicsL2();sh.AddAmbientLight(RenderSettings.ambientLight);RenderSettings.ambientProbe=sh;
                    foreach(var l in Rig.poolLamps){l.type=LightType.Point;l.range=11;l.intensity=13;}
                    foreach(var l in Rig.rippleLights)l.intensity=90;
                    var h=Rig.hallLamps.ToList();var lenses=Rig.hallLenses.ToList();
                    if(!GameObject.Find("Wall_Indirect_Wash"))foreach(float x in new[]{-19.6f,19.6f})foreach(float z in new[]{6f,18f,30f,42f})
                    {
                        h.Add(Lamp("Wall_Indirect_Wash",Rig.transform,V(x,7,z),V(x<0?1:-1,.28f,0),LightType.Point,28,25,0,LightShadows.None));
                        lenses.Add(Fixture("Wall_Lamp",Rig.transform,V(x<0?-19.94f:19.94f,7,z),V(x<0?1:-1,0,0),.55f,AssetDatabase.LoadAssetAtPath<Material>(Root+"/M_Lamp_Rim.mat"),AssetDatabase.LoadAssetAtPath<Material>(Root+"/M_Hall_Lens.mat")));
                    }
                    Rig.hallLamps=h.ToArray();Rig.hallLenses=lenses.ToArray();
                    var water=new SerializedObject(C.waterFrame.GetComponent<WaterVolume>());Set(water,"waterFogSettings.lightScatter",.055f);water.ApplyModifiedPropertiesWithoutUndo();
                    Save();
                }
                else if(cmd=="look"){Check(!EditorApplication.isPlaying,"Start in edit mode");SessionState.SetInt("LampTest",1);EditorApplication.isPlaying=true;at=EditorApplication.timeSinceStartup+6;}
                else if(cmd=="reflections")
                {
                    Check(!EditorApplication.isPlaying,"Capture probes in edit mode");
                    foreach(var p in UnityEngine.Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None))Lightmapping.BakeReflectionProbe(p,Root+"/"+p.name+".exr");Save();
                }
                else if(cmd=="stop"){SessionState.SetInt("LampTest",0);EditorApplication.isPlaying=false;}
                else throw new Exception("Unknown lamp command "+cmd);
            }
            if(EditorApplication.timeSinceStartup>nextStatus)
            {
                nextStatus=EditorApplication.timeSinceStartup+3;File.WriteAllText(Out+"/status.txt",$"{DateTime.Now:s} playing={EditorApplication.isPlaying} shaders={ShaderUtil.anythingCompiling} step={SessionState.GetInt("LampTest",0)} phase={(C?C.CurrentPhase:0)} height={(C?C.WaterHeight:0)}");
            }
            if(EditorApplication.isPlaying && SessionState.GetInt("LampTest",0)>0 && EditorApplication.timeSinceStartup>at)
            {
                if(ShaderUtil.anythingCompiling){at=EditorApplication.timeSinceStartup+1;return;}Test();
            }
        }
        catch(Exception e){File.WriteAllText(Out+"/error.txt",DateTime.Now.ToString("s")+"\n"+e);SessionState.SetInt("LampTest",0);Debug.LogException(e);if(EditorApplication.isPlaying)EditorApplication.isPlaying=false;}
    }
    static void Check(bool yes,string message){if(!yes)throw new Exception(message);}
    static Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);
    static GameObject Need(string name){var g=GameObject.Find(name);Check(g,"Missing "+name);return g;}
    static void Save(){EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();}
    static void Install()
    {
        Check(!EditorApplication.isPlaying && !Lightmapping.isRunning,"Edit mode, no bake");
        Check(SceneManager.GetActiveScene().name=="PoolHall_Monument_01","Use current monumental hall");
        Check(!GameObject.Find("PoolHall_LampLighting"),"Do not duplicate lighting rig");
        Save();File.Copy(SceneManager.GetActiveScene().path,Out+"/BeforeLampLighting.unity",false);
        Directory.CreateDirectory(Root);AssetDatabase.Refresh();
        var parent=new GameObject("PoolHall_LampLighting").transform;parent.SetParent(Need("Monument_Architecture").transform,false);
        var roofMat=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/MonumentHall/M_QuietCeiling.mat");
        var roof=GameObject.CreatePrimitive(PrimitiveType.Cube);roof.name="Closed_Skylight_Roof";roof.transform.SetParent(parent,false);roof.transform.position=V(0,24.3f,22);roof.transform.localScale=V(9.5f,.6f,32.2f);roof.GetComponent<Renderer>().sharedMaterial=roofMat;
        foreach(string name in new[]{"Aperture_W","Aperture_E","Aperture_S","Aperture_N"})Need(name).SetActive(false);
        foreach(var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if(light.type==LightType.Directional || light.type==LightType.Rectangle){light.enabled=false;if(light.type==LightType.Directional)light.intensity=0;}
        RenderSettings.sun=null;RenderSettings.ambientMode=AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=new Color(.075f,.105f,.125f);RenderSettings.ambientEquatorColor=new Color(.04f,.06f,.075f);RenderSettings.ambientGroundColor=new Color(.025f,.035f,.045f);RenderSettings.reflectionIntensity=.55f;
        Lightmapping.Clear();
        foreach(var renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)){renderer.lightmapIndex=-1;renderer.receiveGI=ReceiveGI.LightProbes;GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,GameObjectUtility.GetStaticEditorFlags(renderer.gameObject)&~StaticEditorFlags.ContributeGI);}
        var ls=UnityEngine.Object.Instantiate(Lightmapping.lightingSettings);ls.name="LS_Lamps_Realtime";ls.bakedGI=false;ls.realtimeGI=false;AssetDatabase.CreateAsset(ls,Root+"/LS_Lamps.lighting");Lightmapping.lightingSettings=ls;
        var metal=Mat("M_Lamp_Rim",new Color(.15f,.19f,.2f),.7f,Color.black);
        var lens=Mat("M_Pool_Lens",new Color(.7f,.85f,.9f),.9f,new Color(.82f,.96f,1)*3.2f);
        var ceilingLens=Mat("M_Hall_Lens",new Color(.8f,.9f,.94f),.85f,new Color(.85f,.94f,1)*2.5f);
        var pool=new List<Light>();var hall=new List<Light>();var ripples=new List<Light>();var poolGlass=new List<Renderer>();var hallGlass=new List<Renderer>();
        Texture2D[] frames=CookieFrames();
        foreach(float x in new[]{-10.92f,10.92f})foreach(float z in new[]{6f,18f,30f,42f})
        {
            Vector3 facing=x<0?Vector3.right:Vector3.left;
            poolGlass.Add(Fixture("Pool_Lamp",parent,V(x,-1.45f,z),facing,.45f,metal,lens));
            pool.Add(Lamp("Pool_Wash",parent,V(x+facing.x*.18f,-1.45f,z),facing+Vector3.down*.22f,LightType.Spot,14,17,115,LightShadows.None));
            var shimmer=Lamp("Water_Reflection_Projector",parent,V(x+facing.x*.3f,-1.05f,z),facing*.18f+Vector3.up,LightType.Spot,25,36,88,LightShadows.None);shimmer.cookie=frames[0];ripples.Add(shimmer);
        }
        foreach(float x in new[]{-14.6f,14.6f})foreach(float z in new[]{2f,14f,26f,38f})
        {
            hallGlass.Add(Fixture("Ceiling_Lamp",parent,V(x,23.9f,z),Vector3.down,.7f,metal,ceilingLens));
            hall.Add(Lamp("Hall_Downlight",parent,V(x,23.65f,z),Vector3.down,LightType.Spot,70,34,105,(z==14 || z==38)?LightShadows.Soft:LightShadows.None));
        }
        // A small, steady passage cue. The next room remains unbuilt.
        var passage=Lamp("Passage_Guide",parent,V(8,2.7f,59),Vector3.down,LightType.Point,2.8f,7,0,LightShadows.None);passage.color=new Color(.6f,.82f,.88f);
        var rig=parent.gameObject.AddComponent<PoolHallLampRig>();rig.cycle=C;rig.poolLamps=pool.ToArray();rig.hallLamps=hall.ToArray();rig.rippleLights=ripples.ToArray();rig.poolLenses=poolGlass.ToArray();rig.hallLenses=hallGlass.ToArray();rig.rippleFrames=frames;
        var atmosphere=C.GetComponent<FloodAtmosphere>();atmosphere.sun=null;atmosphere.darkeningStops=.3f;atmosphere.floodColor=new Color(.85f,.96f,1);atmosphere.finalVignette=.15f;
        var profile=Need("Global Volume").GetComponent<Volume>().sharedProfile;
        if(profile.TryGet<ColorAdjustments>(out var color)){color.postExposure.Override(.3f);color.contrast.Override(8);color.saturation.Override(-3);}
        if(!profile.TryGet<Bloom>(out var bloom))bloom=profile.Add<Bloom>(true);bloom.intensity.Override(.12f);bloom.threshold.Override(1.2f);EditorUtility.SetDirty(profile);
        var water=new SerializedObject(C.waterFrame.GetComponent<WaterVolume>());
        Set(water,"waterFogSettings.fogDensity",.18f);Set(water,"waterFogSettings.waterOpacity",.015f);Set(water,"waterFogSettings.lightScatter",.2f);
        water.FindProperty("waterFogSettings.fogColor").colorValue=new Color(.025f,.13f,.17f);water.FindProperty("waterFogSettings.fogExtinction").colorValue=new Color(.22f,.08f,.045f);
        Set(water,"depthAttenuation.causticDepthFade",.06f);Set(water,"depthAttenuation.screenCausticIntensity",.7f);
        Set(water,"reflectionSettings.fresnelFloor",.04f);Set(water,"reflectionSettings.fresnelPower",4);Set(water,"reflectionSettings.sunRoughness",.065f);Set(water,"reflectionSettings.reflectionDistortion",.025f);Set(water,"reflectionSettings.refractionDistortion",.035f);
        Set(water,"windWaveSettings.windResponse",0);Set(water,"windWaveSettings.waveHeightMeters",.032f);Set(water,"windWaveSettings.waveLengthMeters",.85f);Set(water,"windWaveSettings.waveAnimationSpeed",.55f);Set(water,"windWaveSettings.waveNormalStrength",1.1f);
        // Indoor water should not retain a procedural daytime sky at grazing angles.
        water.FindProperty("reflectionSettings.reflectUrpProbe").boolValue=true;water.FindProperty("reflectionSettings.reflectionProbe").objectReferenceValue=UnityEngine.Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None).OrderBy(p=>p.transform.position.z).First();
        water.ApplyModifiedPropertiesWithoutUndo();
        Save();File.WriteAllText(Out+"/installed.txt",DateTime.Now.ToString("s")+"\n8 pool fixtures, 8 ceiling fixtures, 8 animated caustic projectors; closed aperture; local lamps turn cyan after letter.\n");
    }
    static void Set(SerializedObject target,string path,float value){var p=target.FindProperty(path);Check(p!=null,path);p.floatValue=value;}
    static Material Mat(string name,Color color,float smooth,Color emission)
    {
        var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smooth);
        m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",emission);AssetDatabase.CreateAsset(m,Root+"/"+name+".mat");return m;
    }
    static Renderer Fixture(string name,Transform parent,Vector3 point,Vector3 normal,float diameter,Material rim,Material glass)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name=name;go.transform.SetParent(parent,false);go.transform.SetPositionAndRotation(point,Quaternion.FromToRotation(Vector3.up,normal));go.transform.localScale=V(diameter,.025f,diameter);UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=rim;
        var face=GameObject.CreatePrimitive(PrimitiveType.Cylinder);face.name="Sealed_Glass";face.transform.SetParent(parent,false);face.transform.SetPositionAndRotation(point+normal*.028f,go.transform.rotation);face.transform.localScale=V(diameter*.78f,.009f,diameter*.78f);UnityEngine.Object.DestroyImmediate(face.GetComponent<Collider>());face.GetComponent<Renderer>().sharedMaterial=glass;return face.GetComponent<Renderer>();
    }
    static Light Lamp(string name,Transform parent,Vector3 pos,Vector3 direction,LightType type,float intensity,float range,float angle,LightShadows shadows)
    {
        var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.SetPositionAndRotation(pos,Quaternion.LookRotation(direction));
        var light=go.AddComponent<Light>();light.type=type;light.color=new Color(.82f,.94f,1);light.intensity=intensity;light.range=range;light.lightmapBakeType=LightmapBakeType.Realtime;light.shadows=shadows;light.shadowBias=.025f;light.shadowNormalBias=.12f;
        if(type==LightType.Spot){light.spotAngle=angle;light.innerSpotAngle=angle*.65f;}light.GetUniversalAdditionalLightData().usePipelineSettings=false;return light;
    }
    static float Hash(float x,float y)=>Mathf.Repeat(Mathf.Sin(x*127.1f+y*311.7f)*43758.5453f,1);
    static Texture2D[] CookieFrames()
    {
        const int size=128,count=16;var frames=new Texture2D[count];
        for(int f=0;f<count;f++)
        {
            var tex=new Texture2D(size,size,TextureFormat.RGBA32,true,true){name="WaterCaustic_"+f,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear};var pixels=new Color[size*size];float phase=f*Mathf.PI*2/count;
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float u=(x+.5f)/size,v=(y+.5f)/size;
                float px=u*6+.18f*Mathf.Sin(v*13+phase),py=v*6+.18f*Mathf.Cos(u*11-phase);int ix=Mathf.FloorToInt(px),iy=Mathf.FloorToInt(py);float first=100,second=100;
                for(int j=-1;j<=1;j++)for(int i=-1;i<=1;i++)
                {
                    float cx=ix+i,cy=iy+j;float dx=cx+.5f+.3f*Mathf.Sin(Hash(cx,cy)*6.28f+phase)-px,dy=cy+.5f+.3f*Mathf.Cos(Hash(cy,cx+11)*6.28f+phase)-py;float d=dx*dx+dy*dy;
                    if(d<first){second=first;first=d;}else if(d<second)second=d;
                }
                float line=1-Mathf.SmoothStep(.018f,.095f,second-first);float edge=1-Mathf.SmoothStep(.65f,1,Mathf.Sqrt((u-.5f)*(u-.5f)+(v-.5f)*(v-.5f))*2);float c=(.035f+line*.965f)*edge;pixels[y*size+x]=new Color(c,c,c,1);
            }
            tex.SetPixels(pixels);tex.Apply(true,false);AssetDatabase.CreateAsset(tex,Root+"/WaterCaustic_"+f+".asset");frames[f]=tex;
        }
        return frames;
    }
    static void Next(int n,double wait){SessionState.SetInt("LampTest",n);at=EditorApplication.timeSinceStartup+wait;}
    static void Photo(Vector3 eye,Vector3 look)
    {
        C.player.GetComponent<PlayerMovement>().enabled=false;Cam.GetComponent<CameraFollow>().enabled=false;Cam.GetComponent<DryRegionCameraGuard>().enabled=false;C.showHints=false;
        Cam.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(look-eye));Cam.fieldOfView=68;EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).maximized=true;
    }
    static void Capture(string name){ScreenCapture.CaptureScreenshot(Out+"/"+name+".png");File.AppendAllText(Out+"/look.txt",$"{DateTime.Now:s} {name} height={C.WaterHeight} tint={Rig.Transition} scatterLights={Shader.GetGlobalFloat("_WaterSceneLightCount")} exposure={C.GetComponent<FloodAtmosphere>().Exposure}\n");}
    static void Test()
    {
        int step=SessionState.GetInt("LampTest",0);
        if(step>=601){TransitionTest(step);return;}
        if(step==1){Photo(V(-3,2.1f,-4),V(0,8,32));Next(2,5);}
        else if(step==2){Capture("01-cold-white-entry");Next(3,.8);}
        else if(step==3){Photo(V(-9,1.4f,8),V(3,0,29));Next(4,3);}
        else if(step==4){Capture("02-water-detail");Next(5,.8);}
        else if(step==5){C.TryInteract();C.Advance(100);C.BeginFloodFromLetter();C.Advance(30);C.enabled=false;Photo(V(-5,2.2f,-4),V(0,3,30));Next(6,3);}
        else if(step==6){Capture("03-water-over-deck");Next(7,.8);}
        else if(step==7){C.Advance(15);Photo(V(0,1.6f,18),V(0,8,35));Next(8,4);}
        else if(step==8){Capture("04-submerged-lamps");Next(9,.8);}
        else if(step==9){Photo(V(8,1.7f,51.3f),V(0,5,22));C.Advance(100);Next(10,4);}
        else if(step==10)
        {
            Capture("05-dry-pocket-flood");Check(Rig.Transition>.99f,"Lights reach cyan");Check(C.seepage.LiveParticles==0,"No final submerged seepage");Next(11,1);
        }
        else if(step==11){C.ResetSequence(true);Next(12,2);}
        else if(step==12)
        {
            Check(Rig.Transition==0,"Reset restores white lamps");Check(Mathf.Abs(C.GetComponent<FloodAtmosphere>().Exposure-.3f)<.01f,"Reset restores exposure");File.WriteAllText(Out+"/pass.txt",DateTime.Now.ToString("s")+" light transition, local scatter, full flood and reset passed. Review screenshots for visual quality.");SessionState.SetInt("LampTest",0);EditorApplication.isPlaying=false;
        }
    }
    static void TransitionCapture(string name)
    {
        Capture(name);File.AppendAllText(Out+"/transition.txt",$"{name}: phase={C.CurrentPhase} elapsed={C.Elapsed:F2} reading={C.letter.IsReading} dim={Rig.Dimming:F4} tint={Rig.Transition:F4} fill={Rig.normalFillLights.Sum(l=>l.intensity):F3} ambient={RenderSettings.ambientLight}\n");
    }
    static void PlacePlayer(Vector3 pos)
    {
        var cc=C.player.GetComponent<CharacterController>();cc.enabled=false;C.player.position=pos;cc.enabled=true;
    }
    static void TransitionTest(int step)
    {
        if(step==601){C.enabled=false;PlacePlayer(V(18,1.05f,-8));Photo(V(0,2.2f,-5),V(0,7,32));Next(602,3);}
        else if(step==602){Check(Rig.Dimming==0,"Opening is not dimmed");TransitionCapture("bright-01-opening");Next(603,1);}
        else if(step==603){C.TryInteract();C.Advance(100);Next(604,2);}
        else if(step==604){Check(C.CurrentPhase==PoolWaterCycle.Phase.Empty && Rig.Dimming==0,"Drained pool stays bright before pickup");TransitionCapture("bright-02-before-letter");Next(605,1);}
        else if(step==605){PlacePlayer(C.letter.transform.position+Vector3.up);Check(C.letter.TryPickUp(),"Real letter pickup starts the transition");Next(606,2);}
        else if(step==606){Check(C.letter.IsReading && C.Elapsed==0 && Rig.Dimming==0,"No abrupt blackout on letter pickup");TransitionCapture("bright-03-letter-start");Next(607,1);}
        else if(step==607){C.Advance(4);Next(608,2);}
        else if(step==608){Check(C.letter.IsReading && Rig.Dimming>.2f && Rig.Dimming<.4f,"Visible gradual dimming while letter is open");TransitionCapture("bright-04-reading-4s");Next(609,1);}
        else if(step==609){C.Advance(4);Next(610,2);}
        else if(step==610){Check(C.letter.IsReading && Rig.Dimming>.6f && Rig.Dimming<.85f,"Dimming continues behind letter");TransitionCapture("bright-05-reading-8s");Next(611,1);}
        else if(step==611){C.letter.CloseLetter();C.Advance(4);Next(612,2);}
        else if(step==612){Check(Rig.Dimming>.999f && Rig.normalFillLights.All(l=>l.intensity<.001f),"Opening fill fully fades after 12 seconds");TransitionCapture("bright-06-dim-12s");Next(613,1);}
        else if(step==613){C.Advance(12);Next(614,2);}
        else if(step==614){Check(Rig.Transition>.999f,"Cyan tint completes at 24 seconds");TransitionCapture("bright-07-cyan-24s");Next(615,1);}
        else if(step==615){C.Advance(100);Photo(V(8,1.7f,51.3f),V(0,5,22));Next(616,3);}
        else if(step==616)
        {
            Check(C.CurrentPhase==PoolWaterCycle.Phase.Flooded && Rig.normalFillLights.All(l=>l.intensity==0),"No bright opening fill in final flood");
            Check(Rig.poolLamps.All(l=>Mathf.Abs(l.intensity-13*1.35f)<.01f),"Final underwater lamp intensity preserved");
            Check(Rig.hallLamps.All(l=>Mathf.Abs(l.intensity-(l.name=="Hall_Downlight"?32:28)*.32f)<.01f),"Final hall lamp intensity preserved");
            TransitionCapture("bright-08-final-flood");Next(617,1);
        }
        else if(step==617){C.ResetSequence(true);PlacePlayer(V(18,1.05f,-8));Photo(V(0,2.2f,-5),V(0,7,32));Next(618,3);}
        else if(step==618){Check(Rig.Dimming==0 && Rig.Transition==0 && Rig.normalFillLights.All(l=>Mathf.Abs(l.intensity-40)<.01f),"Reset restores bright white opening");TransitionCapture("bright-09-reset");Next(619,1);}
        else if(step==619){File.WriteAllText(Out+"/transition-pass.txt",DateTime.Now.ToString("s")+" PASS: bright opening and draining, real letter pickup, 4s/8s reading transition, 12s fill fade, 24s cyan, identical final lamp intensities, reset.");SessionState.SetInt("LampTest",0);EditorApplication.isPlaying=false;}
    }
}
