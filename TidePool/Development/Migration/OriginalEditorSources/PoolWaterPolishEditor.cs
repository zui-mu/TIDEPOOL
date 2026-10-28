using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using AbstractOcclusion.WebGpuWater;

public static class PoolWaterPolishEditor
{
    const string Out="D:/awestlakeselfstudy/unity_water_cycle";
    static double at;
    static bool release;
    static PoolWaterCycle C=>UnityEngine.Object.FindFirstObjectByType<PoolWaterCycle>();
    [InitializeOnLoadMethod]static void Init(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;File.WriteAllText(Out+"/polish-ready.txt",DateTime.Now.ToString("s"));at=EditorApplication.timeSinceStartup+4;}
    static void Tick()
    {
        if(EditorApplication.isUpdating||EditorApplication.isCompiling)return;
        try
        {
            string path=Out+"/polish-command.txt";
            if(File.Exists(path))
            {
                string cmd;try{cmd=File.ReadAllText(path).Trim();File.Delete(path);}catch(IOException){return;}
                if(cmd=="install")Install();
                else if(cmd=="test")
                {
                    Check(!EditorApplication.isPlaying,"Exit Play before test");
                    SessionState.SetInt("WaterPolishTest",1);EditorApplication.isPlaying=true;at=EditorApplication.timeSinceStartup+5;
                }
            }
            if(!EditorApplication.isPlaying)return;
            if(release){InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());release=false;}
            if(SessionState.GetInt("WaterPolishTest",0)>0 && EditorApplication.timeSinceStartup>=at)Test();
        }
        catch(Exception e){SessionState.SetInt("WaterPolishTest",0);File.WriteAllText(Out+"/polish-error.txt",e.ToString());Debug.LogException(e);}
    }
    static void Install()
    {
        Check(!EditorApplication.isPlaying,"Exit Play first");
        Check(SceneManager.GetActiveScene().path=="Assets/_Game/Scenes/PoolHall_WaterCycle_01.unity","Open water-cycle scene");
        var scene=SceneManager.GetActiveScene();EditorSceneManager.SaveScene(scene);
        if(!File.Exists(Out+"/BeforeSurfacePolish.unity"))File.Copy(scene.path,Out+"/BeforeSurfacePolish.unity");
        var surface=C.GetComponent<FloodSurfaceState>();if(!surface)surface=C.gameObject.AddComponent<FloodSurfaceState>();surface.cycle=C;
        surface.deckRenderers=new[]{"Deck_South","Deck_North","Deck_West","Deck_East"}.Select(n=>GameObject.Find(n).GetComponent<Renderer>()).ToArray();
        Check(surface.deckRenderers.All(r=>r.sharedMaterial.HasProperty("_FloodWetness")),"Flood shader property imported");
        Check(!ShaderUtil.ShaderHasError(surface.deckRenderers[0].sharedMaterial.shader),"Floor shader compiled");
        Check(!ShaderUtil.ShaderHasError(C.seepage.dropletsMaterial.shader),"Seep shader compiled");
        var atmosphere=C.GetComponent<FloodAtmosphere>();if(!atmosphere)atmosphere=C.gameObject.AddComponent<FloodAtmosphere>();
        atmosphere.cycle=C;atmosphere.globalVolume=GameObject.Find("Global Volume").GetComponent<Volume>();
        var ws=new SerializedObject(C.waterFrame.GetComponent<WaterVolume>());
        atmosphere.sun=ws.FindProperty("sun").objectReferenceValue as Light;
        Camera.main.GetUniversalAdditionalCameraData().renderPostProcessing=true;
        EditorUtility.SetDirty(surface);EditorUtility.SetDirty(atmosphere);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        File.WriteAllText(Out+"/polish-installed.txt",DateTime.Now.ToString("s"));
    }
    static void Check(bool ok,string message){if(!ok)throw new Exception("Polish check: "+message);}
    static void Next(int n,double delay){SessionState.SetInt("WaterPolishTest",n);at=EditorApplication.timeSinceStartup+delay;}
    static void Photo(Vector3 p,Vector3 look)
    {
        var cam=Camera.main;cam.GetComponent<CameraFollow>().enabled=false;cam.GetComponent<DryRegionCameraGuard>().enabled=false;
        cam.transform.SetPositionAndRotation(p,Quaternion.LookRotation(look-p));C.showHints=false;
        EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).maximized=true;
    }
    static void Snapshot(string name)
    {
        ScreenCapture.CaptureScreenshot(Out+"/polish-"+name+".png");
        var a=C.GetComponent<FloodAtmosphere>();var s=C.GetComponent<FloodSurfaceState>();
        File.WriteAllText(Out+"/polish-"+name+".json",JsonUtility.ToJson(new Report{phase=C.CurrentPhase.ToString(),height=C.WaterHeight,
            wetness=s.FloodBlend,atmosphere=a.Blend,exposure=a.Exposure,particles=C.seepage.LiveParticles,streams=C.seepage.VisibleStreams,
            shaderEnabled=Shader.GetGlobalFloat("_TidePoolSeepEnabled")},true));
    }
    [Serializable]class Report{public string phase;public float height,wetness,atmosphere,exposure,shaderEnabled;public int particles,streams;}
    static void Test()
    {
        int stage=SessionState.GetInt("WaterPolishTest",0);var surface=C.GetComponent<FloodSurfaceState>();var air=C.GetComponent<FloodAtmosphere>();
        if(stage==1)
        {
            Check(surface.FloodBlend==0 && air.Blend==0,"Original dry-daylight start");
            Photo(new Vector3(-5,2.4f,-3),new Vector3(1,3,22));Snapshot("initial");Next(2,1);
        }
        else if(stage==2)
        {
            Check(C.TryInteract(),"Drain start");C.Advance(C.drainSeconds+.1f);Check(C.BeginFloodFromLetter(),"Flood start");C.Advance(C.refillSeconds+4);Next(3,3);
        }
        else if(stage==3)
        {
            Check(C.WaterHeight>.02f && surface.FloodBlend>.999f,"Flooded deck is uniformly wet");
            var block=new MaterialPropertyBlock();
            foreach(var renderer in surface.deckRenderers){renderer.GetPropertyBlock(block);Check(block.GetFloat("_FloodWetness")>.999f,"All four decks receive wetness");}
            Check(C.seepage.LiveParticles>0 && Shader.GetGlobalFloat("_TidePoolSeepEnabled")==1,"Airborne seepage still renders");
            Check(Shader.GetGlobalFloat("_TidePoolSeepWaterLevel")>=C.WaterHeight+.03f,"Droplet tails clipped at water surface");
            Check(air.Blend>.2f && air.Blend<1 && air.Exposure<.25f,"Gradual darkening in progress");
            Snapshot("overflow");Next(4,1);
        }
        else if(stage==4)
        {
            C.Advance(100);
            var cc=C.player.GetComponent<CharacterController>();cc.enabled=false;C.player.position=new Vector3(8,1,39.5f);cc.enabled=true;
            Photo(new Vector3(8,1.75f,39.3f),new Vector3(-2,2,15));Next(5,3);
        }
        else if(stage==5)
        {
            Check(C.CurrentPhase==PoolWaterCycle.Phase.Flooded,"Room fully flooded");
            Check(C.seepage.LiveParticles==0 && C.seepage.VisibleStreams==0 && C.seepage.Intensity==0,"No submerged leak animation");
            Check(Shader.GetGlobalFloat("_TidePoolSeepEnabled")==0,"Shader also stops all seepage");
            Check(air.Blend>.999f && air.Exposure<-.9f,"Final deep blue darkening reached");
            Snapshot("flooded");Next(6,1);
        }
        else if(stage==6)
        {
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.R));release=true;Next(7,3);
        }
        else if(stage==7)
        {
            Check(C.CurrentPhase==PoolWaterCycle.Phase.Pool && surface.FloodBlend==0 && air.Blend==0,"R restores original wet pattern and lighting");
            Check(C.seepage.LiveParticles==0 && Shader.GetGlobalFloat("_TidePoolSeepEnabled")==0,"No seepage after reset");
            var original=AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/_Game/HallDream/VP_ColdDream.asset");
            Check(original.TryGet<ColorAdjustments>(out var color) && Mathf.Abs(color.postExposure.value-.25f)<.001f && color.colorFilter.value==Color.white,"Original lighting asset unchanged");
            File.WriteAllText(Out+"/polish-test-pass.txt","PASS: initial/reset lighting and wetness; uniformly soaked decks on overflow; particle/thread fragment clipping above water; all seepage disabled when flooded; gradual blue darkening; original profile unchanged.\n"+DateTime.Now.ToString("s"));
            SessionState.SetInt("WaterPolishTest",0);EditorApplication.isPlaying=false;
        }
    }
}
