using UnityEngine;

[DisallowMultipleComponent]
public sealed class GirlAnimationPreview : MonoBehaviour
{
    public enum Motion { Idle, Walk, Run }
    public Animator animator;
    public bool autoCycle = true;
    [Min(1)] public float secondsPerMotion = 4f;
    public Motion motion;
    void Awake() { if (!animator) animator = GetComponentInChildren<Animator>(); }
    void Update()
    {
        if (!animator) return;
        if (autoCycle) motion = (Motion)(Mathf.FloorToInt(Time.time / Mathf.Max(1, secondsPerMotion)) % 3);
        animator.SetInteger("PreviewMotion", (int)motion);
    }
    void OnGUI()
    {
        GUI.Label(new Rect(16,16,650,28), "Girl v06 - Mixamo animation check: " + motion + " (in place)");
    }
}

