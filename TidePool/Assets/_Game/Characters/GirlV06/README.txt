女孩 v06：已配置的 Unity 角色与 Mixamo 动画（2026-10-30）

运行角色：GirlV06_Character.prefab。
人物 Animator 使用女孩自己的 Humanoid Avatar，并挂有肩部修形组件。
Animations/ 包含独立的 Idle、Walk、Run 人形动画和预览 Animator Controller。
Editor/MixamoSources/ 仅用于重新导入与重建源 Avatar，保留你下载文件的副本；游戏中的角色不依赖这些源网格。

原始下载文件保留于仓库根目录 sources/mixamo/。
本文件夹是用于 Unity 的已配置副本，移动时应通过 Unity Project 窗口移动并保留 .meta。

预览：打开 Assets/_Game/Scenes/GirlV06_MixamoValidation.unity 后进入 Play，默认每四秒在待机、走路、跑步之间循环，人物保持原地。
角色上的 GirlAnimationPreview 仅用于测试场景。关闭 Auto Cycle 后可用 Motion 下拉框选择动作。

已在 PoolHall_ArmLab_01 场景接入 Player，并用实际移动速度驱动走跑混合。详见 PLAYER_INTEGRATION.md。
源 Prefab 和独立验证场景继续使用预览配置；ArmLab 场景实例使用正式 Locomotion 控制器。跳跃空中为临时固定姿势，游泳与专门跳跃动作尚待补齐。

