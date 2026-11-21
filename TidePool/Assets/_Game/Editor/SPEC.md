---
id: tidepool-editor
type: submodule-design
status: draft
title: 编辑器装配与验证工具
parent: tidepool-game
references: [tidepool-gianthand, tidepool-girl, tidepool-water-cycle]
tags: [editor, verification]
---

# 编辑器装配与验证工具

## 职责

本目录维护澡堂场景装配、通用预览、正式关卡回归和开发输出定位。角色和巨手的专属 Editor 工具由各自模块维护；所有工具均不是运行时依赖。

TidePoolDevelopmentPaths 将通用输出定位到 `Application.dataPath/../Development`。历史快照的绝对盘符不是当前工具路径，不应为修复历史记录而批量重写归档。

## 执行边界

部分工具使用 InitializeOnLoad 轮询 command 文件并写 ready/status，甚至可安装、保存场景；打开工程不一定是文件系统只读操作。不要提交活跃命令文件，避免另一台机器导入后执行遗留请求。

Park → Expanded → Dream → WaterCycle → Monument 的历史生成链及其专用编辑器入口随五张场景退役。通用预览与正式关卡回归仍可从当前工程执行。运行会保存场景的装配命令前，应先保存用户工作，完成后核对场景和资产差异。

## 验证与已知偏差

真实 Play 测试会进入运行、模拟输入、产生临时对象／报告；不能同时手动控制角色。异步 RunBatch 的退出由测试自己管理；具体可运行命令和输出见 [测试指南](../../../../docs/testing.md)。

正式水流程回归以拾信触发上涨为准。角色重建分支和旧移动跳回归也可能保存／验证旧规则；发现与现行设计不符的断言时记录差异。

历史结果和迁移资料留在 Development，不重新导入 Assets。新增工具通信文件按精确路径忽略；已跟踪的历史快照不会因 ignore 自动停止跟踪。
