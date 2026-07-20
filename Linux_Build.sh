#!/bin/bash
# ============================================================
#  Sounder-APP Linux 发布构建脚本
#  用法: ./Linux_Build.sh [Runtime] [--deb] [--portable] [--all]
#  示例: ./Linux_Build.sh                        # 只发布 linux-x64
#        ./Linux_Build.sh --deb                  # 发布 + .deb
#        ./Linux_Build.sh --portable             # 发布 + tar.gz
#        ./Linux_Build.sh --deb --portable       # 发布 + 两种包
#        ./Linux_Build.sh --all --deb --portable # 全架构 + 两种包
# ============================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_FILE="$SCRIPT_DIR/src/Sounder-APP.Desktop/Sounder-APP.Desktop.csproj"
OUTPUT_DIR="$SCRIPT_DIR/publish"

ALL_ARCHS=("linux-x64" "linux-arm64" "linux-arm")

# RID → Debian 架构
rid_to_deb_arch() {
    case "$1" in
        linux-x64)   echo "amd64" ;;
        linux-arm64) echo "arm64" ;;
        linux-arm)   echo "armhf" ;;
        *)           echo "amd64" ;;
    esac
}

# ============================================================
#  为单个架构执行一次完整构建
# ============================================================
build_arch() {
    local RUNTIME="$1"
    local BUILD_DEB="$2"
    local BUILD_PORTABLE="$3"
    local DEB_ARCH
    DEB_ARCH=$(rid_to_deb_arch "$RUNTIME")

    echo ""
    echo "=========================================="
    echo "  架构: $RUNTIME"
    echo "=========================================="

    # 用临时目录发布，避免覆盖 publish/ 中已有包
    local PUB_TMP
    PUB_TMP=$(mktemp -d)

    echo "[INFO]  发布 Release ($RUNTIME) ..."
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 dotnet publish "$PROJECT_FILE" \
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
        -o "$PUB_TMP"

    # 只把二进制复制到 publish/（覆盖旧架构），不清除已有包
    BINARY=$(find "$PUB_TMP" -maxdepth 1 -type f -executable | head -1)
    if [ -n "$BINARY" ]; then
        cp -a "$BINARY" "$OUTPUT_DIR/Sounder-APP"
        SIZE=$(du -h "$OUTPUT_DIR/Sounder-APP" | cut -f1)
        echo "[OK]    生成: $(basename "$BINARY") ($SIZE)"
    fi
    rm -rf "$PUB_TMP"

    VERSION=$(grep -oP '<Version>\K[^<]+' "$PROJECT_FILE" || echo "1.0.0")

    # 可选：构建 .deb 包
    if [ "$BUILD_DEB" = true ]; then
        echo "[INFO]  构建 .deb 包..."

        BUILD_DIR="$SCRIPT_DIR/build/linux"
        DEB_TMP=$(mktemp -d)
        DEB_PKG_DIR="$DEB_TMP/sounder-app_${VERSION}_${DEB_ARCH}"
        DEB_APP_DIR="$DEB_PKG_DIR/usr/lib/sounder-app"

        mkdir -p "$DEB_PKG_DIR/DEBIAN"
        mkdir -p "$DEB_APP_DIR/Assets"
        mkdir -p "$DEB_PKG_DIR/usr/share/applications"
        mkdir -p "$DEB_PKG_DIR/usr/share/icons/hicolor/256x256/apps"

        # 目录发布（非单文件），原生库作为独立文件
        echo "[INFO]  目录发布（DEB 专用）..."
        DEB_PUB_DIR=$(mktemp -d)
        DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 dotnet publish "$PROJECT_FILE" \
            -c Release \
            -r "$RUNTIME" \
            --self-contained true \
            -p:PublishTrimmed=true \
            -p:TrimMode=partial \
            -p:PublishSingleFile=false \
            -p:DebugType=none \
            -p:DebugSymbols=false \
            -o "$DEB_PUB_DIR"

        cp -a "$DEB_PUB_DIR/"* "$DEB_APP_DIR/"
        rm -rf "$DEB_PUB_DIR"

        # DEBIAN 控制文件
        for f in control postinst prerm postrm; do
            sed 's/\r$//' "$BUILD_DIR/debian/$f" > "$DEB_PKG_DIR/DEBIAN/$f"
        done
        chmod 755 "$DEB_PKG_DIR/DEBIAN/postinst"
        chmod 755 "$DEB_PKG_DIR/DEBIAN/prerm"
        chmod 755 "$DEB_PKG_DIR/DEBIAN/postrm"

        sed -i "s/^Version:.*/Version: $VERSION/" "$DEB_PKG_DIR/DEBIAN/control"
        sed -i "s/^Architecture:.*/Architecture: $DEB_ARCH/" "$DEB_PKG_DIR/DEBIAN/control"

        # 图标
        cp "$SCRIPT_DIR/src/Sounder-APP.Desktop/Assets/ico.png" \
           "$DEB_APP_DIR/Assets/Sounder-APP.png"
        cp "$SCRIPT_DIR/src/Sounder-APP.Desktop/Assets/ico.png" \
           "$DEB_PKG_DIR/usr/share/icons/hicolor/256x256/apps/sounder-app.png"

        # .desktop 文件
        cp "$BUILD_DIR/sounder-app.desktop" \
           "$DEB_PKG_DIR/usr/share/applications/sounder-app.desktop"

        # 构建 .deb
        DEB_OUTPUT="$OUTPUT_DIR/sounder-app_${VERSION}_${DEB_ARCH}.deb"
        dpkg-deb --build "$DEB_PKG_DIR" "$DEB_OUTPUT"
        rm -rf "$DEB_TMP"

        DEB_SIZE=$(du -h "$DEB_OUTPUT" | cut -f1)
        echo "[OK]    生成 .deb: $(basename "$DEB_OUTPUT") ($DEB_SIZE)"
    fi

    # 可选：构建 tar.gz 便携版
    if [ "$BUILD_PORTABLE" = true ]; then
        echo "[INFO]  构建 tar.gz 便携版..."

        # 便携版采用目录发布（非单文件），避免单文件自解压在部分 Linux 上的兼容性问题
        PORTABLE_NAME="sounder-app-${VERSION}-${RUNTIME}"
        PORTABLE_TMP=$(mktemp -d)
        PORTABLE_DIR="$PORTABLE_TMP/$PORTABLE_NAME"

        mkdir -p "$PORTABLE_DIR/Assets"
        DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 dotnet publish "$PROJECT_FILE" \
            -c Release \
            -r "$RUNTIME" \
            --self-contained true \
            -p:PublishTrimmed=true \
            -p:TrimMode=partial \
            -p:PublishSingleFile=false \
            -p:DebugType=none \
            -p:DebugSymbols=false \
            -o "$PORTABLE_DIR"

        # 清理 pdb 文件（如有）
        find "$PORTABLE_DIR" -name '*.pdb' -delete 2>/dev/null || true
        # 复制 Assets
        cp -a "$SCRIPT_DIR/src/Sounder-APP.Desktop/Assets/"* "$PORTABLE_DIR/Assets/"
        # 添加启动脚本
        cat > "$PORTABLE_DIR/run.sh" << 'SCRIPT'
#!/usr/bin/env bash
DIR="$(cd "$(dirname "$0")" && pwd)"
exec "$DIR/Sounder-APP" "$@"
SCRIPT
        chmod +x "$PORTABLE_DIR/run.sh"

        PORTABLE_OUTPUT="$OUTPUT_DIR/${PORTABLE_NAME}.tar.gz"
        tar -czf "$PORTABLE_OUTPUT" -C "$PORTABLE_TMP" "$PORTABLE_NAME"
        rm -rf "$PORTABLE_TMP"

        PORTABLE_SIZE=$(du -h "$PORTABLE_OUTPUT" | cut -f1)
        echo "[OK]    生成便携版: $(basename "$PORTABLE_OUTPUT") ($PORTABLE_SIZE)"
        echo "[INFO]  使用: tar xzf ${PORTABLE_NAME}.tar.gz && cd ${PORTABLE_NAME} && ./run.sh"
    fi

    # 清理当前架构的 bin/obj
    echo "[INFO]  清理中间编译产物..."
    for dir in "$SCRIPT_DIR/src/Sounder-APP.Desktop/bin" "$SCRIPT_DIR/src/Sounder-APP.Desktop/obj" \
               "$SCRIPT_DIR/src/Sounder-APP.Core/bin" "$SCRIPT_DIR/src/Sounder-APP.Core/obj"; do
        if [ -d "$dir" ]; then
            rm -rf "$dir"
        fi
    done
}

# ============================================================
#  解析参数
# ============================================================
RUNTIME="linux-x64"
BUILD_DEB=false
BUILD_PORTABLE=false
BUILD_ALL=false

for arg in "$@"; do
    case "$arg" in
        --deb)      BUILD_DEB=true ;;
        --portable) BUILD_PORTABLE=true ;;
        --all)      BUILD_ALL=true ;;
        --help|-h)
            echo "用法: ./Linux_Build.sh [Runtime] [--deb] [--portable] [--all]"
            echo ""
            echo "参数:"
            echo "  Runtime       目标运行时（默认 linux-x64）"
            echo "  --deb         额外构建 .deb 安装包"
            echo "  --portable    额外构建 tar.gz 便携版"
            echo "  --all         构建所有架构（linux-x64, linux-arm64, linux-arm）"
            echo ""
            echo "示例:"
            echo "  ./Linux_Build.sh                        # 只发布 linux-x64"
            echo "  ./Linux_Build.sh --deb                  # 发布 + .deb"
            echo "  ./Linux_Build.sh --all --deb --portable # 全架构 + 两种包"
            exit 0 ;;
        *)          RUNTIME="$arg" ;;
    esac
done

# ============================================================
#  执行构建
# ============================================================

# 只 restore 一次
echo "[INFO]  还原 NuGet 包..."
DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 dotnet restore "$PROJECT_FILE" -p:RestorePackagesWithLock=false

# 初始化 publish/ 目录
rm -rf "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR"

if [ "$BUILD_ALL" = true ]; then
    for arch in "${ALL_ARCHS[@]}"; do
        build_arch "$arch" "$BUILD_DEB" "$BUILD_PORTABLE"
    done
else
    build_arch "$RUNTIME" "$BUILD_DEB" "$BUILD_PORTABLE"
fi

echo ""
echo "=========================================="
echo "[OK]    全部完成！产物位于: $OUTPUT_DIR"
echo "=========================================="
ls -lh "$OUTPUT_DIR" 2>/dev/null || true
echo ""
