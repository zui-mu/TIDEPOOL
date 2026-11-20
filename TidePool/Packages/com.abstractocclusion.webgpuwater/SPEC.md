---
id: tidepool-water-package
type: module-design
status: draft
title: WebGpuWater 嵌入包边界
parent: tidepool-unity
tags: [third-party, water, urp]
---

# WebGpuWater 嵌入包边界

## 职责与集成表面

拥有水面模拟、渲染、波纹、浮力与水面／排除区查询。第一方通过 WaterVolume、WaterExclusionVolume、查询和 AddRipple 等接口消费；池厅水位阶段、拾信和巨手规则不进入本包。

包以 embedded 的项目相对路径存在，保留源码、默认资源和 LICENSE。现有 Unity 6.6 EntityId 兼容修改必须在升级时核对，不用下载缓存替换当前源码。

## 程序集与资源边界

当前 Runtime asmdef 显式引用 Core、URP 和 Input System，不能照上游旧说明宣称“无 URP 也能编译”。Editor 是独立 Editor-only 程序集，Tests 使用 TestAssemblies；内部 API 的友元可见性不等于第一方任意调用内部实现的承诺。

包内 defaults 与项目 `Assets/WebGpuWater` 的可编辑材质／配置分开；样例并非第一方剧情。原始法线生成图未完整分发，不能声称仅凭仓库就能重建所有最终纹理。

## 文档与测试效力

上游文档入口为 [Documentation~/index.md](Documentation~/index.md)，新功能说明以 Markdown 为准，旧 PDF 不替代当前源码。包内原文保留其上游身份；本文件只拥有 TidePool 的集成边界，不重写整套第三方手册。

[Tests/README.md](Tests/README.md) 描述确定性 PlayMode 契约测试，不覆盖 GPU、URP 画面和异步 readback 的全部行为。当前项目 manifest **没有 testables 声明**，与该 README 的声称不同；测试发现需要在对应 Unity 环境核实，本轮不修改依赖配置或宣称套件已通过。

部分示例（如 WaterSplashRange）仍读取旧 Input API，而本工程启用新版输入；不能把全部包样例列为已兼容。第一方主场景与上游样例应分别验证。
