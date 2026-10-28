using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class GiantHandDebugView : MonoBehaviour
{
    [Header("actual sksleton")]
    public Transform shoulder;
    public Transform elbow;
    public Transform wrist;

    [Header("Control target")]
    public Transform wristTarget;
    public Transform elbowPole;

    [Header("Assistive Display")]
    public bool showDebug = true;

    [Min(0.01f)]
    public float markerRadius = 0.3f;

    private void OnDrawGizmos()
    {
        if (!showDebug)
            return;

        Color previousColor = Gizmos.color;

        // 标记实际关节和控制点。
        DrawMarker(shoulder, Color.yellow, "Shoulder");
        DrawMarker(elbow, Color.green, "Elbow");
        DrawMarker(wrist, Color.white, "Wrist");
        DrawMarker(wristTarget, Color.cyan, "Target");
        DrawMarker(elbowPole, Color.magenta, "Pole");

        // 绿色：当前实际骨架。
        DrawSegment(shoulder, elbow, Color.green);
        DrawSegment(elbow, wrist, Color.green);

        // 紫色：肩部到 Pole 的方向参考。
        DrawSegment(shoulder, elbowPole, Color.magenta);

        // 红色：当前手腕与目标之间的距离。
        if (wrist != null && wristTarget != null)
        {
            DrawSegment(wrist, wristTarget, Color.red);

            float distance = Vector3.Distance(
                wrist.position,
                wristTarget.position
            );

#if UNITY_EDITOR
            Vector3 labelPosition =
                (wrist.position + wristTarget.position) * 0.5f
                + Vector3.up * 0.5f;

            Handles.Label(
                labelPosition,
                $"Wrist -> Target: {distance:F2} m"
            );
#endif
        }

        DrawSolvedPreview();

        Gizmos.color = previousColor;
    }

    private void DrawSolvedPreview()
    {
        if (shoulder == null ||
            elbow == null ||
            wrist == null ||
            wristTarget == null ||
            elbowPole == null)
        {
            return;
        }

        // 读取当前真实骨架的两段长度。
        float length1 = Vector3.Distance(
            shoulder.position,
            elbow.position
        );

        float length2 = Vector3.Distance(
            elbow.position,
            wrist.position
        );

        bool success = TwoBoneSolver.TrySolve(
            shoulder.position,
            wristTarget.position,
            elbowPole.position,
            length1,
            length2,
            out Vector3 solvedElbow,
            out string message
        );

        if (!success)
        {
#if UNITY_EDITOR
            Handles.Label(
                wristTarget.position + Vector3.up,
                $"IK Preview: {message}"
            );
#endif
            return;
        }

        // 橙色：算法求出的骨链。
        Gizmos.color = new Color(1f, 0.55f, 0.1f);

        Gizmos.DrawLine(shoulder.position, solvedElbow);
        Gizmos.DrawLine(solvedElbow, wristTarget.position);

        Gizmos.DrawWireSphere(solvedElbow, markerRadius);
        Gizmos.DrawWireSphere(wristTarget.position, markerRadius);

#if UNITY_EDITOR
        Handles.Label(
            solvedElbow + Vector3.up * 0.8f,
            "Solved Elbow"
        );

        Handles.Label(
            shoulder.position + Vector3.up * 1.2f,
            $"IK Preview OK | L1: {length1:F2}, L2: {length2:F2}"
        );
#endif
    }

    private void DrawMarker(
        Transform point,
        Color color,
        string label
    )
    {
        if (point == null)
            return;

        Gizmos.color = color;
        Gizmos.DrawWireSphere(point.position, markerRadius);

#if UNITY_EDITOR
        Handles.Label(
            point.position + Vector3.up * 0.45f,
            label
        );
#endif
    }

    private void DrawSegment(
        Transform from,
        Transform to,
        Color color
    )
    {
        if (from == null || to == null)
            return;

        Gizmos.color = color;
        Gizmos.DrawLine(from.position, to.position);
    }
}