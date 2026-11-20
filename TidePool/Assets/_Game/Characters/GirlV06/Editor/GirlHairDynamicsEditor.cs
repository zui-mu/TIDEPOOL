using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[CustomEditor(typeof(GirlHairDynamics))]
public sealed class GirlHairDynamicsEditor : Editor
{
    [Serializable] sealed class SetupRecord
    {
        public string scene;
        public float leftTip, rightTip, leftRadius, rightRadius, headRadius;
        public int bodyProxies;
    }
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector(); var hair = (GirlHairDynamics)target;
        if (!hair.ValidateConfiguration(out string issue)) EditorGUILayout.HelpBox(issue, MessageType.Warning);
        EditorGUILayout.Space();
        if (GUILayout.Button("Bind missing bone references"))
        {
            Undo.RecordObject(hair, "Bind braid references"); hair.ConfigureFromHierarchy(); EditorUtility.SetDirty(hair);
        }
        if (GUILayout.Button("Reset simulation")) hair.RequestReset();
        using (new EditorGUI.DisabledScope(Application.isPlaying)) if (GUILayout.Button("Measure tips and configure default body proxies"))
        {
            Undo.RecordObject(hair, "Measure braid geometry"); MeasureAndConfigure(hair); EditorUtility.SetDirty(hair);
        }
        EditorGUILayout.HelpBox("The solver changes hair rotations only. Cyan gizmos show braid segments; yellow shows data-only body proxies. Reset does not replace your parameters.", MessageType.Info);
    }
    [MenuItem("Tools/Girl V06/11 - Install braid dynamics")]
    public static void InstallCurrentCharacter()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode && !AnimationMode.InAnimationMode(), "Stop Play/animation preview before installing braid dynamics.");
        var scene = SceneManager.GetActiveScene();
        Require(scene.path == GirlPlayerIntegration.ScenePath, "Open the integrated ArmLab scene first.");
        for (int n = 0; n < SceneManager.sceneCount; n++) Require(!SceneManager.GetSceneAt(n).isDirty, "Save existing scene edits before hair setup.");
        var players = UnityEngine.Object.FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        Require(players.Length == 1, "Expected one scene player.");
        var visual = players[0].transform.Find("GirlVisual"); Require(visual, "GirlVisual is missing.");
        string backup = Path.Combine(Application.dataPath, "..", "Library", "HairDynamicsImplementation", "ArmLab.before-hair.unity");
        Directory.CreateDirectory(Path.GetDirectoryName(backup));
        if (!File.Exists(backup)) File.Copy(Path.Combine(Application.dataPath, "..", scene.path), backup);
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Install braid dynamics");
        try
        {
            var hair = visual.GetComponent<GirlHairDynamics>(); bool added = !hair;
            if (added) hair = Undo.AddComponent<GirlHairDynamics>(visual.gameObject);
            else Undo.RecordObject(hair, "Bind existing braid dynamics");
            hair.ConfigureFromHierarchy();
            if (added) MeasureAndConfigure(hair);
            Require(hair.ValidateConfiguration(out string issue), issue);
            EditorUtility.SetDirty(hair); PrefabUtility.RecordPrefabInstancePropertyModifications(hair);
            EditorSceneManager.MarkSceneDirty(scene);
            Require(EditorSceneManager.SaveScene(scene), "Could not save the configured ArmLab scene.");
            Undo.CollapseUndoOperations(group);
            var record = new SetupRecord
            {
                scene = scene.path, leftTip = hair.left.tipOffset.magnitude, rightTip = hair.right.tipOffset.magnitude,
                leftRadius = hair.left.rootRadius, rightRadius = hair.right.rootRadius,
                headRadius = hair.colliders.Length > 0 ? hair.colliders[0].radius : 0f, bodyProxies = hair.colliders.Length
            };
            File.WriteAllText(Path.Combine(GirlPlayerIntegration.Out, "hair-setup.json"), JsonUtility.ToJson(record, true));
        }
        catch { Undo.RevertAllDownToGroup(group); throw; }
    }
    static void MeasureAndConfigure(GirlHairDynamics hair)
    {
        hair.ConfigureFromHierarchy(); Require(hair.ValidateConfiguration(out string issue), issue);
        var renderer = hair.hairRenderers.FirstOrDefault(r => r && r.sharedMesh);
        Require(renderer, "Hair SkinnedMeshRenderer is missing.");
        var baked = new Mesh { name = "Temporary hair measurement" };
        try
        {
            renderer.BakeMesh(baked); var vertices = baked.vertices; var weights = renderer.sharedMesh.boneWeights;
            Require(vertices.Length == weights.Length, "Skinning weights do not match the baked mesh.");
            MeasureChain(hair.left, renderer, vertices, weights); MeasureChain(hair.right, renderer, vertices, weights);
            int index = Array.IndexOf(renderer.bones, hair.head); bool found = false; Bounds scalp = default;
            for (int n = 0; n < vertices.Length; n++) if (Weight(weights[n], index) >= .95f)
            {
                Vector3 world = renderer.transform.TransformPoint(vertices[n]);
                if (!found) { scalp = new Bounds(world, Vector3.zero); found = true; } else scalp.Encapsulate(world);
            }
            Require(found, "Could not measure the Head-weighted scalp.");
            float scale = hair.head.lossyScale.x;
            float headRadius = Mathf.Clamp(Mathf.Min(scalp.extents.x, scalp.extents.z) * .78f, .065f * scale, .11f * scale);
            float rootClearance = Mathf.Min(Vector3.Distance(hair.left.bones[0].position, scalp.center) - hair.left.rootRadius * scale,
                Vector3.Distance(hair.right.bones[0].position, scalp.center) - hair.right.rootRadius * scale);
            headRadius = Mathf.Min(headRadius, Mathf.Max(.035f * scale, rootClearance - .008f * scale));
            var list = new List<GirlHairCollisionProxy>();
            Add(list, "Head", hair.head, scalp.center, scalp.center, headRadius);
            Transform neck = Bone(hair, HumanBodyBones.Neck, "Neck");
            if (neck) Add(list, "Neck", neck, neck.position, hair.head.position - Vector3.up * .015f, .032f * scale);
            Transform chest = Bone(hair, HumanBodyBones.Chest, "Chest");
            Transform leftArm = Bone(hair, HumanBodyBones.LeftUpperArm, "LeftUpperArm"), rightArm = Bone(hair, HumanBodyBones.RightUpperArm, "RightUpperArm");
            if (chest && leftArm && rightArm)
            {
                Vector3 center = (leftArm.position + rightArm.position) * .5f + hair.transform.forward * .015f;
                float radius = Mathf.Clamp(Vector3.Distance(leftArm.position, rightArm.position) * .25f, .07f * scale, .11f * scale);
                Add(list, "Upper torso", chest, center - Vector3.up * (.24f * scale), center - Vector3.up * (.055f * scale), radius);
            }
            foreach (var arm in new[] { leftArm, rightArm }) if (arm)
            {
                Transform lower = arm == leftArm ? Bone(hair, HumanBodyBones.LeftLowerArm, "LeftLowerArm") : Bone(hair, HumanBodyBones.RightLowerArm, "RightLowerArm");
                float radius = lower ? Mathf.Clamp(Vector3.Distance(arm.position, lower.position) * .20f, .04f * scale, .065f * scale) : .05f * scale;
                Add(list, arm == leftArm ? "Left shoulder" : "Right shoulder", arm, arm.position, arm.position, radius);
            }
            hair.colliders = list.ToArray(); hair.RequestReset();
        }
        finally { UnityEngine.Object.DestroyImmediate(baked); }
    }
    static void MeasureChain(GirlHairDynamics.ChainSettings chain, SkinnedMeshRenderer renderer, Vector3[] vertices, BoneWeight[] weights)
    {
        Transform last = chain.bones[3]; int index = Array.IndexOf(renderer.bones, last);
        Require(index >= 0, "Last braid bone is absent from skinning references.");
        Vector3 axis = last.localPosition.normalized; float length = 0f; var radial = new List<float>();
        for (int n = 0; n < vertices.Length; n++) if (Weight(weights[n], index) >= .25f)
        {
            Vector3 local = last.InverseTransformPoint(renderer.transform.TransformPoint(vertices[n])); float depth = Vector3.Dot(local, axis);
            length = Mathf.Max(length, depth); radial.Add((local - axis * depth).magnitude);
        }
        Require(radial.Count > 0 && length > .02f, "The last braid bone needs nonzero skin weights and a measurable tip.");
        radial.Sort(); float width = radial[Mathf.Clamp((int)(radial.Count * .9f), 0, radial.Count - 1)];
        chain.tipOffset = axis * Mathf.Clamp(length, .03f, .35f);
        // The mesh is a wide, tapered hair mass; the circular proxy is intentionally conservative.
        chain.rootRadius = Mathf.Clamp(width * .55f, .018f, .055f); chain.tipRadius = chain.rootRadius * .75f;
    }
    static void Add(List<GirlHairCollisionProxy> list, string label, Transform anchor, Vector3 start, Vector3 end, float worldRadius)
    {
        list.Add(new GirlHairCollisionProxy { label = label, anchor = anchor, localStart = anchor.InverseTransformPoint(start),
            localEnd = anchor.InverseTransformPoint(end), radius = worldRadius / Mathf.Max(.0001f, anchor.lossyScale.x) });
    }
    static Transform Bone(GirlHairDynamics hair, HumanBodyBones key, string name)
    {
        if (hair.animator && hair.animator.isHuman) { var result = hair.animator.GetBoneTransform(key); if (result) return result; }
        return hair.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
    }
    static float Weight(BoneWeight w, int index) => (w.boneIndex0 == index ? w.weight0 : 0f) + (w.boneIndex1 == index ? w.weight1 : 0f)
        + (w.boneIndex2 == index ? w.weight2 : 0f) + (w.boneIndex3 == index ? w.weight3 : 0f);
    static void Require(bool condition, string issue) { if (!condition) throw new InvalidOperationException(issue); }
}
