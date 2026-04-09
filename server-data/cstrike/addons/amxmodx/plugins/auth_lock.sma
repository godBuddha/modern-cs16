/**
 * auth_lock.sma — JWT Authentication Gate cho Modern CS 1.6 VN
 *
 * Flow: client_putinserver → đọc token từ setinfo "_token" → gọi API
 *       Nếu API không trả 200 → kick player
 *
 * CVars:
 *   auth_lock_enabled  1      (1 = bật, 0 = tắt)
 *
 * Compile: amxxpc auth_lock.sma -o auth_lock.amxx
 */

#include <amxmodx>
#include <amxmisc>
#include <sockets>

// ── CVars ────────────────────────────────────────────────────────────────────
new g_cvar_enabled
new const API_HOST[] = "host.docker.internal"
new const API_PORT = 7777

// ── Plugin info ───────────────────────────────────────────────────────────────
public plugin_init()
{
    register_plugin("Auth Lock", "1.0", "Modern CS 1.6 VN")

    g_cvar_enabled  = register_cvar("auth_lock_enabled", "1")

    register_logevent("event_round_start", 2, "1=Round_Start")
}

// ── Client connect ───────────────────────────────────────────────────────────
public client_putinserver(id)
{
    if (!get_pcvar_num(g_cvar_enabled))
        return

    if (is_user_bot(id) || is_user_hltv(id))
        return

    // Lấy Steam ID
    new steam_id[32]
    get_user_authid(id, steam_id, charsmax(steam_id))

    // Lấy JWT token player đặt qua: setinfo "_token" "JWT_HERE"
    new token[256]
    get_user_info(id, "_token", token, charsmax(token))

    if (strlen(token) < 10)
    {
        // Không có token → kick ngay
        set_task(0.5, "task_kick_no_token", id)
        return
    }

    // Check Auth API
    CheckPlayerAuth(id, steam_id, token)
}

CheckPlayerAuth(id, const steam_id[], const token[])
{
    new error;
    new socket = socket_open(API_HOST, API_PORT, SOCKET_TCP, error);
    if (socket <= 0)
    {
        kick_player(id, "Loi ket noi xac thuc. Vui long thu lai!")
        return
    }

    new request[1024]
    formatex(request, charsmax(request), "GET /api/auth/verify?steam_id=%s&token=%s HTTP/1.1^r^nHost: %s:%d^r^nConnection: close^r^n^r^n", steam_id, token, API_HOST, API_PORT)
    
    socket_send(socket, request, strlen(request))
    
    // Read response (Micro-block allowed since API is Localhost < 5ms)
    new response[2048]
    socket_recv(socket, response, charsmax(response))
    socket_close(socket)

    // Kiểm tra HTTP 200 OK
    if (contain(response, "HTTP/1.1 200") == -1)
    {
        kick_player(id, "Vui long dang nhap qua Launcher de tham gia server!")
        return
    }

    // Xác thực thành công — cho phép vào
    new name[32]
    get_user_name(id, name, charsmax(name))
    log_amx("[AuthLock] Player %s (ID: %d) authenticated successfully.", name, id)
}

// ── Kick helpers ──────────────────────────────────────────────────────────────
kick_player(id, const reason[])
{
    new name[32]
    get_user_name(id, name, charsmax(name))
    log_amx("[AuthLock] Kicking player %s — %s", name, reason)
    server_cmd("kick #%d ^"%s^"", get_user_userid(id), reason)
}

public task_kick_no_token(id)
{
    if (is_user_connected(id))
        kick_player(id, "Vui long dang nhap qua Launcher de tham gia server!")
}

// ── Cleanup khi disconnect ────────────────────────────────────────────────────
public client_disconnect(id)
{
    // Cũ dùng huỷ tiến trình CURL async, nay dùng socket sync không cần dọn dẹp biến g_curl.
}

// ── Round start (optional: log active players) ────────────────────────────────
public event_round_start()
{
    if (!get_pcvar_num(g_cvar_enabled))
        return

    new players[32], num
    get_players(players, num, "ch")
    log_amx("[AuthLock] Round start — %d authenticated players in-game.", num)
}
