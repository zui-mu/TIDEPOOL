using AbstractOcclusion.WebGpuWater;
using UnityEngine;
using Random = UnityEngine.Random;

[RequireComponent(typeof(PlayerMovement), typeof(CharacterController), typeof(AudioSource))]
public sealed class PlayerSfx : MonoBehaviour
{
    [SerializeField] private WaterVolume water;
    [SerializeField] private WaterExclusionVolume dryAirPocket;
    [SerializeField] private AudioClip[] tileSteps;
    [SerializeField] private AudioClip[] waterSteps;
    [SerializeField] private AudioClip[] takeoff;
    [SerializeField] private AudioClip[] landing;
    [SerializeField] private AudioClip splash;

    private const float MinimumStepSpeed = 0.15f;
    private PlayerMovement movement;
    private CharacterController controller;
    private AudioSource source;
    private bool hasPreviousSample;
    private bool wasInWater;
    private uint previousLaunch;
    private float distanceSinceStep;
    private float timeInAir;
    private float previousVerticalSpeed;
    private int lastTileStep = -1;
    private int lastWaterStep = -1;
    private int lastTakeoff = -1;
    private int lastLanding = -1;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        controller = GetComponent<CharacterController>();
        source = GetComponent<AudioSource>();
    }

    private void OnEnable()
    {
        movement.MotionReset += ResetTracking;
        ResetTracking();
    }

    private void OnDisable()
    {
        movement.MotionReset -= ResetTracking;
        source.Stop();
    }

    private void ResetTracking()
    {
        hasPreviousSample = false;
        previousLaunch = movement.JumpLaunchSequence;
        distanceSinceStep = 0f;
        timeInAir = 0f;
        previousVerticalSpeed = 0f;
    }

    private void LateUpdate()
    {
        if (!movement.isActiveAndEnabled || !controller.enabled ||
            !movement.HasMotionSample || Time.deltaTime <= 0f)
            return;

        Vector3 feet = transform.position;
        feet.y = controller.bounds.min.y + 0.04f;
        bool grounded = movement.IsGrounded;
        float speed = movement.PlanarVelocity.magnitude;
        float verticalSpeed = movement.VerticalSpeed;
        bool inWater = IsInWater(feet);
        bool launched = movement.JumpLaunchSequence != previousLaunch;
        bool movingIntoWater = speed > MinimumStepSpeed || Mathf.Min(verticalSpeed, previousVerticalSpeed) < -0.5f;
        bool splashed = hasPreviousSample && inWater && !wasInWater && movingIntoWater;
        bool landedOnTile = hasPreviousSample && grounded && !inWater && timeInAir >= 0.10f;

        if (splashed)
            Play(splash, 0.85f);
        else if (launched)
            PlayVariation(takeoff, ref lastTakeoff, 0.85f);
        else if (landedOnTile)
            PlayVariation(landing, ref lastLanding, 0.82f);

        if (launched || splashed || landedOnTile || inWater != wasInWater ||
            speed <= MinimumStepSpeed || (!grounded && !inWater))
        {
            distanceSinceStep = 0f;
        }
        else
        {
            float run = Mathf.InverseLerp(movement.WalkSpeed, movement.RunSpeed, speed);
            float stepDistance = Mathf.Lerp(0.65f, 1.05f, run);
            distanceSinceStep += speed * Time.deltaTime;
            if (distanceSinceStep >= stepDistance)
            {
                distanceSinceStep %= stepDistance;
                float gain = Mathf.Lerp(0.58f, 0.75f, run);
                if (inWater)
                    PlayVariation(waterSteps, ref lastWaterStep, gain);
                else
                    PlayVariation(tileSteps, ref lastTileStep, gain);
            }
        }

        timeInAir = grounded || inWater ? 0f : timeInAir + Time.deltaTime;
        previousLaunch = movement.JumpLaunchSequence;
        previousVerticalSpeed = verticalSpeed;
        wasInWater = inWater;
        hasPreviousSample = true;
    }

    private bool IsInWater(Vector3 feet)
    {
        if (!water || !water.isActiveAndEnabled || IsInDryAirPocket(feet))
            return false;

        if (!water.TryGetAnalyticWaterline(feet.x, feet.z, out float surfaceY))
            return false;

        float threshold = hasPreviousSample && wasInWater ? -0.04f : 0.06f;
        return surfaceY - feet.y > threshold;
    }

    private bool IsInDryAirPocket(Vector3 position)
    {
        if (!dryAirPocket || !dryAirPocket.isActiveAndEnabled)
            return false;

        Vector3 local = dryAirPocket.transform.InverseTransformPoint(position);
        Vector3 halfSize = dryAirPocket.size * 0.5f;
        return Mathf.Abs(local.x) < halfSize.x
            && Mathf.Abs(local.y) < halfSize.y
            && Mathf.Abs(local.z) < halfSize.z;
    }

    private void PlayVariation(AudioClip[] clips, ref int lastClip, float volume)
    {
        if (clips == null || clips.Length == 0)
            return;

        int index = 0;
        if (clips.Length > 1)
        {
            bool hasLastClip = lastClip >= 0 && lastClip < clips.Length;
            index = Random.Range(0, hasLastClip ? clips.Length - 1 : clips.Length);
            if (hasLastClip && index >= lastClip)
                index++;
        }

        lastClip = index;
        Play(clips[index], volume);
    }

    private void Play(AudioClip clip, float volume)
    {
        if (clip)
            source.PlayOneShot(clip, volume);
    }
}
