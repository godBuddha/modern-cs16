#include <amxmodx>
#include <amxmisc>
#include <cstrike>
#include <hamsandwich>
#include <easy_http>

#define PLUGIN "Modern CS1.6 API Bridge"
#define VERSION "1.0.0-beta"
#define AUTHOR "Antigravity"

// API endpoint - Docker internal IP (cùng network modern-cs16_default)
#define API_URL "http://172.20.0.2:7777/api/game/event"

public plugin_init() {
    register_plugin(PLUGIN, VERSION, AUTHOR);

    // Ham_Killed: server-side, fires for all kills (human + bot)
    RegisterHam(Ham_Killed, "player", "OnPlayerKilled", 0);

    // Round win events
    register_event("SendAudio", "Event_TerroristWin", "a", "2&%!MRAD_terwin");
    register_event("SendAudio", "Event_CounterTerroristWin", "a", "2&%!MRAD_ctwin");

    log_amx("[CS16Bridge] v%s loaded. API: %s (non-blocking ezhttp)", VERSION, API_URL);
}

public plugin_cfg() {
    log_amx("[CS16Bridge] Ready. Using AmxxEasyHttp for async requests.");
}

// Ham_Killed post-hook: victim, attacker, shouldgib
public OnPlayerKilled(victim, attacker, shouldgib) {
    if (!is_user_connected(victim)) return HAM_IGNORED;
    if (!attacker || attacker == victim || !is_user_connected(attacker)) return HAM_IGNORED;

    new authAttacker[35], authVictim[35], mapName[32];
    get_user_name(attacker, authAttacker, charsmax(authAttacker));
    get_user_name(victim, authVictim, charsmax(authVictim));
    get_mapname(mapName, charsmax(mapName));

    new jsonData[512];
    formatex(jsonData, charsmax(jsonData),
        "{^"attackerId^":^"%s^",^"victimId^":^"%s^",^"kills^":1,^"headshots^":0,^"wins^":0,^"weapon^":^"unknown^",^"mapName^":^"%s^"}",
        authAttacker, authVictim, mapName);

    SendApiRequest(jsonData);
    return HAM_IGNORED;
}

public Event_TerroristWin() {
    ProcessWinTeam(1);
}

public Event_CounterTerroristWin() {
    ProcessWinTeam(2);
}

ProcessWinTeam(teamId) {
    new players[32], playerCount, auth[35], mapName[32];
    get_players(players, playerCount, "ch"); // humans + bots
    get_mapname(mapName, charsmax(mapName));

    for (new i = 0; i < playerCount; i++) {
        new id = players[i];
        if (cs_get_user_team(id) == (teamId == 1 ? CS_TEAM_T : CS_TEAM_CT)) {
            get_user_name(id, auth, charsmax(auth));
            new jsonData[256];
            formatex(jsonData, charsmax(jsonData),
                "{^"attackerId^":^"%s^",^"victimId^":null,^"kills^":0,^"headshots^":0,^"wins^":1,^"weapon^":^"none^",^"mapName^":^"%s^"}",
                auth, mapName);
            SendApiRequest(jsonData);
        }
    }
}

// Non-blocking HTTP POST via AmxxEasyHttp
SendApiRequest(const data[]) {
    new EzHttpOptions:opts = ezhttp_create_options();
    ezhttp_option_set_header(opts, "Content-Type", "application/json");
    ezhttp_option_set_body(opts, data);
    // Fire & forget — callback chỉ để log lỗi
    ezhttp_post(API_URL, "OnApiResponse", opts);
}

// Callback async — không block server
public OnApiResponse(EzHttpRequest:request_id) {
    new EzHttpErrorCode:err = ezhttp_get_error_code(request_id);
    if (err != EZH_OK) {
        new errMsg[64];
        ezhttp_get_error_message(request_id, errMsg, charsmax(errMsg));
        log_amx("[CS16Bridge] ERROR: %s", errMsg);
    }
}
