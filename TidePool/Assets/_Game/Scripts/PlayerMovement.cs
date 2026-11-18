using UnityEngine;
using UnityEngine.InputSystem;

public interface IPlayerJumpHandler
{
    bool HandleJumpRequest(bool hasMovementInput);
    bool SuppressGroundMovement { get; }
    bool CanExecuteAnimatedJump { get; }
    void CancelJump();
}

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float runSpeed = 6f;
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private Transform cameraTransform;
    [Tooltip("Metres per second squared. Zero preserves the original instant movement.")]
    [SerializeField, Min(0f)] private float acceleration;

    public event System.Action MotionReset;

    private CharacterController controller;
    private float verticalVelocity;
    private Vector3 requestedVelocity;
    private IPlayerJumpHandler jumpHandler;
    private bool animatedLaunchPending;
    private Object movementLockOwner;
    public Vector3 PlanarVelocity { get; private set; }
    public float VerticalSpeed { get; private set; }
    public bool IsGrounded { get; private set; }
    public float WalkSpeed => walkSpeed;
    public float RunSpeed => runSpeed;
    public float JumpTakeoffSpeed => Mathf.Sqrt(Mathf.Max(0f, jumpHeight * -2f * gravity));
    public bool HasMotionSample { get; private set; }
    public bool IsMovementLocked => movementLockOwner;
    public bool HitCeiling { get; private set; }
    public uint JumpLaunchSequence { get; private set; }

    private void Awake() { controller = GetComponent<CharacterController>(); }
    private void OnDisable()
    {
        movementLockOwner = null;
        ResetMotion();
    }

    public bool TryLockMovement(Object owner)
    {
        if (!owner || !isActiveAndEnabled || !controller || !controller.enabled ||
            (movementLockOwner && movementLockOwner != owner))
            return false;

        if (movementLockOwner == owner)
            return true;

        movementLockOwner = owner;
        ResetMotion();
        IsGrounded = controller.isGrounded;
        HasMotionSample = true;
        return true;
    }

    public bool IsMovementLockedBy(Object owner) => owner && movementLockOwner == owner;

    internal bool TryMoveWhileLocked(Object owner, float targetWorldHeight, out CollisionFlags flags)
    {
        flags = CollisionFlags.None;
        if (!IsMovementLockedBy(owner) || !isActiveAndEnabled || !controller || !controller.enabled ||
            float.IsNaN(targetWorldHeight) || float.IsInfinity(targetWorldHeight))
            return false;

        float delta = targetWorldHeight - transform.position.y;
        if (Mathf.Abs(delta) > 0.00001f)
            flags = controller.Move(delta * Vector3.up);
        IsGrounded = controller.isGrounded || (flags & CollisionFlags.Below) != 0;
        HitCeiling = (flags & CollisionFlags.Above) != 0;
        HasMotionSample = true;
        return true;
    }

    public void ReleaseMovementLock(Object owner)
    {
        if (IsMovementLockedBy(owner))
            movementLockOwner = null;
    }

    public bool RegisterJumpHandler(IPlayerJumpHandler handler)
    {
        if (handler == null || (jumpHandler != null && !ReferenceEquals(jumpHandler, handler))) return false;
        jumpHandler = handler;
        return true;
    }
    public void UnregisterJumpHandler(IPlayerJumpHandler handler)
    {
        if (!ReferenceEquals(jumpHandler, handler)) return;
        animatedLaunchPending = false;
        jumpHandler = null;
    }
    public bool QueueAnimatedJump(IPlayerJumpHandler handler)
    {
        if (movementLockOwner || !ReferenceEquals(jumpHandler, handler) || !isActiveAndEnabled || !controller ||
            !controller.enabled || !controller.isGrounded || animatedLaunchPending) return false;
        animatedLaunchPending = true;
        return true;
    }
    public void CancelPendingAnimatedJump(IPlayerJumpHandler handler)
    {
        if (ReferenceEquals(jumpHandler, handler)) animatedLaunchPending = false;
    }
    public void ResetMotion()
    {
        animatedLaunchPending = false;
        requestedVelocity = PlanarVelocity = Vector3.zero;
        VerticalSpeed = verticalVelocity = 0f;
        IsGrounded = HasMotionSample = HitCeiling = false;
        jumpHandler?.CancelJump();
        MotionReset?.Invoke();
    }
    private void Update()
    {
        if (movementLockOwner)
        {
            IsGrounded = controller.enabled && controller.isGrounded;
            HasMotionSample = true;
            return;
        }

        var keyboard = Keyboard.current;
        Vector2 input = Vector2.zero;
        bool sprint = false, jump = false;
        if (keyboard != null)
        {
            input.x = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
            input.y = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
            sprint = keyboard.leftShiftKey.isPressed;
            jump = keyboard.spaceKey.wasPressedThisFrame;
        }
        if (!controller.enabled) { ResetMotion(); return; }
        if (Time.deltaTime <= 0f) return;
        bool launch = false;
        if (controller.isGrounded && jump)
            launch = jumpHandler == null || !jumpHandler.HandleJumpRequest(input.sqrMagnitude > 0f);
        if (animatedLaunchPending)
        {
            animatedLaunchPending = false;
            if (controller.isGrounded && jumpHandler != null && jumpHandler.CanExecuteAnimatedJump)
                launch = true;
            else
                jumpHandler?.CancelJump();
        }
        Vector3 forward = cameraTransform ? cameraTransform.forward : Vector3.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < .001f) forward = Vector3.forward;
        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector3 direction = Vector3.ClampMagnitude(forward * input.y + right * input.x, 1f);
        Vector3 desired = direction * (sprint ? runSpeed : walkSpeed);
        bool planted = controller.isGrounded && jumpHandler != null && jumpHandler.SuppressGroundMovement;
        requestedVelocity = planted ? Vector3.zero : acceleration > 0f
            ? Vector3.MoveTowards(requestedVelocity, desired, acceleration * Time.deltaTime)
            : desired;
        if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        if (launch)
        {
            verticalVelocity = JumpTakeoffSpeed;
            JumpLaunchSequence++;
        }
        verticalVelocity += gravity * Time.deltaTime;
        Vector3 velocity = requestedVelocity;
        velocity.y = verticalVelocity;
        Vector3 before = transform.position;
        CollisionFlags flags = controller.Move(velocity * Time.deltaTime);
        Vector3 actual = (transform.position - before) / Time.deltaTime;
        PlanarVelocity = Vector3.ProjectOnPlane(actual, Vector3.up);
        VerticalSpeed = actual.y;
        IsGrounded = controller.isGrounded || (flags & CollisionFlags.Below) != 0;
        HitCeiling = (flags & CollisionFlags.Above) != 0;
        if (HitCeiling && verticalVelocity > 0f) verticalVelocity = 0f;
        HasMotionSample = true;
    }
}
