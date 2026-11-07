using UnityEngine;

// Physics owns displacement; this component owns the complete animation's phase clocks.
[DisallowMultipleComponent]
[DefaultExecutionOrder(200)]
[RequireComponent(typeof(PlayerMovement))]
public sealed class GirlPlayerAnimation : MonoBehaviour, IPlayerJumpHandler
{
    public Animator animator;
    public Transform visualRoot;
    [Min(1f)] public float turnSpeed = 720f;
    [Min(0f)] public float animationDamping = .08f;
    [Header("Full jump clip landmarks (source frames)")]
    [SerializeField, Min(0f)] float takeoffFrame = 19f;
    [SerializeField, Min(0f)] float apexFrame = 25f;
    [SerializeField, Min(0f)] float contactFrame = 30f;
    enum JumpPhase { Idle, Preparing, AwaitingTakeoff, Airborne, Recovery }
    [SerializeField] JumpPhase jumpPhase;
    PlayerMovement movement;
    RuntimeAnimatorController boundController;
    AnimationClip jumpClip;
    float airborneTime, sourceFrame, fallingFrame, recoveryFrame, lastFrame;
    uint launchBeforeRequest;
    bool registered, fallOnly, movingJump;
    static readonly int MoveSpeed = Animator.StringToHash("MoveSpeed");
    static readonly int Grounded = Animator.StringToHash("Grounded");
    static readonly int VerticalSpeed = Animator.StringToHash("VerticalSpeed");
    static readonly int JumpActive = Animator.StringToHash("JumpActive");
    static readonly int JumpLanding = Animator.StringToHash("JumpLanding");
    static readonly int FallOnly = Animator.StringToHash("FallOnly");
    static readonly int JumpTime = Animator.StringToHash("JumpTime");
    static readonly int LandingTime = Animator.StringToHash("LandingTime");
    static readonly int FallTime = Animator.StringToHash("FallTime");

    bool InfrastructureReady => movement && movement.isActiveAndEnabled && animator &&
        animator.isActiveAndEnabled && visualRoot && boundController && animator.runtimeAnimatorController == boundController;
    public bool SuppressGroundMovement => InfrastructureReady && !movingJump &&
        (jumpPhase == JumpPhase.Preparing || jumpPhase == JumpPhase.AwaitingTakeoff || jumpPhase == JumpPhase.Recovery);
    public bool CanExecuteAnimatedJump => registered && InfrastructureReady && jumpPhase == JumpPhase.AwaitingTakeoff;

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        if (!animator) animator = GetComponentInChildren<Animator>();
        if (animator) animator.applyRootMotion = false;
    }
    void OnEnable() { TryBind(); }
    void OnDisable() { Unbind(); }
    bool HasParameter(int hash, AnimatorControllerParameterType type)
    {
        foreach (var p in animator.parameters) if (p.nameHash == hash && p.type == type) return true;
        return false;
    }
    bool TryBind()
    {
        if (!movement) movement = GetComponent<PlayerMovement>();
        if (!animator) animator = GetComponentInChildren<Animator>();
        if (registered && InfrastructureReady) return true;
        // Movement.OnDisable already cancels the sequence. Retain this valid visual owner
        // while movement is paused so its first re-enabled input cannot bypass preparation.
        if (registered && movement && !movement.isActiveAndEnabled && animator &&
            animator.isActiveAndEnabled && visualRoot && boundController &&
            animator.runtimeAnimatorController == boundController) return false;
        if (registered) Unbind();
        if (!movement || !movement.isActiveAndEnabled || !animator || !animator.isActiveAndEnabled || !visualRoot || !animator.runtimeAnimatorController) return false;
        if (!HasParameter(JumpActive, AnimatorControllerParameterType.Bool) ||
            !HasParameter(JumpLanding, AnimatorControllerParameterType.Bool) ||
            !HasParameter(FallOnly, AnimatorControllerParameterType.Bool) ||
            !HasParameter(JumpTime, AnimatorControllerParameterType.Float) ||
            !HasParameter(LandingTime, AnimatorControllerParameterType.Float) ||
            !HasParameter(FallTime, AnimatorControllerParameterType.Float)) return false;
        foreach (var clip in animator.runtimeAnimatorController.animationClips)
            if (clip.name == "Girl_Jump") { jumpClip = clip; break; }
        if (!jumpClip || jumpClip.frameRate <= 0f) return false;
        lastFrame = jumpClip.length * jumpClip.frameRate;
        if (!(0f < takeoffFrame && takeoffFrame < apexFrame && apexFrame < contactFrame && contactFrame < lastFrame)) return false;
        if (!movement.RegisterJumpHandler(this)) return false;
        registered = true;
        boundController = animator.runtimeAnimatorController;
        animator.applyRootMotion = false;
        CancelJump();
        return true;
    }
    void Unbind()
    {
        CancelJump();
        if (movement) movement.UnregisterJumpHandler(this);
        registered = false; boundController = null; jumpClip = null;
    }
    public bool HandleJumpRequest(bool hasMovementInput)
    {
        if (!InfrastructureReady) return false;
        if (jumpPhase != JumpPhase.Idle || animator.IsInTransition(0) ||
            !animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion")) return true;
        // Latch input intent once: W+Space from rest is moving; later input cannot change the type.
        movingJump = hasMovementInput;
        jumpPhase = JumpPhase.Preparing; sourceFrame = 0f; fallOnly = false;
        animator.SetFloat(JumpTime, 0f);
        WriteFlags();
        return true;
    }
    public void CancelJump()
    {
        if (movement) movement.CancelPendingAnimatedJump(this);
        jumpPhase = JumpPhase.Idle; fallOnly = movingJump = false; airborneTime = 0f;
        // Keep outgoing pose clocks intact while blending back to locomotion.
        WriteFlags();
    }
    void WriteFlags()
    {
        if (!animator || !boundController || animator.runtimeAnimatorController != boundController) return;
        animator.SetBool(JumpActive, jumpPhase != JumpPhase.Idle);
        animator.SetBool(JumpLanding, jumpPhase == JumpPhase.Recovery);
        animator.SetBool(FallOnly, fallOnly);
    }
    void BeginFall()
    {
        movement.CancelPendingAnimatedJump(this);
        jumpPhase = JumpPhase.Airborne; fallOnly = true; fallingFrame = apexFrame;
        animator.SetFloat(FallTime, fallingFrame / lastFrame);
        WriteFlags();
    }
    void BeginRecovery()
    {
        jumpPhase = JumpPhase.Recovery; recoveryFrame = contactFrame;
        animator.SetFloat(LandingTime, recoveryFrame / lastFrame);
        WriteFlags();
    }
    void AdvanceFlight()
    {
        if (movement.IsGrounded) { BeginRecovery(); return; }
        if (!fallOnly && movement.HitCeiling) { BeginFall(); return; }
        float speed = movement.VerticalSpeed;
        float launchSpeed = Mathf.Max(.01f, movement.JumpTakeoffSpeed);
        float target = speed > 0f && !fallOnly
            ? Mathf.Lerp(takeoffFrame, apexFrame, 1f - Mathf.Clamp01(speed / launchSpeed))
            : Mathf.Lerp(apexFrame, contactFrame, Mathf.Clamp01(-speed / launchSpeed));
        if (fallOnly)
        {
            // A newly acquired fall may already have substantial downward velocity.
            // Catch up continuously at the source sampling rate, not several poses in one tick.
            fallingFrame = Mathf.MoveTowards(fallingFrame, Mathf.Max(fallingFrame, target),
                jumpClip.frameRate * Time.deltaTime);
            animator.SetFloat(FallTime, fallingFrame / lastFrame);
        }
        else
        {
            sourceFrame = Mathf.Max(sourceFrame, target);
            animator.SetFloat(JumpTime, sourceFrame / lastFrame);
        }
    }
    void Update()
    {
        if (!TryBind() || !movement.HasMotionSample) return;
        Vector3 velocity = movement.PlanarVelocity;
        float speed = velocity.magnitude;
        if (speed > .08f)
        {
            Quaternion target = Quaternion.LookRotation(velocity, Vector3.up);
            visualRoot.rotation = Quaternion.RotateTowards(visualRoot.rotation, target, turnSpeed * Time.deltaTime);
        }
        airborneTime = movement.IsGrounded ? 0f : airborneTime + Time.deltaTime;
        bool stableGrounded = movement.IsGrounded || (airborneTime < .07f && movement.VerticalSpeed < .5f);
        animator.SetFloat(MoveSpeed, speed < .035f ? 0f : speed, animationDamping, Time.deltaTime);
        animator.SetBool(Grounded, stableGrounded);
        animator.SetFloat(VerticalSpeed, movement.VerticalSpeed);
        if (Time.deltaTime <= 0f) return;
        switch (jumpPhase)
        {
            case JumpPhase.Idle:
                if (!stableGrounded) BeginFall();
                break;
            case JumpPhase.Preparing:
                if (!stableGrounded) { BeginFall(); break; }
                sourceFrame = Mathf.Min(takeoffFrame, sourceFrame + jumpClip.frameRate * Time.deltaTime);
                animator.SetFloat(JumpTime, sourceFrame / lastFrame);
                if (sourceFrame >= takeoffFrame)
                {
                    launchBeforeRequest = movement.JumpLaunchSequence;
                    if (movement.QueueAnimatedJump(this)) jumpPhase = JumpPhase.AwaitingTakeoff;
                    else if (!movement.IsGrounded) BeginFall();
                    else CancelJump();
                }
                break;
            case JumpPhase.AwaitingTakeoff:
                if (movement.JumpLaunchSequence != launchBeforeRequest)
                {
                    jumpPhase = JumpPhase.Airborne;
                    if (movement.IsGrounded && movement.VerticalSpeed <= 0f) BeginRecovery();
                    else if (!movement.IsGrounded) AdvanceFlight();
                }
                else if (!stableGrounded) BeginFall();
                break;
            case JumpPhase.Airborne:
                AdvanceFlight();
                break;
            case JumpPhase.Recovery:
                if (!stableGrounded) { BeginFall(); break; }
                recoveryFrame = Mathf.Min(lastFrame, recoveryFrame + jumpClip.frameRate * Time.deltaTime);
                animator.SetFloat(LandingTime, recoveryFrame / lastFrame);
                if (recoveryFrame >= lastFrame) { jumpPhase = JumpPhase.Idle; movingJump = false; WriteFlags(); }
                break;
        }
    }
}
