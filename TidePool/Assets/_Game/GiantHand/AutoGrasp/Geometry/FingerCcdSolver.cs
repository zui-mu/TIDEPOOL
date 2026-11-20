using System;
using UnityEngine;

namespace TidePool.GiantHand.AutoGrasp
{
    /// <summary>Four limited angles on three fixed finger offsets. Angles are in degrees.</summary>
    internal sealed class FingerCcdSolver
    {
        private readonly Pose[] openJoints;
        private readonly Vector3 tipOffset;
        private readonly Vector3 spreadAxisInParent;
        private readonly Vector2[] limits = new Vector2[4];
        private readonly float[] angles = new float[4];
        private readonly float[] bestAngles = new float[4];
        private readonly float[] initialAngles = new float[4];
        private readonly Pose[] worldJoints = new Pose[3];
        private Pose lastParent = new Pose(Vector3.zero, Quaternion.identity);

        internal Pose[] LocalPoses { get; } = new Pose[3];
        internal Vector3 TipWorld { get; private set; }

        internal FingerCcdSolver(Pose[] openJoints, Vector3 tipOffset,
            Vector3 spreadAxisInParent, Vector2 spreadLimits, Vector2[] flexLimits)
        {
            if (openJoints == null || openJoints.Length != 3)
                throw new ArgumentException("A finger requires exactly three open joint poses.", nameof(openJoints));
            if (flexLimits == null || flexLimits.Length != 3)
                throw new ArgumentException("A finger requires exactly three flexion limits.", nameof(flexLimits));
            if (!CapsuleGeometry.IsFinite(tipOffset))
                throw new ArgumentException("The fingertip offset must be finite.", nameof(tipOffset));
            double spreadLengthSquared = Dot(spreadAxisInParent, spreadAxisInParent);
            if (!CapsuleGeometry.IsFinite(spreadAxisInParent) || spreadLengthSquared <= 0d)
                throw new ArgumentException("The spread axis must have a finite nonzero length.", nameof(spreadAxisInParent));

            this.openJoints = (Pose[])openJoints.Clone();
            this.tipOffset = tipOffset;
            double inverseSpreadLength = 1d / Math.Sqrt(spreadLengthSquared);
            this.spreadAxisInParent = new Vector3((float)(spreadAxisInParent.x * inverseSpreadLength),
                (float)(spreadAxisInParent.y * inverseSpreadLength), (float)(spreadAxisInParent.z * inverseSpreadLength));
            limits[0] = spreadLimits;
            for (int i = 0; i < 3; i++)
            {
                if (!CapsuleGeometry.IsFinite(openJoints[i].position) || !ValidRotation(openJoints[i].rotation))
                    throw new ArgumentException("Open joint poses must be finite with valid rotations.", nameof(openJoints));
                this.openJoints[i].rotation = UnitRotation(openJoints[i].rotation);
                limits[i + 1] = flexLimits[i];
            }
            for (int i = 0; i < limits.Length; i++)
            {
                if (!CapsuleGeometry.IsFinite(limits[i].x) || !CapsuleGeometry.IsFinite(limits[i].y)
                    || limits[i].x > limits[i].y)
                    throw new ArgumentException("Joint limits must be finite ordered degree ranges.");
            }
            Reset();
        }

        internal void Reset()
        {
            for (int i = 0; i < angles.Length; i++)
                angles[i] = Mathf.Clamp(0f, limits[i].x, limits[i].y);
            UpdateFk(lastParent);
        }

        internal void CopyAnglesTo(float[] destination)
        {
            if (destination == null || destination.Length < 4)
                throw new ArgumentException("Four angle values are required.", nameof(destination));
            Array.Copy(angles, destination, 4);
        }

        internal void SetAngles(float[] source)
        {
            if (source == null || source.Length < 4)
                throw new ArgumentException("Four angle values are required.", nameof(source));
            for (int i = 0; i < 4; i++)
            {
                if (!CapsuleGeometry.IsFinite(source[i]))
                    throw new ArgumentException("Angle values must be finite.", nameof(source));
            }
            for (int i = 0; i < 4; i++)
                angles[i] = Mathf.Clamp(source[i], limits[i].x, limits[i].y);
            UpdateFk(lastParent);
        }

        internal bool IsClear(Pose parentWorld, CapsuleSnapshot capsule, float boneRadius)
        {
            if (!ValidParent(parentWorld)) return false;
            UpdateFk(parentWorld);
            if (!CapsuleGeometry.IsValid(capsule) || !CapsuleGeometry.IsFinite(boneRadius) || boneRadius < 0f)
                return false;

            Vector3 halfAxis = capsule.axis.normalized * CapsuleGeometry.AxisHalfLength(capsule);
            Vector3 axisStart = capsule.center - halfAxis;
            Vector3 axisEnd = capsule.center + halfAxis;
            double radius = (double)capsule.radius + boneRadius;
            // This margin only covers float FK/distance rounding, independent of IK tolerance.
            double clearance = Math.Max(0d, radius - 0.000002d * Math.Max(1d, radius));
            double minimumSquared = clearance * clearance;
            for (int i = 0; i < worldJoints.Length; i++)
            {
                Vector3 start = worldJoints[i].position;
                Vector3 end = i == 2 ? TipWorld : worldJoints[i + 1].position;
                if (!CapsuleGeometry.IsFinite(start) || !CapsuleGeometry.IsFinite(end)) return false;
                float distanceSquared = CapsuleGeometry.SegmentDistanceSquared(start, end, axisStart, axisEnd);
                if (!CapsuleGeometry.IsFinite(distanceSquared) || distanceSquared < minimumSquared) return false;
            }
            return true;
        }

        // Both FK chains must be evaluated in the candidate pose first.
        internal bool HasClearanceFrom(FingerCcdSolver other, float separation)
        {
            float minimumSquared = separation * separation;
            for (int i = 0; i < worldJoints.Length; i++)
            {
                Vector3 end = i + 1 < worldJoints.Length ? worldJoints[i + 1].position : TipWorld;
                for (int j = 0; j < other.worldJoints.Length; j++)
                {
                    Vector3 otherEnd = j + 1 < other.worldJoints.Length ? other.worldJoints[j + 1].position : other.TipWorld;
                    if (CapsuleGeometry.SegmentDistanceSquared(worldJoints[i].position, end,
                            other.worldJoints[j].position, otherEnd) < minimumSquared)
                        return false;
                }
            }
            return true;
        }

        internal bool Solve(Pose parentWorld, Vector3 target, CapsuleSnapshot capsule,
            float boneRadius, float tolerance, int maxIterations)
        {
            if (!ValidParent(parentWorld) || !CapsuleGeometry.IsFinite(target)
                || !CapsuleGeometry.IsFinite(tolerance) || tolerance <= 0f || maxIterations < 1
                || !CapsuleGeometry.IsValid(capsule) || !CapsuleGeometry.IsFinite(boneRadius) || boneRadius < 0f)
                return false;

            Array.Copy(angles, initialAngles, 4);
            if (!IsClear(parentWorld, capsule, boneRadius))
            {
                Reset();
                if (!IsClear(parentWorld, capsule, boneRadius))
                {
                    Array.Copy(initialAngles, angles, 4);
                    UpdateFk(parentWorld);
                    return false;
                }
            }

            double bestError = ErrorSquared(TipWorld, target);
            double allowedError = (double)tolerance * tolerance;
            Array.Copy(angles, bestAngles, 4);
            for (int iteration = 0; iteration < maxIterations && bestError > allowedError; iteration++)
            {
                double previousError = bestError;
                // Flex the distal joint first, then move toward the root and finally spread.
                for (int coordinate = 3; coordinate >= 0; coordinate--)
                {
                    TryCoordinate(coordinate, parentWorld, target, capsule, boneRadius);
                    double error = ErrorSquared(TipWorld, target);
                    if (error < bestError)
                    {
                        bestError = error;
                        Array.Copy(angles, bestAngles, 4);
                    }
                    if (bestError <= allowedError) break;
                }
                if (bestError >= previousError) break;
            }
            Array.Copy(bestAngles, angles, 4);
            UpdateFk(parentWorld);
            return bestError <= allowedError && IsClear(parentWorld, capsule, boneRadius);
        }

        private void TryCoordinate(int coordinate, Pose parentWorld, Vector3 target,
            CapsuleSnapshot capsule, float boneRadius)
        {
            int joint = Math.Max(0, coordinate - 1);
            Vector3 pivot = worldJoints[joint].position;
            Vector3 axis = coordinate == 0
                ? lastParent.rotation * spreadAxisInParent
                : worldJoints[joint].rotation * Vector3.forward;
            axis.Normalize();
            Vector3 toTip = TipWorld - pivot;
            Vector3 toTarget = target - pivot;
            double tipAlongAxis = Dot(toTip, axis);
            double targetAlongAxis = Dot(toTarget, axis);
            double cosine = Dot(toTip, toTarget) - tipAlongAxis * targetAlongAxis;
            double sine = axis.x * ((double)toTip.y * toTarget.z - (double)toTip.z * toTarget.y)
                + axis.y * ((double)toTip.z * toTarget.x - (double)toTip.x * toTarget.z)
                + axis.z * ((double)toTip.x * toTarget.y - (double)toTip.y * toTarget.x);
            if (Math.Abs(cosine) + Math.Abs(sine) < 1e-16) return;

            float oldAngle = angles[coordinate];
            float delta = (float)(Math.Atan2(sine, cosine) * (180d / Math.PI));
            float nextAngle = Mathf.Clamp(oldAngle + delta, limits[coordinate].x, limits[coordinate].y);
            float step = nextAngle - oldAngle;
            double oldError = ErrorSquared(TipWorld, target);
            for (int attempt = 0; attempt < 16 && Math.Abs(step) > 0.000001f; attempt++)
            {
                angles[coordinate] = oldAngle + step;
                if (IsClear(parentWorld, capsule, boneRadius) && ErrorSquared(TipWorld, target) <= oldError)
                    return;
                step *= 0.5f;
            }
            angles[coordinate] = oldAngle;
            UpdateFk(parentWorld);
        }

        private void UpdateFk(Pose parentWorld)
        {
            parentWorld.rotation = UnitRotation(parentWorld.rotation);
            lastParent = parentWorld;
            Pose parent = parentWorld;
            for (int i = 0; i < 3; i++)
            {
                Quaternion rotation = openJoints[i].rotation * AxisRotation(Vector3.forward, angles[i + 1]);
                if (i == 0) rotation = AxisRotation(spreadAxisInParent, angles[0]) * rotation;
                LocalPoses[i] = new Pose(openJoints[i].position, UnitRotation(rotation));
                worldJoints[i] = new Pose(parent.position + parent.rotation * LocalPoses[i].position,
                    UnitRotation(parent.rotation * LocalPoses[i].rotation));
                parent = worldJoints[i];
            }
            TipWorld = parent.position + parent.rotation * tipOffset;
        }

        private static Quaternion AxisRotation(Vector3 axis, float degrees)
        {
            double halfAngle = degrees * (Math.PI / 360d);
            float sine = (float)Math.Sin(halfAngle);
            return new Quaternion(axis.x * sine, axis.y * sine, axis.z * sine, (float)Math.Cos(halfAngle));
        }

        private static bool ValidParent(Pose parent)
        {
            return CapsuleGeometry.IsFinite(parent.position) && ValidRotation(parent.rotation);
        }

        private static bool ValidRotation(Quaternion rotation)
        {
            return CapsuleGeometry.IsFinite(rotation.x) && CapsuleGeometry.IsFinite(rotation.y)
                && CapsuleGeometry.IsFinite(rotation.z) && CapsuleGeometry.IsFinite(rotation.w)
                && (double)rotation.x * rotation.x + (double)rotation.y * rotation.y
                    + (double)rotation.z * rotation.z + (double)rotation.w * rotation.w > 1e-20;
        }

        private static Quaternion UnitRotation(Quaternion value)
        {
            double inverseLength = 1d / Math.Sqrt((double)value.x * value.x + (double)value.y * value.y
                + (double)value.z * value.z + (double)value.w * value.w);
            return new Quaternion((float)(value.x * inverseLength), (float)(value.y * inverseLength),
                (float)(value.z * inverseLength), (float)(value.w * inverseLength));
        }

        private static double Dot(Vector3 a, Vector3 b)
        {
            return (double)a.x * b.x + (double)a.y * b.y + (double)a.z * b.z;
        }

        private static double ErrorSquared(Vector3 a, Vector3 b)
        {
            double x = (double)a.x - b.x, y = (double)a.y - b.y, z = (double)a.z - b.z;
            return x * x + y * y + z * z;
        }
    }
}
