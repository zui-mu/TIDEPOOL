using System;
using UnityEngine;

// Reference pose -> damped secondary motion -> hair rotations only.
[DisallowMultipleComponent]
[DefaultExecutionOrder(11000)]
public sealed class GirlHairDynamics : MonoBehaviour
{
    [Serializable] public sealed class ChainSettings
    {
        public Transform[] bones = new Transform[4];
        public Vector3 tipOffset;
        [Min(.001f)] public float rootRadius = .045f;
        [Min(.001f)] public float tipRadius = .035f;
    }
    public Animator animator;
    public Transform head;
    public ChainSettings left = new ChainSettings(), right = new ChainSettings();
    [Header("Restrained secondary motion")]
    [Range(0f, 1f)] public float strength = .75f;
    [Range(0f, 1f)] public float inertia = .30f;
    [Range(0f, 1f)] public float gravityScale = .20f;
    [Min(.1f)] public float rootFrequency = 4.2f, tipFrequency = 2.6f;
    [Range(.1f, 2f)] public float dampingRatio = .8f;
    [Range(0f, 80f)] public float rootAngleLimit = 18f, tipAngleLimit = 35f;
    [Header("Body avoidance")]
    public GirlHairCollisionProxy[] colliders = Array.Empty<GirlHairCollisionProxy>();
    public SkinnedMeshRenderer[] hairRenderers = Array.Empty<SkinnedMeshRenderer>();
    [Range(1, 10)] public int constraintIterations = 5;
    [Header("Stability and diagnostics")]
    [Min(1f)] public float maxAcceleration = 80f;
    [Min(.1f)] public float maxRelativeSpeed = 3f;
    public bool drawDebug;

    sealed class ChainState
    {
        public ChainSettings settings;
        public readonly Transform[] bones = new Transform[4];
        public readonly Vector3[] target = new Vector3[5], position = new Vector3[5], velocity = new Vector3[5], oldPosition = new Vector3[5];
        public readonly Vector3[] previousTarget = new Vector3[5], previousTargetVelocity = new Vector3[5], acceleration = new Vector3[5];
        public readonly Quaternion[] baseLocalRotation = new Quaternion[4], baseWorldRotation = new Quaternion[4];
        public readonly float[] length = new float[4];
    }
    ChainState[] chains;
    Bounds[] originalBounds;
    Transform boundHead;
    SkinnedMeshRenderer[] boundRenderers;
    PlayerMovement movement;
    Vector3 previousHeadPosition, previousHeadScale;
    Quaternion previousHeadRotation;
    bool resetRequested = true, warned;
    float fade, maximumChainLength;
    const float Substep = 1f / 120f;
    const int MaximumSubsteps = 12;

    void OnEnable()
    {
        if (!head || !Complete(left) || !Complete(right)) ConfigureFromHierarchy();
        BindResetSource(); TryInitialize(); RequestReset();
    }
    void OnDisable()
    {
        RestoreBasePose(); RestoreRendererBounds(); UnbindResetSource(); chains = null; fade = 0f; resetRequested = true;
    }
    void OnDestroy() { UnbindResetSource(); }
    void Update() { RestoreBasePose(); }
    void LateUpdate()
    {
        if (animator && !animator.isActiveAndEnabled) { RestoreBasePose(); RequestReset(); return; }
        Simulate(Time.deltaTime);
    }
    public void RequestReset() { resetRequested = true; }
    public void ConfigureFromHierarchy()
    {
        RestoreBasePose(); RestoreRendererBounds(); chains = null; originalBounds = null;
        if (!animator) animator = GetComponentInChildren<Animator>(true);
        var transforms = GetComponentsInChildren<Transform>(true);
        if (!head && animator && animator.isHuman) head = animator.GetBoneTransform(HumanBodyBones.Head);
        if (!head) head = Find(transforms, "Head");
        BindChain(left, "Left", transforms); BindChain(right, "Right", transforms);
        if (hairRenderers == null || hairRenderers.Length == 0)
        {
            var all = GetComponentsInChildren<SkinnedMeshRenderer>(true); int count = 0;
            foreach (var renderer in all) if (renderer.name.IndexOf("Hair", StringComparison.OrdinalIgnoreCase) >= 0) count++;
            hairRenderers = new SkinnedMeshRenderer[count]; count = 0;
            foreach (var renderer in all) if (renderer.name.IndexOf("Hair", StringComparison.OrdinalIgnoreCase) >= 0) hairRenderers[count++] = renderer;
        }
        warned = false; BindResetSource(); RequestReset();
    }
    static Transform Find(Transform[] transforms, string name)
    {
        foreach (var t in transforms) if (t.name == name) return t;
        return null;
    }
    static bool Complete(ChainSettings settings)
    {
        if (settings == null || settings.bones == null || settings.bones.Length != 4) return false;
        foreach (var b in settings.bones) if (!b) return false;
        return true;
    }
    static void BindChain(ChainSettings settings, string side, Transform[] transforms)
    {
        if (settings == null) return;
        if (settings.bones == null || settings.bones.Length != 4) settings.bones = new Transform[4];
        for (int n = 0; n < 4; n++) if (!settings.bones[n]) settings.bones[n] = Find(transforms, "Hair_" + side + "_" + (n + 1));
        if (Complete(settings) && settings.tipOffset.sqrMagnitude < .00000001f)
            settings.tipOffset = settings.bones[3].localPosition.normalized * settings.bones[3].localPosition.magnitude * .7f;
    }
    public bool ValidateConfiguration(out string issue)
    {
        issue = null;
        if (!head) issue = "Assign the animated Head anchor.";
        else if (!UniformPositive(head.lossyScale)) issue = "Braid dynamics require positive uniform head/parent scale.";
        else foreach (var settings in new[] { left, right })
        {
            if (!Complete(settings)) { issue = "Bind all four bones in each braid."; break; }
            if (!settings.bones[0].IsChildOf(head)) { issue = "Braid root must be a child of Head."; break; }
            for (int n = 1; n < 4; n++)
                if (settings.bones[n].parent != settings.bones[n - 1] || settings.bones[n].localPosition.sqrMagnitude < .00000001f)
                { issue = "Braid bones must form a nonzero contiguous chain."; break; }
            if (issue != null) break;
            if (settings.tipOffset.sqrMagnitude < .00000001f) { issue = "Measure a nonzero virtual tip offset."; break; }
        }
        return issue == null;
    }
    static bool UniformPositive(Vector3 scale)
    {
        return scale.x > .00001f && scale.y > .00001f && scale.z > .00001f
            && Mathf.Abs(scale.x - scale.y) < .001f && Mathf.Abs(scale.x - scale.z) < .001f;
    }
    void BindResetSource()
    {
        var source = GetComponentInParent<PlayerMovement>();
        if (movement == source) return;
        UnbindResetSource(); movement = source;
        if (movement) movement.MotionReset += RequestReset;
    }
    void UnbindResetSource()
    {
        if (movement) movement.MotionReset -= RequestReset;
        movement = null;
    }
    bool TryInitialize()
    {
        if (chains != null) return true;
        if (!ValidateConfiguration(out string issue))
        {
            if (!warned) { Debug.LogWarning("Girl hair dynamics: " + issue, this); warned = true; }
            return false;
        }
        BindResetSource();
        warned = false; boundHead = head;
        chains = new[] { CreateState(left), CreateState(right) };
        SampleReferencePose(); RebuildState(); ExpandRendererBounds(); return true;
    }
    static ChainState CreateState(ChainSettings settings)
    {
        var state = new ChainState { settings = settings };
        for (int n = 0; n < 4; n++)
        { state.bones[n] = settings.bones[n]; state.baseLocalRotation[n] = settings.bones[n].localRotation; }
        return state;
    }
    void RestoreBasePose()
    {
        if (chains == null) return;
        foreach (var c in chains) for (int n = 0; n < 4; n++) if (c.bones[n]) c.bones[n].localRotation = c.baseLocalRotation[n];
    }
    void SampleReferencePose()
    {
        foreach (var c in chains)
        {
            for (int n = 0; n < 4; n++)
            {
                Transform bone = c.bones[n];
                c.baseLocalRotation[n] = bone.localRotation; c.baseWorldRotation[n] = bone.rotation; c.target[n] = bone.position;
            }
            c.target[4] = c.bones[3].TransformPoint(c.settings.tipOffset);
            for (int n = 0; n < 4; n++) c.length[n] = Vector3.Distance(c.target[n], c.target[n + 1]);
        }
    }
    void RebuildState()
    {
        maximumChainLength = 0f;
        foreach (var c in chains)
        {
            float length = 0f;
            for (int n = 0; n < 5; n++)
            {
                c.position[n] = c.previousTarget[n] = c.target[n];
                c.velocity[n] = c.previousTargetVelocity[n] = c.acceleration[n] = Vector3.zero;
                if (n < 4) length += c.length[n];
            }
            maximumChainLength = Mathf.Max(maximumChainLength, length);
        }
        previousHeadPosition = head.position; previousHeadRotation = head.rotation; previousHeadScale = head.lossyScale;
        resetRequested = false; fade = 0f;
    }
    bool BindingsMatch()
    {
        if (!head || head != boundHead || !Complete(left) || !Complete(right)) return false;
        for (int chain = 0; chain < 2; chain++)
        {
            var settings = chain == 0 ? left : right;
            if (chains[chain].settings != settings) return false;
            for (int n = 0; n < 4; n++) if (!chains[chain].bones[n] || chains[chain].bones[n] != settings.bones[n]) return false;
        }
        int count = hairRenderers == null ? 0 : hairRenderers.Length;
        if ((boundRenderers == null ? 0 : boundRenderers.Length) != count) return false;
        for (int n = 0; n < count; n++) if (boundRenderers[n] != hairRenderers[n]) return false;
        return true;
    }
    void Simulate(float deltaTime)
    {
        if (chains != null && !BindingsMatch())
        {
            RestoreBasePose(); RestoreRendererBounds(); chains = null; boundHead = null; fade = 0f; RequestReset();
        }
        if (!TryInitialize()) return;
        if (deltaTime <= 0f) { ApplyPose(); return; }
        if (!UniformPositive(head.lossyScale)) { RestoreBasePose(); RequestReset(); return; }
        SampleReferencePose();
        if (resetRequested || deltaTime > Substep * MaximumSubsteps
            || Vector3.Distance(head.position, previousHeadPosition) > Mathf.Max(.75f, maximumChainLength * 1.5f)
            || Quaternion.Angle(head.rotation, previousHeadRotation) > 120f
            || Vector3.Distance(head.lossyScale, previousHeadScale) > .001f)
        {
            RebuildState(); ApplyPose(); return;
        }
        if (strength <= .0001f) { RestoreBasePose(); RequestReset(); return; }
        fade = Mathf.MoveTowards(fade, strength, deltaTime / .15f);
        Quaternion frameRotation = head.rotation * Quaternion.Inverse(previousHeadRotation);
        float accelerationBlend = 1f - Mathf.Exp(-deltaTime / .04f);
        foreach (var c in chains)
        {
            for (int n = 0; n < 5; n++)
            {
                c.position[n] = head.position + frameRotation * (c.position[n] - previousHeadPosition);
                c.velocity[n] = frameRotation * c.velocity[n];
                Vector3 targetVelocity = (c.target[n] - c.previousTarget[n]) / deltaTime;
                Vector3 acceleration = Vector3.ClampMagnitude((targetVelocity - c.previousTargetVelocity[n]) / deltaTime, maxAcceleration);
                c.acceleration[n] = Vector3.Lerp(c.acceleration[n], acceleration, accelerationBlend);
                c.previousTarget[n] = c.target[n]; c.previousTargetVelocity[n] = targetVelocity;
            }
        }
        int steps = Mathf.Clamp(Mathf.CeilToInt(deltaTime / Substep), 1, MaximumSubsteps);
        float h = deltaTime / steps;
        for (int step = 0; step < steps; step++) foreach (var c in chains)
        {
            c.position[0] = c.target[0];
            for (int n = 1; n < 5; n++)
            {
                c.oldPosition[n] = c.position[n];
                float omega = 2f * Mathf.PI * Mathf.Lerp(rootFrequency, tipFrequency, (n - 1f) / 3f);
                Vector3 force = (c.target[n] - c.position[n]) * (omega * omega)
                    - c.velocity[n] * (2f * dampingRatio * omega)
                    + (Physics.gravity * gravityScale - c.acceleration[n] * inertia) * fade;
                c.velocity[n] = Vector3.ClampMagnitude(c.velocity[n] + force * h, maxRelativeSpeed);
                c.position[n] += c.velocity[n] * h;
            }
            for (int iteration = 0; iteration < constraintIterations; iteration++)
            {
                Constrain(c);
                if (colliders != null) foreach (var proxy in colliders)
                {
                    if (proxy == null) continue;
                    for (int n = 0; n < 4; n++) proxy.ProjectSegment(ref c.position[n], ref c.position[n + 1],
                        Mathf.Lerp(c.settings.rootRadius, c.settings.tipRadius, n / 3f) * previousHeadScale.x, n == 0, c.target[n + 1] - head.position);
                }
            }
            Constrain(c);
            for (int n = 1; n < 5; n++) c.velocity[n] = Vector3.ClampMagnitude((c.position[n] - c.oldPosition[n]) / h, maxRelativeSpeed);
        }
        previousHeadPosition = head.position; previousHeadRotation = head.rotation; previousHeadScale = head.lossyScale;
        ApplyPose();
    }
    void Constrain(ChainState c)
    {
        c.position[0] = c.target[0];
        for (int n = 0; n < 4; n++)
        {
            Vector3 reference = (c.target[n + 1] - c.target[n]).normalized;
            if (n > 0) reference = StableSwing(c.target[n] - c.target[n - 1], c.position[n] - c.position[n - 1], head.right) * reference;
            Vector3 desired = c.position[n + 1] - c.position[n];
            if (desired.sqrMagnitude < .00000001f) desired = reference;
            float limit = Mathf.Lerp(rootAngleLimit, tipAngleLimit, n / 3f) * Mathf.Deg2Rad;
            Vector3 direction = Vector3.RotateTowards(reference, desired.normalized, limit, 0f).normalized;
            c.position[n + 1] = c.position[n] + direction * c.length[n];
        }
    }
    static Quaternion StableSwing(Vector3 from, Vector3 to, Vector3 referenceNormal)
    {
        if (from.sqrMagnitude < .00000001f || to.sqrMagnitude < .00000001f) return Quaternion.identity;
        from.Normalize(); to.Normalize();
        if (Vector3.Dot(from, to) > -.9999f) return Quaternion.FromToRotation(from, to);
        Vector3 axis = Vector3.Cross(from, referenceNormal);
        if (axis.sqrMagnitude < .00000001f) axis = Vector3.Cross(from, Vector3.forward);
        if (axis.sqrMagnitude < .00000001f) axis = Vector3.Cross(from, Vector3.up);
        return Quaternion.AngleAxis(180f, axis.normalized);
    }
    void ApplyPose()
    {
        if (chains == null) return;
        foreach (var c in chains) for (int n = 0; n < 4; n++)
            c.bones[n].rotation = StableSwing(c.target[n + 1] - c.target[n], c.position[n + 1] - c.position[n], head.right) * c.baseWorldRotation[n];
    }
    void ExpandRendererBounds()
    {
        if (hairRenderers == null) return;
        boundRenderers = (SkinnedMeshRenderer[])hairRenderers.Clone();
        originalBounds = new Bounds[boundRenderers.Length];
        for (int n = 0; n < boundRenderers.Length; n++) if (boundRenderers[n])
        {
            originalBounds[n] = boundRenderers[n].localBounds; Bounds bounds = originalBounds[n];
            float scale = Mathf.Max(.0001f, boundRenderers[n].transform.lossyScale.x);
            bounds.Expand((maximumChainLength * .65f + .06f) * 2f / scale); boundRenderers[n].localBounds = bounds;
        }
    }
    void RestoreRendererBounds()
    {
        if (originalBounds == null || boundRenderers == null) return;
        for (int n = 0; n < Mathf.Min(originalBounds.Length, boundRenderers.Length); n++) if (boundRenderers[n]) boundRenderers[n].localBounds = originalBounds[n];
        originalBounds = null; boundRenderers = null;
    }
    void OnDrawGizmosSelected()
    {
        if (!drawDebug) return;
        Gizmos.color = Color.cyan;
        if (chains != null) foreach (var c in chains) for (int n = 0; n < 4; n++)
        { Gizmos.DrawLine(c.position[n], c.position[n + 1]); Gizmos.DrawWireSphere(c.position[n + 1], Mathf.Lerp(c.settings.rootRadius, c.settings.tipRadius, n / 3f) * (head ? head.lossyScale.x : 1f)); }
        Gizmos.color = Color.yellow;
        if (colliders != null) foreach (var proxy in colliders) if (proxy != null && proxy.GetWorldCapsule(out var a, out var b, out float r))
        { Gizmos.DrawWireSphere(a, r); Gizmos.DrawWireSphere(b, r); Gizmos.DrawLine(a, b); }
    }
}
