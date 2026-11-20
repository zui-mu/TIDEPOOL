---
id: tidepool-girl
type: submodule-design
status: draft
title: GirlV06 角色表现
parent: tidepool-game
depends-on: [tidepool-player, tidepool-jiggle]
tags: [character]
---

# GirlV06 角色表现

## 职责

把玩家运动状态呈现为女孩的 Humanoid 动画、视觉朝向、肩部修形与双辫动态；保持原 FBX、蒙皮与源动画的来源可追溯。场景实例的主控覆盖与源 prefab 的独立预览用途分开。

## 所属规范与边界

- [tidepool-jump](../../../../../docs/superpowers/specs/2026-11-06-full-jump-sequence-design.md) 拥有完整跳跃时钟与实际起跳确认；其子节点 tidepool-moving-jump 拥有移动跳规则。
- [tidepool-hair](../../../../../docs/superpowers/specs/2026-11-06-girl-braid-dynamics-design.md) 拥有当前 Jiggle 适配、绑定和重置；旧自写弹簧不能同时写发骨。
- GirlShoulderCorrectives 负责肩部姿态修形，GirlPlayerAnimation 根据真实速度驱动走跑和视觉子对象转向；GirlAnimationPreview 是独立验证用途，不代替主控桥。

## 当前接入事实

ArmLab 使用 Player/GirlVisual；CharacterController 是碰撞代理，视觉网格不替代玩家胶囊。水体 rendererOverride 指向 Body，不使用隐藏胶囊计算角色可视水体交互。

当前动画桥关闭 Root Motion，读取玩家模块的实际速度，通过跳跃处理器请求冲量；实际锁定会被视为 carried，抑制舞蹈／空中切换。这里只描述现状，**长期世界位移驱动仍未决**，见 tidepool-goals。头发和四肢的骨骼运动并未被禁止。

带相应控制器参数时，T 在接地静止状态切换 Salsa，移动、起跳或被抓会终止；这是当前演示行为，不扩展为正式舞蹈系统或菜单需求。

## 资产与验证

导入检查负责 Avatar、网格和运行组件依赖；集成回归负责真实运动；跳跃和发辫各有专项验证。安装／重建工具可能保存场景和控制器，不能当作只读检查。旧重建分支和旧回归断言不一定已跟上当前缩短的移动跳规则。

[PLAYER_INTEGRATION.md](PLAYER_INTEGRATION.md) 保留接入证据与旧实测，不另拥有设计要求；当前操作及验证方法见 [测试指南](../../../../../docs/testing.md)。
