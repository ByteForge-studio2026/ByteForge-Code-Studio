# ByteForge Code Studio

ByteForge 工作室的**轻量级文本 / 源码编辑器**。开源、公益、免费，MIT 许可证。

| 项目 | 内容 |
| --- | --- |
| 软件名称 | ByteForge Code Studio |
| 定位 | 轻量级文本 / 源码编辑器（**打开文件**的工具，不提供"新建文件"） |
| 版本 | v0.1.0-alpha.1 |
| 支持功能 | 打开文件、编辑文本、保存文件、语法高亮（Python / C / C++ / Java / C# / Kotlin / PHP / XML / XAML / Markdown）、Markdown 预览、查找替换、最近打开、内置文件管理器 |
| 技术栈 | C# + WPF（编辑器，产品主体）；WPF（安装程序）；WinUI 3（设置程序，源码保留、**不随安装包发布**） |
| 运行环境 | Windows 10 1809+，需要 **.NET 10 桌面运行时**（编辑器为框架依赖版） |
| UI 风格 | 参考 VS Code 的布局与克制配色（不照抄），控件**全圆角** |
| 开发团队 | ByteForge 工作室 |
| 官方渠道 | 抖音号 ByteForge2026 |
| 开源协议 | MIT · Copyright (c) 2026 ByteForge |

> 产品入口就是编辑器本体 **`ByteForgeCodeStudio.exe`**（设置 / 关于面板已内置）。
> `ByteForge.Settings.WinUI`（`ByteForgeSettings.exe`）的源码保留在仓库里，但**不再打进安装包**——
> 它会拖进整套 Windows App SDK（约 86 MB）。安装包里只有编辑器（10 个文件）。

---

## 1. 目录结构

```
ByteForgeCodeStudio/
├─ LICENSE                        MIT 许可证
├─ README.md
├─ .gitignore
├─ ByteForgeCodeStudio.slnx       解决方案
├─ Directory.Build.props          统一的目标框架 / 版本 / 版权
├─ assets/
│  └─ icons/                      图标：app-icon.png(.ico) + 177 个线性 SVG 图标库
├─ docs/
│  └─ ByteForge产品用户协议-2026年修订版.txt    用户协议（Core 内嵌打包）
├─ tools/
│  ├─ publish.py                  一键构建 + 发布（编辑器 → payload.zip → 单文件 Setup）
│  ├─ build.ps1                   PowerShell 版入口（-Release 转调 publish.py）
│  ├─ _build_winui.py             单独构建 WinUI 设置程序（需 VS MSBuild）
│  └─ make_icon.py                app-icon.png → app-icon.ico（自动裁掉主体外白边）
└─ src/
   ├─ ByteForge.Core/             公共核心：动态路径、设置存储、关于信息、用户协议
   ├─ ByteForge.FileIO/           【独立模块】打开文件：编码识别、二进制判定、读写、文件关联
   ├─ ByteForge.Syntax/           【独立模块】语法高亮：词法扫描、语言定义、配色主题
   ├─ ByteForge.Markdown/         【独立模块】Markdown 解析（输出与 UI 无关的文档模型）
   ├─ ByteForge.Editor.Wpf/       【产品主体】编辑器（WPF，ByteForgeCodeStudio.exe）
   ├─ ByteForge.Setup/            安装程序（WPF，自包含单文件 ByteForgeSetup.exe）
   └─ ByteForge.Settings.WinUI/   （可选）设置 / 关于（WinUI 3，不随安装包发布）
```

模块依赖（`→` 表示"引用"）：

```
ByteForge.Editor.Wpf     → FileIO, Syntax, Markdown, Core
ByteForge.Setup          → FileIO, Core
ByteForge.Settings.WinUI → FileIO, Core
ByteForge.FileIO         → Core
ByteForge.Syntax         → Core
ByteForge.Markdown       → （无依赖）
```

> 打开文件模块（FileIO）与语法高亮模块（Syntax）**互不依赖**，可以单独引用、单独测试、单独替换。

---

## 2. 动态路径约定

程序里**没有任何写死的盘符或固定目录**，一律通过 `ByteForge.Core.AppPaths` 推导：

| 路径 | 取值 |
| --- | --- |
| 安装目录 | `AppContext.BaseDirectory`（即 exe 所在目录） |
| 配置文件 | `<安装目录>\settings.json`；安装目录不可写时自动回落到 `%LOCALAPPDATA%\ByteForge\CodeStudio` |
| 图标 | `<安装目录>\assets\icons\app-icon.ico`（由 app-icon.png 裁掉主体外白边后生成） |
| 默认安装位置 | `<所选磁盘>:\Code Studio`（安装时选磁盘即可，也可自行改路径） |

---

## 3. 功能实现一览

| 需求 | 实现位置 |
| --- | --- |
| 打开文件（含无后缀文件按文本处理） | `FileIO/TextFileOpener.cs`；**不提供"新建文件"**，没带参数就停在欢迎页 |
| 二进制文件提示"该文件是不受支持的二进制文件" | `FileIO/UnsupportedBinaryFileException` + `Editor.Wpf/MainWindow.OpenFile` |
| 编辑器 UI：VS Code 风格灵感 + 全圆角 + 欢迎页 | `Editor.Wpf/MainWindow.xaml`、`App.xaml` |
| 编辑文本（行号 / 当前行 / 缩进 / 括号补全） | `Editor.Wpf/Controls/CodeEditor.xaml(.cs)` |
| 保存 / 另存为（保留原编码与换行符） | `FileIO/TextFileSaver.cs` |
| 语法高亮（9 种语言） | `Syntax/Tokenizer.cs` + `Syntax/LanguageRegistry.cs` |
| Markdown 预览（底部 / 右侧 / 关闭） | `Markdown/MarkdownParser.cs` + `Editor.Wpf/Preview/MarkdownFlowRenderer.cs` |
| 查找 / 替换 + 结果列表跳转 | `Editor.Wpf/Controls/CodeEditor.xaml.cs`（按"行号+列号"定位，纯文本 / 高亮两种模式通用） |
| 内置文件管理器（Ctrl+O） | `Editor.Wpf/Controls/FileBrowserPanel.xaml(.cs)` |
| 默认编辑器关联（仅写 HKCU，免管理员） | `FileIO/FileAssociation.cs` |
| 设置 / 关于（内置面板） | `Editor.Wpf/Controls/SettingsPanel.xaml(.cs)` |
| 安装程序（解压内置压缩包 + 文件关联 + 快捷方式） | `ByteForge.Setup` |
| 轻量（内存占用 ≤ 35 MB） | 逐行扫描 + 局部重排 + 大文件自动降级为纯文本 + 行号栏用 DrawingContext 直接画 |

---

## 4. 构建

### 一键脚本（推荐）

```powershell
.\tools\build.ps1                # 只构建（Debug）：Core / FileIO / Syntax / Markdown / Editor / Setup
.\tools\build.ps1 -Release       # 构建 + 发布单文件安装程序到 publish\
```

发布脚本（跨平台，Windows 上等价）：

```bash
python tools/publish.py          # 同上：构建 + 发布
python tools/publish.py --debug  # 只构建（Debug）
```

### 手动命令

```powershell
# 编辑器、安装程序、公共库（可直接用 dotnet SDK 构建）
dotnet build src\ByteForge.Editor.Wpf\ByteForge.Editor.Wpf.csproj -c Release
dotnet build src\ByteForge.Setup\ByteForge.Setup.csproj -c Release

# 设置程序（WinUI 3）需要 Visual Studio 的 MSIX / Pri 工具集，建议用 tools\_build_winui.py
python tools\_build_winui.py
```

目标框架在 `Directory.Build.props` 里统一改：

```xml
<BfDesktopTargetFramework>net10.0-windows</BfDesktopTargetFramework>          <!-- WPF / 类库 -->
<BfWinUiTargetFramework>net10.0-windows10.0.19041.0</BfWinUiTargetFramework>  <!-- WinUI 3 -->
```

---

## 5. 安装包是怎么做出来的

安装包**就是一个自包含的单文件 `ByteForgeSetup.exe`**，原理是"**内置压缩包 + 安装即解压**"：

```
1) 只发布编辑器（框架依赖版）到暂存目录
      dotnet publish src\ByteForge.Editor.Wpf -c Release -o obj\payload
2) 把暂存目录打成压缩包（排除 .pdb 与开发态 settings.json）
      src\ByteForge.Setup\payload.zip
3) payload.zip 作为嵌入资源编进 Setup，Setup 再按"自包含单文件"发布
      dotnet publish src\ByteForge.Setup -c Release -r win-x64 --self-contained true \
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
        -p:EnableCompressionInSingleFile=true -o publish
4) 产物：publish\ByteForgeSetup.exe（单文件，双击即装）
```

安装 = 把内置的 `payload.zip` 解压到安装目录（带 zip-slip 防护）。安装位置在界面上选磁盘，
自动装到 `<盘>:\Code Studio`，路径与磁盘单选**实时双向同步**。

命令行静默安装：

```powershell
ByteForgeSetup.exe --install "D:\Code Studio" [--no-shortcut] [--no-assoc]
```

---

## 6. 图标

- 源文件：`assets/icons/app-icon.png`
- Windows 图标：`assets/icons/app-icon.ico`（16/20/24/32/40/48/64/96/128/256 多尺寸）
- 重新生成：

```powershell
python tools\make_icon.py
```

---

## 7. 参与贡献

欢迎提交 Issue / PR。请保持代码风格与现有模块划分，**FileIO 与 Syntax 两个模块保持互不依赖**。
提交前请确认 `dotnet build` 通过。

## 8. 免责声明

本软件为开源、公益性质的免费软件，按"现状"提供，不附带任何明示或暗示的担保；
开发者不对使用中可能出现的 Bug、数据丢失或任何直接或间接损失承担责任，亦不提供商业化的技术支持。
完整条款见《ByteForge 产品用户协议 - 2026 年修订版》（`docs/` 目录，编辑器"关于"面板内可全文查看）。
