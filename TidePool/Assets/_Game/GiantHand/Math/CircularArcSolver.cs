using System;
using UnityEngine;

namespace TidePool.GiantHand
{
    public static class CircularArcSolver
    {
        private const double StraightTolerance = 1e-6;
        private const float PoleAngleTolerance = 1e-6f;

        public static bool TrySolve(
            Vector3 root,
            Vector3 target,
            Vector3 pole,
            int segmentCount,
            float segmentLength,
            out Pose rootWorld,
            out Pose[] childLocalPoses,
            out string message)
        {
            return TrySolve(root, target, pole, segmentCount, segmentLength, null,
                out rootWorld, out childLocalPoses, out _, out message);
        }

        internal static bool TrySolve(
            Vector3 root,
            Vector3 target,
            Vector3 pole,
            int segmentCount,
            float segmentLength,
            Quaternion? previousPlane,
            out Pose rootWorld,
            out Pose[] childLocalPoses,
            out Quaternion plane,
            out string message)
        {
            rootWorld = default;
            childLocalPoses = Array.Empty<Pose>();
            plane = Quaternion.identity;

            if (segmentCount < 1 || !IsFinite(segmentLength) || segmentLength <= 0f ||
                !IsFinite(root) || !IsFinite(target) || !IsFinite(pole))
            {
                message = "Arc requires finite positions, a positive segment length, and at least one segment.";
                return false;
            }

            double distance = GetDirectionAndDistance(root, target, out Vector3 chordDirection);
            double reach = (double)segmentLength * segmentCount;
            double reachError = (distance - reach) / reach;
            bool isStraight = Math.Abs(reachError) <= StraightTolerance;
            if (reachError > StraightTolerance || (segmentCount == 1 && !isStraight))
            {
                message = "Arc target is unreachable.";
                return false;
            }

            GetDirectionAndDistance(root, pole, out Vector3 poleDirection);
            Vector3 normal = Vector3.Cross(chordDirection, poleDirection);
            float normalLength = normal.magnitude;
            if (distance == 0d)
            {
                // The chord direction vanishes at closure. Reuse its full frame to keep
                // the same winding. On a cold start, align the bend side with the pole.
                plane = previousPlane ?? (poleDirection.sqrMagnitude > 0f
                    ? Quaternion.FromToRotation(Vector3.up, poleDirection)
                    : Quaternion.identity);
            }
            else if (normalLength > PoleAngleTolerance)
            {
                normal /= normalLength;
                Vector3 bendDirection = Vector3.Cross(normal, chordDirection);
                plane = Quaternion.LookRotation(normal, bendDirection);
            }
            else if (isStraight)
            {
                plane = Quaternion.FromToRotation(Vector3.right, chordDirection);
            }
            else
            {
                message = "Bent arc requires a pole off the root-to-target line.";
                return false;
            }

            double turn = 0d;
            if (!isStraight && !TrySolveTurn(segmentCount, distance / segmentLength, out turn))
            {
                message = "Arc turn did not converge.";
                return false;
            }

            double turnDegrees = turn * (180d / Math.PI);
            float firstLinkAngle = (float)((segmentCount - 1) * turnDegrees / 2d);
            rootWorld = new Pose(root, plane * Quaternion.AngleAxis(firstLinkAngle, Vector3.forward));

            Vector3 linkOffset = segmentLength * Vector3.right;
            Quaternion jointTurn = Quaternion.AngleAxis(-(float)turnDegrees, Vector3.forward);
            childLocalPoses = new Pose[segmentCount];
            for (int i = 0; i < segmentCount - 1; i++)
                childLocalPoses[i] = new Pose(linkOffset, jointTurn);
            childLocalPoses[segmentCount - 1] = new Pose(linkOffset, Quaternion.identity);

            message = string.Empty;
            return true;
        }

        private static bool TrySolveTurn(int segmentCount, double chordInLinks, out double turn)
        {
            if (chordInLinks == 0d)
            {
                turn = 2d * Math.PI / segmentCount;
                return true;
            }

            // The circular chord equation sin(n * turn / 2) / sin(turn / 2) = distance / length
            // becomes the Chebyshev polynomial U_(n-1)(cos(turn / 2)), including the straight limit.
            double lowerCosine = Math.Cos(Math.PI / segmentCount);
            double cosine = 1d;
            for (int iteration = 0; iteration < 24; iteration++)
            {
                double previousValue = 0d;
                double value = 1d;
                double previousDerivative = 0d;
                double derivative = 0d;
                for (int i = 1; i < segmentCount; i++)
                {
                    double nextValue = 2d * cosine * value - previousValue;
                    double nextDerivative = 2d * value + 2d * cosine * derivative - previousDerivative;
                    previousValue = value;
                    value = nextValue;
                    previousDerivative = derivative;
                    derivative = nextDerivative;
                }

                double residual = value - chordInLinks;
                if (Math.Abs(residual) <= 1e-10 * segmentCount)
                {
                    turn = 2d * Math.Acos(cosine);
                    return true;
                }

                // Above its largest root the polynomial is increasing and convex, so Newton
                // from 1 stays on the single-turn branch. Clamp for floating-point roundoff.
                cosine = Math.Max(lowerCosine, Math.Min(1d, cosine - residual / derivative));
            }

            turn = 0d;
            return false;
        }

        private static double GetDirectionAndDistance(Vector3 from, Vector3 to, out Vector3 direction)
        {
            double x = (double)to.x - from.x;
            double y = (double)to.y - from.y;
            double z = (double)to.z - from.z;
            double distance = Math.Sqrt(x * x + y * y + z * z);
            direction = distance == 0d
                ? Vector3.zero
                : new Vector3((float)(x / distance), (float)(y / distance), (float)(z / distance));
            return distance;
        }

        private static bool IsFinite(Vector3 vector)
        {
            return IsFinite(vector.x) && IsFinite(vector.y) && IsFinite(vector.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
