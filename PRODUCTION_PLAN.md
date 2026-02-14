# Atlas Control Panel — Production Plan & Architecture Overview

**Last Updated:** 2026-02-12
**Status:** Pre-release (development)
**Goal:** Self-hosted control panel for any OpenClaw bot — activity logging, task management, credential approval, and monitoring. Privacy-first, foolproof (no bot cooperation required).

---

## Architecture

```
┌─────────────────────────────────────────────────────┐
│                   User (Browser/MAUI)               │
│              Desktop / Mobile Web / App              │
└──────────────────────┬──────────────────────────────┘
                       │ HTTPS
┌──────────────────────▼──────────────────────────────┐
│              Atlas Web App (Blazor)                  │
│              Port 5263 (current)                     │
│                                                      │
│  Pages: Dashboard, Tasks, Activity, Security,        │
│         Monitoring, Chat, Settings, Login             │
│                                                      │
│  Auth: Cookie-based (username/password)              │
│  API:  Internal REST endpoints (merged)              │
│  DB:   Dapper + Stored Procedures                    │
└──────────────────────┬──────────────────────────────┘
                       │ SQL
┌──────────────────────▼──────────────────────────────┐
│              Database                                │
│  Current: SQL Server (AtlasControlPanel)             │
│  Target:  SQL Server OR SQLite (user choice)         │
│                                                      │
│  Tables (9): Tasks, ActivityLogs, PermissionRequests,│
│    SecurityAudits, SecureCredentials, SystemStatus,   │
│    ChatMessages, CostSummary, DailyCosts             │
│                                                      │
│  Stored Procedures (32): CRUD for all entities       │
└─────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────┐
│              OpenClaw Gateway                        │
│                                                      │
│  ┌─────────────────────────────────────────────┐    │
│  │  Atlas Plugin (openclaw-plugin-atlas)        │    │
│  │                                              │    │
│  │  Auto-Capture (no bot cooperation needed):   │    │
│  │  • model.usage → cost, tokens, context %     │    │
│  │  • message.processed → channel, outcome      │    │
│  │  • session.state → Online/Working/Waiting    │    │
│  │  • session.stuck → stuck detection           │    │
│  │  • webhook.error → error logging             │    │
│  │  • run.attempt → retry tracking              │    │
│  │                                              │    │
│  │  Agent Tools (bot uses intentionally):       │    │
│  │  • atlas_create_task                         │    │
│  │  • atlas_update_task                         │    │
│  │  • atlas_log_activity                        │    │
│  │  • atlas_request_credential                  │    │
│  │                                              │    │
│  │  Status Push: every 30s + on state change    │    │
│  └──────────────────┬──────────────────────────┘    │
└─────────────────────┼───────────────────────────────┘
                      │ HTTP (localhost)
                      ▼
              Atlas Web App API endpoints
```

---

## Current State (2026-02-12)

### What Works
- [x] Blazor Web UI — all 8 pages functional
- [x] Cookie authentication (login/logout)
- [x] Task board (create, update status, delete)
- [x] Activity logging (manual via agent tools)
- [x] Security — permission requests, approve/deny, credential vault
- [x] Monitoring — system status, health check, cost chart
- [x] Chat — integrated OpenClaw chat via gateway API
- [x] Plugin — loaded, registers tools, captures diagnostic events
- [x] Plugin — real-time status updates (Online/Working/Waiting/Offline)
- [x] Mobile-responsive UI (radial menu, scroll behaviors)
- [x] Dark theme (GitHub-inspired)

### Recently Completed (2026-02-12)
- [x] API merged into Web app — single process on port 5263
- [x] API key middleware — X-Api-Key header on /api/ routes
- [x] CORS locked to localhost only
- [x] Plugin URLs configurable (no more hardcoded ports)
- [x] Dynamic display name (from Auth config)
- [x] setup.sql — complete database setup script (9 tables, 33 stored procs)
- [x] Cost tracking — plugin pushes model.usage costs to DailyCosts table
- [x] Data export — GET /api/export returns full JSON backup
- [x] Credential storage abstraction — ISecretStore with encrypted file implementation
- [x] First-run setup wizard — /setup page with DB check, admin config, API key gen
- [x] README.md and HTTPS.md documentation

### What Doesn't Work Yet
- [ ] MAUI mobile app — not tested (needs Mac build)
- [ ] SQLite support — SQL Server still required

---

## Critical Issues for Production

### 🔴 P0 — Blockers

| # | Issue | Impact | Fix |
|---|-------|--------|-----|
| 1 | **API has no authentication** | Anyone on network can read/write all data | Add API key middleware |
| 2 | **No database setup script** | Users can't install | Generate single setup.sql with all tables + procs |
| 3 | **Two separate processes** (Web + API) | Complex install, two ports | Merge API controllers into Web app |
| 4 | **Hardcoded localhost:5300 in plugin tools** | Agent tools break if API port changes | Read apiUrl from plugin config in tool execute() |
| 5 | **Hardcoded "Mikal" in dashboard** | Not a product if it says someone's name | Pull from auth/config/settings |
| 6 | **No install documentation** | Users can't set up | Write install guide |
| 7 | **SQL Server dependency** | Most users won't have SQL Server | Add SQLite option |

### 🟡 P1 — Important

| # | Issue | Impact | Fix |
|---|-------|--------|-----|
| 8 | No HTTPS | Insecure in production | Document reverse proxy setup (Caddy/nginx) or add Kestrel HTTPS |
| 9 | CORS AllowAll on API | Security risk | Lock to localhost or configured origins |
| 10 | Credential vault is metadata-only | No actual secret storage cross-platform | Abstract storage backend (Windows Cred Manager, Keychain, encrypted file) |
| 11 | No first-run setup wizard | User has to manually edit appsettings.json | Add /setup page on first launch |
| 12 | No data backup/export | Users could lose data | Add export endpoint (JSON dump) |
| 13 | Cost tracking incomplete | Dashboard shows $0.00 | Wire up model.usage cost data to DailyCosts table |

### 🟢 P2 — Nice to Have

| # | Issue | Fix |
|---|-------|-----|
| 14 | No push notifications | Add web push for permission requests |
| 15 | No multi-user support | Single admin only — fine for V1 |
| 16 | No plugin auto-install | User must manually copy files — publish to npm |
| 17 | No theme customization | Locked to dark theme |
| 18 | MAUI app untested | Need Mac build pipeline (GitHub Actions) |

---

## V1 Launch Plan

### Phase 1 — Merge & Simplify (Priority: NOW)

**Goal:** Single process, single port, one install step.

- [ ] **Merge API into Web app** — move API controllers into Atlas.Web, eliminate Atlas.API project entirely. All endpoints under `/api/` served by the same Blazor app on port 5263.
- [ ] **Add API key middleware** — plugin authenticates with a key from `appsettings.json`. Dashboard API calls use cookie auth (already exists). External API calls require `X-Api-Key` header.
- [ ] **Fix hardcoded URLs in plugin** — agent tool `execute()` functions should use `apiUrl` from plugin config, not hardcoded `http://localhost:5300`.
- [ ] **Fix hardcoded username** — read display name from settings/auth config.

### Phase 2 — Database Portability

**Goal:** Works without SQL Server.

- [ ] **Generate setup.sql** — single script that creates database, all tables, all stored procedures. For SQL Server users.
- [ ] **Add SQLite support** — alternative connection using raw SQL (no stored procs). Auto-detect from connection string.
- [ ] **Auto-migration on first run** — detect empty database, create schema automatically.
- [ ] **First-run setup page** — `/setup` shown when no admin account exists. Configure DB, create admin, set API key.

### Phase 3 — Polish & Package

**Goal:** Installable product.

- [ ] **Write install documentation** — README with step-by-step for Windows, Linux, Mac.
- [ ] **Publish plugin to npm** — `npm install @atlas/openclaw-plugin` or via ClawHub.
- [ ] **Docker image** — `docker run` with SQLite default, optional SQL Server.
- [ ] **Wire up cost tracking** — plugin model.usage events → DailyCosts table.
- [ ] **Add data export** — JSON dump of all tables for backup.
- [ ] **HTTPS documentation** — reverse proxy guide.
- [ ] **Remove AllowAll CORS** — lock to configured origins.

### Phase 4 — Mobile & Distribution

**Goal:** App stores and community.

- [ ] **MAUI iOS build** — GitHub Actions with cloud Mac.
- [ ] **MAUI Android build** — test and publish.
- [ ] **ClawHub listing** — publish plugin with description, screenshots.
- [ ] **Community feedback** — beta testers from OpenClaw Discord.

---

## Ideal Installation (V1 Target)

### For the user:

```bash
# 1. Install the control panel
dotnet tool install -g atlas-control-panel

# 2. Run first-time setup (opens browser to setup wizard)
atlas setup

# 3. Install the OpenClaw plugin
openclaw plugin install @atlas/openclaw-plugin

# 4. Configure plugin (auto-detects local install)
# Plugin config added to openclaw.json automatically

# 5. Done — open dashboard
atlas start
# → http://localhost:5263
```

### Setup wizard handles:
- Database choice (SQLite default, SQL Server optional)
- Admin account creation
- API key generation
- OpenClaw plugin configuration

### Or with Docker:

```bash
docker run -d -p 5263:5263 \
  -v atlas-data:/data \
  -e ADMIN_USER=admin \
  -e ADMIN_PASS=changeme \
  atlas/control-panel
```

---

## Repository Structure (Current)

```
AtlasControlPanel/
├── src/
│   ├── Atlas.API/            # REST API (TO BE MERGED INTO WEB)
│   │   ├── Controllers/      # Activity, Tasks, Monitoring, Security, Credentials
│   │   └── Program.cs        # Minimal API host (no auth)
│   ├── Atlas.Application/    # Interfaces, services
│   ├── Atlas.Domain/         # Entities, enums, value objects
│   ├── Atlas.Infrastructure/ # Dapper repositories, DB connection
│   ├── Atlas.MAUI/           # Mobile app (shared UI)
│   ├── Atlas.Shared/         # Blazor components, pages, layouts, CSS, JS
│   │   ├── Layout/           # MainLayout, NavMenu, RadialMenu, BottomNav
│   │   ├── Pages/            # Dashboard, Tasks, Activity, Security, etc.
│   │   └── wwwroot/          # CSS, JS (radial-menu, scroll-hide, chat)
│   └── Atlas.Web/            # Blazor server host
│       ├── Components/       # App.razor, Routes.razor
│       └── Program.cs        # Host config, auth, endpoints
├── PRODUCTION_PLAN.md        # This file
└── AtlasControlPanel.sln
```

### OpenClaw Plugin (separate)

```
~/.openclaw/extensions/atlas-control-panel/
├── openclaw.plugin.json      # Plugin manifest + config schema
├── index.ts                  # Auto-capture service + agent tools
└── package.json              # npm metadata
```

---

## Database Schema

### Tables
| Table | Purpose |
|-------|---------|
| Tasks | Task board items (title, description, priority, status) |
| ActivityLogs | All logged activities (action, description, category, cost, details) |
| PermissionRequests | Security approval workflow (credential access, external actions) |
| SecurityAudits | Audit trail (action, severity, details) |
| SecureCredentials | Credential metadata (name, category, storage key — not the secrets) |
| SystemStatus | Real-time bot status (health, sessions, memory, uptime) |
| ChatMessages | Persisted chat history |
| CostSummary | Aggregated cost data (daily, monthly) |
| DailyCosts | Per-day cost breakdown |

### Stored Procedures (32)
Full CRUD for all entities. Named `sp_{Table}_{Action}`.

---

## Key Design Decisions

1. **Plugin over AGENTS.md** — Logging is enforced at infrastructure level. Bot can't skip it.
2. **Dapper + Stored Procs** — No EF Core. Explicit, fast, debuggable.
3. **Blazor Server** — Rich interactivity without WASM download. Shared code with MAUI.
4. **Cookie auth for UI, API key for plugin** — Simple, appropriate for self-hosted.
5. **Batched event delivery** — Events buffered 5s, silently dropped on failure. Never crashes gateway.
6. **SQLite default** — Zero-config for most users. SQL Server for power users.

---

*This is a living document. Update as decisions are made and milestones are hit.*
