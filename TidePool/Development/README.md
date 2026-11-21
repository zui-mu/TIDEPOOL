# TidePool 开发记录与工具目录

迁移日期：2026-10-23。原位于 `D:/awestlakeselfstudy` 的四个 `unity_*` 文件夹已归档到本目录；文件清单和 SHA256 位于 `Migration/copied-files.json`。

## 哪些才是游戏项目

- `../Assets`：场景、材质、脚本、模型等正式资源。
- `../Packages`：资源包和依赖配置。
- `../ProjectSettings`：项目设置。
- [项目目标与需求](../../goal-and-requirements.md) 和 [Spec 索引](../../docs/README.md)：当前设计入口。旧《浮力与手写IK学习实操计划》未在仓库中，不再作为可访问入口。
- 本目录：开发截图、测试输出、备份、历史脚本快照和本机编辑器工具通信文件；不参与游戏构建。

不要把本目录拖进 Assets：里面有历史 .cs/.shader/.unity/.dll 快照，放进 Assets 会造成重复编译或误导入。Unity Hub 打开本目录的上一级 `TidePool`。

## 工具输出现在如何定位

通用场景截图和快照工具通过 `Assets/_Game/Editor/TidePoolDevelopmentPaths.cs` 定位 `Application.dataPath/../Development`。输出随项目根目录移动。

四个 `unity_*` 子目录保留当年的截图、命令记录和备份。五版旧池厅及其专用生成、灯光和水位工具已退役。现用检查入口见[测试指南](../../docs/testing.md)。仍有消费者的 command/request 文件只用于本机测试。

## 备份与验证

- `Migration/OriginalEditorSources`：迁移前八个编辑器脚本。
- `Migration/scene-runtime-before.json`：场景与运行时 C# 的迁移前哈希。
- `Migration/unity-health.txt`：迁移后 Unity 编辑器域加载检查。
- `Migration/verification.json`：文件完整性、目录清理前确认结果。
- `unity_pool_lighting/BeforeLampLighting.unity` 与 `BeforeBrightStart.unity` 是备份，不是 Unity Hub 应打开的项目。
- 旧记录里对历史测试的 PASS 只代表当时版本；最近的明亮开场检查是 `unity_pool_lighting/transition-pass.txt`。

历史记录中的独立 `shape_ik_project` 和 D 盘课程路径不是当前运行依赖。当前巨手数学位于 `Assets/_Game/GiantHand/Math`；旧的 Learning/Scripts 移植描述不再作为现行路径承诺。不要把 `.venv`、Python 缓存、历史快照或整套学习目录塞进 Assets。
