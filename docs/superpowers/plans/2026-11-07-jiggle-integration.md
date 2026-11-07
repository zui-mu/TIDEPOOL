# Jiggle Physics integration plan

Approved design: use Jiggle Physics 16.0.1 for the existing eight braid bones. Preserve the character FBX, skinning and movement. Natural gravity-settled resting hair is required before visible movement. Run after shoulder corrections; reset must clear prior inertia.

- [x] Preserve the existing scene and package manifest; retain MIT license and pinned source archive SHA256.
- [x] Add the local UPM package and compatible Mathematics dependency without downgrading built-in Collections/Burst; verify compilation.
- [x] Run a failing real-rig check for the missing Jiggle adapter.
- [x] Add a small adapter using the library's public JiggleJobs solver, existing bone/collision metadata, bounded substeps and library-driven warm-up. Never reimplement the solver.
- [x] Configure only ArmLab GirlVisual, disable the old solver, and save the scene after verifying there are no unsaved edits.
- [ ] Check actual-rig gravity sag, movement response, skeleton lengths/root attachment, reset, frame-rate behavior and solver cost. Run existing movement regression and review the rendered result.

Ruling: reuse current checkout because this is the active Unity project; preserve unrelated generated status changes. The prior discussion and user's explicit download/configure/test instruction approve this design and inline execution.
Verification ledger: RED missing adapter; RED owned-tree reset disposal; GREEN real rig gravity/response/reset; RED actual walk overswing; airDrag restored to upstream default zero and local damping .25; RED body proxies disabled; explicit enable and real endpoint nodes added; GREEN real rig including runtime collision-state assertion. Final full Play capture pending. Independent ownership review of supplied source snippets found no critical/important issue. Full upstream review was not performed.
