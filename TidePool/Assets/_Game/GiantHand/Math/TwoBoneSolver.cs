using UnityEngine;

public static class TwoBoneSolver
{
    public static bool TrySolve(
        Vector3 root,
        Vector3 target,
        Vector3 pole,
        float length1,
        float length2,
        out Vector3 solvedElbow,
        out string message
    )
    {
        solvedElbow = root;
        message = "";

        const float epsilon = 0.00001f;

        if (length1 <= epsilon || length2 <= epsilon)
        {
            message = "Invalid bone length";
            return false;
        }

        Vector3 rootToTarget = target - root;
        float distance = rootToTarget.magnitude;

        if (distance <= epsilon)
        {
            message = "Target is too close to root";
            return false;
        }

        float minReach = Mathf.Abs(length1 - length2);
        float maxReach = length1 + length2;

        if (distance < minReach || distance > maxReach)
        {
            message = "Target is unreachable";
            return false;
        }

        Vector3 direction = rootToTarget / distance;

        Vector3 poleOffset = pole - root;

        Vector3 perpendicular =
            poleOffset
            - Vector3.Dot(poleOffset, direction) * direction;

        if (perpendicular.magnitude <= epsilon)
        {
            message = "Pole is collinear";
            return false;
        }

        Vector3 bendDirection = perpendicular.normalized;

        float a =
            (length1 * length1 + distance * distance - length2 * length2) / (2f * distance);

        float hSquared = length1 * length1 - a * a;
        float h = Mathf.Sqrt(Mathf.Max(0f, hSquared));

        solvedElbow = root + direction * a + bendDirection * h;

        message = "Success";
        return true;
    }
}