using System;
using UnityEngine;

namespace TidePool.GiantHand.AutoGrasp
{
    [Serializable]
    public struct CapsuleSnapshot
    {
        public Vector3 center;
        public Vector3 axis;

        public float radius;
        public float height;
    }

    public sealed class GraspRigSnapshot
    {
        public Vector3 rootPosition;

        public Vector3 startPalmPosition;
        public Quaternion startPalmRotation;

        public Vector3 palmPositionInWrist;
        public Quaternion palmRotationInWrist;

        public Vector3[] openTipPositionsInPalm;
    }

    [Serializable]
    public sealed class GraspSettings
    {
        [Min(0f)]
        public float palmGap = 0.15f;

        [Min(0f)]
        public float fingertipRadius = 0.06f;

        [Min(0.01f)]
        public float contactBandHalfHeight = 0.7f;

        public Vector3 preferredSide = Vector3.right;

        public float[] contactHeightFractions =
        {
            0.75f,
            0.25f,
            -0.25f,
            -0.75f,
            0f
        };

        public float[] contactAngles =
        {
            115f,
            125f,
            135f,
            145f,
            -65f
        };
    }

    public sealed class GraspTargets
    {
        public Vector3 palmPosition;
        public Quaternion palmRotation;

        public Vector3 wristPosition;
        public Quaternion wristRotation;

        public readonly Vector3[] surfacePoints = new Vector3[5];
        public readonly Vector3[] surfaceNormals = new Vector3[5];
        public readonly Vector3[] fingertipPositions = new Vector3[5];
    }
}