# 文档与 Spec 导航

## 如何使用

设计事实源从 [目标与需求](../goal-and-requirements.md) 和 [系统架构](../architecture.md) 向下展开。`id` 是稳定身份，`parent` 是归属，`depends-on` 是真实依赖，`references` 是关联；仅带 `id`／`type` frontmatter 的文件是 Spec 节点。

本轮 20 个节点均为 `draft`，等待用户整体审阅，不因访谈确认了个别决定就自动标为 active。架构决策／意图／契约先在所属 Spec 获得确认和更新，再实施；代码只作为核对证据。未决的角色位移驱动不由文档整理者代选。

## Spec 树

- [tidepool-goals — 目标与需求](../goal-and-requirements.md)
  - [tidepool-architecture — 系统架构](../architecture.md)
    - [tidepool-unity — Unity 工程与依赖装配](../TidePool/SPEC.md)
      - [tidepool-game — 第一方演示与场景装配](../TidePool/Assets/_Game/SPEC.md)
        - [tidepool-player — 玩家基础、相机与声音](../TidePool/Assets/_Game/Scripts/SPEC.md)
        - [tidepool-girl — GirlV06 角色表现](../TidePool/Assets/_Game/Characters/GirlV06/SPEC.md)
          - [tidepool-jump — 完整跳跃](superpowers/specs/2026-11-06-full-jump-sequence-design.md)
            - [tidepool-moving-jump — 移动跳分类与缓冲](superpowers/specs/2026-11-06-moving-jump-input-design.md)
          - [tidepool-hair — 双辫子 Jiggle 适配](superpowers/specs/2026-11-06-girl-braid-dynamics-design.md)
        - [tidepool-gianthand — 巨手分层设计](superpowers/specs/2026-11-02-gianthand-layered-ik-design.md)
          - [tidepool-gianthand-math — 纯数据数学](../TidePool/Assets/_Game/GiantHand/Math/SPEC.md)
          - [tidepool-gianthand-rig — 骨架适配与显示](../TidePool/Assets/_Game/GiantHand/Runtime/SPEC.md)
          - [tidepool-auto-grasp — 自动抓取与几何](../TidePool/Assets/_Game/GiantHand/AutoGrasp/SPEC.md)
        - [tidepool-water-cycle — 水位与信件](../TidePool/Assets/_Game/WaterCycle/SPEC.md)
        - [tidepool-hall-presentation — 池厅灯光与渲染](../TidePool/Assets/_Game/MonumentHall/SPEC.md)
        - [tidepool-wall-eye — 墙眼表现](../TidePool/Assets/_Game/WallEye/SPEC.md)
        - [tidepool-editor — 通用编辑器工具](../TidePool/Assets/_Game/Editor/SPEC.md)
      - [tidepool-water-package — WebGpuWater 嵌入包](../TidePool/Packages/com.abstractocclusion.webgpuwater/SPEC.md)
    - [tidepool-sources — 原始资源与来源](../sources/SPEC.md)
      - [tidepool-jiggle — Jiggle Physics 本地包](../sources/JigglePhysics/SPEC.md)

原地采纳的四份设计保留历史路径；不为它们另建重复规范。其他新模块 Spec 放在所属目录旁。目录职责边界不等于已新增 asmdef 编译隔离。

## 操作与协作入口

- [README](../README.md)：克隆、打开工程、当前演示操作。
- [CONTRIBUTING](../CONTRIBUTING.md)：分支命名、提交、四段 PR 正文、LFS、忽略与证据规则；[GitHub PR 模板](../.github/PULL_REQUEST_TEMPLATE.md) 固定正文结构。
- [AGENTS](../AGENTS.md)：代理执行边界与反向提问规则。
- [测试指南](testing.md)：实际工具入口、运行副作用与验证缺口。
- [Development 说明](../TidePool/Development/README.md)：输出、历史脚本与迁移记录。
- [资源来源](../sources/README.md)：原件、清单与本地依赖位置。

## 历史资料的效力

- `superpowers/specs/` 的四个已采纳节点是现行设计草案；[jump-air 截取方案](superpowers/specs/2026-11-06-jump-air-animation-design.md) 已撤回，只有历史证据价值。
- `superpowers/plans/` 是各次实施记录，不是与 Spec 并列的事实源。其中 [Jiggle 接入计划](superpowers/plans/2026-11-07-jiggle-integration.md) 保存明确批准的替代设计来源，现行契约已归 tidepool-hair。
- [自动抓取 v0.1](../TidePool/Documentation/自动抓取_需求分析与系统边界_v0.1.md) 与 [巨手机关学习清单](../TidePool/Documentation/巨手机关逐步制作与学习清单.md) 保留早期范围讨论，不能覆盖新裁决或当作完成清单。
- [PLAYER_INTEGRATION](../TidePool/Assets/_Game/Characters/GirlV06/PLAYER_INTEGRATION.md) 是按日期积累的接入／实测记录，不是当前参数表；移动跳、头发和启动场景已有变化。
- [Jiggle 快照](backups/2026-11-07-jiggle-snapshot.md)、Development 报告／备份以及 work 图示是证据或参考，不是当前版本验收。
- 第三方 README／手册保留上游身份；当前 TidePool 集成差异看包级 Spec，不能照上游安装说明覆盖本地依赖。

旧文中的盘符和时间只解释当时环境；现工程路径由 manifest、ProjectSettings 和当前工具确定。本轮不移动历史文件、不删除备份，也不将未完成研究要求静默取消。
