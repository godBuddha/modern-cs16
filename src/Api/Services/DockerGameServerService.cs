using Docker.DotNet;
using Docker.DotNet.Models;

namespace Api.Services;

/// <summary>
/// Quản lý CS 1.6 Docker containers qua Docker socket.
/// - Admin servers: prefix "cs16-managed-"
/// - Player rooms:  prefix "cs16-room-"
/// - Tournament:    prefix "cs16-tour-"
/// KHÔNG bao giờ đụng containers gốc (cs16-italy-1/2/3, cs16-dust2, cs16-inferno, cs16-nuke)
/// </summary>
public class DockerGameServerService
{
    private readonly DockerClient _docker;
    private readonly IConfiguration _config;
    private readonly ILogger<DockerGameServerService> _log;

    private const string AdminPrefix      = "cs16-managed-";
    private const string RoomPrefix       = "cs16-room-";
    private const string TournamentPrefix = "cs16-tour-";

    public DockerGameServerService(IConfiguration config, ILogger<DockerGameServerService> log)
    {
        _config = config;
        _log    = log;
        _docker = new DockerClientConfiguration(new Uri("unix:///var/run/docker.sock"))
            .CreateClient();
    }

    // ── Admin Server (Dashboard tạo) ─────────────────────────────────────────
    public Task<(string ContainerName, string InternalIp)> StartServerAsync(
        int serverId, string serverName, int port,
        string map, int maxPlayers, string rconPassword, string dbConnection, string jwtSecret)
        => SpawnContainerAsync(
            name:        $"{AdminPrefix}{serverId}-{port}",
            serverName:  serverName,
            port:        port,
            map:         map,
            maxPlayers:  maxPlayers,
            rconPassword: rconPassword,
            dbConnection: dbConnection,
            jwtSecret:   jwtSecret,
            type:        "admin-server");

    // ── Player Room (Launcher tạo) ────────────────────────────────────────────
    public Task<(string ContainerName, string InternalIp)> StartRoomAsync(
        Guid roomId, string roomName, int port,
        string map, int maxPlayers, string rconPassword, string dbConnection, string jwtSecret)
        => SpawnContainerAsync(
            name:        $"{RoomPrefix}{port}",
            serverName:  roomName,
            port:        port,
            map:         map,
            maxPlayers:  maxPlayers,
            rconPassword: rconPassword,
            dbConnection: dbConnection,
            jwtSecret:   jwtSecret,
            type:        "player-room",
            extraLabels: new() { ["cs16vn.room-id"] = roomId.ToString() });

    // ── Tournament Match Room ─────────────────────────────────────────────────
    public Task<(string ContainerName, string InternalIp)> StartMatchAsync(
        Guid matchId, string tournamentTitle, int port,
        string map, int maxPlayers, string rconPassword, string dbConnection, string jwtSecret)
        => SpawnContainerAsync(
            name:        $"{TournamentPrefix}{port}",
            serverName:  $"[GD] {tournamentTitle} - Match",
            port:        port,
            map:         map,
            maxPlayers:  maxPlayers,
            rconPassword: rconPassword,
            dbConnection: dbConnection,
            jwtSecret:   jwtSecret,
            type:        "tournament-match",
            extraLabels: new() { ["cs16vn.match-id"] = matchId.ToString() });

    // ── HLTV Relay Container ──────────────────────────────────────────────────
    /// <summary>
    /// Spawn một container HLTV riêng, dùng cùng image nhưng override CMD để chạy
    /// /root/hlds/hltv thay vì hlds_run (game server).
    /// Spectators kết nối vào hltvPort (matchPort+1) — KHÔNG phải matchPort.
    /// Players vẫn connect matchPort như bình thường → không ảnh hưởng game logic.
    /// </summary>
    public async Task<string> StartHltvAsync(
        Guid matchId, string roomCode, string gameServerIp, int matchPort, int hltvPort)
    {
        var image   = _config["CS16_IMAGE"]   ?? "cs16vn/gameserver:latest";
        var network = _config["CS16_NETWORK"] ?? "modern-cs16_default";
        var name    = $"cs16-hltv-{hltvPort}";

        // Dọn container cũ nếu trùng tên
        try
        {
            await _docker.Containers.StopContainerAsync(name,
                new ContainerStopParameters { WaitBeforeKillSeconds = 3 });
            await _docker.Containers.RemoveContainerAsync(name,
                new ContainerRemoveParameters { Force = true });
        }
        catch { }

        // Command: sleep 12s chờ game server khởi động, rồi start HLTV
        var hltvCmd = $"sleep 12 && /root/hlds/hltv " +
                      $"+connect {gameServerIp}:{matchPort} " +
                      $"+port {hltvPort} " +
                      $"+delay 30 " +
                      $"+name \"[GD] HLTV {roomCode}\" " +
                      $"> /tmp/hltv.log 2>&1";

        var createResp = await _docker.Containers.CreateContainerAsync(new CreateContainerParameters
        {
            Name  = name,
            Image = image,
            // Override CMD: chạy hltv thay vì /start.sh
            Cmd   = new[] { "/bin/sh", "-c", hltvCmd },
            Labels = new Dictionary<string, string>
            {
                ["com.docker.compose.project"] = "modern-cs16",
                ["com.docker.compose.service"] = name,
                ["cs16vn.managed"]             = "true",
                ["cs16vn.type"]                = "hltv-relay",
                ["cs16vn.match-id"]            = matchId.ToString(),
                ["cs16vn.relay-for"]           = $"{gameServerIp}:{matchPort}",
            },
            ExposedPorts = new Dictionary<string, EmptyStruct>
            {
                [$"{hltvPort}/udp"] = default,
                [$"{hltvPort}/tcp"] = default,
            },
            HostConfig = new HostConfig
            {
                PortBindings = new Dictionary<string, IList<PortBinding>>
                {
                    [$"{hltvPort}/udp"] = new List<PortBinding> { new() { HostPort = hltvPort.ToString() } },
                },
                RestartPolicy = new RestartPolicy { Name = RestartPolicyKind.UnlessStopped },
            },
        });

        await _docker.Networks.ConnectNetworkAsync(network,
            new NetworkConnectParameters { Container = createResp.ID });

        await _docker.Containers.StartContainerAsync(createResp.ID, new ContainerStartParameters());

        _log.LogInformation("[HLTV] Started relay container={Name} hltvPort={HltvPort} → game={GameIp}:{GamePort}",
            name, hltvPort, gameServerIp, matchPort);

        return name;
    }

    // ── Core spawn logic ──────────────────────────────────────────────────────
    private async Task<(string ContainerName, string InternalIp)> SpawnContainerAsync(
        string name, string serverName, int port, string map, int maxPlayers,
        string rconPassword, string dbConnection, string jwtSecret,
        string type, Dictionary<string, string>? extraLabels = null)
    {
        var image    = _config["CS16_IMAGE"]       ?? "cs16vn/gameserver:latest";
        var network  = _config["CS16_NETWORK"]     ?? "modern-cs16_default";
        var projDir  = _config["CS16_PROJECT_DIR"] ?? "";

        // ── Dọn container cũ nếu trùng tên ──────────────────────────────────
        try
        {
            await _docker.Containers.StopContainerAsync(name,
                new ContainerStopParameters { WaitBeforeKillSeconds = 3 });
            await _docker.Containers.RemoveContainerAsync(name,
                new ContainerRemoveParameters { Force = true });
        }
        catch { /* không tồn tại → OK */ }

        // ── Port binding ─────────────────────────────────────────────────────
        var portStr = port.ToString();
        var portBindings = new Dictionary<string, IList<PortBinding>>
        {
            [$"{portStr}/udp"] = new List<PortBinding> { new() { HostPort = portStr } },
            [$"{portStr}/tcp"] = new List<PortBinding> { new() { HostPort = portStr } },
        };

        // ── Volumes – AMX plugins dùng chung ──────────────────────────────────
        var binds = new List<string>();
        if (!string.IsNullOrEmpty(projDir))
        {
            var sd = $"{projDir}/server-data";
            binds.AddRange(new[]
            {
                $"{sd}/cstrike/addons/amxmodx/configs/plugins.ini:/root/hlds/cstrike/addons/amxmodx/configs/plugins.ini:ro",
                $"{sd}/cstrike/addons/amxmodx/configs/modules.ini:/root/hlds/cstrike/addons/amxmodx/configs/modules.ini:ro",
                $"{sd}/cstrike/addons/amxmodx/plugins/cs16_bridge.amxx:/root/hlds/cstrike/addons/amxmodx/plugins/cs16_bridge.amxx:ro",
                $"{sd}/cstrike/addons/amxmodx/modules/easy_http_amxx_i386.so:/root/hlds/cstrike/addons/amxmodx/modules/easy_http_amxx_i386.so:ro",
            });
        }

        // ── Labels – container thuộc modern-cs16 project ────────────────────
        var labels = new Dictionary<string, string>
        {
            ["com.docker.compose.project"] = "modern-cs16",
            ["com.docker.compose.service"] = name,
            ["cs16vn.managed"]             = "true",
            ["cs16vn.type"]                = type,
        };
        if (extraLabels != null)
            foreach (var kv in extraLabels) labels[kv.Key] = kv.Value;

        // ── Env vars ─────────────────────────────────────────────────────────
        var env = new List<string>
        {
            $"SV_PORT={port}",
            $"SV_MAP={map}",
            $"SV_NAME={serverName}",
            $"SV_MAXPLAYERS={maxPlayers}",
            $"SV_LAN=0",
            $"RCON_PASSWORD={rconPassword}",
            $"DB_CONNECTION={dbConnection}",
            $"JWT_SECRET={jwtSecret}",
        };

        // ── Create + connect + start ─────────────────────────────────────────
        var createResp = await _docker.Containers.CreateContainerAsync(new CreateContainerParameters
        {
            Name         = name,
            Image        = image,
            Env          = env,
            Labels       = labels,
            ExposedPorts = new Dictionary<string, EmptyStruct>
            {
                [$"{portStr}/udp"] = default,
                [$"{portStr}/tcp"] = default,
            },
            HostConfig = new HostConfig
            {
                PortBindings  = portBindings,
                Binds         = binds,
                RestartPolicy = new RestartPolicy { Name = RestartPolicyKind.UnlessStopped },
            },
        });

        await _docker.Networks.ConnectNetworkAsync(network, new NetworkConnectParameters
        {
            Container = createResp.ID,
        });

        await _docker.Containers.StartContainerAsync(createResp.ID, new ContainerStartParameters());
        _log.LogInformation("[Docker] Started {Type} container={Name} port={Port}", type, name, port);

        // ── Lấy IP trong network ─────────────────────────────────────────────
        var info = await _docker.Containers.InspectContainerAsync(createResp.ID);
        var ip   = info.NetworkSettings.Networks.TryGetValue(network, out var netInfo)
            ? netInfo.IPAddress : "";

        return (name, ip);
    }

    /// <summary>
    /// Stop + remove container.
    /// Guard: chỉ xử lý containers có prefix managed/room/tournament.
    /// </summary>
    public async Task<bool> RemoveContainerAsync(string containerName)
    {
        var isManaged = containerName.StartsWith(AdminPrefix)
                     || containerName.StartsWith(RoomPrefix)
                     || containerName.StartsWith(TournamentPrefix);

        if (!isManaged)
        {
            _log.LogWarning("[Docker] BLOCKED removal of non-managed container: {Name}", containerName);
            return false;
        }

        try
        {
            await _docker.Containers.StopContainerAsync(containerName,
                new ContainerStopParameters { WaitBeforeKillSeconds = 5 });
            await _docker.Containers.RemoveContainerAsync(containerName,
                new ContainerRemoveParameters { Force = true });
            _log.LogInformation("[Docker] Removed container {Name}", containerName);
            return true;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "[Docker] Failed to remove {Name}", containerName);
            return false;
        }
    }

    // Backward compat alias
    public Task<bool> RemoveServerAsync(string name) => RemoveContainerAsync(name);

    public async Task<bool> IsRunningAsync(string containerName)
    {
        try
        {
            var info = await _docker.Containers.InspectContainerAsync(containerName);
            return info.State.Running;
        }
        catch { return false; }
    }
}
