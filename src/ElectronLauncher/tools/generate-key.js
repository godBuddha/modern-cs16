#!/usr/bin/env node
/**
 * tools/generate-key.js — Tạo file cs16vn.key để nhúng vào CS 1.6 VN Client
 *
 * Cách dùng:
 *   node tools/generate-key.js
 *   → Tạo ra file cs16vn.key, copy vào thư mục chứa hl.exe trong bản phân phối
 *
 * Mỗi version game có thể dùng key khác nhau (đổi VERSION bên dưới).
 */

const crypto = require('crypto');
const fs = require('fs');
const path = require('path');

// ── CONFIG ────────────────────────────────────────────────────────────────────
// Phải khớp với CLIENT_SECRET trong main.js
const SECRET = 'CS16VN_OFFICIAL_KEY_2026';
const VERSION = 'v2.0';
const OUTPUT  = path.join(__dirname, '..', 'cs16vn.key');

// ── Generate ──────────────────────────────────────────────────────────────────
const header = `CS16VN:${VERSION}`;
const hmac   = crypto.createHmac('sha256', SECRET).update(header).digest('hex');
const keyContent = `${header}:${hmac}`;

fs.writeFileSync(OUTPUT, keyContent, 'utf8');

console.log('✅ Generated cs16vn.key:');
console.log(`   File : ${OUTPUT}`);
console.log(`   Value: ${keyContent}`);
console.log('');
console.log('📦 Hướng dẫn:');
console.log('   1. Copy file cs16vn.key vào thư mục gốc của CS 1.6 VN Client (cùng chỗ với hl.exe)');
console.log('   2. Phân phối thư mục đó cho người chơi');
console.log('   3. Launcher sẽ tự verify khi người chơi chọn hl.exe');
