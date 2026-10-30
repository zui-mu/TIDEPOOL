// Girl V06 import helpers. User-triggered menu commands only; never alters a scene.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public static class GirlV06Setup
{
    [Serializable] private class Entry { public string unityHumanBone; public string boneName; }
    [Serializable] private class Mapping { public Entry[] entries; public string restPose; public bool skinned; }
    private static readonly string[] Groups = { "Body", "Hair", "Outfit", "FaceDetails" };

    private static string Root()
    {
        var matches = AssetDatabase.FindAssets("GirlV06Setup t:MonoScript")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.EndsWith("/Editor/GirlV06Setup.cs", StringComparison.Ordinal)).ToArray();
        if (matches.Length != 1)
            throw new InvalidOperationException("Expected one GirlV06/Editor/GirlV06Setup.cs; found " + matches.Length);
        return Path.GetDirectoryName(Path.GetDirectoryName(matches[0])).Replace('\\', '/');
    }

    [MenuItem("Tools/Girl V06/1 - Prepare URP materials")]
    public static void PrepareMaterials()
    {
        string root = Root();
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("URP Lit shader was not found. Activate URP first.");
        foreach (string file in Directory.GetFiles(root + "/Textures", "*.png"))
        {
            string path = file.Replace('\\', '/');
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) throw new InvalidOperationException("Texture importer missing: " + path);
            bool normal = path.EndsWith("_Normal.png", StringComparison.Ordinal) ||
                          path.EndsWith("_HighNormal.png", StringComparison.Ordinal);
            ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.sRGBTexture = path.EndsWith("_BaseMap.png", StringComparison.Ordinal);
            ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.alphaIsTransparency = false;
            ti.mipmapEnabled = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Trilinear;
            ti.anisoLevel = 4;
            ti.maxTextureSize = 2048;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.SaveAndReimport();
        }
        if (!AssetDatabase.IsValidFolder(root + "/Materials")) AssetDatabase.CreateFolder(root, "Materials");
        var modelPath = root + "/Models/Girl_Shoulder_v06.fbx";
        var modelImporter = AssetImporter.GetAtPath(modelPath) as ModelImporter;
        if (modelImporter == null) throw new InvalidOperationException("Model not found: " + modelPath);
        foreach (string group in Groups)
        {
            string path = root + "/Materials/GirlV06_" + group + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            else material.shader = shader;
            Texture2D Load(string suffix)
            {
                string p = root + "/Textures/" + group + "_" + suffix + ".png";
                var t = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                if (t == null) throw new InvalidOperationException("Missing texture: " + p);
                return t;
            }
            material.SetFloat("_WorkflowMode", 1f);
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", Load("BaseMap"));
            material.SetTexture("_BumpMap", Load("Normal"));
            material.SetFloat("_BumpScale", 1f);
            material.SetTexture("_MetallicGlossMap", Load("MetallicSmoothness"));
            material.SetFloat("_Metallic", 1f);
            material.SetFloat("_Smoothness", 1f);
            material.SetFloat("_SmoothnessTextureChannel", 0f);
            material.SetTexture("_OcclusionMap", Load("AO"));
            material.SetFloat("_OcclusionStrength", 0.65f);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.EnableKeyword("_OCCLUSIONMAP");
            material.DisableKeyword("_SPECULAR_SETUP");
            EditorUtility.SetDirty(material);
            modelImporter.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "GirlV06_" + group), material);
        }
        modelImporter.importBlendShapes = true;
        modelImporter.importCameras = false;
        modelImporter.importLights = false;
        modelImporter.importNormals = ModelImporterNormals.Import;
        modelImporter.importTangents = ModelImporterTangents.CalculateMikk;
        modelImporter.SaveAndReimport();
        var diagnostic = AssetImporter.GetAtPath(root + "/Diagnostics/Girl_Shoulder_Diagnostics_v06.fbx") as ModelImporter;
        if (diagnostic != null)
        {
            foreach (string group in Groups)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(root + "/Materials/GirlV06_" + group + ".mat");
                diagnostic.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "GirlV06_" + group), m);
            }
            diagnostic.importBlendShapes = true;
            diagnostic.importNormals = ModelImporterNormals.Import;
            diagnostic.importTangents = ModelImporterTangents.CalculateMikk;
            diagnostic.SaveAndReimport();
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Girl V06: four URP materials prepared. Skinned model prepared. Verify the Avatar and diagnostic clip in Unity.");
    }

    [MenuItem("Tools/Girl V06/2 - Configure Humanoid mapping")]
    public static void ConfigureHumanoid()
    {
        string root = Root();
        string path = root + "/Models/Girl_Shoulder_v06.fbx";
        var mi = AssetImporter.GetAtPath(path) as ModelImporter;
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (mi == null || model == null) throw new InvalidOperationException("Import the supplied FBX first.");
        var data = JsonUtility.FromJson<Mapping>(File.ReadAllText(root + "/UnityHumanoidMapping.json"));
        if (data == null || data.entries == null) throw new InvalidOperationException("Mapping JSON is missing or invalid.");
        var transforms = model.GetComponentsInChildren<Transform>(true);
        var names = new HashSet<string>(transforms.Select(t => t.name));
        var human = new List<HumanBone>();
        foreach (var entry in data.entries)
        {
            if (!Enum.TryParse<HumanBodyBones>(entry.unityHumanBone, out var key) || key == HumanBodyBones.LastBone)
                throw new InvalidOperationException("Unknown Humanoid key: " + entry.unityHumanBone);
            if (!names.Contains(entry.boneName)) throw new InvalidOperationException("Missing FBX bone: " + entry.boneName);
            human.Add(new HumanBone { boneName = entry.boneName,
                humanName = HumanTrait.BoneName[(int)key],
                limit = new HumanLimit { useDefaultValues = true } });
        }
        var hd = mi.humanDescription;
        hd.human = human.ToArray();
        hd.skeleton = GirlAvatarReferencePose.Calibrate(model, hd.human);
        hd.upperArmTwist = hd.lowerArmTwist = hd.upperLegTwist = hd.lowerLegTwist = 0.5f;
        hd.armStretch = hd.legStretch = 0.05f;
        hd.feetSpacing = 0f;
        hd.hasTranslationDoF = false;
        mi.animationType = ModelImporterAnimationType.Human;
        mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        mi.humanDescription = hd;
        mi.SaveAndReimport();
        var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
        if (avatar == null || !avatar.isValid || !avatar.isHuman)
            Debug.LogWarning("Girl V06: Avatar validation has not passed. Open Rig > Configure, check mapping and Enforce T-Pose, then Apply. Source rest pose is A-pose.");
        else Debug.Log("Girl V06: Unity reports a valid Humanoid Avatar.");
        Debug.LogWarning("Girl V06 stage: skinned development model with a joint-test clip. Gameplay locomotion and collision tuning are not supplied.");
    }

    [MenuItem("Tools/Girl V06/3 - Enable shoulder correctives on selected character")]
    public static void EnableShoulderCorrectives()
    {
        var selected = Selection.activeGameObject;
        if (!selected || !selected.GetComponentInChildren<Animator>())
            throw new InvalidOperationException("Select the character root containing its Animator first.");
        var component = selected.GetComponent<GirlShoulderCorrectives>();
        if (!component) component = Undo.AddComponent<GirlShoulderCorrectives>(selected);
        component.ConfigureFromHierarchy();
        EditorUtility.SetDirty(component);
        Debug.Log("Shoulder correctives enabled on selected character. Validate with real animations.");
    }
}
