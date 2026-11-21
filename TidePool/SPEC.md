---
id: tidepool-unity
type: module-design
status: draft
title: Unity 工程与依赖装配
parent: tidepool-architecture
depends-on: [tidepool-jiggle]
tags: [unity, integration]
---

# Unity 工程与依赖装配

## 职责

本目录才是 Unity Hub 应打开的工程；仓库根还包含文档和必须随克隆保留的本地依赖。版本以 `ProjectSettings/ProjectVersion.txt` 为准，当前为 **6000.6.7f1**。第一方场景装配由 [tidepool-game](Assets/_Game/SPEC.md) 管理。

## 导入与运行边界

- `Assets/`、`Packages/`、`ProjectSettings/` 以及资产 `.meta` 是工程源；Library、Temp、Logs、UserSettings 等是可再生产物。不能把 Development 或历史备份拖入 Assets。
- 正式构建场景列表以 `Assets/_Game/Scenes/PoolHall_ArmLab_01.unity` 为启动场景，随后注册独立第二关 `Assets/_Game/Scenes/Bathhouse_02.unity`。两关的通行与装配要求由[第一方场景规范](Assets/_Game/SPEC.md#泳池通往公共澡堂)管理。
- 项目使用 URP 和新版 Input System。当前第一方直接读 `Keyboard.current`／`Mouse.current`；不能假定 `InputSystem_Actions.inputactions` 已接管所有输入，也不能仅凭存在多人相关依赖就宣称支持联网。
- UPM 声明看 manifest，解析结果看 lock，两者版本字段可能不同；更新依赖时应核对 Unity 实际解析，不根据文档数字擅自降级内置包。

## 实际依赖关系

[tidepool-game](Assets/_Game/SPEC.md) 消费 [tidepool-water-package](Packages/com.abstractocclusion.webgpuwater/SPEC.md) 和 [tidepool-jiggle](../sources/JigglePhysics/SPEC.md)。水包在本目录嵌入；Jiggle 由 `file:../../sources/JigglePhysics` 引用，单独复制 TidePool 文件夹不足以恢复它。

水包保持 embedded／项目相对路径，不替换为某台机器的下载缓存绝对路径。第一方 Runtime 不依赖 UnityEditor；编辑器工具仅在 Editor 目录运行。

## 验证边界

Unity 版本、包解析、场景导入与 Play 检查是不同检查。没有实际启动对应编辑器时，只能报告静态核对。操作入口见 [测试指南](../docs/testing.md)；历史报告不能代替当前编译、场景和画面验证。

工作分支推送时检查命名、差异格式、变更的 Unity 资产与元数据配对及 Git LFS 指针。面向主分支的 PR 还需按仓库模板填写四段正文。PR 和主分支推送在独立检出中，用工程指定版本导入并编译、执行巨手数学断言和正式场景 Play Mode 回归。角色覆盖走跑、跳跃与碰撞，水位覆盖排水、拾信、涨水及 R 键复位。每项检查以本次运行的退出状态和结果为依据，并保留失败日志供审查。
