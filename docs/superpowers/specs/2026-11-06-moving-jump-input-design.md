# Input-selected moving jump

The user approved backing up the current project to GitHub, then implementing the discussed moving-jump rule. Backup 204c370 is verified on origin/codex/backup-before-moving-jump-20261106.

## Behavior

- On an accepted grounded Space press, latch moving jump when the net WASD input vector is nonzero. Opposing keys cancel. Shift affects speed only. Classification uses input intent, not measured speed.
- A stationary jump retains the existing grounded preparation/recovery restrictions. A moving jump retains ordinary acceleration, braking and steering throughout Preparing, AwaitingTakeoff, Airborne and Recovery. Releasing input does not change jump type.
- Keep the current horizontal velocity state; do not zero it for a moving jump, add a second inherited velocity or force a fixed speed. Existing acceleration and air input behavior remain.
- CharacterController.Move remains the single displacement/collision owner. Walls constrain real displacement, including diagonal sliding, without cancelling the vertical jump or accumulating an additional launch impulse.
- Losing ground before physical takeoff cancels the pending launch and enters Falling. No coyote time, invisible support or air jump is introduced.
- Preserve the full original 0..50 jump, frame19 takeoff, frame25 apex, frame30 contact and complete recovery. No animation clipping, retiming, root-motion changes or new motion assets.
- Clear the latched type on cancellation, reset and normal completion. Preserve Animator-disabled fallback behavior and the existing reset/disable/ceiling/ledge regressions.

## Approach and limitations

Choose one latched moving flag in GirlPlayerAnimation, passed from PlayerMovement through IPlayerJumpHandler.HandleJumpRequest(bool hasMovementInput). Keep PlayerMovement responsible for velocity; avoid a parallel momentum controller. Actual-speed classification was considered but does not implement the user's W+Space-from-rest rule. Replacing the source animation could improve moving foot contact but is a separate asset change; this implementation must expose the actual visual result for review.

The existing approximately0.63-second anticipation remains. Running over an edge before it completes intentionally becomes a fall. Translating planted anticipation/recovery may show foot sliding; numerical checks cannot establish visual naturalness.

## Verification

Add moving-jump integration cases to the existing real-character runner in a separate partial file. First run held-running continuity against unchanged production and observe the forced-zero failure. Then run the complete stationary/flight/interruption suite plus running/walking, simultaneous W+Space, opposing keys/late input, input release/resume, wall blocking/sliding, moving support loss, and reset-to-stationary. Capture a continuous moving-jump preview. No persistent scene saves during tests.