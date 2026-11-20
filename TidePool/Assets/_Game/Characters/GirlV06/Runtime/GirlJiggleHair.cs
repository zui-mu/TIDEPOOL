using System;
using System.Collections.Generic;
using UnityEngine;
using GatorDragonGames.JigglePhysics;

// Project lifecycle/binding adapter. All integration, constraints and collisions run in upstream JiggleJobs.
[DisallowMultipleComponent, DefaultExecutionOrder(11000)]
public sealed class GirlJiggleHair : MonoBehaviour
{
    public GirlHairDynamics binding;
    [Range(0,1)] public float stiffness = .12f, damping = .25f, airDrag = 0f;
    public float gravity = 1f;
    const float Step = 1f / 60f;
    JiggleJobs jobs;
    readonly List<JiggleTree> trees = new();
    readonly List<GameObject> proxies = new();
    Transform[] bones;
    Vector3[] positions;
    Quaternion[] rotations;
    Bounds[] bounds;
    PlayerMovement movement;
    double clock;
    float accumulator;
    bool reset = true;
    Vector3 previousHead;
    Quaternion previousRotation;

    void OnEnable()
    {
        if (!Application.isPlaying) return;
        movement = GetComponentInParent<PlayerMovement>();
        if (movement) movement.MotionReset += RequestReset;
        reset = true;
    }
    public void RequestReset() { reset = true; }
    void LateUpdate()
    {
        if (!Application.isPlaying || !binding || (binding.animator && !binding.animator.isActiveAndEnabled)) return;
        StepForVerification(Time.deltaTime);
    }
    public void InitializeForVerification()
    {
        if (!binding) throw new InvalidOperationException("Assign the existing braid binding.");
        binding.enabled = false;
        if (!binding.ValidateConfiguration(out string issue)) throw new InvalidOperationException(issue);
        if (bones == null)
        {
            bones = new Transform[8]; positions = new Vector3[8]; rotations = new Quaternion[8];
            for (int i=0;i<8;i++)
            {
                bones[i] = (i<4 ? binding.left : binding.right).bones[i%4];
                positions[i] = bones[i].localPosition; rotations[i] = bones[i].localRotation;
            }
            bounds = new Bounds[binding.hairRenderers.Length];
            for (int i=0;i<bounds.Length;i++) if(binding.hairRenderers[i])
            {
                bounds[i] = binding.hairRenderers[i].localBounds;
                var expanded = bounds[i]; expanded.Expand(1.6f / Mathf.Max(.001f,transform.lossyScale.x));
                binding.hairRenderers[i].localBounds = expanded;
            }
        }
        ReleaseSolver();
        RestoreBones();
        var colliders = new List<JiggleColliderSerializable>();
        foreach(var source in binding.colliders)
        {
            if(source == null || !source.anchor) continue;
            var proxy = new GameObject("Jiggle proxy - " + source.label);
            proxy.hideFlags = HideFlags.HideAndDontSave;
            proxy.transform.SetParent(source.anchor, false);
            proxy.transform.localPosition = (source.localStart + source.localEnd)*.5f;
            var direction = source.localEnd-source.localStart;
            proxy.transform.localRotation = direction.sqrMagnitude > 1e-8f ? Quaternion.FromToRotation(Vector3.up,direction) : Quaternion.identity;
            proxies.Add(proxy);
            colliders.Add(new JiggleColliderSerializable {transform=proxy.transform, collider=new JiggleCollider {
                type=direction.sqrMagnitude > 1e-8f ? JiggleCollider.JiggleColliderType.Capsule : JiggleCollider.JiggleColliderType.Sphere,
                enabled=true, radius=source.radius, height=direction.magnitude, capsuleAxis=JiggleCollider.CapsuleAxis.Y }});
        }
        clock = Time.timeAsDouble; accumulator = 0;
        jobs = new JiggleJobs(clock, Step); jobs.SetConstraintIterations(4);
        foreach(var chain in new[]{binding.left,binding.right})
        {
            // A real endpoint lets the library collide the last weighted bone's full hair segment.
            var endpoint=new GameObject("Jiggle braid endpoint"); endpoint.hideFlags=HideFlags.HideAndDontSave;
            endpoint.transform.SetParent(chain.bones[3],false); endpoint.transform.localPosition=chain.tipOffset;
            proxies.Add(endpoint);
            var data = JiggleRigData.Default(); data.rootBone=chain.bones[0];
            data.jiggleColliders=colliders.ToArray();
            var p=JiggleTreeInputParameters.Default();
            p.advancedToggle=true; p.collisionToggle=true; p.angleLimitToggle=false;
            p.stiffness=new JiggleTreeCurvedFloat(stiffness) {curveEnabled=true,curve=AnimationCurve.Linear(0,1,1,.25f)};
            p.drag=new JiggleTreeCurvedFloat(damping);
            p.airDrag=new JiggleTreeCurvedFloat(airDrag);
            p.gravity=new JiggleTreeCurvedFloat(gravity);
            p.stretch=new JiggleTreeCurvedFloat(0);
            p.rootStretch=0; p.ignoreRootMotion=0;
            p.collisionRadius=new JiggleTreeCurvedFloat(chain.rootRadius*.7f) {curveEnabled=true,curve=AnimationCurve.Linear(0,1,1,.75f)};
            data.jiggleTreeInputParameters=p; data.OnValidate();
            var tree=JigglePhysics.CreateJiggleTree(data,null);
            trees.Add(tree); jobs.ScheduleAdd(tree);
        }
        // Commit and warm up the actual library solver while the animated head is stationary.
        for(int i=0;i<181;i++) Tick();
        previousHead=binding.head.position; previousRotation=binding.head.rotation; reset=false;
    }
    public string CollisionGeometryError()
    {
        var entries=jobs.GetPersonalColliders(out int count); float positionError=0,radiusError=0,axisError=0;
        foreach(var tree in trees)
        {
            int offset=(int)tree.GetStruct().colliderIndexOffset;
            for(int i=0;i<tree.personalColliderTransforms.Length;i++)
            {
                var c=entries[offset+i]; var source=binding.colliders[i];
                source.GetWorldCapsule(out var a,out var b,out float r);
                Vector3 center=c.localToWorldMatrix.c3.xyz; Vector3 axis=c.GetWorldAxis();
                positionError=Mathf.Max(positionError,Vector3.Distance(center,(a+b)*.5f));
                radiusError=Mathf.Max(radiusError,Mathf.Abs(c.worldRadius-r));
                axisError=Mathf.Max(axisError,Vector3.Distance(center-axis*c.worldHeight*.5f,a));
            }
        }
        return "bodyPosition="+positionError+"/bodyRadius="+radiusError+"/bodyAxis="+axisError;
    }
    public float PoseReconstructionError()
    {
        var poses=jobs.GetInterpolatedOutputPoses(out int count); float error=0;
        foreach(var tree in trees)
        {
            int offset=(int)tree.GetStruct().transformIndexOffset;
            for(int i=0;i<tree.bones.Length;i++) if(tree.bones[i] && !poses[offset+i].isVirtual)
                error=Mathf.Max(error,Vector3.Distance(tree.bones[i].position,(Vector3)poses[offset+i].position));
        }
        return error;
    }
    public void VerifyCollisionSetup()
    {
        var entries=jobs.GetPersonalColliders(out int count);
        if(count==0) throw new InvalidOperationException("Body collision proxies are missing.");
        for(int i=0;i<count;i++) if(!entries[i].enabled || entries[i].worldRadius<=0) throw new InvalidOperationException("Body collision proxy is inactive.");
    }
    void Tick()
    {
        clock+=Step;
        jobs.Simulate(clock,clock,1);
        jobs.SchedulePoses(clock + 2*Step); jobs.CompletePoses();
        // Preserve the authored skinning skeleton's exact translations.
        for(int i=0;i<8;i++) bones[i].localPosition=positions[i];
    }
    public void StepForVerification(float delta)
    {
        if (jobs==null || reset || Vector3.Distance(binding.head.position,previousHead)>.8f ||
            Quaternion.Angle(binding.head.rotation,previousRotation)>120)
            InitializeForVerification();
        if(delta<=0) return;
        accumulator+=Mathf.Min(delta,.1f);
        while(accumulator>=Step) { Tick(); accumulator-=Step; }
        previousHead=binding.head.position; previousRotation=binding.head.rotation;
    }
    void RestoreBones()
    {
        if(bones==null) return;
        for(int i=0;i<bones.Length;i++) if(bones[i]) {bones[i].localPosition=positions[i];bones[i].localRotation=rotations[i];}
    }
    void ReleaseSolver()
    {
        if(jobs!=null) { jobs.CompletePoses(); foreach(var tree in trees) tree.Dispose(jobs.FreeOnComplete); jobs.Dispose(); jobs=null; }
        trees.Clear();
        foreach(var proxy in proxies) if(proxy) { proxy.transform.SetParent(null,false); if(Application.isPlaying) Destroy(proxy); else DestroyImmediate(proxy); }
        proxies.Clear();
    }
    void OnApplicationQuit() { JigglePhysics.Dispose(); }
    void OnDisable()
    {
        if(movement) movement.MotionReset-=RequestReset;
        ReleaseSolver(); RestoreBones();
        if(bounds!=null && binding) for(int i=0;i<bounds.Length;i++) if(binding.hairRenderers[i]) binding.hairRenderers[i].localBounds=bounds[i];
        bones=null; bounds=null; reset=true;
    }
}
