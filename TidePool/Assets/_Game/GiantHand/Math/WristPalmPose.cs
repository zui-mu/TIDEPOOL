using UnityEngine;

namespace TidePool.GiantHand
{
    public static class WristPalmPose
    {
        // palmInWrist is the fixed palm pose expressed in wrist coordinates.
        public static Pose ToPalm(Pose wristWorld, Pose palmInWrist)
        {
            return new Pose(
                wristWorld.position + wristWorld.rotation * palmInWrist.position,
                wristWorld.rotation * palmInWrist.rotation
            );
        }

        public static Pose ToWrist(Pose palmWorld, Pose palmInWrist)
        {
            Quaternion wristRotation = palmWorld.rotation * Quaternion.Inverse(palmInWrist.rotation);
            Vector3 wristPosition = palmWorld.position - wristRotation * palmInWrist.position;
            return new Pose(wristPosition, wristRotation);
        }
    }
}
