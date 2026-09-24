# XboxBatteryMonitor 设计文档

- 日期：2026-09-25
- 状态：已获用户批准（头脑风暴阶段两轮确认）
- 项目位置：本仓库根目录

## 1. 背景与目标

做一个 Windows 系统托盘小工具，实时显示 Xbox 手柄电量并在低电量时提醒。

核心需求：

1. 同时支持 **Xbox 无线适配器（dongle）/ USB 连接**的手柄（经 XInput API，电量 4 档）
2. 同时支持 **蓝牙连接**的手柄（经 BLE 标准电量服务，精确百分比）
3. 两种连接方式的手柄**可以同时在线**（混合场景）
4. 低电量提醒：系统 Toast 通知 + 手柄震动提醒
5. 多手柄：托盘图标轮换显示 + 右键菜单列出全部

参考项目（仅参考 API 用法，不复制代码——两者均为 GPL，XB1ControllerBatteryIndicator 为 GPLv2、XBatteryStatus 为 GPLv3）：

- `../XB1ControllerBatteryIndicator/`：XInput 电量读取、Toast 样板、多手柄图标轮换
- `../XBatteryStatus/`：.NET 8 + WinForms + WinRT BLE 的完整先例、GDI+ 图标绘制、句柄防泄漏

## 2. 已确认的决策记录

| 决策点 | 结论 |
|---|---|
| 工具形态 | 系统托盘应用（无主窗口） |
| 技术栈 | C# / .NET 8 / WinForms（`net8.0-windows10.0.19041.0`） |
| 多手柄展示 | 托盘图标每 5 秒轮换 + tooltip/菜单列出全部 |
| 低电量策略 | 双档阈值（默认警告 20% / 危险 10%），**阈值数值可在设置中修改** |
| 震动提醒 | 低电量时 Toast + 手柄震动双脉冲；可在设置中关闭震动 |
| 实现方式 | 全新代码，借鉴参考项目的 API 调用思路（避免 GPL 传染） |

## 3. 总体架构

```
┌─────────────┐   ┌─────────────┐
│ XInputMonitor│   │  BleMonitor │
│ 适配器/USB手柄│   │  蓝牙手柄    │
│ (P/Invoke)   │   │ (WinRT BLE) │
└──────┬──────┘   └──────┬──────┘
       └───────┬─────────┘
        ┌──────▼──────┐     ┌──────────────────┐
        │ Aggregator  │◄────│ ThresholdDetector │
        │ 合并+去重    │     │ 阈值跨越检测       │
        └──────┬──────┘     └──────┬───────────┘
               │                   │
        ┌──────▼───────────────────▼──────┐
        │        AppContext (UI 层)        │
        │ 托盘图标(GDI+) / 菜单 / 设置窗体   │
        │ ToastNotifier / RumbleNotifier   │
        └─────────────────────────────────┘
```

设计原则：监控器（访问原生 API）与聚合逻辑（纯函数）分离，聚合与阈值检测可完全单元测试；UI 层只做组装和呈现。

## 4. 组件设计

### 4.1 Models（`Core/Models.cs`）

- `ControllerStatus`：`Id`、`DisplayName`、`ConnectionKind`（`Bluetooth` / `Adapter` / `Usb`）、`BatteryValue`
- `BatteryValue`（oneOf 语义）：
  - `Percent(int)`（BLE 精确值，0–100）
  - `Level(BatteryLevel)`（XInput 4 档：Empty=10 / Low=40 / Medium=70 / Full=100 的估算百分比）
  - `Wired`（USB 供电）
  - `Waiting`（刚连接尚未上报电量）
- `AppSettings`：`WarningThreshold`（默认 20）、`CriticalThreshold`（默认 10）、`RumbleEnabled`（默认 true）
- 约束：`0 < CriticalThreshold < WarningThreshold < 100`，加载时校验并回退默认值

### 4.2 IXInputApi / XInputApi（`Monitors/`）

- P/Invoke `xinput1_4.dll` 的三个函数：`XInputGetState`（探测连接）、`XInputGetBatteryInformation`（读电量）、`XInputSetState`（震动）
- 结构体：`XINPUT_BATTERY_INFORMATION { byte BatteryType; byte BatteryLevel; }`
- `BatteryType`：`Disconnected=0`（含"已检测到但未上报电量"）、`Wired=1`、`Alkaline=2`、`Nimh=3`、`Unknown=255`
- `BatteryLevel`：`Empty=0`、`Low=1`、`Medium=2`、`Full=3`
- 接口化以便单元测试注入 fake；DLL 加载失败时通道禁用（见 §8）

### 4.3 XInputMonitor（`Monitors/`）

- 每 **2 秒**轮询 4 个槽位，产出快照：每个已连接槽位的电量状态
- 对外暴露统一快照事件（见 §5），并提供 `Rumble(slot, pattern)` 能力
- 轮询在后台线程，事件经 `SynchronizationContext` 封送回 UI 线程

### 4.4 BleMonitor（`Monitors/`）

- 发现：`DeviceInformation.FindAllAsync()` 枚举 + `BluetoothLEDevice.FromIdAsync` 后按 `Appearance.SubCategory == BluetoothLEAppearanceSubcategories.Gamepad` 筛选，且须存在电量服务；每 **60 秒**重新发现一次；监听蓝牙 Radio 开关事件（`Radio.GetRadiosAsync` + `StateChanged`）
- 读电量：电量服务 `0x180F`、特征 `0x2A19`，对**已连接**设备每 **10 秒** `ReadValueAsync()` 轮询单字节百分比（不订阅 Notify，与参考项目实践一致）
- 连接感知：每设备订阅 `ConnectionStatusChanged`，连接/断开即时触发刷新
- 多个同名设备在显示名后加序号区分

### 4.5 Aggregator（`Core/Aggregator.cs`，纯函数）

输入两通道快照，输出统一手柄列表。去重规则见 §6。

### 4.6 ThresholdDetector（`Core/ThresholdDetector.cs`，纯状态机）

- 输入：手柄当前电量（统一为估算百分比）+ 上次电量 + 配置阈值
- 行为：从 `> T` 跌到 `≤ T` 时产生一次对应档位事件（警告/危险）；电量回升超过阈值或手柄重连后重新武装
- `Waiting` / `Wired` 状态不参与检测

### 4.7 AppContext（UI 组装层）

- 持有 `NotifyIcon`、两个监控器、聚合器、阈值检测器、渲染器、通知器
- 托盘图标每 **5 秒**轮换到下一个手柄；tooltip 显示全部手柄及电量；右键菜单：各手柄项（只读展示）+ 设置… + 退出
- 无手柄时显示"无连接"图标

### 4.8 TrayIconRenderer（`UI/`）

- GDI+ 在 32×32 画布绘制电池轮廓 + 电量填充（百分比或档位映射）
- 多手柄时图标左下角绘制小序号角标（1/2/3…）
- 特殊符号：`?`（等待读数）、`w`（有线）、空电池轮廓（无连接）
- 换图标时 `GetHicon()` → 使用后 `DestroyIcon()`，防 GDI 句柄泄漏

### 4.9 ToastNotifier（`UI/`）

- 使用 `Microsoft.Toolkit.Uwp.Notifications`（`ToastContentBuilder`），该包对未打包桌面应用自动处理 AUMID/快捷方式，无需手写 ShellHelpers（XBatteryStatus 在 .NET 8 上已验证可用）

### 4.10 RumbleNotifier（`UI/`）

- 触发模式：4 个脉冲（左右马达全速各约 300ms、脉冲间隔 200ms），总时长约 2 秒；多次脉冲是为了在游戏持续刷新震动状态时仍能命中；结束后恢复 0
- 目标槽位选择规则见 §7

### 4.11 SettingsForm + SettingsStore（`UI/` + `Core/`）

- 设置窗体：警告阈值、危险阈值（NumericUpDown，0–99）、震动开关（CheckBox）
- 持久化：JSON 文件 `%LocalAppData%\XboxBatteryMonitor\settings.json`；损坏/缺失时回退默认值

### 4.12 Program.cs

- 命名 `Mutex` 单实例（第二次启动静默退出）
- 全局异常处理 → 日志
- `Application.Run(new AppContext())`

## 5. 数据流

```
XInputMonitor ──(2s 轮询)──┐
                           ├─► Aggregator ─► 快照 ─► AppContext（图标/tooltip/菜单）
BleMonitor ──(10s 轮询 +────┘       │
   连接事件 + 60s 重发现)            ▼
                        ThresholdDetector ─► 事件 ─► ToastNotifier + RumbleNotifier
```

- 所有后台线程事件经 UI 线程的 `SynchronizationContext.Post` 封送
- 系统从休眠恢复（`SystemEvents.PowerModeChanged`）后强制一次全量刷新

## 6. 去重算法

**问题**：蓝牙手柄同时占用一个 XInput 槽位且电量是错值，直接合并会重复计数、显示错误电量。

**规则**（按序执行）：

1. BLE 通道：每个**已连接**蓝牙手柄生成一条 `Bluetooth` 记录（精确百分比 + 蓝牙设备名）
2. XInput 通道：统计无线槽位数 `K`（`BatteryType ∈ {Alkaline, Nimh, Disconnected}` 且 `IsConnected`）；蓝牙已连接数记 `N`
3. 适配器手柄数 `D = max(0, K − N)`，从槽位中按"`BatteryType ∈ {Alkaline, Nimh}` 优先（有真实读数），再按槽位号升序"选取 `D` 个，生成 `Adapter` 记录
4. `BatteryType == Wired` 的槽位生成 `Usb` 记录（显示"有线"，不参与低电量提醒）

**已知限制**（XInput 公开 API 无法区分槽位归属，接受并在 README 注明）：蓝牙+适配器手柄同时在线时，适配器手柄显示的档位可能实际来自另一槽位；蓝牙手柄固件过旧不暴露 BLE 电量服务时会按适配器手柄处理。

## 7. 通知与震动触发

- **触发条件**：电量（BLE 百分比或 XInput 档位估算百分比）**跨越**阈值的那一刻，每档只触发一次
- 警告档（≤ 警告阈值）：Toast"手柄名 电量低（xx%）" + 震动
- 危险档（≤ 危险阈值）：Toast"手柄名 电量严重不足（xx%）" + 震动
- **震动目标槽位**：
  - 适配器/USB 手柄：精确对其槽位 ✅
  - 蓝牙手柄：震动指令经 XInput 下发。当**无线槽位总数 = 1** 时精确匹配 ✅（覆盖单手柄的绝大多数场景）；多无线手柄且低电量的为蓝牙手柄时存在槽位歧义 → **只发 Toast 不震动**（宁可少提醒不错提醒）
- 震动开关关闭时只发 Toast

## 8. 错误处理

| 场景 | 处理 |
|---|---|
| 蓝牙关闭 / 无蓝牙适配器 | BLE 通道标记不可用（静默降级），仅 XInput 工作；tooltip 提示"蓝牙已关闭" |
| 蓝牙设备打开失败 / 权限问题 | 跳过该设备，记录日志，不影响其他设备 |
| `xinput1_4.dll` 不存在 | XInput 通道禁用，仅 BLE 工作 |
| BLE 读取连续失败 3 次 | 该设备标记离线，等待连接事件重新拉起 |
| 手柄刚连上未上报电量 | 显示"等待读数"（`?`），不触发通知 |
| GDI 句柄泄漏 | 每次换图标 `DestroyIcon` 旧句柄 |
| 重复启动 | Mutex 单实例，二次启动直接退出 |
| 未捕获异常 | 全局异常处理 → `%LocalAppData%\XboxBatteryMonitor\log.txt`（Debug 同时输出控制台）；日志写入用信号量串行化 |
| 系统休眠/恢复 | 恢复后强制一次全量刷新 |

## 9. 测试策略（TDD）

可测逻辑全部抽为纯函数或接口注入，**先写测试再写实现**：

| 测试对象 | 方式 | 覆盖点 |
|---|---|---|
| `Aggregator` | xUnit 纯函数测试 | 只 BLE / 只 XInput / 混合去重（K−N 规则、D 钳制 0、Alkaline 优先、有线排除）/ 全空 |
| `ThresholdDetector` | xUnit 状态机测试 | 跨越触发一次、同档不重复、回升重武装、4 档百分比映射、Waiting/Wired 不触发、重连重置 |
| `TrayIconRenderer` | Bitmap 像素断言 | 满电/空电/等待/有线/角标绘制 |
| `SettingsStore` | 临时目录 JSON | 默认值、往返持久化、损坏文件回退、非法阈值回退 |
| `XInputMonitor` | 注入 `FakeXInputApi` | 轮询快照转换、槽位连接/断开、Waiting 状态 |
| 真实硬件路径 | **手动验证清单**（需真实手柄） | 蓝牙连接读数、适配器连接读数、混合场景、真实 Toast、真实震动、蓝牙开关、休眠恢复 |

## 10. 项目结构

```
XboxBatteryMonitor/
├─ XboxBatteryMonitor.sln
├─ src/XboxBatteryMonitor/
│   ├─ Program.cs
│   ├─ AppContext.cs
│   ├─ Core/
│   │   ├─ Models.cs
│   │   ├─ Aggregator.cs
│   │   ├─ ThresholdDetector.cs
│   │   └─ SettingsStore.cs
│   ├─ Monitors/
│   │   ├─ IXInputApi.cs
│   │   ├─ XInputApi.cs
│   │   ├─ XInputMonitor.cs
│   │   └─ BleMonitor.cs
│   └─ UI/
│       ├─ TrayIconRenderer.cs
│       ├─ ToastNotifier.cs
│       ├─ RumbleNotifier.cs
│       └─ SettingsForm.cs
└─ tests/XboxBatteryMonitor.Tests/
    ├─ AggregatorTests.cs
    ├─ ThresholdDetectorTests.cs
    ├─ TrayIconRendererTests.cs
    ├─ SettingsStoreTests.cs
    └─ XInputMonitorTests.cs
```

NuGet 依赖（主项目仅 1 个）：`Microsoft.Toolkit.Uwp.Notifications`；测试项目：`xunit`、`xunit.runner.visualstudio`、`Microsoft.NET.Test.Sdk`。

## 11. 非目标（明确不做）

开机自启、多语言、自动更新、安装包、每手柄独立图标、非 Xbox 手柄、BLE Notify 订阅、设置图标主题。

## 12. 技术要点备忘（实现时参考）

- TFM 必须带版本号：`net8.0-windows10.0.19041.0`，否则 WinRT 蓝牙投影不可用
- XInput 电量仅对适配器/USB 连接可靠（微软 BT 栈限制，参考项目 Issue #49）；蓝牙电量必须走 BLE 0x180F/0x2A19
- BLE 轮询用 `ReadValueAsync` 即可；不需要 `GattSession.MaintainConnection`
- Toast 在未打包应用需 AUMID，`Microsoft.Toolkit.Uwp.Notifications` 的 `ToastNotificationManagerCompat` 自动完成
- 图标：`Bitmap.GetHicon()` → `Icon.FromHandle(...).Clone()` → 用完 `DestroyIcon`（两个句柄都要处理）
- XInput 震动对蓝牙手柄有效（与电量读取不同），这是震动提醒可行的依据
