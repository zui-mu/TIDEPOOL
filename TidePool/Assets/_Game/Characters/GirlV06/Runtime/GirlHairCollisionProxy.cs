using System;
using UnityEngine;

// Data-only capsule: never registers a Collider or Rigidbody with gameplay physics.
[Serializable]
public sealed class GirlHairCollisionProxy
{
    public string label = "Body";
    public Transform anchor;
    public Vector3 localStart, localEnd;
    [Min(.001f)] public float radius = .05f;
    public bool enabled = true;

    public bool GetWorldCapsule(out Vector3 start, out Vector3 end, out float worldRadius)
    {
        start = end = Vector3.zero; worldRadius = 0f;
        if (!enabled || !anchor) return false;
        start = anchor.TransformPoint(localStart); end = anchor.TransformPoint(localEnd);
        Vector3 scale = anchor.lossyScale;
        worldRadius = radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
        return worldRadius > 0f;
    }
    public float SignedDistanceToSegment(Vector3 start, Vector3 end)
    {
        if (!GetWorldCapsule(out var a, out var b, out float r)) return float.PositiveInfinity;
        ClosestSegments(start, end, a, b, out float s, out float t);
        return Vector3.Distance(Vector3.Lerp(start, end, s), Vector3.Lerp(a, b, t)) - r;
    }
    public bool ProjectSegment(ref Vector3 start, ref Vector3 end, float hairRadius, bool pinStart, Vector3 fallbackNormal)
    {
        if (!GetWorldCapsule(out var a, out var b, out float r)) return false;
        float limit = r + Mathf.Max(0f, hairRadius);
        bool changed = false;
        if (!pinStart) changed |= ProjectPoint(ref start, a, b, limit, fallbackNormal);
        changed |= ProjectPoint(ref end, a, b, limit, fallbackNormal);
        ClosestSegments(start, end, a, b, out float s, out float t);
        Vector3 difference = Vector3.Lerp(start, end, s) - Vector3.Lerp(a, b, t);
        float distance = difference.magnitude;
        if (distance >= limit) return changed;
        float wa = pinStart ? 0f : 1f;
        float denominator = wa * (1f - s) * (1f - s) + s * s;
        // Contacts at an immovable root cannot be solved by translating that root.
        if (denominator < .01f) return changed;
        Vector3 normal = distance > .000001f ? difference / distance : Fallback(fallbackNormal, b - a);
        Vector3 correction = normal * (limit - distance + .0001f);
        start += Vector3.ClampMagnitude(correction * (wa * (1f - s) / denominator), .12f);
        end += Vector3.ClampMagnitude(correction * (s / denominator), .12f);
        return true;
    }
    static bool ProjectPoint(ref Vector3 point, Vector3 a, Vector3 b, float limit, Vector3 fallback)
    {
        Vector3 axis = b - a;
        float t = axis.sqrMagnitude > .00000001f ? Mathf.Clamp01(Vector3.Dot(point - a, axis) / axis.sqrMagnitude) : 0f;
        Vector3 center = Vector3.Lerp(a, b, t), delta = point - center;
        float distance = delta.magnitude;
        if (distance >= limit) return false;
        point = center + (distance > .000001f ? delta / distance : Fallback(fallback, axis)) * (limit + .0001f);
        return true;
    }
    static Vector3 Fallback(Vector3 preferred, Vector3 axis)
    {
        if (axis.sqrMagnitude > .00000001f) preferred = Vector3.ProjectOnPlane(preferred, axis);
        if (preferred.sqrMagnitude < .00000001f)
        {
            preferred = Vector3.Cross(axis, Vector3.forward);
            if (preferred.sqrMagnitude < .00000001f) preferred = Vector3.right;
        }
        return preferred.normalized;
    }
    static void ClosestSegments(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2, out float s, out float t)
    {
        Vector3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
        float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r);
        const float epsilon = .00000001f;
        if (a <= epsilon && e <= epsilon) { s = t = 0f; return; }
        if (a <= epsilon) { s = 0f; t = Mathf.Clamp01(f / e); return; }
        float c = Vector3.Dot(d1, r);
        if (e <= epsilon) { t = 0f; s = Mathf.Clamp01(-c / a); return; }
        float b = Vector3.Dot(d1, d2), denominator = a * e - b * b;
        s = denominator > epsilon ? Mathf.Clamp01((b * f - c * e) / denominator) : 0f;
        t = (b * s + f) / e;
        if (t < 0f) { t = 0f; s = Mathf.Clamp01(-c / a); }
        else if (t > 1f) { t = 1f; s = Mathf.Clamp01((b - c) / a); }
    }
}
