using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class GirlV06ProjectImportCheck
{
    const string Root="Assets/_Game/Characters/GirlV06";
    static string Out=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Development/character_validation"));
    static string Request=>Path.Combine(Out,"check-import.request");
    [Serializable] public class Report
    {
        public string date,unityVersion,activeSceneBefore,activeSceneAfter;
        public bool scriptsCompiled,avatarValid,avatarHuman,shoulderComponent,sceneUntouched,passed;
        public int humanMappings,meshCount,triangles;
        public string[] animations,productionEditorDependencies;
        public List<string> errors=new List<string>();
    }
    static GirlV06ProjectImportCheck(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(!File.Exists(Request)){EditorApplication.update-=Poll;return;}
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)
        {return;}
        EditorApplication.update-=Poll;File.Delete(Request);Run();
    }
    [MenuItem("Tools/Girl V06/4 - Check project assets")]
    public static void Run()
    {
        Directory.CreateDirectory(Out);
        var before=SceneManager.GetActiveScene();bool dirtyBefore=before.isDirty;
        var report=new Report{date=DateTime.Now.ToString("s"),unityVersion=Application.unityVersion,activeSceneBefore=before.path,scriptsCompiled=true};
        try
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/GirlV06_Character.prefab");
            if(!prefab)throw new InvalidOperationException("Girl prefab missing");
            var animator=prefab.GetComponentInChildren<Animator>(true);
            if(!animator||!animator.avatar)throw new InvalidOperationException("Animator/Avatar missing");
            report.avatarValid=animator.avatar.isValid;report.avatarHuman=animator.avatar.isHuman;
            report.shoulderComponent=prefab.GetComponent<GirlShoulderCorrectives>()!=null;
            report.meshCount=prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;
            report.triangles=prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Sum(r=>r.sharedMesh.triangles.Length/3);
            report.humanMappings=((ModelImporter)AssetImporter.GetAtPath(Root+"/Models/Girl_Shoulder_v06.fbx")).humanDescription.human.Length;
            var names=new[]{"Idle","Walk","Run"};
            report.animations=names.Select(n=>{
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/Animations/Girl_"+n+".anim");
                if(!clip||!clip.humanMotion||!clip.isLooping)throw new InvalidOperationException("Invalid Humanoid loop: "+n);
                return n+" seconds="+clip.length+" humanoid="+clip.humanMotion+" looping="+clip.isLooping;
            }).ToArray();
            report.productionEditorDependencies=AssetDatabase.GetDependencies(Root+"/GirlV06_Character.prefab",true).Where(p=>p.StartsWith(Root+"/Editor/",StringComparison.Ordinal)).ToArray();
            if(!report.avatarValid||!report.avatarHuman||!report.shoulderComponent||report.meshCount!=4||report.productionEditorDependencies.Length!=0)
                throw new InvalidOperationException("Character asset checks failed");
            if(!AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/_Game/Scenes/GirlV06_MixamoValidation.unity"))
                throw new InvalidOperationException("Animation validation scene missing");
        }
        catch(Exception e){report.errors.Add(e.ToString());}
        var after=SceneManager.GetActiveScene();report.activeSceneAfter=after.path;
        report.sceneUntouched=after.path==before.path&&after.isDirty==dirtyBefore;
        report.passed=report.errors.Count==0&&report.sceneUntouched;
        File.WriteAllText(Path.Combine(Out,"project-import-check.json"),JsonUtility.ToJson(report,true));
        Debug.Log("Girl v06 project import check passed="+report.passed);
    }
    public static void RunBatch()
    {
        Run();
        var report=JsonUtility.FromJson<Report>(File.ReadAllText(Path.Combine(Out,"project-import-check.json")));
        EditorApplication.Exit(report.passed?0:2);
    }}
