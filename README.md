# Codex Usage Widget

Windows 上的 Codex 额度悬浮窗，显示可用的 5 小时／周剩余额度及重置倒计时。

## 使用

1. 从 Releases 下载 `UsageQuery.exe`，双击启动。
2. 拖动窗口移动位置；鼠标移入展开详情，移开收起。
3. 点击锁定按钮保持展开；点击刷新按钮查询最新额度。
4. 滚轮调整大小；齿轮设置中可切换语言、修改配色，以及通过滑块或百分比输入调整缩放。
5. 缩放范围为 40%–150%，缩放比例 ≤ 自定义隐藏阈值时隐藏文字，默认阈值为 80%。
6. 点击最右侧的 × 关闭窗口。
7. 常规设置可调 30%–100% 不透明度、开启开机启动，以及启动后／每 6 小时检查更新；只提示并打开发布页，不自动下载。

启动和手动刷新时查询账户额度，后台每 15 秒读取本地记录。

## 编译

在 Windows 上，用 PowerShell 在项目根目录运行：

    .\tools\CodexQuota\Build.ps1

生成文件：`artifacts\CodexQuota\UsageQuery.exe`。

---

## English

A floating Windows widget showing available Codex 5-hour / weekly usage remaining and reset countdowns.

### Usage

1. Download `UsageQuery.exe` from Releases and launch it.
2. Drag to move. Hover to expand details; move away to collapse.
3. Use the lock button to keep details expanded, or refresh to query current usage.
4. Resize with the mouse wheel. Gear settings provide language, colors, a scale slider and percentage input.
5. Scale ranges from 40% to 150%. Text is hidden at or below the configurable threshold (80% by default).
6. Click × at the far right to close.
7. General settings provide 30%–100% opacity, optional Windows startup, and optional update checks on launch and every 6 hours. Updates open the release page; files are not downloaded automatically.

Account usage is queried on launch and manual refresh. Local records are read every 15 seconds.

### Build

Run PowerShell from the project root on Windows:

    .\tools\CodexQuota\Build.ps1

Output: `artifacts\CodexQuota\UsageQuery.exe`.
