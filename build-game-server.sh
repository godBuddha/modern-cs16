#!/bin/bash
# CS 1.6 VN Server - Build Script
# QUAN TRỌNG: Phải dùng --platform linux/amd64 để ReHLDS API hoạt động đúng
# Lý do: ReUnion/rechecker/resemiclip cần ReHLDS API qua memory pattern scanning
# Trên ARM64 Mac, chạy dưới QEMU double-emulation (arm64→x86_64→i386) sẽ fail

set -e

echo "[CS16VN] Syncing server-data to /tmp/cs16-build (loại trừ macOS ._* files)..."
rsync -a --exclude='._*' --exclude='.DS_Store' \
  "$(dirname "$0")/server-data/" \
  /tmp/cs16-build/

echo "[CS16VN] Building Docker images (platform: linux/amd64)..."
docker buildx build \
  --platform linux/amd64 \
  -t modern-cs16-cs-italy-1 \
  -t modern-cs16-cs-italy-2 \
  -t modern-cs16-cs-italy-3 \
  -t modern-cs16-cs-dust2 \
  -t modern-cs16-cs-inferno \
  -t modern-cs16-cs-nuke \
  --load \
  /tmp/cs16-build/

echo "[CS16VN] Build done! Restart containers với:"
echo "  docker compose --profile game up -d --force-recreate"
