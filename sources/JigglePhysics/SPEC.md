---
id: tidepool-jiggle
type: submodule-design
status: draft
title: Jiggle Physics 本地依赖边界
parent: tidepool-sources
tags: [third-party, physics]
---

# Jiggle Physics 本地依赖边界

## 职责与装配

此目录是 v16.0.1 的固定本地 UPM 源码，保留 MIT LICENSE；Unity manifest 通过项目相对 file 路径消费。第一方通过 GirlJiggleHair 适配 JiggleJobs，不复制或重新实现其积分／约束求解器。角色侧契约在 tidepool-hair。

Runtime 和 Editor 有独立程序集。包不依赖 TidePool 的玩家、抓取或水流程；特定角色的骨骼绑定、预热、重置和生命周期留在第一方适配器。

## 来源与本地补丁

[补丁记录](TIDEPOOL-PATCHES.md) 记录固定 ZIP 的 SHA256、EntityId 兼容、独立拥有者释放回调和可选约束迭代；下载原件是相邻 `JigglePhysics-v16.0.1.zip`。上游通用 README 的网络安装方法不替代本项目本地依赖配置。

本地补丁与原件应可比较。当前补丁记录没有完整下载 URL，静态容器初始化等差异也未全部解释；**来源记录仍有缺口**，不能宣称已完成完整上游审计或全部变更都有理由。本次不猜测 URL、不篡改原件，也不补写未经核实的来源。

## 维护与验证边界

变更包之前查明角色适配器、Jobs 完成与内存释放的影响，更新补丁记录并复验实际发辫；保留许可证和原始 GUID。包自身的通用功能不等于角色实例已验证支持全部特性，已知头发代理穿插限制见所属角色规范。
