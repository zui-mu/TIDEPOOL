# Girl twin-braid dynamics

## Approved intent
The user selected custom bone springs and approved the detailed implementation outline in the conversation. Implement natural, restrained secondary motion using the existing eight weighted braid bones. Preserve the authored hairstyle, body animation, jump behavior, collision ownership and source FBX/weights.

## Design
- Attach GirlHairDynamics to the ArmLab Player/GirlVisual instance. Two chains Head→Hair_Left/Right_1→2→3→4 plus a measured virtual tip. Pin root positions, preserve bone translations/scales and rest twist, drive rotations after Animator and existing shoulder correctives.
- Keep animation reference rotations separate from last-frame physics; restore reference before Animator, sample current reference afterwards. Never recache physics as the rest hairstyle.
- Use bounded substeps, damped shape springs, relative head-motion acceleration and gravity, then iterative length/bend/capsule constraints. Cache references/arrays. Handle zero delta, long frames, large teleports and enable/disable without stale impulses.
- Use data-only body sphere/capsule proxies attached to Head/Neck/Chest/upper-arm transforms, not game physics colliders or additional bone hierarchy objects. First release prioritizes head/shoulder avoidance. No hair/hair collision, wind or underwater drag.
- Add MotionReset notification to PlayerMovement.ResetMotion; request hair reset and rebuild on the next post-animation update at the final teleported pose. Current PoolWaterCycle already calls ResetMotion before teleporting.
- Editor setup auto-binds missing references, measures virtual tips/radii from actual skinned geometry where available, draws debug geometry, checks configuration and saves only the clean active ArmLab scene. Check/expand hair renderer bounds on the instance.
- Initial tuning: stronger roots/softer ends, root cone about18 degrees, tip cone about35 degrees, moderately damped, strength0.75. Tune from actual walking/stopping/turning/jump/landing captures; no claim of perfect collision or strand-level deformation with four bones.

## Files
New Runtime/GirlHairDynamics.cs and GirlHairCollisionProxy.cs; Editor/GirlHairDynamicsEditor.cs and GirlHairDynamicsRegression.cs, with Unity.meta files, under Assets/_Game/Characters/GirlV06. Modify PlayerMovement only for the reset notification, current ArmLab scene for component/config, and integration notes. Source model, weights and animation assets remain unchanged.

## Verification
Observe baseline failure before production code. Deterministic real-Transform tests cover motion response, fixed roots/lengths, settling,30/60/120fps, zero/long frames, reset and degenerate collision geometry. Observe scene-component failure before installation. Play the actual character through walk/stop/run/jump/turn/reset and record close-up frames and quantitative motion/penetration metrics. Rerun existing23 jump cases and26 integration checks, inspect visual proof, confirm clean stopped editor and request independent code review before completing.
