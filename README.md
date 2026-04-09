<div align="center">

# 🎮 Modern CS 1.6 — Full Server Ecosystem

**Tiếng Việt** | **English**

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Docker](https://img.shields.io/badge/Docker-Compose-2496ED?logo=docker)](https://docker.com)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-15-336791?logo=postgresql)](https://postgresql.org)
[![Electron](https://img.shields.io/badge/Electron-Launcher-47848F?logo=electron)](https://electronjs.org)
[![License](https://img.shields.io/badge/License-MIT-22c55e)](LICENSE)
[![Stars](https://img.shields.io/github/stars/godBuddha/modern-cs16?style=social)](https://github.com/godBuddha/modern-cs16/stargazers)

*A complete, self-hosted Counter-Strike 1.6 server ecosystem — built for the global CS community ❤️*

*Hệ sinh thái máy chủ CS 1.6 hoàn chỉnh, tự host — xây dựng cho cộng đồng CS toàn thế giới ❤️*

[🇻🇳 Tiếng Việt](#-giới-thiệu) • [🌍 English](#-introduction) • [⚡ Quick Start](#-quick-start) • [📡 API](#-api-endpoints) • [💖 Donate](#-support-the-project)

</div>

---

## 🇻🇳 Giới Thiệu

**Modern CS 1.6** là một hệ thống hoàn chỉnh để vận hành cộng đồng Counter-Strike 1.6, bao gồm:

- 🖥️ **Docker Game Servers** — Chạy nhiều server CS 1.6 (Non-Steam) đồng thời, auto-spawn theo yêu cầu
- 🔌 **REST API Backend** — ASP.NET Core 8 với JWT, ELO ranking, kill tracking
- 📊 **Admin Dashboard** — Giao diện web quản lý server, player, KYC, tournament, donation
- 🚀 **Electron Launcher** — App Windows cho người chơi: đăng nhập, xem server, tạo phòng, giải đấu, donate
- 🏆 **Tournament System** — Tổ chức giải đấu với Docker auto-spawn, HLTV relay, xuất Excel
- 🔒 **KYC Verification** — Xác minh CCCD trước khi host phòng/giải đấu
- 💸 **VietQR Donate** — Tích hợp QR chuyển khoản, upload bằng chứng, phân loại donate
- 💬 **Spectator Chat** — Chat real-time khi xem trận đấu

---

## 🌍 Introduction

**Modern CS 1.6** is a complete self-hosted ecosystem for running a Counter-Strike 1.6 community server, featuring:

- 🖥️ **Docker Game Servers** — Run multiple CS 1.6 servers (Non-Steam support) simultaneously with automatic container provisioning
- 🔌 **REST API Backend** — ASP.NET Core 8 with JWT authentication, ELO ranking, real-time kill tracking via AMX plugin
- 📊 **Admin Dashboard** — Web-based control panel for managing servers, players, KYC reviews, tournaments, and donations
- 🚀 **Electron Launcher** — Windows desktop app for players: login, server browser, room hosting, tournament registration, donations
- 🏆 **Tournament System** — Full tournament management with automatic Docker container spawning, HLTV relay for spectators, Excel export
- 🔒 **KYC Verification** — Identity verification (ID card + selfie) required before hosting rooms or tournaments
- 💸 **Donation System** — VietQR bank transfer integration, payment proof upload, per-category tracking
- 💬 **Spectator Chat** — Real-time chat overlay for match spectators

---

## ✨ Key Features / Tính Năng Nổi Bật

### 🎯 Game Servers
| Feature | Vietnamese | English |
|---------|-----------|---------|
| Multi-server | Nhiều server đồng thời | Run Italy, Dust2, Inferno, Nuke simultaneously |
| Player Rooms | Tự tạo phòng riêng | Players spawn their own Docker containers |
| A2S_INFO | Trạng thái server real-time | Real-time player count, map, ping |
| FastDL | Nginx serve maps/skins | Automatic map download for clients |
| RCON UDP | Gửi lệnh admin | GoldSrc challenge/response RCON protocol |
| Non-Steam | Hỗ trợ Non-Steam | ReUnion/dproto v0.2.0 compatibility |

### 🏆 Tournament System
| Feature | Details |
|---------|---------|
| Formats | 1vs1, 3vs3, 5vs5, 10vs10 |
| Round Rules | Best of 5 rounds or Best of 10 rounds |
| Map Pool | de_dust2, cs_italy, de_inferno, de_nuke + custom |
| Auto Docker | 1 container per match (port 27200–27299) |
| HLTV Relay | Spectators connect on separate port (no lag impact) |
| Excel Export | Match results & full tournament export (.xlsx) |
| ELO System | Dynamic rating with stage multipliers (Group/Knockout/Final) |
| Chat | Real-time spectator chat with RCON admin broadcast |

### 🔐 Security / Bảo Mật
- JWT token authentication on all API endpoints
- KYC (ID card + selfie video upload) before room/tournament creation
- Docker container guard — system servers **cannot** be accidentally deleted
- Container prefix isolation: `cs16-room-*`, `cs16-tour-*`, `cs16-managed-*`

---

## 🗂 Project Structure / Cấu Trúc Dự Án

```
modern-cs16/
├── 📄 docker-compose.yml          # Full system orchestration
├── 📄 .env.example                # Environment variables template
├── 📄 PLAN.md                     # Development roadmap
│
├── 🎮 server-data/                # CS 1.6 Game Engine (ReHLDS)
│   ├── Dockerfile
│   └── cstrike/addons/amxmodx/
│       ├── plugins/
│       │   ├── cs16_bridge.sma    # Kill event reporter → API
│       │   └── auth_lock.sma      # Block unregistered players
│       └── configs/
│
├── ⚙️ src/
│   ├── Api/                       # ASP.NET Core 8 REST API (port 7777)
│   │   ├── Controllers/
│   │   │   ├── AuthController.cs              # Register/Login/JWT
│   │   │   ├── ServersController.cs           # Game server management
│   │   │   ├── RconController.cs              # RCON UDP (GoldSrc protocol)
│   │   │   ├── PlayerRoomsController.cs       # Rooms + Docker spawn
│   │   │   ├── TournamentsController.cs       # Tournament management
│   │   │   ├── TournamentMatchesController.cs # Matches + HLTV relay
│   │   │   ├── DonationsController.cs         # VietQR payments
│   │   │   ├── FeedbackController.cs          # Player feedback
│   │   │   ├── KycController.cs               # Identity verification
│   │   │   └── PlayersController.cs           # Rankings + stats
│   │   ├── Models/Models.cs                   # EF Core entities
│   │   └── Services/
│   │       └── DockerGameServerService.cs     # Docker socket management
│   │
│   ├── Dashboard/                 # ASP.NET Core 8 Razor Pages (port 7001)
│   │   └── Pages/
│   │       ├── Server.cshtml          # RCON console + room management
│   │       ├── Servers.cshtml         # Server list & status
│   │       ├── Tournaments.cshtml     # Tournament admin
│   │       ├── Players.cshtml         # Player management & ban
│   │       ├── Kyc.cshtml             # KYC approval queue
│   │       ├── Donations.cshtml       # Donation tracking
│   │       └── Rankings.cshtml        # ELO leaderboard
│   │
│   └── ElectronLauncher/          # Windows Electron App
│       └── renderer/
│           ├── index.html             # Full UI (all tabs)
│           ├── app.js                 # All feature logic (~1000 lines)
│           └── style.css              # Dark theme design
│
├── 🗄️ database/
│   └── init-db.sql                # PostgreSQL initial schema
│
└── 🌐 nginx-fastdl/
    └── nginx.conf                 # FastDL map serving
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
| Dashboard UI | Razor Pages + Bootstrap 5 | 8.0 |
| Launcher | Electron + Vanilla JS | Latest |
| Containerization | Docker + Docker Compose | v2 |
| File Server | Nginx (FastDL) | Alpine |
| RCON Protocol | GoldSrc UDP challenge/response | Custom |
| Excel Export | ClosedXML | Latest |

---

## ⚡ Quick Start

### Requirements / Yêu Cầu
- 🐳 Docker Desktop 4.x+
- 💾 RAM ≥ 4GB, Disk ≥ 20GB
- 🖥️ macOS / Ubuntu / Windows (WSL2)

### Installation / Cài Đặt

```bash
# 1. Clone the repo / Clone dự án
git clone https://github.com/godBuddha/modern-cs16.git
cd modern-cs16

# 2. Configure environment / Cấu hình môi trường
cp .env.example .env
nano .env   # Change passwords! / Đổi mật khẩu!

# 3. Start core services / Khởi động services chính
docker compose up -d

# 4. Start game servers / Khởi động game servers
docker compose --profile game up -d

# 5. Verify / Kiểm tra
curl http://localhost:7777/health     # API ✅
open http://localhost:7001            # Dashboard ✅
```

> **Important / Quan trọng**: Always change `POSTGRES_PASSWORD`, `JWT_SECRET`, and `RCON_PASSWORD` in your `.env` file before production deployment!

### Ports

| Service | Port | Protocol |
|---------|------|----------|
| REST API | 7777 | HTTP |
| Admin Dashboard | 7001 | HTTP |
| Game Server Italy #1 | 27015 | UDP |
| Game Server Italy #2 | 27016 | UDP |
| Game Server Italy #3 | 27017 | UDP |
| Game Server Dust2 | 27018 | UDP |
| Game Server Inferno | 27019 | UDP |
| Game Server Nuke | 27020 | UDP |
| Player Rooms (dynamic) | 27021–27099 | UDP |
| Tournament Matches (dynamic) | 27200–27299 | UDP |
| FastDL (Nginx) | 8080 | HTTP |
| PostgreSQL | 5432 | TCP (internal) |

### Create First Admin Account / Tạo Tài Khoản Admin

```bash
curl -X POST http://localhost:7777/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "username": "admin",
    "password": "YourSecurePass123!",
    "email": "admin@yourserver.com",
    "displayName": "Admin"
  }'
```

---

## 📡 API Endpoints

### Authentication
```
POST /api/auth/register     — Register account
POST /api/auth/login        — Login → JWT token
POST /api/auth/forgot       — Password reset
```

### Player Rooms
```
GET    /api/rooms                    — List active rooms
POST   /api/rooms                    — Create room (KYC required) → Docker spawn
DELETE /api/rooms/{id}?playerId=xxx  — Delete room (owner only)
POST   /api/rooms/{id}/kick          — Kick player via RCON
POST   /api/rooms/{id}/message       — Admin broadcast
```

### Tournaments
```
GET    /api/tournaments                                — List tournaments
POST   /api/tournaments                               — Create tournament (KYC required)
GET    /api/tournaments/{id}/matches                  — List matches
POST   /api/tournaments/{id}/matches                 — Create match
POST   /api/tournament-matches/{id}/start-room       — Spawn container + HLTV
PATCH  /api/tournament-matches/{id}/score            — Update score
GET    /api/tournament-matches/{id}/export           — Export match to Excel
GET    /api/tournaments/{id}/export                  — Export full tournament to Excel
POST   /api/tournament-matches/{id}/broadcast        — RCON broadcast to match
POST   /api/tournament-matches/{id}/spec-chat        — Post spectator chat
GET    /api/tournament-matches/{id}/spec-chat        — Get spectator chat history
```

### Donations
```
GET   /api/donations                      — List donations (admin)
POST  /api/donations                      — Create donation + generate VietQR
POST  /api/donations/{id}/upload-proof    — Upload payment proof
PATCH /api/donations/{id}/confirm         — Confirm donation (admin)
```

### Others
```
POST /api/rcon/command       — Send RCON command to server
POST /api/feedback           — Submit player feedback
GET  /api/kyc/my-status      — Get current user KYC status
GET  /api/rankings           — Get ELO leaderboard
GET  /health                 — Health check
```

---

## 🖥️ Launcher Tabs / Các Tab Launcher

| Tab | Vietnamese | English |
|-----|-----------|---------|
| 🏠 Home | Thống kê hệ thống | System stats & news |
| 🎮 Servers | Danh sách server, join game | Server browser with A2S_INFO |
| 🏆 Rankings | Bảng xếp hạng ELO | ELO leaderboard |
| 🔒 KYC | Upload CCCD + selfie | Identity verification |
| 🏅 Tournaments | Xem/đăng ký giải | View/register tournaments |
| ➕ Create Tournament | Tạo giải đấu | Create new tournament (KYC required) |
| 🚪 Rooms | Tạo/xem phòng | Host or join player rooms |
| 💖 Donate | VietQR donate | Support Dev or Tournaments |
| ❓ Help | Hướng dẫn + góp ý | AMX commands + feedback |
| ℹ️ About | Giới thiệu dự án | Project info & links |

### Build Launcher / Build Launcher

```bash
cd src/ElectronLauncher
npm install
npm run build:win   # Produces .exe for Windows
```

---

## 🐳 Docker Architecture / Kiến Trúc Docker

```
modern-cs16_default  (internal bridge network)
│
├── System Servers [profile: game]
│   ├── cs16-italy-1    :27015/udp
│   ├── cs16-italy-2    :27016/udp
│   ├── cs16-italy-3    :27017/udp
│   ├── cs16-dust2      :27018/udp
│   ├── cs16-inferno    :27019/udp
│   └── cs16-nuke       :27020/udp
│
├── Core Services
│   ├── api             :7777/http   (ASP.NET Core REST API)
│   ├── dashboard       :7001/http   (Razor Pages Admin UI)
│   ├── postgres-db     :5432/tcp    (PostgreSQL 15)
│   └── nginx-fastdl    :8080/http   (FastDL Nginx)
│
└── Dynamic Containers (spawned by API)
    ├── cs16-room-{port}   :27021-27099  (Player rooms)
    ├── cs16-tour-{port}   :27200-27299  (Tournament matches)
    └── cs16-hltv-{port}   :27201-27299  (HLTV spectator relay)
```

> **Safety / An toàn**: `DockerGameServerService` only removes containers with prefix `cs16-room-*`, `cs16-tour-*`, or `cs16-managed-*`. System servers are **never** touched.

---

## 🚀 Production Deployment / Triển Khai Production

```bash
# Ubuntu/Debian server
apt install docker.io docker-compose-plugin -y

git clone https://github.com/godBuddha/modern-cs16.git
cd modern-cs16
cp .env.example .env
# Edit .env with real production credentials

# Update CS16_PROJECT_DIR in docker-compose.yml to your install path

docker compose up -d --build
docker compose --profile game up -d

# Recommended: use Nginx/Caddy as reverse proxy
# API:       https://api.yourserver.com  → localhost:7777
# Dashboard: https://admin.yourserver.com → localhost:7001
```

---

## 🤝 Contributing / Đóng Góp

Pull requests are welcome from the global CS community! / Pull requests chào đón từ cộng đồng CS toàn thế giới!

1. Fork this repository
2. Create branch: `git checkout -b feature/your-feature`
3. Commit: `git commit -m 'feat: add something awesome'`
4. Push: `git push origin feature/your-feature`
5. Open a Pull Request

**Ideas for contributions / Gợi ý đóng góp:**
- 🌐 Add more language translations (UI, README)
- 🗺️ Additional map pool management
- 📊 More tournament bracket formats (elimination, round-robin)
- 🎖️ Achievement system
- 📱 Web-based launcher alternative
- 🔧 Steam authentication support

---

## 💖 Support the Project / Ủng Hộ Dự Án

This project is free and open source, made for the entire Counter-Strike community. If it helped you, please consider a small donation to keep it going!

Dự án này hoàn toàn miễn phí và mã nguồn mở, xây dựng cho toàn bộ cộng đồng Counter-Strike. Nếu hữu ích, hãy ủng hộ để duy trì và phát triển!

### 💎 Crypto Donation / Donate Crypto

| Network | Address |
|---------|---------|
| ₿ **Bitcoin (BTC)** | `bc1q4uk59kemaxkp4ny80p9cwdrdezwkametvp8km6` |
| Ξ **Ethereum (ETH)** | `0xE6cE0E0882573b75D9F96995d9F28130f08caD4a` |
| ◎ **Solana (SOL)** | `GhJGifxjkFTB6nBwZoZucnpy4mD3d9LVffubMojrEyGy` |

### 🇻🇳 Bank Transfer (Vietnam) / Chuyển Khoản (Việt Nam)
> Use the **Donate tab** in the Launcher to generate a VietQR code automatically.
> Dùng tab **Donate** trong Launcher để tạo mã VietQR tự động.

### ⭐ Or simply star this repo!
> A star costs nothing but means everything to open source developers.
> Một ngôi sao không tốn gì nhưng có ý nghĩa rất lớn với developer open source.

---

## 📜 License

**MIT License** — Free to use, modify, and distribute with attribution.

Copyright (c) 2026 Modern CS16 Vietnam

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software.

---

<div align="center">

**Made with ❤️ for the global Counter-Strike 1.6 Community**

**Xây dựng với ❤️ cho cộng đồng Counter-Strike 1.6 toàn thế giới**

*CS 1.6 never dies — it just gets dockerized! 🐳*

*CS 1.6 không bao giờ chết — nó chỉ được containerize lại thôi! 🐳*

[🐙 GitHub](https://github.com/godBuddha/modern-cs16) • [🐛 Issues](https://github.com/godBuddha/modern-cs16/issues) • [💬 Discussions](https://github.com/godBuddha/modern-cs16/discussions)

</div>
