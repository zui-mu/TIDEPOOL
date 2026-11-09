using System;
using System.Collections.Generic;
using UnityEngine;

namespace TidePool.GiantHand
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class GiantHandArcLab : MonoBehaviour
    {
        [SerializeField] private Transform wristTarget;
        [SerializeField] private Transform bendPole;
        [SerializeField, Min(0.01f)] private float segmentLength = 1.5f;
        [SerializeField] private Transform[] joints = Array.Empty<Transform>();

        public IReadOnlyList<Transform> Joints => joints;
        public Transform WristTarget => wristTarget;
        public Transform BendPole => bendPole;
        public float SegmentLength => segmentLength;
        public float WristError { get; private set; } = float.NaN;
        public float MaxBoneLengthError { get; private set; } = float.NaN;
        public bool IsSolved { get; private set; }
        public string Status { get; private set; } = "等待求解";

        private Pose[] worldPoses = Array.Empty<Pose>();
        private Vector3 lastRootPosition;
        private Vector3 lastWristPosition;
        private Vector3 lastPolePosition;
        private bool refreshRequested = true;

        private Transform Root => joints.Length == 0 ? null : joints[0];

        private void OnEnable() => refreshRequested = true;
        private void OnValidate() => refreshRequested = true;

        private void Update()
        {
            Transform root = Root;
            if (refreshRequested ||
                (root && wristTarget && bendPole &&
                 (!root.position.Equals(lastRootPosition) ||
                  !wristTarget.position.Equals(lastWristPosition) ||
                  !bendPole.position.Equals(lastPolePosition))))
                Refresh();
        }

        public void Refresh()
        {
            refreshRequested = false;
            if (!HasChain())
            {
                IsSolved = false;
                Status = "请指定腕目标、pole 和连续的关节层级";
                WristError = float.NaN;
                MaxBoneLengthError = float.NaN;
                return;
            }

            lastRootPosition = Root.position;
            lastWristPosition = wristTarget.position;
            lastPolePosition = bendPole.position;

            IsSolved = CircularArcSolver.TrySolve(
                lastRootPosition, lastWristPosition, lastPolePosition,
                joints.Length - 1, segmentLength,
                out Pose rootWorld, out Pose[] childLocalPoses, out string message);
            Status = IsSolved ? "已到达目标" : message;

            if (IsSolved)
            {
                if (worldPoses.Length != joints.Length)
                    worldPoses = new Pose[joints.Length];
                SerialChainFk.Evaluate(rootWorld, childLocalPoses, worldPoses);

                // Unity displays the poses produced by our FK.
                for (int i = 0; i < joints.Length; i++)
                    joints[i].SetPositionAndRotation(worldPoses[i].position, worldPoses[i].rotation);
            }

            WristError = Vector3.Distance(joints[joints.Length - 1].position, lastWristPosition);
            MaxBoneLengthError = 0f;
            for (int i = 1; i < joints.Length; i++)
            {
                float length = Vector3.Distance(joints[i - 1].position, joints[i].position);
                MaxBoneLengthError = Mathf.Max(MaxBoneLengthError, Mathf.Abs(length - segmentLength));
            }
        }

        private bool HasChain()
        {
            if (!wristTarget || !bendPole || joints.Length < 2 || !Root)
                return false;

            for (int i = 1; i < joints.Length; i++)
            {
                if (!joints[i] || joints[i].parent != joints[i - 1])
                    return false;
            }

            return true;
        }
    }
}
