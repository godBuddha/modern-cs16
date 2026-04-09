<div align="center">

# 🎮 Modern CS 1.6 Vietnam

**Hệ sinh thái máy chủ Counter-Strike 1.6 hiện đại — Tự host, đầy đủ tính năng**

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Docker](https://img.shields.io/badge/Docker-Compose-2496ED?logo=docker)](https://docker.com)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-15-336791?logo=postgresql)](https://postgresql.org)
[![Electron](https://img.shields.io/badge/Electron-Launcher-47848F?logo=electron)](https://electronjs.org)
[![License](https://img.shields.io/badge/License-MIT-green)](LICENSE)

*Dành cho cộng đồng CS 1.6 Việt Nam ❤️*

</div>

---

## 📖 Giới Thiệu

**Modern CS 1.6 Vietnam** là một hệ thống hoàn chỉnh để vận hành cộng đồng Counter-Strike 1.6, bao gồm:

- 🖥️ **Hệ thống máy chủ game Docker** — Chạy nhiều server CS 1.6 (Non-Steam) cùng lúc với quản lý tài nguyên tự động
- 🔌 **REST API Backend** — ASP.NET Core 8, xử lý authentication, ranking, stats, tournament, rooms
- 📊 **Dashboard quản trị** — Giao diện web quản lý server, player, KYC, donation, giải đấu
- 🚀 **Electron Launcher** — App Windows cho người chơi: đăng nhập, xem server, tạo phòng, đăng ký giải đấu, donate
- 🏆 **Hệ thống giải đấu** — Tổ chức tournament với spawn Docker container tự động, HLTV relay, xuất kết quả Excel
- 🔒 **KYC Verification** — Xác minh danh tính người chơi trước khi host phòng/giải đấu
- 💸 **Hệ thống Donate** — Tích hợp VietQR, upload bằng chứng thanh toán, phân loại donate
- 💬 **Spectator Chat** — Chat real-time cho người xem khi theo dõi trận đấu

---

## ✨ Tính Năng Nổi Bật

### 🎯 Gameplay & Server
- **Multi-server**: Quản lý đồng thời nhiều server (Italy, Dust2, Inferno, Nuke,...)
- **Player Rooms**: Người chơi tự tạo phòng riêng → tự động spawn Docker container
- **A2S_INFO Query**: Real-time server status (players, map, ping)
- **FastDL**: Nginx serve map/skin tự động cho client
- **RCON over UDP**: Gửi lệnh admin vào server từ Dashboard

### 🏆 Tournament System
| Tính năng | Chi tiết |
|-----------|---------|
| Thể thức | 1vs1, 3vs3, 5vs5, 10vs10 |
| Quy ước | 5 round (thắng 3/5) hoặc 10 round (thắng 8/10) |
| Map pool | De_dust2, cs_italy, de_inferno, de_nuke +custom |
| Docker auto-spawn | Mỗi trận = 1 container riêng (port 27200–27299) |
| HLTV Relay | Spectators connect port riêng, không ảnh hưởng game |
| Xuất Excel | Kết quả từng trận + toàn giải (.xlsx) |
| KYC Guard | Chỉ tài khoản đã xác minh mới tổ chức giải |

### 🔐 Bảo mật
- JWT Authentication cho tất cả API endpoint
- KYC (CCCD + selfie video) trước khi tạo phòng/giải đấu
- Docker container guard — không thể xóa nhầm server gốc
- Container prefix isolation: `cs16-room-*`, `cs16-tour-*`, `cs16-managed-*`

### 💰 Hệ thống Credits & Donate
- Entry fee tournament → Credits
- Donate cho Dev hoặc cho Giải đấu
- VietQR auto-generate với mã tham chiếu
- Upload ảnh bằng chứng thanh toán
- Admin confirm/reject trong Dashboard

---

## 🗂 Kiến Trúc Dự Án

```
modern-cs16/
├── 📄 docker-compose.yml          # Orchestration toàn bộ hệ thống
├── 📄 .env.example                # Template biến môi trường
├── 📄 PLAN.md                     # Kế hoạch phát triển chi tiết
│
├── 🎮 server-data/                # CS 1.6 Game Server (ReHLDS)
│   ├── Dockerfile                 # Build image game server
│   └── cstrike/
│       └── addons/amxmodx/
│           ├── plugins/
│           │   ├── cs16_bridge.sma    # Plugin gửi kill events lên API
│           │   └── auth_lock.sma      # Chặn player chưa đăng nhập
│           └── configs/
│               ├── plugins.ini
│               └── modules.ini
│
├── ⚙️ src/
│   ├── Api/                       # ASP.NET Core 8 REST API (port 7777)
│   │   ├── Controllers/
│   │   │   ├── AuthController.cs          # Register/Login/JWT
│   │   │   ├── ServersController.cs       # Game server management
│   │   │   ├── RconController.cs          # RCON commands (UDP GoldSrc)
│   │   │   ├── PlayerRoomsController.cs   # Tạo/xóa phòng + Docker
│   │   │   ├── TournamentsController.cs   # Quản lý giải đấu
│   │   │   ├── TournamentMatchesController.cs # Trận đấu + HLTV
│   │   │   ├── DonationsController.cs     # VietQR donations
│   │   │   ├── FeedbackController.cs      # Góp ý
│   │   │   ├── KycController.cs           # Xác minh danh tính
│   │   │   └── PlayersController.cs       # Ranking + stats
│   │   ├── Models/Models.cs               # EF Core entities
│   │   ├── Data/AppDbContext.cs           # Database context
│   │   └── Services/
│   │       └── DockerGameServerService.cs # Docker socket management
│   │
│   ├── Dashboard/                 # ASP.NET Core 8 Razor Pages (port 7001)
│   │   └── Pages/
│   │       ├── Index.cshtml           # Dashboard tổng quan
│   │       ├── Server.cshtml          # RCON console + Rooms management
│   │       ├── Servers.cshtml         # Danh sách game servers
│   │       ├── Tournaments.cshtml     # Quản lý giải đấu
│   │       ├── Players.cshtml         # Quản lý người chơi
│   │       ├── Kyc.cshtml             # Duyệt KYC
│   │       ├── Donations.cshtml       # Quản lý donations
│   │       └── Rankings.cshtml        # Bảng xếp hạng ELO
│   │
│   └── ElectronLauncher/          # Electron app cho Windows
│       ├── main.js                    # Electron main process
│       ├── preload.js                 # Bridge IPC
│       └── renderer/
│           ├── index.html             # UI chính
│           ├── app.js                 # Logic tất cả tính năng
│           └── style.css              # Dark theme ui
│
├── 🗄️ database/
│   └── init-db.sql                # PostgreSQL schema khởi tạo
│
├── 🌐 nginx-fastdl/
│   └── nginx.conf                 # FastDL serve maps/skins
│
└── 📚 docs/
    └── setup.md                   # Hướng dẫn cài đặt chi tiết
```

---

## 🛠 Tech Stack

| Component | Technology | Version |
|-----------|-----------|---------|
| Game Engine | ReHLDS + ReGameDLL_CS | 3.14 / 5.28 |
| Plugin System | Metamod-r + ReAPI + AMX Mod X | 1.3 / 5.26 / 1.8.2 |
| Non-Steam Auth | ReUnion (dproto) | 0.2.0 |
| Backend API | ASP.NET Core + EF Core + JWT | 8.0 |
| Database | PostgreSQL | 15 |
| Dashboard | Razor Pages + Bootstrap 5 | 8.0 |
| Launcher | Electron + Vanilla JS | Latest |
| Containerization | Docker + Docker Compose | v2 |
| HTTP | Nginx (FastDL) | Alpine |
| RCON | GoldSrc UDP challenge/response | Built-in |
| Excel export | ClosedXML | Latest |

---

## ⚡ Quick Start

### Yêu cầu
- 🐳 Docker Desktop 4.x trở lên
- 💾 RAM ≥ 4GB, Disk ≥ 20GB
- 🖥️ macOS / Ubuntu / Windows (WSL2)

### Cài đặt

```bash
# 1. Clone dự án
git clone https://github.com/YourUsername/modern-cs16.git
cd modern-cs16

# 2. Cấu hình môi trường (BẮT BUỘC đổi mật khẩu!)
cp .env.example .env
nano .env

# 3. Khởi động services chính
docker compose up -d

# 4. Khởi động game servers
docker compose --profile game up -d

# 5. Kiểm tra
curl http://localhost:7777/health     # API ✅
open http://localhost:7001            # Dashboard ✅
```

### Ports

| Service | Port | Giao thức |
|---------|------|----------|
| REST API | 7777 | HTTP |
| Dashboard | 7001 | HTTP |
| Game Server Italy #1 | 27015 | UDP |
| Game Server Italy #2 | 27016 | UDP |
| Game Server Italy #3 | 27017 | UDP |
| Game Server Dust2 | 27018 | UDP |
| Game Server Inferno | 27019 | UDP |
| Game Server Nuke | 27020 | UDP |
| Player Rooms (dynamic) | 27021–27099 | UDP |
| Tournament Matches (dynamic) | 27200–27299 | UDP |
| FastDL (Nginx) | 8080 | HTTP |
| PostgreSQL | 5432 | TCP |

### Tạo tài khoản admin đầu tiên

```bash
curl -X POST http://localhost:7777/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "username": "admin",
    "password": "MatKhauAdmin123!",
    "email": "admin@yourserver.com",
    "displayName": "Admin"
  }'
```

---

## 📡 API Endpoints

### Authentication
```
POST /api/auth/register     — Đăng ký
POST /api/auth/login        — Đăng nhập → JWT
POST /api/auth/forgot       — Quên mật khẩu
```

### Player Rooms
```
GET    /api/rooms            — Danh sách phòng đang mở
POST   /api/rooms            — Tạo phòng (KYC required) → spawn Docker
DELETE /api/rooms/{id}       — Xóa phòng (chỉ chủ phòng)
POST   /api/rooms/{id}/kick  — Kick player
POST   /api/rooms/{id}/message — Admin broadcast
```

### Tournaments
```
GET    /api/tournaments                              — Danh sách giải
POST   /api/tournaments                             — Tạo giải (KYC required)
GET    /api/tournaments/{id}/matches                — Trận trong giải
POST   /api/tournaments/{id}/matches               — Tạo trận
POST   /api/tournament-matches/{id}/start-room     — Spawn container + HLTV
PATCH  /api/tournament-matches/{id}/score          — Cập nhật tỉ số
GET    /api/tournament-matches/{id}/export         — Xuất Excel 1 trận
GET    /api/tournaments/{id}/export                — Xuất Excel toàn giải
POST   /api/tournament-matches/{id}/broadcast      — RCON vào phòng đấu
POST   /api/tournament-matches/{id}/spec-chat      — Spectator chat
GET    /api/tournament-matches/{id}/spec-chat      — Lấy chat history
```

### Donations
```
GET  /api/donations              — Danh sách donations (admin)
POST /api/donations              — Tạo donation + VietQR
POST /api/donations/{id}/upload-proof — Upload bằng chứng
PATCH /api/donations/{id}/confirm     — Xác nhận (admin)
```

### Others
```
POST /api/rcon/command          — Gửi lệnh RCON vào server
POST /api/feedback              — Góp ý
GET  /api/kyc/my-status         — Status KYC của user
GET  /api/rankings              — Bảng xếp hạng ELO
```

---

## 🖥️ Electron Launcher

Launcher cho **Windows** (hỗ trợ Non-Steam CS 1.6 Build 4554):

| Tab | Tính năng |
|-----|----------|
| 🏠 Trang chủ | Thống kê hệ thống, tin nhắn chào mừng |
| 🎮 Server Browser | Danh sách server, ping, join game |
| 🏆 Rankings | Bảng xếp hạng ELO, K/D ratio |
| 🔒 KYC | Upload CCCD + selfie video xác minh |
| 🏅 Giải đấu | Xem/đăng ký giải, theo dõi tỉ số real-time |
| ➕ Tạo giải | Form tạo tournament (KYC required) |
| 🚪 Tạo phòng | Host phòng riêng + xem phòng đang mở |
| 💖 Donate | VietQR cho Dev hoặc Giải đấu |
| ❓ Hướng dẫn | Lệnh AMX, góp ý Bug/Suggestion |
| ℹ️ Giới thiệu | Version, links, thống kê |

### Build Launcher

```bash
cd src/ElectronLauncher
npm install
npm run build:win   # Build .exe cho Windows
```

---

## 🗄️ Database Schema

Hệ thống sử dụng **EF Core migrations** với PostgreSQL 15:

```
Players (AspNetUsers)   ← Identity
PlayerStats             ← K/D/ELO/Credits
KycSubmissions          ← CCCD + Video status
GameServers             ← Danh sách server
PlayerRooms             ← Phòng người chơi tạo
SpectatorMessages       ← Chat spectator
Tournaments             ← Giải đấu
TournamentMatches       ← Trận đấu trong giải
TournamentRegistrations ← Đăng ký tham gia
Donations               ← Lịch sử donate
KillLogs                ← Log kill events từ AMX plugin
Feedbacks               ← Góp ý người chơi
```

---

## 🐳 Docker Architecture

```
modern-cs16_default (internal network)
│
├── cs16-italy-1  (27015/udp) ─── game server [profile: game]
├── cs16-italy-2  (27016/udp) ─── game server [profile: game]
├── cs16-italy-3  (27017/udp) ─── game server [profile: game]
├── cs16-dust2    (27018/udp) ─── game server [profile: game]
├── cs16-inferno  (27019/udp) ─── game server [profile: game]
├── cs16-nuke     (27020/udp) ─── game server [profile: game]
│
├── api           (7777/http)  ─── ASP.NET Core REST API
├── dashboard     (7001/http)  ─── Razor Pages Admin UI
├── postgres-db   (5432/tcp)   ─── PostgreSQL 15
└── nginx-fastdl  (8080/http)  ─── FastDL server
│
│   [Dynamic - created by API]
├── cs16-room-{port}   (27021-27099) ─── Player rooms
├── cs16-tour-{port}   (27200-27299) ─── Tournament matches
└── cs16-hltv-{port}   (27201-27299) ─── HLTV relay (spectators)
```

> **Container Guard**: `DockerGameServerService` chỉ xóa containers có prefix `cs16-room-*`, `cs16-tour-*`, hoặc `cs16-managed-*`. Các server gốc **không bao giờ bị xóa nhầm**.

---

## 🚀 Deployment (Production)

```bash
# 1. Server Ubuntu 22.04+
apt install docker.io docker-compose-plugin -y

# 2. Clone và cấu hình
git clone https://github.com/YourUsername/modern-cs16.git
cd modern-cs16
cp .env.example .env
# Chỉnh .env với mật khẩu production thực sự!

# 3. Điều chỉnh CS16_PROJECT_DIR trong docker-compose.yml
# CS16_PROJECT_DIR=/opt/modern-cs16

# 4. Build và chạy
docker compose up -d --build
docker compose --profile game up -d

# 5. Reverse proxy (khuyến nghị dùng nginx hoặc Caddy)
# API: https://api.yourserver.vn → localhost:7777
# Dashboard: https://admin.yourserver.vn → localhost:7001
```

---

## 🤝 Đóng Góp

Pull requests luôn được chào đón! Để đóng góp:

1. Fork repository này
2. Tạo branch: `git checkout -b feature/tinh-nang-moi`
3. Commit: `git commit -m 'feat: thêm tính năng X'`
4. Push: `git push origin feature/tinh-nang-moi`
5. Mở Pull Request

---

## 💖 Ủng Hộ Dự Án

Dự án này được phát triển hoàn toàn miễn phí, dành tặng cho cộng đồng CS 1.6 Việt Nam.
Nếu bạn thấy hữu ích, hãy ủng hộ tác giả để duy trì và phát triển thêm:

### 🇻🇳 Chuyển khoản ngân hàng (VietQR)
> Sử dụng tab **Donate** trong Launcher để tạo mã QR tự động

### 💎 Crypto

| Network | Address |
|---------|---------|
| ₿ **Bitcoin (BTC)** | `bc1q4uk59kemaxkp4ny80p9cwdrdezwkametvp8km6` |
| Ξ **Ethereum (ETH)** | `0xE6cE0E0882573b75D9F96995d9F28130f08caD4a` |
| ◎ **Solana (SOL)** | `GhJGifxjkFTB6nBwZoZucnpy4mD3d9LVffubMojrEyGy` |

> Mọi đóng góp dù nhỏ đều có ý nghĩa rất lớn. Cảm ơn bạn! ❤️

---

## 📜 License

MIT License — Tự do sử dụng, chỉnh sửa và phân phối với điều kiện giữ nguyên credit.

---

<div align="center">

**Made with ❤️ for the Vietnamese CS 1.6 Community**

*Counter-Strike 1.6 không bao giờ chết — Nó chỉ được recompile lại thôi!* 😄

</div>
