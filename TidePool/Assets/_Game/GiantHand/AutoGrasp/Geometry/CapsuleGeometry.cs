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
