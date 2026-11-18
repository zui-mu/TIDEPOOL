using System;
using System.Collections.Generic;
using UnityEngine;

namespace TidePool.GiantHand.AutoGrasp
{
    /// <summary>Checks visible hand geometry against the floor and the player capsule using candidate FK.</summary>
    internal sealed class HandClearance
    {
        private readonly Pose[] anchorInWrist;
        private readonly Pose[] ownerWorld;
        private readonly CachedMesh[] meshes;

        private sealed class CachedMesh
        {
            internal readonly int Owner;
            internal readonly Vector3[] Vertices;
            internal readonly int[] Triangles;
            internal readonly Vector3 Minimum;
            internal readonly Vector3 Maximum;

            internal CachedMesh(int owner, Vector3[] vertices, int[] triangles)
            {
                Owner = owner;
                Vertices = vertices;
                Triangles = triangles;
                Vector3 minimum = vertices[0];
                Vector3 maximum = vertices[0];
                for (int i = 0; i < vertices.Length; i++)
                {
                    if (!CapsuleGeometry.IsFinite(vertices[i]))
                        throw new ArgumentException("Visible hand mesh vertices must be finite.");
                    minimum = Vector3.Min(minimum, vertices[i]);
                    maximum = Vector3.Max(maximum, vertices[i]);
                }
                Minimum = minimum;
                Maximum = maximum;
            }
        }

        internal HandClearance(Transform wrist, FingerLineView[] fingers)
        {
            if (!wrist) throw new ArgumentNullException(nameof(wrist));
            if (fingers == null) throw new ArgumentNullException(nameof(fingers));

            anchorInWrist = new Pose[fingers.Length];
            ownerWorld = new Pose[1 + fingers.Length * 3];
            var owners = new Transform[ownerWorld.Length];
            owners[0] = wrist;
            var ownerIndices = new Dictionary<Transform, int> { { wrist, 0 } };
            Quaternion inverseWrist = Quaternion.Inverse(wrist.rotation);
            for (int i = 0; i < fingers.Length; i++)
            {
                FingerLineView finger = fingers[i];
                if (!finger || !finger.joint1 || !finger.joint1.parent || !finger.joint1.IsChildOf(wrist)
                    || !finger.joint2 || finger.joint2.parent != finger.joint1
                    || !finger.joint3 || finger.joint3.parent != finger.joint2)
                    throw new ArgumentException("Clearance requires continuous finger chains under the wrist.", nameof(fingers));

                Transform anchor = finger.joint1.parent;
                anchorInWrist[i] = new Pose(inverseWrist * (anchor.position - wrist.position),
                    inverseWrist * anchor.rotation);
                int firstOwner = 1 + i * 3;
                owners[firstOwner] = finger.joint1;
                owners[firstOwner + 1] = finger.joint2;
                owners[firstOwner + 2] = finger.joint3;
                for (int j = 0; j < 3; j++) ownerIndices.Add(owners[firstOwner + j], firstOwner + j);
            }

            var cached = new List<CachedMesh>();
            MeshFilter[] filters = wrist.GetComponentsInChildren<MeshFilter>(false);
            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter filter = filters[i];
                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                Mesh mesh = filter.sharedMesh;
                if (!filter.gameObject.activeInHierarchy || !renderer || !renderer.enabled
                    || renderer.forceRenderingOff || !mesh)
                    continue;

                Transform ancestor = filter.transform;
                while (!ownerIndices.ContainsKey(ancestor)) ancestor = ancestor.parent;
                int ownerIndex = ownerIndices[ancestor];
                Transform owner = owners[ownerIndex];
                // Retain mesh/decoration scale in the cached geometry; candidates use rigid owner poses.
                Matrix4x4 meshToOwner = Matrix4x4.TRS(owner.position, owner.rotation, Vector3.one).inverse
                    * filter.transform.localToWorldMatrix;
                Vector3[] vertices = mesh.isReadable ? mesh.vertices : Array.Empty<Vector3>();
                int[] triangles = vertices.Length > 0 ? mesh.triangles : Array.Empty<int>();
                if (vertices.Length == 0) vertices = BoundsCorners(mesh.bounds);
                for (int j = 0; j < vertices.Length; j++) vertices[j] = meshToOwner.MultiplyPoint3x4(vertices[j]);
                cached.Add(new CachedMesh(ownerIndex, vertices, triangles));
            }
            meshes = cached.ToArray();
        }

        internal bool IsClear(Pose wristPose, FingerCcdSolver[] solvers, float floorY, float margin,
            CapsuleSnapshot? capsule = null, float contactSlack = 0f)
        {
            if (solvers == null || solvers.Length != anchorInWrist.Length || !ValidPose(wristPose)
                || !CapsuleGeometry.IsFinite(floorY) || !CapsuleGeometry.IsFinite(margin) || margin < 0f)
                return false;

            wristPose.rotation = UnitRotation(wristPose.rotation);
            ownerWorld[0] = wristPose;
            for (int i = 0; i < solvers.Length; i++)
            {
                if (solvers[i] == null) return false;
                Pose[] locals = solvers[i].LocalPoses;
                if (locals == null || locals.Length != 3) return false;
                Pose parent = Compose(wristPose, anchorInWrist[i]);
                for (int j = 0; j < 3; j++)
                {
                    if (!ValidPose(locals[j])) return false;
                    parent = Compose(parent, locals[j]);
                    if (!ValidPose(parent)) return false;
                    ownerWorld[1 + i * 3 + j] = parent;
                }
            }

            Vector3 capsuleStart = Vector3.zero;
            Vector3 capsuleEnd = Vector3.zero;
            float capsuleRadius = 0f;
            if (capsule is CapsuleSnapshot body)
            {
                if (!CapsuleGeometry.IsValid(body) || !CapsuleGeometry.IsFinite(contactSlack) || contactSlack < 0f)
                    return false;
                Vector3 halfAxis = body.axis * CapsuleGeometry.AxisHalfLength(body);
                capsuleStart = body.center - halfAxis;
                capsuleEnd = body.center + halfAxis;
                capsuleRadius = Mathf.Max(0f, body.radius - contactSlack);
            }

            for (int i = 0; i < meshes.Length; i++)
            {
                CachedMesh mesh = meshes[i];
                Pose owner = ownerWorld[mesh.Owner];
                if (!MeshAboveFloor(mesh, owner, (double)floorY + margin) ||
                    (capsuleRadius > 0f && !MeshOutsideCapsule(mesh, owner, capsuleStart, capsuleEnd, capsuleRadius)))
                    return false;
            }
            return true;
        }

        internal bool FingertipTouches(int digit, Pose distalJoint, CapsuleSnapshot capsule, float tolerance)
        {
            int distalOwner = 3 + 3 * digit;
            Vector3 halfAxis = capsule.axis * CapsuleGeometry.AxisHalfLength(capsule);
            Vector3 start = capsule.center - halfAxis;
            Vector3 end = capsule.center + halfAxis;
            foreach (CachedMesh mesh in meshes)
                if (mesh.Owner == distalOwner && !MeshOutsideCapsule(mesh, distalJoint, start, end, capsule.radius + tolerance))
                    return true;
            return false;
        }

        private static bool MeshAboveFloor(CachedMesh mesh, Pose owner, double threshold)
        {
            Quaternion q = owner.rotation;
            double x = 2d * ((double)q.x * q.y + (double)q.w * q.z);
            double y = 1d - 2d * ((double)q.x * q.x + (double)q.z * q.z);
            double z = 2d * ((double)q.y * q.z - (double)q.w * q.x);
            // The rotated owner-local box encloses every cached vertex.
            Vector3 minimum = mesh.Minimum;
            Vector3 maximum = mesh.Maximum;
            double lowest = owner.position.y + x * (x >= 0d ? minimum.x : maximum.x)
                + y * (y >= 0d ? minimum.y : maximum.y) + z * (z >= 0d ? minimum.z : maximum.z);
            if (lowest >= threshold) return true;
            double highest = owner.position.y + x * (x >= 0d ? maximum.x : minimum.x)
                + y * (y >= 0d ? maximum.y : minimum.y) + z * (z >= 0d ? maximum.z : minimum.z);
            if (highest < threshold) return false;
            foreach (Vector3 v in mesh.Vertices)
                if (owner.position.y + x * v.x + y * v.y + z * v.z < threshold) return false;
            return true;
        }

        private static bool MeshOutsideCapsule(CachedMesh mesh, Pose owner, Vector3 start, Vector3 end, float radius)
        {
            Quaternion inverse = Quaternion.Inverse(owner.rotation);
            start = inverse * (start - owner.position);
            end = inverse * (end - owner.position);
            Vector3 padding = radius * Vector3.one;
            Vector3 min = Vector3.Min(start, end) - padding;
            Vector3 max = Vector3.Max(start, end) + padding;
            if (max.x < mesh.Minimum.x || max.y < mesh.Minimum.y || max.z < mesh.Minimum.z ||
                min.x > mesh.Maximum.x || min.y > mesh.Maximum.y || min.z > mesh.Maximum.z)
                return true;

            Vector3 axis = end - start;
            float axisLengthSquared = axis.sqrMagnitude;
            float inverseLengthSquared = axisLengthSquared > 1e-8f ? 1f / axisLengthSquared : 0f;
            float radiusSquared = radius * radius;
            foreach (Vector3 vertex in mesh.Vertices)
            {
                Vector3 offset = vertex - start;
                Vector3 closest = offset - Mathf.Clamp01(Vector3.Dot(offset, axis) * inverseLengthSquared) * axis;
                if (closest.sqrMagnitude < radiusSquared) return false;
            }

            int[] triangles = mesh.Triangles;
            Vector3[] vertices = mesh.Vertices;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = vertices[triangles[i]];
                Vector3 b = vertices[triangles[i + 1]];
                Vector3 c = vertices[triangles[i + 2]];
                Vector3 triangleMin = Vector3.Min(a, Vector3.Min(b, c));
                Vector3 triangleMax = Vector3.Max(a, Vector3.Max(b, c));
                if (max.x < triangleMin.x || max.y < triangleMin.y || max.z < triangleMin.z ||
                    min.x > triangleMax.x || min.y > triangleMax.y || min.z > triangleMax.z)
                    continue;
                if (CapsuleGeometry.SegmentWithinTriangleDistance(start, end, a, b, c, radiusSquared))
                    return false;
            }
            return true;
        }

        private static Pose Compose(Pose parent, Pose local)
        {
            return new Pose(parent.position + parent.rotation * local.position,
                UnitRotation(parent.rotation * local.rotation));
        }

        private static bool ValidPose(Pose pose)
        {
            Quaternion q = pose.rotation;
            return CapsuleGeometry.IsFinite(pose.position) && CapsuleGeometry.IsFinite(q.x)
                && CapsuleGeometry.IsFinite(q.y) && CapsuleGeometry.IsFinite(q.z) && CapsuleGeometry.IsFinite(q.w)
                && (double)q.x * q.x + (double)q.y * q.y + (double)q.z * q.z + (double)q.w * q.w > 1e-20;
        }

        private static Quaternion UnitRotation(Quaternion q)
        {
            double inverse = 1d / Math.Sqrt((double)q.x * q.x + (double)q.y * q.y + (double)q.z * q.z + (double)q.w * q.w);
            return new Quaternion((float)(q.x * inverse), (float)(q.y * inverse), (float)(q.z * inverse), (float)(q.w * inverse));
        }

        private static Vector3[] BoundsCorners(Bounds bounds)
        {
            var corners = new Vector3[8];
            Vector3 minimum = bounds.min;
            Vector3 maximum = bounds.max;
            for (int i = 0; i < corners.Length; i++)
                corners[i] = new Vector3((i & 1) == 0 ? minimum.x : maximum.x,
                    (i & 2) == 0 ? minimum.y : maximum.y, (i & 4) == 0 ? minimum.z : maximum.z);
            return corners;
        }
    }
}
