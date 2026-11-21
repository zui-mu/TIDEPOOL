# 检查与验证指南

## GitHub Actions

每次推送工作分支会运行 `Repository rules`，PR 编辑正文也会重新检查。面向 `main` 的 PR 和 `main` 的推送会在基础检查通过后运行 `Unity compilation and regressions`。PR 校验 GitHub 生成的试合并结果，便于在合入前发现两条分支之间的冲突。检查范围见 [Unity 工程 Spec](../TidePool/SPEC.md#验证边界)。

GitHub 托管的 Windows 机器按工程版本和 revision 安装官方签名的编辑器，检出完整仓库和 LFS 实体。维护者在仓库 **Settings → Secrets and variables → Actions** 配置 CI 账号的 `UNITY_EMAIL`、`UNITY_PASSWORD` 和一个许可证来源：支持命令行激活的 `UNITY_SERIAL`，或 GameCI 所述、包含 DeveloperData 的 Hub ULF 全文 `UNITY_LICENSE`。可用性取决于许可证类型和剩余席位。请使用仓库 Secret 表单录入这些值。签出凭据仅用于拉取；授权与归还阶段的日志留在临时机器上。Fork PR 无法使用本仓库的 Secret，缺少配置会让 Unity 检查明确失败。[Unity 命令行授权](https://docs.unity.com/en-us/engine/6000.3/manual/get-started/install-and-upgrade/licenses-and-activation/managing-your-unity-license) · [GameCI 许可证说明](https://game.ci/docs/github/activation/)

本地可在仓库根用以下命令复现。在独立检出执行 Unity 套件，它会打开正式场景并刷新已有角色报告。`UNITY_EDITOR` 应指向本机已激活的编辑器可执行文件。

```sh
node --test scripts/ci/check-repository.test.mjs
node scripts/ci/check-repository.mjs --base origin/main --head HEAD --branch ci/add-repository-and-unity-checks
node scripts/ci/run-unity.mjs
```

分支名请替换为实际名称，核对 PR 正文时加 `--body-file <正文文件>`。每次 Unity 运行将新日志和本次生成的 JSON 放在 `TidePool/Logs/ci/run-*/`，云端保留七天。水位回归在正式池厅模拟 E 排水和拾信、Esc 结束阅读、R 复位，长时间的水位变化通过游戏已有的推进方法加速。角色动作、碰撞、读取信件期间的水位变化和输入响应由 Play Mode 验证。

批处理运行期间临时关闭编辑器的启动搜索索引，以避免新检出中空索引干扰 Play 回归；结束时恢复本地搜索设置。若多个 PR 同时争用一个 Unity 席位，可在上一轮归还后重跑失败的作业。

## 先区分检查的含义

静态文档／源码核对、Unity 编译、确定性数学、真实 Play、画面检查和课程批量评测是不同层次。历史 PASS 只证明对应版本；报告须记录场景、编辑器、配置、运行入口与失败，不能只写“全部通过”。

本轮文档重建没有运行 Unity、没有修改测试或降低断言。以下是已核对的入口，不是本轮执行记录。

## 运行前后

1. 使用 ProjectVersion 指定的 Unity 打开整个仓库中的 TidePool 工程，拉齐 LFS 与本地 Jiggle 依赖，等待编译结束。
2. 先保存或另存用户场景，确保没有另一轮测试运行；暂不手动操作角色。导入域加载本身可能改 ready/status 文件。
3. 核对是检查、测试还是安装／保存命令；`Tools > Girl V06 > 5`、`11` 等安装入口可能改场景／资源，不能当作只读验证。
4. 完成后退出 Play，检查 Console、报告、场景 dirty 状态与 git diff；不要自动保存测试临时对象。

## 巨手

- `Tools > GiantHand > Run Math Checks`，入口 `TidePool.GiantHand.Editor.GiantHandMathChecks.Run`：FK、腕掌、目标、锁拥有权、等长圆弧及驱动的部分断言，结果在 Console。
- `Tools > GiantHand > Open Arc Lab`：打开现有独立圆弧实验场景，观察目标／pole、实际骨链和误差；不是自动课程验收。
- ArmLab 还需人工检查完整捏取、抬升、下降、释放，以及玩家走开、五指失败、碰撞阻挡、引用丢失、禁用／重置后的锁恢复。

现数学检查不覆盖 CCD、完整网格净空、非等长、全周期异常或 1000 个随机样本。统一研究指标与对照要求由 tidepool-gianthand 管理。

## 角色与动画

- `Tools > Girl V06 > 4 - Check project assets`：Avatar、网格、动画和运行组件导入检查，输出 `TidePool/Development/character_validation/`。
- `6 - Test integrated player`：真实场景走跑、阻挡、跳跃、重置、镜头等，输出 `Development/character_integration/runtime-report.json`。
- `7 - Test full jump sequence`、`8 - Capture full jump sequence`、`9 - Test moving jumps`：专项回归／采集，报告主要为 `Development/character_integration/jump-sequence-regression.json`，逐帧内容写入 Library／Temp。
- `10 - Test braid dynamics`：旧自写弹簧核心检查，不等同当前 Jiggle 求解器验证；`12 - Capture and verify braid dynamics` 进行实际角色采集并支持 Jiggle。
- 当前 Jiggle 专属工具还轮询 `TidePool/Temp/GirlJiggle/request.txt` 的 check／install／test／save，输出同目录报告。它是活跃命令通道，不是需要提交的配置，不在没有确认副作用时写入。

旧移动跳回归仍可能要求按输入分类、全程锁定类型或完整 frame 50 恢复，与本轮确认的速度分类／接地重判／缩短区间不同。记录失败及冲突，不能为了交付文档而改测试掩盖它。

[历史 Jiggle 快照](backups/2026-11-07-jiggle-snapshot.md) 的 14/15 通过含身体代理净空失败；旧自写弹簧的通过记录不能替代它，也不能宣称现头发没有穿插。

## 批处理入口

以下方法已存在；从仓库根执行，`UNITY_EDITOR` 设为目标版本的实际可执行文件。输出放在忽略的 `TidePool/Logs/`。**Play 型测试由 RunBatch 自己在完成后退出，不加 `-quit`；不要随意加 `-nographics` 绕开需要真实图形环境的检查。**

```sh
mkdir -p TidePool/Logs
"$UNITY_EDITOR" -batchmode -projectPath "$PWD/TidePool" \
  -executeMethod GirlV06ProjectImportCheck.RunBatch \
  -logFile "$PWD/TidePool/Logs/import-check.log"
```

替换 executeMethod 可运行 `GirlPlayerIntegrationTest.RunBatch` 或 `GirlJumpSequenceRegression.RunBatch`，日志也分别命名。这些方法自行管理退出码；如长时间无结果，检查日志，不无限重复启动多个编辑器。

`BathhouseFlowCheck.RunBatch` 从第一关验证入口关闭和开放的水位阶段，并走入第二关。`BathhouseFlowCheck.RunDirectBatch` 单独打开第二关，检查角色、镜头、行走和房间碰撞。两者使用图形设备，完成后退出，并在 `TidePool/Logs/Bathhouse/` 保存第三人称游戏画面与室内全景。

巨手同步检查也可由 `-executeMethod TidePool.GiantHand.Editor.GiantHandMathChecks.Run` 调用；该方法只抛断言／写日志，没有同类 Play 完成退出协议，自动化调用需单独检查日志和退出行为，不能混用上述测试的通过判据。

## 水包与水流程

包有确定性 PlayMode 程序集 `AbstractOcclusion.WebGpuWater.Tests`，当前项目 manifest 没有 testables，不能声称该程序集必然已在 Test Runner 出现。先核对发现情况；修改 testables 属依赖配置变更，不在本次文档操作中顺手添加。

确定性套件不覆盖全部 GPU、异步回读、URP 画面或输入样例。旧 WaterSplashRange 使用旧 Input API，在当前新版输入设置下有兼容风险；包样例与第一方主场景分开验证。

通用水流程／灯光／相机工具通过 Development 下的精确 command 文件运行并写输出；有些可生成／保存场景。旧 PoolWaterCycleEditor 的“第二次 E 回水”断言不符当前拾信流程，不能当新验收标准；不同场景的洪水高度也不能共用一个旧硬编码断言。

## 文档、图与 Git 规则

- Spec 结构：可用时运行 spec_validate，检查重复 id、悬空链接与 parent 环；再检查根节点子树是否覆盖索引列出的节点。
- Markdown：检查此次新增／修订文档的相对链接和锚点，链接到历史资料不等于采纳其设计。
- `git diff --check`：检查空白错误，注意未跟踪新文件也应纳入文档核对。
- `git check-ignore -v --no-index <路径>`：测试缓存、通信文件被忽略；同时测试场景、meta、manifest、Spec、报告、sources 和历史基线不被误忽略。
- `git check-attr filter text -- <路径>`：验证 LFS 与文本边界；不把所有 `.asset` 强制文本或 LFS。
- 对照 CONTRIBUTING 的分支正则测试正／反例；Git ref 合法和符合仓库命名规范不是同一检查。
- 核对新导入文档 `.meta` 配对／GUID，以及最终改动范围；无脚本／场景改动不代表已做 Unity 编译验证。
