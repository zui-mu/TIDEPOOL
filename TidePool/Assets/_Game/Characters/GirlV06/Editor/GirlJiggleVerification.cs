using System;
using System.IO;
using UnityEditor;
using UnityEngine;
[InitializeOnLoad]
public static partial class GirlJiggleVerification
{
    static string Folder => Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/GirlJiggle"));
    [Serializable] public sealed class Report { public string action, status, error; public float sag, response, rootError, lengthError, resetError, milliseconds; }
    static GirlJiggleVerification() { EditorApplication.update += Poll; }
    static void Install()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Exit Play Mode before setup.");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.isDirty) throw new Exception("Save scene changes before setup.");
        var source=UnityEngine.Object.FindFirstObjectByType<GirlHairDynamics>();
        if(!source) throw new Exception("Existing braid bindings are missing.");
        var adapter=source.GetComponent<GirlJiggleHair>();
        if(!adapter) adapter=Undo.AddComponent<GirlJiggleHair>(source.gameObject);
        Undo.RecordObject(source,"Switch braid solver"); Undo.RecordObject(adapter,"Configure Jiggle");
        adapter.binding=source; adapter.damping=.25f; adapter.airDrag=0f; source.enabled=false;
        EditorUtility.SetDirty(adapter); EditorUtility.SetDirty(source);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        if(!UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene)) throw new Exception("Scene save failed.");
    }
    static void Require(bool condition,string message) { if(!condition) throw new Exception(message); }
    static void Test(Report report)
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode,"Exit Play Mode first.");
        var source=UnityEngine.Object.FindFirstObjectByType<GirlJiggleHair>();
        Require(source && source.binding && !source.binding.enabled,"Configured adapter must own the hair bones.");
        var activeScene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        bool wasDirty=activeScene.isDirty;
        var preview=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        GameObject clone=null;
        try
        {
            clone=UnityEngine.Object.Instantiate(source.gameObject);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(clone,preview);
            var adapter=clone.GetComponent<GirlJiggleHair>();
            var binding=adapter.binding;
            binding.animator.enabled=false;
            var chain=binding.left;
            var head=binding.head;
            var initialTip=chain.bones[3].TransformPoint(chain.tipOffset);
            var root=head.InverseTransformPoint(chain.bones[0].position);
            var lengths=new Vector3[8];
            for(int i=0;i<8;i++) lengths[i]=(i<4?binding.left:binding.right).bones[i%4].localPosition;
            adapter.InitializeForVerification(); adapter.VerifyCollisionSetup();
            var rest=head.InverseTransformPoint(chain.bones[3].TransformPoint(chain.tipOffset));
            report.sag=initialTip.y-chain.bones[3].TransformPoint(chain.tipOffset).y;
            Require(report.sag>.08f,"Hair must visibly settle below authored outward pose; sag="+report.sag);
            Vector3 origin=clone.transform.position;
            var watch=System.Diagnostics.Stopwatch.StartNew();
            for(int n=0;n<120;n++)
            {
                clone.transform.position=origin+new Vector3(.09f*Mathf.Sin(n*.17f),0,0);
                adapter.StepForVerification(1f/60f);
                var tip=head.InverseTransformPoint(chain.bones[3].TransformPoint(chain.tipOffset));
                Require(float.IsFinite(tip.x)&&float.IsFinite(tip.y)&&float.IsFinite(tip.z),"Non-finite braid output.");
                report.response=Mathf.Max(report.response,Vector3.Distance(tip,rest));
                report.rootError=Mathf.Max(report.rootError,Vector3.Distance(chain.bones[0].position,head.TransformPoint(root)));
                for(int i=0;i<8;i++) report.lengthError=Mathf.Max(report.lengthError,Vector3.Distance((i<4?binding.left:binding.right).bones[i%4].localPosition,lengths[i]));
            }
            watch.Stop(); report.milliseconds=(float)watch.Elapsed.TotalMilliseconds/120;
            Require(report.response>.03f && report.response<.8f,"Movement must produce clear bounded sway: "+report.response);
            Require(report.rootError<.001f && report.lengthError<.00001f,"Root attachment or bone length changed.");
            clone.transform.position=origin+Vector3.right*5;
            adapter.RequestReset(); adapter.StepForVerification(0);
            report.resetError=Vector3.Distance(head.InverseTransformPoint(chain.bones[3].TransformPoint(chain.tipOffset)),rest);
            Require(report.resetError<.01f,"Teleport reset must restore gravity-settled pose.");
            foreach(float dt in new[]{1f/30f,1f/60f,1f/120f,.15f,0f})
                for(int n=0;n<12;n++) adapter.StepForVerification(dt);
        }
        finally
        {
            if(clone) UnityEngine.Object.DestroyImmediate(clone);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview);
            if(!wasDirty) UnityEditor.SceneManagement.EditorSceneManager.SaveScene(activeScene);
        }
    }
    static void Poll()
    {
        string request = Path.Combine(Folder, "request.txt");
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(request)) return;
        string action = File.ReadAllText(request).Trim(); File.Delete(request);
        var report = new Report { action = action };
        try
        {
            var type = Type.GetType("GirlJiggleHair, Assembly-CSharp");
            if (type == null) throw new Exception("Jiggle adapter is not implemented yet.");
            if (action == "check") { }
            else if (action == "install") Install();
            else if (action == "test") Test(report);
            else if (action == "save") UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            else throw new Exception("Unsupported request.");
            report.status = "passed";
        }
        catch (Exception error) { report.status = "failed"; report.error = error.ToString(); }
        File.WriteAllText(Path.Combine(Folder, "report.json"), JsonUtility.ToJson(report, true));
    }
}
