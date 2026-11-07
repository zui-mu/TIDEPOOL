# Project snapshot — 2026-11-07

User accepted the current preview and requested a GitHub backup. This is a snapshot of the current state, not a claim that every validation passed.

- Unity 6000.6.7f1; ArmLab uses GirlJiggleHair backed by local Jiggle Physics v16.0.1 in sources/JigglePhysics.
- Preserve original source archive, MIT license, package dependencies, project adapters, scene configuration, preview recording and validation reports.
- Upstream compatibility/ownership patches and optional constraint iterations are documented in sources/JigglePhysics/TIDEPOOL-PATCHES.md.
- Latest complete hair report has 15 cases: 14 passed, one body-proxy collision-clearance case failed. Maximum proxy overlap is 0.06811218 m. Gravity settling, motion, attachment, bone positions and reset checks passed. This limitation remains in this backup.
- Existing jump regression report status is passed. No further gameplay changes or tests were started for this backup request.
- Preserve current Unity scene serialization and generated status-file changes. Two trailing spaces in Unity-generated m_Name fields are retained as part of the exact snapshot.
- Unity Library/Temp/Logs caches are excluded by .gitignore. Required external local-package source is tracked under sources/, outside the Unity cache.