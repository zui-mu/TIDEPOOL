using UnityEngine;

// Move the collider with PlayerMovement; rotate and animate only the visual child.
[DisallowMultipleComponent]
[DefaultExecutionOrder(200)]
[RequireComponent(typeof(PlayerMovement))]
public sealed class GirlPlayerAnimation : MonoBehaviour
{
    public Animator animator;
    public Transform visualRoot;
    [Min(1f)] public float turnSpeed = 720f;
    [Min(0f)] public float animationDamping = .08f;
    PlayerMovement movement;
    float airborneTime;
    static readonly int MoveSpeed = Animator.StringToHash("MoveSpeed");
    static readonly int Grounded = Animator.StringToHash("Grounded");
    static readonly int VerticalSpeed = Animator.StringToHash("VerticalSpeed");
    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        if (!animator) animator = GetComponentInChildren<Animator>();
        if (animator) animator.applyRootMotion = false;
    }
    void Update()
    {
        if (!animator || !visualRoot || !movement) return;
        Vector3 velocity = movement.PlanarVelocity;
        float speed = velocity.magnitude;
        if (speed > .08f)
        {
            Quaternion target = Quaternion.LookRotation(velocity, Vector3.up);
            visualRoot.rotation = Quaternion.RotateTowards(visualRoot.rotation, target, turnSpeed * Time.deltaTime);
        }
        airborneTime = movement.IsGrounded ? 0f : airborneTime + Time.deltaTime;
        animator.SetFloat(MoveSpeed, speed < .035f ? 0f : speed, animationDamping, Time.deltaTime);
        animator.SetBool(Grounded, movement.IsGrounded || (airborneTime < .07f && movement.VerticalSpeed < .5f));
        animator.SetFloat(VerticalSpeed, movement.VerticalSpeed);
    }
}
