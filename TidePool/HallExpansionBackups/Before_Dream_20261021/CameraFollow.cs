using UnityEngine;
using UnityEngine.InputSystem;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;

    [SerializeField] private float distance = 6.7f;
    [SerializeField] private float lookHeight = 0.5f;
    [SerializeField] private float mouseSensitivity = 0.15f;

    [SerializeField] private float minPitch = 5f;
    [SerializeField] private float maxPitch = 75f;

    private float yaw;
    private float pitch;

    private void Start()
    {
        yaw = transform.eulerAngles.y;
        pitch = transform.eulerAngles.x;

        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Mouse mouse = Mouse.current;

        if (mouse != null && mouse.rightButton.isPressed)
        {
            Vector2 mouseDelta = mouse.delta.ReadValue();

            yaw += mouseDelta.x * mouseSensitivity;
            pitch -= mouseDelta.y * mouseSensitivity;

            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        Vector3 focusPoint =
            target.position + Vector3.up * lookHeight;

        Quaternion rotation =
            Quaternion.Euler(pitch, yaw, 0f);

        Vector3 offset =
            rotation * new Vector3(0f, 0f, -distance);

        transform.position = focusPoint + offset;
        transform.rotation = rotation;
    }
}