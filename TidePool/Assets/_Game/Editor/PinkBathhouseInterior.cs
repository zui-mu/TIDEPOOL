using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// Authors the persistent environment. The saved scene and prefabs have no dependency on this builder.
public sealed class PinkBathhouseInterior
{
    const string Root = "Assets/_Game/Bathhouse";
    const string Kit = Root + "/Models/BathhouseHardware.fbx";
    const float Height = 3.2f;
    readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
    readonly Dictionary<string, GameObject> fixtures = new Dictionary<string, GameObject>();
    Transform architecture, fittings, lighting;

    public static void Build()
    {
        new PinkBathhouseInterior().Assemble();
    }

    void Assemble()
    {
        foreach (string folder in new[] { "Materials", "Prefabs" })
            if (!AssetDatabase.IsValidFolder(Root + "/" + folder)) AssetDatabase.CreateFolder(Root, folder);
        MakeMaterials();
        MakeFixtures();
        var room = new GameObject("Public Bathhouse").transform;
        architecture = Group("Architecture", room);
        fittings = Group("Fittings", room);
        lighting = Group("Lighting", room);
        Shell();
        WindowsAndDoors();
        ShowerRows();
        LampsAndBench();
        Illuminate();
    }

    static Transform Group(string name, Transform parent)
    {
        Transform t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    static Color Swatch(string hex)
    {
        if (!ColorUtility.TryParseHtmlString("#" + hex, out Color color)) throw new ArgumentException(hex);
        return color;
    }

    Material Store(string name, Shader shader)
    {
        if (!shader) throw new InvalidOperationException("Missing shader for " + name);
        string path = Root + "/Materials/" + name + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!mat)
        {
            mat = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = shader;
        materials.Add(name, mat);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    void Finish(string name, string color, string joint, float size, float jointWidth, float gloss, int kind)
    {
        Material mat = Store(name, Shader.Find("TidePool/Bathhouse/Architectural Finish"));
        mat.SetColor("_BaseColor", Swatch(color));
        mat.SetColor("_JointColor", Swatch(joint));
        mat.SetFloat("_TileSize", size);
        mat.SetFloat("_JointWidth", jointWidth);
        mat.SetFloat("_Smoothness", gloss);
        mat.SetFloat("_Finish", kind);
    }

    void Enamel(string name, string color, float metal, float gloss)
    {
        Material mat = Store(name, Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", Swatch(color));
        mat.SetFloat("_Metallic", metal);
        mat.SetFloat("_Smoothness", gloss);
    }

    void Luminous(string name, string color)
    {
        Material mat = Store(name, Shader.Find("Universal Render Pipeline/Unlit"));
        mat.SetColor("_BaseColor", Swatch(color));
    }

    void MakeMaterials()
    {
        Finish("LotusGlaze", "D5A6A9", "B99DA0", .2f, .0026f, .65f, 0);
        Finish("RoseDado", "9A7D83", "867980", .2f, .0026f, .63f, 0);
        Finish("GripMosaic", "8B8786", "7F7A79", .1f, .0025f, .42f, 1);
        Finish("CeilingPanel", "929391", "7B7B7C", .6f, .0035f, .24f, 2);
        Finish("WiredGlass", "F3D4AC", "74635A", .2f, .003f, .7f, 3);
        Enamel("Steel", "B5BBB8", .80f, .73f);
        Enamel("DarkMetal", "696E6D", .66f, .55f);
        Enamel("GrateIron", "5E6363", .24f, .33f);
        Enamel("BlackRubber", "383738", .02f, .35f);
        Enamel("RedCap", "9F433F", .06f, .52f);
        Enamel("BlueCap", "496B91", .06f, .52f);
        Enamel("MeterCase", "C1BAAC", .12f, .5f);
        Enamel("Screen", "191B1B", .1f, .82f);
        Luminous("RedLED", "FF2514");
        Enamel("BenchWood", "BCA18C", .04f, .45f);
        Enamel("LampGlass", "BDC2BE", .02f, .52f);
        Luminous("LampLit", "EFF7FC");
        Enamel("BlueSoap", "8BB8B6", .03f, .61f);
        Enamel("VentDark", "393B3D", .29f, .33f);
        Enamel("FrameEnamel", "B5A396", .24f, .54f);
        Enamel("PaintedDoor", "A8998E", .06f, .48f);
    }

    void MakeFixtures()
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(Kit);
        if (!model) throw new InvalidOperationException("Import bathhouse hardware before building: " + Kit);
        MeshFilter[] imported = model.GetComponentsInChildren<MeshFilter>(true);
        // The model's mounting planes face -Z after FBX import. Prefabs expose +Z into the room.
        Quaternion faceRoom = Quaternion.Euler(0,180,0);
        foreach (string name in new[] { "ShowerStation", "SoapBasket", "SoapBar", "SlattedBench", "DomeLamp", "VentGrille", "DrainSection" })
        {
            var source = imported.Single(f => f.name == name || f.sharedMesh.name == name);
            var prototype = new GameObject(name);
            try
            {
                var mesh = Group("Hardware", prototype.transform);
                mesh.localPosition = faceRoom * model.transform.InverseTransformPoint(source.transform.position);
                mesh.localRotation = faceRoom * Quaternion.Inverse(model.transform.rotation) * source.transform.rotation;
                Vector3 s = source.transform.lossyScale, rootScale = model.transform.lossyScale;
                mesh.localScale = new Vector3(s.x/rootScale.x, s.y/rootScale.y, s.z/rootScale.z);
                mesh.gameObject.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
                Material[] slots = source.GetComponent<MeshRenderer>().sharedMaterials;
                if (slots.Any(m => !m))
                    throw new InvalidOperationException("Missing FBX material on " + name);
                mesh.gameObject.AddComponent<MeshRenderer>().sharedMaterials = slots.Select(m =>
                    materials[name == "DrainSection" && m.name == "Steel" ? "GrateIron" : m.name]).ToArray();
                if (name == "SlattedBench")
                {
                    var c = prototype.AddComponent<BoxCollider>();
                    c.center = new Vector3(0,.23f,0);
                    c.size = new Vector3(1.5f,.46f,.46f);
                }
                Static(mesh.gameObject);
                string path = Root + "/Prefabs/" + name + ".prefab";
                fixtures.Add(name, PrefabUtility.SaveAsPrefabAsset(prototype, path));
            }
            finally { Object.DestroyImmediate(prototype); }
        }
    }

    static void Static(GameObject go)
    {
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ReflectionProbeStatic | StaticEditorFlags.OccludeeStatic);
    }

    GameObject Solid(string name, Vector3 pos, Vector3 size, string mat, bool collider = true)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(architecture, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        go.GetComponent<MeshRenderer>().sharedMaterial = materials[mat];
        Static(go);
        return go;
    }

    // Coordinates describe the actual inside faces. All glazed walls share world-aligned tile courses.
    void Wall(string name, float x0, float x1, float z0, float z1, float bottom = 0, float top = Height)
    {
        if (bottom < .6f)
        {
            float dado = Mathf.Min(top,.6f);
            Solid(name + " • dado", new Vector3((x0+x1)/2,(bottom+dado)/2,(z0+z1)/2),
                new Vector3(x1-x0,dado-bottom,z1-z0), "RoseDado");
            bottom = dado;
        }
        if (top > bottom)
            Solid(name + " • glaze", new Vector3((x0+x1)/2,(bottom+top)/2,(z0+z1)/2),
                new Vector3(x1-x0,top-bottom,z1-z0), "LotusGlaze");
    }

    void Deck(string name, float x0, float x1, float z0, float z1)
    {
        var size = new Vector3(x1-x0, .12f, z1-z0);
        var pos = new Vector3((x0+x1)/2, -.06f, (z0+z1)/2);
        Solid(name + " • floor", pos, size, "GripMosaic");
        pos.y = Height+.06f;
        Solid(name + " • ceiling", pos, size, "CeilingPanel");
    }

    void Shell()
    {
        const float t = .14f;
        // Real recesses expose the dark drain beds below the grates without coplanar surfaces.
        foreach (var span in new[] { new Vector2(0,.116f), new Vector2(.224f,5.776f), new Vector2(5.884f,6) })
            Solid("Shower room • floor",new Vector3((span.x+span.y)*.5f,-.06f,5),
                new Vector3(span.y-span.x,.12f,10),"GripMosaic");
        foreach (float x in new[] { .17f,5.83f })
            Solid("Drain subfloor",new Vector3(x,-.08f,5),new Vector3(.11f,.12f,10),"VentDark");
        Solid("Shower room • ceiling",new Vector3(3,Height+.06f,5),new Vector3(6,.12f,10),"CeilingPanel");
        Deck("Window room", 0,3,10,12.4f);
        Deck("Vestibule", -2.8f,2.3f,-2.4f,-1);
        Deck("Threshold", .9f,2.3f,-1,0);
        Wall("West", -t,0,0,12.4f);
        Wall("East", 6,6+t,0,10);
        Wall("Entry left", 0,.9f,-t,0);
        Wall("Entry right", 2.3f,6,-t,0);
        Wall("Entry lintel", .9f,2.3f,-t,0,2.2f);
        Wall("Rear left", 0,.7f,10,10+t);
        Wall("Rear right", 2.1f,6,10,10+t);
        Wall("Rear lintel", .7f,2.1f,10,10+t,2.2f);
        Wall("Alcove right", 3,3+t,10+t,12.4f);
        Wall("Window sill wall", 0,3,12.4f,12.4f+t,0,.78f);
        Wall("Window lintel", 0,3,12.4f,12.4f+t,2.65f);
        Wall("Window west pier", 0,.23f,12.4f,12.4f+t,.78f,2.65f);
        Wall("Window east pier", 2.77f,3,12.4f,12.4f+t,.78f,2.65f);
        Wall("Turn exterior", -2.8f,2.3f,-2.4f-t,-2.4f);
        Wall("Turn return", -2.8f,.9f,-1,-1+t);
        Wall("Turn east", 2.3f,2.3f+t,-2.4f,0);
        Wall("Turn west", .9f-t,.9f,-1,0);
        Wall("Entry end cap", -2.8f-t,-2.8f,-2.4f,-1);
    }

    void WindowsAndDoors()
    {
        // The frosted pane admits the exterior light; its metal frame and door still cast real shadows.
        var glass = Solid("Wired privacy glass",new Vector3(1.5f,1.715f,12.45f),new Vector3(2.54f,1.87f,.018f),"WiredGlass");
        glass.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        foreach (float x in new[] { .24f, 1.50f, 2.76f })
            Solid("Window mullion",new Vector3(x,1.71f,12.39f),new Vector3(.042f,1.89f,.075f),"FrameEnamel",false);
        foreach (float y in new[] { .79f, 2.2f, 2.64f })
            Solid("Window rail",new Vector3(1.5f,y,12.39f),new Vector3(2.56f,.043f,.085f),"FrameEnamel",false);
        Solid("Ceramic window ledge",new Vector3(1.5f,.78f,12.31f),new Vector3(2.62f,.065f,.25f),"FrameEnamel",false);
        foreach (float x in new[] { .68f,2.12f })
            Solid("Door jamb",new Vector3(x,1.09f,10),new Vector3(.048f,2.19f,.185f),"FrameEnamel",false);
        Solid("Door header",new Vector3(1.4f,2.2f,10),new Vector3(1.49f,.065f,.185f),"FrameEnamel",false);
        var leaf = Solid("Door • held open",new Vector3(.69f,1.06f,10.73f),new Vector3(.047f,2.12f,1.27f),"PaintedDoor");
        leaf.transform.rotation = Quaternion.Euler(0,-6,0);
        Solid("Interior door handle",new Vector3(.79f,.96f,11.20f),new Vector3(.105f,.035f,.12f),"Steel",false);
    }

    GameObject Place(string kind, Vector3 position, float yaw, string label)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(fixtures[kind], fittings);
        go.name = label;
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(0,yaw,0));
        PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
        return go;
    }

    void FeedPipe(float x, float y, float radius, float start, float end)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "Supply manifold";
        go.transform.SetParent(fittings,false);
        go.transform.SetPositionAndRotation(new Vector3(x,y,(start+end)*.5f),Quaternion.Euler(90,0,0));
        go.transform.localScale = new Vector3(radius*2,(end-start)*.5f,radius*2);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.GetComponent<Renderer>().sharedMaterial = materials["DarkMetal"];
        Static(go);
    }

    void ShowerRows()
    {
        for (int side = 0; side < 2; side++)
        {
            bool west = side == 0;
            float x = west ? .007f : 5.993f;
            float yaw = west ? 90 : -90;
            float start = west ? 2f : 1.3f;
            int count = west ? 5 : 6;
            for (int i = 0; i < count; i++)
            {
                float z = start+1.4f*i;
                Place("ShowerStation",new Vector3(x,0,z),yaw,(west ? "West " : "East ") + "shower " + (i+1));
                if (i%2 == 0 && i < count-1)
                {
                    Vector3 anchor = new Vector3(x,1.10f,z+.64f);
                    Place("SoapBasket",anchor,yaw,"Wall basket");
                    if (!west && i == 0)
                        Place("SoapBar",anchor+Quaternion.Euler(0,yaw,0)*new Vector3(0,.018f,.08f),yaw,"Blue soap");
                }
            }
            FeedPipe(west ? .065f : 5.935f,.36f,.021f,.12f,9.88f);
            for (int i = 0; i<10; i++)
                Place("DrainSection",new Vector3(west ? .17f : 5.83f,.0015f,.5f+i),90,"Floor drain");
        }
    }

    void LampsAndBench()
    {
        Place("SlattedBench",new Vector3(1.52f,0,11.94f),180,"Window bench");
        for (int i = 0; i<3; i++)
        {
            GameObject dome = Place("DomeLamp",new Vector3(3,Height,3.5f+2.3f*i),0,"Ceiling dome " + (i+1));
            if (i != 1) continue;
            Renderer r = dome.GetComponentInChildren<Renderer>();
            r.sharedMaterials = r.sharedMaterials.Select(m=>m == materials["LampGlass"] ? materials["LampLit"] : m).ToArray();
            PrefabUtility.RecordPrefabInstancePropertyModifications(r);
        }
        Place("VentGrille",new Vector3(4.03f,Height-.003f,3.6f),0,"Extract grille");
        Place("VentGrille",new Vector3(1.96f,Height-.003f,7.2f),0,"Return grille");
    }

    Light Lamp(string name, LightType type, Vector3 pos, string rgb, float power, float range)
    {
        Transform t=Group(name,lighting);
        t.position=pos;
        var light=t.gameObject.AddComponent<Light>();
        light.type=type;
        light.color=Swatch(rgb);
        light.intensity=power;
        light.range=range;
        return light;
    }

    void Illuminate()
    {
        RenderSettings.skybox=null;
        RenderSettings.ambientMode=AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=Swatch("62616B");
        RenderSettings.ambientEquatorColor=Swatch("514D56");
        RenderSettings.ambientGroundColor=Swatch("48444C");
        RenderSettings.reflectionIntensity=.85f;
        RenderSettings.fog=false;
        var sun=Lamp("Evening sun through wired glass",LightType.Directional,new Vector3(-1,4,15),"FFB783",3.7f,40);
        sun.transform.rotation=Quaternion.LookRotation(new Vector3(.34f,-.21f,-1).normalized);
        sun.shadows=LightShadows.Soft;
        sun.shadowBias=.025f;
        sun.shadowNormalBias=.10f;
        RenderSettings.sun=sun;
        var dome=Lamp("Middle dome • cool white",LightType.Spot,new Vector3(3,2.99f,5.8f),"D7EAF2",4.6f,7.8f);
        dome.transform.rotation=Quaternion.LookRotation(Vector3.down,Vector3.forward);
        dome.spotAngle=153f;
        dome.innerSpotAngle=85f;
        dome.shadows=LightShadows.Soft;
        Lamp("Ceiling bounce",LightType.Point,new Vector3(3,2.62f,5.8f),"DDE9EE",.26f,3.2f);
        Lamp("Cool reflected light along showers",LightType.Point,new Vector3(4.15f,1.95f,3.3f),"D2E0EE",1.75f,6.8f);
        Lamp("Doorway spill",LightType.Point,new Vector3(1.45f,1.58f,9.92f),"FFCAAD",1.1f,4.6f);
        Lamp("Window bounce",LightType.Point,new Vector3(1.48f,1.62f,11.78f),"FFC498",3.5f,4.8f);
        Lamp("Turn spill",LightType.Point,new Vector3(-.92f,2.2f,-1.75f),"DAD4E0",1.30f,3.1f);
        Transform mount=Group("Glaze and chrome reflection",lighting);
        mount.position=new Vector3(3,1.55f,5.4f);
        var probe=mount.gameObject.AddComponent<ReflectionProbe>();
        probe.mode=ReflectionProbeMode.Baked;
        probe.size=new Vector3(8.8f,4,16);
        probe.center=new Vector3(-.5f,.05f,-.35f);
        probe.boxProjection=true;
        probe.resolution=128;
        probe.clearFlags=ReflectionProbeClearFlags.SolidColor;
        probe.backgroundColor=Swatch("62616B");
        probe.farClipPlane=30;
        probe.nearClipPlane=.1f;
        // Capture static finishes once; actors retain the room's local reflection in standalone play.
        string path=Root + "/Materials/BathhouseReflection.exr";
        bool replacing = File.Exists(path);
        string capture = replacing ? AssetDatabase.GenerateUniqueAssetPath(Root + "/Materials/BathhouseProbeCapture.exr") : path;
        try
        {
            if (!Lightmapping.BakeReflectionProbe(probe,capture))
                throw new InvalidOperationException("Could not bake the bathhouse reflection probe to " + capture);
            if (replacing)
            {
                // Atomically replace only the image; keep the project's existing texture importer and GUID.
                string staged = Path.Combine("Library", "BathhouseProbe-" + Guid.NewGuid().ToString("N") + ".exr");
                try
                {
                    File.Copy(capture, staged);
                    File.Replace(staged, path, null);
                }
                finally { if (File.Exists(staged)) File.Delete(staged); }
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }
        finally
        {
            if (replacing && !AssetDatabase.DeleteAsset(capture))
            {
                if (File.Exists(capture)) File.Delete(capture);
                if (File.Exists(capture + ".meta")) File.Delete(capture + ".meta");
            }
        }
        probe.customBakedTexture=AssetDatabase.LoadAssetAtPath<Cubemap>(path);
        if (!probe.customBakedTexture) throw new InvalidOperationException("Reflection probe did not import as a cubemap.");
        probe.mode=ReflectionProbeMode.Custom;
    }
}
