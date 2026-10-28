using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(LineRenderer))]
public class GiantHandLineView : MonoBehaviour
{
    [Header("Actual joints")]
    public Transform upperArm;
    public Transform forearm;
    public Transform wrist;

    [Header("Rendering")]
    public LineRenderer palmLine;
    public Material lineMaterial;

    [Min(0.001f)]
    public float lineWidth = 0.06f;

    [Header("Palm dimensions")]
    [Min(0.01f)]
    public float palmLength = 2.6f;

    [Min(0.01f)]
    public float palmWidth = 2.8f;

    private LineRenderer armLine;

    // 数组只创建一次，之后每帧更新里面的坐标。
    private readonly Vector3[] armPoints = new Vector3[3];
    private readonly Vector3[] palmPoints = new Vector3[4];

    private void Awake()
    {
        armLine = GetComponent<LineRenderer>();
    }

    private void LateUpdate()
    {
        if(armLine==null) armLine=GetComponent<LineRenderer>();
        if (armLine == null) return;
        bool ready =
            upperArm != null &&
            forearm != null &&
            wrist != null &&
            lineMaterial != null;

        armLine.enabled = ready;

        if (palmLine != null)
            palmLine.enabled = ready;

        if (!ready)
            return;

        // 实际骨架：上臂根部 → 肘部 → 手腕。
        ConfigureLine(armLine, false);

        armPoints[0] = upperArm.position;
        armPoints[1] = forearm.position;
        armPoints[2] = wrist.position;

        armLine.positionCount = armPoints.Length;
        armLine.SetPositions(armPoints);

        if (palmLine == null)
            return;

        // 手掌在 Wrist 的局部 XZ 平面上。
        // +X 为从手腕向手指延伸的方向。
        float halfWidth = palmWidth * 0.5f;

        palmPoints[0] = wrist.TransformPoint(
            new Vector3(0f, 0f, -halfWidth));

        palmPoints[1] = wrist.TransformPoint(
            new Vector3(palmLength, 0f, -halfWidth));

        palmPoints[2] = wrist.TransformPoint(
            new Vector3(palmLength, 0f, halfWidth));

        palmPoints[3] = wrist.TransformPoint(
            new Vector3(0f, 0f, halfWidth));

        ConfigureLine(palmLine, true);

        palmLine.positionCount = palmPoints.Length;
        palmLine.SetPositions(palmPoints);
    }

    private void ConfigureLine(LineRenderer line, bool loop)
    {
        line.useWorldSpace = true;
        line.loop = loop;
        line.sharedMaterial = lineMaterial;

        line.startWidth = lineWidth;
        line.endWidth = lineWidth;

        line.startColor = Color.white;
        line.endColor = Color.white;

        line.alignment = LineAlignment.View;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;

        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
    }
}