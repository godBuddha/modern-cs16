> ⚠️ **BẤT BIẾN:** Không được sửa, xóa hoặc thay đổi logic liên quan đến kết nối server-client game, auth flow, A2S_INFO query, AMX plugin bridge. Chỉ được THÊM MỚI.

# Modern CS16 — Feature Expansion Plan v1.1.0

> **Người test Launcher:** User tự test trên máy Windows sau khi build.
> **Ngày tạo:** 2026-04-06

---

## Phân tích hiện trạng

- **Bug đã xác nhận (Item 8):** Dashboard `/Server` gọi `GET /api/rcon/command` → endpoint không tồn tại → "Failed to fetch"
- **Tournament table:** Thiếu trường `Format`, `RoundSystem`, `MapList`, `OrganizerPlayerId`
- **Không có:** `TournamentMatches`, `PlayerRooms`, `Feedback` table
- **Donations:** Thiếu trường `DonationType`, `TournamentName`, `PaymentProofPath`

---

## Phase 1: Database Migration

### Modify bảng `Tournaments`
```sql
ALTER TABLE "Tournaments" ADD COLUMN "Format"          text NOT NULL DEFAULT '5vs5';
ALTER TABLE "Tournaments" ADD COLUMN "RoundSystem"     text NOT NULL DEFAULT '5round';
ALTER TABLE "Tournaments" ADD COLUMN "MapList"         text NOT NULL DEFAULT '[]';
ALTER TABLE "Tournaments" ADD COLUMN "OrganizerName"   text NOT NULL DEFAULT '';
ALTER TABLE "Tournaments" ADD COLUMN "OrganizerId"     text NOT NULL DEFAULT '';
ALTER TABLE "Tournaments" ADD COLUMN "PlayersPerMap"   integer NOT NULL DEFAULT 5;
```

### Bảng mới: `TournamentMatches`
| Cột | Kiểu | Ghi chú |
|-----|------|---------|
| Id | uuid | PK |
| TournamentId | uuid | FK → Tournaments |
| RoomCode | text | CS16-TOUR-{id}-{n} |
| ContainerId | text | Docker container name |
| Port | int | 27100-27199 |
| MapName | text | |
| TeamA / TeamB | text | Tên team |
| ScoreA / ScoreB | int | Số round thắng |
| Status | text | Pending/Active/Finished |
| CreatedAt | timestamptz | |

### Bảng mới: `PlayerRooms`
| Cột | Kiểu | Ghi chú |
|-----|------|---------|
| Id | uuid | PK |
| HostPlayerId | text | |
| RoomName | text | |
| MapName | text | |
| Format | text | 1vs1/3vs3/5vs5/10vs10 |
| MaxPlayers | int | |
| Port | int | 27020-27099 |
| ContainerId | text | Docker container name |
| Status | text | Active/Closed |
| CreatedAt | timestamptz | |

### Modify bảng `Donations`
```sql
ALTER TABLE "Donations" ADD COLUMN "DonationType"     text NOT NULL DEFAULT 'developer';
ALTER TABLE "Donations" ADD COLUMN "TournamentName"   text;
ALTER TABLE "Donations" ADD COLUMN "PaymentProofPath" text;
```

### Bảng mới: `Feedbacks`
| Cột | Kiểu | Ghi chú |
|-----|------|---------|
| Id | uuid | PK |
| PlayerId | text | nullable |
| PlayerName | text | |
| Type | text | bug/suggestion |
| Content | text | |
| CreatedAt | timestamptz | |

---

## Phase 2: API Backend

### [FIX] RCON Endpoint
- Thêm `POST /api/rcon/command` vào `ServersController.cs`
- Execute lệnh qua `docker exec cs16-{name} ...`

### [NEW] Tournament Match API
```
GET  /api/tournaments/{id}/matches
POST /api/tournaments/{id}/matches       ← tạo trận + spawn Docker container
PATCH /api/tournament-matches/{id}/score ← cập nhật tỉ số
GET  /api/tournament-matches/{id}/export ← xuất Excel
GET  /api/tournaments/{id}/export        ← xuất Excel toàn giải
```

### [NEW] Player Rooms API
```
GET    /api/rooms            ← danh sách phòng Active
POST   /api/rooms            ← tạo phòng (spawn Docker)
DELETE /api/rooms/{id}       ← giải tán phòng + kill container
POST   /api/rooms/{id}/kick  ← kick player
POST   /api/rooms/{id}/message ← gửi thông báo
```

### [MOD] KYC API
- Thêm `GET /api/kyc/my-status` — trả về status KYC của user hiện tại (by JWT)

### [MOD] Donations API
- Thêm `POST /api/donations/upload-proof` (multipart upload)
- Include `DonationType`, `TournamentName` trong DTO

### [NEW] Feedback API
```
POST /api/feedback
GET  /api/feedback  (admin)
```

---

## Phase 3: Launcher UI — Các tab mới

### Tab XÁC MINH KYC (sửa) [Item 1]
- Load tab → gọi `GET /api/kyc/my-status`
- Nếu `Approved` → hiển thị ✅ "Tài khoản đã được xác minh KYC"
- Nếu chưa → hiện form upload như cũ

### Tab TẠO GIẢI ĐẤU [Item 2] — chỉ hiện với KYC approved
- Form: Tên giải, Tên người tổ chức, Thể thức (1vs1/3vs3/5vs5/10vs10)
- Quy ước: `5 round (thắng 3/5)` hoặc `10 round (thắng 8/10)`
- Số người/map, danh sách map checkboxes
- POST /api/tournaments

### Tab GIẢI ĐẤU [Item 3]
- Danh sách giải đấu đang mở
- Click → xem trận đấu: tên map, tỉ số `A:B`, thanh lợi thế xanh/đỏ
- Nút Xuất Excel từng trận / toàn giải
- **Giải thích tỉ số:** `ScoreA : ScoreB` = số round thắng của mỗi team

### Tab GIỚI THIỆU [Item 4]
- Tên: Modern CS 1.6 VN
- Version: v1.0.0-beta
- Thống kê hệ thống (servers, players, kills)

### Tab HƯỚNG DẪN & GÓP Ý [Item 5]
- Danh sách lệnh AMX Mod X hỗ trợ người chơi
- Form góp ý: loại (Bug/Gợi ý) + nội dung → POST /api/feedback

### Tab TẠO PHÒNG [Item 6]
- Chọn map, thể thức (1vs1/3vs3/5vs5/10vs10), số người
- POST /api/rooms → spawn container port 27020+

### Tab DONATE [Item 10]
- Section 1: Donate cho Nhà phát triển
- Section 2: Donate cho Giải đấu (chọn từ dropdown)
- Cả 2: upload ảnh bằng chứng thanh toán
- POST /api/donations

---

## Phase 4: Dashboard Improvements

### Server.cshtml [Item 8 + 9]
- **Fix:** RCON fetch → đúng endpoint
- **Thêm:** Section quản lý Player Rooms (kick, message)
- **Thêm:** Section quản lý Tournament Rooms (kick, message)

### Donations.cshtml [Item 10]
- Thêm cột "Loại Donate": "Nhà phát triển" hoặc "Giải đấu — {tên}"

---

## Phase 5: Build & Test

- Build launcher: `npm run build:win`
- **User tự test launcher trên Windows**
- Rebuild API + Dashboard Docker containers
- Smoke test API bằng curl

---

## Docker Port Allocation
| Range | Dùng cho |
|-------|---------|
| 27015-27019 | 6 server hệ thống (hiện tại) |
| 27020-27099 | Player rooms (tối đa 80 phòng) |
| 27100-27199 | Tournament rooms (tối đa 100 trận) |

---

## Thứ tự ưu tiên
| # | Item | Lý do |
|---|------|-------|
| 1 | Fix RCON bug | Đang broken |
| 2 | DB Migration | Mọi feature đều depend |
| 3 | KYC tab check | Đơn giản nhất |
| 4 | API endpoints mới | Backend trước |
| 5 | Launcher tabs mới | Frontend sau |
| 6 | Dashboard enhancements | Admin-facing |
