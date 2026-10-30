using UnityEngine;
using UnityEngine.InputSystem;

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

    private CharacterController controller;
    private float verticalVelocity;
    private Vector3 requestedVelocity;
    public Vector3 PlanarVelocity { get; private set; }
    public float VerticalSpeed { get; private set; }
    public bool IsGrounded { get; private set; }
    public float WalkSpeed => walkSpeed;
    public float RunSpeed => runSpeed;

    private void Awake() { controller = GetComponent<CharacterController>(); }
    private void OnDisable()
    {
        requestedVelocity = PlanarVelocity = Vector3.zero;
        VerticalSpeed = verticalVelocity = 0f;
    }
    private void Update()
    {
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
        if (!controller.enabled || Time.deltaTime <= 0f) return;
        Vector3 forward = cameraTransform ? cameraTransform.forward : Vector3.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < .001f) forward = Vector3.forward;
        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector3 direction = Vector3.ClampMagnitude(forward * input.y + right * input.x, 1f);
        Vector3 desired = direction * (sprint ? runSpeed : walkSpeed);
        requestedVelocity = acceleration > 0f
            ? Vector3.MoveTowards(requestedVelocity, desired, acceleration * Time.deltaTime)
            : desired;
        if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        if (controller.isGrounded && jump) verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        verticalVelocity += gravity * Time.deltaTime;
        Vector3 velocity = requestedVelocity;
        velocity.y = verticalVelocity;
        Vector3 before = transform.position;
        CollisionFlags flags = controller.Move(velocity * Time.deltaTime);
        Vector3 actual = (transform.position - before) / Time.deltaTime;
        PlanarVelocity = Vector3.ProjectOnPlane(actual, Vector3.up);
        VerticalSpeed = actual.y;
        IsGrounded = controller.isGrounded || (flags & CollisionFlags.Below) != 0;
        if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f;
    }
}
