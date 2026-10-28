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

    private CharacterController controller;
    private float verticalVelocity;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            return;
        }

        // 读取水平移动输入。
        float horizontal = 0f;
        float vertical = 0f;

        if (keyboard.aKey.isPressed)
        {
            horizontal -= 1f;
        }

        if (keyboard.dKey.isPressed)
        {
            horizontal += 1f;
        }

        if (keyboard.sKey.isPressed)
        {
            vertical -= 1f;
        }

        if (keyboard.wKey.isPressed)
        {
            vertical += 1f;
        }

        // 限制方向向量长度，避免斜向移动更快。
        Vector3 forward = Vector3.forward;
        Vector3 right = Vector3.right;

        if (cameraTransform != null)
        {
            forward = cameraTransform.forward;
            right = cameraTransform.right;
        }

        // 去掉竖直分量，只保留地面方向。
        forward.y = 0f;
        right.y = 0f;

        // 归一化，让方向向量的长度恢复为 1。
        forward.Normalize();
        right.Normalize();

        Vector3 moveDirection =
            forward * vertical + right * horizontal;

        moveDirection =
            Vector3.ClampMagnitude(moveDirection, 1f);

        float speed = walkSpeed;

        if (keyboard.leftShiftKey.isPressed)
        {
            speed = runSpeed;
        }

        // 站在地面时，保持轻微向下的速度。
        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        // 仅在地面上按下空格时起跳。
        if (controller.isGrounded &&
            keyboard.spaceKey.wasPressedThisFrame)
        {
            verticalVelocity =
                Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        // 重力改变竖直速度。
        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = moveDirection * speed;
        velocity.y = verticalVelocity;

        // Move 接收这一帧的位移。
        controller.Move(velocity * Time.deltaTime);
    }
}