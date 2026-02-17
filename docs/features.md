# Feature Catalog

## Dashboard & Monitoring

| Feature | Description | Page/Controller | Tier |
|---------|-------------|-----------------|------|
| **System Dashboard** | Real-time overview of gateway health, active sessions, memory usage, uptime | `Dashboard.razor` / `MonitoringController` | Free |
| **System Status** | Gateway health state with last-updated timestamps | `MonitoringController` (GET/PUT `/api/monitoring/status`) | Free |
| **Health Checks** | Periodic health checks for OpenClaw Gateway and Vigil Server | `HealthController` (GET `/api/health/status`, `/checks`, `/events`) | Free |
| **Uptime Stats** | Uptime percentage and average response times over configurable days | `HealthController` (GET `/api/health/uptime`) | Free |
| **Manual Gateway Restart** | Trigger OpenClaw Gateway restart from the UI | `HealthController` (POST `/api/health/restart/{serviceName}`) | Free |
| **Health Guardian** | Background service that monitors gateway health automatically | `HealthGuardianService` (HostedService) | Free |
| **Real-time Notifications** | SignalR push for live activity updates | `NotificationHub`, `ActivityHub` | Free |

## Activity Logging

| Feature | Description | Page/Controller | Tier |
|---------|-------------|-----------------|------|
| **Activity Log** | Chronological feed of all agent actions with categories | `ActivityLogPage.razor` / `ActivityController` | Free |
| **Detailed Activity View** | Hierarchical activity tree with parent/child relationships | `ActivityController` (GET `/api/activity/detailed`) | Free |
| **Auto-logging** | Plugin automatically logs model calls, messages, session state changes | Plugin `onDiagnosticEvent` | Free |
| **Custom Activity Entries** | AI agent can log custom activities via tool | `atlas_log_activity` tool | Free |
| **Activity Categories** | Development, BugFix, System, Communication, Research, Security | Enum-based filtering | Free |

## Task Management

| Feature | Description | Page/Controller | Tier |
|---------|-------------|-----------------|------|
| **Task Board** | Kanban-style board (Backlog → InProgress → Review → Done) | `TaskBoard.razor` / `TasksController` | Free |
| **Task CRUD** | Create, read, update, delete tasks with priority levels | `TasksController` (full REST) | Free |
| **Task Scheduling** | Schedule tasks with recurrence (None, Daily, Weekly, Monthly, Yearly) | `TaskItem.ScheduledAt`, `RecurrenceType` | Pro |
| **Task Cost Tracking** | Attach cost info to tasks | `TaskItem.Cost` (TaskCost value object) | Pro |
| **AI Task Creation** | Agent creates/updates tasks via tools | `atlas_create_task`, `atlas_update_task` | Free |

## Cost Analytics

| Feature | Description | Page/Controller | Tier |
|---------|-------------|-----------------|------|
| **Token Usage Tracking** | Per-request logging: provider, model, tokens, cost, duration | `AnalyticsController` (POST `/api/analytics/usage`) | Free |
| **Daily Cost Charts** | Cost over time visualization | `AnalyticsController` (GET `/api/analytics/cost/daily`) | Free |
| **Cost by Model** | Breakdown of spending per AI model | GET `/api/analytics/cost/by-model` | Free |
| **Cost by Session** | Which sessions cost the most | GET `/api/analytics/cost/by-session` | Pro |
| **Cost by Project** | Project-based cost attribution | GET `/api/analytics/cost/by-project` | Pro |
| **Project Tagging** | Tag sessions/work with project names for attribution | `atlas_set_project` tool | Pro |
| **ROI Dashboard** | Operation breakdown and savings analysis | GET `/api/analytics/roi` | Pro |
| **Efficiency Recommendations** | AI-generated cost optimization suggestions | GET `/api/analytics/efficiency` | Pro |
| **Recommendation Actions** | Apply or reject optimization recommendations with tracking | POST `/api/analytics/recommendation/action` | Pro |
| **Savings Tracking** | Track actual savings since implementing recommendations | GET `/api/analytics/recommendation/actions` | Pro |
| **Analytics Dashboard** | Comprehensive view: daily costs, model breakdown, top requests, recommendations | GET `/api/analytics/dashboard` | Free |
| **Quick Totals** | Period summaries (day/week/month/year) | GET `/api/analytics/totals` | Free |
| **Flexible Summary** | Group usage by model, day, or session | GET `/api/analytics/summary` | Free |
| **Daily Cost Monitoring** | Increment and query daily/monthly cost totals | `MonitoringController` (`/api/monitoring/cost/*`) | Free |

## Security & Credentials

| Feature | Description | Page/Controller | Tier |
|---------|-------------|-----------------|------|
| **Credential Vault** | AES-256 encrypted storage for secrets | `Security.razor` / `CredentialController` | Free |
| **Vault Modes** | Per-credential locked/unlocked mode (locked requires approval) | `SecurityController` (PUT/GET `.../vault-mode`) | Free |
| **Credential Request Flow** | Bot requests → owner approves via Telegram → env var loaded | `atlas_request_credential` → `atlas_get_credential` | Free |
| **HMAC Approval Tokens** | Cryptographic tokens for approve/deny URLs | `ApprovalTokenService` | Free |
| **Anomaly Detection** | Flags approvals <5s after creation as suspicious | `NotificationController.HandleCredentialDecision` | Free |
| **Approval Lockout** | Auto-locks approvals after 3+ suspicious attempts per hour | `ApprovalLockoutService` | Free |
| **Credential Access Logs** | Full audit trail of all credential access attempts | `SecurityController` (GET `/api/access-logs`) | Free |
| **Permission Requests** | Manage pending/approved/denied permission requests | `SecurityController` (GET/POST/PUT `/api/security/permissions`) | Free |
| **Security Audits** | Severity-tagged audit log of all security events | `SecurityController` (GET/POST `/api/security/audits`) | Free |
| **Auto-expiring Env Vars** | Credentials loaded as env vars with automatic cleanup | Plugin `atlas_get_credential` | Free |

## Chat & Communication

| Feature | Description | Page/Controller | Tier |
|---------|-------------|-----------------|------|
| **Chat History** | View agent conversation history from OpenClaw transcripts | `Chat.razor` / `/api/chat/history` | Free |
| **Chat Relay** | Send messages from Control Panel UI to Telegram | `/api/chat/relay` | Free |
| **Telegram Notifications** | Push notifications for credential requests with inline buttons | `TelegramNotificationService` / `NotificationController` | Free |
| **Telegram Callbacks** | Handle approve/deny button presses from Telegram | `SecurityController` (POST `/api/security/credentials/callback`) | Free |

## Device Pairing

| Feature | Description | Page/Controller | Tier |
|---------|-------------|-----------------|------|
| **QR Code Pairing** | Generate pairing codes for mobile app connection | `PairingController` (POST `/api/pairing/generate`) | Free |
| **Device Management** | List and disconnect paired devices | GET `/api/pairing/devices`, DELETE `.../devices/{id}` | Free |
| **API Key Auth** | Paired devices get persistent API keys | `PairingController.Complete` | Free |

## Setup & Configuration

| Feature | Description | Page/Controller | Tier |
|---------|-------------|-----------------|------|
| **Guided Setup Wizard** | Step-by-step OpenClaw configuration | `Setup.razor` / `SetupController` | Free |
| **OpenClaw Detection** | Auto-detect installation, version, patch status | GET `/api/setup/openclaw-status` | Free |
| **Config Generation** | Generate openclaw.json, AGENTS.md, SOUL.md, USER.md, HEARTBEAT.md | POST `/api/setup/apply-config` | Free |
| **Timezone Detection** | Auto-detect system timezone | GET `/api/setup/detect-timezone` | Free |
| **Patch Application** | Apply splitToolExecuteArgs patch | POST `/api/setup/apply-patch` | Free |

## Licensing

| Feature | Description | Page/Controller | Tier |
|---------|-------------|-----------------|------|
| **License Validation** | JWT-based license key validation with Ed25519 signatures | `LicenseController` / `LicenseValidator` | Free |
| **License Activation** | Activate/deactivate license keys | POST/DELETE `/api/license` | Free |
| **Tier Gating** | Features gated by Free/Pro/Team tier | `LicenseValidator.GetCurrentLicense` | Free |
| **Module Access** | License specifies enabled modules | `LicenseStatusResponse.Modules` | Free |

## Data Export

| Feature | Description | Page/Controller | Tier |
|---------|-------------|-----------------|------|
| **Full Export** | Export all tasks, activities, permissions, audits, status as JSON | GET `/api/export` | Free |

## Authentication

| Feature | Description | Tier |
|---------|-------------|------|
| **Cookie Auth** | Username/password login with 30-day sliding expiration | Free |
| **API Key Auth** | `X-Api-Key` header for programmatic access | Free |
| **Dual Auth** | Cookie OR API key accepted for API routes | Free |
| **Auth Exclusions** | `/api/auth/*`, `/api/chat/*`, and notification callback URLs are unauthenticated | Free |
