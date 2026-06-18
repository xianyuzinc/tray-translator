# TrayTranslator

<p align="center">
  <strong>A lightweight Windows tray translator for selected text.</strong>
</p>

<p align="center">
  <a href="#english">English</a> | <a href="#简体中文">简体中文</a>
</p>

<p align="center">
  <a href="LICENSE"><img alt="License: MIT" src="https://img.shields.io/badge/License-MIT-blue.svg"></a>
  <img alt="Platform: Windows" src="https://img.shields.io/badge/platform-Windows-0078D4.svg">
  <img alt=".NET Framework" src="https://img.shields.io/badge/.NET%20Framework-4.5%2B-512BD4.svg">
  <img alt="UI: WinForms" src="https://img.shields.io/badge/UI-WinForms-lightgrey.svg">
</p>

<a id="english"></a>

## English

TrayTranslator is a small Windows tray app that translates text selected in almost any desktop application. Select text, press `Ctrl+Shift+T`, and a compact floating window appears near the cursor with results from multiple translation engines.

It is designed to stay lightweight: no Electron, no embedded browser, no OCR pipeline, and no always-on screen monitoring. The app only reads the current selection when you trigger translation or when the popup is already open and you select another piece of text.

### Features

- Global hotkey translation: select text anywhere and press `Ctrl+Shift+T`.
- Floating popup: draggable, resizable, always on top, and designed for quick reading.
- Reader mode: expand any translation result into a larger resizable reading window with font zoom.
- Follow-up selection: after the popup is open, select new text in another app to translate again without pressing the hotkey.
- Multi-engine output: DeepL, Google Cloud Translation, Baidu Translate, and DeepSeek AI.
- Language controls: source auto/mixed by default, target simplified Chinese by default, with common language options.
- Fast text capture: clipboard sequence detection, Word selection fast path, and fallback copy methods.
- PDF-friendly cleanup: joins abnormal line breaks before sending text to translators.
- Local privacy: API keys are saved with Windows DPAPI under the current Windows user.

### Requirements

- Windows 10 or later is recommended.
- .NET Framework 4.8 runtime is recommended.
- Visual Studio 2019 or MSBuild for building from source.

The project targets `.NET Framework 4.5` for broad compatibility, and it runs on machines with newer .NET Framework versions installed.

### Installation

Download `TrayTranslatorSetup.exe` from [Releases](https://github.com/xianyuzinc/tray-translator/releases) and double-click it.

The installer is user-level and lightweight:

- Installs to `%LocalAppData%\Programs\TrayTranslator`.
- Creates a Start Menu shortcut.
- Registers an uninstall entry for the current Windows user.
- Does not require administrator privileges in normal cases.

To build the installer yourself:

```powershell
.\installer\build-setup.ps1
```

The setup executable will be generated at:

```text
dist\TrayTranslatorSetup.exe
```

### Build

Open `TrayTranslator.sln` with Visual Studio 2019, or build with MSBuild:

```powershell
& "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe" .\TrayTranslator.sln /p:Configuration=Release
```

The executable will be generated at:

```text
bin\Release\TrayTranslator.exe
```

### Usage

1. Start `TrayTranslator.exe`.
2. Right-click the tray icon and open `Settings`.
3. Enter the API keys for the translation providers you want to use.
4. Select text in Word, a browser, Codex, a PDF reader, WPS, Markdown editors, or other desktop apps.
5. Press `Ctrl+Shift+T`.
6. Keep the popup open and select another text range in another app to translate again automatically.
7. For long text, resize the popup from its right/bottom edge or click `展开` on a result card to open the reader window.

If the target app runs as administrator, TrayTranslator may also need to run as administrator so Windows allows simulated copy input.

### Translation Providers

TrayTranslator supports multiple providers. You can enable or disable each provider in settings.

| Provider | Credential | Notes |
| --- | --- | --- |
| DeepL | API Key | Uses the Free or Pro API endpoint depending on the key type. |
| Google Cloud Translation | API Key | Uses Cloud Translation Basic v2. |
| Baidu Translate | API Key, optional APPID | Uses Baidu's API key endpoint first, with legacy APPID/sign support kept as fallback. |
| DeepSeek | API Key | Uses an OpenAI-compatible chat completions endpoint. |

Useful links:

- [DeepL API](https://www.deepl.com/pro-api)
- [Google Cloud Translation](https://cloud.google.com/translate)
- [Baidu Translate Open Platform](https://fanyi-api.baidu.com/)
- [DeepSeek API](https://platform.deepseek.com/api_keys)

### Privacy

TrayTranslator does not upload your local configuration to this repository.

- Runtime settings are stored at `%AppData%\TrayTranslator\settings.json`.
- API keys are encrypted with Windows DPAPI for the current Windows user.
- Build outputs, local settings, `.env` files, and binaries are ignored by `.gitignore`.
- Translation text is sent only to the providers you enable.

### Limitations

- No OCR. Scanned PDFs and images require separate OCR software.
- Some protected documents or elevated apps may block simulated copy.
- Translation quality and speed depend on each provider and network conditions.
- The MVP is portable and does not include an installer or auto-start setup yet.

### Roadmap

- Configurable hotkeys in the settings UI.
- Startup option.
- Optional release packaging.
- More translation provider presets.
- Better popup resizing and history.

### License

This project is licensed under the [MIT License](LICENSE).

<p align="right"><a href="#traytranslator">Back to top</a></p>

---

<a id="简体中文"></a>

## 简体中文

TrayTranslator 是一个轻量 Windows 托盘划词翻译工具。你可以在任意桌面应用里选中文字，按下 `Ctrl+Shift+T`，程序会在鼠标附近弹出一个小浮窗，并同时展示多路翻译结果。

它的设计目标是轻量和直接：不使用 Electron，不常驻浏览器内核，不做 OCR，也不做持续屏幕监听。程序只会在你按快捷键时读取当前选区；当浮窗已经打开后，你再次选中新文本时，它会自动重新取词并翻译。

### 功能特点

- 全局快捷键翻译：任意位置选中文字后按 `Ctrl+Shift+T`。
- 浮动翻译窗口：可拖动、可缩放、置顶、适合快速阅读。
- 阅读模式：任意翻译结果都可以点 `展开`，进入更大的可缩放阅读窗，并支持字号放大/缩小。
- 跨应用连续翻译：浮窗打开后，在其他应用重新选中文字即可自动翻译。
- 多引擎结果：支持 DeepL、Google Cloud Translation、百度翻译和 DeepSeek AI。
- 语言选择：默认源语言为混合/自动，目标语言为简体中文，也支持常见目标语言。
- 快速取词：使用剪贴板序列号检测、Word 选区快速路径和多种复制兜底方案。
- PDF 文本清洗：翻译前会拼接 PDF/论文复制出的异常换行。
- 本地隐私保护：API key 使用 Windows DPAPI 按当前用户加密保存。

### 环境要求

- 推荐 Windows 10 或更高版本。
- 推荐安装 .NET Framework 4.8 运行时。
- 从源码构建需要 Visual Studio 2019 或 MSBuild。

项目目标框架是 `.NET Framework 4.5`，以便兼容更多 Windows 机器；安装了更高版本 .NET Framework 的系统可以直接运行。

### 安装

从 [Releases](https://github.com/xianyuzinc/tray-translator/releases) 下载 `TrayTranslatorSetup.exe`，双击即可安装。

安装包是轻量的用户级安装：

- 安装到 `%LocalAppData%\Programs\TrayTranslator`。
- 创建开始菜单快捷方式。
- 为当前 Windows 用户注册卸载项。
- 一般不需要管理员权限。

如果想自己从源码生成安装包：

```powershell
.\installer\build-setup.ps1
```

生成位置：

```text
dist\TrayTranslatorSetup.exe
```

### 构建

可以用 Visual Studio 2019 打开 `TrayTranslator.sln` 构建，也可以使用 MSBuild：

```powershell
& "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe" .\TrayTranslator.sln /p:Configuration=Release
```

生成文件位置：

```text
bin\Release\TrayTranslator.exe
```

### 使用方法

1. 启动 `TrayTranslator.exe`。
2. 在系统托盘右键 TrayTranslator 图标，打开 `设置`。
3. 填写你想启用的翻译服务 API key。
4. 在 Word、浏览器、Codex、PDF 阅读器、WPS、Markdown 编辑器等应用中选中文字。
5. 按 `Ctrl+Shift+T` 呼出翻译浮窗。
6. 保持浮窗打开，切换到其他应用重新选中文字，即可自动重新翻译。
7. 遇到长文本时，可以拖动浮窗右边缘/下边缘放大，也可以点击结果卡片里的 `展开` 打开阅读窗。

如果目标程序以管理员权限运行，TrayTranslator 也可能需要用管理员权限启动，否则 Windows 可能会阻止模拟复制输入。

### 翻译服务

你可以在设置中单独启用或关闭每个翻译服务。

| 服务 | 凭据 | 说明 |
| --- | --- | --- |
| DeepL | API Key | 根据 key 类型自动使用 Free 或 Pro API endpoint。 |
| Google Cloud Translation | API Key | 使用 Cloud Translation Basic v2。 |
| 百度翻译 | API Key，可选 APPID | 优先使用百度新版 API Key 接口，也保留旧版 APPID/sign 兜底。 |
| DeepSeek | API Key | 使用 OpenAI 兼容的 chat completions 接口。 |

常用链接：

- [DeepL API](https://www.deepl.com/pro-api)
- [Google Cloud Translation](https://cloud.google.com/translate)
- [百度翻译开放平台](https://fanyi-api.baidu.com/)
- [DeepSeek API](https://platform.deepseek.com/api_keys)

### 隐私说明

TrayTranslator 不会把你的本地配置上传到仓库。

- 运行时配置保存在 `%AppData%\TrayTranslator\settings.json`。
- API key 使用 Windows DPAPI 按当前 Windows 用户加密保存。
- `.gitignore` 已排除构建产物、本地配置、`.env` 文件和二进制文件。
- 被翻译文本只会发送给你在设置里启用的翻译服务。

### 当前限制

- 不做 OCR。扫描版 PDF 和图片需要额外 OCR 工具。
- 受保护文档或管理员权限应用可能阻止模拟复制。
- 翻译质量和速度取决于翻译服务及网络状况。
- MVP 默认便携运行，暂未提供安装包和开机自启。

### 路线图

- 在设置界面配置快捷键。
- 开机自启选项。
- 可选发布打包。
- 更多翻译服务预设。
- 更完善的浮窗尺寸调整和翻译历史。

### 开源协议

本项目使用 [MIT License](LICENSE) 开源。

<p align="right"><a href="#traytranslator">返回顶部</a></p>
