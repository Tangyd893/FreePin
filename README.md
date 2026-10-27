# FreePin

FreePin 是一个面向 Windows 的轻量截图、贴图与 OCR 工具，使用 .NET Framework 4.8 + WPF 构建，不依赖 .NET SDK 或额外的运行时。

## 功能

- **`Ctrl+1` 截图**：拖选区域，支持复制（Enter）、贴图、保存、识别文字、长截图、进入标注编辑器。
- **标注编辑器**：矩形、椭圆、箭头、直线、自由画笔、文字、马赛克，颜色/线宽选择，撤销/重做，复制、保存、贴图、OCR。
- **`Ctrl+2` 贴图**：从剪贴板取图并置顶显示，可拖动、滚轮缩放、右键复制/另存/关闭，`Esc` 关闭。
- **`Ctrl+3` 最近截图**：展示近期截图缓存；右键单张图片可复制、另存为或删除。
- **最近截图缓存**：可设置是否启用、保留天数、最多数量、缓存目录；超出限制自动清理。
- **长截图**：在截图上选择区域后点「长截图」，滚动页面，工具条会自动拼接；点「完成」进入编辑器。
- **OCR**：使用 Windows 自带的 `Windows.Media.Ocr`，识别结果按视觉行自动换行，可直接复制。
- **快捷键设置**：三个动作均可单独启用/停用、重新绑定；保存时检查快捷键冲突，失败不会破坏当前生效的快捷键。
- **托盘与单实例**：关闭窗口后留在托盘；重复启动会提示已运行。
- **本地优先**：不联网、不上传截图；设置和缓存保存在 `%LOCALAPPDATA%\FreePin`。

## 快捷键

| 快捷键 | 功能 |
| --- | --- |
| `Ctrl+1` | 区域截图 |
| `Ctrl+2` | 把剪贴板图片贴到桌面最上层 |
| `Ctrl+3` | 最近截图弹窗 |

这些快捷键都可以在设置中修改。截图 overlay 内：`Enter` 复制，`Esc` 取消；编辑器内：`Ctrl+C` 复制、`Ctrl+S` 保存、`Ctrl+Z`/`Ctrl+Y` 撤销/重做、`Esc` 关闭；工具快捷键 `V` 选择、`R` 矩形、`O` 椭圆、`A` 箭头、`L` 直线、`P` 画笔、`T` 文字、`M` 马赛克。

## 构建

要求：

- Windows 10/11 x64
- Visual Studio 2022 Build Tools（需要 MSBuild；`.NET 桌面开发` 工作负载即可）
- PowerShell 5.1 或更高版本
- .NET Framework 4.8（Windows 10/11 已自带）

在仓库根目录执行：

```powershell
.\build.ps1
```

如果系统提示「禁止运行脚本」，改用：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

首次构建会自动下载 `Microsoft.NETFramework.ReferenceAssemblies.net48` 到 `tools\refasm`；构建产物在 `dist\FreePin\FreePin.exe`。清理后重新构建：

```powershell
.\build.ps1 -Clean
```

> 程序使用 Windows SDK 的 `Windows.winmd` 和系统 `System.Runtime.WindowsRuntime` 调用 OCR。仓库默认按本机 Windows SDK 10.0.26100 的路径引用；如果构建机 SDK 版本不同，请调整 `src\FreePin\FreePin.csproj` 中 `Windows.winmd` 的 `HintPath`。

## 运行与设置

直接双击 `dist\FreePin\FreePin.exe` 即可，程序会驻留系统托盘。托盘右键菜单可打开设置或退出。也可以使用：

```powershell
.\dist\FreePin\FreePin.exe --settings
```

直接打开设置窗口（便于首次配置）。

配置文件：`%LOCALAPPDATA%\FreePin\settings.json`  
日志：`%LOCALAPPDATA%\FreePin\Logs\freepin.log`  
默认缓存：`%LOCALAPPDATA%\FreePin\Cache`

## 技术结构

- `src/FreePin/Program.cs`：单实例、DPI 感知、应用启动。
- `src/FreePin/AppHost.cs`：托盘、全局快捷键、各窗口生命周期。
- `src/FreePin/UI/`：截图 overlay、编辑器、贴图、缓存、设置、长截图工具条。
- `src/FreePin/Annotations/`：标注模型与渲染。
- `src/FreePin/Services/`：截图、OCR、缓存、设置、快捷键、日志。
- `lib/`：随仓库提供的 `Hardcodet.NotifyIcon.Wpf` 与 `Newtonsoft.Json` DLL。

翻译功能已从当前版本移除，作为后续扩展项；当前版本不显示翻译入口。

## 许可

FreePin 源代码使用 MIT License。第三方组件许可见 `THIRD-PARTY-NOTICES.txt`。
