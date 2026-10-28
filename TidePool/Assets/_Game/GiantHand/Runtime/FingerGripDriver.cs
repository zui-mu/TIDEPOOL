using UnityEngine;

[DefaultExecutionOrder(50)]
public class FingerGripDriver : MonoBehaviour
{
    [Header("Palm reference")]
    public Transform wrist;

    [Header("Finger joints")]
    public Transform joint1;
    public Transform joint2;
    public Transform joint3;

    [Header("Grip")]
    [Range(0f, 1f)]
    public float grip = 0f;

    [Range(-45f, 45f)]
    public float joint1ClosedAngle = -35f;

    [Range(-90f, 0f)]
    public float joint2ClosedAngle = -65f;

    [Range(-90f, 0f)]
    public float joint3ClosedAngle = -45f;

    [Header("Spread")]
    [Range(0f, 1f)]
    public float spread = 1f;

    [Range(-90f, 90f)]
    public float spreadClosedAngle = -19f;

    [Range(-90f, 90f)]
    public float spreadOpenAngle = 0f;

    private void LateUpdate()
    {
        if (wrist == null || joint1 == null || joint2 == null || joint3 == null || joint1.parent == null)
        {
            return;
        }

        float gripAmount = Mathf.Clamp01(grip);
        float spreadAmount = Mathf.Clamp01(spread);

        float spreadAngle = Mathf.Lerp(spreadClosedAngle,spreadOpenAngle,spreadAmount);

        Vector3 spreadAxis = joint1.parent.InverseTransformDirection(wrist.up).normalized;

        Quaternion spreadRotation = Quaternion.AngleAxis(spreadAngle, spreadAxis);

        Quaternion rootBendRotation =Quaternion.AngleAxis(gripAmount * joint1ClosedAngle,Vector3.forward);

        joint1.localRotation =spreadRotation * rootBendRotation;

        joint2.localRotation =Quaternion.AngleAxis(gripAmount * joint2ClosedAngle,Vector3.forward);

        joint3.localRotation =Quaternion.AngleAxis(gripAmount * joint3ClosedAngle,Vector3.forward);
    }
}