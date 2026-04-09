#!/bin/bash
set -e

# Detect platform RID
case "$(uname -s)-$(uname -m)" in
    Linux-x86_64)  RID="linux-x64"; EXT="so"; PREFIX="lib" ;;
    Linux-aarch64) RID="linux-arm64"; EXT="so"; PREFIX="lib" ;;
    Darwin-x86_64) RID="osx-x64"; EXT="dylib"; PREFIX="lib" ;;
    Darwin-arm64)  RID="osx-arm64"; EXT="dylib"; PREFIX="lib" ;;
    *)             echo "Unsupported platform: $(uname -s)-$(uname -m)"; exit 1 ;;
esac

NATIVE_DIR="runtimes/${RID}/native"
NATIVE_LIB="${NATIVE_DIR}/${PREFIX}rerun_c.${EXT}"

if [ -f "$NATIVE_LIB" ]; then
    echo "Native library already exists: $NATIVE_LIB"
    exit 0
fi

echo "Building rerun_c for ${RID}..."
cd extern/rerun
cargo rustc -p rerun_c --release --crate-type cdylib
cd ../..

mkdir -p "$NATIVE_DIR"
cp "extern/rerun/target/release/${PREFIX}rerun_c.${EXT}" "$NATIVE_LIB"
echo "Installed: $NATIVE_LIB"
