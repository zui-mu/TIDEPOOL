---
id: tidepool-hall-presentation
type: submodule-design
status: draft
title: 池厅灯光与渲染表现
parent: tidepool-game
depends-on: [tidepool-water-cycle]
tags: [presentation]
---

# 池厅灯光与渲染表现

## 职责

PoolHallLampRig 读取水流程的 Filling 时钟，驱动池厅灯色、亮度、环境光与焦散 cookie。表现层解释状态，不反向推进水位、触发抓取或控制玩家。

MonumentRenderPipeline 是可恢复的运行时管线覆盖组件；当前未在已核对的第一方场景中发现挂载。存在脚本不等于启用特性。

## 边界与验证

场景资产保存灯具、材质和配置，Editor 工具负责生成／安装，不是运行时依赖。修改全局渲染状态时须检查退出后的恢复，不以截图替代恢复检查；灯光过渡、渲染后端和焦散效果必须在相应场景中实际观察。

具体美术参数是场景配置，不在本 Spec 复制；未单独确认的视觉风格不得从历史生成工具推断为新的设计要求。
