/* ── State ───────────────────────────────────────────────────────────────── */
let token = null;
let currentUser = null;
let appConfig = {};

/* ── Startup ─────────────────────────────────────────────────────────────── */
window.addEventListener('DOMContentLoaded', async () => {
  appConfig = await window.cs16.getConfig();

  // Hiển thị API URL hiện tại trên màn hình login
  const quickInput = document.getElementById('api-url-quick');
  if (quickInput) quickInput.value = appConfig.apiUrl || 'http://localhost:7777';

  // If stored session
  const saved = localStorage.getItem('cs16_token');
  const savedUser = localStorage.getItem('cs16_user');
  if (saved && savedUser) {
    token = saved;
    currentUser = JSON.parse(savedUser);
    showMain();
  } else {
    showAuth();
    loadAuthStats();
  }

  // Populate settings inputs
  document.getElementById('set-api-url').value = appConfig.apiUrl || '';
  document.getElementById('set-server-ip').value = appConfig.serverIp || '';
  document.getElementById('set-server-port').value = appConfig.serverPort || 27015;
  const csInp = document.getElementById('cs-path-input');
  if (csInp) csInp.value = appConfig.csPath || '';

  // Server IP display
  const ipEl = document.getElementById('server-ip-display');
  if (ipEl) ipEl.textContent = `${appConfig.serverIp || '127.0.0.1'}:${appConfig.serverPort || 27015}`;
});

/* ── Save API URL từ màn hình login ─────────────────────────────────────── */
async function saveApiUrl() {
  const val = document.getElementById('api-url-quick')?.value?.trim();
  if (!val) return;
  appConfig.apiUrl = val;
  await window.cs16.saveConfig({ apiUrl: val });
  // Sync vào settings input nếu đang mở
  const setInp = document.getElementById('set-api-url');
  if (setInp) setInp.value = val;
  toast('✅ API URL đã lưu: ' + val, 'success');
}


/* ── Helpers ─────────────────────────────────────────────────────────────── */
function api(method, endpoint, body) {
  return window.cs16.apiRequest({ method, endpoint, body, token });
}

function toast(msg, type = '') {
  const el = document.getElementById('toast');
  el.textContent = msg;
  el.className = 'toast' + (type ? ' ' + type : '');
  el.classList.remove('hidden');
  clearTimeout(window._toastTimer);
  window._toastTimer = setTimeout(() => el.classList.add('hidden'), 3500);
}

function setLoading(btnId, loading) {
  const btn = document.getElementById(btnId);
  if (!btn) return;
  btn.disabled = loading;
  const text = btn.querySelector('.btn-text');
  const loader = btn.querySelector('.btn-loader');
  if (text) text.classList.toggle('hidden', loading);
  if (loader) loader.classList.toggle('hidden', !loading);
}

function showError(elId, msg) {
  const el = document.getElementById(elId);
  if (el) { el.textContent = msg; el.classList.remove('hidden'); }
}
function clearError(elId) {
  const el = document.getElementById(elId);
  if (el) { el.textContent = ''; el.classList.add('hidden'); }
}
function showSuccess(elId, msg) {
  const el = document.getElementById(elId);
  if (el) { el.textContent = msg; el.classList.remove('hidden'); }
}

/* ── Screen switching ────────────────────────────────────────────────────── */
function showAuth() {
  document.getElementById('screen-auth').classList.add('active');
  document.getElementById('screen-main').classList.remove('active');
}
function showMain() {
  document.getElementById('screen-auth').classList.remove('active');
  document.getElementById('screen-main').classList.add('active');
  // Update sidebar
  const name = currentUser?.username || currentUser?.displayName || '?';
  document.getElementById('sidebar-username').textContent = name;
  document.getElementById('sidebar-avatar-letter').textContent = name[0]?.toUpperCase() || '?';
  loadHomeStats();
  loadRankings();
}

/* ── Tab switching (Auth) ────────────────────────────────────────────────── */
document.querySelectorAll('.tab').forEach(tab => {
  tab.addEventListener('click', () => {
    const id = tab.dataset.tab;
    if (id) showTab(id);
  });
});

function showTab(id) {
  document.querySelectorAll('.tab').forEach(t => t.classList.toggle('active', t.dataset.tab === id));
  document.querySelectorAll('.tab-content').forEach(c => c.classList.toggle('active', c.id === 'tab-' + id));
}

function showForgot() {
  document.querySelectorAll('.tab').forEach(t => t.classList.remove('active'));
  document.querySelectorAll('.tab-content').forEach(c => c.classList.toggle('active', c.id === 'tab-forgot'));
}

/* ── Page switching (Main) ───────────────────────────────────────────────── */
function showPage(id) {
  document.querySelectorAll('.nav-btn').forEach(b => b.classList.toggle('active', b.dataset.page === id));
  document.querySelectorAll('.page').forEach(p => p.classList.toggle('active', p.id === 'page-' + id));
  if (id === 'rankings') loadRankings();
  if (id === 'servers') loadServerBrowser();
  // v1.1.0 hooks
  if (id === 'kyc') checkKycStatus();
  if (id === 'tournaments') loadTournaments();
  if (id === 'rooms') loadRooms();
  if (id === 'donate') loadDonateData();
  if (id === 'about') loadAboutStats();
}


/* ── Toggle password visibility ─────────────────────────────────────────── */
function togglePw(inputId, btn) {
  const inp = document.getElementById(inputId);
  inp.type = inp.type === 'password' ? 'text' : 'password';
  btn.textContent = inp.type === 'password' ? '👁' : '🙈';
}

/* ── Load auth screen live stats ─────────────────────────────────────────── */
async function loadAuthStats() {
  try {
    const r = await api('GET', '/api/players/stats');
    if (r.status === 200 && r.data.length !== undefined) {
      document.getElementById('stat-players').textContent = r.data.length;
    }
  } catch {}
}

/* ── LOGIN ───────────────────────────────────────────────────────────────── */
async function doLogin() {
  clearError('login-error');
  const login = document.getElementById('login-username').value.trim();
  const password = document.getElementById('login-password').value;
  if (!login || !password) { showError('login-error', 'Vui lòng nhập đầy đủ thông tin.'); return; }

  setLoading('btn-login', true);
  try {
    const r = await api('POST', '/api/auth/login', { login, password });
    if (r.status === 200 && r.data.token) {
      token = r.data.token;
      // API trả về { token, userId, displayName } — không có user nested
      currentUser = {
        id: r.data.userId,
        username: login,
        displayName: r.data.displayName || login
      };
      localStorage.setItem('cs16_token', token);
      localStorage.setItem('cs16_user', JSON.stringify(currentUser));
      showMain();
    } else {
      const msg = r.data?.error || r.data?.message || 'Sai tên đăng nhập hoặc mật khẩu.';
      showError('login-error', msg);
    }
  } catch(e) {
    showError('login-error', 'Không thể kết nối đến server. Kiểm tra API đang chạy chưa.');
  }
  setLoading('btn-login', false);
}

// Enter key support
document.getElementById('login-password')?.addEventListener('keydown', e => { if(e.key==='Enter') doLogin(); });

/* ── REGISTER ────────────────────────────────────────────────────────────── */
async function doRegister() {
  clearError('reg-error');
  const username = document.getElementById('reg-username').value.trim();
  const email    = document.getElementById('reg-email').value.trim();
  const password = document.getElementById('reg-password').value;
  const confirm  = document.getElementById('reg-confirm').value;

  if (!username || !email || !password) { showError('reg-error', 'Vui lòng nhập đầy đủ thông tin.'); return; }
  if (password !== confirm) { showError('reg-error', 'Mật khẩu xác nhận không khớp.'); return; }
  if (password.length < 6) { showError('reg-error', 'Mật khẩu tối thiểu 6 ký tự.'); return; }

  setLoading('btn-register', true);
  try {
    const r = await api('POST', '/api/auth/register', { username, email, password });
    if (r.status === 200) {
      document.getElementById('reg-success').textContent = '✅ Đăng ký thành công! Hãy đăng nhập.';
      document.getElementById('reg-success').classList.remove('hidden');
      setTimeout(() => showTab('login'), 1800);
    } else {
      const errors = Array.isArray(r.data) ? r.data.join(', ') : (r.data?.message || JSON.stringify(r.data));
      showError('reg-error', errors);
    }
  } catch(e) {
    showError('reg-error', 'Không thể kết nối đến server.');
  }
  setLoading('btn-register', false);
}

/* ── FORGOT PASSWORD ─────────────────────────────────────────────────────── */
async function doForgot() {
  clearError('forgot-error');
  const email = document.getElementById('forgot-email').value.trim();
  if (!email) { showError('forgot-error', 'Vui lòng nhập email.'); return; }

  setLoading('btn-forgot', true);
  try {
    const r = await api('POST', '/api/auth/forgot-password', { email });
    if (r.status === 200) {
      showSuccess('forgot-msg', '📧 Email đặt lại mật khẩu đã được gửi. Kiểm tra hộp thư!');
    } else {
      showError('forgot-error', r.data?.error || r.data?.message || 'Không tìm thấy email này.');
    }
  } catch {
    showError('forgot-error', 'Không thể kết nối đến server.');
  }
  setLoading('btn-forgot', false);
}

/* ── LOGOUT ──────────────────────────────────────────────────────────────── */
function doLogout() {
  token = null; currentUser = null;
  localStorage.removeItem('cs16_token');
  localStorage.removeItem('cs16_user');
  showAuth();
  showTab('login');
}

/* ── HOME STATS ──────────────────────────────────────────────────────────── */
async function loadHomeStats() {
  try {
    const r = await api('GET', '/api/players/me');
    if (r.status === 200) {
      const s = r.data;
      document.getElementById('hstat-kills').textContent  = s.kills  ?? '–';
      document.getElementById('hstat-deaths').textContent = s.deaths ?? '–';
      document.getElementById('hstat-wins').textContent   = s.wins   ?? '–';
      const kd = s.deaths > 0 ? (s.kills/s.deaths).toFixed(2) : s.kills ?? '–';
      document.getElementById('hstat-kd').textContent = kd;
      document.getElementById('sidebar-rank').textContent = s.rank ? `Rank #${s.rank}` : 'Rank #–';
    }
  } catch {}

  // Online count
  try {
    const r2 = await api('GET', '/api/game/status');
    if (r2.status === 200) {
      const cnt = r2.data.playersOnline ?? 0;
      document.getElementById('home-online').textContent = cnt;
      document.getElementById('stat-online').textContent = cnt;
    }
  } catch {}
}

/* ── RANKINGS ────────────────────────────────────────────────────────────── */
async function loadRankings() {
  const tbody = document.getElementById('rank-tbody');
  if (!tbody) return;
  tbody.innerHTML = '<tr><td colspan="6" class="loading-row">Đang tải...</td></tr>';
  try {
    const r = await api('GET', '/api/players/stats');
    if (r.status === 200 && Array.isArray(r.data)) {
      tbody.innerHTML = r.data.map((p, i) => `
        <tr>
          <td><strong>#${i+1}</strong></td>
          <td>${p.displayName || p.username || '–'}</td>
          <td>${p.kills ?? 0}</td>
          <td>${p.deaths ?? 0}</td>
          <td>${p.deaths > 0 ? (p.kills/p.deaths).toFixed(2) : (p.kills ?? 0)}</td>
          <td>${p.wins ?? 0}</td>
        </tr>`).join('');
    } else {
      tbody.innerHTML = '<tr><td colspan="6" class="loading-row">Chưa có dữ liệu.</td></tr>';
    }
  } catch {
    tbody.innerHTML = '<tr><td colspan="6" class="loading-row">Không thể tải dữ liệu.</td></tr>';
  }
}

/* ── SERVER BROWSER ─────────────────────────────────────────────────────── */
async function loadServerBrowser() {
  const container = document.getElementById('server-browser-list');
  if (!container) return;

  // Skeleton loading
  container.innerHTML = `
    <div class="server-card loading-card">
      <span class="server-status-dot pulsing"></span>
      <span style="color:var(--text2)">Đang tải danh sách server...</span>
    </div>`;

  // Hủy auto-refresh cũ
  if (window._serverRefreshTimer) clearTimeout(window._serverRefreshTimer);

  let servers = [];

  // 1) Lấy danh sách server từ API
  try {
    const r = await api('GET', '/api/servers');
    if (r.status === 200 && Array.isArray(r.data)) {
      servers = r.data.filter(s => s.isActive);
    }
  } catch {}

  // Fallback: nếu API không có data, dùng server từ config
  if (servers.length === 0) {
    servers = [{
      id: 0, name: 'Modern CS 1.6 Vietnam',
      host: appConfig.serverIp || '192.168.1.2',
      port: parseInt(appConfig.serverPort) || 27015,
      currentMap: 'de_dust2', description: '', maxPlayers: 32
    }];
  }

  // 2) A2S query song song tất cả server
  container.innerHTML = '';
  const queries = servers.map(s => window.cs16.queryServer({ host: s.host, port: s.port })
    .then(info => ({ server: s, info }))
    .catch(() => ({ server: s, info: { online: false, error: 'timeout' } }))
  );
  const results = await Promise.all(queries);

  // 3) Render từng card
  for (const { server: s, info } of results) {
    const card = document.createElement('div');
    card.className = 'server-card' + (info.online ? '' : ' offline-card');
    card.id = `srv-${s.id}`;

    if (!info.online) {
      card.innerHTML = `
        <span class="server-status-dot offline"></span>
        <div class="server-info-main">
          <div class="server-name">${s.name}</div>
          <div class="server-meta">
            <span class="server-map">🗺️ ${s.currentMap}</span>
            <span class="server-ip">📡 ${s.host}:${s.port}</span>
            <span class="server-detail offline-text">⚠️ Offline (${info.error || 'timeout'})</span>
          </div>
        </div>
        <button class="btn-join disabled" disabled>OFFLINE</button>`;
    } else {
      const pingClass = info.ping < 50 ? 'ping-good' : info.ping < 100 ? 'ping-ok' : 'ping-bad';
      const mapName = info.mapName || s.currentMap;
      card.innerHTML = `
        <span class="server-status-dot online"></span>
        <div class="server-info-main">
          <div class="server-name">🎮 ${info.serverName || s.name}</div>
          <div class="server-meta">
            <span class="server-map">🗺️ ${mapName}</span>
            <span class="server-players">👥 ${info.players ?? '?'}/${info.maxPlayers ?? s.maxPlayers} người</span>
            <span class="server-ip">📡 ${s.host}:${s.port}</span>
          </div>
          ${s.description ? `<div class="server-desc">${s.description}</div>` : ''}
        </div>
        <div class="server-right">
          <span class="server-ping ${pingClass}">${info.ping}ms</span>
          <button class="btn-join" onclick="joinServer('${s.host}', ${s.port}, this)">THAM GIA</button>
        </div>`;
    }
    container.appendChild(card);
  }

  // Auto-refresh mỗi 30s
  window._serverRefreshTimer = setTimeout(loadServerBrowser, 30000);
}

async function joinServer(host, port, btnEl) {
  if (!token) { toast('Bạn cần đăng nhập trước!', 'error'); return; }
  const csPath = appConfig.csPath || '';
  const username = currentUser?.displayName || currentUser?.username || 'Player';

  // Dùng đúng nút được click, không querySelector chung
  const btn = btnEl || document.querySelector(`#srv- .btn-join`);
  if (btn) { btn.disabled = true; btn.textContent = 'Đang mở...'; }

  toast('⏳ Đang khởi động CS 1.6...');
  const r = await window.cs16.launchCS({ csPath, serverIp: host, port, token, username });

  if (r.success) {
    toast(`✅ CS 1.6 đã khởi động! Đang kết nối ${host}:${port}`, 'success');
    if (btn) { btn.disabled = false; btn.textContent = 'THAM GIA'; }
  } else if (r.error === 'CLIENT_NOT_VERIFIED') {
    // Client không phải bản chính thức
    if (btn) { btn.disabled = false; btn.textContent = 'THAM GIA'; }
    const msg = r.message || 'Vui lòng dùng CS 1.6 VN Client chính thức.';
    alert(`🔒 Client không được xác thực\n\n${msg}`);
  } else {
    toast('❌ ' + (r.error || 'Không thể khởi động CS 1.6'), 'error');
    if (btn) { btn.disabled = false; btn.textContent = 'THAM GIA'; }
  }
}

// Gọi loadServerBrowser khi switch sang tab servers

/* ── BROWSE CS PATH (native dialog) ─────────────────────────────────────── */
async function browseCS() {
  const selectedPath = await window.cs16.browseCSPath();
  if (selectedPath) {
    document.getElementById('cs-path-input').value = selectedPath;
    appConfig.csPath = selectedPath;
    await window.cs16.saveConfig({ csPath: selectedPath });
    toast('✅ Đã chọn: ' + selectedPath.split(/[/\\]/).pop(), 'success');
  }
}


/* ── KYC Upload ─────────────────────────────────────────────────────────── */
function previewFile(inputId, previewId) {
  const input   = document.getElementById(inputId);
  const preview = document.getElementById(previewId);
  const placeholder = input.parentElement.querySelector('.upload-placeholder');
  if (input.files[0]) {
    const reader = new FileReader();
    reader.onload = e => {
      preview.src = e.target.result;
      preview.classList.remove('hidden');
      if (placeholder) placeholder.style.display = 'none';
    };
    reader.readAsDataURL(input.files[0]);
  }
}

function dropFile(event, inputId) {
  event.preventDefault();
  const input = document.getElementById(inputId);
  if (event.dataTransfer.files[0]) {
    input.files = event.dataTransfer.files;
    previewFile(inputId, 'prev-' + inputId.replace('kyc-', ''));
  }
}

async function submitKYC() {
  const front  = document.getElementById('kyc-front').files[0];
  const back   = document.getElementById('kyc-back').files[0];
  const selfie = document.getElementById('kyc-selfie').files[0];
  clearError('kyc-error');

  if (!front || !back || !selfie) { showError('kyc-error', 'Vui lòng tải lên đủ 3 ảnh.'); return; }

  const toBase64 = file => new Promise((res, rej) => {
    const r = new FileReader(); r.onload = () => res(r.result); r.onerror = rej; r.readAsDataURL(file);
  });

  const payload = {
    frontId:  await toBase64(front),
    backId:   await toBase64(back),
    selfieId: await toBase64(selfie),
  };

  const res = await api('POST', '/api/kyc/submit', payload);
  if (res.status === 200) {
    showSuccess('kyc-msg', '✅ Hồ sơ KYC đã được gửi! Chúng tôi sẽ xem xét trong 24h.');
    toast('KYC đã gửi thành công', 'success');
  } else {
    showError('kyc-error', res.data?.error || res.data?.message || 'Gửi KYC thất bại.');
  }
}

/* ── SETTINGS ────────────────────────────────────────────────────────────── */
async function saveSettings() {
  const apiUrl    = document.getElementById('set-api-url').value.trim();
  const serverIp  = document.getElementById('set-server-ip').value.trim();
  const serverPort = parseInt(document.getElementById('set-server-port').value) || 27015;

  appConfig = { ...appConfig, apiUrl, serverIp, serverPort };
  await window.cs16.saveConfig(appConfig);

  const ipEl = document.getElementById('server-ip-display');
  if (ipEl) ipEl.textContent = `${serverIp}:${serverPort}`;

  showSuccess('settings-msg', '✅ Đã lưu cài đặt!');
  setTimeout(() => document.getElementById('settings-msg')?.classList.add('hidden'), 2500);
  toast('Đã lưu cài đặt', 'success');
}

/* ── KYC STATUS CHECK ──────────────────────────────────────────────────────── */
async function checkKycStatus() {
  if (!currentUser?.id) return;
  try {
    const r = await api('GET', `/api/kyc/my-status?playerId=${currentUser.id}`);
    const approved = r.status === 200 && r.data?.isApproved === true;
    document.getElementById('kyc-approved-banner')?.classList.toggle('hidden', !approved);
    document.getElementById('kyc-form-area')?.classList.toggle('hidden', approved);
    if (approved) {
      document.getElementById('nav-create-tournament')?.classList.remove('hidden');
    }
  } catch {}
}

/* ── TOURNAMENTS ────────────────────────────────────────────────────────────── */
async function loadTournaments() {
  const el = document.getElementById('tournaments-list');
  if (!el) return;
  el.innerHTML = '<div class="loading-pulse">Đang tải giải đấu...</div>';
  try {
    const r = await api('GET', '/api/tournaments');
    const list = (r.status === 200 && Array.isArray(r.data)) ? r.data : [];
    if (!list.length) { el.innerHTML = '<p class="empty-msg">Chưa có giải đấu nào.</p>'; return; }
    el.innerHTML = list.map(t => `
      <div class="tournament-card" onclick="viewTournament('${t.id}','${escHtml(t.title)}')">
        <div class="tc-header">
          <span class="tc-title">${escHtml(t.title)}</span>
          <span class="tc-badge tc-${(t.status||'open').toLowerCase()}">${t.status}</span>
        </div>
        <div class="tc-meta">
          <span>⚔️ ${t.format || '5vs5'}</span>
          <span>🔁 ${t.roundSystem === '10round' ? 'Thắng 8/10' : 'Thắng 3/5'}</span>
          <span>👥 ${t.registeredCount || 0}/${t.maxPlayers} người</span>
          <span>🗺️ ${t.matchCount || 0} trận</span>
        </div>
        <div class="tc-org">Tổ chức: ${escHtml(t.organizerName || 'Ban tổ chức')}</div>
      </div>`).join('');
    document.getElementById('tournament-detail')?.classList.add('hidden');
    el.classList.remove('hidden');
  } catch(e) { el.innerHTML = `<p class="form-error">Lỗi: ${e.message}</p>`; }
}

async function viewTournament(id, title) {
  document.getElementById('tournaments-list').classList.add('hidden');
  const detail = document.getElementById('tournament-detail');
  detail.classList.remove('hidden');
  const content = document.getElementById('tournament-detail-content');
  content.innerHTML = '<div class="loading-pulse">Đang tải trận đấu...</div>';
  try {
    const r = await api('GET', `/api/tournaments/${id}/matches`);
    const matches = (r.status === 200 && Array.isArray(r.data)) ? r.data : [];
    const ip = appConfig.serverIp || '192.168.1.2';
    const eloMap = { 'Final': '×4.0', 'Knockout': '×3.0', 'Group': '×2.0' };
    content.innerHTML = `
      <div class="detail-header">
        <h3>🏅 ${escHtml(title)}</h3>
        <button class="btn-excel" onclick="exportTournament('${id}')">📥 Xuất Excel toàn giải</button>
      </div>
      ${matches.length === 0 ? '<p class="empty-msg">Chưa có trận nào.</p>' : matches.map(m => {
        const total = (m.scoreA + m.scoreB) || 1;
        const pctA  = Math.round((m.scoreA / total) * 100);
        const pctB  = 100 - pctA;
        const isActive = m.status === 'Active';
        const port = m.matchPort || m.port;
        const stage = m.stage || 'Group';
        const elo   = eloMap[stage] || '×2.0';
        return `<div class="match-card">
          <div class="match-top">
            <span class="match-room">${escHtml(m.roomCode)}</span>
            <span class="match-map">🗺️ ${escHtml(m.mapName)}</span>
            <span class="match-stage match-stage-${stage.toLowerCase()}">${stage} <small>${elo} ELO</small></span>
            <span class="match-status match-${(m.status||'pending').toLowerCase()}">${m.status}</span>
            <button class="btn-excel-sm" onclick="exportMatch('${m.id}')">📥 Excel</button>
          </div>
          <div class="match-score">
            <span class="team-a">${escHtml(m.teamA)||'Team A'}</span>
            <span class="score">${m.scoreA} : ${m.scoreB}</span>
            <span class="team-b">${escHtml(m.teamB)||'Team B'}</span>
          </div>
          <div class="score-bar">
            <div class="bar-a" style="width:${pctA}%"></div>
            <div class="bar-b" style="width:${pctB}%"></div>
          </div>
          <div class="match-actions">
            ${isActive && port ? `
              <button class="btn-join" onclick="joinRoom(${port})">▶ Tham gia</button>
              ${m.hltvPort
                ? `<button class="btn-spectate hltv-btn" onclick="spectateRoom(${port},${m.hltvPort})">
                     📺 HLTV Live <small>(:${m.hltvPort})</small></button>
                   <span class="hltv-badge" title="30s delay bảo vệ tính công bằng giải đấu">🛡 30s delay</span>`
                : `<button class="btn-spectate" onclick="spectateRoom(${port},null)">👁 Spectate</button>`
              }
            ` : ''}
            <button class="btn-chat" onclick="openSpecChat('${m.id}','match','${escHtml(m.roomCode)}')">💬 Chat spectator</button>
          </div>
        </div>`;
      }).join('')}`;
  } catch(e) { content.innerHTML = `<p class="form-error">Lỗi: ${e.message}</p>`; }
}

function exportTournament(id) {
  const base = appConfig.apiUrl || 'http://localhost:7777';
  window.open(`${base}/api/tournaments/${id}/export`, '_blank');
}

function exportMatch(id) {
  const base = appConfig.apiUrl || 'http://localhost:7777';
  window.open(`${base}/api/tournament-matches/${id}/export`, '_blank');
}

async function createTournament() {
  const title = document.getElementById('ct-title').value.trim();
  if (!title) { showError('ct-error','Vui lòng nhập tên giải đấu'); return; }
  const maps = [...document.querySelectorAll('.map-checkboxes input:checked')].map(cb => cb.value);
  if (!maps.length) { showError('ct-error','Chọn ít nhất 1 map'); return; }
  try {
    const r = await api('POST', '/api/tournaments', {
      title,
      description: document.getElementById('ct-desc').value,
      entryFee: parseFloat(document.getElementById('ct-fee').value) || 0,
      maxPlayers: 32,
      startDate: null,
      format: document.getElementById('ct-format').value,
      roundSystem: document.getElementById('ct-rounds').value,
      mapList: JSON.stringify(maps),
      organizerName: document.getElementById('ct-organizer').value || currentUser?.username || '',
      organizerId: currentUser?.id || '',
      playersPerMap: parseInt(document.getElementById('ct-ppm').value) || 5
    });
    if (r.status === 200 || r.status === 201) {
      showSuccess('ct-msg', '✅ Đã tạo giải đấu! Chuyển sang tab Giải Đấu để xem.');
      setTimeout(() => showPage('tournaments'), 2000);
    } else {
      showError('ct-error', r.data?.error || r.data?.message || 'Lỗi tạo giải đấu');
    }
  } catch(e) { showError('ct-error', e.message || 'Lỗi tạo giải đấu'); }
}

/* ── PLAYER ROOMS ──────────────────────────────────────────────────────── */
async function loadRooms() {
  const el = document.getElementById('rooms-list');
  if (!el) return;
  try {
    const r = await api('GET', '/api/rooms');
    const list = (r.status === 200 && Array.isArray(r.data)) ? r.data : [];
    if (!list.length) { el.innerHTML = '<p class="empty-msg">Chưa có phòng nào đang mở. Hãy tạo phòng mới!</p>'; return; }
    const ip = appConfig.serverIp || '192.168.1.2';
    const myId = (currentUser?.id || '').toLowerCase();
    el.innerHTML = list.map(rm => {
      // Hỗ trợ cả camelCase (hostPlayerId) và PascalCase (HostPlayerId)
      const hostId = (rm.hostPlayerId || rm.HostPlayerId || '').toLowerCase();
      const isHost = myId && hostId && myId === hostId;
      return `
      <div class="room-card" id="room-${rm.id}">
        <div class="room-info">
          <div class="room-name">${escHtml(rm.roomName)}
            ${rm.hasPassword ? '<span class="room-lock">&#x1F512;</span>' : '<span class="room-badge room-open">Công khai</span>'}
          </div>
          <div class="room-meta">
            <span class="room-tag">${rm.format}</span>
            <span class="room-tag">&#x1F5FA; ${rm.mapName}</span>
            <span class="room-tag">&#x1F4E1; ${ip}:${rm.port}</span>
            <span class="room-tag">&#x1F464; ${rm.maxPlayers} ng</span>
          </div>
        </div>
        <div class="room-actions">
          <button class="btn-join" onclick="joinRoom(${rm.port})">&#x25B6; Tham gia</button>
          <button class="btn-spectate" onclick="spectateRoom(${rm.port})">&#x1F441; Spectate</button>
          <button class="btn-chat" onclick="openSpecChat('${rm.id}','room','${escHtml(rm.roomName)}')">&#x1F4AC; Chat</button>
          ${isHost
            ? `<button class="btn-delete-room" onclick="deleteRoom('${rm.id}')" title="Xóa phòng và dừng container">&#x1F5D1; Xóa</button>`
            : ''}
        </div>
      </div>`;
    }).join('');
  } catch(e) { el.innerHTML = `<p class="form-error">Lỗi: ${e.message}</p>`; }
}

async function createTournament() {
  const title = document.getElementById('ct-title').value.trim();
  if (!title) { showError('ct-error','Vui lòng nhập tên giải đấu'); return; }
  const maps = [...document.querySelectorAll('.map-checkboxes input:checked')].map(cb => cb.value);
  if (!maps.length) { showError('ct-error','Chọn ít nhất 1 map'); return; }
  try {
    const r = await api('POST', '/api/tournaments', {
      title,
      description: document.getElementById('ct-desc').value,
      entryFee: parseFloat(document.getElementById('ct-fee').value) || 0,
      maxPlayers: 32,
      startDate: null,
      format: document.getElementById('ct-format').value,
      roundSystem: document.getElementById('ct-rounds').value,
      mapList: JSON.stringify(maps),
      organizerName: document.getElementById('ct-organizer').value || currentUser?.username || '',
      organizerId: currentUser?.id || '',
      playersPerMap: parseInt(document.getElementById('ct-ppm').value) || 5
    });
    if (r.status === 200 || r.status === 201) {
      showSuccess('ct-msg', '✅ Đã tạo giải đấu! Chuyển sang tab Giải Đấu để xem.');
      setTimeout(() => showPage('tournaments'), 2000);
    } else {
      showError('ct-error', r.data?.error || r.data?.message || 'Lỗi tạo giải đấu');
    }
  } catch(e) { showError('ct-error', e.message || 'Lỗi tạo giải đấu'); }
}

/* ── PLAYER ROOMS ──────────────────────────────────────────────────────── */
async function loadRooms() {
  const el = document.getElementById('rooms-list');
  if (!el) return;
  try {
    const r = await api('GET', '/api/rooms');
    const list = (r.status === 200 && Array.isArray(r.data)) ? r.data : [];
    if (!list.length) { el.innerHTML = '<p class="empty-msg">Chưa có phòng nào đang mở. Hãy tạo phòng mới!</p>'; return; }
    const ip = appConfig.serverIp || '192.168.1.2';
    const myId = (currentUser?.id || '').toLowerCase();
    el.innerHTML = list.map(rm => {
      // Hỗ trợ camelCase (hostPlayerId) và PascalCase (HostPlayerId)
      const hostId = (rm.hostPlayerId || rm.HostPlayerId || '').toLowerCase();
      const isHost = myId && hostId && myId === hostId;
      return `
      <div class="room-card" id="room-${rm.id}">
        <div class="room-info">
          <div class="room-name">${escHtml(rm.roomName)}
            ${rm.hasPassword ? '<span class="room-lock">&#x1F512;</span>' : '<span class="room-badge room-open">Công khai</span>'}
          </div>
          <div class="room-meta">
            <span class="room-tag">${rm.format}</span>
            <span class="room-tag">&#x1F5FA; ${rm.mapName}</span>
            <span class="room-tag">&#x1F4E1; ${ip}:${rm.port}</span>
            <span class="room-tag">&#x1F464; ${rm.maxPlayers} ng</span>
          </div>
        </div>
        <div class="room-actions">
          <button class="btn-join" onclick="joinRoom(${rm.port})">&#x25B6; Tham gia</button>
          <button class="btn-spectate" onclick="spectateRoom(${rm.port})">&#x1F441; Spectate</button>
          <button class="btn-chat" onclick="openSpecChat('${rm.id}','room','${escHtml(rm.roomName)}')" >&#x1F4AC; Chat</button>
          ${isHost
            ? `<button class="btn-delete-room" onclick="deleteRoom('${rm.id}')" title="Xóa phòng và dừng container">&#x1F5D1; Xóa</button>`
            : ''}
        </div>
      </div>`;
    }).join('');
  } catch(e) { el.innerHTML = `<p class="form-error">Lỗi: ${e.message}</p>`; }
}

async function createRoom() {
  if (!currentUser?.id) { toast('Hãy đăng nhập', 'error'); return; }
  const name = document.getElementById('room-name').value.trim();
  if (!name) { showError('room-error', 'Nhập tên phòng'); return; }

  // ── KYC pre-check: kiểm tra trước khi gọi API ────────────────────────────
  try {
    const kycRes = await api('GET', `/api/kyc/my-status?playerId=${currentUser.id}`);
    const isApproved = kycRes.status === 200 && kycRes.data?.isApproved === true;
    if (!isApproved) {
      showError('room-error',
        '🔒 Bạn cần xác minh KYC được duyệt mới có thể tạo phòng. '
        + 'Hãy sang tab \'Xác Thực\' để nộp hồ sơ.');
      return;
    }
  } catch {
    showError('room-error', 'Không thể kiểm tra trạng thái KYC. Kiểm tra kết nối API.');
    return;
  }

  const format = document.getElementById('room-format').value;
  const maxMap = { '1vs1': 2, '3vs3': 6, '5vs5': 10, '10vs10': 20 };
  const btn = document.querySelector('#page-rooms .btn-primary');
  if (btn) { btn.disabled = true; btn.textContent = '⏳ Đang khởi tạo...'; }
  try {
    const r = await api('POST', '/api/rooms', {
      hostPlayerId: currentUser.id,
      roomName: name,
      mapName: document.getElementById('room-map').value,
      format,
      maxPlayers: maxMap[format] || 10,
      password: document.getElementById('room-password').value || null
    });
    if (r.status === 200 || r.status === 201) {
      const room = r.data;
      const ip = appConfig.serverIp || '192.168.1.2';
      const msgEl = document.getElementById('room-msg');
      msgEl.classList.remove('hidden');
      msgEl.innerHTML =
        `✅ Phòng <b>${escHtml(room.roomName || name)}</b> đã tạo! Port <b>${room.port}</b> &nbsp;` +
        `<button onclick="joinRoom(${room.port})" style="background:#e67e22;color:#fff;border:none;` +
        `padding:4px 12px;border-radius:6px;cursor:pointer;font-weight:700">▶ Vào chơi ngay</button>`;
      document.getElementById('room-error').textContent = '';
      document.getElementById('room-name').value = '';
      document.getElementById('room-password').value = '';
      loadRooms();
    } else if (r.status === 403) {
      showError('room-error', r.data?.error || 'Bạn chưa được xác minh KYC.');
    } else {
      showError('room-error', r.data?.error || r.data?.message || 'Lỗi tạo phòng');
    }
  } catch(e) { showError('room-error', e.message || 'Lỗi tạo phòng'); }
  finally { if (btn) { btn.disabled = false; btn.textContent = '🚪 TẠO PHÒNG'; } }
}

/* ── XÓA PHÒNG — chỉ chủ phòng, dừng và xóa Docker container ─────────── */
async function deleteRoom(id) {
  if (!currentUser?.id) { toast('Hãy đăng nhập', 'error'); return; }
  const confirmed = confirm(
    'Xóa phòng này sẽ dừng server và giải phóng tài nguyên trên Docker.\n'
    + 'Người chơi trong phòng sẽ bị ngắt kết nối.\n\nTiếp tục?'
  );
  if (!confirmed) return;

  // Card hiện spinner trong khi chờ
  const card = document.getElementById(`room-${id}`);
  const delBtn = card?.querySelector('.btn-delete-room');
  if (delBtn) { delBtn.disabled = true; delBtn.textContent = '⏳...'; }

  try {
    // DELETE /api/rooms/{id}?playerId={currentUser.id}
    // Backend xác nhận owner, gọi DockerGameServerService.RemoveContainerAsync()
    // Guard prefix cs16-room-* bảo vệ container gốc không bị xóa nhầm
    const r = await api('DELETE', `/api/rooms/${id}?playerId=${currentUser.id}`);
    if (r.status === 200) {
      toast('✅ Phòng đã xóa, container đã dừng và giải phóng', 'success');
      loadRooms();
    } else if (r.status === 403) {
      toast('❌ Bạn không phải chủ phòng này.', 'error');
      if (delBtn) { delBtn.disabled = false; delBtn.textContent = '🗑 Xóa'; }
    } else {
      toast('❌ ' + (r.data?.error || 'Không thể xóa phòng'), 'error');
      if (delBtn) { delBtn.disabled = false; delBtn.textContent = '🗑 Xóa'; }
    }
  } catch(e) {
    toast('❌ Lỗi: ' + (e.message || 'Không thể kết nối API'), 'error');
    if (delBtn) { delBtn.disabled = false; delBtn.textContent = '🗑 Xóa'; }
  }
}

function joinRoom(port) {
  const ip = appConfig.serverIp || '192.168.1.2';
  if (window.cs16?.launchGame) {
    window.cs16.launchGame(`+connect ${ip}:${port}`);
  } else {
    toast(`Kết nối: ${ip}:${port}`, 'info');
  }
}

// spectateRoom — hai chế độ:
//   hltvPort có → dùng HLTV relay (tournament, delay 30s)
//   hltvPort null → direct connect + chọn Spectate trong game (casual room)
function spectateRoom(gamePort, hltvPort) {
  const ip = appConfig.serverIp || '192.168.1.2';
  if (hltvPort) {
    // Tournament HLTV: connect vào HLTV relay, không phải game server
    if (window.cs16?.launchGame) {
      window.cs16.launchGame(`+connect ${ip}:${hltvPort}`);
      toast(`📺 HLTV Live (delay 30s) → ${ip}:${hltvPort}`, 'info');
    } else {
      toast(`HLTV: ${ip}:${hltvPort} (delay 30s)`, 'info');
    }
  } else {
    // Casual room: direct connect, chọn Spectate trong CS 1.6
    if (window.cs16?.launchGame) {
      window.cs16.launchGame(`+connect ${ip}:${gamePort}`);
      toast('👁 Vào game → chọn SPECTATE để xem trận', 'info');
    } else {
      toast(`Spectate: ${ip}:${gamePort} → chọn Spectate trong CS 1.6`, 'info');
    }
  }
}

/* ── SPECTATOR CHAT ──────────────────────────────────────────────────────── */
let _specChatRoomId = null;
let _specChatType   = 'room'; // 'room' | 'match'
let _specChatLastTs = 0;
let _specChatTimer  = null;

function openSpecChat(roomId, roomType, roomName) {
  _specChatRoomId = roomId;
  _specChatType   = roomType;
  _specChatLastTs = 0;
  document.getElementById('spec-chat-title').textContent = `💬 Chat: ${roomName}`;
  document.getElementById('spec-chat-messages').innerHTML = '';
  document.getElementById('spec-chat-overlay').classList.remove('hidden');
  if (_specChatTimer) clearInterval(_specChatTimer);
  _specChatTimer = setInterval(pollSpecChat, 2000);
  pollSpecChat();
}

function closeSpecChat() {
  document.getElementById('spec-chat-overlay').classList.add('hidden');
  if (_specChatTimer) { clearInterval(_specChatTimer); _specChatTimer = null; }
  _specChatRoomId = null;
}

async function pollSpecChat() {
  if (!_specChatRoomId) return;
  const ep = _specChatType === 'match'
    ? `/api/tournament-matches/${_specChatRoomId}/spec-chat`
    : `/api/rooms/${_specChatRoomId}/spec-chat`;
  try {
    const r = await api('GET', ep + (_specChatLastTs ? `?since=${_specChatLastTs}` : ''));
    if (r.status === 200 && r.data.length) {
      const box = document.getElementById('spec-chat-messages');
      r.data.forEach(msg => {
        _specChatLastTs = Math.max(_specChatLastTs, msg.ts + 1);
        const div = document.createElement('div');
        div.className = `spec-msg${msg.isAdminBroadcast ? ' spec-msg-admin' : ''}`;
        div.innerHTML = `<span class="spec-sender">${escHtml(msg.senderName)}</span>: ${escHtml(msg.message)}`;
        box.appendChild(div);
      });
      box.scrollTop = box.scrollHeight;
    }
  } catch {}
}

async function sendSpecChat() {
  if (!_specChatRoomId) return;
  if (!currentUser) { toast('Cần đăng nhập để chat', 'error'); return; }
  const input = document.getElementById('spec-chat-input');
  const msg = input.value.trim();
  if (!msg) return;
  input.value = '';
  const ep = _specChatType === 'match'
    ? `/api/tournament-matches/${_specChatRoomId}/spec-chat`
    : `/api/rooms/${_specChatRoomId}/spec-chat`;
  try {
    await api('POST', ep, {
      senderName: currentUser.displayName || currentUser.id,
      message: msg
    });
    pollSpecChat(); // hiện ngay
  } catch { toast('Gửi thất bại', 'error'); }
}

/* ── DONATE ─────────────────────────────────────────────────────────────────── */
async function loadDonateData() {
  try {
    const r = await api('GET', '/api/tournaments');
    const tours = (r.status === 200 && Array.isArray(r.data)) ? r.data : [];
    const sel = document.getElementById('donate-tour-select');
    if (sel) {
      sel.innerHTML = tours.length
        ? tours.map(t => `<option value="${escHtml(t.title)}">${escHtml(t.title)}</option>`).join('')
        : '<option value="">Chưa có giải đấu nào</option>';
    }
  } catch {}
}

function switchDonateTab(tab) {
  document.getElementById('donate-dev').classList.toggle('hidden', tab !== 'dev');
  document.getElementById('donate-tour').classList.toggle('hidden', tab !== 'tour');
  document.getElementById('dtab-dev').classList.toggle('active', tab === 'dev');
  document.getElementById('dtab-tour').classList.toggle('active', tab === 'tour');
}

async function submitDonate(type) {
  const isDev = type === 'developer';
  const amountEl = document.getElementById(isDev ? 'donate-dev-amount' : 'donate-tour-amount');
  const nameEl   = document.getElementById(isDev ? 'donate-dev-name'   : 'donate-tour-name');
  const outId    = isDev ? 'donate-dev-msg-out' : 'donate-tour-msg-out';
  const amount   = parseFloat(amountEl?.value);
  const name     = nameEl?.value?.trim();
  const tour     = isDev ? null : document.getElementById('donate-tour-select')?.value;

  if (!amount || amount < 10000) { showError(outId, 'Số tiền tối thiểu 10,000 VND'); return; }

  try {
    const res = await api('POST', '/api/donations', {
      playerId: currentUser?.id,
      playerName: name || currentUser?.username || 'Anonymous',
      amount,
      message: isDev ? (document.getElementById('donate-dev-msg')?.value || '') : '',
      donationType: type,
      tournamentName: tour || null
    });

    if (res.status !== 200 && res.status !== 201) {
      showError(outId, res.data?.error || res.data?.message || 'Lỗi gửi donate');
      return;
    }

    const donation = res.data;

    // Upload proof file nếu có
    const proofInput = document.getElementById(isDev ? 'donate-dev-proof' : 'donate-tour-proof');
    if (proofInput?.files?.[0] && donation?.id) {
      const fd = new FormData();
      fd.append('proofFile', proofInput.files[0]);
      const base = appConfig.apiUrl || 'http://localhost:7777';
      await fetch(`${base}/api/donations/${donation.id}/upload-proof`, { method: 'POST', body: fd });
    }

    // Hiện QR code
    if (isDev && donation?.qrUrl) {
      const qrEl = document.getElementById('donate-dev-qr');
      if (qrEl) {
        qrEl.innerHTML = `<img src="${donation.qrUrl}" alt="QR" class="qr-image"><p><strong>Nội dung CK: ${donation.vietqrRef}</strong></p>`;
        qrEl.classList.remove('hidden');
      }
    }

    showSuccess(outId, `✅ Cảm ơn bạn! Chuyển khoản nội dung: ${donation?.vietqrRef || 'CS16VN'}`);
  } catch(e) { showError(outId, e.message || 'Lỗi gửi donate'); }
}

/* ── FEEDBACK ────────────────────────────────────────────────────────────────── */
async function submitFeedback() {
  const content = document.getElementById('fb-content').value.trim();
  if (!content) { showError('fb-error', 'Vui lòng nhập nội dung'); return; }
  try {
    const r = await api('POST', '/api/feedback', {
      playerId: currentUser?.id,
      playerName: currentUser?.username || 'Anonymous',
      type: document.getElementById('fb-type').value,
      content
    });
    if (r.status === 200 || r.status === 201) {
      showSuccess('fb-msg', '📨 Cảm ơn! Góp ý của bạn đã được ghi nhận.');
      document.getElementById('fb-content').value = '';
    } else {
      showError('fb-error', r.data?.error || 'Lỗi gửi góp ý');
    }
  } catch(e) { showError('fb-error', e.message); }
}

function switchHelpTab(tab) {
  document.getElementById('help-guide').classList.toggle('hidden', tab !== 'guide');
  document.getElementById('help-feedback').classList.toggle('hidden', tab !== 'feedback');
  document.getElementById('htab-guide').classList.toggle('active', tab === 'guide');
  document.getElementById('htab-feedback').classList.toggle('active', tab === 'feedback');
}

/* ── ABOUT PAGE ──────────────────────────────────────────────────────────────── */
async function loadAboutStats() {
  try {
    const r = await api('GET', '/api/tournaments');
    const tours = (r.status === 200 && Array.isArray(r.data)) ? r.data : [];
    document.getElementById('astat-tours').textContent = tours.length || '–';
    document.getElementById('ainfo-api').textContent = appConfig.apiUrl || '–';
  } catch {}
}

/* ── HELPERS ─────────────────────────────────────────────────────────────────── */
function escHtml(s) {
  return (s || '').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
}
