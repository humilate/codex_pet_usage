# Codex 宠物用量插件

插件提供两种可切换布局：**环绕圆环**在宠物左右各显示半圈额度，左侧为 5 小时，右侧为本周；圆环底部为原生语音输入条留出缺口，按钮展开时两侧弧线平滑收起，离开后连续反向恢复。圆环持续显示，鼠标悬停宠物时才浮出数值和重置时间。**下方双卡片**在宠物的原生按钮下方显示 5 小时和本周额度，悬停时浮出；桌面底部空间不足时，两张卡片紧贴宠物上方以保持完整可见。没有上下文用量。右键系统托盘的“Codex 宠物用量”图标可切换布局，选择会保存并在下次启动时恢复。

宠物动画、发送和语音按钮仍由 Codex 控制。浮层只读取原生宠物的位置，没有独立拖动或位置预测；圆环与额度卡片一起跟随宠物，屏幕边缘放不下标签时仅调整卡片排列。浮层排在宠物窗口后面，其他窗口遮住宠物时也会一并遮住额度显示。它可以覆盖任务栏。

标签底色为完全不透明的白色。Codex 显示“正在…”等任务进度提示且与标签区域相交时，标签暂时收起，圆环仍可见；提示消失后标签再出现。检测只在宠物悬停期间使用 Windows 辅助功能读取可见文字与位置，不保存这些文字。

项目中的 `PetUsageOverlay` 和 `PetUsageWatcher` 分别是浮层与监听程序，`plugin` 保存插件清单、技能与脚本。发布脚本会将这些文件复制到当前用户的 `~/plugins/pet-usage`，编译程序，并更新本地 Codex 插件缓存。插件没有修改 Codex 安装目录或原生宠物控件。

## 运行

需要 Windows 和 .NET 8 Desktop Runtime。编译还需要 .NET 8 SDK。

```powershell
.\start-pet-usage.ps1
```

浮层在系统托盘显示一个图标；右键图标可切换“环绕圆环 / 下方双卡片”或退出。重复运行不会打开第二份。自动启动使用当前用户的 Windows 登录计划任务：由系统独立启动无界面的轻量监听程序，每 2 秒检查一次 ChatGPT 桌面应用是否运行，不依赖进入 Codex 会话。ChatGPT 打开时启动浮层，主窗口关闭时浮层立即退出并通知监听程序；再次打开时重新启动。手动退出浮层后，监听程序会等到下次重新打开 ChatGPT 才再次启动它。

自动启动计划任务由插件的 `scripts\install-autostart.ps1` 安装，目标是插件源码目录中稳定的 `bin\PetUsageWatcher.exe`。要取消这种自动启动，可运行 `scripts\uninstall-autostart.ps1`；这只移除本插件自己的计划任务与旧版快捷方式。

可以直接运行插件内的切换脚本：

```powershell
& "$env:USERPROFILE\plugins\pet-usage\scripts\set-mode.ps1" -Mode below
& "$env:USERPROFILE\plugins\pet-usage\scripts\set-mode.ps1" -Mode orbit
```

需要重新编译时：

```powershell
dotnet build .\PetUsageOverlay\PetUsageOverlay.csproj -c Release
```

需要把源代码改动更新到插件时，运行：

```powershell
.\publish-pet-usage-plugin.ps1
```

脚本会重新发布程序、校验插件、更新本地版本并重新安装。已有会话不会热加载新插件；在新会话里使用插件技能。

## 数据

- 5 小时与本周额度：每 60 秒通过本机 Codex CLI 的 `account/rateLimits/read` 接口刷新一次。显示的百分比是 `100 − usedPercent`，重置倒计时每 15 秒更新。不会读取或保存认证文件。
- 宠物位置与开关：读取 Codex 本机 `.codex-global-state.json` 中的宠物位置，不修改 Codex 设置。

若额度接口暂时不可用，显示上一次成功读取的值；首次读取失败时显示“—”。

调试数据来源可运行：

```powershell
dotnet .\PetUsageOverlay\bin\Release\net8.0-windows\PetUsageOverlay.dll --diagnose
```

此命令只输出额度百分比，不输出凭据。

布局边界检查可运行：

```powershell
dotnet .\PetUsageOverlay\bin\Release\net8.0-windows\PetUsageOverlay.dll --check-layout
```
