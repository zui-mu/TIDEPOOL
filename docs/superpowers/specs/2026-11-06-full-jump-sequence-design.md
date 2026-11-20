# Full jump sequence and physical takeoff

## Approved intent

The user approved repairing the complete jump control flow after rejecting the cropped-animation workaround. Preserve the full `Girl_Jump` (frames 0..50 at 30 fps). Space starts grounded anticipation; physical takeoff happens at the authored launch; rising/apex/falling poses follow physical flight; actual contact starts complete landing recovery before locomotion. The approximately 0.63 s anticipation delay was explicitly explained and accepted. Do not crop, reverse, or replace the source animation.

## Evidence

The original runtime was forward: 34 unblended knee-pose samples matched forward sampling with 0.000261 degree RMS error, versus 42.044 degrees against reverse sampling. The defect is phase ownership: original physical apex at 0.333 s coincided with source frame 9.5 (anticipation), and contact at 0.683 s with source frame 20 (takeoff). The reverted excerpt workaround introduced a 54-degree first-frame knee change and compressed landing recovery into 0.08 s.

## Approach and alternatives

Choose physics-owned displacement with a full-clip animation clock. Root motion driving collision displacement would change the existing jump height/physics and gameplay ownership. Speeding/cropping the whole clip does not repair launch and contact ownership and was rejected. A single common time parameter for outgoing air and incoming landing would jump both poses on early contact; separate full-clip clocks preserve the outgoing pose during a blend.

## Responsibilities

- `PlayerMovement` remains the only owner of CharacterController.Move, gravity, and launch velocity. An optional registered jump handler consumes requests and queues one launch; scenes without a compatible handler keep their original immediate jump.
- `GirlPlayerAnimation` owns preparation/awaiting-launch/flight/recovery. Preparation advances the original clip at native rate up to frame 19. Hold there until movement acknowledges an actually applied launch.
- During flight, actual vertical speed maps monotonically from frame 19 to frame 25 at apex and then toward frame 30 at first contact. A long fall holds a suitable end-air pose until real contact.
- Landing uses the same full clip with a separate `LandingTime` clock from frame 30 through frame 50. The outgoing flight clock freezes at contact, so early contact blends from its actual current pose rather than jumping its time.
- Walking off an edge uses a separate `Falling` state/clock, also referencing the full clip, starting near apex. This permits blending from anticipation/recovery without moving the outgoing clock backwards.
- Use fixed 0.1 s entry/air-contact/fall blends and 0.12 s recovery-to-locomotion blends, subject to actual visual verification. Do not use Grounded alone to finish the full sequence.
- Grounded preparation and recovery suppress horizontal movement, because this is a planted-foot standing jump; air steering stays available. This is an implementation decision to prevent the complete standing animation sliding over the floor.
- Reset, disabled movement, and disabled animation cancel pending launches. Pool reset explicitly clears movement/sequence together; wait for a new movement sample before inferring a fall. Invalid or disabled animation infrastructure leaves base movement usable.

## Scope

Production changes: movement/animation bridge, their controller, and a minimal reset hook in PoolWaterCycle. Keep scene references, Avatar, water behavior, source clip, 1.2 m configured jump height, -20 m/s² gravity, and unrelated dirty work intact. No commits or publishing.

## Verification

Run the new real-component regression against old production first and observe failure because the player is airborne during anticipation. Then verify one launch after the authored anticipation, physical apex near source frame 25, no backwards clock movement, contact and full recovery to frame 50, repeated/running jump, blocked takeoff/ceiling, reset/disable cancellation and unanimated fallback. Run existing integration checks with an elapsed-time completion check. Inspect generated whole-sequence visual evidence; passing numeric assertions alone is insufficient to call motion natural.
