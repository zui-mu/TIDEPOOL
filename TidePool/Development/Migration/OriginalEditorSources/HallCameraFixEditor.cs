using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class HallCameraFixEditor
{
    const string Out="D:/awestlakeselfstudy/unity_water_cycle";
    const string AssetRoot="Assets/_Game/WaterCycle";
    static double at,nextStatus;
    static int caseIndex;
    static RenderPipelineAsset originalQuality;
    static UniversalRenderPipelineAsset testPipeline;
    static UniversalRendererData testRenderer;
    static int featureIndex;
    static bool[] featureStates;
    static ScriptableRendererFeature[] testFeatureList;
    static void RestorePipeline(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.ExitingPlayMode || !testPipeline)return;
        QualitySettings.renderPipeline=originalQuality;
        UnityEngine.Object.DestroyImmediate(testPipeline);UnityEngine.Object.DestroyImmediate(testRenderer);
        testPipeline=null;testRenderer=null;
    }
    static readonly Vector3[] positions={new Vector3(-19.1f,1.08f,16),new Vector3(19.1f,1.08f,16),new Vector3(0,1.08f,-9),new Vector3(0,1.08f,41),new Vector3(12.2f,1.08f,17),new Vector3(0,1.08f,-7.5f),new Vector3(0,1.08f,35.2f),new Vector3(8,-1.92f,8)};
    static Camera Cam=>Camera.main;
    static Transform Player=>GameObject.Find("Player").transform;
    [InitializeOnLoadMethod]static void Init()
    {
        EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        EditorApplication.playModeStateChanged-=RestorePipeline;EditorApplication.playModeStateChanged+=RestorePipeline;
        Lightmapping.bakeCompleted-=Baked;Lightmapping.bakeCompleted+=Baked;
        File.WriteAllText(Out+"/camera-ready.txt",DateTime.Now.ToString("s"));at=EditorApplication.timeSinceStartup+4;
    }
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Tick()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        try
        {
            string path=Out+"/camera-command.txt";
            if(File.Exists(path))
            {
                string cmd;try{cmd=File.ReadAllText(path).Trim();File.Delete(path);}catch(IOException){return;}
                if(cmd=="inspect"){Inspect();Begin(90);}
                else if(cmd=="install")Install();
                else if(cmd=="bake")Bake();
                else if(cmd=="test"){caseIndex=0;Begin(1);}
                else if(cmd=="refine-shadow")
                {
                    Check(!EditorApplication.isPlaying,"Exit Play first");
                    var light=GameObject.Find("Sun").GetComponent<Light>();
                    light.shadowNormalBias=.02f;light.shadowBias=.01f;
                    light.GetUniversalAdditionalLightData().usePipelineSettings=false;
                    EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                    Begin(95);
                }
                else if(cmd=="diagnose-light")Begin(100);
                else if(cmd=="diagnose-material")Begin(110);
                else if(cmd=="diagnose-occlusion")Begin(120);
                else if(cmd=="diagnose-pipeline")Begin(130);
                else if(cmd=="diagnose-features")Begin(140);
                else if(cmd=="diagnose-mesh")Begin(150);
                else if(cmd=="diagnose-culling")Begin(160);
                else if(cmd=="refresh")AssetDatabase.Refresh();
                else if(cmd=="restore")Restore();
            }
            if(EditorApplication.timeSinceStartup>nextStatus)
            {
                nextStatus=EditorApplication.timeSinceStartup+4;
                File.WriteAllText(Out+"/camera-status.txt",$"{DateTime.Now:s} playing={EditorApplication.isPlaying} baking={Lightmapping.isRunning} stage={SessionState.GetInt("HallCameraTest",0)} cases={caseIndex}");
            }
            if(EditorApplication.isPlaying && SessionState.GetInt("HallCameraTest",0)>0 && EditorApplication.timeSinceStartup>=at)Test();
        }
        catch(Exception e){SessionState.SetInt("HallCameraTest",0);File.WriteAllText(Out+"/camera-error.txt",e.ToString());Debug.LogException(e);}
    }
    static void Inspect()
    {
        var renderers=UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r=>r.name=="Side_Aisle_Roof"||r.name.StartsWith("West_")||r.name.StartsWith("East_")).ToArray();
        File.WriteAllText(Out+"/camera-geometry.json",JsonUtility.ToJson(new Geometry{objects=renderers.Select(r=>new Item{name=r.name,position=r.transform.position,scale=r.transform.localScale,min=r.bounds.min,max=r.bounds.max,lightmap=r.lightmapIndex}).ToArray()},true));
    }
    [Serializable]class Geometry{public Item[] objects;}
    [Serializable]class Item{public string name;public Vector3 position,scale,min,max;public int lightmap;}
    static void Install()
    {
        Check(!EditorApplication.isPlaying,"Exit Play first");
        var scene=SceneManager.GetActiveScene();Check(scene.path.EndsWith("PoolHall_WaterCycle_01.unity"),"Open water cycle scene");
        EditorSceneManager.SaveScene(scene);if(!File.Exists(Out+"/BeforeCameraAndSeamFix.unity"))File.Copy(scene.path,Out+"/BeforeCameraAndSeamFix.unity");
        Cam.nearClipPlane=.12f;
        // The source roof ends exactly at the wall's inside plane. Extend only the outer
        // end into the thick wall; also overlap both end walls to seal the chamfered junctions.
        foreach(var roof in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r=>r.name=="Side_Aisle_Roof"))
        {
            if(roof.GetComponent<HallSeamFixedMarker>())continue;
            Vector3 size=roof.bounds.size;Vector3 scale=roof.transform.localScale;
            scale.x*=(size.x+.35f)/size.x;scale.z*=(size.z+.4f)/size.z;
            roof.transform.localScale=scale;roof.transform.position+=Vector3.right*Mathf.Sign(roof.transform.position.x)*.175f;
            roof.gameObject.AddComponent<HallSeamFixedMarker>();
        }
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Inspect();
        File.WriteAllText(Out+"/camera-installed.txt",DateTime.Now.ToString("s"));
    }
    static void Bake()
    {
        Check(!EditorApplication.isPlaying && !Lightmapping.isRunning,"Exit Play/wait for bake");
        string path=AssetRoot+"/LS_WaterCycle_Sealed.lighting";
        var settings=AssetDatabase.LoadAssetAtPath<LightingSettings>(path);
        if(!settings)
        {
            settings=UnityEngine.Object.Instantiate(Lightmapping.lightingSettings);settings.name="LS_WaterCycle_Sealed";
            AssetDatabase.CreateAsset(settings,path);
        }
        Lightmapping.lightingSettings=settings;
        // Generate scene-local output rather than overwrite the original Dream hall's data.
        Lightmapping.lightingDataAsset=null;
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());SessionState.SetBool("HallSeamBake",true);
        Check(Lightmapping.BakeAsync(),"Bake did not start");
    }
    static void Baked()
    {
        if(!SessionState.GetBool("HallSeamBake",false))return;
        SessionState.SetBool("HallSeamBake",false);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        File.WriteAllText(Out+"/camera-bake-complete.txt",DateTime.Now.ToString("s")+" lightmaps="+LightmapSettings.lightmaps.Length);
    }
    static void Begin(int stage)
    {
        Check(!EditorApplication.isPlaying && !Lightmapping.isRunning,"Exit Play / wait for bake");
        SessionState.SetInt("HallCameraTest",stage);EditorApplication.isPlaying=true;at=EditorApplication.timeSinceStartup+5;
    }
    static void Next(int stage,double delay){SessionState.SetInt("HallCameraTest",stage);at=EditorApplication.timeSinceStartup+delay;}
    static void Photo()
    {
        Cam.GetComponent<CameraFollow>().enabled=false;Cam.GetComponent<DryRegionCameraGuard>().enabled=false;
        Cam.transform.SetPositionAndRotation(new Vector3(-18,1.8f,9),Quaternion.LookRotation(new Vector3(-18,2.4f,35)-new Vector3(-18,1.8f,9)));
        UnityEngine.Object.FindFirstObjectByType<PoolWaterCycle>().showHints=false;
        EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).maximized=true;
    }
    static void CheckCamera()
    {
        var follow=Cam.GetComponent<CameraFollow>();
        var obstacles=Physics.OverlapSphere(Cam.transform.position,follow.CollisionRadius*.95f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore).Where(follow.IsCameraObstacle).ToArray();
        Check(obstacles.Length==0,"Camera intersects "+string.Join(",",obstacles.Select(c=>c.name))+" case "+caseIndex+" at "+Cam.transform.position);
        Check(Mathf.Abs(Cam.transform.position.x)<20 && Cam.transform.position.z>-10.1f && Cam.transform.position.z<42.1f,"Camera left hall bounds");
    }
    static void Test()
    {
        int stage=SessionState.GetInt("HallCameraTest",0);
        if(stage==90){Photo();Next(91,3);}
        else if(stage==91){ScreenCapture.CaptureScreenshot(Out+"/camera-seam-before.png");Next(92,1);}
        else if(stage==92){SessionState.SetInt("HallCameraTest",0);EditorApplication.isPlaying=false;}
        else if(stage==95){Photo();Next(96,3);}
        else if(stage==96){ScreenCapture.CaptureScreenshot(Out+"/camera-seam-shadow-refined.png");Next(97,1);}
        else if(stage==97){SessionState.SetInt("HallCameraTest",0);EditorApplication.isPlaying=false;}
        else if(stage==100)
        {
            Photo();UnityEngine.Object.FindFirstObjectByType<FloodAtmosphere>().enabled=false;
            GameObject.Find("Sun").GetComponent<Light>().intensity=0;Next(101,3);
        }
        else if(stage==101){ScreenCapture.CaptureScreenshot(Out+"/camera-seam-sun-off.png");Next(102,1);}
        else if(stage==102){SessionState.SetInt("HallCameraTest",0);EditorApplication.isPlaying=false;}
        else if(stage==110)
        {
            Photo();Next(111,3);
        }
        else if(stage==111)
        {
            var lines=new List<string>();
            foreach(float x in new[]{.32f,.36f,.40f,.44f,.48f})foreach(float y in new[]{.65f,.75f,.85f,.95f})
            {
                if(Physics.Raycast(Cam.ViewportPointToRay(new Vector3(x,y,0)),out RaycastHit hit,100))
                {
                    var r=hit.collider.GetComponent<Renderer>();
                    lines.Add($"uv={x},{y} object={hit.collider.name} point={hit.point} normal={hit.normal} material={(r?r.sharedMaterial.name:"none")}");
                }
            }
            File.WriteAllLines(Out+"/camera-seam-rays.txt",lines);
            var wall=GameObject.Find("West_Plaster").GetComponent<Renderer>();
            var mat=wall.material;mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            mat.SetFloat("_EnvironmentReflections",0);mat.SetFloat("_SpecularHighlights",0);Next(112,3);
        }
        else if(stage==112){ScreenCapture.CaptureScreenshot(Out+"/camera-seam-no-specular.png");Next(113,1);}
        else if(stage==113)
        {
            GameObject.Find("West_Plaster").GetComponent<Renderer>().lightmapIndex=-1;Next(114,3);
        }
        else if(stage==114){ScreenCapture.CaptureScreenshot(Out+"/camera-seam-no-lightmap.png");Next(115,1);}
        else if(stage==115){SessionState.SetInt("HallCameraTest",0);EditorApplication.isPlaying=false;}
        else if(stage==120)
        {
            Photo();
            var light=GameObject.Find("Sun").GetComponent<Light>();
            var lines=new List<string>{"Sun "+light.transform.eulerAngles+" directionToLight="+(-light.transform.forward)+" shadows="+light.shadows+" bias="+light.shadowBias+","+light.shadowNormalBias+" local="+(!light.GetUniversalAdditionalLightData().usePipelineSettings)};
            foreach(var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r=>r.name=="Side_Aisle_Roof"||r.name=="West_Plaster"))lines.Add(r.name+" cast="+r.shadowCastingMode+" receive="+r.receiveShadows+" enabled="+r.enabled+" bounds="+r.bounds);
            foreach(float y in new[]{4f,5f,6f})
            {
                Vector3 origin=new Vector3(-19.99f,y,16);
                var hits=Physics.RaycastAll(origin,-light.transform.forward,50).OrderBy(h=>h.distance);
                lines.Add("to-sun from "+origin+": "+string.Join(";",hits.Select(h=>h.collider.name+" "+h.point)));
            }
            File.WriteAllLines(Out+"/camera-occlusion.txt",lines);
            light.transform.SetParent(null,true);GameObject.Find("WebGPU Water").SetActive(false);Next(121,3);
        }
        else if(stage==121){ScreenCapture.CaptureScreenshot(Out+"/camera-seam-no-water.png");Next(122,1);}
        else if(stage==122)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name="Temporary shadow blocker";
            g.transform.position=new Vector3(-16.8f,7.1f,16);g.transform.localScale=new Vector3(7.2f,1,54);
            g.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.ShadowsOnly;Next(123,3);
        }
        else if(stage==123){ScreenCapture.CaptureScreenshot(Out+"/camera-seam-extra-blocker.png");Next(124,1);}
        else if(stage==124)
        {
            var light=GameObject.Find("Sun").GetComponent<Light>();light.lightmapBakeType=LightmapBakeType.Realtime;light.shadows=LightShadows.Hard;
            Next(125,3);
        }
        else if(stage==125){ScreenCapture.CaptureScreenshot(Out+"/camera-seam-realtime.png");Next(126,1);}
        else if(stage==126){SessionState.SetInt("HallCameraTest",0);EditorApplication.isPlaying=false;}
        else if(stage==130)
        {
            Photo();
            originalQuality=QualitySettings.renderPipeline;
            var source=(UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
            testPipeline=UnityEngine.Object.Instantiate(source);
            var serialized=new SerializedObject(testPipeline);
            var list=serialized.FindProperty("m_RendererDataList");
            testRenderer=UnityEngine.Object.Instantiate((UniversalRendererData)list.GetArrayElementAtIndex(0).objectReferenceValue);
            testRenderer.renderingMode=RenderingMode.Forward;
            list.GetArrayElementAtIndex(0).objectReferenceValue=testRenderer;serialized.ApplyModifiedPropertiesWithoutUndo();
            QualitySettings.renderPipeline=testPipeline;Next(131,5);
        }
        else if(stage==131){ScreenCapture.CaptureScreenshot(Out+"/camera-seam-forward.png");Next(132,1);}
        else if(stage==132){testPipeline.shadowCascadeCount=1;Next(133,4);}
        else if(stage==133){ScreenCapture.CaptureScreenshot(Out+"/camera-seam-one-cascade.png");Next(134,1);}
        else if(stage==134){testRenderer.rendererFeatures.Clear();testRenderer.SetDirty();Next(135,5);}
        else if(stage==135){ScreenCapture.CaptureScreenshot(Out+"/camera-seam-no-features.png");Next(136,1);}
        else if(stage==136){SessionState.SetInt("HallCameraTest",0);EditorApplication.isPlaying=false;}
        else if(stage==140)
        {
            Photo();originalQuality=QualitySettings.renderPipeline;
            testPipeline=UnityEngine.Object.Instantiate((UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline);
            var serialized=new SerializedObject(testPipeline);var list=serialized.FindProperty("m_RendererDataList");
            testRenderer=UnityEngine.Object.Instantiate((UniversalRendererData)list.GetArrayElementAtIndex(0).objectReferenceValue);
            for(int i=0;i<testRenderer.rendererFeatures.Count;i++)testRenderer.rendererFeatures[i]=UnityEngine.Object.Instantiate(testRenderer.rendererFeatures[i]);
            featureStates=testRenderer.rendererFeatures.Select(f=>f.isActive).ToArray();testFeatureList=testRenderer.rendererFeatures.ToArray();featureIndex=0;
            testRenderer.renderingMode=RenderingMode.Forward;testPipeline.shadowCascadeCount=1;
            list.GetArrayElementAtIndex(0).objectReferenceValue=testRenderer;serialized.ApplyModifiedPropertiesWithoutUndo();
            QualitySettings.renderPipeline=testPipeline;Next(141,5);
        }
        else if(stage==141)
        {
            if(featureIndex>=testFeatureList.Length){SessionState.SetInt("HallCameraTest",0);EditorApplication.isPlaying=false;return;}
            testRenderer.rendererFeatures.Clear();
            for(int i=0;i<testFeatureList.Length;i++)if(i!=featureIndex)testRenderer.rendererFeatures.Add(testFeatureList[i]);
            testRenderer.SetDirty();
            Next(142,4);
        }
        else if(stage==142)
        {
            ScreenCapture.CaptureScreenshot(Out+"/camera-without-"+testFeatureList[featureIndex].GetType().Name+".png");
            featureIndex++;Next(141,1);
        }
        else if(stage==150)
        {
            Photo();var lines=new List<string>();
            foreach(var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r=>r.bounds.min.x<-19.8f && r.bounds.max.x>-20.2f && r.bounds.max.y>3 && r.bounds.min.z<18 && r.bounds.max.z>14))
                lines.Add(r.name+" parent="+r.transform.parent.name+" material="+r.sharedMaterial.name+" bounds="+r.bounds);
            var camData=Cam.GetUniversalAdditionalCameraData();
            lines.Add("pipeline="+GraphicsSettings.currentRenderPipeline.name+" cameraRenderer="+camData.scriptableRenderer.GetType().Name);
            File.WriteAllLines(Out+"/camera-mesh.txt",lines);
            var wall=GameObject.Find("West_Plaster");
            var unit=GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.GetComponent<MeshFilter>().mesh=unit.GetComponent<MeshFilter>().sharedMesh;
            wall.transform.localScale=new Vector3(.5f,18,52.5f);
            UnityEngine.Object.DestroyImmediate(unit);
            Next(151,3);
        }
        else if(stage==151){ScreenCapture.CaptureScreenshot(Out+"/camera-seam-replaced-mesh.png");Next(152,1);}
        else if(stage==152)
        {
            var mat=new Material(Shader.Find("Universal Render Pipeline/Unlit"));mat.SetColor("_BaseColor",Color.magenta);
            GameObject.Find("West_Plaster").GetComponent<Renderer>().material=mat;Next(153,3);
        }
        else if(stage==153){ScreenCapture.CaptureScreenshot(Out+"/camera-seam-unlit-wall.png");Next(154,1);}
        else if(stage==154){SessionState.SetInt("HallCameraTest",0);EditorApplication.isPlaying=false;}
        else if(stage==160){Photo();GameObject.Find("Sun").GetComponent<Light>().useViewFrustumForShadowCasterCull=false;Next(161,3);}
        else if(stage==161){ScreenCapture.CaptureScreenshot(Out+"/camera-seam-no-shadow-cull.png");Next(162,1);}
        else if(stage==162){SessionState.SetInt("HallCameraTest",0);EditorApplication.isPlaying=false;}
        else if(stage==1)
        {
            Player.GetComponent<PlayerMovement>().enabled=false;Player.GetComponent<CharacterController>().enabled=false;
            caseIndex=0;Next(2,.1);
        }
        else if(stage==2)
        {
            if(caseIndex>=positions.Length*24){Next(4,.1);return;}
            Player.position=positions[caseIndex/24];int angle=caseIndex%24;
            var follow=Cam.GetComponent<CameraFollow>();var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            typeof(CameraFollow).GetField("yaw",flags).SetValue(follow,(angle%8)*45f);
            typeof(CameraFollow).GetField("pitch",flags).SetValue(follow,new[]{-10f,15f,65f}[angle/8]);
            Physics.SyncTransforms();Next(3,.08);
        }
        else if(stage==3){CheckCamera();caseIndex++;Next(2,.02);}
        else if(stage==4)
        {
            Player.position=new Vector3(8,1.08f,39.5f);Next(5,.2);
        }
        else if(stage==5)
        {
            Check(Cam.GetComponent<DryRegionCameraGuard>().IsPlayerInside,"Dry-region eye view broken");CheckCamera();
            Photo();Next(6,3);
        }
        else if(stage==6){ScreenCapture.CaptureScreenshot(Out+"/camera-seam-after.png");Next(7,1);}
        else if(stage==7)
        {
            File.WriteAllText(Out+"/camera-test-pass.txt","PASS: 192 camera wall/column/entry/pool-floor orbit cases, full pitch range, no collider overlaps, hall bounds, dry-region eye view.\n"+DateTime.Now.ToString("s"));
            SessionState.SetInt("HallCameraTest",0);EditorApplication.isPlaying=false;
        }
    }
    static void Restore()
    {
        Check(!EditorApplication.isPlaying,"Exit Play first");
        EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).maximized=false;
        Selection.activeGameObject=Cam.gameObject;EditorWindow.GetWindow<SceneView>().Focus();
    }
}
