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

跳跃使用完整 Girl_Jump（0～50 帧），由准备、实际蹬地、物理腾空、接地恢复共同驱动。预备动作约 0.63 秒，之后才施加跳跃冲量；原有跳跃高度和重力保持不变。最高点对应原片段约第 25 帧，实际接地后继续第 30～50 帧的恢复，完成后才返回走跑。按下 Space 并接受跳跃请求时，根据有效 WASD 输入确定本次类型：无方向输入（含反向键抵消）沿用原地跳；有方向输入使用移动跳，准备、蹬地和接地恢复期间均继续正常移动、加减速，腾空时保留既有方向控制。跳跃类型在本次动作期间固定，松键不会切回原地跳；重置、取消和正常结束会清除类型。墙壁继续由 CharacterController 阻挡并允许沿墙滑动；准备阶段跑出边缘则取消蹬地并进入下落。游泳动作尚未提供。

AirborneHold、Falling、LandingRecovery 都引用同一个完整原片段，各自使用 JumpTime、FallTime、LandingTime；没有裁切素材。分开的时钟保证提前撞顶/接地时可从当前姿势混合，而不强行跳转正在播放的时间。Grounded 本身不再决定整段跳跃是否结束。PlayerMovement 是位移和冲量的唯一拥有者；GirlPlayerAnimation 请求一次蹬地并等待实际执行确认。没有兼容动画桥接的其他场景保留原来的即时跳跃。

R 重置和禁用移动组件会清除待执行的蹬地及当前动作阶段，避免重置后突然起跳。
衣物碰撞、斜坡脚部 IK、精细转身和最终材质提升仍属于后续工作。
源 GirlV06_Character.prefab 保留原动作预览控制器，正式主控控制器作为 ArmLab 场景实例的覆盖保存；原独立动画验证场景继续可用。
其他泳池场景和 Build Settings 没有切换到本主控场景；若要构建游戏，需选择正确的启动场景。

## 验证与备份

项目 Development/character_integration/ 保存安装和真实 Play 模式报告。
测试使用临时输入设备，在现有场景运行时检查走跑、停止、转向、碰撞、空中状态、落地、水体交互、重置和相机显隐，结束后退出 Play，不保存测试临时物体。
接入前的原场景和 PlayerMovement.cs 位于 Development/character_integration/BeforeIntegration/；ArmLab_in_memory_before.unity 保存了接入前编辑器里的场景。
Tools/Girl V06/5 与 6 提供接入和运行验证菜单。验证会模拟键盘并进入 Play，请在不手动操控角色时运行。
Tools/Girl V06/7 与 8 提供完整跳跃回归和画面采集，9 提供移动跳专项验证。新检查覆盖预备时保持接地、实际起跳时刻、最高点姿势、完整落地恢复及中断；逐帧画面用于检查连续性，不能只凭旧的功能检查判定动作自然。

2026-10-30 接入验证：25 项检查通过，runtime-report.json passed=true、errors=[]。实测步行 1.5500 m/s、跑步 3.7000 m/s、顶墙速度 0、跳跃上升约 1.143 m；待机脚底与地面的绝对误差约 1.3 cm。

2026-11-06 完整跳跃验证：4 类完整跳跃和 10 类中断/跌落/阻挡场景通过，原有集成检查扩展为 26 项并通过。静止跳跃实测约 0.650 秒蹬地，最高点对应源帧 24.77，接地约 1.317 秒，完整恢复到第 50 帧后约 2.133 秒回到稳定 Locomotion。连续运行预览：Development/character_integration/full-jump-preview.gif；逐帧证据保存在忽略的 Library/JumpSequenceVerification，测试报告在 Development/character_integration/jump-sequence-regression.json。

2026-11-06 移动跳验证：修改前新增实测用例在准备阶段记录到水平速度 0 并失败；修改后 4 类原有跳跃、9 类移动跳和 10 类中断/跌落/阻挡场景全部通过，原有 26 项角色集成检查也通过。跑跳准备、蹬地、落地恢复实测均约 3.70 m/s，步行跳约 1.55 m/s；同时按方向与 Space、反向键抵消、松键再恢复、撞墙、沿墙滑动、预备阶段跑出边缘、重置后原地跳均有真实运行覆盖。连续预览：Development/character_integration/moving-jump-preview.gif（64 帧、30 fps）。源动画、0.63 秒预备和完整落地动作保持不变；移动时的脚部滑动仍受现有站定动作素材限制。


## 双辫子动态

ArmLab 的 Player/GirlVisual 实例挂载 GirlHairDynamics，使用现有 Hair_Left/Right_1..4 的八根加权骨骼。每条链另有一个从实际末端蒙皮测量的虚拟发梢，左侧约0.19065m、右侧约0.19042m。原 FBX、蒙皮和动画资源保持原样。

组件在 Animator 和肩部修正后更新，只写发骨旋转。动画基础姿态与物理历史分开保存；发根固定、骨段长度保持，形状弹簧、阻尼、头部运动惯性和重力在有界子步中计算，再进行弯曲限制和身体代理避碰。头、颈、上躯干和两肩共5个球／胶囊代理只服务于头发，不注册游戏 Collider/Rigidbody。统一缩放会同步头发与身体代理尺寸。

PlayerMovement.ResetMotion 发出 MotionReset 通知；头发在下一次动画更新后、最终传送位置上重建历史。暂停、长帧、大位移、禁用和重启均有相应处理。关闭组件会恢复发骨基础姿态与头发 Renderer 包围盒。

在 Hierarchy 选中 Player/GirlVisual，Inspector 的 Girl Hair Dynamics 可调：

- Strength：整体响应，当前0.75；0为基础发型。
- Inertia：起步、急停和转身等响应，当前0.30。
- Root/Tip Frequency：根部与末端回弹频率，当前4.2/2.6Hz。
- Damping Ratio：阻尼，当前0.8；提高可减少余晃。
- Gravity Scale：重力影响，当前0.20。
- Root/Tip Angle Limit：每段相对基础弯曲的限制，当前18/35度。
- Left/Right：实际骨链、虚拟发梢和碰撞半径。
- Colliders、Draw Debug：身体代理与调试绘制。场景中的配置已保存。

Bind missing bone references 只补缺失绑定；Reset simulation 清除运动历史、保留参数。Measure tips and configure default body proxies 会重新测量并替换默认代理，支持 Undo。Tools/Girl V06/10 为核心验证，11 为当前场景接入（已有组件时保留参数），12 为实际走跑跳与重置的近景采集。

默认效果偏轻，实测发梢最大响应约1.8cm。碰撞采用简化代理，粗骨链和扁宽发束在极端姿态下仍可能穿插；首版未包含发辫互撞、整条手臂碰撞、风和水下阻力。

验证记录：Development/character_integration/hair-core-regression.json、hair-dynamics-report.json、hair-setup.json；实际运行预览为 hair-dynamics-preview.gif。详细帧和执行记录保存在忽略的 Library/HairDynamicsImplementation。

2026-11-06 头发最终实测：9项核心行为与场景、状态查询隔离、真实动作检查共15项通过，errors=[]；164帧近景覆盖待机、走路、急停、跑跳、转身和R重置。最大发梢偏移约1.84cm、代理最大穿插约8.11mm、发根最大误差约0.0043mm、骨骼局部位置变化为0、重置后偏移约1.58mm。运行中改绑会安全回退并在绑定恢复后重建；状态查询采用独立响应文件，不干扰采集。
