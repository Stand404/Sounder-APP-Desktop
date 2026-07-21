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

# step 4 - 清理中间编译产物
echo "[INFO]  清理中间编译产物..."
for dir in "$SCRIPT_DIR/src/Sounder-APP.Desktop/bin" "$SCRIPT_DIR/src/Sounder-APP.Desktop/obj" "$SCRIPT_DIR/src/Sounder-APP.Core/bin" "$SCRIPT_DIR/src/Sounder-APP.Core/obj"; do
    if [ -d "$dir" ]; then
        rm -rf "$dir"
        echo "[OK]    已删除: $(basename "$dir")"
    fi
done

# step 5 - 显示结果
echo "[OK]    发布完成！输出目录: $OUTPUT_DIR"
FILE=$(find "$OUTPUT_DIR" -maxdepth 1 -type f -perm +111 | head -1)
if [ -n "$FILE" ]; then
    SIZE=$(du -h "$FILE" | cut -f1)
    echo "[OK]    生成: $(basename "$FILE") ($SIZE)"
fi

echo ""
echo "[INFO]  下一步:"
echo "[INFO]    可执行文件位于: $OUTPUT_DIR"
echo "[INFO]    运行: ./$OUTPUT_DIR/Sounder-APP"
echo ""
