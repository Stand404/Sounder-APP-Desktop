中文 | [English](EN_README.md)

<div align="center">
  <img src="src/Sounder-APP.Desktop/Assets/ico.png" width="64" alt="发声APP图标"/>
  <h1>发声APP · 造化版（桌面端）</h1>
  <p>一款基于 Avalonia UI 构建的跨平台桌面应用</p>
</div>

## ✨ 功能介绍

### <img src="src/Sounder-APP.Desktop/Assets/sounder.png" width="28" style="vertical-align: middle;" alt=""/> 发声APP —— 初版
一款点击后只会播放声音的原生 Android 解压小软件。第一次启动时选择音频文件，之后每次点击图标直接播放声音，不弹出界面。  
初版仓库：[https://github.com/Stand404/Sounder](https://github.com/Stand404/Sounder)

### <img src="src/Sounder-APP.Desktop/Assets/sounder.png" width="28" style="vertical-align: middle;" alt=""/> 发声APP —— 独立版
在初版基础上打造的一系列独立 Android APP，每个 APP 拥有独立的图标和默认音频，点击即播，轻松解压。  
可以在官网获取：https://stand.homes/apps

### <img src="src/Sounder-APP.Desktop/Assets/ico.png" width="28" style="vertical-align: middle;" alt=""/> 发声APP · 造化版（本程序）
在独立版概念基础上全面扩展的完整功能版，支持浏览、搜索、下载、创建、编辑、导入导出各种资源包，以及播放各类音频资源。  
当前项目为 **造化版的桌面端**，与 [造化版 Android 端](https://github.com/stand404/Sounder-APP-Android)功能对应。

### 核心功能
- **在线商店** — 浏览、搜索和下载音频资源包
- **音频资源管理** — 浏览、播放和管理本地音频文件
- **编辑与创建** — 创建和编辑自定义资源包
- **多模式播放** — 支持叠加播放、替换播放、循环播放
- **任务管理** — 桌面播放任务列表展示和控制
- **快捷方式** — 一键创建桌面快捷方式，双击即播
- **投稿板块** — 提交投稿和查看投稿列表
- **多语言支持** — 支持简体中文、繁体中文、English、日本語、Русский，即时切换

---

## 🖥️ 系统要求

| 平台 | 要求 |
|------|------|
| **Windows** | Windows 10+ (x64) |
| **Linux** (x64) | 桌面环境（X11/Wayland）；需安装 `alsa-utils` (提供 `aplay`) |
| **macOS** | macOS 11+ (x64 / arm64) |
| **运行时** | .NET 10 Runtime（框架依赖模式）或无需（自包含模式） |

---

## 📦 下载与安装

### 获取安装包

前往以下地址下载各平台的最新安装包： **https://stand.homes/apps/e95a1dab-2f24-4557-ba9d-98e82861705d**

提供以下格式：
- **Windows** — 安装包（.exe）
- **Linux** — `.deb` 包（Debian/Ubuntu 系）或便携版（tar.gz）
- **macOS** — 磁盘映像（.dmg）

### Windows 安装说明

**安装包**：双击运行，按向导安装即可，自动注册 `sounder://` 协议。

### Linux 安装说明

> 仅已在 **Debian x64** 上测试通过，其他发行版或架构请自行验证。

#### 安装 alsa-utils（音频输出依赖）

```bash
# Debian/Ubuntu
sudo apt install alsa-utils

# Fedora
sudo dnf install alsa-utils

# Arch
sudo pacman -S alsa-utils
```

#### .deb 包安装

```bash
sudo dpkg -i sounder-app_*.deb
sudo apt install -f    # 补全依赖
```

安装后可在应用菜单找到「发声APP」，或终端运行 `Sounder-APP`。

#### 卸载

```bash
sudo apt remove sounder-app
```

或

```bash
sudo dpkg -r sounder-app
```

卸载时自动清理 `sounder://` 协议注册和菜单项。

#### 便携版

> 便携版采用目录发布，原生库与可执行文件同目录，兼容性更好。

```bash
# 解压
tar -xzf sounder-app-*.tar.gz
cd sounder-app-*
# 运行（推荐）
./run.sh
# 或直接运行
./Sounder-APP
```

### macOS 安装说明

**磁盘映像（推荐）**：双击 `.dmg` 文件打开，将 `Sounder APP.app` 拖入 `Applications` 文件夹即可。
首次启动时如果系统提示"无法验证开发者"，请在「系统设置 → 隐私与安全性」中点击"仍要打开"。

> 在 macOS 上，应用内的提示弹窗使用直角风格，与其他平台有所区别。

---

## 🔧 自编译

### 前置要求
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### 编译运行（开发）

```bash
dotnet restore src/Sounder-APP.Desktop/Sounder-APP.Desktop.csproj
dotnet build   src/Sounder-APP.Desktop/Sounder-APP.Desktop.csproj
dotnet run     src/Sounder-APP.Desktop/Sounder-APP.Desktop.csproj
```

### 发布构建（Release 单文件）

按平台选择对应的构建脚本：

**Windows**
```bat
Windows_Build.bat                  # 默认 win-x64
Windows_Build.bat win-arm64        # 指定平台
```
产物在 `publish/` 目录，然后可用 Inno Setup 编译安装包：
```
ISCC.exe Windows_Installer.iss
```

**Linux**
```bash
chmod +x Linux_Build.sh
./Linux_Build.sh                            # 默认 linux-x64
./Linux_Build.sh --deb                      # 发布 + .deb 包
./Linux_Build.sh --portable                 # 发布 + tar.gz 便携版
./Linux_Build.sh --deb --portable           # 发布 + 两种包
./Linux_Build.sh linux-arm64 --deb          # 指定 arm64
./Linux_Build.sh --all --deb --portable     # 全架构 + 两种包
```

**macOS**
```bash
chmod +x Mac_Build.sh
./Mac_Build.sh                     # 默认 osx-x64
./Mac_Build.sh osx-arm64           # Apple Silicon
```

---

## 📁 项目结构

```
Sounder-APP-Desktop/
├── .gitignore                     # Git 忽略规则
├── src/
│   ├── Sounder-APP.Core/          # 核心类库（模型、服务、转换器等）
│   │   ├── Converters/            #   值转换器（13 个）
│   │   ├── Models/                #   数据模型（8 个）
│   │   ├── Properties/            #   多语言资源文件（5 种语言）
│   │   ├── Services/              #   核心服务（7 个）
│   │   └── Sounder-APP.Core.csproj
│   └── Sounder-APP.Desktop/       # 桌面应用主项目
│       ├── Assets/                #   图标、图片资源（17 个文件）
│       ├── Properties/
│       ├── Services/              #   平台相关服务
│       │   ├── IAudioBackend.cs            #   音频后端接口
│       │   ├── IDesktopShortcutBackend.cs  #   桌面快捷方式接口
│       │   ├── IIconGeneratorBackend.cs    #   图标生成接口
│       │   ├── IInstanceLockBackend.cs     #   单实例锁接口
│       │   ├── IUrlProtocolBackend.cs      #   自定义协议接口
│       │   ├── BackendFactory.cs           #   根据 OS 选择后端
│       │   ├── AudioPlaybackService.cs     #   音频播放服务
│       │   ├── BackgroundPlaybackManager.cs#   后台播放管理
│       │   ├── DesktopShortcutService.cs   #   快捷方式服务
│       │   ├── IconGeneratorService.cs     #   图标生成服务
│       │   ├── IconGeneratorUtils.cs       #   图标生成工具
│       │   ├── SingleInstanceService.cs    #   单实例服务
│       │   ├── UrlProtocolService.cs       #   协议注册服务
│       │   ├── Windows/                    #   Windows 后端实现（5 个）
│       │   ├── Linux/                      #   Linux 后端实现（6 个）
│       │   └── Mac/                        #   macOS 后端实现（7 个）
│       ├── Views/                 #   UI 视图
│       │   ├── Common/                    #   通用组件（10+ 个）
│       │   │   └── Templates/             #     样式模板（4 个）
│       │   ├── InstallButton/             #   安装按钮组件
│       │   ├── PersonalResource/          #   个人资源视图（6 个）
│       │   ├── Shop/                      #   商店视图（6 个）
│       │   ├── Submissions/               #   投稿视图（3 个）
│       │   ├── TaskManager/               #   任务管理视图
│       │   ├── MainWindow.axaml(.cs)      #   主窗口
│       │   ├── ResourceDetailLayout.axaml(.cs)  # 资源详情布局
│       │   ├── SettingsView.axaml(.cs)    #   设置视图
│       │   └── TranslateExtension.cs      #   翻译标记扩展
│       ├── ViewModels/            #   视图模型（10 个）
│       ├── App.axaml(.cs)         #   应用入口
│       ├── Program.cs             #   程序入口
│       ├── ViewLocator.cs         #   视图定位器
│       ├── app.manifest           #   Windows 清单
│       └── Sounder-APP.Desktop.csproj
├── build/
│   ├── linux/                     # Linux 打包资源
│   │   ├── debian/                #   DEB 控制文件（4 个）
│   │   └── sounder-app.desktop    #   桌面菜单入口
│   └── macos/                     # macOS 打包资源
│       ├── Info.plist             #   应用 info 配置
│       └── en.lproj/zh-Hans.lproj/#   多语言 plist 字符串
├── Windows_Build.bat              # Windows 发布脚本
├── Windows_Installer.iss          # Windows 安装包脚本 (Inno Setup)
├── Linux_Build.sh                 # Linux 发布脚本 (支持 --deb 和 --portable)
├── Mac_Build.sh                   # macOS 发布脚本
├── Sounder-APP.slnx               # 解决方案文件
├── LICENSE
├── README.md
└── EN_README.md
```

---

## 📄 协议

MIT © Stand404
