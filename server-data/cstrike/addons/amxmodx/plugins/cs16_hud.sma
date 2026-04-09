#include <amxmodx>
#include <amxmisc>
#include <hamsandwich>
#include <easy_http>

#define PLUGIN "CS16 HUD & Scoreboard"
#define VERSION "1.0.0-beta"
#define AUTHOR "Antigravity"
#define API_BASE "http://172.20.0.2:7777"

new g_kills[33];
new g_deaths[33];
new Float:g_elo[33];
new g_roundKills[33];
new HudSync:g_hudSync;

public plugin_init() {
    register_plugin(PLUGIN, VERSION, AUTHOR);
    g_hudSync = CreateHudSyncObj();

    RegisterHam(Ham_Killed, "player", "OnPlayerKilled", 0);
    register_event("SendAudio", "OnRoundEnd", "a", "2&%!MRAD_terwin");
    register_event("SendAudio", "OnRoundEnd", "a", "2&%!MRAD_ctwin");
    register_event("HLTV", "OnRoundStart", "a", "1=0", "2=0");
    set_task(3.0, "TaskRefreshHud", 0, "", 0, "b");

    log_amx("[CS16HUD] v%s loaded.", VERSION);
}

public client_putinserver(id) {
    g_kills[id] = 0;
    g_deaths[id] = 0;
    g_elo[id] = 1000.0;
    g_roundKills[id] = 0;

    if (!is_user_bot(id)) {
        new param[1];
        param[0] = id;
        set_task(2.0, "FetchElo", 0, param, sizeof(param));
    }
}

public client_disconnect(id) {
    g_kills[id] = 0;
    g_deaths[id] = 0;
    g_elo[id] = 1000.0;
    g_roundKills[id] = 0;
}

/* ── Fetch ELO async ───────────────────────────────────────────────────── */

public FetchElo(const param[], len) {
    new id = param[0];
    if (id < 1 || id > 32 || !is_user_connected(id)) return;

    new EzHttpOptions:opts = ezhttp_create_options();
    new userData[1];
    userData[0] = id;
    ezhttp_option_set_user_data(opts, userData, sizeof(userData));

    new url[128];
    formatex(url, charsmax(url), "%s/api/players/stats", API_BASE);
    ezhttp_get(url, "OnEloFetched", opts);
}

public OnEloFetched(EzHttpRequest:request_id, const userData[]) {
    new id = userData[0];
    if (!is_user_connected(id)) return;
    if (ezhttp_get_error_code(request_id) != EZH_OK) return;

    new name[35];
    get_user_name(id, name, charsmax(name));

    new response[8192];
    ezhttp_get_data(request_id, response, charsmax(response));

    new nameStr[64];
    formatex(nameStr, charsmax(nameStr), "%c%s%c", 34, name, 34);
    new namePos = contain(response, nameStr);
    if (namePos == -1) return;

    new sub[256];
    new i = 0;
    while (i < 255 && response[namePos + i] && response[namePos + i] != '}') {
        sub[i] = response[namePos + i];
        i++;
    }
    sub[i] = 0;

    new eloPos = contain(sub, "eloScore");
    if (eloPos == -1) return;
    eloPos += 8;
    while (sub[eloPos] && (sub[eloPos] == ':' || sub[eloPos] == ' ')) eloPos++;

    new eloStr[24], j = 0;
    while (sub[eloPos] && ((sub[eloPos] >= '0' && sub[eloPos] <= '9') || sub[eloPos] == '.')) {
        eloStr[j++] = sub[eloPos++];
        if (j >= 23) break;
    }
    eloStr[j] = 0;
    if (j > 0) g_elo[id] = floatstr(eloStr);
}

/* ── Kill hook ─────────────────────────────────────────────────────────── */

public OnPlayerKilled(victim, attacker, shouldgib) {
    if (!is_user_connected(victim)) return HAM_IGNORED;
    if (!attacker || attacker == victim || !is_user_connected(attacker)) return HAM_IGNORED;

    g_kills[attacker]++;
    g_deaths[victim]++;
    g_roundKills[attacker]++;
    g_elo[attacker] += 20.0;
    g_elo[victim] = floatmax(0.0, g_elo[victim] - 15.0);

    ShowHud(attacker);
    ShowHud(victim);
    return HAM_IGNORED;
}

/* ── HUD display ───────────────────────────────────────────────────────── */

ShowHud(id) {
    if (!is_user_connected(id)) return;
    set_hudmessage(50, 255, 100, 0.01, 0.06, 0, 6.0, 3.0, 0.05, 0.05, 1);
    ShowSyncHudMsg(id, g_hudSync, "Kills: %d  |  Deaths: %d  |  ELO: %d",
        g_kills[id], g_deaths[id], floatround(g_elo[id]));
}

public TaskRefreshHud() {
    for (new i = 1; i <= 32; i++)
        if (is_user_connected(i)) ShowHud(i);
}

/* ── Round events ──────────────────────────────────────────────────────── */

public OnRoundStart() {
    for (new i = 1; i <= 32; i++) g_roundKills[i] = 0;
}

public OnRoundEnd() {
    set_task(1.5, "ShowScoreboard");
}

/* ── Round-end MOTD scoreboard ─────────────────────────────────────────── */

public ShowScoreboard() {
    new players[32], count = 0;
    for (new i = 1; i <= 32; i++)
        if (is_user_connected(i)) players[count++] = i;

    // Bubble sort by round kills desc
    for (new i = 0; i < count - 1; i++) {
        for (new j = i + 1; j < count; j++) {
            if (g_roundKills[players[j]] > g_roundKills[players[i]]) {
                new tmp = players[i]; players[i] = players[j]; players[j] = tmp;
            }
        }
    }

    new limit = (count > 8) ? 8 : count;
    new motd[2048], len = 0;

    // Build HTML - one format() per line (Pawn does not concat string literals)
    len += format(motd[len], charsmax(motd)-len, "<html><head><style>");
    len += format(motd[len], charsmax(motd)-len, "body{background:#0a0f14;color:#ddd;font-family:Arial;margin:8px}");
    len += format(motd[len], charsmax(motd)-len, "h3{color:#00e676;text-align:center;margin:4px 0;font-size:13px}");
    len += format(motd[len], charsmax(motd)-len, "table{width:100%%;border-collapse:collapse;font-size:11px}");
    len += format(motd[len], charsmax(motd)-len, "th{background:#122212;color:#00e676;padding:4px}");
    len += format(motd[len], charsmax(motd)-len, "td{padding:3px 5px;text-align:center;border-bottom:1px solid #1a2a1a}");
    len += format(motd[len], charsmax(motd)-len, ".r1 td{color:#FFD700;font-weight:bold}.r2 td{color:#C0C0C0}.r3 td{color:#CD7F32}");
    len += format(motd[len], charsmax(motd)-len, "</style></head><body><h3>KET QUA ROUND</h3>");
    len += format(motd[len], charsmax(motd)-len, "<table><tr><th>#</th><th>Ten</th><th>K-Round</th><th>K-Total</th><th>Deaths</th><th>ELO</th></tr>");

    for (new i = 0; i < limit; i++) {
        new pid = players[i], pname[35];
        get_user_name(pid, pname, charsmax(pname));

        new cls[4];
        if (i == 0) cls = "r1";
        else if (i == 1) cls = "r2";
        else if (i == 2) cls = "r3";
        else cls = "";

        len += format(motd[len], charsmax(motd)-len,
            "<tr class='%s'><td>%d</td><td>%s</td><td>%d</td><td>%d</td><td>%d</td><td>%d</td></tr>",
            cls, i+1, pname, g_roundKills[pid], g_kills[pid], g_deaths[pid], floatround(g_elo[pid]));
    }

    len += format(motd[len], charsmax(motd)-len, "</table></body></html>");

    for (new i = 1; i <= 32; i++)
        if (is_user_connected(i) && !is_user_bot(i))
            show_motd(i, motd, "Ket Qua Round");
}
