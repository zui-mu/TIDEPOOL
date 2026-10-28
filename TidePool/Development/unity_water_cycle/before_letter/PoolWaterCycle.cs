using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;

// This scene owns its water sequence. The imported asset continues to simulate waves.
public sealed class PoolWaterCycle : MonoBehaviour
{
    public enum Phase { Pool, Draining, Empty, Filling, Flooded }
    [Header("References")]
    public Transform player;
    public Transform waterFrame;
    public Transform dryRegion;
    [Min(0.1f)] public float interactionDistance = 2.5f;
    [Header("Water heights (world metres)")]
    public float poolHeight = -0.3f;
    public float emptyHeight = -3.35f;
    public float overflowHeight = 0.05f;
    public float pressureHeight = 2.5f;
    public float floodedHeight = 18.5f;
    [Header("Duration (seconds)")]
    [Min(1)] public float drainSeconds = 24;
    [Min(1)] public float refillSeconds = 22;
    [Min(1)] public float platformFloodSeconds = 36;
    [Min(1)] public float hallFloodSeconds = 70;
    [Header("Prototype controls")]
    public bool allowResetKey = true;
    public bool showHints = true;
    [Header("Hooks for the later letter / weather sequence")]
    public UnityEvent onDrainStarted = new UnityEvent();
    public UnityEvent onPoolEmpty = new UnityEvent();
    public UnityEvent onFloodStarted = new UnityEvent();
    public UnityEvent onHallFlooded = new UnityEvent();
    public Phase CurrentPhase { get; private set; }
    public float Elapsed { get; private set; }
    public float WaterHeight => waterFrame ? waterFrame.position.y : poolHeight;
    Vector3 spawnPosition;
    Quaternion spawnRotation;
    GUIStyle hintStyle;

    void Start()
    {
        if (player) { spawnPosition = player.position; spawnRotation = player.rotation; }
        ResetSequence(false);
    }
    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (allowResetKey && keyboard.rKey.wasPressedThisFrame) ResetSequence(true);
            if (keyboard.eKey.wasPressedThisFrame && PlayerInRange()) TryInteract();
        }
        Advance(Time.deltaTime);
    }
    public bool PlayerInRange() => player && Vector3.Distance(player.position, transform.position) <= interactionDistance;
    public bool TryInteract()
    {
        if (!waterFrame) return false;
        if (CurrentPhase == Phase.Pool)
        {
            CurrentPhase = Phase.Draining; Elapsed = 0; onDrainStarted.Invoke(); return true;
        }
        if (CurrentPhase == Phase.Empty)
        {
            CurrentPhase = Phase.Filling; Elapsed = 0; onFloodStarted.Invoke(); return true;
        }
        return false; // Repeated E / Space cannot toggle the solver or interrupt a transition.
    }
    public void Advance(float seconds)
    {
        if (!waterFrame || seconds <= 0) return;
        if (CurrentPhase == Phase.Draining)
        {
            Elapsed = Mathf.Min(Elapsed + seconds, drainSeconds);
            SetHeight(Blend(poolHeight, emptyHeight, Elapsed / drainSeconds));
            if (Elapsed >= drainSeconds) { CurrentPhase = Phase.Empty; onPoolEmpty.Invoke(); }
        }
        else if (CurrentPhase == Phase.Filling)
        {
            float total = refillSeconds + platformFloodSeconds + hallFloodSeconds;
            Elapsed = Mathf.Min(Elapsed + seconds, total);
            if (Elapsed < refillSeconds) SetHeight(Blend(emptyHeight, overflowHeight, Elapsed / refillSeconds));
            else if (Elapsed < refillSeconds + platformFloodSeconds)
                SetHeight(Blend(overflowHeight, pressureHeight, (Elapsed - refillSeconds) / platformFloodSeconds));
            else SetHeight(Blend(pressureHeight, floodedHeight, (Elapsed - refillSeconds - platformFloodSeconds) / hallFloodSeconds));
            if (Elapsed >= total) { CurrentPhase = Phase.Flooded; onHallFlooded.Invoke(); }
        }
    }
    static float Blend(float a, float b, float t) => Mathf.Lerp(a, b, Mathf.SmoothStep(0, 1, Mathf.Clamp01(t)));
    void SetHeight(float height)
    {
        Vector3 p = waterFrame.position; p.y = height; waterFrame.position = p;
    }
    public void ResetSequence(bool resetPlayer)
    {
        CurrentPhase = Phase.Pool; Elapsed = 0;
        if (waterFrame) SetHeight(poolHeight);
        if (resetPlayer && player)
        {
            CharacterController cc = player.GetComponent<CharacterController>();
            bool wasEnabled = cc && cc.enabled;
            if (cc) cc.enabled = false;
            player.SetPositionAndRotation(spawnPosition, spawnRotation);
            if (cc) cc.enabled = wasEnabled;
        }
    }
    void OnGUI()
    {
        if (!showHints) return;
        if (hintStyle == null) hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 19, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        string text = "";
        if (CurrentPhase == Phase.Pool && PlayerInRange()) text = "E  -  Drain the pool";
        else if (CurrentPhase == Phase.Draining) text = "The water is receding...";
        else if (CurrentPhase == Phase.Empty && PlayerInRange()) text = "E  -  Let the water return";
        else if (CurrentPhase == Phase.Filling && WaterHeight > 0.15f) text = "The water keeps rising.";
        if (allowResetKey) text += (text.Length > 0 ? "\n" : "") + "R  -  Reset water demo";
        GUI.Box(new Rect(Screen.width * .5f - 220, Screen.height - 100, 440, 78), GUIContent.none);
        GUI.Label(new Rect(Screen.width * .5f - 215, Screen.height - 98, 430, 74), text, hintStyle);
    }
}
