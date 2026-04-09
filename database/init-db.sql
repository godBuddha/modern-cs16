-- ============================================================
-- Modern CS 1.6 Vietnam — Game Schema
-- NOTE: ASP.NET Identity tables (AspNetUsers, AspNetRoles, etc.)
--       are created automatically by EF Core migrations.
--       This file only creates game-logic tables.
-- ============================================================

-- Enable UUID generation
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- ============================================================
-- 1. player_stats — Game statistics per player
-- ============================================================
CREATE TABLE IF NOT EXISTS player_stats (
    player_id       TEXT        PRIMARY KEY,  -- FK → AspNetUsers.Id (managed by EF)
    display_name    TEXT        NOT NULL DEFAULT 'Player',
    kills           INT         NOT NULL DEFAULT 0,
    deaths          INT         NOT NULL DEFAULT 0,
    headshots       INT         NOT NULL DEFAULT 0,
    wins            INT         NOT NULL DEFAULT 0,
    elo_score       FLOAT       NOT NULL DEFAULT 1000.0,
    credits         INT         NOT NULL DEFAULT 0,
    is_banned       BOOLEAN     NOT NULL DEFAULT FALSE,
    ban_reason      TEXT,
    updated_at      TIMESTAMP   NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_player_stats_elo ON player_stats (elo_score DESC);
CREATE INDEX IF NOT EXISTS idx_player_stats_kills ON player_stats (kills DESC);

-- ============================================================
-- 2. kill_logs — Per-kill event log
-- ============================================================
CREATE TABLE IF NOT EXISTS kill_logs (
    id              BIGSERIAL   PRIMARY KEY,
    attacker_id     TEXT        NOT NULL,
    victim_id       TEXT        NOT NULL,
    weapon          VARCHAR(32) NOT NULL DEFAULT 'unknown',
    headshot        BOOLEAN     NOT NULL DEFAULT FALSE,
    map_name        VARCHAR(64) NOT NULL DEFAULT 'unknown',
    created_at      TIMESTAMP   NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_kill_logs_attacker ON kill_logs (attacker_id);
CREATE INDEX IF NOT EXISTS idx_kill_logs_victim   ON kill_logs (victim_id);
CREATE INDEX IF NOT EXISTS idx_kill_logs_created  ON kill_logs (created_at DESC);

-- ============================================================
-- 3. tournaments — Tournament listings
-- ============================================================
CREATE TABLE IF NOT EXISTS tournaments (
    id              UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    title           TEXT        NOT NULL,
    description     TEXT,
    entry_fee       DECIMAL(18,2) NOT NULL DEFAULT 0,
    prize_pool      DECIMAL(18,2) NOT NULL DEFAULT 0,
    status          VARCHAR(20) NOT NULL DEFAULT 'Open',  -- Open/Ongoing/Finished/Cancelled
    max_players     INT         NOT NULL DEFAULT 16,
    start_date      TIMESTAMP,
    created_at      TIMESTAMP   NOT NULL DEFAULT NOW(),
    CONSTRAINT chk_tournament_status CHECK (status IN ('Open','Ongoing','Finished','Cancelled'))
);

CREATE INDEX IF NOT EXISTS idx_tournaments_status     ON tournaments (status);
CREATE INDEX IF NOT EXISTS idx_tournaments_created_at ON tournaments (created_at DESC);

-- ============================================================
-- 4. tournament_registrations — Player registrations
-- ============================================================
CREATE TABLE IF NOT EXISTS tournament_registrations (
    tournament_id   UUID        NOT NULL REFERENCES tournaments(id) ON DELETE CASCADE,
    player_id       TEXT        NOT NULL,
    registered_at   TIMESTAMP   NOT NULL DEFAULT NOW(),
    PRIMARY KEY (tournament_id, player_id)
);

CREATE INDEX IF NOT EXISTS idx_tourney_reg_player ON tournament_registrations (player_id);

-- ============================================================
-- 5. donations — VietQR donation history
-- ============================================================
CREATE TABLE IF NOT EXISTS donations (
    id              SERIAL      PRIMARY KEY,
    player_id       TEXT,       -- nullable for anonymous
    player_name     TEXT        NOT NULL DEFAULT 'Anonymous',
    amount          DECIMAL(18,2) NOT NULL,
    message         TEXT,
    vietqr_ref      TEXT        UNIQUE,
    status          VARCHAR(20) NOT NULL DEFAULT 'Pending',  -- Pending/Confirmed/Rejected
    created_at      TIMESTAMP   NOT NULL DEFAULT NOW(),
    CONSTRAINT chk_donation_status CHECK (status IN ('Pending','Confirmed','Rejected'))
);

CREATE INDEX IF NOT EXISTS idx_donations_player    ON donations (player_id);
CREATE INDEX IF NOT EXISTS idx_donations_created   ON donations (created_at DESC);
CREATE INDEX IF NOT EXISTS idx_donations_status    ON donations (status);

-- ============================================================
-- 6. kyc_submissions — Identity verification
-- ============================================================
CREATE TABLE IF NOT EXISTS kyc_submissions (
    id                  UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    player_id           TEXT        NOT NULL UNIQUE,  -- FK → AspNetUsers.Id
    cccd_image_path     TEXT,
    selfie_video_path   TEXT,
    status              VARCHAR(20) NOT NULL DEFAULT 'Pending',  -- Pending/Approved/Rejected
    reviewer_note       TEXT,
    reviewed_at         TIMESTAMP,
    submitted_at        TIMESTAMP   NOT NULL DEFAULT NOW(),
    CONSTRAINT chk_kyc_status CHECK (status IN ('Pending','Approved','Rejected'))
);

CREATE INDEX IF NOT EXISTS idx_kyc_status ON kyc_submissions (status);

-- ============================================================
-- Seed: Demo tournament (comment out nếu không cần)
-- ============================================================
-- INSERT INTO tournaments (title, description, prize_pool, max_players)
-- VALUES ('Grand Opening Tournament', 'Giải đấu khai mạc server Modern CS 1.6 VN', 500000, 16);
