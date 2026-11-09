using System;
using TidePool.GiantHand.AutoGrasp;
using UnityEditor;
using UnityEngine;

namespace TidePool.GiantHand.Editor
{
    public static class GiantHandMathChecks
    {
        [MenuItem("Tools/GiantHand/Run Math Checks")]
        public static void Run()
        {
            CheckForwardKinematics();
            CheckWristPalmRoundTrip();
            CheckGeneratedWristTarget();
            CheckCircularArc();
            Debug.Log("GiantHand math checks passed: FK, wrist/palm, grasp target, circular arc.");
        }

        private static void CheckForwardKinematics()
        {
            var root = new Pose(new Vector3(10f, 2f, -3f), Quaternion.AngleAxis(90f, Vector3.forward));
            var locals = new[]
            {
                new Pose(new Vector3(2f, 0f, 0f), Quaternion.AngleAxis(90f, Vector3.right)),
                new Pose(Vector3.up, Quaternion.identity)
            };
            var world = new Pose[3];

            SerialChainFk.Evaluate(root, locals, world);

            ExpectVector(root.position, world[0].position, "FK root");
            ExpectVector(new Vector3(10f, 4f, -3f), world[1].position, "FK first child");
            ExpectVector(new Vector3(10f, 4f, -2f), world[2].position, "FK second child");
            ExpectVector(Vector3.up, world[1].rotation * Vector3.right, "FK child right");
            ExpectVector(Vector3.forward, world[1].rotation * Vector3.up, "FK child up");
        }

        private static void CheckWristPalmRoundTrip()
        {
            var wrist = new Pose(new Vector3(3f, -2f, 4f), Quaternion.AngleAxis(90f, Vector3.forward));
            var palmInWrist = new Pose(new Vector3(2f, 1f, -3f), Quaternion.AngleAxis(90f, Vector3.right));

            Pose palm = WristPalmPose.ToPalm(wrist, palmInWrist);

            ExpectVector(new Vector3(2f, 0f, 1f), palm.position, "Palm position");
            ExpectVector(Vector3.up, palm.rotation * Vector3.right, "Palm right");
            ExpectVector(Vector3.forward, palm.rotation * Vector3.up, "Palm up");

            Pose recovered = WristPalmPose.ToWrist(palm, palmInWrist);
            ExpectVector(wrist.position, recovered.position, "Recovered wrist position");
            ExpectVector(Vector3.up, recovered.rotation * Vector3.right, "Recovered wrist right");
            ExpectVector(Vector3.left, recovered.rotation * Vector3.up, "Recovered wrist up");
        }

        private static void CheckGeneratedWristTarget()
        {
            var capsule = new CapsuleSnapshot
            {
                center = new Vector3(4f, 2f, -3f),
                axis = Vector3.up,
                radius = 0.5f,
                height = 2f
            };
            var rig = new GraspRigSnapshot
            {
                rootPosition = new Vector3(8f, 2f, -3f),
                startPalmRotation = Quaternion.identity,
                palmPositionInWrist = new Vector3(2f, 1f, -3f),
                palmRotationInWrist = Quaternion.AngleAxis(90f, Vector3.right),
                openTipPositionsInPalm = new Vector3[5]
            };
            var targets = new GraspTargets();

            if (!GraspTargetGenerator.TryGenerate(capsule, rig, new GraspSettings { palmGap = 0f },
                    1f, targets, out string message))
                throw new InvalidOperationException(message);

            ExpectVector(new Vector3(4.5f, 2f, -3f), targets.palmPosition, "Generated palm");
            ExpectVector(new Vector3(3.5f, 5f, -5f), targets.wristPosition, "Generated wrist");
            ExpectVector(Vector3.forward, targets.wristRotation * Vector3.right, "Generated wrist right");
            ExpectVector(Vector3.right, targets.wristRotation * Vector3.up, "Generated wrist up");
        }

        private static void CheckCircularArc()
        {
            const int count = 8;
            const float length = 2f;
            var root = new Vector3(4f, -2f, 6f);
            var axis = new Vector3(0.6f, 0f, 0.8f);
            Vector3 pole = root + 2f * axis + Vector3.up;
            float diameter = length / Mathf.Sin(Mathf.PI / (2f * count));

            foreach (float distance in new[] { 0.01f, 1f, diameter, 15.999f, count * length })
            {
                Vector3 target = root + distance * axis;
                Vector3 bendPole = distance == count * length ? root : pole;
                if (!CircularArcSolver.TrySolve(root, target, bendPole, count, length,
                        out Pose rootWorld, out Pose[] locals, out string message))
                    throw new InvalidOperationException(message);

                var joints = new Pose[count + 1];
                SerialChainFk.Evaluate(rootWorld, locals, joints);
                ExpectVector(root, joints[0].position, "Arc root");
                ExpectVector(target, joints[count].position, $"Arc wrist at distance {distance}");
                for (int i = 0; i < count; i++)
                {
                    float actualLength = Vector3.Distance(joints[i].position, joints[i + 1].position);
                    ExpectNear(length, actualLength, $"Arc bone {i}");
                }

                if (distance == diameter)
                    ExpectVector(root + (diameter / 2f) * (axis + Vector3.up),
                        joints[count / 2].position, "Semicircle midpoint");
            }

            if (CircularArcSolver.TrySolve(root, root + 17f * axis, pole, count, length,
                    out _, out _, out _))
                throw new InvalidOperationException("Arc accepted an unreachable wrist.");
            if (CircularArcSolver.TrySolve(root, root + 8f * axis, root, count, length,
                    out _, out _, out _))
                throw new InvalidOperationException("Bent arc accepted a missing bend direction.");
        }

        private static void ExpectVector(Vector3 expected, Vector3 actual, string label)
        {
            ExpectNear(0f, Vector3.Distance(expected, actual), $"{label}, expected {expected}, got {actual}");
        }

        private static void ExpectNear(float expected, float actual, string label)
        {
            if (!(Mathf.Abs(expected - actual) <= 0.0001f))
                throw new InvalidOperationException($"{label}: expected {expected}, got {actual}.");
        }
    }
}
