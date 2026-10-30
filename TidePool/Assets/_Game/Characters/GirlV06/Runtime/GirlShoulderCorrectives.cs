using System;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(10000)]
public sealed class GirlShoulderCorrectives : MonoBehaviour
{
    public Animator animator;
    public SkinnedMeshRenderer[] targets;
    [Range(0f, 1f)] public float strength = 1f;
    public bool flipForward;
    Transform chest, leftUpper, leftLower, rightUpper, rightLower;
    Vector3 localUp, localForward;
    int[,] indices;
    static readonly string[] Names = {
        "ShoulderRaise_L", "ShoulderForward_L", "ShoulderOverhead_L",
        "ShoulderRaise_R", "ShoulderForward_R", "ShoulderOverhead_R"
    };
    public void ConfigureFromHierarchy()
    {
        if (!animator) animator = GetComponentInChildren<Animator>();
        targets = GetComponentsInChildren<SkinnedMeshRenderer>();
    }
    void Reset() { ConfigureFromHierarchy(); }
    void OnEnable()
    {
        if (!animator || targets == null || targets.Length == 0) ConfigureFromHierarchy();
        chest = Bone(HumanBodyBones.Chest, "Chest");
        leftUpper = Bone(HumanBodyBones.LeftUpperArm, "LeftUpperArm");
        leftLower = Bone(HumanBodyBones.LeftLowerArm, "LeftLowerArm");
        rightUpper = Bone(HumanBodyBones.RightUpperArm, "RightUpperArm");
        rightLower = Bone(HumanBodyBones.RightLowerArm, "RightLowerArm");
        if (!chest || !leftUpper || !leftLower || !rightUpper || !rightLower)
        {
            Debug.LogWarning("Girl shoulder correctives: required bones are missing.", this);
            enabled = false; return;
        }
        Transform basis = animator ? animator.transform : transform;
        localUp = ClosestAxis(chest.InverseTransformDirection(basis.up), -1);
        int upIndex = Mathf.Abs(localUp.x) > .5f ? 0 : Mathf.Abs(localUp.y) > .5f ? 1 : 2;
        localForward = ClosestAxis(chest.InverseTransformDirection(basis.forward), upIndex);
        indices = new int[targets.Length, Names.Length];
        for (int r = 0; r < targets.Length; r++)
            for (int k = 0; k < Names.Length; k++)
                indices[r,k] = FindShape(targets[r] ? targets[r].sharedMesh : null, Names[k]);
    }
    Transform Bone(HumanBodyBones key, string fallback)
    {
        if (animator && animator.isHuman)
        {
            var result = animator.GetBoneTransform(key);
            if (result) return result;
        }
        foreach (var t in GetComponentsInChildren<Transform>())
            if (t.name == fallback) return t;
        return null;
    }
    static Vector3 ClosestAxis(Vector3 v, int excluded)
    {
        int index = -1; float best = -1f;
        for (int i = 0; i < 3; i++)
            if (i != excluded && Mathf.Abs(v[i]) > best) { best = Mathf.Abs(v[i]); index = i; }
        Vector3 result = Vector3.zero; result[index] = v[index] < 0 ? -1f : 1f;
        return result;
    }
    static int FindShape(Mesh mesh, string name)
    {
        if (!mesh) return -1;
        int exact = mesh.GetBlendShapeIndex(name);
        if (exact >= 0) return exact;
        for (int i = 0; i < mesh.blendShapeCount; i++)
            if (mesh.GetBlendShapeName(i).EndsWith("." + name, StringComparison.Ordinal) ||
                mesh.GetBlendShapeName(i).EndsWith("__" + name, StringComparison.Ordinal)) return i;
        return -1;
    }
    void LateUpdate()
    {
        if (!chest || indices == null) return;
        Vector3 up = chest.TransformDirection(localUp).normalized;
        Vector3 forward = chest.TransformDirection(localForward).normalized * (flipForward ? -1 : 1);
        ApplySide(0, leftUpper, leftLower, up, forward);
        ApplySide(3, rightUpper, rightLower, up, forward);
    }
    void ApplySide(int offset, Transform upper, Transform lower, Vector3 up, Vector3 forward)
    {
        Vector3 d = (lower.position - upper.position).normalized;
        float u = Vector3.Dot(d, up), f = Vector3.Dot(d, forward);
        float forwardWeight = Mathf.Pow(Mathf.Clamp01((f - .25f) / .65f), 2f) *
                              Mathf.Clamp01((.85f - Mathf.Abs(u)) / .35f);
        float raiseWeight = Mathf.Clamp01((u + .5f) / .5f) *
                            Mathf.Clamp01((.85f - u) / .85f) *
                            Mathf.Clamp01((.65f - Mathf.Abs(f)) / .65f);
        float overheadWeight = Mathf.Pow(Mathf.Clamp01((u - .25f) / .627f), 2f) *
                               Mathf.Clamp01((.65f - Mathf.Abs(f)) / .65f);
        Write(offset, raiseWeight); Write(offset + 1, forwardWeight); Write(offset + 2, overheadWeight);
    }
    void Write(int key, float value)
    {
        for (int r = 0; r < targets.Length; r++)
            if (targets[r] && indices[r,key] >= 0)
                targets[r].SetBlendShapeWeight(indices[r,key], value * 100f * strength);
    }
    void OnDisable()
    {
        if (indices == null || targets == null) return;
        for (int r = 0; r < targets.Length; r++)
            for (int k = 0; k < Names.Length; k++)
                if (targets[r] && indices[r,k] >= 0) targets[r].SetBlendShapeWeight(indices[r,k], 0f);
    }
}
