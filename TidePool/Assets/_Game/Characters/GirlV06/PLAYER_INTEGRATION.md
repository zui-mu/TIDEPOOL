# 女孩主控接入记录（2026-10-30）

接入场景：Assets/_Game/Scenes/PoolHall_ArmLab_01.unity。
打开该场景并进入 Play：WASD 移动，左 Shift 跑步，Space 跳跃，按住鼠标右键转动相机；原有 E 交互、R 重置继续由原场景控制。

## 已接入

- 保留原 Player 根对象、相机目标、水体、巨手及场景交互引用；女孩位于 Player/GirlVisual。
- 隐藏原胶囊 MeshRenderer；CharacterController 继续负责碰撞，底部与原场景一致，尺寸按身体调整。头发和手臂不参与胶囊碰撞。
- 此场景步行 1.55 m/s、跑步 3.7 m/s、加速度 10 m/s²。其他场景保留原来的移动速度和即时输入方式。
- GirlPlayerAnimation 从 CharacterController.Move 的实际位移取得速度，驱动待机、走路、跑步混合；转动视觉子对象。碰到墙后实际速度归零，动画也返回待机。
- GirlV06_Locomotion.controller 使用女孩自己的 Humanoid Avatar，Apply Root Motion 关闭。脚接地阶段的后移速度用于估算步频：走路约 1.752 倍，跑步约 1.341 倍。此估算减少滑步，不等于全姿态脚锁定。
- 保留肩部修形。水体的 rendererOverride 改为角色 Body，避免用隐藏胶囊的网格计算水体交互。
- 相机距离 4.5 m、目标偏移高度 0.35 m（Player 根在脚底上方约 1 m），沿用原相机避障和干燥区域显隐逻辑。

## 当前范围

跳跃物理已接通；空中暂用由现有待机动作提取的固定姿势，落地回到走跑混合。尚未提供专门的起跳、下落、落地、游泳动作。
头发动态、衣物碰撞、斜坡脚部 IK、精细转身和最终材质提升仍属于后续工作。
源 GirlV06_Character.prefab 保留原动作预览控制器，正式主控控制器作为 ArmLab 场景实例的覆盖保存；原独立动画验证场景继续可用。
其他泳池场景和 Build Settings 没有切换到本主控场景；若要构建游戏，需选择正确的启动场景。

## 验证与备份

项目 Development/character_integration/ 保存安装和真实 Play 模式报告。
测试使用临时输入设备，在现有场景运行时检查走跑、停止、转向、碰撞、空中状态、落地、水体交互、重置和相机显隐，结束后退出 Play，不保存测试临时物体。
接入前的原场景和 PlayerMovement.cs 位于 Development/character_integration/BeforeIntegration/；ArmLab_in_memory_before.unity 保存了接入前编辑器里的场景。
Tools/Girl V06/5 与 6 提供接入和运行验证菜单。验证会模拟键盘并进入 Play，请在不手动操控角色时运行。

本轮最终验证：25 项检查通过，runtime-report.json passed=true、errors=[]。实测步行 1.5500 m/s、跑步 3.7000 m/s、顶墙速度 0、跳跃上升约 1.143 m；待机脚底与地面的绝对误差约 1.3 cm。
