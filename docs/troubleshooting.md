# Troubleshooting Guide

## Plugin Issues

### Plugin changes not picked up after edit

**Symptom:** You edit `index.ts` but the gateway still runs old code.

**Cause:** OpenClaw uses `jiti` for JIT TypeScript compilation and caches the result.

**Fix:**
```powershell
# Clear jiti cache
Remove-Item -Recurse -Force "$env:USERPROFILE\.openclaw\jiti-cache"
# Restart gateway
openclaw gateway restart
```

### SSRF error when plugin calls Vigil API

**Symptom:** Plugin tool calls fail with "blocked by SSRF guard" or similar network errors.

**Cause:** OpenClaw patches `globalThis.fetch` to block requests to private IPs (`127.0.0.1`, `localhost`).

**Fix:** The plugin already uses `node:http` to bypass this. If you're writing custom code, use the same pattern:
```typescript
const http = await import("node:http");
// Use http.request() instead of fetch()
```

### Plugin tools return "Failed to create task" or similar

**Symptom:** Tools execute but return error messages.

**Checks:**
1. Is Vigil server running? `curl http://127.0.0.1:5263/api/health/status`
2. Is API key required? Check `appsettings.json` → `Api:Key`
3. Check gateway logs: `openclaw gateway logs`

**Fallback:** Use PowerShell wrapper scripts in `{workspace}/atlas-tools/`:
```powershell
& .\atlas-tools\atlas_create_task.ps1 -Title "Test" -Priority "Medium"
```

### Plugin doesn't detect Telegram config

**Symptom:** Credential request notifications aren't sent.

**Checks:**
1. Verify `~/.openclaw/openclaw.json` has `channels.telegram.botToken` and `channels.telegram.allowFrom`
2. Check gateway startup logs for `"telegram configured = true"`
3. The `allowFrom` array must contain the chat ID (not username)

## Notification Issues

### Approve/Deny buttons don't work from phone

**Symptom:** Clicking approve/deny in Telegram opens a page that can't connect.

**Cause:** The notification URL points to `127.0.0.1:5263` which isn't reachable from your phone.

**Fix:** 
1. The plugin auto-detects your external IP via `api.ipify.org`. Check that it resolved correctly in gateway logs: `"publicUrl = http://x.x.x.x:5263"`
2. Ensure port 5263 is accessible from your phone's network
3. For remote access, set up port forwarding or use a reverse proxy/tunnel (see `docs/HTTPS.md`)

### Telegram notification shows "Request Not Found"

**Symptom:** Clicking approve returns "Request Not Found" page.

**Cause:** The permission request may have been deleted or the request ID is invalid.

**Fix:** Check the security permissions: `curl http://127.0.0.1:5263/api/security/permissions`

### Duplicate Telegram notifications

**Symptom:** Two notifications are sent for one credential request.

**Cause:** Both the plugin and server were sending notifications.

**Fix:** This was resolved — only the server sends notifications now (via `POST /api/notifications/credential-request`). The plugin calls this endpoint instead of sending directly. If you see duplicates, ensure you're on the latest plugin version.

## UI Issues

### MudBlazor CSS not loading / unstyled pages

**Symptom:** Pages render without styling, missing icons, broken layout.

**Checks:**
1. Verify `app.UseStaticFiles()` is in `Program.cs`
2. Check that `_Imports.razor` includes `@using MudBlazor`
3. In development, try: `dotnet clean && dotnet build`
4. Clear browser cache

### Blazor circuit disconnected

**Symptom:** UI freezes, shows "Reconnecting..." banner.

**Cause:** SignalR connection lost (common if server restarts or network blip).

**Fix:** Refresh the page. For persistent issues, check that Vigil server is running.

## Database Issues

### "Invalid column name" errors

**Symptom:** API returns 500 errors with SQL column not found.

**Cause:** Database schema is out of date.

**Fix:** Run the latest migration scripts. Check `src/Atlas.Infrastructure/` for SQL files or schema changes.

### Connection string issues

**Symptom:** Server fails to start with connection errors.

**Check:** `appsettings.json` → `ConnectionStrings:DefaultConnection`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=AtlasControlPanel;Trusted_Connection=true;TrustServerCertificate=true;"
  }
}
```

## Authentication Issues

### "Invalid or missing API key" on all API calls

**Symptom:** Plugin or curl calls get 401.

**Cause:** API key is configured in `appsettings.json` but not provided in requests.

**Fix:** Either:
1. Add `X-Api-Key` header to requests
2. Remove the `Api:Key` setting from `appsettings.json` for dev mode (allows all)
3. Use cookie auth (login via browser first)

### Login page loops

**Symptom:** Logging in redirects back to login.

**Check:** `appsettings.json` → `Auth:Username` and `Auth:Password` are set correctly.

## Cost Analytics Issues

### Analytics showing $0 cost

**Symptom:** Dashboard shows no cost data.

**Cause:** Token usage isn't being logged.

**Checks:**
1. Plugin must be running (check `model.usage` events in activity log)
2. The plugin pushes cost data to both `/api/monitoring/cost` and `/api/analytics/usage`
3. Verify: `curl http://127.0.0.1:5263/api/analytics/totals?period=day`

### HMAC secret not persisting across restarts

**Symptom:** Warning in logs: "Generated new HMAC approval secret."

**Cause:** `appsettings.json` wasn't writable or `Security:ApprovalHmacSecret` is missing.

**Fix:** Add to `appsettings.json`:
```json
{
  "Security": {
    "ApprovalHmacSecret": "your-base64-secret-from-logs"
  }
}
```

## Gateway Issues

### Gateway won't start after OpenClaw update

**Symptom:** Plugin fails to load with TypeScript/import errors.

**Fix:**
```powershell
# Clear caches
Remove-Item -Recurse -Force "$env:USERPROFILE\.openclaw\jiti-cache"
# Reinstall plugin dependencies if needed
cd "$env:USERPROFILE\.openclaw\extensions\atlas-control-panel"
npm install
# Restart
openclaw gateway restart
```

### Health Guardian reports "Unhealthy" but gateway is running

**Symptom:** Dashboard shows gateway as unhealthy despite it working.

**Cause:** The health check probe might be failing due to network/timing issues.

**Check:** `curl http://127.0.0.1:5263/api/health/status` — look at the response times and status.

## Common Error Patterns

| Error | Likely Cause | Quick Fix |
|-------|-------------|-----------|
| `ECONNREFUSED 127.0.0.1:5263` | Vigil server not running | Start Vigil: `dotnet run` |
| `401 Unauthorized` | API key mismatch | Check `Api:Key` in appsettings |
| `HMAC token invalid` | Secret changed between restarts | Persist secret to appsettings |
| `Approvals Locked` | Too many rapid approvals | Unlock via UI or `POST /api/notifications/approval-lockout/unlock` |
| `fetch blocked by SSRF` | Using `fetch()` in plugin | Switch to `node:http` |
| `jiti cache stale` | Plugin edits not reflected | Delete `~/.openclaw/jiti-cache/` |
