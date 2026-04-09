const { app, BrowserWindow, ipcMain, shell, dialog } = require('electron');
const path = require('path');
const fs = require('fs');
const dgram = require('dgram');
const { exec, spawn } = require('child_process');

// ── Fix Windows GPU hardware video overlay issue ──────────────────────────────
// MP4/WebM video bị Windows render ở GPU overlay tách biệt → không thấy trong UI
// Force software decode → video render cùng layer với toàn bộ UI
app.commandLine.appendSwitch('disable-gpu-video-decode');
app.commandLine.appendSwitch('disable-features', 'HardwareMediaKeyHandling,MediaSessionService');

// ── A2S_INFO: query real server info via GoldSrc UDP protocol ────────────────
// Giao thức: gửi UDP packet → server CS 1.6 trả về tên, map, players, maxplayers
function queryGameServer(host, port, timeoutMs = 3000) {
  return new Promise((resolve) => {
    const client = dgram.createSocket('udp4');
    let resolved = false;

    const done = (result) => {
      if (resolved) return;
      resolved = true;
      try { client.close(); } catch {}
      resolve(result);
    };

    const timer = setTimeout(() => done({ online: false, error: 'timeout' }), timeoutMs);

    // A2S_INFO challenge packet (GoldSrc / Source protocol)
    const A2S_INFO = Buffer.from([
      0xFF, 0xFF, 0xFF, 0xFF,  // header
      0x54,                     // T = A2S_INFO
      // "Source Engine Query\0"
      0x53,0x6F,0x75,0x72,0x63,0x65,0x20,0x45,0x6E,0x67,
      0x69,0x6E,0x65,0x20,0x51,0x75,0x65,0x72,0x79,0x00,
    ]);

    client.on('error', (err) => { clearTimeout(timer); done({ online: false, error: err.message }); });

    client.on('message', (msg) => {
      clearTimeout(timer);
      try {
        // Parse A2S_INFO response
        let offset = 4; // skip 0xFFFFFFFF header
        const type = msg[offset++]; // 0x49 = I (info response) or 0x6D = m (GoldSrc)

        if (type === 0x6D) {
          // GoldSrc old format
          // skip ip\0
          while (msg[offset++] !== 0); // ip
          const readStr = () => {
            let s = ''; while (msg[offset] !== 0) s += String.fromCharCode(msg[offset++]); offset++; return s;
          };
          const serverName = readStr();
          const mapName    = readStr();
          const gameDir    = readStr();
          const gameDesc   = readStr();
          offset++; // activePlayers count in next byte
          const players    = msg[offset++];
          const maxPlayers = msg[offset++];
          done({ online: true, serverName, mapName, players, maxPlayers, gameDir });
        } else if (type === 0x49) {
          // Source/new format
          offset++; // protocol
          const readStr = () => {
            let s = ''; while (offset < msg.length && msg[offset] !== 0) s += String.fromCharCode(msg[offset++]); offset++; return s;
          };
          const serverName = readStr();
          const mapName    = readStr();
          const gameDir    = readStr();
          const gameDesc   = readStr();
          offset += 2; // appid
          const players    = msg[offset++];
          const maxPlayers = msg[offset++];
          done({ online: true, serverName, mapName, players, maxPlayers, gameDir });
        } else {
          done({ online: false, error: 'unknown response type: ' + type.toString(16) });
        }
      } catch(e) {
        done({ online: false, error: 'parse error: ' + e.message });
      }
    });

    client.send(A2S_INFO, 0, A2S_INFO.length, port, host, (err) => {
      if (err) { clearTimeout(timer); done({ online: false, error: err.message }); }
    });
  });
}

// Measure ping (RTT) with a lightweight challenge packet
function measurePing(host, port) {
  return new Promise((resolve) => {
    const start = Date.now();
    const client = dgram.createSocket('udp4');
    const timer = setTimeout(() => { try { client.close(); } catch {} resolve(999); }, 2000);
    const packet = Buffer.from([0xFF,0xFF,0xFF,0xFF,0x54,
      0x53,0x6F,0x75,0x72,0x63,0x65,0x20,0x45,0x6E,0x67,
      0x69,0x6E,0x65,0x20,0x51,0x75,0x65,0x72,0x79,0x00]);
    client.on('message', () => { clearTimeout(timer); client.close(); resolve(Date.now() - start); });
    client.on('error', () => { clearTimeout(timer); resolve(999); });
    client.send(packet, 0, packet.length, port, host, (err) => { if (err) { clearTimeout(timer); resolve(999); } });
  });
}

// IPC handler: query server và trả về thông tin thật
ipcMain.handle('query-server', async (event, { host, port }) => {
  const [info, ping] = await Promise.all([
    queryGameServer(host, port || 27015),
    measurePing(host, port || 27015),
  ]);
  return { ...info, ping, host, port: port || 27015 };
});


// Load config
const configPath = path.join(__dirname, 'config.json');
let config = { apiUrl: 'http://localhost:7777' };
if (fs.existsSync(configPath)) {
  config = JSON.parse(fs.readFileSync(configPath, 'utf8'));
}

let mainWindow;

// ── Splash Screen (2 giây trước khi mở launcher) ──────────────────────────
function createSplashWindow() {
  const splash = new BrowserWindow({
    width: 900,
    height: 506,
    frame: false,
    transparent: true,
    alwaysOnTop: true,
    skipTaskbar: true,
    resizable: false,
    center: true,
    webPreferences: {
      contextIsolation: true,
      nodeIntegration: false,
    }
  });
  splash.loadFile('renderer/splash.html');
  return splash;
}

function createWindow() {
  mainWindow = new BrowserWindow({
    width: 1100,
    height: 650,
    minWidth: 1100,
    minHeight: 650,
    frame: false,
    resizable: false,
    transparent: false,
    backgroundColor: '#0a0a0f',
    show: false, // ẩn cho đến khi sẵn sàng
    icon: path.join(__dirname, 'assets', process.platform === 'win32' ? 'icon.ico' : 'icon.png'),
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
    }
  });

  mainWindow.loadFile('renderer/index.html');
  // Uncomment for debugging:
  // mainWindow.webContents.openDevTools();
}

app.whenReady().then(() => {
  const splash = createSplashWindow();
  createWindow(); // load main window in background

  setTimeout(() => {
    if (!splash.isDestroyed()) splash.close();
    mainWindow.show();
    mainWindow.center();
  }, 2000);
});

app.on('window-all-closed', () => { if (process.platform !== 'darwin') app.quit(); });
app.on('activate', () => { if (BrowserWindow.getAllWindows().length === 0) createWindow(); });

// ── Window controls ────────────────────────────────────────────────────────────
ipcMain.on('win-close',    () => mainWindow.close());
ipcMain.on('win-minimize', () => mainWindow.minimize());

// ── Branded Client Key Verification ───────────────────────────────────────────
// Secret nhúng trong binary — dùng XOR obfuscation đơn giản để tránh plaintext
const _KS = [0x43,0x53,0x31,0x36,0x56,0x4e,0x5f,0x4f,0x46,0x46,0x49,0x43,0x49,0x41,0x4c,0x5f,0x4b,0x45,0x59,0x5f,0x32,0x30,0x32,0x36];
const CLIENT_SECRET = Buffer.from(_KS).toString();
// = "CS16VN_OFFICIAL_KEY_2026"

const CS_KEY_FILENAME = 'cs16vn.key';

function verifyClientKey(gameDir) {
  const keyFile = path.join(gameDir, CS_KEY_FILENAME);
  if (!fs.existsSync(keyFile)) return false;
  try {
    const content = fs.readFileSync(keyFile, 'utf8').trim();
    // Format: CS16VN:v2.0:HMAC
    // HMAC = HMAC-SHA256("CS16VN:v2.0", CLIENT_SECRET)
    const parts = content.split(':');
    if (parts.length < 3 || parts[0] !== 'CS16VN') return false;
    const header = parts.slice(0, 2).join(':');
    const clientHmac = parts[2];
    const crypto = require('crypto');
    const expected = crypto.createHmac('sha256', CLIENT_SECRET).update(header).digest('hex');
    return clientHmac === expected;
  } catch { return false; }
}


ipcMain.handle('api-request', async (event, { method, endpoint, body, token }) => {
  const https = require('https');
  const http = require('http');
  const url = new URL(config.apiUrl + endpoint);
  const mod = url.protocol === 'https:' ? https : http;

  return new Promise((resolve) => {
    const payload = body ? JSON.stringify(body) : null;
    const options = {
      hostname: url.hostname,
      port: url.port || (url.protocol === 'https:' ? 443 : 80),
      path: url.pathname + url.search,
      method: method || 'GET',
      headers: {
        'Content-Type': 'application/json',
        ...(payload ? { 'Content-Length': Buffer.byteLength(payload) } : {}),
        ...(token ? { 'Authorization': `Bearer ${token}` } : {}),
      }
    };

    const req = mod.request(options, (res) => {
      let data = '';
      res.on('data', chunk => data += chunk);
      res.on('end', () => {
        try { resolve({ status: res.statusCode, data: JSON.parse(data) }); }
        catch { resolve({ status: res.statusCode, data: data }); }
      });
    });
    req.on('error', (e) => resolve({ status: 0, error: e.message }));
    if (payload) req.write(payload);
    req.end();
  });
});

// ── Launch CS 1.6 ─────────────────────────────────────────────────────────────
ipcMain.handle('launch-cs', async (event, { csPath, serverIp, port, token, username }) => {
  // Common CS paths — Steam AND Non-Steam, sorted by priority
  const HOME = process.env.HOME || process.env.USERPROFILE || '';
  const defaultPaths = {
    win32: [
      // Steam
      'C:\\Program Files (x86)\\Steam\\steamapps\\common\\Half-Life\\hl.exe',
      'C:\\Program Files\\Steam\\steamapps\\common\\Half-Life\\hl.exe',
      `${HOME}\\Steam\\steamapps\\common\\Half-Life\\hl.exe`,
      // Non-Steam (standalone)
      'C:\\Games\\Counter-Strike\\hl.exe',
      'C:\\Games\\Counter-Strike 1.6\\hl.exe',
      'C:\\Counter Strike Cataclysm\\hl.exe',
      'C:\\CS\\hl.exe',
      'C:\\counter-strike\\hl.exe',
      `${HOME}\\Desktop\\CS 1.6\\hl.exe`,
    ],
    darwin: [
      `${HOME}/Library/Application Support/Steam/steamapps/common/Half-Life/hl`,
      `${HOME}/Games/CS16/hl`,
    ],
    linux: [
      `${HOME}/.steam/steam/steamapps/common/Half-Life/hl_linux`,
      `${HOME}/.local/share/Steam/steamapps/common/Half-Life/hl_linux`,
      `${HOME}/Games/CS16/hl_linux`,
    ],
  };

  let exePath = csPath;
  if (!exePath || !fs.existsSync(exePath)) {
    const paths = defaultPaths[process.platform] || [];
    exePath = paths.find(p => fs.existsSync(p));
  }

  if (!exePath) return { success: false, error: 'Không tìm thấy hl.exe. Hãy dùng nút DUYỆT để chỉ định đường dẫn đến CS 1.6 VN Client.' };

  // ── Branded client verification ─────────────────────────────────────────────
  const gameDirCheck = path.dirname(exePath);
  if (!verifyClientKey(gameDirCheck)) {
    return {
      success: false,
      error: 'CLIENT_NOT_VERIFIED',
      message: 'Vui lòng dùng CS 1.6 VN Client chính thức.\n\nTải về tại: [link của bạn]\n\nNếu đã dùng đúng client, hãy chỉ đường dẫn đến thư mục chứa hl.exe và cs16vn.key'
    };
  }

  // ── Detect executable type ──────────────────────────────────────────────────
  const exeName = path.basename(exePath).toLowerCase();
  const isCstrikeExe   = exeName === 'cstrike.exe';  // Non-Steam launcher wrapper
  const isHlExe        = exeName === 'hl.exe' || exeName === 'hl_linux' || exeName === 'hl';
  const gameDir        = path.dirname(exePath);

  // ── Build args depending on exe type ───────────────────────────────────────
  // cstrike.exe: is already a CS mod launcher, do NOT pass -game cstrike
  // hl.exe:      standard GoldSrc, needs -game cstrike
  const args = [];
  if (isHlExe) args.push('-game', 'cstrike');

  args.push(
    '+connect', `${serverIp}:${port}`,
    '+name', username,
    '-novid',
    // Rate settings — phải set ở đây để áp dụng ngay khi connect
    '+rate', '100000',
    '+cl_updaterate', '101',
    '+cl_cmdrate', '101',
    '+ex_interp', '0.01',
  );

  // JWT via +password (game server auth_lock reads this via setinfo)
  // Use short token to avoid cmdline length issues — server validates from DB
  if (token) {
    // Only first 200 chars to avoid cmd length issues on Win32
    const shortToken = token.substring(0, 200);
    args.push('+setinfo', '_token', shortToken);
  }

  // ── Spawn with correct CWD (critical for Non-Steam DLL loading) ─────────
  const spawnOpts = {
    cwd: gameDir,          // MUST be game dir so steam_api/revemu DLLs load
    detached: true,
    stdio: 'ignore',
    windowsHide: false,    // allow the game window to appear
  };

  try {
    if (process.platform === 'win32') {
      spawn(exePath, args, spawnOpts).unref();
    } else {
      // Linux/macOS: ensure executable bit
      try { fs.chmodSync(exePath, 0o755); } catch {}
      spawn(exePath, args, spawnOpts).unref();
    }
    return { success: true, exe: exeName, dir: gameDir };
  } catch(e) {
    return { success: false, error: e.message };
  }
});


// ── Get API URL ────────────────────────────────────────────────────────────────
ipcMain.handle('get-config', () => config);
ipcMain.handle('save-config', (event, newConfig) => {
  Object.assign(config, newConfig);
  fs.writeFileSync(configPath, JSON.stringify(config, null, 2));
  return true;
});

// ── Browse CS 1.6 executable ───────────────────────────────────────────────────
ipcMain.handle('browse-cs-path', async () => {
  const filters = process.platform === 'win32'
    ? [{ name: 'Half-Life Executable', extensions: ['exe'] }]
    : [{ name: 'All Files', extensions: ['*'] }];

  const result = await dialog.showOpenDialog(mainWindow, {
    title: 'Chọn file CS 1.6 (hl.exe hoặc hl_linux)',
    properties: ['openFile'],
    filters,
  });

  if (!result.canceled && result.filePaths.length > 0) {
    return result.filePaths[0];
  }
  return null;
});

