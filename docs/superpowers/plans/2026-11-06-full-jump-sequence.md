# Full Jump Sequence Implementation Plan

> 历史实施记录，不是当前执行授权；现行阶段契约见 [tidepool-jump](../specs/2026-11-06-full-jump-sequence-design.md)，移动跳已有独立的缩短缓冲规则。下文检查结果与禁止提交等指令仅属于当时任务。

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Preserve the full jump while aligning physical launch, airborne poses, contact, and recovery.

**Architecture:** Movement owns physics and acknowledges one queued animated launch; the bridge owns phase clocks. All jump/fall/recovery states reference the unchanged complete source clip. Separate clocks preserve outgoing poses through collision-driven transitions.

**Tech Stack:** Unity 6000.6, C#, CharacterController, Humanoid Animator, Input System.

**Spec:** docs/superpowers/specs/2026-11-06-full-jump-sequence-design.md

## Global Constraints

- Work in the current checkout, preserve unrelated existing edits, and do not commit.
- Do not crop or reverse source clips; preserve configured height/gravity and collision ownership.
- No source/scene changes until the runtime anticipation regression has failed against old production.
- Runtime tests must restore input settings, temporary devices, scene objects, and camera state.

## Review Focus

- One acknowledged impulse per request, including blocked takeoff with no airborne frame.
- Continuous outgoing pose clocks through normal/early landing and ledge falls.
- Reset, disabled components, initial invalid samples, and unsupported animation-controller fallback.
- Planted-foot movement in preparation/recovery, unchanged walking and airborne steering.
- Visual continuity across the complete sequence rather than isolated apex matching.

### Task 1: Reproduce and repair complete jump ownership

**Files:** PlayerMovement.cs, GirlPlayerAnimation.cs, GirlV06_Locomotion.controller, PoolWaterCycle.cs, GirlJumpSequenceRegression.cs and its meta.

**Interfaces:** `IPlayerJumpHandler` consumes jump requests/cancels and exposes grounded movement suppression. `PlayerMovement` registers/unregisters one handler, queues an owner-validated launch, exposes launch acknowledgement/sample validity/takeoff speed, and resets motion. Animator parameters: bool JumpActive/JumpLanding/FallOnly; float JumpTime/LandingTime/FallTime. Source clocks are normalized over the entire original 0..50 clip.

- [x] Run GirlJumpSequenceRegression.RunBatch before production edits; require failed anticipation assertion.
- [x] Implement optional deferred launch with acknowledgement, cancellation, and a fresh-motion-sample guard.
- [x] Implement native anticipation, velocity-driven monotonic flight, separate recovery/fall clocks, and controller wiring to full source clip.
- [x] Add explicit player-reset cancellation and preserve legacy immediate jump where the bridge is unavailable.
- [x] Run new regression to green, including complete recovery and pose-change evidence; update existing integration completion check and run it.

### Task 2: Review and visually validate

- [x] Inspect real captures across anticipation, takeoff, apex, contact, and recovery; collect a complete sequence for continuity inspection.
- [x] Request a fresh focused review, fix concrete findings with appropriate regression coverage, and verify cleanup/assets.
- [x] Record results and material limitations, leave editor/scene state clean, and report the actual verification scope.

## Execution record

- User approved the full sequence approach and preparation delay in this chat.
- A test agent owns only the new regression harness; root owns production integration. An independent design audit identified launch acknowledgement, explicit reset cancellation, fall blending, and horizontal foot planting as required safeguards.
- The editor was closed; use GPU-enabled Unity batch runs rather than take over the desktop. Shell execution needs the environment's approved outside-sandbox route because sandbox startup reports a Codex-home registration conflict.
- RED: the unchanged production failed at 0.30 s after Space: grounded=False, rise=1.128 m. Saved baseline-failure.json and baseline.log under Temp/JumpSequenceVerification before production changes.
- First implementation run: three normal jump cases passed (takeoff 0.650 s, apex source 24.77, unchanged rise 1.142734 m, complete recovery to source 50, return to stable locomotion at 2.133 s). The ceiling case exposed an overstrict test expectation: the outgoing fall pose intentionally remains at 27.41 on early contact while the incoming separate landing clock starts at 30. Correct the collision-case observation, not the outgoing pose clock; forcing it to 30 would recreate the snap.
- A fresh review found two lifecycle defects. Tests reproduced same-frame movement reenable bypassing preparation, and disabled-Animator execution of a previously queued impulse. Retain the valid handler while movement alone is paused; revalidate owner/readiness/awaiting-takeoff at impulse execution. Both cases now pass, including fresh unanimated Space fallback.
- Support-loss testing exposed a fall clock catching up nearly three source frames in its first visible tick. Limit catch-up to the native source sampling rate, without changing transition durations or locomotion foot IK. The previously failing 38.7-degree adjacent knee change now passes the same continuity assertion. This identifies a clock warp contribution; it does not claim foot IK was independently proved to cause the snap.
- Final regression: 4 full jump cases plus 10 reset/disable/ledge/queued-launch/blocked-headroom scenarios passed, errors empty (2026-11-06T08:39:16 local). Existing integrated-player suite: 26 checks passed, errors empty (08:41:21 local). Full first-jump capture: 71 frames at 30 fps, 2.333 s including recovery/idle tail, exported to Development/character_integration/full-jump-preview.gif; contact sheet inspected alongside event captures.
- Final review minor (deferred): GirlPlayerIntegration's controller-missing regeneration branch still creates its historical hold graph. Ordinary reinstall returns the existing repaired controller, so current runtime is unaffected; a future recovery-tool update should bring that missing-asset path into agreement. No unrelated installer refactor was included.
- Rulings: keep planted horizontal movement during anticipation/recovery for this standing-jump source; preserve air steering and existing gravity/height. Existing source clip, Avatar, scene, water sequence references, and user BlendTree choices remain intact. No commit was created.
