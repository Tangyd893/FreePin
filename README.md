<p align="center">
  <img src="https://img.shields.io/badge/.NET%20Framework-4.8-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET Framework 4.8">
  <img src="https://img.shields.io/badge/Windows-10%20%2F%2011-0078D6?style=for-the-badge&logo=windows&logoColor=white" alt="Windows 10 / 11">
  <img src="https://img.shields.io/badge/UI-WPF-7B5AA6?style=for-the-badge" alt="WPF">
  <img src="https://img.shields.io/badge/Version-1.4.0-B44E32?style=for-the-badge" alt="v1.4.0">
  <img src="https://img.shields.io/badge/License-MIT-2EA44F?style=for-the-badge" alt="MIT">
</p>

<h1 align="center">📌 FreePin</h1>
<h3 align="center">Windows 上「截、标、贴」一气呵成的轻量截图工具</h3>
<p align="center">把截图从「先框选，再打开编辑器」变成「框选即标注，回车即完成」。</p>
<p align="center">
  <b>Ctrl+1 截图 → 智能选区 → 就地标注 → 复制 / 贴图 / OCR</b>
</p>

---

## 💡 核心理念

> **截图工具的瓶颈不该是工具本身。注意力应该留在内容上，而不是留在窗口切换和等待上。**

FreePin = **Free + Pin**。Free 是轻量、免费、本地优先；Pin 是把截图钉在桌面最上层。参考 PixPin、ShareX 的交互，但刻意不堆功能：不接翻译、不上传、不录屏、不云同步，先把 Windows 上最高频的截图链路做顺。

用 .NET Framework 4.8 + WPF 而不是 Electron，是为了让一个「随手按 Ctrl+1」的工具只占很少内存、不要求安装额外运行时；把标注放回截图遮罩层，是为了消掉「截完图再打开一个编辑器」的等待和上下文切换。

## 🏗️ 工作流

```
Ctrl+1 / 托盘左键
        │
        ▼
智能选区（窗口 / 控件高亮，或拖动自由框选）
        │
        ▼
就地标注（画笔 / 直线 / 箭头 / 矩形 / 文字 / 取色）
        │
        ▼
复制 · 保存 · 贴图 · OCR · 长截图
        │
        ▼
最近截图缓存（Ctrl+3）
```

## 📁 项目结构

```
FreePin/
├── 📖 README.md
├── 🚫 .gitignore
├── 🔨 build.ps1                  # 构建脚本，自动准备 net48 引用程序集
├── 📜 LICENSE
├── 📜 THIRD-PARTY-NOTICES.txt
├── 🎨 assets/                    # F + 图钉图标
├── 📦 lib/                       # vendored 依赖，clone 后无需 NuGet restore
│   ├── Hardcodet.NotifyIcon.Wpf.dll
│   └── Newtonsoft.Json.dll
└── 🖥️ src/FreePin/
    ├── Program.cs                # 单实例、DPI 感知、启动入口
    ├── AppHost.cs                # 托盘、快捷键、窗口生命周期
    ├── UI/                       # 截图遮罩、工具条、放大镜、贴图、缓存、设置
    ├── Services/                 # 截图、智能选区、OCR、缓存、设置、快捷键
    ├── Annotations/              # 标注模型与合成渲染
    ├── Imaging/                  # 图像工具、标注画布
    ├── Models/                   # 设置与快捷键模型
    ├── Native/                   # Win32 P/Invoke
    └── FreePin.csproj            # classic net48 WinExe (x64)
```

## 🚀 快速开始

### ✅ 前置条件

| 依赖 | 必需 | 说明 |
|:-----|:----:|------|
| **Windows 10 / 11 x64** | ✅ | 仅支持 Windows 桌面 |
| **.NET Framework 4.8** | ✅ | Windows 10/11 已自带 |
| **Visual Studio 2022 Build Tools** | 仅构建 | 需要 MSBuild 与「.NET 桌面开发」工作负载 |
| **Windows SDK 10** | 仅构建 | 提供 OCR 所需的 `Windows.winmd` |
| **PowerShell 5.1+** | 仅构建 | 运行 `build.ps1` |

> 💡 **构建前自检：**
> `& "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe" -version`

### 📦 Step 1 — 获取程序

直接下载可用版本（推荐）：

从 [GitHub Releases](https://github.com/Tangyd893/FreePin/releases) 下载 `FreePin-v1.4.0-win-x64.zip`，解压后运行 `FreePin.exe`。

从源码构建：

```powershell
git clone https://github.com/Tangyd893/FreePin.git
cd FreePin
```

### ⚙️ Step 2 — 构建

在仓库根目录执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

首次构建会下载 `Microsoft.NETFramework.ReferenceAssemblies.net48` 到 `tools\refasm`（已 gitignore），不需要 NuGet restore。构建产物为 `dist\FreePin\FreePin.exe`。

需要从干净状态重新构建：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Clean
```

> 程序依赖 Windows SDK 的 `Windows.winmd` 和系统 `System.Runtime.WindowsRuntime`。仓库按 Windows SDK 10.0.26100 配置；如果构建机 SDK 版本不同，请调整 `src\FreePin\FreePin.csproj` 中 `Windows.winmd` 的 `HintPath`。

### ▶️ Step 3 — 运行

正常启动（驻留托盘）：

```powershell
.\dist\FreePin\FreePin.exe
```

直接打开设置窗口：

```powershell
.\dist\FreePin\FreePin.exe --settings
```

程序启动后，按 `Ctrl+1` 开始截图；也可以左键单击托盘图标。截图完成后不会打开独立编辑器。

## ✨ 特性

### 🖱️ 截图：少一步，快一拍

- **`Ctrl+1` 全局截图**：框选后立即在遮罩层内调整和标注，不打开第二个窗口。
- **智能选区**：鼠标悬停时高亮当前窗口或可访问控件，单击直接选中；从高亮块内拖动超过阈值则自动回退到自由框选。检测完全在本机通过 Windows UI Automation 完成，不联网、不上传截图。
- **预取缓存**：1.3.1 会在启动、切换前台窗口和按下截图快捷键前预热 UIA 控件树，悬停预览直接命中缓存；候选框使用轻量覆盖层，不再每次移动都重绘整屏。
- **八向调整**：鼠标拖动四条边和四个角；按住手柄时方向键每次移动 1 个物理像素，`Shift`/`Ctrl` + 方向键向内收缩/向外扩展。
- **像素放大镜**：选区外侧固定显示像素网格、坐标、RGB 和 HEX，方便确认边缘颜色。
- **工具条跟随选区**：优先出现在选区下方，放不下时自动翻到上方，再不行才放进选区内，并始终限制在屏幕内。

### ✍️ 就地标注

- 画笔、直线、箭头、矩形（直角/圆角）、文字、取色器，颜色和笔触大小即时生效。
- 笔触以圆点 + 数字 px 显示，鼠标滚轮即可调整；文字工具滚轮调字号。
- 支持撤销/重做、选中标注后拖动、`Delete` 删除、`Ctrl+Z`/`Ctrl+Y`。
- 截图完成动作会先把标注合成到图像：复制、保存、贴图、OCR 都拿到最终图。
- 普通截图不再打开 `EditorWindow`；它只保留给长截图完成后的结果查看。

### 📌 贴图与最近截图

- **`Ctrl+2` 贴图**：从剪贴板取图并置顶显示，可拖动、滚轮缩放、右键复制/识别文字/另存/关闭，`Esc` 关闭。
- **双击关闭**：可在设置中开关；关闭后双击只激活贴图，仍可用右键或 `Esc` 关闭。
- **`Ctrl+3` 最近截图**：缩略图列表；右键单张可复制、另存为或删除。
- **缓存策略**：可设置启用、保留天数、最多数量和缓存目录，超出限制自动清理。

### 🔤 OCR 与长截图

- **OCR**：使用 Windows 自带的 `Windows.Media.Ocr`，识别结果按视觉行自动换行；结果文字可直接编辑后再复制。当前机器识别语言为简体中文（zh-Hans-CN），其他语言取决于 Windows 语言包。
- **长截图**：选择区域后点「长截图」，滚动页面，工具条会自动拼接，完成后进入长截图编辑器。
- **贴图 OCR**：贴图右键菜单可直接识别文字，结果窗口置顶且可编辑。

### ⚙️ 设置与本地优先

- 三个全局快捷键均可单独启用/停用、重新绑定；保存时检查冲突，失败不会破坏当前生效的快捷键。
- 截图颜色与笔触大小可记忆，可在设置中关闭。
- **开机自启动**可开关：写入当前用户 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`，不请求管理员权限；便携目录移动后，下次启动会自动修正启动命令。
- 单实例运行，关闭窗口后驻留托盘。
- 不联网、不上传截图；设置、日志、缓存全部在 `%LOCALAPPDATA%\FreePin`。

## 📂 本地数据

| 路径 | 内容 |
|:-----|------|
| `%LOCALAPPDATA%\FreePin\settings.json` | 快捷键、缓存策略、截图样式、智能选区、开机自启动等设置 |
| `%LOCALAPPDATA%\FreePin\Cache\FreePin_yyyyMMdd_HHmmss_fff.png` | 最近截图缓存 |
| `%LOCALAPPDATA%\FreePin\Logs\freepin.log` | 运行日志 |

## ⌨️ 快捷键

### 全局

| 操作 | 快捷键 |
|:-----|:-------|
| 截图 | `Ctrl+1` |
| 贴图 | `Ctrl+2` |
| 最近截图 | `Ctrl+3` |

三个快捷键都可以在设置里重新绑定。

### 截图遮罩内

| 操作 | 快捷键 |
|:-----|:-------|
| 完成并复制 | `Enter` / `Ctrl+C` |
| 保存 | `Ctrl+S` |
| 取消 | `Esc` |
| 选择/移动 | `V` |
| 画笔 | `P` |
| 直线 | `L` |
| 箭头 | `A` |
| 矩形 | `R` |
| 文字 | `T` |
| 取色器 | `I` |
| 移动选区 / 按住手柄时微调当前边 | 方向键（每次 1 物理像素） |
| 向内收缩 / 向外扩展对应边 | `Shift` / `Ctrl` + 方向键 |
| 撤销 / 重做 | `Ctrl+Z` / `Ctrl+Y` |
| 删除选中标注 | `Delete` |

长截图完成后的编辑器沿用原有快捷键：`Ctrl+C` 复制、`Ctrl+S` 保存、`Ctrl+Z`/`Ctrl+Y` 撤销/重做、`Esc` 关闭；工具快捷键 `V` 选择、`R` 矩形、`O` 椭圆、`A` 箭头、`L` 直线、`P` 画笔、`T` 文字、`M` 马赛克。

## 🏛️ 架构

```
WPF UI
├── CaptureOverlayWindow / CaptureSurface / CaptureToolbar
├── PinWindow / CacheWindow / SettingsWindow / LongCaptureWindow
│
AppHost
├── ScreenCapture ── Win32 GDI 虚拟屏幕截图
├── SmartSelectionService ── EnumWindows + UI Automation 控件树
├── OcrService ── Windows.Media.Ocr
├── AnnotationCanvas / AnnotationRenderer ── 标注合成
└── Settings / Cache / HotkeyManager / TaskbarIcon
│
Win32 + System.Windows.Automation + Windows Runtime
```

| 层级 | 技术 |
|:-----|:-----|
| UI | WPF / .NET Framework 4.8 / x64 |
| 截图 | Win32 虚拟屏幕 + DPI 感知 |
| 智能选区 | `EnumWindows` + Windows UI Automation（本地，无 AI） |
| OCR | `Windows.Media.Ocr` + `Windows.winmd` |
| 数据 | 本地 JSON 设置 + PNG 缓存 |
| 依赖 | Hardcodet.NotifyIcon.Wpf 1.1.0 / Newtonsoft.Json 13.0.3 |

## 🗃️ 构建资源说明

- `lib/Hardcodet.NotifyIcon.Wpf.dll` 与 `lib/Newtonsoft.Json.dll` 已随仓库提供。
- `tools/refasm` 是构建时自动下载的 .NET Framework 4.8 引用程序集，已被 `.gitignore` 排除。
- `dist/` 是构建产物目录，不进入仓库；发布包只包含 `FreePin.exe` 和所需 DLL，不需要安装 .NET 运行时。

## ❓ 常见问题

| 问题 | 解决方案 |
|:-----|---------|
| 智能选区一开始不够跟手 | 1.3.1 已加入前台窗口预取缓存；刚切换到从未预热过的窗口时，最初约 0.1–0.3 秒可能先显示窗口级候选框，之后恢复控件级。若仍不满意，可在设置中关闭智能选区。 |
| 网页里只能选整块/整窗 | 浏览器暴露给 UI Automation 的节点有限，Phase 1 不做 OCR/视觉分段；整窗回退是预期行为，不是崩溃。 |
| OCR 不可用 | 在 Windows「设置 → 时间和语言 → 语言」中安装对应语言的 OCR 功能；当前默认使用简体中文（zh-Hans-CN）。 |
| 快捷键注册失败 | 通常是被其他程序占用；打开设置重新绑定，保存时会检查冲突。 |
| 构建时提示禁止运行脚本 | 使用 `powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1`。 |
| 构建失败，找不到 `Windows.winmd` | 修改 `src\FreePin\FreePin.csproj` 里的 Windows SDK 路径，指向本机已安装的 `UnionMetadata\<版本>\Windows.winmd`。 |
| 翻译功能在哪里 | 已从当前版本移除，作为后续扩展项；界面不显示翻译入口。 |
| 开机自启动没有生效 | 在设置中勾选「开机自启动」并保存；Windows 11 可能在「任务管理器 → 启动应用」中把它标记为关闭，请在那里恢复。 |
| FreePin 会把截图上传吗 | 不会。截图、OCR、智能选区全部在本机完成。 |

## 📜 License

FreePin 源代码使用 [MIT License](LICENSE)。第三方组件许可见 [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt)。

---

<p align="center">
  <b>⭐ 觉得有用？点个 Star 支持一下！</b>
</p>

<p align="center">
  <a href="https://github.com/Tangyd893/FreePin">github.com/Tangyd893/FreePin</a>
</p>
