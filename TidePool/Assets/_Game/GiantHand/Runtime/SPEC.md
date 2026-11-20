---
id: tidepool-gianthand-rig
type: submodule-design
status: draft
title: 巨手骨架适配与显示
parent: tidepool-gianthand
depends-on: [tidepool-gianthand-math]
tags: [rig]
---

# 巨手骨架适配与显示

## 职责

读取序列化骨架及腕掌标定，将数学结果应用到 Unity 关节，提供实际骨架、误差与控制柄显示。算法支持域由 tidepool-gianthand-math 管理，动作周期由 tidepool-auto-grasp 管理，本层不各自启动抓取周期。

## 真实驱动边界

GiantHandCircularArmDriver.Refresh 验证骨架，求圆弧并用自写 FK 检查，再提交旋转。当前要求连续父子链、每段沿父局部 +X、等长、骨链及祖先单位缩放；目标和 pole 不能是受驱动链的后代。不能把数学层的某种支持直接宣称为该驱动已经支持。

GiantHandPalmControl 反算腕目标并保持 pole；其独立跟随分支可夹紧目标。正式抓取使用 TryApplyWristGoal 驱动，结果应依据实际提交和误差判断，不把目标被夹紧当作原目标已命中。

GiantArmIKDriver／GiantHandDebugView 是旧 Two-Bone 适配；HandGripController／FingerGripDriver 是预设角插值，不是接触 IK。GiantHandArcLab 为单独实验入口，不等同于真实场景驱动的所有约束。

## 写入权与只读视图

自动抓取启用时接管掌／臂的 DrivenExternally 并禁用已有手指手动驱动，禁用时恢复。该布尔门不是通用多拥有者仲裁；配置者仍须排除旧两段驱动等其他写入者。

GiantHandLineView、FingerLineView 读取实际关节点连线；GiantHandGimbalView 只写装饰环，不反向修正求解骨架。线宽不是物理碰撞半径。

## 命名与序列化

本目录有带命名空间的 GraspRig、PlayerCapsuleSampler；AutoGrasp/Runtime 中还存在不同 GUID 的同名全局占位类。有效性以命名空间、GUID 和实际场景引用判断；不得仅按短类名删除或批量替换。
