# Plugin Reference

The Vigil plugin (`atlas-control-panel`) runs inside the OpenClaw gateway process. It bridges the AI agent with the Vigil server by intercepting diagnostic events and registering tools.

**Location:** `~/.openclaw/extensions/atlas-control-panel/index.ts`

## Configuration

The plugin hardcodes its connection settings (since OpenClaw 2026.2.9+ enforces strict config schema):

```typescript
runtimeApiUrl = "http://127.0.0.1:5263";  // Vigil server
runtimeApiKey = undefined;                  // No API key in dev mode
```

Telegram credentials are auto-detected from:
1. `ctx.config.channels.telegram` (OpenClaw runtime config)
2. Plugin config `telegram.botToken` / `telegram.chatId`
3. Direct read of `~/.openclaw/openclaw.json`

The public URL (for Telegram button URLs) is auto-detected via `https://api.ipify.org` with 5-second timeout, falling back to localhost.

## SSRF Workaround

OpenClaw's gateway patches `globalThis.fetch` with an SSRF guard that blocks requests to private IPs (`127.0.0.1`). The plugin bypasses this by using `node:http` directly:

```typescript
async function httpRequest(method: string, path: string, body?: string): Promise<{ status: number; body: string }> {
  const http = await import("node:http");
  // ... uses http.request() instead of fetch()
}
```

All API calls from the plugin to Vigil use this `httpJson()` helper.

## Registered Tools

### `atlas_create_task`

Create a task in the Vigil task board.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `title` | string | ✅ | Task title |
| `description` | string | | Task description |
| `priority` | enum | | `Low`, `Medium`, `High`, `Critical` (default: `Medium`) |
| `status` | enum | | `Backlog`, `InProgress`, `Review`, `Done` (default: `Backlog`) |

**API:** `POST /api/tasks`

---

### `atlas_update_task`

Update a task's status.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `id` | string | ✅ | Task GUID |
| `status` | enum | ✅ | `Backlog`, `InProgress`, `Review`, `Done` |

**API:** `PUT /api/tasks/{id}/status`

---

### `atlas_log_activity`

Log a custom activity entry.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | string | ✅ | Action name (e.g., "Deployed Feature") |
| `description` | string | ✅ | Activity description |
| `category` | enum | | `Development`, `BugFix`, `System`, `Communication`, `Research`, `Security` (default: `System`) |

**API:** `POST /api/activity`

Category is mapped to numeric enum: Development=0, BugFix=1, System=2, Communication=3, Research=4, Security=5.

---

### `atlas_set_project`

Tag subsequent token usage with a project name for cost attribution.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `project` | string | ✅ | Project name (empty string to clear) |
| `sessionKey` | string | | Tag a specific session instead of global default |

**Behavior:**
- Sets `currentProject` (global) or updates `sessionProjectMap` (per-session)
- All subsequent `model.usage` events will include this project in analytics
- Cron/sub-agent sessions auto-detect projects from label patterns (e.g., "compliance" → "Control Panel Operations")

---

### `atlas_request_credential`

Request access to a credential. Sends push notification to owner.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `credentialName` | string | ✅ | Name of the credential |
| `reason` | string | ✅ | Why access is needed |
| `durationMinutes` | number | | Access duration (default: 30) |

**Flow:**
1. `POST /api/security/credentials/request` — creates PermissionRequest
2. `POST /api/notifications/credential-request` — server generates HMAC token and sends Telegram notification
3. Returns request ID for use with `atlas_get_credential`

---

### `atlas_get_credential`

Retrieve an approved credential as an environment variable.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `credentialName` | string | ✅ | Name of the credential |
| `permissionRequestId` | string | ✅ | Request ID from `atlas_request_credential` |

**Behavior:**
1. Calls `POST /api/security/credentials/by-name/{name}/decrypt` with the permission request ID
2. Server verifies the permission is Approved and not expired
3. Plugin sets `process.env["ATLAS_CRED_{NAME}"]` (uppercase, non-alphanumeric → `_`)
4. Starts auto-expiry timer based on approved duration
5. Returns env var name (never the value)
6. On expiry: deletes env var, logs `CredentialExpired` audit

**The secret value never appears in the agent conversation.**

## Event Interception

The plugin subscribes to OpenClaw diagnostic events via `onDiagnosticEvent()`:

| Event Type | What It Does |
|-----------|-------------|
| `model.usage` | Logs model call details, pushes cost to monitoring + analytics, updates status to "Working" |
| `message.processed` | Logs channel/outcome, updates status to "Online" |
| `session.state` | Tracks running/thinking/waiting/idle states, updates system status |
| `session.stuck` | Logs stuck sessions with queue depth info |
| `webhook.error` | Logs webhook errors with channel/type details |
| `run.attempt` | Logs retry attempts (only attempt > 1) |

### Batching

Activities are batched and flushed periodically:
- Default batch interval: **5 seconds**
- Status heartbeat: every **30 seconds**
- Each flush sends individual POST requests per entry

### Project Auto-Detection

For `model.usage` events, the plugin resolves the project:
1. Check `sessionProjectMap` for the session key
2. Fall back to `currentProject` (global)
3. Auto-detect from session key patterns:
   - `cron:*` → matches against `LABEL_PROJECT_MAP` or defaults to "Cron Jobs & Automation"
   - `subagent:*` → uses `currentProject` or "Sub-Agent Tasks"

## Lifecycle

### Start (`service.start`)
1. Set runtime API URL and key
2. Auto-detect external IP for notification URLs
3. Write PowerShell wrapper scripts to `{workspace}/atlas-tools/`
4. Read Telegram config from OpenClaw
5. Mark status as "Online"
6. Start batch flush timer (5s) and status heartbeat timer (30s)
7. Subscribe to diagnostic events

### Stop (`service.stop`)
1. Unsubscribe from events
2. Clear timers
3. Push "Offline" status
4. Clear batch

### Restart Behavior

The plugin is loaded by OpenClaw's gateway using `jiti` (JIT TypeScript compilation). When the gateway restarts:
- The plugin's `stop()` is called (if graceful)
- On next start, `start()` re-initializes everything
- Active credential env vars are lost (they live in `process.env`)
- The jiti cache at `~/.openclaw/jiti-cache/` may need clearing if the plugin source changes but isn't picked up

## PowerShell Wrapper Scripts

On start, the plugin generates wrapper scripts in `{workspace}/atlas-tools/` as a fallback for when tool dispatch has issues:

- `atlas_create_task.ps1`
- `atlas_update_task.ps1`
- `atlas_log_activity.ps1`
- `atlas_request_credential.ps1`
- `atlas_get_credential.ps1`

These use `curl` directly and can be invoked via `exec` if the native tool dispatch fails.
