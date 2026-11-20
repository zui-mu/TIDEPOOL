---
id: tidepool-water-cycle
type: submodule-design
status: draft
title: 水位与信件流程
parent: tidepool-game
depends-on: [tidepool-player, tidepool-water-package]
tags: [water, interaction]
---

# 水位与信件流程

## 职责与现状

PoolWaterCycle 拥有场景的水位流程，不重写外部水包的波浪模拟。当前状态为 Pool → Draining → Empty → Filling → Flooded：靠近控制点按 E 开始排水；Empty 后拾取信件触发回水／上涨，不是再按一次 E 切换。

此状态机来自源码核对；更广的叙事含义和与巨手的玩法联动未在本轮另行定案，不从场景效果推断新产品意图。

## 对外表面与边界

TryInteract、BeginFloodFromLetter、ResetSequence 和 CurrentPhase／Elapsed／WaterHeight 是流程表面。UnityEvent 为扩展点，不表示已有监听者；其他系统读取状态不应各自改写流程阶段。

PoolLetterPickup 处理拾取、阅读和临时暂停玩家／相机，关闭时恢复原 enabled 状态；阅读时水位仍推进。重置先清玩家运动再复位出生位置；与抓取锁或阅读暂停交错时不能假定所有恢复已验证。

CeilingSeepage 按流程产生滴水并调用水包 AddRipple；FloodSurfaceState、FloodAtmosphere 负责湿润与氛围表现，不决定抓取成功或移动算法。DryRegionCameraGuard 在普通相机后处理干区相机位置及显隐。

## 参数与验证限制

水位高度来自场景序列化配置，WaterCycle、Monument、ArmLab 不同，不能用一个旧场景的硬编码高度验收全部场景。当前流程通过移动 waterFrame 改水位，不能据此宣称实现了整个房间的流体物理。

旧 PoolWaterCycleEditor 有“第二次 E 回水”的断言，与当前拾信流程冲突；这是应修订的测试偏差，不是旧断言仍为设计真相。本次只记录，不更改测试或代码。
