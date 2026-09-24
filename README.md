# XboxBatteryMonitor

Windows 系统托盘小工具：实时显示 Xbox 手柄电量，低电量时 Toast 通知 + 手柄震动提醒。

## 功能

- 蓝牙连接的手柄：BLE 标准电量服务（0x180F），精确百分比
- Xbox 无线适配器 / USB 连接的手柄：XInput 电量（4 档估算百分比）
- 两种连接方式可同时在线（混合场景自动去重，蓝牙读数优先）
- 托盘图标轮换显示多个手柄，右键菜单列出全部
- 低电量双档提醒（默认警告 20% / 危险 10%，可自定义），阈值跨越时触发一次，不重复轰炸
- 震动提醒可关闭

## 使用

要求 Windows 10 19041+ / Windows 11 与 .NET 8 Desktop Runtime。

```bash
dotnet run --project src/XboxBatteryMonitor
```

设置保存在 `%LocalAppData%\XboxBatteryMonitor\settings.json`，日志在同目录 `log.txt`。

## 已知限制

- XInput 只能给出 4 档电量（空/低/中/满，估算 10/40/70/100%）；精确百分比只有蓝牙手柄可读
- 蓝牙 + 适配器手柄同时在线时，适配器手柄显示的档位可能实际来自另一槽位
  （XInput 公开 API 无法区分槽位归属）；此场景下蓝牙手柄低电只发 Toast 不震动
- 蓝牙手柄需固件支持标准 BLE 电量服务（Xbox One S 之后的固件均支持；
  不支持时会被当作适配器手柄显示 4 档估算）
- 适配器手柄刚连接约 10 秒内可能显示"等待读数"（XInput API 限制）

## 开发

```bash
dotnet build
dotnet test
```

设计文档见 `docs/design.md`，真实硬件手动验证清单见 `docs/manual-verification.md`。
