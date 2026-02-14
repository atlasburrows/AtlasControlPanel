# Atlas Control Panel — OpenClaw Plugin

Automatic activity logging, task management, credential approval, and real-time monitoring for [Atlas Control Panel](https://github.com/atlas-control-panel/atlas).

## What It Does

### Auto-Capture (no bot cooperation needed)
- **Model usage** — tokens, cost, context %, duration per call
- **Messages** — channel, outcome, processing time
- **Session state** — online/working/waiting/offline status
- **Errors** — webhook failures, stuck sessions, retries
- **Cost tracking** — pushes per-call cost to daily totals

### Agent Tools (bot uses intentionally)
- `atlas_create_task` — Create tasks on the board
- `atlas_update_task` — Update task status
- `atlas_log_activity` — Log custom activity entries
- `atlas_request_credential` — Request credential access (requires owner approval)

### Real-Time Status
Pushes bot status every 30 seconds:
- **Online** — Waiting for directive
- **Working** — Processing (model name, context %)
- **Waiting** — Rate limited / waiting for token reset
- **Offline** — Gateway stopped

## Install

```bash
# Copy to extensions folder
cp -r atlas-control-panel ~/.openclaw/extensions/

# Or install from npm (when published)
# openclaw plugin install @atlas/openclaw-plugin
```

## Configure

Add to your `openclaw.json`:

```json
{
  "plugins": {
    "entries": {
      "atlas-control-panel": {
        "enabled": true,
        "config": {
          "apiUrl": "http://localhost:5263",
          "apiKey": "your-api-key-here",
          "autoLog": {
            "toolCalls": true,
            "execCommands": true,
            "sessionEvents": true,
            "messageSends": true
          },
          "batchIntervalMs": 5000
        }
      }
    }
  }
}
```

### Config Options

| Option | Default | Description |
|--------|---------|-------------|
| `apiUrl` | `http://localhost:5263` | Atlas Control Panel URL |
| `apiKey` | _(empty)_ | API key for authentication |
| `autoLog.toolCalls` | `true` | Log model/tool usage |
| `autoLog.execCommands` | `true` | Log exec commands |
| `autoLog.sessionEvents` | `true` | Log session state changes |
| `autoLog.messageSends` | `true` | Log processed messages |
| `batchIntervalMs` | `5000` | Event batch flush interval (ms) |

## Requirements

- [OpenClaw](https://github.com/openclaw/openclaw) >= 2026.2.0
- [Atlas Control Panel](https://github.com/atlas-control-panel/atlas) running and accessible

## License

MIT
