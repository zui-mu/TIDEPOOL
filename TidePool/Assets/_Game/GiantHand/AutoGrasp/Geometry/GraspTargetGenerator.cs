using UnityEngine;

namespace TidePool.GiantHand.AutoGrasp
{
    public static class GraspTargetGenerator
    {
        public static bool TryGenerate(
            CapsuleSnapshot capsule,
            GraspRigSnapshot rig,
            GraspSettings settings,
            float progress,
            GraspTargets output,
            out string message)
        {
            if (!CapsuleGeometry.IsValid(capsule)
                || rig == null
                || settings == null
                || output == null
                || rig.openTipPositionsInPalm == null
                || rig.openTipPositionsInPalm.Length != 5
                || settings.contactHeightFractions == null
                || settings.contactHeightFractions.Length != 5
                || settings.contactAngles == null
                || settings.contactAngles.Length != 5)
            { 
                message = "目标生成所需的数据不完整。";
                return false;
            }

            if (!CapsuleGeometry.IsFinite(progress)
                || !CapsuleGeometry.IsFinite(settings.palmGap)
                || !CapsuleGeometry.IsFinite(settings.fingertipRadius)
                || !CapsuleGeometry.IsFinite(
                    settings.contactBandHalfHeight)
                || settings.palmGap < 0f
                || settings.fingertipRadius < 0f
                || settings.contactBandHalfHeight <= 0f)
            {
                message = "进度或抓取尺寸参数无效。";
                return false;
            }

            Vector3 side = Vector3.ProjectOnPlane(
                rig.rootPosition - capsule.center,
                capsule.axis
            );

            if (side.sqrMagnitude < 0.000001f)
            {
                side = Vector3.ProjectOnPlane(
                    settings.preferredSide,
                    capsule.axis
                );
            }

            if (!CapsuleGeometry.IsFinite(side)
                || side.sqrMagnitude < 0.000001f)
            {
                message = "无法确定接近胶囊的侧向方向。";
                return false;
            }

            side.Normalize();

            Vector3 around = Vector3.Cross(
                capsule.axis,
                side
            ).normalized;

            Vector3 finalPalmPosition =
                capsule.center
                + side * (
                    capsule.radius + settings.palmGap
                );

            Quaternion finalPalmRotation =
                Quaternion.LookRotation(
                    -side,
                    capsule.axis
                );

            float reachProgress = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(0f, 0.6f, progress)
            );

            float closeProgress = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(0.6f, 1f, progress)
            );

            output.palmPosition = Vector3.Lerp(
                rig.startPalmPosition,
                finalPalmPosition,
                reachProgress
            );

            output.palmRotation = Quaternion.Slerp(
                rig.startPalmRotation,
                finalPalmRotation,
                reachProgress
            );

            Pose wristTarget = WristPalmPose.ToWrist(
                new Pose(output.palmPosition, output.palmRotation),
                new Pose(rig.palmPositionInWrist, rig.palmRotationInWrist)
            );
            output.wristPosition = wristTarget.position;
            output.wristRotation = wristTarget.rotation;

            float halfBand = Mathf.Min(
                settings.contactBandHalfHeight,
                capsule.height * 0.45f
            );

            for (int i = 0; i < 5; i++)
            {
                float heightFraction =
                    settings.contactHeightFractions[i];

                float angleDegrees =
                    settings.contactAngles[i];

                if (!CapsuleGeometry.IsFinite(heightFraction)
                    || !CapsuleGeometry.IsFinite(angleDegrees)
                    || Mathf.Abs(heightFraction) > 1f)
                {
                    message = "手指接触模板参数无效。";
                    return false;
                }

                float angle =
                    angleDegrees * Mathf.Deg2Rad;

                Vector3 radialDirection =
                    Mathf.Cos(angle) * side
                    + Mathf.Sin(angle) * around;

                if (!CapsuleGeometry.TrySurfacePoint(
                        capsule,
                        heightFraction * halfBand,
                        radialDirection,
                        out Vector3 surfacePoint,
                        out Vector3 surfaceNormal))
                {
                    message = "无法生成胶囊表面接触点。";
                    return false;
                }

                Vector3 openTipPosition =
                    output.palmPosition
                    + output.palmRotation
                    * rig.openTipPositionsInPalm[i];

                Vector3 closedTipPosition =
                    surfacePoint
                    + surfaceNormal
                    * settings.fingertipRadius;

                output.surfacePoints[i] = surfacePoint;
                output.surfaceNormals[i] = surfaceNormal;

                output.fingertipPositions[i] =
                    Vector3.Lerp(
                        openTipPosition,
                        closedTipPosition,
                        closeProgress
                    );
            }

            message = "抓取目标已生成，尚未进行 IK 求解。";
            return true;
        }
    }
}
