# Jump air animation correction

> 历史已撤回方案，不纳入现行 Spec 图；当前规范见 [tidepool-jump](2026-11-06-full-jump-sequence-design.md)。下文保留原记录，不作为重新实施截取方案的授权。

**Reverted after visual regression feedback.** The excerpt and shorter blends made transitions abrupt. The original full Girl_Jump reference, speed 1, and 0.1/0.12 s blends have been restored. Prior timing/integration checks did not validate visual continuity or establish the original reverse-playback complaint's root cause. This proposal is historical, not an approved future fix.

The user approved the previously described minimal fix: retain immediate, code-driven Space jumping and replace the full jump in AirborneHold with a dedicated airborne excerpt. The original full Girl_Jump remains available. Anticipation and a separate landing state are outside this change.

The full source lasts 50 frames at 30 fps, while the configured 1.2 m jump under -20 m/s² gravity lasts about 0.693 seconds. Select the excerpt using source foot contact evidence, and set a positive playback speed so the excerpt spans that airtime. Preserve Grounded transition conditions, root-motion ownership, movement scripts, scene references, and existing user changes. Use fixed 0.03 s entry and 0.08 s return blends initially.

Alternatives considered: speeding up the entire clip still plays anticipation after takeoff; a three-stage anticipation/air/landing state machine requires gameplay changes. The user authorized the minimal excerpt approach after receiving its limitations.

Verify the imported clip/reference and source apex alignment, exercise the existing integrated-player checks, inspect the jump visually where desktop access permits, and leave Unity out of Play mode. Do not commit or stage unrelated work.
