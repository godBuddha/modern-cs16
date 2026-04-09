# 📖 Hướng Dẫn Cài Đặt — Modern CS 1.6 Vietnam

## 1. Yêu Cầu Hệ Thống

| Thành phần | Phiên bản tối thiểu |
|-----------|-------------------|
| Docker Desktop | 4.x trở lên |
| .NET SDK | 8.0 (để dev local) |
| RAM | 4GB trở lên |
| OS | macOS / Ubuntu / Windows với WSL2 |
| Ổ đĩa | 20GB trống (cho game server + DB) |

---

## 2. Cài Đặt Lần Đầu

```bash
# 1. Clone dự án
git clone <repo-url> modern-cs16
cd modern-cs16

# 2. Copy và chỉnh sửa file env
cp .env.example .env
nano .env   # hoặc dùng editor yêu thích
```

**Các biến cần thay đổi trong `.env`:**

```env
POSTGRES_PASSWORD=mat_khau_manh_cua_ban    # Đổi bắt buộc!
JWT_SECRET=chuoi_bi_mat_rat_dai_va_ngau_nhien  # Đổi bắt buộc!
RCON_PASSWORD=rcon_rieng_cua_ban
SERVER_NAME=Ten Server CS 1.6 Cua Ban
```

---

## 3. Khởi Động Server

```bash
# Khởi động tất cả services
docker compose up -d

# Kiểm tra trạng thái
docker compose ps

# Xem logs
docker compose logs -f api
```

**Verify ports hoạt động:**

| Service | URL/Port | Mô tả |
|---------|---------|-------|
| API | http://localhost:7000/health | Backend REST API |
| Dashboard | http://localhost:7001 | Trang quản trị |
| Game Server | UDP 27015 | CS 1.6 game port |
| FastDL | http://localhost:8080/fastdl/ | Tải map cho client |
| PostgreSQL | localhost:5432 | Database (internal) |

---

## 4. Thêm Tài Khoản Admin

```bash
# Tạo admin user đầu tiên qua API
curl -X POST http://localhost:7000/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "username": "admin",
    "password": "MatKhauAdmin123!",
    "email": "admin@example.com",
    "displayName": "Admin"
  }'
```

---

## 5. Cài Plugins AMX Mod X

```bash
# Bước 1: Compile plugin (cần cài amxmodx SDK)
amxxpc server-data/cstrike/addons/amxmodx/plugins/auth_lock.sma \
  -o server-data/cstrike/addons/amxmodx/plugins/auth_lock.amxx

# Bước 2: Thêm vào plugins.ini
echo "auth_lock.amxx" >> server-data/cstrike/addons/amxmodx/configs/plugins.ini

# Bước 3: Restart game server
docker compose restart cs-server
```

---

## 6. Thêm Map Vào FastDL

```bash
# Copy map .bsp vào thư mục cstrike/maps/
cp de_mymap.bsp server-data/cstrike/maps/

# FastDL tự động serve - client CS 1.6 tải qua:
# http://your-server-ip:8080/fastdl/maps/de_mymap.bsp
```

---

## 7. Troubleshooting

### ❌ Docker port bị conflict
```bash
# Kiểm tra port nào đang dùng
lsof -i :7000
lsof -i :27015

# Kill process hoặc dừng service khác
sudo kill -9 <PID>
```

### ❌ PostgreSQL connection refused
```bash
# Kiểm tra container postgres đang chạy
docker compose ps postgres-db

# Xem logs postgres
docker compose logs postgres-db

# Thử kết nối thủ công
docker compose exec postgres-db psql -U cs16admin -d cs16_modern
```

### ❌ Plugin auth_lock không load
```bash
# Xem AMXX log trong game server
docker compose exec cs-server cat logs/error*.log

# Kiểm tra plugin đã được compile chưa
docker compose exec cs-server ls cstrike/addons/amxmodx/plugins/

# Chắc chắn plugins.ini có dòng auth_lock.amxx
docker compose exec cs-server cat cstrike/addons/amxmodx/configs/plugins.ini
```

### ❌ API không trả về JWT khi đăng nhập
- Kiểm tra `JWT_SECRET` trong `.env` đủ dài (>= 32 ký tự)
- Restart API: `docker compose restart api`

### ❌ Dashboard lỗi "connection refused"
- Dashboard cần API chạy trước: `docker compose up api -d`
- Kiểm tra biến `API_URL=http://api:7000` trong `.env`

---

## 8. Cập Nhật

```bash
# Pull code mới nhất
git pull

# Rebuild và restart
docker compose up -d --build

# Migrations DB sẽ tự chạy khi API khởi động
```

---

## 9. Backup Database

```bash
# Backup
docker compose exec postgres-db pg_dump -U cs16admin cs16_modern > backup_$(date +%Y%m%d).sql

# Restore
cat backup_20260401.sql | docker compose exec -T postgres-db psql -U cs16admin -d cs16_modern
```
