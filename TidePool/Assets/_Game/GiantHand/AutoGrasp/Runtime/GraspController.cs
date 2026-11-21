using System;
using UnityEngine;

namespace TidePool.GiantHand.AutoGrasp
{
    [DefaultExecutionOrder(60)]
    [DisallowMultipleComponent]
    public sealed class GraspController : MonoBehaviour
    {
        private enum Phase { Idle, Reach, Pinch, Lift, Hold, Lower, Open, Return }

        [Header("Scene")]
        [SerializeField] private GiantHandPalmControl palmControl;
        [SerializeField] private GiantHandCircularArmDriver arm;
        [SerializeField] private Transform shoulder;
        [SerializeField] private Transform wrist;
        [SerializeField] private Transform restWristTarget;
        [SerializeField] private CharacterController playerCapsule;
        [SerializeField] private PlayerMovement playerMovement;
        [Tooltip("Index, middle, ring, little, thumb.")]
        [SerializeField] private FingerLineView[] fingers = Array.Empty<FingerLineView>();

        [Header("Timing (seconds)")]
        [SerializeField, Min(0.05f)] private float sampleInterval = 3f;
        [SerializeField, Min(0.05f)] private float reachTime = 2f;
        [SerializeField, Min(0.05f)] private float wrapTime = 2.8f;
        [SerializeField, Min(0.05f)] private float liftTime = 0.75f;
        [SerializeField, Min(0f)] private float holdTime = 1f;
        [SerializeField, Min(0.05f)] private float lowerTime = 1.2f;
        [SerializeField, Min(0.05f)] private float openTime = 2.4f;
        [SerializeField, Min(0.05f)] private float returnTime = 1.8f;
        [SerializeField, Min(0f)] private float restTime = 0.75f;

        [Header("Five-finger calibration")]
        [Tooltip("Wrist pose relative to the player capsule. Forward points toward the shoulder.")]
        [SerializeField] private Vector3 wristOffset;
        [SerializeField] private Quaternion wristOrientation = Quaternion.identity;
        [Tooltip("Offset from the final wrist pose where the fingers start curling, in the same frame as Wrist Offset.")]
        [SerializeField] private Vector3 preGraspOffset = new Vector3(0f, 1.5f, 0f);
        [Tooltip("Per fingertip: azimuth from forward in degrees, height from the capsule center, pad thickness. Index through thumb.")]
        [SerializeField] private Vector3[] contactOffsets = Array.Empty<Vector3>();
        [Tooltip("Initial CCD angles in degrees: spread, base flexion, middle flexion, distal flexion. Index through thumb.")]
        [SerializeField] private Vector4[] fingerSeeds = Array.Empty<Vector4>();
        [SerializeField, Min(0f)] private float fingerRadius = 0.038f;
        [SerializeField, Min(0.001f)] private float fingerTolerance = 0.025f;
        [SerializeField, Min(0f)] private float floorClearance = 0.04f;
        [SerializeField, Min(0f)] private float liftHeight = 0.5f;

        [Header("Diagnostics")]
        [SerializeField] private Phase phase;
        [SerializeField] private string outcome = "Waiting for Play mode";
        [SerializeField] private int fingerContacts;
        [Tooltip("Actual fingertip-to-target distances. Index, middle, ring, little, thumb.")]
        [SerializeField] private float[] fingertipErrors = new float[DigitCount];
        [SerializeField] private float lastLiftDistance;

        private const int DigitCount = 5;
        private const int Thumb = DigitCount - 1;
        private const float ThumbCurlLead = 0.85f;
        private const float WristDescentExponent = 11f;
        private const float CarryPositionTolerance = 0.012f;
        private const float PoseErrorTolerance = 0.005f;
        private const float SolverTolerance = 0.0025f;
        private const float MeshContactSlack = 0.004f;
        private const float MeshContactTolerance = 0.012f;
        private static readonly float[] YawOffsets = { 0f, -20f, 20f, -40f, 40f };
        private static readonly Vector2[] FlexLimits =
        {
            new Vector2(-90f, 0f), new Vector2(-105f, 0f), new Vector2(-85f, 0f)
        };

        private sealed class Finger
        {
            public FingerLineView Rig;
            public FingerGripDriver ManualDriver;
            public bool ManualDriverEnabled;
            public Pose AnchorInWrist;
            public FingerCcdSolver Solver;
            public readonly float[] ClosedAngles = new float[4];

            public Pose ParentPose(Pose pose) => new Pose(
                pose.position + pose.rotation * AnchorInWrist.position,
                pose.rotation * AnchorInWrist.rotation);

            public void Apply()
            {
                Pose[] poses = Solver.LocalPoses;
                Rig.joint1.localRotation = poses[0].rotation;
                Rig.joint2.localRotation = poses[1].rotation;
                Rig.joint3.localRotation = poses[2].rotation;
            }
        }

        private Finger[] rigFingers;
        private FingerCcdSolver[] solvers;
        private HandClearance clearance;
        private GiantHandPalmControl drivenPalm;
        private GiantHandCircularArmDriver drivenArm;
        private PlayerMovement lockedPlayer;
        private bool ownsRig;
        private float elapsed;
        private double nextSample;
        private double readyAt;
        private float floorY;
        private float closure;
        private float openFrom;
        private float lowerFrom;
        private float returnClosure;
        private float returnToRestDuration;
        private bool returningToRest;
        private CapsuleSnapshot lockedCapsule;
        private Vector3 lockedPosition;
        private Vector3 playerFromMidpoint;
        private Quaternion contactFrame;
        private Pose approachFrom;
        private Pose prePinch;
        private Pose pinchWrist;
        private Pose carryFrom;
        private Pose returnFrom;
        private Pose returnVia;
        private Vector3 approachSide;
        private Vector3 openPathOffset;
        private readonly float[] angleScratch = new float[4];
        private readonly RaycastHit[] floorHits = new RaycastHit[12];

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;
            if (!RigAvailable() || palmControl.DrivenExternally || arm.DrivenExternally ||
                restWristTarget.IsChildOf(shoulder) || fingers == null || fingers.Length != DigitCount ||
                contactOffsets == null || contactOffsets.Length != DigitCount ||
                fingerSeeds == null || fingerSeeds.Length != DigitCount ||
                !TryBuildFingers())
            {
                outcome = "Invalid pinch rig bindings";
                enabled = false;
                return;
            }

            clearance = new HandClearance(wrist, fingers);
            drivenPalm = palmControl;
            drivenArm = arm;
            drivenPalm.DrivenExternally = drivenArm.DrivenExternally = true;
            foreach (Finger finger in rigFingers)
                if (finger.ManualDriver)
                    finger.ManualDriver.enabled = false;

            ownsRig = true;
            fingertipErrors = new float[DigitCount];
            ResetFingers();
            if (PlayerCapsuleSampler.TryCapture(playerCapsule, out CapsuleSnapshot capsule, out _))
                floorY = StandingFloor(capsule);
            Enter(Phase.Idle);
            readyAt = Time.timeAsDouble;
            nextSample = readyAt + sampleInterval;
            outcome = "Waiting for player sample";
        }

        private void OnDisable()
        {
            ReleasePlayer();
            if (!ownsRig)
                return;
            if (drivenPalm) drivenPalm.DrivenExternally = false;
            if (drivenArm) drivenArm.DrivenExternally = false;
            foreach (Finger finger in rigFingers)
            {
                if (FingerValid(finger.Rig))
                {
                    finger.Solver.Reset();
                    finger.Apply();
                }
                if (finger.ManualDriver)
                    finger.ManualDriver.enabled = finger.ManualDriverEnabled;
            }
            ownsRig = false;
        }

        private void LateUpdate()
        {
            if (!ownsRig)
                return;
            if (!RigAvailable())
            {
                outcome = "Pinch cancelled: a rig or player binding was lost";
                enabled = false;
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            // Reading the pool letter pauses movement without removing the player binding.
            bool playerCanBeTargeted = playerMovement.isActiveAndEnabled && playerCapsule.enabled;
            if (!playerCanBeTargeted && phase != Phase.Idle && phase != Phase.Return)
            {
                outcome = "Grasp paused: player controls unavailable";
                BeginReturn();
            }

            double now = Time.timeAsDouble;
            bool sampleDue = now >= nextSample;
            if (sampleDue)
            {
                float interval = Mathf.Max(0.05f, sampleInterval);
                nextSample += (Math.Floor((now - nextSample) / interval) + 1d) * interval;
            }
            elapsed += dt;

            switch (phase)
            {
                case Phase.Idle:
                    Pose rest = WorldPose(restWristTarget);
                    if (!clearance.IsClear(rest, solvers, floorY, floorClearance) || !DriveWrist(rest, dt))
                        outcome = "Rest wrist is blocked or out of reach";
                    else if (playerCanBeTargeted && sampleDue && now >= readyAt)
                        BeginReach();
                    break;
                case Phase.Reach:
                    UpdateReach(dt);
                    break;
                case Phase.Pinch:
                    UpdatePinch(dt);
                    break;
                case Phase.Lift:
                    UpdateLift(dt);
                    break;
                case Phase.Hold:
                    if (!TryMoveCarried(liftHeight, dt, out bool holdBlocked) || holdBlocked || elapsed >= holdTime)
                        BeginLower();
                    break;
                case Phase.Lower:
                    UpdateLower(dt);
                    break;
                case Phase.Open:
                    UpdateOpen(dt);
                    break;
                case Phase.Return:
                    UpdateReturn(dt, now);
                    break;
            }
        }

        private void BeginReach()
        {
            if (!playerMovement.IsGrounded ||
                !PlayerCapsuleSampler.TryCapture(playerCapsule, out CapsuleSnapshot capsule, out _))
            {
                outcome = "Waiting for a grounded player";
                return;
            }

            floorY = StandingFloor(capsule);
            Vector3 outward = Vector3.ProjectOnPlane(shoulder.position - capsule.center, capsule.axis);
            if (outward.sqrMagnitude < 1e-6f)
                outward = Vector3.ProjectOnPlane(Vector3.forward, capsule.axis);
            outward.Normalize();
            Quaternion frame = Quaternion.LookRotation(outward, capsule.axis);
            approachFrom = WorldPose(wrist);
            fingerContacts = 0;
            Array.Clear(fingertipErrors, 0, DigitCount);
            int bestSolved = 0;
            foreach (float yaw in YawOffsets)
            {
                if (!TryPlan(capsule, Quaternion.AngleAxis(yaw, capsule.axis) * frame, out int solved))
                {
                    bestSolved = Mathf.Max(bestSolved, solved);
                    continue;
                }
                ResetFingers();
                outcome = "Reaching for five-finger grasp";
                Enter(Phase.Reach);
                return;
            }
            ResetFingers();
            outcome = $"Miss: no clear grasp path (IK {bestSolved}/{DigitCount})";
        }

        private bool TryPlan(CapsuleSnapshot capsule, Quaternion frame, out int solvedCount)
        {
            solvedCount = 0;
            contactFrame = frame;
            approachSide = frame * Vector3.forward;
            pinchWrist = new Pose(capsule.center + frame * wristOffset,
                frame * wristOrientation.normalized);
            prePinch = new Pose(pinchWrist.position + frame * preGraspOffset, pinchWrist.rotation);
            Pose raised = Translated(pinchWrist, liftHeight);
            if (!arm.IsWristReachable(pinchWrist.position) || !arm.IsWristReachable(raised.position))
                return false;

            ResetSolvers();
            for (int i = 0; i < DigitCount; i++)
                if (TryFingertipTarget(capsule, i, out Vector3 target) && SolveFinger(i, target, capsule))
                    solvedCount++;
            if (solvedCount != DigitCount)
                return false;

            for (int i = 0; i <= 32; i++)
            {
                float progress = i / 32f;
                SetClosure(progress);
                Pose goal = CloseGoal(progress);
                if (!arm.IsWristReachable(goal.position) || !PoseIsClear(goal, capsule))
                    return false;
            }
            SetClosure(0f);
            for (int i = 0; i <= 32; i++)
            {
                Pose goal = Blend(approachFrom, prePinch, i / 32f);
                if (!arm.IsWristReachable(goal.position) || !PoseIsClear(goal, capsule))
                    return false;
            }
            return true;
        }

        private bool TryFingertipTarget(CapsuleSnapshot capsule, int digit, out Vector3 target)
        {
            Vector3 offset = contactOffsets[digit];
            Vector3 direction = contactFrame * (Quaternion.AngleAxis(offset.x, Vector3.up) * Vector3.forward);
            return GraspTargetGenerator.TryGetFingertipTarget(capsule, direction, offset.y, offset.z, out target);
        }

        private bool SolveFinger(int digit, Vector3 tipTarget, CapsuleSnapshot capsule)
        {
            Finger finger = rigFingers[digit];
            Vector4 seed = fingerSeeds[digit];
            for (int i = 0; i < angleScratch.Length; i++)
                angleScratch[i] = seed[i];
            finger.Solver.SetAngles(angleScratch);
            if (!finger.Solver.Solve(finger.ParentPose(pinchWrist), tipTarget, capsule, fingerRadius,
                    Mathf.Min(SolverTolerance, fingerTolerance), 48))
                return false;
            finger.Solver.CopyAnglesTo(finger.ClosedAngles);
            return true;
        }

        private void UpdateReach(float dt)
        {
            float progress = Progress(reachTime);
            Pose goal = Blend(approachFrom, prePinch, progress);
            if (!PlayerCapsuleSampler.TryCapture(playerCapsule, out CapsuleSnapshot live, out _) ||
                !PoseIsClear(goal, live) || !DriveWrist(goal, dt))
            {
                outcome = "Miss: reach became obstructed";
                BeginReturn();
                return;
            }
            if (progress >= 1f)
                Enter(Phase.Pinch);
        }

        private void UpdatePinch(float dt)
        {
            float progress = Progress(wrapTime);
            Pose goal = CloseGoal(progress);
            SetClosure(progress);
            if (!PlayerCapsuleSampler.TryCapture(playerCapsule, out CapsuleSnapshot live, out _) ||
                !PoseIsClear(goal, live) || !DriveWrist(goal, dt))
            {
                SetClosure(closure);
                outcome = "Miss: the player moved into the pinch path";
                BeginOpen();
                return;
            }
            closure = progress;
            ApplyFingers();
            if (progress < 1f)
                return;

            bool hasGrip = ReadContacts(live);
            if (!playerMovement.IsGrounded || !hasGrip || !playerMovement.TryLockMovement(this))
            {
                outcome = fingerContacts == DigitCount
                    ? "Miss: player could not be carried"
                    : $"Partial grasp: {fingerContacts}/{DigitCount} fingertips";
                BeginOpen();
                return;
            }

            lockedPlayer = playerMovement;
            lockedPosition = playerMovement.transform.position;
            lockedCapsule = live;
            carryFrom = WorldPose(wrist);
            playerFromMidpoint = lockedPosition - GripMidpoint();
            lastLiftDistance = 0f;
            outcome = $"Grasp confirmed: {DigitCount}/{DigitCount}";
            Enter(Phase.Lift);
        }

        private void UpdateLift(float dt)
        {
            float progress = Progress(liftTime);
            if (!TryMoveCarried(liftHeight * Ease(progress), dt, out bool blocked))
            {
                CancelCarry();
                return;
            }
            if (blocked)
            {
                outcome = "Lift blocked: lowering player";
                BeginLower();
            }
            else if (progress >= 1f)
                Enter(Phase.Hold);
        }

        private void BeginLower()
        {
            lowerFrom = lockedPlayer ? lockedPlayer.transform.position.y - lockedPosition.y : 0f;
            Enter(Phase.Lower);
        }

        private void UpdateLower(float dt)
        {
            float progress = Progress(lowerTime);
            if (!TryMoveCarried(Mathf.Lerp(lowerFrom, 0f, Ease(progress)), dt, out bool blocked))
            {
                CancelCarry();
                return;
            }
            if (blocked && !lockedPlayer.IsGrounded)
            {
                CancelCarry();
                return;
            }
            if (blocked || progress >= 1f)
                BeginOpen();
        }

        private bool TryMoveCarried(float height, float dt, out bool blocked)
        {
            blocked = false;
            if (!lockedPlayer || lockedPlayer != playerMovement || !lockedPlayer.IsMovementLockedBy(this) ||
                !PlayerCapsuleSampler.TryCapture(playerCapsule, out CapsuleSnapshot live, out _) ||
                live.radius != lockedCapsule.radius || live.height != lockedCapsule.height ||
                (Vector3.ProjectOnPlane(lockedPlayer.transform.position - lockedPosition, Vector3.up)).sqrMagnitude > 0.0001f)
                return false;

            Pose goal = Translated(carryFrom, height);
            CapsuleSnapshot proposed = lockedCapsule;
            proposed.center += height * Vector3.up;
            if (!PoseIsClear(goal, proposed) || !DriveWrist(goal, dt))
                return false;

            float beforeY = lockedPlayer.transform.position.y;
            float targetY = (GripMidpoint() + playerFromMidpoint).y;
            if (!lockedPlayer.TryMoveWhileLocked(this, targetY, out CollisionFlags flags))
                return false;
            if (!PlayerCapsuleSampler.TryCapture(playerCapsule, out live, out _))
                return false;

            Vector3 actual = lockedPlayer.transform.position;
            lastLiftDistance = Mathf.Max(lastLiftDistance, actual.y - lockedPosition.y);
            if (Vector3.ProjectOnPlane(actual - lockedPosition, Vector3.up).sqrMagnitude > 0.0001f)
                return false;
            float deltaY = actual.y - targetY;
            blocked = Mathf.Abs(deltaY) > CarryPositionTolerance ||
                (targetY > beforeY && (flags & CollisionFlags.Above) != 0);
            // The capsule's skin can stop a few millimetres before its target on landing.
            // Follow the achieved height even when this is within the allowed travel error.
            if (Mathf.Abs(deltaY) > 0.0001f)
            {
                goal = Translated(carryFrom, actual.y - lockedPosition.y);
                if (!PoseIsClear(goal, live) || !DriveWrist(goal, dt))
                    return false;
            }
            return ReadContacts(live);
        }

        private void CancelCarry()
        {
            outcome = "Carry cancelled: contact or clearance lost";
            ReleasePlayer();
            BeginOpen();
        }

        private void BeginOpen()
        {
            openFrom = closure;
            openPathOffset = wrist.position - CloseGoal(closure).position;
            Enter(Phase.Open);
        }

        private void UpdateOpen(float dt)
        {
            float progress = Progress(openTime);
            float nextClosure = openFrom * (1f - progress);
            SetClosure(nextClosure);
            Pose goal = CloseGoal(nextClosure);
            goal.position += openPathOffset;
            if (!PoseIsSafeWithCurrentPlayer(goal) || !DriveWrist(goal, dt))
            {
                SetClosure(closure);
                outcome = "Opening obstructed: withdrawing hand";
                BeginReturn();
                return;
            }
            closure = nextClosure;
            ApplyFingers();
            if (progress >= 1f)
                BeginReturn(true);
        }

        private void BeginReturn(bool alreadyWithdrawn = false)
        {
            ReleasePlayer();
            returnFrom = WorldPose(wrist);
            returnVia = new Pose(returnFrom.position + 0.65f * Vector3.up + 0.3f * approachSide,
                returnFrom.rotation);
            if (!arm.IsWristReachable(returnVia.position))
                returnVia.position = returnFrom.position + 0.65f * Vector3.up;
            if (!arm.IsWristReachable(returnVia.position))
                returnVia = returnFrom;
            returnClosure = closure;
            returningToRest = alreadyWithdrawn;
            returnToRestDuration = alreadyWithdrawn ? returnTime : returnTime * 0.65f;
            Enter(Phase.Return);
        }

        private void UpdateReturn(float dt, double now)
        {
            if (!returningToRest)
            {
                float retract = Progress(returnTime * 0.35f);
                Pose goal = Blend(returnFrom, returnVia, retract);
                if (!PoseIsSafeWithCurrentPlayer(goal) || !DriveWrist(goal, dt))
                {
                    elapsed = Mathf.Max(0f, elapsed - dt);
                    outcome = "Return blocked: waiting for clearance";
                    return;
                }
                if (retract >= 1f)
                {
                    returnFrom = WorldPose(wrist);
                    returnClosure = closure;
                    elapsed = 0f;
                    returningToRest = true;
                }
                return;
            }
            float progress = Progress(returnToRestDuration);
            float nextClosure = returnClosure * (1f - Ease(progress));
            SetClosure(nextClosure);
            Pose returning = Blend(returnFrom, WorldPose(restWristTarget), progress);
            if (!PoseIsSafeWithCurrentPlayer(returning) || !DriveWrist(returning, dt))
            {
                SetClosure(closure);
                elapsed = Mathf.Max(0f, elapsed - dt);
                outcome = "Return blocked: waiting for clearance";
                return;
            }
            closure = nextClosure;
            ApplyFingers();
            if (progress >= 1f)
            {
                ResetFingers();
                readyAt = now + restTime;
                Enter(Phase.Idle);
            }
        }

        private bool ReadContacts(CapsuleSnapshot capsule)
        {
            fingerContacts = 0;
            for (int i = 0; i < DigitCount; i++)
            {
                fingertipErrors[i] = TryFingertipTarget(capsule, i, out Vector3 target)
                    ? Vector3.Distance(rigFingers[i].Rig.tip.position, target)
                    : float.PositiveInfinity;
                if (fingertipErrors[i] <= fingerTolerance &&
                    clearance.FingertipTouches(i, WorldPose(rigFingers[i].Rig.joint3), capsule,
                        Mathf.Min(fingerTolerance, MeshContactTolerance)))
                    fingerContacts++;
            }
            return fingerContacts == DigitCount && PoseIsClear(WorldPose(wrist), capsule);
        }

        private bool PoseIsClear(Pose pose, CapsuleSnapshot capsule)
        {
            if (!clearance.IsClear(pose, solvers, floorY, floorClearance, capsule, MeshContactSlack))
                return false;
            foreach (Finger finger in rigFingers)
                if (!finger.Solver.IsClear(finger.ParentPose(pose), capsule, fingerRadius))
                    return false;
            for (int i = 0; i < solvers.Length; i++)
                for (int j = 0; j < i; j++)
                    if (!solvers[i].HasClearanceFrom(solvers[j], 2f * fingerRadius))
                        return false;
            return true;
        }

        private bool PoseIsSafeWithCurrentPlayer(Pose pose)
        {
            if (PlayerCapsuleSampler.TryCapture(playerCapsule, out CapsuleSnapshot live, out _))
                return PoseIsClear(pose, live);
            return !playerCapsule.enabled && clearance.IsClear(pose, solvers, floorY, floorClearance);
        }

        private float StandingFloor(CapsuleSnapshot capsule)
        {
            float feetY = capsule.center.y - 0.5f * capsule.height;
            float planeY = feetY;
            int count = Physics.RaycastNonAlloc(capsule.center, Vector3.down, floorHits,
                0.5f * capsule.height + 0.15f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = floorHits[i];
                if (hit.collider && !hit.collider.transform.IsChildOf(playerMovement.transform) &&
                    hit.normal.y > 0.55f && Mathf.Abs(hit.point.y - feetY) <= 0.15f)
                    planeY = Mathf.Max(planeY, hit.point.y);
            }
            return planeY;
        }

        private Vector3 GripMidpoint()
        {
            Vector3 opposingCenter = Vector3.zero;
            for (int i = 0; i < Thumb; i++)
                opposingCenter += rigFingers[i].Rig.tip.position;
            return 0.5f * (rigFingers[Thumb].Rig.tip.position + opposingCenter / Thumb);
        }

        private Pose CloseGoal(float progress)
        {
            // Start moving with the fingers, then ease in once the lower links have curled clear.
            float lateDescent = Mathf.Pow(progress, WristDescentExponent) *
                (1f + WristDescentExponent * (1f - progress));
            float descent = 0.5f * (Ease(progress) + lateDescent);
            return new Pose(Vector3.Lerp(prePinch.position, pinchWrist.position, descent), pinchWrist.rotation);
        }

        private bool DriveWrist(Pose goal, float dt) =>
            palmControl.TryApplyWristGoal(goal, Mathf.Min(dt, 0.05f)) && arm.Refresh() &&
            Vector3.Distance(wrist.position, goal.position) <= PoseErrorTolerance;

        private void SetClosure(float amount)
        {
            float bend = Ease(amount);
            // Move the thumb farther early in the curl to clear the index finger.
            // Both curves begin and end at the same times.
            float thumbBend = Ease(amount + ThumbCurlLead * amount * (1f - amount));
            for (int i = 0; i < rigFingers.Length; i++)
            {
                Finger finger = rigFingers[i];
                float fingerBend = i == Thumb ? thumbBend : bend;
                for (int angle = 0; angle < angleScratch.Length; angle++)
                    angleScratch[angle] = fingerBend * finger.ClosedAngles[angle];
                finger.Solver.SetAngles(angleScratch);
            }
        }

        private void ApplyFingers()
        {
            foreach (Finger finger in rigFingers)
                finger.Apply();
        }

        private void ResetSolvers()
        {
            foreach (FingerCcdSolver solver in solvers)
                solver.Reset();
        }

        private void ResetFingers()
        {
            ResetSolvers();
            closure = 0f;
            ApplyFingers();
        }

        private void ReleasePlayer()
        {
            if (lockedPlayer)
                lockedPlayer.ReleaseMovementLock(this);
            lockedPlayer = null;
        }

        private bool TryBuildFingers()
        {
            rigFingers = new Finger[fingers.Length];
            solvers = new FingerCcdSolver[fingers.Length];
            Quaternion invWrist = Quaternion.Inverse(wrist.rotation);
            for (int i = 0; i < fingers.Length; i++)
            {
                FingerLineView rig = fingers[i];
                if (!FingerValid(rig))
                    return false;
                Transform parent = rig.joint1.parent;
                var manual = rig.GetComponent<FingerGripDriver>();
                float spread = i == Thumb ? 82f : 65f;
                solvers[i] = new FingerCcdSolver(
                    new[] { LocalPose(rig.joint1), LocalPose(rig.joint2), LocalPose(rig.joint3) },
                    rig.tip.localPosition, parent.InverseTransformDirection(wrist.up),
                    new Vector2(-spread, spread), FlexLimits);
                rigFingers[i] = new Finger
                {
                    Rig = rig,
                    ManualDriver = manual,
                    ManualDriverEnabled = manual && manual.enabled,
                    AnchorInWrist = new Pose(invWrist * (parent.position - wrist.position), invWrist * parent.rotation),
                    Solver = solvers[i]
                };
            }
            return true;
        }

        private bool RigAvailable()
        {
            if (!palmControl || !palmControl.isActiveAndEnabled || !arm || !arm.isActiveAndEnabled ||
                !shoulder || !wrist || !restWristTarget || !playerCapsule || !playerMovement ||
                !playerMovement.gameObject.activeInHierarchy || playerCapsule.gameObject != playerMovement.gameObject)
                return false;
            if (ownsRig)
            {
                if (drivenPalm != palmControl || drivenArm != arm)
                    return false;
                foreach (Finger finger in rigFingers)
                    if (!FingerValid(finger.Rig))
                        return false;
            }
            return true;
        }

        private bool FingerValid(FingerLineView rig) =>
            wrist && rig && rig.wrist == wrist && rig.joint1 && rig.joint1.parent && rig.joint1.IsChildOf(wrist) &&
            rig.joint2 && rig.joint2.parent == rig.joint1 && rig.joint3 && rig.joint3.parent == rig.joint2 &&
            rig.tip && rig.tip.parent == rig.joint3;

        private void Enter(Phase next)
        {
            phase = next;
            elapsed = 0f;
        }

        private float Progress(float seconds) => Mathf.Clamp01(elapsed / Mathf.Max(0.05f, seconds));
        private static float Ease(float t) => t * t * (3f - 2f * t);
        private static Pose Blend(Pose a, Pose b, float t) =>
            new Pose(Vector3.Lerp(a.position, b.position, Ease(t)), Quaternion.Slerp(a.rotation, b.rotation, Ease(t)));
        private static Pose Translated(Pose pose, float height) => new Pose(pose.position + height * Vector3.up, pose.rotation);
        private static Pose WorldPose(Transform item) => new Pose(item.position, item.rotation);
        private static Pose LocalPose(Transform item) => new Pose(item.localPosition, item.localRotation);

        private void OnDrawGizmosSelected()
        {
            if (restWristTarget)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(restWristTarget.position, 0.2f);
            }
            if (ownsRig && phase != Phase.Idle && phase != Phase.Return)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(pinchWrist.position, 0.14f);
            }
        }
    }
}
