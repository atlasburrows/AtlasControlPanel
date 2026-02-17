# API Reference

Base URL: `http://localhost:5263`

All API routes (except `/api/auth/*`, `/api/chat/*`, and `/api/notifications/credential/*`) require either:
- **Cookie auth** (login via `/api/auth/login`)
- **API key** header: `X-Api-Key: {key}` (configured in `appsettings.json` → `Api:Key`)
- If no API key is configured, all requests are allowed (dev mode)

---

## Authentication

### `POST /api/auth/login`
Login and receive auth cookie. **No auth required.**

| Field | Type | Description |
|-------|------|-------------|
| `username` | string | Username |
| `password` | string | Password |

**Response:** `200 OK` (sets cookie) or `401 Unauthorized`

### `POST /api/auth/logout`
Clear auth cookie. **No auth required.**

---

## Activity

### `GET /api/activity`
List activity logs.

| Param | Type | Default | Description |
|-------|------|---------|-------------|
| `take` | int | 50 | Max entries to return |

**Response:** `ActivityLog[]`

### `GET /api/activity/detailed`
Hierarchical activity tree (parent entries with sub-entries populated).

| Param | Type | Default | Description |
|-------|------|---------|-------------|
| `take` | int | 50 | Max entries |

### `GET /api/activity/{id}`
Get single activity by GUID.

### `POST /api/activity`
Create activity log entry.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `action` | string | ✅ | Action name |
| `description` | string | ✅ | Description |
| `category` | int | | Enum: 0=Development, 1=BugFix, 2=System, 3=Communication, 4=Research, 5=Security |
| `details` | string | | JSON details |
| `parentId` | guid | | Parent activity ID |
| `relatedTaskId` | guid | | Associated task |

---

## Analytics

### `POST /api/analytics/usage`
Log a token usage record.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `provider` | string | ✅ | e.g., "anthropic" |
| `model` | string | ✅ | e.g., "claude-opus-4-6" |
| `inputTokens` | int | ✅ | Input token count |
| `outputTokens` | int | ✅ | Output token count |
| `costUsd` | decimal | ✅ | Must be > 0 |
| `durationMs` | int | | Request duration |
| `sessionKey` | string | | Session identifier |
| `taskCategory` | string | | Category tag |
| `project` | string | | Project name for cost attribution |
| `contextPercent` | int | | Context window usage % |
| `timestamp` | datetime | | Defaults to UTC now |

### `GET /api/analytics/summary`
Flexible usage summary.

| Param | Type | Required | Description |
|-------|------|----------|-------------|
| `from` | datetime | ✅ | Start date |
| `to` | datetime | ✅ | End date (must be after `from`) |
| `groupBy` | string | | `day` (default), `model`, or `session` |

### `GET /api/analytics/cost/daily`
Daily cost data for charts.

| Param | Type | Default | Description |
|-------|------|---------|-------------|
| `days` | int | 30 | Lookback period |

### `GET /api/analytics/cost/by-model`
Cost breakdown by AI model.

| Param | Type | Default |
|-------|------|---------|
| `days` | int | 30 |

### `GET /api/analytics/cost/by-session`
Cost breakdown by session key.

| Param | Type | Default |
|-------|------|---------|
| `days` | int | 30 |

### `GET /api/analytics/cost/by-project`
Cost breakdown by project.

| Param | Type | Default | Description |
|-------|------|---------|-------------|
| `days` | int | 30 | Lookback period |
| `inactiveDays` | int | 7 | Days to consider a project inactive |

### `GET /api/analytics/roi`
ROI dashboard: operation breakdown and savings.

| Param | Type | Default |
|-------|------|---------|
| `days` | int | 30 |

### `GET /api/analytics/efficiency`
Cost optimization recommendations.

| Param | Type | Default |
|-------|------|---------|
| `days` | int | 30 |

**Response:** List of recommendations with title, action items, estimated savings.

### `GET /api/analytics/totals`
Quick summary totals.

| Param | Type | Default | Values |
|-------|------|---------|--------|
| `period` | string | month | `day`, `week`, `month`, `year` |

**Response:**
```json
{
  "period": "month",
  "fromDate": "2026-01-17",
  "toDate": "2026-02-17",
  "totalCost": 45.23,
  "averageDailyCost": 1.51,
  "totalRequests": 892,
  "totalTokens": 2340000
}
```

### `GET /api/analytics/dashboard`
Comprehensive analytics dashboard (combines all above).

| Param | Type | Default |
|-------|------|---------|
| `days` | int | 30 |

**Response:** `CostAnalyticsSummary` with daily costs, model breakdown, session breakdown, project breakdown, top sessions, top requests, and recommendations.

### `POST /api/analytics/recommendation/action`
Apply or reject a recommendation.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `title` | string | ✅ | Recommendation title |
| `action` | string | ✅ | `applied` or `rejected` |
| `details` | string | | Additional notes |
| `estimatedMonthlySavings` | decimal | | Expected monthly savings |
| `previousCostPerDay` | decimal | | Cost before change |
| `newCostPerDay` | decimal | | Cost after change |

### `GET /api/analytics/recommendation/actions`
Get recommendation action history with computed savings.

---

## Credentials

### `GET /api/security/credentials`
List all credentials (via both SecurityController and CredentialController).

### `GET /api/security/credentials/{id}`
Get credential by GUID.

### `GET /api/security/credentials/by-name/{name}`
Get credential by name.

### `POST /api/security/credentials`
Create a new credential.

| Field | Type | Description |
|-------|------|-------------|
| `name` | string | Credential name |
| `category` | string | Grouping category |
| `username` | string | Associated username |
| `description` | string | Description |
| `storageKey` | string | Encrypted value |

### `DELETE /api/security/credentials/{id}`
Delete a credential.

### `POST /api/security/credentials/{id}/decrypt`
Decrypt a credential by ID. **Requires approved permission request.**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `permissionRequestId` | guid | ✅ | Approved permission request ID |

**Response:** `{ "value": "decrypted-secret" }`

### `POST /api/security/credentials/by-name/{name}/decrypt`
Decrypt a credential by name. **Requires approved permission request.**

Same body as above. **Response:** `{ "value": "...", "credentialId": "...", "name": "..." }`

### `POST /api/security/credentials/{id}/access`
Record an access event (increments counter).

### `PUT /api/security/credentials/{id}/vault-mode`
Set vault mode.

| Field | Type | Values |
|-------|------|--------|
| `vaultMode` | string | `locked`, `unlocked` |

### `GET /api/security/credentials/{id}/vault-mode`
Get current vault mode.

### `GET /api/security/credentials/{id}/access-logs`
Get access logs for a specific credential.

### `GET /api/security/access-logs`
Get all credential access logs.

| Param | Type | Default |
|-------|------|---------|
| `take` | int | 100 |

---

## Security

### `POST /api/security/credentials/request`
Request credential access (creates permission request, may auto-approve for unlocked vaults).

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `credentialName` | string | ✅ | Credential name |
| `reason` | string | ✅ | Access reason |
| `durationMinutes` | int | | Duration (default: 30) |

### `GET /api/security/permissions`
List all permission requests.

### `GET /api/security/permissions/pending`
List pending permission requests only.

### `POST /api/security/permissions`
Create a permission request.

### `PUT /api/security/permissions/{id}`
Update permission status. **Cannot approve CredentialAccess type** (returns 403 — must use HMAC endpoint).

| Field | Type | Description |
|-------|------|-------------|
| `status` | enum | `Pending`, `Approved`, `Denied` |
| `resolvedBy` | string | Who resolved it |

### `POST /api/security/credentials/callback`
Telegram callback handler for inline buttons.

| Field | Type | Description |
|-------|------|-------------|
| `callbackQueryId` | string | Telegram callback ID |
| `data` | string | `cred_approve:{id}` or `cred_deny:{id}` |
| `messageId` | string | For editing the notification |
| `fromUsername` | string | Who pressed the button |
| `fromId` | string | Telegram user ID |

### `GET /api/security/audits`
List security audit entries.

| Param | Type | Default |
|-------|------|---------|
| `take` | int | 100 |

### `POST /api/security/audits`
Create a security audit entry.

---

## Notifications

### `POST /api/notifications/credential-request`
Generate HMAC token and send Telegram push notification. **No auth required** (notification callback route).

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `requestId` | guid | ✅ | Permission request ID |
| `credentialName` | string | ✅ | Credential name |
| `reason` | string | ✅ | Access reason |
| `durationMinutes` | int | ✅ | Requested duration |

### `GET /api/notifications/credential/{requestId}/approve?token={hmac}`
Approve a credential request. **No cookie/API auth** — protected by HMAC token. Returns HTML page.

### `GET /api/notifications/credential/{requestId}/deny?token={hmac}`
Deny a credential request. Same auth model. Returns HTML page.

### `POST /api/notifications/approval-lockout/unlock`
Manually clear the approval lockout.

### `GET /api/notifications/approval-lockout/status`
Get lockout status.

**Response:** `{ "suspiciousCount": 0, "lockoutUntil": null, "isLockedOut": false }`

---

## Tasks

### `GET /api/tasks`
List all tasks.

### `GET /api/tasks/{id}`
Get task by GUID.

### `POST /api/tasks`
Create a task.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `title` | string | ✅ | Task title |
| `description` | string | | Description |
| `priority` | enum | | `Low`, `Medium`, `High`, `Critical` |
| `status` | enum | | `ToDo`, `Backlog`, `InProgress`, `Review`, `Done` |
| `assignedTo` | string | | Assignee |
| `scheduledAt` | datetime | | Scheduled execution time |
| `recurrenceType` | enum | | `None`, `Daily`, `Weekly`, `Monthly`, `Yearly` |
| `recurrenceInterval` | int | | Interval multiplier |
| `recurrenceDays` | string | | Days spec for weekly |

### `PUT /api/tasks/{id}`
Full update of a task.

### `PUT /api/tasks/{id}/status`
Update task status only. Body is the status enum value as JSON string.

### `DELETE /api/tasks/{id}`
Delete a task.

---

## Health

### `GET /api/health/status`
Service health check for OpenClaw Gateway and Vigil Server.

### `GET /api/health/checks`
Recent health check records.

| Param | Type | Default |
|-------|------|---------|
| `take` | int | 50 |

### `GET /api/health/events`
Health events (restarts, outages).

| Param | Type | Default |
|-------|------|---------|
| `take` | int | 100 |

### `GET /api/health/uptime`
Uptime statistics.

| Param | Type | Default | Description |
|-------|------|---------|-------------|
| `service` | string | OpenClaw Gateway | Service name |
| `days` | int | 7 | Lookback period |

### `POST /api/health/restart/{serviceName}`
Manually restart a service. Only supports `OpenClaw Gateway`.

---

## Monitoring

### `GET /api/monitoring/status`
Get system status (gateway health, sessions, memory, uptime).

### `PUT /api/monitoring/status`
Upsert system status.

### `POST /api/monitoring/cost`
Increment daily cost tracker.

| Field | Type | Required |
|-------|------|----------|
| `costUsd` | decimal | ✅ (must be > 0) |

### `GET /api/monitoring/cost/daily?date={date}`
Get daily cost summary.

### `GET /api/monitoring/cost/monthly?year={year}&month={month}`
Get monthly cost summary.

---

## Licensing

### `GET /api/license`
Get current license status.

**Response:**
```json
{
  "isValid": true,
  "tier": "pro",
  "email": "user@example.com",
  "modules": ["analytics", "security", "scheduling"],
  "issuedAt": "2026-01-01T00:00:00Z",
  "expiresAt": "2027-01-01T00:00:00Z",
  "licenseId": "...",
  "daysUntilExpiry": 318
}
```

### `POST /api/license`
Activate a license key.

| Field | Type | Required |
|-------|------|----------|
| `licenseKey` | string | ✅ |

### `DELETE /api/license`
Clear license (revert to free tier).

---

## Device Pairing

### `POST /api/pairing/generate`
Generate a pairing code and QR data (valid 5 minutes).

**Response:**
```json
{
  "id": "guid",
  "code": "123456",
  "token": "base64-token",
  "qrData": "atlas://http://server:5263/pair?token=...",
  "serverUrl": "http://server:5263",
  "expiresAt": "...",
  "expiresInSeconds": 300
}
```

### `POST /api/pairing/complete`
Complete pairing — exchange token for persistent API key.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `token` | string | ✅ | Token from pairing code |
| `deviceName` | string | | Device name |
| `deviceType` | string | | `mobile`, `desktop`, `browser` |
| `platform` | string | | `ios`, `android`, `windows`, `macos` |

**Response:** `{ "id": "guid", "apiKey": "atlas_dk_...", "name": "...", "message": "..." }`

### `GET /api/pairing/devices`
List all paired devices.

### `DELETE /api/pairing/devices/{id}`
Disconnect a paired device.

---

## Setup

### `GET /api/setup/openclaw-status`
Detect OpenClaw installation status.

### `POST /api/setup/install-openclaw`
Install OpenClaw via npm.

### `POST /api/setup/apply-patch`
Apply the splitToolExecuteArgs patch.

### `POST /api/setup/apply-config`
Generate and write OpenClaw configuration files.

| Field | Type | Description |
|-------|------|-------------|
| `displayName` | string | User's display name |
| `aiName` | string | AI agent name (default: "Atlas") |
| `timezone` | string | Timezone ID |
| `aiProvider` | string | `anthropic`, `openai`, `google` |
| `aiProviderKey` | string | API key |
| `channel` | string | `telegram`, `discord`, `signal`, `whatsapp` |
| `channelToken` | string | Channel bot token |

**Generates:** `openclaw.json`, `AGENTS.md`, `SOUL.md`, `USER.md`, `HEARTBEAT.md`

### `GET /api/setup/detect-timezone`
Detect system timezone.

---

## Export

### `GET /api/export`
Export all data (tasks, activities, permissions, audits, status) as JSON.

---

## Inline Endpoints (Program.cs)

### `GET /api/health` *(requires auth)*
Comprehensive health check combining `openclaw health --json` and `openclaw status --json`.

### `GET /api/chat/history`
Read chat history from OpenClaw session transcripts. Filters out heartbeats, compaction summaries, system messages.

| Param | Type | Default |
|-------|------|---------|
| `limit` | int | 100 |

### `POST /api/chat/relay`
Relay a user+assistant message pair to Telegram.

| Field | Type | Description |
|-------|------|-------------|
| `userMessage` | string | User's message (sent with 📱 prefix) |
| `assistantMessage` | string | Assistant's reply (sent with 🌐 prefix) |

### `GET /api/tokens`
Token usage and cost from `openclaw status --json`. Estimates cost using Claude Opus 4 pricing ($15/MTok input, $75/MTok output).
