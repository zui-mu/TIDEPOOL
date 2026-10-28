# TidePool 开发记录与工具目录

迁移日期：2026-10-23。原位于 `D:/awestlakeselfstudy` 的四个 `unity_*` 文件夹已归档到本目录；文件清单和 SHA256 位于 `Migration/copied-files.json`。

## 哪些才是游戏项目

- `../Assets`：场景、材质、脚本、模型等正式资源。
- `../Packages`：资源包和依赖配置。
- `../ProjectSettings`：项目设置。
- `../Documentation/浮力与手写IK学习实操计划.md`：接下来逐步动手的完整计划。
- 本目录：开发截图、测试输出、备份、历史脚本快照和本机编辑器工具通信文件；不参与游戏构建。

不要把本目录拖进 Assets：里面有历史 .cs/.shader/.unity/.dll 快照，放进 Assets 会造成重复编译或误导入。Unity Hub 打开本目录的上一级 `TidePool`。

## 工具输出现在如何定位

八个编辑器工具通过 `Assets/_Game/Editor/TidePoolDevelopmentPaths.cs` 定位 `Application.dataPath/../Development`，不再写死 D 盘学习文件夹。以后移动整个项目，也会跟随新项目根目录。

四个子目录名保持不变，因此旧工具里的 command、截图和记录文件名可以继续沿用。已有历史脚本快照与备份中的 D 盘文字保留作为原始记录，不是当前生效路径。Unity 导入可能生成/更新 ready、status 文件，这是正常的开发记录。

## 备份与验证

- `Migration/OriginalEditorSources`：迁移前八个编辑器脚本。
- `Migration/scene-runtime-before.json`：场景与运行时 C# 的迁移前哈希。
- `Migration/unity-health.txt`：迁移后 Unity 编辑器域加载检查。
- `Migration/verification.json`：文件完整性、目录清理前确认结果。
- `unity_pool_lighting/BeforeLampLighting.unity` 与 `BeforeBrightStart.unity` 是备份，不是 Unity Hub 应打开的项目。
- 旧记录里对历史测试的 PASS 只代表当时版本；最近的明亮开场检查是 `unity_pool_lighting/transition-pass.txt`。

独立的 `shape_ik_project` 以及课程资料仍在 D 盘；当前游戏没有导入它、也不依赖该 Python 路径。后续将算法移植到 `Assets/_Game/Learning/Scripts`，不把 `.venv`、Python 缓存或整套学习目录塞进 Assets。
