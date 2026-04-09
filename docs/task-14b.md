# 📋 Tasks cho Model 14B — Modern CS 1.6 Server

> Dự án tại: `/Volumes/PortableSSD/Congviec/modern-cs16/`
> 
> Đây là các task **độc lập**, có thể làm lần lượt, không cần đợi task khác.
> Mỗi task có: mô tả rõ ràng, context đầy đủ, output cụ thể cần tạo.

---

## T1 — Docker Compose + Dockerfile + .env.example

**File cần tạo:**
- `/Volumes/PortableSSD/Congviec/modern-cs16/docker-compose.yml`
- `/Volumes/PortableSSD/Congviec/modern-cs16/server-data/Dockerfile`
- `/Volumes/PortableSSD/Congviec/modern-cs16/.env.example`
- `/Volumes/PortableSSD/Congviec/modern-cs16/nginx-fastdl/nginx.conf`

**Yêu cầu docker-compose.yml:**
- Services: `cs-server`, `api`, `dashboard`, `postgres-db`, `nginx-fastdl`
- `cs-server`: `network_mode: host` (cần thiết cho UDP game traffic), dùng Dockerfile tự build
- `api`: image build từ `src/Api/`, port `7000:7000`
- `dashboard`: image build từ `src/Dashboard/`, port `7001:7001`
- `postgres-db`: image `postgres:15`, volume persist data, mount `database/init-db.sql`
- `nginx-fastdl`: image `nginx:alpine`, port `8080:80`, serve `server-data/cstrike/` tĩnh
- Tất cả services dùng biến môi trường từ `.env`

**Yêu cầu server-data/Dockerfile:**
- Base: `debian:bookworm-slim`
- Cài `steamcmd` → dùng steamcmd để tải Half-Life dedicated server (app ID 90)
- Copy ReHLDS binaries từ context (sẽ được build system unzip sẵn)
- Expose port `27015/udp` và `27015/tcp`
- CMD: chạy `./hlds_run -game cstrike -port 27015 +maxplayers 32 +map de_dust2`

**Yêu cầu .env.example:**
```
POSTGRES_PASSWORD=changeme
POSTGRES_DB=cs16_modern
POSTGRES_USER=cs16admin
DB_CONNECTION=Host=postgres-db;Database=cs16_modern;Username=cs16admin;Password=changeme
JWT_SECRET=change_this_to_a_very_long_random_secret_key
RCON_PASSWORD=rcon_changeme
SERVER_NAME=Modern CS 1.6 Vietnam
START_MAP=de_dust2
MAX_PLAYERS=32
API_URL=http://localhost:7000
```

**nginx-fastdl/nginx.conf:** serve `/data/` dưới path `/fastdl/` với content-type `application/octet-stream`.

---

## T2 — PostgreSQL Schema (init-db.sql)

**File cần tạo:** `/Volumes/PortableSSD/Congviec/modern-cs16/database/init-db.sql`

**Yêu cầu:** Tạo các bảng sau với đầy đủ constraints, indexes, và foreign keys:

```sql
-- ASP.NET Core Identity tự tạo bảng AspNetUsers/Roles/etc qua EF migration
-- File này chỉ tạo bảng game logic:

-- 1. player_stats: stats game của từng player (liên kết với AspNetUsers.Id)
--    Columns: player_id (UUID PK, FK → AspNetUsers.Id), kills, deaths, headshots, wins,
--             elo_score (FLOAT DEFAULT 1000), credits (INT DEFAULT 0), updated_at

-- 2. kill_logs: log từng pha kill
--    Columns: id (BIGSERIAL PK), attacker_id (UUID), victim_id (UUID),
--             weapon (VARCHAR 32), headshot (BOOL), map_name (VARCHAR 64),
--             created_at (TIMESTAMP DEFAULT NOW())

-- 3. tournaments: giải đấu
--    Columns: id (UUID PK DEFAULT gen_random_uuid()), title (TEXT),
--             description (TEXT), entry_fee (DECIMAL 18,2 DEFAULT 0),
--             prize_pool (DECIMAL 18,2 DEFAULT 0),
--             status (VARCHAR 20 DEFAULT 'Open'), -- Open/Ongoing/Finished
--             max_players (INT DEFAULT 16), created_at

-- 4. tournament_registrations: đăng ký giải
--    Columns: tournament_id (UUID FK), player_id (UUID FK),
--             registered_at (TIMESTAMP DEFAULT NOW()), PRIMARY KEY(tournament_id, player_id)

-- 5. donations: lịch sử donate
--    Columns: id (SERIAL PK), player_id (UUID FK), amount (DECIMAL 18,2),
--             message (TEXT), vietqr_ref (TEXT UNIQUE), status (VARCHAR 20 DEFAULT 'Pending'),
--             created_at

-- 6. kyc_submissions: xác minh danh tính
--    Columns: id (UUID PK DEFAULT gen_random_uuid()), player_id (UUID UNIQUE FK),
--             cccd_image_path (TEXT), selfie_video_path (TEXT),
--             status (VARCHAR 20 DEFAULT 'Pending'), -- Pending/Approved/Rejected
--             reviewer_note (TEXT), reviewed_at (TIMESTAMP), submitted_at

-- Thêm indexes: player_stats.elo_score DESC, kill_logs.created_at, donations.created_at
-- Thêm seed data: 1 admin user note (comment SQL)
```

---

## T3 — AMX Plugin: auth_lock.sma

**File cần tạo:** `/Volumes/PortableSSD/Congviec/modern-cs16/server-data/cstrike/addons/amxmodx/plugins/auth_lock.sma`

**Yêu cầu:**
- Khi player kết nối (`client_putinserver`), gửi HTTP GET đến `http://API_URL/api/auth/verify?steam_id=STEAM_ID&token=TOKEN`
- Token được truyền qua `setinfo` của player (ví dụ: `setinfo "_token" "JWT_HERE"`)
- Nếu API trả về khác 200 hoặc không có response trong 5s → kick player với message: `"Vui long dang nhap qua Launcher de tham gia server!"`
- Dùng `curl_easy_*` module của AMXX để gọi HTTP
- Có CVar: `auth_lock_enabled 1` để bật/tắt
- Có CVar: `auth_api_url "http://localhost:7000"` để config URL

---

## T4 — Dashboard: Trang Rankings + Donations

**Thư mục:** `/Volumes/PortableSSD/Congviec/modern-cs16/src/Dashboard/Pages/`

**Context:** Dashboard là ASP.NET Core 8 Razor Pages project. Đã có base layout với Bootstrap 5 (sẽ được Antigravity tạo). Bạn chỉ cần tạo 2 file Razor Page:

**File cần tạo:**
- `Rankings.cshtml` + `Rankings.cshtml.cs`
- `Donations.cshtml` + `Donations.cshtml.cs`

**Rankings.cshtml yêu cầu:**
- Table Bootstrap 5 hiển thị Top 50 players: Rank#, Name, ELO Score, Kills, Deaths, K/D ratio, Wins
- Auto-refresh mỗi 30s dùng `<meta http-equiv="refresh" content="30">`
- Highlight top 3 với màu vàng/bạc/đồng
- PageModel gọi `GET /api/leaderboard` từ API (dùng `HttpClient` inject)

**Donations.cshtml yêu cầu:**
- Table hiển thị danh sách donations: Player Name, Amount (VND), Message, Thời gian, Status
- Filter dropdown theo Status (All/Pending/Confirmed)
- Tổng tiền donate hiển thị ở đầu trang
- PageModel gọi `GET /api/donations` từ API

---

## T5 — Dashboard: Trang Server Console (RCON)

**File cần tạo:**
- `/Volumes/PortableSSD/Congviec/modern-cs16/src/Dashboard/Pages/Server.cshtml`
- `/Volumes/PortableSSD/Congviec/modern-cs16/src/Dashboard/Pages/Server.cshtml.cs`

**Yêu cầu:**
- Form gõ RCON command (text input + nút Send)
- Output area hiển thị response (styled như terminal: đen nền, xanh lá chữ, font monospace)
- Danh sách quick-commands: `status`, `mp_restartgame 1`, `changelevel de_dust2`, `sv_cheats 0`, `kick #ID`
- PageModel gửi `POST /api/rcon/command` với body `{ "command": "..." }` và hiển thị response
- Hiển thị trạng thái server: Map hiện tại, số players, uptime (lấy từ `GET /api/server/status`)

---

## T6 — Docs: setup.md

**File cần tạo:** `/Volumes/PortableSSD/Congviec/modern-cs16/docs/setup.md`

**Nội dung cần có (tiếng Việt):**
1. **Yêu cầu hệ thống:** Docker Desktop, .NET 8 SDK, macOS/Windows/Linux
2. **Cài đặt lần đầu:** clone, copy .env, chỉnh config
3. **Khởi động:** `docker compose up -d`, verify ports
4. **Thêm admin account:** curl command tạo admin user
5. **Cài plugins:** hướng dẫn compile .sma → .amxx và copy vào addons/
6. **Troubleshooting:** các lỗi thường gặp (Docker port conflict, PostgreSQL connection refused, plugin không load)
7. **Cập nhật:** `git pull && docker compose up -d --build`
