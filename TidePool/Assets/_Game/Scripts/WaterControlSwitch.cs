using UnityEngine;
using UnityEngine.InputSystem;

public class WaterControlSwitch : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private float interactionDistance = 2.5f;

    [SerializeField] private Transform waterFrame;
    [SerializeField] private float normalHeight = -0.3f;
    [SerializeField] private float loweredHeight = -1.8f;
    [SerializeField] private float waterMoveSpeed = 0.5f;

    private bool isActivated;
    private float targetHeight;

    private void Start()
    {
        targetHeight = normalHeight;
    }

    private void Update()
    {
        MoveWater();

        if (player == null || waterFrame == null)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            return;
        }

        float distance = Vector3.Distance(
            transform.position,
            player.position
        );

        if (distance > interactionDistance)
        {
            return;
        }

        if (keyboard.eKey.wasPressedThisFrame)
        {
            isActivated = !isActivated;

            if (isActivated)
            {
                targetHeight = loweredHeight;
                Debug.Log("Start lowering the water level", this);
            }
            else
            {
                targetHeight = normalHeight;
                Debug.Log("Start restoring the water level", this);
            }
        }
    }

    private void MoveWater()
    {
        if (waterFrame == null)
        {
            return;
        }

        Vector3 position = waterFrame.position;

        position.y = Mathf.MoveTowards(
            position.y,
            targetHeight,
            waterMoveSpeed * Time.deltaTime
        );

        waterFrame.position = position;
    }
}