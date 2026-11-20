---
id: tidepool-gianthand-math
type: submodule-design
status: draft
title: 巨手纯数据数学
parent: tidepool-gianthand
tags: [math, ik, fk]
---

# 巨手纯数据数学

## 职责与边界

用数值输入产生骨链姿态、关节点和状态；允许 UnityEngine 的 Vector3、Quaternion、Pose 等基础数学类型，不读取输入、场景组件或玩家，不写 Transform。数学定义和研究要求由父节点 tidepool-gianthand 拥有；这里限定当前公共表面的真实支持域。

## 当前接口

- SerialChainFk.Evaluate 接受根世界 Pose、逐子关节局部 Pose 和 N+1 长度的输出数组，按父世界乘局部重建。旋转须为单位四元数；不是含非均匀缩放的通用仿射骨架求解器。
- WristPalmPose.ToPalm／ToWrist 是固定腕掌位姿的正反变换，不计算碰撞或抓取成功。
- CircularArcSolver 处理等长串联圆弧，返回根姿态及子局部姿态；弯曲框架可缓存以处理闭合链。等长大弧／闭合支持不能类推给另一求解器。
- UnequalCircularArcSolver 是独立非等长、小弧（不超过 180°）实现，不支持根目标重合；当前未接正式驱动或数学检查。存在文件不等于完成了通用非等长能力。
- 全局 TwoBoneSolver 是旧两段解析肘点接口；不可达或 pole 共线会失败，不是任意长度链求解器，也没有完整的统一结果／回退实现。

## 正确性与失败

输入无效、距离不可达、退化和数值未收敛必须区分；当前 bool/message 不等于课程设计的统一 SolveResult。不得靠改段长、改缩放或直接搬显示点满足目标。

[巨手数学检查](../Editor/GiantHandMathChecks.cs) 覆盖部分 FK、腕掌、圆弧和适配路径，不覆盖全部求解器、CCD 或课程统计。目标点、纯 FK 重建与引擎最终位置应独立比较；不能用 Transform 自动传播代替自写 FK 的证据。
