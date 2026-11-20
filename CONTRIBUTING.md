# 协作与版本控制

## 设计先行

先查 [Spec 索引](docs/README.md)。设计意图先于实现：确认行为／边界／验收变更，先修订唯一所属 Spec，再写代码。当前实现不能自动覆盖已确认要求；冲突通过反向对话澄清。新建和重建的 Spec 保持 draft，用户审阅后才转 active。

README 是使用入口；Spec 是设计事实源；计划是任务安排；报告是对应版本证据。历史资料保留原路径并明确状态，不从旧实施计划重新执行已经被替代的方案。

## 分支命名

新工作分支统一为：

```text
<类型>/<小写英文短横线描述>
```

允许类型：`feat`、`fix`、`docs`、`refactor`、`test`、`chore`、`build`、`ci`、`perf`、`release`、`hotfix`。描述由小写英文字母或数字组成，用单个短横线分隔；不用空格、下划线、额外斜杠或大小写混排。

等价正则：

```regex
^(feat|fix|docs|refactor|test|chore|build|ci|perf|release|hotfix)/[a-z0-9]+(-[a-z0-9]+)*$
```

**禁止使用代理／模型名称作分支前缀**，包括 `codex/`、`claude/`、`Claude/`、`copilot/`、`cursor/`、`gemini/`、`gpt/`、`agent/` 等；不能因执行工具默认生成这类名称而例外。

- 合法：`docs/improve-the-project-documentation-system`、`feat/grasp-debug-view`、`fix/jump-recovery`。
- 非法：`docs/Improve the project documentation system`、`codex/update-docs`、`Claude/fix-jump`。

现有主／发布集成分支及历史分支不在本次强制重命名范围；规范约束新工作分支。命名检查是仓库约定，本轮未安装 Git hook 或 CI 门禁，不能声称已经自动强制。

开始前检查工作区；有用户修改先确认处理方式，不自动 stash／覆盖：

```sh
git status --short
git check-ref-format --branch docs/improve-the-project-documentation-system
git switch -c docs/improve-the-project-documentation-system
```

示例切换命令只在该分支尚不存在时使用；已有分支用 git switch，不强行覆盖。Git ref 合法不等于符合上述仓库正则。

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
