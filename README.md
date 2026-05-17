# TrayTranslator

TrayTranslator 是一个轻量 Windows 托盘划词翻译工具。选中任意 App 里的文字后按 `Ctrl+Shift+T`，程序会临时复制选区、恢复原剪贴板，并在鼠标附近显示 DeepL、Google、百度和 DeepSeek 多路翻译结果。

## 构建

本项目使用 .NET Framework 4.5 + WinForms，无第三方依赖。它可以在安装了 .NET Framework 4.8 的 Windows 上直接运行。可用 Visual Studio 2019 打开 `TrayTranslator.csproj` 构建，也可用 MSBuild：

```powershell
& "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe" .\TrayTranslator.csproj /p:Configuration=Release
```

生成文件在 `bin\Release\TrayTranslator.exe`。

## 使用

1. 启动 `TrayTranslator.exe`。
2. 在系统托盘找到 TrayTranslator 图标，右键打开 `设置`。
3. 填入需要启用的翻译服务密钥。
4. 在 Word、浏览器、Codex、PDF 阅读器等应用里选中文字。
5. 按 `Ctrl+Shift+T` 查看翻译浮窗。

浮窗出现后可以拖动位置；在浮窗保持打开时，直接去其他窗口重新选中一段文字，程序会自动重新取词并翻译，不需要再次按快捷键。

弹窗顶部可以选择源语言和目标语言，默认是 `混合/自动 -> 简体中文`。改动后会保存设置，并使用当前选中的文本重新翻译。

设置页里的 `界面字号` 可以调整浮窗和结果正文大小。界面中文默认使用 `Microsoft YaHei UI`，翻译正文中的英文和数字会使用 `Times New Roman` 混排。

如果目标程序以管理员权限运行，TrayTranslator 也可能需要以管理员权限启动，否则 Windows 可能阻止模拟复制按键。

## DeepL 配置

1. 打开 https://www.deepl.com/pro-api
2. 选择 DeepL API Free 或 Pro，创建 API Key。
3. 在设置页填写 `DeepL API Key`。

DeepL 当前 Developer 免费方案页面显示提供 100 万字符的一次性额度；付费 Growth 方案包含年度字符量并按超额计费。Free Key 通常以 `:fx` 结尾，程序会自动使用 `https://api-free.deepl.com/v2/translate`；Pro Key 会使用 `https://api.deepl.com/v2/translate`。

## 百度翻译配置

1. 打开 https://fanyi-api.baidu.com/ 并登录。
2. 开通大模型文本翻译 API 或通用文本翻译 API。
3. 进入 API Key 管理页创建 API Key：https://fanyi-api.baidu.com/manage/apiKey
4. 在设置页填写百度 `API Key`。如果页面也给了 `APPID`，可以一并填写。

程序优先使用百度新版 API Key 鉴权接口：`https://fanyi-api.baidu.com/ait/api/aiTextTranslate`。以百度开放平台当前页面的免费额度、QPS 和计费规则为准。

## Google 翻译配置

1. 打开 https://console.cloud.google.com/
2. 新建或选择项目，启用 `Cloud Translation API`。
3. 创建 API key，建议把 key 限制到 `Cloud Translation API`。
4. 在设置页填写 `Google API Key`。

程序调用 Cloud Translation Basic v2：`https://translation.googleapis.com/language/translate/v2`。Google Cloud Translation 通常有每月 50 万字符免费额度，超出后按字符计费，以 Google Cloud 当前价格页为准。

如果觉得 Google Cloud 配置麻烦，可以先只启用 DeepL、百度和 DeepSeek。

## DeepSeek 配置

1. 打开 https://platform.deepseek.com/api_keys 创建 API key。
2. 在设置页填写 `DeepSeek API Key`。
3. 默认 `Base URL` 为 `https://api.deepseek.com`。
4. 默认模型为 `deepseek-v4-flash`。

DeepSeek 按 token 计费，以官方价格页为准：https://api-docs.deepseek.com/quick_start/pricing/

## 配置与隐私

设置文件保存在 `%AppData%\TrayTranslator\settings.json`。API 密钥使用 Windows DPAPI 按当前 Windows 用户加密保存，不以明文写入配置文件。

## MVP 限制

- 第一版仍以快捷键呼出为入口；浮窗打开后支持重新选区自动翻译。
- 第一版不做 OCR。
- PDF/论文复制出的异常换行会在翻译前做轻量拼接，但扫描版 PDF 仍需要 OCR。
- 单次翻译默认最多 2000 字符，可在设置中调整到 100-6000。
- 取词会优先尝试 Windows UI Automation 读取选区，再使用多轮 `Ctrl+C` / `WM_COPY` / `SendKeys` 兜底。扫描版 PDF、禁复制文档或权限隔离窗口仍可能无法取词。
