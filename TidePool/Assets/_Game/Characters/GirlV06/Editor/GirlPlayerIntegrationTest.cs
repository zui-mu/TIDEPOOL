using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Exercises real PlayerMovement.Update, Animator transitions, collisions and scene references.
[InitializeOnLoad]
public static class GirlPlayerIntegrationTest
{
    const string Running="GirlPlayerIntegrationTest.Running";
    const string Batch="GirlPlayerIntegrationTest.Batch";
    [Serializable] public class Report
    {
        public string date,scene; public bool passed;
        public float measuredWalkSpeed,measuredRunSpeed,blockedSpeed,jumpRise,footGap;
        public List<string> checks=new List<string>(),errors=new List<string>();
    }
    static Report report;
    static PlayerMovement movement;static CharacterController cc;static GirlPlayerAnimation bridge;
    static Camera camera;static CameraFollow follow;static Keyboard keyboard,previousKeyboard;
    static GameObject floor,wall;static Vector3 spawn,stagePosition;static Quaternion spawnRotation;
    static int stage,frames,lastFrame;static double deadline;static float jumpBase,maxJump;
    static bool sawAirPose;static float oldCaptureDelta;static bool oldRunInBackground;
    static InputSettings.BackgroundBehavior oldBackground;
    static InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;
    static bool inputSettingsOverridden;
    static string Out=>GirlPlayerIntegration.Out;
    static GirlPlayerIntegrationTest()
    {
        EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;
    }
    [MenuItem("Tools/Girl V06/6 - Test integrated player")]
    public static void Begin()
    {
        GirlPlayerIntegration.Require(!EditorApplication.isPlayingOrWillChangePlaymode,"Exit Play before testing.");
        GirlPlayerIntegration.Require(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path==GirlPlayerIntegration.ScenePath,"Open integrated ArmLab first.");
        GirlPlayerIntegration.Require(!UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty,"Save scene before runtime test.");
        SessionState.SetBool(Running,true);EditorApplication.isPlaying=true;
    }
    public static void RunBatch()
    {
        if(!Application.isBatchMode)throw new InvalidOperationException("RunBatch is for a separate batch editor.");
        EditorSceneManager.OpenScene(GirlPlayerIntegration.ScenePath);
        SessionState.SetBool(Batch,true);
        Begin();
    }
    static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Running,false)) return;
        if(state==PlayModeStateChange.EnteredPlayMode)
        {
            report=new Report{date=DateTime.Now.ToString("s"),scene=GirlPlayerIntegration.ScenePath};
            oldCaptureDelta=Time.captureDeltaTime;oldRunInBackground=Application.runInBackground;
            try
            {
                movement=UnityEngine.Object.FindFirstObjectByType<PlayerMovement>();cc=movement.GetComponent<CharacterController>();
                bridge=movement.GetComponent<GirlPlayerAnimation>();
                Check(bridge&&bridge.animator&&bridge.animator.isHuman,"Player uses girl's Humanoid Animator");
                Check(!bridge.animator.applyRootMotion,"Code controls displacement; root motion disabled");
                Check(!movement.GetComponent<MeshRenderer>().enabled,"Legacy capsule renderer hidden");
                Check(bridge.GetComponentsInChildren<SkinnedMeshRenderer>().Length==4,"Four character meshes attached before camera renderer caches");
                spawn=movement.transform.position;spawnRotation=movement.transform.rotation;
                camera=Camera.main;follow=camera.GetComponent<CameraFollow>();follow.enabled=false;
                camera.transform.rotation=Quaternion.identity;
                oldBackground=InputSystem.settings.backgroundBehavior;
                oldEditorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
                inputSettingsOverridden=true;
                InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                previousKeyboard=Keyboard.current;keyboard=InputSystem.AddDevice<Keyboard>("GirlIntegrationTestKeyboard");keyboard.MakeCurrent();
                Time.captureDeltaTime=1f/60f;
                Application.runInBackground=true;
                floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Temporary character test floor";
                floor.transform.position=new Vector3(50,-.25f,8);floor.transform.localScale=new Vector3(20,.5f,30);
                Teleport(new Vector3(50,1,0));Physics.SyncTransforms();
                stage=0;frames=0;lastFrame=-1;deadline=EditorApplication.timeSinceStartup+240;
                Application.logMessageReceived+=Log;EditorApplication.update+=Tick;Save();
            }
            catch(Exception e){Fail(e);}
        }
        if(state==PlayModeStateChange.ExitingPlayMode) Cleanup();
        if(state==PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Running,false);
            if(report!=null) Save();
            if(SessionState.GetBool(Batch,false))
            {
                SessionState.SetBool(Batch,false);
                var final=JsonUtility.FromJson<Report>(File.ReadAllText(Path.Combine(Out,"runtime-report.json")));
                EditorApplication.delayCall+=()=>EditorApplication.Exit(final.passed&&final.errors.Count==0?0:2);
            }
        }
    }
    static void Log(string text,string stack,LogType type)
    {
        if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)
        {report.errors.Add(type+": "+text+"\n"+stack);Save();}
    }
    static void Check(bool ok,string message)
    {
        if(!ok)throw new InvalidOperationException(message);report.checks.Add(message);
    }
    static void Keys(params Key[] keys){InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));}
    static void Teleport(Vector3 position)
    {
        cc.enabled=false;movement.transform.position=position;cc.enabled=true;Physics.SyncTransforms();
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying)return;
        if(EditorApplication.timeSinceStartup>deadline){Fail(new TimeoutException("Character runtime test timed out at stage "+stage));return;}
        if(Time.frameCount==lastFrame)return;lastFrame=Time.frameCount;frames++;
        try
        {
            switch(stage)
            {
                case 0:
                    Keys();if(frames<25)return;
                    Check(movement.IsGrounded,"Character settles onto floor");
                    stagePosition=movement.transform.position;Next();break;
                case 1:
                    Keys(Key.W);if(frames<90)return;
                    report.measuredWalkSpeed=movement.PlanarVelocity.magnitude;
                    Check(Mathf.Abs(report.measuredWalkSpeed-movement.WalkSpeed)<.12f,"Walking reaches configured speed");
                    Check(bridge.animator.GetFloat("MoveSpeed")>movement.WalkSpeed*.9f,"Walk animation follows actual movement");
                    Check(Vector3.Distance(stagePosition,movement.transform.position)>1.5f,"W moves the existing Player root");
                    Next();break;
                case 2:
                    Keys(Key.W,Key.LeftShift);if(frames<90)return;
                    report.measuredRunSpeed=movement.PlanarVelocity.magnitude;
                    Check(Mathf.Abs(report.measuredRunSpeed-movement.RunSpeed)<.15f,"Shift running reaches configured speed");
                    Check(bridge.animator.GetCurrentAnimatorClipInfo(0).Any(c=>c.clip.name.Contains("Run")&&c.weight>.85f),"Run clip blends in at running speed");
                    Next();break;
                case 3:
                    Keys();if(frames<45)return;
                    Check(movement.PlanarVelocity.magnitude<.035f&&bridge.animator.GetFloat("MoveSpeed")<.05f,"Release keys returns movement and animation to idle");
                    Next();break;
                case 4:
                    Keys(Key.D);if(frames<60)return;
                    Check(Vector3.Dot(bridge.visualRoot.forward,Vector3.right)>.97f,"Visual turns toward movement without rotating Player root");
                    Check(Quaternion.Angle(movement.transform.rotation,spawnRotation)<.01f,"Gameplay root orientation preserved");
                    wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Temporary character test wall";
                    wall.transform.position=movement.transform.position+Vector3.right*.9f;
                    wall.transform.localScale=new Vector3(.3f,4,4);Physics.SyncTransforms();Next();break;
                case 5:
                    Keys(Key.D);if(frames<90)return;
                    report.blockedSpeed=movement.PlanarVelocity.magnitude;
                    Check(report.blockedSpeed<.04f,"Collider blocks motion into wall");
                    Check(bridge.animator.GetFloat("MoveSpeed")<.06f,"Blocked character stops walking animation despite held input");
                    UnityEngine.Object.DestroyImmediate(wall);wall=null;Keys();Next();break;
                case 6:
                    Keys();if(frames<30)return;
                    jumpBase=movement.transform.position.y;maxJump=jumpBase;sawAirPose=false;Keys(Key.Space);Next();break;
                case 7:
                    Keys();maxJump=Mathf.Max(maxJump,movement.transform.position.y);
                    sawAirPose|=bridge.animator.GetCurrentAnimatorStateInfo(0).IsName("AirborneHold");
                    if(frames<150)return;
                    report.jumpRise=maxJump-jumpBase;
                    Check(report.jumpRise>.95f&&report.jumpRise<1.35f,"Space jump retains existing jump height");
                    Check(sawAirPose,"Full jump state replaces walk/run during preparation and flight");
                    Check(movement.IsGrounded&&bridge.animator.GetBool("Grounded"),"Landing returns Animator to grounded locomotion");
                    Check(!bridge.animator.GetBool("JumpActive")&&bridge.animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"),"Complete landing recovery finishes before locomotion resumes");
                    var cycle=UnityEngine.Object.FindFirstObjectByType<PoolWaterCycle>();
                    if(cycle)
                    {
                        Check(cycle.player==movement.transform,"Water sequence still references same Player");
                        Check(cycle.TryInteract()&&cycle.CurrentPhase==PoolWaterCycle.Phase.Draining,"Water drain interaction still works");
                        cycle.ResetSequence(true);
                        Check(Vector3.Distance(movement.transform.position,spawn)<.01f,"Water sequence reset still restores Player spawn");
                    }
                    else Teleport(spawn);
                    camera.transform.rotation=Quaternion.Euler(12,0,0);follow.enabled=true;
                    Next();break;
                case 8:
                    Keys();if(frames<35)return;
                    var body=bridge.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.name.Contains("Body"));
                    var baked=new Mesh();body.BakeMesh(baked);
                    float minY=baked.vertices.Min(v=>body.transform.TransformPoint(v).y);UnityEngine.Object.DestroyImmediate(baked);
                    if(Physics.Raycast(new Vector3(movement.transform.position.x,minY+.2f,movement.transform.position.z),Vector3.down,out RaycastHit hit,1,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                    {report.footGap=minY-hit.point.y;Check(Mathf.Abs(report.footGap)<.08f,"Idle feet align with scene floor within 8 cm");}
                    else Check(false,"Could not find floor below player's feet");
                    Check(bridge.GetComponentsInChildren<SkinnedMeshRenderer>().All(r=>r.enabled&&!r.forceRenderingOff),"Character visible with camera and dry-region guards enabled");
                    var guard=camera.GetComponent<DryRegionCameraGuard>();
                    if(guard&&guard.dryRegion){Teleport(guard.dryRegion.transform.position);Next();}
                    else {stage=10;frames=0;}break;
                case 9:
                    Keys();if(frames<12)return;
                    Check(bridge.GetComponentsInChildren<SkinnedMeshRenderer>().All(r=>r.forceRenderingOff),"Dry-region camera hides all girl meshes");
                    Teleport(spawn);Next();break;
                case 10:
                    Keys();if(frames<30)return;
                    Check(bridge.GetComponentsInChildren<SkinnedMeshRenderer>().All(r=>!r.forceRenderingOff),"Girl visibility restores after leaving dry region");
                    Capture();report.passed=report.errors.Count==0;Save();
                    EditorApplication.update-=Tick;EditorApplication.isPlaying=false;break;
            }
        }
        catch(Exception e){Fail(e);}
    }
    static void Next(){stage++;frames=0;Save();EditorApplication.QueuePlayerLoopUpdate();}
    static void Save()=>File.WriteAllText(Path.Combine(Out,"runtime-report.json"),JsonUtility.ToJson(report,true));
    static void Fail(Exception error)
    {
        if(report==null)report=new Report{date=DateTime.Now.ToString("s")};
        report.errors.Add("Stage "+stage+": "+error);report.passed=false;Save();
        EditorApplication.update-=Tick;EditorApplication.isPlaying=false;
    }
    static void Capture()
    {
        var rt=new RenderTexture(1280,800,24);rt.Create();var previous=RenderTexture.active;
        camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
        var image=new Texture2D(1280,800,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();
        File.WriteAllBytes(Path.Combine(Out,"Girl_in_ArmLab.png"),image.EncodeToPNG());
        camera.targetTexture=null;RenderTexture.active=previous;rt.Release();
        UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);
    }
    static void Cleanup()
    {
        EditorApplication.update-=Tick;Application.logMessageReceived-=Log;
        if(keyboard!=null&&keyboard.added){Keys();InputSystem.RemoveDevice(keyboard);}
        if(previousKeyboard!=null&&previousKeyboard.added)previousKeyboard.MakeCurrent();
        Time.captureDeltaTime=oldCaptureDelta;Application.runInBackground=oldRunInBackground;
        if(inputSettingsOverridden)
        {
            InputSystem.settings.backgroundBehavior=oldBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode=oldEditorInput;
            inputSettingsOverridden=false;
        }
        if(floor)UnityEngine.Object.DestroyImmediate(floor);if(wall)UnityEngine.Object.DestroyImmediate(wall);
    }
}
