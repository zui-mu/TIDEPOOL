using UnityEngine;

namespace TidePool.GiantHand
{
    [ExecuteAlways]
    [DefaultExecutionOrder(-10)]
    [DisallowMultipleComponent]
    public sealed class GiantHandPalmControl : MonoBehaviour
    {
        private const float HoldThresholdSquared = 0.0025f;
        private const float ResumeThresholdSquared = 0.0064f;
        private const float BendSpeed = 360f;
        private const float PositionToleranceSquared = 0.0000000001f;

        [SerializeField] private bool driveFromPalm = true;

        [Tooltip("Additional rotation of the arm's bend around the shoulder-to-wrist line, in degrees.")]
        [Range(-180f, 180f)]
        [SerializeField] private float bendOffset;

        [Header("Rig References")]
        [SerializeField] private Transform palmTarget;
        [SerializeField] private Transform armRoot;
        [SerializeField] private Transform wristTarget;
        [SerializeField] private Transform elbowPole;

        [Tooltip("Center of the visible palm in wrist coordinates. Palm and wrist use the same axes.")]
        [SerializeField] private Vector3 palmCenterInWrist;

        [Header("Player Follow (Play Mode)")]
        [SerializeField] private bool followPlayer;
        [SerializeField] private Vector3 followOffset;
        [SerializeField] private CharacterController playerBody;
        [SerializeField] private GiantHandCircularArmDriver circularArm;

        private double lastUpdateTime;
        private Vector3 bendDirection;
        private bool holdingDirection;

        private void OnEnable()
        {
            lastUpdateTime = Time.realtimeSinceStartupAsDouble;
            bendDirection = Vector3.zero;
            holdingDirection = false;
        }

        private void LateUpdate()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            float elapsed = Mathf.Clamp((float)(now - lastUpdateTime), 0f, 0.05f);
            lastUpdateTime = now;
            Refresh(elapsed);
        }

        private bool Refresh(float elapsed)
        {
            if (!driveFromPalm || !HasValidBindings())
            {
                bendDirection = Vector3.zero;
                holdingDirection = false;
                return false;
            }

            bool following = followPlayer && playerBody && circularArm && Application.IsPlaying(gameObject);
            Vector3 palmPosition = following
                ? playerBody.transform.TransformPoint(playerBody.center) + followOffset
                : palmTarget.position;
            var palm = new Pose(palmPosition, palmTarget.rotation);
            var palmInWrist = new Pose(palmCenterInWrist, Quaternion.identity);
            Pose wrist = WristPalmPose.ToWrist(palm, palmInWrist);
            if (following)
            {
                if (!circularArm.TryClampWristTarget(wrist.position, out Vector3 reachableWrist))
                    return false;

                // The wrist and palm have the same positional correction when rotation is fixed.
                Vector3 reachablePalm = palmPosition + (reachableWrist - wrist.position);
                if ((palmTarget.position - reachablePalm).sqrMagnitude > PositionToleranceSquared)
                    palmTarget.position = reachablePalm;
                wrist.position = reachableWrist;
            }

            Vector3 chord = wrist.position - armRoot.position;
            float lengthSquared = chord.sqrMagnitude;
            if (float.IsNaN(lengthSquared) || float.IsInfinity(lengthSquared))
                return false;

            if ((wristTarget.position - wrist.position).sqrMagnitude > PositionToleranceSquared ||
                Quaternion.Angle(wristTarget.rotation, wrist.rotation) > 0.01f)
                wristTarget.SetPositionAndRotation(wrist.position, wrist.rotation);

            if (lengthSquared <= PositionToleranceSquared)
                return false;

            float length = Mathf.Sqrt(lengthSquared);
            Vector3 axis = chord / length;

            // Local -Y faces the grasp. Bend away from the palm, toward its +Y.
            Vector3 desired = Vector3.ProjectOnPlane(palmTarget.up, axis);
            float threshold = holdingDirection ? ResumeThresholdSquared : HoldThresholdSquared;
            holdingDirection = desired.sqrMagnitude < threshold;

            Vector3 previous = Vector3.ProjectOnPlane(bendDirection, axis);
            if (previous.sqrMagnitude < HoldThresholdSquared)
                previous = holdingDirection ? InitialBendDirection(axis) : desired;
            bendDirection = previous.normalized;

            float remainingTurn = 0f;
            if (!holdingDirection)
            {
                desired.Normalize();
                float turn = Vector3.Dot(bendDirection, desired) < -0.99999f
                    ? 180f
                    : Vector3.SignedAngle(bendDirection, desired, axis);
                float maxTurn = BendSpeed * elapsed;
                float step = Mathf.Clamp(turn, -maxTurn, maxTurn);
                bendDirection = Quaternion.AngleAxis(step, axis) * bendDirection;
                remainingTurn = turn - step;
            }

            // Keep the automatic direction separate from the user offset.
            Vector3 bend = Quaternion.AngleAxis(bendOffset, axis) * bendDirection;
            Vector3 pole = armRoot.position + 0.5f * chord + Mathf.Max(1f, 0.25f * length) * bend;
            if ((elbowPole.position - pole).sqrMagnitude > PositionToleranceSquared)
                elbowPole.position = pole;

#if UNITY_EDITOR
            if (Mathf.Abs(remainingTurn) > 0.05f && !Application.IsPlaying(gameObject))
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
            return true;
        }

        private Vector3 InitialBendDirection(Vector3 axis)
        {
            Vector3 currentPole = Vector3.ProjectOnPlane(elbowPole.position - armRoot.position, axis);
            if (currentPole.sqrMagnitude > PositionToleranceSquared)
                return Quaternion.AngleAxis(-bendOffset, axis) * currentPole;

            // When the palm normal is parallel to the chord, the palm's forward axis is perpendicular to it.
            return Vector3.ProjectOnPlane(palmTarget.forward, axis);
        }

        private bool HasValidBindings()
        {
            return palmTarget && armRoot && wristTarget && elbowPole &&
                !palmTarget.IsChildOf(armRoot) &&
                !palmTarget.IsChildOf(wristTarget) && !palmTarget.IsChildOf(elbowPole) &&
                !armRoot.IsChildOf(wristTarget) && !armRoot.IsChildOf(elbowPole) &&
                !wristTarget.IsChildOf(armRoot) && !elbowPole.IsChildOf(armRoot) &&
                !wristTarget.IsChildOf(elbowPole) && !elbowPole.IsChildOf(wristTarget);
        }
    }
}