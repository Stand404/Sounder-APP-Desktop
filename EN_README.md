## English | [中文](README.md)

<div align="center">
  <img src="src/Sounder-APP.Desktop/Assets/ico.png" width="64" alt="Sounder App Icon"/>
  <h1>Sounder APP · Evolution Edition (Desktop)</h1>
  <p>A cross-platform desktop application built with Avalonia UI</p>
</div>

## ✨ Features

### <img src="src/Sounder-APP.Desktop/Assets/sounder.png" width="28" style="vertical-align: middle;" alt=""/> Sounder APP — Original Edition
A native Android stress-relief app that simply plays a sound on tap. On first launch, select an audio file; each subsequent tap on the icon plays the sound directly without opening the UI.  
Original repository: [https://github.com/Stand404/Sounder](https://github.com/Stand404/Sounder)

### <img src="src/Sounder-APP.Desktop/Assets/sounder.png" width="28" style="vertical-align: middle;" alt=""/> Sounder APP — Standalone Edition
A collection of standalone Android apps built on the original concept. Each app has its own icon and default audio — tap and play, easy stress relief.  
Available at: https://stand.homes/apps

### <img src="src/Sounder-APP.Desktop/Assets/ico.png" width="28" style="vertical-align: middle;" alt=""/> Sounder APP · Evolution Edition (This App)
A fully-featured extension of the standalone concept, supporting browsing, searching, downloading, creating, editing, importing, and exporting various resource packs, as well as playing various audio resources.  
This project is the **desktop version of the Evolution Edition**, corresponding to the [Evolution Edition Android version](https://github.com/stand404/Sounder-APP-Android).

### Core Features
- **Online Store** — Browse, search, and download audio resource packs
- **Audio Resource Management** — Browse, play, and manage local audio files
- **Edit & Create** — Create and edit custom resource packs
- **Multi-Mode Playback** — Overlay, replace, and loop playback
- **Task Management** — Desktop playback task list display and control
- **Shortcuts** — One-click desktop shortcut creation, double-click to play
- **Submission Section** — Submit and browse submissions
- **Multi-Language Support** — Simplified Chinese, Traditional Chinese, English, 日本語, Русский, switch instantly

---

## 🖥️ System Requirements

| Platform | Requirements |
|------|------|
| **Windows** | Windows 10+ (x64) |
| **Linux** (x64) | Desktop environment (X11/Wayland); requires `alsa-utils` (provides `aplay`) |
| **macOS** | macOS 11+ (x64 / arm64) |
| **Runtime** | .NET 10 Runtime (framework-dependent) or none (self-contained) |

---

## 📦 Download & Installation

### Getting the Installer

Download the latest installer for your platform from: **https://stand.homes/apps/e95a1dab-2f24-4557-ba9d-98e82861705d**

Available formats:
- **Windows** — Installer (.exe)
- **Linux** — `.deb` package (Debian/Ubuntu-based) or portable version (tar.gz)
- **macOS** — Disk image (.dmg)

### Windows Installation

**Installer**: Double-click and follow the setup wizard. The `sounder://` protocol will be registered automatically.

### Linux Installation

> Only tested on **Debian x64**. Please verify on other distributions or architectures yourself.

#### Install alsa-utils (Audio Output Dependency)

```bash
# Debian/Ubuntu
sudo apt install alsa-utils

# Fedora
sudo dnf install alsa-utils

# Arch
sudo pacman -S alsa-utils
```

#### .deb Package Installation

```bash
sudo dpkg -i sounder-app_*.deb
sudo apt install -f    # Install dependencies
```

After installation, find "Sounder APP" in your application menu, or run `Sounder-APP` in the terminal.

#### Uninstall

```bash
sudo apt remove sounder-app
```

Or

```bash
sudo dpkg -r sounder-app
```

The `sounder://` protocol registration and menu entries will be automatically cleaned up on uninstall.

#### Portable Version

> The portable version uses a directory-based layout. Native libraries are in the same directory as the executable for better compatibility.

```bash
# Extract
tar -xzf sounder-app-*.tar.gz
cd sounder-app-*
# Run (recommended)
./run.sh
# Or run directly
./Sounder-APP
```

### macOS Installation

**Disk image (recommended)**: Double-click the `.dmg` file, then drag `Sounder APP.app` into the `Applications` folder.
On first launch, if the system shows "cannot verify developer", go to **System Settings → Privacy & Security** and click "Open Anyway".

> On macOS, in-app prompt dialogs use a sharp-corner style, differing from other platforms.

---

## 🔧 Building from Source

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### Build & Run (Development)

```bash
dotnet restore src/Sounder-APP.Desktop/Sounder-APP.Desktop.csproj
dotnet build   src/Sounder-APP.Desktop/Sounder-APP.Desktop.csproj
dotnet run     src/Sounder-APP.Desktop/Sounder-APP.Desktop.csproj
```

### Release Build (Single-file)

Choose the appropriate build script for your platform:

**Windows**
```bat
Windows_Build.bat                  # Default: win-x64
Windows_Build.bat win-arm64        # Specify platform
```
Output goes to `publish/` directory. Then build the installer with Inno Setup:
```
ISCC.exe Windows_Installer.iss
```

**Linux**
```bash
chmod +x Linux_Build.sh
./Linux_Build.sh                            # Default: linux-x64
./Linux_Build.sh --deb                      # Build + .deb package
./Linux_Build.sh --portable                 # Build + tar.gz portable
./Linux_Build.sh --deb --portable           # Build + both packages
./Linux_Build.sh linux-arm64 --deb          # Specify arm64
./Linux_Build.sh --all --deb --portable     # All architectures + both packages
```

**macOS**
```bash
chmod +x Mac_Build.sh
./Mac_Build.sh                     # Default: osx-x64
./Mac_Build.sh osx-arm64           # Apple Silicon
```

---

## 📁 Project Structure

```
Sounder-APP-Desktop/
├── .gitignore                     # Git ignore rules
├── src/
│   ├── Sounder-APP.Core/          # Core class library (models, services, converters)
│   │   ├── Converters/            #   Value converters (13)
│   │   ├── Models/                #   Data models (8)
│   │   ├── Properties/            #   Localization resource files (5 languages)
│   │   ├── Services/              #   Core services (7)
│   │   └── Sounder-APP.Core.csproj
│   └── Sounder-APP.Desktop/       # Desktop application main project
│       ├── Assets/                #   Icons, images (17 files)
│       ├── Properties/
│       ├── Services/              #   Platform-specific services
│       │   ├── IAudioBackend.cs            #   Audio backend interface
│       │   ├── IDesktopShortcutBackend.cs  #   Desktop shortcut interface
│       │   ├── IIconGeneratorBackend.cs    #   Icon generator interface
│       │   ├── IInstanceLockBackend.cs     #   Single instance lock interface
│       │   ├── IUrlProtocolBackend.cs      #   Custom protocol interface
│       │   ├── BackendFactory.cs           #   OS-based backend selection
│       │   ├── AudioPlaybackService.cs     #   Audio playback service
│       │   ├── BackgroundPlaybackManager.cs#   Background playback manager
│       │   ├── DesktopShortcutService.cs   #   Shortcut service
│       │   ├── IconGeneratorService.cs     #   Icon generation service
│       │   ├── IconGeneratorUtils.cs       #   Icon generation utilities
│       │   ├── SingleInstanceService.cs    #   Single instance service
│       │   ├── UrlProtocolService.cs       #   Protocol registration service
│       │   ├── Windows/                    #   Windows backend (5)
│       │   ├── Linux/                      #   Linux backend (6)
│       │   └── Mac/                        #   macOS backend (7)
│       ├── Views/                 #   UI views
│       │   ├── Common/                    #   Shared components (10+)
│       │   │   └── Templates/             #     Style templates (4)
│       │   ├── InstallButton/             #   Install button component
│       │   ├── PersonalResource/          #   Personal resource views (6)
│       │   ├── Shop/                      #   Shop views (6)
│       │   ├── Submissions/               #   Submissions views (3)
│       │   ├── TaskManager/               #   Task manager view
│       │   ├── MainWindow.axaml(.cs)      #   Main window
│       │   ├── ResourceDetailLayout.axaml(.cs)  # Resource detail layout
│       │   ├── SettingsView.axaml(.cs)    #   Settings view
│       │   └── TranslateExtension.cs      #   Translation markup extension
│       ├── ViewModels/            #   View models (10)
│       ├── App.axaml(.cs)         #   Application entry
│       ├── Program.cs             #   Program entry
│       ├── ViewLocator.cs         #   View locator
│       ├── app.manifest           #   Windows manifest
│       └── Sounder-APP.Desktop.csproj
├── build/
│   ├── linux/                     # Linux packaging resources
│   │   ├── debian/                #   DEB control files (4)
│   │   └── sounder-app.desktop    #   Desktop menu entry
│   └── macos/                     # macOS packaging resources
│       ├── Info.plist             #   App info configuration
│       └── en.lproj/zh-Hans.lproj/#   Localized plist strings
├── Windows_Build.bat              # Windows build script
├── Windows_Installer.iss          # Windows installer script (Inno Setup)
├── Linux_Build.sh                 # Linux build script (supports --deb and --portable)
├── Mac_Build.sh                   # macOS build script
├── Sounder-APP.slnx               # Solution file
├── LICENSE
├── README.md
└── EN_README.md
```

---

## 📄 License

MIT © Stand404
