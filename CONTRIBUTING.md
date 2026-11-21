# 协作与版本控制

## 设计先行

先查 [Spec 索引](docs/README.md)。设计意图先于实现：确认行为／边界／验收变更，先修订唯一所属 Spec，再写代码。当前实现不能自动覆盖已确认要求；冲突通过反向对话澄清。新建和重建的 Spec 保持 draft，用户审阅后才转 active。

README 是使用入口；Spec 是设计事实源；计划是任务安排；报告是对应版本证据。历史资料保留原路径并明确状态，不从旧实施计划重新执行已经被替代的方案。

## 分支命名

所有新工作分支统一使用以下格式，适用于手动创建、IDE 创建和代理创建的分支。

```text
<类型>/<小写英文短横线描述>
```

- **类型**必须是 `feat`、`fix`、`docs`、`refactor`、`test`、`chore`、`build`、`ci`、`perf`、`release` 或 `hotfix`。按变更目的选择，例如新增功能用 `feat`，修复已有行为用 `fix`，纯文档变更用 `docs`。
- **描述**使用小写 ASCII 字母和数字，词组之间用一个 `-` 连接。整条分支名只有一个 `/`，禁止空格、下划线、大写字母，以及开头、结尾或连续的短横线。
- **禁止以代理、模型或工具名作前缀**。例如 `codex/`、`claude/`、`copilot/`、`cursor/`、`gemini/`、`gpt/`、`agent/` 及其大小写变体。直接为工具指定合规名称，或在提交和推送前改名。

创建、改名和推送前均按此正则检查名称。

```regex
^(feat|fix|docs|refactor|test|chore|build|ci|perf|release|hotfix)/[a-z0-9]+(-[a-z0-9]+)*$
```

- 合法示例有 `fix/giant-hand-water-cycle`、`feat/grasp-debug-view` 和 `docs/clarify-branch-naming`。
- 非法示例有 `codex/update-docs`、`Claude/fix-jump`、`fix/codex/water-cycle`、`fix/water_cycle` 和 `fix/water--cycle`。

`main` 等既有主分支、发布集成分支和历史分支可保留原名。工作分支以本节为准，当前由创建者和审查者检查命名。

开始前检查工作区。有用户修改时先确认处理方式，禁止自动 stash 或覆盖。以下示例在新分支尚不存在时使用。

```sh
git status --short --branch
git check-ref-format --branch fix/giant-hand-water-cycle
git switch -c fix/giant-hand-water-cycle
```

已有合规分支用 `git switch <分支名>` 进入。如果当前**本地工作分支**需要更名，校验目标名称后使用 `git branch -m fix/giant-hand-water-cycle`，原提交会随分支保留。`git check-ref-format --branch` 只校验 Git 自身的命名格式，请另按本节正则检查。

## 提交与审查

沿用 `feat:`、`fix:`、`docs:` 等现有提交风格，主题描述实际变化；不以代理名、TODO 编号或工具归因为主题。一次提交对应清楚的修改目的。

```sh
git status --short
git diff --check
git diff --stat
git diff -- <本次修改路径>
git add <明确属于本次任务的路径>
git diff --cached --check
git diff --cached --stat
```

不要无差别 git add . 混入场景自动保存、状态文件或其他人的改动。提交／切换前等待 Unity 完成保存；有未保存场景先由使用者决定保存或另存，不擅自丢弃。未经要求不推送、合并或创建发行。

## PR 正文结构

所有 PR 的正文必须按以下顺序包含四个二级标题，每段填写本次变更的具体内容，不得省略或以自动生成的摘要代替：

1. **做了什么**：说明修改范围、主要内容，以及明确未包含的事项。
2. **为什么这么做**：说明问题、目标和选择理由；相关设计变更链接所属 Spec，不从代码现状倒推批准。
3. **可观测的结果是什么**：给出可复核的变化、验证命令与真实结果。可含截图，但截图非必需；未运行、失败或尚待确认的检查必须如实说明，不能以历史 PASS 替代。
4. **有什么收益/风险**：分别说明收益、风险、兼容性影响和已知限制；没有新增风险也应交代依据，不能空置。

[GitHub PR 模板](.github/PULL_REQUEST_TEMPLATE.md) 固定这四段。正文只使用这四个二级标题；验证细节、计划摘录和相关链接放入对应段内，可用三级标题细分，不另起并列的 Summary／Changes／Testing 结构。自动工具和代理创建／更新 PR 时也必须遵守，不能直接套用工具默认正文。

发布前核对最终 diff、分支基线、工作区和实际验证证据，正文须与准备推送的版本一致。推送和创建 PR 需要用户授权；创建 PR 不等于获准合并。

## Unity 资产与 Git LFS

- 资产与其 `.meta` 一起提交；重命名／移动也保持配对和原 GUID。新加到 Assets 或本地包可导入目录的 Markdown 同样需要稳定 `.meta`。
- 保存的场景、prefab、材质、着色器、输入配置、asmdef、ProjectSettings、manifest 与 lock 都是工程源，不能因“由工具生成”就忽略。
- 第一方不通过改源 FBX、权重或缩放骨架迎合求解结果；第三方升级与补丁必须有来源和验证。
- `.gitattributes` 指定模型、贴图、音视频、ZIP 等 LFS 类型；scene／prefab／脚本／meta 保持普通文本 Git。`.asset` 可为 YAML 或二进制，不能全局强制文本或批量 LFS。
- 本轮没有新增 GIF／二进制 `.asset` 的 LFS 归档规则，也不做历史迁移；以后需要时单独确认影响范围。

```sh
git lfs install
git lfs pull
git check-attr filter text -- <待检查资产路径>
git lfs ls-files
```

LFS 指针不是可供 Unity 使用的模型／纹理本体；切换机器后核对本地依赖与真实二进制内容。

## 忽略与保留

`.gitignore` 只排除可再生 Unity 缓存、本机凭据／IDE 文件、ThinkRail 本地状态及精确命名的工具命令／状态文件。不整体排除 Development、sources、work、根 Temp 或所有 JSON／TXT／META。

Development、HallExpansionBackups、work 和根 `Temp/JumpSequenceVerification/controller.before` 含已跟踪历史证据／基线，不等于 `TidePool/Temp/`。本轮不删除、不取消跟踪、不重写历史。新增 ignore 只防止新加入；已跟踪的 ready/status/health 仍可能产生 diff，应逐项审查，不能假装已被忽略。

工具命令是活跃指令，不是持久配置；不得把遗留 command/request 文件传播给另一台会自动执行它们的编辑器。报告、截图与预览是否提交取决于是否是必要的可复现证据，不以扩展名批量遮蔽。

```sh
git check-ignore -v --no-index <待检查路径>
git ls-files -- <待检查路径>
```

凭据不得入库；`.env.example`／`.env.sample` 可保留不含秘密的模板。忽略不能撤回已提交的秘密，发现泄漏需单独处理和轮换。

## 验证与交付说明

按 [测试指南](docs/testing.md) 选择与改动相关的检查。文档任务至少检查图结构、相对链接、忽略／属性规则和 diff；代码任务还需所属模块的编译、数学、运行及视觉验证。

交付要分清实际运行与历史记录，说明未达成课程要求、失败和未决事项。没有 Unity 环境就明说未做编译／Play；不以旧 PASS 或只读源码核对代替新测试。
