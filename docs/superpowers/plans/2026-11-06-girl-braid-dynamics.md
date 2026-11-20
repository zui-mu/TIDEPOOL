# Girl braid dynamics Implementation Plan

> 历史实施记录：自写弹簧已被 Jiggle 替代，现行契约见 [tidepool-hair](../specs/2026-11-06-girl-braid-dynamics-design.md)。下文任务、分支名和约束仅代表当时，不是当前执行授权；新工作遵守根协作指南。

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans task-by-task. Steps use checkbox syntax.

**Goal:** Add restrained twin-braid secondary motion using the existing eight weighted bones.
**Architecture:** Independent post-animation spring/constraint component with data-only body proxies; one movement reset event. Scene-only configuration and editor setup, no source rig edits.
**Tech Stack:** Unity6000.6.7f1, C#, Transform, Animator, existing Editor request/Play Mode verification pattern.
**Spec:** docs/superpowers/specs/2026-11-06-girl-braid-dynamics-design.md

## Global Constraints
- Preserve b5f6fc7 movement/jump behavior, source FBX/weights/animations and unrelated dirty status files.
- Only hair rotations are written; roots remain attached and lengths/scales unchanged.
- Work in the current loaded checkout on codex/girl-braid-dynamics.
- No third-party dependencies, gameplay Rigidbody/colliders, hair self-collision, wind or water drag.
- Every Unity API import/refresh result must be observed; old reports are not fresh evidence.

## Review Focus
- Physics must never become the next-frame animation reference; disable/reset restores reference.
- Late execution, rotating/scaling head and frame-rate changes must not detach roots or accumulate energy.
- Teleport/reset notification occurs before teleport; rebuilding must wait until the final pose.
- Degenerate capsule contacts must stay finite, and collision projection must not stretch bones.
- Editor setup and validation must preserve clean scene/prefab ownership and restore temporary test settings.

---

### Task1: Spring/constraint component and reset contract
**Files:** New Runtime/GirlHairDynamics.cs, Runtime/GirlHairCollisionProxy.cs, Editor/GirlHairDynamicsRegression.cs and.meta; modify Assets/_Game/Scripts/PlayerMovement.cs.
**Interfaces:** GirlHairDynamics.ConfigureFromHierarchy(), RequestReset(); private simulation step consumes deltaTime and reference transforms. Serializable GirlHairCollisionProxy holds anchor/local capsule ends/radius and projects a hair segment. PlayerMovement.MotionReset notifies the component.
- [x] Write reflection-based real-Transform regression and rig inspection before core implementation; compile it and observe missing-dynamics baseline failure.
- [x] Implement the two runtime files and reset event. Cache reference pose, preserve fixed roots/lengths/rest twist, apply bounded semi-implicit spring substeps and iterative bend/collision constraints; restore bounds/pose on disable.
- [x] Run deterministic tests through actual component methods at30/60/120fps; inspect failures, settle and reset behavior until all pass.

### Task2: Actual-character setup, capture and regression
**Files:** New Editor/GirlHairDynamicsEditor.cs and.meta; extend hair regression with actual Play Mode; configure Assets/_Game/Scenes/PoolHall_ArmLab_01.unity; update PLAYER_INTEGRATION.md and Development/character_integration hair reports/preview.
**Interfaces:** Consumes core configuration/step/reset and current GirlVisual rig; produces serialized scene setup, inspector tools, actual-motion verification.
- [x] Verify missing scene component before installation, then bind chains and measured virtual tips/radii; tune conservative body proxies and save the initially clean ArmLab scene.
- [x] Exercise real walking/stopping/running/jumping/turning/reset; capture close-up frames and validate finite motion, attachment, length, reasonable amplitude and proxy clearance. Tune using evidence.
- [x] Run existing23 jump and26 integration checks; inspect close-up visual proof, confirm Play stopped/scene clean, perform independent review, document limitations and commit this feature only.

## Execution record
Conversation approval authorizes direct execution. Use TidePool/Library/HairDynamicsImplementation/progress.md as the execution ledger because installed task-start/task-done helpers are absent. Task2 consumes Task1's configuration and reset contract. Preserve runtime evidence for review.


## Final evidence
- Missing-feature and missing-scene-component baselines observed before implementation/installation.
- Core9/9: inertia, pinned roots/lengths, settling,30/60/120fps, deferred movement reset, zero/long frames, disable, live binding recovery, uniform scaling, capsule degeneracy.
- Actual15/15 combined checks pass with164close-up frames; status query during recording preserves capture state. Max tip response approximately1.8cm; maximum simplified-proxy overlap approximately8.1mm; root error approximately0.0043mm; local bone translations unchanged; reset error approximately1.6mm.
- Existing23/23 jump and26/26 integration checks pass after final runtime changes. No errors.
- Independent review of exact source payloads covered runtime, setup and capture cleanup. Two findings (mutable live bindings and status overwriting capture state) were regraded as required protections, reproduced RED and verified GREEN. No hair findings deferred.
- Reviewed actual close-up frames and exported Development/character_integration/hair-dynamics-preview.gif. Collision remains an approximation for broad hair masses; no wind, water drag or braid self-collision.
- Editor final state: playing=false, compiling=false, dirty=false. Source FBX, weights and animation assets unchanged. Unrelated generated status changes retained outside feature commit.
- Scene-save Light format migrations were restored only when exactly semantically equal to the prior scene; new hair configuration/references remain.
