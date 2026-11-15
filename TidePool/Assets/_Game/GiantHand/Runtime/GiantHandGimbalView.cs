using System;
using UnityEngine;

[ExecuteAlways]
[DefaultExecutionOrder(120)]
[DisallowMultipleComponent]
public sealed class GiantHandGimbalView : MonoBehaviour
{
    [SerializeField] private Transform incoming;
    [SerializeField] private Transform outgoing;
    [SerializeField] private Transform pitch;
    [SerializeField] private Transform yaw;
    [SerializeField] private Transform roll;

    // Around this threshold, the forward direction cannot define a stable pitch.
    private const double SingularCosine = 0.00001;
    private const double DegreesPerRadian = 180.0 / Math.PI;
    private bool hasPose;

    public float RotationError { get; private set; } = float.NaN;
    public float PitchAngle { get; private set; }
    public float YawAngle { get; private set; }
    public float RollAngle { get; private set; }

    public void Configure(Transform incomingFrame, Transform outgoingFrame,
        Transform pitchFrame, Transform yawFrame, Transform rollFrame)
    {
        incoming = incomingFrame;
        outgoing = outgoingFrame;
        pitch = pitchFrame;
        yaw = yawFrame;
        roll = rollFrame;
        hasPose = false;
        Refresh();
    }

    private void LateUpdate() => Refresh();

    public bool Refresh()
    {
        if (incoming == null || outgoing == null || pitch == null || yaw == null || roll == null
            || transform.parent != incoming || pitch.parent != transform || yaw.parent != pitch
            || roll.parent != yaw || outgoing == transform || outgoing.IsChildOf(transform))
        {
            hasPose = false;
            RotationError = float.NaN;
            return false;
        }

        Quaternion relative = Quaternion.Inverse(transform.rotation) * outgoing.rotation;
        Vector3 angles = Decompose(relative);
        SetLocalRotation(pitch, Quaternion.AngleAxis(angles.x, Vector3.forward));
        SetLocalRotation(yaw, Quaternion.AngleAxis(angles.y, Vector3.up));
        SetLocalRotation(roll, Quaternion.AngleAxis(angles.z, Vector3.right));
        PitchAngle = Mathf.DeltaAngle(0f, angles.x);
        YawAngle = Mathf.DeltaAngle(0f, angles.y);
        RollAngle = Mathf.DeltaAngle(0f, angles.z);
        hasPose = true;

        Quaternion error = Quaternion.Inverse(roll.rotation) * outgoing.rotation;
        double sine = Math.Sqrt((double)error.x * error.x + (double)error.y * error.y + (double)error.z * error.z);
        RotationError = (float)(2.0 * Math.Atan2(sine, Math.Abs(error.w)) * DegreesPerRadian);
        return true;
    }

    private Vector3 Decompose(Quaternion rotation)
    {
        double x = rotation.x;
        double y = rotation.y;
        double z = rotation.z;
        double w = rotation.w;
        double scale = 2.0 / (x * x + y * y + z * z + w * w);

        // First column of Rz(pitch) * Ry(yaw) * Rx(roll), i.e. the +X direction.
        double forwardX = 1.0 - scale * (y * y + z * z);
        double forwardY = scale * (x * y + z * w);
        double forwardZ = scale * (x * z - y * w);
        double horizontal = Math.Sqrt(forwardX * forwardX + forwardY * forwardY);
        float yawDegrees = (float)(Math.Atan2(-forwardZ, horizontal) * DegreesPerRadian);

        if (horizontal < SingularCosine)
        {
            float pitchDegrees = hasPose ? PitchAngle : 0f;
            Quaternion pitchYaw = Quaternion.AngleAxis(pitchDegrees, Vector3.forward)
                                  * Quaternion.AngleAxis(yawDegrees, Vector3.up);
            Quaternion twist = Quaternion.Inverse(pitchYaw) * rotation;
            float rollDegrees = (float)(2.0 * Math.Atan2(twist.x, twist.w) * DegreesPerRadian);
            return new Vector3(pitchDegrees, yawDegrees, rollDegrees);
        }

        double rowTwoY = scale * (y * z + x * w);
        double rowTwoZ = 1.0 - scale * (x * x + y * y);
        var principal = new Vector3(
            (float)(Math.Atan2(forwardY, forwardX) * DegreesPerRadian),
            yawDegrees,
            (float)(Math.Atan2(rowTwoY, rowTwoZ) * DegreesPerRadian));

        if (!hasPose)
            return principal;

        // Both decompositions give the same orientation. Keep the branch that
        // moves the physical rings least across the +/-90 degree yaw boundary.
        var alternate = new Vector3(principal.x + 180f, 180f - principal.y, principal.z + 180f);
        return TravelSquared(alternate) < TravelSquared(principal) ? alternate : principal;
    }

    private float TravelSquared(Vector3 angles)
    {
        var delta = new Vector3(
            Mathf.DeltaAngle(PitchAngle, angles.x),
            Mathf.DeltaAngle(YawAngle, angles.y),
            Mathf.DeltaAngle(RollAngle, angles.z));
        return delta.sqrMagnitude;
    }

    private static void SetLocalRotation(Transform frame, Quaternion desired)
    {
        Quaternion current = frame.localRotation;
        if (Quaternion.Dot(current, desired) < 0f)
            desired = new Quaternion(-desired.x, -desired.y, -desired.z, -desired.w);
        var delta = new Vector4(current.x - desired.x, current.y - desired.y,
            current.z - desired.z, current.w - desired.w);
        if (delta.sqrMagnitude > 1e-14f)
            frame.localRotation = desired;
    }
}
