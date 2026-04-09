FROM fnjn/cs1.6

WORKDIR /root/hlds

# Fix CPU_MHZ warning (required for hlds_run on ARM/emulated environments)
ENV CPU_MHZ=1000

# Default values (overridden per-service in docker-compose)
ENV SV_PORT=27015
ENV SV_MAP=de_dust2
ENV SV_NAME="Modern CS 1.6 Vietnam"
ENV SV_MAXPLAYERS=32

# Install dependencies needed for plugins
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl unzip xz-utils lib32stdc++6 && rm -rf /var/lib/apt/lists/*

# Fix steamclient.so path (ReHLDS looks for ~/.steam/sdk32/steamclient.so)
RUN mkdir -p /root/.steam/sdk32 /root/.steam/sdk64 && \
    ln -sf /root/linux32/steamclient.so /root/.steam/sdk32/steamclient.so 2>/dev/null || \
    ln -sf /root/hlds/steamclient.so /root/.steam/sdk32/steamclient.so || true

# Copy ReHLDS zip and extract
COPY ./rehlds/rehlds-bin-3.14.0.857.zip /tmp/rehlds.zip
RUN unzip -o /tmp/rehlds.zip -d /tmp/rehlds_build && \
    cp -rf /tmp/rehlds_build/bin/linux32/. ./ && \
    rm -rf /tmp/rehlds.zip /tmp/rehlds_build

# Copy CS 1.6 game files
COPY ./cstrike/ ./cstrike/

# Auto-decompress .ztmp files (bzip2 FastDL compressed format) → real files for serving
RUN find ./cstrike -name "*.ztmp" | while read f; do \
      out="${f%.ztmp}"; \
      bunzip2 -c "$f" > "$out" && echo "Decompressed: $out" || true; \
    done

# Install dproto — download trực tiếp (tránh macOS xattr issue khi COPY)
RUN mkdir -p /root/hlds/cstrike/addons/dproto && \
    curl -fsSLo /root/hlds/cstrike/addons/dproto/dproto_i386.so \
      "https://raw.githubusercontent.com/artkirienko/hlds-docker-dproto/master/lib/dproto/bin/Linux/dproto_i386.so" && \
    chmod +x /root/hlds/cstrike/addons/dproto/dproto_i386.so && \
    echo "[dproto] installed $(ls -lh /root/hlds/cstrike/addons/dproto/dproto_i386.so)"

# CRITICAL: Hook Metamod into engine via liblist.gam
# Without this, Metamod never loads → dproto never loads → non-Steam gets rejected
RUN sed -i 's|gamedll_linux "dlls/cs.so"|gamedll_linux "addons/metamod/metamod_i386.so"|' \
      /root/hlds/cstrike/liblist.gam && \
    grep "gamedll_linux" /root/hlds/cstrike/liblist.gam

# Upgrade AMXX 1.8.2 → 1.10 (fix Segfault với ReHLDS trên QEMU i386 emulation)
# AMXX 1.8.2 (từ base image) không tương thích với ReHLDS → crash khi player join
RUN AMXX_BASE="https://www.amxmodx.org/amxxdrop/1.10" && \
    curl -fsSL "${AMXX_BASE}/amxmodx-1.10.0-git5474-base-linux.tar.gz" | tar -xz -C /root/hlds/cstrike && \
    curl -fsSL "${AMXX_BASE}/amxmodx-1.10.0-git5474-cstrike-linux.tar.gz" | tar -xz -C /root/hlds/cstrike && \
    echo "[AMXX] Upgraded to 1.10-git5474"

# Install YAPB 4.4.957 — AI bot plugin (Metamod) với auto-balance
# Binary: extras.zip/linux-x86-gcc/yapb.so, nav graphs: linux.tar.xz
RUN YAPB_VER="4.4.957" && YAPB_BASE="https://github.com/yapb/yapb/releases/download/${YAPB_VER}" && \
    mkdir -p /root/hlds/cstrike/addons/yapb/bin && \
    curl -fsSL "${YAPB_BASE}/yapb-${YAPB_VER}-linux.tar.xz" | tar -xJ -C /root/hlds/cstrike && \
    curl -fsSL "${YAPB_BASE}/yapb-${YAPB_VER}-extras.zip" -o /tmp/yapb_extras.zip && \
    unzip -o /tmp/yapb_extras.zip -d /tmp/yapb_bin && \
    cp /tmp/yapb_bin/linux-x86-gcc/yapb.so /root/hlds/cstrike/addons/yapb/bin/yapb.so && \
    rm -rf /tmp/yapb_extras.zip /tmp/yapb_bin && \
    echo "[YAPB] ${YAPB_VER}: $(ls /root/hlds/cstrike/addons/yapb/bin/)"

# Copy ReGameDLL_CS — enhanced CS gamedll 
COPY ./downloads/regamedll-bin-5.28.0.756.zip /tmp/regamedll.zip
RUN unzip -o /tmp/regamedll.zip "bin/linux32/cstrike/dlls/cs.so" -d /tmp/rdll \
    && cp /tmp/rdll/bin/linux32/cstrike/dlls/cs.so /root/hlds/cstrike/dlls/cs.so \
    && rm -rf /tmp/regamedll.zip /tmp/rdll

# Entrypoint script nhận PORT, MAP, LAN từ env
ENV SV_LAN=0
RUN printf '#!/bin/sh\nexec ./hlds_run \\\n  -game cstrike \\\n  -port "${SV_PORT}" \\\n  +ip 0.0.0.0 \\\n  +maxplayers "${SV_MAXPLAYERS}" \\\n  +map "${SV_MAP}" \\\n  +hostname "${SV_NAME}" \\\n  +sv_lan "${SV_LAN}" \\\n  +sys_ticrate 1000 \\\n  -insecure \\\n  -nowatchdog\n' > /start.sh && chmod +x /start.sh

EXPOSE 27015/udp
EXPOSE 27015/tcp

CMD ["/start.sh"]
