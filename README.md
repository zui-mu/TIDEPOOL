# TIDEPOOL

Unity project for TidePool, including the pool hall scene, water rendering assets, and GiantHand arm IK / finger FK controls.

## Open the project

1. Install Unity **6000.3.26f1** through Unity Hub.
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
