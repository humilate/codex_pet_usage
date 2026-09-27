---
name: pet-usage
description: Start the Codex desktop pet quota overlay or switch between its orbit and below-card modes when the user asks about pet quota display.
---

# Codex 宠物用量

此插件在 Windows 桌面显示 Codex 的五小时和本周额度。它不修改 Codex 安装文件。用量窗口跟随原生宠物；圆环底部以连续动画避让语音输入条，托盘菜单也可以切换布局。Windows 登录计划任务独立启动轻量监听程序；打开 ChatGPT 时启动浮层，主窗口关闭时浮层立即退出，再次打开时重新启动。

- 先从本 `SKILL.md` 的路径向上三级定位已安装插件根目录，再运行其中的 PowerShell 脚本。
- 启动浮层：`scripts/start.ps1`。
- 切换环绕圆环：`scripts/set-mode.ps1 -Mode orbit`。
- 切换下方双卡片：`scripts/set-mode.ps1 -Mode below`。
- 仅当用户明确要求切换布局时运行切换脚本。不要把“下方双卡片”描述为三个指标；它只显示五小时和本周额度。
- 环绕模式的圆环持续显示，数值和重置时间在悬停时出现；下方模式的两张卡片也在悬停时出现。底部空间不足时，卡片移到宠物上方。
- 自动启动由 Windows 当前用户的登录计划任务负责。`scripts/install-autostart.ps1` 安装计划任务并移除旧版启动快捷方式；`scripts/uninstall-autostart.ps1` 移除本插件的计划任务与旧版快捷方式。只在用户要求更改自动启动时运行这两个脚本。
