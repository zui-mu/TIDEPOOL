using System;
using System.Collections.Generic;
using UnityEngine;

namespace TidePool.GiantHand
{
    public static class UnequalCircularArcSolver
    {
        private const double StraightTolerance = 1e-6;
        private const double ResidualTolerance = 1e-12;
        private const double RadiansToDegrees = 180d / Math.PI;
        private const float PoleAngleTolerance = 1e-6f;

        public static bool TrySolve(
            Vector3 root,
            Vector3 target,
            Vector3 pole,
            IReadOnlyList<float> segmentLengths,
            out Pose rootWorld,
            out Pose[] childLocalPoses,
            out string message)
        {
            rootWorld = default;
            childLocalPoses = Array.Empty<Pose>();

            if (segmentLengths == null || segmentLengths.Count == 0 ||
                !IsFinite(root) || !IsFinite(target) || !IsFinite(pole))
            {
                message = "Arc requires finite positions and at least one segment.";
                return false;
            }

            if (!TryGetLengthBounds(segmentLengths, out double longest, out double reach))
            {
                message = "Arc requires finite, positive segment lengths.";
                return false;
            }

            double distance = GetDirectionAndDistance(root, target, out Vector3 chordDirection);
            if (distance == 0d)
            {
                message = "Arc target and root coincide, so the chord has no direction.";
                return false;
            }

            double reachError = (distance - reach) / reach;
            bool isStraight = Math.Abs(reachError) <= StraightTolerance;
            if (reachError > StraightTolerance || (segmentLengths.Count == 1 && !isStraight))
            {
                message = "Arc target is unreachable.";
                return false;
            }

            double q = 0d;
            if (!isStraight)
            {
                // q = distance / diameter. The largest allowed sweep is a semicircle,
                // where q = 1 and the half angles add to pi / 2.
                if (distance < longest || SumHalfAngles(segmentLengths, distance, 1d) > Math.PI / 2d + ResidualTolerance)
                {
                    message = "Arc target is too close for a sweep of at most 180 degrees.";
                    return false;
                }

                if (!TrySolveRadiusRatio(segmentLengths, distance, reach, out q))
                {
                    message = "Arc radius did not converge.";
                    return false;
                }
            }

            GetDirectionAndDistance(root, pole, out Vector3 poleDirection);
            Vector3 normal = Vector3.Cross(chordDirection, poleDirection);
            float normalLength = normal.magnitude;
            Quaternion plane;
            if (normalLength > PoleAngleTolerance)
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

            double firstHalfAngle = GetHalfAngle(segmentLengths[0], distance, q);
            double firstLinkAngle = SumHalfAngles(segmentLengths, distance, q) - firstHalfAngle;
            rootWorld = new Pose(root, plane * Quaternion.AngleAxis((float)(firstLinkAngle * RadiansToDegrees), Vector3.forward));

            childLocalPoses = new Pose[segmentLengths.Count];
            double halfAngle = firstHalfAngle;
            for (int i = 0; i < childLocalPoses.Length - 1; i++)
            {
                double nextHalfAngle = GetHalfAngle(segmentLengths[i + 1], distance, q);
                float turnDegrees = (float)(-(halfAngle + nextHalfAngle) * RadiansToDegrees);
                childLocalPoses[i] = new Pose(segmentLengths[i] * Vector3.right, Quaternion.AngleAxis(turnDegrees, Vector3.forward));
                halfAngle = nextHalfAngle;
            }

            int last = childLocalPoses.Length - 1;
            childLocalPoses[last] = new Pose(segmentLengths[last] * Vector3.right, Quaternion.identity);
            message = string.Empty;
            return true;
        }

        private static bool TrySolveRadiusRatio(IReadOnlyList<float> lengths, double distance, double reach, out double q)
        {
            // The endpoint chord condition is sin(sum of half angles) / q = 1.
            // At q = 0 the quotient has the straight limit reach / distance.
            double previous = 0d;
            double previousResidual = reach / distance - 1d;
            double current = 1d;
            double currentResidual = Math.Sin(SumHalfAngles(lengths, distance, current)) - 1d;
            for (int iteration = 0; iteration < 64; iteration++)
            {
                if (Math.Abs(currentResidual) <= ResidualTolerance)
                {
                    q = current;
                    return true;
                }

                // Illinois false position. Reweight a retained endpoint so the
                // interpolation does not stall when one side of the bracket stays fixed.
                double next = (previous * currentResidual - current * previousResidual) / (currentResidual - previousResidual);
                double nextResidual = Math.Sin(SumHalfAngles(lengths, distance, next)) / next - 1d;
                if (Math.Sign(nextResidual) != Math.Sign(currentResidual))
                {
                    previous = current;
                    previousResidual = currentResidual;
                }
                else
                {
                    previousResidual *= 0.5d;
                }

                current = next;
                currentResidual = nextResidual;
            }

            q = 0d;
            return false;
        }

        internal static bool TryGetReachLimits(IReadOnlyList<float> lengths, out float minimum, out float maximum)
        {
            minimum = 0f;
            maximum = 0f;
            if (!TryGetLengthBounds(lengths, out double lower, out double total))
                return false;

            // For a semicircle, the half-angles sum to PI/2 and its diameter is the wrist distance.
            double upper = total;
            for (int i = 0; i < 40 && upper > lower; i++)
            {
                double diameter = (lower + upper) * 0.5d;
                if (SumHalfAngles(lengths, diameter, 1d) > Math.PI / 2d)
                    lower = diameter;
                else
                    upper = diameter;
            }

            minimum = (float)upper;
            maximum = (float)total;
            return true;
        }

        private static bool TryGetLengthBounds(IReadOnlyList<float> lengths, out double longest, out double total)
        {
            longest = 0d;
            total = 0d;
            if (lengths == null || lengths.Count == 0)
                return false;

            for (int i = 0; i < lengths.Count; i++)
            {
                float length = lengths[i];
                if (!IsFinite(length) || length <= 0f)
                    return false;

                longest = Math.Max(longest, length);
                total += length;
            }
            return true;
        }

        private static double SumHalfAngles(IReadOnlyList<float> lengths, double distance, double q)
        {
            double sum = 0d;
            for (int i = 0; i < lengths.Count; i++)
                sum += GetHalfAngle(lengths[i], distance, q);
            return sum;
        }

        private static double GetHalfAngle(float length, double distance, double q)
        {
            return Math.Asin(length / distance * q);
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
