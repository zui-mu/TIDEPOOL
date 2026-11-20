---
id: tidepool-auto-grasp
type: submodule-design
status: draft
title: 自动抓取与几何检查
parent: tidepool-gianthand
depends-on: [tidepool-gianthand-rig, tidepool-player, tidepool-gianthand-math]
tags: [grasp, geometry]
---

# 自动抓取与几何检查

## 职责与数据流

将真实玩家胶囊、手部标定和动作配置转换为有预算的五指方案，协调腕／臂／指输出、抓取判定和玩家锁。动作意图、场景预设与课程要求只由父节点 tidepool-gianthand 定义。

GraspController 是有效的命名空间总控：采胶囊 → 尝试有限接近方位 → FingerCcdSolver 求闭合角 → 检查离散动作样本 → 驱动骨架 → 检查实时接触 → 请求移动锁 → 搬运／释放。手指动作中插值角度再检查，不是每帧都重新 CCD。

## 几何与求解边界

CapsuleGeometry 提供数值胶囊和距离操作；GraspTargetGenerator 只产生目标。旧 TryGenerate 仍带进度分段语义且主要用于数学检查，正式总控只复用其指尖目标接口，不能按旧目标生成链解释当前整轮动作。

FingerCcdSolver 为每指三实体关节、四角度限位的内部求解器，用自写 FK 与碰撞／误差回退；不修改掌部来掩盖单指失败。腕部协调属于总控明确的规划与动作输入，不是手指求解器隐含副作用。

PlayerCapsuleSampler 读真实 CharacterController.center／radius／height，要求有效、竖直和祖先单位缩放。不能用视觉模型原点替代胶囊中心。

HandClearance 缓存可见网格检查候选姿态与地板／胶囊；不可读网格仅使用包围盒角点，不等于完整网格碰撞。另有指段代理和指间检查；缺少末节可见网格时不能冒称接触验证通过。

## 调度、锁与失败

- 周期时钟只由总控拥有；忙碌时跳过采样机会，不排队积压过时快照。静态测试可复现，移动目标可能使规划失效，须允许取消而非强行抓住。
- 五指检查通过且人物接地后才尝试取得移动锁；锁获取失败不得搬运。当前竖直搬运经 TryMoveWhileLocked 处理碰撞，不把人物挂到腕部。
- 丢失绑定或禁用会释放自身持有的锁并恢复受接管驱动。不能释放别的 owner 的锁；释放、取消、关闭和重置交错需要真实周期测试。
- 当前驱动接管使用布尔门，异常路径没有统一 try/finally；Return 阻挡可能持续等待，进入该阶段前已释放玩家。它们是实现风险，不是已证明完善的恢复保证。

## 验证边界

当前 GiantHandMathChecks 没有覆盖 CCD、网格净空、总控完整周期或全部异常释放。成功规划和样本无穿插不是连续碰撞证明；必须检查实际提交的姿态、碰撞阻挡下的搬运和每指真实结果。
