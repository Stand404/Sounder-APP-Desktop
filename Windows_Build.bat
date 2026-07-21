@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

REM ============================================================
REM  Sounder-APP Windows 发布构建脚本
REM  用法: Windows_Build.bat [Runtime]
REM  示例: Windows_Build.bat           (默认 win-x64)
REM        Windows_Build.bat win-x86
REM ============================================================

set "Runtime=win-x64"
if not "%~1"=="" set "Runtime=%~1"

set "ProjectFile=%~dp0src\Sounder-APP.Desktop\Sounder-APP.Desktop.csproj"
set "OutputDir=%~dp0publish"

echo ==INFO==  目标运行时: %Runtime%
echo ==INFO==  输出目录:   %OutputDir%

REM step 1 - 清理旧输出
if exist "%OutputDir%" (
    echo ==INFO==  清理旧输出: %OutputDir%
    rmdir /s /q "%OutputDir%"
)

REM step 2 - 恢复 NuGet 包
echo ==INFO==  还原 NuGet 包...
call dotnet restore "%ProjectFile%" -p:RestorePackagesWithLock=false
if %ERRORLEVEL% neq 0 (
    echo ==ERROR== dotnet restore 失败
    exit /b 1
)

REM step 3 - 发布 Release 单文件
echo ==INFO==  发布 Release (%Runtime%) ...
call dotnet publish "%ProjectFile%" ^
    -c Release ^
    -r %Runtime% ^
    --self-contained true ^
    -p:PublishTrimmed=true ^
    -p:TrimMode=partial ^
    -p:PublishSingleFile=true ^
    -p:EnableCompressionInSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:DebugType=none ^
    -p:DebugSymbols=false ^
    -o "%OutputDir%"

if %ERRORLEVEL% neq 0 (
    echo ==ERROR== dotnet publish 失败
    exit /b 1
)

REM step 4 - 清理中间编译产物
echo ==INFO==  清理中间编译产物...
for %%d in ("%~dp0src\Sounder-APP.Desktop\bin", "%~dp0src\Sounder-APP.Desktop\obj", "%~dp0src\Sounder-APP.Core\bin", "%~dp0src\Sounder-APP.Core\obj") do (
    if exist "%%d" (
        rmdir /s /q "%%d"
        echo ==OK==    已删除: %%~nxd
    )
)

REM step 5 - 显示结果
echo ==OK==    发布完成！输出目录: %OutputDir%
for %%f in ("%OutputDir%\*.exe") do (
    set "size=%%~zf"
    set /a "mb=!size! / 1048576"
    echo ==OK==    生成: %%~nxf (!mb! MB)
)

echo:
echo ==INFO==  下一步:
echo ==INFO==    用 Inno Setup 编译安装包:
echo ==INFO==      ISCC.exe Windows_Installer.iss
echo ==INFO==    或双击 Windows_Installer.iss
echo:
exit /b 0
