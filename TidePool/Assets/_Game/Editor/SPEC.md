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

本目录拥有通用池厅场景生成、灯光／相机／水流程安装与开发输出定位。角色和巨手的专属 Editor 工具由各自模块维护；所有工具均不是运行时依赖。

TidePoolDevelopmentPaths 将通用输出定位到 `Application.dataPath/../Development`。历史快照的绝对盘符不是当前工具路径，不应为修复历史记录而批量重写归档。

## 执行边界

部分工具使用 InitializeOnLoad 轮询 command 文件并写 ready/status，甚至可安装、保存场景；打开工程不一定是文件系统只读操作。不要提交活跃命令文件，避免另一台机器导入后执行遗留请求。

生成链 Park → Expanded → Dream → WaterCycle → Monument 产生场景变体，不是 BuildPlayer 或运行时 LoadScene。检查／测试与安装／保存命令必须区别对待；运行前保存用户工作，之后核对场景 dirty 状态及 diff。

## 验证与已知偏差

真实 Play 测试会进入运行、模拟输入、产生临时对象／报告；不能同时手动控制角色。异步 RunBatch 的退出由测试自己管理；具体可运行命令和输出见 [测试指南](../../../../docs/testing.md)。

旧水循环工具含不符拾信流程的断言；角色重建分支和旧移动跳回归也可能保存／验证旧规则。既不绕过失败，也不把工具名称中的 Test 当作最新验收承诺。

历史结果和迁移资料留在 Development，不重新导入 Assets。新增工具通信文件按精确路径忽略；已跟踪的历史快照不会因 ignore 自动停止跟踪。
