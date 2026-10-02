# XboxControllerBatteryMonitor

Windows 系统托盘小工具：实时显示 Xbox 手柄电量，低电量时 Toast 通知 + 手柄震动提醒。

## 功能

- 蓝牙连接的手柄：BLE 标准电量服务（0x180F），精确百分比
- Xbox 无线适配器 / USB 连接的手柄：XInput 电量（4 档估算百分比）
- 两种连接方式可同时在线（混合场景自动去重，蓝牙读数优先）
- 托盘图标轮换显示多个手柄，右键菜单列出全部
- 高分屏适配：Per-Monitor V2 DPI 感知，托盘图标按系统小图标尺寸原生绘制
- 低电量双档提醒（默认警告 20% / 危险 10%，可自定义），阈值跨越时触发一次，不重复轰炸
- 震动提醒可关闭

## 下载使用

从 [Releases](https://github.com/94leon/XboxControllerBatteryMonitor/releases) 下载最新版
`XboxControllerBatteryMonitor.exe`（约 25 MB 单文件，免安装，双击即用）。

- 系统要求：Windows 10 19041+ / Windows 11，以及 [.NET 8 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/8.0)（未安装时微软官网可一键安装）
- 托盘图标右键菜单：查看全部手柄电量、`设置…`（调整提醒阈值、关闭震动）、`退出`
- 设置保存在 `%LocalAppData%\XboxControllerBatteryMonitor\settings.json`，日志在同目录 `log.txt`
- 纯本地小工具：无后台、无联网、无开机自启（需要自启可自行把 exe 快捷方式放进启动文件夹）

## 打包发布

双击仓库根目录的 `publish.cmd`（或在命令行执行）：

```bash
publish.cmd
```

脚本会自动完成：结束正在运行的程序实例（否则 exe 被占用，发布时无法覆盖）、
清空旧的 `publish/` 目录（不残留旧文件）、发布单文件 exe。

产物为**框架依赖的单文件 exe**（`publish/XboxControllerBatteryMonitor.exe`，约 25 MB），
目标机器需已安装 .NET 8 Desktop Runtime (x64)。版本号读取仓库根目录的 `VERSION` 文件。

不用脚本的等价命令（需先手动退出正在运行的程序）：

```bash
dotnet publish src/XboxBatteryMonitor -p:PublishProfile=src/XboxBatteryMonitor/Properties/PublishProfiles/FolderProfile.pubxml
```

如需免装 .NET 运行时的独立版，在上述命令追加 `-p:SelfContained=true -p:EnableCompressionInSingleFile=true`（体积会增大不少）。

## 已知限制

- XInput 只能给出 4 档电量（空/低/中/满，估算 10/40/70/100%）；精确百分比只有蓝牙手柄可读
- 蓝牙 + 适配器手柄同时在线时，适配器手柄显示的档位可能实际来自另一槽位
  （XInput 公开 API 无法区分槽位归属）；此场景下蓝牙手柄低电只发 Toast 不震动
- 蓝牙手柄需固件支持标准 BLE 电量服务（Xbox One S 之后的固件均支持；
  不支持时会被当作适配器手柄显示 4 档估算）
- 适配器手柄刚连接约 10 秒内可能显示"等待读数"（XInput API 限制）

## 开发

从源码运行与测试：

```bash
dotnet run --project src/XboxBatteryMonitor
dotnet build
dotnet test
```

设计文档见 `docs/design.md`，真实硬件手动验证清单见 `docs/manual-verification.md`。

## 许可

MIT，见 [LICENSE](LICENSE)。

## 致谢

开发过程中参考了以下两个项目的 API 用法与技术思路（本项目为独立实现，未复制代码）：

- [XB1ControllerBatteryIndicator](https://github.com/NiyaShy/XB1ControllerBatteryIndicator)（GPLv2）——XInput 电量读取行为、低电量 Toast 触发策略、多手柄图标轮换的产品形态
- [XBatteryStatus](https://github.com/tommaier123/XBatteryStatus)（GPLv3）——.NET 8 + WinForms + WinRT BLE 的完整先例、GDI+ 托盘图标绘制与 GDI 句柄防泄漏实践
