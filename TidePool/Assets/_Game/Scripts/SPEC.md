---
id: tidepool-player
type: submodule-design
status: draft
title: 玩家基础与场景输入
parent: tidepool-game
depends-on: [tidepool-water-package]
tags: [player]
---

# 玩家基础与场景输入

## 职责与当前实现

PlayerMovement 处理键盘移动、加减速、重力、实际碰撞位移与运动状态；CameraFollow 处理轨道、缩放和避障；PlayerSfx 根据真实运动与水体状态发声。泳池首关和恢复场景仍保存旧 WaterControlSwitch／PoolWetnessController 的禁用组件。正式水位流程由 PoolWaterCycle 驱动。

**长期世界位移驱动尚未决定。** 当前由 PlayerMovement 调用 CharacterController.Move；这是实现记录，不是用户已批准的永久排他选型。动画有四肢运动，头发有相对角色的动态，二者都随角色发生世界位移。

## 现有接口契约

- PlanarVelocity、VerticalSpeed、IsGrounded、HasMotionSample、HitCeiling 和 JumpLaunchSequence 描述实际运动及确认，不应由动画猜测输入代替。外部消费者应区分尚无运动样本与静止。
- IPlayerJumpHandler 接受／取消请求、声明地面运动限制及是否允许实际起跳；QueueAnimatedJump 只接受已注册处理器的一次待执行请求。源帧和动作阶段由 [tidepool-jump](../../../../docs/superpowers/specs/2026-11-06-full-jump-sequence-design.md) 拥有。
- TryLockMovement 使用单一非空 owner；同 owner 幂等，不能抢占其他 owner。锁定清理运动；ReleaseMovementLock 只能释放自己的锁。
- 当前 internal TryMoveWhileLocked 仅接受锁拥有者的有限目标世界高度，经 CharacterController 做竖直位移并返回碰撞标志；不是任意传送或公开的通用搬运服务。抓取使用方式见 tidepool-auto-grasp。
- ResetMotion 清理待执行起跳、速度与样本，并发布 MotionReset。调用方若要传送，附属动态必须在最终位置重建，不能拿事件发出时的旧位置作为最终位置。

## 相机与声音边界

CameraFollow 负责常规镜头及近距 ShadowsOnly；水流程的 DryRegionCameraGuard 随后修正干区相机并独立控制 forceRenderingOff。两种显隐机制不能混为同一开关。PlayerSfx 读水包 WaterVolume／WaterExclusionVolume 判断水面和干区，不写水位状态。

本目录不拥有巨手求解、角色动画时钟、发辫求解或信件剧情。目录内组件没有因此获得关闭其他系统或改写其配置的权限。

## 验证与风险

锁持有／重复锁定／错误拥有者释放由巨手数学检查覆盖部分用例；走跑跳和镜头另经真实角色集成检查。水流程重置当前可直接复位角色 Transform，是有意区分的重置路径，不可据此宣称所有世界位移都已通过同一入口。
