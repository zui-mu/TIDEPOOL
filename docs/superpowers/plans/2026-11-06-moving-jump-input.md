# Input-selected moving jump Implementation Plan

> 历史实施记录，不是当前执行授权；[tidepool-moving-jump](../specs/2026-11-06-moving-jump-input-design.md) 已明确采用速度分类和缩短缓冲。下文输入锁定方案、旧分支名与测试结果仅属于当时，新分支遵守根协作指南。

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Preserve continuous horizontal movement when Space is accepted with net directional input, while retaining the existing stationary jump.

**Architecture:** Pass net input intent across the existing jump handler. Latch it once in GirlPlayerAnimation and gate only its grounded suppression. Reuse the existing movement integration and full-clip clocks.

**Tech Stack:** Unity6000.6.7f1, C#, CharacterController, Input System, existing Editor-driven Play Mode regression.

**Spec:** docs/superpowers/specs/2026-11-06-moving-jump-input-design.md

## Global Constraints

- Verify GitHub backup before any implementation: origin/codex/backup-before-moving-jump-20261106 at204c370.
- Keep the source jump0..50, phase landmarks19/25/30, current acceleration, jump height and gravity.
- Modify no hair, water gameplay, Avatar or persistent scene assets.
- Work in the current Unity checkout on codex/moving-jump-input; keep the backup branch unchanged.
- All movement and collision displacement remain in PlayerMovement.

## Review Focus

- Direction plus Space in the same frame must classify as moving before actual speed exists.
- Opposing keys cancel; later input must not reclassify a stationary request.
- Releasing/resuming movement must brake/accelerate without changing type or a one-frame launch/landing clamp.
- Walls must block/slide while still allowing a jump; lost support must never fire a stale launch.
- Reset, component disable and normal completion must clear moving state before a fresh stationary jump.

---

### Task1: Implement and verify input-selected moving jumps

**Files:**
- Modify: TidePool/Assets/_Game/Scripts/PlayerMovement.cs
- Modify: TidePool/Assets/_Game/Characters/GirlV06/Runtime/GirlPlayerAnimation.cs
- Modify: TidePool/Assets/_Game/Characters/GirlV06/Editor/GirlJumpSequenceRegression.cs
- Create: TidePool/Assets/_Game/Characters/GirlV06/Editor/GirlJumpSequenceRegression.Moving.cs and.meta
- Update: TidePool/Assets/_Game/Characters/GirlV06/PLAYER_INTEGRATION.md

**Interfaces:**
- Consumes: the existing real-character runner, Keyboard state events, full jump controller and actual PlanarVelocity samples.
- Produces: IPlayerJumpHandler.HandleJumpRequest(bool hasMovementInput), with input captured only for accepted jumps; moving regression results and visual proof.

- [x] Add a partial moving regression and targeted `moving` request mode. Use real movement and Animator. Assert held-run preparation/launch/recovery speeds exceed3.3m/s; walk exceeds1.2m/s; simultaneous W+Space accelerates without reaching fixed run speed; opposing/late input stays planted; release/resume has no discontinuity; walls block/slide; support loss before frame19 never launches; reset permits a fresh stationary jump.
- [x] Refresh Unity and run `moving` against unchanged production. Expected: FAIL held-running continuity because preparation speed is zero. Save RED evidence separately before the next run.
- [x] Change the handler to accept net-input intent. Latch moving type at accepted request; condition grounded suppression on stationary type; clear type on cancellation and completion. Keep velocity integration and source animation intact.
- [x] Refresh and run full `test`. Expected: all original4 jump and10 scenario cases pass together with every moving case; no compilation or runtime error.
- [x] Inspect moving video frames and existing stationary proof; report visible asset limitations honestly. Run existing GirlPlayerIntegrationTest entry, verify scene remains clean, and check git diff.
- [x] Review the final diff and commit only this feature's code, tests, docs and proof artifacts. Keep the pre-change backup ref unchanged.

## Execution record

Approved scope comes from the conversation; proceed directly after backup. The installed executing-plans skill refers to absent task-start/task-done helper scripts, so record RED/GREEN and review evidence in TidePool/Library/MovingJumpImplementation/progress.md instead. One combined task has no inter-task interface dependencies.
## Final evidence

- RED: held-run anticipation measured0m/s against unchanged production.
- GREEN:23/23 real-component jump cases pass;26/26 legacy integration checks pass; errors=[].
- Visual:64 full-body frames inspected as a contact sheet and exported as moving-jump-preview.gif. Existing planted-foot motion can slide while translating; no new animation asset was added.
- Independent review: no Critical/Important production findings. Deferred minor: capture mode timeout records an error but does not advance until the global timeout; normal fail-fast tests are unaffected.
- Editor final state: playing=false, compiling=false, dirty=false.
- Keep feature on codex/moving-jump-input in the current checkout; pre-change backup remains on GitHub.
