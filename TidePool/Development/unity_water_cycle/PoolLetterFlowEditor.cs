using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using AbstractOcclusion.WebGpuWater;

public static class PoolLetterFlowEditor
{
    const string ScenePath="Assets/_Game/Scenes/PoolHall_WaterCycle_01.unity";
    const string Root="Assets/_Game/WaterCycle";
    const string Out="D:/awestlakeselfstudy/unity_water_cycle";
    static double at,nextStatus;
    static bool release;
    [InitializeOnLoadMethod] static void Init()
    {
        EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        File.WriteAllText(Out+"/letter-ready.txt",DateTime.Now.ToString("s"));at=EditorApplication.timeSinceStartup+4;
    }
    static PoolWaterCycle Cycle=>UnityEngine.Object.FindFirstObjectByType<PoolWaterCycle>();
    static void Tick()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        try
        {
            string path=Out+"/letter-command.txt";
            if(File.Exists(path))
            {
                string cmd;
                // A local editor command can be observed before its writer releases the file.
                // Retry on the next editor tick instead of treating a transient lock as a scene error.
                try { cmd=File.ReadAllText(path).Trim();File.Delete(path); }
                catch(IOException) { return; }
                if(cmd=="install")Install();
                else if(cmd=="test")
                {
                    if(EditorApplication.isPlaying)throw new Exception("Stop Play first.");
                    Validate();SessionState.SetInt("LetterFlowTest",1);EditorApplication.isPlaying=true;at=EditorApplication.timeSinceStartup+5;
                }
                else if(cmd=="stop"){SessionState.SetInt("LetterFlowTest",0);EditorApplication.isPlaying=false;}
                else if(cmd=="restore")Restore();
                else if(cmd=="clear-tools-log")
                {
                    var logs=typeof(EditorWindow).Assembly.GetType("UnityEditor.LogEntries");
                    logs.GetMethod("Clear",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public).Invoke(null,null);
                }
            }
            if(EditorApplication.timeSinceStartup>nextStatus){nextStatus=EditorApplication.timeSinceStartup+4;Status();}
            if(!EditorApplication.isPlaying)return;
            if(release){InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());release=false;}
            if(SessionState.GetInt("LetterFlowTest",0)>0 && EditorApplication.timeSinceStartup>=at)TestStep();
        }
        catch(Exception e){SessionState.SetInt("LetterFlowTest",0);File.WriteAllText(Out+"/letter-error.txt",e.ToString());Debug.LogException(e);}
    }
    [MenuItem("Tools/TidePool/Install Pool Letter Sequence")]
    static void Install()
    {
        if(EditorApplication.isPlaying)throw new Exception("Exit Play first.");
        if(SceneManager.GetActiveScene().path!=ScenePath)EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        var scene=SceneManager.GetActiveScene();EditorSceneManager.SaveScene(scene);
        if(!File.Exists(Out+"/BeforeLetterSequence.unity"))File.Copy(ScenePath,Out+"/BeforeLetterSequence.unity");
        var c=Cycle;if(!c)throw new Exception("Missing cycle controller");
        c.drainSeconds=8;c.refillSeconds=16;c.platformFloodSeconds=18;c.hallFloodSeconds=26;
        if(!c.letter)
        {
            var root=new GameObject("Pool_Letter_Sequence");
            var letter=new GameObject("Pool_Floor_Letter");letter.transform.SetParent(root.transform);letter.transform.position=new Vector3(7.8f,-2.985f,8);
            var pickup=letter.AddComponent<PoolLetterPickup>();pickup.cycle=c;c.letter=pickup;
            var paper=Material("M_Letter_Paper",new Color(.86f,.85f,.76f),false);
            var ink=Material("M_Letter_Ink",new Color(.19f,.28f,.32f),true);
            var visual=new GameObject("Envelope_Visual");visual.transform.SetParent(letter.transform,false);pickup.envelopeVisual=visual;
            var sheet=GameObject.CreatePrimitive(PrimitiveType.Cube);sheet.name="Envelope";sheet.transform.SetParent(visual.transform,false);sheet.transform.localScale=new Vector3(.66f,.008f,.46f);sheet.GetComponent<Renderer>().sharedMaterial=paper;
            UnityEngine.Object.DestroyImmediate(sheet.GetComponent<Collider>());
            var fold=new GameObject("Envelope fold");fold.transform.SetParent(visual.transform,false);var line=fold.AddComponent<LineRenderer>();
            line.sharedMaterial=ink;line.useWorldSpace=false;line.positionCount=3;line.widthMultiplier=.0045f;
            line.SetPositions(new[]{new Vector3(-.31f,.009f,-.2f),new Vector3(0,.009f,.06f),new Vector3(.31f,.009f,-.2f)});
            line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            var glint=new GameObject("Letter_Glint");glint.transform.SetParent(letter.transform,false);glint.transform.localPosition=new Vector3(0,.24f,0);
            var light=glint.AddComponent<Light>();light.type=LightType.Point;light.range=1.8f;light.intensity=.7f;light.color=new Color(.6f,.8f,1);light.shadows=LightShadows.None;pickup.revealLight=light;
            visual.SetActive(false);light.enabled=false;
            var roof=new GameObject("Ceiling_Seepage");roof.transform.SetParent(root.transform);var seep=roof.AddComponent<CeilingSeepage>();
            seep.cycle=c;seep.water=c.waterFrame.GetComponent<WaterVolume>();c.seepage=seep;
            var points=new List<Vector3>();Physics.SyncTransforms();
            for(float x=-18;x<=18;x+=4)for(float z=-8;z<=40;z+=4)
            {
                if(Physics.Raycast(new Vector3(x,.3f,z),Vector3.up,out RaycastHit hit,23) && hit.point.y>2.5f)
                    points.Add(hit.point-Vector3.up*.04f);
            }
            if(points.Count<30)throw new Exception("Too few actual ceiling samples: "+points.Count);
            seep.ceilingPoints=points.ToArray();
            Shader waterShader=AssetDatabase.LoadAssetAtPath<Shader>(Root+"/RoofWater.shader");
            if(!waterShader || ShaderUtil.ShaderHasError(waterShader))throw new Exception("Roof water shader failed.");
            seep.dropletsMaterial=RoofMaterial("M_Roof_Droplets",waterShader,0);
            seep.streamMaterial=RoofMaterial("M_Roof_Streams",waterShader,1);
        }
        EditorUtility.SetDirty(c);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Validate();Restore();File.WriteAllText(Out+"/letter-installed.txt",DateTime.Now.ToString("s"));
    }
    static Material Material(string name,Color color,bool unlit)
    {
        string path=Root+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m)return m;
        m=new Material(Shader.Find(unlit?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit"));m.name=name;m.SetColor("_BaseColor",color);if(!unlit)m.SetFloat("_Smoothness",.2f);AssetDatabase.CreateAsset(m,path);return m;
    }
    static Material RoofMaterial(string name,Shader shader,float stream)
    {
        string path=Root+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(shader){name=name};AssetDatabase.CreateAsset(m,path);}m.SetFloat("_Stream",stream);EditorUtility.SetDirty(m);return m;
    }
    static void Require(bool yes,string text){if(!yes)throw new Exception("Letter sequence check failed: "+text);}
    static void Validate()
    {
        var c=Cycle;Require(c && c.letter && c.seepage,"References assigned");
        Require(c.seepage.ceilingPoints.Length>=30,"Roof samples");
        Require(!ShaderUtil.ShaderHasError(c.seepage.dropletsMaterial.shader),"Drip shader compiled");
        Require(c.drainSeconds==8 && c.refillSeconds+c.platformFloodSeconds+c.hallFloodSeconds==60,"Faster timings");
        Require(Physics.Raycast(c.letter.transform.position+Vector3.up,Vector3.down,out RaycastHit hit,2) && Mathf.Abs(hit.point.y+3)<.08f,"Letter on pool floor");
        Require(SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject))==0,"No missing scripts");
        File.WriteAllText(Out+"/letter-validation.txt","PASS: letter/floor/roof references, shaders, faster timings, no missing scripts. Roof sources="+c.seepage.ceilingPoints.Length);
    }
    static void Restore()
    {
        if(EditorApplication.isPlaying)throw new Exception("Exit Play first.");
        EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).maximized=false;
        Selection.activeGameObject=Cycle.gameObject;
        var view=EditorWindow.GetWindow<SceneView>();view.LookAt(new Vector3(0,5,17),Quaternion.Euler(20,-25,0),35);view.Focus();Status();
    }
    static void Status()
    {
        var c=Cycle;if(!c)return;
        File.WriteAllText(Out+"/letter-status.json",JsonUtility.ToJson(new Report{time=DateTime.Now.ToString("s"),playing=EditorApplication.isPlaying,
            phase=c.CurrentPhase.ToString(),height=c.WaterHeight,elapsed=c.Elapsed,picked=c.letter&&c.letter.PickedUp,reading=c.letter&&c.letter.IsReading,
            intensity=c.seepage?c.seepage.Intensity:0,particles=c.seepage?c.seepage.LiveParticles:0,streams=c.seepage?c.seepage.VisibleStreams:0,
            stage=SessionState.GetInt("LetterFlowTest",0)},true));
    }
    [Serializable]class Report{public string time,phase;public bool playing,picked,reading;public float height,elapsed,intensity;public int particles,streams,stage;}
    static void Next(int stage,double delay){SessionState.SetInt("LetterFlowTest",stage);at=EditorApplication.timeSinceStartup+delay;Status();}
    static void Key(Key key){InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(key));release=true;}
    static void Move(Vector3 p){var cc=Cycle.player.GetComponent<CharacterController>();cc.enabled=false;Cycle.player.position=p;cc.enabled=true;}
    static void View(Vector3 p,Vector3 look)
    {
        var cam=Camera.main;cam.GetComponent<CameraFollow>().enabled=false;cam.GetComponent<DryRegionCameraGuard>().enabled=false;
        cam.transform.SetPositionAndRotation(p,Quaternion.LookRotation(look-p));Cycle.showHints=false;
        EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).maximized=true;
    }
    static void Snap(string n){ScreenCapture.CaptureScreenshot(Out+"/letter-"+n+".png");Status();}
    static void TestStep()
    {
        var c=Cycle;int stage=SessionState.GetInt("LetterFlowTest",0);
        if(stage==1)
        {
            Require(!c.letter.IsRevealed && !c.letter.TryPickUp(),"No early pickup");
            Move(new Vector3(2.5f,1,-3.3f));Key(UnityEngine.InputSystem.Key.E);Next(2,3);
        }
        else if(stage==2){Require(c.CurrentPhase==PoolWaterCycle.Phase.Draining,"Button drains");Next(3,6);}
        else if(stage==3)
        {
            Require(c.CurrentPhase==PoolWaterCycle.Phase.Empty && c.letter.IsRevealed,"Eight second drain reveals letter");
            Require(!c.TryInteract(),"Button cannot refill");Require(!c.letter.TryPickUp(),"Distant pickup blocked");
            View(new Vector3(8,-1.2f,5.2f),c.letter.transform.position);Next(4,2);
        }
        else if(stage==4){Snap("revealed");Next(5,1);}
        else if(stage==5){Move(new Vector3(7.8f,-1.92f,7));Key(UnityEngine.InputSystem.Key.E);Next(6,3);}
        else if(stage==6)
        {
            Require(c.letter.PickedUp && c.letter.IsReading && c.CurrentPhase==PoolWaterCycle.Phase.Filling,"Letter pickup starts flood while reading");
            Require(!c.player.GetComponent<PlayerMovement>().enabled,"Reading holds movement");
            Require(c.seepage.Intensity>0 && c.seepage.LiveParticles>0,"Drips begin immediately");
            Snap("reading");Next(7,1);
        }
        else if(stage==7){Key(UnityEngine.InputSystem.Key.Escape);Next(8,2);}
        else if(stage==8)
        {
            Require(!c.letter.IsReading && c.player.GetComponent<PlayerMovement>().enabled,"Closing restores movement");
            Require(!c.letter.TryPickUp(),"Letter triggers only once");
            View(new Vector3(9.5f,1.8f,3),new Vector3(0,6,22));Next(9,14);
        }
        else if(stage==9)
        {
            Require(c.seepage.Intensity>.65f && c.seepage.VisibleStreams>5,"Seepage grows into streams");
            Require(c.WaterHeight>0,"Pool overflows");Snap("seepage");Next(10,1);
        }
        else if(stage==10)
        {
            Move(new Vector3(8,1,39.5f));View(new Vector3(8,1.75f,39.3f),new Vector3(-2,2.6f,15));Next(11,3);
        }
        else if(stage==11){Snap("dry-corner");Next(12,1);}
        else if(stage==12){Next(13,Mathf.Max(.5f,61-c.Elapsed));}
        else if(stage==13)
        {
            Require(c.CurrentPhase==PoolWaterCycle.Phase.Flooded && Mathf.Abs(c.WaterHeight-18.5f)<.001f,"Sixty second real time flood endpoint");
            var seep=c.seepage;var ps=seep.GetComponentInChildren<ParticleSystem>();var all=new ParticleSystem.Particle[6000];int n=ps.GetParticles(all);
            Require(!all.Take(n).Any(p=>p.remainingLifetime>0 && Mathf.Abs(p.position.x-8)<2 && Mathf.Abs(p.position.z-39.5f)<2),"No wet particles in dry column");
            Camera.main.GetComponent<CameraFollow>().enabled=true;Camera.main.GetComponent<DryRegionCameraGuard>().enabled=true;Next(14,2);
        }
        else if(stage==14)
        {
            Require(Camera.main.GetComponent<DryRegionCameraGuard>().IsPlayerInside,"Camera dry region retained");
            Key(UnityEngine.InputSystem.Key.R);Next(15,2);
        }
        else if(stage==15)
        {
            Require(c.CurrentPhase==PoolWaterCycle.Phase.Pool && !c.letter.PickedUp && !c.letter.IsReading,"Reset restores pool and letter");
            Require(c.seepage.LiveParticles==0 && c.seepage.VisibleStreams==0,"Reset stops roof water");
            File.WriteAllText(Out+"/letter-test-pass.txt","PASS: real-time 8 second drain; early/distant pickup blocked; E at letter starts flood; water continues during Chinese letter reading; close restores movement; second console activation blocked; drips become threads; real-time 60 second fill reaches 18.5m; dry column/camera; R resets letter, water and roof.\n"+DateTime.Now.ToString("s"));
            SessionState.SetInt("LetterFlowTest",0);EditorApplication.isPlaying=false;
        }
    }
}
