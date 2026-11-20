---
id: tidepool-sources
type: module-design
status: draft
title: 原始资源与来源管理
parent: tidepool-architecture
tags: [assets, provenance]
---

# 原始资源与来源管理

## 职责

保存可追溯的原始模型／动画、下载清单、许可证与上游归档。通常由 Assets 保存配置后的 Unity 运行资产；**JigglePhysics 是例外**：它本身是 manifest 指向的本地 UPM 包，不能视为可删缓存。

## 边界

mixamo 的原件与 download_manifest.json、unity-official-tests 的来源提交与许可证是导入来源；已配置的女孩、动画和验证场景位于第一方 Assets。原始来源不因重新命名导入资产而丢失。

[tidepool-jiggle](JigglePhysics/SPEC.md) 拥有该依赖的版本／补丁边界。上游许可证继续有效；根目录新增的协作规则不替代第三方许可，也不凭空为整个项目声明开源许可证。

不能整体忽略 sources、所有 FBX、所有 JSON 或所有 `.meta`。需要修改上游源码时记录来源版本和本地差异，不能默默替换固定归档或以另一台机器的绝对路径作为依赖。资源版本化操作见 [协作指南](../CONTRIBUTING.md)。
