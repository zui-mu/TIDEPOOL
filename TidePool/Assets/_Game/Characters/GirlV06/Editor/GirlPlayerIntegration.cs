using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Playables;
using UnityEngine.Animations;

[InitializeOnLoad]
public static class GirlPlayerIntegration
{
    public const string Root = "Assets/_Game/Characters/GirlV06";
    public const string ScenePath = "Assets/_Game/Scenes/PoolHall_ArmLab_01.unity";
    public static string Out => TidePoolDevelopmentPaths.Folder("character_integration");
    public static string ControllerPath => Root + "/Animations/GirlV06_Locomotion.controller";
    [Serializable] public class Report
    {
        public string date,scene,backup,controller;
        public bool passed,rootPreserved,cameraReferencePreserved,waterReferencePreserved;
        public float walkSpeed,runSpeed,walkStrideSpeed,runStrideSpeed,walkClipRate,runClipRate,bodyHeight,visualFootOffset;
        public Vector3 playerPosition,colliderCenter;
        public float colliderHeight,colliderRadius;
        public List<string> errors = new List<string>();
    }
    static GirlPlayerIntegration()
    {
        EditorApplication.update -= Poll; EditorApplication.update += Poll;
        EditorApplication.delayCall += () => WriteStatus("ready");
    }
    static void WriteStatus(string state)
    {
        var scene=SceneManager.GetActiveScene();
        File.WriteAllText(Path.Combine(Out,"status.txt"),$"{DateTime.Now:s}\nstate={state}\nscene={scene.path}\ndirty={scene.isDirty}\nplaying={EditorApplication.isPlaying}");
    }
    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        string path=Path.Combine(Out,"command.txt");
        if (!File.Exists(path)) return;
        string command;
        try { command=File.ReadAllText(path).Trim(); if(command.Length==0)return; File.Delete(path); }
        catch(IOException) { return; }
        try
        {
            if(command=="inspect") WriteStatus("inspected");
            else if(command=="install") Install();
            else if(command=="test") GirlPlayerIntegrationTest.Begin();
        }
        catch(Exception e)
        {
            File.WriteAllText(Path.Combine(Out,"command-error.txt"),e.ToString());
            Debug.LogException(e);
        }
    }
    public static void Require(bool ok,string message) { if(!ok) throw new InvalidOperationException(message); }
    [MenuItem("Tools/Girl V06/5 - Install player in ArmLab")]
    public static void Install()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode,"Exit Play Mode before installing the character.");
        var report=new Report{date=DateTime.Now.ToString("s"),scene=ScenePath,controller=ControllerPath,walkSpeed=1.55f,runSpeed=3.7f};
        try
        {
            if(SceneManager.GetActiveScene().path!=ScenePath)
            {
                for(int i=0;i<SceneManager.sceneCount;i++)
                    Require(!SceneManager.GetSceneAt(i).isDirty,"An open scene has unsaved edits; installation preserves them. Save it before opening ArmLab.");
                EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            }
            var scene=SceneManager.GetActiveScene();
            var player=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<PlayerMovement>(true)).Single();
            var cc=player.GetComponent<CharacterController>();
            Require(cc && player.transform.lossyScale==Vector3.one,"Expected unscaled Player CharacterController.");
            string backup=Path.Combine(Out,"BeforeIntegration"); Directory.CreateDirectory(backup);
            report.backup=Path.Combine(backup,"ArmLab_in_memory_before.unity");
            if(!File.Exists(report.backup)) Require(EditorSceneManager.SaveScene(scene,report.backup,true),"Could not back up in-memory scene.");
            var rootBefore=player.transform.position;var rotationBefore=player.transform.rotation;
            var follow=Camera.main.GetComponent<CameraFollow>();
            var cameraSO=new SerializedObject(follow);
            var targetBefore=cameraSO.FindProperty("target").objectReferenceValue;
            var cycle=UnityEngine.Object.FindFirstObjectByType<PoolWaterCycle>();
            Transform waterPlayer=cycle?cycle.player:null;
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/GirlV06_Character.prefab");
            Require(prefab,"Girl character prefab missing.");
            // Measure on a disposable instance: sampling must not save a posed skeleton as scene overrides.
            float foot;
            var probe=UnityEngine.Object.Instantiate(prefab);probe.name="Temporary stride calibration";
            probe.hideFlags=HideFlags.HideAndDontSave;
            try
            {
                var measure=probe.GetComponentInChildren<Animator>();
                Require(measure&&measure.avatar&&measure.avatar.isHuman&&measure.avatar.isValid,"Girl Avatar invalid.");
                measure.runtimeAnimatorController=null;measure.applyRootMotion=false;measure.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                measure.Rebind();measure.Update(0);
                var sourceClips=new[]{"Idle","Walk","Run"}.Select(n=>AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/Animations/Girl_"+n+".anim")).ToArray();
                Require(sourceClips.All(c=>c&&c.humanMotion),"Missing Humanoid locomotion clips.");
                report.walkStrideSpeed=EstimateStride(measure,sourceClips[1]);
                report.runStrideSpeed=EstimateStride(measure,sourceClips[2]);
                report.walkClipRate=report.walkSpeed/report.walkStrideSpeed;
                report.runClipRate=report.runSpeed/report.runStrideSpeed;
                Require(report.walkClipRate>.3f&&report.walkClipRate<3f&&report.runClipRate>.3f&&report.runClipRate<3f,"Unexpected cadence calibration; inspect the source animations.");
                Sample(measure,sourceClips[0],0);
                var probeBody=probe.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.name.Contains("Body"));
                var mesh=new Mesh();probeBody.BakeMesh(mesh);
                var verts=mesh.vertices.Select(v=>probe.transform.InverseTransformPoint(probeBody.transform.TransformPoint(v))).ToArray();
                foot=verts.Min(v=>v.y);report.bodyHeight=verts.Max(v=>v.y)-foot;UnityEngine.Object.DestroyImmediate(mesh);
            }
            finally { UnityEngine.Object.DestroyImmediate(probe); }
            var visual=player.transform.Find("GirlVisual");
            if(!visual)
            {
                visual=((GameObject)PrefabUtility.InstantiatePrefab(prefab,scene)).transform;
                visual.name="GirlVisual";visual.SetParent(player.transform,false);
            }
            // Remove only transform overrides generated by the earlier calibration in this installation.
            foreach(var t in visual.GetComponentsInChildren<Transform>(true))
                if(t!=visual&&PrefabUtility.IsPartOfPrefabInstance(t))PrefabUtility.RevertObjectOverride(t,InteractionMode.AutomatedAction);
            visual.localScale=Vector3.one;visual.localRotation=Quaternion.identity;visual.localPosition=Vector3.zero;
            var animator=visual.GetComponentInChildren<Animator>();
            var body=visual.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.name.Contains("Body"));
            var clips=new[]{"Idle","Walk","Run"}.Select(n=>AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/Animations/Girl_"+n+".anim")).ToArray();
            // Keep the existing root and ground plane: its root remains one metre above its feet.
            float bottom=cc.center.y-cc.height*.5f;
            cc.height=Mathf.Clamp(report.bodyHeight+.03f,1.6f,1.85f);cc.radius=.28f;
            cc.center=new Vector3(0,bottom+cc.height*.5f,0);cc.skinWidth=.025f;cc.stepOffset=.25f;cc.minMoveDistance=0;
            visual.localPosition=new Vector3(0,bottom+.012f-cc.skinWidth-foot,0);report.visualFootOffset=visual.localPosition.y;
            var movementSO=new SerializedObject(player);
            movementSO.FindProperty("walkSpeed").floatValue=report.walkSpeed;
            movementSO.FindProperty("runSpeed").floatValue=report.runSpeed;
            movementSO.FindProperty("acceleration").floatValue=10f;movementSO.ApplyModifiedPropertiesWithoutUndo();
            var controller=BuildController(clips,report);
            animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;
            animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
            PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
            var bridge=player.GetComponent<GirlPlayerAnimation>();if(!bridge)bridge=player.gameObject.AddComponent<GirlPlayerAnimation>();
            bridge.animator=animator;bridge.visualRoot=visual;
            var preview=visual.GetComponent<GirlAnimationPreview>();if(preview)UnityEngine.Object.DestroyImmediate(preview);
            var capsuleRenderer=player.GetComponent<MeshRenderer>();if(capsuleRenderer)capsuleRenderer.enabled=false;
            // Water simulation must sample the animated body, not the hidden legacy capsule mesh.
            var water=player.GetComponents<Component>().FirstOrDefault(c=>c&&c.GetType().FullName=="AbstractOcclusion.WebGpuWater.WaterInteractable");
            if(water)
            {
                var so=new SerializedObject(water);so.FindProperty("rendererOverride").objectReferenceValue=body;so.ApplyModifiedPropertiesWithoutUndo();
            }
            cameraSO.FindProperty("lookHeight").floatValue=.35f;
            cameraSO.FindProperty("distance").floatValue=4.5f;cameraSO.ApplyModifiedPropertiesWithoutUndo();
            report.rootPreserved=player.transform.position==rootBefore&&player.transform.rotation==rotationBefore;
            report.cameraReferencePreserved=cameraSO.FindProperty("target").objectReferenceValue==targetBefore&&targetBefore==player.transform;
            report.waterReferencePreserved=!cycle||cycle.player==waterPlayer&&cycle.player==player.transform;
            Require(report.rootPreserved&&report.cameraReferencePreserved&&report.waterReferencePreserved,"Player references changed unexpectedly.");
            report.playerPosition=player.transform.position;report.colliderCenter=cc.center;report.colliderHeight=cc.height;report.colliderRadius=cc.radius;
            EditorSceneManager.MarkSceneDirty(scene);Require(EditorSceneManager.SaveScene(scene),"Could not save integrated scene.");AssetDatabase.SaveAssets();
            report.passed=true;Selection.activeGameObject=player.gameObject;
            WriteStatus("installed");
        }
        catch(Exception e){report.errors.Add(e.ToString());Debug.LogException(e);}
        File.WriteAllText(Path.Combine(Out,"install-report.json"),JsonUtility.ToJson(report,true));
    }
    static AnimatorController BuildController(AnimationClip[] clips,Report report)
    {
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if(controller) return controller; // Reinstalling adjusts the scene without duplicating controller subassets.
        controller=AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("MoveSpeed",AnimatorControllerParameterType.Float);
        controller.AddParameter("Grounded",AnimatorControllerParameterType.Bool);
        controller.AddParameter("VerticalSpeed",AnimatorControllerParameterType.Float);
        var parameters=controller.parameters;parameters.First(p=>p.name=="Grounded").defaultBool=true;controller.parameters=parameters;
        var sm=controller.layers[0].stateMachine;
        var locomotion=sm.AddState("Locomotion");locomotion.iKOnFeet=true;
        var tree=new BlendTree{name="Actual ground speed",blendType=BlendTreeType.Simple1D,blendParameter="MoveSpeed",useAutomaticThresholds=false};
        AssetDatabase.AddObjectToAsset(tree,controller);
        tree.children=new[]{new ChildMotion{motion=clips[0],threshold=0,timeScale=1},
            new ChildMotion{motion=clips[1],threshold=report.walkSpeed,timeScale=report.walkClipRate},
            new ChildMotion{motion=clips[2],threshold=report.runSpeed,timeScale=report.runClipRate}};
        locomotion.motion=tree;sm.defaultState=locomotion;
        string posePath=Root+"/Animations/Girl_AirborneHold.anim";
        var pose=new AnimationClip{name="Girl_AirborneHold",frameRate=30};
        foreach(var binding in AnimationUtility.GetCurveBindings(clips[0]))
        {
            var curve=AnimationUtility.GetEditorCurve(clips[0],binding);
            float value=curve.Evaluate(.5f);
            AnimationUtility.SetEditorCurve(pose,binding,AnimationCurve.Constant(0,1,value));
        }
        AssetDatabase.CreateAsset(pose,posePath);
        var air=sm.AddState("AirborneHold");air.motion=pose;air.iKOnFeet=false;
        var takeoff=locomotion.AddTransition(air);takeoff.hasExitTime=false;takeoff.duration=.1f;takeoff.AddCondition(AnimatorConditionMode.IfNot,0,"Grounded");
        var landing=air.AddTransition(locomotion);landing.hasExitTime=false;landing.duration=.12f;landing.AddCondition(AnimatorConditionMode.If,0,"Grounded");
        EditorUtility.SetDirty(controller);return controller;
    }
    static void Sample(Animator animator,AnimationClip clip,double t)
    {
        var graph=PlayableGraph.Create("Girl contact measurement");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        try
        {
            var playable=AnimationClipPlayable.Create(graph,clip);playable.SetTime(t);playable.SetApplyFootIK(true);
            AnimationPlayableOutput.Create(graph,"Girl",animator).SetSourcePlayable(playable);graph.Play();graph.Evaluate(0);
        }
        finally{graph.Destroy();}
    }
    static float EstimateStride(Animator animator,AnimationClip clip)
    {
        const int count=120;var left=new Vector3[count];var right=new Vector3[count];
        var graph=PlayableGraph.Create("Girl stride calibration");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        try
        {
            var playable=AnimationClipPlayable.Create(graph,clip);playable.SetApplyFootIK(true);
            AnimationPlayableOutput.Create(graph,"Girl",animator).SetSourcePlayable(playable);graph.Play();
            for(int i=0;i<count;i++)
            {
                playable.SetTime(clip.length*i/count);graph.Evaluate(0);
                left[i]=animator.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.LeftFoot).position);
                right[i]=animator.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.RightFoot).position);
            }
        }
        finally{graph.Destroy();}
        var speeds=new List<float>();
        foreach(var samples in new[]{left,right})
        {
            float low=samples.Min(p=>p.y);float high=samples.Max(p=>p.y);float ceiling=low+(high-low)*.3f;
            for(int i=1;i<count;i++)
            {
                float backward=(samples[i-1].z-samples[i].z)*count/clip.length;
                if(samples[i].y<ceiling&&samples[i-1].y<ceiling&&backward>.1f)speeds.Add(backward);
            }
        }
        Require(speeds.Count>=8,"Not enough planted foot samples for "+clip.name);
        speeds.Sort();return speeds[speeds.Count/2];
    }
}



