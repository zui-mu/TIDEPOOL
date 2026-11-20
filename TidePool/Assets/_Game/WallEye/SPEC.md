---
id: tidepool-wall-eye
type: submodule-design
status: draft
title: 墙眼注视表现
parent: tidepool-game
tags: [presentation]
---

# 墙眼注视表现

## 职责与边界

WallEyeLook 通过显式目标 Transform、距离滞回、反应延迟和角度限制产生眼睛朝向。它是独立表现组件，不拥有玩家位移、抓取判定或水位状态。

基础 prefab 可没有目标；ArmLab 的场景实例将 target 覆盖为 Player。不能因 prefab 为空就自动扫描全局同名物体重绑，也不能把视觉注视解释为已实现的敌人 AI。

## 验证

检查目标缺失、距离阈值附近、限角和启停后的姿态；确认多实例使用各自场景引用。不在本轮补出未确认的攻击、感知或关卡规则。
