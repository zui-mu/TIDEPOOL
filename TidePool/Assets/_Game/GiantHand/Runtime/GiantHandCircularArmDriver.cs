using System;
using UnityEngine;

namespace TidePool.GiantHand
{
    [ExecuteAlways]
    [DefaultExecutionOrder(0)]
    [DisallowMultipleComponent]
    public sealed class GiantHandCircularArmDriver : MonoBehaviour
    {
        private const float BasisTolerance = 0.00001f;
        private const float PositionTolerance = 0.001f;

        [SerializeField] private Transform[] joints = Array.Empty<Transform>();
        [SerializeField] private Transform wristTarget;
        [SerializeField] private Transform elbowPole;
        [SerializeField] private bool matchWristRotation = true;

        [Header("Diagnostics")]
        [SerializeField] private string solverStatus = "Waiting to solve.";
        [SerializeField] private float wristError = float.NaN;
        [SerializeField] private float maxBoneLengthError = float.NaN;

        public string Status => solverStatus;
        public float WristError => wristError;
        public float MaxBoneLengthError => maxBoneLengthError;
        public bool IsSolved { get; private set; }

        private float[] segmentLengths = Array.Empty<float>();
        private Pose[] worldPoses = Array.Empty<Pose>();
        private bool reachLimitsValid;
        private float minimumReach;
        private float maximumReach;

        private void LateUpdate() => Refresh();

        public bool Refresh()
        {
            solverStatus = ValidateChain();
            if (solverStatus != null)
            {
                IsSolved = false;
                wristError = float.NaN;
                maxBoneLengthError = float.NaN;
                return false;
            }

            Vector3 targetPosition = wristTarget.position;
            IsSolved = TryApplyPose(targetPosition, out solverStatus);
            if (IsSolved)
                solverStatus = "Target reached.";

            wristError = Vector3.Distance(joints[joints.Length - 1].position, targetPosition);
            maxBoneLengthError = 0f;
            for (int i = 0; i < segmentLengths.Length; i++)
            {
                float actualLength = Vector3.Distance(joints[i].position, joints[i + 1].position);
                maxBoneLengthError = Mathf.Max(maxBoneLengthError, Mathf.Abs(actualLength - segmentLengths[i]));
            }
            return IsSolved;
        }

        internal bool TryClampWristTarget(Vector3 desiredTarget, out Vector3 reachableTarget)
        {
            reachableTarget = desiredTarget;
            if (!isActiveAndEnabled || ValidateChain() != null)
                return false;

            if (!reachLimitsValid)
            {
                if (!UnequalCircularArcSolver.TryGetReachLimits(segmentLengths, out minimumReach, out maximumReach))
                    return false;

                // Leave room for world-position rounding at the semicircle limit.
                minimumReach = Mathf.Min(maximumReach, minimumReach + 0.0001f);
                reachLimitsValid = true;
            }

            Vector3 root = joints[0].position;
            Vector3 chord = desiredTarget - root;
            float distance = chord.magnitude;
            if (float.IsNaN(distance) || float.IsInfinity(distance))
                return false;
            if (distance >= minimumReach && distance <= maximumReach)
                return true;

            Vector3 axis = distance > 0.0001f ? chord / distance : (wristTarget.position - root).normalized;
            if (axis == Vector3.zero)
                axis = Vector3.forward;

            reachableTarget = root + Mathf.Clamp(distance, minimumReach, maximumReach) * axis;
            return true;
        }

        private string ValidateChain()
        {
            if (joints == null || joints.Length < 2 || joints[0] == null ||
                wristTarget == null || elbowPole == null)
                return "Assign the arm joints, wrist target, and elbow pole.";

            Transform root = joints[0];
            if (wristTarget.IsChildOf(root) || elbowPole.IsChildOf(root))
                return "Unsupported arm controls under the driven chain.";

            for (Transform ancestor = root; ancestor != null; ancestor = ancestor.parent)
            {
                if (!HasUnitScale(ancestor))
                    return "Unsupported arm scale. The chain and its ancestors must have unit scale.";
            }

            if (segmentLengths.Length != joints.Length - 1)
            {
                segmentLengths = new float[joints.Length - 1];
                reachLimitsValid = false;
            }

            for (int i = 1; i < joints.Length; i++)
            {
                Transform joint = joints[i];
                if (joint == null || joint.parent != joints[i - 1])
                    return "Unsupported arm hierarchy. Assign a continuous chain from shoulder to wrist.";

                if (!HasUnitScale(joint))
                    return "Unsupported arm scale. The chain and its ancestors must have unit scale.";

                Vector3 offset = joint.localPosition;
                if (!(offset.x > 0f && !float.IsInfinity(offset.x) &&
                      Mathf.Abs(offset.y) <= BasisTolerance && Mathf.Abs(offset.z) <= BasisTolerance))
                    return "Unsupported arm offset. Each child must be translated along its parent's positive X axis.";

                if (segmentLengths[i - 1] != offset.x)
                {
                    segmentLengths[i - 1] = offset.x;
                    reachLimitsValid = false;
                }
            }
            return null;
        }

        private bool TryApplyPose(Vector3 targetPosition, out string message)
        {
            if (!UnequalCircularArcSolver.TrySolve(
                    joints[0].position, targetPosition, elbowPole.position, segmentLengths,
                    out Pose rootWorld, out Pose[] childLocalPoses, out message))
                return false;

            // Verify the candidate against the offsets the Unity hierarchy will actually use.
            for (int i = 0; i < childLocalPoses.Length; i++)
                childLocalPoses[i].position = joints[i + 1].localPosition;

            if (worldPoses.Length != joints.Length)
                worldPoses = new Pose[joints.Length];

            int wristIndex = joints.Length - 1;
            childLocalPoses[wristIndex - 1].rotation = joints[wristIndex].localRotation;
            SerialChainFk.Evaluate(rootWorld, childLocalPoses, worldPoses);
            if (!(Vector3.Distance(worldPoses[wristIndex].position, targetPosition) <= PositionTolerance))
            {
                message = "Circular arm FK did not reach the wrist target.";
                return false;
            }

            for (int i = 0; i < segmentLengths.Length; i++)
            {
                float length = Vector3.Distance(worldPoses[i].position, worldPoses[i + 1].position);
                if (!(Mathf.Abs(length - segmentLengths[i]) <= PositionTolerance))
                {
                    message = "Circular arm FK changed a bone length.";
                    return false;
                }
            }

            joints[0].rotation = rootWorld.rotation;
            for (int i = 1; i < wristIndex; i++)
                joints[i].localRotation = childLocalPoses[i - 1].rotation;

            if (matchWristRotation)
            {
                Quaternion parentRotation = worldPoses[wristIndex - 1].rotation;
                joints[wristIndex].localRotation = Quaternion.Inverse(parentRotation) * wristTarget.rotation;
            }
            return true;
        }

        private static bool HasUnitScale(Transform joint)
        {
            return (joint.localScale - Vector3.one).sqrMagnitude <= BasisTolerance * BasisTolerance;
        }
    }
}
