# Jump Air Animation Implementation Plan

> 历史已撤回计划，不是执行授权。当前规范为 [tidepool-jump](../specs/2026-11-06-full-jump-sequence-design.md)，不得按下文任务重新安装截取方案。

**Status: reverted.** User video showed abrupt pose transitions. Restore complete Girl_Jump (frames 0..50), speed 1, and original entry/return blends 0.1/0.12 s. The numeric timing checks below are historical and do not establish a visually correct repair or the original issue's root cause. Further work is diagnostic only until evidence supports a cause.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Align the airborne pose sequence with the existing immediate physical jump.

**Architecture:** Add a non-looping FBX subclip using Unity's importer; bind it to AirborneHold and calibrate positive playback speed against the current jump duration. Movement code and scene ownership stay intact.

**Tech Stack:** Unity 6000.6, ModelImporter, AnimatorController.

**Spec:** docs/superpowers/specs/2026-11-06-jump-air-animation-design.md

## Global Constraints

- Work in the current open Unity checkout and preserve pre-existing dirty files.
- Keep the original Girl_Jump, immediate Space response, current jump height/gravity, and Grounded conditions.
- Make only animation importer/controller production changes; temporary diagnostic tooling must be removed.
- No commit, push, scene replacement, or movement-script changes.

## Review Focus

- Source jump is staggered: determine actual foot-contact boundaries rather than assuming generic frame numbers.
- Keep the new clip's imported identifier valid and preserve the original clip's identity.
- Confirm positive speed, no looping, and matching apex/landing timing.
- Verify actual movement, grounding, scene dependencies, and restored editor state.

### Task 1: Configure and verify the airborne excerpt

**Files:** Modify Jump.fbx.meta and GirlV06_Locomotion.controller under TidePool/Assets/_Game/Characters/GirlV06/Animations.

- [x] Snapshot the current files; measure source foot contacts and capture the current configuration.
- [x] Use Unity's importer to add Girl_JumpAir, bind it, calibrate speed, and set fixed entry/return blends.
- [x] Verify imported assets and sample poses; run the existing integrated-player test and inspect results.
- [x] Request a focused independent diff review, remove temporary tooling, and confirm no unrelated edits.

Configuration-only change: verify real importer/controller behavior and existing runtime checks instead of adding a test that merely mirrors serialized values.

Progress: Source hierarchy/curve analysis establishes frames 19..31 as the practical last-contact/first-contact boundaries. Source hips apex at frame 25 is exactly halfway. Imported length 0.4 s implies speed approximately 0.57735. Backups and transient diagnostic outputs live under Unity's ignored Temp/JumpAnimationFix directory. The active scene was clean and outside Play before validation.

Completed: Unity imported Girl_JumpAir with local ID 4061839508520268033 while preserving the original full clip ID. Playback speed is 0.57735044; entry/return blends are 0.03/0.08 s. Baseline and final integrated-player checks both passed 25 checks with no errors; jump rise stayed 1.142734 m. Runtime apex sampling moved from original source frame about 9.5 to about 24.5 (source apex 25), and animation time progressed monotonically through landing back to locomotion. The initial unfocused baseline run failed at walking because InputSystem routed synthetic keyboard input away from Game View; two input focus settings were temporarily overridden only by diagnostic tooling during subsequent test runs, restored on play exit, and the tooling was removed. Source FBX, scene, and both movement/animation scripts match their initial hashes. Focused independent snapshot review found no actionable issues. See Development/character_integration/jump-air-verification.json under the Unity project.
