#!/bin/bash
# Script: deploy-bridge.sh
# Dùng để: compile .sma → .amxx → deploy lên tất cả containers
# Usage: ./deploy-bridge.sh

set -e

PLUGIN_NAME="cs16_bridge"
SMA_FILE="server-data/cstrike/addons/amxmodx/plugins/${PLUGIN_NAME}.sma"
AMXX_FILE="server-data/cstrike/addons/amxmodx/plugins/${PLUGIN_NAME}.amxx"
CONTAINER_MAIN="cs16-italy-1"
ALL_CONTAINERS="cs16-italy-2 cs16-italy-3 cs16-dust2 cs16-inferno cs16-nuke"

echo "🔨 [1/4] Copy .sma vào container để compile..."
docker cp "$SMA_FILE" "$CONTAINER_MAIN:/root/hlds/cstrike/addons/amxmodx/scripting/${PLUGIN_NAME}.sma"

echo "⚙️  [2/4] Compile trong container..."
docker exec "$CONTAINER_MAIN" bash -c \
  "cd /root/hlds/cstrike/addons/amxmodx/scripting && ./amxxpc ${PLUGIN_NAME}.sma -o../plugins/${PLUGIN_NAME}.amxx"

echo "📦 [3/4] Lấy .amxx về local..."
docker cp "$CONTAINER_MAIN:/root/hlds/cstrike/addons/amxmodx/plugins/${PLUGIN_NAME}.amxx" "$AMXX_FILE"

echo "🚀 [4/4] Deploy lên tất cả containers..."
for c in $ALL_CONTAINERS; do
  docker cp "$AMXX_FILE" "$c:/root/hlds/cstrike/addons/amxmodx/plugins/"
  echo "   ✅ $c"
done

echo ""
echo "🔄 Restart tất cả servers..."
docker restart $CONTAINER_MAIN $ALL_CONTAINERS

echo ""
echo "✅ DONE! Bridge plugin đã được cập nhật trên tất cả 6 servers."
echo "📋 Xem log: docker exec $CONTAINER_MAIN cat /root/hlds/cstrike/addons/amxmodx/logs/L\$(date +%Y%m%d).log | grep CS16Bridge"
