# TidePool

基于 Unity 的可解释骨骼 FK／IK／Shape IK 课程研究工程。池厅、玩家、巨手与水体是数学演示的宿主，不以完整商业游戏为验收标准。

**设计事实源是 Spec，不是代码现状或旧测试报告。** 本轮 Spec 均为待审阅的 `draft`；未实现要求与未决事项已明确区分。

## 从哪里开始

- [目标与需求](goal-and-requirements.md)：项目目的、范围、能力与非目标。
- [系统架构](architecture.md)：系统边界、依赖与设计变更规则。
- [文档与 Spec 索引](docs/README.md)：模块树、已采纳设计与历史资料。
- [贡献指南](CONTRIBUTING.md)：分支、Git／LFS、资产提交和验证约定。
- [AGENTS.md](AGENTS.md)：代理必须遵守的执行规则。
- [测试指南](docs/testing.md)：检查入口、输出、副作用与已知验证缺口。

## 克隆与打开

需要 Git、Git LFS 和 Unity Hub。编辑器版本以 [ProjectVersion.txt](TidePool/ProjectSettings/ProjectVersion.txt) 为准，当前 **6000.6.7f1**。

```sh
git lfs install
git clone <仓库地址>
cd <克隆后的仓库目录>
git lfs pull
```

1. Unity Hub 添加仓库内的 **`TidePool/`**，不是仓库根目录。
2. 等待 Unity 恢复包并重新生成 Library；不要把旧电脑的 Library 当作迁移必需品。
3. 打开首关 `Assets/_Game/Scenes/PoolHall_ArmLab_01.unity`。要直接试玩公共澡堂，可打开同目录的 `Bathhouse_02.unity`。
4. 先核对 Console，再进入 Play。数学实验可另开 `Assets/_Game/Scenes/GiantHand_ArcLab_01.unity`。

必须克隆**整个仓库**：`sources/JigglePhysics` 是运行时本地 UPM 依赖。水包嵌入在 `TidePool/Packages/com.abstractocclusion.webgpuwater`，保持项目相对路径；不要换成 Asset Store 下载缓存或旧电脑的绝对路径。

## 当前演示操作

ArmLab 的当前输入直接使用新版 Input System：

- WASD 移动，左 Shift 跑步，Space 跳跃。
- 按住鼠标右键转相机，滚轮缩放。
- 靠近控制点按 E 排水，池底拾取信件后触发上涨；阅读界面按其提示操作。
- 拾信后，在水位涨满前进入巨手右下方的门洞，沿通廊绕过尽头的挡墙，右转进入公共澡堂。
- R 重置水位演示及玩家位置。
- T 在接地静止且控制器支持时切换 Salsa；移动／跳跃／被抓会终止。
- 巨手按周期尝试五指捏取，检查通过后抬升并放回；玩家走开允许本轮失败，不保证持续追踪。

角色当前移动跳已缩短准备／恢复，头发采用 Jiggle。更精确的行为与研究完成范围请读对应 Spec，不能仅凭演示能运行就认定课程要求已全部达成。

## 仓库边界

- `TidePool/Assets/`：Unity 运行资产、场景与第一方脚本；资产和 `.meta` 一起维护。
- `TidePool/Packages/`、`TidePool/ProjectSettings/`：包源码／解析清单及工程设置。
- `sources/`：原始资源和来源；其中 JigglePhysics 是正式本地依赖。
- `docs/`、根目标／架构文档、各模块 `SPEC.md`：设计、协作与验证说明。
- `TidePool/Development/`、`TidePool/HallExpansionBackups/`、`work/`、根 `Temp/`：保留的开发证据／历史文件，不等于 Unity 缓存，不拖入 Assets。

Unity 生成缓存、本机配置及精确列出的工具通信文件由 `.gitignore` 排除。已跟踪历史文件不会因新增 ignore 自动停止跟踪；本轮不做批量清理或 LFS 历史迁移。

## 检查与保存

在 Unity 中可运行 `Tools > GiantHand > Run Math Checks` 或 `Tools > Girl V06 > 4 - Check project assets`。其他菜单可能进入 Play、模拟输入或保存场景，先阅读 [测试指南](docs/testing.md)，不要把安装菜单当成只读检查。

提交前：

```sh
git status --short
git diff --check
git diff --stat
# 确认 Unity 保存完毕，再按实际范围添加文件
git add <本次修改的文件或目录>
git commit -m "docs: describe the documentation change"
```

功能／修复使用对应提交类型，不直接照抄 docs 示例。分支必须采用 `<类型>/<小写英文短横线描述>`；禁止 `codex/`、`claude/`、`Claude/` 等代理／模型名前缀，完整规则见 [贡献指南](CONTRIBUTING.md#分支命名)。

## 换电脑与排障

- 资源若只有 LFS 指针文本，先运行 `git lfs pull`；丢失 `.meta` 会破坏 GUID 引用，不能靠重新导入随意重建。
- 水包和 Jiggle 带有 Unity 6.6 兼容补丁，更新依赖时先阅读各自 Spec 与补丁记录，不能用原包覆盖本地修改。
- 如果编辑器显示 `Untitled` 空场景，在 Project 窗口双击 `Assets/_Game/Scenes/PoolHall_ArmLab_01.unity` 即可打开泳池首关。
- 在 x64 Windows 上遇到 Bee `HostPlatform architecture detection failure []`，检查**系统级** `PROCESSOR_ARCHITECTURE=AMD64`；项目 `.env.local` 不替代 Machine scope。不要在其他架构的机器上照抄这个值。
- 历史 PASS 不是当前复验；已知课程缺口、头发净空失败和旧回归偏差见 [测试指南](docs/testing.md)。

## 来源与许可

本仓库包含第三方资产与包，各自许可证／来源记录继续适用，见 [sources](sources/README.md) 和各包目录。本文件不为整个项目新增统一开源许可。AI 辅助与学生独立验证的边界见研究 Spec。
