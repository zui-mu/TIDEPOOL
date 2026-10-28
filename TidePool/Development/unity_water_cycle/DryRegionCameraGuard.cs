using UnityEngine;
using AbstractOcclusion.WebGpuWater;

// CameraFollow runs first. Keep the viewing point in the dry air when the player enters it.
[DefaultExecutionOrder(1000)]
public sealed class DryRegionCameraGuard : MonoBehaviour
{
    public Transform player;
    public WaterExclusionVolume dryRegion;
    [Min(.05f)] public float wallClearance = .18f;
    public bool IsPlayerInside { get; private set; }
    Renderer[] playerRenderers;
    bool[] previousHidden;
    bool hidden;
    void Awake()
    {
        if (!player) return;
        playerRenderers = player.GetComponentsInChildren<Renderer>();
        previousHidden = new bool[playerRenderers.Length];
        for (int i = 0; i < playerRenderers.Length; i++) previousHidden[i] = playerRenderers[i].forceRenderingOff;
    }
    void SetHidden(bool value)
    {
        if (value == hidden || playerRenderers == null) return;
        hidden = value;
        for (int i = 0; i < playerRenderers.Length; i++)
            if (playerRenderers[i]) playerRenderers[i].forceRenderingOff = value || previousHidden[i];
    }
    void OnDisable() { SetHidden(false); }
    void LateUpdate()
    {
        if (!player || !dryRegion) return;
        Vector3 p = dryRegion.transform.InverseTransformPoint(player.position);
        Vector3 h = dryRegion.size * .5f;
        IsPlayerInside = Mathf.Abs(p.x) < h.x && Mathf.Abs(p.y) < h.y && Mathf.Abs(p.z) < h.z;
        SetHidden(IsPlayerInside);
        if (!IsPlayerInside) return;
        // The normal six metre orbit cannot fit in a four metre air pocket. Use an eye-height
        // viewing point here; CameraFollow still supplies the right-mouse yaw and pitch.
        Vector3 c = dryRegion.transform.InverseTransformPoint(player.position + Vector3.up * .65f);
        Vector3 safe = Vector3.Max(Vector3.one * .05f, h - Vector3.one * wallClearance);
        c.x = Mathf.Clamp(c.x, -safe.x, safe.x);
        c.y = Mathf.Clamp(c.y, -safe.y, safe.y);
        c.z = Mathf.Clamp(c.z, -safe.z, safe.z);
        transform.position = dryRegion.transform.TransformPoint(c);
    }
}
