using UnityEngine;

[DefaultExecutionOrder(25)]
[DisallowMultipleComponent]
public class HandGripController : MonoBehaviour
{
    [Header("Whole hand")]
    [Range(0f, 1f)]
    public float grip = 0f;

    [Range(0f, 1f)]
    public float spread = 1f;

    public bool driveFingers = true;
    public bool driveSpread = true;

    [Header("Finger drivers")]
    public FingerGripDriver[] fingers = new FingerGripDriver[0];

    private void LateUpdate()
    {
        if (!driveFingers||fingers==null) return;

        float gripAmount = Mathf.Clamp01(grip);
        float spreadAmount = Mathf.Clamp01(spread);

        foreach(FingerGripDriver finger in fingers)
        {
            if (finger == null) continue;
            finger.grip = gripAmount;
            if(driveSpread) finger.spread = spreadAmount;
        }

    }
}