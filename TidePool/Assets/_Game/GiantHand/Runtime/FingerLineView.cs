using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(LineRenderer))]
public class FingerLineView : MonoBehaviour
{
    [Header("Palm connection")]
    public Transform wrist;

    [Header("Finger joints")]
    public Transform joint1;
    public Transform joint2;
    public Transform joint3;
    public Transform tip;

    [Header("Appearance")]
    public Material lineMaterial;
    [Min(0.001f)] public float lineWidth = 0.05f;

    public LineRenderer line;
    private readonly Vector3[] points=new Vector3[5];


    private void OnEnable()
    {
        RefreshLine();
    }

    private void LateUpdate()
    {
        RefreshLine();
    }

    private void RefreshLine()
    {
        if (line == null)
            line = GetComponent<LineRenderer>();

        if (line == null)
            return;

        bool ready =
            wrist != null &&
            joint1 != null &&
            joint2 != null &&
            joint3 != null &&
            tip != null &&
            lineMaterial != null;

        line.enabled = ready;

        if (!ready)
            return;

        points[0] = wrist.position;
        points[1] = joint1.position;
        points[2] = joint2.position;
        points[3] = joint3.position;
        points[4] = tip.position;

        line.useWorldSpace = true;
        line.loop = false;
        line.positionCount = points.Length;

        line.sharedMaterial = lineMaterial;
        line.startColor = Color.white;
        line.endColor = Color.white;
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;

        line.numCapVertices = 4;
        line.numCornerVertices = 4;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;

        line.SetPositions(points);
    }
}