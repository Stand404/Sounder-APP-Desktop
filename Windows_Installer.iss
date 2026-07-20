;============================================================
;  Sounder-APP（发声APP）安装向导 — Inno Setup 脚本
;  使用前：
;    1. 运行 Windows_Build.bat 生成 publish/ 目录
;    2. 用 Inno Setup 编译器打开本文件 → 编译
;  下载 Inno Setup: https://jrsoftware.org/isdl.php
;============================================================

#define MyAppName "发声APP"
#define MyAppShortName "Sounder-APP"
#define MyAppPublisher "Stand404"
#define MyAppURL "https://stand.homes"
#define MyAppExeName "Sounder-APP.exe"

; 从 csproj 读取版本（需安装 Inno Setup 预处理器的 XML 扩展）
; 或者直接在此指定版本号
#define MyAppVersion "1.0.0.0"

; 发布输出目录（由 Windows_Build.bat 生成）
#define SourceDir "publish"

[Setup]
; 安装包元信息
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}

; 输出文件名
OutputDir=.
OutputBaseFilename=Sounder-APP-Setup-{#MyAppVersion}

; 安装包图标与压缩
SetupIconFile=src\Sounder-APP.Desktop\Assets\Sounder-APP.ico
Compression=lzma2/ultra64
SolidCompression=yes
InternalCompressLevel=max

; 应用列表显示名称（不含版本号）
AppVerName={#MyAppName}
; 应用列表显示图标
UninstallDisplayIcon={app}\Assets\Sounder-APP.ico

; 安装选项
DefaultDirName={autopf}\{#MyAppShortName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
PrivilegesRequired=admin
DisableProgramGroupPage=yes

; 许可协议（如无可删除此字段）
; LicenseFile=LICENSE.txt

; 安装后自动运行选项
CloseApplications=force
RestartApplications=no

; Windows 版本支持
MinVersion=10.0.0

; 安装包校验（如需数字签名，改为 yes 并配置 SignTool）
SignedUninstaller=no

[Languages]
; ChineseSimplified / Japanese / Russian 需额外下载语言文件
; 下载地址: https://jrsoftware.org/isdl.php#additional
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Languages\EnglishBritish.isl"

; ============================================================
;  安装的文件
; ============================================================
[Files]
; 主程序及所有依赖（单文件发布产物）
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb,*.config,*.xml"

; 应用图标（用于快捷方式）
Source: "src\Sounder-APP.Desktop\Assets\Sounder-APP.ico"; DestDir: "{app}\Assets"; Flags: ignoreversion

; ============================================================
;  快捷方式
; ============================================================
[Icons]
; 桌面快捷方式
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\Assets\Sounder-APP.ico"; Comment: "{#MyAppName}"
; 开始菜单快捷方式
Name: "{autoprograms}\{#MyAppName}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\Assets\Sounder-APP.ico"; Comment: "{#MyAppName}"
; 卸载快捷方式
Name: "{autoprograms}\{#MyAppName}\卸载 {#MyAppName}"; Filename: "{uninstallexe}"; IconFilename: "{app}\Assets\Sounder-APP.ico"

; ============================================================
;  运行前检查（检测旧版本、运行时等）
; ============================================================
[Run]
; 安装完成后可选启动
Filename: "{app}\{#MyAppExeName}"; Description: "立即运行 {#MyAppName}"; Flags: postinstall nowait skipifsilent unchecked

; ============================================================
;  注册 URL 协议（发声协议：sounder://）
;  使得 sounder:// 链接可以唤起应用
; ============================================================
[Registry]
; 协议注册（仅在管理员权限下写入 HKLM）
Root: HKLM; Subkey: "Software\Classes\sounder"; ValueType: string; ValueName: ""; ValueData: "URL:Sounder Protocol"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\sounder"; ValueType: string; ValueName: "URL Protocol"; ValueData: ""; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\sounder\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Flags: uninsdeletekey

; 应用路径注册（方便其他工具查找）
Root: HKLM; Subkey: "Software\{#MyAppPublisher}\{#MyAppShortName}"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; Flags: uninsdeletekey

; ============================================================
;  安装过程中的自定义页面
; ============================================================
[Code]

{--------------------------------------------------------------------------------
  自定义欢迎图片（可选）：
  在 Setup 段添加 WizardImageFile=path 即可
  默认使用 Inno Setup 内置图片
--------------------------------------------------------------------------------}

{--------------------------------------------------------------------------------
  安装前检测：若已有旧版本运行中，提示关闭
--------------------------------------------------------------------------------}
function InitializeSetup: Boolean;
begin
  Result := True;
end;

// ---- 卸载时确认是否清除 APP 数据 ----

var
  ClearData: Boolean;

function InitializeUninstall: Boolean;
begin
  ClearData := False;
  Result := True;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  AppDataPath: string;
begin
  if CurUninstallStep = usUninstall then
  begin
    ClearData := MsgBox('是否同时清除所有 APP 数据？'#13#13 +
      '清除后将删除已安装的资源、音频缓存以及本地创建的全部数据。'#13#13 +
      '如果不清除，应用数据将保留在您的计算机上。',
      mbConfirmation, MB_YESNO) = idYes;
  end;

  if (CurUninstallStep = usPostUninstall) and ClearData then
  begin
    AppDataPath := ExpandConstant('{localappdata}\Sounder-APP');
    if DirExists(AppDataPath) then
    begin
      DelTree(AppDataPath, True, True, True);
    end;
  end;
end;
