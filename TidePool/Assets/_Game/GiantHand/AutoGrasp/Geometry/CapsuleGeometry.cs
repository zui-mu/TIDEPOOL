using UnityEngine;
namespace TidePool.GiantHand.AutoGrasp
{
    public static class CapsuleGeometry
    {
        public static bool IsFinite(float value)
        {
            return !float.IsNaN(value)&&!float.IsInfinity(value);
        }

        public static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x)&&IsFinite(value.y)&&IsFinite(value.z);
        }

        public static bool IsValid(CapsuleSnapshot capsule)
        {
            if (!IsFinite(capsule.center) || !IsFinite(capsule.axis) || !IsFinite(capsule.radius) ||
                !IsFinite(capsule.height))
            {
                return false;
            }
            if (capsule.radius <= 0.00001f) return false;

            if (capsule.height < capsule.radius * 2f) return false;
            return Mathf.Abs(capsule.axis.sqrMagnitude - 1f ) < 0.001f;
        }

        public static float AxisHalfLength(CapsuleSnapshot capsule)
        {
            return Mathf.Max(0f,capsule.height * 0.5f - capsule.radius);
        }

        internal static Vector3 ClosestAxisPoint(CapsuleSnapshot capsule, Vector3 point)
        {
            float halfAxis = AxisHalfLength(capsule);
            float projection = Vector3.Dot(point - capsule.center, capsule.axis);
            return capsule.center + Mathf.Clamp(projection, -halfAxis, halfAxis) * capsule.axis;
        }

        internal static float SegmentDistanceSquared(Vector3 p0, Vector3 p1, Vector3 q0, Vector3 q1)
        {
            const float pointTolerance = 1e-12f;
            Vector3 p = p1 - p0;
            Vector3 q = q1 - q0;
            Vector3 offset = p0 - q0;
            float pp = Vector3.Dot(p, p);
            float qq = Vector3.Dot(q, q);
            float pq = Vector3.Dot(p, q);
            float po = Vector3.Dot(p, offset);
            float qo = Vector3.Dot(q, offset);
            float s = 0f;
            float t = 0f;

            if (pp <= pointTolerance)
            {
                if (qq > pointTolerance)
                    t = Mathf.Clamp01(qo / qq);
            }
            else if (qq <= pointTolerance)
            {
                s = Mathf.Clamp01(-po / pp);
            }
            else
            {
                Vector3 normal = Vector3.Cross(p, q);
                float determinant = normal.sqrMagnitude;
                if (determinant > 0f)
                    s = Mathf.Clamp01(Vector3.Dot(normal, Vector3.Cross(q, offset)) / determinant);

                t = (pq * s + qo) / qq;
                if (t < 0f || t > 1f)
                {
                    t = Mathf.Clamp01(t);
                    s = Mathf.Clamp01((pq * t - po) / pp);
                }
            }

            return (offset + s * p - t * q).sqrMagnitude;
        }

        internal static bool SegmentWithinTriangleDistance(Vector3 start, Vector3 end, Vector3 a, Vector3 b,
            Vector3 c, float limitSquared)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a);
            float areaSquared = normal.sqrMagnitude;
            if (areaSquared > 1e-15f)
            {
                float startOffset = Vector3.Dot(start - a, normal);
                float endOffset = Vector3.Dot(end - a, normal);
                // Projecting along the normal preserves the side tests for the three edges.
                float planeLimitSquared = limitSquared * areaSquared;
                if ((startOffset * startOffset < planeLimitSquared && IsInsideTriangle(start, a, b, c, normal)) ||
                    (endOffset * endOffset < planeLimitSquared && IsInsideTriangle(end, a, b, c, normal)))
                    return true;
                if ((startOffset < 0f && endOffset > 0f) || (startOffset > 0f && endOffset < 0f))
                {
                    Vector3 intersection = Vector3.LerpUnclamped(start, end, startOffset / (startOffset - endOffset));
                    if (IsInsideTriangle(intersection, a, b, c, normal))
                        return true;
                }
            }

            return SegmentDistanceSquared(start, end, a, b) < limitSquared ||
                SegmentDistanceSquared(start, end, b, c) < limitSquared ||
                SegmentDistanceSquared(start, end, c, a) < limitSquared;
        }

        private static bool IsInsideTriangle(Vector3 point, Vector3 a, Vector3 b, Vector3 c, Vector3 normal) =>
            Vector3.Dot(Vector3.Cross(b - a, point - a), normal) >= 0f &&
            Vector3.Dot(Vector3.Cross(c - b, point - b), normal) >= 0f &&
            Vector3.Dot(Vector3.Cross(a - c, point - c), normal) >= 0f;

        public static bool TrySurfacePoint(
            CapsuleSnapshot capsule,
            float axialHeight,
            Vector3 radialDirection,
            out Vector3 surfacePoint,
            out Vector3 surfaceNormal)
        {
            surfacePoint = Vector3.zero;
            surfaceNormal = Vector3.zero;
            if (!IsValid(capsule) || !IsFinite(axialHeight) || !IsFinite(radialDirection)) return false;
            if (Mathf.Abs(axialHeight) > capsule.height * 0.5f) return false;
            Vector3 radial = Vector3.ProjectOnPlane(radialDirection, capsule.axis);
            if (radial.sqrMagnitude < 0.000001f) return false;
            if (Mathf.Abs(axialHeight) == capsule.height * 0.5f)
            {
                surfaceNormal = Mathf.Sign(axialHeight) * capsule.axis;
                surfacePoint = capsule.center + capsule.axis * axialHeight;
                return true;
            }
            radial.Normalize();
            float halfAxis = AxisHalfLength(capsule);
            float capHeight = Mathf.Max(0f,Mathf.Abs(axialHeight)-halfAxis);
            float sectionRadius = Mathf.Sqrt(Mathf.Max(0f,capsule.radius * capsule.radius - capHeight * capHeight));
            surfacePoint = capsule.center + capsule.axis * axialHeight + radial * sectionRadius;
            Vector3 axisPoint = capsule.center + capsule.axis * Mathf.Clamp(axialHeight,-halfAxis,halfAxis);
            surfaceNormal = (surfacePoint - axisPoint).normalized;
            return true;
        }
    }
}
