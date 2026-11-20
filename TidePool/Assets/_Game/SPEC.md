---
id: tidepool-game
type: module-design
status: draft
title: 第一方演示与场景装配
parent: tidepool-unity
tags: [game, composition]
---

# 第一方演示与场景装配

## 职责

将研究用巨手与玩家、水流程及池厅表现绑定到明确的场景。当前演示入口是 `Scenes/PoolHall_ArmLab_01.unity`，数学实验另有 `GiantHand_ArcLab_01.unity`。场景负责装配，不把求解逻辑藏进资产名称或编辑器工具。

## 模块之间的边

- [tidepool-girl](Characters/GirlV06/SPEC.md) 从 [tidepool-player](Scripts/SPEC.md) 消费实际速度、跳跃确认和重置事件；头发再适配本地 Jiggle。
- [tidepool-gianthand](../../../docs/superpowers/specs/2026-11-02-gianthand-layered-ik-design.md) 采样真实玩家胶囊，通过玩家模块的拥有者接口锁定／搬运；不得把角色临时挂到手掌下代替碰撞处理。此为当前适配契约，不决定长期位移驱动选型。
- [tidepool-water-cycle](WaterCycle/SPEC.md) 控制水位与信件，通过玩家重置接口清理运动；波浪、波纹和水面查询交给水包。
- [tidepool-hall-presentation](MonumentHall/SPEC.md) 读取水流程状态改变灯光；[tidepool-wall-eye](WallEye/SPEC.md) 只依赖目标 Transform，不依赖抓取或洪水状态机。
- [tidepool-player](Scripts/SPEC.md) 中音效消费实际运动与水面查询；相机执行常规跟随，干区相机保护由水流程随后修正。
- [tidepool-editor](Editor/SPEC.md) 生成／验证场景及资产；专属角色和巨手工具留在各模块 Editor 目录，不成为运行时依赖。

## 装配约束

同一关节或状态的写入权必须明确；通过序列化引用绑定实际组件，不因短类名相同而删除或替换组件。不同场景保存的参数覆盖优先于 C# 默认值，修改默认不等于已修改场景。

**当前没有巨手完成后触发排水的场景连接。** `PoolWaterCycle` 有 UnityEvent 扩展点不代表已绑定；早期机关学习清单不是联动已实现的证据。新增联动需先确认并更新所属规范。

Park、Expanded、Dream、WaterCycle、Monument 是编辑器生成的场景变体，不是已实现的关卡切换系统。旧 WaterControlSwitch 等组件仍在部分旧场景生效，不能因主场景禁用就删除全局脚本。

## 非运行模块

`Assets/Database/schema.sql` 当前为空，未发现数据库／存档调用，不建立虚构的数据层规范。样例、Recovery 和 TutorialInfo 也不自动成为第一方业务边界。职责外资产的使用与保留遵守 [tidepool-sources](../../../sources/SPEC.md)。
