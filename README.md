<p align="center"><img src="assets/logo.png" width="88" alt="Taskbar Countdown logo"></p>
<h1 align="center">任务栏倒计时 · Taskbar Countdown</h1>
<p align="center">把值得期待的日子，留在每天看得见的地方。</p>

一个使用 C# / WPF 编写的 Windows 任务栏倒计时小工具。无需 Python、Node.js 或额外服务，解压即可使用。

## 下载与使用

从 [Releases](https://github.com/DingYi1024/taskbar-countdown/releases/latest) 下载 Windows 压缩包，解压到有写入权限的固定目录。

1. 双击 `start.cmd` 启动。
2. 右键卡片 → **设置**，或双击 `settings.cmd`。
3. 添加自己的日期，点击 **保存并应用**。

默认示例仅包含元旦、春节等公开节日，年份为 2027，不会自动更新年份。

## 功能

- 多个日子并排显示，或按 2–60 秒间隔轮播。
- 日历天数模式与精确的「天 + 时分秒」模式。
- 跟随系统主题，或选择浅色、深色外观；可选半透明背景。
- 自定义名称、日期和显示状态，内置节日预设。
- 横向拖动卡片并记住位置，单击切换时分秒。
- 原生设置窗口和日历时钟图标。
- 守护进程在主程序异常退出后尝试重新启动。
- 皮肤主题：圣诞（圣诞树）、元旦（金星）、春节（红灯笼）、中秋（月亮）、国庆（五角星），或「跟随最近节日」自动切换；配色与图案随浅色/深色主题适配。
- 右键菜单精简为「设置 / 退出」，点击菜单外任意位置自动关闭。
- 设置窗口打开即置顶、Esc 关闭；「我的日子」为扁平表格行，输入框悬停/聚焦才显示边框。

## 操作

| 操作 | 效果 |
| --- | --- |
| 单击卡片 | 切换时分秒并保存 |
| 按住左键拖动 | 调整任务栏上的水平位置 |
| 右键卡片 | 设置 / 退出 |
| `stop.cmd` | 结束倒计时及守护进程 |
| `install-startup.ps1` | 创建当前用户登录启动快捷方式 |
| `uninstall-startup.ps1` | 移除该启动快捷方式 |

退出时使用右键菜单的「退出倒计时」，避免被守护进程重新拉起。`stop.cmd` 会结束当前用户可终止的所有同名倒计时进程。

## 配置与隐私

首次启动读取 `config.example.json` 中的通用示例。保存设置后，在程序旁生成 `config.json`；拖动后生成 `pos.json`。配置完全保存在本地，程序不包含联网上传功能。

仓库排除了个人配置、窗口位置、日志、缓存、历史备份与旧版 Python 实现。发布脚本使用文件白名单，运行时生成的个人数据不会被打进压缩包。

日期格式：`2027-01-01 00:00`，也支持秒。仅天数模式按日历日期计算，今日显示「今天」；精确模式按剩余完整的 24 小时计算天数；到期显示「已到期」。

## 从源码构建

需要 Windows x64 与 .NET Framework 4.8（包含 WPF 和系统自带的 C# 编译器）。当前主要面向 Windows 10 / 11 的主显示器水平任务栏。

```powershell
powershell -NoProfile -File .\scripts\build.ps1
powershell -NoProfile -File .\scripts\test.ps1
powershell -NoProfile -File .\scripts\package.ps1 -Version 1.0.0
```

生成的程序位于 `dist/`，压缩包和 SHA-256 校验值位于 `artifacts/`。GitHub Actions 会在推送和 PR 时执行相同的构建与测试。

## 项目结构

```text
src/                 WPF 卡片、设置窗口、主题与守护进程
assets/              Logo PNG 和多尺寸 ICO
scripts/             构建、测试、打包和启动脚本
tests/               日期解析与计数边界测试
config.example.json  仅含公开节日的示例
```

## 当前限制

- 卡片是覆盖在任务栏上的置顶窗口，不是任务栏原生插件；可能遮挡其他内容或全屏应用。
- 多显示器独立放置、竖向任务栏及自动隐藏任务栏尚未完整支持。
- 设置窗口保持置顶；Esc 会关闭设置，建议先保存修改。
- 程序尚未进行代码签名。

## 许可与致谢

MIT License。项目由 DingYi1024 使用 DeepSeek 和 Codex 辅助开发；Logo 使用 AI 辅助生成。
