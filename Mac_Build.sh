#!/bin/bash
# ============================================================
#  Sounder-APP macOS 发布构建脚本
#  用法: chmod +x Mac_Build.sh && ./Mac_Build.sh [Runtime]
#  示例: ./Mac_Build.sh
#        ./Mac_Build.sh osx-arm64
# ============================================================
set -euo pipefail

RUNTIME="${1:-osx-x64}"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_FILE="$SCRIPT_DIR/src/Sounder-APP.Desktop/Sounder-APP.Desktop.csproj"
OUTPUT_DIR="$SCRIPT_DIR/publish"

echo "[INFO]  目标运行时: $RUNTIME"
echo "[INFO]  输出目录:   $OUTPUT_DIR"

# step 1 - 清理旧输出
if [ -d "$OUTPUT_DIR" ]; then
    echo "[INFO]  清理旧输出: $OUTPUT_DIR"
    rm -rf "$OUTPUT_DIR"
fi

# step 2 - 恢复 NuGet 包
echo "[INFO]  还原 NuGet 包..."
dotnet restore "$PROJECT_FILE" -p:RestorePackagesWithLock=false

# step 3 - 发布 Release 单文件
echo "[INFO]  发布 Release ($RUNTIME) ..."
dotnet publish "$PROJECT_FILE" \
    -c Release \
    -r "$RUNTIME" \
    --self-contained true \
    -p:PublishTrimmed=true \
    -p:TrimMode=partial \
    -p:PublishSingleFile=true \
    -p:EnableCompressionInSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:DebugType=none \
    -p:DebugSymbols=false \
    -o "$OUTPUT_DIR"

# step 4 - 构建 .app 应用包
EXECUTABLE="$OUTPUT_DIR/Sounder-APP"
if [ ! -f "$EXECUTABLE" ]; then
    echo "[ERROR] 可执行文件不存在: $EXECUTABLE"
    exit 1
fi

echo "[INFO]  构建 .app 应用包..."
APP_BUNDLE="$OUTPUT_DIR/Sounder-APP.app"
APP_CONTENTS="$APP_BUNDLE/Contents"
APP_MACOS="$APP_CONTENTS/MacOS"
APP_RESOURCES="$APP_CONTENTS/Resources"

mkdir -p "$APP_MACOS" "$APP_RESOURCES"

# 可执行文件
cp "$EXECUTABLE" "$APP_MACOS/"
echo "[OK]    可执行文件已放入 .app"

# Info.plist
cp "$SCRIPT_DIR/build/macos/Info.plist" "$APP_CONTENTS/"

# 应用图标 — 从 Assets/ico.png 生成 .icns
ICON_SRC="$SCRIPT_DIR/src/Sounder-APP.Desktop/Assets/ico.png"
ICONSET_DIR="$OUTPUT_DIR/Sounder-APP.iconset"
ICON_DEST="$APP_RESOURCES/Sounder-APP.icns"

if command -v iconutil &>/dev/null && [ -f "$ICON_SRC" ]; then
    mkdir -p "$ICONSET_DIR"
    sips -z 16 16   "$ICON_SRC" --out "$ICONSET_DIR/icon_16x16.png"        &>/dev/null
    sips -z 32 32   "$ICON_SRC" --out "$ICONSET_DIR/icon_16x16@2x.png"     &>/dev/null
    sips -z 32 32   "$ICON_SRC" --out "$ICONSET_DIR/icon_32x32.png"        &>/dev/null
    sips -z 64 64   "$ICON_SRC" --out "$ICONSET_DIR/icon_32x32@2x.png"     &>/dev/null
    sips -z 128 128 "$ICON_SRC" --out "$ICONSET_DIR/icon_128x128.png"      &>/dev/null
    sips -z 256 256 "$ICON_SRC" --out "$ICONSET_DIR/icon_128x128@2x.png"   &>/dev/null
    sips -z 256 256 "$ICON_SRC" --out "$ICONSET_DIR/icon_256x256.png"      &>/dev/null
    sips -z 512 512 "$ICON_SRC" --out "$ICONSET_DIR/icon_256x256@2x.png"   &>/dev/null
    sips -z 512 512 "$ICON_SRC" --out "$ICONSET_DIR/icon_512x512.png"      &>/dev/null
    sips -z 1024 1024 "$ICON_SRC" --out "$ICONSET_DIR/icon_512x512@2x.png" &>/dev/null
    iconutil -c icns "$ICONSET_DIR" -o "$ICON_DEST" &>/dev/null
    rm -rf "$ICONSET_DIR"
    echo "[OK]    应用图标已生成 (来源: Assets/ico.png)"
else
    echo "[WARN]  iconutil 不可用或源图标不存在，跳过 .icns 生成"
fi

# 语言本地化目录
cp -R "$SCRIPT_DIR/build/macos/en.lproj" "$APP_RESOURCES/"
cp -R "$SCRIPT_DIR/build/macos/zh_CN.lproj" "$APP_RESOURCES/"
echo "[OK]    本地化资源已放入 .app"

# step 5 - 生成 .pkg 安装包
# 从 csproj 提取版本号
VERSION=$(grep -m1 '<Version>' "$PROJECT_FILE" | sed 's/.*<Version>\(.*\)<\/Version>.*/\1/')
PKG_FILE="$OUTPUT_DIR/Sounder-APP-$RUNTIME.pkg"

echo "[INFO]  生成 .pkg 安装包 (v$VERSION)..."

if ! command -v pkgbuild &> /dev/null; then
    echo "[WARN]  未找到 pkgbuild 命令，跳过 .pkg 生成"
else
    pkgbuild \
        --root "$APP_BUNDLE" \
        --identifier "com.sounderapp.desktop" \
        --version "$VERSION" \
        --install-location "/Applications/Sounder-APP.app" \
        "$PKG_FILE" 2>&1
    echo "[OK]    安装包: $PKG_FILE"
fi

# step 6 - 清理中间编译产物
echo "[INFO]  清理中间编译产物..."
for dir in "$SCRIPT_DIR/src/Sounder-APP.Desktop/bin" "$SCRIPT_DIR/src/Sounder-APP.Desktop/obj" "$SCRIPT_DIR/src/Sounder-APP.Core/bin" "$SCRIPT_DIR/src/Sounder-APP.Core/obj"; do
    if [ -d "$dir" ]; then
        rm -rf "$dir"
        echo "[OK]    已删除: $(basename "$dir")"
    fi
done

# step 7 - 显示结果
echo ""
echo "[OK]    发布完成！输出目录: $OUTPUT_DIR"
EXEC_SIZE=$(du -h "$EXECUTABLE" | cut -f1)
echo "[OK]    可执行文件: $(basename "$EXECUTABLE") ($EXEC_SIZE)"
echo "[OK]    应用包:     Sounder-APP.app"
if [ -f "$PKG_FILE" ]; then
    PKG_SIZE=$(du -h "$PKG_FILE" | cut -f1)
    echo "[OK]    安装包:     $(basename "$PKG_FILE") ($PKG_SIZE)"
fi
echo ""
echo "[INFO]  下一步:"
echo "[INFO]    安装: 双击 $PKG_FILE 或拖拽 Sounder-APP.app 到 Applications"
echo ""
