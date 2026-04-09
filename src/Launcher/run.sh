#!/bin/bash
# ─── CS 1.6 Modern Launcher — Cross-Platform Run Script ───────────────────────
# Tự động cài thư viện SDL2 phù hợp theo OS rồi khởi chạy Launcher

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$SCRIPT_DIR"

echo "=== CS 1.6 Modern Launcher ==="

# ── Detect OS ──────────────────────────────────────────────────────────────────
if [[ "$OSTYPE" == "darwin"* ]]; then
    echo "[macOS] Kiểm tra thư viện SDL2..."
    if ! brew list sdl2 > /dev/null 2>&1; then
        echo "[macOS] Chưa có SDL2 — đang cài qua Homebrew..."
        if ! command -v brew &>/dev/null; then
            echo "[macOS] Cài Homebrew trước..."
            /bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"
        fi
        brew install sdl2
    fi
    # Set library path cho Apple Silicon và Intel
    export DYLD_LIBRARY_PATH="/opt/homebrew/lib:/usr/local/lib:$DYLD_LIBRARY_PATH"
    echo "[macOS] SDL2 OK — Khởi động Launcher..."

elif [[ "$OSTYPE" == "linux-gnu"* ]]; then
    echo "[Linux] Kiểm tra thư viện SDL2..."
    if ! ldconfig -p | grep -q "libSDL2" 2>/dev/null; then
        echo "[Linux] Chưa có SDL2 — đang cài qua apt..."
        sudo apt-get update -qq && sudo apt-get install -y libsdl2-2.0-0 libsdl2-dev
    fi
    echo "[Linux] SDL2 OK — Khởi động Launcher..."

else
    echo "[Unknown OS] Thử khởi động trực tiếp..."
fi

# ── Chạy Launcher ─────────────────────────────────────────────────────────────
dotnet run --project Launcher.csproj
