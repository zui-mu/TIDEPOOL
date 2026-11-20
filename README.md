# TIDEPOOL

Unity project for TidePool, including the pool hall scene, water rendering assets, and GiantHand arm IK / finger FK controls.

## Open the project

1. Install Unity **6000.6.7f1** through Unity Hub.
2. Install Git and Git LFS, then run `git lfs install`.
3. Clone this repository and, if needed, run `git lfs pull`.
4. In Unity Hub, add the **TidePool/** folder (the repository root is one level above the Unity project).
5. Let Unity restore packages and regenerate the Library folder.
6. Open `Assets/_Game/Scenes/PoolHall_ArmLab_01.unity` to inspect the giant hand prototype.

## Repository contents

- `TidePool/Assets/`: scenes, scripts, materials, assets, and their `.meta` files.
- `TidePool/Packages/`: package manifests and embedded packages.
- `TidePool/ProjectSettings/`: Unity project configuration.
- Existing project documentation, development files, and backup scenes are preserved.

Unity caches, temporary files, logs, per-user settings, and generated IDE project files are excluded by `.gitignore`. Binary art/audio/model files use Git LFS. Scene, prefab, script, and metadata files remain normal Git files.

## Save changes

From the repository root:

```sh
git status
git add .
git commit -m "Describe your changes"
git push
```

Commit asset `.meta` files together with the assets so that Unity GUID references stay intact. Close Unity or wait for scene and asset saves to finish before making a commit. Before working on another computer, run `git pull` and `git lfs pull`.

## Restore after cloning or reinstalling Windows

- Open `TidePool/` in Unity Hub, not the repository root. Use the version recorded in `TidePool/ProjectSettings/ProjectVersion.txt` (currently 6000.6.7f1).
- Run `git lfs install` and `git lfs pull` in the repository root so model and texture files contain the actual binary content.
- The water system is already embedded at `TidePool/Packages/com.abstractocclusion.webgpuwater/`. Its lock-file source is `embedded`, using the project-relative path `file:com.abstractocclusion.webgpuwater`; do not replace it with an old computer's absolute download/cache path.
- Unity restores registry dependencies from `Packages/manifest.json` and regenerates `Library/`. Asset Store download-cache folders are not runtime resource paths for this project.
- Open `Assets/_Game/Scenes/PoolHall_ArmLab_01.unity`. This is the player/giant-hand scene and is the configured startup scene. `Untitled` and `SampleScene` do not contain that gameplay setup.
- The embedded water package includes a Unity 6.6 EntityId compatibility fix. Preserve these source changes when moving or updating the project.
- On an x64 Windows installation, if Bee reports `HostPlatform architecture detection failure []`, verify the **system-level** environment variable `PROCESSOR_ARCHITECTURE` is `AMD64`. This editor's Bee reads Machine scope; a project `.env.local` or process-only value does not replace it.
- The existing `Tools > Girl V06 > 6 - Test integrated player` command verifies the saved ArmLab scene in Play mode. It writes its report under `TidePool/Development/character_integration/` and exits Play when finished.
